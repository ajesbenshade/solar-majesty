"""DSP primitives and instruments for Solar Majesty's offline score renderer.

Everything here is numpy/scipy only and deterministic given the rng passed in.

The loop-safety rule: anything that runs over a whole stem either works on a circular buffer
(``Track`` with ``cyclic=True``, ``np.roll`` delays, FFT circular convolution, ``mode='wrap'``
dynamics) or is run through :func:`cyclic` so its filter state at sample 0 is the state it would
have after playing the end of the loop. That makes every stem exactly periodic, so the seam is
not faded — it simply is not there.
"""

from __future__ import annotations

import numpy as np
from scipy import signal
from scipy.ndimage import maximum_filter1d, minimum_filter1d, uniform_filter1d

SR = 44100
TAU = 2.0 * np.pi


# ---------------------------------------------------------------------------------------------
# basics

def midi_hz(m):
    return 440.0 * 2.0 ** ((np.asarray(m, dtype=np.float64) - 69.0) / 12.0)


def db(x):
    return 20.0 * np.log10(np.maximum(np.abs(x), 1e-12))


def undb(d):
    return 10.0 ** (np.asarray(d) / 20.0)


def pan_stereo(x, pan=0.0):
    """Constant-power pan, pan in [-1, 1]. Accepts a scalar or per-sample pan."""
    th = (np.clip(pan, -1.0, 1.0) + 1.0) * (np.pi / 4.0)
    return np.stack([x * np.cos(th), x * np.sin(th)]) * np.sqrt(2.0)


def fade_tail(x, ms=8.0):
    """Short linear fade at the very end of a note buffer so truncation never clicks."""
    n = min(x.shape[-1], int(ms * 1e-3 * SR))
    if n > 1:
        x[..., -n:] *= np.linspace(1.0, 0.0, n)
    return x


def cyclic(fn, x, pad_s=3.0):
    """Run a stateful (IIR) process as if the loop had already been playing.

    Prepends the last ``pad_s`` seconds of the loop, processes, and drops the pre-roll, so the
    filter state entering sample 0 equals the state leaving sample L-1.
    """
    L = x.shape[-1]
    pad = min(L, int(pad_s * SR))
    ext = np.concatenate([x[..., L - pad:], x], axis=-1)
    y = fn(ext)
    return y[..., pad:]


def lfo(L, cycles, phase=0.0):
    """Sine LFO with an integer number of cycles per loop (periodic by construction)."""
    n = np.arange(L)
    return np.sin(TAU * cycles * n / L + phase)


def periodic_hz(f, L):
    """Nudge a frequency so a free-running oscillator completes whole cycles in the loop."""
    cycles = max(1.0, np.round(f * L / SR))
    return cycles * SR / L


# ---------------------------------------------------------------------------------------------
# oscillators (PolyBLEP band-limited)

def _phase(freq, n, phase0):
    f = np.asarray(freq, dtype=np.float64)
    if f.ndim == 0:
        dt = np.full(n, f / SR)
    else:
        dt = f / SR
    ph = np.empty(n)
    ph[0] = 0.0
    np.cumsum(dt[:-1], out=ph[1:])
    ph += phase0
    return np.mod(ph, 1.0), dt


def _blep(t, dt):
    out = np.zeros_like(t)
    m = t < dt
    x = t[m] / dt[m]
    out[m] = x + x - x * x - 1.0
    m = t > 1.0 - dt
    x = (t[m] - 1.0) / dt[m]
    out[m] = x * x + x + x + 1.0
    return out


def saw(freq, n, phase0=0.0):
    t, dt = _phase(freq, n, phase0)
    return 2.0 * t - 1.0 - _blep(t, dt)


def square(freq, n, phase0=0.0, pw=0.5):
    t, dt = _phase(freq, n, phase0)
    t2 = np.mod(t + (1.0 - pw), 1.0)
    s1 = 2.0 * t - 1.0 - _blep(t, dt)
    s2 = 2.0 * t2 - 1.0 - _blep(t2, dt)
    return s1 - s2


