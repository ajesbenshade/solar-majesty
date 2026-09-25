"""Per-world ambient beds (stereo, seamless loops).

Each world is built from the same three strata so the beds sit together as a family:
  1. a continuous "air" layer (wind where there is atmosphere, suit/structure hum where there
     is not),
  2. a quiet machinery drone tuned to the body's AmbientHum from CelestialBodyCatalog, and
  3. sparse positioned events on a reverberant bus (distant clanks, creaks, ice pings...).
Beds are rendered longer than needed and the overhang is crossfaded into the head, so the loop
point is continuous and nothing ever fades to silence.
"""

from __future__ import annotations

import numpy as np
from scipy import signal

from sfx_dsp import (
    SR, Canvas, biquad, bp, brown, crackle, env_exp, env_pts, glide, hp, lfo_noise, lp, modal, ns,
    pan, pink, reverb_ir, saw, sine, square, sweep, tt, white,
)
from sfx_events import PLATE, click, metal_hit, servo, thump

# World -> (AmbientHum from CelestialBodyCatalog, integrated LUFS target)
WORLDS = {
    "earth": (72.0, -30.0),
    "luna": (48.0, -34.0),
    "mars": (58.0, -30.5),
    "belt": (38.0, -31.0),
    "europa": (44.0, -31.0),
}


def _st(n, r, fn, width=0.8):
    """Stereo noise pair: a shared component plus independent per-side components."""
    common = fn(n, r)
    return np.vstack([common * (1 - width) + fn(n, r) * width,
                      common * (1 - width) + fn(n, r) * width])


def _drone(n, r, f0, harmonics, lp_f=700.0, beat=0.25, throb=1.3, depth=0.15):
    """Distant machinery: a harmonic stack with a slowly beating twin and a mechanical throb."""
    out = np.zeros(n)
    for k, a in enumerate(harmonics, start=1):
        out += a * sine(f0 * k * (1 + 0.002 * lfo_noise(n, r, 0.05)), n, r.uniform(0, 6.28))
        out += a * 0.5 * sine(f0 * k + beat * k, n, r.uniform(0, 6.28))
    out *= 1 + depth * sine(throb, n)
    return lp(out, lp_f, 2)


def _events_bus(n, r, schedule, rt60, mix=0.8, dark=1500.0, bright=5000.0):
    """Place mono events in stereo and push them back into a shared reverb (distance)."""
    dry = Canvas(n / SR, channels=2)
    for t, x, p, g in schedule:
        dry.add(pan(x, p), t, g)
    d = dry.out()
    d = d[:, :n] if d.shape[1] >= n else np.pad(d, ((0, 0), (0, n - d.shape[1])))
    ir = reverb_ir(r, rt60, stereo=True, dark=dark, bright=bright, predelay=0.03)
    wet = np.vstack([signal.fftconvolve(d[0], ir[0])[:n], signal.fftconvolve(d[1], ir[1])[:n]])
    return d * (1 - mix) + wet * mix * 1.4


def _times(r, total, mean_gap, jitter=0.5, start=0.5):
    out, t = [], start + r.uniform(0, mean_gap)
    while t < total - 0.5:
        out.append(t)
        t += mean_gap * r.uniform(1 - jitter, 1 + jitter)
    return out


def _gust(n, r, rate=0.12):
    g = 0.55 + 0.45 * (0.75 * lfo_noise(n, r, rate) + 0.25 * lfo_noise(n, r, rate * 5))
    return np.clip(g, 0.05, 1.0)


# --------------------------------------------------------------------------------------------

