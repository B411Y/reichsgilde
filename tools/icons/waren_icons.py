#!/usr/bin/env python3
"""
Zeichnet die Waren-Icons von Reichsgilde im Stil eines kolorierten Holzschnitts (eigene Grafiken, MIT-Lizenz wie das Projekt).

Aufruf (aus dem Projektordner, braucht Pillow und numpy):
    python3 tools/icons/waren_icons.py                     # schreibt game/assets/waren/<ware>.png (128 × 128)
    python3 tools/icons/waren_icons.py --vorschau x.png    # zusätzlich eine Übersicht aller Icons
    python3 tools/icons/waren_icons.py --nur Truhe,Kerze   # nur diese Waren zeichnen (zum Ausprobieren)

Jede Ware ist eine kleine Zeichenfunktion aus Formen (Vieleck, Ellipse, Rechteck, Ring, Blatt, Balken) im
64er-Raster: (0, 0) ist oben links, (64, 64) unten rechts. Das Skript zeichnet achtfach vergrößert (512 px)
und verkleinert am Ende auf 128 px – so werden die Kanten glatt.
Wie in einem Druck um 1480 (Schedelsche Weltchronik) bekommt jede Fläche einen schwarzen Umriss, im Schatten
(Licht von links oben) eine schräge Schraffur, die zum Dunkel hin dicker wird, und darunter eine von Hand
aufgetragene Wasserfarbe, die leicht neben den Linien sitzt und ungleich deckt. Eine leichte Verbiegung
aller Formen lässt die Linien wie geschnitten wirken.
Neue Ware: Funktion schreiben und in WAREN eintragen – der Dateiname entsteht aus dem Warennamen
(„Kräuter“ → kraeuter.png), genauso wie im Spiel (WarenIcons.cs).
"""
import math
import os
import random
import sys
from multiprocessing import Pool

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

G = 64            # Entwurfsraster
S = 8             # Überabtastung
W = G * S         # Zeichenfläche in Pixeln
AUS = 128         # Größe der fertigen Icons

# ── Farben (an mittelalterliche Pigmente angelehnt) ───────────────────────────
GOLD = (222, 172, 62)
EISEN = (122, 126, 134)
EISEN_D = (84, 86, 94)
SILBER = (204, 210, 220)
HOLZ = (146, 96, 54)
HOLZ_H = (178, 126, 74)
HOLZ_D = (104, 66, 36)
STEIN = (158, 150, 138)
STEIN_H = (184, 176, 162)
STEIN_D = (118, 112, 104)
GRANIT = (132, 126, 136)
KRAUT = (88, 138, 60)
KRAUT_H = (126, 170, 82)
KRAUT_D = (60, 100, 42)
KROETE = (112, 128, 54)
KRAPPROT = (150, 36, 32)
ZINNOBER = (206, 64, 40)
ROSA = (224, 140, 162)
GLAS = (184, 212, 220)
GIFTGRUEN = (92, 172, 96)
LEDER = (112, 64, 38)
TON = (178, 110, 70)
WEISS = (240, 234, 218)
BLAU = (44, 74, 156)           # Azurit
EDELSTEIN = (52, 98, 192)
VIOLETT = (112, 60, 132)
PERGAMENT = (234, 222, 188)
PERGAMENT_D = (204, 186, 142)
SCHAUM = (242, 236, 216)
DUNKEL = (36, 26, 22)
LICHT = (255, 252, 240)        # Glanzpunkte

SCHWARZ = (22, 18, 16)         # Druckerschwärze
PAPIER = (238, 228, 202)


# ── Zeichnung: sammelt die Formen als Masken ─────────────────────────────────
class Zeichnung:
    """Formen in Zeichenreihenfolge. Arten: „flaeche“ (koloriert, schraffiert, mit Umriss), „tupfen“ (nur Farbe),
    „strich“ (schwarze Linie). Was später gezeichnet wird, verdeckt Früheres."""

    def __init__(self):
        self.ebenen = []
        self.drehung = None

    def drehen(self, winkel, um=(32, 32), mass=1.0):
        """Alle folgenden Formen um `um` drehen (Grad, im Uhrzeigersinn) und mit `mass` skalieren – bis gerade()."""
        self.drehung = (math.radians(winkel), um, mass)

    def gerade(self):
        self.drehung = None

    def _t(self, p):
        x, y = p
        if self.drehung:
            w, (cx, cy), m = self.drehung
            dx, dy = (x - cx) * m, (y - cy) * m
            x = cx + dx * math.cos(w) - dy * math.sin(w)
            y = cy + dx * math.sin(w) + dy * math.cos(w)
        return (x * S, y * S)

    def _mass(self):
        return self.drehung[2] if self.drehung else 1.0

    @staticmethod
    def _maske(malen):
        im = Image.new("L", (W, W), 0)
        malen(ImageDraw.Draw(im))
        return np.array(im) > 127

    def _ablegen(self, maske, farbe, kontur):
        self.ebenen.append((maske, farbe, "flaeche" if kontur else "tupfen"))

    @staticmethod
    def ellipsenpunkte(cx, cy, rx, ry, von=0, bis=360, n=72):
        return [(cx + rx * math.cos(math.radians(von + (bis - von) * i / n)),
                 cy + ry * math.sin(math.radians(von + (bis - von) * i / n))) for i in range(n + 1)]

    # ── Formen ───────────────────────────────────────────────────────────────
    def vieleck(self, punkte, farbe, kontur=True):
        pts = [self._t(p) for p in punkte]
        self._ablegen(self._maske(lambda d: d.polygon(pts, fill=255)), farbe, kontur)

    def ellipse(self, cx, cy, rx, ry, farbe, kontur=True):
        self.vieleck(self.ellipsenpunkte(cx, cy, rx, ry), farbe, kontur)

    def rechteck(self, x0, y0, x1, y1, farbe, kontur=True):
        self.vieleck([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], farbe, kontur)

    def ring(self, cx, cy, rx, ry, dicke, farbe, von=0, bis=360, kontur=True):
        """Ellipsenring; mit von/bis (Grad, 0 = rechts, 90 = unten) nur ein Bogenstück."""
        aussen = self.ellipsenpunkte(cx, cy, rx, ry, von, bis)
        innen = self.ellipsenpunkte(cx, cy, rx - dicke, ry - dicke, von, bis)
        if bis - von >= 360:
            a = self._maske(lambda d: d.polygon([self._t(p) for p in aussen], fill=255))
            i = self._maske(lambda d: d.polygon([self._t(p) for p in innen], fill=255))
            self._ablegen(a & ~i, farbe, kontur)
        else:
            self.vieleck(aussen + innen[::-1], farbe, kontur)

    def blatt(self, x, y, laenge, breite, winkel, farbe, kontur=True):
        """Spitzes Blatt vom Ansatz (x, y) aus in Richtung `winkel` (Grad, 0 = rechts, -90 = oben)."""
        w = math.radians(winkel)
        ux, uy, nx, ny = math.cos(w), math.sin(w), -math.sin(w), math.cos(w)
        links, rechts = [], []
        for i in range(17):
            t = i / 16
            b = breite / 2 * math.sin(math.pi * t) ** 0.8
            px, py = x + ux * laenge * t, y + uy * laenge * t
            links.append((px + nx * b, py + ny * b))
            rechts.append((px - nx * b, py - ny * b))
        self.vieleck(links + rechts[::-1], farbe, kontur)

    def _linienmaske(self, punkte, dicke):
        pts = [self._t(p) for p in punkte]
        r = dicke * S / 2 * self._mass()

        def malen(d):
            d.line(pts, fill=255, width=max(1, int(round(r * 2))), joint="curve")
            for x, y in (pts[0], pts[-1]):
                d.ellipse((x - r, y - r, x + r, y + r), fill=255)
        return self._maske(malen)

    def balken(self, punkte, dicke, farbe, kontur=True):
        """Dicke Linie als Fläche (Stiel, Griff, Bügel)."""
        self._ablegen(self._linienmaske(punkte, dicke), farbe, kontur)

    def strich(self, punkte, dicke=0.8, farbe=None):
        """Binnenzeichnung (Maserung, Falten, Schrift): schwarz, mit `farbe` als farbige Linie."""
        if farbe is None:
            self.ebenen.append((self._linienmaske(punkte, dicke), None, "strich"))
        else:
            self._ablegen(self._linienmaske(punkte, dicke), farbe, False)

    def kreis(self, cx, cy, rx, ry, dicke=0.8, farbe=None, von=0, bis=360):
        """Ellipse als schwarze Linie (Jahresringe, Bohrlöcher)."""
        self.strich(self.ellipsenpunkte(cx, cy, rx, ry, von, bis), dicke, farbe)

    def punkt(self, x, y, r=0.8, farbe=None):
        """Schwarzer Punkt, mit `farbe` ein farbiger Tupfen (Glanzlicht, Niete, Warze)."""
        maske = self._linienmaske([(x, y), (x, y)], r * 2)
        if farbe is None:
            self.ebenen.append((maske, None, "strich"))
        else:
            self._ablegen(maske, farbe, False)


