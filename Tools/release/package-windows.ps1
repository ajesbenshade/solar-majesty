<#
  Zips a local Windows build with the bundled local-AI pack, for testers outside Steam. The Windows
  counterpart of package.sh (which CI runs, and which needs zip): same zip and folder names, same
  debug folders left out. Steam uploads go through Tools\steam\upload_to_steam.ps1 instead.

    powershell -ExecutionPolicy Bypass -File Tools\release\package-windows.ps1 `
        -GameDir Builds\Release\Windows -AiDir Builds\AiPack\ai -NoAi

  Writes into Builds\Packages (or -OutDir):
    SolarMajesty-<version>-Windows.zip        the game, plus the ai folder when -AiDir is given (about 4 GB)
    SolarMajesty-<version>-Windows-NoAI.zip   with -NoAi: the game only (about 0.2 GB; baked lines only)
    SHA256SUMS.txt                            checksums to paste next to the download links

  <version> comes from the build's build-info.json (ReleaseStamp), or -Version. Testers unzip and
  double-click SolarMajesty.exe; the game finds the ai folder by itself (LocalAiHost). Build the ai
  folder with Tools\local_ai\build_ai_pack.ps1. See Docs/LOCAL_AI_PACK.md.
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$AiDir,
  [switch]$NoAi,
  [string]$Version,
  [string]$OutDir = 'Builds\Packages'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Say($m) { Write-Host "[package] $m" -ForegroundColor Yellow }

$GameDir = (Resolve-Path $GameDir).Path
if (-not (Test-Path (Join-Path $GameDir 'SolarMajesty.exe'))) {
  throw "No SolarMajesty.exe in $GameDir (the demo player goes through package.sh ... demo, without the AI pack)."
}
$info = Join-Path $GameDir 'SolarMajesty_Data\StreamingAssets\build-info.json'
if (Test-Path $info) {
  $json = Get-Content $info -Raw
  if ($json -match '"flavour"\s*:\s*"demo"') { throw "$info says this is a demo build; the AI pack ships with the full game only." }
  if (-not $Version -and $json -match '"version"\s*:\s*"([^"]+)"') { $Version = $Matches[1] }
}
if (-not $Version) { throw "No build-info.json in $GameDir to read the version from; pass -Version." }
if ($AiDir) {
  $AiDir = (Resolve-Path $AiDir).Path
  foreach ($need in @('llama', 'models')) {
    if (-not (Test-Path (Join-Path $AiDir $need))) { throw "AI pack at $AiDir has no $need folder (build it with Tools\local_ai\build_ai_pack.ps1)." }
  }
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

# Weights and compiled libraries do not shrink; everything else does.
$stored = @('.gguf', '.onnx', '.bin', '.pyd', '.dll', '.resS', '.resource', '.assets')
# Unity's debug-symbol side folders (as package.sh), any ai folder already in the build, and AI logs.
$skip = '^[^\\]*_(BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame)(\\|$)|^ai\\'

function Add-Tree($zip, $root, $prefix, $skipPattern) {
  $rootLen = $root.TrimEnd('\').Length + 1
  Get-ChildItem $root -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($rootLen)
    if ($skipPattern -and $rel -match $skipPattern) { return }
    if ($_.Extension -eq '.log') { return }
    $level = if ($stored -contains $_.Extension) { [IO.Compression.CompressionLevel]::NoCompression } else { [IO.Compression.CompressionLevel]::Fastest }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, ($prefix + $rel.Replace('\', '/')), $level)
  }
}

$written = @()
function New-Package($suffix, [bool]$withAi) {
  $top = "SolarMajesty-$Version-Windows"
  $name = "$top$suffix.zip"
  $zipPath = Join-Path $OutDir $name
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Say "writing $name"
  $fs = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
  $zip = New-Object IO.Compression.ZipArchive($fs, [IO.Compression.ZipArchiveMode]::Create)
  try {
    Add-Tree $zip $GameDir "$top/" $skip
    if ($withAi) { Add-Tree $zip $AiDir "$top/ai/" $null }
  }
  finally { $zip.Dispose(); $fs.Dispose() }
  Say ("{0}: {1:N2} GB" -f $name, ((Get-Item $zipPath).Length / 1GB))
  $script:written += $zipPath
}

New-Package '' ([bool]$AiDir)
if ($NoAi) {
  if (-not $AiDir) { Say '-NoAi without -AiDir: the main zip already has no AI pack; skipping the second zip' }
  else { New-Package '-NoAI' $false }
}

$sums = Join-Path $OutDir 'SHA256SUMS.txt'
$written | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $_) } |
  Set-Content -Encoding ASCII $sums
Get-Content $sums | ForEach-Object { Say $_ }
Say "done: $OutDir"
