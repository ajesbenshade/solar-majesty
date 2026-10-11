#!/usr/bin/env python3
"""Summarize Solar Majesty playtest telemetry into a markdown report.

Reads the session-*.jsonl files PlaytestTelemetry writes (one JSON object per line,
"t" = realtime seconds since app start, "e" = event name, every other field a string)
and prints, per session, the tutorial timeline with time per step, where the player
stalled, invalid placements, flags, and whether they finished the Earth beat; then an
aggregate funnel across sessions.

    python3 Tools/playtest/summarize.py ~/Downloads/tester-a/Playtest tester-b/*.jsonl
    python3 Tools/playtest/summarize.py Tools/playtest/fixtures -o report.md

Arguments may be files or folders (searched recursively for session-*.jsonl).
Standard library only. Field names match Assets/Scripts/Systems/PlaytestTelemetry.cs;
change both together.
"""

import argparse
import json
import statistics
import sys
from collections import Counter, OrderedDict
from pathlib import Path

# Step numbers are TutorialStep values. Earth demo: FirstHourTutorial.cs. Full campaign:
# GameLoop.TickTutorial / OverseerHud.DrawTutorial.
STEP_NAMES = {
    "first_hour": OrderedDict([
        (0, "Workshop: place the Engineer workshop, wait for the robot"),
        (1, "Cheap Build: post Build at 700"),
        (2, "Raise price: + until the pole says tempted"),
        (3, "Defend: post F5 on the creeper"),
        (4, "Done: Earth beat finished"),
    ]),
    "campaign": OrderedDict([
        (0, "Commons"),
        (1, "Tax collectors"),
        (2, "HAB"),
        (3, "Workshop"),
        (4, "First flag"),
        (5, "Bounty tempts a robot"),
        (6, "Done"),
    ]),
}
DEFAULT_GOAL = {"first_hour": 4, "campaign": 6}


def fnum(value, default=0.0):
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def inum(value, default=None):
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def fmt_secs(seconds):
    if seconds is None:
        return "-"
    seconds = int(round(seconds))
    if seconds < 60:
        return f"{seconds}s"
    m, s = divmod(seconds, 60)
    if m < 60:
        return f"{m}m {s:02d}s"
    h, m = divmod(m, 60)
    return f"{h}h {m:02d}m"


def step_name(flow, step):
    if step is None:
        return "-"
    if step < 0:
        return "after tutorial"
    names = STEP_NAMES.get(flow or "", {})
    return f"{step} {names.get(step, '')}".strip()


def find_files(args):
    files = []
    for raw in args:
        p = Path(raw).expanduser()
        if p.is_dir():
            files.extend(sorted(p.rglob("session-*.jsonl")))
        elif p.is_file():
            files.append(p)
        else:
            print(f"warning: {raw} not found", file=sys.stderr)
    seen, out = set(), []
    for f in files:
        key = f.resolve()
        if key not in seen:
            seen.add(key)
            out.append(f)
    return out


def read_sessions(path):
    """One file can hold more than one session (two scene loads in the same second)."""
    sessions, current, bad = [], None, 0
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except ValueError:
                bad += 1
                continue
            if not isinstance(row, dict) or "e" not in row:
                bad += 1
                continue
            if row["e"] == "session_start" or current is None:
                current = []
                sessions.append(current)
            current.append(row)
    return sessions, bad


