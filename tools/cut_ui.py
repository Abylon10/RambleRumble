"""Cut the world map, chapter maps and item icons out of the UI mockup.

Writes art/ui/:
  world_map.png      floating islands of Chapters 1-4 (world map page background)
  chapter1_map.png   Whispering Plains stage map (chapter map background)
  chapter2_map.png   Verdant Forest stage map (enlarged from the world map island)
  icon_<name>.png    reward/drop icons with the dark background removed

Usage (needs Pillow and NumPy):
  py -3 tools/cut_ui.py

Boxes are in mockup pixels (1536x1024). Stage node positions on the chapter
maps are listed in StageCatalog.cs as fractions of these crops.
"""
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "art" / "source" / "ui_mockup.webp"
OUT = ROOT / "art" / "ui"

MAPS = {
    "world_map": (100, 62, 790, 596),
    "chapter1_map": (834, 62, 1500, 372),
    "chapter2_map": (440, 140, 790, 420),
}

ICONS = {
    "gold": (837, 887, 873, 923),
    "slime_gel": (887, 887, 923, 923),
    "leaf": (937, 887, 973, 923),
    "wood": (988, 887, 1024, 923),
    "crystal": (1039, 887, 1075, 923),
    "exp": (1138, 826, 1180, 866),
}

BG_TOLERANCE = 34


def cut_icon(image: Image.Image, box) -> Image.Image:
    """The icon on its slot with the slot's dark background flood-filled away from the edges."""
    rgb = np.asarray(image.crop(box).convert("RGB"), dtype=np.int16)
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    bg = np.median(border, axis=0)
    near_bg = np.abs(rgb - bg).max(axis=2) <= BG_TOLERANCE
    background = np.zeros((h, w), dtype=bool)
    queue = deque([(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)])
    while queue:
        y, x = queue.popleft()
        if background[y, x] or not near_bg[y, x]:
            continue
        background[y, x] = True
        queue.extend((ny, nx) for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)) if 0 <= ny < h and 0 <= nx < w)
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = rgb.astype(np.uint8)
    rgba[..., 3] = np.where(background, 0, 255)
    return Image.fromarray(rgba, "RGBA")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    image = Image.open(SRC).convert("RGB")
    for name, box in MAPS.items():
        image.crop(box).save(OUT / f"{name}.png")
    for name, box in ICONS.items():
        cut_icon(image, box).save(OUT / f"icon_{name}.png")
    print("wrote", len(MAPS) + len(ICONS), "files to", OUT)


if __name__ == "__main__":
    main()
