"""Köln um 1400, Paket 3 der Wahrzeichen: Rathaus mit Ratsturm im Bau, Torburgen Eigelstein, Hahnen und Severin, Bayenturm,
Stapelhaus und Rheinkran (Schwimmkran). Nur für Köln; die Modelle der übrigen Karten bleiben.

    blender -b --python tools/modelle/koeln_stadtbauten.py -- [--bau=rathaus,mauer,stapelhaus,rheinkran] [--schnell] [--ohne-bilder]
                                                               [--aus=<ordner>]

Schreibt (Material-Bibliothek je Fläche wie beim Kölner Dom und den Kirchen, Stoffe Tuff, Trachyt, Basalt aus kirchen_baukasten.py):
    game/assets/modelle/gebaeude/koelner_rathaus.glb    Sonderbau 22 × 13 m: „Haus“ (Saalbau mit Hansasaal, um 1330, Walmdach ohne
                                                         Zinnenkranz, kleine Vorhalle von 1404 mit Zinnen und Stadtwappen), „Baustelle“
                                                         (Ratsturm 1407–14 als Stumpf des ersten Geschosses mit Gerüst, Galgenkran,
                                                         Werksteinen); Anker_Tuer vor der Vorhalle, Anker_Laterne an ihrem linken Pfeiler
    game/assets/modelle/gebaeude/stadtmauer_koeln.glb   Kölner Mauerbaukasten: Mauerstueck, Mauerturm, Eckturm und Tor wie stadtmauer.glb
                                                         (stadtmauer.py; das allgemeine Tor für Ehren-, Weyer-, Kunibert- und Bayentor), dazu
                                                         Tor_Eigelstein, Tor_Hahnen, Tor_Severin (die Darstellung wählt das Teil nach dem
                                                         Anfang des Tornamens) mit Anker_Tuer_<Wort> und Anker_Feuer_<Wort>_1/2, und
                                                         Eckturm_Bayen (Bayenturm: ersetzt den Eckturm am Mauerende neben dem Bayentor)
    game/assets/modelle/gebaeude/stapelhaus.glb         Sonderbau 26 × 16 m (Fiktion: Gemeinschaftswerk von Menschen und Zwergen):
                                                         „Haus“ (lange Halle, drei Querdächer mit sechs Treppengiebeln, Basaltsockel aus
                                                         Buckelquadern, eiserne Maueranker), „Waage“ (Stadtwaage im Waagegalgen, Erzkisten
                                                         der Zwerge, Weinfässer der Halblinge); Anker_Tuer, Anker_Karren, Anker_Waage
    game/assets/modelle/wasser/wasser_schwimmkran.glb   Rheinkran als Schwimmkran (8 × 8 m Land wie wasser_kran): „Haus“ (Kai mit
                                                         Spundwand), „Ponton“ (Ursprung auf seiner Wasserlinie; das Spiel setzt ihn auf den
                                                         Spiegel wie die Kähne des Hafens) mit Kind „Kranhaus“ und dessen Kind „Tretrad“
                                                         (Ursprung in der Radachse, dreht sich); Anker_Tuer, Anker_Ufer, Anker_Anleger

Koordinaten wie in bausteine.py: Z oben, Vorderseite −Y (im Spiel +Z), Ursprung in der Mitte der Fläche. Torburgen wie das Tor der
Stadtmauer: X entlang der Mauer, −Y Feldseite, die Mauerstücke schließen bei x = ±7,6 an, Durchfahrt mit echtem Gewölbe. Rheinkran wie
wasser_kran (hafenbauten.py): z = 0 Uferkante, Land bei −Y, Uferkante bei y = 4, Wasser bei −1,0 m.
Keine Zustände und keine Hausfarbe (die Bauten gehören der Stadt): `pruefen.py … --sonderbau` bzw. `--wasser`.
Recherche, Zustand um 1400 und Entscheidungen des Nutzers: docs/bereiche/darstellung.md („Köln: Rathaus, Torburgen …“).
"""

import math
import os
import random
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import kirchen_baukasten as bk  # noqa: E402  (Tuff, Trachyt, Basalt, Mauerzüge, Friese, Gerüst; setzt BIBLIOTHEK_JE_FLAECHE)
import bausteine as bs  # noqa: E402
import dom  # noqa: E402  (Spitzbogen)
import stadtmauer as sm  # noqa: E402  (allgemeine Teile des Mauerbaukastens)
from bausteine import Matrix, Vector  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]


def arg(name, vorgabe=None):
    for a in ARGS:
        if a.startswith("--" + name + "="):
            return a.split("=", 1)[1]
        if a == "--" + name:
            return True
    return vorgabe


# Farben nur dieser Bauten (sRGB): Buckelquader aus Basalt (Material-Bibliothek „quader“), Erz in den Kisten der Zwerge
bs.FARBEN.update({"Erz": (0.30, 0.24, 0.22), "Basaltquader": (0.22, 0.22, 0.24), "Teerholz": (0.19, 0.15, 0.12)})
WASSER = -1.0                       # Wasserspiegel unter der Uferkante (wie wasser.py)


# ─────────────────────────────────────────────────────────────────────────────
#  Hilfen
# ─────────────────────────────────────────────────────────────────────────────

