# Runs every automatic check in order and writes the results into Chopper\Docs\Reportcard.md
# (the "Automatic checks" section). Then you only play the build and fill your own rows.
# Run from the repo root in PowerShell, with the Unity editor closed:
#   pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1            # everything (about 15 minutes)
#   pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1 -Quick     # skip the art checks and unit tests
#   pwsh -ExecutionPolicy Bypass -File Tools\playtest.ps1 -ToursOnly # the current build's tours only
param(
    [switch] $Quick,
    [switch] $ToursOnly
)

$ErrorActionPreference = "Continue"
$Repo = Split-Path -Parent $PSScriptRoot
$Homework = Join-Path $PSScriptRoot "homework.ps1"
$Game = Join-Path $Repo "Builds\Dev\Chopper.exe"
$Report = Join-Path $Repo "Chopper\Docs\Reportcard.md"
$Stamp = Get-Date -Format "MMdd-HHmm"
$Out = Join-Path $Repo "Logs\homework\playtest-$Stamp"
New-Item -ItemType Directory -Force $Out | Out-Null
$Started = Get-Date

# Id, what it checks, homework task and argument, the line that means PASS ("" for picture-only tours),
# and what to look at in the shots.
$Steps = @(
    @{ Id = "A1"; Name = "Art: check_assets and check_rig"; Task = "blender"; Pass = "0 FAIL"; Count = 2; Art = $true }
    @{ Id = "A2"; Name = "KayKit strike and flinch clips import"; Task = "unity"; Arg = "KayKitImport.Apply"; Pass = "RESULT PASS"; Unity = $true }
    @{ Id = "A3"; Name = "Player prefab rebuild"; Task = "player"; Pass = "Exiting batchmode successfully"; Unity = $true }
    @{ Id = "A4"; Name = "Zones sealed, passes open"; Task = "unity"; Arg = "VoxelChecks.ZoneSeal"; Pass = "RESULT PASS"; Unity = $true }
    @{ Id = "A5"; Name = "Crypt and mine doors reachable on foot"; Task = "unity"; Arg = "VoxelChecks.EntranceReach"; Pass = "RESULT PASS"; Unity = $true }
    @{ Id = "A6"; Name = "EditMode unit tests"; Task = "editmode"; Pass = "failed 0"; Unity = $true; Art = $true }
    @{ Id = "A7"; Name = "Dev build"; Task = "build"; Pass = "Result: Success"; Unity = $true }
    @{ Id = "T1"; Name = "Title screen"; Task = "title"; Pass = ""; Look = "CHOPPER in the heading font over the meadow and hut, menu Continue..Exit" }
    @{ Id = "T2"; Name = "Arrival: drop into the pond, kit on the bank"; Task = "tour"; Arg = "Arrival"; Pass = "RESULT PASS"; Look = "head and shoulders above water while swimming; dressed shot" }
    @{ Id = "T3"; Name = "Swimming"; Task = "tour"; Arg = "Swim"; Pass = ""; Look = "the head and shoulders clear of the water" }
    @{ Id = "T4"; Name = "Double jump with the flip"; Task = "tour"; Arg = "DoubleJump"; Pass = "RESULT PASS"; Look = "doublejump_flip: Chopper mid front flip" }
    @{ Id = "T5"; Name = "Axe combo (KayKit strikes)"; Task = "tour"; Arg = "Combo"; Pass = "RESULT PASS"; Look = "three different strikes, axe head up, blade forward" }
    @{ Id = "T6"; Name = "Combat: enemy engages, staggers, falls; group takes turns"; Task = "tour"; Arg = "Combat"; Pass = "RESULT PASS"; Look = "windup, hit, fall, the group spread round the player" }
    @{ Id = "T7"; Name = "Block guard"; Task = "tour"; Arg = "GuardTest"; Pass = "RESULT PASS"; Look = "axe across the chest, left hand on the haft" }
    @{ Id = "T8"; Name = "Armour kit"; Task = "tour"; Arg = "Armour"; Pass = "RESULT PASS"; Look = "dressed: no helmet; armoured: helmet and shield, hair hidden" }
    @{ Id = "T9"; Name = "Movement: no teleports or freezes"; Task = "tour"; Arg = "MoveTest"; Pass = "RESULT"; Look = "" }
    @{ Id = "T10"; Name = "Block ground and building rules"; Task = "tour"; Arg = "BlockGround"; Pass = "RESULT PASS"; Look = "" }
    @{ Id = "T11"; Name = "Hut: floors, stairs, enemies can leave"; Task = "tour"; Arg = "House"; Pass = "RESULT PASS"; Look = "floors on every level, no light between the log courses" }
    @{ Id = "T12"; Name = "Mine: portal, veins, ladder out"; Task = "tour"; Arg = "Mine"; Pass = "RESULT PASS"; Look = "the portal sits on flat ground you can walk to" }
    @{ Id = "T13"; Name = "Crypt: door, rooms, vault"; Task = "tour"; Arg = "Crypt"; Pass = ""; Look = "crypt_1_entrance: the door at ground level, no hill to climb" }
    @{ Id = "T14"; Name = "Fishing"; Task = "tour"; Arg = "Fishing"; Pass = ""; Look = "rod in the right hand, tension bar over the float" }
    @{ Id = "T15"; Name = "Character against the model sheet"; Task = "tour"; Arg = "Concept"; Pass = ""; Look = "character_front beside the model sheet; colours not too bright" }
)

