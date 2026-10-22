<#
  Zips a built game for beta testers.

    powershell -ExecutionPolicy Bypass -File Tools\package_beta.ps1 -GameDir Builds\Detail\Windows `
        -AiDir Builds\Beta\stage\ai

  Produces in Builds\Beta (or -OutDir):
    SolarMajesty-Beta-<date>-Full.zip    game + the ai folder (models included; about 4 GB)
    SolarMajesty-Beta-<date>-Lite.zip    game only (about 0.2 GB); baked lines, no live AI
    *.sha256.txt                         checksums to paste next to the OneDrive links

  Testers unzip and double-click SolarMajesty.exe. The game finds the ai folder by itself
  (LocalAiHost). Build the ai folder with Tools\local_ai\build_ai_pack.ps1.
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$AiDir,
  [string]$OutDir = 'Builds\Beta',
  [string]$Tag = (Get-Date -Format 'yyyyMMdd')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$GameDir = (Resolve-Path $GameDir).Path
if (-not (Test-Path (Join-Path $GameDir 'SolarMajesty.exe'))) { throw "No SolarMajesty.exe in $GameDir" }
if ($AiDir) { $AiDir = (Resolve-Path $AiDir).Path }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

function Say($m) { Write-Host "[package] $m" -ForegroundColor Yellow }

# Weights and compiled libraries do not shrink; everything else does.
$stored = @('.gguf', '.onnx', '.bin', '.pyd', '.dll', '.resS', '.resource', '.assets')

function Add-Tree($zip, $root, $prefix, $skipDirPattern) {
  $rootLen = $root.TrimEnd('\').Length + 1
  Get-ChildItem $root -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($rootLen)
    if ($skipDirPattern -and $rel -match $skipDirPattern) { return }
    $level = if ($stored -contains $_.Extension) { [IO.Compression.CompressionLevel]::NoCompression } else { [IO.Compression.CompressionLevel]::Fastest }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, ($prefix + $rel.Replace('\', '/')), $level)
  }
}

function New-Package($name, [bool]$withAi) {
  $zipPath = Join-Path $OutDir $name
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Say "writing $name"
  $fs = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
  $zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
  try {
    Add-Tree $zip $GameDir 'SolarMajesty-Beta/' '^[^\\]*_BurstDebugInformation_DoNotShip(\\|$)|^ai\\'
    if ($withAi) { Add-Tree $zip $AiDir 'SolarMajesty-Beta/ai/' $null }
  }
  finally { $zip.Dispose(); $fs.Dispose() }
  $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
  "$hash  $name" | Set-Content -Encoding ASCII "$zipPath.sha256.txt"
  Say ("{0}: {1:N2} GB  sha256 {2}" -f $name, ((Get-Item $zipPath).Length / 1GB), $hash.Substring(0, 12))
}

if ($AiDir) { New-Package "SolarMajesty-Beta-$Tag-Full.zip" $true }
New-Package "SolarMajesty-Beta-$Tag-Lite.zip" $false
Say "done: $OutDir"
