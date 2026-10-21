#!/usr/bin/env python3
"""
Zeichnet die Waren-Icons von Reichsgilde als 32×32-Pixel-Art (eigene Grafiken, MIT-Lizenz wie das Projekt).

Aufruf (aus dem Projektordner):
    python3 tools/icons/waren_icons.py              # schreibt game/assets/oberflaeche/icons/<ware>.png
    python3 tools/icons/waren_icons.py --vorschau x.png   # zusätzlich eine vergrößerte Übersicht

Jede Ware ist eine kleine Zeichenfunktion aus Grundformen (Rechteck, Ellipse, Linie, Vieleck).
Danach schattiert das Skript automatisch (Licht von links oben) und zieht eine dunkle Kontur.
Neue Ware: Funktion schreiben und in WAREN eintragen – der Dateiname entsteht aus dem Warennamen
(„Kräuter“ → kraeuter.png), genauso wie im Spiel (WarenIcons.cs).
"""
import os
import sys
from PIL import Image

N = 32
KONTUR = (34, 24, 20, 255)

# ── Farben ────────────────────────────────────────────────────────────────────
EISEN = (112, 116, 124)
EISEN_D = (78, 80, 88)
SILBER = (196, 204, 214)
GOLD = (232, 182, 54)
GOLD_D = (176, 124, 30)
HOLZ = (150, 98, 54)
HOLZ_H = (186, 132, 78)
HOLZ_D = (108, 68, 36)
STEIN = (150, 144, 134)
STEIN_D = (112, 106, 98)
GRANIT = (128, 122, 130)
KRAUT = (86, 150, 62)
KRAUT_H = (128, 186, 86)
KROETE = (112, 132, 52)
ROT = (190, 48, 40)
ZINNOBER = (214, 66, 44)
ROSA = (226, 140, 168)
GLAS = (170, 210, 220)
GIFTGRUEN = (96, 186, 104)
BRAUN = (124, 80, 44)
LEDER = (120, 70, 40)
TON = (178, 112, 72)
WEISS = (236, 232, 220)
EDELSTEIN = (60, 120, 210)
VIOLETT = (128, 70, 150)


def hex_(c):
    return c if len(c) == 4 else (*c, 255)


