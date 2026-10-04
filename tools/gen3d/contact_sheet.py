"""Arena contact sheet for T-TER-046: input image, generator mesh render (before), finished low (after), albedo, normal.
A Tripo column is drawn as 'not run' when no Tripo finish folder exists."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
RUN = ROOT / "orchestration/runs/terrarium/T-TER-046"
SRC = ROOT / "apps/terrarium/Art/Source/gen3d"
S = 320
rows = [("mushroom", "mushroom", 42), ("mushroom", "mushroom", 7), ("pebble_set", "pebble_set", 42), ("pebble_set", "pebble_set", 7),
        ("moss_clump", "moss_clump", 42)]
cols = ["input (Nano Banana)", "TRELLIS.2 high (render)", "finished low", "albedo bake", "normal bake", "Tripo smart_low_poly"]
sheet = Image.new("RGB", (S * len(cols), 24 + S * len(rows)), (24, 24, 24))
d = ImageDraw.Draw(sheet)
for i, c in enumerate(cols):
    d.text((i * S + 6, 6), c, fill=(230, 230, 230))
for r, (img, cls, seed) in enumerate(rows):
    y = 24 + r * S
    folder = RUN / "finished" / f"{cls}_s{seed}"
    tiles = [SRC / f"{img}.png"] + [folder / f for f in ("before.png", "after.png", "albedo.png", "normal.png")]
    for c, p in enumerate(tiles):
        if p.exists():
            sheet.paste(Image.open(p).convert("RGB").resize((S, S)), (c * S, y))
    card = folder / "scorecard.json"
    label = f"{cls} seed {seed}"
    if card.exists():
        sc = json.loads(card.read_text())
        label += f" | tris {sc['tris']}/{sc['requested_tris']} {'OK' if sc['within_tolerance'] else 'MISS'}"
    d.text((2 * S + 6, y + S - 16), label, fill=(255, 255, 120))
    d.text(((len(cols) - 1) * S + 10, y + S // 2), "not run: no TRIPO_API_KEY", fill=(255, 140, 140))
out = RUN / "arena_sheet.png"
sheet.save(out)
print(out)
