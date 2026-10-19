"""
Draws the Steam achievement icons: 256x256 PNG, one badge per achievement, plus the greyed
"unachieved" version Steam asks for. Standard library only (no Pillow), so it runs anywhere.

    python Tools/steam/make_achievement_icons.py            -> Tools/steam/achievement_icons/
    python Tools/steam/make_achievement_icons.py --size 128 --out somewhere

Names match Tools/steam/achievements.csv: ACH_FIRST_CONTRACT.png and ACH_FIRST_CONTRACT_locked.png.
The look follows the HUD: graphite badge, orange rim, cream emblem.
"""
import argparse
import csv
import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))

GRAPHITE_TOP = (44, 48, 56)
GRAPHITE_BOT = (22, 24, 29)
ORANGE = (240, 138, 36)
CREAM = (239, 230, 210)
DARK = (22, 24, 29)


# ---- signed distance shapes (unit space: centre 0,0, badge radius 1) ------------------------

def sd_circle(x, y, cx, cy, r):
    return math.hypot(x - cx, y - cy) - r


def sd_ring(x, y, cx, cy, r, w):
    return abs(math.hypot(x - cx, y - cy) - r) - w * 0.5


def sd_box(x, y, cx, cy, hw, hh, rad=0.0):
    qx = abs(x - cx) - hw + rad
    qy = abs(y - cy) - hh + rad
    return math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - rad


