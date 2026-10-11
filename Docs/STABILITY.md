# Stability: automated tests and the session error log

What guards the build against crashes, leaks, save corruption and frame-time regressions, how to run it, and where a tester's logs end up.

---

## The tests

All PlayMode tests below **reset the campaign**. They run only when `Application.companyName == "SolarMajestyValidation"` (the isolated validation project) or the environment has `CI=true`. Otherwise they report *Ignored*, so a normal editor run never touches your colony. Saves, playtest telemetry and the error log go to a temp folder for the length of each test (`SaveSystem`, `PlaytestTelemetry` and `ErrorLog` `DirectoryOverride`). `DeveloperPrefsShield` puts your PlayerPrefs back afterwards.

The new PlayMode tests collect every error, exception and assert from any thread and fail once at the end with a deduplicated list. They do not stop at the first line. They set `LogAssert.ignoreFailingMessages` for the length of the test and restore it in teardown. No message is whitelisted.

| Test | File | Category | What fails it |
|---|---|---|---|
| `SoakTests.World_RunsAtSpeedWithoutErrorsNaNsStuckRobotsOrLeaks` (×5: Earth, Luna, Mars, Belt, Europa) | `Assets/Scripts/Tests/PlayMode/SoakTests.cs` | `Soak` | Any logged error/exception/assert. A NaN/∞ position on a robot, fauna or the camera. A robot stuck on a live path. GameObject count above `baseline × 2 + 1500`. Less than 95% of the requested sim time reached. |
| `SaveRoundTripTests.PlayedMarsColony_RoundTripsEveryFieldTwice_AndKeepsRunning` | `Assets/Scripts/Tests/PlayMode/SaveRoundTripTests.cs` | — | Any snapshot field that changes across save → scene reload → Continue → save (two rounds). Errors, or NaN poses, in the 30 s the restored colony then runs. |
| `PerfBudgetTests.DevelopedMars_FrameTimeAndGcStayInBudget` | `Assets/Scripts/Tests/PlayMode/PerfBudgetTests.cs` | `Perf` | Average or p95 frame time, or average GC allocation per frame, over budget. Any error. |
| `ErrorLogTests` (EditMode) | `Assets/Scripts/Tests/EditMode/ErrorLogTests.cs` | — | Ring buffer, dedupe, eviction, header, size cap, pruning, thread safety, write-on-error. |
| `SaveSnapshotDiffTests` (EditMode) | `Assets/Scripts/Tests/EditMode/SaveSnapshotDiffTests.cs` | — | The comparer used by the round-trip test: ignores order and metadata, tolerates nav-mesh pose snaps, reports lost state. |

### Soak

For each world:

1. Fresh campaign with every world unlocked. Boot into Earth, then hop with `GameLoop.SelectBody`.
2. **Starter colony.** An arriving world has a Commons and no robots. The test raises Engineer, Defense and Scout workshops, an inn and a market. It uses the village path (`TryFindVillagePlot` + `RaiseVillageBuilding`), fabricates each workshop's robot, and tops the treasury up to 1500 metals.
3. **Fast-forward.** The game runs at its fastest player speed (4×, `Time.timeScale` 2) with a fixed frame step (`Time.captureDeltaTime` 0.05 s). Each frame then advances 0.1 s of game time, which is 2 SimClock steps. The result is the same on any machine, and the step stays under SimClock's 8-step catch-up cap. If the game holds itself, the test resumes it and counts a *self-hold*. If it reloads the scene (a retry), the test picks up the new `GameLoop` and counts a *reload*. If it goes back to the title, the soak stops early. That is reported in the summary but is not a failure.
4. Every 5 s of sim time it checks for NaN poses and samples robots for the stuck check. Every 30 s it counts GameObjects. The object baseline is taken after a 10 s warm-up.

**Stuck heuristic (lenient on purpose).** A robot counts as stuck only when all of these hold on **every** sample for `SM_SOAK_STUCK_SECONDS` (default 120 s of sim time):

- It is alive, not down, not scrapped, and not resting.
- Its NavMeshAgent is on the mesh, not stopped, and has a pending path or a path with more than 2 m left.
- It has not moved more than 1 m.

