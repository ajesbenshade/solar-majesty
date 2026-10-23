# Playtest reports

Summaries of external playtest telemetry, newest first. How to collect the logs and run the
summarizer: [PLAYTEST_PROTOCOL.md](PLAYTEST_PROTOCOL.md#collecting-and-summarizing). Observed
behaviour from the recordings goes in [PHASE_1_FRICTION.md](Roadmap/PHASE_1_FRICTION.md); this file
holds the numbers.

For each round, add a section with the date, the build, who played (the protocol's mix, no names),
the summarizer's **Aggregate** block, and three lines: what gets fixed now, what gets cut, what was
only surprising to us. Keep per-session timelines in the round's folder unless one explains a stall.

The bar (Docs/NEXT_STEPS.md, "Playable demo"): a stranger finishes the Earth beat — place an
Engineer workshop, post Build at the default bounty (700 EU in this build), raise the price, post
Defend on the tutorial creeper. In the report that is "Finished" and the step 4 row of the funnel.

Reading the funnel:

- **reached** counts sessions whose tutorial got to that step (a jump from 1 straight to 3, when the
  Engineer takes the first Build, counts as reaching 2).
- **median time on step** is play time (pause and menus excluded) between step changes, plus the
  wall time on the step a session ended on.
- **idle gaps here / nudges here**: a gap is 30 s with no key, click, or wheel while the world ran.
  Many gaps and nudges on one step is the strongest "I didn't know what to do" signal we have.

## No external rounds yet

No stranger session has been summarized. The first 5–8 person round from the protocol goes here.

## Sample report (fixture data, not a real playtest)

Generated from `Tools/playtest/fixtures/` with:

```
python3 Tools/playtest/summarize.py Tools/playtest/fixtures
```

The fixtures are hand-written to exercise every event: a title-only session, one that finishes the
Earth beat, one that stalls on the price lesson, a crash after skipping the tutorial, and a
September-format file (`tutorial_step` without `from`/`seconds`, which the summarizer still reads).

5 file(s), 5 session(s), 4 with play (1 title-only).

### Aggregate

#### Earth demo (4 sessions)

- Finished: **1/4** (25%). Median time to finish: 3m 39s.

Time on step includes the step a session ended on (wall clock from its last step change).

| step | reached | median time on step | idle gaps here | nudges here |
|---|---|---|---|---|
| 0 Workshop: place the Engineer workshop, wait for the robot | 4/4 (100%) | 1m 48s | 2 | 2 |
| 1 Cheap Build: post Build at 700 | 4/4 (100%) | 29s | 0 | 0 |
| 2 Raise price: + until the pole says tempted | 3/4 (75%) | 59s | 3 | 2 |
| 3 Defend: post F5 on the creeper | 1/4 (25%) | 32s | 0 | 0 |
| 4 Done: Earth beat finished | 1/4 (25%) | - | 0 | 0 |

- Where unfinished sessions stopped: 2 Raise price: + until the pole says tempted x2, skipped at 1 Cheap Build: post Build at 700 x1.
- Median longest idle: 1m 10s; worst 1m 35s on step 2 Raise price: + until the pole says tempted.
- Top invalid placements: Engineer Workshop: Blocked by a building. x2, Engineer Workshop: That spot is off the map. x1.
- Flag posts refused by the treasury: 1.
- Skipped the tutorial: 1.
- Errors logged across sessions: 1.

### Sessions

#### session-20260918-003813.jsonl

- Build `0.3.1` on OSXPlayer; wall 3m 36s; quit on **Playing** (Earth, 3 modules, play 3m 34s).
- Did **not** finish. Stopped at: **2 Raise price: + until the pole says tempted**.
- No idle gaps over 30 s.

| t | step left | step entered | time on step |
|---|---|---|---|
| 0s | - | tutorial start | - |
| 1m 34s | 0 Workshop: place the Engineer workshop, wait for the robot | 1 Cheap Build: post Build at 700 | 1m 34s |
| 2m 37s | 1 Cheap Build: post Build at 700 | 2 Raise price: + until the pole says tempted | 1m 03s |
| 3m 36s | 2 Raise price: + until the pole says tempted | (quit) | 59s wall |

- Flags posted: Build @700
- Pause opened 0x, settings 0x, speed changes 0

#### session-20261011-140002.jsonl

- Build `0.4.0` on WindowsPlayer; wall 38s; quit on **Title** (Earth, 0 modules, play 0s).
- Title screen only; left out of the aggregate.

#### session-20261011-140043.jsonl

- Build `0.4.0` on WindowsPlayer; wall 4m 50s; quit on **Paused** (Earth, 4 modules, play 4m 28s).
- **Finished the Earth beat** in 3m 39s of play (3m 39s wall).
- Longest idle: 45s on step 0 Workshop: place the Engineer workshop, wait for the robot (ended by input). 2 gap(s), 1m 18s total; 2 nudge(s) shown.

| t | step left | step entered | time on step |
|---|---|---|---|
| 2s | - | tutorial start | - |
| 2m 02s | 0 Workshop: place the Engineer workshop, wait for the robot | 1 Cheap Build: post Build at 700 | 2m 00s |
| 2m 21s | 1 Cheap Build: post Build at 700 | 2 Raise price: + until the pole says tempted | 18s |
| 3m 10s | 2 Raise price: + until the pole says tempted | 3 Defend: post F5 on the creeper | 49s |
| 3m 41s | 3 Defend: post F5 on the creeper | 4 Done: Earth beat finished | 32s |

- Invalid placements: Engineer Workshop: Blocked by a building. x2
- Flags posted: Build @700, DefendArea @300
- Bounty edits: 4 raise(s), 0 lower(s); first tempted at 900 (Build)
- Refusals: EngineerBot Greed x1
- Claimed: Build x1
- Pause opened 1x, settings 0x, speed changes 1

#### session-20261011-151500.jsonl

- Build `0.4.0` on OSXPlayer; wall 7m 30s; quit on **Paused** (Earth, 3 modules, play 7m 20s).
- Did **not** finish. Stopped at: **2 Raise price: + until the pole says tempted**.
- Longest idle: 1m 35s on step 2 Raise price: + until the pole says tempted (ended by input). 3 gap(s), 3m 12s total; 2 nudge(s) shown.

| t | step left | step entered | time on step |
|---|---|---|---|
| 5s | - | tutorial start | - |
| 3m 25s | 0 Workshop: place the Engineer workshop, wait for the robot | 1 Cheap Build: post Build at 700 | 3m 20s |
| 4m 04s | 1 Cheap Build: post Build at 700 | 2 Raise price: + until the pole says tempted | 39s |
| 7m 30s | 2 Raise price: + until the pole says tempted | (quit) | 3m 26s wall |

- Invalid placements: Engineer Workshop: That spot is off the map. x1
- Flags posted: Build @700 x2
- Refusals: EngineerBot Greed x2
- Cancelled: Build @700
- Post failed: Build @700 (treasury)
- Pause opened 1x, settings 0x, speed changes 0
- Errors logged: 1 (first: `NullReferenceException: Object reference not set to an instance of an object`)

#### session-20261011-163000.jsonl

- Build `0.4.0` on LinuxPlayer; wall 2m 58s; **no session_quit** (crash, kill, or still running).
- Did **not** finish. Stopped at: **skipped at 1 Cheap Build: post Build at 700**.
- No idle gaps over 30 s.

| t | step left | step entered | time on step |
|---|---|---|---|
| 3s | - | tutorial start | - |
| 1m 38s | 0 Workshop: place the Engineer workshop, wait for the robot | 1 Cheap Build: post Build at 700 | 1m 36s |
| - | 1 Cheap Build: post Build at 700 | (skipped) | 17s wall |

- Flags posted: Explore @400
- Pause opened 0x, settings 0x, speed changes 1
