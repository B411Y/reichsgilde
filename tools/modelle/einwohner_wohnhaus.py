"""Einfaches Wohnhaus der Einwohner – drei Varianten in einem Modell. Belegte Fläche 6 × 8 m.

    blender -b --python tools/modelle/einwohner_wohnhaus.py        oder
    python tools/modelle/einwohner_wohnhaus.py [--schnell] [--ohne-bilder] [--nur-anker] [--nur-nacht]

Schreibt game/assets/modelle/gebaeude/einwohner_wohnhaus.glb und Bilder nach docs/modelle/.

Objekte im GLB:
    Variante1   Fachwerk mit Lehmputz, Ziegeldach, Schleppgaube
    Variante2   glatt verputzt, Ziegeldach, Vordach über der Tür, Bank
    Variante3   dunkles Fachwerk mit Andreaskreuzen, Schieferdach, Holzstapel an der Seite
    Anker_Tuer  vor der Haustür (in allen Varianten an derselben Stelle)
Das Spiel zeigt genau eine Variante (nach der Hausnummer; wie heute im Code: meist Fachwerk, jedes dritte verputzt,
jedes vierte mit Schiefer). Leerstand und Gerüst gibt es nicht. Läden und Tür tragen „Hausfarbe“ (hier ein gedecktes
Rotbraun), das Spiel kann sie je Haus umfärben.

Aufbau wie bisher im Spiel (Fachwerk oder Putz, Traufe zur Gasse), Blender: Z oben, Gasse bei -Y:
    Haus 5 × 5,4 m (mit Dachüberstand im 6-m-Raster), Front bei y = -2 (2 m Vorplatz), Erdgeschoss und
    niedriges Obergeschoss, Satteldach mit der Traufe zur Gasse, Haustür rechts der Mitte.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Oeffnung, Vector  # noqa: E402

NAME = "einwohner_wohnhaus"
FLAECHE = (6.0, 8.0)

# Farben nur für dieses Modell (jedes Modell läuft in einem eigenen Prozess)
bs.FARBEN.update({
    "Hausfarbe": (0.42, 0.22, 0.16),       # Läden und Tür in gedecktem Rotbraun; das Spiel kann je Haus umfärben
})

# ── Maße (Meter) ─────────────────────────────────────────────────────────────
HX, VORN, HINTEN = 2.5, -2.0, 3.4
TIEF = HINTEN - VORN
Y_MITTE = (VORN + HINTEN) / 2
Z_SOCKEL = 0.3
GESCHOSSE = (2.6, 2.2)
H_WAND = sum(GESCHOSSE)
Z_OG = Z_SOCKEL + GESCHOSSE[0]
Z_TRAUFE = Z_SOCKEL + H_WAND               # 5,1
NEIGUNG = 48
TUER_U = 0.8

VARIANTEN = {
    "Variante1": dict(wand="fachwerk", putz="Lehmputz", dach=bs.ZIEGEL, kreuze=False, gaube=True, extra="tonne"),
    "Variante2": dict(wand="putz", putz="Putz", dach=bs.ZIEGEL, kreuze=False, gaube=False, extra="vordach"),
    "Variante3": dict(wand="fachwerk", putz="Putz", dach=bs.SCHIEFER, kreuze=True, gaube=False, extra="holz"),
}


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = []
    for name, v in VARIANTEN.items():
        netz = bs.Netz(name)
        haus(netz, st, v)
        objekte.append(netz.objekt())
    objekte.append(bs.anker("Anker_Tuer", (TUER_U, VORN - 0.25, 0.2)))       # auf der Stufe vor der Tür
    return objekte


def haus(netz, st, v):
    putz = st[v["putz"]]
    netz.quader((0, Y_MITTE, Z_SOCKEL / 2), (2 * HX + 0.1, TIEF + 0.1, Z_SOCKEL), st["Bruchstein"])
    netz.quader((0, Y_MITTE, Z_SOCKEL + H_WAND / 2), (2 * HX, TIEF, H_WAND), putz, ohne=("+z",))
    leer = []
    F, T = "fenster", "tuer"
    eg = dict(v=0.85, b=0.8, h=1.05)
    og = dict(v=0.55, b=0.75, h=0.95)
    waende = {
        "vorne": ((0, VORN), 2 * HX, [Oeffnung(F, -1.4, **eg), Oeffnung(T, TUER_U, 0.0, 1.0, 2.0)],
                  [Oeffnung(F, u, **og) for u in (-1.4, 0.8)]),
        "rechts": ((HX, Y_MITTE), TIEF, [Oeffnung(F, 1.2, laeden=False, **eg)], [Oeffnung(F, -1.2, laeden=False, **og)]),
        "hinten": ((0, HINTEN), 2 * HX, [Oeffnung(F, -1.2, **eg), Oeffnung(T, 1.3, 0.0, 0.9, 1.9)],
                   [Oeffnung(F, 0.0, **og)]),
        "links": ((-HX, Y_MITTE), TIEF, [Oeffnung(F, -1.0, laeden=False, **eg)], [Oeffnung(F, 1.0, laeden=False, **og)]),
    }
    for seite, ((x, y), L, unten, oben) in waende.items():
        for z, hoehe, liste, og_ja in ((Z_SOCKEL, GESCHOSSE[0], unten, False), (Z_OG, GESCHOSSE[1], oben, True)):
            M = bs.wandmatrix((x, y, z), seite)
            if v["wand"] == "fachwerk":
                bs.fachwerk(netz, M, L, hoehe, liste, st, kreuze=v["kreuze"] and og_ja, schief=0.04,
                            saat=len(seite) + int(z * 10))
            for o in liste:
                if o.art == T:
                    bs.tuer(netz, M, o, st)
                else:
                    bs.fenster(netz, M, o, st, winkel=55, kreuz="Holz")
                leer.append((M, o))
    if v["wand"] == "putz":                  # Geschossband aus Holz statt Fachwerk
        bs.gesims(netz, st, -HX, HX, VORN, HINTEN, Z_OG - 0.06, hoehe=0.12, vor=0.04, stoff="Holz")
    netz.quader((TUER_U, VORN - 0.25, 0.1), (1.3, 0.5, 0.2), st["Bruchstein"], ohne=("+y",))

    # Satteldach mit der Traufe zur Gasse, Giebel an den Seiten
    dach = bs.satteldach(netz, st, -HX, HX, VORN, HINTEN, Z_TRAUFE, NEIGUNG, traufe=0.35, ortgang=0.25,
                         enden="Holz", **v["dach"])
    hoch = TIEF / 2 * dach["tan"]
    for x, seite in ((HX, "rechts"), (-HX, "links")):
        M = bs.wandmatrix((x, Y_MITTE, Z_TRAUFE), seite)
        if v["wand"] == "fachwerk":
            bs.fachwerkgiebel(netz, st, M, TIEF, hoch, leer)
        else:
            bs.platte(netz, M, [(-TIEF / 2, 0), (TIEF / 2, 0), (0, hoch)], 0, 0.3, putz)
            o = Oeffnung(F, 0.0, 0.6, 0.55, 0.75)
            bs.fenster(netz, M, o, st, winkel=55, kreuz="Holz")
            leer.append((M, o))
    if v["gaube"]:
        bs.gaube(netz, st, dach, leer, x0=-1.9, x1=-0.7, y_front=VORN + 1.1, hoehe=1.1)
    xs, ys = -0.9, Y_MITTE + 1.0             # Schornstein auf der hinteren Dachfläche
    z_dach = dach["oben_hinten"](ys) - 0.3
    hoehe = dach["z_first"] + 0.6 - z_dach
    netz.quader((xs, ys, z_dach + hoehe / 2), (0.55, 0.55, hoehe), st["Bruchstein dunkel"])
    netz.quader((xs, ys, z_dach + hoehe + 0.05), (0.7, 0.7, 0.1), st["Bruchstein"])

    if v["extra"] == "vordach":
        profil = [(VORN + 0.01, 2.75), (VORN - 0.75, 2.45), (VORN - 0.75, 2.52), (VORN + 0.01, 2.82)]
        netz.prisma(profil, TUER_U - 0.8, TUER_U + 0.8, st[v["dach"]["deckung"]], m_enden=st["Holz"])
        for s in (-1, 1):
            netz.stab((TUER_U + s * 0.7, VORN, 2.1), (TUER_U + s * 0.7, VORN - 0.6, 2.5), 0.07, 0.07, st["Holz"])
        holz = st["Holz"]
        netz.quader((-1.4, VORN - 0.3, 0.44), (1.3, 0.34, 0.07), holz)
        for x in (-1.95, -0.85):
            netz.quader((x, VORN - 0.3, 0.2), (0.08, 0.28, 0.4), holz)
    elif v["extra"] == "holz":
        for k in range(4):
            netz.quader((-HX - 0.2, Y_MITTE + 0.4, 0.13 + 0.25 * k), (0.4, 2.6 - 0.2 * k, 0.24), st["Holz alt"], ohne=("+x",))
        netz.vieleck_nach([(-HX - 0.401, Y_MITTE + 0.4 + d, 0.02 + z) for d, z in ((-1.25, 0), (1.25, 0), (1.15, 0.97), (-1.15, 0.97))],
                          (-1, 0, 0), st["Scheitholz"])
    else:
        bs.fass(netz, st, Vector((HX + 0.22, HINTEN - 0.8, 0)), r=0.2, h=0.8)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte)
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    schnell = "--schnell" in sys.argv

    def nur(name):
        return lambda o: not o.name.startswith("Variante") or o.name == name

    bs.vorschau_buehne(FLAECHE, ziel_hoehe=4.0, ausschnitt=15.0, schnell=schnell)
    if "--nur-nacht" in sys.argv:
        print(bs.nachtbild(NAME, nur("Variante1")))
        return
    if "--nur-anker" in sys.argv:
        print(bs.anker_bild(NAME, nur("Variante1")))
        return
    bilder = [bs.rendern(f"{NAME}-{k}", nur(f"Variante{k}")) for k in (1, 2, 3)]
    print(bs.nebeneinander(NAME, bilder, [f"Variante {k}" for k in (1, 2, 3)]))
    if not schnell:
        for k in (1, 2, 3):
            print(bs.rundum(f"{NAME}-{k}", nur(f"Variante{k}")))
    print(bs.anker_bild(NAME, nur("Variante1")))
    print(bs.nachtbild(NAME, nur("Variante1")))


if __name__ == "__main__":
    main()
