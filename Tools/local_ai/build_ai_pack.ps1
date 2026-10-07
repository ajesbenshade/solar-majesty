<#
  Assembles the self-contained AI pack the game starts by itself (see LocalAiHost.cs):

    <OutDir>\ai\python\       portable Python with Kokoro's packages  (copied from this PC)
    <OutDir>\ai\llama\        llama.cpp CPU build with llama-server.exe (from a release zip)
    <OutDir>\ai\models\       Qwen3 1.7B + 4B (GGUF), Kokoro model + voices
    <OutDir>\ai\scripts\      voice_server.py, model_router.py
    <OutDir>\ai\THIRD-PARTY-NOTICES.txt

  Nothing is downloaded unless you pass -DownloadLlama. Prefer handing in a zip you fetched yourself:

    powershell -ExecutionPolicy Bypass -File Tools\local_ai\build_ai_pack.ps1 -OutDir Builds\Beta\stage `
        -LlamaZip C:\path\llama-b11457-bin-win-cpu-x64.zip

  Needs Tools\local_ai\.venv (python -m venv, then pip install kokoro-onnx) and Tools\local_ai\models.
  -NoChat leaves out the 4B chat model (saves 2.5 GB); -NoVoice leaves out Kokoro and Python.
#>
param(
  [Parameter(Mandatory = $true)][string]$OutDir,
  [string]$LlamaZip,
  [switch]$DownloadLlama,
  [string]$LlamaTag = 'b11457',
  [string]$Venv,
  [string]$Models,
  [switch]$NoChat,
  [switch]$NoVoice,
  [switch]$SkipLlama,
  [switch]$SkipVerify
)
$ErrorActionPreference = 'Stop'
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Venv) { $Venv = Join-Path $Here '.venv' }
if (-not $Models) { $Models = Join-Path $Here 'models' }
$Ai = Join-Path $OutDir 'ai'

function Say($m) { Write-Host "[ai-pack] $m" -ForegroundColor Yellow }
function Copy-Tree($from, $to, [string[]]$excludeDirs = @(), [string[]]$excludeFiles = @()) {
  New-Item -ItemType Directory -Force $to | Out-Null
  $a = @($from, $to, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1')
  if ($excludeDirs.Count) { $a += '/XD'; $a += $excludeDirs }
  if ($excludeFiles.Count) { $a += '/XF'; $a += $excludeFiles }
  & robocopy @a | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE): $from" }
}

if (Test-Path $Ai) { Say "replacing $Ai"; Remove-Item -Recurse -Force $Ai }
New-Item -ItemType Directory -Force $Ai | Out-Null

# --- models -------------------------------------------------------------------------------------
$files = @('Qwen3-1.7B-Q4_K_M.gguf')
if (-not $NoChat) { $files += 'Qwen3-4B-Instruct-2507-Q4_K_M.gguf' }
if (-not $NoVoice) { $files += @('kokoro-v1.0.onnx', 'voices-v1.0.bin') }
New-Item -ItemType Directory -Force (Join-Path $Ai 'models') | Out-Null
foreach ($f in $files) {
  $src = Join-Path $Models $f
  if (-not (Test-Path $src)) { throw "Missing model file: $src" }
  Say "copying $f"
  Copy-Item $src (Join-Path $Ai 'models') -Force
}

# --- scripts ------------------------------------------------------------------------------------
New-Item -ItemType Directory -Force (Join-Path $Ai 'scripts') | Out-Null
Copy-Item (Join-Path $Here 'voice_server.py') (Join-Path $Ai 'scripts') -Force
if (-not $NoChat) { Copy-Item (Join-Path $Here 'model_router.py') (Join-Path $Ai 'scripts') -Force }

# --- llama.cpp ----------------------------------------------------------------------------------
if ($SkipLlama) { Say 'skipping llama.cpp (-SkipLlama): no live hero lines in this pack' }
else {
if (-not $LlamaZip) {
  if (-not $DownloadLlama) { throw 'Give -LlamaZip <llama-bNNNN-bin-win-cpu-x64.zip>, or -DownloadLlama to fetch it.' }
  $name = "llama-$LlamaTag-bin-win-cpu-x64.zip"
  $LlamaZip = Join-Path $env:TEMP $name
  if (-not (Test-Path $LlamaZip)) {
    $url = "https://github.com/ggml-org/llama.cpp/releases/download/$LlamaTag/$name"
    Say "downloading $url"
    & curl.exe -fL --progress-bar -o "$LlamaZip.part" $url
    if ($LASTEXITCODE -ne 0) { throw "Download failed: $url" }
    Move-Item -Force "$LlamaZip.part" $LlamaZip
  }
}
Say "unpacking $(Split-Path -Leaf $LlamaZip)"
$tmp = Join-Path $env:TEMP ("llama-" + [guid]::NewGuid().ToString('N'))
Expand-Archive -Path $LlamaZip -DestinationPath $tmp -Force
$server = Get-ChildItem $tmp -Recurse -Filter 'llama-server.exe' | Select-Object -First 1
if (-not $server) { throw 'llama-server.exe is not in that zip.' }
$llamaDir = Join-Path $Ai 'llama'
New-Item -ItemType Directory -Force $llamaDir | Out-Null
# The server needs its own DLLs and the ggml backends; the other tools' DLLs (cli, bench, quantize...) are dead weight.
$unused = 'llama-(cli|bench|batched-bench|completion|fit-params|perplexity|quantize)-impl\.dll'
Get-ChildItem $server.DirectoryName -File |
  Where-Object { ($_.Name -eq 'llama-server.exe' -or $_.Extension -in '.dll', '.txt', '.md' -or $_.Name -like 'LICENSE*') -and $_.Name -notmatch $unused } |
  Copy-Item -Destination $llamaDir -Force
Remove-Item -Recurse -Force $tmp
}

# --- portable Python with Kokoro ----------------------------------------------------------------
if (-not $NoVoice) {
  $venvPy = Join-Path $Venv 'Scripts\python.exe'
  if (-not (Test-Path $venvPy)) { throw "No venv at $Venv. Run: python -m venv `"$Venv`" ; `"$venvPy`" -m pip install kokoro-onnx" }
  $base = (& $venvPy -c "import sys; print(sys.base_prefix)").Trim()
  $site = Join-Path $Venv 'Lib\site-packages'
  Say "copying Python from $base"
  $py = Join-Path $Ai 'python'
  Copy-Tree $base $py `
    -excludeDirs @('Doc', 'include', 'libs', 'Scripts', 'tcl', 'Tools', '_resources', 'AppxMetadata', 'microsoft.system.package.metadata',
                   'test', 'tests', 'idlelib', 'tkinter', 'turtledemo', 'site-packages', '__pycache__') `
    -excludeFiles @('Appx*', '*.sccd', 'idle*.exe', 'pip*.exe', 'python3.12.exe', 'pythonw*.exe', 'tcl*.dll', 'tk*.dll', '_tkinter.pyd', '*.pdb')
  Say 'copying Kokoro packages'
  Copy-Tree $site (Join-Path $py 'Lib\site-packages') -excludeDirs @('pip', '__pycache__') -excludeFiles @('*.pyc')
  Get-ChildItem (Join-Path $py 'Lib\site-packages') -Directory -Filter 'pip-*' | Remove-Item -Recurse -Force
}

