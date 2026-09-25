"""Shared DSP building blocks for render_sfx.py.

Everything here is plain numpy/scipy so the renderer is deterministic and needs no plug-ins.
Signals are float64 mono arrays (or (2, n) stereo for beds) at SR, nominally in [-1, 1].
"""

from __future__ import annotations

import zlib

import numpy as np
from scipy import signal
from scipy.ndimage import maximum_filter1d, minimum_filter1d, uniform_filter1d

SR = 44100
NYQ = SR / 2


# --------------------------------------------------------------------------------------------
# Basics
# --------------------------------------------------------------------------------------------

def ns(seconds: float) -> int:
    return max(1, int(round(seconds * SR)))


def tt(n: int) -> np.ndarray:
    return np.arange(n) / SR


def seed_for(*parts) -> int:
    """Stable seed (Python's hash() is salted per process, crc32 is not)."""
    return zlib.crc32("|".join(str(p) for p in parts).encode("utf-8"))


def rng_for(*parts) -> np.random.Generator:
    return np.random.default_rng(seed_for(*parts))


def as_arr(v, n: int) -> np.ndarray:
    if np.isscalar(v):
        return np.full(n, float(v))
    a = np.asarray(v, dtype=float)
    if len(a) != n:
        a = np.interp(np.linspace(0, len(a) - 1, n), np.arange(len(a)), a)
    return a


def semis(x: float) -> float:
    return 2.0 ** (x / 12.0)


def db(x: float) -> float:
    return 10.0 ** (x / 20.0)


# --------------------------------------------------------------------------------------------
# Envelopes
# --------------------------------------------------------------------------------------------

def env_exp(n: int, decay: float, attack: float = 0.0008) -> np.ndarray:
    """Linear attack into an exponential decay (decay = time constant in seconds)."""
    t = tt(n)
    e = np.exp(-np.maximum(t - attack, 0.0) / max(decay, 1e-5))
    if attack > 0:
        e *= np.clip(t / attack, 0.0, 1.0)
    return e


def env_pts(n: int, pts) -> np.ndarray:
    """Piecewise-linear envelope from (time, value) pairs."""
    ts, vs = zip(*pts)
    return np.interp(tt(n), ts, vs)


def smoothstep(x: np.ndarray) -> np.ndarray:
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def glide(n: int, f0: float, f1: float, curve: str = "exp", shape: float = 1.0) -> np.ndarray:
    """Frequency trajectory from f0 to f1 across n samples."""
    x = np.linspace(0.0, 1.0, n) ** shape
    if curve == "exp":
        return f0 * (f1 / f0) ** x
    if curve == "smooth":
        return f0 + (f1 - f0) * smoothstep(x)
    return f0 + (f1 - f0) * x


def fades(x: np.ndarray, fin: float = 0.0015, fout: float = 0.006) -> np.ndarray:
    """Raised-cosine fades so clips start and end on zero (no clicks)."""
    x = np.array(x, dtype=float, copy=True)
    n = x.shape[-1]
    a = min(ns(fin), n // 2)
    b = min(ns(fout), n // 2)
    if a > 1:
        x[..., :a] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, a))
    if b > 1:
        x[..., n - b:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, b))
    x[..., 0] = 0.0
    x[..., -1] = 0.0
    return x


# --------------------------------------------------------------------------------------------
# Oscillators
# --------------------------------------------------------------------------------------------

def phase(f, n: int, ph0: float = 0.0) -> np.ndarray:
    return ph0 + 2 * np.pi * np.cumsum(as_arr(f, n)) / SR


def sine(f, n: int, ph0: float = 0.0) -> np.ndarray:
    return np.sin(phase(f, n, ph0))


def additive(f, n: int, amp_fn, max_h: int = 32, ph0: float = 0.0) -> np.ndarray:
    """Band-limited additive oscillator; harmonics above ~0.45*SR are muted per sample."""
    f = as_arr(f, n)
    ph = phase(f, n, ph0)
    out = np.zeros(n)
    for k in range(1, max_h + 1):
        a = amp_fn(k)
        if a == 0:
            continue
        mask = (k * f) < 0.45 * SR
        if not mask.any():
            break
        out += a * np.sin(k * ph) * mask
    return out


