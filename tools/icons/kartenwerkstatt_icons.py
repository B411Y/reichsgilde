"""
Zeichnet die Icons der Kartenwerkstatt im Stil des kolorierten Holzschnitts (eigene Grafiken, MIT-Lizenz wie das Projekt):
schwarzer Umriss, Schraffur im Schatten (unten rechts), Farbe von Hand aufgetragen und leicht neben den Linien.

Aufruf (aus dem Projektordner; Pillow ist nicht nötig, das Skript braucht nur Blenders Python mit numpy):
    ~/werkzeuge/blender/blender.sh -b --python tools/icons/kartenwerkstatt_icons.py
    -> schreibt game/assets/oberflaeche/icons/kartenwerkstatt/<name>.png (48 x 48, durchsichtig)
    ... -- <ordner> schreibt stattdessen in einen anderen Ordner, "--vorschau <bild.png>" dazu eine vergrößerte Übersicht.

Jedes Icon ist eine kleine Funktion aus Grundformen (Vieleck, Ellipse, dicke Linie) auf einem 48er Raster. Das Skript zeichnet
vierfach überabgetastet, setzt Farbe und Tinte getrennt (die Farbe um einen Bruchteil eines Pixels versetzt) und rechnet herunter.
Neues Icon: Funktion schreiben, in ICONS eintragen – der Dateiname ist der Schlüssel.
"""
import os
import sys
import numpy as np

N = 48
S = 4            # Überabtastung
M = N * S
TINTE = np.array([0.13, 0.09, 0.07])
VERSATZ = (1.1 * S, 0.8 * S)   # die Farbe sitzt neben den Linien

def c(r, g, b):
    return np.array([r, g, b]) / 255.0

PAPIER = c(238, 226, 196)
WEISS = c(236, 230, 214)
ROT = c(176, 60, 46)
DACH = c(168, 74, 52)
BLAU = c(72, 112, 160)
WASSER = c(100, 150, 188)
GRUEN = c(104, 148, 74)
GRUEN_D = c(66, 106, 56)
BRAUN = c(138, 92, 54)
HOLZ = c(176, 128, 76)
STEIN = c(156, 150, 140)
GOLD = c(216, 172, 62)
ERDE = c(150, 112, 72)
SAND = c(212, 188, 136)
ROSA = c(214, 140, 140)
DUNKEL = c(82, 62, 48)

_yy, _xx = np.mgrid[0:M, 0:M]
_xx = (_xx + 0.5) / S
_yy = (_yy + 0.5) / S


