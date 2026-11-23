"""Winkelkurven des Gangs je Bild (Winkel und Winkelgeschwindigkeit), aus gehen_messung.json von pferd2_gang.py ("winkel_bilder"), nur Pillow.

    python -I tools/modelle/pferd2_kurven.py <aus.png> <messung.json>=<Name> [<messung2.json>=<Name2> ...]

Vorn (LF): Karpus, Schulter (Neigung des Oberarms gegen die Senkrechte), Laengslage des Schultergelenks; hinten (LH): Huefte (Neigung des Oberschenkels),
Knie, Sprunggelenk. Geschwindigkeit als zentrale Differenz, zyklisch (die Schleifennaht ist eingeschlossen). Graue Linien: Wechsel Stand/Schwung.
"""
import json
import sys

from PIL import Image, ImageDraw, ImageFont

FPS = 24
PLOTS = [("LF", 1, "Karpus vorn (Grad)"), ("LF", 6, "Schulter vorn: Oberarm gegen Senkrechte (Grad)"), ("LF", 7, "Schultergelenk laengs (m)"),
         ("LH", 6, "Huefte: Oberschenkel gegen Senkrechte (Grad)"), ("LH", 1, "Knie hinten (Grad)"), ("LH", 2, "Sprunggelenk hinten (Grad)")]
FARBEN = [(200, 40, 40), (40, 90, 200), (40, 150, 60)]
aus = sys.argv[1]
reihen = []
for a in sys.argv[2:]:
    pfad, name = a.rsplit("=", 1)
    reihen.append((name, json.load(open(pfad))["winkel_bilder"]))
try:
    font = ImageFont.truetype("arial.ttf", 14)
except OSError:
    font = ImageFont.load_default()
PW, PH, RAND = 620, 230, 46
bild = Image.new("RGB", (2 * PW, len(PLOTS) * PH), (255, 255, 255))
d = ImageDraw.Draw(bild)


def kurve(ox, oy, werte_liste, titel, stand):
    alle = [v for _, w, _ in werte_liste for v in w]
    lo, hi = min(alle), max(alle)
    if hi - lo < 1e-6:
        hi = lo + 1
    lo, hi = lo - 0.05 * (hi - lo), hi + 0.05 * (hi - lo)
    x0, x1, y0, y1 = ox + RAND, ox + PW - 12, oy + 26, oy + PH - 30
    d.rectangle([x0, y0, x1, y1], outline=(0, 0, 0))
    d.text((ox + 8, oy + 5), titel, fill=(0, 0, 0), font=font)
    n = len(werte_liste[0][1])
    X = lambda i: x0 + (x1 - x0) * i / (n - 1)
    Y = lambda v: y1 - (y1 - y0) * (v - lo) / (hi - lo)
    for i in range(1, n):
        if stand[i] != stand[i - 1]:
            d.line([X(i - 0.5), y0, X(i - 0.5), y1], fill=(170, 170, 170))
    for k in range(5):
        v = lo + (hi - lo) * k / 4
        d.text((ox + 2, Y(v) - 7), ("%.0f" % v) if hi - lo > 2 else ("%.3f" % v), fill=(90, 90, 90), font=font)
    for i in range(0, n, 2):
        d.text((X(i) - 4, y1 + 3), str(i), fill=(90, 90, 90), font=font)
    for name, w, farbe in werte_liste:
        pts = [(X(i), Y(v)) for i, v in enumerate(w)]
        d.line(pts, fill=farbe, width=2)
        for p in pts:
            d.ellipse([p[0] - 2, p[1] - 2, p[0] + 2, p[1] + 2], fill=farbe)


for zeile, (nm, k, titel) in enumerate(PLOTS):
    wl, vl = [], []
    stand = None
    for j, (name, daten) in enumerate(reihen):
        werte = daten[nm]["werte"]
        y = [r[k] for r in werte]
        n = len(y)
        v = [(y[(i + 1) % n] - y[(i - 1) % n]) / 2 * FPS for i in range(n)]
        wl.append((name, y, FARBEN[j % 3]))
        vl.append((name, v, FARBEN[j % 3]))
        stand = [r[0] < 0.62 for r in werte]
    kurve(0, zeile * PH, wl, f"{titel}, {nm}; " + ", ".join(f"{n_} = {('rot', 'blau', 'gruen')[i % 3]}" for i, (n_, _) in enumerate(reihen)), stand)
    kurve(PW, zeile * PH, vl, f"Geschwindigkeit je s ({titel.split(' (')[0]})", stand)
bild.save(aus)
print("geschrieben", aus)
