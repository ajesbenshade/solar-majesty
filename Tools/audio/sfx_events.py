"""One-shot sound designs for Solar Majesty.

Every design is a function (rng, variant) -> mono array (or (2, n) stereo). Variants 0..2 change
pitch sets, timing and layer balance so repeated triggers do not sound like one sample looping.
Levels inside a design only set the balance between layers; render_sfx.py normalises loudness.

Palette: industrial but hopeful. Metal is modal (inharmonic resonator banks), robots speak in
band-limited square chirps, holograms are FM, tech/rewards are bells and chimes in major keys,
and fauna are formant-filtered saws with rough amplitude modulation so they read as organic.
"""

from __future__ import annotations

import numpy as np

from scipy import signal

from sfx_dsp import (
    SR, Canvas, biquad, bp, brown, crackle, env_exp, env_pts, fm, glide, hp, lfo_noise, lp,
    modal, ns, pink, reverb, reverb_ir, sat, saw, semis, sine, square, sweep, triangle, tt, white,
)

PLATE = [1.0, 1.59, 2.14, 2.30, 2.65, 2.92, 3.16, 3.50, 4.15, 4.60, 5.20, 5.93]
COIN = [1.0, 1.52, 2.33, 2.90, 3.61, 4.24]
STONE = [1.0, 1.83, 2.47, 3.3]


# --------------------------------------------------------------------------------------------
# Building blocks
# --------------------------------------------------------------------------------------------

def click(r, dur=0.004, lo=1500.0, hi=9000.0):
    n = ns(dur * 4)
    x = white(n, r) * env_exp(n, dur / 3, 0.00008)
    return bp(x, lo, hi)


def thump(r, f0, f1, dur, decay, drop=0.02, noise=0.25):
    """Pitch-dropping sine body with a low noise knock: the weight under an impact."""
    n = ns(dur)
    t = tt(n)
    f = f1 + (f0 - f1) * np.exp(-t / drop)
    body = sine(f, n) * env_exp(n, decay, 0.0008)
    knock = lp(white(n, r) * env_exp(n, 0.004, 0.0002), 1800) * noise
    return body + knock


def metal_hit(r, base, decay, modes=8, bright=0.8, ratios=PLATE, jitter=0.025):
    """Inharmonic modal ring excited by an impulse plus a short noise burst."""
    n = ns(min(3.0, decay * 6 + 0.02))
    exc = np.zeros(n)
    exc[0] = 1.0
    burst = ns(0.0015)
    exc[:burst] += white(burst, r) * 0.4 * np.linspace(1, 0, burst)
    exc = lp(exc, 2500 + 12000 * bright, 1)
    ms = []
    for k, ratio in enumerate(ratios[:modes]):
        f = base * ratio * (1 + r.uniform(-jitter, jitter))
        d = decay * (1.0 / ratio) ** 0.7 * r.uniform(0.8, 1.2)
        g = (1.0 / ratio ** 0.45) * r.uniform(0.5, 1.0) * (1.0 if k < 2 else bright + 0.2)
        ms.append((f, d, g))
    return modal(exc, ms)


def chime(r, f, dur, decay=None, bright=0.35):
    n = ns(dur)
    decay = decay or dur / 4
    x = (sine(f, n) + bright * sine(2.0 * f, n) * env_exp(n, decay * 0.5)
         + bright * 0.4 * sine(3.01 * f, n) * env_exp(n, decay * 0.3)
         + bright * 0.15 * sine(4.13 * f, n) * env_exp(n, decay * 0.2))
    return x * env_exp(n, decay, 0.002)


def bell(r, f, dur, index=2.2, ratio=1.41, decay=None):
    """FM bell: inharmonic sidebands that collapse toward a pure tone as the index decays."""
    n = ns(dur)
    decay = decay or dur / 4
    idx = index * env_exp(n, decay * 0.6) + 0.15
    x = fm(f, n, ratio, idx) + 0.22 * sine(2.76 * f, n) * env_exp(n, decay * 0.35)
    return x * env_exp(n, decay, 0.0015)


def servo(r, f0, f1, dur, grit=1.0, curve="smooth"):
    """Small actuator: gear-rippled saw with a pitch glide and a little bearing hiss."""
    n = ns(dur)
    f = glide(n, f0, f1, curve)
    w = saw(f, n, 18) * 0.6 + sine(f * 2.0, n) * 0.25
    w *= 1 + 0.3 * sine(f / 7.3, n)
    w = bp(w, 250, 6500)
    w += bp(white(n, r), 2500, 8000) * 0.12 * grit
    a = min(0.012, dur / 4)
    return w * env_pts(n, [(0, 0), (a, 1), (max(a, dur - 0.03), 0.85), (dur, 0)])


def robo_chirp(r, f0, f1, dur, harm=11, vib=0.012, shape=0.6):
    """Friendly robot syllable: a soft square with a quick glide and a touch of vibrato."""
    n = ns(dur)
    f = glide(n, f0, f1, "exp", shape) * (1 + vib * sine(26.0, n))
    w = square(f, n, harm) * 0.7 + sine(f, n) * 0.4
    w = lp(w, 5200, 2)
    a = min(0.004, dur / 5)
    return w * env_pts(n, [(0, 0), (a, 1), (dur * 0.6, 0.8), (dur, 0)])


def hiss(r, dur, lo, hi, attack=0.005, decay=None):
    n = ns(dur)
    decay = decay or dur / 3
    return bp(white(n, r), lo, hi) * env_exp(n, decay, attack)


def sparkle(r, dur, density=600.0, lo=4500.0):
    n = ns(dur)
    x = crackle(n, r, density * np.linspace(1, 0.15, n), 0.0003)
    return hp(x, lo, 2) * env_exp(n, dur / 2.5, 0.01)


