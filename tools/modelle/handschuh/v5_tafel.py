"""Mehrere PNG nebeneinander mit Beschriftung. python -I v5_tafel.py <aus.png> <bild1> <bild2> ... (Beschriftung = Dateiname)"""
import sys
from PIL import Image, ImageDraw
aus = sys.argv[1]; fs = sys.argv[2:]; BG = (230, 220, 190)
ims = []
for f in fs:
    im = Image.open(f).convert("RGBA"); b = Image.new("RGBA", im.size, BG + (255,)); b.alpha_composite(im); ims.append(b.convert("RGB"))
w, h = ims[0].size; n = len(ims); cols = min(n, 3); rows = (n + cols - 1) // cols
T = Image.new("RGB", (w * cols, h * rows), BG); d = ImageDraw.Draw(T)
for i, (im, f) in enumerate(zip(ims, fs)):
    T.paste(im, ((i % cols) * w, (i // cols) * h)); d.text(((i % cols) * w + 4, (i // cols) * h + 4), f.replace("\\", "/").split("/")[-1], fill=(255, 0, 0))
T.save(aus)