# ── Malen im Stil des kolorierten Holzschnitts ───────────────────────────────
def _rauschen(rng, zellen):
    klein = (rng.random((zellen, zellen)) * 255).astype(np.uint8)
    return np.array(Image.fromarray(klein).resize((W, W), Image.BICUBIC), dtype=np.float32) / 255 - 0.5


def _verbiegen(maske, dx, dy):
    yy, xx = np.mgrid[0:W, 0:W]
    return maske[np.clip((yy + dy).astype(int), 0, W - 1), np.clip((xx + dx).astype(int), 0, W - 1)]


def _schieben(a, dx, dy):
    """b[y, x] = a[y + dy, x + dx], außerhalb 0."""
    b = np.zeros_like(a)
    h, w = a.shape
    ys, yd = (slice(dy, h), slice(0, h - dy)) if dy >= 0 else (slice(0, h + dy), slice(-dy, h))
    xs, xd = (slice(dx, w), slice(0, w - dx)) if dx >= 0 else (slice(0, w + dx), slice(-dx, w))
    b[yd, xd] = a[ys, xs]
    return b


def _als_bild(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L")


def _erodieren(m, px):
    return np.array(_als_bild(m.astype(np.float32)).filter(ImageFilter.MinFilter(2 * px + 1))) > 127


def _dehnen(m, px):
    return np.array(_als_bild(m.astype(np.float32)).filter(ImageFilter.MaxFilter(2 * px + 1))) > 127


def _weich(a, r):
    return np.array(_als_bild(a).filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255


def _licht(maske, r=26, k=20):
    """hell an der Kante oben links, dunkel an der Kante unten rechts (0 … 1)."""
    w = _weich(maske.astype(np.float32), r)
    hell = maske * np.clip(1 - _schieben(w, -k, -k) * 1.6, 0, 1)
    dunkel = maske * np.clip(1 - _schieben(w, k, k) * 1.6, 0, 1)
    return hell, dunkel


class _Leinwand:
    def __init__(self):
        self.p = np.zeros((W, W, 3), np.float32)   # Farbe, mit Deckung vormultipliziert
        self.a = np.zeros((W, W), np.float32)

    def malen(self, maske, farbe, deckung=1.0):
        m = np.clip(maske.astype(np.float32) * deckung, 0, 1)[..., None]
        f = np.broadcast_to(np.asarray(farbe, np.float32) / 255, self.p.shape)
        self.p = f * m + self.p * (1 - m)
        self.a = m[..., 0] + self.a * (1 - m[..., 0])

    def bild(self):
        arr = np.dstack([np.clip(self.p, 0, 1), np.clip(self.a, 0, 1)])
        im = Image.fromarray((arr * 255).astype(np.uint8), "RGBa")
        return im.resize((AUS, AUS), Image.LANCZOS).convert("RGBA")


def malen(z, saat):
    rng = np.random.default_rng(saat)
    wx, wy = _rauschen(rng, 6) * 6, _rauschen(rng, 6) * 6        # geschnittene Linien: alles leicht verbogen
    ebenen = [(_verbiegen(m, wx, wy), f, art) for m, f, art in z.ebenen]

    # Jede Form nur dort, wo keine spätere Fläche sie verdeckt
    sichtbar = [None] * len(ebenen)
    darueber = np.zeros((W, W), bool)
    for i in reversed(range(len(ebenen))):
        m, _, art = ebenen[i]
        sichtbar[i] = m & ~darueber
        if art == "flaeche":
            darueber |= m
    umriss = darueber

    lw = _Leinwand()
    lw.malen(umriss, PAPIER)
    # Handkolorierung: Wasserfarbe leicht neben den Linien, ungleich deckend
    vx, vy = _rauschen(rng, 5) * 14 + 4, _rauschen(rng, 5) * 14 + 3
    fleck = _rauschen(rng, 12)
    for (m, farbe, art), v in zip(ebenen, sichtbar):
        if art == "strich":
            continue
        f = np.asarray(farbe, np.float32)
        lw.malen(_verbiegen(v, vx, vy) & umriss, f + (np.asarray(PAPIER, np.float32) - f) * 0.1, 0.9 + 0.3 * fleck)
    # Dünne Stellen (unter etwa 3,5 Rastereinheiten: Kettenglieder, Reifen, Stiele) bekommen feinere Linien und keine Schraffur,
    # sonst bliebe dort nur Schwarz übrig
    duenn = {i: v & ~_dehnen(_erodieren(v, 13), 13) for i, (v, e) in enumerate(zip(sichtbar, ebenen)) if e[2] == "flaeche"}
    alle_duenn = np.zeros((W, W), bool)
    for d in duenn.values():
        alle_duenn |= d
    # Schraffur im Schatten: schräge Linien, zum Dunkel hin dicker
    yy, xx = np.mgrid[0:W, 0:W]
    phase = np.abs(((xx - yy) % 16) - 8)
    for i, d in duenn.items():
        if sichtbar[i].sum() < 5000:                                 # kleine Flächen (Blütenblatt, Knauf) bleiben hell
            continue
        _, dunkel = _licht(ebenen[i][0], 30, 26)
        lw.malen(sichtbar[i] & ~d & (phase < np.clip(dunkel * 1.25 - 0.12, 0, 0.75) * 8), SCHWARZ)
    for i, d in duenn.items():
        v = sichtbar[i]
        lw.malen(v & ~np.where(d, _erodieren(v, 2), _erodieren(v, 4)), SCHWARZ)
    lw.malen(umriss & ~np.where(alle_duenn, _erodieren(umriss, 3), _erodieren(umriss, 8)), SCHWARZ)
    for (_, _, art), v in zip(ebenen, sichtbar):
        if art == "strich":
            lw.malen(v, SCHWARZ)
    return lw.bild()


# ── Hilfsformen ──────────────────────────────────────────────────────────────
def brocken(z, cx, cy, r, farbe, saat, ecken=9, zacken=0.22):
    """Unregelmäßiger Stein- oder Erzbrocken."""
    rnd = random.Random(saat)
    pts = []
    for i in range(ecken):
        w = 2 * math.pi * i / ecken + rnd.uniform(-0.2, 0.2)
        rr = r * (1 + rnd.uniform(-zacken, zacken))
        pts.append((cx + rr * math.cos(w), cy + rr * 0.82 * math.sin(w)))
    z.vieleck(pts, farbe)


def flasche(z, inhalt, korken=HOLZ_D, breit=15, hals=GLAS):
    z.ellipse(32, 42, breit, 15, GLAS)
    z.vieleck(Zeichnung.ellipsenpunkte(32, 42, breit - 2.5, 12.5, 15, 165), inhalt)   # Flüssigkeit, untere Hälfte
    z.ellipse(32, 44.5, breit - 2.5, 3.2, inhalt)
    z.rechteck(27.5, 13, 36.5, 29, hals)
    z.ellipse(32, 13, 6.5, 2, hals)
    z.rechteck(28, 6, 36, 13, korken)
    z.ellipse(32, 6, 4, 1.4, korken)
    z.balken([(22, 36), (21, 44)], 1.6, LICHT, kontur=False)          # Glanz auf dem Glas
    z.punkt(23, 32, 0.9, LICHT)


def aehren(z, farbe, dunkel, grannen):
    for x1 in (16, 32, 48):
        z.balken([(32, 58), (x1, 30)], 2.2, (150, 132, 64))
    for cx, w in ((16, -20), (32, 0), (48, 20)):
        z.drehen(w, um=(cx, 20))
        z.ellipse(cx, 20, 5, 12, dunkel)
        for i in range(5):
            for s in (-1, 1):
                z.ellipse(cx + s * 2.4, 11 + i * 4.2, 2.6, 2.6, farbe)
        if grannen:
            for s in (-1, 0, 1):
                z.strich([(cx + s * 2.5, 10), (cx + s * 6, 1)], 0.6)
        z.gerade()
    z.rechteck(26, 44, 38, 48, (154, 112, 62))                      # Bindfaden
    z.strich([(27, 46), (37, 46)], 0.5)


def humpen(z, koerper, band, schaum, inhalt):
    z.ring(46, 39, 10, 12, 4, koerper)                               # Henkel
    z.vieleck([(17, 22), (43, 22), (45, 57), (15, 57)], koerper)
    if koerper == HOLZ:
        for x0, x1 in ((22, 21.5), (30, 30), (38, 38.8)):
            z.strich([(x0, 31), (x1, 46)], 0.8)
    z.vieleck([(16.6, 26), (43.4, 26), (43.7, 31), (16.3, 31)], band)
    z.vieleck([(15.6, 47), (44.4, 47), (44.7, 52), (15.3, 52)], band)
    z.balken([(41, 24), (41.5, 33)], 2.6, inhalt)                     # übergelaufenes Bier
    z.ellipse(30, 21, 15, 4.5, schaum)
    z.ellipse(21, 17.5, 7, 5.5, schaum)
    z.ellipse(31, 14.5, 8.5, 7, schaum)
    z.ellipse(40, 18, 6.5, 5, schaum)


def buch(z, deckel, ruecken, schnitt):
    z.rechteck(17, 9, 52, 58, schnitt)                               # Buchblock
    for x in (49.5, 51):
        z.strich([(x, 11), (x, 56)], 0.5)
    z.rechteck(11, 6, 48, 60, deckel)
    z.rechteck(9, 6, 15, 60, ruecken)
    for y in (14, 33, 52):
        z.strich([(9, y), (15, y)], 0.8)


def eckbeschlaege(z, farbe):
    for (x, y), (dx, dy) in (((15, 6), (1, 1)), ((48, 6), (-1, 1)), ((15, 60), (1, -1)), ((48, 60), (-1, -1))):
        z.vieleck([(x, y), (x + 9 * dx, y), (x, y + 9 * dy)], farbe)


def truhe(z, holz, deckel, band, schloss):
    z.rechteck(10, 57, 15, 61, HOLZ_D)                               # Füße
    z.rechteck(49, 57, 54, 61, HOLZ_D)
    z.rechteck(8, 31, 56, 58, holz)
    for y in (38, 45, 52):
        z.strich([(9, y), (55, y)], 0.7)
    z.vieleck([(6, 31), (6, 22), (12, 15), (52, 15), (58, 22), (58, 31)], deckel)
    z.strich([(8, 23), (56, 23)], 0.7)
    for x in (14, 44):
        z.vieleck([(x, 15), (x + 6, 15), (x + 6, 58), (x, 58)], band)
        for y in (19, 27, 36, 46, 54):
            z.punkt(x + 3, y, 1.0)
    z.rechteck(27, 26, 37, 39, schloss)
    z.ellipse(32, 31, 1.8, 1.8, DUNKEL)
    z.vieleck([(31.2, 31), (32.8, 31), (33.4, 36), (30.6, 36)], DUNKEL)


def kette(z, glied, anhaenger, stein=None):
    """Halskette als U, abwechselnd flache und hochkant stehende Glieder, unten ein Anhänger."""
    n = 13
    for i in range(n):
        w = math.radians(-10 + i * 200 / (n - 1))
        x, y = 32 + 24 * math.cos(w), 18 + 21 * math.sin(w)
        richtung = math.degrees(math.atan2(21 * math.cos(w), -24 * math.sin(w)))
        z.drehen(richtung, um=(x, y))
        if i % 2 == 0:
            z.ring(x, y, 5, 3.8, 2.8, glied)
        else:
            z.balken([(x - 2.6, y), (x + 2.6, y)], 3.2, glied)
        z.gerade()
    z.balken([(32, 41), (32, 46)], 2.6, glied)
    z.ellipse(32, 53, 8, 8.5, anhaenger)
    if stein:
        z.ellipse(32, 53, 4.4, 4.8, stein)
        z.punkt(30.4, 51.2, 1.1, LICHT)
    else:
        z.kreis(32, 53, 5, 5.5, 0.6)


def klinge_senkrecht(z, unten, spitze, breite, farbe):
    z.vieleck([(32 - breite, unten), (32 - breite, spitze + breite * 2.5), (32, spitze),
               (32 + breite, spitze + breite * 2.5), (32 + breite, unten)], farbe)
    z.strich([(32, spitze + breite * 2.5), (32, unten - 2)], 0.8)


def dolch(z, klinge, parier, knauf, gift):
    z.drehen(30)
    klinge_senkrecht(z, 40, 4, 4.2, klinge)
    z.balken([(32, 44), (32, 54)], 5.5, LEDER)
    for y in (46, 49, 52):
        z.strich([(29.4, y), (34.6, y + 1.5)], 0.6)
    z.vieleck([(20, 39), (44, 39), (42, 44), (22, 44)], parier)
    z.ellipse(32, 58, 4.4, 4.4, knauf)
    z.gerade()
    for x, y, r in gift:
        z.ellipse(x, y, r * 0.8, r, GIFTGRUEN)


def duftflakon(z, inhalt, verschluss):
    z.vieleck([(32, 18), (50, 36), (32, 58), (14, 36)], GLAS)
    z.vieleck([(32, 24), (45, 37), (32, 52), (19, 37)], inhalt)
    z.rechteck(28, 10, 36, 19, GLAS)
    z.ellipse(32, 8, 6, 4.5, verschluss)
    z.punkt(32, 3.5, 1.6, verschluss)
    z.balken([(22, 33), (26, 29)], 1.6, LICHT, kontur=False)


# ── Rohstoffe ────────────────────────────────────────────────────────────────
def eisen(z):
    for (cx, cy, r), s in zip(((21, 42, 15), (44, 44, 13), (32, 24, 12)), (1, 2, 3)):
        brocken(z, cx, cy, r, EISEN, s)
    for x, y, r in ((18, 36, 2), (40, 46, 1.6), (30, 22, 1.6), (25, 46, 1.3)):
        z.ellipse(x, y, r, r * 0.8, (150, 74, 50), kontur=False)        # Rost
    for x, y in ((16, 41), (44, 38), (34, 28)):
        z.punkt(x, y, 0.9, (196, 202, 214))


def silber(z):
    for (cx, cy, r), s in zip(((21, 42, 15), (44, 44, 13), (32, 24, 12)), (4, 5, 6)):
        brocken(z, cx, cy, r, STEIN_D, s)
    for x, y, r in ((15, 40, 3.2), (24, 47, 2.6), (40, 41, 3.2), (48, 48, 2.4), (29, 22, 3), (37, 27, 2.2)):
        z.ellipse(x, y, r, r * 0.8, SILBER)                               # Silberkörner im Gestein
        z.punkt(x - r * 0.35, y - r * 0.3, 0.7, LICHT)


def gold(z):
    for dx, dy in ((0, 14), (-9, 0), (9, 0)):                            # drei Barren gestapelt
        x, y = 32 + dx, 26 + dy
        z.vieleck([(x - 14, y + 10), (x + 14, y + 10), (x + 16, y + 16), (x - 16, y + 16)], GOLD)
        z.vieleck([(x - 11, y + 2), (x + 11, y + 2), (x + 14, y + 10), (x - 14, y + 10)], GOLD)
    z.vieleck([(18, 16), (46, 16), (49, 22), (15, 22)], GOLD)
    z.vieleck([(21, 10), (43, 10), (46, 16), (18, 16)], GOLD)


def holz(z):
    for x0, y0 in ((4, 38), (32, 38), (18, 18)):                         # drei Stämme als Pyramide
        z.rechteck(x0, y0, x0 + 20, y0 + 17, HOLZ)
        z.strich([(x0 + 2, y0 + 5), (x0 + 14, y0 + 5)], 0.7)
        z.strich([(x0 + 4, y0 + 11), (x0 + 16, y0 + 12)], 0.7)
        z.ellipse(x0 + 21, y0 + 8.5, 5.5, 8.5, HOLZ_H)
        z.kreis(x0 + 21, y0 + 8.5, 3.4, 5.4, 0.6)
        z.kreis(x0 + 21, y0 + 8.5, 1.4, 2.2, 0.6)


def bruchstein(z):
    brocken(z, 19, 43, 15, STEIN, 11, ecken=6, zacken=0.15)
    brocken(z, 45, 45, 15, STEIN_D, 12, ecken=6, zacken=0.15)
    brocken(z, 32, 23, 13, STEIN_H, 13, ecken=6, zacken=0.15)
    for pts in (((14, 44), (20, 40)), ((40, 48), (46, 44)), ((28, 26), (34, 24))):
        z.strich(pts, 0.6)


def granit(z):
    z.vieleck([(8, 24), (38, 16), (56, 24), (26, 32)], (170, 162, 170))  # Oberseite
    z.vieleck([(8, 24), (26, 32), (26, 56), (8, 46)], GRANIT)
    z.vieleck([(26, 32), (56, 24), (56, 46), (26, 56)], (100, 96, 106))
    rnd = random.Random(7)
    for _ in range(26):
        x, y = rnd.uniform(10, 54), rnd.uniform(20, 52)
        z.punkt(x, y, 0.7, rnd.choice(((58, 54, 62), (214, 204, 210), (150, 120, 120))))


def kraeuter(z):
    stiele = [(-62, 26), (-78, 30), (-96, 30), (-112, 28), (-126, 22)]
    spitzen = [(32 + math.cos(math.radians(w)) * l, 52 + math.sin(math.radians(w)) * l) for w, l in stiele]
    for x, y in spitzen:
        z.balken([(32, 56), (x, y)], 2.2, KRAUT_D)
    for k, ((w, _), (x, y)) in enumerate(zip(stiele, spitzen)):
        for t, seite in ((0.45, 1), (0.62, -1), (0.8, 1)):
            bx, by = 32 + (x - 32) * t, 52 + (y - 52) * t
            z.blatt(bx, by, 14, 7, w + 50 * seite, KRAUT if (k + int(t * 10)) % 2 else KRAUT_H)
        z.blatt(x, y, 13, 7, w, KRAUT_H)
    z.rechteck(27.5, 48, 36.5, 53, (176, 146, 88))                       # Bindfaden
    z.strich([(28, 50.5), (36, 50.5)], 0.6)


def kroete(z):
    z.ellipse(13, 50, 8, 4, KROETE)                                      # Hinterbeine
    z.ellipse(51, 50, 8, 4, KROETE)
    z.ellipse(32, 40, 22, 14, KROETE)                                    # Leib
    z.ellipse(20, 26, 7, 7, KROETE)                                      # Augenwülste
    z.ellipse(44, 26, 7, 7, KROETE)
    z.ellipse(20, 26, 3.6, 3.6, GOLD)
    z.ellipse(44, 26, 3.6, 3.6, GOLD)
    z.rechteck(19, 25, 21, 27.5, DUNKEL, kontur=False)
    z.rechteck(43, 25, 45, 27.5, DUNKEL, kontur=False)
    z.strich([(22, 41), (32, 43.5), (42, 41)], 0.9)                      # Maul
    for x, y in ((16, 36), (48, 36), (26, 48), (38, 48), (32, 34), (22, 44), (42, 44)):
        z.punkt(x, y, 1.2, (152, 162, 74))                                # Warzen
    for x in (20, 44):                                                   # Vorderfüße
        z.ellipse(x, 54, 5, 2.6, KROETE)


# ── Zwischenwaren ────────────────────────────────────────────────────────────
def beschlag(z):
    z.vieleck([(6, 22), (28, 25), (28, 41), (6, 44)], EISEN)              # Scharnier: zwei Lappen und ein Bolzen
    z.vieleck([(34, 25), (52, 18), (58, 32), (52, 46), (34, 41)], EISEN)
    z.rechteck(28, 18, 34, 48, EISEN_D)
    for y in (22, 30, 38, 46):
        z.strich([(28, y), (34, y)], 0.5)
    for x, y in ((13, 33), (46, 26), (46, 38)):
        z.ellipse(x, y, 2.4, 2.4, (60, 62, 70))
        z.punkt(x - 0.7, y - 0.7, 0.6, (190, 194, 204))


def holzzapfen(z):
    for x, y in ((12, 16), (28, 24), (44, 32)):                           # drei Zapfen schräg
        z.balken([(x - 4, y + 22), (x + 4, y)], 8, HOLZ)
        z.ellipse(x + 4.5, y - 1, 4, 4, HOLZ_H)
        z.kreis(x + 4.5, y - 1, 1.8, 1.8, 0.5)
        z.strich([(x - 2, y + 16), (x + 2, y + 5)], 0.5)


def schleifziegel(z):
    z.vieleck([(8, 28), (36, 18), (56, 26), (28, 36)], (196, 128, 86))
    z.vieleck([(8, 28), (28, 36), (28, 50), (8, 42)], TON)
    z.vieleck([(28, 36), (56, 26), (56, 40), (28, 50)], (142, 86, 56))
    rnd = random.Random(3)
    for _ in range(18):
        z.punkt(rnd.uniform(14, 50), rnd.uniform(22, 32), 0.6, (226, 180, 140))


def farbe(z):
    z.vieleck([(14, 22), (50, 22), (47, 54), (17, 54)], TON)              # Farbtopf aus Ton
    z.ellipse(32, 54, 15, 3.5, TON)
    z.ellipse(32, 22, 18, 5, (196, 128, 86))
    z.ellipse(32, 22, 15, 3.6, BLAU)
    z.balken([(16, 23), (16, 34)], 3.4, BLAU)                             # übergelaufen
    z.ellipse(16, 35, 2.2, 2.4, BLAU)
    z.balken([(36, 21), (52, 4)], 3, HOLZ)                                # Pinselstiel
    z.vieleck([(32, 23), (34, 18), (39, 22), (36, 25)], (60, 44, 30))


# ── Schmied ──────────────────────────────────────────────────────────────────
def rapier(z):
    z.drehen(45, mass=1.06)
    z.vieleck([(29.8, 40), (29.8, 6), (32, 0), (34.2, 6), (34.2, 40)], (214, 220, 228))
    z.ring(32, 42, 9, 7, 2, GOLD, von=0, bis=180)                       # Korb
    z.ring(39, 48, 3.6, 8, 1.8, GOLD, von=-90, bis=90)                   # Griffbügel
    z.balken([(22, 41), (42, 41)], 2.4, GOLD)
    z.balken([(32, 44), (32, 55)], 4, LEDER)
    for y in (46, 49, 52):
        z.strich([(30, y), (34, y + 1.3)], 0.5)
    z.ellipse(32, 59, 3.6, 3.6, GOLD)
    z.gerade()


def silberring(z):
    z.ring(32, 38, 18, 16, 5, SILBER)
    z.vieleck([(24, 22), (40, 22), (36, 28), (28, 28)], SILBER)          # Fassung
    z.ellipse(32, 17, 7, 6.5, EDELSTEIN)
    z.vieleck([(28, 15), (32, 12), (36, 15), (32, 18)], (110, 150, 230), kontur=False)
    z.punkt(29.5, 14.5, 1.0, LICHT)


def werkzeug(z):
    z.balken([(20, 12), (48, 52)], 4.4, EISEN_D)                          # Zange
    z.balken([(16, 16), (44, 56)], 4.4, EISEN)
    z.ellipse(19, 14, 4, 4, EISEN_D)
    z.balken([(12, 54), (40, 22)], 5, HOLZ)                               # Hammer
    z.vieleck([(30, 14), (44, 4), (54, 16), (40, 26)], EISEN)
    z.vieleck([(44, 4), (48, 1), (58, 12), (54, 16)], EISEN_D)


def silberkette(z):
    kette(z, SILBER, SILBER)


def goldkette(z):
    kette(z, GOLD, GOLD, stein=ZINNOBER)


def prunkschwert(z):
    z.drehen(45, mass=1.08)
    klinge_senkrecht(z, 42, 1, 4.2, (212, 218, 226))
    z.balken([(31.5, 47), (32.5, 57)], 5.0, LEDER)
    for y in (49, 52, 55):
        z.strich([(29.6, y), (34.4, y + 1.5)], 0.6)
    z.vieleck([(18, 42), (46, 42), (44, 47), (20, 47)], GOLD)
    z.ellipse(17, 44.5, 3, 3, GOLD)
    z.ellipse(47, 44.5, 3, 3, GOLD)
    z.ellipse(32, 44.5, 2.2, 2.2, KRAPPROT)
    z.ellipse(32, 60, 4.5, 4.5, GOLD)
    z.ellipse(32, 60, 2, 2, BLAU)
    z.gerade()


# ── Tischler ─────────────────────────────────────────────────────────────────
def schemel(z):
    z.balken([(14, 26), (8, 58)], 4, HOLZ_D)                               # Beine
    z.balken([(50, 26), (56, 58)], 4, HOLZ_D)
    z.balken([(32, 30), (32, 60)], 4, HOLZ)
    z.balken([(12, 44), (52, 44)], 2.6, HOLZ_D)                            # Querstrebe
    z.ellipse(32, 26, 24, 6, HOLZ)                                         # Sitzfläche
    z.ellipse(32, 22, 24, 7, HOLZ_H)
    z.kreis(32, 22, 15, 4, 0.6, bis=180)


def truhe_(z):
    truhe(z, HOLZ, HOLZ_H, EISEN, EISEN_D)


def gehstock(z):
    z.balken([(20, 58), (40, 14)], 5, HOLZ)
    z.ring(32, 13, 10, 8, 4.6, HOLZ_D, von=180, bis=360)                     # Krücke
    z.ellipse(23, 13, 2.2, 2.2, HOLZ_D)
    z.balken([(19.6, 59), (21, 56)], 4.6, EISEN)                           # Zwinge
    z.strich([(28, 40), (31, 34)], 0.5)


def armbrust(z):
    z.balken([(32, 12), (32, 60)], 6, HOLZ)                                # Schaft
    z.ring(32, 26, 26, 12, 4, EISEN, von=180, bis=360)                     # Stahlbogen
    z.strich([(6.5, 26), (32, 36), (57.5, 26)], 1.0, (226, 216, 186))      # Sehne
    z.rechteck(28, 34, 36, 40, EISEN_D)                                    # Nuss
    z.balken([(32, 42), (30, 50)], 2, EISEN_D)                              # Abzug
    z.balken([(32, 4), (32, 13)], 3, EISEN_D)                               # Bügel


def schrank(z):
    z.rechteck(12, 58, 18, 62, HOLZ_D)
    z.rechteck(46, 58, 52, 62, HOLZ_D)
    z.rechteck(12, 8, 52, 59, HOLZ)
    z.rechteck(9, 4, 55, 9, HOLZ_D)                                         # Gesims
    for x0 in (15, 34):                                                    # bemalte Türen
        z.rechteck(x0, 12, x0 + 15, 55, BLAU)
        z.rechteck(x0 + 3, 16, x0 + 12, 51, HOLZ_H)
        z.vieleck([(x0 + 7.5, 24), (x0 + 10.5, 33.5), (x0 + 7.5, 43), (x0 + 4.5, 33.5)], ZINNOBER)
    z.punkt(30.5, 34, 1.2, EISEN)
    z.punkt(33.5, 34, 1.2, EISEN)


def prunktruhe(z):
    truhe(z, (124, 44, 38), (150, 56, 46), GOLD, GOLD)
    z.ellipse(32, 20, 3.4, 3, EDELSTEIN)
    z.punkt(31, 19, 0.8, LICHT)


# ── Steinmetz ────────────────────────────────────────────────────────────────
def grabstein(z):
    z.rechteck(16, 22, 48, 56, STEIN)
    z.ellipse(32, 22, 16, 14, STEIN)
    z.vieleck([(30, 14), (34, 14), (34, 22), (42, 22), (42, 26), (34, 26), (34, 44), (30, 44),
               (30, 26), (22, 26), (22, 22), (30, 22)], STEIN_D)            # eingemeißeltes Kreuz
    z.ellipse(32, 57, 26, 4, (92, 132, 62))                                 # Gras
    for x in (12, 18, 46, 52):
        z.blatt(x, 58, 7, 2.4, -100 if x < 32 else -80, KRAUT_H)


def muehlstein(z):
    z.ellipse(32, 38, 26, 16, STEIN_D)                                      # Seite
    z.rechteck(6, 30, 58, 38, STEIN_D)
    z.ellipse(32, 30, 26, 16, STEIN)                                        # Mahlfläche
    for k in range(10):
        w = math.radians(k * 36)
        z.strich([(32 + 8 * math.cos(w), 30 + 5 * math.sin(w)),
                  (32 + 22 * math.cos(w + 0.35), 30 + 13.5 * math.sin(w + 0.35))], 0.7)
    z.ellipse(32, 30, 5.5, 3.4, DUNKEL)                                     # Auge


def saeule(z):
    z.rechteck(20, 12, 44, 52, (180, 172, 160))                             # Schaft
    for x in (25, 29, 33, 37, 41):
        z.strich([(x, 14), (x, 50)], 0.6)
    z.vieleck([(14, 5), (50, 5), (46, 12), (18, 12)], STEIN)                # Kapitell
    z.rechteck(12, 2, 52, 6, STEIN_H)
    z.rechteck(16, 52, 48, 56, STEIN)                                       # Basis
    z.rechteck(12, 56, 52, 61, STEIN_D)


def taufbecken(z):
    z.rechteck(14, 54, 50, 60, STEIN)
    z.vieleck([(24, 34), (40, 34), (37, 54), (27, 54)], STEIN_D)             # Fuß
    z.vieleck([(6, 22), (58, 22), (48, 36), (16, 36)], STEIN)                # Becken
    z.ellipse(32, 22, 26, 7, STEIN_H)
    z.ellipse(32, 22, 22, 5, (70, 120, 170))                                 # Wasser
    z.balken([(22, 21), (30, 20)], 1.2, (170, 206, 230), kontur=False)
    for x in (18, 32, 46):
        z.strich([(x, 26.5), (x - (x - 32) * 0.18, 34)], 0.6)


def steinfigur(z):
    gewand = (182, 174, 160)
    z.rechteck(14, 54, 50, 61, STEIN_D)                                       # Sockel
    z.vieleck([(22, 22), (42, 22), (47, 54), (17, 54)], gewand)               # Gewand
    for pts in (((28, 28), (26, 52)), ((36, 28), (38, 52)), ((32, 34), (32, 52))):
        z.strich(pts, 0.7)
    z.ellipse(32, 14, 7, 8, gewand)                                           # Kopf
    z.balken([(24, 26), (20, 38), (30, 36)], 4, gewand)                       # Arm vor der Brust
    z.strich([(29, 13), (30.5, 13)], 0.6)
    z.strich([(33.5, 13), (35, 13)], 0.6)


def grabmal(z):
    z.rechteck(8, 38, 56, 58, STEIN)                                        # Sockel
    z.rechteck(14, 22, 50, 38, STEIN_H)
    z.vieleck([(10, 22), (32, 6), (54, 22)], STEIN_D)                       # Giebel
    z.rechteck(8, 36, 56, 40, GOLD)                                         # Goldband
    z.ellipse(32, 30, 5, 5, GOLD)
    z.ellipse(32, 30, 2.4, 2.4, KRAPPROT)
    for x in (16, 48):
        z.strich([(x, 42), (x, 56)], 0.6)


# ── Alchimist ────────────────────────────────────────────────────────────────
def kraeuterschnaps(z):
    flasche(z, (156, 112, 44))
    z.blatt(26, 48, 13, 6, -40, KRAUT)


def rosenwasser(z):
    flasche(z, ROSA)
    z.balken([(44, 22), (39, 32)], 1.4, KRAUT_D)
    z.blatt(42, 28, 7, 3.6, 10, KRAUT)
    for k in range(5):                                                   # Rosenblüte
        w = math.radians(k * 72 - 90)
        z.ellipse(46 + 4 * math.cos(w), 17 + 4 * math.sin(w), 3.4, 3.4, ZINNOBER)
    z.ellipse(46, 17, 2.4, 2.4, KRAPPROT)


def gegengift(z):
    flasche(z, GIFTGRUEN, korken=SILBER, breit=13)
    z.rechteck(26.5, 25, 37.5, 28.5, SILBER)


def theriak(z):
    z.vieleck([(18, 22), (46, 22), (43, 37), (46, 54), (18, 54), (21, 37)], WEISS)  # Albarello
    z.ellipse(32, 54, 14, 3.4, WEISS)
    z.rechteck(17, 32, 47, 42, BLAU)                                         # Band
    z.ellipse(32, 37, 3.6, 3.6, GOLD)
    z.ellipse(32, 22, 15, 4, GOLD)                                           # Deckel
    z.ellipse(32, 18, 12, 4, GOLD)
    z.ellipse(32, 12, 3.6, 3.6, GOLD)
    z.strich([(22, 46), (42, 46)], 0.6)
    z.strich([(22, 28), (42, 28)], 0.6)


def zinnober(z):
    z.balken([(36, 36), (52, 8)], 5, STEIN)                                  # Stößel
    z.ellipse(52, 8, 3.6, 3.6, STEIN)
    z.vieleck([(8, 38), (56, 38), (48, 56), (16, 56)], (150, 148, 156))      # Mörser
    z.ellipse(32, 38, 24, 6, (172, 170, 178))
    z.ellipse(32, 38, 20, 4.4, ZINNOBER)
    z.ellipse(30, 34, 11, 6, ZINNOBER)                                       # Pulverhaufen
    z.rechteck(18, 56, 46, 59, (128, 126, 134))


def heilsalbe(z):
    z.vieleck([(14, 26), (50, 26), (48, 54), (16, 54)], WEISS)                # Tiegel
    z.ellipse(32, 54, 16, 3.4, WEISS)
    z.ellipse(32, 26, 18, 5, (214, 206, 186))
    z.ellipse(32, 26, 15, 3.6, (164, 198, 112))                               # Salbe
    z.rechteck(20, 36, 44, 46, ZINNOBER)                                      # Etikett
    z.strich([(24, 41), (40, 41)], 0.7)
    z.drehen(-18, um=(40, 14))                                                # Deckel schräg darüber
    z.ellipse(40, 14, 16, 4.4, (170, 112, 72))
    z.ellipse(40, 11, 4, 2.4, (190, 128, 86))
    z.gerade()


# ── Wirt ─────────────────────────────────────────────────────────────────────
KORN = (218, 180, 82)
KORN_D = (168, 128, 52)
ROGGEN = (176, 140, 76)


def gerste(z):
    aehren(z, KORN, KORN_D, True)


def roggen(z):
    aehren(z, ROGGEN, (128, 96, 52), False)


def weingeist(z):
    flasche(z, (214, 232, 238), breit=13)
    z.punkt(38, 44, 1.0, LICHT)


def koernermahlzeit(z):
    z.balken([(36, 32), (54, 8)], 3.4, HOLZ_H)                                # Löffel
    z.ellipse(54, 7, 4, 3, HOLZ_H)
    z.ellipse(32, 34, 25, 7, (222, 198, 142))                                 # Brei
    for x, y in ((22, 33), (30, 31), (38, 33), (44, 35), (26, 36), (34, 36)):
        z.punkt(x, y, 0.9, (176, 146, 88))
    z.vieleck(Zeichnung.ellipsenpunkte(32, 34, 26, 22, 0, 180), HOLZ)          # Schüssel
    z.ellipse(32, 34, 26, 4, HOLZ_H)
    z.ellipse(32, 34, 22.5, 3, (222, 198, 142))
    z.rechteck(24, 54, 40, 58, HOLZ_D)


def duennbier(z):
    humpen(z, HOLZ, EISEN, SCHAUM, (222, 182, 72))


def starkbier(z):
    humpen(z, TON, (110, 70, 40), (236, 226, 200), (122, 72, 30))


def kraeuterlikoer(z):
    flasche(z, (70, 140, 60), breit=13, hals=(98, 156, 86))
    z.rechteck(23, 38, 41, 48, PERGAMENT)
    z.blatt(27, 45, 10, 4.6, -30, KRAUT)


def festbier(z):
    z.vieleck([(14, 12), (50, 12), (55, 34), (50, 56), (14, 56), (9, 34)], HOLZ)   # bauchiges Fass
    z.ellipse(32, 12, 18, 4.6, HOLZ_H)
    z.kreis(32, 12, 12, 2.8, 0.6)
    for x0, x1 in ((22, 21), (32, 32), (42, 43)):
        z.strich([(x0, 17), (x1 - (x1 - 32) * 0.3, 34), (x1, 52)], 0.7)
    for y, b in ((18, 46), (50, 46)):
        z.vieleck([(32 - b / 2 + 1, y), (32 + b / 2 - 1, y), (32 + b / 2, y + 4), (32 - b / 2, y + 4)], EISEN)
    z.vieleck([(10.5, 31), (53.5, 31), (54, 36), (10, 36)], EISEN)
    z.rechteck(28, 38, 36, 42, EISEN_D)                                       # Zapfhahn
    z.rechteck(30.5, 42, 33.5, 48, EISEN_D)
    z.ellipse(32, 51, 2, 2.6, (226, 186, 76))                                 # Tropfen


def schaedelbrand(z):
    z.ellipse(32, 42, 15, 15, (64, 42, 42))
    z.rechteck(27.5, 13, 36.5, 29, (64, 42, 42))
    z.rechteck(27, 5, 37, 13, ZINNOBER)                                        # Siegellack
    z.ellipse(32, 15, 6, 2.2, ZINNOBER)
    z.ellipse(32, 40, 7.5, 7, WEISS)                                           # Totenkopf
    z.rechteck(27.5, 44, 36.5, 50, WEISS)
    z.ellipse(29, 40, 2.2, 2.4, DUNKEL, kontur=False)
    z.ellipse(35, 40, 2.2, 2.4, DUNKEL, kontur=False)
    z.vieleck([(32, 43), (33.4, 46), (30.6, 46)], DUNKEL, kontur=False)
    for x in (30, 32, 34):
        z.strich([(x, 47), (x, 50)], 0.5)
    z.balken([(21, 36), (20, 46)], 1.6, (130, 100, 100), kontur=False)


# ── Prediger ─────────────────────────────────────────────────────────────────
WACHS = (232, 196, 104)


def lumpen(z):
    """Ein Stapel alter, geflickter Stofffetzen mit ausgefransten Kanten."""
    def fransen(x_rechts, x_links, y, tiefe, n):
        return [(x_rechts - (x_rechts - x_links) * i / n, y + (tiefe if i % 2 else 0)) for i in range(n + 1)]
    z.vieleck([(8, 44), (56, 42), (57, 55)] + fransen(56, 9, 56, 3.5, 12), (180, 156, 118))      # Leinen
    z.vieleck([(12, 33), (52, 30), (55, 44), (9, 46)], (100, 112, 150))                         # blau
    z.vieleck([(46, 38), (56, 36), (58, 46)] + fransen(57, 48, 50, 3, 4), (100, 112, 150))      # Zipfel
    z.rechteck(18, 36, 28, 43, (156, 80, 62))                                                    # Flicken
    for x0, y0, x1, y1 in ((18, 35, 28, 35), (29, 36, 29, 43), (18, 44, 28, 44), (17, 36, 17, 43)):
        for k in range(3):
            a, b = k / 3 + 0.04, k / 3 + 0.22
            z.strich([(x0 + (x1 - x0) * a, y0 + (y1 - y0) * a), (x0 + (x1 - x0) * b, y0 + (y1 - y0) * b)], 0.5)
    z.vieleck([(16, 24), (28, 17), (40, 22), (50, 19), (49, 31), (36, 34), (22, 33), (13, 30)], (156, 80, 62))  # rot
    for pts in (((26, 21), (29, 31)), ((40, 24), (38, 32)), ((20, 47), (23, 54)), ((40, 46), (43, 54)), ((36, 34), (44, 40))):
        z.strich(pts, 0.6)


def wachs(z):
    for cx, cy in ((20, 22), (38, 22), (11, 36), (29, 36), (47, 36), (20, 50), (38, 50)):   # Wabe
        z.vieleck([(cx - 9, cy), (cx - 4.5, cy - 7.8), (cx + 4.5, cy - 7.8), (cx + 9, cy),
                   (cx + 4.5, cy + 7.8), (cx - 4.5, cy + 7.8)], WACHS)
        z.vieleck([(cx - 4.5, cy), (cx - 2.2, cy - 3.9), (cx + 2.2, cy - 3.9), (cx + 4.5, cy),
                   (cx + 2.2, cy + 3.9), (cx - 2.2, cy + 3.9)], (196, 146, 52), kontur=False)


def papier(z):
    z.vieleck([(10, 30), (46, 22), (54, 46), (18, 54)], PERGAMENT_D)
    z.vieleck([(8, 26), (44, 18), (52, 42), (16, 50)], PERGAMENT)
    z.vieleck([(8, 22), (44, 14), (52, 38), (16, 46)], PERGAMENT)
    for k in range(4):
        y = 25 + k * 5
        z.strich([(16 + k * 0.6, y + 2), (42 + k * 0.6, y - 4)], 0.5)


def kerze(z):
    z.ring(50, 50, 6, 6, 2.4, EISEN_D)                                         # Griff
    z.ellipse(32, 54, 18, 6, EISEN)
    z.ellipse(32, 52.5, 12, 3.5, EISEN_D)
    z.rechteck(25, 23, 39, 52, (238, 228, 192))
    z.ellipse(32, 23, 7, 2.2, (246, 238, 208))
    z.balken([(37, 24), (37.5, 34)], 3.0, (238, 228, 192))                     # Wachsnase
    z.strich([(32, 15), (32, 22)], 1.0)
    z.vieleck([(32, 2), (36.5, 11), (35.5, 16), (32, 18.5), (28.5, 16), (27.5, 11)], (240, 160, 50))
    z.vieleck([(32, 8), (34, 13), (32, 16.5), (30, 13)], (255, 236, 150))


def lehrschrift(z):
    z.rechteck(14, 14, 50, 50, PERGAMENT)                                       # Schriftrolle
    z.ellipse(14, 32, 5, 18, PERGAMENT_D)
    z.ellipse(50, 32, 5, 18, PERGAMENT_D)
    z.kreis(14, 32, 2, 7, 0.5)
    z.kreis(50, 32, 2, 7, 0.5)
    z.rechteck(21, 19, 25, 25, ZINNOBER)                                        # Initiale
    for y in (21, 26, 31, 36, 41):
        z.strich([(27 if y < 27 else 21, y), (44, y)], 0.7)
    z.balken([(24, 46), (22, 56)], 2.4, ZINNOBER)                              # Siegelband
    z.ellipse(22, 56, 4, 3.4, KRAPPROT)


def hetzschrift(z):
    z.rechteck(28, 4, 36, 62, HOLZ)                                             # Pfahl
    z.vieleck([(10, 12), (54, 10), (52, 50), (12, 52)], PERGAMENT)
    z.ellipse(32, 27, 10, 10, ZINNOBER)                                         # Fratze
    z.vieleck([(23, 19), (29, 24), (27, 25)], DUNKEL, kontur=False)
    z.vieleck([(41, 19), (35, 24), (37, 25)], DUNKEL, kontur=False)
    z.ellipse(28, 26.5, 1.4, 1.4, DUNKEL, kontur=False)
    z.ellipse(36, 26.5, 1.4, 1.4, DUNKEL, kontur=False)
    z.strich([(26, 34), (29, 31), (35, 31), (38, 34)], 1.0)
    for y in (41, 45):
        z.strich([(18, y), (46, y - 0.5)], 0.7)
    z.ellipse(32, 13, 1.8, 1.8, EISEN_D)                                        # Nagel


def psalter(z):
    buch(z, KRAPPROT, (112, 26, 24), PERGAMENT)
    eckbeschlaege(z, GOLD)
    z.rechteck(29, 18, 34, 48, GOLD)
    z.rechteck(22, 25, 41, 30, GOLD)
    z.ellipse(31.5, 27.5, 3, 3, BLAU)
    z.rechteck(46, 30, 56, 36, GOLD)


def stundenbuch(z):
    buch(z, (104, 42, 72), (80, 30, 54), (240, 220, 150))
    z.rechteck(17, 13, 42, 53, BLAU)
    for k in range(8):                                                          # goldene Rosette
        w = math.radians(k * 45)
        z.ellipse(29.5 + 5.5 * math.cos(w), 33 + 5.5 * math.sin(w), 3, 3, GOLD)
    z.ellipse(29.5, 33, 3.4, 3.4, ZINNOBER)
    for x, y in ((14, 9), (45, 9), (14, 57), (45, 57)):
        z.ellipse(x, y, 2.6, 2.6, GOLD)
    z.rechteck(46, 30, 56, 36, GOLD)


def reliquiar(z):
    z.rechteck(10, 54, 54, 58, GOLD)                                            # Schrein mit Satteldach
    z.rechteck(12, 30, 52, 54, GOLD)
    z.vieleck([(8, 31), (32, 15), (56, 31)], GOLD)
    for x0, f in ((17, EDELSTEIN), (27, ZINNOBER), (37, EDELSTEIN)):
        z.vieleck([(x0, 50), (x0, 38), (x0 + 5, 34), (x0 + 10, 38), (x0 + 10, 50)], f)
    z.rechteck(30.5, 2, 33.5, 17, SILBER)
    z.rechteck(26, 6, 38, 9, SILBER)
    for x in (12, 52):
        z.ellipse(x, 30, 2, 2, ZINNOBER)


# ── Parfumeur ────────────────────────────────────────────────────────────────
LAVENDEL = (128, 98, 190)
LAVENDEL_H = (170, 144, 222)
JASMIN = (248, 246, 232)


def lavendel(z):
    tops = ((16, 12), (24, 7), (32, 4), (40, 7), (48, 12))
    for x, y in tops:
        z.balken([(32, 58), (x, y + 12)], 2.2, KRAUT)
    for x, y in tops:
        for i in range(6):
            z.ellipse(x + (1.6 if i % 2 else -1.6), y + i * 3.2, 3.4, 2.8, LAVENDEL_H if i % 2 else LAVENDEL)
    z.blatt(30, 46, 12, 3, -150, KRAUT_H)
    z.blatt(34, 46, 12, 3, -30, KRAUT_H)
    z.rechteck(27, 46, 37, 50, (176, 146, 88))


def jasmin(z):
    z.balken([(10, 58), (30, 34), (50, 18)], 2.6, KRAUT_D)
    z.balken([(30, 34), (40, 44)], 2, KRAUT_D)
    for x, y, w in ((14, 46, 150), (24, 42, 30), (36, 28, -60), (44, 46, 40)):
        z.blatt(x, y, 12, 5, w, KRAUT)
    for cx, cy in ((22, 24), (44, 44), (50, 16)):
        for k in range(5):
            w = math.radians(k * 72 - 90)
            z.ellipse(cx + 4.6 * math.cos(w), cy + 4.6 * math.sin(w), 3.8, 3.8, JASMIN)
        z.ellipse(cx, cy, 2.2, 2.2, GOLD)


def politur(z):
    z.vieleck([(12, 22), (52, 22), (52, 48), (12, 48)], (150, 120, 72))        # Dose
    z.ellipse(32, 48, 20, 5, (150, 120, 72))
    z.ellipse(32, 22, 20, 5, (190, 168, 120))
    z.ellipse(32, 22, 17, 3.8, (228, 202, 122))                                # glänzende Paste
    z.balken([(23, 21), (30, 20)], 1.2, (255, 246, 206), kontur=False)
    z.rechteck(12, 32, 52, 38, LAVENDEL)                                       # Banderole
    z.vieleck([(32, 33), (34, 35), (32, 37), (30, 35)], GOLD)


def blume_der_zwietracht(z):
    z.balken([(32, 60), (31, 40), (32, 26)], 2.6, (60, 90, 40))
    z.blatt(31, 48, 14, 5, 200, (70, 110, 50))
    z.blatt(32, 42, 12, 4.4, -20, (70, 110, 50))
    for k in range(5):
        w = math.radians(k * 72 - 90)
        z.ellipse(32 + 9 * math.cos(w), 20 + 9 * math.sin(w), 7, 7, (116, 30, 72))
    z.ellipse(32, 20, 5, 5, (44, 22, 34))
    for x, y in ((46, 40), (50, 48), (16, 52), (20, 36)):
        z.punkt(x, y, 1.2, (210, 64, 108))                                   # Blütenstaub


def giftdolch(z):
    dolch(z, (204, 208, 218), EISEN_D, EISEN, ((32, 22, 1.8), (36, 30, 1.5), (30, 34, 1.3)))


def schattendolch(z):
    dolch(z, (66, 62, 84), (44, 40, 54), EDELSTEIN, ((32, 22, 1.8), (36, 30, 1.5), (30, 34, 1.3), (34, 14, 1.2)))


def moschusduft(z):
    duftflakon(z, (152, 98, 60), GOLD)


def fechterduft(z):
    z.drehen(45, mass=1.05)
    z.vieleck([(30.8, 60), (30.8, 6), (32, 0), (33.2, 6), (33.2, 60)], (214, 220, 228))
    z.gerade()
    duftflakon(z, (90, 130, 190), SILBER)


def heiligenduft(z):
    z.ring(32, 36, 27, 27, 3, GOLD)                                             # Heiligenschein
    duftflakon(z, (236, 220, 150), GOLD)
    z.rechteck(30.5, 30, 33.5, 46, WEISS)
    z.rechteck(26, 34, 38, 37, WEISS)


WAREN = {
    "Eisen": eisen, "Silber": silber, "Gold": gold, "Holz": holz, "Bruchstein": bruchstein, "Granit": granit,
    "Kräuter": kraeuter, "Kröte": kroete,
    "Beschlag": beschlag, "Holzzapfen": holzzapfen, "Schleifziegel": schleifziegel, "Farbe": farbe,
    "Rapier": rapier, "Silberring": silberring, "Werkzeug": werkzeug, "Silberkette": silberkette, "Goldkette": goldkette,
    "Prunkschwert": prunkschwert,
    "Schemel": schemel, "Truhe": truhe_, "Gehstock": gehstock, "Armbrust": armbrust, "Schrank": schrank, "Prunktruhe": prunktruhe,
    "Grabstein": grabstein, "Mühlstein": muehlstein, "Säule": saeule, "Taufbecken": taufbecken, "Steinfigur": steinfigur,
    "Grabmal": grabmal,
    "Heilsalbe": heilsalbe, "Rosenwasser": rosenwasser, "Gegengift": gegengift, "Theriak": theriak, "Zinnober": zinnober,
    "Kräuterschnaps": kraeuterschnaps,
    "Gerste": gerste, "Roggen": roggen, "Weingeist": weingeist, "Körnermahlzeit": koernermahlzeit, "Dünnbier": duennbier,
    "Starkbier": starkbier, "Kräuterlikör": kraeuterlikoer, "Festbier": festbier, "Schädelbrand": schaedelbrand,
    "Lumpen": lumpen, "Wachs": wachs, "Papier": papier, "Kerze": kerze, "Lehrschrift": lehrschrift, "Hetzschrift": hetzschrift,
    "Psalter": psalter, "Stundenbuch": stundenbuch, "Reliquiar": reliquiar,
    "Lavendel": lavendel, "Jasmin": jasmin, "Politur": politur, "Blume der Zwietracht": blume_der_zwietracht,
    "Giftdolch": giftdolch, "Moschusduft": moschusduft, "Fechterduft": fechterduft, "Heiligenduft": heiligenduft,
    "Schattendolch": schattendolch,
}


def dateiname(ware):
    """„Kräuterschnaps“ → kraeuterschnaps.png – dieselbe Regel wie WarenIcons.Dateiname im Spiel."""
    s = ware.lower()
    for a, b in (("ä", "ae"), ("ö", "oe"), ("ü", "ue"), ("ß", "ss"), (" ", "_")):
        s = s.replace(a, b)
    return s + ".png"


def zeichnen(name):
    z = Zeichnung()
    WAREN[name](z)
    saat = sum(ord(c) for c in name)       # fester Zufall je Ware: gleiche Eingabe, gleiches Bild
    return name, malen(z, saat)


def main():
    wurzel = os.path.join(os.path.dirname(__file__), "..", "..")
    ziel = os.path.join(wurzel, "game", "assets", "waren")
    os.makedirs(ziel, exist_ok=True)
    namen = list(WAREN)
    if "--nur" in sys.argv:
        namen = sys.argv[sys.argv.index("--nur") + 1].split(",")
    with Pool() as pool:
        bilder = dict(pool.map(zeichnen, namen))
    for name in namen:
        bilder[name].save(os.path.join(ziel, dateiname(name)))
    print(f"{len(bilder)} Icons nach {os.path.normpath(ziel)} geschrieben")

    if "--vorschau" in sys.argv:
        pfad = sys.argv[sys.argv.index("--vorschau") + 1]
        spalten, zelle = 9, AUS + 12
        zeilen = (len(bilder) + spalten - 1) // spalten
        bogen = Image.new("RGBA", (spalten * zelle, zeilen * zelle), (56, 46, 36, 255))
        for i, name in enumerate(namen):
            bogen.alpha_composite(bilder[name], ((i % spalten) * zelle + 6, (i // spalten) * zelle + 6))
        bogen.save(pfad)


if __name__ == "__main__":
    main()
