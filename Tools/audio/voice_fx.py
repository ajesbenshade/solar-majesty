"""Robot voice processing for the bark bake (render_voices.py). Live lines from the Kokoro server
(Tools/local_ai/voice_server.py) get HeroSpeech.ApplyRobot in-game at a matching strength instead.

Every speaker in Solar Majesty is a machine, so the processing is part of the character: the
Overseer is clean with a digital sheen, workers get a servo box, mechs a heavy armoured
resonance, drones a small radio speaker. Intelligibility wins every trade-off; each preset keeps
the 1-4 kHz consonant band intact.
"""

from __future__ import annotations

from fractions import Fraction

import numpy as np
from scipy import signal

SAMPLE_RATE = 24000  # Kokoro's native rate; the game plays clips at their own rate.


def _butter(kind: str, freq, sr: int, order: int = 2):
    return signal.butter(order, freq, btype=kind, fs=sr, output="sos")


def _filt(x: np.ndarray, sos) -> np.ndarray:
    return signal.sosfilt(sos, x)


def pitch_ratio(semitones: float) -> float:
    return float(2.0 ** (semitones / 12.0))


def resample_by(x: np.ndarray, ratio: float) -> np.ndarray:
    """Play x back `ratio` times faster (higher pitch, shorter). Formants move with it, which is the
    point: a scout drone should sound small and a sentinel should sound big."""
    if abs(ratio - 1.0) < 1e-4:
        return x
    frac = Fraction(1.0 / ratio).limit_denominator(64)
    return signal.resample_poly(x, frac.numerator, frac.denominator)


def _comb(x: np.ndarray, sr: int, delay_ms: float, feedback: float, mix: float) -> np.ndarray:
    """Short feedback comb: the 'speaking from inside a metal chassis' resonance."""
    d = max(1, int(sr * delay_ms / 1000.0))
    b = np.zeros(d + 1)
    b[0] = 1.0
    a = np.zeros(d + 1)
    a[0] = 1.0
    a[d] = -feedback
    wet = signal.lfilter(b, a, x)
    wet /= max(1e-9, np.max(np.abs(wet))) / max(1e-9, np.max(np.abs(x)))
    return (1.0 - mix) * x + mix * wet


def _ring(x: np.ndarray, sr: int, hz: float, mix: float) -> np.ndarray:
    t = np.arange(len(x)) / sr
    return (1.0 - mix) * x + mix * x * np.sin(2 * np.pi * hz * t)


def _chorus(x: np.ndarray, sr: int, ms: float, depth_ms: float, rate_hz: float, mix: float) -> np.ndarray:
    n = np.arange(len(x))
    delay = (ms + depth_ms * np.sin(2 * np.pi * rate_hz * n / sr)) * sr / 1000.0
    idx = np.clip(n - delay, 0, len(x) - 1)
    i0 = np.floor(idx).astype(int)
    i1 = np.minimum(i0 + 1, len(x) - 1)
    frac = idx - i0
    wet = x[i0] * (1 - frac) + x[i1] * frac
    return (1.0 - mix) * x + mix * wet


def _crush(x: np.ndarray, sr: int, target_sr: int, bits: int, mix: float) -> np.ndarray:
    hold = max(1, int(round(sr / target_sr)))
    held = np.repeat(x[::hold], hold)[: len(x)]
    q = 2 ** (bits - 1)
    crushed = np.round(held * q) / q
    return (1.0 - mix) * x + mix * crushed


def _drive(x: np.ndarray, amount: float) -> np.ndarray:
    if amount <= 0:
        return x
    k = 1.0 + amount * 6.0
    return np.tanh(k * x) / np.tanh(k)


def _room(x: np.ndarray, sr: int, seconds: float, mix: float, seed: int = 7) -> np.ndarray:
    """Tiny synthetic room so dry TTS doesn't sound pasted on top of the mix."""
    n = int(sr * seconds)
    rng = np.random.default_rng(seed)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = _filt(ir, _butter("lowpass", 5000, sr))
    ir /= np.sqrt(np.sum(ir ** 2))
    wet = signal.fftconvolve(x, ir)[: len(x)]
    return (1.0 - mix) * x + mix * wet


def _presence(x: np.ndarray, sr: int, hz: float, gain: float) -> np.ndarray:
    """Add a band-passed copy for a gentle intelligibility lift around `hz`."""
    band = _filt(x, _butter("bandpass", [hz * 0.6, min(hz * 1.6, sr / 2 - 100)], sr))
    return x + gain * band


