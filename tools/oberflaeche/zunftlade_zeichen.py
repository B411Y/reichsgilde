#!/usr/bin/env python3
"""Zeichnet die plastischen Zeichen der Fächer der Zunftlade als SVG.

Aufruf:  python3 tools/oberflaeche/zunftlade_zeichen.py [zielordner]
Ziel ohne Angabe: game/assets/oberflaeche/zunftlade/symbole

Jedes Zeichen ist ein alter, abgegriffener Beschlag aus Messing auf dem dunklen Holz des Fachs. Die Tiefe
entsteht nur aus gezeichneten Flächen, ohne Filter, Verlauf oder Shader. Je Teil, von hinten nach vorn:
  1. Schlagschatten aller Teile (nach rechts unten versetzt, halb durchsichtig),
  2. Schmutzrand (dunkle Patina rund um das Teil: in der Fuge zum Holz und zum Teil dahinter),
  3. Schattenkante (dunkel, nach rechts unten versetzt),
  4. Lichtkante (blank gerieben, nach links oben versetzt),
  5. die Fläche selbst in einem von drei Tönen (a = vorn, b = dahinter, c = tief mit Patina).
Darauf die Spuren des Alters: Rillen (dunkler Strich, heller Strich daneben), blank geriebene Stellen,
Dellen mit Patina und Kratzer. Das Licht kommt wie bei den Kanten der Fächer von links oben.

Handgeschmiedet statt gezirkelt: Jeder Eckpunkt wird um einen festen kleinen Betrag verschoben (aus dem
Namen und dem Ort gerechnet, kein Zufall – jeder Lauf schreibt dieselben Dateien). Gleiche Punkte
verschieben sich gleich, darum bleiben Flächen geschlossen und Nachbarn passen aneinander.

Zeichenfläche 24 x 24, Ausgabe 48 x 48 Pixel.
"""
import hashlib
import math
import os
import re
import sys

TON = {"a": "#b89a5e", "b": "#8d7342", "c": "#5f5532"}
LICHT = "#ecd9a2"          # blank geriebene Kante
BLANK = "#d4bc80"          # blank geriebene Stelle auf der Fläche
SCHATTENKANTE = "#33240e"
SCHLAGSCHATTEN = "#0a0502"
PATINA = "#27240f"         # Schmutz und Grünspan in Fugen, Rillen und Dellen
KANTE = 0.55       # Versatz der Licht- und Schattenkante
SCHATTEN = 1.0     # Versatz des Schlagschattens
SCHMUTZ = 1.7      # Strichbreite des Schmutzrands (die Hälfte ragt über das Teil hinaus)
UNRUHE = 0.27      # so weit wandert ein Eckpunkt höchstens


def versatz(name, x, y, weite=UNRUHE):
    """Fester Versatz für den Punkt (x, y) des Zeichens name."""
    h = hashlib.md5(f"{name}:{round(x * 4)}:{round(y * 4)}".encode()).digest()
    return (h[0] / 127.5 - 1) * weite, (h[1] / 127.5 - 1) * weite


def unruhig(name, d):
    """Liest einen SVG-Pfad, macht alle Befehle absolut und verschiebt jeden Eckpunkt leicht (Stützpunkte wandern mit ihrem Eckpunkt)."""
    teile = re.findall(r"[MmLlHhVvCcSsAaZz]|-?(?:\d+\.?\d*|\.\d+)", d)
    aus, i, x, y, start, befehl, letzter = [], 0, 0.0, 0.0, (0.0, 0.0), "", None

    def zahl():
        nonlocal i
        i += 1
        return float(teile[i - 1])

    def p(px, py, ax=None, ay=None):  # Punkt (px, py), verschoben wie sein Eckpunkt (ax, ay)
        dx, dy = versatz(name, px if ax is None else ax, py if ay is None else ay)
        return f"{px + dx:.2f} {py + dy:.2f}"

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
            aus.append(f"C{p(x1, y1, x, y)} {p(x2, y2, nx, ny)} {p(nx, ny)}")
            letzter = (x2, y2); x, y = nx, ny
        elif art == "A":
            rx, ry, dreh, gross, richtung, nx, ny = zahl(), zahl(), zahl(), zahl(), zahl(), zahl(), zahl()
            if rel: nx, ny = x + nx, y + ny
            aus.append(f"A{rx:g} {ry:g} {dreh:g} {int(gross)} {int(richtung)} {p(nx, ny)}")
            x, y = nx, ny
            letzter = None
    return "".join(aus)


