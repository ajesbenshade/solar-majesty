"""
Solar Majesty — crop panel 01 (three-quarter view) from Imagine turnaround sheets.

Imagine sheets are 16:9 with three labeled views. Panel 01 is the left third — best input
for Copilot 3D monocular reconstruction.

Run from repo root (no Blender required):
  python Blender/scripts/sm_crop_turnaround.py
  python Blender/scripts/sm_crop_turnaround.py --only SM_Unit_CourierBot
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("[SM] Pillow required: pip install Pillow")
    sys.exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent.parent
CONCEPT_DIR = PROJECT_ROOT / "ConceptSheets"
OUTPUT_DIR = CONCEPT_DIR / "Copilot3D_Input"

# Fractional crop box (left, upper, right, lower) for panel 01 on standard sheets.
# Tune if a sheet layout differs.
PANEL_01_BOX = (0.0, 0.12, 0.34, 0.92)


def crop_three_quarter(src: Path, dst: Path, box=PANEL_01_BOX):
    with Image.open(src) as im:
        w, h = im.size
        l, u, r, b = box
        crop = im.crop((int(w * l), int(h * u), int(w * r), int(h * b)))
        dst.parent.mkdir(parents=True, exist_ok=True)
        crop.save(dst, format="PNG", optimize=True)
    print(f"[SM] {src.name} -> {dst.relative_to(PROJECT_ROOT)}")


def stem_to_asset(stem: str) -> str:
    # SM_Unit_CourierBot_Turnaround → SM_Unit_CourierBot
    if stem.endswith("_Turnaround"):
        return stem[: -len("_Turnaround")]
    if stem.endswith("_ThreeQuarter"):
        return stem[: -len("_ThreeQuarter")]
    return stem


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Crop Copilot 3D input from turnaround JPGs")
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        help="Asset name prefix (repeatable), e.g. SM_Unit_CourierBot",
    )
    parser.add_argument(
        "--input-dir",
        type=Path,
        default=CONCEPT_DIR,
        help="Folder containing *_Turnaround.jpg/png",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=OUTPUT_DIR,
        help="Output folder for *_ThreeQuarter.png",
    )
    return parser.parse_args()


def main():
    args = parse_args()
    patterns = ("*_Turnaround.jpg", "*_Turnaround.jpeg", "*_Turnaround.png")
    sources: list[Path] = []
    for pat in patterns:
        sources.extend(sorted(args.input_dir.glob(pat)))
    if not sources:
        print(f"[SM] No turnaround sheets in {args.input_dir}")
        print("[SM] Add ConceptSheets/SM_Unit_*_Turnaround.jpg then re-run.")
        sys.exit(0)

    only = set(args.only) if args.only else None
    count = 0
    for src in sources:
        asset = stem_to_asset(src.stem)
        if only is not None and asset not in only:
            continue
        dst = args.output_dir / f"{asset}_ThreeQuarter.png"
        crop_three_quarter(src, dst)
        count += 1

    print(f"[SM] Cropped {count} sheet(s) -> {args.output_dir.relative_to(PROJECT_ROOT)}")


if __name__ == "__main__":
    main()
