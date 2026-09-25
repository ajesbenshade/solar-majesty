param([switch]$Wait)
$ErrorActionPreference = 'Stop'

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = (Resolve-Path (Join-Path $Here '..')).Path
$BuildsRoot = Join-Path $Root 'Builds'

if (-not (Test-Path $BuildsRoot)) {
  throw "Could not find $BuildsRoot"
}

$Exe = Get-ChildItem -Path $BuildsRoot -Recurse -File -Filter 'SolarMajesty.exe' |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

if (-not $Exe) {
  throw 'No Windows build was found under Builds. Create one from Solar Majesty -> Build -> Windows.'
}

$DataFolder = Join-Path $Exe.Directory.FullName 'SolarMajesty_Data'
if (-not (Test-Path $DataFolder)) {
  throw "The build is incomplete: missing $DataFolder"
}

$PlaytestNotes = Join-Path $Exe.Directory.FullName 'PLAYTEST.txt'
Write-Host "[solar-majesty] Launching build"
Write-Host "[solar-majesty] Game:  $($Exe.FullName)"
if (Test-Path $PlaytestNotes) {
  Write-Host "[solar-majesty] Notes: $PlaytestNotes"
}

if ($Wait) {
  Start-Process -FilePath $Exe.FullName -WorkingDirectory $Exe.Directory.FullName -Wait
} else {
  Start-Process -FilePath $Exe.FullName -WorkingDirectory $Exe.Directory.FullName | Out-Null
}
