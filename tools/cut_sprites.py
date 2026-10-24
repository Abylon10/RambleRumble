"""Cut placeholder game art out of art/source/character_sheet.webp.

Writes into art/:
  characters/<gender>_<anim>.png   horizontal strips of equal-size frames, bottom-center aligned
  characters/manifest.json         frame count and frame size per strip
  icons/<skill_id>.png             skill button icons
  portraits/<gender>.png           character select portraits

Usage (needs Pillow, NumPy, SciPy):
  python tools/cut_sprites.py

Coordinates below are for the 1536x1024 source sheet. When proper sprite
sheets exist, replace the strips and manifest; the game code reads only those.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "art" / "source" / "character_sheet.webp"
ART = ROOT / "art"

LABEL_H = 22  # height of each panel's title strip
BG_THRESHOLD = 34  # how far (max channel) a pixel must differ from the panel colour

# name: (x0, y0, x1, y1), optional per-panel fixes
PANELS = {
    "male_idle": (356, 10, 573, 102), "male_walk": (579, 10, 819, 102),
    "male_run": (824, 10, 1046, 102), "male_attack": (1051, 10, 1213, 102),
    "male_hurt": (357, 290, 651, 382), "male_death": (657, 290, 919, 382),
    "male_victory": (926, 290, 1213, 382),
    "female_idle": (356, 398, 573, 498), "female_walk": (579, 398, 819, 498),
    "female_run": (824, 398, 1046, 498), "female_attack": (1051, 398, 1213, 498),
    "female_hurt": (357, 702, 662, 795), "female_death": (668, 702, 928, 795),
    "female_victory": (935, 702, 1213, 795),
}
# The sheet's 3rd "female attack" frame is drawn as the male character.
DROP_FRAMES = {"female_attack": [2]}
# Two touching frames come out as one blob wider than this; split them in half.
SPLIT_WIDER_THAN = 90

ICONS = {  # skill id -> source box
    "flame_fist": (941, 842, 996, 897), "wind_slash": (1003, 842, 1058, 897),
    "stone_breaker": (1065, 842, 1120, 897), "thunder_dash": (1127, 842, 1182, 897),
}
PORTRAITS = {"male": (674, 846, 759, 937), "female": (768, 846, 853, 937)}


def cut_frames(sheet, box):
    x0, y0, x1, y1 = box
    crop = sheet[y0 + LABEL_H:y1, x0:x1]
    border = np.concatenate([crop[0], crop[-1], crop[:, 0], crop[:, -1]])
    background = np.median(border, axis=0)
    mask = np.abs(crop - background).max(axis=2) > BG_THRESHOLD
    mask = ndimage.binary_fill_holes(ndimage.binary_closing(mask))
    mask[:2] = mask[-2:] = False
    mask[:, :2] = mask[:, -2:] = False
    labels, _ = ndimage.label(mask)

    blobs = []
    for i, sl in enumerate(ndimage.find_objects(labels), 1):
        if sl is not None and (labels[sl] == i).sum() >= 60:
            blobs.append([sl[1].start, sl[0].start, sl[1].stop, sl[0].stop, [i]])
    blobs.sort()
    # Blobs that overlap horizontally belong to the same frame (e.g. a sword).
    frames = []
    for b in blobs:
        if frames and b[0] <= frames[-1][2] + 1:
            f = frames[-1]
            f[1], f[2], f[3] = min(f[1], b[1]), max(f[2], b[2]), max(f[3], b[3])
            f[4] += b[4]
        else:
            frames.append(b)

    out = []
    for fx0, fy0, fx1, fy1, ids in frames:
        alpha = np.isin(labels[fy0:fy1, fx0:fx1], ids)
        rgba = np.dstack([crop[fy0:fy1, fx0:fx1], alpha * 255]).astype(np.uint8)
        if rgba.shape[1] > SPLIT_WIDER_THAN:
            mid = rgba.shape[1] // 2
            out += [trim(rgba[:, :mid]), trim(rgba[:, mid:])]
        else:
            out.append(rgba)
    return out


def trim(rgba):
    ys, xs = np.nonzero(rgba[:, :, 3])
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def rounded(img, radius=8):
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, img.width - 1, img.height - 1), radius, fill=255)
    img = img.convert("RGBA")
    img.putalpha(mask)
    return img


def main():
    sheet_img = Image.open(SRC).convert("RGB")
    sheet = np.asarray(sheet_img).astype(np.int16)
    manifest = {}
    for name, box in PANELS.items():
        frames = [f for i, f in enumerate(cut_frames(sheet, box)) if i not in DROP_FRAMES.get(name, [])]
        w = max(f.shape[1] for f in frames)
        h = max(f.shape[0] for f in frames)
        strip = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            im = Image.fromarray(f, "RGBA")
            strip.paste(im, (i * w + (w - im.width) // 2, h - im.height))
        strip.save(ART / "characters" / f"{name}.png")
        manifest[name] = {"frames": len(frames), "width": w, "height": h}
        print(f"{name:16} {len(frames)} frames  {w}x{h}")
    (ART / "characters" / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")

    for skill_id, box in ICONS.items():
        rounded(sheet_img.crop(box)).save(ART / "icons" / f"{skill_id}.png")
    for gender, box in PORTRAITS.items():
        rounded(sheet_img.crop(box), radius=10).save(ART / "portraits" / f"{gender}.png")
    print(f"{len(ICONS)} icons, {len(PORTRAITS)} portraits")


if __name__ == "__main__":
    main()
