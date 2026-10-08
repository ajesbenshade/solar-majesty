#!/usr/bin/env python3
"""Solar Majesty intro camera: authored shot generator (3D Artist, 2026-10-08).

Writes a 30 fps sample table (t, position, rotation quaternion, FOV) that a one-off editor
bake turns into the "Intro Camera (Authored)" Timeline track. World numbers come from
IntroShot / ColonyLayout on the #72 branch:
  colony focus F = (192, 0, 190), Earth E = F + (-62, 28, 18), settle S = F + (-18, 22, -18),
  settle rotation Euler(30, 45, 0), title 11 m ahead of S at FOV 35.
The last pose must equal the settle pose so the title slot (placed from it) is centred.
"""
import math, sys

F = (192.0, 0.0, 190.0)
def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, k): return tuple(x * k for x in a)
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def length(a): return math.sqrt(dot(a, a))
def norm(a): l = length(a); return mul(a, 1.0 / l)
def lerp(a, b, t): return add(a, mul(sub(b, a), t))

E = add(F, (-62.0, 28.0, 18.0))
EARTH_R = 7.0
S = add(F, (-18.0, 22.0, -18.0))
CITADEL = add(F, (0.0, 7.0, 2.0))
DURATION = 5.0
SETTLE = 3.35
FPS = 30

def euler_fwd(pitch, yaw):
    p, y = math.radians(pitch), math.radians(yaw)
    return (math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p))
FS = euler_fwd(30.0, 45.0)

# ---- path: cubic Hermite through keyed points with explicit tangents (m/unit) ----
U = norm((F[0]-E[0], 0.0, F[2]-E[2]))           # Earth -> colony, horizontal
RIGHT = (U[2], 0.0, -U[0])                       # camera-right when looking along U
LEFT = mul(RIGHT, -1.0)
UP = (0.0, 1.0, 0.0)

DOLLY = 9.0                                      # final push-in along the settle axis
A = sub(S, mul(FS, DOLLY))
# Approach Earth from its south-west so the Americas face the lens (the globe's texture is
# fixed in world space apart from a slow spin), pass under its south side, then curve onto the
# title axis over the colony.
P0 = add(E, (-30.0, -3.0, -24.0))                # low, south-west of Earth; Earth left, colony far right
P1 = add(E, (-2.0, 4.0, -27.0))                  # passing south of Earth, rising
P2 = add(F, (-40.0, 35.0, -22.0))                # crest over the dark plain, colony lights ahead
KEYS = [P0, P1, P2, A, S]

def tangents():
    ts = []
    for i, p in enumerate(KEYS):
        if i == 0:
            ts.append(mul(sub(KEYS[1], KEYS[0]), 0.8))
        elif i == len(KEYS) - 1:
            ts.append(mul(FS, DOLLY))                 # arrive straight down the title axis
        elif i == len(KEYS) - 2:
            ts.append(mul(FS, length(sub(KEYS[i], KEYS[i-1])) * 0.9))  # already on the axis
        else:
            ts.append(mul(sub(KEYS[i+1], KEYS[i-1]), 0.5))
    return ts
TANS = tangents()

def hermite(u):
    n = len(KEYS) - 1
    seg = min(int(u * n), n - 1)
    t = u * n - seg
    p0, p1, m0, m1 = KEYS[seg], KEYS[seg+1], TANS[seg], TANS[seg+1]
    h00 = 2*t**3 - 3*t**2 + 1; h10 = t**3 - 2*t**2 + t; h01 = -2*t**3 + 3*t**2; h11 = t**3 - t**2
    return add(add(mul(p0, h00), mul(m0, h10)), add(mul(p1, h01), mul(m1, h11)))

# arc-length table so speed follows the ease curve, not the key spacing
N = 4000
_pts = [hermite(i / N) for i in range(N + 1)]
_cum = [0.0]
for i in range(1, N + 1):
    _cum.append(_cum[-1] + length(sub(_pts[i], _pts[i-1])))
TOTAL = _cum[-1]
def at_arclength(s):
    d = s * TOTAL
    lo, hi = 0, N
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if _cum[mid] < d: lo = mid
        else: hi = mid
    seg = _cum[hi] - _cum[lo]
    k = 0.0 if seg <= 1e-9 else (d - _cum[lo]) / seg
    return lerp(_pts[lo], _pts[hi], k)

def smooth(a, b, x):
    if x <= a: return 0.0
    if x >= b: return 1.0
    t = (x - a) / (b - a)
    return t*t*t*(t*(6*t - 15) + 10)

_EASE_N = 2000
def _speed(u):
    """Speed profile over path progress: starts already drifting (35%), builds to full by 40%,
    then lands with zero velocity and zero acceleration at the settle."""
    rise = 0.22 + 0.78 * smooth(0.0, 0.50, u)
    land = 1.0 - smooth(0.52, 1.0, u)
    return rise * land
_EASE = [0.0]
for _i in range(1, _EASE_N + 1):
    _a, _b = (_i - 1) / _EASE_N, _i / _EASE_N
    _EASE.append(_EASE[-1] + 0.5 * (_speed(_a) + _speed(_b)) / _EASE_N)
_EASE = [x / _EASE[-1] for x in _EASE]

def ease(u):
    u = min(max(u, 0.0), 1.0)
    x = u * _EASE_N
    i = min(int(x), _EASE_N - 1)
    return _EASE[i] + (_EASE[i + 1] - _EASE[i]) * (x - i)

