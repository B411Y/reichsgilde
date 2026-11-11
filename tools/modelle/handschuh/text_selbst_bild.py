"""Beschriftungsbild aus den Zwischendateien von text_selbst_bauen.py (_render.png, _knochen_bild.json): python -I text_selbst_bild.py <selbstordner>
Foto links (Lage laut Ausgabe 'FOTO im Bild' x 0,04-0,58, y 0,15-0,86), Handschuh rechts, Daumenknochen farbig. Kein GUI-Screenshot."""
import sys, os, json
from PIL import Image, ImageDraw
o = sys.argv[1]
r = Image.open(os.path.join(o, "_render.png")).convert("RGBA"); W = r.width
bg = Image.new("RGBA", r.size, (226, 214, 188, 255)); f = Image.open(os.path.join(o, "referenz_texthand.jpg")).convert("RGB")
x0, x1, yt, yb = 0.04, 0.58, 1 - 0.86, 1 - 0.15
bg.paste(f.resize((int((x1 - x0) * W), int((yb - yt) * W))), (int(x0 * W), int(yt * W))); bg.alpha_composite(r)
d = ImageDraw.Draw(bg); K = json.load(open(os.path.join(o, "_knochen_bild.json")))
col = {"finger1-1.R": (230, 30, 30), "finger1-2.R": (250, 160, 0), "finger1-3.R": (30, 120, 240)}
txt = {"finger1-1.R": "1  finger1-1.R (Daumen unten)", "finger1-2.R": "2  finger1-2.R (Daumen Mitte)", "finger1-3.R": "3  finger1-3.R (Daumenspitze)"}
for i, (n, (h, t)) in enumerate(K.items()):
    p = [(h[0] * W, (1 - h[1]) * W), (t[0] * W, (1 - t[1]) * W)]
    d.line(p, fill=col[n], width=7); d.ellipse([p[1][0] - 7, p[1][1] - 7, p[1][0] + 7, p[1][1] + 7], fill=col[n])
    d.rectangle([20, W - 120 + i * 34, 430, W - 90 + i * 34], fill=(255, 255, 255)); d.text((30, W - 112 + i * 34), txt[n], fill=col[n])
d.text((20, 10), "Links: Foto (Vorlage). Rechts: Handschuh. Farbige Linien = Daumenknochen. KEIN Blender-Screenshot, nur Skizze aus dem Hintergrund-Render.", fill=(20, 20, 20))
bg.convert("RGB").save(os.path.join(o, "bild_knochen.png"))
