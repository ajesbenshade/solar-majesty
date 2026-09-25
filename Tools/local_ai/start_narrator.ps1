<#
  Starts Solar Majesty's local AI for hero voices: a small LLM that writes each hero's line and a
  Kokoro text-to-speech server that speaks it. Then (optionally) the game.

    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1             # LLM + voices + built game
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoGame     # servers only (Unity editor)
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoSpeech   # text lines only
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -Laya       # also Laya decisions (PyTorch)

  LLM backend order: llama.cpp (llama-server) -> Ollama -> Python fallback (llama-cpp-python, CPU).
  Models: Qwen3-1.7B 4-bit (~1.1 GB); Kokoro-82M voices (~340 MB, port 8880). Downloads resume.
  Closing this window stops the servers. See Docs/HERO_NARRATION.md.
#>
param([switch]$NoGame, [switch]$Laya, [switch]$NoSpeech, [int]$Port = 8080, [int]$VoicePort = 8880)
$ErrorActionPreference = 'Stop'

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $Here '..\..')
$Models = Join-Path $Here 'models'
$Venv = Join-Path $Here '.venv'
$GgufRepo = 'unsloth/Qwen3-1.7B-GGUF'
$GgufFile = 'Qwen3-1.7B-Q4_K_M.gguf'
$OllamaModel = 'qwen3:1.7b'
$Log = Join-Path $Here 'narrator.log'
$Started = @()

function Say($m) { Write-Host "[narrator] $m" -ForegroundColor Yellow }
function Has($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function VenvPython {
  $py = Join-Path $Venv 'Scripts\python.exe'
  if (-not (Test-Path $py)) {
    Say "creating Python environment in $Venv"
    & python -m venv $Venv
    & $py -m pip install --quiet --upgrade pip
  }
  return $py
}
function Download($url, $dest, $label) {
  if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 0) { return }
  New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
  Say "downloading $label (once)"
  for ($a = 1; $a -le 5; $a++) {
    # curl.exe ships with Windows 10+; -C - resumes an interrupted download.
    & curl.exe -fL -C - --progress-bar -o "$dest.part" $url
    if ($LASTEXITCODE -eq 0) { Move-Item -Force "$dest.part" $dest; return }
    Say "download interrupted, resuming ($a/5)..."
    Start-Sleep 3
  }
  throw "Could not download $url"
}
function Up($url) {
  try { Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 "$url/v1/models" | Out-Null; return $true } catch { return $false }
}

$Url = "http://127.0.0.1:$Port"
$ModelName = 'local'