def formant(src, formants):
    out = np.zeros(len(src))
    for f, q, g in formants:
        out += g * biquad(src, "bp", f, q)
    return out


def rough(n, r, rate, depth):
    """Rough amplitude modulation, the cue that turns a tone into a throat."""
    return 1 + depth * (0.6 * sine(rate * (1 + 0.1 * lfo_noise(n, r, 6)), n) + 0.4 * lfo_noise(n, r, rate * 1.7))


def stereo_reverb(x, r, rt60, mix, **kw):
    """Mono dry in the centre, decorrelated stereo tail: width without a stereo source."""
    ir = reverb_ir(r, rt60, stereo=True, **kw)
    wet = np.vstack([signal.fftconvolve(x, ir[0]), signal.fftconvolve(x, ir[1])])
    out = np.zeros_like(wet)
    out[:, : len(x)] += x * 0.7071
    return out + wet * mix


def pv(v, spread=(0.0, -2.0, 2.0)):
    return semis(spread[v])


# --------------------------------------------------------------------------------------------
# Command and UI
# --------------------------------------------------------------------------------------------

def ev_ui_hover(r, v):
    p = pv(v, (0, 1, -1))
    n = ns(0.075)
    f = glide(n, 2250 * p, 2550 * p, "exp", 0.5)
    x = sine(f, n) * env_pts(n, [(0, 0), (0.007, 1), (0.02, 0.45), (0.075, 0)])
    x += lp(white(n, r), 5000) * 0.03 * env_exp(n, 0.015, 0.004)
    return x


def ev_ui_click(r, v):
    p = pv(v, (0, 0.7, -0.7))
    c = Canvas(0.08)
    n = ns(0.05)
    c.add(sine(3100 * p, n) * env_exp(n, 0.006, 0.0003), 0, 0.6)
    c.add(biquad(white(n, r) * env_exp(n, 0.0025, 0.0001), "bp", 1900 * p, 1.6), 0, 0.9)
    c.add(sine(620 * p, n) * env_exp(n, 0.009, 0.0005), 0, 0.35)
    if v == 2:  # a softer double-click latch
        c.add(biquad(white(n, r) * env_exp(n, 0.002, 0.0001), "bp", 2600, 2.0), 0.018, 0.35)
    return c.out()


def ev_retry(r, v):
    """UI confirm: a tidy two-note 'acknowledged'."""
    notes = [(1318.5, 1975.5), (1174.7, 1760.0), (1396.9, 2093.0)][v]
    c = Canvas(0.6)
    c.add(ev_ui_click(r, v), 0, 0.5)
    for i, f in enumerate(notes):
        n = ns(0.3)
        tone = sine(f, n) + 0.25 * fm(f, n, 2.0, 0.8 * env_exp(n, 0.04)) + 0.15 * sine(2 * f, n)
        c.add(tone * env_exp(n, 0.09, 0.003), 0.012 + i * 0.075, 0.55 if i == 0 else 0.5)
    return reverb(c.out(), r, rt60=0.35, mix=0.14, bright=9000)


def ev_flag_post(r, v):
    """Holographic bounty beacon: spike into regolith, FM field spin-up, two-note beacon."""
    p = pv(v) * r.uniform(0.99, 1.01)
    c = Canvas(1.2)
    c.add(thump(r, 115 * p, 46, 0.14, 0.05), 0, 0.9)
    c.add(lp(hiss(r, 0.09, 180, 2600, 0.001, 0.02), 3000), 0, 0.5)
    c.add(metal_hit(r, 1650 * p, 0.05, modes=6, bright=0.7), 0.002, 0.18)
    n = ns(0.3)
    f = glide(n, 520 * p, 1180 * p, "exp", 0.7)
    riser = fm(f, n, 2.005, env_pts(n, [(0, 2.6), (0.3, 0.4)]))
    riser *= env_pts(n, [(0, 0), (0.05, 0.45), (0.25, 0.7), (0.3, 0)])
    c.add(lp(riser, 6500), 0.03, 0.3)
    a, b = [(0, 7), (0, 5), (0, 4)][v]
    c.add(chime(r, 1046.5 * p * semis(a), 0.6, bright=0.3), 0.2, 0.42)
    c.add(chime(r, 1046.5 * p * semis(b), 0.75, bright=0.3), 0.28 + 0.01 * v, 0.38)
    c.add(sparkle(r, 0.4, 700), 0.21, 0.06)
    return reverb(c.out(), r, rt60=0.6, mix=0.22, bright=8500)


def ev_build_place(r, v):
    """Blueprint drop: scan sweep, three grid-lock blips, magnetic clamp."""
    p = pv(v, (0, -1, 1))
    c = Canvas(0.7)
    n = ns(0.12)
    scan = sweep(white(n, r), "bp", glide(n, 700, 5200 * p, "exp"), q=3.0)
    c.add(scan * env_pts(n, [(0, 0), (0.02, 1), (0.1, 0.6), (0.12, 0)]), 0, 0.35)
    steps = [(0, 5, 9), (0, 4, 7), (0, 7, 12)][v]
    gap = [0.04, 0.035, 0.045][v]
    for i, s in enumerate(steps):
        m = ns(0.05)
        f = 1318.5 * p * semis(s)
        c.add((sine(f, m) + 0.3 * square(f, m, 5)) * env_exp(m, 0.014, 0.001), 0.06 + i * gap, 0.3)
    t_clamp = 0.06 + 3 * gap + 0.01
    c.add(thump(r, 78 * p, 44, 0.14, 0.05), t_clamp, 0.9)
    c.add(metal_hit(r, 420 * p, 0.08, modes=6, bright=0.5), t_clamp, 0.3)
    m = ns(0.3)
    hum = (sine(220 * p, m) + 0.5 * sine(440 * p, m)) * (1 + 0.5 * sine(30, m)) * env_exp(m, 0.08, 0.005)
    c.add(lp(hum, 1500), t_clamp, 0.12)
    return reverb(c.out(), r, rt60=0.4, mix=0.15)


