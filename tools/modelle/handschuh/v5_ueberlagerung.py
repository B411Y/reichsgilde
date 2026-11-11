"""Fingerlinien (rot) und Schnittpunkt auf das Handflaechenbild zeichnen, gedreht wie in der Skizze (Finger nach links).
python -I v5_ueberlagerung.py <bild_unten.png> <achsen.json> <aus.png> [reihe=pip]"""
import sys, json
from PIL import Image, ImageDraw
bild, js, aus = sys.argv[1:4]; reihe = sys.argv[4] if len(sys.argv) > 4 else "pip"
BG = (230, 220, 190)
im = Image.open(bild).convert("RGBA"); b = Image.new("RGBA", im.size, BG + (255,)); b.alpha_composite(im); b = b.convert("RGB")
r = json.load(open(js))["unten"][reihe]; d = ImageDraw.Draw(b)
for k, g in r["gelenke_px"].items():
    pip = g[1]; tip = g[3]; sx, sy = r["schnitt"]
    d.line([(tip[0], tip[1]), (sx, sy)], fill=(237, 28, 36), width=5)      # Fingerreihe von der Spitze bis zum gemeinsamen Punkt
    d.line([(g[1][0], g[1][1]), (g[2][0], g[2][1]), (g[3][0], g[3][1])], fill=(0, 120, 255), width=3)  # Gelenkzug PIP-DIP-Spitze
s = r["schnitt"]; d.ellipse([s[0] - 7, s[1] - 7, s[0] + 7, s[1] + 7], outline=(0, 90, 255), width=3)
b.rotate(90, expand=True).save(aus)
