"""Setzt die Rechenvorschau von zeiger_symbole_bilder.py zusammen (kein Spielbild; Python mit Pillow, ausserhalb von Blender, mit -I starten):
  - kacheln_windrose.png: Zeigehand mit Windrose (und zum Vergleich mit Fragezeichen) bei Zeigergroesse 64 und 96, auf hellem und dunklem Grund, 1:1 und 3-fach
  - folge_text.gif / folge_text_zeitlupe.gif / folge_text_blatt.png: Hand -> I -> Hand ueber einer Textfeld-Attrappe, Zeitpunkte i/60 s (je Richtung 0,2 s),
    der Zeigerpunkt (Fingerspitze bzw. Mitte des I-Balkens) liegt in jedem Bild auf demselben Mauspunkt zwischen zwei Buchstaben (rote Marken ober- und unterhalb)
  - nah_symbole.png: Nahaufnahmen beider Symbole auf hell und dunkel
Aufruf: python -I zeiger_symbole_blatt.py <ausordner von zeiger_symbole_bilder.py>
"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont

aus = sys.argv[1]
roh = os.path.join(aus, "roh")
P = json.load(open(os.path.join(roh, "punkte.json")))
punkte, n = P["punkte"], P["folge_bilder"] - 1
HELL, DUNKEL = (232, 222, 200), (38, 32, 28)
SUPER = 2   # gerendert mit Superabtastung 2, gezeigt halb so gross (wie HandschuhZeiger)


def lade(name):
    im = Image.open(os.path.join(roh, name + ".png")).convert("RGBA")
    return im.resize((im.width // SUPER, im.height // SUPER), Image.LANCZOS)


def schrift(groesse):
    for f in ("C:/Windows/Fonts/georgia.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f, groesse)
    return ImageFont.load_default()


# ------------------------------------------------------------------ Kacheln Windrose
zeilen = []
for g in (64, 96):
    for name in ("windrose", "fragezeichen"):
        bild = lade(f"{name}_{g}")
        teile = []
        for grund in (HELL, DUNKEL):
            k = Image.new("RGBA", bild.size, grund + (255,)); k.alpha_composite(bild); teile.append(k)
        for grund in (HELL, DUNKEL):
            k = Image.new("RGBA", bild.size, grund + (255,)); k.alpha_composite(bild); teile.append(k.resize((bild.width * 3, bild.height * 3), Image.NEAREST))
        zeilen.append((f"Zeigergroesse {g} px ({name}), Bild {bild.width} px: hell, dunkel, dann 3-fach", teile))
breite = max(sum(t.width for t in teile) + 10 * len(teile) for _, teile in zeilen) + 20
hoehe = sum(max(t.height for t in teile) + 30 for _, teile in zeilen) + 40
blatt = Image.new("RGB", (breite, hoehe), (250, 250, 250)); d = ImageDraw.Draw(blatt); f = schrift(14)
d.text((10, 8), "Rechenvorschau (Blender, kein Spielbild): Zeigehand mit Windrose am Platz des Fragezeichens", fill=(0, 0, 0), font=f)
y = 32
for titel, teile in zeilen:
    d.text((10, y), titel, fill=(0, 0, 0), font=f); y += 20; x = 10
    for t in teile: blatt.paste(t.convert("RGB"), (x, y)); x += t.width + 10
    y += max(t.height for t in teile) + 10
blatt.save(os.path.join(aus, "kacheln_windrose.png"))

# ------------------------------------------------------------------ Folge Hand -> I -> Hand ueber einem Textfeld
W, H = 380, 220
FELD = (20, 30, 330, 74)
TEXT = "Hansestadt Koeln"
f_text = schrift(22)
links = "Hanse"
tx, ty = FELD[0] + 10, FELD[1] + 9
asc, desc = f_text.getmetrics()
MAUS = (tx + f_text.getlength(links), ty + (asc + desc) / 2)   # Einfuegemarke zwischen "Hanse" und "stadt", mitten auf der Zeile


def grund():
    im = Image.new("RGBA", (W, H), (205, 196, 176, 255)); dr = ImageDraw.Draw(im)
    dr.rectangle(FELD, fill=(246, 240, 226), outline=(60, 48, 36), width=2)
    dr.text((tx, ty), TEXT, fill=(30, 24, 18), font=f_text)
    mx = round(MAUS[0])
    dr.line((mx, FELD[1] - 12, mx, FELD[1] - 3), fill=(210, 0, 0), width=1)
    dr.line((mx, FELD[3] + 3, mx, FELD[3] + 12), fill=(210, 0, 0), width=1)
    return im


def bild_mit(name):
    im = grund(); z = lade(name); px, py = punkte[name]
    im.alpha_composite(z, (round(MAUS[0] - px / SUPER), round(MAUS[1] - py / SUPER)))
    return im


folge = []   # (Bildname, Zeit in ms, Text)
for _ in range(6): folge.append(("folge_hand_00", "Hand"))
for i in range(1, n + 1): folge.append((f"folge_hand_{i:02d}", f"Hand schrumpft {i}/{n}"))
for i in range(1, n + 1): folge.append((f"folge_i_{i:02d}", f"I waechst {i}/{n}"))
for _ in range(10): folge.append((f"folge_i_{n:02d}", "I (Textcursor)"))
for i in range(n - 1, -1, -1): folge.append((f"folge_i_{i:02d}", f"I schrumpft {n - i}/{n}"))
for i in range(n - 1, -1, -1): folge.append((f"folge_hand_{i:02d}", f"Hand waechst {n - i}/{n}"))
for _ in range(6): folge.append(("folge_hand_00", "Hand"))
bilder = []
for k, (name, text) in enumerate(folge):
    im = bild_mit(name); dr = ImageDraw.Draw(im)
    dr.text((10, H - 22), f"t = {k * 1000 / 60:6.1f} ms  {text}   (Rechenvorschau, kein Spielbild)", fill=(20, 20, 20), font=schrift(12))
    bilder.append(im.convert("RGB"))
bilder[0].save(os.path.join(aus, "folge_text.gif"), save_all=True, append_images=bilder[1:], duration=20, loop=0)
bilder[0].save(os.path.join(aus, "folge_text_zeitlupe.gif"), save_all=True, append_images=bilder[1:], duration=120, loop=0)
# Blatt: die Bilder des Hinwegs (Hand schrumpft, I waechst) und das Ende des Rueckwegs
auswahl = list(range(5, 6 + 2 * n + 1))
sp = 7; kw, kh = 190, 110
blatt = Image.new("RGB", (sp * (kw + 6) + 6, ((len(auswahl) + sp - 1) // sp) * (kh + 6) + 6), (250, 250, 250))
for j, k in enumerate(auswahl):
    ausschnitt = bilder[k].crop((0, 0, kw, kh))
    blatt.paste(ausschnitt, (6 + (j % sp) * (kw + 6), 6 + (j // sp) * (kh + 6)))
    ImageDraw.Draw(blatt).text((10 + (j % sp) * (kw + 6), 8 + (j // sp) * (kh + 6)), f"{(k - 5) * 1000 / 60:.0f} ms", fill=(200, 0, 0), font=schrift(12))
blatt.save(os.path.join(aus, "folge_text_blatt.png"))

# ------------------------------------------------------------------ Nahaufnahmen
nah = [lade(x) for x in ("nah_windrose", "nah_text_i")]
blatt = Image.new("RGB", (4 * 262 + 10, 272), (250, 250, 250))
x = 5
for b in nah:
    for grund_f in (HELL, DUNKEL):
        k = Image.new("RGBA", b.size, grund_f + (255,)); k.alpha_composite(b); blatt.paste(k.convert("RGB"), (x, 5)); x += b.width + 6
ImageDraw.Draw(blatt).text((8, 258), "Nahaufnahmen (Rechenvorschau, kein Spielbild)", fill=(0, 0, 0), font=schrift(11))
blatt.save(os.path.join(aus, "nah_symbole.png"))
print("Mauspunkt", MAUS, "Bilder", len(bilder))