def slerp_dir(a, b, t):
    a, b = norm(a), norm(b)
    d = max(-1.0, min(1.0, dot(a, b)))
    th = math.acos(d)
    if th < 1e-6: return a
    s = math.sin(th)
    return norm(add(mul(a, math.sin((1-t)*th)/s), mul(b, math.sin(t*th)/s)))

EARTH_AIM = add(E, (9.0, -1.0, -8.0))            # aim right of Earth so the colony shares the frame

def forward_at(s, pos):
    # The aim point glides from Earth to the citadel (smoother than swinging the direction),
    # then the direction settles onto the title axis for the push-in.
    aim = lerp(EARTH_AIM, CITADEL, smooth(W1[0], W1[1], s))
    f = sub(aim, pos)
    return slerp_dir(f, FS, smooth(W2[0], W2[1], s))

W1 = (0.12, 0.70)
W2 = (0.70, 1.00)

def look_quat(fwd, roll_deg):
    z = norm(fwd)
    x = norm(cross(UP, z))
    y = cross(z, x)
    m00, m01, m02 = x[0], y[0], z[0]
    m10, m11, m12 = x[1], y[1], z[1]
    m20, m21, m22 = x[2], y[2], z[2]
    tr = m00 + m11 + m22
    if tr > 0:
        S4 = math.sqrt(tr + 1.0) * 2
        w = 0.25 * S4; qx = (m21 - m12) / S4; qy = (m02 - m20) / S4; qz = (m10 - m01) / S4
    elif m00 > m11 and m00 > m22:
        S4 = math.sqrt(1.0 + m00 - m11 - m22) * 2
        w = (m21 - m12) / S4; qx = 0.25 * S4; qy = (m01 + m10) / S4; qz = (m02 + m20) / S4
    elif m11 > m22:
        S4 = math.sqrt(1.0 + m11 - m00 - m22) * 2
        w = (m02 - m20) / S4; qx = (m01 + m10) / S4; qy = 0.25 * S4; qz = (m12 + m21) / S4
    else:
        S4 = math.sqrt(1.0 + m22 - m00 - m11) * 2
        w = (m10 - m01) / S4; qx = (m02 + m20) / S4; qy = (m12 + m21) / S4; qz = 0.25 * S4
    q = (qx, qy, qz, w)
    if abs(roll_deg) > 1e-6:
        r = math.radians(roll_deg) * 0.5
        rq = (0.0, 0.0, math.sin(r), math.cos(r))   # about local +Z
        q = qmul(q, rq)
    return q

def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw*bx + ax*bw + ay*bz - az*by,
            aw*by - ax*bz + ay*bw + az*bx,
            aw*bz + ax*by - ay*bx + az*bw,
            aw*bw - ax*bx - ay*by - az*bz)

def qrot(q, v):
    qv = (v[0], v[1], v[2], 0.0)
    qc = (-q[0], -q[1], -q[2], q[3])
    r = qmul(qmul(q, qv), qc)
    return r[:3]

def fov_at(s):
    return 44.0 + (35.0 - 44.0) * smooth(0.35, 1.0, s)

def yaw_of(f): return math.degrees(math.atan2(f[0], f[2]))

def sample(t):
    u = min(t / SETTLE, 1.0)
    s = ease(u)
    pos = at_arclength(s)
    fwd = forward_at(s, pos)
    return s, pos, fwd

def bank_at(t, dt=1.0/FPS):
    # Gentle bank into the turn: proportional to yaw rate, eased to zero at both ends.
    def yr(tt):
        a = yaw_of(sample(max(tt - dt, 0))[2]); b = yaw_of(sample(min(tt + dt, SETTLE))[2])
        d = (b - a + 540) % 360 - 180
        return d / (2 * dt)
    raw = sum(yr(t + k * dt) for k in range(-3, 4)) / 7.0
    env = smooth(0.0, 0.25, t / SETTLE) * (1.0 - smooth(0.70, 0.95, t / SETTLE))
    return 3.0 * math.tanh(-raw * 0.06 / 3.0) * env

def table():
    rows = []
    frames = int(round(DURATION * FPS))
    for i in range(frames + 1):
        t = i / FPS
        s, pos, fwd = sample(t)
        roll = bank_at(t) if t < SETTLE else 0.0
        q = look_quat(fwd, roll)
        rows.append((t, pos, q, fov_at(s), s, fwd, roll))
    # continuity: keep quaternion hemisphere
    out = []
    prev = None
    for r in rows:
        q = r[2]
        if prev is not None and sum(a*b for a, b in zip(prev, q)) < 0:
            q = tuple(-x for x in q)
        prev = q
        out.append((r[0], r[1], q, r[3], r[4], r[5], r[6]))
    return out

if __name__ == "__main__":
    rows = table()
    path = sys.argv[1] if len(sys.argv) > 1 else "intro_camera_samples.csv"
    with open(path, "w") as fh:
        fh.write("t,px,py,pz,qx,qy,qz,qw,fov\n")
        for t, p, q, fov, *_ in rows:
            fh.write("%.6f,%.5f,%.5f,%.5f,%.7f,%.7f,%.7f,%.7f,%.4f\n" % (t, p[0], p[1], p[2], q[0], q[1], q[2], q[3], fov))
    print("wrote", path, len(rows), "rows; path length %.1f m" % TOTAL)