def flaeche(d, regel=False):
    return {"d": d, "regel": regel}


def strich(d, breite, ende="round"):
    return {"d": d, "breite": breite, "ende": ende}


def rund(x, y, r):
    """Kreis als vier Viertelbögen, damit seine vier Punkte wandern können."""
    return f"M{x - r} {y}A{r} {r} 0 0 1 {x} {y - r}A{r} {r} 0 0 1 {x + r} {y}A{r} {r} 0 0 1 {x} {y + r}A{r} {r} 0 0 1 {x - r} {y}z"


def kreis(x, y, r):
    return {"d": rund(x, y, r)}


def ring(x, y, r, breite):
    return {"d": rund(x, y, r), "breite": breite, "ende": "round"}


def gedreht(winkel, teil):
    return dict(teil, dreh=winkel)


def zahnrad():
    """Zahnrad mit acht Zähnen und Loch in der Mitte."""
    punkte = []
    for i in range(8):
        mitte = i * math.pi / 4
        for winkel, radius in ((-0.25, 7.0), (-0.17, 9.6), (0.17, 9.6), (0.25, 7.0)):
            w = mitte + winkel
            punkte.append(f"{12 + radius * math.sin(w):.2f} {12 - radius * math.cos(w):.2f}")
    return "M" + "L".join(punkte) + "z" + rund(12, 12, 3.2)


def element(name, teil, farbe, breite=None, rauh=True):
    """Ein Teil als SVG-Element in einer Farbe; breite zeichnet statt der Fläche ihren Umriss (Schmutzrand)."""
    d = unruhig(name, teil["d"]) if rauh else teil["d"]
    if "breite" in teil or breite is not None:
        b = teil.get("breite", 0) + (breite or 0)
        aus = f'<path fill="none" stroke="{farbe}" stroke-width="{b:g}" stroke-linecap="{teil.get("ende", "round")}" stroke-linejoin="round" d="{d}"/>'
    else:
        regel = ' fill-rule="evenodd"' if teil.get("regel") else ""
        aus = f'<path fill="{farbe}"{regel} d="{d}"/>'
    return f'<g transform="rotate({teil["dreh"]} 12 12)">{aus}</g>' if "dreh" in teil else aus


def fleck(name, x, y, rx, ry):
    """Unregelmäßiges Vieleck um (x, y): blank geriebene Stelle oder Delle."""
    punkte = []
    for k in range(7):
        w = k * 2 * math.pi / 7
        dx, dy = versatz(name, x * 3 + k, y * 5 + k, 0.3)
        f = 1 + dx
        punkte.append(f"{x + rx * f * math.cos(w):.2f} {y + ry * f * math.sin(w):.2f}")
    return "M" + "L".join(punkte) + "z"