# --- notices ------------------------------------------------------------------------------------
@"
Solar Majesty - local AI pack: third-party software and models
================================================================
These run on your PC only. Nothing is sent anywhere. Each keeps its own license.

Qwen3 1.7B and Qwen3 4B Instruct 2507 (GGUF, 4-bit)   Apache-2.0   Alibaba Cloud / Qwen team
Kokoro-82M voice model (ONNX) and voices              Apache-2.0   hexgrad; ONNX build by thewh1teagle
llama.cpp (llama-server)                              MIT          ggml-org, https://github.com/ggml-org/llama.cpp
kokoro-onnx                                           MIT          https://github.com/thewh1teagle/kokoro-onnx
ONNX Runtime                                          MIT          Microsoft
NumPy                                                 BSD-3-Clause
Python 3.12 (python\)                                 PSF License  https://www.python.org
phonemizer                                            GPL-3.0      https://github.com/bootphon/phonemizer
espeak-ng (espeakng_loader)                           GPL-3.0      https://github.com/espeak-ng/espeak-ng

phonemizer and espeak-ng are GPL-3.0 programs the voice server runs as a separate process.
Their source is in python\Lib\site-packages (Python code) and at the links above. The game itself
does not link them. To switch the AI off entirely, start SolarMajesty.exe with -noai or delete this folder.
"@ | Set-Content -Encoding UTF8 (Join-Path $Ai 'THIRD-PARTY-NOTICES.txt')

# --- verify -------------------------------------------------------------------------------------
if (-not $SkipVerify) {
  Say 'verifying'
  $srv = Join-Path $Ai 'llama\llama-server.exe'
  if (Test-Path $srv) {
    $ErrorActionPreference = 'Continue'   # llama-server prints its version on stderr
    $v = (& $srv --version 2>&1 | ForEach-Object { "$_" }) -join "`n"
    $ErrorActionPreference = 'Stop'
    Say ("llama-server: " + ($v -split "`n" | Where-Object { $_ -match 'version' } | Select-Object -First 1))
  }
  if (-not $NoVoice) {
    $pp = Join-Path $Ai 'python\python.exe'
    & $pp -I -c "import kokoro_onnx, onnxruntime, numpy, phonemizer; print('python ok', onnxruntime.__version__)"
    if ($LASTEXITCODE -ne 0) { throw 'The copied Python cannot import Kokoro.' }
  }
}
$size = (Get-ChildItem $Ai -Recurse -File | Measure-Object Length -Sum).Sum / 1GB
Say ("done: {0} ({1:N2} GB)" -f $Ai, $size)