def bed_earth(r, n, flavor):
    hum, _ = WORLDS["earth"]
    f0 = hum * (1.12 if flavor else 1.0)
    total = n / SR
    g = _gust(n, r)
    wind = np.zeros((2, n))
    for ch in range(2):
        cr = np.random.default_rng(r.integers(1 << 31))
        gg = g * (0.9 + 0.1 * lfo_noise(n, cr, 0.3))
        x = sweep(pink(n, cr), "lp", 220 + 1150 * gg ** 1.5, q=0.8, block=256)
        x += biquad(pink(n, cr), "bp", 1350 * (1 + 0.1 * lfo_noise(n, cr, 0.2)), 12) * gg ** 2 * 0.25
        wind[ch] = x * (0.25 + 0.75 * gg)
    drone = _drone(n, r, f0, [1, 0.6, 0.4, 0.3, 0.15, 0.1, 0.06], 600)
    sched = []
    for t in _times(r, total, 5.5):
        kind = r.integers(4)
        if kind == 0:
            x = lp(metal_hit(r, r.uniform(180, 380), 0.4, modes=10, bright=0.4), 1500)
            gain = 0.35
        elif kind == 1:
            m = ns(r.uniform(0.6, 1.1))
            x = bp(white(m, r), 1000, 4000) * env_pts(m, [(0, 0), (0.05, 1), (m / SR, 0)])
            gain = 0.12
        elif kind == 2:
            x = lp(servo(r, r.uniform(260, 320), r.uniform(360, 460), r.uniform(1.2, 2.0), grit=0.3), 1200)
            gain = 0.18
        else:
            m = ns(3.0)
            x = lp(saw(45 * (1 + 0.02 * lfo_noise(m, r, 1)), m, 20), 380)
            x *= env_pts(m, [(0, 0), (1.2, 1), (1.8, 1), (3.0, 0)]) * (1 + 0.3 * sine(7, m))
            gain = 0.15
        sched.append((t, x, r.uniform(-0.8, 0.8), gain))
    ev = _events_bus(n, r, sched, rt60=2.5, mix=0.75)
    return wind * 0.5 + np.vstack([drone, drone]) * 0.09 + ev


def bed_luna(r, n, flavor):
    hum, _ = WORLDS["luna"]
    f0 = hum * (1.12 if flavor else 1.0)
    total = n / SR
    fan = _st(n, r, pink, 0.3)
    fan = np.vstack([bp(fan[0], 180, 2400), bp(fan[1], 180, 2400)]) * (1 + 0.05 * lfo_noise(n, r, 0.2))
    drone = _drone(n, r, f0, [1, 0.7, 0.35, 0.2, 0.1, 0.05], 900, beat=0.18, throb=0.6, depth=0.08)
    gate = np.clip((lfo_noise(n, r, 0.15) - 0.3) * 3, 0, 1)
    buzz = lp(square(120.0 * (1.03 if flavor else 1.0), n, 21), 3000) * gate * 0.25
    fizz = hp(crackle(n, r, 3.0 + 25 * gate, 0.0003), 2000) * 0.15
    sched = []
    for t in _times(r, total, 4.2):
        kind = r.integers(4)
        if kind == 0:   # servo tick conducted through the hull
            x = lp(metal_hit(r, r.uniform(600, 1200), 0.05, modes=6), 1500)
            gain = 0.35
        elif kind == 1:  # relay click
            x = lp(click(r, 0.003, 800, 6000), 3000)
            gain = 0.3
        elif kind == 2:  # faint comms squelch
            m = ns(r.uniform(0.12, 0.25))
            x = bp(white(m, r), 1200, 2800) * env_pts(m, [(0, 0), (0.01, 1), (m / SR - 0.02, 0.8), (m / SR, 0)])
            x *= 1 + 0.6 * sine(r.uniform(40, 80), m)
            gain = 0.05
        else:  # a footfall on regolith, felt more than heard
            x = lp(thump(r, 70, 40, 0.25, 0.08), 400)
            gain = 0.35
        sched.append((t, x, r.uniform(-0.5, 0.5), gain))
    ev = _events_bus(n, r, sched, rt60=0.5, mix=0.25, dark=1200)  # vacuum: structure-borne, dry
    return fan * 0.1 + np.vstack([drone, drone]) * 0.12 + np.vstack([buzz + fizz] * 2) * 0.3 + ev