class Bild:
    def __init__(self):
        self.farbe = np.zeros((M, M, 3))
        self.deck = np.zeros((M, M))
        self.tinte = np.zeros((M, M), bool)

    def mal(self, maske, fuell, schraffur=0.45, umriss=True, dicke=1.3):
        """Setzt eine Form: Farbe (versetzt), dann Tinte (Umriss, Schraffur) – spätere Formen decken frühere zu."""
        if not maske.any():
            return
        dx, dy = int(round(VERSATZ[0])), int(round(VERSATZ[1]))
        versetzt = np.roll(np.roll(maske, dy, axis=0), dx, axis=1)
        self.farbe[versetzt] = fuell
        self.deck[versetzt] = 1.0
        self.tinte[maske] = False
        if umriss:
            r = max(1, int(round(dicke * S)))
            innen = maske.copy()
            for ax, sh in ((0, r), (0, -r), (1, r), (1, -r)):
                innen &= np.roll(maske, sh, axis=ax)
            self.tinte |= maske & ~innen
        if schraffur > 0:
            ys, xs = np.nonzero(maske)
            x0, x1, y0, y1 = _xx[0, xs.min()], _xx[0, xs.max()], _yy[ys.min(), 0], _yy[ys.max(), 0]
            schatten = ((_xx - x0) / max(x1 - x0, 1) + (_yy - y0) / max(y1 - y0, 1)) > 2 * (1 - schraffur)
            strich = np.mod(_xx + _yy, 2.6) < 0.7
            r = max(1, int(round(0.8 * S)))
            kern = maske.copy()
            for ax, sh in ((0, r), (0, -r), (1, r), (1, -r)):
                kern &= np.roll(maske, sh, axis=ax)
            self.tinte |= kern & schatten & strich

    def fertig(self, spiegeln=False):
        a = self.deck.reshape(N, S, N, S).mean(axis=(1, 3))
        farbe = (self.farbe * self.deck[..., None]).reshape(N, S, N, S, 3).mean(axis=(1, 3))
        t = self.tinte.reshape(N, S, N, S).mean(axis=(1, 3))
        # Tinte liegt über der Farbe; die Farbe ist mit der Deckung gewichtet, also zurückrechnen
        vor = farbe * (1 - t[..., None]) + TINTE * t[..., None]
        alpha = a * (1 - t) + t
        rgba = np.concatenate([np.clip(vor / np.maximum(alpha[..., None], 1e-6), 0, 1), alpha[..., None]], axis=2)
        if spiegeln:
            rgba = rgba[:, ::-1]
        return rgba

    # Grundformen -> Masken
    @staticmethod
    def vieleck(pts):
        maske = np.zeros((M, M), bool)
        p = list(pts)
        for i in range(len(p)):
            (x0, y0), (x1, y1) = p[i], p[(i + 1) % len(p)]
            if y0 == y1:
                continue
            ueber = ((_yy >= min(y0, y1)) & (_yy < max(y0, y1)))
            xs = x0 + (_yy - y0) * (x1 - x0) / (y1 - y0)
            maske ^= ueber & (_xx < xs)
        return maske

    @staticmethod
    def ellipse(cx, cy, rx, ry):
        return ((_xx - cx) / rx) ** 2 + ((_yy - cy) / ry) ** 2 <= 1

    @staticmethod
    def strecke(pts, breite):
        maske = np.zeros((M, M), bool)
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            l2 = dx * dx + dy * dy
            t = np.clip(((_xx - x0) * dx + (_yy - y0) * dy) / l2, 0, 1) if l2 else 0
            maske |= (_xx - (x0 + t * dx)) ** 2 + (_yy - (y0 + t * dy)) ** 2 <= (breite / 2) ** 2
        return maske

    def v(self, pts, farbe, **kw):
        self.mal(self.vieleck(pts), farbe, **kw)

    def e(self, cx, cy, rx, ry, farbe, **kw):
        self.mal(self.ellipse(cx, cy, rx, ry), farbe, **kw)

    def l(self, pts, breite, farbe, **kw):
        self.mal(self.strecke(pts, breite), farbe, **kw)

    def r(self, x0, y0, x1, y1, farbe, **kw):
        self.v([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], farbe, **kw)


# ── Gruppen ───────────────────────────────────────────────────────────────────

def auswaehlen(b):
    b.v([(14, 6), (14, 38), (21, 31), (27, 43), (32, 41), (26, 29), (36, 29)], WEISS)

def gelaende(b):
    b.r(4, 36, 44, 43, GRUEN, schraffur=0.3)
    b.v([(4, 38), (19, 11), (34, 38)], STEIN)
    b.v([(19, 11), (15, 19), (19, 17), (23, 20)], WEISS, schraffur=0)
    b.v([(24, 38), (35, 20), (46, 38)], ERDE)

def boden(b):
    b.v([(5, 16), (43, 16), (43, 40), (5, 40)], ERDE)
    b.v([(5, 16), (43, 16), (43, 24), (5, 24)], GRUEN, schraffur=0.3)
    b.e(14, 33, 3, 2, STEIN, schraffur=0.2)
    b.e(30, 31, 4, 2.5, STEIN, schraffur=0.2)
    b.v([(34, 10), (36, 17), (32, 17)], GRUEN_D, schraffur=0)

def wasser(b):
    b.v([(24, 4), (36, 26), (36, 34), (30, 42), (18, 42), (12, 34), (12, 26)], WASSER)
    b.l([(18, 32), (22, 36), (26, 32), (30, 36)], 2, WEISS, umriss=False, schraffur=0)

def wege(b):
    b.v([(8, 44), (40, 44), (29, 6), (19, 6)], SAND)
    for y in (38, 26, 14):
        b.v([(23, y), (25, y), (25, y - 6), (23, y - 6)], BRAUN, schraffur=0)

def flaechen(b):
    b.v([(5, 20), (28, 8), (43, 20), (36, 42), (9, 38)], GRUEN)
    b.r(23, 27, 26, 36, BRAUN, schraffur=0)
    b.e(24.5, 22, 7, 7, GRUEN_D)