# --------------------------------------------------------------------------------------------
# Robots
# --------------------------------------------------------------------------------------------

def ev_claim(r, v):
    """A robot accepts a bounty: servo nod plus a two-syllable 'on it'."""
    a, b = [(0, 4), (0, 7), (2, 9)][v]
    base = 880.0 * pv(v, (0, -1, 1)) * r.uniform(0.99, 1.01)
    c = Canvas(0.45)
    c.add(click(r), 0, 0.35)
    c.add(servo(r, 900, 1400, 0.07, grit=0.5), 0.0, 0.16)
    c.add(robo_chirp(r, base * semis(a) * 0.9, base * semis(a), 0.07), 0.02, 0.5)
    c.add(robo_chirp(r, base * semis(b) * 0.97, base * semis(b) * 1.01, 0.12), 0.1 + 0.01 * v, 0.55)
    return reverb(c.out(), r, rt60=0.3, mix=0.12)


def ev_robot_spawn(r, v):
    """Fabricator: whirr up, assembly clicks, power-on thump, boot chirps."""
    p = pv(v, (0, -2, 2))
    c = Canvas(1.0)
    c.add(servo(r, 280 * p, 1250 * p, 0.36, grit=0.8), 0, 0.28)
    for t in np.sort(r.uniform(0.05, 0.3, 4)):
        c.add(metal_hit(r, r.uniform(900, 2000), 0.03, modes=5), t, 0.25)
    c.add(thump(r, 110, 60, 0.12, 0.04), 0.36, 0.7)
    notes = [(0, 7, 12), (0, 5, 9), (0, 4, 7)][v]
    for i, s in enumerate(notes):
        f = 660.0 * p * semis(s)
        c.add(robo_chirp(r, f * 0.94, f, 0.055), 0.42 + i * 0.06, 0.45)
    return reverb(c.out(), r, rt60=0.4, mix=0.14)


def ev_party_form(r, v):
    """Three robots answer one another, then a short rally horn."""
    p = pv(v, (0, -1, 2))
    c = Canvas(1.0)
    calls = [((700, 900), (950, 1150), (1200, 1500)),
             ((820, 700), (900, 1100), (1150, 1450)),
             ((650, 820), (980, 880), (1100, 1480))][v]
    for i, (f0, f1) in enumerate(calls):
        c.add(robo_chirp(r, f0 * p, f1 * p, 0.09 + 0.02 * i), i * 0.12, 0.5)
    n = ns(0.45)
    horn = sum(saw(f * p * (1 + d), n, 20) for f in (392.0, 587.3) for d in (-0.004, 0.004))
    horn = sweep(horn, "lp", glide(n, 700, 2200, "exp"), q=0.8)
    horn *= env_pts(n, [(0, 0), (0.03, 1), (0.3, 0.8), (0.45, 0)])
    c.add(horn, 0.36, 0.14)
    return reverb(c.out(), r, rt60=0.5, mix=0.18)


def ev_level_up(r, v):
    """Hero level: power sweep, rising arpeggio, bright bell cap."""
    p = pv(v, (0, 2, -2))
    c = Canvas(1.4)
    n = ns(0.32)
    sw = saw(glide(n, 200 * p, 1600 * p, "exp"), n, 24)
    sw = sweep(sw, "lp", glide(n, 400, 6500, "exp"), q=1.2) * env_pts(n, [(0, 0), (0.25, 1), (0.32, 0)])
    c.add(sw, 0, 0.18)
    c.add(servo(r, 700, 1250, 0.15, grit=0.4), 0, 0.08)
    pattern = [(0, 5, 9, 12), (0, 4, 7, 12), (0, 7, 9, 14)][v]
    for i, s in enumerate(pattern):
        f = 783.99 * p * semis(s)
        c.add(robo_chirp(r, f * 0.97, f, 0.11, harm=7, vib=0.0), 0.18 + i * 0.075, 0.32)
    t_cap = 0.18 + 4 * 0.075
    c.add(bell(r, 1567.98 * p * semis(pattern[-1] - 12), 0.8, index=1.4), t_cap, 0.35)
    c.add(bell(r, 2093.0 * p * semis(pattern[-1] - 12), 0.8, index=1.2), t_cap + 0.012, 0.25)
    c.add(sparkle(r, 0.7, 500), t_cap, 0.07)
    return reverb(c.out(), r, rt60=0.9, mix=0.25, bright=9000)


def ev_robot_hit(r, v):
    """Metal impact on a mech chassis: transient, modal ring, weight, loose-part rattle."""
    p = pv(v, (0, -1.5, 1.5)) * r.uniform(0.98, 1.02)
    c = Canvas(0.6)
    c.add(click(r, 0.003, 2000, 10000), 0, 0.6)
    c.add(metal_hit(r, 520 * p, 0.16, modes=10, bright=0.9), 0, 0.9)
    c.add(thump(r, 125 * p, 60, 0.18, 0.06), 0, 0.9)
    for t in np.sort(r.uniform(0.025, 0.09, 3)):
        c.add(metal_hit(r, r.uniform(1800, 3000), 0.02, modes=4), t, 0.18)
    x = sat(c.out() * 0.8, 2.2)
    return reverb(x, r, rt60=0.35, mix=0.12)