class Session:
    def __init__(self, path, rows, index):
        self.path = Path(path)
        self.label = self.path.name + (f" #{index + 1}" if index else "")
        self.rows = rows
        self.start = next((r for r in rows if r["e"] == "session_start"), {})
        self.quit = next((r for r in reversed(rows) if r["e"] == "session_quit"), None)
        self.t0 = fnum(rows[0].get("t")) if rows else 0.0
        self.t1 = fnum(rows[-1].get("t")) if rows else 0.0
        self.flow = None
        self.goal = None
        self.timeline = []          # (t, from, to, seconds on from)
        self.step_seconds = Counter()
        self.step = None            # current tutorial step
        self.max_step = None
        self.finished_at = None     # (t, play seconds to finish)
        self.tutorial_t0 = None
        self.skipped = None         # step skipped from
        self.restarts = 0
        self.idle = []              # (seconds, step, ended)
        self.hints = []             # (t, step)
        self.invalid = Counter()
        self.flags_posted = []      # (type, bounty, interest)
        self.bounty_changes = []    # (type, from, to, interest)
        self.cancelled = []
        self.post_failed = []
        self.refused = Counter()
        self.claimed = Counter()
        self.pauses = 0
        self.settings = 0
        self.speed_changes = 0
        self.errors = []
        self.error_count = 0
        self._parse()

    def _enter_step(self, step):
        self.step = step
        if step is not None and (self.max_step is None or step > self.max_step):
            self.max_step = step

    def _parse(self):
        last_step_t = self.t0  # legacy files: step 0 is timed from session start
        for r in self.rows:
            e, t = r["e"], fnum(r.get("t"))
            if e == "tutorial_start":
                self.flow = r.get("flow") or self.flow
                self.goal = inum(r.get("goal"), self.goal)
                self.tutorial_t0 = t if self.tutorial_t0 is None else self.tutorial_t0
                self._enter_step(inum(r.get("step"), 0))
                last_step_t = t
            elif e == "tutorial_step":
                to = inum(r.get("step"))
                self.flow = r.get("flow") or self.flow
                self.goal = inum(r.get("goal"), self.goal)
                frm = inum(r.get("from"))
                if frm is None:  # sessions logged before "from"/"seconds" existed
                    frm = self.step if self.step is not None else (to - 1 if to else 0)
                if "seconds" in r:
                    secs = fnum(r.get("seconds"))
                else:
                    secs = t - last_step_t if last_step_t is not None else 0.0
                if self.tutorial_t0 is None:
                    self.tutorial_t0 = t - secs
                    self._enter_step(frm)
                self.timeline.append((t, frm, to, secs))
                self.step_seconds[frm] += secs
                self._enter_step(to)
                last_step_t = t
                goal = self.goal if self.goal is not None else DEFAULT_GOAL.get(self.flow or "", 4)
                if to is not None and to >= goal and self.finished_at is None:
                    self.finished_at = (t, sum(self.step_seconds.values()))
            elif e == "tutorial_skip":
                self.flow = r.get("flow") or self.flow
                self.skipped = inum(r.get("step"))
            elif e == "tutorial_restart":
                self.restarts += 1
                self._enter_step(0)
                self.step = 0
            elif e == "idle_gap":
                self.idle.append((fnum(r.get("seconds")), inum(r.get("step")), r.get("ended", "")))
            elif e == "idle_hint":
                self.hints.append((t, inum(r.get("step"))))
            elif e == "placement_invalid":
                reason = r.get("reason") or "(no reason)"
                self.invalid[f"{r.get('building', '?')}: {reason}"] += 1
            elif e == "flag_posted":
                self.flags_posted.append((r.get("type", "?"), r.get("bounty", "?"), inum(r.get("interest"), 0)))
            elif e == "flag_bounty":
                self.bounty_changes.append((r.get("type", "?"), fnum(r.get("from")), fnum(r.get("to")),
                                            inum(r.get("interest"), 0)))
            elif e == "flag_cancelled":
                self.cancelled.append((r.get("type", "?"), r.get("bounty", "?")))
            elif e == "flag_post_failed":
                self.post_failed.append((r.get("type", "?"), r.get("bounty", "?"), r.get("reason", "")))
            elif e == "flag_refused":
                self.refused[f"{r.get('class', '?')} {r.get('kind', '?')}"] += 1
            elif e == "flag_claimed":
                self.claimed[r.get("type", "?")] += 1
            elif e == "pause_opened":
                self.pauses += 1
            elif e == "settings_opened":
                self.settings += 1
            elif e == "speed_change":
                self.speed_changes += 1
            elif e == "error":
                self.errors.append(r.get("msg", ""))
        self.error_count = inum(self.quit.get("errors"), len(self.errors)) if self.quit else len(self.errors)
        # The step they were on when the session ended counts too (wall clock; it never closed).
        self.open_seconds = 0.0
        if self.finished_at is None and self.step is not None and last_step_t is not None:
            end = fnum(self.quit.get("t")) if self.quit else self.t1
            if self.skipped is not None:
                skip = next((r for r in self.rows if r["e"] == "tutorial_skip"), None)
                end = fnum(skip.get("t")) if skip else end
            self.open_seconds = max(0.0, end - last_step_t)
        if self.flow is None and self.tutorial_t0 is not None:
            self.flow = "first_hour"

    @property
    def title_only(self):
        if self.tutorial_t0 is not None or self.flags_posted or self.invalid or self.idle:
            return False
        return self.quit is None or self.quit.get("screen") == "Title"

    @property
    def finished(self):
        return self.finished_at is not None

    @property
    def play_seconds(self):
        if self.quit and "play_seconds" in self.quit:
            return fnum(self.quit.get("play_seconds"))
        return None

    @property
    def longest_idle(self):
        return max(self.idle, key=lambda g: g[0]) if self.idle else None

    def step_at_quit(self):
        if self.finished:
            return "finished"
        if self.skipped is not None:
            return f"skipped at {step_name(self.flow, self.skipped)}"
        if self.step is None:
            return "tutorial not running"
        return step_name(self.flow, self.step)


