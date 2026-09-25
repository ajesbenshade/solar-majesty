#!/usr/bin/env python3
"""Offline, deterministic sound-effect renderer for Solar Majesty.

    Tools/audio/.venv/bin/python Tools/audio/render_sfx.py            # render all + check + audition
    Tools/audio/.venv/bin/python Tools/audio/render_sfx.py --only bite,claim
    Tools/audio/.venv/bin/python Tools/audio/render_sfx.py --check    # verify files on disk only

Outputs to Assets/Resources/Audio/Sfx/:
    sfx_<event>_<1..3>.wav|ogg   one-shots, 44.1 kHz mono PCM16 (long/stereo 2D stingers are OGG)
    sfx_ambient_<world>.ogg      48 s stereo seamless loop, campus A
    sfx_ambient_<world>_b.ogg    32 s stereo seamless loop, campus B variation
    ("ambient" in the name is load-bearing: Editor/AudioImportRules.cs keys on it to keep beds
    stereo and compressed in memory instead of force-to-mono ADPCM like the one-shots.)

Loudness hierarchy (max momentary loudness, BS.1770 K-weighted, 400 ms window). Files are
normalised to these targets so DemoAudio can play every event at a similar gain:
    ui        -36 .. -26   hover, click, confirm: present but never competing with the game
    feedback  -25 .. -20   per-robot/per-fauna events (also distance-attenuated in game)
    milestone -19.5 .. -18 things the player caused and should notice
    alert     -17 .. -15   warnings, critical, mission fail
    fanfare   -13.5 .. -13 victory and launch, the loudest moments in the game
Beds: integrated LUFS -30 .. -34 (Luna deliberately quietest), played under SoundBus Ambient.

You cannot trust ears you do not have, so --check measures what a mix engineer would listen for:
peaks, true peaks, RMS, DC, clicks at the edges, loop seams, variant spread and tier ordering.
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from sfx_dsp import (  # noqa: E402
    SR, db, fades, hp, limit, loop_crossfade, lufs_integrated, lufs_momentary_max, master_oneshot,
    ns, rng_for, true_peak,
)
from sfx_events import EVENTS, TIER_ORDER  # noqa: E402
from sfx_beds import BEDS, WORLDS  # noqa: E402

REPO = HERE.parent.parent
OUT_DIR = REPO / "Assets" / "Resources" / "Audio" / "Sfx"
PREVIEW_DIR = Path("/private/tmp/claude-501/-Volumes-Storage-Projects-IRLobby/"
                   "25b273c1-6bc0-46cd-b1d4-69313852fa43/scratchpad/sfx_preview")
VARIANTS = 3
BED_SECONDS = {"a": 48.0, "b": 32.0}
BED_XFADE = 4.0
CEILING_DB = -1.6          # sample-peak ceiling; leaves room for inter-sample overs
OGG_QUALITY = 0.45         # libsndfile compression level (0 = best quality, 1 = smallest)
BUDGET_MB = 12.0


# --------------------------------------------------------------------------------------------
# Rendering
# --------------------------------------------------------------------------------------------

def oneshot_path(name: str, v: int, fmt: str) -> Path:
    return OUT_DIR / f"sfx_{name}_{v + 1}.{fmt}"


def bed_path(world: str, flavor: str) -> Path:
    return OUT_DIR / (f"sfx_ambient_{world}.ogg" if flavor == "a" else f"sfx_ambient_{world}_b.ogg")


def _write(path: Path, x: np.ndarray, fmt: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    for other in ("wav", "ogg"):  # a format change must not leave a twin behind in Resources
        twin = path.with_suffix("." + other)
        if other != fmt and twin.exists():
            twin.unlink()
    data = x.T if x.ndim == 2 else x
    if fmt == "wav":
        # TPDF dither before 16-bit so quiet UI tails decay smoothly instead of truncating.
        r = rng_for("dither", path.name)
        lsb = 1.0 / 32768.0
        d = (r.random(data.shape) - r.random(data.shape)) * lsb
        sf.write(str(path), np.clip(data + d, -1, 1), SR, subtype="PCM_16")
    else:
        write_ogg(path, data, OGG_QUALITY)


def write_ogg(path: Path, data: np.ndarray, level: float):
    """Block-wise Vorbis write. A single sf.write of a long file segfaults this libsndfile build."""
    data = np.ascontiguousarray(np.clip(data, -1, 1), dtype=np.float32)
    chans = 1 if data.ndim == 1 else data.shape[1]
    with sf.SoundFile(str(path), "w", SR, chans, format="OGG", subtype="VORBIS",
                      compression_level=level) as f:
        for i in range(0, data.shape[0], 4096):
            f.write(data[i:i + 4096])


def render_event(name: str) -> list[str]:
    fn, target, _tier, fmt, _desc = EVENTS[name]
    written = []
    for v in range(VARIANTS):
        r = rng_for("sfx", name, v)
        x = np.asarray(fn(r, v), dtype=float)
        x = master_oneshot(x, target, CEILING_DB)
        p = oneshot_path(name, v, fmt)
        _write(p, x, fmt)
        written.append(p.name)
    return written


def render_bed(world: str, flavor: str) -> str:
    fn, _desc = BEDS[world]
    _hum, target = WORLDS[world]
    r = rng_for("bed", world, flavor)
    seconds = BED_SECONDS[flavor]
    n_out, xf = ns(seconds), ns(BED_XFADE)
    x = fn(r, n_out + xf, flavor == "b")
    x = np.vstack([hp(x[0], 25, 2), hp(x[1], 25, 2)])
    x -= x.mean(axis=1, keepdims=True)
    x = loop_crossfade(x, n_out, xf)
    for _ in range(2):
        x = x * db(target - lufs_integrated(x))
        x = limit(x, -3.0)
    p = bed_path(world, flavor)
    _write(p, x, "ogg")
    return p.name


def _job(kind_name):
    kind, a, b = kind_name
    t0 = time.time()
    out = render_event(a) if kind == "event" else [render_bed(a, b)]
    return kind_name, out, time.time() - t0


def render(only: set[str] | None):
    jobs = []
    for name in EVENTS:
        if only is None or name in only:
            jobs.append(("event", name, None))
    for world in BEDS:
        for flavor in ("a", "b"):
            if only is None or f"amb_{world}" in only or "beds" in only:
                jobs.append(("bed", world, flavor))
    if not jobs:
        sys.exit(f"--only matched nothing. Events: {', '.join(EVENTS)}; beds: amb_<world> or 'beds'.")
    workers = min(len(jobs), max(1, (os.cpu_count() or 2) - 1))
    with ProcessPoolExecutor(max_workers=workers) as pool:
        for (kind, a, b), out, secs in pool.map(_job, jobs):
            label = a if kind == "event" else f"amb_{a}_{b}"
            print(f"  rendered {label:<20} {secs:5.1f}s  {', '.join(out)}")


# --------------------------------------------------------------------------------------------
# Verification
# --------------------------------------------------------------------------------------------

def _read(path: Path) -> np.ndarray:
    x, sr = sf.read(str(path), always_2d=True)
    assert sr == SR, f"{path.name}: sample rate {sr}"
    return x.T


def _centroid(x) -> float:
    """Spectral centroid, a sanity check that each design lives in the register it was meant to."""
    mono = x.mean(axis=0)
    mag = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    f = np.fft.rfftfreq(len(mono), 1.0 / SR)
    return float(np.sum(f * mag) / (np.sum(mag) + 1e-12))


def _similarity(a, b) -> float:
    """Peak normalised cross-correlation of two takes; ~1.0 means the variants are duplicates."""
    a, b = a.mean(axis=0), b.mean(axis=0)
    n = 1 << int(np.ceil(np.log2(len(a) + len(b))))
    c = np.fft.irfft(np.fft.rfft(a, n) * np.conj(np.fft.rfft(b, n)), n)
    return float(np.max(np.abs(c)) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-12))


def _edge_metrics(x):
    """Start/end sample values, and the max level in the first/last 0.5 ms relative to peak."""
    mono = np.max(np.abs(x), axis=0)
    peak = mono.max() + 1e-12
    w = ns(0.0005)
    return (float(mono[0]), float(mono[-1]), float(mono[:w].max() / peak), float(mono[-w:].max() / peak))


def _seam_metrics(x):
    """How the loop point compares with the rest of the bed.

    jump_ratio: |x[0] - x[-1]| relative to the 99.9th percentile of sample-to-sample steps.
                <= ~1 means the seam is no bigger a step than the signal takes anyway.
    rms_step_db: 50 ms RMS just before vs just after the seam.
    flux_ratio: spectral change across the seam vs the median change between adjacent frames.
    """
    step = np.abs(np.diff(x, axis=1))
    p999 = np.percentile(step, 99.9) + 1e-12
    jump = float(np.max(np.abs(x[:, 0] - x[:, -1])) / p999)
    w = ns(0.05)
    a = np.sqrt(np.mean(x[:, -w:] ** 2))
    b = np.sqrt(np.mean(x[:, :w] ** 2))
    rms_step = float(20 * np.log10((b + 1e-12) / (a + 1e-12)))
    frame = 2048
    mono = x.mean(axis=0)
    wrapped = np.concatenate([mono[-8 * frame:], mono[: 8 * frame]])
    win = np.hanning(frame)

    def spec(sig):
        k = len(sig) // frame
        return np.abs(np.fft.rfft(sig[: k * frame].reshape(k, frame) * win, axis=1))

    s_seam = spec(wrapped)
    seam_flux = np.linalg.norm(s_seam[8] - s_seam[7])
    s_all = spec(mono)
    flux = np.linalg.norm(np.diff(s_all, axis=0), axis=1)
    return jump, rms_step, float(seam_flux / (np.median(flux) + 1e-12))


def check(names: set[str] | None) -> bool:
    ok = True
    rows = []
    per_event = {}
    problems = []
    print("\nONE-SHOTS  (peak/true-peak dBFS, RMS dBFS, LUFS = max momentary; edge = first/last 0.5 ms vs peak)")
    print(f"{'file':<30}{'ch':>3}{'dur s':>7}{'peak':>7}{'tp':>7}{'rms':>7}{'LUFS':>7}{'tgt':>6}{'dc':>9}{'edge in/out':>13}{'cent Hz':>9}")
    for name, (_fn, target, tier, fmt, _d) in EVENTS.items():
        if names is not None and name not in names:
            continue
        levels = []
        takes = []
        for v in range(VARIANTS):
            p = oneshot_path(name, v, fmt)
            if not p.exists():
                problems.append(f"missing {p.name}")
                continue
            x = _read(p)
            peak = 20 * np.log10(np.max(np.abs(x)) + 1e-12)
            tp = 20 * np.log10(true_peak(x) + 1e-12)
            rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)
            lu = lufs_momentary_max(x)
            dc = float(np.max(np.abs(x.mean(axis=1))))
            s0, s1, ein, eout = _edge_metrics(x)
            cent = _centroid(x)
            levels.append(lu)
            takes.append(x)
            flag = []
            if peak > -1.0: flag.append("PEAK")
            if tp > -1.0: flag.append("TRUEPEAK")
            if dc > 1e-3: flag.append("DC")
            # Vorbis leaves codec ripple (~-50 dBFS) on the first samples; PCM must be exact.
            edge_tol = 1e-3 if fmt == "wav" else 4e-3
            if s0 > edge_tol or s1 > edge_tol: flag.append("EDGE")
            if ein > 0.05 or eout > 0.05: flag.append("CLICK")
            if abs(lu - target) > 1.0: flag.append("LOUDNESS")
            if not np.all(np.isfinite(x)): flag.append("NAN")
            if flag:
                problems.append(f"{p.name}: {' '.join(flag)}")
            print(f"{p.name:<30}{x.shape[0]:>3}{x.shape[1] / SR:>7.2f}{peak:>7.1f}{tp:>7.1f}{rms:>7.1f}"
                  f"{lu:>7.1f}{target:>6.0f}{dc:>9.1e}{ein:>7.2f}/{eout:<5.2f}{cent:>8.0f} {' '.join(flag)}")
        if len(takes) > 1:
            sim = max(_similarity(takes[i], takes[j]) for i in range(len(takes)) for j in range(i + 1, len(takes)))
            print(f"{'':<30}variant similarity (max xcorr) {sim:.2f}")
            if sim > 0.97:
                problems.append(f"{name}: variants nearly identical (xcorr {sim:.2f})")
        if levels:
            per_event[name] = (tier, float(np.median(levels)), float(np.ptp(levels)))
            if np.ptp(levels) > 1.5:
                problems.append(f"{name}: variant loudness spread {np.ptp(levels):.1f} LU")

    if per_event:
        print("\nLOUDNESS HIERARCHY (median of variants; each tier must sit at or above the one before)")
        prev_max, prev_tier = -99.0, None
        for tier in TIER_ORDER:
            items = [(n, l, s) for n, (t, l, s) in per_event.items() if t == tier]
            if not items:
                continue
            lo, hi = min(l for _, l, _ in items), max(l for _, l, _ in items)
            print(f"  {tier:<10} {lo:6.1f} .. {hi:6.1f} LUFS   " + ", ".join(f"{n} {l:.1f}" for n, l, _ in items))
            if prev_tier and lo < prev_max - 0.5:
                problems.append(f"tier {tier} min {lo:.1f} below {prev_tier} max {prev_max:.1f}")
            prev_max, prev_tier = max(prev_max, hi), tier

    if names is None or any(n.startswith("amb_") or n == "beds" for n in names):
        print("\nAMBIENT BEDS  (LUFS = integrated; seam: jump vs p99.9 step, 50 ms RMS step, spectral flux vs median)")
        print(f"{'file':<26}{'ch':>3}{'dur s':>7}{'peak':>7}{'LUFS':>7}{'tgt':>6}{'dc':>9}{'jump':>7}{'rms dB':>8}{'flux':>6}")
        for world in BEDS:
            if names is not None and "beds" not in names and f"amb_{world}" not in names:
                continue
            for flavor in ("a", "b"):
                p = bed_path(world, flavor)
                if not p.exists():
                    problems.append(f"missing {p.name}")
                    continue
                x = _read(p)
                peak = 20 * np.log10(np.max(np.abs(x)) + 1e-12)
                lu = lufs_integrated(x)
                dc = float(np.max(np.abs(x.mean(axis=1))))
                jump, rstep, flux = _seam_metrics(x)
                target = WORLDS[world][1]
                expect = BED_SECONDS[flavor]
                flag = []
                if x.shape[0] != 2: flag.append("MONO")
                if abs(x.shape[1] / SR - expect) > 0.05: flag.append("LENGTH")
                if peak > -1.0: flag.append("PEAK")
                if dc > 1e-3: flag.append("DC")
                if abs(lu - target) > 1.0: flag.append("LOUDNESS")
                if jump > 1.5 or abs(rstep) > 3.0 or flux > 2.5: flag.append("SEAM")
                if flag:
                    problems.append(f"{p.name}: {' '.join(flag)}")
                print(f"{p.name:<26}{x.shape[0]:>3}{x.shape[1] / SR:>7.2f}{peak:>7.1f}{lu:>7.1f}{target:>6.0f}"
                      f"{dc:>9.1e}{jump:>7.2f}{rstep:>8.2f}{flux:>6.2f} {' '.join(flag)}")

    total = sum(f.stat().st_size for f in OUT_DIR.glob("sfx_*") if f.suffix in (".wav", ".ogg"))
    print(f"\nTOTAL Sfx/ footprint: {total / 1e6:.2f} MB (budget {BUDGET_MB} MB)")
    if total / 1e6 > BUDGET_MB:
        problems.append(f"footprint {total / 1e6:.2f} MB over budget")

    if problems:
        ok = False
        print("\nPROBLEMS:")
        for pr in problems:
            print("  - " + pr)
    else:
        print("\nAll checks passed.")
    return ok


# --------------------------------------------------------------------------------------------
# Audition sheet
# --------------------------------------------------------------------------------------------

def audition():
    """Every variant back to back, then each bed's loop seam (last 5 s -> first 5 s)."""
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    parts, lines, t = [], [], 0.0

    def push(x, label, gap):
        nonlocal t
        if x.shape[0] == 1:
            x = np.vstack([x[0], x[0]]) * 0.7071
        lines.append(f"{int(t // 60):02d}:{t % 60:05.2f}  {label}")
        parts.append(x)
        parts.append(np.zeros((2, ns(gap))))
        t += x.shape[1] / SR + gap

    for name, (_fn, _t, _tier, fmt, _d) in EVENTS.items():
        for v in range(VARIANTS):
            p = oneshot_path(name, v, fmt)
            if p.exists():
                push(_read(p), f"{name} v{v + 1}", 0.45 if v < VARIANTS - 1 else 1.1)
    for world in BEDS:
        for flavor in ("a", "b"):
            p = bed_path(world, flavor)
            if p.exists():
                x = _read(p)
                w = ns(5.0)
                seam = np.concatenate([x[:, -w:], x[:, :w]], axis=1)
                push(fades(seam, 0.3, 0.3) * 1.6, f"amb_{world}{'' if flavor == 'a' else '_b'} (loop seam at +5.0 s)", 1.2)
    sheet = np.concatenate(parts, axis=1)
    sheet = limit(sheet, -1.0)
    out = PREVIEW_DIR / "all_sfx.ogg"
    write_ogg(out, sheet.T, 0.4)
    (PREVIEW_DIR / "all_sfx.txt").write_text("\n".join(lines) + "\n")
    print(f"\nAudition sheet: {out} ({sheet.shape[1] / SR:.1f} s), index: {PREVIEW_DIR / 'all_sfx.txt'}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="comma-separated events (e.g. bite,claim), amb_<world>, or 'beds'")
    ap.add_argument("--check", action="store_true", help="verify files on disk without rendering")
    ap.add_argument("--no-preview", action="store_true", help="skip the audition sheet")
    args = ap.parse_args()
    only = {s.strip() for s in args.only.split(",")} if args.only else None
    t0 = time.time()
    if not args.check:
        print(f"Rendering to {OUT_DIR}")
        render(only)
    ok = check(only)
    if not args.no_preview and (only is None or args.check):
        audition()
    print(f"\nDone in {time.time() - t0:.1f}s")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