function Invoke-Step($step) {
    $callArgs = @("-ExecutionPolicy", "Bypass", "-File", $Homework, $step.Task)
    if ($step.Arg) { $callArgs += $step.Arg }
    return (& pwsh @callArgs 2>&1 | ForEach-Object { "$_" })
}

function Invoke-Title {
    $folder = Join-Path $Repo "Logs\homework\tour-Title-$(Get-Date -Format 'MMdd-HHmm')"
    $p = Start-Process -FilePath $Game -ArgumentList '-chopperTitle', $folder, '-screen-width', '2560', '-screen-height', '1440', '-logFile', "$folder.log" -PassThru
    if (-not $p.WaitForExit(300000)) { $p.Kill(); return @("TIMEOUT", "shots: $folder") }
    return @("log: $folder.log", "shots: $folder")
}

$rows = @()
$sheets = @()
foreach ($step in $Steps) {
    if (($ToursOnly -and $step.Task -ne "tour" -and $step.Task -ne "title") -or ($Quick -and $step.Art)) {
        $rows += "| $($step.Id) | $($step.Name) | SKIPPED ($(if ($Quick) { '-Quick' } else { '-ToursOnly' })) | | |"
        continue
    }
    Write-Host "== $($step.Id) $($step.Name)"
    $lines = if ($step.Task -eq "title") { Invoke-Title } else { Invoke-Step $step }
    $lines | ForEach-Object { Write-Host "   $_" }
    $text = $lines -join "`n"
    $log = ($lines | Where-Object { $_ -like "log: *" } | Select-Object -First 1) -replace '^log: ', ''
    $shots = ($lines | Where-Object { $_ -like "shots: *" } | Select-Object -First 1) -replace '^shots: ', ''
    $csErrors = ($lines | Where-Object { $_ -match 'error CS lines: (\d+)' } | ForEach-Object { [int]$Matches[1] } | Measure-Object -Maximum).Maximum

    if ($text -match "Close the Unity editor") { $status = "BLOCKED (close Unity)" }
    elseif ($csErrors -gt 0) { $status = "FAIL ($csErrors compile errors)" }
    elseif ($step.Pass -eq "") { $status = "LOOK" }
    else {
        $hits = ($lines | Where-Object { $_ -cmatch [regex]::Escape($step.Pass) }).Count
        $need = if ($step["Count"]) { $step["Count"] } else { 1 }
        $status = if ($hits -ge $need -and $text -cnotmatch "RESULT FAIL") { "PASS" } else { "FAIL" }
    }
    $proof = ($lines | Where-Object { $_ -cmatch 'RESULT|SUMMARY|Build Finished|EditMode:|Exiting batchmode|FAILED:|TIMEOUT|Exception' } | Select-Object -First 3) -join " / "
    if (-not $proof) { $proof = ($lines | Select-Object -Last 1) }
    $proof = $proof -replace '\|', '/'
    $rel = if ($log) { $log.Replace("$Repo\", "") } else { "" }
    $rows += "| $($step.Id) | $($step.Name) | $status | ``$proof`` | ``$rel`` |"

    if ($shots -and (Test-Path $shots)) {
        $sheet = Join-Path $Out "$($step.Id)-$($step.Arg)$($step.Task -eq 'title' ? 'Title' : '').jpg"
        & python (Join-Path $PSScriptRoot "contact_sheet.py") $shots $sheet | Out-Null
        if (Test-Path $sheet) { $sheets += "| $($step.Id) | $($step.Name) | ``$($sheet.Replace("$Repo\", ''))`` | $($step.Look) |" }
    }
    if ($status -like "BLOCKED*") { break }
}

$minutes = [int]((Get-Date) - $Started).TotalMinutes
$section = @(
    "<!-- playtest:start -->"
    "## Automatic checks"
    ""
    "Filled by ``Tools\playtest.ps1`` on $(Get-Date -Format 'yyyy-MM-dd HH:mm') ($minutes min). Do not edit by hand; run the script again."
    ""
    "| ID | Check | Status | Printed line | Log |"
    "|---|---|---|---|---|"
) + $rows + @(
    ""
    "### Shots to look at"
    ""
    "Open each contact sheet. Put anything that looks wrong in your rows below."
    ""
    "| ID | Check | Contact sheet | Look for |"
    "|---|---|---|---|"
) + $sheets + @("<!-- playtest:end -->")

$body = Get-Content $Report -Raw
$block = $section -join "`n"
if ($body -match '(?s)<!-- playtest:start -->.*<!-- playtest:end -->') {
    $body = [regex]::Replace($body, '(?s)<!-- playtest:start -->.*<!-- playtest:end -->', [System.Text.RegularExpressions.MatchEvaluator] { param($m) $block })
} else {
    $body = $body -replace '(?m)^## Current', "$block`n`n## Current"
}
Set-Content -Path $Report -Value $body.TrimEnd() -NoNewline
Add-Content -Path $Report -Value ""
Write-Host ""
Write-Host "Report card updated: $Report"
Write-Host "Contact sheets: $Out"