def gebaeude(b):
    b.r(9, 22, 39, 42, WEISS)
    b.v([(4, 24), (24, 6), (44, 24)], DACH)
    b.r(20, 30, 28, 42, BRAUN, schraffur=0.3)
    b.r(30, 27, 35, 32, WASSER, schraffur=0)

def mauer(b):
    b.r(5, 18, 43, 42, STEIN)
    for x in (5, 16, 27, 38):
        b.r(x, 10, x + 6, 18, STEIN)
    b.l([(5, 30), (43, 30)], 0.8, TINTE, umriss=False, schraffur=0)
    b.l([(16, 18), (16, 30)], 0.8, TINTE, umriss=False, schraffur=0)
    b.l([(30, 30), (30, 42)], 0.8, TINTE, umriss=False, schraffur=0)

def start(b):
    b.l([(13, 6), (13, 44)], 3, BRAUN, schraffur=0)
    b.v([(15, 7), (40, 13), (15, 22)], ROT)

def pruefen(b):
    b.e(24, 24, 18, 18, WEISS)
    b.v([(13, 24), (17, 20), (22, 26), (32, 14), (36, 18), (22, 34)], GRUEN)


# ── Gelände ───────────────────────────────────────────────────────────────────

def pfeil(b, hoch, farbe=ROT):
    y0, y1 = (4, 20) if hoch else (44, 28)
    if hoch:
        b.v([(24, y0), (33, y1), (28, y1), (28, 26), (20, 26), (20, y1), (15, y1)], farbe)
    else:
        b.v([(24, y0), (33, y1), (28, y1), (28, 22), (20, 22), (20, y1), (15, y1)], farbe)

def heben(b):
    b.e(24, 38, 20, 10, GRUEN)
    b.e(24, 30, 12, 12, GRUEN, umriss=False, schraffur=0)
    b.v([(24, 4), (33, 16), (28, 16), (28, 24), (20, 24), (20, 16), (15, 16)], ROT)

def senken(b):
    b.r(4, 14, 44, 24, GRUEN, schraffur=0.2)
    b.e(24, 24, 12, 6, DUNKEL)
    b.v([(24, 44), (33, 33), (28, 33), (28, 26), (20, 26), (20, 33), (15, 33)], ROT)

def glaetten(b):
    b.v([(4, 40), (4, 31), (12, 27), (20, 31), (28, 27), (36, 31), (44, 28), (44, 40)], GRUEN)
    b.r(6, 15, 42, 21, GOLD)
    b.l([(24, 21), (24, 28)], 2, BRAUN, schraffur=0)

def ebnen(b):
    b.v([(5, 42), (5, 32), (15, 32), (15, 24), (25, 24), (25, 16), (43, 16), (43, 42)], GRUEN)
    b.l([(5, 32), (15, 32), (15, 24), (25, 24)], 1, TINTE, umriss=False, schraffur=0)

def rauschen(b):
    b.v([(4, 40), (4, 30), (9, 20), (15, 29), (20, 14), (26, 27), (32, 18), (37, 28), (44, 22), (44, 40)], ERDE)
    for x, y in ((12, 35), (24, 36), (36, 35)):
        b.e(x, y, 2, 1.5, STEIN, schraffur=0, umriss=False)

def rampe(b):
    b.v([(4, 42), (44, 42), (44, 10)], SAND)
    b.l([(10, 38), (38, 18)], 3, BRAUN, schraffur=0, umriss=False)

def radierer(b):
    b.v([(8, 32), (26, 12), (40, 24), (22, 44)], ROSA)
    b.v([(8, 32), (14, 38), (32, 18), (26, 12)], WEISS, schraffur=0)

def pinsel(b):
    b.l([(10, 40), (30, 14)], 5, HOLZ)
    b.l([(28, 16), (36, 6)], 7, GOLD)
    b.v([(10, 40), (6, 44), (14, 40)], GRUEN_D, schraffur=0)
    b.e(9, 42, 4, 3, GRUEN_D, schraffur=0)

def pflanze(b):
    b.r(22, 28, 27, 44, BRAUN, schraffur=0.5)
    b.e(24, 20, 14, 15, GRUEN)


