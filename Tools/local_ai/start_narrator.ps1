<#
  Starts a local LLM for Solar Majesty's hero voices, then (optionally) the game.

    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1            # server + built game
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoGame    # server only (Unity editor)
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -Laya      # also Laya decisions (PyTorch)
    powershell -ExecutionPolicy Bypass -File Tools\local_ai\start_narrator.ps1 -NoVoices  # text only, no spoken lines

  Backend order: llama.cpp (llama-server) -> Ollama -> Python fallback (llama-cpp-python, CPU).
  Model: Qwen3-1.7B 4-bit (~1.1 GB). If Tools\audio is set up, also starts the Kokoro voice server on
  :8081 so LLM lines are spoken. Closing this window stops the servers. See Docs/HERO_NARRATION.md, Docs/AUDIO.md.
#>
param([switch]$NoGame, [switch]$Laya, [switch]$NoVoices, [int]$Port = 8080, [int]$VoicePort = 8081)
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
    New-Item -ItemType Directory -Force $Models | Out-Null
    $gguf = Join-Path $Models $GgufFile
    if (-not (Test-Path $gguf) -or (Get-Item $gguf).Length -eq 0) {
      Say "downloading $GgufFile (~1.1 GB, once)"
      $ProgressPreference = 'SilentlyContinue'
      Invoke-WebRequest -UseBasicParsing "https://huggingface.co/$GgufRepo/resolve/main/$GgufFile" -OutFile "$gguf.part"
      Move-Item -Force "$gguf.part" $gguf
    }
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
  if ($Laya) {
    if (Has 'laya-serve') {
      Say 'starting Laya decisions on :8765'
      $env:LAYA_HOST = '127.0.0.1'; $env:LAYA_PORT = '8765'; $env:LAYA_MODELS = 'english'
      $Started += Start-Process laya-serve -PassThru -WindowStyle Hidden
      $gameArgs += '-laya'
    } else { Say 'Laya: run  pip install "laya[serve]"  first (PyTorch; GPU recommended).' }
  }

  if (-not $NoVoices) {
    $Audio = Join-Path $Root 'Tools\audio'
    $AudioPy = Join-Path $Audio '.venv\Scripts\python.exe'
    $VoiceUrl = "http://127.0.0.1:$VoicePort"
    if ((Test-Path $AudioPy) -and (Test-Path (Join-Path $Audio 'models\kokoro-v1.0.int8.onnx'))) {
      Say "starting spoken lines (Kokoro TTS) on :$VoicePort"
      $Started += Start-Process $AudioPy -ArgumentList @('voice_server.py', '--port', $VoicePort) -WorkingDirectory $Audio `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Here 'voice.log') -RedirectStandardError (Join-Path $Here 'voice.err.log')
      $voiceUp = $false
      for ($i = 0; $i -lt 60 -and -not $voiceUp; $i++) {
        try { Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 "$VoiceUrl/health" | Out-Null; $voiceUp = $true } catch { Start-Sleep 1 }
      }
      if ($voiceUp) { Say "spoken lines online at $VoiceUrl"; $gameArgs += @('-voice-server', $VoiceUrl) }
      else { Say "voice server did not come up; lines stay text (see $Here\voice.err.log)" }
    } else { Say 'spoken LLM lines: set up Tools\audio once (see Docs\AUDIO.md); baked barks work without it.' }
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
  Say "Playing in the Unity editor? Settings -> HERO VOICES - LOCAL AI (it uses $Url)."
  if ($Url -ne 'http://127.0.0.1:8080') { Say "Editor note: set SOLAR_NARRATOR_URL=$Url before starting Unity, or use llama.cpp." }
  Say 'Server running. Press Ctrl+C to stop.'
  while ($true) { Start-Sleep 3600 }
}
finally {
  foreach ($p in $Started) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }
}
