"""Beschriftet und nummeriert die Rohbilder von render_bilder.gd (Ordner roh/) fuer die Sichtpruefung.
  python -I bilder_montage.py <rohordner> <ausordner>
Je Bild drei Spalten: Pixel-Look aus, Pixel 2, Pixel 3."""
import os, sys
from PIL import Image, ImageDraw, ImageFont

roh, aus = sys.argv[1], sys.argv[2]
os.makedirs(aus, exist_ok=True)
try:
    f = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 22)
    fk = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
except OSError:
    f = fk = ImageFont.load_default()
look = {1: "Pixel-Look aus", 2: "Pixel-Look an, Pixel 2", 3: "Pixel-Look an, Pixel 3"}
W, H = 720, 480
blaetter = [("still_%d_zu", "1 Grosser Brief zu (gefaltet, Siegel heil)"),
            ("still_%d_siegelbruch", "2 Siegelbruch (0,15 s): zwei Siegelhaelften springen weg"),
            ("still_%d_halb", "3 Grosser Brief halb offen (0,30 s)"),
            ("still_%d_offen", "4 Grosser Brief offen, Inhaltsflaeche mit Probeinhalt"),
            ("klein_%d", "5 Kleiner Faltbrief mit Siegel rot (Gefahr), gruen (Gesundheit), gold (Ansehen)"),
            ("klein_nah_%d", "6 Kleiner Faltbrief, Nahaufnahme"),
            ("streck_%d", "7 Streckung offen: 0,42 m (links) und 0,60 m (rechts), Inhaltsflaeche streckt mit"),
            ("vergleich_%d", "8 Stilvergleich: Handschuh, Schriftrolle, grosser Brief offen, kleiner Brief")]
kacheln = []
for i, (muster, titel) in enumerate(blaetter):
    b = Image.new("RGB", (W * 3, H + 44), (24, 22, 20))
    d = ImageDraw.Draw(b)
    d.text((10, 8), titel, fill=(235, 225, 200), font=f)
    for c, ps in enumerate((1, 2, 3)):
        im = Image.open(os.path.join(roh, (muster % ps) + ".png")).convert("RGB")
        b.paste(im, (c * W, 44))
        d.text((c * W + 10, H + 16), look[ps], fill=(255, 220, 120), font=fk)
    b.save(os.path.join(aus, f"{i + 1:02d}_{muster.replace('_%d', '')}.png"))
    kacheln.append(b)
u = Image.new("RGB", (W * 3, (H + 44) * len(kacheln)))
for i, b in enumerate(kacheln):
    u.paste(b, (0, i * (H + 44)))
u.resize((W * 3 // 3, (H + 44) * len(kacheln) // 3), Image.LANCZOS).save(os.path.join(aus, "00_uebersicht.png"))
print("fertig", aus)
