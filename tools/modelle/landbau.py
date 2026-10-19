"""Gemeinsame Teile der Umlandbetriebe (Grube, Holzschlag, Steinbruch, Gärten, Bauernhof, Klosterhof, Wachturm)
und der Arbeitsstellen der Einwohner (Weide, Feld, Wald). Kein eigenes Modell – die Skripte importieren es.

Knoten eines Umlandbetriebs im GLB:
    Haus        Gebäude und alles, was auch eine verlassene Anlage noch hat (Hütte, Bruchwand, Beete, Felder)
    Arbeit      was nur im Betrieb da ist: Ware, Werkzeug, Pflanzen, Bienenkörbe – im Leerstand aus
    Fahne       Fahne in der Hausfarbe (Besitzer) – im Leerstand aus
    Leerstand   Spuren der verlassenen Anlage: Bretter vor Tür und Fenstern, Unkraut, Brache (standardmäßig aus)
    Anker_Tuer, Anker_Karren
Umlandbetriebe haben keine Stufen und kein Gerüst (sie bleiben auf Stufe 1).
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Oeffnung, Vector  # noqa: E402

bs.FARBEN.update({
    "Erde": (0.36, 0.27, 0.18),            # Beete, Acker
    "Erde hell": (0.47, 0.39, 0.28),       # festgetretener Hof und Feldweg
    "Tanne": (0.15, 0.24, 0.15),
    "Tanne hell": (0.21, 0.31, 0.18),
    "Fels": (0.44, 0.42, 0.38),
    "Fels dunkel": (0.33, 0.32, 0.29),
    "Rinde": (0.30, 0.22, 0.15),
    "Korn": (0.78, 0.66, 0.32),            # Gerste (wie bisher im Spiel)
    "Roggen": (0.66, 0.55, 0.28),          # wie bisher im Spiel
    "Stoppel": (0.55, 0.48, 0.32),         # abgeerntetes oder brachliegendes Feld
    "Kohl": (0.42, 0.55, 0.32),
    "Lavendel": (0.55, 0.43, 0.78),
    "Jasmin": (0.96, 0.95, 0.88),
    "Erz": (0.46, 0.27, 0.19),             # rostiges Eisenerz
    "Silbererz": (0.60, 0.61, 0.63),
    "Schilf": (0.47, 0.50, 0.29),
    "Segeltuch": (0.80, 0.76, 0.64),       # Bespannung der Mühlenflügel
})

ZUSTAENDE = ("Leerstand",)


# ─────────────────────────────────────────────────────────────────────────────
#  Boden
# ─────────────────────────────────────────────────────────────────────────────

def flaeche(netz, st, umriss, z, stoff):
    """Flaches Vieleck nach oben auf Höhe z (Hof, Weg, Acker). Verschiedene Flächen bekommen verschiedene z."""
    netz.vieleck_nach([(x, y, z) for x, y in umriss], (0, 0, 1), st[stoff])


def rechteck(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


# ─────────────────────────────────────────────────────────────────────────────
#  Hütte: kleines Haus mit Satteldach (First entlang X), Tür und Fenster vorn
# ─────────────────────────────────────────────────────────────────────────────

def huette(netz, st, x0, x1, y0, y1, h, wand="bretter", deckung=bs.SCHIEFER, neigung=45, tuer_u=0.0,
           fenster_vorn=(), fenster_seite=(), sockel=0.25, giebel_fenster=False, traufe=0.35):
    """Hütte von x0..x1 und y0 (vorn) .. y1, Wandhöhe h über dem Sockel.
    wand: "bretter" (senkrechte Bretter), "block" (liegende Stämme), "fachwerk" (Lehmputz), "putz", "stein".
    `tuer_u=None`: keine Tür (der Aufrufer baut ein Tor). Gibt ein Wörterbuch mit dem Dach, den Öffnungen (für die
    Bretter im Leerstand) und dem Punkt vor der Tür zurück."""
    L, T = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    stoffe = {"bretter": "Holz alt", "block": "Holz alt", "fachwerk": "Lehmputz", "putz": "Putz", "stein": "Bruchstein"}
    m_wand = st[stoffe[wand]]
    netz.quader((cx, cy, sockel / 2), (L + 0.1, T + 0.1, sockel), st["Bruchstein dunkel"], ohne=("-z",))
    netz.quader((cx, cy, sockel + h / 2), (L, T, h), m_wand, ohne=("+z", "-z"))
    oeffnungen = []
    eg = dict(v=0.8, b=0.7, h=0.85)
    tuer = [Oeffnung("tuer", tuer_u, 0.0, 0.95, 1.95)] if tuer_u is not None else []
    waende = {
        "vorne": ((cx, y0), L, tuer + [Oeffnung("fenster", u, **eg) for u in fenster_vorn]),
        "hinten": ((cx, y1), L, []),
        "links": ((x0, cy), T, [Oeffnung("fenster", u, laeden=False, **eg) for s, u in fenster_seite if s == "links"]),
        "rechts": ((x1, cy), T, [Oeffnung("fenster", u, laeden=False, **eg) for s, u in fenster_seite if s == "rechts"]),
    }
    for seite, ((x, y), breite, liste) in waende.items():
        M = bs.wandmatrix((x, y, sockel), seite)
        wandbild(netz, st, M, breite, h, liste, wand, saat=len(seite), ecken=seite in ("vorne", "hinten"))
        for o in liste:
            if o.art == "tuer":
                bs.tuer(netz, M, o, st)
            else:
                bs.fenster(netz, M, o, st, winkel=60, kreuz="Holz")
            oeffnungen.append((M, o))
    z_traufe = sockel + h
    dach = bs.satteldach(netz, st, x0, x1, y0, y1, z_traufe, neigung, traufe=traufe, ortgang=0.3, enden="Holz", **deckung)
    hoch = T / 2 * dach["tan"]
    for x, seite in ((x0, "links"), (x1, "rechts")):
        M = bs.wandmatrix((x, cy, z_traufe), seite)
        if wand == "fachwerk":
            bs.fachwerkgiebel(netz, st, M, T, hoch, oeffnungen if giebel_fenster else [])
        elif wand == "stein":
            bs.steingiebel(netz, st, M, T, hoch, oeffnungen, mit_fenster=giebel_fenster)
        else:
            bs.platte(netz, M, [(-T / 2, 0), (T / 2, 0), (0, hoch)], 0, 0.3, m_wand)
            if wand in ("bretter", "block"):
                senkrechte_fugen(netz, st, M, T, 0, hoch, schraeg=hoch / (T / 2))
            if giebel_fenster and hoch > 1.4:
                o = Oeffnung("fenster", 0.0, 0.35, 0.5, 0.55, laeden=False)
                bs.fenster(netz, M, o, st, kreuz=None)
                oeffnungen.append((M, o))
    return {"dach": dach, "oeffnungen": oeffnungen, "tuer": Vector((cx + (tuer_u or 0.0), y0 - 0.3, 0.0)),
            "z_traufe": z_traufe, "hoch": hoch}


def wandbild(netz, st, M, L, H, oeffnungen, wand, saat=0, ecken=True):
    """Oberfläche einer Hüttenwand: Fugen, Fachwerk oder Eckbalken. `ecken`: Vorköpfe der Blockwand an dieser Wand
    (nur an zwei gegenüberliegenden Wänden, sonst lägen sie doppelt)."""
    if wand == "fachwerk":
        # etwas kürzer als die Wand: sonst liegen die Hirnenden von Schwelle und Rähm in der Nachbarwand
        bs.fachwerk(netz, M, L - 0.06, H, oeffnungen, st, schief=0.04, saat=saat)
    elif wand == "stein":
        bs.fugen(netz, M, L, 0, H, st)
    elif wand == "bretter":
        senkrechte_fugen(netz, st, M, L, 0, H)
        for u in (-L / 2 + 0.1, L / 2 - 0.1):                    # Eckbretter (nicht bündig mit der Nachbarwand)
            bs.balken(netz, M, u, 0, u, H, st["Holz"], vor=0.03, breite=0.14)
    elif wand == "block":
        v = 0.27
        while v < H - 0.1:                                       # Fugen zwischen den Stämmen
            netz.vieleck([M @ Vector(p) for p in ((-L / 2, -0.003, v - 0.025), (L / 2, -0.003, v - 0.025),
                                                   (L / 2, -0.003, v + 0.025), (-L / 2, -0.003, v + 0.025))], st["Fachwerk"])
            v += 0.3
        for s in ((-1, 1) if ecken else ()):                     # Vorköpfe an den Ecken
            k = 0
            v = 0.12 + (0.15 if s > 0 else 0)
            while v < H - 0.1:
                netz.quader(M @ Vector((s * (L / 2 + 0.12), -0.12, v)), (0.26, 0.26, 0.24), st["Scheitholz"])
                v += 0.6
                k += 1


def senkrechte_fugen(netz, st, M, L, v0, v1, abstand=0.32, schraeg=None):
    """Fugen zwischen senkrechten Brettern; `schraeg`: Giebel, die Fuge endet an der Dachschräge."""
    u = -L / 2 + abstand
    while u < L / 2 - 0.05:
        oben = v1 if schraeg is None else v1 - abs(u) * schraeg
        if oben - v0 > 0.15:
            netz.vieleck([M @ Vector(p) for p in ((u - 0.012, -0.003, v0), (u + 0.012, -0.003, v0),
                                                   (u + 0.012, -0.003, oben - 0.05), (u - 0.012, -0.003, oben - 0.05))],
                         st["Fachwerk"])
        u += abstand


def vernageln(netz, st, oeffnungen):
    """Leerstand: Bretter vor allen Türen und Fenstern."""
    for i, (M, o) in enumerate(oeffnungen):
        bs.bretter(netz, M, o, st, i)


def unkraut(netz, st, punkte, groesse=0.55, saat=0):
    zufall = random.Random(saat)
    for x, y in punkte:
        bs.buschel(netz, st, Vector((x, y, 0)), groesse=groesse * zufall.uniform(0.75, 1.2),
                   stoff="Gras" if zufall.random() < 0.6 else "Moos")


# ─────────────────────────────────────────────────────────────────────────────
#  Natur: Bäume, Felsen, Pflanzen
# ─────────────────────────────────────────────────────────────────────────────

def saeule(netz, st, boden, r, z0, z1, stoff, enden=None, n=8):
    """Stehendes n-Eck (Stamm, Stumpf, Pfahl) mit eigenem Material für Deckel."""
    profil = [(r * math.cos(math.radians(180 / n + 360 / n * k)), r * math.sin(math.radians(180 / n + 360 / n * k)))
              for k in range(n)]
    dreh = Matrix.Translation(Vector(boden)) @ Matrix.Rotation(math.radians(-90), 4, "Y")
    netz.prisma(profil, z0, z1, st[stoff], matrix=dreh, m_enden=st[enden] if enden else None)


def kegel(netz, mitte, r_unten, z0, z1, stoff_a, stoff_b=None, n=7, dreh=0.0):
    """Kegel aus n Dreiecken (ohne Boden – den sieht die Kamera nicht)."""
    spitze = Vector((mitte[0], mitte[1], z1))
    ring = [Vector((mitte[0] + r_unten * math.cos(dreh + 2 * math.pi * k / n),
                    mitte[1] + r_unten * math.sin(dreh + 2 * math.pi * k / n), z0)) for k in range(n)]
    for k in range(n):
        a, b = ring[k], ring[(k + 1) % n]
        netz.vieleck_nach([a, b, spitze], (a + b) / 2 - Vector((mitte[0], mitte[1], z0)) + Vector((0, 0, 0.3)),
                          stoff_b if (stoff_b is not None and k % 2) else stoff_a)


def tanne(netz, st, fuss, hoehe=8.0, r=2.0, saat=0):
    """Nadelbaum: kurzer Stamm, drei gestaffelte Kegel (wie bisher im Spiel, aber gestuft)."""
    zufall = random.Random(saat)
    x, y = fuss[0], fuss[1]
    saeule(netz, st, (x, y, 0), 0.2, 0.0, hoehe * 0.22, "Rinde", n=6)
    for i, (f_r, f_z0, f_z1) in enumerate(((1.0, 0.16, 0.62), (0.78, 0.38, 0.84), (0.52, 0.6, 1.0))):
        kegel(netz, (x, y), r * f_r * zufall.uniform(0.92, 1.08), hoehe * f_z0, hoehe * f_z1,
              st["Tanne"], st["Tanne hell"], n=7, dreh=zufall.uniform(0, 1) + i * 0.4)


def felsbrocken(netz, st, mitte, r, h, saat=0, n=7, stoff="Fels", stoff2="Fels dunkel",
                profil=((1.0, 0.0), (0.88, 0.5), (0.45, 1.0))):
    """Unregelmäßiger Felsbrocken: drei Ringe aus n Ecken (`profil`: Anteil von r und h je Ring),
    Flächen abwechselnd hell und dunkel."""
    zufall = random.Random(saat)
    x, y = mitte[0], mitte[1]
    z0 = mitte[2] if len(mitte) > 2 else 0.0
    ringe = []
    for f_r, f_z in profil:
        ringe.append([Vector((x + r * f_r * zufall.uniform(0.8, 1.15) * math.cos(2 * math.pi * (k + zufall.uniform(-0.2, 0.2)) / n),
                              y + r * f_r * zufall.uniform(0.8, 1.15) * math.sin(2 * math.pi * (k + zufall.uniform(-0.2, 0.2)) / n),
                              z0 + h * f_z * (zufall.uniform(0.85, 1.1) if f_z else 1) - (0.05 if not f_z else 0)))
                      for k in range(n)])
    innen = Vector((x, y, z0 + h * 0.4))
    for i in range(2):
        for k in range(n):
            a, b = ringe[i][k], ringe[i][(k + 1) % n]
            c, d = ringe[i + 1][(k + 1) % n], ringe[i + 1][k]
            netz.vieleck_nach([a, b, c, d], (a + b + c + d) / 4 - innen, st[stoff if (k + i) % 2 else stoff2])
    netz.vieleck_nach(ringe[2], (0, 0, 1), st[stoff])


def busch(netz, st, mitte, r, h, stoff="Laub", stoff2="Laub hell", n=6, saat=0):
    """Niedriger Busch oder Pflanze: Pyramide aus n Flächen über einem Ring knapp über dem Boden."""
    zufall = random.Random(saat)
    kegel(netz, mitte, r * zufall.uniform(0.85, 1.1), 0.02, h * zufall.uniform(0.85, 1.1),
          st[stoff], st[stoff2] if stoff2 else None, n=n, dreh=zufall.uniform(0, 3))


def stamm(netz, st, a, b, r, n=7):
    """Liegender Stamm von a nach b: Rinde, die Schnittflächen hell."""
    a, b = Vector(a), Vector(b)
    t = Matrix.Translation(a) @ bs._richtung(b - a, Vector((0, 0, 1)).cross(b - a).normalized())
    profil = [(r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n)) for k in range(n)]
    netz.prisma(profil, 0, (b - a).length, st["Rinde"], matrix=t, m_enden=st["Scheitholz"])


def stumpf(netz, st, fuss, r=0.4, h=0.55):
    saeule(netz, st, (fuss[0], fuss[1], 0), r, 0.0, h, "Rinde", enden="Scheitholz", n=7)


def polter(netz, st, mitte, laenge, reihen=4, r=0.3, quer=False):
    """Holzpolter: Stämme in Lagen (unten `reihen`, jede Lage einer weniger) zwischen zwei Pfählen."""
    x, y = mitte[0], mitte[1]
    for lage in range(reihen):
        for k in range(reihen - lage):
            versatz = (k - (reihen - lage - 1) / 2) * 2 * r * 1.02
            z = r + lage * r * 1.72
            a = 0.045 * ((k * 3 + lage * 5) % 7)          # Enden gestaffelt: keine zwei Schnittflächen in einer Ebene
            b = 0.05 * ((k * 2 + lage * 3) % 5)
            if quer:
                stamm(netz, st, (x + versatz, y - laenge / 2 + a, z), (x + versatz, y + laenge / 2 - b, z), r)
            else:
                stamm(netz, st, (x - laenge / 2 + a, y + versatz, z), (x + laenge / 2 - b, y + versatz, z), r)
    breite = reihen * r * 2.05
    for s in (-1, 1):
        p = (x + s * (breite / 2 + 0.1), y - laenge / 3, 0) if quer else (x - laenge / 3, y + s * (breite / 2 + 0.1), 0)
        saeule(netz, st, p, 0.08, 0, reihen * r * 1.6, "Holz", n=5)


def beet(netz, st, x0, y0, x1, y1, z=0.14, stoff="Erde"):
    """Erhöhtes Beet als flacher Quader."""
    netz.quader(((x0 + x1) / 2, (y0 + y1) / 2, z / 2), (x1 - x0, y1 - y0, z), st[stoff], ohne=("-z",))


def pflanzreihe(netz, st, a, b, abstand, r, h, stoff, stoff2=None, z=0.14, saat=0, n=5):
    """Pflanzen in einer Reihe von a nach b auf Höhe z (Beetoberkante)."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    anzahl = max(1, round((b - a).length / abstand))
    zufall = random.Random(saat)
    for k in range(anzahl + 1):
        p = a.lerp(b, k / anzahl) + Vector((zufall.uniform(-0.05, 0.05), zufall.uniform(-0.05, 0.05), 0))
        kegel(netz, (p.x, p.y), r * zufall.uniform(0.85, 1.15), z, z + h * zufall.uniform(0.85, 1.15),
              st[stoff], st[stoff2] if stoff2 else None, n=n, dreh=zufall.uniform(0, 3))


