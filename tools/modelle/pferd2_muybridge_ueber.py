"""Muybridge-Tafel 563 (nur Anschauung, NICHT im Repository) Bild fuer Bild mit dem Pferd in gleicher Phase ueberlagern.

    python -I tools/modelle/pferd2_muybridge_ueber.py <plate.jpg> <Bilderordner> <Ausgabe.png> [--zyklus=8.5] [--bild_ref=3] [--phase_ref=0.72] [--bein_off=0.25]

Die zwoelf Bilder der Tafel umfassen etwa 1,4 Schrittzyklen (dasselbe Vorderbein haengt in Bild 3 und zwischen Bild 11 und 12 gebeugt):
Phase des nahen Vorderbeins in Tafelbild i = phase_ref + (i - bild_ref) / zyklus. Das Pferdebild mit derselben Phase des linken Vorderbeins
(Bild f = (Phase + bein_off) * 26, Viertakt LF bei 0,25) wird gespiegelt, auf Bodenlinie und Hoehe der Tafel skaliert (Umriss aus dem hellen Hintergrund)
und als roter Umriss ueber das Tafelbild gelegt; daneben beide Bilder einzeln.
Pferdebilder: spielglb_seite_Gehen_<nr>.png aus pferd2_spielrender.py (Hintergrund hell und einfarbig).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps

ARGS = sys.argv[1:]
POS = [a for a in ARGS if not a.startswith("--")]
OPT = dict(a[2:].split("=", 1) for a in ARGS if a.startswith("--") and "=" in a)
plate, ordner, aus = POS[0], POS[1], POS[2]
ZYK = float(OPT.get("zyklus", 8.5))
BREF = int(OPT.get("bild_ref", 3))
PREF = float(OPT.get("phase_ref", 0.72))
BOFF = float(OPT.get("bein_off", 0.25))
NB = int(OPT.get("bilder", 26))

P = Image.open(plate).convert("L")
xs = [(20, 396), (414, 790), (810, 1184), (1206, 1580)]
ys = [(20, 304), (330, 616), (636, 920)]
kacheln = [P.crop((x0, y0, x1, y1)) for (y0, y1) in ys for (x0, x1) in xs]


def pferd_maske_tafel(k):
    """Dunkle Flaechen (Pferd) ohne die duennen Rasterlinien: Schwelle, dann Oeffnen (Min/Max 5)."""
    m = k.point(lambda v: 255 if v < 95 else 0)
    m = m.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))
    return m


def rumpf(m):
    """Aus der Pferdemaske: Bodenzeile, Rueckenhoehe ueber der Rumpfmitte und Rumpf links/rechts in Bauchhoehe (ohne Beine, Hals, Latte)."""
    a = (np.asarray(m) > 127).copy()
    voll = np.where(a.mean(1) > 0.5)[0]                               # dunkler Boden der Tafel (Zeilen fast ganz dunkel) abschneiden
    if len(voll):
        a[voll.min():] = False
    zeilen = np.where(a.sum(1) >= 3)[0]
    boden = int(zeilen.max())
    oben = int(zeilen.min())
    hz = boden - int((boden - oben) * 0.62)                           # Hoehe etwa des Bauchs/der Brust
    band = a[hz - 3:hz + 4].any(0)
    xs_ = np.where(band)[0]
    # laengste zusammenhaengende Strecke = Rumpf
    teile = np.split(xs_, np.where(np.diff(xs_) > 3)[0] + 1)
    t = max(teile, key=len)
    xl, xr = int(t.min()), int(t.max())
    mitte = (xl + xr) // 2
    spalte = a[:, mitte - 5:mitte + 6].any(1)
    ruecken = int(np.where(spalte)[0].min())
    return boden, ruecken, xl, xr


try:
    font = ImageFont.truetype("arial.ttf", 16)
except OSError:
    font = ImageFont.load_default()
W, H = 376, 284
bl = Image.new("RGB", (3 * W, 12 * (H + 24)), (20, 20, 20))
d = ImageDraw.Draw(bl)
for i, k in enumerate(kacheln):
    nr = i + 1
    ph = (PREF + (nr - BREF) / ZYK) % 1.0
    f = int(round((ph + BOFF) * NB)) % NB
    pf = os.path.join(ordner, "spielglb_seite_Gehen_%03d.png" % f)
    im = ImageOps.mirror(Image.open(pf).convert("RGB"))
    # Pferd im Render: alles, was deutlich vom Hintergrund (Ecke oben links) abweicht
    bg = Image.new("RGB", im.size, im.getpixel((5, 5)))
    diff = ImageChops.difference(im, bg).convert("L").point(lambda v: 255 if v > 40 else 0)
    diff = diff.filter(ImageFilter.MinFilter(7)).filter(ImageFilter.MaxFilter(7))      # Messlatte und Bodenlinie weg
    mk = pferd_maske_tafel(k)
    y = i * (H + 24)
    bl.paste(k.convert("RGB").resize((W, H)), (0, y + 24))
    ueber = k.convert("RGB").resize((W, H))
    if diff.getbbox():
        # Massstab fest: Tafel etwa 126 px/m (Bild 3: Widerrist 201 px ueber dem Boden bei 1,6 m), Render 225 px/m (Ansicht "seite", 4 m auf 900 px);
        # Boden der Render-Ansicht bei Zeile 535,5, Boden der Tafel bei Kachelzeile 259. Seitlich: Verschiebung mit der groessten Deckung der Masken.
        s = float(OPT.get("tafel_px_m", 126.0)) / (im.size[0] / 4.0)
        rs = im.resize((max(1, int(im.size[0] * s)), max(1, int(im.size[1] * s))))
        ds = diff.resize(rs.size)
        kante = ds.filter(ImageFilter.FIND_EDGES).point(lambda v: 255 if v > 60 else 0).filter(ImageFilter.MaxFilter(3))
        oy = int(round(float(OPT.get("tafel_boden", 259)) - (im.size[1] / 2 + 0.98 * im.size[0] / 4.0) * s))
        a_t = np.asarray(mk) > 127
        a_t[int(OPT.get("tafel_boden", 259)) + 2:] = False
        a_r = np.asarray(ds) > 127
        best = (-1.0, 0)
        for ox_ in range(-a_r.shape[1], a_t.shape[1], 2):
            t = np.zeros_like(a_t)
            x0, x1 = max(0, ox_), min(a_t.shape[1], ox_ + a_r.shape[1])
            y0, y1 = max(0, oy), min(a_t.shape[0], oy + a_r.shape[0])
            if x1 <= x0 or y1 <= y0:
                continue
            t[y0:y1, x0:x1] = a_r[y0 - oy:y1 - oy, x0 - ox_:x1 - ox_]
            iou = float((t & a_t).sum()) / max(1.0, float((t | a_t).sum()))
            if iou > best[0]:
                best = (iou, ox_)
        ox = best[1]
        rot = Image.new("RGB", rs.size, (230, 30, 30))
        ueber.paste(rot, (ox, oy), kante)
        klein = rs.copy()
        klein.thumbnail((W, H))
        bl.paste(klein, (2 * W + (W - klein.size[0]) // 2, y + 24 + (H - klein.size[1]) // 2))
    bl.paste(ueber, (W, y + 24))
    d.text((6, y + 4), "Tafel 563, Bild %d | Phase nahes Vorderbein %.2f | Pferd Bild %d (gespiegelt, roter Umriss)" % (nr, ph, f), fill=(235, 235, 120), font=font)
bl.save(aus)
print("geschrieben", aus)