def bed_mars(r, n, flavor):
    hum, _ = WORLDS["mars"]
    f0 = hum * (1.12 if flavor else 1.0)
    total = n / SR
    g = _gust(n, r, 0.16)
    wind = np.zeros((2, n))
    for ch in range(2):
        cr = np.random.default_rng(r.integers(1 << 31))
        gg = g * (0.85 + 0.15 * lfo_noise(n, cr, 0.4))
        thin = sweep(pink(n, cr), "bp", 650 + 1700 * gg, q=0.9, block=256) * (0.2 + 0.8 * gg)
        whistle = (biquad(pink(n, cr), "bp", 950 * (1 + 0.08 * lfo_noise(n, cr, 0.25)), 18)
                   + biquad(pink(n, cr), "bp", 1720 * (1 + 0.08 * lfo_noise(n, cr, 0.3)), 18)) * gg ** 2 * 0.35
        grit = hp(crackle(n, cr, 200 + 3500 * gg ** 2, 0.00025), 2500) * 0.6
        low = lp(brown(n, cr), 120) * gg * 0.5
        wind[ch] = thin + whistle + grit * gg + low
    # dust devil sweeping across the stereo field
    m = ns(6.0)
    dd = bp(pink(m, r), 500, 3000) * env_pts(m, [(0, 0), (3.0, 1), (6.0, 0)])
    sched = [(r.uniform(4, total - 8), dd, 0.0, 0.0)]
    t_dd = sched[0][0]
    drone = _drone(n, r, f0, [1, 0.5, 0.3, 0.2, 0.1], 450)
    for t in _times(r, total, 3.5):
        x = metal_hit(r, r.uniform(2500, 5000), 0.02, modes=4, bright=0.5)  # pebble ticks on panels
        sched.append((t, x, r.uniform(-0.9, 0.9), 0.08))
    ev = _events_bus(n, r, sched[1:], rt60=1.2, mix=0.5)
    i = int(t_dd * SR)
    k = min(m, n - i)
    p = np.linspace(-0.9, 0.9, k)
    ang = (p + 1) * np.pi / 4
    ev[0, i:i + k] += dd[:k] * np.cos(ang) * 0.35
    ev[1, i:i + k] += dd[:k] * np.sin(ang) * 0.35
    return wind * 0.45 + np.vstack([drone, drone]) * 0.07 + ev


def bed_belt(r, n, flavor):
    hum, _ = WORLDS["belt"]
    f0 = hum * (1.12 if flavor else 1.0)
    total = n / SR
    drone = _drone(n, r, f0, [0.8, 1.0, 0.5, 0.35, 0.25, 0.15, 0.1, 0.06, 0.04, 0.03], 800, beat=0.12, throb=0.4)
    room = _st(n, r, brown, 0.5)
    room = np.vstack([lp(room[0], 300), lp(room[1], 300)]) * 0.25
    sched = []
    # far drills: cycles on and off
    t = r.uniform(0.5, 3.0)
    while t < total - 2:
        d = r.uniform(3.0, 6.0)
        m = ns(d)
        ff = r.uniform(90, 120)
        x = saw(ff * (1 + 0.01 * lfo_noise(m, r, 2)), m, 20) * (1 + 0.5 * sine(r.uniform(9, 13), m))
        x += bp(white(m, r), 300, 1200) * (1 + 0.6 * sine(r.uniform(9, 13), m)) * 0.5
        x = lp(x, 700) * env_pts(m, [(0, 0), (0.4, 1), (d - 0.6, 1), (d, 0)])
        sched.append((t, x, r.uniform(-0.8, 0.8), 0.12))
        t += d + r.uniform(4.0, 8.0)
    # metallic creaks: stick-slip friction exciting hull modes
    for t in _times(r, total, 6.0, 0.6):
        d = r.uniform(0.6, 1.5)
        m = ns(d)
        rate = glide(m, r.uniform(20, 40), r.uniform(80, 160), "exp")
        ph = np.cumsum(rate) / SR
        imp = np.diff(np.floor(ph), prepend=0) * r.uniform(0.3, 1.0, m)
        imp = imp + white(m, r) * 0.02
        imp *= env_pts(m, [(0, 0), (0.1, 1), (d - 0.1, 0.7), (d, 0)])
        modes = [(r.uniform(150, 1800), r.uniform(0.3, 0.8), r.uniform(0.3, 1.0)) for _ in range(8)]
        x = modal(np.pad(imp, (0, ns(1.0))), modes)
        sched.append((t, x, r.uniform(-0.7, 0.7), 0.06))
    for t in _times(r, total, 9.0):
        x = metal_hit(r, r.uniform(2000, 4000), 1.2, modes=6, bright=0.6)
        sched.append((t, x, r.uniform(-0.9, 0.9), 0.03))
    ev = _events_bus(n, r, sched, rt60=1.8, mix=0.6, dark=1000)
    ticks = hp(crackle(n, r, 1.0, 0.0003), 1500) * 0.2
    return np.vstack([drone, drone]) * 0.1 + room * 0.3 + ev + np.vstack([ticks, ticks]) * 0.3


