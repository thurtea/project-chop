# Homework runner for Chopper: one command per check, printing only the lines the report card needs.
# Run from the repo root in PowerShell:
#   pwsh -ExecutionPolicy Bypass -File Tools\homework.ps1 <task> [argument]
# Tasks:
#   build                 Windows development player (Builds\Dev)          -> "Build Finished, Result: Success", 0 error CS
#   tour <flag>           a Play tour on the dev build, e.g. tour BlockGround -> its RESULT line, shots folder
#   editmode              Unity EditMode unit tests                        -> passed / failed counts
#   unity <Class.Method>  an editor batch check, e.g. unity VoxelChecks.ZoneSeal -> its RESULT line
#   blender               check_assets.py and check_rig.py                 -> both SUMMARY lines
#   player                rebuild Player.prefab (UseChopperPlayer)         -> "Exiting batchmode successfully"
# Logs land in Logs\homework\. Close the Unity editor first: batch jobs need the project to themselves.
param(
    [Parameter(Position = 0)] [string] $Task = "help",
    [Parameter(Position = 1)] [string] $Arg = ""
)

$ErrorActionPreference = "Stop"
$Repo = Split-Path -Parent $PSScriptRoot
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe"
$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
$Game = Join-Path $Repo "Builds\Dev\Chopper.exe"
$Logs = Join-Path $Repo "Logs\homework"
New-Item -ItemType Directory -Force $Logs | Out-Null
$Stamp = Get-Date -Format "MMdd-HHmm"

function Assert-EditorClosed {
    $open = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
        Where-Object { $_.CommandLine -match "project-chop" -and $_.CommandLine -notmatch "-batchmode" }
    if ($open) { throw "Close the Unity editor that has project-chop open, then run this again." }
}

function Invoke-UnityBatch([string[]] $UnityArgs, [string] $Log) {
    Assert-EditorClosed
    $p = Start-Process -FilePath $Unity -ArgumentList ($UnityArgs + @('-logFile', $Log)) -PassThru -Wait
    $errors = (Select-String -Path $Log -Pattern 'error CS' -ErrorAction SilentlyContinue | Measure-Object).Count
    Write-Host "log: $Log"
    Write-Host "error CS lines: $errors"
    return $errors
}

switch ($Task.ToLower()) {
    "build" {
        $log = Join-Path $Logs "build-$Stamp.log"
        Invoke-UnityBatch @('-batchmode', '-quit', '-projectPath', $Repo, '-executeMethod', 'Chopper.Editor.BuildTools.BuildDevPlayer', '-buildOutput', 'Builds/Dev') $log | Out-Null
        Select-String -Path $log -Pattern 'Build Finished' | ForEach-Object { $_.Line }
    }
    "player" {
        $log = Join-Path $Logs "player-$Stamp.log"
        Invoke-UnityBatch @('-batchmode', '-quit', '-projectPath', $Repo, '-executeMethod', 'Chopper.Editor.NetworkScenesBuilder.UseChopperPlayer') $log | Out-Null
        Select-String -Path $log -Pattern 'Exiting batchmode' | ForEach-Object { $_.Line }
    }
    "unity" {
        if (-not $Arg) { throw "Name the check, e.g. unity VoxelChecks.ZoneSeal" }
        $log = Join-Path $Logs "unity-$($Arg -replace '\W','_')-$Stamp.log"
        Invoke-UnityBatch @('-batchmode', '-quit', '-projectPath', $Repo, '-executeMethod', "Chopper.Editor.$Arg") $log | Out-Null
        Select-String -Path $log -Pattern 'RESULT|SHADER' | ForEach-Object { $_.Line }
    }
    "editmode" {
        $log = Join-Path $Logs "editmode-$Stamp.log"
        $xml = Join-Path $Logs "editmode-$Stamp.xml"
        # -runTests quits by itself; adding -quit would stop it before the tests run.
        Invoke-UnityBatch @('-batchmode', '-projectPath', $Repo, '-runTests', '-testPlatform', 'EditMode', '-testResults', $xml) $log | Out-Null
        if (Test-Path $xml) {
            $run = (Select-String -Path $xml -Pattern '<test-run' | Select-Object -First 1).Line
            if ($run -match 'result="([^"]+)".*total="(\d+)" passed="(\d+)" failed="(\d+)"') {
                Write-Host "EditMode: $($Matches[1]) - total $($Matches[2]), passed $($Matches[3]), failed $($Matches[4])"
            }
            Select-String -Path $xml -Pattern 'result="Failed"' | Where-Object { $_.Line -match 'test-case' } |
                ForEach-Object { "FAILED: " + ($_.Line -replace '.*fullname="([^"]+)".*', '$1') }
        } else { Write-Host "No results file: the scripts did not compile. See the log." }
    }
    "tour" {
        if (-not $Arg) { throw "Name the tour, e.g. tour BlockGround (the -chopper prefix is added)" }
        if (-not (Test-Path $Game)) { throw "No development build. Run: homework.ps1 build" }
        $name = $Arg -replace '^-?chopper', ''
        $log = Join-Path $Logs "tour-$name-$Stamp.log"
        $shots = Join-Path $Logs "tour-$name-$Stamp"
        $p = Start-Process -FilePath $Game -ArgumentList '-chopperHost', '-chopperShot', $shots, "-chopper$name", '-screen-width', '2560', '-screen-height', '1440', '-logFile', $log -PassThru
        if (-not $p.WaitForExit(900000)) { $p.Kill(); Write-Host "TIMEOUT after 15 minutes" }
        Write-Host "log: $log"
        Write-Host "shots: $shots"
        Select-String -Path $log -Pattern 'RESULT|Exception' | Select-Object -First 15 | ForEach-Object { $_.Line }
    }
    "blender" {
        Push-Location (Join-Path $Repo "Chopper\Art\Scripts")
        try {
            & $Blender -b -P check_assets.py 2>&1 | Select-String 'SUMMARY' | ForEach-Object { "check_assets: " + $_.Line }
            & $Blender -b --factory-startup -P check_rig.py 2>&1 | Select-String 'SUMMARY' | ForEach-Object { "check_rig: " + $_.Line }
        } finally { Pop-Location }
    }
    default {
        Get-Content $PSCommandPath | Select-Object -First 11 | ForEach-Object { $_ -replace '^# ?', '' }
    }
}
