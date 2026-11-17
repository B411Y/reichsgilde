"""Beschriftet und nummeriert die Rohbilder von render_bilder.gd (Pixel-Look aus, 2 und 3 nebeneinander) und legt eine Uebersicht an.
  python -I bilder_montage.py <rohordner> <ausordner>"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

roh, aus = sys.argv[1], sys.argv[2]
os.makedirs(aus, exist_ok=True)
try:
    f = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    fk = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
except OSError:
    f = fk = ImageFont.load_default()
NAMEN = {"personal": "Personalbuch (Lesepult der Werkstatt)", "familie": "Familienbuch (Stubentisch im Wohnhaus)", "kontor": "Kontorbuch (Schreibpult der Werkstatt)"}
LOOK = {1: "Pixel-Look aus", 2: "Pixel-Look an, Pixel 2", 3: "Pixel-Look an, Pixel 3"}
POSEN = [("zu", "zu (Rast-Pose)"), ("halb", "halb aufgeklappt (Clip aufklappen, Winkel 90 Grad)"), ("offen", "offen (Ende von aufklappen)"),
         ("umblaettern", "Blatt wird umgeblaettert (Clip umblaettern, Blatt senkrecht)"), ("inhalt", "offen, Platzhalter-Tafel auf den beiden Inhaltsflaechen"),
         ("moebel_zu", "zu, auf dem Moebel"), ("moebel_offen", "offen, auf dem Moebel")]
W, H = 720, 480
nr = 1
kacheln = []
for art in ("personal", "familie", "kontor"):
    for kz, titel in POSEN:
        dat = [f"{roh}/{art}_{ps}_{kz}.png" for ps in (1, 2, 3)]
        if not all(os.path.exists(d) for d in dat):
            continue
        b = Image.new("RGB", (W * 3, H + 44), (24, 22, 20))
        d = ImageDraw.Draw(b)
        d.text((10, 8), f"{nr} {NAMEN[art]}: {titel}", fill=(235, 225, 200), font=f)
        for c, ps in enumerate((1, 2, 3)):
            b.paste(Image.open(dat[c]).convert("RGB"), (c * W, 44))
            d.text((c * W + 10, H + 16), LOOK[ps], fill=(255, 220, 120), font=fk)
        b.save(f"{aus}/{nr:02d}_{art}_{kz}.png")
        if kz in ("zu", "offen") or kz == "umblaettern":
            kacheln.append(b)
        nr += 1
for kz, titel in (("zu", "alle drei Buecher zu, links Handschuh, Schriftrolle und Brief als Stilvergleich"), ("offen", "alle drei Buecher offen, links Handschuh, Schriftrolle und Brief")):
    dat = [f"{roh}/vergleich_{ps}_{kz}.png" for ps in (1, 2, 3)]
    if not all(os.path.exists(d) for d in dat):
        continue
    b = Image.new("RGB", (W * 2, (H + 44) * 3 - 0), (24, 22, 20))
    d = ImageDraw.Draw(b)
    for c, ps in enumerate((1, 2, 3)):
        y0 = c * (H + 44)
        d.text((10, y0 + 8), f"{nr} Stilvergleich: {titel} - {LOOK[ps]}", fill=(235, 225, 200), font=f)
        b.paste(Image.open(dat[c]).convert("RGB"), (0, y0 + 44))
    b.save(f"{aus}/{nr:02d}_stilvergleich_{kz}.png")
    nr += 1
# Uebersicht: je Buch Pixel 2 in den vier Posen
ues = Image.new("RGB", (W * 4 // 2, (H // 2 + 36) * 3), (24, 22, 20))
d = ImageDraw.Draw(ues)
for r, art in enumerate(("personal", "familie", "kontor")):
    d.text((8, r * (H // 2 + 36) + 6), NAMEN[art] + " (Pixel-Look 2): zu, halb, offen, umblaettern", fill=(235, 225, 200), font=fk)
    for c, kz in enumerate(("zu", "halb", "offen", "umblaettern")):
        p = f"{roh}/{art}_2_{kz}.png"
        if os.path.exists(p):
            ues.paste(Image.open(p).convert("RGB").resize((W // 2, H // 2), Image.LANCZOS), (c * W // 2, r * (H // 2 + 36) + 36))
ues.save(f"{aus}/00_uebersicht.png")
print("FERTIG", nr - 1)