Robots that are working, standing at the plaza, or in a shop stop their agent and clear the path, so they never count. Off-mesh samples (the robot is on the transform fallback mover) are counted in the summary but do not fail the test.

Every world logs one summary line. The numbers below only show the format; they are not a real run:

```
[Soak] Mars: sim 10.0 min in 74 s real over 6000 frames (×2.00); robots start 3 peak 4; fauna peak 9; objects base 5210 peak 5480 final 5422 (bound 11920); errors 0; warnings 3; stuck 0; NaN 0; off-mesh samples 0; self-holds 0; reloads 0
```

### Save round trip

`DevelopedColonySaveTests` already checks the fields of a hand-written fixture. This test grows a Mars colony through play instead: starter colony, then `SM_ROUNDTRIP_SECONDS` (default 180 s) of fast-forward. The snapshot therefore holds whatever the sim really produced: damaged buildings, half-built village projects, collectors, wandering fauna, cleared dens, research progress and robot purses.

It captures the snapshot, writes the world file and the autosave, reloads the scene, presses Continue and captures again. Then it does that a second time. `SaveSnapshotDiff` (`Assets/Scripts/Systems/SaveSnapshotDiff.cs`) compares:

- body, seed and campaign stamp
- sim steps
- stockpile, settlement, research, mission and replay state
- village growth
- collectors
- orbital cooldowns
- buildings, matched by category + cell: progress, health, levy, laser
- flags: bounty, escrow, work, claims
- robots, matched by class/level/xp/credits: health, fatigue, purse, downed state, level, xp, suit, revives, carried tax, claim, party, pose
- fauna: kind, den, health, pose
- nodes: remaining
- dens, in order: cleared, scouted, expansion
- parties
- roster

Before comparing, the expected side is adjusted for what Continue is designed to drop:

- fauna already dead at save time
- fauna beyond `OverseerRules.MaxFaunaPerDen` per den (`FaunaRestoreCap`)
- soft claims held by downed robots

Lists are compared as multisets. Poses may move up to 1.25 m because restore snaps them onto the nav mesh. Label, timestamps, play time and build stamp are ignored.

### Perf budget

The test grows a Mars colony for 60 s of sim time. It then holds a 30 fps step at 4× speed and measures `SM_PERF_FRAMES` frames (default 300):

- wall-clock frame time: average, p95 and max
- GC allocation per frame, from the `GC Allocated In Frame` profiler counter (heap-delta fallback if the counter is unavailable)

Default budgets are deliberately loose, because CI runners have no GPU and the editor adds overhead: **avg 50 ms, p95 120 ms, GC 512 KB/frame**. The test is a regression tripwire, not a frame-rate certificate. Every run logs a line like this (format only, not a real run):

```
[Perf] Mars frames=300 avgMs=11.42 p95Ms=18.90 maxMs=41.02 gcKbAvg=38.1 gcKbP95=52.7 gcSource=recorder robots=4 fauna=7 objects=5390 ...
```

Set `SM_PERF_OUT=/path/perf.jsonl` to append one JSON line per run, so CI can chart the trend.

---

## Running locally

**Unity Test Runner** (Window → General → Test Runner):

1. Open the validation project. Its Player Settings company name is `SolarMajestyValidation`; that is the profile the destructive tests accept. Alternatively, launch the editor with `CI=true` in the environment.
2. **EditMode** tab → Run All. That includes `ErrorLogTests` and `SaveSnapshotDiffTests`. Neither needs the validation profile.
3. **PlayMode** tab → pick `SoakTests`, `SaveRoundTripTests` or `PerfBudgetTests`, or filter by category `Soak` / `Perf`.

**Command line** (batch mode):

```bash
CI=true SM_SOAK_MINUTES=10 Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testCategory "Soak;Perf" -testResults results-playmode.xml -logFile -
```

Leave out `-testCategory` to run everything. Use `-testCategory "!Soak"` to skip the long soak on a quick pass.

### Environment variables