# ── Wege ──────────────────────────────────────────────────────────────────────

def weg(b, breite, farbe, strich=False, rand=None):
    pts = [(11, 38), (22, 28), (28, 20), (38, 10)]
    if rand is not None:
        b.l(pts, breite + 5, rand, schraffur=0)
    b.l(pts, breite, farbe, schraffur=0.5)
    if strich:
        for p in ((15, 35), (23, 27), (33, 14)):
            b.e(p[0], p[1], 1.5, 1.5, DUNKEL, umriss=False, schraffur=0)

def weg_land(b):   weg(b, 12, SAND, rand=GRUEN)
def weg_haupt(b):  weg(b, 10, STEIN, strich=True, rand=GRUEN)
def weg_gasse(b):  weg(b, 8, WEISS, rand=GRUEN)
def weg_feld(b):   weg(b, 6, ERDE, rand=GRUEN)
def weg_fuss(b):   weg(b, 4, ERDE, strich=True)


# ── Flächen ───────────────────────────────────────────────────────────────────

def bauzone(b):
    b.v([(5, 16), (30, 8), (43, 20), (34, 42), (8, 38)], SAND)
    b.r(18, 22, 30, 32, WEISS)
    b.v([(16, 24), (24, 15), (32, 24)], DACH)

def bauzone_kern(b):
    """Kern: Steinfläche mit zwei Häuschen."""
    b.v([(5, 16), (30, 8), (43, 20), (34, 42), (8, 38)], STEIN)
    b.r(12, 20, 21, 29, WEISS)
    b.v([(10, 22), (16.5, 14), (23, 22)], DACH)
    b.r(26, 28, 34, 36, WEISS)
    b.v([(24, 30), (30, 23), (36, 30)], DACH)

def bauzone_mauerring(b):
    """Mauerring: helle Fläche mit Häuschen hinter einem Stück Zinnenmauer am unteren Rand."""
    b.v([(5, 14), (30, 6), (43, 18), (38, 38), (6, 36)], WEISS)
    b.r(14, 15, 22, 23, SAND)
    b.v([(12, 17), (18, 10), (24, 17)], DACH)
    b.r(8, 27, 38, 35, STEIN)
    for x in (9, 17, 25, 33):
        b.r(x, 22, x + 5, 27, STEIN)
    b.l([(8, 31), (38, 31)], 0.8, TINTE, umriss=False, schraffur=0)

def bauzone_vorstadt(b):
    """Vorstadt: Sandfläche mit einem Häuschen (wie die bisherige Bauzone)."""
    bauzone(b)

def bauzone_umland(b):
    """Umland: grüne Fläche mit Zaun unten."""
    b.v([(5, 16), (30, 8), (43, 20), (34, 42), (8, 38)], GRUEN)
    b.l([(10, 31), (37, 30)], 1.4, HOLZ, schraffur=0)
    for x in (12, 21, 30, 36):
        b.l([(x, 28), (x, 36)], 1.6, BRAUN, schraffur=0)

def wald(b):
    for x, y, h in ((13, 40, 22), (35, 40, 22), (24, 36, 28)):
        b.r(x - 1.5, y - 4, x + 1.5, y + 4, BRAUN, schraffur=0)
        b.v([(x, y - h), (x + 9, y - 3), (x - 9, y - 3)], GRUEN_D)

def streu(b):
    b.e(16, 32, 10, 8, GRUEN)
    b.e(34, 36, 6, 4, STEIN)
    b.e(30, 20, 3, 2, STEIN, schraffur=0.2)
    b.l([(38, 8), (38, 22)], 1.2, GOLD, umriss=False, schraffur=0)

def feld(b):
    b.v([(5, 18), (30, 8), (43, 20), (36, 42), (9, 38)], ERDE)
    for k in range(5):
        o = 5 + k * 6
        b.l([(8 + o * 0.35, 36 - o * 0.1), (28 + o * 0.35, 12 + o * 0.5)], 1.6, GOLD, umriss=False, schraffur=0)

def aussparung(b):
    b.v([(5, 18), (30, 8), (43, 20), (36, 42), (9, 38)], GRUEN)
    b.e(23, 26, 9, 7, DUNKEL, schraffur=0.7)

