# Solar Majesty — production readiness review

Reviewed September 21, 2026 (test execution September 22 UTC). Baseline: `6a8ea94` plus the working-tree changes present during review. This is an assessment, not a release certification.

## Verdict

**The Majesty-in-space premise is present in the code. The project is a substantial prototype, not a production-ready game.** The largest remaining risks are first-hour comprehension, reliable campaign/save behavior, and proof that the five worlds offer different strategic decisions. More feature breadth should wait for those risks to close.

The agreed first commercial scope is **Earth → Luna → Mars → Asteroid Belt → Europa**. The user confirmed this during review. Treat broader solar-system conquest as the longer-term vision; the five-world release should describe its actual destinations honestly.

Keep the current Unity project and its indirect-control foundation. A wholesale engine change or rewrite would discard useful work without resolving the central design questions. Preserve `SpecialistBrain` as the decision authority, the robot/human distinction, and the locked art direction. This review does not mark the existing Phase 4 exit complete or begin Phase 5 implementation.

## Evidence and limits

- Inspected the current gameplay, AI, economy, campaign, persistence, presentation, input, build, test, and planning code. Inventory: **158 C# files / 48,021 lines** under `Assets/Scripts`, including tests and editor tools. Counts are source inventory, not a quality score.
- Ran the real **Unity 6000.5.10f1 EditMode suite: 279 tests, 270 passed, 9 failed, 0 skipped**. Results: [test report](/Users/aaronesbenshade/solar-conquest/Docs/ReviewEvidence/2026-09-21-editmode.xml). There is no PlayMode test assembly in the inspected project. Passing unit tests do not establish campaign correctness.
- Opened the existing macOS player and observed the live solar-system title screen. Continue/Settings did not visibly respond to automated clicks, even after activating the window. This may be an automation/input limitation; it is **not** classified as a confirmed game defect. A live first-hour or full-campaign playthrough was not completed.
- Visually inspected archived `Docs/Roadmap/SM_Capture.png` and the locked spaced-campus concept. Archived imagery is not proof of the current build's appearance. The packaged player predates some working-tree edits and was not rebuilt during this review.
- The requested Bugbot-specific service was unavailable. A code-review subagent performed the fallback review; its first run was interrupted by the app update, and one recovery pass completed. Its three regressions below were verified by tracing code, not runtime reproduction.
- No external tester sessions, hardware profiles, Windows runtime results, remote CI outcomes, store account configuration, or asset purchase records were available as verified evidence. Existing documents explicitly leave external testing open.
- The working tree already contained many edits. No gameplay fixes were intentionally made. Unity rebuilt its ignored import cache. A pre-existing one-line modification in `ProjectSettings/ProjectSettings.asset` disappeared during the test session; the original line was not captured, so it was not guessed or restored. Review that setting from editor history if it was intentional. Future verification should use an isolated snapshot of the dirty tree.

## What is worth keeping

