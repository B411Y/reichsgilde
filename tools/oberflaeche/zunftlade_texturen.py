#!/usr/bin/env python3
"""Baut die Texturen der Zunftlade (game/assets/oberflaeche/zunftlade/): Holz der Lade, Holz der Fächer,
Steinrahmen (9 Felder), Steinbogen ums Porträt und Pergament.

Nur Zuschnitt, Verkleinern, Drehen, Biegen und Farbe; das Holz wird zusätzlich mit der Normalkarte und der
Verdeckungskarte DERSELBEN Textur schattiert. Keine hinzugerechneten Effekte (keine Kerben, Flecken, Verläufe,
Schatten) – Entscheidung des Nutzers vom 18.11.2026.

Quellen (alle CC0 1.0, nicht im Repository; Seiten und Urheber: game/assets/oberflaeche/zunftlade/HERKUNFT.md):
  <quellen>/polyhaven_dark_wooden_planks/dark_wooden_planks_{diff,nor_gl,ao}_1k.jpg
  <quellen>/polyhaven_wood_table_worn/wood_table_worn_{diff,nor_gl}_1k.jpg
  <quellen>/polyhaven_large_sandstone_blocks/large_sandstone_blocks_diff_1k.jpg
  <quellen>/commons_pergament/Pergament.1.jpg

Aufruf (Python 3 mit numpy und Pillow):
  python3 -I tools/oberflaeche/zunftlade_texturen.py <quellen> game/assets/oberflaeche/zunftlade
"""
import sys
import numpy as np
from PIL import Image

Q, ZIEL = sys.argv[1], sys.argv[2]

LICHT = np.array([-0.55, -0.62, 0.56])
LICHT = LICHT / np.linalg.norm(LICHT)