def saw(f, n: int, max_h: int = 32, ph0: float = 0.0) -> np.ndarray:
    return additive(f, n, lambda k: 1.0 / k, max_h, ph0) * 0.6


def square(f, n: int, max_h: int = 31, ph0: float = 0.0) -> np.ndarray:
    return additive(f, n, lambda k: (1.0 / k) if k % 2 else 0.0, max_h, ph0) * 0.8


def triangle(f, n: int, max_h: int = 15, ph0: float = 0.0) -> np.ndarray:
    return additive(f, n, lambda k: ((-1) ** ((k - 1) // 2)) / (k * k) if k % 2 else 0.0,
                    max_h, ph0) * 0.8


def fm(fc, n: int, ratio: float, index, ph0: float = 0.0) -> np.ndarray:
    """Two-operator FM: carrier fc, modulator at ratio*fc, index may be an envelope."""
    pc = phase(fc, n, ph0)
    return np.sin(pc + as_arr(index, n) * np.sin(ratio * pc))


# --------------------------------------------------------------------------------------------
# Noise
# --------------------------------------------------------------------------------------------

def white(n: int, r: np.random.Generator) -> np.ndarray:
    return r.standard_normal(n)


def _shaped(n: int, r: np.random.Generator, power: float) -> np.ndarray:
    x = np.fft.rfft(r.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    f[0] = f[1] if n > 1 else 1.0
    x /= f ** (power / 2.0)
    x[0] = 0
    y = np.fft.irfft(x, n)
    return y / (np.std(y) + 1e-12)


def pink(n: int, r: np.random.Generator) -> np.ndarray:
    return _shaped(n, r, 1.0)


def brown(n: int, r: np.random.Generator) -> np.ndarray:
    return _shaped(n, r, 2.0)


def lfo_noise(n: int, r: np.random.Generator, rate_hz: float, lo: float = -1.0, hi: float = 1.0) -> np.ndarray:
    """Smooth random control signal (random points at rate_hz, cosine-interpolated)."""
    pts = max(4, int(n / SR * rate_hz) + 3)
    vals = r.uniform(lo, hi, pts)
    x = np.linspace(0, pts - 3, n)
    i = np.floor(x).astype(int)
    frac = x - i
    w = 0.5 - 0.5 * np.cos(np.pi * frac)
    return vals[i] * (1 - w) + vals[i + 1] * w


def crackle(n: int, r: np.random.Generator, density, decay: float = 0.0004) -> np.ndarray:
    """Sparse random impulses (density may be an array, impulses/second) with a tiny decay."""
    d = as_arr(density, n)
    hits = (r.random(n) < d / SR) * r.standard_normal(n)
    k = np.exp(-np.arange(ns(decay * 6)) / (decay * SR))
    return signal.fftconvolve(hits, k)[:n]


# --------------------------------------------------------------------------------------------
# Filters
# --------------------------------------------------------------------------------------------

def _clampf(f: float) -> float:
    return float(np.clip(f, 10.0, 0.47 * SR))


def lp(x, f, order: int = 2):
    return signal.sosfilt(signal.butter(order, _clampf(f), "low", fs=SR, output="sos"), x)


def hp(x, f, order: int = 2):
    return signal.sosfilt(signal.butter(order, _clampf(f), "high", fs=SR, output="sos"), x)


def bp(x, lo, hi, order: int = 2):
    lo, hi = _clampf(lo), _clampf(hi)
    if hi <= lo * 1.02:
        hi = lo * 1.05
    return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def rbj(kind: str, f: float, q: float, gain_db: float = 0.0):
    """RBJ cookbook biquad coefficients."""
    w = 2 * np.pi * _clampf(f) / SR
    cw, sw = np.cos(w), np.sin(w)
    alpha = sw / (2 * max(q, 1e-3))
    A = 10 ** (gain_db / 40.0)
    if kind == "lp":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "hp":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "bp":
        b = [alpha, 0.0, -alpha]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "peak":
        b = [1 + alpha * A, -2 * cw, 1 - alpha * A]
        a = [1 + alpha / A, -2 * cw, 1 - alpha / A]
    elif kind == "hs":
        sq = 2 * np.sqrt(A) * alpha
        b = [A * ((A + 1) + (A - 1) * cw + sq), -2 * A * ((A - 1) + (A + 1) * cw),
             A * ((A + 1) + (A - 1) * cw - sq)]
        a = [(A + 1) - (A - 1) * cw + sq, 2 * ((A - 1) - (A + 1) * cw), (A + 1) - (A - 1) * cw - sq]
    else:
        raise ValueError(kind)
    b = np.array(b) / a[0]
    a = np.array(a) / a[0]
    return b, a


def biquad(x, kind: str, f, q: float = 0.707, gain_db: float = 0.0):
    if not np.isscalar(f):  # a frequency trajectory: hand over to the time-varying version
        return sweep(x, kind, f, q, block=256)
    b, a = rbj(kind, f, q, gain_db)
    return signal.lfilter(b, a, x)


def sweep(x, kind: str, f, q: float = 0.707, block: int = 128) -> np.ndarray:
    """Time-varying biquad: coefficients updated per block, filter state carried across."""
    n = len(x)
    f = as_arr(f, n)
    out = np.empty(n)
    zi = np.zeros(2)
    for s in range(0, n, block):
        e = min(n, s + block)
        b, a = rbj(kind, f[(s + e) // 2], q)
        out[s:e], zi = signal.lfilter(b, a, x[s:e], zi=zi)
    return out


def resonator(x, f: float, decay: float) -> np.ndarray:
    """Two-pole resonator whose impulse response is a unit-amplitude decaying sine."""
    w = 2 * np.pi * _clampf(f) / SR
    rr = np.exp(-1.0 / (max(decay, 1e-4) * SR))
    return signal.lfilter([np.sin(w)], [1.0, -2 * rr * np.cos(w), rr * rr], x)


def modal(excite, modes) -> np.ndarray:
    """Bank of resonators, modes = [(freq, decay_s, gain), ...]."""
    out = np.zeros(len(excite))
    for f, d, g in modes:
        if f < 0.45 * SR:
            out += g * resonator(excite, f, d)
    return out


# --------------------------------------------------------------------------------------------
# Effects
# --------------------------------------------------------------------------------------------

def sat(x, drive: float = 2.0) -> np.ndarray:
    return np.tanh(drive * x) / np.tanh(drive)


def varispeed(x, ratio: float) -> np.ndarray:
    """Tape-style pitch/time change (ratio > 1 = higher and shorter)."""
    if abs(ratio - 1.0) < 1e-4:
        return np.array(x, copy=True)
    n = max(8, int(round(x.shape[-1] / ratio)))
    return signal.resample(x, n, axis=-1)


def reverb_ir(r: np.random.Generator, rt60: float, bright: float = 7000.0, dark: float = 1800.0,
              predelay: float = 0.008, early: int = 6, stereo: bool = False, length: float = None) -> np.ndarray:
    """Synthetic room: sparse early taps plus a noise tail that darkens as it decays."""
    length = length if length is not None else rt60 * 1.1
    n = ns(length)
    t = tt(n)
    env = 10 ** (-3.0 * t / rt60)
    chans = []
    for _ in range(2 if stereo else 1):
        w = r.standard_normal(n)
        tail = lp(w, bright, 1) * env ** 2.2 + lp(w, dark, 2) * env
        tail *= np.clip(t / 0.012, 0, 1)
        for _k in range(early):
            i = int(r.uniform(0.003, 0.04) * SR)
            if i < n:
                tail[i] += r.uniform(-1, 1) * 6.0
        tail = np.concatenate([np.zeros(ns(predelay)), tail])
        chans.append(tail / np.sqrt(np.sum(tail ** 2)))
    return np.array(chans) if stereo else chans[0]


def reverb(x, r: np.random.Generator, rt60: float = 0.8, mix: float = 0.25, **kw) -> np.ndarray:
    """Mono in, mono out (tail appended). Wet energy is roughly matched to dry before mix."""
    ir = reverb_ir(r, rt60, **kw)
    wet = signal.fftconvolve(x, ir)
    out = np.zeros(len(wet))
    out[: len(x)] += x
    return out + wet * mix


def delay(x, seconds: float, feedback: float = 0.35, mix: float = 0.3, taps: int = 4, lp_f: float = 5000) -> np.ndarray:
    d = ns(seconds)
    out = np.zeros(len(x) + d * taps)
    out[: len(x)] += x
    echo = np.array(x, dtype=float)
    for k in range(1, taps + 1):
        echo = lp(echo, lp_f, 1) * feedback
        out[k * d: k * d + len(x)] += echo * (mix / feedback)
    return out


def pan(x, p: float) -> np.ndarray:
    """Equal-power pan, p in [-1, 1]; returns (2, n)."""
    a = (np.clip(p, -1, 1) + 1) * np.pi / 4
    return np.vstack([x * np.cos(a), x * np.sin(a)])


def pan_arr(x, p) -> np.ndarray:
    a = (np.clip(as_arr(p, len(x)), -1, 1) + 1) * np.pi / 4
    return np.vstack([x * np.cos(a), x * np.sin(a)])


# --------------------------------------------------------------------------------------------
# Composition
# --------------------------------------------------------------------------------------------

class Canvas:
    """Growable mix buffer. add() places a layer at a time offset (seconds)."""

    def __init__(self, seconds: float = 0.5, channels: int = 1):
        self.ch = channels
        self.buf = np.zeros((channels, ns(seconds)))

    def add(self, x, at: float = 0.0, gain: float = 1.0):
        x = np.atleast_2d(np.asarray(x, dtype=float))
        if x.shape[0] != self.ch:
            x = np.repeat(x[:1], self.ch, axis=0) if self.ch > 1 else x.mean(axis=0, keepdims=True)
        i = max(0, int(round(at * SR)))
        need = i + x.shape[1]
        if need > self.buf.shape[1]:
            self.buf = np.pad(self.buf, ((0, 0), (0, need - self.buf.shape[1])))
        self.buf[:, i:need] += x * gain
        return self

    def out(self) -> np.ndarray:
        return self.buf[0].copy() if self.ch == 1 else self.buf.copy()


# --------------------------------------------------------------------------------------------
# Loudness (ITU-R BS.1770 K-weighting) and mastering
# --------------------------------------------------------------------------------------------

def _kweight_coeffs():
    # Pre-filter (high shelf) and RLB high-pass, re-derived for SR as pyloudnorm does.
    G, Q, fc = 3.99984385397, 0.7071752369554193, 1681.9744509555319
    K = np.tan(np.pi * fc / SR)
    Vh = 10 ** (G / 20)
    Vb = Vh ** 0.499666774155
    a0 = 1 + K / Q + K * K
    b1 = [(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0]
    a1 = [1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]
    Q2, fc2 = 0.5003270373253953, 38.13547087613982
    K = np.tan(np.pi * fc2 / SR)
    a0 = 1 + K / Q2 + K * K
    b2 = [1, -2, 1]
    a2 = [1, 2 * (K * K - 1) / a0, (1 - K / Q2 + K * K) / a0]
    return (b1, a1), (b2, a2)


_KW = _kweight_coeffs()


def kweight(x):
    (b1, a1), (b2, a2) = _KW
    return signal.lfilter(b2, a2, signal.lfilter(b1, a1, x, axis=-1), axis=-1)


def _block_power(x, win: float, hop: float):
    x = np.atleast_2d(x)
    y = kweight(x) ** 2
    w, h = ns(win), ns(hop)
    y = np.pad(y, ((0, 0), (0, w)))  # short clips are measured against silence, like the ear does
    c = np.concatenate([np.zeros((y.shape[0], 1)), np.cumsum(y, axis=1)], axis=1)
    starts = np.arange(0, max(1, y.shape[1] - w), h)
    p = (c[:, starts + w] - c[:, starts]) / w
    return p.sum(axis=0)


def lufs_momentary_max(x) -> float:
    p = _block_power(x, 0.4, 0.01)
    return float(-0.691 + 10 * np.log10(p.max() + 1e-20))


def lufs_integrated(x) -> float:
    x = np.atleast_2d(x)
    w, h = ns(0.4), ns(0.1)
    y = kweight(x) ** 2
    c = np.concatenate([np.zeros((y.shape[0], 1)), np.cumsum(y, axis=1)], axis=1)
    starts = np.arange(0, max(1, y.shape[1] - w + 1), h)
    p = ((c[:, starts + w] - c[:, starts]) / w).sum(axis=0)
    l = -0.691 + 10 * np.log10(p + 1e-20)
    p = p[l > -70]
    if len(p) == 0:
        return -99.0
    rel = -0.691 + 10 * np.log10(p.mean()) - 10
    p2 = p[(-0.691 + 10 * np.log10(p)) > rel]
    return float(-0.691 + 10 * np.log10(p2.mean()))


def true_peak(x) -> float:
    x = np.atleast_2d(x)
    up = signal.resample_poly(x, 4, 1, axis=-1)
    return float(np.max(np.abs(up)))


def limit(x, ceiling_db: float = -1.6, look: float = 0.0015):
    """Look-ahead brickwall limiter: the gain never exceeds what the peak around it needs."""
    ceil = db(ceiling_db)
    x = np.atleast_2d(np.asarray(x, dtype=float))
    peak = np.max(np.abs(x), axis=0)
    L = max(3, ns(look))
    need = np.minimum(1.0, ceil / np.maximum(maximum_filter1d(peak, L), 1e-12))
    g = minimum_filter1d(need, 2 * L + 1)
    g = uniform_filter1d(g, L)
    g = np.minimum(g, need)
    y = np.clip(x * g, -ceil, ceil)
    return y[0] if y.shape[0] == 1 else y


def trim_tail(x, floor_db: float = -62.0, keep: float = 0.02):
    """Drop trailing near-silence so reverb tails do not waste bytes."""
    a = np.max(np.abs(np.atleast_2d(x)), axis=0)
    idx = np.nonzero(a > db(floor_db))[0]
    if len(idx) == 0:
        return x
    end = min(a.shape[0], idx[-1] + ns(keep))
    return x[..., :end]


def master_oneshot(x, target_lufs: float, ceiling_db: float = -1.6, preroll: float = 0.003):
    """Trim, fade, then normalise to a momentary-max loudness target and limit.

    A few ms of pre-roll silence lets the fade-in land on silence instead of shaving the
    transient (and gives a Vorbis encoder somewhere to put its pre-echo).
    """
    x = np.atleast_2d(np.asarray(x, dtype=float))
    x = np.pad(x, ((0, 0), (ns(preroll), 0)))
    x = hp(x, 22, 2)          # remove DC / sub rumble the speakers cannot reproduce
    x = x - np.mean(x, axis=1, keepdims=True)
    x = trim_tail(x)
    x = fades(x, preroll * 0.8, min(0.03, 0.2 * x.shape[-1] / SR))
    for _ in range(3):
        x = x * db(target_lufs - lufs_momentary_max(x))
        x = limit(x, ceiling_db)
    x = np.atleast_2d(x)
    return x[0] if x.shape[0] == 1 else x


def loop_crossfade(x, n_out: int, xfade: int) -> np.ndarray:
    """Make a seamless loop of length n_out from x (length >= n_out + xfade).

    The tail beyond n_out is equal-power crossfaded into the head, so sample n_out-1 flows into
    what was sample n_out of the source: the seam is continuous and nothing fades to silence.
    """
    x = np.atleast_2d(x)
    assert x.shape[1] >= n_out + xfade
    out = x[:, :n_out].copy()
    ph = np.linspace(0, np.pi / 2, xfade)
    out[:, :xfade] = x[:, :xfade] * np.sin(ph) + x[:, n_out:n_out + xfade] * np.cos(ph)
    return out
