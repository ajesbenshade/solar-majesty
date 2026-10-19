<#
  Runs the PlayMode suite (campaign smoke, developed-colony save round trips, ...) without touching
  your saves or settings.

    powershell -ExecutionPolicy Bypass -File Tools\qa\run_playmode_isolated.ps1
    ... -Filter CampaignSmokeTests          # one fixture
    ... -Ref claude/steam-release           # test a branch instead of HEAD

  Those tests start New Games and delete saves, so they refuse to run unless the project's company
  name is "SolarMajestyValidation" (or CI=true). This script keeps a separate git worktree next to
  the repo (..\<repo>-validation), checks out the commit under test, renames the company/product
  there only, and runs Unity on it. Unity's data for that copy lives under
  LocalLow\SolarMajestyValidation, apart from the real game's.

  The first run imports the whole project into the worktree's Library (several minutes).
  Later runs reuse it. Exit code: 0 when every test passed.
#>
param(
  [string]$Ref = 'HEAD',
  [string]$Filter,
  [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe',
  [string]$OutDir
)
$ErrorActionPreference = 'Stop'
function Say($m) { Write-Host "[playmode] $m" -ForegroundColor Yellow }

$repo = (git rev-parse --show-toplevel).Trim()
$commit = (git rev-parse $Ref).Trim()
$wt = Join-Path (Split-Path $repo -Parent) ((Split-Path $repo -Leaf) + '-validation')
if (-not $OutDir) { $OutDir = Join-Path $wt 'TestOutput' }

if (-not (Test-Path (Join-Path $wt '.git'))) {
  Say "creating validation worktree at $wt"
  git worktree add --detach $wt $commit | Out-Null
} else {
  Say "updating validation worktree to $($commit.Substring(0, 8))"
  git -C $wt checkout -- ProjectSettings/ProjectSettings.asset 2>$null
  git -C $wt checkout --detach --force $commit | Out-Null
}

$ps = Join-Path $wt 'ProjectSettings\ProjectSettings.asset'
$text = [IO.File]::ReadAllText($ps)
$text = $text -replace '(?m)^  companyName: .*$', '  companyName: SolarMajestyValidation' `
              -replace '(?m)^  productName: .*$', '  productName: StabilizationTests'
[IO.File]::WriteAllText($ps, $text, (New-Object Text.UTF8Encoding($false)))

New-Item -ItemType Directory -Force $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$results = Join-Path $OutDir "playmode-$stamp.xml"
$log = Join-Path $OutDir "playmode-$stamp.log"
$unityArgs = @('-batchmode', '-projectPath', $wt, '-runTests', '-testPlatform', 'PlayMode',
  '-testResults', $results, '-logFile', $log)
if ($Filter) { $unityArgs += @('-testFilter', $Filter) }

Say "running PlayMode tests on $($commit.Substring(0, 8))$(if ($Filter) { " ($Filter)" })"
$p = Start-Process $Unity -ArgumentList $unityArgs -PassThru -Wait
if (-not (Test-Path $results)) { throw "No results written (exit $($p.ExitCode)). Log: $log" }

[xml]$x = Get-Content $results
$run = $x.'test-run'
Say ("total {0}  passed {1}  failed {2}  skipped {3}" -f $run.total, $run.passed, $run.failed, $run.skipped)
$x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
  Write-Host ("  FAILED {0}`n    {1}" -f $_.fullname, ($_.failure.message.InnerText -split "`n")[0]) -ForegroundColor Red
}
Say "results: $results"
exit $(if ([int]$run.failed -eq 0 -and [int]$run.total -gt 0) { 0 } else { 1 })
