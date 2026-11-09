"""Die fünf romanischen Kirchen Kölns um 1400 (Sonderbauten, nur für Köln) aus dem Baukasten kirchen_baukasten.py.

    blender -b --python tools/modelle/koelner_kirchen.py -- [--kirche=martin,gereon,aposteln,severin,kunibert] [--schnell] [--ohne-bilder] [--aus=<ordner>]
    python tools/modelle/koelner_kirchen.py [--kirche=…] [--schnell] [--ohne-bilder] [--aus=<ordner>]

Schreibt je Kirche game/assets/modelle/gebaeude/koeln_<name>.glb und Vorschaubilder nach docs/modelle/ (oder --aus):
<name>.png (Spielkamera von vorne links), mit --alle-bilder dazu <name>-chor.png (hinten rechts), -nah.png, -anker.png, -nacht.png.
Ohne --kirche alle fünf.

Recherche, Zustand um 1400 und Entscheidungen des Nutzers (9.11.2026): docs/bereiche/darstellung.md „Köln: die fünf romanischen Kirchen“.
Steinsichtiger Tuff mit Gliederung aus Trachyt und Sockeln aus Basalt (nicht verputzt), Schieferdächer, keine Spur der Fantasy-Völker.
    Groß St. Martin   Kleeblattchor, Vierungsturm mit vier Giebeln und Rautendach (Notdach nach dem Brand 1378: unsicher), vier achteckige
                      Ecktürmchen mit Faltdach, kurzes Langhaus, Westvorhalle                                       14 × 22 m, bis 29,5 m
    St. Gereon        Zehneck (Dekagon) mit Strebepfeilern, Zwerggalerie und Blei-Zeltdach, Vorhalle, langer Ostchor mit Apsis, zwei
                      Chortürme mit Faltdach                                                                          14 × 30 m, bis 30,2 m
    St. Aposteln      Kleeblattchor mit achteckigem Vierungsturm und Laterne, zwei Osttürmchen, Westquerhaus mit Westturm (Rautendach)
                                                                                                                      14 × 29 m, bis 35,1 m
    St. Severin       romanisches Langhaus mit Querhaus, spätromanischer zweigeschossiger Chor mit Apsis und zwei Osttürmen, Westturm als
                      Baustelle (Neubau seit 1393/94: Stumpf, Gerüst, Tretradkran wie am Dom; das Rad dreht sich im Spiel)  12 × 32 m, bis 21,3 m
    St. Kunibert      Apsis mit Zwerggalerie und zwei schlanken Osttürmen, Langhaus ohne Vierungsturm, Westquerhaus mit Westturm und gotischem
                      Knickhelm (nach dem Brand 1376 erneuert)                                                        14 × 29 m, bis 37,6 m
Der Kölner Dom (koelner_dom.py, etwa 41 m) bleibt der höchste Bau.

Objekte im GLB:
    Kirche              das ganze Bauwerk (ein Netz)
    Baustelle, Tretrad  nur St. Severin: Gerüst, Werksteine, Kranhaus; die Räder mit dem Ursprung in der Radachse (das Spiel dreht „Tretrad“)
    Anker_Tuer          0,5 m vor dem Westportal auf der Stufe, Blick ins Haus (Pflicht nach pruefen.py)
    Anker_EwigesLicht   im Bogen des Westportals, Blick nach außen (rotes Nachtlicht wie am Dom)
    Anker_Glocke        in der Glockenstube des Hauptturms (bei St. Severin im südlichen Ostturm; für ein eigenes Geläut, siehe sound)
Keine Zustände und keine Hausfarbe: Die Kirchen gehören der Stadt (`pruefen.py … B T --sonderbau`). Die Fenster tragen das Material „Fenster“
und leuchten im Spiel wie die Domfenster nur zur Messe; die kleinen Turmfenster tragen „Glas“ (immer dunkel).

Aufbau (Blender: Z oben, Westseite mit dem Portal bei −Y, Chor bei +Y, Süden = +X; Ursprung in der Mitte der Fläche). Im Stadtskript stehen
alle fünf mit Drehung 270: Chor nach Osten, Westfront zur Gasse. Maßstab etwa 1 : 2,5 (Grundriss und Höhe).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kirchen_baukasten as bk  # noqa: E402
from kirchen_baukasten import bs, dom, Vector, Matrix  # noqa: E402


def arg(name, vorgabe=None):
    """Schalter --name oder --name=wert aus der Befehlszeile (Blender: nach „--“, Python-Modul: überall)."""
    for a in sys.argv:
        if a.startswith("--" + name + "="):
            return a.split("=", 1)[1]
        if a == "--" + name:
            return True
    return vorgabe


# ─────────────────────────────────────────────────────────────────────────────
#  Groß St. Martin – Kleeblattchor, Vierungsturm mit Giebeln und Rautendach, vier Ecktürmchen
# ─────────────────────────────────────────────────────────────────────────────

def martin(st):
    n = bs.Netz("Kirche")
    YV, A = 4.4, 3.0                    # Mitte und halbe Breite der Vierung
    ZK = 9.0                            # Traufe der Konchen
    for mitte, richt in (((0, YV + A), 90), ((A, YV), 0), ((-A, YV), 180)):
        bk.konche(n, st, mitte, A, richt, ZK, n=5, fenster=((1.8, 0.9, 2.2), (4.6, 0.9, 1.9)), z_spitze=ZK + 3.2)
    # Vierungsturm mit zwei Geschossen Schallarkaden, vier Giebeln und Rautendach (Zustand nach dem Brand 1378: unsicher)
    bk.turm4(n, st, 0, YV, A, 0.0, 21.0, geschosse=(ZK + 3.4, 15.2, 18.0), schall=((15.6, 2.0, 2.1, 2), (18.3, 2.0, 2.1, 2)),
             fenster=((ZK + 3.9, 0.7, 1.5, 2),), dach="rhomben", dach_h=7.6)
    # vier achteckige Ecktürmchen mit Faltdach
    for sx in (-1, 1):
        for sy in (-1, 1):
            bk.turm8(n, st, sx * A, YV + sy * A, 0.95, 0.0, 19.6, geschosse=(ZK + 0.2, 13.6, 16.6), schall=(17.0, 0.45, 1.6),
                     fenster=(10.6, 0.35, 1.0), dach="falt", dach_h=5.2)
    # kurzes Langhaus (3½ Joche) mit Seitenschiffen
    y0, y1 = -8.6, YV - A
    bk.basilika(n, st, y0, y1, 3.0, 5.0, 11.5, 5.8, 3, neigung=48, westgiebel=False, portal=False)
    # Westvorhalle (zwei Joche, niedrig) mit Portal
    yv0 = -10.3
    for s in (-1, 1):
        M = bk.wm((s * 3.0, (yv0 + 0.7 + y0) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.35, 2.6), (y0 - yv0 - 0.7, 0.7, 5.2), st["Tuff"], M, ohne=("-z",))
        bk.sockel(n, st, M, y0 - yv0 - 0.7)
        bk.rundfenster(n, st, M, 0, 1.9, 0.9, 1.9)
    Mf = bs.wandmatrix((0, yv0, 0), "vorne")
    n.quader((0, 0.35, 2.6), (6.0, 0.7, 5.2), st["Tuff"], Mf, ohne=("-z",))
    bk.sockel(n, st, Mf, 6.0)
    d = bk.satteldach_y(n, st, yv0, y0 + 0.3, 3.0, 5.2, 45)
    bk.dachgiebel(n, st, (0, yv0 + 0.3), 5.2, 3.0, d["tan"], "vorne", oculus=False)
    bk.portal_rund(n, st, Mf, 0.0, 1.9, 3.3, stufen=2)
    bk.fries(n, st, Mf, 6.0, 5.1)
    # Westgiebel des Mittelschiffs über der Vorhalle
    bk.dachgiebel(n, st, (0, y0 + 0.3), 11.5, 3.0, math.tan(math.radians(48)), "vorne")
    Mg = bs.wandmatrix((0, y0, 0), "vorne")
    n.quader((0, 0.35, 8.3), (6.0, 0.7, 6.4), st["Tuff"], Mg, ohne=("-z",))
    bk.rundfenster(n, st, Mg, -1.1, 6.6, 0.8, 2.0)
    bk.rundfenster(n, st, Mg, 1.1, 6.6, 0.8, 2.0)
    bk.fries(n, st, Mg, 6.0, 11.35)
    anker = [bs.anker("Anker_Tuer", (0.0, yv0 - 0.5, 0.16)), bs.anker("Anker_EwigesLicht", (0.0, yv0 - 0.15, 2.2), 180.0),
             bs.anker("Anker_Glocke", (0.0, YV, 16.5))]
    return [n.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  St. Gereon – Zehneck (Dekagon) mit Bleizeltdach, langer Ostchor mit Apsis und zwei Chortürmen mit Faltdächern
# ─────────────────────────────────────────────────────────────────────────────

def gereon(st):
    n = bs.Netz("Kirche")
    yd = -5.2
    rx, ry = 6.0, 7.2
    bk.zehneck(n, st, (0, yd), rx, ry, 16.5, fenster_unten=(3.6, 1.3, 2.4), fenster_oben=(9.0, 1.2, 3.6), dach="kegel", dach_h=12.5,
               dach_stoff="Blei")
    # Vorhalle im Westen
    yv0 = -14.2
    yv1 = yd - ry + 0.6
    for s in (-1, 1):
        M = bk.wm((s * 2.6, (yv0 + 0.7 + yv1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.35, 2.6), (yv1 - yv0 - 0.7, 0.7, 5.2), st["Tuff"], M, ohne=("-z",))
        bk.sockel(n, st, M, yv1 - yv0 - 0.7)
    Mf = bs.wandmatrix((0, yv0, 0), "vorne")
    n.quader((0, 0.35, 2.6), (5.2, 0.7, 5.2), st["Tuff"], Mf, ohne=("-z",))
    bk.sockel(n, st, Mf, 5.2)
    d = bk.satteldach_y(n, st, yv0, yv1, 2.6, 5.2, 45)
    bk.dachgiebel(n, st, (0, yv0 + 0.3), 5.2, 2.6, d["tan"], "vorne", oculus=False)
    bk.portal_rund(n, st, Mf, 0.0, 1.8, 3.2, stufen=2)
    bk.fries(n, st, Mf, 5.2, 5.1)
    # langer Ostchor
    yc0, yc1 = yd + ry - 0.8, 10.8
    L = yc1 - yc0
    for s in (-1, 1):
        M = bk.wm((s * 3.0, (yc0 + yc1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 5.75), (L, 0.8, 11.5), st["Tuff"], M, ohne=("-z",))
        bk.sockel(n, st, M, L)
        bk.fries(n, st, M, L, 11.35)
        bk.gesims(n, st, M, L, 11.45)
        for i in range(4):
            u = -L / 2 + L * (i + 0.5) / 4
            bk.rundfenster(n, st, M, u, 6.4, 1.0, 2.8)
            bk.blende(n, st, M, u, 1.4, 1.4, 3.0)
        for i in range(5):
            bk.lisene(n, st, M, -L / 2 + L * i / 4, 0.7, 10.8)
    bk.satteldach_y(n, st, yc0 - 0.5, yc1, 3.0, 11.5, 47)
    bk.konche(n, st, (0, yc1), 3.0, 90, 10.0, n=5, fenster=((2.0, 0.9, 2.3), (5.3, 1.0, 2.4)), z_spitze=13.0)
    # Chorflankentürme (sechs Geschosse, Schallarkaden, Faltdach mit zwei Giebeln je Seite)
    for s in (-1, 1):
        bk.turm4(n, st, s * 4.2, yc1 - 0.6, 1.25, 0.0, 22.0, geschosse=(4.0, 7.6, 11.2, 14.8, 18.4), schall=((18.8, 1.5, 2.2, 1), (15.2, 1.5, 2.2, 1)),
                 fenster=((8.2, 0.5, 1.4, 1), (11.8, 0.5, 1.4, 1)), dach="falt2", dach_h=4.6, fries_oben=False)
    anker = [bs.anker("Anker_Tuer", (0.0, yv0 - 0.5, 0.16)), bs.anker("Anker_EwigesLicht", (0.0, yv0 - 0.15, 2.1), 180.0),
             bs.anker("Anker_Glocke", (4.2, yc1 - 0.6, 19.8))]
    return [n.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  St. Aposteln – Kleeblattchor, achteckiger Vierungsturm mit Laterne und zwei Osttürmchen, Westquerhaus mit hohem Westturm
# ─────────────────────────────────────────────────────────────────────────────

def aposteln(st):
    n = bs.Netz("Kirche")
    YV, A = 5.4, 3.0
    ZK = 9.5
    for mitte, richt in (((0, YV + A), 90), ((A, YV), 0), ((-A, YV), 180)):
        bk.konche(n, st, mitte, A, richt, ZK, n=5, fenster=((2.0, 0.9, 2.2), (5.0, 0.9, 1.9)), z_spitze=ZK + 3.0)
    n.quader((0, YV, 6.5), (2 * A, 2 * A, 13.0), st["Tuff"], ohne=("-z",))
    bk.turm8(n, st, 0, YV, 3.3, 0.0, 17.4, geschosse=(13.0,), fenster=(14.0, 0.8, 2.0), dach="kegel", dach_h=3.8)
    # Laterne (ob mittelalterlich, ist unsicher; behalten – Entscheidung des Nutzers, 9.11.2026)
    bk.turm8(n, st, 0, YV, 0.75, 21.0, 22.6, schall=(21.3, 0.35, 1.0), dach="helm", dach_h=2.4)
    for s in (-1, 1):
        bk.turm8(n, st, s * 3.1, YV + 3.2, 0.95, 0.0, 19.5, geschosse=(ZK + 0.2, 13.4, 16.5), schall=(16.9, 0.45, 1.7),
                 fenster=(11.0, 0.35, 1.0), dach="helm", dach_h=5.0)
    # Langhaus
    y0, y1 = -8.4, YV - A
    bk.basilika(n, st, y0, y1, 3.0, 5.4, 12.0, 6.0, 4, neigung=47, westgiebel=False, portal=False)
    # Westquerhaus mit Westturm über der Mitte
    yq0, yq1 = -13.6, y0
    for s in (-1, 1):
        Ms = bk.wm((s * 6.6, (yq0 + yq1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 5.75), (yq1 - yq0, 0.8, 11.5), st["Tuff"], Ms, ohne=("-z",))
        bk.sockel(n, st, Ms, yq1 - yq0)
        bk.rundfenster(n, st, Ms, 0, 5.5, 1.0, 2.6)
        bk.fries(n, st, Ms, yq1 - yq0, 11.35)
    for y, rt in ((yq0, "vorne"), (yq1, "hinten")):
        M = bs.wandmatrix((0, y + (0.4 if rt == "vorne" else -0.4), 0), rt)
        n.quader((0, 0.0, 5.75), (11.6, 0.8, 11.5), st["Tuff"], M, ohne=("-z",))
        if rt == "vorne":
            Mv = bs.wandmatrix((0, y, 0), rt)
            bk.sockel(n, st, Mv, 13.2)
            for u in (-4.6, 4.6):
                bk.rundfenster(n, st, Mv, u, 5.5, 1.0, 2.6)
            bk.fries(n, st, Mv, 13.2, 11.35)
    d = bk.satteldach_x(n, st, -6.6, 6.6, yq0, yq1, 11.5, 47)
    for s, rt in ((-1, "links"), (1, "rechts")):
        bk.dachgiebel(n, st, (s * 6.3, (yq0 + yq1) / 2), 11.5, (yq1 - yq0) / 2, d["tan"], rt)
    yt = (yq0 + yq1) / 2
    bk.turm4(n, st, 0, yt, 2.9, 0.0, 26.0, geschosse=(11.5, 15.4, 19.2, 22.6), schall=((23.0, 2.0, 2.3, 2), (19.6, 2.0, 2.3, 2)),
             blenden=((15.8, 1.4, 2.6, 2),), fenster=((6.2, 0.9, 2.4, 1),), dach="rhomben", dach_h=8.2)
    yf = yt - 2.9                                       # Westfront des Turms (steht vor dem Querhaus)
    Mt = bs.wandmatrix((0, yf, 0), "vorne")
    bk.portal_rund(n, st, Mt, 0.0, 2.0, 3.6, stufen=3)
    anker = [bs.anker("Anker_Tuer", (0.0, yf - 0.45, 0.16)), bs.anker("Anker_EwigesLicht", (0.0, yf - 0.15, 2.4), 180.0),
             bs.anker("Anker_Glocke", (0.0, yt, 21.5))]
    return [n.objekt()] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  St. Severin – romanisches Langhaus mit Querhaus, langer spätromanischer Chor mit zwei Osttürmen, Westturm als Baustelle
# ─────────────────────────────────────────────────────────────────────────────

def severin(st):
    import koelner_dom as kd           # Tretradkran wie am Dom
    n = bs.Netz("Kirche")
    g = bs.Netz("Baustelle")
    # Langhaus (romanisch, der gotische Umbau beginnt erst)
    y0, y1 = -9.0, 2.0
    dl = bk.basilika(n, st, y0, y1, 2.8, 4.9, 11.5, 5.8, 4, neigung=47, westgiebel=False, portal=False)
    bk.dachgiebel(n, st, (0, y1), 11.5, 2.8, dl["tan"], "hinten", oculus=False)     # Ostgiebel über dem niedrigeren Querhausdach
    # Querhaus
    yq0, yq1 = 2.0, 6.4
    for s in (-1, 1):
        M = bk.wm((s * 5.6, (yq0 + yq1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 5.75), (yq1 - yq0, 0.8, 11.5), st["Tuff"], M, ohne=("-z",))
        bk.sockel(n, st, M, yq1 - yq0)
        bk.rundfenster(n, st, M, 0, 5.0, 1.0, 2.8)
        for y, rt in ((yq0, "vorne"), (yq1, "hinten")):
            Mw = bs.wandmatrix((s * 3.8, y + (0.4 if rt == "vorne" else -0.4), 0), rt)
            n.quader((0, 0, 5.75), (2.0, 0.8, 11.5), st["Tuff"], Mw, ohne=("-z",))
    d = bk.satteldach_x(n, st, -5.6, 5.6, yq0, yq1, 11.5, 47)
    for s, rt in ((-1, "links"), (1, "rechts")):
        bk.dachgiebel(n, st, (s * 5.3, (yq0 + yq1) / 2), 11.5, (yq1 - yq0) / 2, d["tan"], rt)
    # langer zweigeschossiger Chor (geweiht 1237) mit Apsis
    yc0, yc1 = yq1, 11.2
    L = yc1 - yc0
    for s in (-1, 1):
        M = bk.wm((s * 2.8, (yc0 + yc1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 6.0), (L, 0.8, 12.0), st["Tuff"], M, ohne=("-z",))
        bk.sockel(n, st, M, L)
        bk.fries(n, st, M, L, 11.85)
        for i in range(2):
            u = -L / 2 + L * (i + 0.5) / 2
            bk.rundfenster(n, st, M, u, 2.0, 0.9, 2.4)
            bk.rundfenster(n, st, M, u, 6.6, 1.1, 3.4)
        bk.gesims(n, st, M, L, 5.2, vor=0.1)
    dc = bk.satteldach_y(n, st, yc0 - 0.3, yc1, 2.8, 12.0, 50)
    bk.dachgiebel(n, st, (0, yc0 - 0.3), 12.0, 2.8, dc["tan"], "vorne", oculus=False)     # Westgiebel des Chors über dem Querhausdach
    bk.konche(n, st, (0, yc1), 2.8, 90, 11.0, n=5, fenster=((2.0, 0.85, 2.2), (6.2, 1.0, 3.0)), z_spitze=14.2)
    # zwei Osttürme am Chor (die spätgotischen Aufbauten kamen später; um 1400 niedriger mit Pyramidendach: unsicher)
    for s in (-1, 1):
        bk.turm4(n, st, s * 3.95, yc0 + 1.25, 1.15, 0.0, 16.5, geschosse=(5.2, 9.0, 12.8), schall=((13.2, 1.4, 2.0, 1),),
                 fenster=((9.6, 0.5, 1.4, 1),), dach="pyramide", dach_h=4.2, fries_oben=False)
    # Westturm im Bau (Neubau seit 1393/94): Stumpf mit Portal und Mauerkrone, Gerüst und Tretradkran wie am Dom
    yt, a, ht = -11.7, 3.1, 13.0
    n.quader((0, yt, ht / 2), (2 * a, 2 * a, ht), st["Tuff"], ohne=("-z",))
    bk.band4(n, st, 0, yt, a, 0.6, h=1.2, vor=0.1, stoff="Basalt")
    bk.band4(n, st, 0, yt, a, 6.0)
    for M, w in bk.seiten4(0, yt, a):
        for sx in (-1, 1):                          # gotische Eckstrebepfeiler
            n.quader((sx * (a - 0.4), -0.45, ht * 0.45), (0.8, 0.9, ht * 0.9), st["Tuff"], M, ohne=("+y", "-z"))
        if w != 270:
            bk.spitzfenster(n, st, M, 0, 7.2, 1.3, 4.0, glas="Glas")
    Mf = bs.wandmatrix((0, yt - a, 0), "vorne")
    bs.platte(n, Mf, dom.spitzbogen(0, 0, 2.2, 4.4, 4), -0.03, -0.002, st["Holz alt"])
    dom.spitzring(n, Mf, 0, 0, 2.2, 4.4, 0.3, -0.2, -0.004, st["Trachyt"])
    n.quader((0, -0.55, 0.08), (3.6, 1.1, 0.16), st["Trachyt"], Mf, ohne=("+y", "-z"))
    rnd = random.Random(7)
    for k in range(18):                                 # unfertige Mauerkrone
        f = (k % 9 + 0.5) / 9
        if k < 9:
            p = (-a + 0.5 + f * (2 * a - 1), yt + (-(a - 0.5) if k % 2 else (a - 0.5)))
        else:
            p = ((-(a - 0.5) if k % 2 else (a - 0.5)), yt - a + 0.5 + f * (2 * a - 1))
        h = 0.3 + rnd.random() * 1.0
        n.quader((p[0], p[1], ht + h / 2 - 0.05), (0.8, 0.8, h), st["Tuff" if rnd.random() < 0.6 else "Trachyt"], ohne=("-z",))
    bk.geruest(g, st, -a - 0.7, a + 0.7, yt - a - 0.7, yt + a + 0.7, 0.0, ht + 2.2, ebene=2.2)
    for k in range(3):                                  # Werksteine am Fuß
        g.quader((4.9, yt - 1.5 + 1.0 * k, 0.35), (0.8, 0.7, 0.7), st["Tuff"], ohne=("-z",))
    # Tretradkran auf dem Stumpf (Bauteile vom Kölner Dom, Ausleger nach Osten über das Langhaus, der Haken über dem Dachfirst)
    g.quader((0, yt, ht + 0.02), (2 * a - 1.2, 2 * a - 1.2, 0.14), st["Bretter"], ohne=("-z",))
    rad = bs.Netz("Tretrad", alle=True)
    kran = Vector((0.0, yt, 0.0))
    kd.kran_bauen(g, rad, st, kran=kran, h_turm=ht, haken_z=16.6)
    anker = [bs.anker("Anker_Tuer", (0.0, yt - a - 0.5, 0.16)), bs.anker("Anker_EwigesLicht", (0.0, yt - a - 0.2, 2.6), 180.0),
             bs.anker("Anker_Glocke", (3.95, yc0 + 1.25, 14.2))]
    # Das Spiel dreht den Knoten „Tretrad“ um seinen Ursprung: Der gehört in die Radachse (wie beim Dom)
    o_rad = rad.objekt()
    achse = Vector((0.0, yt, ht + 0.15 + 1.55 + 1.3))
    o_rad.data.transform(Matrix.Translation(-achse))
    o_rad.location = achse
    return [n.objekt(), g.objekt(), o_rad] + anker


# ─────────────────────────────────────────────────────────────────────────────
#  St. Kunibert – Apsis mit zwei schlanken Osttürmen, Langhaus, Westquerhaus mit Westturm und gotischem Knickhelm (um 1400 neu)
# ─────────────────────────────────────────────────────────────────────────────

def kunibert(st):
    n = bs.Netz("Kirche")
    # Ostteil: Chorjoch, Apsis mit Zwerggalerie, Osttürme (deren Untergeschosse wirken wie kurze Querhausarme)
    yc0, yc1 = 3.0, 8.6
    L = yc1 - yc0
    for s in (-1, 1):
        M = bk.wm((s * 3.0, (yc0 + yc1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 6.0), (L, 0.8, 12.0), st["Tuff"], M, ohne=("-z",))
        bk.fries(n, st, M, L, 11.85)
    dc = bk.satteldach_y(n, st, yc0 - 0.3, yc1, 3.0, 12.0, 48)
    bk.dachgiebel(n, st, (0, yc0 - 0.3), 12.0, 3.0, dc["tan"], "vorne", oculus=False)     # Westgiebel des Chors über dem Langhausdach
    bk.konche(n, st, (0, yc1), 3.0, 90, 11.0, n=5, fenster=((2.0, 0.85, 2.3), (5.6, 1.1, 3.0)), z_spitze=14.6)
    for s in (-1, 1):
        bk.turm4(n, st, s * 4.3, 5.0, 1.3, 0.0, 23.0, geschosse=(9.0, 12.6, 16.2, 19.6), schall=((20.0, 1.6, 2.2, 1), (16.6, 1.6, 2.2, 1)),
                 blenden=((12.9, 1.4, 2.6, 1),), fenster=((5.0, 0.8, 2.2, 1),), dach="pyramide", dach_h=6.5, fries_oben=False)
    # Langhaus
    y0, y1 = -8.2, yc0
    bk.basilika(n, st, y0, y1, 3.0, 5.3, 11.5, 5.8, 4, neigung=47, westgiebel=False, portal=False)
    # Westquerhaus mit Westturm
    yq0, yq1 = -13.4, y0
    for s in (-1, 1):
        Ms = bk.wm((s * 6.5, (yq0 + yq1) / 2, 0), 0 if s > 0 else 180)
        n.quader((0, 0.4, 5.5), (yq1 - yq0, 0.8, 11.0), st["Tuff"], Ms, ohne=("-z",))
        bk.sockel(n, st, Ms, yq1 - yq0)
        bk.rundfenster(n, st, Ms, 0, 5.2, 1.0, 2.6)
    for y, rt in ((yq0, "vorne"), (yq1, "hinten")):
        M = bs.wandmatrix((0, y + (0.4 if rt == "vorne" else -0.4), 0), rt)
        n.quader((0, 0.0, 5.5), (11.4, 0.8, 11.0), st["Tuff"], M, ohne=("-z",))
        if rt == "vorne":
            Mv = bs.wandmatrix((0, y, 0), rt)
            bk.sockel(n, st, Mv, 13.0)
            for u in (-4.6, 4.6):
                bk.rundfenster(n, st, Mv, u, 5.2, 1.0, 2.6)
            bk.fries(n, st, Mv, 13.0, 10.85)
    d = bk.satteldach_x(n, st, -6.5, 6.5, yq0, yq1, 11.0, 47)
    for s, rt in ((-1, "links"), (1, "rechts")):
        bk.dachgiebel(n, st, (s * 6.2, (yq0 + yq1) / 2), 11.0, (yq1 - yq0) / 2, d["tan"], rt)
    yt = (yq0 + yq1) / 2
    bk.turm4(n, st, 0, yt, 3.1, 0.0, 25.0, geschosse=(11.0, 15.0, 18.6, 21.8), schall=((22.2, 2.0, 2.2, 2), (19.0, 2.0, 2.2, 2)),
             blenden=((15.4, 1.5, 2.6, 2),), dach="knick", dach_h=12.0)
    yf = yt - 3.1                                       # Westfront des Turms
    Mt = bs.wandmatrix((0, yf, 0), "vorne")
    bk.portal_rund(n, st, Mt, 0.0, 2.0, 3.6, stufen=3)
    anker = [bs.anker("Anker_Tuer", (0.0, yf - 0.45, 0.16)), bs.anker("Anker_EwigesLicht", (0.0, yf - 0.15, 2.4), 180.0),
             bs.anker("Anker_Glocke", (0.0, yt, 20.5))]
    return [n.objekt()] + anker


# Schlüssel: Modellname, Fläche (B × T), Bauen, Bildausschnitt (m), Zielhöhe der Kamera, Nahblick (Drehung, Ziel, Breite)
KIRCHEN = {
    "martin": ("koeln_gross_st_martin", (14.0, 22.0), martin, 42.0, 11.0, (0, (0, 4.4, 14), 22.0)),
    "gereon": ("koeln_st_gereon", (14.0, 30.0), gereon, 50.0, 10.0, (3, (0, -5, 11), 24.0)),
    "aposteln": ("koeln_st_aposteln", (14.0, 29.0), aposteln, 50.0, 13.0, (1, (0, 5.4, 11), 22.0)),
    "severin": ("koeln_st_severin", (12.0, 32.0), severin, 48.0, 9.0, (3, (0, -12, 9), 20.0)),
    "kunibert": ("koeln_st_kunibert", (14.0, 29.0), kunibert, 52.0, 13.0, (1, (0, 7, 12), 22.0)),
}


def bauen(schluessel):
    """Neue Szene, die Kirche bauen; gibt die Objekte (Netze und Anker) zurück."""
    bs.neue_szene()
    return KIRCHEN[schluessel][2](bs.Stoffe())


def ausgeben(schluessel):
    """Bauen, prüfen, Farben zusammenlegen und als GLB schreiben; druckt Dreiecke, Materialien und geschätzte Zeichenaufrufe."""
    name = KIRCHEN[schluessel][0]
    objekte = bauen(schluessel)
    bs.bericht(name, objekte)
    netze = [o for o in objekte if o.type == "MESH"]
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    # Zeichenaufrufe: je Netz ein Aufruf je Material, dazu ein Schattenaufruf je Netz (Schattenproxy)
    aufrufe = sum(len([m for m in o.data.materials if m]) for o in netze) + len(netze)
    stoffe = sorted({m.name for o in netze for m in o.data.materials if m})
    print(f"  Materialien: {vorher} → {nachher} ({', '.join(stoffe)}); Netze {len(netze)}; Zeichenaufrufe geschätzt {aufrufe}")
    print("geschrieben:", bs.exportieren(name, objekte))


def bilder(schluessel, schnell, alle):
    """Vorschau mit den Texturen der Material-Bibliothek wie im Spiel (vor dem Zusammenlegen, frisch gebaut)."""
    import bpy
    import fuellung_vorschau as fv
    name, flaeche, _, ausschnitt, ziel, nah = KIRCHEN[schluessel]
    bauen(schluessel)
    bs.vorschau_buehne(flaeche, ziel_hoehe=ziel, ausschnitt=ausschnitt, schnell=schnell)
    szene = bpy.context.scene
    os.environ.setdefault("FUELLUNG_KOPIE", bpy.app.tempdir)       # Ablage der Schichtbilder für die Vorschau
    fv.texturieren()
    alles = lambda o: True  # noqa: E731
    print(bs.rendern(name, alles, 3))                               # vorne links (Westfront und Südseite), wie die Spielkamera
    if not alle:
        return
    print(bs.rendern(name + "-chor", alles, 1))                     # hinten rechts (Chor)
    print(bs.anker_bild(name, alles))
    print(bs.nachtbild(name, alles, 3))                             # Fenster hell (im Spiel nur zur Messe)
    d, z, breite = nah
    szene["_ziel"] = 0.0
    bs.blick(d)
    szene.camera.location += Vector(z)
    szene.camera.data.ortho_scale = breite
    print(bs.rendern_einzeln(name + "-nah"))
    szene["_ziel"] = ziel
    szene.camera.data.ortho_scale = ausschnitt


def main():
    welche = arg("kirche")
    liste = list(KIRCHEN) if welche in (None, True, "alle") else welche.split(",")
    if arg("aus"):
        bs.VORSCHAU = arg("aus")
    for k in liste:
        ausgeben(k)
        if not arg("ohne-bilder"):
            bilder(k, bool(arg("schnell")), bool(arg("alle-bilder")))


if __name__ == "__main__":
    main()