# name: (Teile von hinten nach vorn, Rillen)
ZEICHEN = {
    # Zwei Büsten: vorn das Oberhaupt, dahinter kleiner der Ehepartner.
    "familie": ([
        ("b", kreis(16.9, 8.4, 2.7)),
        ("b", flaeche("M12.8 20.5c0-4.6 1.5-7.2 4.2-7.2 2.800 0 4.700 2.500 4.700 7.200z")),
        ("a", kreis(8.4, 6.9, 3.4)),
        ("a", flaeche("M2.200 20.500c0-4.700 2.600-7.400 6.200-7.400s6.200 2.700 6.200 7.400z")),
    ], []),
    # Schmiedehammer, schräg gehalten: Stiel aus Holz, Kopf aus Eisen.
    "betriebe": ([
        ("b", gedreht(40, flaeche("M11 8h2l.300 13.800h-2.600z"))),
        ("a", gedreht(40, flaeche("M4.800 2.600h11.700l2.700 1.900v3l-2.700 1.900H4.800z"))),
    ], [gedreht(40, strich("M7.800 3.500v5", 0.8))]),
    # Haus mit Giebel und Tür.
    "bauen": ([
        ("b", flaeche("M4.600 11h14.800v10H14v-5.600h-4V21H4.600z")),
        ("a", flaeche("M12 2.300 1.800 11.600h20.400z")),
        ("b", flaeche("M16.200 3.400h2.300v4.300l-2.300-2.100z")),
    ], ["M6.500 14.300h2M15.500 14.300h2"]),
    # Rathaus: Staffelgiebel über drei Pfeilern.
    "rat": ([
        ("b", flaeche("M4.400 11h2.800v8H4.400z")),
        ("b", flaeche("M10.600 11h2.800v8h-2.800z")),
        ("b", flaeche("M16.800 11h2.800v8h-2.800z")),
        ("a", flaeche("M10.200 2.300h3.600v2.500h2.800v2.500h2.800v2.300h2.800v2.300H1.800V9.600h2.800V7.300h2.800V4.800h2.800z")),
        ("a", flaeche("M1.800 18.400h20.400V21H1.800z")),
    ], []),
    # Schriftrolle, oben und unten eingerollt, mit drei Zeilen.
    "bericht": ([
        ("a", flaeche("M5.800 4.500h12.400v15H5.800z")),
        ("b", flaeche("M4.300 2.300h15.400a1.700 1.700 0 0 1 0 3.400H4.300a1.700 1.700 0 0 1 0-3.400z")),
        ("b", flaeche("M4.300 18.300h15.400a1.700 1.700 0 0 1 0 3.400H4.300a1.700 1.700 0 0 1 0-3.400z")),
    ], ["M8.300 8.600h7.400M8.300 11.600h7.400M8.300 14.600h4.600"]),
    # Aufgeschlagenes Buch: Deckel, darauf zwei gewölbte Seiten.
    "lexikon": ([
        ("c", flaeche("M1.800 6.600h20.400v14H1.800z")),
        ("a", flaeche("M3 4.800c3-1 5.800-.700 8.300 1.100v13.300c-2.500-1.700-5.300-1.900-8.300-1z")),
        ("a", flaeche("M21 4.800c-3-1-5.800-.700-8.300 1.100v13.300c2.500-1.700 5.300-1.900 8.300-1z")),
    ], ["M5 8.700c1.500-.300 3-.100 4.300.500M5 11.900c1.500-.300 3-.100 4.300.500M14.700 9.200c1.300-.600 2.800-.800 4.300-.500M14.700 12.400c1.300-.600 2.800-.800 4.300-.500"]),
    # Geselle: Büste mit Kappe und Schurz.
    "personal": ([
        ("a", flaeche("M3.800 20.500c0-5 3.400-7.600 8.200-7.600s8.200 2.600 8.200 7.600z")),
        ("b", flaeche("M9.200 14.400h5.600v6.100H9.200z")),
        ("a", kreis(12, 8, 3.6)),
        ("b", flaeche("M7.400 6.700c.200-2.900 2-4.500 4.600-4.500s4.400 1.600 4.600 4.500z")),
    ], []),
    # Amboss mit Horn.
    "produktion": ([
        ("b", flaeche("M9.600 12.500h5.800v4.500l3.600 2v2H5v-2l4.600-2z")),
        ("a", flaeche("M1.800 6.500h20.400v2.300l-3.200 1.500v2.500H8.600c-3.600 0-5.800-2.100-6.800-6.300z")),
    ], []),
    # Kiste, schräg von oben: Deckel hell, linke Seite, rechte Seite im Schatten.
    "lager": ([
        ("b", flaeche("M2.500 8.100 11.400 12v9.600l-8.900-4z")),
        ("c", flaeche("M21.500 8.100 12.600 12v9.600l8.900-4z")),
        ("a", flaeche("M12 2.300l9.300 4.100L12 10.500 2.700 6.400z")),
    ], ["M2.900 12.800l8.100 3.600M21.100 12.800 13 16.400"]),
    # Zweirädriger Karren von der Seite: Kasten, Deichsel, Rad mit Nabe.
    "karren": ([
        ("b", flaeche("M16 10.300l6.200-2.700v2l-6.200 2.800z")),
        ("a", flaeche("M1.800 5.500h15.400l-1 8H2.800z")),
        ("c", kreis(9.5, 15.5, 5.4)),
        ("b", ring(9.5, 15.5, 4.5, 1.9)),
        ("b", strich("M9.500 11.500v8M5.500 15.500h8", 1.1, "butt")),
        ("a", kreis(9.5, 15.5, 1.5)),
    ], ["M4.300 7.500h10.500"]),
    # Baum mit drei Knoten: Stamm und Äste, an den Enden die Talente.
    "talentbaum": ([
        ("b", strich("M12 20.800V6M12 15.500 5.600 10.800M12 15.500l6.400-4.700", 2)),
        ("b", flaeche("M7.500 19.800h9v2h-9z")),
        ("a", kreis(12, 5, 2.9)),
        ("a", kreis(5.2, 10.2, 2.9)),
        ("a", kreis(18.8, 10.2, 2.9)),
    ], []),
    # Stadttor: Turm mit Zinnen und offenem Bogen.
    "stadt": ([
        ("a", flaeche("M4.500 21V2.500h3v2.300h3V2.500h3v2.300h3V2.500h3V21H15v-6a3 3 0 0 0-6 0v6z")),
        ("b", flaeche("M3.200 8.300h17.600v1.900H3.200z")),
    ], ["M7 13h1.500M15.500 13H17"]),
    # Waage: Ständer, Balken, zwei Schalen an Schnüren.
    "markt": ([
        ("b", flaeche("M11 4.500h2v14h-2z")),
        ("b", strich("M5.300 6.300 2.600 12.500M5.300 6.300 8 12.500M18.700 6.300 16 12.500M18.700 6.300l2.700 6.200", 0.9)),
        ("a", flaeche("M3.300 5.200h17.400V7H3.300z")),
        ("a", flaeche("M1.500 12.500h7.600a3.800 3.800 0 0 1-7.600 0z")),
        ("a", flaeche("M14.900 12.500h7.600a3.800 3.800 0 0 1-7.600 0z")),
        ("a", flaeche("M6.800 18.500h10.400V21H6.800z")),
        ("a", kreis(12, 4.2, 1.9)),
    ], []),
    # Halbmaske mit zwei Augenlöchern.
    "hinterzimmer": ([
        ("a", flaeche("M1.800 7.800c4-2 6.200-2 10.200 0 4-2 6.200-2 10.200 0-.500 6.200-3 9.400-6.100 9.400-2 0-3.100-1.500-4.100-3.100-1 1.600-2.100 3.100-4.100 3.100-3.100 0-5.600-3.200-6.100-9.400zM5.700 11.500a2.200 1.700 0 1 0 4.400 0 2.200 1.700 0 1 0-4.400 0zM13.900 11.500a2.200 1.700 0 1 0 4.400 0 2.200 1.700 0 1 0-4.400 0z", True)),
    ], []),
    # Zahnrad.
    "menue": ([
        ("a", flaeche(zahnrad(), True)),
        ("b", ring(12, 12, 4.6, 1.4)),
    ], []),
}