def section_session(s):
    out = []
    build = s.start.get("build", "?")
    platform = s.start.get("platform", "?")
    out.append(f"### {s.label}")
    out.append("")
    quit_line = (f"quit on **{s.quit.get('screen', '?')}** ({s.quit.get('body', '?')}, "
                 f"{s.quit.get('modules', '?')} modules, play {fmt_secs(s.play_seconds)})"
                 if s.quit else "**no session_quit** (crash, kill, or still running)")
    out.append(f"- Build `{build}` on {platform}; wall {fmt_secs(s.t1 - s.t0)}; {quit_line}.")
    if s.title_only:
        out.append("- Title screen only; left out of the aggregate.")
        out.append("")
        return out
    flow = s.flow or "?"
    if s.finished:
        t_done, play_done = s.finished_at
        out.append(f"- **Finished the {'Earth beat' if flow == 'first_hour' else 'tutorial'}** in "
                   f"{fmt_secs(play_done)} of play ({fmt_secs(t_done - s.tutorial_t0)} wall).")
    else:
        out.append(f"- Did **not** finish. Stopped at: **{s.step_at_quit()}**.")
    li = s.longest_idle
    if li:
        out.append(f"- Longest idle: {fmt_secs(li[0])} on step {step_name(flow, li[1])} (ended by {li[2]}). "
                   f"{len(s.idle)} gap(s), {fmt_secs(sum(g[0] for g in s.idle))} total; "
                   f"{len(s.hints)} nudge(s) shown.")
    else:
        out.append("- No idle gaps over 30 s.")
    if s.restarts:
        out.append(f"- Restarted the tutorial {s.restarts} time(s).")
    if s.timeline or s.tutorial_t0 is not None:
        out.append("")
        out.append("| t | step left | step entered | time on step |")
        out.append("|---|---|---|---|")
        if s.tutorial_t0 is not None:
            out.append(f"| {fmt_secs(s.tutorial_t0 - s.t0)} | - | tutorial start | - |")
        for t, frm, to, secs in s.timeline:
            out.append(f"| {fmt_secs(t - s.t0)} | {step_name(flow, frm)} | {step_name(flow, to)} | {fmt_secs(secs)} |")
        if not s.finished and s.step is not None:
            end_label = "(skipped)" if s.skipped is not None else "(quit)"
            out.append(f"| {fmt_secs(s.t1 - s.t0) if s.skipped is None else '-'} | {step_name(flow, s.step)} | "
                       f"{end_label} | {fmt_secs(s.open_seconds)} wall |")
        out.append("")
    bits = []
    if s.invalid:
        bits.append("Invalid placements: " + ", ".join(f"{k} x{v}" for k, v in s.invalid.most_common()))
    if s.flags_posted:
        posted = Counter(f"{ty} @{b}" for ty, b, _ in s.flags_posted)
        bits.append("Flags posted: " + ", ".join(f"{k} x{v}" if v > 1 else k for k, v in posted.items()))
    raises = [c for c in s.bounty_changes if c[2] > c[1]]
    lowers = [c for c in s.bounty_changes if c[2] < c[1]]
    if s.bounty_changes:
        tempted = next((c for c in s.bounty_changes if c[3] > 0), None)
        line = f"Bounty edits: {len(raises)} raise(s), {len(lowers)} lower(s)"
        if tempted:
            line += f"; first tempted at {tempted[2]:.0f} ({tempted[0]})"
        bits.append(line)
    if s.refused:
        bits.append("Refusals: " + ", ".join(f"{k} x{v}" for k, v in s.refused.most_common()))
    if s.claimed:
        bits.append("Claimed: " + ", ".join(f"{k} x{v}" for k, v in s.claimed.items()))
    if s.cancelled:
        bits.append("Cancelled: " + ", ".join(f"{ty} @{b}" for ty, b in s.cancelled))
    if s.post_failed:
        bits.append("Post failed: " + ", ".join(f"{ty} @{b} ({why})" for ty, b, why in s.post_failed))
    bits.append(f"Pause opened {s.pauses}x, settings {s.settings}x, speed changes {s.speed_changes}")
    if s.error_count:
        sample = f" (first: `{s.errors[0]}`)" if s.errors else ""
        bits.append(f"Errors logged: {s.error_count}{sample}")
    out.extend(f"- {b}" for b in bits)
    out.append("")
    return out