def ev_robot_down(r, v):
    """Robot incapacitated: descending power-down, sparks, heavy collapse clatter, fizz."""
    p = pv(v, (0, -2, 1.5))
    c = Canvas(1.8)
    n = ns(0.95)
    f = glide(n, 1300 * p, 70 * p, "exp", 0.8)
    wh = saw(f, n, 20) * 0.5 + sine(f, n)
    wh = sweep(wh, "lp", glide(n, 6000, 400, "exp"), q=1.0) * env_pts(n, [(0, 0), (0.02, 1), (0.8, 0.6), (0.95, 0)])
    c.add(wh, 0, 0.3)
    m = ns(0.55)
    sp = hp(crackle(m, r, 900 * (0.5 + lfo_noise(m, r, 9, 0, 1.2)), 0.0003), 2500) * env_exp(m, 0.25)
    c.add(sp, 0.02, 0.25)
    t_fall = [0.55, 0.6, 0.5][v]
    c.add(metal_hit(r, 210 * p, 0.22, modes=10, bright=0.7), t_fall, 0.8)
    c.add(thump(r, 92, 45, 0.35, 0.1), t_fall, 0.9)
    c.add(metal_hit(r, 430 * p, 0.1, modes=8), t_fall + 0.16, 0.45)
    for t in np.sort(r.uniform(t_fall + 0.2, t_fall + 0.4, 3)):
        c.add(metal_hit(r, r.uniform(1400, 2600), 0.02, modes=4), t, 0.15)
    c.add(hiss(r, 0.45, 3000, 8000, 0.01, 0.15), t_fall, 0.08)
    return reverb(sat(c.out() * 0.8, 1.6), r, rt60=0.7, mix=0.18)


def ev_construction_tick(r, v):
    """Rivet / weld tick, designed to be retriggered while a module is being built."""
    p = pv(v, (0, 1, -1)) * r.uniform(0.97, 1.03)
    c = Canvas(0.22)
    hits = [(0.0,), (0.0,), (0.0, 0.045)][v]
    for t in hits:
        c.add(metal_hit(r, 1400 * p, 0.03, modes=6, bright=1.0), t, 0.7)
        c.add(thump(r, 320, 150, 0.04, 0.01, noise=0.1), t, 0.35)
    m = ns([0.08, 0.16, 0.06][v])
    siz = hp(crackle(m, r, 3000, 0.0003), 3000) + bp(white(m, r), 4000, 9000) * 0.3
    c.add(siz * env_exp(m, m / SR / 2.5, 0.002), 0.01, [0.25, 0.45, 0.2][v])
    return c.out()


def ev_repair(r, v):
    """Repair finished: ratchet run, weld hiss, small positive chime."""
    p = pv(v, (0, -1, 1))
    c = Canvas(0.9)
    t = 0.0
    gap = 0.035
    for k in range(7):
        c.add(metal_hit(r, 2500 * p * r.uniform(0.97, 1.03), 0.012, modes=4), t, 0.4)
        c.add(click(r, 0.002), t, 0.2)
        t += gap
        gap *= 1.08
    m = ns(0.25)
    c.add((hp(crackle(m, r, 2000), 3000) + bp(white(m, r), 3000, 9000) * 0.3) * env_exp(m, 0.08, 0.01), 0.05, 0.2)
    base = [(1318.5, 1760.0), (1174.7, 1568.0), (1396.9, 1864.7)][v]
    c.add(chime(r, base[0], 0.45), t + 0.02, 0.3)
    c.add(chime(r, base[1], 0.55), t + 0.08, 0.28)
    return reverb(c.out(), r, rt60=0.4, mix=0.15)


def ev_heal(r, v):
    """Aid station / medic patch: soft rising shimmer and nano-hiss."""
    p = pv(v, (0, 2, -1))
    c = Canvas(1.2)
    for i, s in enumerate([0, 4, 7, 12]):
        f = 1046.5 * p * semis(s)
        n = ns(0.7)
        tone = sine(f * (1 + 0.004 * sine(6.0, n)), n)
        tone *= env_pts(n, [(0, 0), (0.06 + 0.03 * i, 1), (0.7, 0)]) ** 1.5
        c.add(tone, i * 0.05, 0.22)
    n = ns(0.35)
    c.add(sine(glide(n, 520 * p, 780 * p, "smooth"), n) * env_pts(n, [(0, 0), (0.1, 1), (0.35, 0)]), 0, 0.14)
    m = ns(0.5)
    c.add(bp(white(m, r), 5000, 10000) * env_pts(m, [(0, 0), (0.1, 1), (0.5, 0)]), 0, 0.05)
    return reverb(c.out(), r, rt60=0.9, mix=0.3, bright=9000)


def ev_equip(r, v):
    """Shop purchase equipped: credit blip, servo tighten, latch clunk, lock click."""
    p = pv(v, (0, -1.5, 1.5))
    c = Canvas(0.6)
    n = ns(0.06)
    c.add(sine(1568 * p, n) * env_exp(n, 0.02, 0.002), 0, 0.2)
    c.add(servo(r, 800 * p, 1100 * p, 0.12, grit=0.6), 0.02, 0.12)
    c.add(metal_hit(r, 700 * p, 0.06, modes=8), 0.07, 0.7)
    c.add(thump(r, 180, 90, 0.08, 0.03), 0.07, 0.6)
    c.add(metal_hit(r, 1500 * p, 0.03, modes=5), 0.15 + 0.01 * v, 0.35)
    return reverb(c.out(), r, rt60=0.3, mix=0.12)


