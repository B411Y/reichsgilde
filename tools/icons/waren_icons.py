#!/usr/bin/env python3
"""
Zeichnet die Waren-Icons von Reichsgilde als 32×32-Pixel-Art (eigene Grafiken, MIT-Lizenz wie das Projekt).

Aufruf (aus dem Projektordner):
    python3 tools/icons/waren_icons.py              # schreibt game/assets/waren/<ware>.png
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
    ziel = os.path.join(wurzel, "game", "assets", "waren")
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