def sd_segment(x, y, ax, ay, bx, by, w):
    px, py = x - ax, y - ay
    dx, dy = bx - ax, by - ay
    h = max(0.0, min(1.0, (px * dx + py * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - dx * h, py - dy * h) - w * 0.5


def sd_poly(x, y, pts):
    d = float("inf")
    inside = False
    n = len(pts)
    for i in range(n):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        wx, wy = x - ax, y - ay
        h = max(0.0, min(1.0, (wx * ex + wy * ey) / (ex * ex + ey * ey)))
        d = min(d, math.hypot(wx - ex * h, wy - ey * h))
        if (ay > y) != (by > y) and x < ax + (y - ay) * ex / ey:
            inside = not inside
    return -d if inside else d


def union(*ds):
    return min(ds)


def subtract(a, b):
    return max(a, -b)


# ---- emblems: each returns a signed distance in unit space (y up) -----------------------------

def emblem_first_contract(x, y):  # flag on a pole, check mark in the pennant
    pole = sd_segment(x, y, -0.38, -0.5, -0.38, 0.5, 0.09)
    flag = sd_poly(x, y, [(-0.36, 0.5), (0.48, 0.32), (-0.36, 0.08)])
    check = union(sd_segment(x, y, -0.22, 0.29, -0.1, 0.18, 0.07), sd_segment(x, y, -0.1, 0.18, 0.14, 0.4, 0.07))
    base = sd_box(x, y, -0.38, -0.52, 0.22, 0.05, 0.03)
    return union(pole, subtract(flag, check), base)


def emblem_hard_bargain(x, y):  # coin stack with an up arrow
    coins = union(*[sd_box(x, y, -0.18, -0.46 + i * 0.16, 0.3, 0.06, 0.06) for i in range(4)])
    shaft = sd_segment(x, y, 0.36, -0.45, 0.36, 0.3, 0.1)
    head = sd_poly(x, y, [(0.18, 0.24), (0.54, 0.24), (0.36, 0.52)])
    return union(coins, shaft, head)


def emblem_skinflint(x, y):  # coin held in a clamp
    coin = subtract(sd_circle(x, y, 0.0, 0.02, 0.34), sd_ring(x, y, 0.0, 0.02, 0.22, 0.05))
    jaw_l = sd_box(x, y, -0.5, 0.02, 0.07, 0.36, 0.03)
    jaw_r = sd_box(x, y, 0.5, 0.02, 0.07, 0.36, 0.03)
    bar = sd_box(x, y, 0.0, -0.44, 0.57, 0.06, 0.03)
    return union(coin, jaw_l, jaw_r, bar)


def robot_head(x, y, cx, cy, s):
    head = sd_box(x, y, cx, cy, 0.22 * s, 0.17 * s, 0.07 * s)
    eyes = union(sd_circle(x, y, cx - 0.09 * s, cy + 0.01 * s, 0.045 * s), sd_circle(x, y, cx + 0.09 * s, cy + 0.01 * s, 0.045 * s))
    antenna = sd_segment(x, y, cx, cy + 0.17 * s, cx, cy + 0.29 * s, 0.05 * s)
    tip = sd_circle(x, y, cx, cy + 0.31 * s, 0.05 * s)
    return union(subtract(head, eyes), antenna, tip)


def emblem_full_crew(x, y):  # three robot heads
    return union(robot_head(x, y, -0.38, -0.12, 0.85), robot_head(x, y, 0.38, -0.12, 0.85), robot_head(x, y, 0.0, 0.1, 1.15))


def emblem_clean_sheet(x, y):  # shield with a check
    shield = sd_poly(x, y, [(-0.42, 0.42), (0.42, 0.42), (0.42, 0.0), (0.0, -0.52), (-0.42, 0.0)])
    check = union(sd_segment(x, y, -0.2, -0.02, -0.05, -0.18, 0.1), sd_segment(x, y, -0.05, -0.18, 0.24, 0.2, 0.1))
    return subtract(shield, check)


def emblem_den_breaker(x, y):  # cracked mound
    mound = max(sd_circle(x, y, 0.0, -0.32, 0.6), -(y + 0.36))
    crack = union(sd_segment(x, y, -0.02, 0.28, 0.08, 0.08, 0.06), sd_segment(x, y, 0.08, 0.08, -0.08, -0.08, 0.06),
                  sd_segment(x, y, -0.08, -0.08, 0.04, -0.36, 0.06))
    entrance = sd_circle(x, y, 0.28, -0.3, 0.1)
    return subtract(subtract(mound, crack), entrance)


def emblem_self_sufficient(x, y):  # full energy cell with a bolt
    body = subtract(sd_box(x, y, 0.0, -0.06, 0.28, 0.44, 0.06), sd_box(x, y, 0.0, -0.06, 0.18, 0.34, 0.03))
    cap = sd_box(x, y, 0.0, 0.43, 0.12, 0.05, 0.02)
    bolt = sd_poly(x, y, [(0.05, 0.24), (-0.12, -0.06), (0.0, -0.06), (-0.06, -0.34), (0.12, 0.0), (0.0, 0.0)])
    return union(body, cap, bolt)


def emblem_rebuilder(x, y):  # circular arrow around a gear tooth ring
    ang = math.atan2(y, x)
    arc = sd_ring(x, y, 0.0, 0.0, 0.38, 0.11)
    gap = 0.0 if not (-0.35 < ang < 0.55) else 1.0
    arc = arc if gap == 0.0 else max(arc, 0.02)
    head = sd_poly(x, y, [(0.25, 0.18), (0.56, 0.18), (0.42, -0.06)])
    hub = subtract(sd_circle(x, y, 0.0, 0.0, 0.17), sd_circle(x, y, 0.0, 0.0, 0.07))
    return union(arc, head, hub)


def emblem_homesteader(x, y):  # habitat dome with a door and two windows
    dome = max(sd_circle(x, y, 0.0, -0.3, 0.56), -(y + 0.3))
    base = sd_box(x, y, 0.0, -0.36, 0.62, 0.06, 0.02)
    door = sd_box(x, y, 0.0, -0.14, 0.09, 0.17, 0.08)
    w1 = sd_circle(x, y, -0.27, -0.02, 0.07)
    w2 = sd_circle(x, y, 0.27, -0.02, 0.07)
    return union(subtract(subtract(dome, door), union(w1, w2)), base)


def emblem_veteran(x, y):  # three rank chevrons
    out = float("inf")
    for i in range(3):
        cy = 0.28 - i * 0.26
        out = union(out, sd_segment(x, y, -0.4, cy - 0.16, 0.0, cy + 0.06, 0.12), sd_segment(x, y, 0.0, cy + 0.06, 0.4, cy - 0.16, 0.12))
    return out


def emblem_solar_conquest(x, y):  # sun with five worlds on an orbit
    sun = sd_circle(x, y, 0.0, 0.0, 0.17)
    orbit = sd_ring(x, y, 0.0, 0.0, 0.44, 0.03)
    worlds = float("inf")
    for i in range(5):
        a = math.pi / 2 + i * 2 * math.pi / 5
        worlds = min(worlds, sd_circle(x, y, 0.44 * math.cos(a), 0.44 * math.sin(a), 0.085))
    halo = subtract(orbit, union(*[sd_circle(x, y, 0.44 * math.cos(math.pi / 2 + i * 2 * math.pi / 5),
                                                0.44 * math.sin(math.pi / 2 + i * 2 * math.pi / 5), 0.13) for i in range(5)]))
    return union(sun, halo, worlds)


def emblem_ironman(x, y):  # anvil
    top = sd_poly(x, y, [(-0.52, 0.2), (0.42, 0.2), (0.42, 0.04), (0.16, -0.06), (-0.16, -0.06), (-0.34, 0.04)])
    waist = sd_box(x, y, 0.0, -0.16, 0.12, 0.12, 0.02)
    foot = sd_poly(x, y, [(-0.3, -0.42), (0.3, -0.42), (0.18, -0.26), (-0.18, -0.26)])
    spark = union(sd_segment(x, y, 0.1, 0.32, 0.18, 0.5, 0.05), sd_segment(x, y, 0.28, 0.3, 0.42, 0.44, 0.05))
    return union(top, waist, foot, spark)


EMBLEMS = {
    "ACH_FIRST_CONTRACT": emblem_first_contract,
    "ACH_HARD_BARGAIN": emblem_hard_bargain,
    "ACH_SKINFLINT": emblem_skinflint,
    "ACH_FULL_CREW": emblem_full_crew,
    "ACH_CLEAN_SHEET": emblem_clean_sheet,
    "ACH_DEN_BREAKER": emblem_den_breaker,
    "ACH_SELF_SUFFICIENT": emblem_self_sufficient,
    "ACH_REBUILDER": emblem_rebuilder,
    "ACH_HOMESTEADER": emblem_homesteader,
    "ACH_VETERAN": emblem_veteran,
    "ACH_SOLAR_CONQUEST": emblem_solar_conquest,
    "ACH_IRONMAN": emblem_ironman,
}


# ---- rendering ---------------------------------------------------------------------------------

def coverage(d, px):
    """Anti-aliased coverage from a signed distance (in unit space) and the pixel size."""
    return max(0.0, min(1.0, 0.5 - d / px))


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def grey(c, dim):
    l = 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]
    return (l * dim, l * dim, l * dim)


def render(emblem, size, locked):
    px = 2.0 / size
    rows = []
    for j in range(size):
        y = 1.0 - (j + 0.5) * px
        row = bytearray()
        for i in range(size):
            x = -1.0 + (i + 0.5) * px
            badge = coverage(sd_circle(x, y, 0, 0, 0.97), px)
            if badge <= 0.0:
                row += b"\x00\x00\x00\x00"
                continue
            col = mix(GRAPHITE_TOP, GRAPHITE_BOT, (1.0 - y) * 0.5)
            col = mix(col, ORANGE, coverage(sd_ring(x, y, 0, 0, 0.9, 0.1), px))
            col = mix(col, CREAM, coverage(sd_ring(x, y, 0, 0, 0.8, 0.02), px) * 0.6)
            # emblem scaled into the inner disc, with a soft dark drop shadow for depth
            ex, ey = x / 0.72, y / 0.72
            shadow = coverage(emblem(ex - 0.03, ey + 0.04) - 0.02, px / 0.72)
            col = mix(col, DARK, shadow * 0.55)
            col = mix(col, CREAM, coverage(emblem(ex, ey), px / 0.72))
            if locked:
                col = grey(col, 0.55)
            row += bytes((int(col[0]), int(col[1]), int(col[2]), int(round(badge * 255))))
        rows.append(bytes(row))
    return rows


def write_png(path, rows, size):
    raw = b"".join(b"\x00" + r for r in rows)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--size", type=int, default=256)
    ap.add_argument("--out", default=os.path.join(HERE, "achievement_icons"))
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    with open(os.path.join(HERE, "achievements.csv"), newline="", encoding="utf-8") as f:
        names = [row["api_name"] for row in csv.DictReader(f)]
    missing = [n for n in names if n not in EMBLEMS]
    if missing:
        raise SystemExit("No emblem for: " + ", ".join(missing))

    for name in names:
        for locked in (False, True):
            path = os.path.join(args.out, name + ("_locked" if locked else "") + ".png")
            write_png(path, render(EMBLEMS[name], args.size, locked), args.size)
        print("wrote", name)


if __name__ == "__main__":
    main()