**The player already governs through incentives.** `SpecialistBrain` evaluates bounty, distance, risk, fatigue, courage and hunger, with fleeing, resting, hunting, repairs and vocation. The inspected input code does not need click-to-move to make the design work. This matches the key inspiration: Majesty's independent heroes are persuaded through indirect control. [Paradox's official description](https://www.paradoxinteractive.com/games/majesty-2-collection/about).

**There is a recognizable economic society.** Workshops fabricate robots; humans occupy HABs; heroes have purses and equipment; shops, guilds, levy delivery and revival costs can create meaningful decisions. Those systems are a stronger basis for the intended fantasy than adding more generic RTS commands.

**There is already a campaign backbone.** Five profiles, unlocks, launch research, procedural seeds, ecology, population/sustain gates, and arrival/victory text exist. Planets have differences in yields, logistics and hazards. These are useful ingredients, although their balance and distinctness remain unproven.

**There is useful technical groundwork.** Systems/runtime/editor assemblies, a fixed-step clock, ScriptableObject definitions, an extensive EditMode suite, save schemas, local playtest telemetry, and packaged players all exist. Improve their integration rather than replacing them reflexively.

## Confirmed code findings

Priorities here distinguish player impact from delivery urgency. P1 findings below concern save/campaign integrity; P2 findings still need correction before the affected feature ships. The three recent regressions retain the specialist review's P2 severity. All need runtime regression coverage.

### F01 — P1: loading mixes a global snapshot with separate per-body colony data

**Evidence:** [EnterPlaying](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:679), [ApplySave](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:4722), [RestoreCampus](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:5128), [AdvanceCampaign](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:3818).

Every Continue uses global autosave slot 0. `ApplySave` reads stockpile, research, fauna and mission from that snapshot but reconstructs campus and hero progression from current per-body PlayerPrefs. It does not select/validate the snapshot's body and seed, or rebuild the campus from `save.buildings`. Travel saves the origin and then opens the destination through the same loader. The origin mission state/population goal/fauna can therefore be applied to a different generated world; an origin Won state is particularly serious. Restoring a numbered snapshot is also not independent of subsequent preferences changes.

**Required change:** one authoritative, versioned campaign save containing explicit per-world state and stable entity IDs. Separate Continue, travel, retry and new-game transitions. Validate world identity before applying any state and do not overwrite a save after failed/partial restoration. Legacy preferences should be a one-time migration source.

**Acceptance:** save on Earth with partial construction/research, depleted nodes, damaged heroes and open escrow; visit Luna; return; quit/relaunch; load an older manual slot. Each state must match its own snapshot without importing the other world's mission or replacing progress with later preferences.

### F02 — P2, recent regression: demo filtering discards saved research progress

**Evidence:** [ResearchManager.cs:150](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/ResearchManager.cs:150), [RestoreFrom](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/ResearchManager.cs:95).

`CanSelect` now checks `DemoSlice.ShowTech`. Restore clears the active technology/progress and calls `TrySelect`, which uses that visibility gate. The new first-hour setting defaults on. Continuing an older campaign with partially researched Mars Ship or another hidden technology loses that active progress, then `EnterPlaying` immediately persists the result.

**Required change:** restore valid saved state independently of menu filtering; persist/restore the intended campaign mode. Validate partially researched hidden technology across upgrade, Continue, and toggling the demo setting.

### F03 — P2, recent regression: a slow reader can miss the tutorial's required refusal

**Evidence:** [FirstHourTutorial.cs:37](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/FirstHourTutorial.cs:37), [SpecialistAgent.cs:463](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/SpecialistAgent.cs:463), [SpecialistBrain.cs:220](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/SpecialistBrain.cs:220).

The tutorial requires a refused Build flag. A newly fabricated Engineer starts with hunger 0.55; vocation/wandering raises hunger by 0.012–0.018 per second. In roughly 12–17 seconds it can cross the 0.75 cheap-job bypass and accept the prescribed 70-CRED flag. The tutorial then instructs cancel/repost at the same price. Following those instructions repeats the mismatch. Completing work can alter hunger, and Skip exists, so this is not an unconditional permanent softlock.

**Required change:** accommodate legitimate acceptance and teach changing incentives through an observable outcome. Avoid a narrow timing window or an unexplained exception to AI rules. Validate immediate action, 30-second reading delay, high initial bounty, cancel/repost, save/Continue, Skip and replay. Preserve the brain scoring lock unless a separate design change is approved.

### F04 — P2, recent regression: fresh campaign arrivals can omit the free Commons

**Evidence:** [GameLoop.cs:705](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:705).

First arrival on an unbuilt destination has no restored campus. `!continuedColony` calls `PlaceFirstHourShell`, which returns outside the Earth demo. The missing-Commons fallback is an `else if`, so it cannot run in this case. The player must discover and buy a Commons manually for 70 CRED and 10 PWR before normal docking works. This is recoverable, but breaks the expected opening and consumes unplanned resources.

**Required change:** independently ensure the correct start condition for each transition and mode. Validate new Earth, first Luna, revisited Luna, retry, and fresh full-campaign starts.

### F05 — P2: ecology and defense-board time advance twice

**Evidence:** [Update](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:1679), [TickSimulation](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:1724), [ecology cooldown](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:3268).

`TickFlagInterest`, `TickCampusEcology` and `TickCampusBoard` run in both the fixed-step loop and the frame update. In ordinary play the dt-driven cooldowns receive approximately twice the intended elapsed time. Defense-board methods also mix frame-count work with simulation steps. This undermines tuning and the fixed-step guarantee; it is a pre-existing issue, separate from the latest regressions.

**Required change:** one owner for simulation time and separate presentation updates. Check equal-duration runs at 30/60/120 fps and all supported speeds; compare economic totals, ecology timings and defensive damage, not just clock arithmetic.

### F06 — P2: remapping and localization are disconnected scaffolds

**Evidence:** [InputBindings](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/InputBindings.cs:39), [runtime tool inputs](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/GameLoop.cs:3939), [Loc](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/Localization.cs:16).

A source scan found **68 literal runtime `Input.GetKey*` calls with `KeyCode`, zero runtime `InputBindings.` references and zero runtime `Loc.T(` calls**. Tests establish helper behavior, not usable rebinding or translated UI. Do not advertise these as completed player features.

**Required change:** route every exposed action through one binding map; derive hints from bindings; support reset/conflict handling and non-US keyboards. Extract user-facing strings during UI work. English-only release is acceptable if explicitly scoped; localization readiness is not the same as shipped translations.

### F07 — P2: local telemetry can produce invalid JSON on comma-decimal locales

**Evidence:** [PlaytestTelemetry.cs:61](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Systems/PlaytestTelemetry.cs:61).

The numeric `t` field uses current-culture `ToString("F1")` and is inserted unquoted. A comma-decimal culture produces invalid JSON such as `{"t":1,2,...}`. This can invalidate the playtest evidence used to tune the game.

**Required change:** use a JSON serializer or invariant numeric formatting with complete escaping; include build commit/session IDs. Validate logs under en-US, fr-FR and de-DE. Preserve local-only behavior unless collection is explicitly designed and disclosed.

### F08 — release gate: the test suite is red and CI uses a different editor

**Evidence:** [CI](/Users/aaronesbenshade/solar-conquest/.github/workflows/ci.yml:18) pins **6000.5.6f1**; [ProjectVersion](/Users/aaronesbenshade/solar-conquest/ProjectSettings/ProjectVersion.txt:1) pins **6000.5.10f1**. The local run used the latter.

All nine observed failures are in `CampusDressingTests`: three airlock white-color assertions, one HAB dark-band assertion, one dense-pack workshop docking assertion, and four tests encountering forbidden EditMode `Destroy` calls. The relevant destruction sites include [ColonyVisualUtility.cs:530](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/ColonyVisualUtility.cs:530) and [CampusDressing.cs:765](/Users/aaronesbenshade/solar-conquest/Assets/Scripts/Runtime/CampusDressing.cs:765). Do not weaken visual assertions automatically: resolve whether the expectation or implementation matches the accepted art direction.

The workflow has EditMode tests and scheduled/main-branch builds, but no player-smoke/PlayMode gate, and ordinary PR builds are skipped. Remote success was not verified.

**Required change:** align the editor pin, repair the nine failures, add high-value integration coverage, and produce identifiable release candidates from CI. Test and build artifacts must refer to the same commit and content revision.

## Gameplay assessment

**Majesty feel — promising, unvalidated.** Autonomous refusal, fear, shopping and social roles exist. A fixed “raise 70 to 90” puzzle teaches only a price threshold. The intended pleasure is learning what particular robots want and shaping a colony that makes their choices useful. Show intention, refusal reason, destination and outcome clearly. Give recurring heroes recognizable identities and a visible progression history. Players should remember an Engineer who kept a colony alive, not merely a moving class icon.

**Construction — useful strategic lever, high teaching burden.** Commons, airlocks, workshops, sites, power and bounty flags arrive together. Keep the prepared Earth shell, highlight valid dock faces, explain invalid placement at the cursor, and make the first completed job visibly affect the colony. Teach one new reason for a decision at a time.

**Economy — breadth exceeds demonstrated clarity.** Public funds, personal hero purses, escrow, tax delivery, resource production and gear can support the premise. `ResourceId.Metals` is still used internally for player-facing CRED; hints and freight text retain MET. Decide whether mining intentionally generates spendable currency and label it consistently. Keep public funds and personal wallets distinguishable. Show net income, reserved bounties, outstanding levy and the cost of recovery. Test viable recovery after a poor first purchase, a lost courier and a downed specialist.

**Campaign — connected destinations, not yet proven strategic variety.** All worlds use the same basic dens/sustain/launch pattern. Catalog modifiers are real, but changing population targets and colors alone would feel repetitive. Give each destination a dominant constraint and a mission payoff. Define what conquest means: securing a colony and logistics foothold is consistent with current systems; rival-empire warfare would be a large new game. The first release should choose the former.

**Progression and loss — needs a written contract.** Specify which heroes, research, buildings, stockpiles and objectives survive travel, retry, death and defeat. Maintain meaningful costs and a recoverable early game. A five-world campaign requires reliable resumability and a clear ending before additional replay systems matter.

**Replayability — already enough tools to test.** Existing doctrines, challenges, seeds and alternate economic paths should produce observed differences before adding factions, more classes, procedural quest systems or live LLM agents. A small set of good authored objectives is a safer first target than more system count.

## Presentation and accessibility assessment

The archived Mars image has recognizable landmarks and a coherent industrial vocabulary. At its capture scale, most HUD text is extremely small; saturated ground and large flat bright areas compete with the colony. The concept has stronger distance haze, material separation, ground contact and regional composition. These are observations of the archived image, not a current-render failure report.

The live title presents the solar-system premise clearly, but the menu occupies a tiny portion of an ultrawide window. Prioritize scalable typography, clear action hierarchy and click targets. Validate 1280×720, 1920×1080, 2560×1440, 3440×1440 and 4K with OS scaling; capture actual gameplay at each supported UI scale.

Respect the newer **smooth command-dome Commons** lock even though the older concept shows a lattice. Preserve spaced yards and readable landmarks. A screenshot-matching pass must not reverse later design decisions. Finish one representative Earth and Mars gameplay scene, then apply an asset checklist to other bodies. Required criteria: recognizable classes/fauna at normal zoom, readable threats/flags, no missing materials, stable shadows, limited visual noise and visible work/combat outcomes.

UI Toolkit exists alongside a 2,428-line IMGUI HUD. Migrate in bounded pieces around the player tasks: objectives, build/flag tools, specialist inspection, research, and pause/save/settings. Accessibility helpers are useful groundwork but need in-game verification. Ship readable text, non-color status cues, reduced motion, remapping and adjustable audio. Controller support and additional languages can be deferred with truthful platform/store claims.

Adaptive music, positional audio and generated voice/chirps exist in source. Sound quality, repetition, intelligibility and fatigue were not evaluated by listening. Author an audio acceptance pass with independent volume controls, subtitle/event equivalents and priority rules for simultaneous alerts. Avoid costly runtime generation if profiling shows a startup stall; do not infer performance solely from the code comment.

## Architecture, performance and operations

`GameLoop` is **6,266 lines**, and owns boot, world construction, input, campaign transitions, persistence, economy integration, presentation and numerous timed behaviors. The duplicate ticking and mixed save restoration show concrete consequences of this coupling. Extract responsibilities incrementally behind behavior tests: session transitions, save capture/restore, simulation scheduling, scenario rules, then UI presenters. Keep pure decision logic free of MonoBehaviour lifecycles; “pure C#” here still uses Unity value types and services, so it is not engine-independent.

No measured frame-time or memory result was produced. Potential hot spots to profile include runtime NavMesh builds, repeated world searches/allocations, procedural mesh/material generation, OnGUI work and startup audio synthesis. Use Profiler evidence before introducing ECS, Addressables, jobs or broad pooling rewrites. The current one-robot-per-class design also makes an old “200 agents” target ambiguous: benchmark the maximum supported colony first and define an additional synthetic stress scene separately.

The inspected custom scripts did not expose a network/backend requirement. Security/reliability work should concentrate on save validation, corrupted-file handling, safe migration, release signing, dependency provenance and debug-feature separation. This was not a penetration test or a legal asset-rights audit. Inventory imported assets, fonts, audio and generated content with sources/permissions; choose original public branding instead of assuming internal SpaceX/xAI/Grok naming is cleared.

## Delivery decision

**Proceed to a stabilized external playtest, not a commercial launch.** Follow the accompanying [production plan](/Users/aaronesbenshade/solar-conquest/Docs/PRODUCTION_PLAN.md). First prove that strangers can finish Earth's meaningful opening without coaching, then validate complete travel/save continuity and distinct planet decisions. Visual completion and release packaging remain required gates; neither substitutes for those results.