PRESETS = {
    # Grok: must be the most intelligible voice in the game. Clean, slight digital doubling.
    "overseer": lambda x, sr: _room(
        _presence(_chorus(_filt(x, _butter("highpass", 90, sr)), sr, 9.0, 0.6, 0.35, 0.18), sr, 2600, 0.25),
        sr, 0.18, 0.08),
    # Workers: servo box. Light chassis resonance, a whisper of ring mod.
    "servo": lambda x, sr: _room(
        _filt(_ring(_comb(_filt(x, _butter("highpass", 110, sr)), sr, 2.6, 0.35, 0.22), sr, 48.0, 0.10),
              _butter("lowpass", 7200, sr)),
        sr, 0.14, 0.07),
    # Mechs: armoured, heavy, a little driven. Still keeps the consonant band.
    "mech": lambda x, sr: _room(
        _presence(_drive(_ring(_comb(_filt(x, _butter("highpass", 70, sr)), sr, 5.5, 0.55, 0.35), sr, 31.0, 0.16),
                         0.25), sr, 2200, 0.2),
        sr, 0.22, 0.10),
    # Drones and surveyors: small speaker / radio link.
    "drone": lambda x, sr: _crush(
        _drive(_filt(x, _butter("bandpass", [330, 5200], sr, order=3)), 0.18), sr, 12000, 12, 0.35),
    "none": lambda x, sr: x,
}


def edge_fades(x: np.ndarray, sr: int, ms: float = 6.0) -> np.ndarray:
    n = min(len(x) // 2, int(sr * ms / 1000.0))
    if n > 0:
        ramp = np.linspace(0.0, 1.0, n)
        x = x.copy()
        x[:n] *= ramp
        x[-n:] *= ramp[::-1]
    return x


def trim_silence(x: np.ndarray, sr: int, threshold_db: float = -48.0, pad_ms: float = 40.0) -> np.ndarray:
    thr = 10 ** (threshold_db / 20.0) * max(1e-9, np.max(np.abs(x)))
    idx = np.where(np.abs(x) > thr)[0]
    if len(idx) == 0:
        return x
    pad = int(sr * pad_ms / 1000.0)
    return x[max(0, idx[0] - pad): min(len(x), idx[-1] + pad)]


def normalize(x: np.ndarray, sr: int, target_rms_db: float = -20.0, peak_db: float = -1.5) -> np.ndarray:
    """Loudness-match all voices (RMS over voiced frames) then clamp the peak."""
    frame = int(sr * 0.03)
    if len(x) > frame:
        frames = x[: len(x) // frame * frame].reshape(-1, frame)
        rms = np.sqrt(np.mean(frames ** 2, axis=1))
        voiced = rms[rms > np.max(rms) * 0.1]
        level = np.sqrt(np.mean(voiced ** 2)) if len(voiced) else np.sqrt(np.mean(x ** 2))
    else:
        level = np.sqrt(np.mean(x ** 2))
    x = x * (10 ** (target_rms_db / 20.0) / max(1e-9, level))
    peak = np.max(np.abs(x))
    ceiling = 10 ** (peak_db / 20.0)
    if peak > ceiling:
        # Soft-knee limit rather than scaling everything down for one plosive.
        x = np.tanh(x / ceiling * 0.9) / np.tanh(0.9) * ceiling
    return x


def process(raw: np.ndarray, sr: int, cast: dict) -> np.ndarray:
    """Apply a cast entry's pitch and FX to raw TTS audio. `raw` must have been synthesised at
    `synth_speed(cast)` so that the pitch shift lands on the cast's intended tempo."""
    x = np.asarray(raw, dtype=np.float64)
    x = resample_by(x, pitch_ratio(cast.get("pitch", 0.0)))
    x = trim_silence(x, sr)
    fx = PRESETS.get(cast.get("fx", "none"), PRESETS["none"])
    x = fx(x, sr)
    x = normalize(x, sr)
    return edge_fades(x, sr).astype(np.float32)


def synth_speed(cast: dict) -> float:
    """Kokoro speed that, after the resample pitch shift, lands on the cast's speaking rate."""
    speed = float(cast.get("speed", 1.0)) / pitch_ratio(cast.get("pitch", 0.0))
    return float(np.clip(speed, 0.5, 2.0))
