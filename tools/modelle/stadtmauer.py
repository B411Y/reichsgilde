"""Stadtmauer – Baukasten aus Mauerstück, Mauerturm, Eckturm und Torburg (Sonderbau).

    blender -b --python tools/modelle/stadtmauer.py        oder
    python tools/modelle/stadtmauer.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/stadtmauer.glb und Bilder nach docs/modelle/.

Objekte im GLB – jedes Bauteil liegt mit seinem eigenen Ursprung im Nullpunkt, das Spiel setzt Kopien entlang
der Mauerlinien der Karte (gerade Strecken, Ecken und Tore; auch für gebogene Mauern wie in Köln):
    Mauerstueck  4 m lang, 6 m hoch, 2 m dick, Wehrgang mit Zinnen und Schießscharten. Kachelbar: Stücke stoßen
                 ohne Stirnflächen aneinander; in Längsrichtung darf das Spiel es auf 3–5 m strecken.
    Mauerturm    runder Schalenturm (Radius 2,8 m, 9,5 m hoch, Kegeldach), sitzt auf der Mauerlinie und steht
                 zur Feldseite vor – alle 30–40 m einer
    Eckturm      runder Turm für Ecken und Knicke (Radius 3,6 m, 11 m hoch, Kegeldach)
    Tor          Torburg: zwei Rundtürme und ein Torbau mit Durchfahrt (3,6 m breit), Fallgitter, Stadtwappen;
                 16 m entlang der Mauer, die Mauerstücke schließen bei x = ±7,6 an (sie stecken etwas in den Türmen)
    Anker_Tuer   in der Durchfahrt des Tors
Lokale Richtung: X entlang der Mauer, −Y ist die Feldseite (im Spiel +Z), +Y die Stadtseite. Keine Zustände und
keine Hausfarbe (`pruefen.py … --sonderbau`).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402

NAME = "stadtmauer"
FLAECHE = (18.0, 10.0)

# ── Maße (Meter) wie bisher im Spiel ─────────────────────────────────────────
STUECK = 4.0              # Länge eines Mauerstücks
H_MAUER = 6.0             # Wehrgang
DICKE = 2.0
ZINNE = 1.0               # Breite einer Zinne und einer Lücke
TOR_HALB = 7.6            # die Mauerstücke schließen bei x = ±7,6 an die Tortürme an


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    teile = []
    for name, bau in (("Mauerstueck", mauerstueck), ("Mauerturm", mauerturm), ("Eckturm", eckturm), ("Tor", tor)):
        netz = bs.Netz(name)
        bau(netz, st)
        teile.append(netz.objekt())
    teile.append(bs.anker("Anker_Tuer", (0.0, -3.0, 0.0)))
    return teile


# ─────────────────────────────────────────────────────────────────────────────
#  Teile
# ─────────────────────────────────────────────────────────────────────────────

def zinnen(netz, st, x0, x1, y_aussen, z, dicke=0.5, hoehe_bruestung=0.6, hoehe_zinne=1.0, phase=0.0):
    """Brustwehr mit Zinnen auf der Feldseite zwischen x0 und x1; jede Zinne mit einer Schießscharte.
    Zinnen sitzen bei phase + 2k (Breite 1 m), damit aneinandergesetzte Stücke im Takt bleiben."""
    ym = y_aussen + dicke / 2
    netz.quader(((x0 + x1) / 2, ym, z + hoehe_bruestung / 2), (x1 - x0, dicke, hoehe_bruestung), st["Haustein"],
                ohne=("+x", "-x", "-z"))
    x = phase - math.floor((phase - x0) / (2 * ZINNE)) * 2 * ZINNE
    while x + ZINNE / 2 <= x1 + 1e-6:
        if x - ZINNE / 2 >= x0 - 1e-6:
            netz.quader((x, ym, z + hoehe_bruestung + hoehe_zinne / 2), (ZINNE, dicke, hoehe_zinne), st["Haustein"],
                        ohne=("-z",))
            netz.vieleck_nach([(x - 0.06, y_aussen - 0.003, z + hoehe_bruestung + 0.2), (x + 0.06, y_aussen - 0.003, z + hoehe_bruestung + 0.2),
                               (x + 0.06, y_aussen - 0.003, z + hoehe_bruestung + 0.85), (x - 0.06, y_aussen - 0.003, z + hoehe_bruestung + 0.85)],
                              (0, -1, 0), st["Loch"])
        x += 2 * ZINNE


def mauerstueck(netz, st):
    """Gerades Stück: Mauerkörper, Sockel zur Feldseite, Wehrgang mit Zinnen."""
    h = STUECK / 2
    netz.quader((0, 0, H_MAUER / 2), (STUECK, DICKE, H_MAUER), st["Haustein"], ohne=("+x", "-x", "-z"))
    netz.quader((0, -DICKE / 2 - 0.08, 0.4), (STUECK, 0.16, 0.8), st["Bruchstein dunkel"], ohne=("+x", "-x", "+y"))
    zinnen(netz, st, -h, h, -DICKE / 2, H_MAUER, phase=-1.5)
    # Holzgeländer zur Stadtseite
    for x in (-h + 0.5, h - 1.5):
        netz.stab((x, DICKE / 2 - 0.08, H_MAUER), (x, DICKE / 2 - 0.08, H_MAUER + 1.0), 0.08, 0.08, st["Holz alt"])
    netz.stab((-h, DICKE / 2 - 0.08, H_MAUER + 0.95), (h, DICKE / 2 - 0.08, H_MAUER + 0.95), 0.07, 0.07, st["Holz alt"])


def rundturm(netz, st, r, hoehe, dach_hoehe, scharten=(2.5, 5.5), n=12, nur_aussen=False, mitte=(0, 0)):
    """Runder Turm: Sockel, Schaft, zwei Gesimse, Schießscharten ringsum (oder nur zur Feldseite), Kegeldach."""
    m = Vector((mitte[0], mitte[1], 0))
    bs.achteck(netz, st, m, r + 0.15, 0.0, 0.9, "Bruchstein dunkel", n=n)
    bs.achteck(netz, st, m, r, 0.9, hoehe, "Haustein", n=n)
    for z in (H_MAUER + 0.02, hoehe - 0.3):
        bs.achteck(netz, st, m, r + 0.12, z, z + 0.25, "Sandstein", n=n)
    apo = r * math.cos(math.pi / n)
    for k in range(n):
        w = math.radians(180 / n + 360 / n * k) + math.pi / n          # Mitte der Seite k
        rich = Vector((math.cos(w), math.sin(w), 0))
        if nur_aussen and rich.y > -0.2:
            continue
        for z in scharten:
            if k % 2:
                continue
            p = m + rich * (apo + 0.004)
            quer = Vector((-rich.y, rich.x, 0)) * 0.07
            netz.vieleck_nach([p - quer + Vector((0, 0, z)), p + quer + Vector((0, 0, z)),
                               p + quer + Vector((0, 0, z + 0.8)), p - quer + Vector((0, 0, z + 0.8))], rich, st["Loch"])
    bs.turmhelm(netz, st, m, r + 0.35, hoehe, dach_hoehe, stoff="Schiefer", n=n)


def mauerturm(netz, st):
    """Schalenturm auf der Mauerlinie, nach außen vorstehend."""
    rundturm(netz, st, 2.8, 9.5, 4.5, scharten=(2.2, 4.6, 7.4), nur_aussen=True)


def eckturm(netz, st):
    rundturm(netz, st, 3.6, 11.0, 6.0, scharten=(2.2, 4.8, 8.2))


def tor(netz, st):
    """Torburg: Torbau mit Durchfahrt zwischen zwei Rundtürmen, Fallgitter, offene Torflügel, Stadtwappen,
    Satteldach mit dem First entlang der Mauer."""
    BX, BY, BH = 3.0, 2.6, 9.0             # Torbau x ±3, y ±2,6, Mauerwerk bis 9 m
    DX, DH = 1.8, 3.4                      # Durchfahrt x ±1,8, Kämpfer bei 3,4 m (Scheitel 5,2 m)
    stein = st["Haustein"]
    # Türme links und rechts, etwas zur Feldseite gerückt
    for sx in (-1, 1):
        rundturm(netz, st, 2.6, 11.0, 5.0, scharten=(2.4, 5.0, 8.0), mitte=(sx * 5.4, -0.4))
    # Seitenteile des Torbaus neben der Durchfahrt, Decke über der Durchfahrt
    for sx in (-1, 1):
        xm = sx * (BX + DX) / 2
        netz.quader((xm, 0, BH / 2), (BX - DX, 2 * BY, BH), stein, ohne=("-z",))
    oben = DH + DX + 0.3                   # Unterkante des Mauerwerks über dem Bogen
    netz.quader((0, 0, (oben + BH) / 2), (2 * DX, 2 * BY, BH - oben), stein, ohne=("-z",))
    # Bogen auf beiden Seiten: Zwickel über dem Rundbogen, Bogenring
    teile = 10
    bogen = [(DX * math.cos(math.pi * i / teile), DH + DX * math.sin(math.pi * i / teile)) for i in range(teile + 1)]
    zwickel = [(DX, DH), (DX, oben), (-DX, oben), (-DX, DH)] + list(reversed(bogen[1:-1]))
    bs.platte(netz, bs.wandmatrix((0, -BY, 0), "vorne"), zwickel, 0.0, 2 * BY, stein)       # Mauerwerk über dem Bogen
    for seite, y in (("vorne", -BY), ("hinten", BY)):
        M = bs.wandmatrix((0, y, 0), seite)
        aussen = [((DX + 0.25) * math.cos(math.pi * i / teile), DH + (DX + 0.25) * math.sin(math.pi * i / teile)) for i in range(teile + 1)]
        bs.platte(netz, M, aussen + list(reversed(bogen)), -0.08, -0.002, st["Sandstein"])
    # Durchfahrt: das Gewölbe bildet die Unterseite des Mauerwerks über dem Bogen; Boden aus Pflaster
    netz.quader((0, 0, 0.03), (2 * DX, 2 * BY, 0.06), st["Bruchstein dunkel"], ohne=("-z",))
    # Fallgitter halb hochgezogen, offene Torflügel innen
    for k in range(-3, 4):
        x = k * DX / 3.5
        netz.quader((x, -BY + 0.5, DH + 0.9), (0.08, 0.08, 2.2), st["Eisen"])
    for z in (DH + 0.2, DH + 1.0, DH + 1.8):
        netz.quader((0, -BY + 0.5, z), (2 * DX - 0.1, 0.08, 0.08), st["Eisen"])
    for sx in (-1, 1):
        t = Matrix.Translation((sx * DX, BY - 0.6, 0)) @ Matrix.Rotation(math.radians(-sx * 80), 4, "Z")
        netz.quader((-sx * DX / 2, 0, DH / 2 + 0.3), (DX - 0.05, 0.12, DH + 0.6), st["Holz alt"], t)
    # Gesims, Zinnen auf der Feldseite unter dem Dach, Satteldach
    bs.gesims(netz, st, -BX, BX, -BY, BY, BH - 0.32, hoehe=0.25, vor=0.12)
    dach = bs.satteldach(netz, st, -BX, BX, -BY, BY, BH, 45, traufe=0.4, ortgang=0.2, enden="Haustein", **bs.SCHIEFER)
    for x, seite in ((BX, "rechts"), (-BX, "links")):
        bs.platte(netz, bs.wandmatrix((x, 0, BH), seite), [(-BY, 0), (BY, 0), (0, BY * dach["tan"])], 0, 0.4, stein)
    # Stadtwappen über der Durchfahrt auf der Feldseite
    M = bs.wandmatrix((0, -BY, 0), "vorne")
    g = 1.1
    bs.platte(netz, M, [(x * 1.16 * g, 7.0 + y * 1.12 * g) for x, y in bs.SCHILD], -0.085, -0.05, st["Sandstein"])
    bs.platte(netz, M, [(x * g, 7.0 + y * g) for x, y in bs.SCHILD], -0.11, -0.08, st["Stadtrot"])
    netz.quader((0, -0.12, 7.0), (0.12 * g, 0.02, 0.7 * g), st["Stadtweiss"], M)
    netz.quader((0, -0.12, 7.08 * 1.0), (0.6 * g, 0.02, 0.12 * g), st["Stadtweiss"], M)


# ─────────────────────────────────────────────────────────────────────────────
#  Vorschau: Teile nebeneinander und ein Probeaufbau mit Ecke und Tor
# ─────────────────────────────────────────────────────────────────────────────

def kopie(obj, ort, drehung=0.0):
    import bpy
    k = obj.copy()
    k.location = Vector(ort)
    k.rotation_euler = (0, 0, math.radians(drehung))
    bpy.context.scene.collection.objects.link(k)
    return k


def probeaufbau(teile):
    """Ein Stück Stadtmauer: Tor, Mauerstücke, ein Mauerturm, der Eckturm und die Mauer um die Ecke."""
    t = {o.name: o for o in teile}
    neu = [kopie(t["Tor"], (0, 0, 0))]
    x = -TOR_HALB - STUECK / 2
    for i in range(7):
        neu.append(kopie(t["Mauerstueck"], (x, 0, 0)))
        if i == 3:
            neu.append(kopie(t["Mauerturm"], (x, -1.0, 0)))
        x -= STUECK
    neu.append(kopie(t["Eckturm"], (x + STUECK / 2, 0, 0)))
    ecke = x + STUECK / 2
    for i in range(4):
        neu.append(kopie(t["Mauerstueck"], (ecke, 2.0 + STUECK * i + STUECK / 2 - 0.0, 0), 90 + 180))
    for i in range(3):
        neu.append(kopie(t["Mauerstueck"], (TOR_HALB + STUECK / 2 + STUECK * i, 0, 0)))
    mitte = sum((k.location for k in neu), Vector()) / len(neu)          # Aufbau in die Bildmitte rücken
    for k in neu:
        k.location -= Vector((mitte.x, mitte.y, 0))
    return neu


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte, einzeln=True)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv
    teile = [o for o in objekte if o.type == "MESH"]
    # Teile nebeneinander (nur fürs Bild verschoben, die GLB ist schon geschrieben)
    for o, x in zip(teile, (-15.0, -7.5, 1.5, 13.0)):
        o.location = Vector((x * 0.7071, x * 0.7071, 0))
    bs.vorschau_buehne(None, ziel_hoehe=6.0, ausschnitt=40.0, schnell=schnell)
    nur_teile = lambda o: o in teile or o.name.startswith("_")                         # noqa: E731
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, nur_teile))
        return
    print(bs.rendern(NAME, nur_teile))
    for o in teile:
        o.location = Vector((0, 0, 0))
    print(bs.anker_bild(NAME, lambda o: o.name == "Tor" or o.name.startswith("_")))
    if "--nur-anker" in sys.argv:
        return
    aufbau = probeaufbau(teile)
    for o in teile:
        o.location = Vector((0, 0, -200))           # Originale aus dem Bild
    import bpy
    bpy.context.scene.camera.data.ortho_scale = 62.0
    print(bs.rendern(NAME + "-aufbau", lambda o: o in aufbau or o.name.startswith("_")))
    print(bs.nachtbild(NAME, lambda o: o in aufbau or o.name.startswith("_")))


if __name__ == "__main__":
    main()
