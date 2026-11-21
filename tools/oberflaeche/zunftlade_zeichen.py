#!/usr/bin/env python3
"""Setzt die Zeichen der Fächer der Zunftlade als alten Messingbeschlag um (SVG).

Aufruf:  python3 tools/oberflaeche/zunftlade_zeichen.py [zielordner]
Ziel ohne Angabe: game/assets/oberflaeche/zunftlade/symbole

Die Formen stammen von game-icons.net (Lorc, Delapouite; CC BY 3.0) und liegen unverändert in
tools/oberflaeche/zunftlade_zeichen_quellen/ (Zuordnung und Lizenz: LIZENZ.md dort). Dieses Skript
liest aus jeder Quelle den weißen Pfad und zeichnet ihn als Beschlag auf dem dunklen Holz des Fachs.
Die Tiefe entsteht nur aus gezeichneten Flächen, ohne Filter, Verlauf oder Shader:
  1. Schlagschatten (nach rechts unten versetzt, halb durchsichtig),
  2. Schmutzrand (dunkle Patina rund um die Form, in der Fuge zum Holz),
  3. Schattenkante (dunkel, nach rechts unten versetzt),
  4. Lichtkante (blank gerieben, nach links oben versetzt),
  5. die Fläche in gedämpftem Messing.
Das Licht kommt wie bei den Kanten der Fächer von links oben.

Handgeschmiedet statt gezirkelt: Alle Punkte der Form werden sanft verzogen (eine feste Welle über die
Fläche, je Zeichen anders versetzt, kein Zufall – jeder Lauf schreibt dieselben Dateien). Die Form
bleibt erkennbar, die Kanten sind nur leicht ungerade.

Zeichenfläche der Quellen 512 x 512, Ausgabe 64 x 64 Pixel mit etwas Rand für Schatten und Schmutzrand.
"""
import hashlib
import math
import os
import re
import sys

MESSING = "#b89a5e"
LICHT = "#ecd9a2"          # blank geriebene Kante
SCHATTENKANTE = "#33240e"
SCHLAGSCHATTEN = "#0a0502"
PATINA = "#27240f"         # Schmutz und Grünspan in der Fuge
KANTE = 11         # Versatz der Licht- und Schattenkante (Einheiten der Quelle, 512 = ganze Breite)
SCHATTEN = 20      # Versatz des Schlagschattens
SCHMUTZ = 34       # Strichbreite des Schmutzrands (die Hälfte ragt über die Form hinaus)
UNRUHE = 5.0       # so weit wandert ein Punkt höchstens
RAND = 24          # Luft um die Zeichenfläche

NAMEN = ["familie", "betriebe", "bauen", "rat", "bericht", "lexikon", "personal", "produktion", "lager", "karren",
         "talentbaum", "stadt", "markt", "hinterzimmer", "menue"]


def verzug(name):
    """Liefert die Welle des Zeichens name: (x, y) -> Versatz (dx, dy). Sanft, darum bleiben Bögen glatt."""
    h = hashlib.md5(name.encode()).digest()
    p = [b / 255 * 2 * math.pi for b in h[:4]]

    def welle(x, y):
        dx = (math.sin(x / 61 + y / 97 + p[0]) + math.sin(y / 43 + p[1])) / 2
        dy = (math.sin(y / 67 + x / 89 + p[2]) + math.sin(x / 47 + p[3])) / 2
        return dx * UNRUHE, dy * UNRUHE
    return welle


