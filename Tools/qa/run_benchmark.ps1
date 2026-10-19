<#
  Runs the built game's -benchmark mode against a throwaway profile and prints the result.

    powershell -ExecutionPolicy Bypass -File Tools\qa\run_benchmark.ps1 -GameDir Builds\SteamRC\Windows
    ... -Bodies Earth,Mars -Seconds 60 -Width 1920 -Height 1080

  Every world gets a fresh colony; the game sweeps the camera while the simulation runs at speed,
  then writes Logs\benchmark-*.json under the real persistent data folder and quits.
  Exit code 0 = all worlds ran without an exception. Your saves and settings are never touched:
  -saveDir and -profileDir point at a temp folder that is deleted afterwards.

  Run it on the slowest PC you want to support, with the window in front, to fill in the
  store page's minimum requirements.
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$Bodies = 'Earth,Luna,Mars,Belt,Europa',
  [int]$Seconds = 45,
  [int]$Speed = 7,
  [int]$Width = 1920,
  [int]$Height = 1080,
  [switch]$Fullscreen,
  [switch]$KeepTemp
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path (Resolve-Path $GameDir).Path 'SolarMajesty.exe'
if (-not (Test-Path $exe)) { throw "No SolarMajesty.exe in $GameDir" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("sm-bench-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $tmp 'Saves'), (Join-Path $tmp 'Profile') | Out-Null
$logs = Join-Path $env:USERPROFILE 'AppData\LocalLow\SolarMajesty\Solar Majesty\Logs'
$before = @(Get-ChildItem $logs -Filter 'benchmark-*.json' -ErrorAction SilentlyContinue | ForEach-Object FullName)

$gameArgs = @('-benchmark', '-benchmarkBodies', $Bodies, '-benchmarkSeconds', $Seconds, '-benchmarkSpeed', $Speed,
  '-saveDir', (Join-Path $tmp 'Saves'), '-profileDir', (Join-Path $tmp 'Profile'),
  '-nosteam', '-noai', '-screen-width', $Width, '-screen-height', $Height,
  '-screen-fullscreen', $(if ($Fullscreen) { '1' } else { '0' }),
  '-logFile', (Join-Path $tmp 'Player.log'))

Write-Host "[bench] $Bodies, ${Seconds}s each, ${Width}x$Height" -ForegroundColor Yellow
$p = Start-Process $exe -ArgumentList $gameArgs -PassThru
$budget = 120 + ($Bodies.Split(',').Count * ($Seconds + 60))
if (-not $p.WaitForExit($budget * 1000)) {
  Stop-Process -Id $p.Id -Force
  throw "Benchmark did not finish in $budget s. Log: $(Join-Path $tmp 'Player.log')"
}

$report = Get-ChildItem $logs -Filter 'benchmark-*.json' -ErrorAction SilentlyContinue |
  Where-Object { $before -notcontains $_.FullName } | Sort-Object LastWriteTime | Select-Object -Last 1
Select-String -Path (Join-Path $tmp 'Player.log') -Pattern '\[Benchmark\]' | ForEach-Object { $_.Line }
if (-not $report) { throw "No report written. Log: $(Join-Path $tmp 'Player.log')" }

$j = Get-Content $report.FullName -Raw | ConvertFrom-Json
"{0} | {1} ({2} cores) | {3} ({4} MB) | {5} MB RAM | {6} | {7}" -f $j.version, $j.cpu, $j.cpuCores, $j.gpu, $j.gpuMemoryMb, $j.ramMb, $j.resolution, $j.quality
$j.worlds | Format-Table world, avgFps, p50Ms, p95Ms, p99Ms, maxMs, pctOver33Ms, robots, fauna, errors, note -AutoSize | Out-String
"report: $($report.FullName)"
if (-not $KeepTemp) { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue } else { "temp kept: $tmp" }
exit $p.ExitCode
