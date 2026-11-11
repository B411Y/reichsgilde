"""Bildblaetter, GIFs, Nahbild und Lesbarkeitsblatt der Klinken-Clips. python -I v6_klinke_blatt.py <bilderordner> <ausordner>
(Bilder <Clip>_<Bild>_<Ansicht>.png aus v6_klinke.py; Ansichten spiel, seite, schraeg, vorn, nah.)"""
import sys, os, re, glob
from PIL import Image, ImageDraw
ordner, aus = sys.argv[1], sys.argv[2]; os.makedirs(aus, exist_ok=True)
BG = (230, 220, 190); ANS = ["spiel", "seite", "schraeg", "vorn", "nah"]; GIFANS = ["spiel", "seite", "schraeg", "nah"]
fr = {}
for f in glob.glob(ordner + "/*_spiel.png"):
    m = re.match(r"(.*)_(\d+)_spiel\.png", os.path.basename(f)); fr.setdefault(m.group(1), []).append(int(m.group(2)))
def lade(n, f, a, s):
    im = Image.open(f"{ordner}/{n}_{f:02d}_{a}.png").convert("RGBA").resize((s, s), Image.LANCZOS)
    b = Image.new("RGBA", (s, s), BG + (255,)); b.alpha_composite(im); return b.convert("RGB")
def reihe(n, f, ans, s):
    k = Image.new("RGB", (s * len(ans), s), BG)
    for i, a in enumerate(ans): k.paste(lade(n, f, a, s), (i * s, 0))
    return k
HAUPT = ["Klinke_bereit", "Klinke_greifen", "Klinke_druecken", "Klinke_loslassen"]
for n in sorted(fr):
    f_ = sorted(fr[n]); pick = f_ if len(f_) <= 11 else f_[:11]
    S = 190; b = Image.new("RGB", (S * len(pick), S * len(ANS)), BG); d = ImageDraw.Draw(b)
    for j, f in enumerate(pick):
        for i, a in enumerate(ANS): b.paste(lade(n, f, a, S), (j * S, i * S))
        d.text((j * S + 4, 3), f"Bild {f}", fill=(40, 30, 20))
    for i, a in enumerate(ANS): d.text((4, i * S + 16), a, fill=(40, 30, 20))
    b.save(f"{aus}/blatt_{n}.png")
    fl = [reihe(n, f, GIFANS, 256).convert("P", palette=Image.ADAPTIVE) for f in f_]
    fl[0].save(f"{aus}/{n}.gif", save_all=True, append_images=fl[1:], duration=(110 if n == "Klinke_bereit" else 90), loop=0)
# Folge Bereit -> Greifen -> Druecken -> Loslassen -> Bereit in einem GIF
folge = [("Klinke_bereit", 0)] * 8 + [("Klinke_bereit", 8), ("Klinke_bereit", 16)]
for n in HAUPT[1:]: folge += [(n, f) for f in sorted(fr[n])]
folge += [("Klinke_bereit", 0)] * 8
fl = [reihe(n, f, GIFANS, 256).convert("P", palette=Image.ADAPTIVE) for n, f in folge]
fl[0].save(f"{aus}/Folge_Bereit_Greifen_Druecken_Loslassen.gif", save_all=True, append_images=fl[1:], duration=[110] * len(fl), loop=0)
# Nahbild des Griffs: Greifen (Ende), Druecken (Anfang, halb, gedrueckt), Loslassen (Mitte)
nb = [("Klinke_greifen", max(fr["Klinke_greifen"])), ("Klinke_druecken", 0), ("Klinke_druecken", 8), ("Klinke_druecken", 16), ("Klinke_loslassen", 6)]
b = Image.new("RGB", (400 * len(nb), 400), BG)
for i, (n, f) in enumerate(nb): b.paste(lade(n, f, "nah", 400), (i * 400, 0))
b.save(f"{aus}/nahbild_griff.png")
# Lesbarkeit: Spielansicht bei Zeigergroesse 48 und 64 (Bild = 1,5 Zeigergroessen = 72 / 96 px), 4x NEAREST: Bereit, Greifen, Druecken
st = [("Klinke_bereit", 0), ("Klinke_greifen", 4), ("Klinke_greifen", max(fr["Klinke_greifen"])), ("Klinke_druecken", 8), ("Klinke_druecken", 16), ("Klinke_loslassen", 6)]
rows = []
for s in (48, 64):
    px = int(s * 1.5); z = 4; row = Image.new("RGB", (len(st) * (px * z + 8) + 8, px * z + 8), BG); x = 8
    for n, f in st:
        im = Image.open(f"{ordner}/{n}_{f:02d}_spiel.png").convert("RGBA").resize((px, px), Image.LANCZOS)
        bb = Image.new("RGBA", (px, px), BG + (255,)); bb.alpha_composite(im)
        row.paste(bb.convert("RGB").resize((px * z, px * z), Image.NEAREST), (x, 4)); x += px * z + 8
    rows.append(row)
W = max(r.width for r in rows); out = Image.new("RGB", (W, sum(r.height for r in rows)), BG); y = 0
for r in rows: out.paste(r, (0, y)); y += r.height
out.save(f"{aus}/klein_lesbarkeit_klinke_48_64.png")
print("fertig")