# ─────────────────────────────────────────────────────────────────────────────
#  Kleinteile
# ─────────────────────────────────────────────────────────────────────────────

def kiste(netz, st, mitte, masse, stoff="Holz", dreh=0.0):
    t = Matrix.Translation(Vector(mitte) + Vector((0, 0, masse[2] / 2))) @ Matrix.Rotation(dreh, 4, "Z")
    netz.quader((0, 0, 0), masse, st[stoff], t, ohne=("-z",))


def haufen(netz, st, mitte, r, h, stoff, stoff2=None, saat=0, n=8):
    """Flacher Haufen (Erz, Kohle, Splitt, Abraum)."""
    felsbrocken(netz, st, mitte, r, h, saat=saat, n=n, stoff=stoff, stoff2=stoff2 or stoff,
                profil=((1.0, 0.0), (0.62, 0.6), (0.22, 1.0)))


def werkzeug(netz, st, fuss, laenge=1.1, winkel=20, art="hacke"):
    """Werkzeug an die Wand gelehnt oder im Klotz: Stiel und Kopf (Hacke, Axt, Schaufel)."""
    w = math.radians(winkel)
    a = Vector(fuss)
    b = a + Vector((math.sin(w) * laenge * 0.3, 0, math.cos(w) * laenge))
    netz.stab(a, b, 0.05, 0.05, st["Holz"])
    if art == "hacke":
        netz.stab(b + Vector((-0.28, 0, -0.02)), b + Vector((0.28, 0, 0.04)), 0.06, 0.07, st["Eisen"])
    elif art == "axt":
        netz.quader(b + Vector((0.1, 0, -0.08)), (0.22, 0.04, 0.16), st["Eisen"])
    else:
        netz.quader(a + Vector((0, 0, 0.18)), (0.24, 0.04, 0.3), st["Eisen"])


