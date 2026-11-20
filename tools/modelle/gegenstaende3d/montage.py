"""Setzt die Rohbilder von bilder.py zu beschrifteten Bildern zusammen (Sichtpruefung, nicht im Spiel):
  python -I tools/modelle/gegenstaende3d/montage.py <rohordner> <bildordner>
Je Gegenstand: Vorschau, Zustaende, Clipphasen als Bildleiste (Pixel-Look aus und Pixel 3), Vergleich Pixel-Look aus / 2 / 3.
Pixel 2 und 3: die in 1/2 bzw. 1/3 Aufloesung gerenderten Bilder werden hart (naechster Nachbar) hochgezogen."""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

roh, aus = sys.argv[1], sys.argv[2]
os.makedirs(aus, exist_ok=True)
try:
    SCHRIFT = ImageFont.truetype("arial.ttf", 18)
    KLEIN = ImageFont.truetype("arial.ttf", 14)
except OSError:
    SCHRIFT = KLEIN = ImageFont.load_default()

STAND = {
    "kartentisch": ["zu", "entrollen_halb", "glaetten", "offen", "zu_nah", "offen_nah"],
    "geldtruhe": ["zu", "aufspringen", "offen_leer", "offen_viertel", "offen_halb", "offen_voll"],
    "siegelstempel": ["bereit", "ueber_dem_wachs", "gepresst", "abgehoben", "gesiegelt", "gesiegelt_wappen"],
}
LEISTE = {
    "kartentisch": [("entrollen", 7, 37), ("zusammenrollen", 6, 30)],
    "geldtruhe": [("aufspringen", 7, 31), ("zuklappen", 6, 21)],
    "siegelstempel": [("siegeln", 8, 38)],
}
VERGLEICH = {"kartentisch": ["offen", "zu_nah"], "geldtruhe": ["zu", "offen_voll"],
             "siegelstempel": ["bereit", "gesiegelt_wappen"]}


def lade(name, ps, b=720, h=540):
    im = Image.open(os.path.join(roh, name + f"__p{ps}.png")).convert("RGB")
    return im.resize((b, h), Image.NEAREST) if ps > 1 else im


def schild(im, text, font=SCHRIFT):
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = d.textbbox((8, 6), text, font=font)
    d.rectangle((x0 - 4, y0 - 3, x1 + 4, y1 + 3), fill=(20, 16, 12))
    d.text((8, 6), text, fill=(236, 226, 205), font=font)
    return im


nr = 0
for g in STAND:
    nr += 1
    v = schild(lade(f"{g}__{STAND[g][-1] if g == 'kartentisch' else STAND[g][0]}", 1), f"{g}: Vorschau (Pixel-Look aus)")
    v.save(os.path.join(aus, f"{nr:02d}_{g}_vorschau.png"))
    # Zustaende (Pixel-Look aus)
    st = STAND[g]
    sp = 3
    zeilen = (len(st) + sp - 1) // sp
    z = Image.new("RGB", (360 * sp, 270 * zeilen), (30, 26, 22))
    for i, s in enumerate(st):
        z.paste(schild(lade(f"{g}__{s}", 1).resize((360, 270), Image.LANCZOS), s, KLEIN), ((i % sp) * 360, (i // sp) * 270))
    z.save(os.path.join(aus, f"{nr:02d}_{g}_zustaende.png"))
    # Clipphasen
    for clip, anzahl, bilder in LEISTE[g]:
        leiste = Image.new("RGB", (240 * anzahl, 200 * 2 + 34), (30, 26, 22))
        for i in range(anzahl):
            t = bilder * i / (anzahl - 1) / 30.0
            for zeile, ps in enumerate((1, 3)):
                im = lade(f"{g}__leiste_{clip}_{i}", ps, 240, 200)
                if ps == 1:
                    im = im.resize((240, 200), Image.LANCZOS)
                schild(im, f"{t:.2f} s" + (" Pixel 3" if ps > 1 else ""), KLEIN)
                leiste.paste(im, (i * 240, 34 + zeile * 200))
        schild(leiste, f"{g}: Clip {clip} (oben Pixel-Look aus, unten Pixel 3)")
        leiste.save(os.path.join(aus, f"{nr:02d}_{g}_clip_{clip}.png"))
    # Pixel-Vergleich
    for s in VERGLEICH[g]:
        bl = Image.new("RGB", (720 * 3, 540), (30, 26, 22))
        for i, ps in enumerate((1, 2, 3)):
            im = lade(f"{g}__{s}", ps)
            schild(im, ["Pixel-Look aus", "Pixel 2", "Pixel 3"][i], KLEIN)
            bl.paste(im, (i * 720, 0))
        bl.save(os.path.join(aus, f"{nr:02d}_{g}_pixel_{s}.png"))
print("MONTAGE", nr, "Gegenstaende nach", aus)
