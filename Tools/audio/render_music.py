#!/usr/bin/env python3
"""Offline composer / renderer for the Solar Majesty soundtrack.

Writes, for every world, four sample-locked seamless loop stems that AdaptiveMusic.cs crossfades
by mood, plus a title theme and victory / defeat stings:

    Assets/Resources/Audio/Music/<world>_{bed,rhythm,harmony,threat}.ogg
    Assets/Resources/Audio/Music/title_theme.ogg
    Assets/Resources/Audio/Music/sting_victory.ogg, sting_defeat.ogg

Usage:
    Tools/audio/.venv/bin/python Tools/audio/render_music.py            # render all + check
    Tools/audio/.venv/bin/python Tools/audio/render_music.py --world mars
    Tools/audio/.venv/bin/python Tools/audio/render_music.py --check    # analyse existing files

Everything is deterministic (fixed seeds). See music_dsp.py for the loop-seam strategy: stems are
rendered on circular buffers, so the loop point is not faded, it is simply continuous.

Loudness method: ITU-R BS.1770-4 integrated loudness implemented here (K-weighting biquads
re-derived for 44.1 kHz, 400 ms blocks with 75% overlap, -70 LUFS absolute and -10 LU relative
gates). "Work mix" means the stems summed at the Work levels in MOOD_LEVELS, which must match
AdaptiveMusic.cs.
"""

from __future__ import annotations

import argparse
import itertools
import os
import sys
import time
import zlib

import numpy as np
import soundfile as sf
from scipy import signal
from scipy.ndimage import uniform_filter1d

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import music_dsp as D  # noqa: E402
from music_dsp import SR  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "Audio", "Music")
PREVIEW_DIR = ("/private/tmp/claude-501/-Volumes-Storage-Projects-IRLobby/"
               "25b273c1-6bc0-46cd-b1d4-69313852fa43/scratchpad/music_preview")

STEMS = ("bed", "rhythm", "harmony", "threat")
WORLDS = ("earth", "luna", "mars", "belt", "europa")

# Must match AdaptiveMusic.AuthoredLevels (bed, rhythm, harmony, threat).
MOOD_LEVELS = {
    "calm": (1.00, 0.00, 0.70, 0.00),
    "work": (0.90, 0.90, 0.75, 0.00),
    "tension": (0.85, 0.60, 0.45, 0.65),
    "crisis": (0.75, 1.00, 0.25, 1.00),
}
MAX_LEVELS = tuple(max(v[i] for v in MOOD_LEVELS.values()) for i in range(4))

WORK_LUFS = -17.0          # target for the Work mix
TITLE_LUFS = -16.0
CEIL_DB = -1.0             # hard requirement after decoding
INTERNAL_CEIL_DB = -1.7    # limiter ceiling before Vorbis (codec overshoot margin)
VORBIS_LEVEL = 0.6         # libsndfile compression_level: 0.6 ~ Vorbis q4 (~115-130 kbps)
# Tempo / length per piece (bars of 4/4). Loop length = bars * 4 beats.
META = {
    "earth": dict(tonic=65, mode="ionian", bpm=96, bars=32, air=2.0),     # F major, 80.0 s
    "luna": dict(tonic=64, mode="dorian", bpm=72, bars=24, air=3.0),      # E dorian, 80.0 s
    "mars": dict(tonic=57, mode="dorian", bpm=100, bars=32, air=1.0),     # A dorian, 76.8 s
    "belt": dict(tonic=67, mode="aeolian", bpm=120, bars=40, air=4.0),    # G minor, 80.0 s
    "europa": dict(tonic=59, mode="aeolian", bpm=64, bars=20, air=0.0),   # B minor, 75.0 s
    "title": dict(tonic=62, mode="ionian", bpm=88, bars=32, air=2.0),     # D major, 87.3 s
}
# Loop files do not start on bar 1. The music is periodic, so the file boundary can sit anywhere;
# choose_rotation() puts it at the quietest between-16ths point across all four stems (one
# offset per world, so the stems stay sample-locked). The Vorbis encoder then never has to start
# or end a file on a transient, which is where codec edge error lives.
STEM_BALANCE = {"bed": -20.0, "rhythm": -21.0, "harmony": -22.0, "threat": -20.0}


# =============================================================================================
# music theory

MODES = {
    "ionian": (0, 2, 4, 5, 7, 9, 11),
    "dorian": (0, 2, 3, 5, 7, 9, 10),
    "aeolian": (0, 2, 3, 5, 7, 8, 10),
    "mixolydian": (0, 2, 4, 5, 7, 9, 10),
}

# The leitmotif, in scale degrees (1 = tonic, 8 = octave) and beats. A rising 1-5-8 "we can do
# this" call, a stepwise settle, then an answer that climbs past the octave and lands open on 5
# so the loop always wants to go round again.
MOTIF_A = [(1, 1), (5, 1), (8, 2), (7, .5), (6, .5), (5, 1), (3, 2),
           (4, 1), (5, 1), (6, 1.5), (5, .5), (3, 1), (2, 1), (1, 2)]
MOTIF_B = [(3, 1), (5, 1), (8, 1.5), (9, .5), (10, 2), (9, 1), (8, 1),
           (6, 1), (5, 1), (4, 1), (3, 1), (2, 2), (5, 2)]
COUNTER = [(5, 2), (6, 2), (8, 3), (7, 1), (6, 2), (5, 2), (3, 4)]
FRAGMENT = [(1, 1), (5, 1), (8, 2)]