def bed_europa(r, n, flavor):
    hum, _ = WORLDS["europa"]
    f0 = hum * (1.12 if flavor else 1.0)
    total = n / SR
    swell = 0.6 + 0.4 * lfo_noise(n, r, 0.03)
    deep = (sine(f0, n) + 0.6 * sine(f0 * 1.5, n, 1.0) + 0.3 * sine(f0 * 2, n, 2.0)) * swell
    deep += lp(brown(n, r), 60) * 0.3
    shimmer = np.zeros((2, n))
    for _k in range(5):
        f = r.uniform(2800, 6000)
        a = np.clip(lfo_noise(n, r, 0.2), 0, 1) ** 3
        shimmer += pan(sine(f, n, r.uniform(0, 6.28)) * a, r.uniform(-0.9, 0.9))
    sched = []
    for t in _times(r, total, 9.0, 0.5, 1.0):  # ice groans
        d = r.uniform(2.0, 4.0)
        m = ns(d)
        a, b = (r.uniform(45, 70), r.uniform(90, 120)) if r.random() < 0.5 else (r.uniform(90, 120), r.uniform(45, 60))
        f = glide(m, a, b, "smooth") * (1 + 0.04 * lfo_noise(m, r, 8))
        x = saw(f, m, 30) * (1 + 0.5 * lfo_noise(m, r, 20))
        x = biquad(x, "bp", 300, 3) + 0.6 * biquad(x, "bp", 700, 4)
        x = lp(x, 1200) * env_pts(m, [(0, 0), (d * 0.4, 1), (d * 0.7, 0.8), (d, 0)])
        sched.append((t, x, r.uniform(-0.6, 0.6), 0.3))
    for t in _times(r, total, 4.5, 0.7):  # dispersive ice pings: the frozen-lake "pew"
        d = r.uniform(0.3, 0.6)
        m = ns(d)
        x = (sine(glide(m, r.uniform(3000, 4200), r.uniform(350, 600), "exp", 0.6), m)
             + 0.5 * sine(glide(m, r.uniform(5000, 6000), r.uniform(800, 1100), "exp", 0.5), m))
        x *= env_exp(m, d / 3, 0.001)
        sched.append((t, x, r.uniform(-0.9, 0.9), 0.09))
    for t in _times(r, total, 12.0):  # cracks
        x = Canvas(0.6).add(click(r, 0.004, 800, 9000)).add(
            metal_hit(r, r.uniform(1000, 2000), 0.08, modes=5), 0, 0.5).out()
        sched.append((t, x, r.uniform(-0.9, 0.9), 0.25))
    ev = _events_bus(n, r, sched, rt60=3.5, mix=0.7, dark=1100, bright=4500)
    return np.vstack([deep, deep]) * 0.12 + shimmer * 0.006 + ev


BEDS = {
    "earth": (bed_earth, "Gusting wind, distant 72 Hz plant hum, far clanks/hiss/crane whine."),
    "luna": (bed_luna, "Near-silent: suit fan, 48 Hz hum, gated 120 Hz buzz, hull ticks, faint squelch."),
    "mars": (bed_mars, "Thin whistling dust wind, sand grit on panels, low gusts, a dust devil pass."),
    "belt": (bed_belt, "Vacuum hull: 38 Hz structure hum, far drill cycles, stick-slip creaks, pings."),
    "europa": (bed_europa, "Deep 44 Hz resonance, ice groans, dispersive ice pings, cracks, shimmer."),
}