def tri(freq, n, phase0=0.0):
    t, _ = _phase(freq, n, phase0)
    return 2.0 * np.abs(2.0 * t - 1.0) - 1.0


def sine(freq, n, phase0=0.0):
    t, _ = _phase(freq, n, phase0)
    return np.sin(TAU * t)


# ---------------------------------------------------------------------------------------------
# envelopes

def env_adsr(n, gate, a=0.01, d=0.2, s=0.7, r=0.3):
    """Exponential ADSR over ``n`` samples with a note-on of ``gate`` samples."""
    t = np.arange(n) / SR
    g = gate / SR
    a = max(a, 1e-4)
    d = max(d, 1e-4)
    r = max(r, 1e-4)
    att = np.clip(t / a, 0.0, 1.0)
    att = att * att * (3.0 - 2.0 * att)          # smoothstep attack, no corner
    dec = s + (1.0 - s) * np.exp(-np.maximum(t - a, 0.0) / d)
    e = np.where(t < a, att, dec)
    if g < a:
        u = g / a
        lg = u * u * (3.0 - 2.0 * u)
    else:
        lg = s + (1.0 - s) * np.exp(-(g - a) / d)
    rel = lg * np.exp(-np.maximum(t - g, 0.0) / r)
    return np.where(t < g, e, rel)


def note_len(dur, release, k=4.5):
    return int((dur + release * k) * SR) + 16


# ---------------------------------------------------------------------------------------------
# filters

def _lp_coefs(fc, q):
    fc = np.clip(fc, 20.0, SR * 0.45)
    w0 = TAU * fc / SR
    c = np.cos(w0)
    al = np.sin(w0) / (2.0 * q)
    a0 = 1.0 + al
    b0 = (1.0 - c) / 2.0 / a0
    return b0, 2.0 * b0, b0, -2.0 * c / a0, (1.0 - al) / a0


def lp_tv(x, fc, q=0.707, block=64):
    """Resonant 2-pole low-pass with a time-varying cutoff (per-block coefficient update)."""
    x = np.asarray(x, dtype=np.float64)
    n = x.shape[-1]
    fc = np.asarray(fc, dtype=np.float64)
    if fc.ndim == 0:
        b0, b1, b2, a1, a2 = _lp_coefs(fc, q)
        return signal.lfilter([b0, b1, b2], [1.0, a1, a2], x, axis=-1)
    nb = (n + block - 1) // block
    centers = np.minimum(np.arange(nb) * block + block // 2, n - 1)
    b0, b1, b2, a1, a2 = _lp_coefs(fc[centers], q)
    y = np.empty_like(x)
    zi = np.zeros(x.shape[:-1] + (2,))
    for i in range(nb):
        s = i * block
        e = min(n, s + block)
        y[..., s:e], zi = signal.lfilter([b0[i], b1[i], b2[i]], [1.0, a1[i], a2[i]],
                                         x[..., s:e], axis=-1, zi=zi)
    return y


def butter(x, fc, kind="low", order=2):
    sos = signal.butter(order, fc, btype=kind, fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def bandpass(x, f, q):
    b, a = signal.iirpeak(f, q, fs=SR)
    return signal.lfilter(b, a, x, axis=-1)


def shelf_high(x, fc, gain_db, q=0.707):
    A = 10 ** (gain_db / 40.0)
    w0 = TAU * fc / SR
    c = np.cos(w0)
    al = np.sin(w0) / (2 * q)
    sa = 2 * np.sqrt(A) * al
    b = [A * ((A + 1) + (A - 1) * c + sa), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - sa)]
    a = [(A + 1) - (A - 1) * c + sa, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - sa]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=-1)


# ---------------------------------------------------------------------------------------------
# effects