def tor(b):
    b.r(8, 8, 40, 44, STEIN)
    b.e(24, 28, 9, 9, DUNKEL, schraffur=0, umriss=True)
    b.r(15, 28, 33, 44, DUNKEL, schraffur=0, umriss=False)
    b.l([(24, 20), (24, 44)], 1, BRAUN, umriss=False, schraffur=0)


# ── Wasser ────────────────────────────────────────────────────────────────────

def fluss(b):
    b.l([(12, 8), (30, 16), (16, 30), (34, 40)], 11, WASSER)
    b.l([(12, 8), (30, 16), (16, 30), (34, 40)], 2, WEISS, umriss=False, schraffur=0)

def bach(b):
    b.l([(10, 8), (30, 18), (18, 32), (36, 40)], 5, WASSER)
    b.e(28, 30, 3, 2, STEIN, schraffur=0)
    b.e(14, 18, 3, 2, STEIN, schraffur=0)

def see(b):
    b.e(24, 26, 20, 13, WASSER)
    b.l([(10, 28), (16, 31)], 1.5, WEISS, umriss=False, schraffur=0)
    for x in (38, 41):
        b.l([(x, 14), (x, 28)], 1.5, GRUEN_D, umriss=False, schraffur=0)

def teich(b):
    b.e(24, 28, 13, 8, WASSER)
    b.e(22, 27, 4, 2.5, GRUEN, schraffur=0)

def faehre(b):
    b.v([(6, 34), (42, 34), (36, 42), (12, 42)], BRAUN)
    b.l([(24, 34), (24, 6)], 2, DUNKEL, schraffur=0)
    b.v([(26, 8), (26, 30), (38, 30)], WEISS)
    b.l([(4, 44), (12, 44), (18, 46), (26, 44), (34, 46), (44, 44)], 1.5, WASSER, umriss=False, schraffur=0)

def hafen(b):
    b.r(4, 28, 34, 34, HOLZ)
    for x in (6, 18, 31):
        b.r(x, 34, x + 3, 44, BRAUN, schraffur=0.5)
    b.l([(38, 8), (38, 28)], 3, DUNKEL, schraffur=0)
    b.l([(38, 8), (26, 14)], 2, DUNKEL, schraffur=0)
    b.l([(26, 14), (26, 24)], 1, TINTE, umriss=False, schraffur=0)


# ── Kopfleiste ────────────────────────────────────────────────────────────────

def neu(b):
    b.v([(10, 4), (30, 4), (38, 12), (38, 44), (10, 44)], PAPIER)
    b.v([(30, 4), (30, 12), (38, 12)], WEISS, schraffur=0)
    b.l([(16, 20), (32, 20)], 1.2, DUNKEL, umriss=False, schraffur=0)
    b.l([(16, 26), (32, 26)], 1.2, DUNKEL, umriss=False, schraffur=0)

def oeffnen(b):
    b.v([(4, 12), (18, 12), (22, 17), (44, 17), (44, 40), (4, 40)], GOLD)
    b.v([(4, 40), (9, 24), (46, 24), (44, 40)], c(232, 196, 90))

def speichern(b):
    b.r(8, 8, 40, 44, ROT)
    b.r(14, 8, 34, 22, PAPIER, schraffur=0.2)
    b.r(15, 30, 33, 44, WEISS, schraffur=0.2)
    b.r(28, 11, 31, 19, DUNKEL, schraffur=0, umriss=False)

def speichern_unter(b):
    speichern(b)
    b.v([(34, 30), (44, 20), (47, 23), (37, 33), (32, 35)], GOLD)

def rueckgaengig(b):
    b.v([(6, 18), (18, 6), (18, 14), (30, 14), (42, 24), (42, 40), (36, 40), (36, 28), (28, 22), (18, 22), (18, 30)], BLAU)

def netz(b):
    for a, z in (((10, 12), (36, 10)), ((10, 12), (14, 38)), ((36, 10), (38, 36)), ((14, 38), (38, 36)), ((10, 12), (38, 36))):
        b.l([a, z], 2, ROSA, schraffur=0)
    for p in ((10, 12), (36, 10), (14, 38), (38, 36)):
        b.e(p[0], p[1], 4, 4, GOLD, schraffur=0.3)