def schubkarre(netz, st, mitte, dreh=0.0, ladung=None):
    t = Matrix.Translation(Vector(mitte)) @ Matrix.Rotation(dreh, 4, "Z")
    netz.quader((0, 0, 0.5), (0.9, 0.6, 0.3), st["Holz"], t)
    for s in (-1, 1):
        netz.stab(t @ Vector((-0.4, s * 0.22, 0.45)), t @ Vector((-1.1, s * 0.25, 0.55)), 0.05, 0.05, st["Holz"])
        netz.stab(t @ Vector((-0.4, s * 0.22, 0.35)), t @ Vector((-0.4, s * 0.22, 0.0)), 0.05, 0.05, st["Holz"])
    rad = Matrix.Translation(t @ Vector((0.55, 0, 0.25))) @ Matrix.Rotation(dreh, 4, "Z") @ Matrix.Rotation(math.radians(90), 4, "X")
    profil = [(0.25 * math.cos(2 * math.pi * k / 8), 0.25 * math.sin(2 * math.pi * k / 8)) for k in range(8)]
    netz.prisma(profil, -0.04, 0.04, st["Holz alt"], matrix=rad @ Matrix.Rotation(math.radians(90), 4, "Y"))
    if ladung:
        netz.quader(t @ Vector((0, 0, 0.7)), (0.75, 0.48, 0.12), st[ladung])


