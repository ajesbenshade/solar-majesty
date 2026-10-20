<#
  Plays the whole campaign (Earth -> Luna -> Mars -> Belt -> Europa) in the built game with
  -autoplay, on a throwaway profile, and fails on any exception, error log, stall or timeout.

    powershell -ExecutionPolicy Bypass -File Tools\qa\run_autoplay.ps1 -Build
    powershell -ExecutionPolicy Bypass -File Tools\qa\run_autoplay.ps1 -GameDir Builds\SteamRC\Windows

  The game plays through the player's own controls (build menu placement, bounties and their +/-,
  the tech panel, pause / settings / title / save / load / continue / victory buttons) with the
  world running faster than 4x. On every world it saves and loads a slot and goes to the title and
  Continues, checking nothing is lost. Midway through -RelaunchOn (Mars by default) it presses QUIT;
  this script starts the game again and the run carries on from the title's Continue.

  Exit code 0 = the campaign was won on all five worlds with no error logged.
  Report: <OutDir>\autoplay-<time>.json plus both Player logs. Your saves and settings are never
  touched: -saveDir and -profileDir point at a temp folder that is deleted afterwards.
#>
param(
  [string]$GameDir = 'Builds\Autoplay\Windows',
  # Build a release player into GameDir's parent first (Unity batch mode; the editor must be closed on this project).
  [switch]$Build,
  [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe',
  # Extra world speed on top of the game's fastest (4x). 6 means the world runs 24x the 1x pace.
  [double]$Boost = 6,
  [int]$TimeoutMinutes = 150,
  # Sim-minutes with no progress on any front (dens, research, treasury, gates, buildings) before failing.
  [int]$StallMinutes = 15,
  # World on which to quit and relaunch the game mid-run; '' to skip.
  [string]$RelaunchOn = 'Mars',
  # Start a new campaign on a later world instead of Earth (for iterating on one world; a
  # release check starts on Earth). The run then passes when every world from there on is won.
  [string]$From = '',
  [int]$Width = 1280,
  [int]$Height = 720,
  # 0 Very Low .. 5 Ultra; -1 keeps the game's own pick. Low tiers run the campaign faster.
  [int]$Quality = -1,
  [string]$OutDir = 'Builds\Autoplay\Reports',
  [switch]$FailFast,
  # Leave the victory / launch camera beats on (they are skipped with -nobeats by default, which
  # keeps the run fast; the harness waits on game state either way).
  [switch]$Beats,
  [switch]$KeepTemp,
  [string]$ExtraArgs = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if ($Build) {
  $buildRoot = Split-Path (Join-Path $root $GameDir) -Parent
  $buildLog = Join-Path ([IO.Path]::GetTempPath()) ("sm-autoplay-build-" + [Guid]::NewGuid().ToString('N') + ".log")
  Write-Host "[autoplay] building release player -> $buildRoot" -ForegroundColor Yellow
  $b = Start-Process $Unity -ArgumentList "-batchmode -quit -projectPath `"$root`" -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindows -smBuildPath `"$buildRoot`" -logFile `"$buildLog`"" -PassThru -Wait
  Select-String -Path $buildLog -Pattern '\[Build\]|error CS' | ForEach-Object { $_.Line }
  if ($b.ExitCode -ne 0) { throw "Build failed (exit $($b.ExitCode)). Log: $buildLog" }
}

$exe = Join-Path (Resolve-Path (Join-Path $root $GameDir)).Path 'SolarMajesty.exe'
if (-not (Test-Path $exe)) { throw "No SolarMajesty.exe in $GameDir (use -Build)" }
New-Item -ItemType Directory -Force (Join-Path $root $OutDir) | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$report = Join-Path (Resolve-Path (Join-Path $root $OutDir)).Path "autoplay-$stamp.json"

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("sm-autoplay-" + [Guid]::NewGuid().ToString('N'))
$saves = Join-Path $tmp 'Saves'
New-Item -ItemType Directory -Force $saves, (Join-Path $tmp 'Profile') | Out-Null

function Invoke-Game([string]$extra, [string]$log, [int]$budgetSeconds) {
  # One quoted string: Start-Process joins an array with bare spaces, which would split a path with a space in it.
  $gameArgs = "-autoplay -autoplayOut `"$report`" -autoplayBoost $Boost -autoplayTimeout $TimeoutMinutes " +
    "-autoplayStall $StallMinutes -saveDir `"$saves`" -profileDir `"$(Join-Path $tmp 'Profile')`" " +
    "-nosteam -noai -screen-width $Width -screen-height $Height -screen-fullscreen 0 -logFile `"$log`" $extra"
  if ($RelaunchOn) { $gameArgs += " -autoplayRelaunchOn $RelaunchOn" }
  if ($FailFast) { $gameArgs += " -autoplayFailFast" }
  if ($Quality -ge 0) { $gameArgs += " -autoplayQuality $Quality" }
  if ($From) { $gameArgs += " -autoplayFrom $From" }
  if (-not $Beats) { $gameArgs += " -nobeats" }
  if ($ExtraArgs) { $gameArgs += " $ExtraArgs" }
  $p = Start-Process $exe -ArgumentList $gameArgs -PassThru
  if (-not $p.WaitForExit($budgetSeconds * 1000)) {
    Stop-Process -Id $p.Id -Force
    Write-Warning "Game did not exit within $budgetSeconds s; killed. Log: $log"
    return 3
  }
  return $p.ExitCode
}

$started = Get-Date
$budget = ($TimeoutMinutes + 10) * 60
Write-Host "[autoplay] campaign run, boost ${Boost}x, relaunch on '$RelaunchOn' -> $report" -ForegroundColor Yellow
$log1 = Join-Path $tmp 'Player.log'
$code = Invoke-Game '' $log1 $budget
$logs = @($log1)

$state = Join-Path $saves 'autoplay-state.json'
if ((Test-Path $state) -and ((Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).relaunchPending)) {
  Write-Host "[autoplay] game quit for the relaunch check (exit $code); starting it again" -ForegroundColor Yellow
  $log2 = Join-Path $tmp 'Player-relaunch.log'
  $left = [int]($budget - ((Get-Date) - $started).TotalSeconds)
  $code = Invoke-Game '-autoplayResume' $log2 ([Math]::Max(300, $left))
  $logs += $log2
}

foreach ($l in $logs) {
  if (Test-Path $l) {
    Select-String -Path $l -Pattern '\[Autoplay\]' | ForEach-Object { $_.Line }
    Copy-Item $l (Join-Path (Split-Path $report -Parent) ("autoplay-$stamp-" + (Split-Path $l -Leaf)))
  }
}
if (Test-Path $report) {
  $j = Get-Content $report -Raw -Encoding UTF8 | ConvertFrom-Json
  ""
  "{0}  {1:N1} real-min  relaunches {2}  errors {3}  {4}" -f ($(if ($j.passed) { 'PASSED' } else { 'FAILED' })), ($j.realSeconds / 60), $j.relaunches, $j.errorCount, $j.failure
  # Plain lines: Format-Table drops columns that do not fit the console, whatever Out-String's width.
  # checks = settings / save-load / title-Continue / relaunch (+ done, - not on this world).
  $row = "{0,-7} {1,-5} {2,6} {3,7} {4,5} {5,8} {6,4} {7,10} {8,6} {9,8} {10,6}"
  $row -f 'body', 'won', 'simMin', 'realMin', 'built', 'bounties', 'dens', 'robotsLost', 'refabs', 'restarts', 'checks'
  foreach ($b in $j.bodies) {
    $checks = (@($b.settingsChecked, $b.saveLoadChecked, $b.titleContinueChecked, $b.relaunchChecked) |
      ForEach-Object { if ($_) { '+' } else { '-' } }) -join ''
    $row -f $b.body, $b.won, ('{0:N1}' -f ($b.simSeconds / 60)), ('{0:N1}' -f ($b.realSeconds / 60)),
      $b.buildingsPlaced, $b.bountiesPosted, $b.densCleared, $b.robotsLost, $b.refabs, $b.restarts, $checks
  }
  ""
  "achievements: " + ($j.achievements -join ', ')
  "last verdict: " + $j.verdict
  ""
  "report: $report"
} else {
  Write-Warning "No summary written (exit $code)."
  if ($code -eq 0) { $code = 4 }
}
if (-not $KeepTemp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue } else { "temp kept: $tmp" }
exit $code