| Variable | Default | Effect |
|---|---|---|
| `CI` | — | `true` lets the destructive PlayMode tests run outside the validation project. |
| `SM_SOAK_MINUTES` | `10` | Sim minutes per world (minimum 0.5). Five worlds run, so real time is roughly 5 × minutes ÷ (speed-up). |
| `SM_SOAK_TIMESCALE` | `2` (4× player speed) | `Time.timeScale` during the soak and the colony growth phases. |
| `SM_SOAK_STUCK_SECONDS` | `120` | Sim seconds a robot must stay stuck on a live path before the soak fails (minimum 30). |
| `SM_SOAK_MAX_OBJECT_GROWTH` | `2` | Allowed GameObject growth ratio over the warm baseline. |
| `SM_SOAK_OBJECT_SLACK` | `1500` | Extra GameObjects allowed on top of the ratio (colonies legitimately grow). |
| `SM_ROUNDTRIP_SECONDS` | `180` | Sim seconds of play before the round-trip snapshot (minimum 30). |
| `SM_PERF_FRAMES` | `300` | Frames measured (minimum 60). |
| `SM_PERF_AVG_MS` | `50` | Average frame-time budget. |
| `SM_PERF_P95_MS` | `120` | 95th-percentile frame-time budget. |
| `SM_PERF_GC_KB` | `512` | Average GC allocation per frame budget, in KB. |
| `SM_PERF_OUT` | — | File that receives one JSON line per perf run. |

---

## Session error log (crash / bug report)

`ErrorLog` (`Assets/Scripts/Systems/ErrorLog.cs`, `ErrorLogBuffer.cs`) is booted before the first scene by `ErrorLogHook` (`Assets/Scripts/Runtime/ErrorLogHook.cs`, `[RuntimeInitializeOnLoadMethod]`). It is on in players and in editor Play mode.

- **What it keeps:** the most recent 256 distinct log lines. A repeated message bumps a count (`(x37, last 812.4s)`) instead of filling the buffer. When the buffer is full, the oldest non-error line goes first, so a session's first exception survives a later flood of info lines. Error stack traces are kept. Messages are capped at 2000 characters and stacks at 4000.
- **Header:** game version + build GUID, Unity version, platform and OS, device, GPU, RAM, start time (UTC), uptime, world, seed, sol, screen, and line/error/warning totals. World, seed, sol and screen refresh every second from the main thread.
- **When it writes:**
  - immediately on an error, exception or assert (at most every 0.5 s; later errors in a burst wait for the next write)
  - every 10 s when anything new arrived
  - when the app is paused (mobile)
  - on quit
  - on unhandled .NET exceptions (`AppDomain.UnhandledException`, also flushed) and unobserved task exceptions
  - when the bug-report chip is pressed

  Each write replaces the whole file through a temp file, so a crash mid-write leaves the previous complete copy.
- **Threading:** the hook uses `Application.logMessageReceivedThreaded`. `ErrorLog.Record` takes a lock and never calls `Debug.Log` (that would feed itself). The log folder is resolved on the main thread at boot, because `Application.persistentDataPath` throws off the main thread.
- **Where:** `Application.persistentDataPath/Logs/session-YYYYMMDD-HHMMSS-fff.log`. Each file is capped at **512 KB**, by dropping the oldest lines with a note. Only the newest **10** session files are kept.
  - Windows: `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Logs\`
  - macOS: `~/Library/Application Support/<Company>/<Product>/Logs/`
  - Linux: `~/.config/unity3d/<Company>/<Product>/Logs/`
- **Pause menu → REPORT A BUG · open logs:** writes the log now, copies the newest log's path to the clipboard (`GUIUtility.systemCopyBuffer`) and opens the folder (`Application.OpenURL("file:///…")`). The tester attaches that file. It sits next to `Playtest/session-*.jsonl` (telemetry) and Unity's own `Player.log`.
- **Tests** set `ErrorLog.DirectoryOverride` to a temp folder, the same way they redirect `SaveSystem`. The override is read at every write, so it takes effect mid-session.

Nothing is uploaded. Logs stay on the machine until the player sends them.