# Spuren des Alters je Zeichen: blank = geriebene Stellen (x, y, halbe Breite, halbe Höhe), dellen = (x, y, Radius), kratzer = Pfad,
# dreh = Winkel, wenn die Spuren auf einem gedrehten Teil sitzen.
SPUREN = {
    "familie": dict(blank=[(7.4, 5.8, 1.4, 1.0), (7, 15.8, 2, 1.1)], dellen=[(10.2, 8.2, .5), (11.5, 17.8, .6), (18.5, 17, .5)], kratzer="M4.500 18.200l2.200 1"),
    "betriebe": dict(blank=[(10, 4.6, 2.6, .9)], dellen=[(15.5, 7.4, .6), (5.6, 3.4, .55), (12, 15, .45)], kratzer="M13.500 5l2.500 1.600", dreh=40),
    "bauen": dict(blank=[(10.5, 7.5, 2, 1.1)], dellen=[(15, 9.6, .55), (6.2, 18.5, .6), (17.8, 16.5, .5)], kratzer="M7.500 10l2-1.600"),
    "rat": dict(blank=[(11.5, 6.5, 2.2, 1), (8, 19.6, 2.4, .5)], dellen=[(17.5, 9.8, .55), (5, 10.5, .5), (18.5, 19.9, .5)], kratzer="M13 8.600l2.400.900"),
    "bericht": dict(blank=[(9, 3.8, 3, .6), (10, 16.6, 2.4, .7)], dellen=[(16.3, 12.6, .55), (18.6, 4.2, .5), (6.5, 20.2, .5)], kratzer="M14 16l2 1"),
    "lexikon": dict(blank=[(6.5, 6.4, 2, .7), (16.5, 15.8, 2, .8)], dellen=[(9.3, 16, .5), (19.5, 7, .45), (2.6, 19.6, .5)], kratzer="M5.500 15.500l2.300.600"),
    "personal": dict(blank=[(10.8, 8.6, 1.2, .9), (6.5, 16.5, 1.3, 1.5)], dellen=[(17.5, 17.5, .55), (13.5, 4, .5), (12, 18, .45)], kratzer="M5.300 18.800l1.800.800"),
    "produktion": dict(blank=[(11, 7.7, 4.5, .7)], dellen=[(18.5, 8.2, .55), (5.5, 9.5, .5), (12.5, 15, .5), (8, 19.6, .5)], kratzer="M13 10.300l3 .800"),
    "lager": dict(blank=[(11, 6, 2.6, .9), (5.5, 11.6, 1.6, .8)], dellen=[(15.5, 7, .5), (8.5, 19, .55), (18, 14.5, .5)], kratzer="M5 15.500l3 1.600"),
    "karren": dict(blank=[(6, 8.6, 2.6, .7)], dellen=[(14.6, 9.6, .5), (3.3, 6.6, .5), (20.5, 9.2, .4)], kratzer="M10.500 9.500l2.500.600"),
    "talentbaum": dict(blank=[(11.3, 4.2, 1.2, .9), (4.6, 9.5, 1.1, .9), (18.2, 9.5, 1.1, .9)], dellen=[(13.2, 6.2, .45), (6.4, 11.5, .45), (12, 18, .4)]),
    "stadt": dict(blank=[(7, 6.2, 1.6, .8), (6.7, 17, 1.2, 2)], dellen=[(17.5, 6.5, .5), (17.6, 18.5, .55), (12, 11.5, .5)], kratzer="M14 5.600l2 .900"),
    "markt": dict(blank=[(8, 6, 2.5, .4), (5.2, 14.4, 1.8, .7), (12, 19.6, 3, .5)], dellen=[(19.5, 14.5, .45), (15.5, 19.8, .45)]),
    "hinterzimmer": dict(blank=[(5, 9, 2, .7), (15.5, 8.8, 2.2, .6)], dellen=[(8.6, 15.4, .5), (19.5, 13.5, .5), (3.4, 9.6, .45)], kratzer="M14.500 14.800l2 .800"),
    "menue": dict(blank=[(8, 7.5, 1.6, .9)], dellen=[(17, 15.5, .5), (12, 3.4, .45), (6.3, 15.2, .45)], kratzer="M15.500 6.800l1.500 1.400"),
}