def ev_credits(r, v):
    """Credits paid out: a small cascade of coin chinks plus a register double-blip."""
    c = Canvas(0.7)
    k = 5 + v
    times = np.cumsum(np.r_[0.0, r.uniform(0.028, 0.055, k - 1)])
    for i, t in enumerate(times):
        c.add(metal_hit(r, r.uniform(3000, 4200), r.uniform(0.07, 0.13), modes=6, ratios=COIN, bright=0.9),
              t, 0.55 * (0.85 ** i))
    notes = [(1568.0, 2093.0), (1760.0, 2349.3), (1396.9, 1864.7)][v]
    for i, f in enumerate(notes):
        n = ns(0.08)
        c.add(lp(square(f, n, 7), 6000) * env_exp(n, 0.03, 0.002), times[-1] + 0.04 + i * 0.06, 0.22)
    return reverb(c.out(), r, rt60=0.4, mix=0.15, bright=9000)


def ev_extract(r, v):
    """Ore extracted/hauled: rock crunch, pebbles rattling into a hopper, canister seal, blip."""
    p = pv(v, (0, -2, 2))
    c = Canvas(0.9)
    n = ns(0.14)
    crunch = bp(crackle(n, r, 2200, 0.0006), 700, 3500) * env_exp(n, 0.045, 0.002)
    c.add(crunch, 0, 0.9)
    c.add(thump(r, 140, 70, 0.1, 0.04), 0, 0.5)
    t = 0.07
    k = 0
    while t < 0.42 and k < 18:
        c.add(metal_hit(r, r.uniform(1800, 4500), 0.018, modes=4, ratios=STONE, bright=0.6), t, 0.4 * (1 - t))
        t += r.exponential(0.018) + 0.006
        k += 1
    c.add(thump(r, 150 * p, 80, 0.12, 0.04), 0.42, 0.7)
    c.add(hiss(r, 0.16, 3000, 9000, 0.004, 0.05), 0.43, 0.16)
    m = ns(0.07)
    c.add(sine(glide(m, 700 * p, 1400 * p, "exp"), m) * env_exp(m, 0.03, 0.003), 0.47, 0.22)
    c.add(chime(r, 1760 * p, 0.3), 0.5, 0.16)
    return reverb(c.out(), r, rt60=0.35, mix=0.14)


# --------------------------------------------------------------------------------------------
# Construction and progress
# --------------------------------------------------------------------------------------------

def ev_build_complete(r, v):
    """Module finished: heavy industrial clunk, latch, hydraulic release, bright bell arpeggio."""
    p = pv(v, (0, -1, 1))
    c = Canvas(2.2)
    c.add(metal_hit(r, 118 * p, 0.35, modes=12, bright=0.6), 0, 1.0)
    c.add(thump(r, 72 * p, 38, 0.5, 0.15), 0, 1.1)
    c.add(click(r, 0.004, 1200, 7000), 0, 0.4)
    c.add(metal_hit(r, 262 * p, 0.12, modes=8), 0.09, 0.5)
    c.add(servo(r, 600, 920, 0.18, grit=0.6), 0.05, 0.1)
    c.add(hiss(r, 0.5, 2500, 8000, 0.01, 0.14), 0.12, 0.22)
    c.add(lp(hiss(r, 0.2, 300, 1500, 0.005, 0.05), 1500), 0.12, 0.18)
    chords = [(0, 4, 7, 12), (0, 2, 7, 12), (0, 4, 11, 16)][v]
    for i, s in enumerate(chords):
        c.add(bell(r, 659.25 * semis(s), 1.2, index=1.8), 0.3 + i * 0.07, 0.3 if i < 3 else 0.22)
    c.add(sparkle(r, 0.6, 400), 0.5, 0.05)
    return reverb(sat(c.out() * 0.7, 1.3), r, rt60=1.1, mix=0.25)


def ev_research(r, v):
    """Tech complete: rising pentatonic bell shimmer resolving into a bright held chord."""
    p = pv(v, (0, -2, 2))
    c = Canvas(2.6)
    n = ns(0.35)
    c.add(sweep(white(n, r), "bp", glide(n, 1000, 8000, "exp"), q=2.0) * env_pts(n, [(0, 0), (0.3, 1), (0.35, 0)]),
          0, 0.08)
    scale = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24]
    for i, s in enumerate(scale[: 8 + v]):
        c.add(bell(r, 587.33 * p * semis(s), 1.0, index=0.9, ratio=2.0), 0.02 + i * 0.045, 0.22)
    t_chord = 0.05 + 8 * 0.045
    for s in [(12, 16, 19), (12, 14, 19), (12, 16, 21)][v]:
        m = ns(1.6)
        f = 587.33 * p * semis(s)
        tone = (sine(f * (1 + 0.003 * sine(5.0, m)), m) + 0.2 * sine(2 * f, m))
        c.add(tone * env_pts(m, [(0, 0), (0.25, 1), (1.6, 0)]) ** 1.3, t_chord, 0.2)
    c.add(sparkle(r, 1.5, 450), t_chord - 0.1, 0.09)
    return reverb(c.out(), r, rt60=1.8, mix=0.35, bright=9500)


def ev_launch_ready(r, v):
    """Departure craft staged: power-up whine, clamp release, 'systems go' chime."""
    p = pv(v, (0, -1, 2))
    c = Canvas(1.6)
    n = ns(0.62)
    f = glide(n, 150 * p, 900 * p, "smooth")
    wh = sine(f, n) + 0.4 * saw(f, n, 12)
    c.add(lp(wh, 3000) * env_pts(n, [(0, 0), (0.5, 1), (0.62, 0)]), 0, 0.18)
    c.add(metal_hit(r, 180 * p, 0.2, modes=10, bright=0.5), 0.52, 0.6)
    c.add(thump(r, 85, 45, 0.3, 0.1), 0.52, 0.7)
    c.add(hiss(r, 0.45, 2000, 7000, 0.01, 0.12), 0.55, 0.22)
    a, b = [(880.0, 1318.5), (784.0, 1174.7), (987.8, 1480.0)][v]
    c.add(bell(r, a, 0.9, index=1.2), 0.66, 0.34)
    c.add(bell(r, b, 1.0, index=1.0), 0.82, 0.32)
    return reverb(c.out(), r, rt60=1.0, mix=0.25)


