"""Skizze des Nutzers (links: echte Faust mit Fingerlinien) gegen Handschuh v4 und v5 mit denselben Linien (Fingerreihe bis zum gemeinsamen Punkt).
python -I v5_skizze_paar.py <skizze.png> <v4_unten_linien.png> <v5_unten_linien.png> <aus.png>"""
import sys
from PIL import Image, ImageDraw
sk, a, b, aus = sys.argv[1:5]
S = Image.open(sk).convert("RGB").crop((0, 0, 596, 597)).resize((600, 600))
def pan(p):
    im = Image.open(p).convert("RGB"); w, h = im.size; return im.crop((int(w * 0.18), int(h * 0.27), int(w * 0.82), int(h * 0.73))).resize((600, 429))
P = [S, pan(a), pan(b)]; T = Image.new("RGB", (1800, 640), (230, 220, 190)); d = ImageDraw.Draw(T)
for i, (p, t) in enumerate(zip(P, ("Skizze des Nutzers: echte Faust (Daumenseite oben)", "Handschuh v5 (Fingerreihe rot, Gelenkzug blau)", "Handschuh v6"))):
    T.paste(p, (i * 600, 30)); d.text((i * 600 + 6, 8), t, fill=(200, 0, 0))
T.save(aus)
