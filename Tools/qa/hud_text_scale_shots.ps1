<#
  HUD stills for the TEXT SIZE and tooltip review, from a development player (the HUD shot harness,
  Assets/Scripts/Runtime/UI/HudShotHarness.cs, only exists in development builds):

    "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -quit -projectPath . `
      -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindows -smBuildPath Builds\Hud -smDevelopment -logFile build.log
    powershell -ExecutionPolicy Bypass -File Tools\qa\hud_text_scale_shots.ps1 -GameDir Builds\Hud\Windows

  Runs every combination of screen (1280x800, 1920x1080, 3840x2160) and text size (default 1.0 and
  the largest, 1.5). Each run writes the harness stills plus tooltip stills (16_tip_chip ...
  22_tip_title) under -OutDir; the harness keeps its profile and saves beside the stills, so the
  player's own settings and colonies are not touched.
  The desktop must be unlocked and the session connected: a locked or disconnected session renders
  nothing (blank frames, and OnGUI never runs, so the tooltip anchors are missing in the log).
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$OutDir = 'Builds\HudTextScaleShots',
  [string]$Screens = '1280x800,1080p,4k',
  [string]$Scales = '1.0,1.5'
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path (Resolve-Path $GameDir).Path 'SolarMajesty.exe'
if (-not (Test-Path $exe)) { throw "No SolarMajesty.exe in $GameDir" }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$sizes = @{ '1280x800' = @(1280, 800, 0); '1080p' = @(1920, 1080, 0); '4k' = @(3840, 2160, 1) }

foreach ($screen in $Screens.Split(',')) {
  $s = $sizes[$screen]
  foreach ($scale in $Scales.Split(',')) {
    $name = "$screen-$($scale.Replace('.', ''))"
    $out = Join-Path $OutDir $name
    New-Item -ItemType Directory -Force $out | Out-Null
    $log = Join-Path $out 'Player.log'
    $a = "-smHudShot `"$out`" -nosteam -screen-width $($s[0]) -screen-height $($s[1]) " +
      "-screen-fullscreen $($s[2]) -textScale $scale -logFile `"$log`""
    Write-Host "[hudshots] $name" -ForegroundColor Yellow
    $p = Start-Process $exe -ArgumentList $a -PassThru
    if (-not $p.WaitForExit(300000)) { Stop-Process -Id $p.Id -Force; Write-Warning "$name timed out" }
    if (Test-Path $log) {
      Select-String -Path $log -Pattern '\[HudShot\] hover|no tooltip anchor' | ForEach-Object { $_.Line }
    }
  }
}
