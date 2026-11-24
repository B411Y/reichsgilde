"""Vergleichsblatt: je Zeile 12 Muybridge-Bilder (Tafel 563, nur Anschauung, nicht im Repository) und 12 Bilder des Pferds (gespiegelt, Blick nach rechts).

    python -I pferd2_muybridge.py <plate.jpg> <Bilderordner> <Ausgabe.png> <start> [<Anzahl Bilder im Zyklus>]
Die Pferdebilder henry_gehen_seite_gehen_<nr>.png werden gleichmaessig ueber den Zyklus (12 Stuecke) gewaehlt, Beginn bei Bild <start>.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont, ImageOps

plate, ordner, aus, start = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
n = int(sys.argv[5]) if len(sys.argv) > 5 else 22
P = Image.open(plate).convert("RGB")
xs = [(20, 396), (414, 790), (810, 1184), (1206, 1580)]
ys = [(20, 304), (330, 616), (636, 920)]
kacheln = [P.crop((x0, y0, x1, y1)) for (y0, y1) in ys for (x0, x1) in xs]
W, Hh = 376, 284
try:
    font = ImageFont.truetype("arial.ttf", 18)
except OSError:
    font = ImageFont.load_default()
bl = Image.new("RGB", (4 * W, 6 * (Hh + 26)), (20, 20, 20))
d = ImageDraw.Draw(bl)
for i, k in enumerate(kacheln):
    zeile, sp = divmod(i, 4)
    y = zeile * 2 * (Hh + 26)
    bl.paste(ImageOps.fit(k, (W, Hh)), (sp * W, y + 26))
    d.text((sp * W + 6, y + 3), "Muybridge Tafel 563, Bild %d" % (i + 1), fill=(235, 235, 235), font=font)
    f = (start + round(i * n / 12)) % n
    pf = os.path.join(ordner, "henry_gehen_seite_gehen_%03d.png" % f)
    im = ImageOps.mirror(Image.open(pf).convert("RGB"))
    bw, bh = im.size
    im = im.crop((int(bw * 0.0), int(bh * 0.05), bw, int(bh * 0.97)))
    im = ImageOps.contain(im, (W, Hh))
    bl.paste(im, (sp * W + (W - im.size[0]) // 2, y + Hh + 26 + 26))
    d.text((sp * W + 6, y + Hh + 26 + 3), "Pferd B, Bild %d (gespiegelt)" % f, fill=(235, 235, 120), font=font)
bl.save(aus)
print("geschrieben", aus)