class Bild:
    def __init__(self):
        self.p = [[None] * N for _ in range(N)]

    def px(self, x, y, c):
        if 0 <= x < N and 0 <= y < N:
            self.p[y][x] = hex_(c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            for x in range(min(x0, x1), max(x0, x1) + 1):
                self.px(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(N):
            for x in range(N):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    self.px(x, y, c)

    def ring(self, cx, cy, rx, ry, dicke, c):
        for y in range(N):
            for x in range(N):
                a = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
                b = ((x + 0.5 - cx) / max(0.1, rx - dicke)) ** 2 + ((y + 0.5 - cy) / max(0.1, ry - dicke)) ** 2
                if a <= 1 and b > 1:
                    self.px(x, y, c)

    def linie(self, x0, y0, x1, y1, c, dicke=1):
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            for ox in range(dicke):
                for oy in range(dicke):
                    self.px(x0 + ox, y0 + oy, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def vieleck(self, punkte, c):
        for y in range(N):
            yy = y + 0.5
            xs = []
            for i in range(len(punkte)):
                (x0, y0), (x1, y1) = punkte[i], punkte[(i + 1) % len(punkte)]
                if (y0 <= yy < y1) or (y1 <= yy < y0):
                    xs.append(x0 + (yy - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for a, b in zip(xs[::2], xs[1::2]):
                for x in range(int(round(a)), int(round(b))):
                    self.px(x, y, c)

    # ── Nachbearbeitung ──────────────────────────────────────────────────────
    def schattieren(self):
        """Licht von links oben: Kanten zum Leeren oben/links heller, unten/rechts dunkler."""
        alt = [row[:] for row in self.p]
        leer = lambda x, y: not (0 <= x < N and 0 <= y < N) or alt[y][x] is None
        for y in range(N):
            for x in range(N):
                c = alt[y][x]
                if c is None:
                    continue
                if leer(x - 1, y) or leer(x, y - 1):
                    self.p[y][x] = misch(c, (255, 255, 255), 0.28)
                elif leer(x + 1, y) or leer(x, y + 1):
                    self.p[y][x] = misch(c, (0, 0, 0), 0.30)

    def kontur(self):
        alt = [row[:] for row in self.p]
        for y in range(N):
            for x in range(N):
                if alt[y][x] is not None:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < N and 0 <= yy < N and alt[yy][xx] is not None and alt[yy][xx] != KONTUR:
                        self.p[y][x] = KONTUR
                        break

    def bild(self):
        im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        for y in range(N):
            for x in range(N):
                if self.p[y][x] is not None:
                    im.putpixel((x, y), self.p[y][x])
        return im


def misch(c, z, t):
    return tuple(int(c[i] + (z[i] - c[i]) * t) for i in range(3)) + (255,)


# ── Rohstoffe ────────────────────────────────────────────────────────────────
def erz(b, farbe, glanz):
    for cx, cy, r in ((12, 19, 7), (21, 21, 6), (16, 12, 5)):
        b.ellipse(cx, cy, r, r * 0.85, farbe)
    for x, y in ((10, 17), (20, 19), (15, 10), (13, 22), (23, 23)):
        b.px(x, y, glanz)
        b.px(x + 1, y, glanz)


def eisen(b):
    erz(b, EISEN, (150, 156, 168))
    for x, y in ((9, 21), (18, 24), (22, 18)):
        b.px(x, y, (140, 70, 50))  # Rost


def silber(b):
    erz(b, STEIN_D, SILBER)
    for x, y in ((11, 20), (12, 19), (19, 22), (20, 22), (16, 13), (17, 12)):
        b.px(x, y, (240, 244, 250))


def gold(b):
    # Barren
    b.vieleck([(4, 22), (10, 14), (26, 14), (28, 22)], GOLD)
    b.vieleck([(6, 26), (4, 22), (28, 22), (26, 26)], GOLD_D)
    b.linie(11, 16, 24, 16, (255, 230, 140))


def holz(b):
    # Drei Stämme als Pyramide, mit Lücke dazwischen, damit jeder seine Kontur bekommt
    for x0, y0 in ((2, 19), (17, 19), (9, 9)):
        b.rect(x0, y0, x0 + 8, y0 + 7, HOLZ)
        b.linie(x0 + 1, y0 + 2, x0 + 6, y0 + 2, HOLZ_D)
        b.linie(x0 + 2, y0 + 5, x0 + 7, y0 + 5, HOLZ_D)
        b.ellipse(x0 + 10, y0 + 4, 2.6, 4, HOLZ_H)
        b.ring(x0 + 10, y0 + 4, 1.8, 2.6, 1, HOLZ)
        b.px(x0 + 10, y0 + 3, HOLZ_D)


def bruchstein(b):
    b.vieleck([(4, 24), (6, 15), (13, 12), (16, 18), (14, 26)], STEIN)
    b.vieleck([(14, 26), (16, 16), (24, 13), (28, 20), (26, 27)], STEIN_D)
    b.vieleck([(10, 12), (14, 6), (20, 7), (22, 13), (15, 15)], STEIN)


def granit(b):
    b.vieleck([(5, 12), (20, 8), (27, 12), (12, 16)], (160, 152, 160))
    b.vieleck([(5, 12), (12, 16), (12, 27), (5, 23)], GRANIT)
    b.vieleck([(12, 16), (27, 12), (27, 23), (12, 27)], (100, 96, 104))
    for x, y in ((8, 16), (9, 21), (15, 19), (20, 22), (23, 16), (17, 11), (22, 11), (7, 20)):
        b.px(x, y, (60, 56, 64))
    for x, y in ((10, 18), (18, 24), (24, 19), (13, 10)):
        b.px(x, y, (210, 200, 205))


def kraeuter(b):
    for x0, x1 in ((15, 8), (16, 16), (17, 24), (16, 12), (16, 20)):
        b.linie(16, 27, x1, 9, KRAUT, 1)
    for cx, cy in ((8, 9), (12, 7), (16, 6), (20, 7), (24, 9), (10, 13), (22, 13), (14, 10), (18, 10)):
        b.ellipse(cx, cy, 3, 2.2, KRAUT_H if (cx + cy) % 2 else KRAUT)
    b.rect(13, 20, 19, 22, (180, 150, 90))  # Bindfaden


def kroete(b):
    b.ellipse(16, 20, 11, 7, KROETE)
    b.ellipse(10, 13, 3.5, 3.5, KROETE)
    b.ellipse(22, 13, 3.5, 3.5, KROETE)
    b.ellipse(10, 13, 1.5, 1.5, (230, 200, 60))
    b.ellipse(22, 13, 1.5, 1.5, (230, 200, 60))
    b.px(10, 13, (20, 20, 20))
    b.px(22, 13, (20, 20, 20))
    b.linie(11, 21, 21, 21, (70, 80, 30))
    for x, y in ((8, 18), (24, 18), (14, 24), (19, 24), (16, 17)):
        b.px(x, y, (150, 160, 70))
    b.rect(4, 25, 8, 26, KROETE)
    b.rect(24, 25, 28, 26, KROETE)


# ── Zwischenwaren ────────────────────────────────────────────────────────────
def beschlag(b):
    # Scharnier: zwei Lappen und ein Bolzen
    b.vieleck([(4, 10), (15, 12), (15, 20), (4, 22)], EISEN)
    b.vieleck([(17, 12), (28, 8), (28, 24), (17, 20)], EISEN)
    b.rect(15, 9, 17, 23, EISEN_D)
    for x, y in ((7, 16), (24, 12), (24, 20)):
        b.ellipse(x + 0.5, y + 0.5, 1.6, 1.6, (40, 40, 46))


def holzzapfen(b):
    for i, (x, y) in enumerate(((6, 8), (14, 12), (22, 16))):
        b.linie(x, y + 10, x + 4, y, HOLZ, 4)
        b.ellipse(x + 6, y + 1, 2.2, 2.2, HOLZ_H)


def schleifziegel(b):
    b.vieleck([(4, 14), (18, 9), (28, 13), (14, 18)], (190, 120, 80))
    b.vieleck([(4, 14), (14, 18), (14, 25), (4, 21)], TON)
    b.vieleck([(14, 18), (28, 13), (28, 20), (14, 25)], (140, 84, 54))
    for x in range(7, 26, 3):
        b.px(x, 13 + (x % 2), (220, 170, 130))


def farbe(b):
    b.ellipse(16, 9, 9, 3, (150, 150, 160))
    b.rect(7, 9, 25, 25, (130, 130, 140))
    b.ellipse(16, 25, 9, 3, (130, 130, 140))
    b.ellipse(16, 9, 8, 2.4, (40, 90, 170))
    b.rect(7, 10, 9, 17, (40, 90, 170))  # Farbe läuft über den Rand
    b.ellipse(8, 17, 1.4, 1.4, (40, 90, 170))
    b.linie(18, 9, 26, 2, HOLZ, 2)  # Pinselstiel


# ── Schmied ──────────────────────────────────────────────────────────────────
def rapier(b):
    b.linie(9, 22, 27, 4, (210, 214, 224), 2)
    b.ring(9, 21, 5, 5, 1.6, GOLD_D)
    b.linie(4, 27, 8, 23, LEDER, 2)
    b.ellipse(3.5, 28, 1.6, 1.6, GOLD)


def silberring(b):
    b.ring(16, 18, 9, 8, 2.6, SILBER)
    b.ellipse(16, 9, 3.5, 3, EDELSTEIN)
    b.px(15, 8, (180, 220, 255))


def werkzeug(b):
    # Hammer und Zange gekreuzt
    b.linie(7, 26, 21, 12, HOLZ, 3)
    b.vieleck([(16, 7), (24, 3), (28, 11), (20, 15)], EISEN)
    b.linie(24, 26, 10, 10, EISEN_D, 2)
    b.linie(26, 24, 12, 8, EISEN, 2)


def kette(b, glied, anhaenger):
    for i in range(7):
        x = 5 + i * 3.4
        y = 9 + abs(3 - i) * -1 + 3 + (i % 2)
        b.ring(x, y + 4, 2.4, 2.0, 1, glied)
    b.ellipse(16, 22, 4.5, 5, anhaenger)
    b.px(15, 20, (255, 255, 255))


def silberkette(b):
    kette(b, SILBER, SILBER)


def goldkette(b):
    kette(b, GOLD, ROT)


def prunkschwert(b):
    b.linie(13, 18, 27, 4, (220, 224, 232), 3)
    b.linie(9, 14, 17, 22, GOLD, 2)  # Parierstange
    b.linie(5, 26, 11, 20, LEDER, 3)
    b.ellipse(4.5, 27, 2.2, 2.2, GOLD)
    b.ellipse(13, 18, 1.6, 1.6, ROT)


# ── Tischler ─────────────────────────────────────────────────────────────────
def schemel(b):
    b.ellipse(16, 10, 11, 4, HOLZ_H)
    b.rect(5, 10, 27, 13, HOLZ)
    b.linie(8, 13, 5, 28, HOLZ_D, 2)
    b.linie(24, 13, 27, 28, HOLZ_D, 2)
    b.linie(15, 13, 15, 27, HOLZ, 2)
    b.linie(7, 21, 25, 21, HOLZ_D)


def truhe(b, rahmen=EISEN_D, schloss=EISEN, holzfarbe=HOLZ):
    b.rect(4, 15, 27, 27, holzfarbe)
    b.ellipse(16, 15, 12, 6, misch(holzfarbe, (255, 255, 255), 0.15)[:3])
    b.rect(4, 15, 27, 16, rahmen)
    for x in (7, 24):
        b.rect(x, 10, x + 1, 27, rahmen)
    b.rect(14, 16, 17, 20, schloss)
    b.px(15, 18, KONTUR)


def truhe_(b):
    truhe(b)


def gehstock(b):
    b.linie(10, 28, 20, 8, HOLZ, 2)
    b.ring(16, 7, 5, 4, 2, HOLZ_D)
    b.rect(9, 27, 11, 29, EISEN)


def armbrust(b):
    b.linie(16, 4, 16, 28, HOLZ, 3)  # Schaft
    b.vieleck([(4, 10), (16, 6), (28, 10), (28, 12), (16, 9), (4, 12)], EISEN)  # Bogen
    b.linie(4, 11, 16, 17, (220, 210, 180))
    b.linie(28, 11, 16, 17, (220, 210, 180))
    b.rect(14, 20, 18, 22, EISEN_D)


def schrank(b):
    b.rect(6, 4, 25, 28, HOLZ)
    b.rect(5, 3, 26, 4, HOLZ_D)
    b.linie(15, 6, 15, 26, HOLZ_D)
    b.rect(8, 7, 13, 25, (60, 100, 160))  # bemalte Türen
    b.rect(17, 7, 23, 25, (60, 100, 160))
    b.rect(9, 8, 12, 24, HOLZ_H)
    b.rect(18, 8, 22, 24, HOLZ_H)
    b.px(13, 16, EISEN)
    b.px(17, 16, EISEN)


def prunktruhe(b):
    truhe(b, rahmen=GOLD, schloss=GOLD, holzfarbe=(120, 50, 40))
    b.ellipse(16, 13, 2, 2, EDELSTEIN)


# ── Steinmetz ────────────────────────────────────────────────────────────────
def grabstein(b):
    b.rect(8, 12, 23, 27, STEIN)
    b.ellipse(15.5, 12, 8, 7, STEIN)
    b.rect(15, 9, 16, 21, STEIN_D)  # Kreuz eingemeißelt
    b.rect(12, 12, 19, 13, STEIN_D)
    b.rect(5, 27, 26, 29, (90, 130, 60))  # Gras


def muehlstein(b):
    b.ellipse(16, 16, 13, 12, STEIN)
    b.ellipse(16, 16, 3.5, 3, KONTUR)
    for a in range(0, 12):
        import math
        w = a * math.pi / 6
        b.linie(int(16 + 5 * math.cos(w)), int(16 + 4.5 * math.sin(w)), int(16 + 11 * math.cos(w)), int(16 + 10 * math.sin(w)), STEIN_D)


def saeule(b):
    b.rect(9, 3, 22, 6, STEIN)
    b.rect(11, 6, 20, 25, (176, 170, 160))
    for x in (13, 16, 19):
        b.linie(x, 7, x, 24, STEIN_D)
    b.rect(8, 25, 23, 29, STEIN)


def taufbecken(b):
    b.ellipse(16, 10, 12, 4, STEIN)
    b.ellipse(16, 10, 10, 2.6, (70, 120, 170))
    b.vieleck([(4, 10), (28, 10), (22, 18), (10, 18)], STEIN)
    b.rect(13, 18, 18, 25, STEIN_D)
    b.rect(9, 25, 22, 28, STEIN)


def steinfigur(b):
    b.ellipse(16, 7, 3.5, 4, (176, 170, 160))  # Kopf
    b.vieleck([(10, 12), (22, 12), (24, 25), (8, 25)], (176, 170, 160))  # Gewand
    b.linie(16, 13, 16, 24, STEIN_D)
    b.linie(10, 15, 6, 20, (176, 170, 160), 2)  # Arm
    b.rect(7, 25, 25, 29, STEIN_D)


def grabmal(b):
    b.rect(5, 18, 26, 28, STEIN)
    b.rect(8, 10, 23, 18, (176, 170, 160))
    b.vieleck([(7, 10), (16, 3), (25, 10)], STEIN_D)
    b.ellipse(16, 14, 2.4, 2.4, GOLD)
    b.rect(5, 18, 26, 19, GOLD_D)


# ── Alchimist ────────────────────────────────────────────────────────────────
def flasche(b, inhalt, hals=GLAS, korken=HOLZ_D, breit=7):
    b.ellipse(16, 21, breit, 7, GLAS)
    b.ellipse(16, 22, breit - 1, 5.5, inhalt)
    b.rect(14, 7, 17, 15, hals)
    b.rect(13, 4, 18, 7, korken)
    b.px(12, 18, (240, 250, 255))
    b.px(12, 19, (240, 250, 255))


def kraeuterschnaps(b):
    flasche(b, (150, 110, 40))
    b.ellipse(16, 21, 2.5, 3, KRAUT)


def rosenwasser(b):
    flasche(b, ROSA)
    b.ellipse(22, 9, 3, 3, ROT)  # Rosenblüte
    b.linie(21, 12, 19, 16, KRAUT)


def gegengift(b):
    flasche(b, GIFTGRUEN, hals=GLAS, korken=SILBER, breit=6)
    b.rect(12, 14, 19, 15, SILBER)


def theriak(b):
    b.ellipse(16, 20, 10, 8, TON)
    b.rect(9, 9, 22, 13, TON)
    b.ellipse(16, 9, 7, 2.5, GOLD)
    b.rect(14, 5, 17, 8, GOLD)
    b.rect(7, 18, 24, 20, GOLD_D)  # Goldband
    b.ellipse(16, 19, 1.8, 1.8, EDELSTEIN)


def zinnober(b):
    b.ellipse(16, 22, 12, 5, (150, 150, 160))  # Mörser
    b.ellipse(16, 20, 10, 3, ZINNOBER)
    b.ellipse(16, 17, 6, 3.5, ZINNOBER)
    b.linie(18, 18, 26, 5, STEIN, 3)  # Stößel


def heilsalbe(b):
    b.rect(7, 13, 24, 26, (220, 214, 196))  # Tiegel
    b.ellipse(15.5, 26, 8.5, 2, (220, 214, 196))
    b.ellipse(15.5, 13, 8.5, 3, (170, 200, 120))  # Salbe
    b.rect(6, 9, 25, 11, HOLZ)  # Deckel daneben schräg? – als Band
    b.rect(10, 18, 21, 21, (190, 60, 50))  # Etikett


# ── Wirt ─────────────────────────────────────────────────────────────────────
KORN = (214, 178, 80)
KORN_D = (168, 128, 52)
ROGGEN = (176, 140, 74)


def aehren(b, farbe, dunkel, grannen):
    for x1 in (8, 16, 24):
        b.linie(16, 29, x1, 15, (150, 130, 60))
    for cx in (8, 16, 24):
        b.ellipse(cx, 10, 3, 6, dunkel)           # die Ähre als Ganzes
        for i in range(4):                        # Körner versetzt darauf
            b.ellipse(cx - 1 + (i % 2) * 2, 6 + i * 2.6, 1.6, 1.4, farbe)
        if grannen:
            b.linie(cx - 2, 5, cx - 4, 1, dunkel)
            b.linie(cx + 2, 5, cx + 4, 1, dunkel)
            b.linie(cx, 4, cx, 0, dunkel)
    b.rect(13, 22, 19, 24, (150, 110, 60))  # Bindfaden


def gerste(b):
    aehren(b, KORN, KORN_D, True)


def roggen(b):
    aehren(b, ROGGEN, (130, 98, 52), False)


def weingeist(b):
    flasche(b, (210, 230, 236), breit=6)
    b.px(18, 20, (255, 255, 255))


def koernermahlzeit(b):
    b.ellipse(16, 18, 12, 4, (220, 196, 140))  # Brei
    b.vieleck([(4, 18), (28, 18), (24, 26), (8, 26)], HOLZ)  # Schüssel
    for x, y in ((11, 17), (15, 16), (19, 17), (22, 18), (13, 19)):
        b.px(x, y, (180, 150, 90))  # Körner
    b.linie(20, 17, 27, 6, HOLZ_H, 2)  # Löffel


def humpen(b, koerper, band, schaum, inhalt):
    b.rect(8, 10, 21, 27, koerper)
    b.ring(23, 18, 4.5, 5, 1.6, koerper)
    for y in (13, 23):
        b.rect(8, y, 21, y, band)
    b.ellipse(14.5, 9, 7.5, 3, schaum)
    b.ellipse(11, 7, 3, 2.5, schaum)
    b.ellipse(17, 7, 3, 2.5, schaum)
    b.rect(9, 11, 10, 22, inhalt)  # durchscheinendes Bier am Rand


def duennbier(b):
    humpen(b, HOLZ, EISEN, (244, 238, 220), (220, 180, 70))


def starkbier(b):
    humpen(b, TON, (110, 70, 40), (236, 226, 200), (120, 70, 30))


def kraeuterlikoer(b):
    flasche(b, (70, 140, 60), hals=(90, 150, 80), korken=HOLZ_D, breit=6)
    b.rect(12, 19, 19, 23, (230, 220, 180))  # Etikett
    b.ellipse(15.5, 21, 1.6, 1.4, KRAUT)


def festbier(b):
    b.ellipse(16, 17, 11, 11, HOLZ)  # Fass von vorn
    for x in (9, 16, 23):
        b.linie(x, 7, x, 27, HOLZ_D)
    for y in (10, 24):
        b.rect(6, y, 26, y + 1, EISEN)
    b.rect(14, 18, 18, 20, EISEN_D)  # Zapfhahn
    b.rect(16, 20, 17, 23, EISEN_D)


def schaedelbrand(b):
    b.ellipse(16, 21, 7, 7, (60, 40, 40))  # dunkle Flasche
    b.rect(14, 6, 17, 15, (60, 40, 40))
    b.rect(13, 3, 18, 6, ROT)
    # Totenkopf auf dem Etikett
    b.ellipse(16, 20, 3.4, 3, WEISS)
    b.rect(14, 22, 18, 24, WEISS)
    b.px(15, 20, KONTUR)
    b.px(17, 20, KONTUR)
    b.px(16, 23, KONTUR)


# ── Prediger ─────────────────────────────────────────────────────────────────
PERGAMENT = (232, 218, 176)
PERGAMENT_D = (196, 176, 128)
WACHS = (236, 206, 112)


def lumpen(b):
    # Ein Bündel alter Stofffetzen, mit Schnur zusammengebunden
    b.vieleck([(4, 20), (10, 11), (17, 14), (13, 25), (6, 26)], (150, 130, 110))
    b.vieleck([(12, 12), (22, 8), (28, 17), (21, 24), (14, 21)], (120, 120, 150))
    b.vieleck([(9, 19), (20, 18), (27, 25), (17, 28), (8, 27)], (170, 150, 120))
    b.linie(6, 22, 26, 19, (90, 70, 50), 1)
    for x, y in ((14, 16), (20, 13), (12, 25), (22, 24)):
        b.px(x, y, (200, 190, 170))


def wachs(b):
    # Wabe: zwei Reihen goldener Sechsecke
    for cx, cy in ((10, 11), (19, 11), (5.5, 18), (14.5, 18), (23.5, 18), (10, 25), (19, 25)):
        b.vieleck([(cx - 4, cy), (cx - 2, cy - 3.5), (cx + 2, cy - 3.5), (cx + 4, cy), (cx + 2, cy + 3.5), (cx - 2, cy + 3.5)], WACHS)
        b.ellipse(cx, cy, 1.6, 1.6, (200, 150, 50))


def papier(b):
    # Ein Stapel Bögen, der oberste leicht verdreht
    b.vieleck([(5, 15), (23, 11), (27, 23), (9, 27)], PERGAMENT_D)
    b.vieleck([(4, 12), (22, 9), (26, 21), (8, 24)], PERGAMENT)
    for y in (14, 17, 20):
        b.linie(9, y + 0, 21, y - 2, (205, 190, 150))


def kerze(b):
    b.ellipse(16, 27, 9, 2.5, EISEN)  # Leuchter
    b.rect(14, 23, 18, 26, EISEN_D)
    b.rect(12, 9, 20, 23, (240, 228, 196))  # Kerze
    b.rect(12, 9, 13, 14, (220, 200, 160))  # Wachstropfen
    b.linie(16, 6, 16, 8, KONTUR)
    b.ellipse(16, 4.5, 1.8, 2.8, (255, 196, 70))  # Flamme
    b.px(16, 4, (255, 245, 200))


def buch(b, deckel, schnitt, zier):
    b.vieleck([(6, 6), (24, 4), (27, 25), (9, 28)], deckel)
    b.vieleck([(24, 4), (27, 6), (29, 26), (27, 25)], schnitt)
    b.linie(9, 28, 27, 25, schnitt)
    zier(b)


def lehrschrift(b):
    # Aufgerollte Schriftrolle mit Zeilen
    b.rect(7, 8, 25, 24, PERGAMENT)
    b.ellipse(7, 16, 2.5, 9, PERGAMENT_D)
    b.ellipse(25, 16, 2.5, 9, PERGAMENT_D)
    for y in range(11, 23, 3):
        b.linie(11, y, 21, y, (90, 70, 50))
    b.rect(11, 22, 13, 25, ROT)  # Siegel
    b.ellipse(12, 26, 1.8, 1.4, ROT)


def hetzschrift(b):
    # Flugblatt mit roter Fratze, an einen Pfahl genagelt
    b.rect(14, 3, 17, 29, HOLZ)
    b.vieleck([(5, 6), (26, 5), (25, 24), (6, 25)], PERGAMENT)
    b.ellipse(15.5, 13, 5, 5, ROT)
    b.px(13, 12, KONTUR)
    b.px(18, 12, KONTUR)
    b.linie(12, 9, 14, 11, KONTUR)  # böse Brauen
    b.linie(19, 9, 17, 11, KONTUR)
    b.linie(14, 15, 17, 15, KONTUR)  # Mundwinkel nach unten
    b.px(13, 16, KONTUR)
    b.px(18, 16, KONTUR)
    for y in (20, 22):
        b.linie(9, y, 22, y, (90, 70, 50))
    b.px(15, 6, EISEN_D)


def psalter(b):
    def zier(b):
        b.rect(12, 9, 20, 22, (150, 40, 34))  # Lederfeld
        b.rect(15, 11, 17, 20, GOLD)  # Kreuz
        b.rect(13, 13, 19, 14, GOLD)
        for x, y in ((8, 7), (22, 5), (10, 25), (24, 23)):
            b.ellipse(x + 0.5, y + 0.5, 1.4, 1.4, EISEN)  # Buchbeschläge
    buch(b, LEDER, PERGAMENT, zier)


def stundenbuch(b):
    def zier(b):
        b.rect(11, 9, 22, 22, (40, 70, 140))  # blaues Feld
        b.ellipse(16.5, 15.5, 3.6, 3.6, GOLD)  # goldene Rosette
        b.ellipse(16.5, 15.5, 1.5, 1.5, ROT)
        for x, y in ((8, 7), (22, 5), (10, 25), (24, 23)):
            b.ellipse(x + 0.5, y + 0.5, 1.6, 1.6, GOLD)
    buch(b, (110, 40, 60), (240, 220, 150), zier)


def reliquiar(b):
    # Kleiner goldener Schrein mit Satteldach und Silberkreuz
    b.rect(6, 15, 26, 27, GOLD)
    b.vieleck([(5, 15), (16, 7), (27, 15)], GOLD_D)
    b.rect(10, 18, 14, 24, EDELSTEIN)
    b.rect(18, 18, 22, 24, ROT)
    b.rect(15, 2, 16, 8, SILBER)
    b.rect(13, 4, 18, 5, SILBER)
    b.rect(6, 27, 26, 28, GOLD_D)



# ── Parfumeur ────────────────────────────────────────────────────────────────
LAVENDEL = (140, 110, 200)
LAVENDEL_H = (176, 150, 226)
JASMIN = (246, 244, 230)
MOSCHUS = (150, 96, 60)


def lavendel(b):
    for x1 in (9, 13, 17, 21, 24):
        b.linie(16, 28, x1, 10, KRAUT, 1)
    for x, top in ((9, 5), (13, 3), (17, 2), (21, 3), (24, 6)):
        for i in range(5):
            b.ellipse(x + (0.5 if i % 2 else -0.5), top + 1.5 + i * 1.6, 1.6, 1.1, LAVENDEL_H if i % 2 else LAVENDEL)
    b.rect(13, 22, 19, 23, (180, 150, 90))  # Bindfaden


def jasmin(b):
    b.linie(6, 27, 24, 11, KRAUT, 2)
    for cx, cy in ((10, 12), (20, 20), (24, 9)):
        for dx, dy in ((0, -3), (3, -1), (2, 3), (-2, 3), (-3, -1)):
            b.ellipse(cx + dx, cy + dy, 2.2, 2.2, JASMIN)
        b.ellipse(cx, cy, 1.4, 1.4, GOLD)
    for cx, cy in ((14, 22), (7, 17)):
        b.ellipse(cx, cy, 3, 1.6, KRAUT_H)  # Blätter


def politur(b):
    b.ellipse(16, 10, 10, 3, (190, 170, 120))
    b.rect(6, 10, 26, 24, (150, 120, 70))  # Dose
    b.ellipse(16, 24, 10, 3, (150, 120, 70))
    b.ellipse(16, 10, 9, 2.4, (226, 200, 120))  # glänzende Paste
    b.px(12, 9, (255, 246, 200))
    b.px(13, 9, (255, 246, 200))
    b.rect(6, 15, 26, 16, LAVENDEL)  # Etikett


def blume_der_zwietracht(b):
    b.linie(16, 28, 16, 13, (60, 90, 40), 2)
    b.ellipse(11, 22, 3.5, 1.6, (70, 110, 50))
    for dx, dy in ((0, -5), (5, -2), (3, 4), (-3, 4), (-5, -2)):
        b.ellipse(16 + dx, 10 + dy, 3.4, 3.4, (110, 30, 70))
    b.ellipse(16, 10, 2.4, 2.4, (40, 20, 30))
    for x, y in ((22, 20), (24, 24), (9, 26)):
        b.px(x, y, (200, 60, 100))  # Blütenstaub


def dolch(b, klinge, griff, knauf, gift):
    b.vieleck([(14, 4), (18, 4), (18, 18), (16, 21), (14, 18)], klinge)
    b.linie(16, 6, 16, 17, misch(klinge, (255, 255, 255), 0.4))
    b.rect(10, 18, 22, 20, griff)  # Parierstange
    b.rect(14, 21, 18, 27, LEDER)
    b.ellipse(16, 28, 2.4, 2, knauf)
    for x, y in gift:
        b.px(x, y, GIFTGRUEN)


def giftdolch(b):
    dolch(b, (200, 204, 214), EISEN_D, EISEN, ((15, 13), (17, 15), (16, 18), (17, 10)))


def schattendolch(b):
    dolch(b, (60, 58, 76), (40, 38, 50), EDELSTEIN, ((15, 9), (17, 12), (15, 15), (16, 18), (17, 6)))


def duftflakon(b, inhalt, verschluss, akzent=None):
    b.vieleck([(16, 9), (25, 17), (16, 28), (7, 17)], GLAS)  # rautenförmiges Fläschchen
    b.vieleck([(16, 12), (22, 18), (16, 25), (10, 18)], inhalt)
    b.rect(14, 5, 17, 9, GLAS)
    b.ellipse(15.5, 4, 3, 2.4, verschluss)
    b.px(11, 16, (240, 250, 255))
    if akzent:
        akzent(b)


def moschusduft(b):
    duftflakon(b, MOSCHUS, GOLD)


def fechterduft(b):
    duftflakon(b, (90, 130, 190), SILBER, lambda b: b.linie(4, 28, 27, 5, (210, 214, 224), 1))


def heiligenduft(b):
    duftflakon(b, (236, 220, 150), GOLD, lambda b: (b.ring(16, 18, 13, 13, 1, GOLD), b.rect(15, 15, 16, 21, WEISS), b.rect(13, 17, 18, 18, WEISS)))


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


def zeichnen(funktion):
    b = Bild()
    funktion(b)
    b.schattieren()
    b.kontur()
    return b.bild()


def main():
    wurzel = os.path.join(os.path.dirname(__file__), "..", "..")
    ziel = os.path.join(wurzel, "game", "assets", "oberflaeche", "icons")
    os.makedirs(ziel, exist_ok=True)
    bilder = {}
    for name, f in WAREN.items():
        bilder[name] = zeichnen(f)
        bilder[name].save(os.path.join(ziel, dateiname(name)))
    print(f"{len(bilder)} Icons nach {os.path.normpath(ziel)} geschrieben")

    if "--vorschau" in sys.argv:
        pfad = sys.argv[sys.argv.index("--vorschau") + 1]
        spalten, gross = 9, 4
        zeilen = (len(bilder) + spalten - 1) // spalten
        bogen = Image.new("RGBA", (spalten * N * gross + spalten * 8, zeilen * N * gross + zeilen * 8), (60, 54, 46, 255))
        for i, im in enumerate(bilder.values()):
            x, y = (i % spalten) * (N * gross + 8) + 4, (i // spalten) * (N * gross + 8) + 4
            bogen.alpha_composite(im.resize((N * gross, N * gross), Image.NEAREST), (x, y))
        bogen.save(pfad)


if __name__ == "__main__":
    main()