def unruhig(name, d):
    """Liest einen SVG-Pfad, macht alle Befehle absolut und verzieht jeden Punkt mit der Welle des Zeichens."""
    welle = verzug(name)
    teile = re.findall(r"[MmLlHhVvCcSsAaZz]|-?(?:\d+\.?\d*|\.\d+)(?:e-?\d+)?", d)
    if re.search(r"[QqTt]", d):
        raise ValueError(f"{name}: Pfadbefehl Q/T wird nicht gelesen")
    aus, i, x, y, start, befehl, letzter = [], 0, 0.0, 0.0, (0.0, 0.0), "", None

    def zahl():
        nonlocal i
        i += 1
        return float(teile[i - 1])

    def p(px, py):
        dx, dy = welle(px, py)
        return f"{px + dx:.1f} {py + dy:.1f}"

    while i < len(teile):
        if teile[i].isalpha():
            befehl = teile[i]
            i += 1
            if befehl in "Zz":
                aus.append("z")
                x, y = start
                letzter = None
                continue
        rel = befehl.islower()
        art = befehl.upper()
        if art == "M":
            nx, ny = zahl(), zahl()
            if rel: nx, ny = x + nx, y + ny
            aus.append("M" + p(nx, ny)); x, y = nx, ny; start = (x, y)
            befehl = "l" if rel else "L"
            letzter = None
        elif art in "LHV":
            nx, ny = x, y
            if art == "L":
                nx, ny = zahl(), zahl()
                if rel: nx, ny = x + nx, y + ny
            elif art == "H":
                nx = zahl() + (x if rel else 0)
            else:
                ny = zahl() + (y if rel else 0)
            aus.append("L" + p(nx, ny)); x, y = nx, ny
            letzter = None
        elif art in "CS":
            if art == "C":
                x1, y1 = zahl(), zahl()
                if rel: x1, y1 = x + x1, y + y1
            else:
                x1, y1 = (2 * x - letzter[0], 2 * y - letzter[1]) if letzter else (x, y)
            x2, y2, nx, ny = zahl(), zahl(), zahl(), zahl()
            if rel: x2, y2, nx, ny = x + x2, y + y2, x + nx, y + ny
            aus.append(f"C{p(x1, y1)} {p(x2, y2)} {p(nx, ny)}")
            letzter = (x2, y2); x, y = nx, ny
        elif art == "A":
            rx, ry, dreh, gross, richtung, nx, ny = zahl(), zahl(), zahl(), zahl(), zahl(), zahl(), zahl()
            if rel: nx, ny = x + nx, y + ny
            aus.append(f"A{rx:g} {ry:g} {dreh:g} {int(gross)} {int(richtung)} {p(nx, ny)}")
            x, y = nx, ny
            letzter = None
        else:
            raise ValueError(f"{name}: unbekannter Pfadbefehl {befehl}")
    return "".join(aus)


def zeichnen(name, quelle):
    pfade = re.findall(r'<path fill="#fff"[^>]* d="([^"]+)"', quelle)
    if not pfade:
        raise ValueError(f"{name}: kein weißer Pfad in der Quelle")
    formen = [unruhig(name, d) for d in pfade]

    def form(farbe, zusatz=""):
        return "".join(f'<path fill="{farbe}"{zusatz} d="{d}"/>' for d in formen)

    seite = 512 + 2 * RAND
    return "".join([
        f'<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="{-RAND} {-RAND} {seite} {seite}">',
        f'<g transform="translate({SCHATTEN} {SCHATTEN})" opacity="0.5">{form(SCHLAGSCHATTEN)}</g>',
        form("none", f' stroke="{PATINA}" stroke-width="{SCHMUTZ}" stroke-linejoin="round"'),
        f'<g transform="translate({KANTE} {KANTE})">{form(SCHATTENKANTE)}</g>',
        f'<g transform="translate(-{KANTE} -{KANTE})">{form(LICHT)}</g>',
        form(MESSING),
        "</svg>\n"])


def main():
    hier = os.path.dirname(os.path.abspath(__file__))
    quellen = os.path.join(hier, "zunftlade_zeichen_quellen")
    ziel = sys.argv[1] if len(sys.argv) > 1 else os.path.join(hier, "..", "..", "game", "assets", "oberflaeche", "zunftlade", "symbole")
    os.makedirs(ziel, exist_ok=True)
    for name in NAMEN:
        with open(os.path.join(quellen, name + ".svg"), encoding="utf-8") as datei:
            quelle = datei.read()
        with open(os.path.join(ziel, name + ".svg"), "w", encoding="utf-8") as datei:
            datei.write(zeichnen(name, quelle))
    print(f"{len(NAMEN)} Zeichen nach {os.path.normpath(ziel)}")


if __name__ == "__main__":
    main()
