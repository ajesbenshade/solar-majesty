param([switch]$Wait)
$ErrorActionPreference = 'Stop'

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = (Resolve-Path (Join-Path $Here '..')).Path
$ProjectVersionFile = Join-Path $Root 'ProjectSettings\ProjectVersion.txt'

if (-not (Test-Path $ProjectVersionFile)) {
  throw "Could not find $ProjectVersionFile"
}

$versionLine = Get-Content $ProjectVersionFile | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
if (-not $versionLine) {
  throw 'Could not read m_EditorVersion from ProjectSettings\ProjectVersion.txt'
}

$version = ($versionLine -split ':', 2)[1].Trim()
$UnityExe = Join-Path "C:\Program Files\Unity\Hub\Editor\$version\Editor" 'Unity.exe'

if (-not (Test-Path $UnityExe)) {
  throw "Unity $version was not found at $UnityExe. Install that editor version in Unity Hub first."
}

$ScenePath = Join-Path $Root 'Assets\Scenes\LunarOutpost_Sandbox.unity'
Write-Host "[solar-majesty] Opening Unity $version"
Write-Host "[solar-majesty] Project: $Root"
Write-Host "[solar-majesty] Scene:   $ScenePath"

$args = @('-projectPath', $Root)
if ($Wait) {
  Start-Process -FilePath $UnityExe -WorkingDirectory (Split-Path $UnityExe) -ArgumentList $args -Wait
} else {
  Start-Process -FilePath $UnityExe -WorkingDirectory (Split-Path $UnityExe) -ArgumentList $args | Out-Null
}