# --------------------------------------------------------------------------------------------
# Fauna
# --------------------------------------------------------------------------------------------

def _growl(r, dur, f0, formants, rough_rate=33.0, depth=0.6):
    n = ns(dur)
    f = f0 * (1 + 0.06 * lfo_noise(n, r, 7)) * glide(n, 1.0, 0.9, "lin")
    src = saw(f, n, 40) + 0.3 * white(n, r) * 0.2
    x = formant(src, formants) * rough(n, r, rough_rate, depth)
    x += bp(white(n, r), 400, 3000) * 0.12 * (0.5 + 0.5 * lfo_noise(n, r, 12, 0, 1))
    return x * env_pts(n, [(0, 0), (0.06, 1), (dur * 0.7, 0.85), (dur, 0)])


def ev_bite(r, v):
    """Fauna bite on a robot: snap, crunch, low body, and the metal plate it bit."""
    p = pv(v, (0, -1.5, 1.5)) * r.uniform(0.98, 1.02)
    c = Canvas(0.4)
    c.add(click(r, 0.003, 2500, 10000), 0, 0.7)
    n = ns(0.07)
    c.add(bp(crackle(n, r, 2600, 0.0005), 1200, 5000) * env_exp(n, 0.025, 0.001), 0.002, 1.0)
    c.add(thump(r, 165 * p, 70, 0.09, 0.03), 0, 0.7)
    c.add(metal_hit(r, 2300 * p, 0.07, modes=6, bright=0.9), 0.004, 0.3)
    c.add(_growl(r, 0.12, 95 * p, [(600, 4, 1.0), (1200, 5, 0.6)], 40, 0.7), 0.0, [0.25, 0.15, 0.3][v])
    return reverb(sat(c.out() * 0.8, 2.5), r, rt60=0.25, mix=0.1)


def ev_stalker_growl(r, v):
    """Enemy aggro: v0 throat growl, v1 rising screech, v2 chittering snarl."""
    p = r.uniform(0.96, 1.04)
    c = Canvas(1.0)
    if v == 0:
        c.add(_growl(r, 0.7, 78 * p, [(650, 4, 1.0), (1100, 5, 0.7), (2500, 6, 0.35)]), 0, 1.0)
    elif v == 1:
        n = ns(0.5)
        f = np.interp(tt(n), [0, 0.2, 0.5], [700 * p, 1600 * p, 900 * p])
        scr = fm(f, n, 1.48, 2.5 * env_exp(n, 0.3)) * rough(n, r, 45, 0.5)
        c.add(bp(scr, 600, 6000) * env_pts(n, [(0, 0), (0.05, 1), (0.35, 0.8), (0.5, 0)]), 0.08, 0.6)
        c.add(_growl(r, 0.35, 90 * p, [(700, 4, 1.0), (1300, 5, 0.6)]), 0, 0.6)
    else:
        n = ns(0.45)
        ch = hp(crackle(n, r, 1800 * (0.5 + 0.5 * sine(18, n)) + 100, 0.0004), 1500)
        c.add(ch * env_pts(n, [(0, 0), (0.03, 1), (0.45, 0)]), 0, 0.5)
        c.add(_growl(r, 0.5, 105 * p, [(800, 5, 1.0), (1500, 6, 0.6), (2800, 7, 0.3)], 28, 0.7), 0.1, 0.9)
    return reverb(sat(c.out(), 1.8), r, rt60=0.5, mix=0.15)


def ev_stalker_death(r, v):
    """Stalker dies: descending screech, gurgle, body collapse, dust puff, debris."""
    p = pv(v, (0, -2, 2))
    c = Canvas(1.4)
    n = ns(0.55)
    f = glide(n, 1100 * p, 180 * p, "exp", 0.7) * (1 + 0.03 * lfo_noise(n, r, 20))
    scr = fm(f, n, 1.5, 3.0 * env_exp(n, 0.25)) * rough(n, r, 47, 0.5)
    c.add(bp(scr, 300, 5000) * env_pts(n, [(0, 0), (0.02, 1), (0.4, 0.5), (0.55, 0)]), 0, 0.55)
    c.add(_growl(r, 0.45, 120 * p, [(600, 5, 1.0), (1200, 6, 0.6), (2500, 8, 0.3)], 40, 0.8), 0.1, 0.45)
    t_fall = [0.35, 0.4, 0.3][v]
    c.add(thump(r, 92, 40, 0.35, 0.12), t_fall, 1.0)
    m = ns(0.6)
    c.add(bp(pink(m, r), 300, 2000) * env_exp(m, 0.2, 0.02), t_fall, 0.35)
    c.add(bp(crackle(m, r, 300 * np.linspace(1, 0.1, m), 0.0005), 1000, 5000) * env_exp(m, 0.2), t_fall + 0.03, 0.4)
    return reverb(c.out(), r, rt60=0.9, mix=0.2)


# --------------------------------------------------------------------------------------------
# Alerts and outcomes (loudest tier)
# --------------------------------------------------------------------------------------------