def make_ir(t60, seed, predelay=0.02, damp=0.6, width=1.0, early=True, color=None):
    """Synthesised stereo room/hall impulse: band-split noise with frequency-dependent decay.

    ``damp`` scales how much faster the highs die than the lows (0 = flat, 1 = very dark).
    ``color`` optionally adds a resonant metallic tint (frequency in Hz).
    """
    rng = np.random.default_rng(seed)
    n = int(t60 * 1.3 * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal((2, n))
    # Decorrelate less when width < 1.
    noise[1] = width * noise[1] + (1.0 - width) * noise[0]
    bands = [(None, 300.0, 1.15), (300.0, 1800.0, 1.0), (1800.0, 6000.0, 1.0 - 0.45 * damp),
             (6000.0, None, 1.0 - 0.75 * damp)]
    ir = np.zeros((2, n))
    for lo, hi, mul in bands:
        if lo is None:
            b = butter(noise, hi, "low", 4)
        elif hi is None:
            b = butter(noise, lo, "high", 4)
        else:
            b = butter(noise, [lo, hi], "band", 2)
        ir += b * np.exp(-6.91 * t / max(0.05, t60 * mul))
    if color is not None:
        ir += 0.35 * bandpass(ir, color, 12.0)
    onset = np.clip(t / 0.012, 0.0, 1.0)
    ir *= onset
    if early:
        for _ in range(10):
            k = int(rng.uniform(0.004, 0.07) * SR)
            g = rng.uniform(0.25, 0.7) * np.exp(-k / SR / 0.08)
            ch = rng.integers(0, 2)
            ir[ch, k] += g * 12.0 * rng.choice([-1, 1])
    ir /= np.sqrt(np.sum(ir ** 2, axis=1, keepdims=True))
    pd = int(predelay * SR)
    return np.concatenate([np.zeros((2, pd)), ir], axis=1)


def reverb_cyclic(x, ir, wet):
    """Circular convolution: the tail that rings past the loop end lands on the loop start."""
    L = x.shape[-1]
    X = np.fft.rfft(x, axis=-1)
    H = np.fft.rfft(ir[:, :L], n=L, axis=-1)
    return np.fft.irfft(X * H, n=L, axis=-1) * wet


def reverb_linear(x, ir, wet):
    y = signal.fftconvolve(x, ir[:, :], axes=-1, mode="full")[:, : x.shape[-1]]
    return y * wet


def delay_cyclic(x, d, fb=0.45, taps=6, pingpong=True, lp=3500.0):
    """Tempo delay as a finite sum of circularly shifted copies (exactly loop-periodic)."""
    mono = x.mean(axis=0)
    out = np.zeros_like(x)
    for k in range(1, taps + 1):
        g = fb ** (k - 1)
        sh = np.roll(mono, k * d)
        if pingpong:
            out[(k - 1) % 2] += g * sh
        else:
            out += g * sh
    return cyclic(lambda z: butter(z, lp, "low", 2), out)


def comb_cyclic(x, d, g=0.6, taps=14):
    out = x.copy()
    for k in range(1, taps + 1):
        out += (g ** k) * np.roll(x, k * d, axis=-1)
    return out * (1.0 - g)


def chorus_cyclic(x, cycles=3, base_ms=11.0, depth_ms=3.5, mix=0.5):
    L = x.shape[-1]
    n = np.arange(L, dtype=np.float64)
    out = x.copy() * (1.0 - mix * 0.5)
    for ch, ph in ((0, 0.0), (1, np.pi / 2)):
        d = (base_ms + depth_ms * np.sin(TAU * cycles * n / L + ph)) * 1e-3 * SR
        idx = np.mod(n - d, L)
        src = np.concatenate([x[ch], x[ch, :1]])
        out[ch] += mix * np.interp(idx, np.arange(L + 1), src)
    return out


def chorus_linear(x, rate=0.4, base_ms=11.0, depth_ms=3.5, mix=0.5):
    L = x.shape[-1]
    n = np.arange(L, dtype=np.float64)
    out = x.copy() * (1.0 - mix * 0.5)
    for ch, ph in ((0, 0.0), (1, np.pi / 2)):
        d = (base_ms + depth_ms * np.sin(TAU * rate * n / SR + ph)) * 1e-3 * SR
        out[ch] += mix * np.interp(np.clip(n - d, 0, L - 1), np.arange(L), x[ch])
    return out


def widen(x, amount=1.3):
    m = 0.5 * (x[0] + x[1])
    s = 0.5 * (x[0] - x[1]) * amount
    return np.stack([m + s, m - s])


def saturate(x, drive=1.5):
    return np.tanh(drive * x) / np.tanh(drive)


def glue_compress(x, thr_db=-18.0, ratio=2.0, win_ms=40.0, smooth_ms=150.0, mode="wrap"):
    """Look-ahead RMS compressor (non-causal smoothing; offline so no pumping lag).

    ``mode='wrap'`` treats the signal as a loop, so gain at the seam matches both sides.
    """
    win = max(3, int(win_ms * 1e-3 * SR) | 1)
    sm = max(3, int(smooth_ms * 1e-3 * SR) | 1)
    p = np.max(x ** 2, axis=0)
    lev = db(np.sqrt(uniform_filter1d(p, win, mode=mode))) - 0.0
    gr = np.maximum(0.0, lev - thr_db) * (1.0 - 1.0 / ratio)
    gr = uniform_filter1d(maximum_filter1d(gr, win, mode=mode), sm, mode=mode)
    return x * undb(-gr), gr


def limiter_gain(peak, thr, half=220, mode="wrap"):
    """Gain curve g <= thr/|peak| at every sample, smooth (box-of-min construction).

    With a min filter and a box average of the same half-width h, the smoothed gain at any
    sample p is an average of values that were each minimised over a window containing p, so
    it can never exceed the gain p itself required. That is the no-overshoot guarantee.
    """
    req = np.minimum(1.0, thr / np.maximum(peak, 1e-12))
    size = 2 * half + 1
    m = minimum_filter1d(req, size, mode=mode)
    return uniform_filter1d(m, size, mode=mode)


# ---------------------------------------------------------------------------------------------
# timeline

class Track:
    """Stereo accumulation buffer. Cyclic tracks wrap anything that rings past the loop end."""

    def __init__(self, L, cyclic=True):
        self.L = L
        self.cyclic = cyclic
        self.buf = np.zeros((2, L))

    def add(self, pos, sig, pan=0.0, gain=1.0):
        st = pan_stereo(sig, pan) if sig.ndim == 1 else sig
        st = st * gain
        n = st.shape[1]
        L = self.L
        if not self.cyclic:
            if pos >= L:
                return
            s = max(0, pos)
            e = min(L, pos + n)
            self.buf[:, s:e] += st[:, s - pos: e - pos]
            return
        pos %= L
        off = 0
        while off < n:
            take = min(n - off, L - pos)
            self.buf[:, pos:pos + take] += st[:, off:off + take]
            off += take
            pos = 0


# ---------------------------------------------------------------------------------------------
# tonal instruments. Each returns a mono (n,) or stereo (2, n) array with release included.

def pad_saw(freq, dur, rng, voices=3, detune=11.0, attack=0.8, release=2.0, fc=2400.0,
            width=0.8, sustain=0.85, sub=0.0):
    n = note_len(dur, release, 3.5)
    gate = int(dur * SR)
    out = np.zeros((2, n))
    spread = np.linspace(-1.0, 1.0, voices) if voices > 1 else np.zeros(1)
    for sp in spread:
        f = freq * 2.0 ** (sp * detune / 1200.0)
        out += pan_stereo(saw(f, n, rng.random()), sp * width)
    if sub > 0:
        out += pan_stereo(sine(freq * 0.5, n, rng.random()) * sub * voices, 0.0)
    out *= env_adsr(n, gate, attack, 1.2, sustain, release) / voices
    out = butter(out, fc, "low", 2)
    return fade_tail(out)


def pad_glass(freq, dur, rng, attack=1.4, release=3.0, shimmer=0.25):
    n = note_len(dur, release, 3.5)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    out = np.zeros((2, n))
    for sp, c in ((-1.0, -4.0), (1.0, 4.0)):
        f = freq * 2.0 ** (c / 1200.0)
        x = sine(f, n, rng.random()) + 0.28 * tri(f * 2.0, n, rng.random()) \
            + 0.10 * sine(f * 3.0, n, rng.random())
        trem = 1.0 + shimmer * 0.3 * np.sin(TAU * rng.uniform(0.15, 0.35) * t + rng.random() * TAU)
        out += pan_stereo(x * trem, sp * 0.75)
    out *= env_adsr(n, gate, attack, 2.0, 0.8, release) * 0.5
    return fade_tail(out)


def pad_choir(freq, dur, rng, attack=1.8, release=3.5):
    """Formant-filtered saw ensemble ('ooh/aah' between) for Europa's ice choir."""
    n = note_len(dur, release, 3.5)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    src = np.zeros((2, n))
    for sp in (-1.0, -0.35, 0.35, 1.0):
        vib = 1.0 + 0.003 * np.sin(TAU * rng.uniform(4.2, 5.4) * t + rng.random() * TAU)
        src += pan_stereo(saw(freq * vib * 2.0 ** (sp * 7.0 / 1200.0), n, rng.random()), sp * 0.8)
    f1 = bandpass(src, 420.0, 5.0)
    f2 = bandpass(src, 950.0, 6.0) * 0.6
    f3 = bandpass(src, 2600.0, 8.0) * 0.18
    out = (f1 + f2 + f3) * env_adsr(n, gate, attack, 2.0, 0.85, release) * 0.35
    return fade_tail(out)


def pluck(freq, dur, vel=1.0, wave="saw", fc0=350.0, fc_env=3800.0, fdecay=0.16, q=1.3,
          adecay=0.4, release=0.12, drive=0.0, pw=0.5, phase=0.0):
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    if wave == "saw":
        x = saw(freq, n, phase) * 0.7 + saw(freq * 1.004, n, phase + 0.3) * 0.5
    elif wave == "square":
        x = square(freq, n, phase, pw)
    elif wave == "tri":
        x = tri(freq, n, phase)
    else:
        x = sine(freq, n, phase)
    fc = fc0 + fc_env * vel * np.exp(-t / fdecay)
    x = lp_tv(x, fc, q, block=32)
    env = np.exp(-t / adecay) * np.clip(t / 0.002, 0.0, 1.0)
    env *= np.where(t < gate / SR, 1.0, np.exp(-(t - gate / SR) / release))
    if drive > 0:
        x = saturate(x * env, drive)
        return fade_tail(x * vel)
    return fade_tail(x * env * vel)


def epiano(freq, dur, vel=1.0, release=0.4):
    """Two-operator FM electric piano: warm body plus a short tine."""
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    idx = (1.1 + 1.2 * vel) * np.exp(-t / 0.35)
    body = np.sin(TAU * freq * t + idx * np.sin(TAU * freq * t))
    tine = np.sin(TAU * freq * 7.0 * t) * np.exp(-t / 0.025) * 0.18 * min(1.0, 900.0 / freq)
    env = np.exp(-t / 1.2) * np.clip(t / 0.003, 0.0, 1.0)
    env *= np.where(t < gate / SR, 1.0, np.exp(-(t - gate / SR) / release))
    return fade_tail((body + tine) * env * vel)


def fm_bell(freq, dur, vel=1.0, ratio=3.5, index=2.2, decay=1.6, idecay=0.35, release=None):
    n = int(decay * 4.5 * SR) + 16
    t = np.arange(n) / SR
    ix = index * min(1.0, 900.0 / freq) * np.exp(-t / idecay)
    mod = np.sin(TAU * freq * ratio * t) * ix
    car = np.sin(TAU * freq * t + mod)
    car += 0.25 * np.sin(TAU * freq * 2.0 * t) * np.exp(-t / (decay * 0.5))
    env = np.exp(-t / decay) * np.clip(t / 0.0015, 0.0, 1.0)
    return fade_tail(car * env * vel)


_KS_CACHE: dict = {}


def ks_pluck(freq, vel=1.0, rho=0.996, bright=0.55, seconds=1.6, seed=0):
    """Karplus-Strong string (Mars' dusty 'desert guitar'), cached per pitch."""
    key = (round(float(freq), 3), rho, bright, seconds, seed)
    if key not in _KS_CACHE:
        rng = np.random.default_rng(seed + int(freq * 10))
        N = max(2, int(SR / freq - 0.5))
        n = int(seconds * SR)
        exc = np.zeros(n)
        burst = rng.uniform(-1, 1, N)
        burst = signal.lfilter([bright, 1 - bright], [1.0], burst)
        exc[:N] = burst
        a = np.zeros(N + 2)
        a[0] = 1.0
        a[N] = -rho * 0.5
        a[N + 1] = -rho * 0.5
        y = signal.lfilter([1.0], a, exc)
        y = butter(y, 60.0, "high", 2)
        y /= max(1e-9, np.max(np.abs(y)))
        _KS_CACHE[key] = fade_tail(y, 30.0)
    return _KS_CACHE[key] * vel


def lead(freq, dur, vel=1.0, prev=None, kind="saw", glide=0.06, vib=0.0045, fc=1900.0, q=0.9,
         attack=0.035, release=0.35, bloom=0.9, pw=0.5, drive=0.0):
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    f = np.full(n, float(freq))
    if prev is not None and glide > 0:
        f = freq * (prev / freq) ** np.exp(-t / glide)
    vramp = np.clip((t - 0.22) / 0.5, 0.0, 1.0)
    f = f * (1.0 + vib * vramp * np.sin(TAU * 5.1 * t))
    if kind == "saw":
        x = saw(f, n, 0.1) * 0.6 + saw(f * 1.0035, n, 0.6) * 0.5
    elif kind == "tri":
        x = tri(f, n) + 0.12 * sine(f * 2.0, n)
    elif kind == "square":
        x = square(f, n, 0.0, pw) * 0.8
    elif kind == "sine":
        x = sine(f, n) + 0.18 * sine(f * 2.0, n) + 0.05 * sine(f * 3.0, n)
    else:  # horn: saw + square blend
        x = saw(f, n, 0.2) * 0.55 + square(f * 0.5, n, 0.0, 0.42) * 0.35
    cut = fc * (1.0 + bloom * np.exp(-t / 0.18)) * (0.6 + 0.4 * vel)
    x = lp_tv(x, cut, q, block=64)
    x *= env_adsr(n, gate, attack, 0.4, 0.8, release)
    if drive > 0:
        x = saturate(x, drive)
    return fade_tail(x * vel)


def bass(freq, dur, vel=1.0, fc=260.0, env_amt=900.0, fdecay=0.12, q=1.3, sub=0.6, drive=1.2,
         attack=0.004, release=0.09, wave="saw", decay=0.8, sustain=0.75):
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    if wave == "saw":
        x = saw(freq, n, 0.0)
    else:
        x = square(freq, n, 0.0, 0.5) * 0.7
    x = x + sub * sine(freq * 0.5 if freq > 70 else freq, n, 0.25)
    x = lp_tv(x, fc + env_amt * vel * np.exp(-t / fdecay), q, block=32)
    x *= env_adsr(n, gate, attack, decay, sustain, release)
    if drive > 0:
        x = saturate(x, drive)
    return fade_tail(x * vel)


def brass(freq, dur, vel=1.0, rng=None, fc_lo=220.0, fc_hi=1500.0, swell=0.7, release=0.6,
          drive=2.2, voices=3):
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    t = np.arange(n) / SR
    x = np.zeros((2, n))
    spread = np.linspace(-1, 1, voices)
    for sp in spread:
        ph = rng.random() if rng is not None else 0.0
        x += pan_stereo(saw(freq * 2 ** (sp * 9 / 1200), n, ph), sp * 0.5)
    rise = np.clip(t / swell, 0.0, 1.0) ** 1.6
    cut = fc_lo + (fc_hi - fc_lo) * rise * vel
    x = lp_tv(x, cut, 1.1, block=64) / voices
    x *= env_adsr(n, gate, swell * 0.9, 0.6, 0.9, release)
    return fade_tail(saturate(x, drive) * vel)


def sine_tone(freq, dur, vel=1.0, attack=0.02, release=0.4, harm=0.0):
    n = note_len(dur, release, 4.0)
    gate = int(dur * SR)
    x = sine(freq, n) + harm * sine(freq * 2.0, n)
    return fade_tail(x * env_adsr(n, gate, attack, 0.3, 0.9, release) * vel)


# ---------------------------------------------------------------------------------------------
# percussion (rng is always passed so hits vary but stay deterministic)

def kick(rng, vel=1.0, f0=150.0, f1=46.0, tau=0.045, decay=0.38, click=0.25, drive=1.6):
    n = int((decay * 4) * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / tau)
    ph = np.cumsum(f) / SR
    body = np.sin(TAU * ph) * np.exp(-t / decay)
    cl = butter(rng.standard_normal(n), 1800.0, "high", 2) * np.exp(-t / 0.004) * click
    return fade_tail(saturate(body + cl, drive) * vel)


def snare(rng, vel=1.0, tone=185.0, decay=0.16, bright=5500.0):
    n = int(decay * 5 * SR)
    t = np.arange(n) / SR
    nz = butter(rng.standard_normal(n), [900.0, bright], "band", 2) * np.exp(-t / decay)
    body = np.sin(TAU * tone * t) * np.exp(-t / 0.05) * 0.8
    return fade_tail((nz * 1.6 + body) * vel)


def clap(rng, vel=1.0, decay=0.14):
    n = int((0.03 + decay * 5) * SR)
    t = np.arange(n) / SR
    env = np.zeros(n)
    for k, off in enumerate((0.0, 0.011, 0.021)):
        env += np.where(t >= off, np.exp(-(t - off) / 0.006), 0.0) * (0.8 if k < 2 else 1.0)
    env += np.where(t >= 0.021, np.exp(-(t - 0.021) / decay), 0.0) * 0.6
    nz = butter(rng.standard_normal(n), [1000.0, 3200.0], "band", 2)
    return fade_tail(nz * env * vel * 1.4)


_HAT_RATIOS = np.array([1.0, 1.4471, 1.6170, 1.9265, 2.5028, 2.6637])


def hat(rng, vel=1.0, decay=0.045, metal=0.6, hp=7000.0):
    n = int(decay * 6 * SR) + 64
    t = np.arange(n) / SR
    nz = rng.standard_normal(n)
    if metal > 0:
        m = sum(square(330.0 * r, n, rng.random()) for r in _HAT_RATIOS) / 6.0
        nz = (1.0 - metal) * nz + metal * m * 2.0
    x = butter(nz, hp, "high", 2) * np.exp(-t / decay)
    return fade_tail(x * vel * 0.9)


def shaker(rng, vel=1.0, decay=0.05, grit=0.0):
    n = int(decay * 6 * SR) + 64
    t = np.arange(n) / SR
    env = np.clip(t / 0.012, 0, 1) * np.exp(-t / decay)
    x = butter(rng.standard_normal(n), [3500.0, 11000.0], "band", 2) * env
    if grit > 0:
        steps = 2 ** (8 - 4 * grit)
        x = np.round(x * steps) / steps
    return fade_tail(x * vel)


def tom(rng, vel=1.0, f0=160.0, f1=90.0, decay=0.35, noise=0.25):
    n = int(decay * 5 * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.06)
    body = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / decay)
    nz = butter(rng.standard_normal(n), 900.0, "low", 2) * np.exp(-t / 0.05) * noise * 3
    return fade_tail(saturate(body + nz, 1.3) * vel)


