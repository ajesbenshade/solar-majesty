# External playtest protocol

The 45–90 minute playthrough has never been run by anyone outside the project. It is listed as an
open leftover in the Phase 1, 2, and 3 exit docs. Until it runs, the core loop is unvalidated and
every hour spent on art is a bet.

This is the M1 gate. Do not start M2 (the look stack) until it has run.

## Who

Five to eight people. **Not friends, not family, not anyone who has watched you build this.**
You need people who will get lost, because the things that confuse them are the real backlog.

Useful mix:

- Two who play colony/strategy games regularly (Frostpunk, Against the Storm, Rimworld, Timberborn).
- Two who play games but not this genre.
- One who has played Majesty or another indirect-control game — they will be your only reader of
  whether the Overseer fantasy lands as intended.

## Setup

1. Build a player: `Solar Majesty → Build → macOS` (or Windows/Linux), or grab a nightly CI artifact.
2. Ship them the build plus nothing else. **No instructions, no tutorial walkthrough, no "you'll
   want to click X first".** If the game needs you in the room, it is not finished.
3. Ask them to record their screen and, if they are willing, to think out loud.
4. Ask for 60–90 minutes but tell them explicitly: **stop whenever you get bored.** The moment they
   stop is the single most valuable data point in the whole exercise.

## What gets collected automatically

`PlaytestTelemetry` writes one JSON-lines file per scene load (title, then the colony) to
`<persistentDataPath>/Playtest/session-<UTC timestamp>.jsonl`. Nothing leaves the machine on its own.
Company `SolarMajesty`, product `Solar Majesty` (ProjectSettings), so the folder is:

| OS | Folder |
|---|---|
| Windows | `%USERPROFILE%\AppData\LocalLow\SolarMajesty\Solar Majesty\Playtest\` |
| macOS | `~/Library/Application Support/SolarMajesty/Solar Majesty/Playtest/` |
| Linux | `~/.config/unity3d/SolarMajesty/Solar Majesty/Playtest/` (or `$XDG_CONFIG_HOME/unity3d/...`) |

The Editor writes to the same place as a player on that OS. Paste the Windows path into Explorer's
address bar; on macOS use Finder → Go → Go to Folder (`Cmd+Shift+G`).

What a session records:

- Session start/quit: build, platform, quit screen, body, module count, play seconds, error count.
- Tutorial: `tutorial_start`, every `tutorial_step` with the step left, the step entered, and the play
  seconds spent on it, plus `tutorial_skip` and `tutorial_restart`.
- Idle gaps: no key, button, or wheel for over 30 s while the world runs (pause, HOLD, menus, and an
  unfocused window do not count). One `idle_gap` per gap with its length, the tutorial step, and what
  ended it (input, pause, quit). In the Earth demo the first 30 s also shows a concrete hint for the
  current step (`idle_hint`).
- Placement: `placement_invalid` with the building and the red-ghost reason.
- Flags: `flag_posted`, `flag_bounty` (+ / − on a pole, with whether it now tempts anyone),
  `flag_cancelled`, `flag_post_failed` (treasury), `flag_refused`, `flag_claimed`.
- Menus and speed: `pause_opened`, `settings_opened`, `speed_change`; saves and loads.
- The first five errors or exceptions (`error`, message trimmed); the count goes on `session_quit`.

### Collecting and summarizing

1. Ask the tester to zip the whole `Playtest` folder and send it back with "where did you stop".
2. Unzip each tester into its own folder, e.g. `playtests/2026-10-tester-a/`.
3. Run the summarizer (Python 3, standard library only):

   ```
   python3 Tools/playtest/summarize.py playtests/2026-10-tester-a playtests/2026-10-tester-b -o report.md
   ```

   Arguments are files or folders (searched recursively for `session-*.jsonl`). Without `-o` it prints
   to stdout. Title-only sessions are listed but left out of the funnel.
4. The report has an aggregate funnel (share of sessions reaching each tutorial step, median time on
   each step, where unfinished sessions stopped, idle gaps and nudges per step, invalid placements) and
   a per-session timeline. "Finished the Earth beat" means the Earth demo tutorial reached step 4.
5. Paste the aggregate and anything surprising into [PLAYTEST_REPORTS.md](PLAYTEST_REPORTS.md), and the
   observed behaviour per tester into [PHASE_1_FRICTION.md](Roadmap/PHASE_1_FRICTION.md).

The telemetry tells you *where* and *how long*. Only the recording tells you *why*.

## What you watch for

Do not ask leading questions during the session. Afterwards, answer these from the recording:

**The first ten minutes**
- How long before they place the Engineer workshop? (The Earth demo starts with the Commons and a HAB
  already standing.)
- Do they understand they cannot order anyone around? When does that land?
- Do they find the flag tool without being told?

**The greed gate — the signature moment**
- Do they understand *why* the Engineer refused the Build flag?
- Do they find the bounty raise, or do they just wait and hope?
- Does "ENG wants 79" read as a price, or as an error message?

**The middle**
- Which win gate stalls them: dens, sustain, or launch?
- Do they ever use game speed? Do they discover it?
- Do they read the Overseer log, or is it wallpaper?
- What do they do when a robot goes down?

**The stop**
- Where exactly did they stop, and was it boredom, confusion, or a wall?
- Could they explain what the game is about in one sentence afterwards?

## The two questions that decide M2

1. **Did anyone finish a body without help?** If nobody did, the loop has a teaching problem or a
   balance wall, and that is the next milestone instead of art.
2. **Did anyone want to keep playing when the session ended?** If nobody did, the loop has a depth
   problem. Prettier robots will not fix it.

## Recording the results

Append findings to [PHASE_1_FRICTION.md](Roadmap/PHASE_1_FRICTION.md), one section per tester.
Log observed behaviour, not your interpretation of it. "Spent four minutes trying to click the
Engineer onto the build site" is data. "Tutorial needs work" is not.

Then decide, in writing, before touching M2:

- What gets fixed now.
- What gets cut.
- What is fine and was only surprising to you.
