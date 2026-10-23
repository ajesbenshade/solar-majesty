<#
  Stages a Windows build (plus the bundled local-AI pack) and uploads it to Steam with SteamPipe.

    powershell -ExecutionPolicy Bypass -File Tools\steam\upload_to_steam.ps1 `
        -GameDir Builds\Steam\Windows -AiDir Builds\Beta\stage\ai `
        -AppId 1234560 -DepotId 1234561 -SteamUser my_build_account -Description "EA 0.9.0 RC1"

  What it does:
    1. Copies the game into Builds\Steam\content (leaving out Burst debug symbols and any
       steam_appid.txt; Steam supplies the app ID when it launches the game).
    2. Copies the ai folder in beside the exe, as LocalAiHost expects.
    3. Writes app_build_<app>.vdf and depot_build_<depot>.vdf into Builds\Steam\scripts.
    4. Runs steamcmd +run_app_build. Steam Guard will prompt the first time.
    5. With -Preview, it builds the manifests without uploading, to check file lists.

  The build lands on the branch named by -SetLive (default: none, so nothing reaches players
  until you set it live on the partner site). Never pass "default" here for a release candidate.

  Needs steamcmd (https://developer.valvesoftware.com/wiki/SteamCMD). Pass -SteamCmd if it is
  not on PATH. Use a dedicated build account with only the "Edit App Metadata" and
  "Publish App Changes To Steam" permissions, never the owner account.
#>
param(
  [Parameter(Mandatory = $true)][string]$GameDir,
  [string]$AiDir,
  [Parameter(Mandatory = $true)][uint32]$AppId,
  [Parameter(Mandatory = $true)][uint32]$DepotId,
  [string]$SteamUser,
  [string]$SteamCmd = 'steamcmd',
  [string]$Description = "Solar Majesty $(Get-Date -Format 'yyyy-MM-dd HH:mm')",
  [string]$SetLive = '',
  [string]$WorkDir = 'Builds\Steam',
  [switch]$Preview
)
$ErrorActionPreference = 'Stop'
function Say($m) { Write-Host "[steam] $m" -ForegroundColor Yellow }


$GameDir = (Resolve-Path $GameDir).Path
if (-not (Test-Path (Join-Path $GameDir 'SolarMajesty.exe'))) { throw "No SolarMajesty.exe in $GameDir" }
if (-not (Test-Path (Join-Path $GameDir 'steam_api64.dll')) -and
    -not (Get-ChildItem $GameDir -Recurse -Filter 'steam_api64.dll' -ErrorAction SilentlyContinue)) {
  throw "steam_api64.dll is missing from $GameDir. Rebuild with the Steamworks.NET package installed."
}
if ($AiDir) {
  $AiDir = (Resolve-Path $AiDir).Path
  foreach ($need in @('llama', 'models')) {
    if (-not (Test-Path (Join-Path $AiDir $need))) { throw "AI pack at $AiDir has no $need folder (build it with Tools\local_ai\build_ai_pack.ps1)." }
  }
}

New-Item -ItemType Directory -Force $WorkDir | Out-Null
$WorkDir = (Resolve-Path $WorkDir).Path
$Content = Join-Path $WorkDir 'content'
$Scripts = Join-Path $WorkDir 'scripts'
$Output = Join-Path $WorkDir 'output'

Say "staging content in $Content"
if (Test-Path $Content) { Remove-Item $Content -Recurse -Force }
New-Item -ItemType Directory -Force $Content, $Scripts, $Output | Out-Null

# robocopy exit codes below 8 mean success.
& robocopy $GameDir $Content /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 `
  /XD '*_BurstDebugInformation_DoNotShip' '*_BackUpThisFolder_ButDontShipItWithYourGame' 'ai' `
  /XF 'steam_appid.txt' '*.pdb' | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy (game) failed with $LASTEXITCODE" }

if ($AiDir) {
  & robocopy $AiDir (Join-Path $Content 'ai') /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 /XF '*.log' | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy (ai) failed with $LASTEXITCODE" }
}

$size = (Get-ChildItem $Content -Recurse -File | Measure-Object Length -Sum).Sum
Say ("content: {0:N2} GB" -f ($size / 1GB))

$depotVdf = Join-Path $Scripts "depot_build_$DepotId.vdf"
@"
"DepotBuild"
{
	"DepotID" "$DepotId"
	"ContentRoot" "$Content"
	"FileMapping"
	{
		"LocalPath" "*"
		"DepotPath" "."
		"Recursive" "1"
	}
	"FileExclusion" "*.pdb"
	"FileExclusion" "steam_appid.txt"
	"FileExclusion" "ai\\*.log"
}
"@ | Set-Content -Encoding UTF8 $depotVdf

$appVdf = Join-Path $Scripts "app_build_$AppId.vdf"
$desc = $Description.Replace('"', "'")
@"
"AppBuild"
{
	"AppID" "$AppId"
	"Desc" "$desc"
	"Preview" "$(if ($Preview) { '1' } else { '0' })"
	"SetLive" "$SetLive"
	"ContentRoot" "$Content"
	"BuildOutput" "$Output"
	"Depots"
	{
		"$DepotId" "$depotVdf"
	}
}
"@ | Set-Content -Encoding UTF8 $appVdf

Say "wrote $appVdf"
if (-not $SteamUser) {
  Say 'no -SteamUser given: scripts are ready, upload skipped.'
  Say "upload later with: $SteamCmd +login <user> +run_app_build `"$appVdf`" +quit"
  return
}

Say "uploading as $SteamUser$(if ($Preview) { ' (preview only)' })"
& $SteamCmd +login $SteamUser +run_app_build $appVdf +quit
if ($LASTEXITCODE -ne 0) { throw "steamcmd failed with $LASTEXITCODE. Logs: $Output" }
Say "done. Set the build live on a beta branch from the partner site (Builds tab) and test it there first."