def bienenkorb(netz, st, fuss, r=0.42, h=0.75):
    """Strohkorb: zwei Ringe und eine runde Kappe, dazu das Flugloch."""
    x, y, z = fuss
    saeule(netz, st, (x, y, z), r, 0, h * 0.45, "Stroh", n=8)
    kegel(netz, (x, y), r, z + h * 0.45, z + h, st["Stroh"], st["Strohkante"], n=8)
    netz.quader((x, y - r + 0.01, z + 0.1), (0.14, 0.04, 0.08), st["Loch"])


# ─────────────────────────────────────────────────────────────────────────────
#  Bericht und Bilder
# ─────────────────────────────────────────────────────────────────────────────

def bericht(name, objekte):
    """Dreiecke je Knoten, dann Flimmern getrennt für „in Betrieb“ und „Leerstand“."""
    print(f"\n{name}: Dreiecke")
    summe = 0
    for o in objekte:
        if o.type != "MESH":
            print(f"  {o.name:10s} Anker bei ({o.location.x:.2f}, {o.location.y:.2f}, {o.location.z:.2f})")
            continue
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        summe += n
        print(f"  {o.name:10s} {n:6d}")
    print(f"  {'gesamt':10s} {summe:6d}\n")
    netze = {o.name: o for o in objekte if o.type == "MESH"}
    funde = []
    for teil in (("Haus", "Arbeit", "Fahne"), ("Haus", "Leerstand")):
        funde += bs.deckungsgleich([netze[n] for n in teil if n in netze])
    if funde:
        print(f"  WARNUNG: {len(funde)} deckungsgleiche Flächen (flimmern im Spiel), größte:")
        for ort, a, b, fl in sorted(funde, key=lambda f: -f[3])[:10]:
            print(f"    bei ({ort.x:.2f}, {ort.y:.2f}, {ort.z:.2f})  {a} / {b}  {fl:.2f} m²")
    else:
        print("  keine deckungsgleichen Flächen")