class Comp:
    """Time grid + key + harmony for one piece. All positions are in beats from loop start."""

    def __init__(self, name, tonic, mode, bpm, bars, prog, seed, cyclic=True, seconds=None,
                 air=0.0):
        self.name = name
        self.air = air
        self.tonic = tonic
        self.scale = MODES[mode]
        self.mode = mode
        self.bpm = bpm
        self.bars = bars
        self.spb = 60.0 / bpm * SR
        self.cyclic = cyclic
        self.L = int(round(bars * 4 * self.spb)) if seconds is None else int(seconds * SR)
        self.prog = [prog[i % len(prog)] for i in range(bars)]
        self.seed = seed

    # --- time
    def rng(self, tag):
        return np.random.default_rng(self.seed * 7919 + zlib.crc32(tag.encode()))

    def bpos(self, beat):
        return int(round(beat * self.spb))

    def sec(self, beats):
        return beats * 60.0 / self.bpm

    def track(self):
        return D.Track(self.L, self.cyclic)

    # --- pitch
    def deg(self, d):
        o, k = divmod(d - 1, 7)
        return self.tonic + 12 * o + self.scale[k]

    def chord_at(self, beat):
        bar = int(beat // 4) % self.bars
        e = self.prog[bar]
        if isinstance(e, list):
            b = beat - (beat // 4) * 4
            acc = 0.0
            for spec, beats in e:
                acc += beats
                if b < acc - 1e-6:
                    return spec
            return e[-1][0]
        return e

    def segments(self):
        """(start_beat, length_beats, chord) with repeated chords merged."""
        raw = []
        for bar, e in enumerate(self.prog):
            if isinstance(e, list):
                b = bar * 4.0
                for spec, beats in e:
                    raw.append([b, beats, spec])
                    b += beats
            else:
                raw.append([bar * 4.0, 4.0, e])
        segs = []
        for s in raw:
            if segs and segs[-1][2] == s[2]:
                segs[-1][1] += s[1]
            else:
                segs.append(list(s))
        return [tuple(s) for s in segs]

    @staticmethod
    def chord_degs(spec):
        d, fl = spec
        degs = [d, d + 2, d + 4]
        if "s2" in fl:
            degs[1] = d + 1
        if "s4" in fl:
            degs[1] = d + 3
        if "7" in fl:
            degs.append(d + 6)
        if "9" in fl:
            degs.append(d + 8)
        return degs

    def pcs(self, spec):
        return {self.deg(x) % 12 for x in self.chord_degs(spec)}

    def chord_midis(self, spec, lo, hi):
        pcs = self.pcs(spec)
        return [m for m in range(lo, hi + 1) if m % 12 in pcs]

    def root(self, spec, lo):
        pc = self.deg(spec[0]) % 12
        m = lo
        while m % 12 != pc:
            m += 1
        return m

    def fifth_above(self, spec, root):
        pc = self.deg(self.chord_degs(spec)[2]) % 12
        m = root + 1
        while m % 12 != pc:
            m += 1
        return m

    def voice(self, spec, lo, hi, prev=None, n=4):
        """Pick a voicing in [lo, hi] with minimal movement from the previous chord."""
        degs = self.chord_degs(spec)
        pcs = [self.deg(x) % 12 for x in degs][:n]
        while len(pcs) < n:
            pcs.append(pcs[0])
        options = [[m for m in range(lo, hi + 1) if m % 12 == pc] for pc in pcs]
        best, best_cost = None, 1e9
        center = (lo + hi) / 2.0
        for combo in itertools.product(*options):
            v = sorted(combo)
            if len(set(v)) < len(v):
                continue
            gaps = np.diff(v)
            if np.any(gaps < 2) or v[-1] - v[0] > 19:
                continue
            if prev is not None:
                cost = sum(abs(a - b) for a, b in zip(v, prev))
            else:
                cost = abs(np.mean(v) - center) * 2
            cost += 0.3 * abs(np.mean(v) - center)
            if cost < best_cost:
                best, best_cost = v, cost
        if best is None:
            best = sorted(o[0] for o in options)
        return best

    def fit(self, d, spec):
        pcs = self.pcs(spec)
        if self.deg(d) % 12 in pcs:
            return d
        for alt in (d - 1, d + 1):
            if self.deg(alt) % 12 in pcs:
                return alt
        return d


# =============================================================================================
# placement helpers

def place_pads(c, tr, synth, lo, hi, gain=1.0, n=4, pan=0.0, octave=0, legato=1.02, rng=None):
    prev = None
    rng = rng or c.rng("pads")
    for start, length, spec in c.segments():
        v = c.voice(spec, lo, hi, prev, n)
        prev = v
        dur = c.sec(length) * legato
        for k, m in enumerate(v):
            f = D.midi_hz(m + 12 * octave)
            sig = synth(f, dur, rng)
            tr.add(c.bpos(start), sig, pan, gain / n)


def place_bass_sustain(c, tr, synth, lo, gain=1.0, legato=0.98):
    for start, length, spec in c.segments():
        m = c.root(spec, lo)
        tr.add(c.bpos(start), synth(D.midi_hz(m), c.sec(length) * legato), 0.0, gain)


def place_melody(c, tr, phrase, bar, synth, octave=0, gain=1.0, pan=0.0, stretch=1.0,
                 shift=0, fit=True, vel=0.9, legato=0.94, rng=None, human_ms=6.0):
    rng = rng or c.rng(f"mel{bar}{shift}")
    beat = bar * 4.0
    prev = None
    for d, b in phrase:
        b *= stretch
        if d is not None:
            dd = d + shift
            spec = c.chord_at(beat)
            if fit and b >= 1.5:
                dd = c.fit(dd, spec)
            f = D.midi_hz(c.deg(dd) + 12 * octave)
            accent = 1.0 if abs(beat % 2) < 1e-6 else 0.86
            sig = synth(f, c.sec(b) * legato, vel * accent, prev)
            j = int(rng.uniform(-human_ms, human_ms) * 1e-3 * SR)
            tr.add(c.bpos(beat) + j, sig, pan, gain)
            prev = f
        beat += b


def place_arp(c, tr, synth, lo, hi, bars, step=0.5, order="updown", gain=1.0, prob=1.0,
              rng=None, pans=(-0.35, 0.35), accents=(1.0, 0.72), human_ms=3.0, gate=0.9):
    rng = rng or c.rng("arp")
    k = 0
    per_bar = int(round(4 / step))
    for bar in bars:
        for s in range(per_bar):
            beat = bar * 4.0 + s * step
            spec = c.chord_at(beat)
            tones = c.chord_midis(spec, lo, hi)
            if order == "up":
                seq = tones
            elif order == "down":
                seq = tones[::-1]
            elif order == "updown":
                seq = tones + tones[-2:0:-1]
            else:  # "leap": root-high alternation
                seq = [tones[i // 2] if i % 2 == 0 else tones[-1 - i // 2] for i in range(len(tones))]
            m = seq[k % len(seq)]
            k += 1
            if prob < 1.0 and rng.random() > prob:
                continue
            on_beat = abs((s * step) % 1.0) < 1e-6
            v = accents[0] if on_beat else accents[1]
            v *= rng.uniform(0.9, 1.0)
            sig = synth(D.midi_hz(m), c.sec(step) * gate, v)
            j = int(rng.uniform(-human_ms, human_ms) * 1e-3 * SR)
            tr.add(c.bpos(beat) + j, sig, pans[k % len(pans)], gain)


VEL = {"x": 0.8, "X": 1.0, "g": 0.38, "o": 0.7, "a": 0.55}


def place_pattern(c, tr, hit, patterns, bars, gain=1.0, pan=0.0, swing=0.0, human_ms=3.0,
                  rng=None, steps=16, pan_jitter=0.0):
    """patterns: a string, a list cycled per bar, or a callable bar -> string."""
    rng = rng or c.rng("pat")
    for bar in bars:
        if callable(patterns):
            p = patterns(bar)
        elif isinstance(patterns, str):
            p = patterns
        else:
            p = patterns[bar % len(patterns)]
        stepb = 4.0 / steps
        for i, ch in enumerate(p):
            if ch not in VEL:
                continue
            beat = bar * 4.0 + i * stepb
            if i % 2 == 1:
                beat += swing * stepb
            v = VEL[ch] * rng.uniform(0.9, 1.0)
            j = int(rng.uniform(-human_ms, human_ms) * 1e-3 * SR)
            pj = pan + (rng.uniform(-pan_jitter, pan_jitter) if pan_jitter else 0.0)
            tr.add(c.bpos(beat) + j, hit(rng, v), pj, gain)


def place_brass(c, tr, lo, every_bars=2, gain=1.0, swell_beats=3.0, fifth=True, rng=None,
                bars=None, vel=1.0, **kw):
    rng = rng or c.rng("brass")
    bars = bars if bars is not None else range(0, c.bars, every_bars)
    for bar in bars:
        spec = c.chord_at(bar * 4.0)
        r = c.root(spec, lo)
        notes = [r, c.fifth_above(spec, r)] if fifth else [r]
        dur = c.sec(every_bars * 4) * 0.96
        for m in notes:
            sig = D.brass(D.midi_hz(m), dur, vel, rng, swell=c.sec(swell_beats), **kw)
            tr.add(c.bpos(bar * 4.0), sig, 0.0, gain / len(notes))


def place_cluster(c, tr, lo, synth, bars, gain=1.0, span_beats=8.0, trem_steps=0.25):
    """Diatonic cluster (root, 2nd, 3rd of the chord's scale position): tense but in key."""
    for bar in bars:
        spec = c.chord_at(bar * 4.0)
        d = spec[0]
        base = c.root(spec, lo)
        for dd in (d, d + 1, d + 2):
            m = base + (c.deg(dd) - c.deg(d))
            sig = synth(D.midi_hz(m), c.sec(span_beats))
            n = sig.shape[-1]
            t = np.arange(n) / SR
            trem = 0.62 + 0.38 * np.cos(D.TAU * t / c.sec(trem_steps))
            tr.add(c.bpos(bar * 4.0), sig * trem, 0.0, gain / 3)


def place_risers(c, tr, every_bars=8, length_beats=8.0, gain=1.0, rng=None, tone_midi=None, **kw):
    rng = rng or c.rng("riser")
    for end_bar in range(every_bars, c.bars + 1, every_bars):
        end = end_bar * 4.0
        start = end - length_beats
        sig = D.riser(rng, c.sec(length_beats), 1.0,
                      tone=D.midi_hz(tone_midi) if tone_midi else None, **kw)
        tr.add(c.bpos(start), sig, 0.0, gain)


def drone(c, midi, gain=1.0, lfo_cycles=2, saw_mix=0.2, fc=500.0, width=0.5):
    """Free-running drone tuned so it completes whole cycles per loop (seamless)."""
    L = c.L
    f = D.periodic_hz(float(D.midi_hz(midi)), L)
    f2 = D.periodic_hz(float(D.midi_hz(midi)) * 1.003, L)
    a = D.sine(f, L) + saw_mix * D.saw(f, L)
    b = D.sine(f2, L, 0.37) + saw_mix * D.saw(f2, L, 0.37)
    x = np.stack([a + width * (b - a) * 0.5, b - width * (b - a) * 0.5])
    x = D.cyclic(lambda z: D.butter(z, fc, "low", 2), x)
    amp = 0.8 + 0.2 * D.lfo(L, lfo_cycles, 0.3)
    return x * amp * gain


def pump_env(c, beats, depth=0.4, tau=0.14):
    L = c.L
    imp = np.zeros(L)
    for bar in range(c.bars):
        for b in beats:
            imp[c.bpos(bar * 4.0 + b) % L] = 1.0
    n = int(0.8 * SR)
    t = np.arange(n) / SR
    k = np.minimum(1.0, t / 0.004) * np.exp(-t / tau)
    kk = np.zeros(L)
    kk[:n] = k
    dip = np.fft.irfft(np.fft.rfft(imp) * np.fft.rfft(kk), n=L)
    return 1.0 - depth * np.clip(dip, 0.0, 1.0)


# =============================================================================================
# parts / stems

class Part:
    def __init__(self, c, send=0.25, fx=None, name=""):
        self.tr = c.track()
        self.send = send
        self.fx = fx
        self.name = name

    def out(self):
        x = self.tr.buf
        return self.fx(x) if self.fx is not None else x


def build_stem(c, parts, ir, hp=28.0, glue=(4.0, 1.8)):
    """Sum parts, add the shared reverb, DC/rumble high-pass, and glue-compress.

    ``glue`` = (threshold above the stem's own RMS in dB, ratio): only the loudest moments are
    tamed, which lowers crest factor so the Work mix can reach its loudness under the ceiling.
    """
    dry = np.zeros((2, c.L))
    send = np.zeros((2, c.L))
    for p in parts:
        x = p.out()
        dry += x
        send += x * p.send
    tilt = (lambda z: D.shelf_high(z, 3500.0, c.air)) if c.air else (lambda z: z)
    if c.cyclic:
        wet = D.reverb_cyclic(send, ir, 1.0)
        x = D.cyclic(lambda z: tilt(D.butter(z, hp, "high", 2)), dry + wet)
    else:
        wet = D.reverb_linear(send, ir, 1.0)
        x = tilt(D.butter(dry + wet, hp, "high", 2))
    if glue is not None:
        x, _ = D.glue_compress(x, float(D.db(rms(x))) + glue[0], glue[1],
                               mode="wrap" if c.cyclic else "nearest")
    return x


def cyc(c, fn):
    """Wrap an IIR fx so it is loop-safe for cyclic comps."""
    if c.cyclic:
        return lambda x: D.cyclic(fn, x)
    return fn


def sweep(c, lo, hi, cycles=1, phase=0.0, q=0.9):
    """Slow resonant low-pass sweep across the loop (integer cycles so the seam matches)."""
    def fx(x):
        u = 0.5 + 0.5 * D.lfo(x.shape[-1], cycles, phase) if c.cyclic else \
            0.5 + 0.5 * np.sin(D.TAU * np.arange(x.shape[-1]) / x.shape[-1] * cycles + phase)
        fc = lo * (hi / lo) ** u
        return D.lp_tv(x, fc, q, block=128)
    if c.cyclic:
        # The cutoff curve itself must be pre-rolled with the signal.
        def wrapped(x):
            L = x.shape[-1]
            pad = int(3.0 * SR)
            u = 0.5 + 0.5 * D.lfo(L, cycles, phase)
            fc = lo * (hi / lo) ** u
            ext = np.concatenate([x[:, L - pad:], x], axis=1)
            fce = np.concatenate([fc[L - pad:], fc])
            return D.lp_tv(ext, fce, q, block=128)[:, pad:]
        return wrapped
    return fx


def rms(x):
    return float(np.sqrt(np.mean(x ** 2)))


# =============================================================================================
# instrument presets (closures with a fixed signature)

def s_pad_warm(f, dur, rng):
    return D.pad_saw(f, dur, rng, voices=3, detune=11.0, attack=0.9, release=1.6, fc=2600.0)


def s_strings(f, dur, rng):
    return D.pad_saw(f, dur, rng, voices=2, detune=7.0, attack=1.8, release=1.8, fc=3600.0,
                     width=1.0)


def s_bed_bass(f, dur):
    return D.bass(f, dur, 0.9, fc=170.0, env_amt=160.0, fdecay=0.4, q=0.8, sub=0.7, drive=0.8,
                  attack=0.09, release=0.5, decay=2.0, sustain=0.8)


# =============================================================================================
# WORLDS

def world_earth():
    prog = [(1, ""), (3, ""), (4, ""), [((5, "s4"), 2), ((5, ""), 2)],
            (6, "7"), (4, "7"), (2, "7"), (5, "")]
    c = Comp("earth", prog=prog, seed=11, **META["earth"])
    ir = D.make_ir(3.0, 101, predelay=0.025, damp=0.55)

    # bed: warm supersaw pad + high strings + round sustained bass
    pad = Part(c, 0.35, sweep(c, 1300, 3400, 1, -1.2), "pad")
    place_pads(c, pad.tr, s_pad_warm, 53, 74, gain=1.0)
    strings = Part(c, 0.45, cyc(c, lambda x: D.chorus_cyclic(x, 5, 12, 3, 0.4) if c.cyclic else x), "str")
    place_pads(c, strings.tr, s_strings, 65, 84, gain=0.35, n=3)
    bb = Part(c, 0.05, None, "bass")
    place_bass_sustain(c, bb.tr, s_bed_bass, 36, gain=0.55)
    bed = [pad, strings, bb]

    # rhythm: FM e-piano arpeggio with dotted-8th delay, bouncing bass pluck, soft kit
    ep = Part(c, 0.25, None, "ep")
    place_arp(c, ep.tr, lambda f, d, v: D.epiano(f, d, v), 60, 81, range(c.bars), step=0.5,
              order="updown", gain=0.5, rng=c.rng("ep"))
    d = c.bpos(0.75)
    ep.fx = lambda x: x + 0.32 * D.delay_cyclic(x, d, 0.42, 6)
    bp = Part(c, 0.05, None, "bpluck")
    for bar in range(c.bars):
        for s in range(8):
            beat = bar * 4 + s * 0.5
            spec = c.chord_at(beat)
            r = c.root(spec, 36) + (12 if s % 2 else 0)
            bp.tr.add(c.bpos(beat), D.bass(D.midi_hz(r), c.sec(0.42), 0.8 if s % 2 == 0 else 0.55,
                                           fc=220, env_amt=1100, fdecay=0.07, q=1.2, sub=0.4,
                                           drive=1.1), 0.0, 0.26)
    kit = Part(c, 0.12, None, "kit")
    place_pattern(c, kit.tr, lambda r, v: D.kick(r, v, 120, 48, 0.04, 0.3, 0.15, 1.2),
                  ["x.......x.....x.", "x.......x.......", "x.......x.....x.", "x.......x..x.x.."],
                  range(c.bars), gain=0.55)
    place_pattern(c, kit.tr, lambda r, v: D.hat(r, v, 0.035, 0.3, 7500),
                  "..x...x...x...x.", range(c.bars), gain=0.18, pan=0.3)
    place_pattern(c, kit.tr, lambda r, v: D.shaker(r, v, 0.04),
                  "gggagggagggagggx", range(c.bars), gain=0.14, pan=-0.35, swing=0.12)
    place_pattern(c, kit.tr, lambda r, v: D.clap(r, v), "....a.......a...", range(8, c.bars),
                  gain=0.22)
    rhythm = [ep, bp, kit]

    # harmony: horn-synth motif, glockenspiel counter-line
    ld = Part(c, 0.3, None, "lead")
    lead = lambda f, d, v, p: D.lead(f, d, v, p, kind="horn", fc=1500, attack=0.05, vib=0.004)
    place_melody(c, ld.tr, MOTIF_A, 0, lead, gain=0.55, pan=0.1)
    place_melody(c, ld.tr, MOTIF_B, 4, lead, gain=0.55, pan=0.1)
    place_melody(c, ld.tr, MOTIF_A, 16, lead, gain=0.55, pan=0.1)
    place_melody(c, ld.tr, MOTIF_B, 20, lead, gain=0.55, pan=0.1)
    place_melody(c, ld.tr, MOTIF_A, 16, lead, shift=-2, gain=0.26, pan=-0.25)
    place_melody(c, ld.tr, MOTIF_B, 20, lead, shift=-2, gain=0.26, pan=-0.25)
    d = c.bpos(1.5)
    ld.fx = lambda x: x + 0.18 * D.delay_cyclic(x, d, 0.35, 4)
    bl = Part(c, 0.4, None, "bells")
    bell = lambda f, d, v, p: D.fm_bell(f, d, v, ratio=2.0, index=1.4, decay=1.1)
    place_melody(c, bl.tr, COUNTER, 8, bell, octave=1, gain=0.3, pan=-0.3)
    place_melody(c, bl.tr, COUNTER, 12, bell, octave=1, gain=0.3, pan=-0.3, shift=2)
    for b in (24, 26, 28):
        place_melody(c, bl.tr, FRAGMENT, b, bell, octave=1, gain=0.26, pan=0.35)
    place_melody(c, bl.tr, [(5, 2), (3, 2), (2, 4)], 30, bell, octave=1, gain=0.24, pan=0.35)
    harmony = [ld, bl]

    # threat: taiko + toms, low brass swells, diatonic cluster tremolo, risers
    dr = Part(c, 0.2, None, "drums")
    place_pattern(c, dr.tr, lambda r, v: D.taiko(r, v, 64, 0.6),
                  ["X.....x.x.....x.", "X.....x.x...x.x."], range(c.bars), gain=0.6)
    place_pattern(c, dr.tr, lambda r, v: D.tom(r, v, 180, 110, 0.3),
                  ["..............xx", "......x.......xx", "..........x.x.xx", "......x...xxxxxx"],
                  range(c.bars), gain=0.32, pan_jitter=0.5)
    place_pattern(c, dr.tr, lambda r, v: D.boom(r, v), "X...............", range(0, c.bars, 8),
                  gain=0.5)
    br = Part(c, 0.3, None, "brass")
    place_brass(c, br.tr, 36, 2, gain=0.55, swell_beats=4.0, fc_hi=1300, drive=2.4)
    cl = Part(c, 0.35, None, "cluster")
    place_cluster(c, cl.tr, 53, lambda f, d: D.pad_saw(f, d, c.rng("cl"), 2, 9, 0.6, 1.0, 1400.0),
                  range(1, c.bars, 2), gain=0.4, span_beats=4.0)
    rs = Part(c, 0.4, None, "risers")
    place_risers(c, rs.tr, 8, 8.0, 0.12)
    threat = [dr, br, cl, rs]
    return c, ir, {"bed": bed, "rhythm": rhythm, "harmony": harmony, "threat": threat}


def world_luna():
    prog = [(1, "9"), (1, "9"), (4, ""), (4, "9"), (7, ""), (7, "s2"), (5, "7"),
            [((5, "s4"), 2), ((5, ""), 2)]]
    c = Comp("luna", prog=prog, seed=23, **META["luna"])
    ir = D.make_ir(6.0, 202, predelay=0.04, damp=0.15, width=1.0)

    # bed: glass pad high, vacuum sub drone (tonic + fifth)
    gp = Part(c, 0.6, None, "glass")
    place_pads(c, gp.tr, lambda f, d, r: D.pad_glass(f, d, r, 1.6, 3.0), 64, 86, gain=1.0)
    dn = Part(c, 0.1, None, "drone")
    dn.tr.buf += drone(c, 28, 0.35, 3, 0.1, 180.0) + drone(c, 35, 0.16, 2, 0.05, 240.0)
    bed = [gp, dn]

    # rhythm: sonar pings, clock ticks, sparse triangle arp, soft sub-pulse
    pg = Part(c, 0.5, None, "ping")
    d_q = c.bpos(1.5)
    for bar in range(0, c.bars, 2):
        pg.tr.add(c.bpos(bar * 4.0), D.ping(D.midi_hz(c.deg(15)), 0.8, 0.5), -0.2, 0.28)
    pg.fx = lambda x: x + 0.55 * D.delay_cyclic(x, d_q, 0.5, 7, lp=5000)
    tk = Part(c, 0.25, None, "ticks")
    place_pattern(c, tk.tr, lambda r, v: D.tick(r, v, 3400, 0.004),
                  ["x.g.x.g.x.ggx.g.", "x.g.x.g.x.g.x.gg"], range(c.bars), gain=0.16,
                  pan_jitter=0.6)
    ar = Part(c, 0.5, None, "arp")
    place_arp(c, ar.tr, lambda f, d, v: D.pluck(f, d, v, "tri", 2000, 3000, 0.2, 0.8, 0.5),
              76, 93, range(c.bars), step=0.5, order="up", gain=0.28, prob=0.62,
              rng=c.rng("larp"), pans=(-0.6, 0.6))
    d_e = c.bpos(0.75)
    ar.fx = lambda x: x + 0.4 * D.delay_cyclic(x, d_e, 0.45, 6, lp=6000)
    sp = Part(c, 0.05, None, "subpulse")
    place_pattern(c, sp.tr, lambda r, v: D.kick(r, v, 85, 41, 0.05, 0.28, 0.0, 1.0),
                  "x.......x.......", range(c.bars), gain=0.35)
    rhythm = [pg, tk, ar, sp]

    # harmony: glass flute motif an octave up, FM glass bells
    ld = Part(c, 0.55, None, "lead")
    lead = lambda f, d, v, p: D.lead(f, d, v, p, kind="tri", fc=3200, attack=0.08, vib=0.003,
                                     glide=0.03, bloom=0.4, release=0.6)
    place_melody(c, ld.tr, MOTIF_A, 0, lead, octave=1, gain=0.5, pan=0.15)
    place_melody(c, ld.tr, MOTIF_B, 4, lead, octave=1, gain=0.5, pan=0.15)
    place_melody(c, ld.tr, MOTIF_B, 20, lead, octave=0, gain=0.42, pan=0.15)
    bl = Part(c, 0.6, None, "bells")
    bell = lambda f, d, v, p: D.fm_bell(f, d, v, ratio=3.5, index=1.6, decay=2.2, idecay=0.5)
    place_melody(c, bl.tr, COUNTER, 8, bell, octave=1, gain=0.26, pan=-0.35)
    place_melody(c, bl.tr, COUNTER, 12, bell, octave=1, gain=0.26, pan=0.35, shift=-2)
    place_melody(c, bl.tr, MOTIF_A, 16, bell, octave=1, gain=0.22, pan=-0.2)
    harmony = [ld, bl]

    # threat: slow beating low cluster, timpani, hiss swells
    lo = Part(c, 0.25, None, "lowcluster")
    place_cluster(c, lo.tr, 40, lambda f, d: D.sine_tone(f, d, 1.0, 1.2, 1.4, 0.3),
                  range(0, c.bars, 2), gain=0.8, span_beats=8.0, trem_steps=2.0)
    tp = Part(c, 0.35, None, "timp")
    place_pattern(c, tp.tr, lambda r, v: D.taiko(r, v, 55, 0.9),
                  ["X.......x.......", "X.......x...x.x.", "X.......x.......", "X...x...x.x.xxxx"],
                  range(c.bars), gain=0.55)
    hs = Part(c, 0.4, None, "hiss")
    place_risers(c, hs.tr, 4, 8.0, 0.14, fc0=400, fc1=4000)
    br = Part(c, 0.4, None, "brass")
    place_brass(c, br.tr, 28, 4, gain=0.45, swell_beats=8.0, fc_hi=700, drive=1.6)
    threat = [lo, tp, hs, br]
    return c, ir, {"bed": bed, "rhythm": rhythm, "harmony": harmony, "threat": threat}


def world_mars():
    prog = [(1, ""), (7, ""), (4, ""), (1, ""), (3, ""), (7, ""), (4, "s2"),
            [((5, "s4"), 2), ((5, ""), 2)]]
    c = Comp("mars", prog=prog, seed=37, **META["mars"])
    ir = D.make_ir(2.2, 303, predelay=0.015, damp=0.85, width=0.8)

    # bed: dusty saturated pad, wind/dust noise, gritty root bass
    pad = Part(c, 0.3, None, "pad")
    place_pads(c, pad.tr, lambda f, d, r: D.pad_saw(f, d, r, 3, 15.0, 0.7, 1.4, 1500.0), 52, 72)
    sw = sweep(c, 700, 2200, 2, 0.5, q=1.6)
    pad.fx = lambda x: D.saturate(sw(x) * 2.2, 1.8) / 2.2
    du = Part(c, 0.4, None, "dust")
    nz = c.rng("dust").standard_normal((2, c.L))
    dust = D.cyclic(lambda z: D.butter(D.butter(z, 1400.0, "low", 2), 250.0, "high", 1), nz)
    du.tr.buf += dust * (0.55 + 0.45 * D.lfo(c.L, 4, 0.0)) * 0.05
    sand = D.cyclic(lambda z: D.butter(z, [3500.0, 9000.0], "band", 2), c.rng("sand").standard_normal((2, c.L)))
    du.tr.buf += sand * (0.5 + 0.5 * D.lfo(c.L, 6, 1.3)) * 0.012
    bb = Part(c, 0.05, None, "bass")
    place_bass_sustain(c, bb.tr, lambda f, d: D.bass(f, d, 0.9, 150, 260, 0.3, 1.0, 0.6, 2.0,
                                                   0.06, 0.4, "saw", 2.0, 0.8), 33, 0.42)
    bed = [pad, du, bb]

    # rhythm: Karplus-Strong ostinato, syncopated bass, frame drum, gritty shaker, rim
    ks = Part(c, 0.2, None, "ks")
    rng = c.rng("ks")
    pat = "x.xx.x.xx.x.x.xx"
    for bar in range(c.bars):
        for i, ch in enumerate(pat):
            if ch != "x":
                continue
            beat = bar * 4 + i * 0.25
            spec = c.chord_at(beat)
            tones = c.chord_midis(spec, 57, 76)
            m = tones[(i * 3 + bar) % len(tones)]
            v = (0.95 if i % 4 == 0 else 0.65) * rng.uniform(0.85, 1.0)
            ks.tr.add(c.bpos(beat) + int(rng.uniform(-3, 3) * 44), D.ks_pluck(D.midi_hz(m), v, 0.994, 0.5, 1.2),
                      (-0.4 if i % 2 else 0.4), 0.32)
    d = c.bpos(0.75)
    ks.fx = lambda x: x + 0.22 * D.delay_cyclic(x, d, 0.35, 4, lp=2500)
    bs = Part(c, 0.03, None, "bassline")
    bpat = "x..x..x...x.x..."
    for bar in range(c.bars):
        for i, ch in enumerate(bpat):
            if ch != "x":
                continue
            beat = bar * 4 + i * 0.25
            r = c.root(c.chord_at(beat), 33) + (12 if i in (6, 12) else 0)
            bs.tr.add(c.bpos(beat), D.bass(D.midi_hz(r), c.sec(0.6), 0.9, 180, 1300, 0.09, 1.6,
                                           0.5, 2.2), 0.0, 0.26)
    kit = Part(c, 0.15, None, "kit")
    place_pattern(c, kit.tr, lambda r, v: D.tom(r, v, 110, 58, 0.28, 0.35),
                  ["x..x..x...x.....", "x..x..x...x..x..", "x..x..x...x.....", "x..x..x...x.x.x."],
                  range(c.bars), gain=0.38)
    place_pattern(c, kit.tr, lambda r, v: D.shaker(r, v, 0.045, grit=0.6),
                  "gaxagaxagaxagaxa", range(c.bars), gain=0.22, swing=0.18, pan=0.3)
    place_pattern(c, kit.tr, lambda r, v: D.tick(r, v, 1800, 0.012),
                  "....x.......x..g", range(c.bars), gain=0.3, pan=-0.2)
    pmp = pump_env(c, (0.0, 0.75, 1.5, 2.5), 0.25, 0.12)
    ks_fx = ks.fx
    ks.fx = lambda x: ks_fx(x) * pmp
    rhythm = [ks, bs, kit]

    # harmony: reedy pulse-wave lead, KS counter-melody
    ld = Part(c, 0.25, None, "lead")
    lead = lambda f, d, v, p: D.lead(f, d, v, p, kind="square", pw=0.32, fc=1700, attack=0.03,
                                     vib=0.006, glide=0.05, drive=1.3)
    for b, ph, sh, g, pn in ((0, MOTIF_A, 0, 0.5, 0.15), (4, MOTIF_B, 0, 0.5, 0.15),
                             (16, MOTIF_A, 0, 0.5, 0.15), (20, MOTIF_B, 0, 0.5, 0.15),
                             (16, MOTIF_A, -2, 0.22, -0.3), (20, MOTIF_B, -2, 0.22, -0.3)):
        place_melody(c, ld.tr, ph, b, lead, octave=1, gain=g, pan=pn, shift=sh)
    d = c.bpos(1.0)
    ld.fx = lambda x: x + 0.2 * D.delay_cyclic(x, d, 0.4, 4, lp=2200)
    kc = Part(c, 0.35, None, "kscounter")
    ksv = lambda f, d, v, p: D.ks_pluck(f, v, 0.997, 0.7, 2.0)
    place_melody(c, kc.tr, COUNTER, 8, ksv, octave=1, gain=0.45, pan=-0.3, legato=1.0)
    place_melody(c, kc.tr, COUNTER, 12, ksv, octave=1, gain=0.45, pan=0.3, shift=2)
    for b in (24, 26, 28, 30):
        place_melody(c, kc.tr, FRAGMENT, b, ksv, octave=1, gain=0.4, pan=0.2 if b % 4 else -0.2)
    harmony = [ld, kc]

    # threat: war toms, distorted brass, clap bursts, risers
    dr = Part(c, 0.18, None, "war")
    place_pattern(c, dr.tr, lambda r, v: D.taiko(r, v, 70, 0.5),
                  ["X.x.x.xXx.x.x.xx", "X.x.x.xXx.x.xxxx"], range(c.bars), gain=0.34)
    place_pattern(c, dr.tr, lambda r, v: D.tom(r, v, 220, 130, 0.22),
                  ["..............xx", "..........x.xxxx"], range(c.bars), gain=0.28, pan_jitter=0.6)
    place_pattern(c, dr.tr, lambda r, v: D.clap(r, v), "....x.......x.xx", range(3, c.bars, 4),
                  gain=0.25)
    br = Part(c, 0.25, None, "brass")
    place_brass(c, br.tr, 33, 2, gain=0.6, swell_beats=3.0, fc_hi=1800, drive=3.2)
    rs = Part(c, 0.3, None, "riser")
    place_risers(c, rs.tr, 8, 8.0, 0.13, tone_midi=45)
    threat = [dr, br, rs]
    return c, ir, {"bed": bed, "rhythm": rhythm, "harmony": harmony, "threat": threat}


def world_belt():
    prog = [(1, ""), (1, ""), (6, ""), (6, ""), (3, ""), (3, ""), (7, ""), (7, "s4")]
    c = Comp("belt", prog=prog, seed=41, **META["belt"])
    ir = D.make_ir(2.4, 404, predelay=0.012, damp=0.35, color=float(D.midi_hz(67 + 12)))

    # bed: metallic comb-resonated pad, square drone bass, machine hum
    pad = Part(c, 0.3, None, "pad")
    place_pads(c, pad.tr, lambda f, d, r: D.pad_saw(f, d, r, 3, 9.0, 0.5, 1.2, 3000.0), 55, 76)
    comb_d = int(round(SR / float(D.midi_hz(55))))
    pad.fx = lambda x: 0.6 * x + 0.8 * D.comb_cyclic(x, comb_d, 0.55, 12)
    bb = Part(c, 0.05, None, "bass")
    place_bass_sustain(c, bb.tr, lambda f, d: D.bass(f, d, 0.9, 200, 200, 0.3, 1.2, 0.8, 1.4,
                                                   0.04, 0.3, "square", 2.0, 0.85), 31, 0.36)
    hm = Part(c, 0.1, None, "hum")
    hm.tr.buf += drone(c, 43, 0.12, 5, 0.6, 900.0, 0.9)
    bed = [pad, bb, hm]

    # rhythm: acid 16ths, hammer kick, metal hats, girder clangs, sidechain pump
    ac = Part(c, 0.08, None, "acid")
    rng = c.rng("acid")
    offs = [0, 0, 12, 0, 7, 0, 10, 12, 0, 0, 12, 0, 3, 5, 7, 10]
    acc = "X.x.X.xxX.x.Xxxx"
    for bar in range(c.bars):
        for i in range(16):
            beat = bar * 4 + i * 0.25
            spec = c.chord_at(beat)
            r = c.root(spec, 31)
            m = r + offs[i]
            # keep every note in the scale (offsets are relative to the chord root)
            if (m - c.tonic) % 12 not in c.scale:
                m -= 1
            v = 1.0 if acc[i] == "X" else 0.7
            env_amt = 2600 + 1600 * (0.5 + 0.5 * np.sin(D.TAU * (bar * 16 + i) / (16 * 8)))
            ac.tr.add(c.bpos(beat), D.bass(D.midi_hz(m), c.sec(0.22), v, 220, env_amt, 0.07, 4.5,
                                           0.15, 1.8, release=0.03), 0.0, 0.24)
    kit = Part(c, 0.1, None, "kit")
    place_pattern(c, kit.tr, lambda r, v: D.kick(r, v, 170, 50, 0.03, 0.22, 0.5, 2.0),
                  "x...x...x...x...", range(c.bars), gain=0.48)
    place_pattern(c, kit.tr, lambda r, v: D.hat(r, v, 0.03, 0.8, 8000),
                  "gxgxgxgxgxgxgxgx", range(c.bars), gain=0.24, pan=0.25)
    place_pattern(c, kit.tr, lambda r, v: D.hat(r, v, 0.18, 0.8, 7000),
                  "..a...a...a...a.", range(c.bars), gain=0.17, pan=-0.25)
    place_pattern(c, kit.tr, lambda r, v: D.clang(r, v, float(D.midi_hz(c.deg(5))), 0.8),
                  ["......x.......x.", "......x....x..x."], range(c.bars), gain=0.26, pan_jitter=0.7)
    pmp = pump_env(c, (0.0, 1.0, 2.0, 3.0), 0.45, 0.13)
    ac.fx = lambda x: x * pmp
    fm = Part(c, 0.3, None, "fmseq")
    place_arp(c, fm.tr, lambda f, d, v: D.fm_bell(f, d, v, 1.0, 2.0, 0.25, 0.08), 67, 86,
              range(8, c.bars), step=0.25, order="leap", gain=0.14, prob=0.7, rng=c.rng("fmseq"),
              pans=(-0.5, 0.5))
    fm_d = c.bpos(0.75)
    fm.fx = lambda x: (x + 0.3 * D.delay_cyclic(x, fm_d, 0.4, 4)) * pmp
    rhythm = [ac, kit, fm]

    # harmony: bright saw lead with delay, bell arps
    ld = Part(c, 0.25, None, "lead")
    lead = lambda f, d, v, p: D.lead(f, d, v, p, kind="saw", fc=2600, q=1.5, attack=0.02,
                                     vib=0.004, glide=0.03, drive=1.4, bloom=1.4)
    for b, ph, sh, g, pn in ((0, MOTIF_A, 0, 0.42, 0.1), (4, MOTIF_B, 0, 0.42, 0.1),
                             (16, MOTIF_A, 0, 0.42, 0.1), (20, MOTIF_B, 0, 0.42, 0.1),
                             (16, MOTIF_A, -2, 0.2, -0.3), (20, MOTIF_B, -2, 0.2, -0.3),
                             (32, FRAGMENT, 0, 0.4, 0.1), (34, FRAGMENT, 2, 0.4, 0.1),
                             (36, MOTIF_A[:7], 0, 0.4, 0.1)):
        place_melody(c, ld.tr, ph, b, lead, gain=g, pan=pn, shift=sh)
    d = c.bpos(0.75)
    ld.fx = lambda x: x + 0.3 * D.delay_cyclic(x, d, 0.45, 5, lp=3000)
    bl = Part(c, 0.35, None, "bells")
    bell = lambda f, d, v, p: D.fm_bell(f, d, v, ratio=2.0, index=2.0, decay=0.9)
    place_melody(c, bl.tr, COUNTER, 8, bell, octave=1, gain=0.26, pan=-0.3)
    place_melody(c, bl.tr, COUNTER, 12, bell, octave=1, gain=0.26, pan=0.3, shift=-2)
    place_melody(c, bl.tr, COUNTER, 24, bell, octave=1, gain=0.26, pan=-0.3, shift=2)
    place_melody(c, bl.tr, COUNTER, 28, bell, octave=1, gain=0.26, pan=0.3)
    harmony = [ld, bl]

    # threat: industrial hits, toms, cluster pulses, distorted brass, risers
    dr = Part(c, 0.2, None, "hits")
    place_pattern(c, dr.tr, lambda r, v: D.clang(r, v, float(D.midi_hz(c.deg(1) - 24)), 1.6),
                  "X.......x.......", range(c.bars), gain=0.45)
    place_pattern(c, dr.tr, lambda r, v: D.boom(r, v, 80, 36, 0.8), "X.........x.....",
                  range(c.bars), gain=0.28)
    place_pattern(c, dr.tr, lambda r, v: D.tom(r, v, 200, 120, 0.2),
                  ["............xxxx", "......x.....xxxx", "..x...x...x.xxxx", "xxxxxxxxxxxxxxxx"],
                  range(c.bars), gain=0.24, pan_jitter=0.6)
    cp = Part(c, 0.25, None, "cluster")
    place_cluster(c, cp.tr, 55, lambda f, d: D.pad_saw(f, d, c.rng("bcl"), 2, 12, 0.02, 0.1, 2200.0),
                  range(c.bars), gain=0.36, span_beats=4.0, trem_steps=0.5)
    br = Part(c, 0.25, None, "brass")
    place_brass(c, br.tr, 31, 2, gain=0.55, swell_beats=2.0, fc_hi=2000, drive=3.5)
    rs = Part(c, 0.3, None, "riser")
    place_risers(c, rs.tr, 8, 8.0, 0.12, tone_midi=43)
    threat = [dr, cp, br, rs]
    return c, ir, {"bed": bed, "rhythm": rhythm, "harmony": harmony, "threat": threat}


def world_europa():
    prog = [(1, "9"), (1, "9"), (6, "7"), (6, "7"), (3, "9"), (3, "9"), (7, ""), (7, ""),
            (4, "7"), (4, "7"), (1, "9"), (1, "9"), (6, "7"), (6, "7"), (3, ""), (3, ""),
            (7, "s4"), (7, "s4"), (5, ""), [((5, "s4"), 2), ((5, ""), 2)]]
    c = Comp("europa", prog=prog, seed=53, **META["europa"])
    ir = D.make_ir(8.0, 505, predelay=0.05, damp=0.8)

    # bed: ice choir, deep drone, filtered saw undertow
    ch = Part(c, 0.6, None, "choir")
    place_pads(c, ch.tr, lambda f, d, r: D.pad_choir(f, d, r, 2.2, 3.5), 50, 71)
    dn = Part(c, 0.2, None, "drone")
    dn.tr.buf += drone(c, 35, 0.3, 2, 0.35, 260.0, 0.8) + drone(c, 23, 0.18, 1, 0.0, 120.0)
    bed = [ch, dn]

    # rhythm: heartbeat, ice ticks, slow glass arp with long delay, cracks
    hb = Part(c, 0.1, None, "heart")
    place_pattern(c, hb.tr, lambda r, v: D.kick(r, v, 80, 38, 0.06, 0.35, 0.0, 1.1),
                  "x..g........x..g", range(c.bars), gain=0.5)
    it = Part(c, 0.6, None, "ice")
    rng = c.rng("ice")
    ice_pats = ["".join("x" if rng.random() < 0.28 else ("g" if rng.random() < 0.2 else ".")
                        for _ in range(16)) for _ in range(4)]
    place_pattern(c, it.tr, lambda r, v: D.tick(r, v, 6200, 0.003), ice_pats, range(c.bars),
                  gain=0.13, pan_jitter=0.8)
    ga = Part(c, 0.55, None, "glassarp")
    place_arp(c, ga.tr, lambda f, d, v: D.fm_bell(f, d, v, 3.5, 1.2, 0.9, 0.2), 71, 90,
              range(c.bars), step=0.5, order="updown", gain=0.2, prob=0.85, rng=c.rng("ga"),
              pans=(-0.6, 0.6))
    d = c.bpos(1.5)
    ga.fx = lambda x: x + 0.45 * D.delay_cyclic(x, d, 0.5, 6, lp=4500)
    ck = Part(c, 0.5, None, "crack")
    rng = c.rng("crack")
    for bar in range(0, c.bars, 2):
        ck.tr.add(c.bpos(bar * 4 + rng.uniform(0.5, 7.5)), D.crack(rng, 0.8), rng.uniform(-0.7, 0.7), 0.35)
    rhythm = [hb, it, ga, ck]

    # harmony: whale-song sine lead (motif at half speed), ice bells
    ld = Part(c, 0.55, None, "whale")
    lead = lambda f, d, v, p: D.lead(f, d, v, p, kind="sine", fc=2600, attack=0.25, vib=0.006,
                                     glide=0.22, release=1.0, bloom=0.2)
    place_melody(c, ld.tr, MOTIF_A, 2, lead, octave=1, gain=0.55, pan=0.1, stretch=2.0)
    place_melody(c, ld.tr, MOTIF_B, 10, lead, octave=0, gain=0.5, pan=-0.1, stretch=2.0)
    bl = Part(c, 0.65, None, "bells")
    bell = lambda f, d, v, p: D.fm_bell(f, d, v, ratio=3.5, index=1.3, decay=2.8, idecay=0.6)
    place_melody(c, bl.tr, FRAGMENT, 0, bell, octave=1, gain=0.3, pan=-0.35)
    place_melody(c, bl.tr, COUNTER, 18, bell, octave=1, gain=0.26, pan=0.35, stretch=0.5)
    place_melody(c, bl.tr, FRAGMENT, 19, bell, octave=2, gain=0.2, pan=-0.35)
    harmony = [ld, bl]

    # threat: deep booms, crack storms, low swells, choir cluster
    bm = Part(c, 0.3, None, "boom")
    place_pattern(c, bm.tr, lambda r, v: D.boom(r, v, 60, 28, 1.8),
                  ["X...............", "X.......x......."], range(c.bars), gain=0.6)
    place_pattern(c, bm.tr, lambda r, v: D.taiko(r, v, 50, 1.0),
                  ["........x.......", "....x.......x.x."], range(c.bars), gain=0.35)
    cs = Part(c, 0.5, None, "cracks")
    rng = c.rng("storm")
    for bar in range(c.bars):
        for _ in range(3):
            cs.tr.add(c.bpos(bar * 4 + rng.uniform(0, 4)), D.crack(rng, rng.uniform(0.5, 1.0)),
                      rng.uniform(-0.8, 0.8), 0.3)
    br = Part(c, 0.45, None, "swell")
    place_brass(c, br.tr, 23, 2, gain=0.5, swell_beats=6.0, fc_hi=600, drive=1.8)
    cc = Part(c, 0.5, None, "cluster")
    place_cluster(c, cc.tr, 47, lambda f, d: D.pad_choir(f, d, c.rng("ecl"), 1.5, 2.0),
                  range(0, c.bars, 2), gain=0.8, span_beats=8.0, trem_steps=1.0)
    threat = [bm, cs, br, cc]
    return c, ir, {"bed": bed, "rhythm": rhythm, "harmony": harmony, "threat": threat}


WORLD_FNS = {"earth": world_earth, "luna": world_luna, "mars": world_mars, "belt": world_belt,
             "europa": world_europa}


# =============================================================================================
# title theme and stings (single mixes)

def title_theme():
    prog = [(1, ""), (3, ""), (4, ""), [((5, "s4"), 2), ((5, ""), 2)],
            (6, "7"), (4, "7"), (2, "7"), (5, "")]
    c = Comp("title", prog=prog, seed=71, **META["title"])
    ir = D.make_ir(3.4, 707, predelay=0.03, damp=0.5)
    parts = []

    pad = Part(c, 0.4, sweep(c, 1200, 3600, 1, -1.6), "pad")
    place_pads(c, pad.tr, s_pad_warm, 50, 72, gain=1.0)
    st = Part(c, 0.5, None, "str")
    place_pads(c, st.tr, s_strings, 62, 81, gain=0.4, n=3)
    bb = Part(c, 0.05, None, "bass")
    place_bass_sustain(c, bb.tr, s_bed_bass, 36, gain=0.75)
    parts += [pad, st, bb]

    # intro / outro bells so the loop point is a gentle breath, not a wall
    bl = Part(c, 0.45, None, "bells")
    bell = lambda f, d, v, p: D.fm_bell(f, d, v, ratio=2.0, index=1.4, decay=1.2)
    place_melody(c, bl.tr, FRAGMENT, 0, bell, octave=1, gain=0.3, pan=-0.3)
    place_melody(c, bl.tr, FRAGMENT, 2, bell, octave=1, gain=0.26, pan=0.3, shift=2)
    place_melody(c, bl.tr, COUNTER, 12, bell, octave=1, gain=0.3, pan=-0.3)
    place_melody(c, bl.tr, COUNTER, 16, bell, octave=1, gain=0.3, pan=0.3, shift=2)
    place_melody(c, bl.tr, FRAGMENT, 28, bell, octave=1, gain=0.28, pan=-0.3)
    place_melody(c, bl.tr, [(5, 2), (3, 2), (2, 4)], 30, bell, octave=1, gain=0.24, pan=0.3)
    parts.append(bl)

    ld = Part(c, 0.35, None, "lead")
    horn = lambda f, d, v, p: D.lead(f, d, v, p, kind="horn", fc=1600, attack=0.05, vib=0.004)
    place_melody(c, ld.tr, MOTIF_A, 4, horn, gain=0.55, pan=0.05)
    place_melody(c, ld.tr, MOTIF_B, 8, horn, gain=0.55, pan=0.05)
    place_melody(c, ld.tr, MOTIF_A, 20, horn, gain=0.6, pan=0.05)
    place_melody(c, ld.tr, MOTIF_B, 24, horn, gain=0.6, pan=0.05)
    place_melody(c, ld.tr, MOTIF_A, 20, horn, octave=-1, gain=0.32, pan=-0.2)
    place_melody(c, ld.tr, MOTIF_B, 24, horn, octave=-1, gain=0.32, pan=-0.2)
    place_melody(c, ld.tr, MOTIF_A, 20, horn, shift=-2, gain=0.22, pan=0.3)
    place_melody(c, ld.tr, MOTIF_B, 24, horn, shift=-2, gain=0.22, pan=0.3)
    d = c.bpos(1.5)
    ld.fx = lambda x: x + 0.18 * D.delay_cyclic(x, d, 0.35, 4)
    parts.append(ld)

    ep = Part(c, 0.25, None, "ep")
    place_arp(c, ep.tr, lambda f, d, v: D.epiano(f, d, v), 57, 79, range(4, 28), step=0.5,
              order="updown", gain=0.34, rng=c.rng("ep"))
    d2 = c.bpos(0.75)
    ep.fx = lambda x: x + 0.3 * D.delay_cyclic(x, d2, 0.4, 5)
    parts.append(ep)

    kit = Part(c, 0.14, None, "kit")
    place_pattern(c, kit.tr, lambda r, v: D.kick(r, v, 120, 48, 0.04, 0.3, 0.15, 1.2),
                  ["x.......x.....x.", "x.......x.......", "x.......x.....x.", "x.......x..x.x.."],
                  range(12, 28), gain=0.5)
    place_pattern(c, kit.tr, lambda r, v: D.hat(r, v, 0.035, 0.3, 7500),
                  "..x...x...x...x.", range(8, 28), gain=0.16, pan=0.3)
    place_pattern(c, kit.tr, lambda r, v: D.shaker(r, v, 0.04), "gggagggagggagggx",
                  range(4, 28), gain=0.12, pan=-0.35, swing=0.12)
    place_pattern(c, kit.tr, lambda r, v: D.clap(r, v), "....a.......a...", range(12, 28),
                  gain=0.2)
    place_pattern(c, kit.tr, lambda r, v: D.taiko(r, v, 64, 0.6), "X.....x.x.....x.",
                  range(20, 28), gain=0.35)
    place_pattern(c, kit.tr, lambda r, v: D.tom(r, v, 180, 110, 0.3), "..........x.xxxx",
                  (19, 27), gain=0.3, pan_jitter=0.5)
    parts.append(kit)

    br = Part(c, 0.35, None, "brass")
    place_brass(c, br.tr, 38, 2, gain=0.35, swell_beats=4.0, fc_hi=1400, drive=1.6,
                bars=range(20, 28, 2))
    rs = Part(c, 0.4, None, "riser")
    for end in (4, 20):
        rs.tr.add(c.bpos(end * 4 - 8), D.riser(c.rng(f"tr{end}"), c.sec(8), 1.0), 0.0, 0.1)
    rs.tr.add(c.bpos(20 * 4 - 8), D.cymbal_swell(c.rng("cym"), c.sec(8)), 0.0, 0.15)
    parts += [br, rs]

    x = build_stem(c, parts, ir, glue=(3.0, 2.0))
    return c, x


def sting(victory):
    if victory:
        c = Comp("sting_victory", 62, "ionian", 112, 3, [(1, "9")], seed=81, cyclic=False,
                 seconds=5.6)
    else:
        c = Comp("sting_defeat", 62, "aeolian", 72, 2, [(6, ""), (4, "")], seed=82, cyclic=False,
                 seconds=5.6)
    ir = D.make_ir(3.2, 808 if victory else 809, predelay=0.02, damp=0.5)
    parts = []
    if victory:
        horn = lambda f, d, v, p: D.lead(f, d, v, p, kind="horn", fc=2000, attack=0.02, vib=0.004,
                                         release=0.8)
        ld = Part(c, 0.35)
        fan = [(1, .5), (5, .5), (8, 3.5)]
        place_melody(c, ld.tr, fan, 0, horn, gain=0.6, fit=False)
        place_melody(c, ld.tr, fan, 0, horn, octave=-1, gain=0.35, fit=False)
        place_melody(c, ld.tr, [(3, .5), (7, .5), (10, 3.5)], 0, horn, gain=0.3, pan=0.3, fit=False)
        parts.append(ld)
        pad = Part(c, 0.4)
        hit = c.bpos(1.0)
        for m in (50, 57, 62, 64, 66, 69, 74):
            pad.tr.add(hit, D.pad_saw(D.midi_hz(m), 2.2, c.rng(f"v{m}"), 3, 12, 0.08, 1.4, 3200), 0.0, 0.2)
        bb = D.bass(D.midi_hz(38), 2.4, 1.0, 200, 400, 0.3, 1.0, 0.8, 1.0, 0.02, 1.0, "saw", 2.0, 0.8)
        pad.tr.add(hit, bb, 0.0, 0.6)
        parts.append(pad)
        dr = Part(c, 0.3)
        rng = c.rng("vdr")
        for b, v in ((0.0, 0.5), (0.5, 0.6), (1.0, 1.0)):
            dr.tr.add(c.bpos(b), D.taiko(rng, v, 62, 0.8), 0.0, 0.5)
        dr.tr.add(hit, D.boom(rng, 0.8, 80, 40, 1.2), 0.0, 0.4)
        dr.tr.add(0, D.cymbal_swell(rng, c.sec(1.0)), 0.0, 0.25)
        dr.tr.add(hit, D.hat(rng, 1.0, 0.9, 0.7, 5000), 0.0, 0.35)
        bl = D.fm_bell(D.midi_hz(86), 1.0, 0.8, 2.0, 1.2, 1.5)
        dr.tr.add(hit + c.bpos(0.5), bl, 0.3, 0.2)
        dr.tr.add(hit + c.bpos(1.0), D.fm_bell(D.midi_hz(81), 1.0, 0.7, 2.0, 1.2, 1.5), -0.3, 0.2)
        parts.append(dr)
    else:
        horn = lambda f, d, v, p: D.lead(f, d, v, p, kind="horn", fc=1100, attack=0.08, vib=0.005,
                                         release=1.2, glide=0.12)
        ld = Part(c, 0.4)
        fall = [(8, 1), (7, 1), (6, 1.5), (5, .5), (4, 1), (3, 1), (2, 1.5), (1, 3)]
        place_melody(c, ld.tr, [(d, b * 0.5) for d, b in fall], 0, horn, gain=0.5, fit=False)
        place_melody(c, ld.tr, [(d - 2, b * 0.5) for d, b in fall], 0, horn, gain=0.22, pan=0.3,
                     fit=False)
        parts.append(ld)
        pad = Part(c, 0.45)
        # bVI -> iv -> i in D minor, all diatonic to the key the defeat lives in
        for beat, ms, dur in ((0.0, (46, 53, 58, 62), 1.6), (2.0, (43, 50, 55, 58), 1.6),
                              (4.0, (38, 50, 53, 57, 62), 3.0)):
            for m in ms:
                pad.tr.add(c.bpos(beat), D.brass(D.midi_hz(m), dur, 0.8, c.rng(f"d{m}{beat}"),
                                                 200, 900, 0.4, 1.4, 1.8), 0.0, 0.22)
        parts.append(pad)
        dr = Part(c, 0.35)
        rng = c.rng("ddr")
        for b in (0.0, 2.0):
            dr.tr.add(c.bpos(b), D.taiko(rng, 0.7, 52, 1.0), 0.0, 0.5)
        dr.tr.add(c.bpos(4.0), D.boom(rng, 1.0, 55, 28, 1.8), 0.0, 0.6)
        parts.append(dr)
    x = build_stem(c, parts, ir, glue=(3.0, 2.0))
    # tail fade into silence (a one-shot: this is the only place we fade)
    n = x.shape[1]
    t = np.arange(n) / SR
    x *= np.clip((5.5 - t) / 1.2, 0.0, 1.0) ** 1.5
    return c, x


# =============================================================================================
# loudness / analysis

def _biquad_rbj(kind, fc, gain_db, q):
    A = 10 ** (gain_db / 40.0)
    w0 = 2 * np.pi * fc / SR
    c, s = np.cos(w0), np.sin(w0)
    al = s / (2 * q)
    if kind == "shelf":
        b = [A * ((A + 1) + (A - 1) * c + 2 * np.sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * c),
             A * ((A + 1) + (A - 1) * c - 2 * np.sqrt(A) * al)]
        a = [(A + 1) - (A - 1) * c + 2 * np.sqrt(A) * al, 2 * ((A - 1) - (A + 1) * c),
             (A + 1) - (A - 1) * c - 2 * np.sqrt(A) * al]
    else:
        b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
        a = [1 + al, -2 * c, 1 - al]
    return np.array(b) / a[0], np.array(a) / a[0]


_KW = (_biquad_rbj("shelf", 1500.0, 4.0, 1 / np.sqrt(2)), _biquad_rbj("hp", 38.0, 0.0, 0.5))


def k_weight(x):
    for b, a in _KW:
        x = signal.lfilter(b, a, x, axis=-1)
    return x


def lufs(x, loop=True):
    """BS.1770 integrated loudness of a (2, n) signal."""
    y = D.cyclic(k_weight, x, 1.0) if loop else k_weight(x)
    blk, hop = int(0.4 * SR), int(0.1 * SR)
    if y.shape[1] < blk:
        return float(-0.691 + 10 * np.log10(np.sum(np.mean(y ** 2, axis=1)) + 1e-20))
    sq = np.cumsum(np.concatenate([np.zeros((2, 1)), y ** 2], axis=1), axis=1)
    starts = np.arange(0, y.shape[1] - blk + 1, hop)
    z = (sq[:, starts + blk] - sq[:, starts]) / blk
    lk = -0.691 + 10 * np.log10(np.sum(z, axis=0) + 1e-20)
    g = lk > -70.0
    if not np.any(g):
        return -70.0
    rel = -0.691 + 10 * np.log10(np.mean(np.sum(z[:, g], axis=0))) - 10.0
    g2 = g & (lk > rel)
    return float(-0.691 + 10 * np.log10(np.mean(np.sum(z[:, g2], axis=0))))


def peak_db(x):
    return float(D.db(np.max(np.abs(x))))


def seam_stats(x):
    """Wrap-point continuity against the wrap's own neighbourhood (robust to arrangement).

    jumpR  - sample step across the wrap / largest sample step within +-20 ms.
    clickdB- >8 kHz energy in a 1.5 ms window on the wrap, dB over the median of such windows
             within +-100 ms (clickFS is the same energy in dBFS).
    fluxR  - 1024-sample log-spectral difference across the wrap / largest such difference at
             any other frame boundary within +-0.5 s (hop 256). >1.5 = timbre jump.
    The signal is treated circularly, i.e. exactly as a looping AudioSource plays it.

    Lossy files: Vorbis quantisation noise loses its overlap-add continuity at the file edges,
    so a decoded loop carries a step at about the codec noise floor (~ -50 dBFS here) even when
    the source is exactly periodic. seam_bad() therefore needs a step to be large in absolute
    terms too (> 0.01, or HF click > -60 dBFS) before calling it a click.
    """
    mono = x.mean(axis=0)
    L = mono.shape[0]

    def circ(a, b):
        return mono[np.arange(a, b) % L]

    w = int(0.02 * SR)
    seg = circ(-w, w + 1)                  # index w is sample 0, w-1 is sample L-1
    d = np.abs(np.diff(seg))
    jump = d[w - 1]
    others = np.delete(d, w - 1)
    jump_r = float(jump / max(1e-12, others.max()))
    w = int(0.1 * SR)
    seg = circ(-w - 64, w + 64)
    hp = D.butter(np.concatenate([circ(-w - 64 - SR // 2, -w - 64), seg]), 8000.0, "high", 4)[SR // 2:]
    e = uniform_filter1d(hp ** 2, 65, mode="nearest")
    c0 = w + 64
    far = np.concatenate([e[64:c0 - 90], e[c0 + 90:-64]])
    click_db = float(10 * np.log10((e[c0] + 1e-20) / (np.median(far) + 1e-20)))
    click_fs = float(10 * np.log10(e[c0] + 1e-20))
    N = 1024
    win = np.hanning(N)

    def spec(start):
        return np.log(np.abs(np.fft.rfft(circ(start, start + N) * win)) + 1e-4)

    def flux(p):
        return float(np.linalg.norm(spec(p) - spec(p - N)))

    ref = [flux(p) for p in range(-SR // 2, SR // 2, 256) if abs(p) > 256]
    flux_r = float(flux(0) / max(1e-9, max(ref)))
    return float(jump), jump_r, click_db, flux_r, click_fs


def seam_bad(st):
    j, jr, cd, fr, cfs = st
    return (jr > 1.5 and j > 0.01) or (cd > 10.0 and cfs > -60.0) or fr > 1.5


def choose_rotation(stems, c):
    """Quietest point between 16th-note grid positions, judged across all stems at once."""
    L = stems[0].shape[1]
    step = c.spb / 4.0
    cand = np.round(np.arange(int(L / step)) * step + step / 2).astype(int) % L
    score = np.zeros(L)
    for x in stems:
        mono = x.mean(axis=0)
        hf = D.cyclic(lambda z: D.butter(z, 4000.0, "high", 2), mono, 0.5)
        e = uniform_filter1d(mono ** 2, int(0.12 * SR), mode="wrap") \
            + 4.0 * uniform_filter1d(hf ** 2, int(0.12 * SR), mode="wrap")
        score += e / (np.mean(e) + 1e-20)
    return int(cand[np.argmin(score[cand])])


def centroid(x):
    mono = x.mean(axis=0)
    f, p = signal.welch(mono, SR, nperseg=8192)
    cen = float(np.sum(f * p) / np.sum(p))
    tot = np.sum(p)
    bands = [np.sum(p[(f >= lo) & (f < hi)]) / tot * 100 for lo, hi in
             ((0, 150), (150, 1000), (1000, 4000), (4000, 22050))]
    return cen, bands


def in_key_pct(x, tonic, mode):
    """Share of tonal (spectral-peak) energy, 90 Hz - 1.4 kHz, on the world's scale pitch classes.

    Fundamentals dominate that range, so a stem written in key reads ~85-100%; upper harmonics
    (5th/7th partials) and unpitched percussion account for the rest. Noise alone reads ~58%.
    """
    mono = x.mean(axis=0)
    f, _, Z = signal.stft(mono, SR, nperseg=8192, noverlap=4096)
    mag = np.abs(Z)
    band = (f > 90) & (f < 1400)
    mag, f = mag[band], f[band]
    peak = (mag > np.roll(mag, 1, axis=0)) & (mag > np.roll(mag, -1, axis=0))
    peak &= mag > mag.max(axis=0, keepdims=True) * 0.03
    pcs = np.mod(np.round(12 * np.log2(f / 440.0) + 69), 12).astype(int)
    scale = {(tonic + k) % 12 for k in MODES[mode]}
    w = (mag ** 2) * peak
    tot = w.sum()
    inside = w[np.isin(pcs, list(scale))].sum()
    return float(inside / max(tot, 1e-20) * 100.0)


def worst_subset_peak(stems, levels):
    """Max |sum| over every non-empty subset of stems at the given per-stem levels."""
    P = np.zeros(stems[0].shape[1])
    for r in range(1, len(stems) + 1):
        for sub in itertools.combinations(range(len(stems)), r):
            s = sum(stems[i] * levels[i] for i in sub)
            P = np.maximum(P, np.max(np.abs(s), axis=0))
    return P


def mood_mix(stems, mood):
    lv = MOOD_LEVELS[mood]
    return sum(s * l for s, l in zip(stems, lv))


# =============================================================================================
# I/O

def write_ogg(path, x):
    """Block-wise Vorbis write: one big sf.write of long real audio segfaults this libsndfile."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.ascontiguousarray(x.T.astype(np.float32))
    tmp = path + ".tmp"
    with sf.SoundFile(tmp, "w", SR, data.shape[1], format="OGG", subtype="VORBIS",
                      compression_level=VORBIS_LEVEL) as f:
        for i in range(0, data.shape[0], 4096):
            f.write(data[i:i + 4096])
    os.replace(tmp, path)


def read(path):
    y, sr = sf.read(path, dtype="float64", always_2d=True)
    assert sr == SR, (path, sr)
    return y.T


def stem_path(world, stem):
    return os.path.join(OUT_DIR, f"{world}_{stem}.ogg")


# =============================================================================================
# render pipeline

def render_world(name):
    t0 = time.time()
    c, ir, parts = WORLD_FNS[name]()
    stems = []
    for s in STEMS:
        x = build_stem(c, parts[s], ir)
        # per-stem balance (relative), then the whole world is scaled together below
        x *= D.undb(STEM_BALANCE[s] - lufs(x))
        stems.append(x)
    # world gain for the Work target
    g = D.undb(WORK_LUFS - lufs(mood_mix(stems, "work")))
    stems = [s * g for s in stems]
    thr = float(D.undb(INTERNAL_CEIL_DB))
    P = worst_subset_peak(stems, MAX_LEVELS)
    # If the limiter would have to work hard, give up some loudness instead of squashing.
    excess = D.db(np.percentile(P, 99.97)) - INTERNAL_CEIL_DB - 3.0
    if excess > 0:
        stems = [s * D.undb(-excess) for s in stems]
        P = P * D.undb(-excess)
    G = D.limiter_gain(P, thr, half=220, mode="wrap")
    gr = -D.db(G)
    stems = [s * G for s in stems]
    rot = choose_rotation(stems, c)
    stems = [np.roll(s, -rot, axis=1) for s in stems]
    info = {"limit_max_gr_db": float(gr.max()), "limit_pct_over_1db": float(np.mean(gr > 1.0) * 100),
            "trim_db": float(max(0.0, excess)), "bpm": c.bpm, "bars": c.bars, "L": c.L,
            "downbeat_s": ((c.L - rot) % c.L) / SR,
            "key": f"{NOTE_NAMES[c.tonic % 12]} {c.mode}"}
    # Encode; if the codec overshoots the hard ceiling, trim and re-encode.
    for attempt in range(4):
        for s, x in zip(STEMS, stems):
            write_ogg(stem_path(name, s), x)
        dec = [read(stem_path(name, s)) for s in STEMS]
        worst = peak_db(worst_subset_peak(dec, MAX_LEVELS))
        if worst <= CEIL_DB - 0.05:
            break
        trim = D.undb(CEIL_DB - 0.15 - worst)
        stems = [s * trim for s in stems]
    info["edge_err_db"] = max(codec_edge_error(a, b) for a, b in zip(stems, dec))
    src_seams = [seam_stats(s) for s in stems]
    info["src_jumpR"] = max(st[1] for st in src_seams)
    info["src_clickdB"] = max(st[2] for st in src_seams)
    info["src_ok"] = not any(seam_bad(st) or st[1] > 1.5 or st[2] > 10.0 for st in src_seams)
    info["seconds"] = time.time() - t0
    return info


def codec_edge_error(src, dec):
    """Codec error-to-signal ratio in the first/last 50 ms minus the same ratio mid-file (dB).

    The source is exactly periodic by construction; this shows whether the Vorbis encoder added
    anything special at the file edges (which is where the loop wraps).
    """
    n = int(0.05 * SR)
    err = dec - src
    mid = err[:, err.shape[1] // 2 - 5 * n: err.shape[1] // 2 + 5 * n]
    edge = np.concatenate([err[:, :n], err[:, -n:]], axis=1)
    smid = src[:, src.shape[1] // 2 - 5 * n: src.shape[1] // 2 + 5 * n]
    sedge = np.concatenate([src[:, :n], src[:, -n:]], axis=1)
    return float((D.db(rms(edge)) - D.db(rms(sedge))) - (D.db(rms(mid)) - D.db(rms(smid))))


NOTE_NAMES = ("C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B")


def render_title():
    c, x = title_theme()
    x *= D.undb(TITLE_LUFS - lufs(x))
    thr = float(D.undb(INTERNAL_CEIL_DB))
    G = D.limiter_gain(np.max(np.abs(x), axis=0), thr, 220, "wrap")
    x = x * G
    x = np.roll(x, -choose_rotation([x], c), axis=1)
    st = seam_stats(x)
    src_ok = not (seam_bad(st) or st[1] > 1.5 or st[2] > 10.0)
    path = os.path.join(OUT_DIR, "title_theme.ogg")
    for _ in range(4):
        write_ogg(path, x)
        p = peak_db(read(path))
        if p <= CEIL_DB - 0.05:
            break
        x *= D.undb(CEIL_DB - 0.15 - p)
    return {"limit_max_gr_db": float((-D.db(G)).max()), "src_ok": src_ok,
            "src_jumpR": st[1], "src_clickdB": st[2]}


def render_stings():
    for victory in (True, False):
        c, x = sting(victory)
        thr = float(D.undb(INTERNAL_CEIL_DB))
        x *= thr / max(1e-9, np.max(np.abs(x))) * D.undb(2.0)   # drive 2 dB into the limiter
        G = D.limiter_gain(np.max(np.abs(x), axis=0), thr, 220, "nearest")
        x = x * G
        path = os.path.join(OUT_DIR, f"{c.name}.ogg")
        for _ in range(4):
            write_ogg(path, x)
            p = peak_db(read(path))
            if p <= CEIL_DB - 0.05:
                break
            x *= D.undb(CEIL_DB - 0.15 - p)


def write_previews(name, stems):
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    L = stems[0].shape[1]
    idx = (np.arange(int(40 * SR)) + L - int(15 * SR)) % L   # straddles the loop seam at 0:15
    for mood in MOOD_LEVELS:
        mix = mood_mix(stems, mood)[:, idx]
        write_ogg(os.path.join(PREVIEW_DIR, f"{name}_{mood}.ogg"), mix)


# =============================================================================================
# check

def check(worlds, previews=True):
    ok = True
    print("\n== per-file ==")
    print(f"{'file':28s} {'len(s)':>7s} {'peak':>7s} {'rms':>7s} {'LUFS':>7s} {'seamJump':>9s} "
          f"{'jumpR':>6s} {'clkdB':>6s} {'clkFS':>6s} {'fluxR':>6s} {'KB':>6s}")
    centroids = {}
    for w in worlds:
        paths = [stem_path(w, s) for s in STEMS]
        if not all(os.path.exists(p) for p in paths):
            print(f"{w}: missing stems")
            ok = False
            continue
        stems = [read(p) for p in paths]
        lens = {s.shape[1] for s in stems}
        for s, p, x in zip(STEMS, paths, stems):
            st = seam_stats(x)
            j, jp, cp, fr, cfs = st
            print(f"{os.path.basename(p):28s} {x.shape[1] / SR:7.2f} {peak_db(x):7.2f} "
                  f"{D.db(rms(x)):7.2f} {lufs(x):7.2f} {j:9.5f} {jp:6.2f} {cp:6.1f} {cfs:6.1f} {fr:6.2f} "
                  f"{os.path.getsize(p) / 1024:6.0f}")
            if seam_bad(st):
                print("   !! seam looks discontinuous")
                ok = False
        keys = "  ".join(f"{s} {in_key_pct(x, META[w]['tonic'], META[w]['mode']):.0f}%"
                         for s, x in zip(STEMS, stems))
        print(f"   {w}: tonal energy on scale tones: {keys}")
        if len(lens) != 1:
            print(f"   !! {w}: stem lengths differ: {sorted(lens)}")
            ok = False
        else:
            print(f"   {w}: all 4 stems exactly {lens.pop()} samples")
        worst = peak_db(worst_subset_peak(stems, MAX_LEVELS))
        line = f"   {w}: worst peak over all 15 layer subsets at max C# levels = {worst:.2f} dBFS"
        print(line + ("" if worst <= CEIL_DB else "  !! OVER CEILING"))
        ok &= worst <= CEIL_DB
        for mood in MOOD_LEVELS:
            m = mood_mix(stems, mood)
            print(f"   {w} {mood:8s} mix: peak {peak_db(m):6.2f} dBFS, {lufs(m):6.2f} LUFS")
        centroids[w] = centroid(mood_mix(stems, "work"))
        if previews:
            write_previews(w, stems)
    for f in ("title_theme", "sting_victory", "sting_defeat"):
        p = os.path.join(OUT_DIR, f + ".ogg")
        if not os.path.exists(p):
            continue
        x = read(p)
        loop = f == "title_theme"
        st = seam_stats(x) if loop else (0, 0, 0, 0, -200)
        j, jp, cp, fr, cfs = st
        print(f"{f + '.ogg':28s} {x.shape[1] / SR:7.2f} {peak_db(x):7.2f} {D.db(rms(x)):7.2f} "
              f"{lufs(x, loop):7.2f} {j:9.5f} {jp:6.2f} {cp:6.1f} {cfs:6.1f} {fr:6.2f} "
              f"{os.path.getsize(p) / 1024:6.0f}")
        if loop and seam_bad(st):
            print("   !! seam looks discontinuous")
            ok = False
        ok &= peak_db(x) <= CEIL_DB
    print("\n== colour (Work mix) ==")
    print(f"{'world':8s} {'centroid Hz':>11s} {'<150':>6s} {'150-1k':>7s} {'1k-4k':>6s} {'>4k':>6s}")
    for w, (cen, b) in centroids.items():
        print(f"{w:8s} {cen:11.0f} {b[0]:6.1f} {b[1]:7.1f} {b[2]:6.1f} {b[3]:6.1f}")
    total = sum(os.path.getsize(os.path.join(OUT_DIR, f)) for f in os.listdir(OUT_DIR)
                if f.endswith(".ogg")) if os.path.isdir(OUT_DIR) else 0
    print(f"\nMusic folder total: {total / 1e6:.2f} MB (budget 30 MB)")
    ok &= total <= 30e6
    print("CHECK", "PASS" if ok else "FAIL")
    return ok


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--world", choices=WORLDS + ("title", "stings"), action="append",
                    help="render only these (repeatable); default everything")
    ap.add_argument("--check", action="store_true", help="only analyse existing files")
    ap.add_argument("--no-preview", action="store_true")
    args = ap.parse_args()
    targets = args.world or list(WORLDS) + ["title", "stings"]
    worlds = [w for w in targets if w in WORLDS]
    render_ok = True
    if not args.check:
        t0 = time.time()
        for w in worlds:
            info = render_world(w)
            render_ok &= info["src_ok"]
            print(f"rendered {w:7s} {info['key']:12s} {info['bpm']:5.0f} bpm {info['bars']} bars "
                  f"= {info['L'] / SR:6.2f}s  limiter max GR {info['limit_max_gr_db']:.2f} dB "
                  f"({info['limit_pct_over_1db']:.3f}% > 1 dB), trim {info['trim_db']:.2f} dB, "
                  f"codec edge err {info['edge_err_db']:+.1f} dB vs mid, bar 1 at {info['downbeat_s']:.2f}s, "
                  f"source seam worst jumpR {info['src_jumpR']:.2f} clickdB {info['src_clickdB']:+.1f} "
                  f"{'OK' if info['src_ok'] else '!! SOURCE SEAM'} "
                  f"[{info['seconds']:.1f}s]")
        if "title" in targets:
            info = render_title()
            render_ok &= info["src_ok"]
            print(f"rendered title   limiter max GR {info['limit_max_gr_db']:.2f} dB, source seam "
                  f"jumpR {info['src_jumpR']:.2f} clickdB {info['src_clickdB']:+.1f} "
                  f"{'OK' if info['src_ok'] else '!! SOURCE SEAM'}")
        if "stings" in targets:
            render_stings()
            print("rendered stings")
        print(f"render time {time.time() - t0:.1f}s")
    ok = check(worlds if args.world else list(WORLDS), not args.no_preview)
    if not render_ok:
        print("!! a pre-encode source seam failed")
    sys.exit(0 if (ok and render_ok) else 1)


if __name__ == "__main__":
    main()