try {
  if (Has 'llama-server') {
    Say "backend: llama.cpp (llama-server) on :$Port"
    $Started += Start-Process llama-server -PassThru -WindowStyle Hidden -RedirectStandardError $Log `
      -ArgumentList @('-hf', "${GgufRepo}:Q4_K_M", '--port', $Port, '--host', '127.0.0.1', '--jinja', '--reasoning-budget', '0')
  }
  elseif (Has 'ollama') {
    $Url = 'http://127.0.0.1:11434'; $ModelName = $OllamaModel
    Say "backend: Ollama ($OllamaModel)"
    if (-not (Up $Url)) { $Started += Start-Process ollama -ArgumentList 'serve' -PassThru -WindowStyle Hidden; Start-Sleep 2 }
    & ollama pull $OllamaModel
  }
  elseif (Has 'python') {
    $py = VenvPython
    & $py -c "import llama_cpp.server" 2>$null
    if ($LASTEXITCODE -ne 0) {
      Say 'installing llama-cpp-python (CPU)'
      & $py -m pip install --quiet --only-binary=:all: llama-cpp-python --extra-index-url https://abetlen.github.io/llama-cpp-python/whl/cpu
      if ($LASTEXITCODE -ne 0) { & $py -m pip install --quiet llama-cpp-python }
      & $py -m pip install --quiet uvicorn fastapi sse-starlette starlette-context pydantic-settings
    }
    $gguf = Join-Path $Models $GgufFile
    Download "https://huggingface.co/$GgufRepo/resolve/main/$GgufFile" $gguf "$GgufFile (~1.1 GB)"
    Say "backend: llama-cpp-python on :$Port"
    $Started += Start-Process $py -PassThru -WindowStyle Hidden -RedirectStandardError $Log `
      -ArgumentList @('-m', 'llama_cpp.server', '--model', $gguf, '--host', '127.0.0.1', '--port', $Port, '--n_ctx', '2048')
  }
  else {
    throw 'No backend found. Install llama.cpp (winget install ggml.llamacpp), Ollama, or Python 3.'
  }

  Say "waiting for $Url (first run loads the model)..."
  for ($i = 0; $i -lt 180 -and -not (Up $Url); $i++) {
    if ($Started.Count -gt 0 -and $Started[0].HasExited) { Get-Content $Log -Tail 20; throw 'The model server exited.' }
    Start-Sleep 2
  }
  if (-not (Up $Url)) { throw "Server did not come up; see $Log" }
  Say "hero voices online at $Url (model: $ModelName)"

  $gameArgs = @('-narrator', $Url, '-narrator-model', $ModelName)

  $VoiceUrl = "http://127.0.0.1:$VoicePort"
  $speech = -not $NoSpeech
  if ($speech -and -not (Up $VoiceUrl)) {
    if (Has 'python') {
      $py = VenvPython
      & $py -c "import kokoro_onnx" 2>$null
      if ($LASTEXITCODE -ne 0) { Say 'installing kokoro-onnx (text-to-speech)'; & $py -m pip install --quiet kokoro-onnx }
      $kb = 'https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0'
      $onnx = Join-Path $Models 'kokoro-v1.0.onnx'; $vbin = Join-Path $Models 'voices-v1.0.bin'
      Download "$kb/kokoro-v1.0.onnx" $onnx 'Kokoro voice model (~310 MB)'
      Download "$kb/voices-v1.0.bin" $vbin 'Kokoro voices (~27 MB)'
      Say "starting voices on :$VoicePort"
      $Started += Start-Process $py -PassThru -WindowStyle Hidden -RedirectStandardError (Join-Path $Here 'voice.log') `
        -ArgumentList @((Join-Path $Here 'voice_server.py'), '--model', $onnx, '--voices', $vbin, '--port', $VoicePort)
      for ($i = 0; $i -lt 60 -and -not (Up $VoiceUrl); $i++) { Start-Sleep 2 }
    }
    if (-not (Up $VoiceUrl)) { Say 'voices did not start (needs Python 3; see voice.log) - text lines only'; $speech = $false }
  }
  if ($speech) { Say "voices online at $VoiceUrl"; $gameArgs += @('-voice', $VoiceUrl) }
  if ($Laya) {
    if (Has 'laya-serve') {
      Say 'starting Laya decisions on :8765'
      $env:LAYA_HOST = '127.0.0.1'; $env:LAYA_PORT = '8765'; $env:LAYA_MODELS = 'english'
      $Started += Start-Process laya-serve -PassThru -WindowStyle Hidden
      $gameArgs += '-laya'
    } else { Say 'Laya: run  pip install "laya[serve]"  first (PyTorch; GPU recommended).' }
  }

  if (-not $NoGame) {
    foreach ($exe in @('Builds\WindowsPlaytest\SolarMajesty.exe', 'Builds\Windows\SolarMajesty.exe')) {
      $path = Join-Path $Root $exe
      if (Test-Path $path) {
        Say "launching $exe"
        Start-Process $path -ArgumentList $gameArgs -Wait
        return
      }
    }
    Say 'no built game found (Solar Majesty -> Build -> Windows).'
  }
  if ($speech) { Say "Playing in the Unity editor? Settings -> HERO LINES - LOCAL LLM and SPOKEN - LOCAL TTS (uses $Url and $VoiceUrl)." }
  else { Say "Playing in the Unity editor? Settings -> HERO LINES - LOCAL LLM (uses $Url)." }
  if ($Url -ne 'http://127.0.0.1:8080') { Say "Editor note: set SOLAR_NARRATOR_URL=$Url before starting Unity, or use llama.cpp." }
  Say 'Server running. Press Ctrl+C to stop.'
  while ($true) { Start-Sleep 3600 }
}
finally {
  foreach ($p in $Started) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}