def taiko(rng, vel=1.0, f=62.0, decay=0.7):
    x = tom(rng, 1.0, f * 1.6, f, decay, noise=0.5)
    n = x.shape[0]
    t = np.arange(n) / SR
    skin = butter(rng.standard_normal(n), [150.0, 900.0], "band", 2) * np.exp(-t / 0.08) * 0.6
    return fade_tail((x + skin) * vel)


def clang(rng, vel=1.0, f=180.0, decay=1.2):
    """Tuned anvil / girder hit: inharmonic partials with the fundamental on a scale tone."""
    ratios = np.array([1.0, 2.0, 2.76, 3.0, 5.40, 8.93])
    amps = np.array([1.0, 0.5, 0.55, 0.3, 0.3, 0.15])
    decs = decay * np.array([1.0, 0.8, 0.45, 0.6, 0.25, 0.12])
    n = int(decay * 4.5 * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for r, a, d in zip(ratios, amps, decs):
        if f * r < SR * 0.45:
            x += a * np.sin(TAU * f * r * t + rng.random() * TAU) * np.exp(-t / d)
    x += butter(rng.standard_normal(n), 3000.0, "high", 2) * np.exp(-t / 0.006) * 0.8
    return fade_tail(x * vel * 0.45)


def tick(rng, vel=1.0, f=3200.0, decay=0.006):
    n = int(decay * 8 * SR) + 64
    t = np.arange(n) / SR
    x = np.sin(TAU * f * t) * np.exp(-t / decay)
    x += butter(rng.standard_normal(n), 5000.0, "high", 2) * np.exp(-t / 0.0015) * 0.5
    return fade_tail(x * vel)


def ping(freq, vel=1.0, decay=0.6):
    n = int(decay * 5 * SR)
    t = np.arange(n) / SR
    x = np.sin(TAU * freq * t) * np.exp(-t / decay) * np.clip(t / 0.001, 0, 1)
    x += 0.2 * np.sin(TAU * freq * 2.01 * t) * np.exp(-t / (decay * 0.3))
    return fade_tail(x * vel)


def crack(rng, vel=1.0):
    """Ice fracture: a short cluster of resonant clicks."""
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for _ in range(rng.integers(3, 8)):
        k = int(rng.uniform(0.0, 0.05) * SR)
        x[k] += rng.uniform(0.4, 1.0) * rng.choice([-1, 1])
    x = bandpass(x, rng.uniform(1800, 4200), 4.0) + 0.5 * bandpass(x, rng.uniform(600, 1100), 6.0)
    x = butter(x, 300.0, "high", 2) * np.exp(-t / 0.08)
    return fade_tail(x * vel * 3.0)


def boom(rng, vel=1.0, f0=70.0, f1=32.0, decay=1.4):
    n = int(decay * 4 * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.18)
    body = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / decay)
    nz = butter(rng.standard_normal(n), 400.0, "low", 2) * np.exp(-t / 0.3) * 0.6
    return fade_tail(saturate(body + nz, 1.4) * vel)


def riser(rng, dur, vel=1.0, fc0=250.0, fc1=7000.0, tone=None):
    n = int(dur * SR)
    t = np.arange(n) / SR
    u = t / dur
    x = rng.standard_normal(n)
    x = lp_tv(x, fc0 * (fc1 / fc0) ** u, 2.2, block=128)
    if tone is not None:
        f = tone * 2 ** (u * 7 / 12)
        x = x + 0.4 * saw(f, n, 0.0) * 0.5
    x *= u ** 2.2
    x[-int(0.02 * SR):] *= np.linspace(1, 0, int(0.02 * SR))
    return x * vel


def cymbal_swell(rng, dur, vel=1.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    m = sum(square(420.0 * r, n, rng.random()) for r in _HAT_RATIOS) / 6.0
    x = butter(0.5 * rng.standard_normal(n) + m, 4500.0, "high", 2)
    x *= (t / dur) ** 2.5
    x[-int(0.015 * SR):] *= np.linspace(1, 0, int(0.015 * SR))
    return x * vel * 0.6
