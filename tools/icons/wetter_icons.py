"""
Zeichnet die Wettersymbole der Zeittafel (PR #195) im Stil des kolorierten Holzschnitts – mit den Werkzeugen von kartenwerkstatt_icons.py
(eigene Grafiken, MIT-Lizenz wie das Projekt): schwarzer Umriss, Schraffur im Schatten, Farbe leicht neben den Linien.

Aufruf (aus dem Projektordner):
    ~/werkzeuge/blender/blender.sh -b --python tools/icons/wetter_icons.py
    -> schreibt game/assets/oberflaeche/icons/wetter/<lage>.png (48 x 48, durchsichtig), eine Datei je Wetterlage (klar, wolkig, regen,
       sturm, gewitter, schnee, nebel) – die Namen sind die Wetterlagen in Kleinbuchstaben (Wetter.cs).
    ... -- <ordner> schreibt stattdessen in einen anderen Ordner, "--vorschau <bild.png>" dazu eine vergrößerte Übersicht.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kartenwerkstatt_icons import Bild, c, schreiben, WEISS, STEIN, WASSER  # noqa: E402

SONNE = c(226, 178, 60)
WOLKE = c(222, 220, 212)
WOLKE_D = c(132, 134, 140)
BLITZ = c(240, 200, 70)


def sonne(b, cx, cy, r):
    import numpy as np
    for i in range(8):
        w = i * np.pi / 4
        b.l([(cx + np.cos(w) * (r + 2), cy + np.sin(w) * (r + 2)), (cx + np.cos(w) * (r + 7), cy + np.sin(w) * (r + 7))], 2.4, SONNE, schraffur=0)
    b.e(cx, cy, r, r, SONNE, schraffur=0.35)


def wolke(b, x, y, farbe=WOLKE, s=1.0):
    """Eine Wolke aus vier Bäuchen mit flachem Boden; (x, y) = linke untere Ecke."""
    maske = (b.ellipse(x + 8 * s, y - 6 * s, 7 * s, 6 * s) | b.ellipse(x + 17 * s, y - 11 * s, 9 * s, 9 * s)
             | b.ellipse(x + 27 * s, y - 8 * s, 8 * s, 7 * s) | b.ellipse(x + 33 * s, y - 5 * s, 5 * s, 5 * s)
             | b.vieleck([(x + 3 * s, y - 6 * s), (x + 36 * s, y - 6 * s), (x + 36 * s, y), (x + 3 * s, y)]))
    b.mal(maske, farbe, schraffur=0.35)


def klar(b):
    sonne(b, 24, 24, 10)


def wolkig(b):
    sonne(b, 17, 17, 8)
    wolke(b, 7, 38)


def regen(b):
    wolke(b, 5, 26, WOLKE_D)
    for x in (12, 20, 28, 36):
        b.l([(x, 30), (x - 3, 41)], 2.2, WASSER, schraffur=0)


def sturm(b):
    wolke(b, 7, 22, WOLKE_D, 0.9)
    b.l([(4, 30), (30, 30), (36, 27), (34, 23)], 2.4, WEISS, schraffur=0)
    b.l([(8, 37), (38, 37), (44, 34), (42, 30)], 2.4, WEISS, schraffur=0)
    b.l([(4, 44), (24, 44)], 2.4, WEISS, schraffur=0)


def gewitter(b):
    wolke(b, 5, 24, c(96, 98, 108))
    b.v([(25, 22), (14, 37), (21, 37), (15, 47), (32, 32), (25, 32), (31, 22)], BLITZ, schraffur=0)


def schnee(b):
    wolke(b, 5, 26, WOLKE)
    for (x, y) in ((12, 33), (24, 37), (36, 33), (17, 43), (31, 43)):
        b.e(x, y, 2.6, 2.6, WEISS, schraffur=0, dicke=1.0)


def nebel(b):
    for (y, x0, x1) in ((12, 8, 36), (20, 4, 42), (28, 10, 44), (36, 5, 33)):
        b.l([(x0, y), (x1, y)], 4.5, STEIN, schraffur=0.25)


ICONS = {'klar': klar, 'wolkig': wolkig, 'regen': regen, 'sturm': sturm, 'gewitter': gewitter, 'schnee': schnee, 'nebel': nebel}


def alle():
    for name, f in ICONS.items():
        b = Bild()
        f(b)
        yield name, b.fertig()


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    vorschau = None
    if '--vorschau' in args:
        i = args.index('--vorschau')
        vorschau = args[i + 1]
        del args[i:i + 2]
    wurzel = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    ordner = args[0] if args else os.path.join(wurzel, 'game', 'assets', 'oberflaeche', 'icons', 'wetter')
    schreiben(list(alle()), ordner, vorschau)
