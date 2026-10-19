<#
  Captures Steam store screenshots from a release build, on a throwaway profile.

    powershell -ExecutionPolicy Bypass -File Tools\qa\store_screenshots.ps1 -GameDir Builds\SteamRC\Windows
    ... -Bodies Earth,Mars -Run 40 -OutDir Docs\StoreShots

  For each world: a working colony, a crew of every robot class, bounties on the nearest dens,
  some time running at 2x, then 1920x1080 captures with and without the HUD. Keep the desktop
  unlocked and the window in front; a locked screen captures black frames.
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$Bodies = 'Earth,Luna,Mars,Belt,Europa',
  [int]$Run = 35,
  [string]$OutDir = 'Builds\StoreShots',
  [switch]$Diorama
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path (Resolve-Path $GameDir).Path 'SolarMajesty.exe'
if (-not (Test-Path $exe)) { throw "No SolarMajesty.exe in $GameDir" }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("sm-shots-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $tmp 'Saves'), (Join-Path $tmp 'Profile') | Out-Null

# One quoted string: Start-Process joins an array with bare spaces, which would split a path with a space in it.
$gameArgs = "-storeshots `"$OutDir`" -storeshotBodies $Bodies -storeshotRun $Run " +
  "-saveDir `"$(Join-Path $tmp 'Saves')`" -profileDir `"$(Join-Path $tmp 'Profile')`" " +
  "-nosteam -noai -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 " +
  "-logFile `"$(Join-Path $tmp 'Player.log')`""
if ($Diorama) { $gameArgs += " -diorama" }

Write-Host "[shots] $Bodies -> $OutDir" -ForegroundColor Yellow
$p = Start-Process $exe -ArgumentList $gameArgs -PassThru
$budget = 120 + ($Bodies.Split(',').Count * ($Run + 90))
if (-not $p.WaitForExit($budget * 1000)) {
  Stop-Process -Id $p.Id -Force
  throw "Screenshots did not finish in $budget s. Log: $(Join-Path $tmp 'Player.log')"
}
Select-String -Path (Join-Path $tmp 'Player.log') -Pattern '\[StoreShots\]|\[Flags\]|bounty' | ForEach-Object { $_.Line }
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
exit $p.ExitCode
