# Steam release guide (Early Access, Windows)

What the code already does, what only you can do on the Steamworks site, and in what order.
Target: **Steam Early Access, Windows 10/11 64-bit**, local AI pack in the base depot.

## Already in the game

| Area | Where | Notes |
|---|---|---|
| Steam API startup | [SteamService.cs](../Assets/Scripts/Runtime/Platform/SteamService.cs) | Optional at runtime: no client, no DLL or `-nosteam` → game plays offline |
| App ID | [SteamConfig.cs](../Assets/Scripts/Systems/Platform/SteamConfig.cs) | **480 (Valve's test app) until you swap it** |
| Achievements | `Achievements.Earned` → `SetAchievement` + `StoreStats` | Offline unlocks are pushed on the next Steam start |
| Achievement sheet | [Tools/steam/achievements.csv](../Tools/steam/achievements.csv) | 12 achievements, API names `ACH_*` |
| Overlay | `GameOverlayActivated_t` | Opening the overlay pauses the colony |
| Rich presence | [Tools/steam/rich_presence_english.vdf](../Tools/steam/rich_presence_english.vdf) | "Overseeing a colony on Mars" |
| Cloud-ready saves | [GamePrefs.cs](../Assets/Scripts/Systems/GamePrefs.cs), [SaveSystem.cs](../Assets/Scripts/Systems/SaveSystem.cs) | Campaign progress moved out of the registry into files |
| Upload | [Tools/steam/upload_to_steam.ps1](../Tools/steam/upload_to_steam.ps1) | Stages game + `ai\`, writes SteamPipe VDFs, runs steamcmd |
| No cheats in release | [DevTools.cs](../Assets/Scripts/Systems/DevTools.cs) | F10 / Shift+click unlocks only in editor, dev builds or `-devtools` |
| Error log | [ErrorLog.cs](../Assets/Scripts/Systems/ErrorLog.cs) | `Logs\errors-*.log`, Settings → Open Logs Folder |
| Live-AI guardrail | [AiContentGuard.cs](../Assets/Scripts/Systems/Narration/AiContentGuard.cs) | Screens chat input, streaming text and replies |
| Build stamp | `build_info.txt` next to the exe | version, commit, time, Steam app |

## 1. Register (you)

1. Join Steamworks as a partner: <https://partner.steamgames.com/> (identity, bank and tax interview).
2. Pay the Steam Direct fee (US$100 per app, recoupable after US$1,000 gross).
3. Create the app. Note the **App ID** and the default **depot ID** (usually App ID + 1).
4. Create a **build account** with only *Edit App Metadata* and *Publish App Changes To Steam*. Never upload with the owner account.

Before you register the name, run a trademark search on **"Solar Majesty"**. *Majesty* is the title of
Paradox Interactive's series, and this game is openly inspired by it. A clearance check now costs far
less than a rename after launch.

Preliminary web check (2026-10-19): no game, product or trademark filing named "Solar Majesty"
turned up; the only hits were a T-shirt graphic and a cargo ship. That is not clearance. The risk
is "Majesty" itself: Paradox has owned the series IP since 2007 and still sells it on Steam in the
same genre. Before paying for the app, search USPTO (Trademark Search), EUIPO eSearch and the WIPO
Global Brand Database for MAJESTY in class 9/41, and ideally get a one-off opinion from a trademark
attorney. Keep the store copy free of comparisons to *Majesty* (it currently has none).

## 2. Swap in the real App ID (code, 2 minutes)

In `Assets/Scripts/Systems/Platform/SteamConfig.cs` change `FullGameAppId = TestAppId` to your App ID
(and `DemoAppId` once the demo app exists, see [section 7](#7-next-fest-demo)).
`RelaunchThroughSteam` turns itself on for any ID other than 480. Then rebuild.
`SteamConfigTests` keeps the 480 safety rule honest for both the game and the demo.

## 3. Steamworks app configuration

**Installation → General:** executable `SolarMajesty.exe`, OS Windows 64-bit. Launch option 1: no arguments.
Optional launch option 2, "Play without local AI": arguments `-noai`.

**Depots:** one Windows depot holds the game and the `ai\` folder (about 4 GB).

**Steam Cloud → Auto-Cloud** (byte quota 200 MB, file count 200):

| Root | Subdirectory | Pattern | OS | Recursive |
|---|---|---|---|---|
| `WinAppDataLocalLow` | `SolarMajesty/Solar Majesty/Saves` | `*` | Windows | No |
| `WinAppDataLocalLow` | `SolarMajesty/Solar Majesty/Profile` | `*` | Windows | No |

Do **not** sync `Logs`, `Playtest` or `ai` (they are machine-local). Resolution, fullscreen, quality and the
frame cap deliberately stay in the registry so they do not follow the player to a different monitor.

**Stats & Achievements:** add the 12 rows of `Tools/steam/achievements.csv` with the exact API names.
`ACH_IRONMAN` is hidden. Each needs a 256×256 icon (achieved) and a greyed version (unachieved).
Publish the stats/achievements config. Achievements set while the config is unpublished do nothing.

**Rich Presence:** Community → Rich Presence → upload `Tools/steam/rich_presence_english.vdf`.

**Steam Input:** the game reads XInput pads itself (left stick moves the cursor, A/B click, right stick
pans, triggers zoom; layout in Settings → Controller). Under Steam Input → set the default configuration to
the **Gamepad** template and leave "Steam Input" opt-in off, so Steam passes the pad through as an Xbox
controller. Claim **Partial Controller Support** until someone has played a full hour on a pad and a Deck.

**Steam Deck:** detected through Steamworks. It starts in controller mode, keeps the HUD at 100 % at
1280×800 (every font at least 9 px), picks the Medium tier, and opens Steam's floating keyboard for hero
chat and flag orders. Preview the layout on a desktop with `-handheld` ([1280×800 capture](ReviewEvidence/steam-deck-1280x800-handheld.png)).
Before submitting for Deck review, play the first hour on a real Deck: cursor speed, text size and the
on-screen keyboard are the parts automated tests cannot judge.

## 4. Build and upload

```powershell
# Release player (writes build_info.txt and steam_appid.txt next to the exe)
& "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -quit -projectPath . `
  -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindows -smBuildPath Builds\SteamRC -smVersion 0.9.0

# AI pack (see BETA_PACKAGE.md). Set -SourceOfferContact to your support email or URL.
powershell -ExecutionPolicy Bypass -File Tools\local_ai\build_ai_pack.ps1 -OutDir Builds\Beta\stage `
  -LlamaZip <llama-bNNNN-bin-win-cpu-x64.zip> -SourceOfferContact support@example.com

# SteamPipe (leaves steam_appid.txt and debug symbols out; lands on no branch until you set it live)
powershell -ExecutionPolicy Bypass -File Tools\steam\upload_to_steam.ps1 -GameDir Builds\SteamRC\Windows `
  -AiDir Builds\Beta\stage\ai -AppId <APP_ID> -DepotId <DEPOT_ID> -SteamUser <build_account> -Description "EA 0.9.0 RC1"
```

Set the build live on a private `beta` branch first, install it through the Steam client on a clean PC,
and run the smoke list below before you touch `default`.

## 5. Store page and surveys (you)

**Assets** (exact pixel sizes, PNG/JPG):

| Asset | Size |
|---|---|
| Header capsule | 920×430 |
| Small capsule | 462×174 |
| Main capsule | 1232×706 |
| Vertical capsule | 748×896 |
| Library capsule | 600×900 |
| Library hero | 3840×1240 (no text) |
| Library logo | 1280×720 PNG with transparency |
| Page background | 1438×810 (optional) |
| Screenshots | at least 5, 1920×1080, gameplay only: nine ready in [Docs/StoreScreenshots](StoreScreenshots/README.md) |
| Trailer | recommended; gameplay first in the first 5 seconds |

No third-party marks anywhere in these: no SpaceX, Starship, xAI or Grok names or logos.
All eight graphics, plus the community icon (184×184 JPG) and the client icon (`.ico`), are ready
in [Docs/StoreArt](StoreArt/README.md), with where each one goes; rebuild them with
`Tools/steam/make_store_art.py`.

**Content survey: AI-generated content disclosure** (required). An honest starting point:

> *Pre-generated:* Some concept art and reference sheets used to design units and buildings were made with
> AI image tools, then rebuilt as original 3D models. Character voice barks are synthesised offline with
> the open Kokoro-82M text-to-speech model.
> *Live-generated (optional, off by default):* When the player turns on "Hero voices · local AI", short
> hero lines and chat replies are generated on the player's own PC by a bundled open model (Qwen3,
> Apache-2.0). Nothing is sent to a server. Guardrails: a content rule in every prompt, a filter on the
> player's chat input, on streaming text and on finished replies (inappropriate output is discarded and
> the hero falls back to pre-written lines), replies capped to one or two sentences, and the feature can
> be switched off in Settings or with the `-noai` launch option.

Check the art history before you submit. If any shipped texture or model is still the direct output of
an image or 3D generator, say so under pre-generated content.

**Rating:** complete the IARC questionnaire (fantasy violence against creatures and robots; user-to-AI
text chat with filtering; no real-money gambling).

**Early Access questionnaire:** what is in now (five worlds Earth → Europa, guided start, doctrines,
challenges, Ironman, achievements), what is planned (from [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)),
how long EA lasts, and whether the price will change.

**System requirements** (fill in only after the profiling pass; the AI pack needs 6 GB of RAM for lines
and speech, 11 GB for chat as well): Windows 10 64-bit, DirectX 12 GPU.

**Legal:** Steam's standard Subscriber Agreement is enough as the EULA. Add a short privacy note:
the game collects nothing; playtest telemetry and error logs stay on the player's PC.

## 6. Smoke list for each release candidate (clean Windows PC, installed through Steam)

1. Starts from the Steam library. The overlay opens with Shift+Tab, and opening it pauses the colony.
2. Friends list shows rich presence ("At the title screen", then "Overseeing a colony on Earth").
3. New Game → guided Earth start → place a workshop → post a bounty → a robot accepts it.
4. Earn **Terms Accepted** (first completed bounty). The Steam toast appears.
5. Save, quit, start on a second PC with the same account: Continue restores the colony and campaign.
6. F10 and Shift+click on a locked world do nothing; locked worlds say "LOCKED" only.
7. Settings → Open Logs Folder opens `...\LocalLow\SolarMajesty\Solar Majesty\Logs`.
8. Hero voices · local AI on: a hero speaks a generated line within about a minute. Then start with
   `-noai`: no `llama-server` process appears.
9. Unplug the network and start from the exe with Steam closed: the game still plays (achievements wait).

## 7. Next Fest demo

The demo is a **separate player** and, on Steam, a **separate app**. It plays Earth to its win and then
shows an end-of-demo screen: **Wishlist on Steam** (the full game's store page in the Steam overlay, or the
browser without Steam), **Keep playing Earth**, **Quit**. What it locks and why is in
[DEMO.md → Steam demo build](DEMO.md#steam-demo-build-next-fest).

**What the code already does**

| Area | Where | Notes |
|---|---|---|
| Demo build | `BuildCommands.BuildWindowsDemo` ([BuildCommands.cs](../Assets/Scripts/Editor/BuildCommands.cs)) | `SM_DEMO` via `extraScriptingDefines` for that build only; product name "Solar Majesty Demo", `SolarMajestyDemo.exe`, bundle id `…solarmajesty.demo`, all put back afterwards. ProjectSettings never change |
| Demo rules | [DemoBuild.cs](../Assets/Scripts/Systems/Platform/DemoBuild.cs), [DemoEndOffer.cs](../Assets/Scripts/Systems/DemoEndOffer.cs) | Earth only, end screen, no Solar Conquest / Ironman, own folders. Tested in `DemoBuildTests` |
| Demo App ID | `SteamConfig.DemoAppId` | **480 placeholder (TODO)**. The demo player reads it; `steam_appid.txt` and `build_info.txt` carry it |
| Wishlist target | `SteamConfig.StorePageUrl` | Always the **full game's** page (`FullGameAppId`), never the demo's |
| Saves | `LocalLow\SolarMajesty\Solar Majesty Demo\DemoSaves`, `…\DemoProfile` | Can never collide with the game's `Saves` / `Profile` |
| Achievements | local only in the demo | The demo app has none on the partner site; `SteamService` does not call `SetAchievement` there |
| Upload | `upload_to_steam.ps1 -Demo` | Checks for `SolarMajestyDemo.exe`, refuses the AI pack, stages into `Builds\SteamDemo` |

**On the partner site (you)**

1. Base game app → **All Associated Packages, DLC, Demos and Tools** → **Create new Demo**. Steam creates the
   demo app with its own **App ID** and a free package. Note the demo's **depot ID** (usually demo App ID + 1).
2. Put the IDs in code: `SteamConfig.DemoAppId` (and `FullGameAppId` if it is still 480). `SteamConfigTests`
   checks the demo ID differs from the game's and that a real demo ID never sits under a 480 game ID.
3. Demo app → **Installation → General**: executable `SolarMajestyDemo.exe`, Windows 64-bit, no arguments.
   No AI pack: the demo depot is the player only (a few hundred MB, not 4 GB).
4. Demo app → **Steam Cloud** (optional): Auto-Cloud root `WinAppDataLocalLow`, subdirectories
   `SolarMajesty/Solar Majesty Demo/DemoSaves` and `SolarMajesty/Solar Majesty Demo/DemoProfile`.
5. No achievements, stats or rich presence on the demo app. Leave them on the full game only.
6. Choose how the demo shows: the **Download Demo** button on the game's store page (default), and optionally
   its own demo store page. Fill in the demo's store presence and submit it for **review**; the demo build is
   reviewed too. Allow a few business days.
7. Build, stage and upload:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -quit -projectPath . `
  -executeMethod SolarMajesty.EditorTools.BuildCommands.BuildWindowsDemo -smBuildPath Builds\Demo -smVersion 0.9.0 -logFile demo-build.log

powershell -ExecutionPolicy Bypass -File Tools\qa\run_autoplay.ps1 -Demo -GameDir Builds\Demo\Windows   # Earth → end screen

powershell -ExecutionPolicy Bypass -File Tools\steam\upload_to_steam.ps1 -Demo -GameDir Builds\Demo\Windows `
  -AppId <DEMO_APP_ID> -DepotId <DEMO_DEPOT_ID> -SteamUser <build_account> -Description "Demo 0.9.0 Next Fest"
```

   Set it live on a private branch first, install it through Steam on a clean PC, play Earth to the end
   screen and press **Wishlist on Steam**: the overlay must open the **full game's** page.

**Next Fest timing.** Steam Next Fest runs three times a year (February, June and October, about a week each).
Valve's rules change between editions, so read the current *Steam Next Fest* page under Steamworks →
*Upcoming Events* before planning. As of writing, the rules that shape our schedule are:

- **Register** on that page before its deadline, typically about two months ahead of the event.
- The game must be **unreleased**, with a **public Coming Soon store page**. A game takes part in **one** Next
  Fest only, so do not spend it on a rough demo.
- The demo must be **approved and live** before the event, in time for the press preview (around a week
  before), and stay available for the whole festival.
- Valve recommends launching the demo shortly **before** the fest rather than months earlier, because the
  festival traffic is what the demo is for. Expect most wishlists in the festival week itself.
- Budget for the demo store review, the build review, the upload, and a clean-PC install check: about **three
  weeks** before the press preview.

**Importing demo progress later (not built).** The demo writes its own `DemoProfile\prefs.json` (Earth's
conquest bit, tutorial done, settings) and `DemoSaves\` (save slots plus Earth's `world0.json`). The full game could offer "continue from
the demo" by reading those from `..\Solar Majesty Demo\` on first launch. The save format is the same
versioned `SaveSystem` file, so the import would be a copy plus `CampaignProgress.MarkConquered(Earth)`.
Nothing does this yet.

## Still open before Early Access

These are tracked in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) and are not solved by the Steam work:

- The uncoached 45–90 minute external playtest (the largest open risk).
- A profiling pass on minimum-spec hardware, then the system requirements.
- Upload the store and library art ([Docs/StoreArt](StoreArt/README.md)) and give it a human artist pass.
- Controller support (leave it unclaimed until done).
- Make the GitHub repository private. It contains Unity Asset Store packages (BOXOPHOBIC, Kevin Iglesias,
  Polytope Studio) whose license does not allow public redistribution of their source files.
