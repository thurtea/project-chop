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
#   prune                 trim Logs\homework now (also runs before every task) -> how many runs were removed
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

function Sync-Exports {
    $export = Join-Path $Repo "Chopper\Art\Export"
    $art = Join-Path $Repo "Assets\_Chopper\Art"
    if (-not (Test-Path $export) -or -not (Test-Path $art)) { return }
    $mark = Join-Path $Repo "Tools\export-sync-since.txt"
    if (-not (Test-Path $mark)) { Set-Content $mark "2026-10-16T23:45:00Z" }
    $since = [DateTime]::Parse((Get-Content $mark -Raw).Trim()).ToUniversalTime()
    $placed = @{}
    Get-ChildItem $art -Recurse -Filter *.fbx | ForEach-Object { if (-not $placed.ContainsKey($_.Name)) { $placed[$_.Name] = $_.FullName } }
    Get-ChildItem $export -Filter *.fbx | ForEach-Object {
        if ($placed.ContainsKey($_.Name)) {
            $dest = Get-Item $placed[$_.Name]
            if ($_.LastWriteTimeUtc -gt $dest.LastWriteTimeUtc -and $_.LastWriteTimeUtc -gt $since) {
                Copy-Item $_.FullName $dest.FullName -Force
                Write-Host "synced export $($_.Name) -> $($dest.FullName.Substring($Repo.Length + 1))"
            }
        }
    }
}

function Prune-Logs([double] $KeepHours = 24) {
    $cut = (Get-Date).AddHours(-$KeepHours)
    $newest = @{}
    $entries = Get-ChildItem $Logs -Force | Sort-Object LastWriteTime -Descending
    $removed = 0
    $freed = 0
    foreach ($e in $entries) {
        $type = if ($e.PSIsContainer) { "dir" } else { $e.Extension }
        if ($e.Name -eq "pruned.txt") { continue }
        $kind = ((($e.Name -replace '\.(log|xml|txt)$', '') -replace '-\d{4}-\d{4}$', '') + "|" + $type)
        $keep = $e.LastWriteTime -ge $cut -or -not $newest.ContainsKey($kind)
        if (-not $newest.ContainsKey($kind)) { $newest[$kind] = $true }
        if ($keep) { continue }
        $freed += (Get-ChildItem $e.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
        Remove-Item $e.FullName -Recurse -Force
        Add-Content (Join-Path $Logs "pruned.txt") ("{0} {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm"), $e.Name)
        $removed++
    }
    if ($removed -gt 0) { Write-Host ("pruned {0} old log entries ({1:N0} MB) from Logs\homework" -f $removed, ($freed / 1MB)) }
}

if ($Task -ne "help") { Prune-Logs; Sync-Exports }

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
    "prune" {
        Write-Host "Logs\homework keeps the last 24 hours and the newest run of every kind."
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
