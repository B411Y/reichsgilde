"""Bildblaetter, GIFs und Lesbarkeitsblatt fuer die Haltung Text. python -I v6_text_blatt.py <bilderordner> <ausordner>
(Bilder: <Clip>_<Bild>_<Ansicht>.png aus v6_text.py; Ansichten oben, seite, schraeg, spitze.)"""
import sys, os, re, glob
from PIL import Image, ImageDraw
ordner, aus = sys.argv[1], sys.argv[2]
os.makedirs(aus, exist_ok=True)
BG = (230, 220, 190); ANS = ["oben", "seite", "schraeg", "spitze"]
clips = {}
for f in glob.glob(ordner + "/*_oben.png"):
    m = re.match(r"(.*)_(\d+)_oben\.png", os.path.basename(f))
    if m and not m.group(1).startswith("probe"): clips.setdefault(m.group(1), []).append(int(m.group(2)))
def lade(n, f, a, s):
    im = Image.open(f"{ordner}/{n}_{f:02d}_{a}.png").convert("RGBA").resize((s, s), Image.LANCZOS)
    b = Image.new("RGBA", (s, s), BG + (255,)); b.alpha_composite(im); return b.convert("RGB")
for n, fr in clips.items():
    fr = sorted(fr)
    pick = fr if len(fr) <= 9 else [fr[round(i * (len(fr) - 1) / 8)] for i in range(9)]   # mindestens 8, hoechstens 9 Bilder je Zeile
    S = 200; b = Image.new("RGB", (S * len(pick), S * 4), BG); d = ImageDraw.Draw(b)
    for j, f in enumerate(pick):
        for i, a in enumerate(ANS):
            b.paste(lade(n, f, a, S), (j * S, i * S))
        d.text((j * S + 4, 3), f"Bild {f}", fill=(40, 30, 20))
    for i, a in enumerate(ANS): d.text((4, i * S + 16), a, fill=(40, 30, 20))
    b.save(f"{aus}/blatt_{n}.png")
    S = 256; fl = []
    for f in fr:
        k = Image.new("RGB", (S * 4, S), BG)
        for i, a in enumerate(ANS): k.paste(lade(n, f, a, S), (i * S, 0))
        fl.append(k.convert("P", palette=Image.ADAPTIVE))
    dau = 80 if n.startswith("Uebergang") else 110
    fl[0].save(f"{aus}/{n}.gif", save_all=True, append_images=fl[1:] + (fl[-2:0:-1] if n.startswith("Uebergang") else []), duration=dau, loop=0)
    print("Blatt", n, len(fr), "Bilder")
# Lesbarkeit als Cursor bei 48 und 64 px: Haltung Text in den Ansichten, 4x vergroessert (NEAREST), daneben Ruhe zum Vergleich
posen = [("Text_00", a) for a in ANS] + [("Uebergang_Ruhe_Text_00", "schraeg")]
rows = []
for s in (48, 64):
    row = Image.new("RGB", (len(posen) * (s * 4 + 12), s * 4 + 8), BG); x = 6
    for f, a in posen:
        pfad = f"{ordner}/{f[:-3]}_{f[-2:]}_{a}.png"
        im = Image.open(pfad).convert("RGBA"); im = im.crop(im.getbbox()); sc = s / max(im.size)
        im = im.resize((max(1, round(im.width * sc)), max(1, round(im.height * sc))), Image.LANCZOS)
        k = Image.new("RGBA", (s, s), BG + (255,)); k.alpha_composite(im, ((s - im.width) // 2, (s - im.height) // 2))
        row.paste(k.convert("RGB").resize((s * 4, s * 4), Image.NEAREST), (x, 4)); x += s * 4 + 12
    rows.append(row)
bl = Image.new("RGB", (max(r.width for r in rows), sum(r.height for r in rows)), BG); y = 0
for r in rows: bl.paste(r, (0, y)); y += r.height
bl.save(f"{aus}/klein_lesbarkeit_text_48_64.png")