def _klaxon_tone(r, f, dur, bend=0.0, harm=9, lp_f=4200):
    n = ns(dur)
    fr = f * glide(n, 1.0, semis(bend), "lin")
    x = triangle(fr, n, harm) * 0.8 + 0.35 * sine(fr * 2, n) + 0.25 * fm(fr, n, 1.0, 0.8)
    x = lp(x, lp_f, 2)
    return x * env_pts(n, [(0, 0), (0.004, 1), (dur * 0.55, 0.75), (dur, 0)])


def ev_alert_warning(r, v):
    """Attention ping: two rounded tones falling a fourth. Firm, not shrill."""
    a, b = [(880.0, 659.25), (784.0, 587.33), (932.3, 698.46)][v]
    c = Canvas(0.6)
    c.add(_klaxon_tone(r, a, 0.12), 0, 0.8)
    c.add(_klaxon_tone(r, b, 0.17), 0.14, 0.8)
    return reverb(c.out(), r, rt60=0.5, mix=0.2)


def ev_alert_critical(r, v):
    """Colony in danger: three lower, bent klaxon pulses with a sub pulse underneath."""
    base = [523.25, 493.88, 554.37][v]
    c = Canvas(1.1)
    for i in range(3):
        f = base * (semis(-3) if i == 2 else 1.0)
        n = ns(0.17)
        pulse = square(f * glide(n, 1.0, semis(-0.7), "lin"), n, 11)
        pulse = lp(pulse, 2600, 2) * env_pts(n, [(0, 0), (0.005, 1), (0.12, 0.8), (0.17, 0)])
        sub = sine(f / 4, n) * env_pts(n, [(0, 0), (0.01, 1), (0.17, 0)])
        c.add(pulse, i * 0.24, 0.7)
        c.add(sub, i * 0.24, 0.5)
    return reverb(sat(c.out(), 1.5), r, rt60=0.6, mix=0.18)


def ev_fail(r, v):
    """Mission lost: power-down whine, electrical crackle, sinking minor brass, sub thud."""
    p = pv(v, (0, -1, 1))
    c = Canvas(2.6)
    n = ns(1.15)
    f = glide(n, 900 * p, 55 * p, "exp", 0.7)
    wh = sweep(sine(f, n) + 0.4 * saw(f, n, 16), "lp", glide(n, 5000, 300, "exp"), q=0.9)
    c.add(wh * env_pts(n, [(0, 0), (0.02, 1), (1.15, 0)]), 0, 0.2)
    m = ns(0.35)
    c.add(hp(crackle(m, r, 600, 0.0003), 3000) * env_exp(m, 0.12), 0, 0.2)
    c.add(thump(r, 60, 32, 0.8, 0.3), 0, 0.9)
    for i, (fb, t0) in enumerate([(98.0, 0.05), (77.78, 0.5)]):
        k = ns(1.1)
        brass = sum(saw(fb * p * (1 + d), k, 30) for d in (-0.005, 0.0, 0.006))
        brass = sweep(brass, "lp", np.interp(tt(k), [0, 0.08, 1.1], [300, 1100, 350]), q=0.9)
        c.add(brass * env_pts(k, [(0, 0), (0.06, 1), (0.8, 0.6), (1.1, 0)]), t0, 0.28)
    return stereo_reverb(c.out(), r, rt60=1.6, mix=0.3, dark=1200)


def ev_victory(r, v):
    """Victory stinger: sub hit, bright major pad swell, bell arpeggio, cymbal shimmer."""
    key = [semis(0), semis(2), semis(-2)][v]
    c = Canvas(3.2)
    c.add(thump(r, 85, 40, 0.9, 0.35), 0, 0.9)
    n = ns(2.8)
    pad = np.zeros(n)
    for f in (261.63, 329.63, 392.0, 523.25, 659.25):
        for d in (-0.004, 0.0, 0.0045):
            pad += saw(f * key * (1 + d), n, 24)
    pad = sweep(pad, "lp", np.interp(tt(n), [0, 0.2, 2.8], [700, 4200, 1500]), q=0.8)
    c.add(pad * env_pts(n, [(0, 0), (0.12, 1), (1.2, 0.7), (2.8, 0)]) / 8, 0, 0.9)
    for i, s in enumerate([0, 4, 7, 12, 16, 19, 24][: 6 + (v == 2)]):
        c.add(bell(r, 523.25 * key * semis(s), 1.3, index=1.6), 0.04 + i * 0.045, 0.28)
    c.add(metal_hit(r, 3100, 1.1, modes=10, bright=1.0, jitter=0.08), 0.0, 0.1)
    m = ns(1.6)
    c.add(hp(white(m, r), 6000) * env_exp(m, 0.45, 0.01), 0.0, 0.1)
    return stereo_reverb(c.out(), r, rt60=2.2, mix=0.3, bright=9000)