def median_or_none(values):
    return statistics.median(values) if values else None


def section_aggregate(sessions, flow):
    group = [s for s in sessions if s.tutorial_t0 is not None and s.flow == flow]
    if not group:
        return []
    names = STEP_NAMES[flow]
    goal = max(names)
    n = len(group)
    out = [f"### {'Earth demo' if flow == 'first_hour' else 'Full campaign'} ({n} session{'s' if n != 1 else ''})", ""]
    finished = [s for s in group if s.finished]
    times = [s.finished_at[1] for s in finished]
    out.append(f"- Finished: **{len(finished)}/{n}** ({100 * len(finished) // n}%). "
               f"Median time to finish: {fmt_secs(median_or_none(times))}.")
    out.append("")
    out.append("Time on step includes the step a session ended on (wall clock from its last step change).")
    out.append("")
    out.append("| step | reached | median time on step | idle gaps here | nudges here |")
    out.append("|---|---|---|---|---|")
    for step, label in names.items():
        reached = sum(1 for s in group if s.max_step is not None and s.max_step >= step)
        spent = [s.step_seconds[step] + (s.open_seconds if s.step == step and not s.finished else 0.0)
                 for s in group
                 if step in s.step_seconds or (s.step == step and not s.finished)]
        gaps = sum(1 for s in group for g in s.idle if g[1] == step)
        nudges = sum(1 for s in group for h in s.hints if h[1] == step)
        med = fmt_secs(median_or_none(spent)) if step < goal else "-"
        out.append(f"| {step} {label} | {reached}/{n} ({100 * reached // n}%) | {med} | {gaps} | {nudges} |")
    out.append("")
    stops = Counter(s.step_at_quit() for s in group if not s.finished)
    if stops:
        out.append("- Where unfinished sessions stopped: " +
                   ", ".join(f"{k} x{v}" for k, v in stops.most_common()) + ".")
    longest = [s.longest_idle for s in group if s.longest_idle]
    if longest:
        worst = max(longest, key=lambda g: g[0])
        out.append(f"- Median longest idle: {fmt_secs(median_or_none([g[0] for g in longest]))}; "
                   f"worst {fmt_secs(worst[0])} on step {step_name(flow, worst[1])}.")
    invalid = Counter()
    for s in group:
        invalid.update(s.invalid)
    if invalid:
        out.append("- Top invalid placements: " +
                   ", ".join(f"{k} x{v}" for k, v in invalid.most_common(5)) + ".")
    failed = sum(len(s.post_failed) for s in group)
    if failed:
        out.append(f"- Flag posts refused by the treasury: {failed}.")
    skipped = sum(1 for s in group if s.skipped is not None)
    if skipped:
        out.append(f"- Skipped the tutorial: {skipped}.")
    errs = sum(s.error_count for s in group)
    if errs:
        out.append(f"- Errors logged across sessions: {errs}.")
    out.append("")
    return out


def build_report(files):
    sessions, bad_lines = [], 0
    for f in files:
        chunks, bad = read_sessions(f)
        bad_lines += bad
        for i, rows in enumerate(chunks):
            sessions.append(Session(f, rows, i))
    out = ["# Playtest report", ""]
    played = [s for s in sessions if not s.title_only]
    out.append(f"{len(files)} file(s), {len(sessions)} session(s), {len(played)} with play "
               f"({len(sessions) - len(played)} title-only)." +
               (f" {bad_lines} unreadable line(s) skipped." if bad_lines else ""))
    out.append("")
    out.append("## Aggregate")
    out.append("")
    if not played:
        out.append("No sessions reached play.")
        out.append("")
    for flow in ("first_hour", "campaign"):
        out.extend(section_aggregate(played, flow))
    after = [s for s in played if s.tutorial_t0 is None]
    if after:
        out.append(f"{len(after)} session(s) played with the tutorial already done or off "
                   "(Continue after finishing); not in the funnel.")
        out.append("")
    out.append("## Sessions")
    out.append("")
    for s in sessions:
        out.extend(section_session(s))
    return "\n".join(out).rstrip() + "\n"


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("paths", nargs="+", help="session-*.jsonl files or folders holding them")
    ap.add_argument("-o", "--output", help="write the markdown here instead of stdout")
    args = ap.parse_args(argv)
    files = find_files(args.paths)
    if not files:
        print("no session-*.jsonl files found", file=sys.stderr)
        return 1
    report = build_report(files)
    if args.output:
        Path(args.output).write_text(report, encoding="utf-8")
    else:
        sys.stdout.write(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