def knoten(haus, arbeit, fahne, leer, anker):
    """Netze zu Objekten; Leerstand ist ein Zustand (aus)."""
    objekte = [haus.objekt(), arbeit.objekt(), fahne.objekt(), bs.zustand(leer.objekt())] + anker
    return objekte


def ausgeben(name, flaeche, objekte, ziel_hoehe=2.5, ausschnitt=26.0):
    """Export und Vorschaubilder: In Betrieb und Leerstand nebeneinander, rundum, Anker, Nacht."""
    bericht(name, objekte)
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(name, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    bs.vorschau_buehne(flaeche, ziel_hoehe=ziel_hoehe, ausschnitt=ausschnitt, schnell=schnell)

    def betrieb(o):
        return o.name != "Leerstand"

    def leer(o):
        return o.name not in ("Arbeit", "Fahne")

    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(name, betrieb))
        return
    a = bs.rendern(name + "-betrieb", betrieb)
    glut = bs.stoff("Glut").node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
    glut.default_value = 0.0                     # verlassen: kein Feuer
    b = bs.rendern(name + "-leerstand", leer)
    glut.default_value = bs.LEUCHTEN["Glut"][1]
    print(bs.nebeneinander(name, [a, b], ["In Betrieb", "Leerstand"]))
    if not schnell:
        print(bs.rundum(name, betrieb))
        print(bs.anker_bild(name, betrieb))
        print(bs.nachtbild(name, betrieb))
        for p in (a, b):
            os.remove(p)