def ev_launch(r, v):
    """Rocket launch from the pad: ignition crack, rising roar, crackle, sub throb, ascent."""
    p = [1.0, 0.95, 1.06][v]
    dur = 6.5
    n = ns(dur)
    t = tt(n)
    amp = np.interp(t, [0, 0.25, 1.4, 3.2, 6.5], [0, 0.75, 1.0, 0.8, 0])
    far = np.interp(t, [0, 3.0, 6.5], [7000, 5000, 700])
    out = np.zeros((2, n))
    for ch in range(2):
        cr = np.random.default_rng(r.integers(1 << 31))
        centre = np.interp(t, [0, 3.0, 6.5], [180 * p, 700 * p, 400 * p])
        roar = sweep(pink(n, cr), "bp", centre, q=0.7) * 1.4 + lp(brown(n, cr), 140) * 0.7
        dens = np.interp(t, [0, 1.0, 2.5, 4.0, 6.5], [30, 250, 800, 500, 60])
        crk = sat(hp(crackle(n, cr, dens, 0.0004), 700) * 2.0, 1.5)
        mix = roar * 0.8 + crk * 0.35 * np.interp(t, [0, 1.2, 3.5, 6.5], [0.2, 1, 0.8, 0.2])
        out[ch] = sweep(mix, "lp", far, q=0.7) * amp
    sub = (sine(32 * p, n) + 0.6 * sine(46 * p, n)) * (0.7 + 0.3 * lfo_noise(n, r, 3)) * amp * 0.5
    out += sub
    ign = np.zeros(n)
    k = ns(1.2)
    ign[:k] = thump(r, 70, 30, 1.2, 0.4) + lp(white(k, r) * env_exp(k, 0.1, 0.001), 3000) * 0.8
    out += ign * 0.9
    ir = reverb_ir(r, 2.5, stereo=True, dark=900, bright=4000)
    wet = np.vstack([signal.fftconvolve(out[0], ir[0]), signal.fftconvolve(out[1], ir[1])])
    full = np.zeros_like(wet)
    full[:, :n] = out
    return full + wet * 0.3


# --------------------------------------------------------------------------------------------
# Registry: name -> (fn, target momentary-max LUFS, tier, format, description)
# Format: PCM16 WAV for short one-shots (instant, exact edges); Vorbis for anything with a long
# tail (> ~1.5 s) or a stereo field, which is where the byte budget goes.
# --------------------------------------------------------------------------------------------

TIER_ORDER = ["ui", "feedback", "milestone", "alert", "fanfare"]

EVENTS = {
    "ui_hover":          (ev_ui_hover, -36.0, "ui", "wav", "Barely-there 2.3 kHz glint with a soft up-glide."),
    "ui_click":          (ev_ui_click, -30.0, "ui", "wav", "Tactile 50 ms click: filtered tick, tiny body, 3 kHz ping."),
    "retry":             (ev_retry, -26.0, "ui", "wav", "UI confirm: click plus a tidy two-note ascending acknowledgement."),
    "construction_tick": (ev_construction_tick, -25.0, "feedback", "wav", "Rivet strike and weld sizzle, built to be retriggered."),
    "claim":             (ev_claim, -23.0, "feedback", "wav", "Robot accepts a bounty: servo nod and a two-syllable chirp."),
    "credits":           (ev_credits, -22.5, "feedback", "wav", "Coin-chink cascade and a register double-blip."),
    "equip":             (ev_equip, -23.0, "feedback", "wav", "Purchase equipped: credit blip, servo tighten, latch clunk."),
    "heal":              (ev_heal, -23.5, "feedback", "wav", "Medic/aid patch: soft rising sine shimmer and nano-hiss."),
    "repair":            (ev_repair, -23.0, "feedback", "wav", "Decelerating ratchet, weld hiss, small positive chime."),
    "extract":           (ev_extract, -22.0, "feedback", "wav", "Rock crunch, pebbles into a hopper, canister seal, up-blip."),
    "flag_post":         (ev_flag_post, -21.5, "feedback", "wav", "Beacon spike into regolith, FM hologram spin-up, two-note chime."),
    "build_place":       (ev_build_place, -22.0, "feedback", "wav", "Blueprint scan sweep, three grid-lock blips, magnetic clamp."),
    "robot_spawn":       (ev_robot_spawn, -22.0, "feedback", "wav", "Fabricator whirr, assembly clicks, power-on thump, boot chirps."),
    "party_form":        (ev_party_form, -22.0, "feedback", "wav", "Three robots answer each other, then a short rally horn."),
    "bite":              (ev_bite, -21.0, "feedback", "wav", "Snap, crunch, throat grunt and the metal plate being bitten."),
    "robot_hit":         (ev_robot_hit, -21.0, "feedback", "wav", "Mech strike: bright transient, modal chassis ring, rattle."),
    "stalker_growl":     (ev_stalker_growl, -21.0, "feedback", "wav", "Aggro: formant growl / rising FM screech / chittering snarl."),
    "stalker_death":     (ev_stalker_death, -20.0, "feedback", "wav", "Falling screech, gurgle, body thud, dust puff, debris."),
    "robot_down":        (ev_robot_down, -19.5, "milestone", "ogg", "Power-down whine, sparks, heavy collapse clatter, fizz."),
    "level_up":          (ev_level_up, -19.0, "milestone", "ogg", "Power sweep, rising robot arpeggio, bell cap and sparkle."),
    "launch_ready":      (ev_launch_ready, -19.0, "milestone", "ogg", "Craft staged: power-up whine, clamp release, systems-go bells."),
    "build_complete":    (ev_build_complete, -18.0, "milestone", "ogg", "Heavy modal clunk, latch, hydraulic hiss, bell arpeggio."),
    "research":          (ev_research, -18.0, "milestone", "ogg", "Rising pentatonic bell shimmer into a bright held chord."),
    "alert_warning":     (ev_alert_warning, -17.0, "alert", "wav", "Two rounded tones falling a fourth; firm, not shrill."),
    "fail":              (ev_fail, -16.0, "alert", "ogg", "Power-down, crackle, sinking minor brass, sub thud (stereo)."),
    "alert_critical":    (ev_alert_critical, -15.0, "alert", "wav", "Three bent klaxon pulses with a sub pulse underneath."),
    "victory":           (ev_victory, -13.5, "fanfare", "ogg", "Sub hit, major pad swell, bell arpeggio, shimmer (stereo)."),
    "launch":            (ev_launch, -13.0, "fanfare", "ogg", "Ignition crack, rising roar, Starship crackle, ascent (stereo)."),
}
