"""Cut the Chapter 1-2 monster sprites out of the roster sheets.

Writes art/monsters/<slug>.png: the monster with its background removed,
trimmed and shrunk back to roughly its native pixel-art resolution. The game
uses these when they exist and falls back to the sprites drawn in code
(scripts/MonsterArt.cs) for anything without one.

Usage (needs Pillow and NumPy):
  py -3 tools/cut_monsters.py

Boxes are in source-sheet pixels (1536x1024). Each box holds one big monster
with the cell's name, level and element badge left out.
"""
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
BESTIARY = ROOT / "art" / "source" / "monster_bestiary.png"  # one big sprite per cell
CATALOG = ROOT / "art" / "source" / "monster_catalog.png"    # used for the two bosses
OUT = ROOT / "art" / "monsters"

BG_TOLERANCE = 30   # how far (max channel) from the local background colour still counts as background
PIXEL = 3           # the sheets draw one art pixel as about 3x3 screen pixels
KEEP_FRACTION = 0.12  # keep separate blobs at least this big relative to the largest (wings, sparks)
# Dark monsters whose outlines are close to the background colour need a tighter tolerance.
TOLERANCE = {"Goblin Scout": 12, "Entling Shaman": 14, "Goblin Brute": 18}

# Cell columns (x0, x1) on each half of the bestiary sheet.
CH1_COLS = [(14, 157), (163, 305), (312, 455), (461, 603), (610, 752)]
CH2_COLS = [(785, 930), (936, 1078), (1084, 1226), (1233, 1376), (1383, 1527)]
# Sprite rows (y0, y1): below the name, above the row of small variants.
ROWS = [(186, 268), (356, 442), (562, 652)]
BADGE = 40  # the element badge sits in the cell's top-right corner

CH1 = [["Green Slime", "Red Slime", "Blue Slime", "Slime Plimt", "Sproutling"],
       ["Field Rat", "Grasshopper", "Beelet", "Plains Boar", "Scarecrow"],
       ["Thorn Slime", "Rock Slime", "Horned Boar", "Wisp Spirit", "Rambling Knight"]]
CH2 = [["Forest Slime", "Wood Sprite", "Mushcap", "Leafy", "Forest Rabbit"],
       ["Green Wolf", "Goblin Scout", "Treantling", "Sporeling", "Forest Bat"],
       ["Dire Wolf", "Poison Cap", "Goblin Brute", "Ancient Treant", "Shadow Bat"]]

# Monsters missing from the bestiary's grid, taken from the catalog sheet instead.
EXTRA = {
    "Wild Dog": (CATALOG, (318, 560, 395, 650)),
    "Entling Shaman": (CATALOG, (785, 735, 905, 852)),
    "Crimson Fang": (CATALOG, (266, 752, 474, 888)),
    "Elderwood Guardian": (CATALOG, (1208, 707, 1512, 910)),
}


def slug(name: str) -> str:
    return name.lower().replace(" ", "_")


def boxes():
    for cols, grid in ((CH1_COLS, CH1), (CH2_COLS, CH2)):
        for (y0, y1), names in zip(ROWS, grid):
            for (x0, x1), name in zip(cols, names):
                yield name, BESTIARY, (x0 + 4, y0, x1 - 4, y1)
    for name, (sheet, box) in EXTRA.items():
        yield name, sheet, box


def components(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    """4-connected blobs of True pixels."""
    seen = np.zeros_like(mask, dtype=bool)
    h, w = mask.shape
    blobs = []
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            blob, queue = [], deque([(sy, sx)])
            seen[sy, sx] = True
            while queue:
                y, x = queue.popleft()
                blob.append((y, x))
                for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
            blobs.append(blob)
    return blobs


def cut(sheet: Image.Image, box, name: str) -> Image.Image:
    x0, y0, x1, y1 = box
    rgb = np.asarray(sheet.convert("RGB"), dtype=np.int16)[y0:y1, x0:x1]
    h, w, _ = rgb.shape
    # The cell background is the median colour of the crop's border.
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    bg = np.median(border, axis=0)
    near_bg = np.abs(rgb - bg).max(axis=2) <= TOLERANCE.get(name, BG_TOLERANCE)
    # Hide the element badge in bestiary cells (top-right corner).
    if sheet.filename.endswith("bestiary.png") and name not in EXTRA:
        near_bg[: min(h, 30), w - BADGE:] = True
    # Background = near-bg pixels reachable from the border; dark outlines inside the sprite stay.
    background = np.zeros((h, w), dtype=bool)
    queue = deque((y, x) for y in range(h) for x in (0, w - 1)) + deque((y, x) for x in range(w) for y in (0, h - 1))
    while queue:
        y, x = queue.popleft()
        if background[y, x] or not near_bg[y, x]:
            continue
        background[y, x] = True
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not background[ny, nx]:
                queue.append((ny, nx))
    # Keep the monster: its biggest blob and any sizeable pieces (stray specks from neighbours go).
    blobs = sorted(components(~background), key=len, reverse=True)
    keep = np.zeros((h, w), dtype=bool)
    for blob in blobs:
        if len(blob) < len(blobs[0]) * KEEP_FRACTION:
            break
        for y, x in blob:
            keep[y, x] = True
    ys, xs = np.nonzero(keep)
    top, bottom, left, right = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = rgb.astype(np.uint8)
    rgba[..., 3] = np.where(keep, 255, 0)
    image = Image.fromarray(rgba[top:bottom, left:right], "RGBA")
    # Back to about one texel per art pixel so the game can scale it up crisply.
    small = image.resize((max(1, image.width // PIXEL), max(1, image.height // PIXEL)), Image.Resampling.BOX)
    alpha = np.asarray(small)[..., 3]
    out = np.asarray(small).copy()
    out[..., 3] = np.where(alpha >= 128, 255, 0)
    return Image.fromarray(out, "RGBA")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sheets = {}
    for name, path, box in boxes():
        sheet = sheets.setdefault(path, Image.open(path))
        sprite = cut(sheet, box, name)
        sprite.save(OUT / f"{slug(name)}.png")
        print(f"{name:20} {sprite.width}x{sprite.height}")


if __name__ == "__main__":
    main()