def ausprobieren(b):
    b.e(24, 24, 19, 19, GRUEN)
    b.v([(19, 13), (36, 24), (19, 35)], WEISS)

def umland(b):
    b.r(8, 22, 34, 42, HOLZ)
    b.v([(5, 24), (21, 8), (37, 24)], DACH)
    b.r(15, 30, 26, 42, DUNKEL, schraffur=0, umriss=False)
    b.l([(40, 10), (40, 42)], 1.4, GOLD, umriss=False, schraffur=0)
    b.l([(44, 14), (44, 42)], 1.4, GOLD, umriss=False, schraffur=0)


ICONS = {
    # Gruppen
    'auswaehlen': auswaehlen, 'gelaende': gelaende, 'boden': boden, 'wasser': wasser, 'wege': wege,
    'flaechen': flaechen, 'gebaeude': gebaeude, 'mauer': mauer, 'start': start, 'pruefen': pruefen,
    # Werkzeuge
    'heben': heben, 'senken': senken, 'glaetten': glaetten, 'ebnen': ebnen, 'rauschen': rauschen, 'rampe': rampe,
    'radierer': radierer, 'pinsel': pinsel, 'pflanze': pflanze,
    'weg_land': weg_land, 'weg_haupt': weg_haupt, 'weg_gasse': weg_gasse, 'weg_feld': weg_feld, 'weg_fuss': weg_fuss,
    'bauzone': bauzone, 'bauzone_kern': bauzone_kern, 'bauzone_mauerring': bauzone_mauerring,
    'bauzone_vorstadt': bauzone_vorstadt, 'bauzone_umland': bauzone_umland,
    'wald': wald, 'streu': streu, 'feld': feld, 'aussparung': aussparung, 'tor': tor,
    'fluss': fluss, 'bach': bach, 'see': see, 'teich': teich, 'faehre': faehre, 'hafen': hafen,
    'umland': umland,
    # Kopfleiste
    'neu': neu, 'oeffnen': oeffnen, 'speichern': speichern, 'speichern_unter': speichern_unter,
    'rueckgaengig': rueckgaengig, 'netz': netz, 'ausprobieren': ausprobieren,
}
SPIEGEL = {'wiederholen': 'rueckgaengig'}


def alle():
    for name, f in ICONS.items():
        b = Bild()
        f(b)
        yield name, b.fertig()
    for name, quelle in SPIEGEL.items():
        b = Bild()
        ICONS[quelle](b)
        yield name, b.fertig(spiegeln=True)


def schreiben(bilder, ordner, vorschau):
    import bpy
    os.makedirs(ordner, exist_ok=True)
    for name, rgba in bilder:
        img = bpy.data.images.new(name, N, N, alpha=True)
        img.pixels = np.flipud(rgba).astype(np.float32).ravel().tolist()   # Blender zählt Zeilen von unten
        img.filepath_raw = os.path.join(ordner, name + '.png')
        img.file_format = 'PNG'
        img.save()
    if vorschau:
        k = 8
        spalten = 8
        zeilen = (len(bilder) + spalten - 1) // spalten
        w, h = spalten * N * 2, zeilen * N * 2
        blatt = np.ones((h, w, 4)) * np.array([0.16, 0.15, 0.13, 1])
        for i, (_, rgba) in enumerate(bilder):
            gross = np.kron(rgba, np.ones((2, 2, 1)))
            y, x = (i // spalten) * N * 2, (i % spalten) * N * 2
            a = gross[..., 3:4]
            blatt[y:y + N * 2, x:x + N * 2, :3] = gross[..., :3] * a + blatt[y:y + N * 2, x:x + N * 2, :3] * (1 - a)
        img = bpy.data.images.new('vorschau', w, h, alpha=True)
        img.pixels = np.flipud(blatt).astype(np.float32).ravel().tolist()
        img.filepath_raw = vorschau
        img.file_format = 'PNG'
        img.save()


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    vorschau = None
    if '--vorschau' in args:
        i = args.index('--vorschau')
        vorschau = args[i + 1]
        del args[i:i + 2]
    wurzel = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    ordner = args[0] if args else os.path.join(wurzel, 'game', 'assets', 'oberflaeche', 'icons', 'kartenwerkstatt')
    schreiben(list(alle()), ordner, vorschau)