def zinnen_linie(netz, st, p0, p1, z, dicke=0.45, hb=0.75, hz=0.9, breite=0.8, stoff="Tuff", anfang=0.0, ende=0.0):
    """Brustwehr mit Zinnen auf der Mauerkrone von p0 nach p1 (Außenseite rechts der Laufrichtung, bündig mit der Außenfläche).
    `anfang`, `ende`: so viel fehlt dort (an einer Ecke liegt schon die nächste Brustwehr – keine doppelten Flächen)."""
    M, L, _ = bk.kante(p0, p1, z)
    u0, u1 = -L / 2 + anfang, L / 2 - ende
    netz.quader(((u0 + u1) / 2, dicke / 2, hb / 2), (u1 - u0, dicke, hb), st[stoff], M, ohne=("-z",))
    lang = u1 - u0
    n = max(1, int((lang + breite) // (2 * breite)))
    rest = lang - (2 * n - 1) * breite
    for k in range(n):
        u = u0 + rest / 2 + breite / 2 + 2 * breite * k
        netz.quader((u, dicke / 2, hb + hz / 2), (breite, dicke, hz), st[stoff], M, ohne=("-z",))


def zinnen_vieleck(netz, st, punkte, z, geschlossen=True, **k):
    """Zinnen auf einem Vieleck (gegen den Uhrzeigersinn, außen rechts); geschlossen: jede Brustwehr endet vor der nächsten."""
    paare = list(zip(punkte, punkte[1:])) + ([(punkte[-1], punkte[0])] if geschlossen else [])
    for i, (p, q) in enumerate(paare):
        zinnen_linie(netz, st, p, q, z, ende=k.get("dicke", 0.45) if geschlossen or i < len(paare) - 1 else 0.0, **k)


def zinnen_rund(netz, st, punkte, z, dicke=0.45, hb=0.75, hz=0.9, stoff="Tuff"):
    """Zinnen auf einem Rundturm: jedes zweite Segment eine Zinne."""
    for i, (p, q) in enumerate(zip(punkte, punkte[1:])):
        M, L, _ = bk.kante(p, q, z)
        h = hb + (hz if i % 2 == 0 else 0.0)
        netz.quader((0, dicke / 2, h / 2), (L + 0.05, dicke, h), st[stoff], M, ohne=("-z",))


def band(netz, st, x0, x1, y0, y1, z, hoehe=0.22, vor=0.12, stoff="Trachyt"):
    """Gesimsband um ein Rechteck: vorn und hinten über die Ecken, die Seiten dazwischen (keine doppelten Flächen)."""
    for y in (y0, y1):
        netz.quader(((x0 + x1) / 2, y, z), (x1 - x0 + 2 * vor, 2 * vor, hoehe), st[stoff])
    for x in (x0, x1):
        netz.quader((x, (y0 + y1) / 2, z), (2 * vor, y1 - y0 - 2 * vor, hoehe), st[stoff])


def streifen(netz, st, M, u0, u1, z0, z1, tiefe, stoff, luecken=()):
    """Vorsatz an der Wand im Wandsystem M (Sockel, Band) von u0 bis u1, z0 bis z1, `tiefe` m vor die Wand (die Hälfte steckt darin);
    `luecken` [(u, halbe Breite)] bleiben frei (Tore)."""
    stuecke = [(u0, u1)]
    for u, h in luecken:
        neu = []
        for a, b in stuecke:
            if u - h > a:
                neu.append((a, min(b, u - h)))
            if u + h < b:
                neu.append((max(a, u + h), b))
        stuecke = neu
    for a, b in stuecke:
        if b - a > 0.05:
            netz.quader(((a + b) / 2, 0.0, (z0 + z1) / 2), (b - a, tiefe, z1 - z0), st[stoff], M, ohne=("+y", "-z"))


def ring(netz, st, x0, x1, y0, y1, z0, z1, tiefe=0.2, stoff="Basalt", luecken=None):
    """Sockel um ein Rechteck (vorn und hinten über die Ecken, Seiten dazwischen); `luecken` je Seite ("vorne", "hinten") [(u, halb)]."""
    luecken = luecken or {}
    xm, ym, L, T = (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0
    t = tiefe / 2
    streifen(netz, st, bs.wandmatrix((xm, y0, 0), "vorne"), -L / 2 - t, L / 2 + t, z0, z1, tiefe, stoff, luecken.get("vorne", ()))
    streifen(netz, st, bs.wandmatrix((xm, y1, 0), "hinten"), -L / 2 - t, L / 2 + t, z0, z1, tiefe, stoff, luecken.get("hinten", ()))
    streifen(netz, st, bs.wandmatrix((x0, ym, 0), "links"), -T / 2 + t, T / 2 - t, z0, z1, tiefe, stoff, luecken.get("links", ()))
    streifen(netz, st, bs.wandmatrix((x1, ym, 0), "rechts"), -T / 2 + t, T / 2 - t, z0, z1, tiefe, stoff, luecken.get("rechts", ()))


def walmdach(netz, st, x0, x1, y0, y1, z, neigung, ueber=0.3, stoff="Schiefer"):
    """Walmdach über dem Rechteck (Traufe auf z, `ueber` Überstand); gibt die Firsthöhe zurück."""
    t = math.tan(math.radians(neigung))
    x0, x1, y0, y1 = x0 - ueber, x1 + ueber, y0 - ueber, y1 + ueber
    z = z - ueber * t
    ym, xm = (y0 + y1) / 2, (x0 + x1) / 2
    if x1 - x0 >= y1 - y0:
        hw = (y1 - y0) / 2
        h = hw * t
        a, b = (x0 + hw, ym, z + h), (x1 - hw, ym, z + h)
        netz.vieleck_nach([(x0, y0, z), (x1, y0, z), b, a], (0, -1, 1), st[stoff])
        netz.vieleck_nach([(x1, y1, z), (x0, y1, z), a, b], (0, 1, 1), st[stoff])
        netz.vieleck_nach([(x0, y1, z), (x0, y0, z), a], (-1, 0, 1), st[stoff])
        netz.vieleck_nach([(x1, y0, z), (x1, y1, z), b], (1, 0, 1), st[stoff])
        netz.stab(Vector(a) + Vector((-0.1, 0, 0.05)), Vector(b) + Vector((0.1, 0, 0.05)), 0.2, 0.2, st["Firstblech"])
    else:
        hw = (x1 - x0) / 2
        h = hw * t
        a, b = (xm, y0 + hw, z + h), (xm, y1 - hw, z + h)
        netz.vieleck_nach([(x0, y1, z), (x0, y0, z), a, b], (-1, 0, 1), st[stoff])
        netz.vieleck_nach([(x1, y0, z), (x1, y1, z), b, a], (1, 0, 1), st[stoff])
        netz.vieleck_nach([(x0, y0, z), (x1, y0, z), a], (0, -1, 1), st[stoff])
        netz.vieleck_nach([(x1, y1, z), (x0, y1, z), b], (0, 1, 1), st[stoff])
        netz.stab(Vector(a) + Vector((0, -0.1, 0.05)), Vector(b) + Vector((0, 0.1, 0.05)), 0.2, 0.2, st["Firstblech"], quer=(1, 0, 0))
    return z + h


def spitzoeffnung(netz, st, M, u, v, b, h, stoff="Loch", rahmen="Trachyt", g=0.25):
    """Spitzbogenöffnung auf der Wand (Fläche aus `stoff`) mit Gewände."""
    bs.platte(netz, M, dom.spitzbogen(u, v, b, h, 4), -0.02, -0.002, st[stoff])
    dom.spitzring(netz, M, u, v, b, h, g, -0.12, -0.004, st[rahmen])


def scharte(netz, st, M, u, v, h=1.0):
    """Schießscharte: schmaler Schlitz in einem Trachytrahmen."""
    netz.quader((u, -0.03, v + h / 2), (0.16, 0.02, h), st["Loch"], M, ohne=("+y",))
    netz.quader((u, -0.01, v + h / 2), (0.4, 0.02, h + 0.3), st["Trachyt"], M, ohne=("+y",))


def kreuzstock(netz, st, M, u, v, b=1.2, h=1.7):
    """Kreuzstockfenster: Glas, steinerner Rahmen mit Pfosten und Kämpfer."""
    netz.quader((u, -0.01, v + h / 2), (b, 0.02, h), st["Fenster"], M, ohne=("+y",))
    for uu, vv, bb, hh in ((u, v - 0.06, b + 0.24, 0.12), (u, v + h + 0.06, b + 0.24, 0.12), (u - b / 2 - 0.06, v + h / 2, 0.12, h),
                           (u + b / 2 + 0.06, v + h / 2, 0.12, h)):
        netz.quader((uu, -0.05, vv), (bb, 0.1, hh), st["Trachyt"], M, ohne=("+y",))
    netz.quader((u, -0.04, v + h / 2), (0.09, 0.06, h), st["Trachyt"], M, ohne=("+y",))
    netz.quader((u, -0.045, v + h * 0.62), (b, 0.06, 0.09), st["Trachyt"], M, ohne=("+y",))


def luke(netz, st, M, u, v, b, h):
    """Ladeluke mit Bohlenladen (Holz, eiserne Bänder) im Trachytrahmen – an Speichergiebeln statt eines Fensters."""
    netz.quader((u, -0.012, v + h / 2), (b, 0.02, h), st["Holz alt"], M, ohne=("+y",))
    for f in (0.25, 0.75):
        netz.quader((u, -0.03, v + h * f), (b * 0.9, 0.02, 0.07), st["Eisen"], M, ohne=("+y",))
    for uu, vv, bb, hh in ((u, v - 0.07, b + 0.28, 0.14), (u, v + h + 0.07, b + 0.28, 0.14), (u - b / 2 - 0.07, v + h / 2, 0.14, h),
                           (u + b / 2 + 0.07, v + h / 2, 0.14, h)):
        netz.quader((uu, -0.05, vv), (bb, 0.1, hh), st["Trachyt"], M, ohne=("+y",))


def stadtwappen(netz, st, M, u, v, g=1.0, y=-0.04):
    """Kölner Wappen (vereinfacht): weißer Schild, rotes Haupt mit drei goldenen Kronen; Mitte bei (u, v), Größe g."""
    schild = [(-0.55, 0.65), (0.55, 0.65), (0.55, 0.0), (0.0, -0.6), (-0.55, 0.0)]
    bs.platte(netz, M, [(u + x * g, v + yy * g) for x, yy in schild], y - 0.02, y, st["Stadtweiss"])
    bs.platte(netz, M, [(u + x * g, v + yy * g) for x, yy in ((-0.55, 0.3), (0.55, 0.3), (0.55, 0.65), (-0.55, 0.65))], y - 0.035, y - 0.02,
              st["Stadtrot"])
    for k in (-0.3, 0.0, 0.3):
        netz.quader((u + k * g, y - 0.045, v + 0.47 * g), (0.16 * g, 0.02, 0.12 * g), st["Gold"], M, ohne=("+y",))


def fallgitter(netz, st, y, b, z_unten, z_oben):
    """Fallgitter in der Durchfahrt (halb hochgezogen); was über dem Bogen liegt, steckt im Mauerwerk."""
    for i in range(7):
        x = -b / 2 + 0.2 + (b - 0.4) * i / 6
        netz.quader((x, y, (z_unten + z_oben) / 2), (0.09, 0.09, z_oben - z_unten), st["Eisen"])
    for z in (z_unten + 0.25, z_unten + 1.0, z_unten + 1.75):
        netz.quader((0, y, z), (b - 0.1, 0.08, 0.09), st["Eisen"])


def torbau(netz, st, bx, y0, y1, h, b, h_bogen, oben, stoff="Tuff"):
    """Baukörper mit Durchfahrt (Spitzbogen b breit, Scheitel h_bogen) von der Feldseite y0 bis zur Stadtseite y1, x ±bx, Höhe h:
    zwei Seitenteile, Mauerwerk über dem Bogen ab `oben`, Zwickel mit dem Gewölbe, Steinboden, Gewände auf beiden Seiten, offene
    Torflügel auf der Stadtseite. Gibt die Kämpferhöhe zurück."""
    tief, ym = y1 - y0, (y0 + y1) / 2
    for s in (-1, 1):
        netz.quader((s * (bx + b / 2) / 2, ym, h / 2), (bx - b / 2, tief, h), st[stoff], ohne=("-z",))
    netz.quader((0, ym, (oben + h) / 2), (b, tief, h - oben), st[stoff], ohne=("-z",))
    bogen = dom.spitzbogen(0.0, 0.0, b, h_bogen, 6)
    k = bogen[2][1]
    zwickel = [(b / 2, k), (b / 2, oben), (-b / 2, oben), (-b / 2, k)] + list(reversed(bogen[3:-1]))
    bs.platte(netz, bs.wandmatrix((0, y0, 0), "vorne"), zwickel, 0.0, tief, st[stoff])
    netz.quader((0, ym, 0.03), (b, tief, 0.06), st["Basalt"], ohne=("-z",))
    for seite, y in (("vorne", y0), ("hinten", y1)):
        dom.spitzring(netz, bs.wandmatrix((0, y, 0), seite), 0.0, 0.0, b, h_bogen, 0.3, -0.12, -0.004, st["Trachyt"])
    for sx in (-1, 1):
        t = Matrix.Translation((sx * b / 2, y1 - 0.5, 0)) @ Matrix.Rotation(math.radians(-sx * 80), 4, "Z")
        netz.quader((-sx * b / 4, 0, k / 2 + 0.3), (b / 2 - 0.05, 0.12, k + 0.6), st["Holz alt"], t)
    return k


# ─────────────────────────────────────────────────────────────────────────────
#  Rathaus: Saalbau (Hansasaal, um 1330) und Ratsturm als Baustelle (1407–1414), kleine Vorhalle (1404 erwähnt)
# ─────────────────────────────────────────────────────────────────────────────

def rathaus(st):
    n = bs.Netz("Haus")
    g = bs.Netz("Baustelle")
    x0, x1, y0, y1 = -4.4, 10.6, -3.4, 4.6
    zeg, ztr = 4.8, 10.6
    L, T = x1 - x0, y1 - y0
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    n.quader((xm, ym, ztr / 2), (L, T, ztr), st["Tuff"], ohne=("-z",))
    seiten = ((bs.wandmatrix((xm, y0, 0), "vorne"), L), (bs.wandmatrix((xm, y1, 0), "hinten"), L),
              (bs.wandmatrix((x0, ym, 0), "links"), T), (bs.wandmatrix((x1, ym, 0), "rechts"), T))
    ring(n, st, x0, x1, y0, y1, 0.0, 0.6, luecken={"vorne": [(0.0, 2.6)]})
    band(n, st, x0, x1, y0, y1, zeg)
    band(n, st, x0, x1, y0, y1, ztr - 0.1, vor=0.18)
    for i, (M, laenge) in enumerate(seiten):
        anzahl = 5 if i < 2 else 2
        for k in range(anzahl):
            u = -laenge / 2 + laenge * (k + 0.5) / anzahl
            bk.spitzfenster(n, st, M, u, zeg + 0.7, 1.4, 4.2, pfosten=1)
            if i == 0 and k in (0, 1, 3, 4):
                spitzoeffnung(n, st, M, u, 1.0, 1.1, 2.4, stoff="Glas")
            elif i != 0:
                spitzoeffnung(n, st, M, u, 1.2, 0.9, 2.0, stoff="Glas")
    # Hauptportal in der Mitte der Platzseite
    Mv = seiten[0][0]
    bs.platte(n, Mv, dom.spitzbogen(0.0, 0.0, 1.9, 3.6, 4), -0.03, -0.005, st["Holz alt"])
    for f in (0.2, 0.45, 0.7):
        n.quader((0.0, -0.045, 3.6 * f), (1.7, 0.02, 0.09), st["Eisen"], Mv, ohne=("+y",))
    dom.spitzring(n, Mv, 0.0, 0.0, 1.9, 3.6, 0.3, -0.14, -0.004, st["Trachyt"])
    # Vorhalle (die Laube von 1569 gibt es noch nicht): zwei Spitzbögen zum Platz, Seitenwände, Plattform mit Zinnen, Stadtwappen
    vx0, vx1, vy0, vh = xm - 2.6, xm + 2.6, y0 - 2.3, 4.0
    for x in (vx0, vx1):
        n.quader((x + (0.3 if x < xm else -0.3), (vy0 + y0) / 2, vh / 2), (0.6, y0 - vy0, vh), st["Tuff"], ohne=("-z", "+y"))
    Mf = bs.wandmatrix((xm, vy0, 0), "vorne")
    bb, hb = 1.75, 3.0
    oben = 3.1
    n.quader((0, 0.3, (oben + vh) / 2), (4.0, 0.6, vh - oben), st["Tuff"], Mf, ohne=("-z",))
    n.quader((0, 0.3, oben / 2), (0.5, 0.6, oben), st["Tuff"], Mf, ohne=("-z",))
    for u in (-1.125, 1.125):
        bogen = dom.spitzbogen(u, 0.0, bb, hb, 4)
        k = bogen[2][1]
        zwickel = [(u + bb / 2, k), (u + bb / 2, oben), (u - bb / 2, oben), (u - bb / 2, k)] + list(reversed(bogen[3:-1]))
        bs.platte(n, Mf, zwickel, 0.0, 0.6, st["Tuff"])
        dom.spitzring(n, Mf, u, 0.0, bb, hb, 0.18, -0.06, -0.002, st["Trachyt"])
    n.quader((xm, (vy0 - 0.12 + y0) / 2, vh), (5.4, y0 - vy0 + 0.12, 0.14), st["Trachyt"], ohne=("+y",))          # Abdeckplatte, vorn als Gesims
    zinnen_vieleck(n, st, [(vx0, y0), (vx0, vy0), (vx1, vy0), (vx1, y0)], vh, geschlossen=False, dicke=0.35, hb=0.5, hz=0.6, breite=0.55)
    stadtwappen(n, st, Mf, 0.0, 3.55, g=0.62, y=-0.02)
    walmdach(n, st, x0, x1, y0, y1, ztr, 52)
    # Ratsturm als Baustelle: Stumpf des ersten Geschosses, Gerüst, Galgenkran mit Haspel, Werksteine
    tx0, tx1, ty0, ty1, th, d = -10.2, -5.4, -4.6, 0.2, 6.5, 1.1
    for y in (ty0, ty1):
        n.quader(((tx0 + tx1) / 2, y + (d / 2 if y == ty0 else -d / 2), th / 2), (tx1 - tx0, d, th), st["Tuff"], ohne=("-z",))
    for x in (tx0, tx1):
        n.quader((x + (d / 2 if x == tx0 else -d / 2), (ty0 + ty1) / 2, th / 2), (d, ty1 - ty0 - 2 * d, th), st["Tuff"], ohne=("-z",))
    ring(n, st, tx0, tx1, ty0, ty1, 0.0, 0.7)
    for M, laenge in ((bs.wandmatrix(((tx0 + tx1) / 2, ty0, 0), "vorne"), tx1 - tx0), (bs.wandmatrix((tx0, (ty0 + ty1) / 2, 0), "links"), ty1 - ty0)):
        bk.spitzfenster(n, st, M, 0.0, 2.6, 1.1, 2.6, glas="Loch", pfosten=0)
    rnd = random.Random(3)
    for k in range(10):
        f = (k % 5 + 0.5) / 5
        p = (tx0 + 0.55 + f * (tx1 - tx0 - 1.1), ty0 + 0.55 if k < 5 else ty1 - 0.55)
        h = 0.25 + rnd.random() * 1.2
        n.quader((p[0], p[1], th + h / 2 - 0.05), (0.7, 0.7, h), st["Tuff" if rnd.random() < 0.7 else "Trachyt"], ohne=("-z",))
    bk.geruest(g, st, tx0 - 0.6, tx1 + 0.6, ty0 - 0.6, ty1 + 0.6, 0.0, th + 2.4, ebene=2.0)
    mast_fuss = Vector((tx0 + 0.3, ty0 + 0.3, th))
    mast_kopf = mast_fuss + Vector((0, 0, 5.0))
    spitze = mast_kopf + Vector((3.4, -0.9, 0.3))
    g.stab(mast_fuss, mast_kopf, 0.25, 0.25, st["Holz"])
    g.stab(mast_kopf - Vector((0, 0, 0.3)), spitze, 0.2, 0.18, st["Holz"], quer=(0, 0, 1))
    g.stab(mast_fuss + Vector((0, 0, 2.0)), mast_kopf.lerp(spitze, 0.6), 0.14, 0.14, st["Holz"], quer=(0, 0, 1))
    g.stab(spitze, Vector((spitze.x, spitze.y, 1.2)), 0.04, 0.04, st["Seil"])
    g.quader((spitze.x, spitze.y, 0.6), (0.9, 0.7, 0.6), st["Tuff"], ohne=("-z",))
    g.stab(Vector((tx0 + 1.2, ty0 + 0.6, th + 0.8)), Vector((tx0 + 2.6, ty0 + 0.6, th + 0.8)), 0.3, 0.3, st["Holz"], quer=(0, 1, 0))
    for k in range(4):
        g.quader((tx1 + 1.6, ty0 + 0.6 + 0.9 * k, 0.3), (1.0, 0.7, 0.6), st["Tuff"], ohne=("-z",))
    for k in range(3):
        g.quader((tx0 + 1.6, ty1 + 1.2 + 0.25 * k, 0.1 + 0.2 * k), (3.6, 0.22, 0.2), st["Geruestholz"], ohne=("-z",))
    anker = [bs.anker("Anker_Tuer", (xm, vy0 - 0.4, 0.0)), bs.anker("Anker_Laterne", (vx0 + 0.3, vy0 - 0.05, 2.6), 180.0)]
    return [n.objekt(), g.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Torburgen (Teile des Kölner Mauerbaukastens)
# ─────────────────────────────────────────────────────────────────────────────

def mauerstummel(netz, st, x_innen, y0, s):
    """Mauerstück vom Torbau bis x = ±7,9 (die Mauerstücke des Baukastens schließen bei ±7,6 an), 6 m hoch wie der Wehrgang."""
    x0, x1 = sorted((s * x_innen, s * 7.9))
    # 1 cm vor der Stadtseite und unter dem Wehrgang des Mauerstücks: wo beide sich überlappen (±7,6 … ±7,9), lägen sonst ihre
    # Flächen in einer Ebene und flimmerten (in Cycles schwarz)
    y1, h = 0.99, 5.98
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), st["Tuff"], ohne=("-z",))
    streifen(netz, st, bs.wandmatrix(((x0 + x1) / 2, y0, 0), "vorne"), -(x1 - x0) / 2 + 0.02, (x1 - x0) / 2 - 0.02, 0.0, 2.0, 0.2,
             "Basalt")
    zinnen_linie(netz, st, (x0, y0), (x1, y0), h, hb=0.62, hz=0.8, breite=0.7)
    # Anschlusszinne: deckt das offene Ende der Brustwehr des anschließenden Mauerstücks (y −1 … −0,5, bis 7,6 m hoch)
    netz.quader((s * 7.75, -0.75, 6.79), (0.3, 0.52, 1.62), st["Tuff"], ohne=("-z",))


def doppelturmtor(st, wort, hb, ht):
    """Doppelturmtor (Eigelstein, Hahnentor): Torbau mit Durchfahrt, zwei halbrunde Türme zur Feldseite, Zinnen, flaches Walmdach
    hinter den Zinnen (Entscheidung des Nutzers, 9.11.2026). `hb` Torbau, `ht` Türme."""
    n = bs.Netz("Tor_" + wort)
    bx, by0, by1 = 5.6, -2.6, 2.6
    r, tx = 2.9, 4.9
    b, h_bogen, oben = 3.4, 5.8, 6.9
    torbau(n, st, bx, by0, by1, hb, b, h_bogen, oben)
    # Sockel: vorn stehen die Türme davor (zwischen ihnen nur die Durchfahrt), hinten bleibt die Durchfahrt frei
    ring(n, st, -bx, bx, by0, by1, 0.0, 4.0, luecken={"vorne": [(0.0, bx + 0.2)], "hinten": [(0.0, b / 2 + 0.3)]})
    band(n, st, -bx, bx, by0, by1, hb - 1.2, hoehe=0.22, vor=0.1)
    Ms = bs.wandmatrix((0, by1, 0), "hinten")
    for u in (-2.6, 2.6):
        bk.spitzfenster(n, st, Ms, u, 7.4, 1.0, 2.4, glas="Glas", pfosten=0)
    if hb > 13:
        for u in (-2.6, 0.0, 2.6):
            bk.rundfenster(n, st, Ms, u, 10.4, 0.8, 1.6, glas="Glas")
    Mf = bs.wandmatrix((0, by0, 0), "vorne")
    stadtwappen(n, st, Mf, 0.0, 7.9, g=1.0)
    scharte(n, st, Mf, 0.0, hb - 3.4)
    fallgitter(n, st, by0 + 0.4, b, 3.4, h_bogen + 0.4)
    # Halbrunde Türme zur Feldseite, hinten mit einer geraden Wand geschlossen
    for s in (-1, 1):
        c = (s * tx, by0)
        pts = bk.bogenpunkte(c, r, 180, 360, 8)
        for p, q in zip(pts, pts[1:]):        # Wand je Segment, oben offen (Plattform und Zinnen decken sie)
            M, L, phi = bk.kante(p, q, 0.0)
            n.quader((0, 0.5, ht / 2), (L + 0.04, 1.0, ht), st["Tuff"], M, ohne=("-z", "+z"))
            n.quader((0, 0.0, 2.0), (L, 0.2, 4.0), st["Basalt"], M, ohne=("+y", "-z", "+z"))
            aussen = [Vector((c[0], c[1])) + (Vector(e) - Vector((c[0], c[1]))) * (1 + 0.1 / (r * math.cos(math.radians(11.25)))) for e in (p, q)]
            n.vieleck_nach([(p[0], p[1], 4.0), (q[0], q[1], 4.0), (*aussen[1], 4.0), (*aussen[0], 4.0)], (0, 0, 1), st["Basalt"])   # Oberkante des Sockels
            bk.fries(n, st, M, L, ht - 0.25, bogen=0.6, hoehe=0.32)
            if -135 < phi < -45:
                scharte(n, st, M, 0.0, 5.0)
                scharte(n, st, M, 0.0, 8.6)
        n.quader((c[0], by0 + 0.4, ht / 2), (2 * r, 0.76, ht), st["Tuff"], ohne=("-z", "+z"))     # Rückwand des Halbrunds
        zinnen_rund(n, st, pts, ht)
        if ht > hb:
            zinnen_linie(n, st, (c[0] + r, by0), (c[0] - r, by0), ht, anfang=0.7, ende=0.7)
        elif s > 0:
            zinnen_linie(n, st, (c[0] + r, by0), (bx, by0), ht, anfang=0.7)
        else:
            zinnen_linie(n, st, (-bx, by0), (c[0] - r, by0), ht, ende=0.7)
        n.vieleck_nach([(c[0] + (r - 0.3) * math.cos(math.radians(a)), c[1] + (r - 0.3) * math.sin(math.radians(a)), ht + 0.02)
                        for a in range(180, 361, 30)] + [(c[0], c[1], ht + 0.02)], (0, 0, 1), st["Trachyt"])
        mauerstummel(n, st, bx, by0 + 0.8, s)
    zinnen_vieleck(n, st, [(-bx, by0), (bx, by0), (bx, by1), (-bx, by1)], hb)
    walmdach(n, st, -bx + 0.6, bx - 0.6, by0 + 0.6, by1 - 0.6, hb + 0.02, 32, ueber=0.0)
    anker = [bs.anker("Anker_Tuer_" + wort, (0.0, -3.0, 0.0)), bs.anker(f"Anker_Feuer_{wort}_1", (-1.95, -2.95, 0.0), 180.0),
             bs.anker(f"Anker_Feuer_{wort}_2", (1.95, -2.95, 0.0), 180.0)]
    return [n.objekt()] + anker


def eigelstein(st):
    return doppelturmtor(st, "Eigelstein", 14.0, 12.5)


def hahnen(st):
    # Spielabweichung (Entscheidung des Nutzers): Die Türme überragen den Torbau, damit Hahnen- und Eigelsteintor sich unterscheiden
    return doppelturmtor(st, "Hahnen", 12.5, 14.5)


def severinstor(st):
    """Severinstorburg: wuchtiger Torturm mit Durchfahrt und vorkragender Zinnengalerie, zwei kleine Eckwarten zur Feldseite (um 1400
    mit Zinnen, ohne die Kegelhelme des 17. Jh.), Seitenflügel bis zur Mauer."""
    wort = "Severin"
    n = bs.Netz("Tor_" + wort)
    a, y0, y1, h = 4.0, -2.6, 4.6, 19.0
    b, h_bogen, oben = 3.4, 5.8, 6.9
    torbau(n, st, a, y0, y1, h, b, h_bogen, oben)
    tor = {"vorne": [(0.0, b / 2 + 0.3)], "hinten": [(0.0, b / 2 + 0.3)]}
    ring(n, st, -a, a, y0, y1, 0.0, 5.0, luecken=tor)
    for z in (7.0, 11.0, 15.0):
        band(n, st, -a, a, y0, y1, z, hoehe=0.18, vor=0.08)
    for M, L in ((bs.wandmatrix((0, y0, 0), "vorne"), 2 * a), (bs.wandmatrix((0, y1, 0), "hinten"), 2 * a),
                 (bs.wandmatrix((-a, (y0 + y1) / 2, 0), "links"), y1 - y0), (bs.wandmatrix((a, (y0 + y1) / 2, 0), "rechts"), y1 - y0)):
        bk.fries(n, st, M, L + 0.5, h + 0.3, bogen=0.8, hoehe=0.5, y=-0.32)
    Mf = bs.wandmatrix((0, y0, 0), "vorne")
    Ms = bs.wandmatrix((0, y1, 0), "hinten")
    fallgitter(n, st, y0 + 0.4, b, 3.4, h_bogen + 0.4)
    stadtwappen(n, st, Mf, 0.0, 8.4, g=1.1)
    for v in (8.0, 12.0, 16.0):
        scharte(n, st, Mf, -2.2, v)
        scharte(n, st, Mf, 2.2, v)
        bk.spitzfenster(n, st, Ms, 0.0, v, 0.9, 2.0, glas="Glas", pfosten=0)
    scharte(n, st, Mf, 0.0, 12.0)
    scharte(n, st, Mf, 0.0, 16.0)
    # vorkragende Zinnengalerie und flaches Walmdach
    n.quader((0, (y0 + y1) / 2, h + 0.15), (2 * a + 0.7, y1 - y0 + 0.7, 0.3), st["Trachyt"])
    zinnen_vieleck(n, st, [(-a - 0.35, y0 - 0.35), (a + 0.35, y0 - 0.35), (a + 0.35, y1 + 0.35), (-a - 0.35, y1 + 0.35)], h + 0.3)
    walmdach(n, st, -a + 0.5, a - 0.5, y0 + 0.5, y1 - 0.5, h + 0.32, 30, ueber=0.0)
    # Eckwarten
    for s in (-1, 1):
        cx, cy, w, hw = s * (a + 0.6), y0 - 0.5, 1.4, 9.5
        n.quader((cx, cy, hw / 2), (2 * w, 2 * w, hw), st["Tuff"], ohne=("-z",))
        ring(n, st, cx - w, cx + w, cy - w, cy + w, 0.0, 3.0)
        band(n, st, cx - w, cx + w, cy - w, cy + w, 6.0, hoehe=0.18, vor=0.08)
        for M, wi in bk.seiten4(cx, cy, w):
            if wi != 90:
                scharte(n, st, M, 0.0, 4.2)
        n.quader((cx, cy, hw + 0.01), (2 * w - 0.6, 2 * w - 0.6, 0.02), st["Trachyt"], ohne=("-z",))
        zinnen_vieleck(n, st, [(cx - w, cy - w), (cx + w, cy - w), (cx + w, cy + w), (cx - w, cy + w)], hw, dicke=0.35, hb=0.6, hz=0.7,
                       breite=0.5)
        mauerstummel(n, st, a, cy + w, s)
    anker = [bs.anker("Anker_Tuer_" + wort, (0.0, -3.0, 0.0)), bs.anker(f"Anker_Feuer_{wort}_1", (-2.2, -2.95, 0.0), 180.0),
             bs.anker(f"Anker_Feuer_{wort}_2", (2.2, -2.95, 0.0), 180.0)]
    return [n.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Bayenturm: Vierkant (3 Geschosse) mit achteckigem Aufsatz von 1325, Bogenfries und vorkragenden Zinnen
# ─────────────────────────────────────────────────────────────────────────────

def bayenturm(st):
    """Eckturm am Südende der Rheinmauer (Teil „Eckturm_Bayen“ des Kölner Mauerbaukastens): Ursprung auf dem Mauerende, X entlang der
    Mauer (sie kommt von −X), −Y Feldseite mit der Pforte."""
    n = bs.Netz("Eckturm_Bayen")
    a, h4, h8 = 4.5, 12.0, 19.5
    n.quader((0, 0, h4 / 2), (2 * a, 2 * a, h4), st["Tuff"], ohne=("-z", "+z"))
    ring(n, st, -a, a, -a, a, 0.0, 3.0)
    for z in (4.6, 8.4):
        band(n, st, -a, a, -a, a, z, hoehe=0.16, vor=0.09)                 # weiter vor als die Lisenen
    for M, w in bk.seiten4(0, 0, a):
        for v in (5.6, 9.2):
            scharte(n, st, M, -1.6, v, 1.2)
            scharte(n, st, M, 1.6, v, 1.2)
        for sx in (-1, 1):
            bk.lisene(n, st, M, sx * (a - 0.3), 3.0, h4 - 0.2, breite=0.6, vor=0.06)
    Mf = bs.wandmatrix((0, -a, 0), "vorne")
    bs.platte(n, Mf, dom.spitzbogen(0.0, 0.0, 1.3, 2.6, 4), -0.03, -0.005, st["Holz alt"])
    dom.spitzring(n, Mf, 0.0, 0.0, 1.3, 2.6, 0.22, -0.2, -0.004, st["Trachyt"])
    # Übergang ins Achteck: Schrägen über den Ecken
    r8 = a / math.cos(math.radians(22.5))
    for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        e = Vector((sx * a, sy * a, h4))
        p = Vector((sx * a, sy * a * math.tan(math.radians(22.5)), h4))
        q = Vector((sx * a * math.tan(math.radians(22.5)), sy * a, h4))
        p0, q0 = p.copy(), q.copy()
        p.z += 1.4
        q.z += 1.4
        n.vieleck_nach([e, p, q], (sx, sy, 1.2), st["Trachyt"])
        n.vieleck_nach([e, p0, p], (sx, 0, 0), st["Tuff"])
        n.vieleck_nach([e, q0, q], (0, sy, 0), st["Tuff"])
    bs.achteck(n, st, (0, 0, 0), r8, h4, h8, "Tuff")
    bs.achteck(n, st, (0, 0, 0), r8 + 0.1, h4 + 3.6, h4 + 3.8, "Trachyt")
    L8 = 2 * r8 * math.sin(math.radians(22.5))
    for k in range(8):
        w = 45 * k
        M = bk.wm((a * math.cos(math.radians(w)), a * math.sin(math.radians(w)), 0), w)
        scharte(n, st, M, 0.0, h4 + 1.6, 1.1)
        if k % 2 == 0:
            bk.spitzfenster(n, st, M, 0.0, h4 + 4.4, 0.8, 1.9, glas="Glas", pfosten=0)
        bk.fries(n, st, M, L8 + 0.6, h8 + 0.15, bogen=0.75, hoehe=0.55, y=-0.32)
    # vorkragender Zinnenkranz, flaches Kegeldach
    bs.achteck(n, st, (0, 0, 0), r8 + 0.4, h8 + 0.15, h8 + 0.45, "Trachyt")
    ecken = [((r8 + 0.4) * math.cos(math.radians(22.5 + 45 * k)), (r8 + 0.4) * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    zinnen_vieleck(n, st, ecken, h8 + 0.45, hb=0.8, hz=0.95, breite=0.7)
    bk.kegelsegmente(n, st, [((r8 - 0.6) * math.cos(math.radians(22.5 + 45 * k)), (r8 - 0.6) * math.sin(math.radians(22.5 + 45 * k)))
                             for k in range(9)], (0, 0, h8 + 3.2), h8 + 0.45)
    return [n.objekt()]


def mauerbaukasten():
    """Der Kölner Mauerbaukasten: die allgemeinen Teile aus stadtmauer.py, dazu die drei Torburgen und der Bayenturm."""
    teile = sm.bauen()                       # neue Szene: Mauerstueck, Mauerturm, Eckturm, Tor, Anker_Tuer, Anker_Feuer_1/2
    st = bs.Stoffe()
    for fn in (eigelstein, hahnen, severinstor, bayenturm):
        teile += fn(st)
    return teile


# ─────────────────────────────────────────────────────────────────────────────
#  Stapelhaus (Fiktion): lange Halle mit drei Treppengiebeln je Seite, zwergischer Basaltsockel, eiserne Beschläge, Stadtwaage
# ─────────────────────────────────────────────────────────────────────────────

def maueranker(netz, st, M, u, v):
    netz.quader((u, -0.03, v), (0.08, 0.05, 0.9), st["Eisen"], M, ohne=("+y",))
    netz.quader((u, -0.06, v), (0.6, 0.02, 0.08), st["Eisen"], M, ohne=("+y",))


def stapelhaus(st):
    n = bs.Netz("Haus")
    st["Sandstein"] = st["Trachyt"]                       # Platten der Treppengiebel in Trachyt statt gelbem Sandstein
    x0, x1, y0, y1 = -12.0, 12.0, -3.7, 7.0
    zs, ztr = 1.9, 8.2
    xm, ym = 0.0, (y0 + y1) / 2
    bays = (-8.0, 0.0, 8.0)
    n.quader((xm, ym, ztr / 2), (x1 - x0, y1 - y0, ztr), st["Tuff"], ohne=("-z",))
    # zwergischer Sockel: Basaltquader, davor unregelmäßig versetzte Buckelquader; die Tore bleiben frei
    tore = {"vorne": [(cx, 1.75) for cx in bays], "hinten": [(cx, 1.45) for cx in bays]}
    ring(n, st, x0, x1, y0, y1, 0.0, zs, tiefe=0.5, stoff="Basaltquader", luecken=tore)
    ring(n, st, x0, x1, y0, y1, zs, zs + 0.2, tiefe=0.6, stoff="Basalt", luecken=tore)
    band(n, st, x0, x1, y0, y1, 5.0, hoehe=0.18, vor=0.1)
    rnd = random.Random(12)
    for M, L, seite in ((bs.wandmatrix((xm, y0, 0), "vorne"), x1 - x0, "vorne"), (bs.wandmatrix((xm, y1, 0), "hinten"), x1 - x0, "hinten"),
                        (bs.wandmatrix((x0, ym, 0), "links"), y1 - y0, "links"), (bs.wandmatrix((x1, ym, 0), "rechts"), y1 - y0, "rechts")):
        frei = tore.get(seite, ())
        u = -L / 2 + 0.1
        while u < L / 2 - 0.4:
            b = min(0.9 + rnd.random() * 0.9, L / 2 - 0.1 - u)
            for v, hh in ((0.45, 0.8), (1.35, 0.85)):
                if rnd.random() < 0.55 and not any(abs(u + b / 2 - t) < h + b / 2 for t, h in frei):
                    n.quader((u + b / 2, -0.3, v), (b - 0.1, 0.12, hh - 0.08), st["Basaltquader"], M, ohne=("+y",))
            u += b
    # Giebel: drei Querdächer, jedes mit Treppengiebel zur Stadt (−Y) und zum Rhein (+Y), Ladeluke und Aufzugsbalken
    Mv = bs.wandmatrix((xm, y0, 0), "vorne")
    Mh = bs.wandmatrix((xm, y1, 0), "hinten")
    for cx in bays:
        drehung = Matrix.Translation((cx, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
        d = bs.satteldach(n, st, y0 + 0.4, y1 - 0.4, -4.0, 4.0, ztr, 52, matrix=drehung, traufe=0.3, ortgang=0.0, enden="Tuff",
                          **bs.SCHIEFER)
        for y, seite in ((y0, "vorne"), (y1, "hinten")):
            M = bs.wandmatrix((cx, y, ztr), seite)
            spitze = bs.treppengiebel(n, st, M, 3.94, 0.35, d["tan"], [], [], stufen=5, oben_halb=0.5, dicke=0.45, stoff="Tuff")
            luke(n, st, M, 0.0, 1.3, 0.9, 1.3)
            for u in (-1.6, 1.6):
                maueranker(n, st, M, u, 3.2)
            n.stab(M @ Vector((0, 0.2, spitze - 1.4)), M @ Vector((0, -0.85, spitze - 1.4)), 0.22, 0.22, st["Holz"],
                   quer=M.to_3x3() @ Vector((1, 0, 0)))          # Aufzugsbalken
        # Obergeschoss: Kreuzstockfenster; unten das Tor zur Stadt bzw. eine Ladeöffnung zum Rhein
        for u in (-2.2, 2.2):
            kreuzstock(n, st, Mv, cx + u, 5.6, 1.1, 1.7)
            kreuzstock(n, st, Mh, -(cx + u), 5.6, 1.1, 1.7)
        bs.platte(n, Mv, dom.spitzbogen(cx, 0.0, 2.8, 4.2, 4), -0.03, -0.005, st["Holz alt"])
        dom.spitzring(n, Mv, cx, 0.0, 2.8, 4.2, 0.3, -0.14, -0.004, st["Basalt"])
        for f in (0.2, 0.45, 0.7):
            n.quader((cx, -0.045, 4.2 * f), (2.6, 0.02, 0.1), st["Eisen"], Mv, ohne=("+y",))
        bs.platte(n, Mh, dom.spitzbogen(-cx, 0.0, 2.2, 3.4, 4), -0.03, -0.005, st["Holz alt"])
        dom.spitzring(n, Mh, -cx, 0.0, 2.2, 3.4, 0.3, -0.14, -0.004, st["Basalt"])
        for f in (0.3, 0.7):
            n.quader((-cx, -0.045, 3.4 * f), (2.0, 0.02, 0.1), st["Eisen"], Mh, ohne=("+y",))
    # Seitenwände: Fenster
    for M in (bs.wandmatrix((x0, ym, 0), "links"), bs.wandmatrix((x1, ym, 0), "rechts")):
        for u in (-2.5, 2.5):
            kreuzstock(n, st, M, u, 5.6, 1.1, 1.7)
            bk.spitzfenster(n, st, M, u, 2.4, 0.9, 2.0, glas="Glas", pfosten=0)
    # Stadtwaage vor der Mitte: Waagegalgen aus Eiche auf Basaltsockeln (Zwergenwerk), großer eiserner Waagebalken an der Kette,
    # Schalen, Gewichte, Erzkisten der Zwerge, Weinfässer der Halblinge
    w = bs.Netz("Waage")
    wy0, wy1, wx = -7.6, -4.2, 3.0
    yb = (wy0 + wy1) / 2
    w.quader((0, yb, 0.12), (2 * wx + 1.4, wy1 - wy0, 0.24), st["Basaltquader"], ohne=("-z",))
    for sx in (-1, 1):
        w.quader((sx * wx, yb, 0.24 + 0.45), (0.7, 0.7, 0.9), st["Basalt"], ohne=("-z",))
        w.stab((sx * wx, yb, 1.14), (sx * wx, yb, 5.6), 0.36, 0.36, st["Fachwerk"])
        w.stab((sx * wx, yb, 4.2), (sx * (wx - 1.0), yb, 5.45), 0.18, 0.18, st["Fachwerk"], quer=(0, 1, 0))
        for dy in (-1, 1):
            w.stab((sx * wx, yb + dy * 1.1, 0.24), (sx * wx, yb + dy * 0.18, 2.2), 0.16, 0.16, st["Fachwerk"], quer=(1, 0, 0))
    w.stab((-wx - 0.4, yb, 5.6), (wx + 0.4, yb, 5.6), 0.36, 0.4, st["Fachwerk"])
    w.stab((-wx - 0.5, yb, 5.86), (wx + 0.5, yb, 5.86), 0.12, 0.5, st["Schiefer"])
    w.stab((0, yb, 5.4), (0, yb, 3.7), 0.06, 0.06, st["Eisen"])
    w.stab((-2.4, yb, 3.55), (2.4, yb, 3.65), 0.22, 0.26, st["Eisen"])
    w.quader((0, yb, 3.95), (0.1, 0.08, 0.6), st["Eisen"])
    for sx in (-1, 1):
        x = sx * 2.3
        zb = 3.6 + 0.05 * sx
        zt = zb - 2.4
        for dx, dy in ((-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)):
            w.stab((x, yb, zb), (x + dx, yb + dy, zt), 0.04, 0.04, st["Eisen"])
        w.quader((x, yb, zt), (1.2, 1.2, 0.1), st["Eisen"])
        if sx < 0:
            w.quader((x, yb, zt + 0.35), (0.9, 0.7, 0.6), st["Erz"], ohne=("-z",))
        else:
            for k in range(3):
                hh = 0.2 + 0.2 * (k % 2)
                w.quader((x - 0.3 + 0.3 * k, yb, zt + 0.05 + hh / 2), (0.25, 0.25, hh), st["Eisen"], ohne=("-z",))
    for k in range(5):
        hh = 0.3 + 0.1 * k
        w.quader((-1.2 + 0.5 * k, wy0 + 0.5, 0.24 + hh / 2), (0.32, 0.32, hh), st["Eisen"], ohne=("-z",))
    for k in range(3):
        w.quader((-wx - 1.8, -7.0 + 1.0 * k, 0.4), (0.9, 0.8, 0.8), st["Erz"], ohne=("-z",))
    for k in range(4):
        bs.fass(w, st, (wx + 1.6 + (k % 2) * 0.8, -6.6 + (k // 2) * 0.9, 0.0), r=0.38, h=1.0)
    anker = [bs.anker("Anker_Tuer", (-8.0, y0 - 0.5, 0.0)), bs.anker("Anker_Karren", (8.0, y0 - 2.6, 0.0)),
             bs.anker("Anker_Waage", (0.0, yb - 1.2, 0.0))]
    return [n.objekt(), w.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Rheinkran: Schwimmkran (in Köln belegt) – Ponton mit Kranhaus und Treträdern, Steg zum Kai
# ─────────────────────────────────────────────────────────────────────────────

def rheinkran(st):
    import koelner_dom as kd
    haus = bs.Netz("Haus")
    ponton = bs.Netz("Ponton")
    kran = bs.Netz("Kranhaus")
    rad = bs.Netz("Tretrad", alle=True)
    # Kai aus Bohlen mit Spundwand und Pollern (Uferkante bei y = 4)
    haus.quader((0, 0.0, 0.1), (8.0, 8.0, 0.2), st["Bretter"], ohne=("-z",))
    for x in range(-4, 5):
        haus.stab((x * 0.95, 4.1, 0.15), (x * 0.95, 4.1, -2.4), 0.3, 0.3, st["Holz alt"], quer=(1, 0, 0))
    haus.quader((0, 4.02, -1.1), (8.0, 0.08, 2.4), st["Bretter"], ohne=("-z", "-y"))
    for x in (-3.2, 3.2):
        haus.stab((x, 3.4, 0.2), (x, 3.4, 0.9), 0.32, 0.32, st["Holz"])
    # Ponton (flacher, geteerter Schwimmkörper) mit Deck und Dalben; Ursprung auf der Wasserlinie
    py0, py1, px = 6.0, 12.0, 5.0
    pm = (py0 + py1) / 2
    deck = WASSER + 0.75
    profil = [(py0 + 0.6, WASSER - 0.6), (py1 - 0.6, WASSER - 0.6), (py1, deck), (py0, deck)]
    ponton.prisma(profil, -px, px, st["Teerholz"])
    ponton.quader((0, pm, deck + 0.05), (2 * px - 0.2, py1 - py0 - 0.2, 0.1), st["Bretter"], ohne=("-z",))
    for x in (-px - 0.4, px + 0.4):
        for y in (py0 + 0.6, py1 - 0.6):
            ponton.stab((x, y, WASSER - 1.4), (x, y, deck + 1.6), 0.35, 0.35, st["Holz alt"])
    for s in (-1, 1):                                                               # Steg vom Kai auf das Deck
        ponton.stab((s * 0.6, 3.3, 0.22), (s * 0.6, py0 + 0.6, deck + 0.12), 0.14, 0.2, st["Holz"], quer=(1, 0, 0))
    for k in range(8):
        f = k / 7
        ponton.quader((0, 3.45 + (py0 + 0.45 - 3.45) * f, 0.32 + (deck + 0.2 - 0.32) * f), (1.5, 0.3, 0.06), st["Bretter"])
    # Kranhaus mit Treträdern und Ausleger zum Strom (+Y), dieselben Teile wie am Dom; die Last ist eine Kiste (Holz statt Werkstein),
    # der Haken hängt so hoch, dass sie über dem Wasser schwebt
    kst = bs.Stoffe(st)
    kst["Haustein"] = st["Bretter"]
    achse_kran = Vector((0.0, pm, deck + 0.05))
    kd.kran_bauen(kran, rad, kst, kran=achse_kran.copy(), h_turm=deck + 0.05, haken_z=2.3)
    anker = [bs.anker("Anker_Tuer", (0.0, -3.0, 0.0)), bs.anker("Anker_Ufer", (0.0, 4.0, 0.0)),
             bs.anker("Anker_Anleger", (0.0, py1 + 1.0, 0.0))]
    o_haus, o_ponton, o_kran, o_rad = haus.objekt(), ponton.objekt(), kran.objekt(), rad.objekt()
    wl = Vector((0.0, pm, WASSER))                                               # Ursprung des Pontons: Wasserlinie in seiner Mitte
    o_ponton.data.transform(Matrix.Translation(-wl))
    o_ponton.location = wl
    o_kran.data.transform(Matrix.Translation(-achse_kran))
    o_kran.location = achse_kran - wl
    o_kran.parent = o_ponton
    achse_rad = Vector((0.0, pm, deck + 0.05 + 0.15 + 1.55 + 1.3))               # Welle der Treträder (kran_bauen: fuss + 1,3)
    o_rad.data.transform(Matrix.Translation(-achse_rad))
    o_rad.location = achse_rad - achse_kran
    o_rad.parent = o_kran
    return [o_haus, o_ponton] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  Ausgabe und Vorschau
# ─────────────────────────────────────────────────────────────────────────────

AUS = arg("aus", None)
SCHNELL = bool(arg("schnell", False))
OHNE_BILDER = bool(arg("ohne-bilder", False))


def netze_von(objekte):
    alle = set(objekte) | {k for o in objekte for k in o.children_recursive}
    return [o for o in alle if o.type == "MESH"]


def kennzahlen(name, objekte):
    netze = netze_von(objekte)
    tri = 0
    for o in netze:
        o.data.calc_loop_triangles()
        tri += len(o.data.loop_triangles)
    aufrufe = sum(len([m for m in o.data.materials if m]) for o in netze)
    stoffe = sorted({m.name for o in netze for m in o.data.materials if m})
    print(f"KENNZAHLEN {name}: Dreiecke {tri}, Netze {len(netze)}, Materialien {len(stoffe)} ({', '.join(stoffe)}), "
          f"Zeichenaufrufe (Netz × Material) {aufrufe}")


def ausgeben(name, objekte, einzeln=False):
    """Bericht (Dreiecke, deckungsgleiche Flächen), Farben zusammenlegen, GLB schreiben."""
    bs.bericht(name, [o for o in objekte] + [k for o in objekte for k in o.children_recursive], einzeln=einzeln)
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"  Materialien: {vorher} → {nachher}")
    kennzahlen(name, objekte)
    print("geschrieben:", bs.exportieren(name, objekte))


def bild(name, sichtbar, drehung):
    return bs.rendern(name, sichtbar, drehung)


def nahbild(name, drehung, ziel, breite, ausschnitt, ziel_hoehe):
    import bpy
    szene = bpy.context.scene
    szene["_ziel"] = 0.0
    bs.blick(drehung)
    szene.camera.location += Vector(ziel)
    szene.camera.data.ortho_scale = breite
    pfad = bs.rendern_einzeln(name)
    szene["_ziel"] = ziel_hoehe
    szene.camera.data.ortho_scale = ausschnitt
    return pfad


def vorschau(name, flaeche, ziel, ausschnitt, nah, sichtbar=lambda o: True, buehne=True):
    import bpy
    import fuellung_vorschau as fv
    szene = bpy.context.scene
    if buehne:
        bs.vorschau_buehne(flaeche, ziel_hoehe=ziel, ausschnitt=ausschnitt, schnell=SCHNELL)
        szene.cycles.samples = 16 if SCHNELL else 48
        szene.render.resolution_x, szene.render.resolution_y = 1200, 900
        fv.texturieren()
    szene["_ziel"] = ziel
    szene.camera.data.ortho_scale = ausschnitt
    print(bild(f"{name}_1_vorne_links", sichtbar, 3))
    print(bild(f"{name}_2_hinten_rechts", sichtbar, 1))
    d, z, breite = nah
    print(nahbild(f"{name}_3_nah", d, z, breite, ausschnitt, ziel))
    if not SCHNELL:
        print(bs.anker_bild(f"{name}_4", sichtbar))


def wasserbuehne():
    """Für den Rheinkran: Land bis zur Uferkante (y = 4), dahinter Wasser bei −1,0 m."""
    import bpy
    alt = bpy.data.objects.get("_Umgebung")
    if alt:
        bpy.data.objects.remove(alt)
    u = bs.Netz("_Umgebung")
    boden = bs._hilfsstoff("_Boden2", (0.33, 0.33, 0.25))
    wasser = bs._hilfsstoff("_Wasser", (0.16, 0.22, 0.24))
    u.quader((0, -28, -0.05), (120, 64, 0.1), boden)
    u.quader((0, 34, WASSER - 0.05), (120, 60, 0.1), wasser)
    u.quader((0, 4.6, -2.6), (120, 1.2, 5.0), boden)
    u.objekt()


def kopie(obj, x):
    import bpy
    k = obj.copy()
    k.name = "_Mauer"
    k.location = Vector((x, 0, 0))
    bpy.context.scene.collection.objects.link(k)
    return k


def neu_bauen(fn):
    """Frische Szene, Bau mit eigenen Stoffen (für die Vorschau ein zweites Mal, ohne zusammengelegte Farben, mit Texturen)."""
    bs.neue_szene()
    return fn(bs.Stoffe())


def bau_rathaus():
    ausgeben("koelner_rathaus", neu_bauen(rathaus))
    if not OHNE_BILDER:
        neu_bauen(rathaus)
        vorschau("rathaus", (22.0, 13.0), 5.0, 34.0, (0, (-4, -2, 5), 16.0))


def bau_mauer():
    import bpy
    ausgeben("stadtmauer_koeln", mauerbaukasten(), einzeln=True)
    if OHNE_BILDER:
        return
    objekte = mauerbaukasten()
    teile = {o.name: o for o in objekte if o.type == "MESH"}
    stueck = teile["Mauerstueck"]
    zeigen = (("eigelstein", "Tor_Eigelstein", (-9.6, -13.6, 9.6, 13.6), 34.0, 6.0, (3, (0, -2, 7), 16.0)),
              ("hahnen", "Tor_Hahnen", (-9.6, -13.6, 9.6, 13.6), 34.0, 6.0, (0, (0, 2, 7), 16.0)),
              ("severinstor", "Tor_Severin", (-9.6, -13.6, 9.6, 13.6), 36.0, 8.0, (3, (0, -2, 11), 18.0)),
              ("bayenturm", "Eckturm_Bayen", (-2.0, -6.0, -10.0), 32.0, 9.0, (3, (0, 0, 17), 14.0)))
    for i, (name, teil, xs, ausschnitt, ziel, nah) in enumerate(zeigen):
        kontext = [kopie(stueck, x) for x in xs]
        o = teile[teil]
        wort = teil.split("_", 1)[1]
        for a in objekte:                     # Ankerbild: nur die Anker dieses Teils
            if a.type == "EMPTY":
                a["anker"] = a.name.startswith(("Anker_Tuer_" + wort, "Anker_Feuer_" + wort))
        sichtbar = (lambda oo, o=o, kontext=kontext: oo is o or oo in kontext)
        vorschau(name, (18.0, 12.0), ziel, ausschnitt, nah, sichtbar, buehne=i == 0)
        for k in kontext:
            bpy.data.objects.remove(k)


def bau_stapelhaus():
    ausgeben("stapelhaus", neu_bauen(stapelhaus))
    if not OHNE_BILDER:
        neu_bauen(stapelhaus)
        vorschau("stapelhaus", (26.0, 16.0), 5.0, 40.0, (3, (0, -5, 2), 14.0))


def bau_rheinkran():
    import bpy
    import fuellung_vorschau as fv
    ausgeben("wasser_schwimmkran", neu_bauen(rheinkran))
    if OHNE_BILDER:
        return
    neu_bauen(rheinkran)
    bs.vorschau_buehne((8.0, 8.0), ziel_hoehe=3.0, ausschnitt=30.0, schnell=SCHNELL)
    wasserbuehne()
    szene = bpy.context.scene
    szene.cycles.samples = 16 if SCHNELL else 48
    szene.render.resolution_x, szene.render.resolution_y = 1200, 900
    fv.texturieren()
    alles = lambda o: True                    # noqa: E731
    print(bild("rheinkran_1_vorne_links", alles, 3))
    print(bild("rheinkran_2_hinten_rechts", alles, 1))
    print(nahbild("rheinkran_3_nah", 3, (0, 8, 3), 14.0, 30.0, 3.0))
    if not SCHNELL:
        print(bs.anker_bild("rheinkran_4", alles))


BAUTEN = {"rathaus": bau_rathaus, "mauer": bau_mauer, "stapelhaus": bau_stapelhaus, "rheinkran": bau_rheinkran}


def main():
    import tempfile
    os.environ.setdefault("FUELLUNG_KOPIE", tempfile.gettempdir())      # Zwischenbilder der Texturen (fuellung_vorschau.texturieren)
    if AUS:
        os.makedirs(AUS, exist_ok=True)
        bs.VORSCHAU = AUS
    welche = arg("bau", "alle")
    for k in (BAUTEN if welche in ("alle", True) else welche.split(",")):
        BAUTEN[k]()


if __name__ == "__main__":
    main()