def zeichnen(name, teile, rillen):
    def versetzt(dx, dy, inhalt, zusatz=""):
        return f'<g transform="translate({dx:g} {dy:g})"{zusatz}>{inhalt}</g>'

    z = ['<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24">']
    z.append(versetzt(SCHATTEN, SCHATTEN, "".join(element(name, t, SCHLAGSCHATTEN) for _, t in teile), ' opacity="0.5"'))
    for ton, teil in teile:
        z.append(element(name, teil, PATINA, SCHMUTZ))
        z.append(versetzt(KANTE, KANTE, element(name, teil, SCHATTENKANTE)))
        z.append(versetzt(-KANTE, -KANTE, element(name, teil, LICHT)))
        z.append(element(name, teil, TON[ton]))
    spuren = SPUREN.get(name, {})
    dreh = {"dreh": spuren["dreh"]} if "dreh" in spuren else {}
    for x, y, rx, ry in spuren.get("blank", []):
        z.append(element(name, dict(dreh, d=fleck(name, x, y, rx, ry)), BLANK, rauh=False))
    for rille in rillen:
        teil = rille if isinstance(rille, dict) else strich(rille, 0.8)
        z.append(versetzt(0.45, 0.45, element(name, teil, LICHT)))
        z.append(element(name, teil, PATINA))
    for x, y, r in spuren.get("dellen", []):
        delle = dict(dreh, d=fleck(name, x, y, r, r))
        z.append(versetzt(0.22, 0.22, element(name, delle, LICHT, rauh=False)))
        z.append(element(name, delle, PATINA, rauh=False))
    if "kratzer" in spuren:
        z.append(element(name, dict(dreh, **strich(spuren["kratzer"], 0.3)), PATINA, rauh=False))
    z.append("</svg>")
    return "".join(z) + "\n"


def main():
    hier = os.path.dirname(os.path.abspath(__file__))
    ziel = sys.argv[1] if len(sys.argv) > 1 else os.path.join(hier, "..", "..", "game", "assets", "oberflaeche", "zunftlade", "symbole")
    os.makedirs(ziel, exist_ok=True)
    for name, (teile, rillen) in ZEICHEN.items():
        with open(os.path.join(ziel, name + ".svg"), "w", encoding="utf-8") as datei:
            datei.write(zeichnen(name, teile, rillen))
    print(f"{len(ZEICHEN)} Zeichen nach {os.path.normpath(ziel)}")


if __name__ == "__main__":
    main()
