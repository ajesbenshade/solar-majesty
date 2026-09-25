"""Bake the character voice bank into Assets/Resources/Audio/Voices/.

    Tools/audio/.venv/bin/python Tools/audio/render_voices.py            # bake what changed
    Tools/audio/.venv/bin/python Tools/audio/render_voices.py --force    # re-bake everything
    Tools/audio/.venv/bin/python Tools/audio/render_voices.py --check    # stats only, no synthesis
    Tools/audio/.venv/bin/python Tools/audio/render_voices.py --preview  # audition sheet per speaker

Sources:
  - Tools/audio/voice_lines.json: authored hero barks and generic Overseer alerts.
  - Overseer (Grok) lines extracted from the C# sources: GrokCatalog.Line, OverseerRules.Grok*
    constants, non-interpolated CompactGrok lines, and literal RaiseAlert messages. Scraping the
    real sources means a copy edit in C# is re-voiced on the next bake instead of drifting.

The bake is incremental: each clip records a hash of (text, cast entry, FX code), and unchanged
clips are kept. The manifest voices.json is what the game reads.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf

from voice_common import (BANK_DIR, HERE, MANIFEST, REPO, line_key, load_cast, load_kokoro,
                          speakable)
from voice_fx import SAMPLE_RATE, process, synth_speed

SCRIPTS = REPO / "Assets" / "Scripts"
FX_VERSION = hashlib.sha1((HERE / "voice_fx.py").read_bytes()).hexdigest()[:8]
STRING = r'"((?:[^"\\]|\\.)*)"'


def _unescape(s: str) -> str:
    return s.replace('\\"', '"').replace("\\\\", "\\").replace("\\n", " ")


def extract_overseer_lines() -> list[str]:
    lines: list[str] = []

    grok = (SCRIPTS / "Systems" / "GrokCatalog.cs").read_text(encoding="utf-8")
    body = grok[grok.index("public static string Line("):grok.index("public static string Say(")]
    lines += [_unescape(m) for m in re.findall(r"return\s+" + STRING + r"\s*;", body)]

    rules = (SCRIPTS / "Systems" / "OverseerRules.cs").read_text(encoding="utf-8")
    for m in re.finditer(r"const\s+string\s+Grok\w+\s*=\s*((?:" + STRING + r"\s*\+?\s*)+);", rules, re.S):
        lines.append("".join(_unescape(p) for p in re.findall(STRING, m.group(1))))

    compact = (SCRIPTS / "Systems" / "CompactGrok.cs").read_text(encoding="utf-8")
    # Only fixed lines; interpolated ($"...{amount}...") lines go to the live server or stay text.
    lines += [_unescape(m) for m in re.findall(r"=>\s*" + STRING + r"\s*;", compact)]

    for path in (SCRIPTS / "Runtime").rglob("*.cs"):
        src = path.read_text(encoding="utf-8")
        for m in re.finditer(r"RaiseAlert\(\s*" + STRING + r"\s*,\s*" + STRING, src):
            lines.append(_unescape(m.group(2)))

    seen, unique = set(), []
    for line in lines:
        line = line.strip()
        if line and line_key(line) not in seen:
            seen.add(line_key(line))
            unique.append(line)
    return unique


def build_jobs(cast: dict) -> list[dict]:
    with open(HERE / "voice_lines.json", encoding="utf-8") as f:
        authored = {k: v for k, v in json.load(f).items() if not k.startswith("_")}

    jobs = []
    for speaker, cues in authored.items():
        if speaker not in cast:
            raise SystemExit(f"voice_lines.json speaker '{speaker}' has no cast.json entry")
        slug = speaker.replace("hero.", "hero_").replace(".", "_").lower()
        for cue, takes in cues.items():
            for i, text in enumerate(takes):
                jobs.append({"speaker": speaker, "cue": cue, "take": i, "text": text,
                             "clip": f"{slug}_{cue}_{i}"})

    for text in extract_overseer_lines():
        key = line_key(text)
        jobs.append({"speaker": "overseer", "cue": "line", "key": key, "text": text,
                     "clip": f"overseer_line_{key}"})
    return jobs


def job_hash(job: dict, cast: dict) -> str:
    blob = json.dumps([speakable(job["text"]), cast[job["speaker"]], FX_VERSION], sort_keys=True)
    return hashlib.sha1(blob.encode("utf-8")).hexdigest()[:12]


def synthesize(kokoro, text: str, entry: dict) -> np.ndarray:
    samples, sr = kokoro.create(speakable(text), voice=entry["voice"], speed=synth_speed(entry), lang="en-us")
    if sr != SAMPLE_RATE:
        raise SystemExit(f"Kokoro returned {sr} Hz; voice_fx assumes {SAMPLE_RATE}")
    return process(samples, sr, entry)


def write_ogg(path: Path, audio: np.ndarray) -> None:
    """Write mono Vorbis in blocks. libsndfile segfaults on long single-call Vorbis writes."""
    with sf.SoundFile(str(path), "w", SAMPLE_RATE, 1, format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(audio), 4096):
            f.write(audio[i:i + 4096])


def write_clip(path: Path, audio: np.ndarray) -> None:
    write_ogg(path, audio)


def stats(audio: np.ndarray) -> dict:
    peak = float(np.max(np.abs(audio))) if len(audio) else 0.0
    rms = float(np.sqrt(np.mean(audio ** 2))) if len(audio) else 0.0
    return {"peak_db": round(20 * np.log10(max(peak, 1e-9)), 1), "rms_db": round(20 * np.log10(max(rms, 1e-9)), 1)}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--force", action="store_true", help="re-bake every clip")
    ap.add_argument("--check", action="store_true", help="validate the bank without synthesising")
    ap.add_argument("--preview", type=Path, nargs="?", const=None, default=False,
                    help="write one audition file per speaker into this directory")
    ap.add_argument("--speaker", help="only bake this speaker (e.g. hero.ScoutDrone, overseer)")
    args = ap.parse_args()

    cast = load_cast()
    jobs = build_jobs(cast)
    if args.speaker:
        jobs = [j for j in jobs if j["speaker"] == args.speaker]

    old = {}
    if MANIFEST.exists():
        old = {c["clip"]: c for c in json.loads(MANIFEST.read_text(encoding="utf-8")).get("clips", [])}

    if args.check:
        return check(jobs, cast, old)

    BANK_DIR.mkdir(parents=True, exist_ok=True)
    kokoro = None
    clips, baked, kept = [], 0, 0
    started = time.time()
    for job in jobs:
        h = job_hash(job, cast)
        path = BANK_DIR / f"{job['clip']}.ogg"
        prev = old.get(job["clip"])
        if not args.force and prev and prev.get("hash") == h and path.exists():
            clips.append(prev)
            kept += 1
            continue
        if kokoro is None:
            kokoro = load_kokoro()
        audio = synthesize(kokoro, job["text"], cast[job["speaker"]])
        write_clip(path, audio)
        entry = dict(job, hash=h, seconds=round(len(audio) / SAMPLE_RATE, 2), **stats(audio))
        clips.append(entry)
        baked += 1
        print(f"  {job['clip']:<44} {entry['seconds']:>5.2f}s  {job['text'][:60]}")

    if args.speaker:
        # Partial bake: keep every other speaker's entries.
        clips += [c for c in old.values() if c["speaker"] != args.speaker]

    live = {c["clip"] for c in clips}
    for stale in BANK_DIR.glob("*.ogg"):
        if stale.stem not in live:
            stale.unlink()
            meta = stale.with_suffix(".ogg.meta")
            if meta.exists():
                meta.unlink()
            print(f"  removed stale {stale.name}")

    clips.sort(key=lambda c: (c["speaker"], c["cue"], c.get("take", 0), c.get("key", "")))
    manifest = {"version": 1, "sampleRate": SAMPLE_RATE, "fx": FX_VERSION, "clips": clips}
    MANIFEST.write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Baked {baked}, kept {kept}, total {len(clips)} clips in {time.time() - started:.0f}s → {MANIFEST.relative_to(REPO)}")

    if args.preview is not False:
        preview(clips, args.preview or (HERE / "preview"))
    return 0


def check(jobs: list[dict], cast: dict, old: dict) -> int:
    problems = 0
    for job in jobs:
        prev = old.get(job["clip"])
        path = BANK_DIR / f"{job['clip']}.ogg"
        if not prev or not path.exists():
            print(f"  MISSING {job['clip']}: {job['text'][:60]}")
            problems += 1
        elif prev.get("hash") != job_hash(job, cast):
            print(f"  STALE   {job['clip']}: {job['text'][:60]}")
            problems += 1
    clips = list(old.values())
    if clips:
        secs = sum(c["seconds"] for c in clips)
        size = sum(p.stat().st_size for p in BANK_DIR.glob("*.ogg"))
        peaks = [c["peak_db"] for c in clips]
        rms = [c["rms_db"] for c in clips]
        print(f"{len(clips)} clips, {secs:.0f}s audio, {size / 1e6:.1f} MB; "
              f"peak max {max(peaks)} dBFS; rms {min(rms)}..{max(rms)} dBFS")
        longest = sorted((c for c in clips if c["speaker"] != "overseer"), key=lambda c: -c["seconds"])[:3]
        for c in longest:
            print(f"  longest hero bark {c['clip']} {c['seconds']}s")
    print("OK" if problems == 0 else f"{problems} problem(s): run render_voices.py")
    return 1 if problems else 0


def preview(clips: list[dict], out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    gap = np.zeros(int(SAMPLE_RATE * 0.45), dtype=np.float32)
    by_speaker: dict[str, list[np.ndarray]] = {}
    order: dict[str, list[str]] = {}
    for c in clips:
        audio, _ = sf.read(str(BANK_DIR / f"{c['clip']}.ogg"), dtype="float32")
        by_speaker.setdefault(c["speaker"], []).extend([audio, gap])
        order.setdefault(c["speaker"], []).append(f"[{c['cue']}] {c['text']}")
    for speaker, parts in by_speaker.items():
        name = speaker.replace(".", "_").lower()
        write_ogg(out / f"{name}.ogg", np.concatenate(parts))
        (out / f"{name}.txt").write_text("\n".join(order[speaker]) + "\n", encoding="utf-8")
    print(f"Preview sheets → {out}")


if __name__ == "__main__":
    sys.exit(main())