def lade(pfad, groesse=None):
    im = Image.open(Q + '/' + pfad).convert('RGB')
    if groesse:
        im = im.resize((groesse, groesse), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def speichere(a, name, q=80):
    a = np.clip(a, 0, 1)
    modus = 'RGBA' if a.shape[2] == 4 else 'RGB'
    Image.fromarray((a * 255 + 0.5).astype(np.uint8), modus).save(ZIEL + '/' + name, quality=q, method=6)


def aus_normalkarte(nor, staerke=1.0):
    """Streulicht (um 1) aus der Normalkarte der Textur (OpenGL-Richtung)."""
    n = nor * 2 - 1
    nx, ny, nz = n[..., 0] * staerke, -n[..., 1] * staerke, n[..., 2]
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return (nx * LICHT[0] + ny * LICHT[1] + nz * LICHT[2]) / l / LICHT[2]


def holz():
    """Holz der Lade: Farbkarte warm eingefärbt, mit Verdeckung und Normalkarte derselben Textur schattiert. Kachelt."""
    d = lade('polyhaven_dark_wooden_planks/dark_wooden_planks_diff_1k.jpg')
    nor = lade('polyhaven_dark_wooden_planks/dark_wooden_planks_nor_gl_1k.jpg')
    ao = lade('polyhaven_dark_wooden_planks/dark_wooden_planks_ao_1k.jpg')[..., :1]
    lum = d.mean(axis=2, keepdims=True)
    warm = lum * np.array([1.42, 0.98, 0.60], dtype=np.float32)
    a = (0.45 * d + 0.55 * warm) * 1.18
    a = a * (0.55 + 0.45 * ao)
    a = a * (1 + 0.55 * (aus_normalkarte(nor, 1.6) - 1))[..., None]
    speichere(a * 0.88, 'holz.webp', 80)


def fachholz():
    """Holz der Fächer: dunkler und entsättigt, 512 px. Kachelt."""
    d = lade('polyhaven_wood_table_worn/wood_table_worn_diff_1k.jpg', 512)
    nor = lade('polyhaven_wood_table_worn/wood_table_worn_nor_gl_1k.jpg', 512)
    lum = d.mean(axis=2, keepdims=True)
    a = (0.55 * d + 0.45 * lum * np.array([1.30, 0.98, 0.66], dtype=np.float32)) * 0.80
    a = a * (1 + 0.6 * (aus_normalkarte(nor, 1.8) - 1))[..., None]
    speichere(a, 'fachholz.webp', 80)


def gesims():
    """Das Gesimsband der Sandsteintextur (Zeilen 38–122), 20 % heller."""
    tex = Image.open(Q + '/polyhaven_large_sandstone_blocks/large_sandstone_blocks_diff_1k.jpg').convert('RGB')
    return tex.crop((0, 38, 1024, 122)).point(lambda v: min(255, int(v * 1.2)))


def rahmen(band_bild):
    """Neun Felder: Rand 32 px (im Spiel halb so breit gezeichnet), das Band läuft um, Ecken auf Gehrung."""
    r = 32
    band = band_bild.resize((round(1024 * r / 84), r), Image.LANCZOS)
    l = band.width
    n = l + 2 * r
    oben = np.asarray(band)
    bild = np.zeros((n, n, 4), dtype=np.uint8)
    lang = np.concatenate([oben, oben, oben], axis=1)[:, l - r:l - r + n]
    yy, xx = np.mgrid[0:n, 0:n]
    voll = {k: np.zeros((n, n, 3), np.uint8) for k in 'oulr'}
    voll['o'][:r], voll['u'][n - r:], voll['l'][:, :r], voll['r'][:, n - r:] = lang, lang[::-1], np.rot90(lang, 1), np.rot90(lang, 3)
    do, du, dl, dr = yy, n - 1 - yy, xx, n - 1 - xx
    naechst = np.argmin(np.stack([do, du, dl, dr]), axis=0)
    for i, k in enumerate('oulr'):
        m = (naechst == i)
        bild[m, :3] = voll[k][m]
    bild[..., 3] = (np.minimum(np.minimum(do, du), np.minimum(dl, dr)) < r) * 255
    Image.fromarray(bild, 'RGBA').save(ZIEL + '/rahmen.webp', lossless=True, method=6)
    return n, r


def bogen(band_bild):
    """Dasselbe Band um den Porträtbogen gebogen: Halbkreis oben, gerade Seiten, gerade Schwelle unten."""
    b, h, dick = 464, 428, 28
    src = np.asarray(band_bild, dtype=np.float32)
    yy, xx = np.mgrid[0:h, 0:b].astype(np.float32)
    cx = cy = b / 2.0
    R = b / 2.0
    rad = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    win = np.arctan2(cy - yy, xx - cx)
    oben = yy < cy
    t_bogen, s_bogen = (R - rad), (np.pi - win) * R
    bogenlang = np.pi * R
    t_li, s_li = xx, -(yy - cy)
    t_re, s_re = (b - 1 - xx), bogenlang + (yy - cy)
    t_un, s_un = (h - 1 - yy), xx + 2000
    t = np.where(oben, t_bogen, np.minimum(np.minimum(t_li, t_re), t_un))
    s = np.where(oben, s_bogen, np.where(t_un <= np.minimum(t_li, t_re), s_un, np.where(t_li <= t_re, s_li, s_re)))
    maske = (t >= 0) & (t < dick) & np.where(oben, rad <= R, True)
    v = np.clip(t / dick * 83, 0, 83).astype(np.int32)
    u = np.mod(s * (84.0 / dick), 1024).astype(np.int32)
    rgba = np.dstack([src[v, u], maske * 255.0]).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').save(ZIEL + '/bogen.webp', lossless=True, method=6)


def pergament():
    """Ein Streifen 2:1 des Pergaments, nur Farbe: etwas wärmer und dunkler."""
    p = Image.open(Q + '/commons_pergament/Pergament.1.jpg').convert('RGB').crop((0, 200, 1024, 712))
    a = np.asarray(p, dtype=np.float32) / 255.0
    speichere(a * np.array([0.93, 0.86, 0.70], dtype=np.float32), 'pergament.webp', 82)


if __name__ == '__main__':
    holz()
    fachholz()
    g = gesims()
    print('Rahmen (Kantenlänge, Rand):', rahmen(g))
    bogen(g)
    pergament()
    print('fertig')
