"""Nachen (Kleinboot der Rheinschiffer und Fischer, flach, vorn und hinten hochgezogen) mit Stakstange.

    blender -b --python tools/modelle/nachen.py -- [--schnell] [--ohne-bilder]

Schreibt game/assets/modelle/wasser/wasser_nachen.glb und das Vorschaubild docs/modelle/wasser_nachen.png.

Nachen: 5 m × 1,3 m (Maße plausibel, nicht belegt), flacher Boden, drei überlappende Plankengänge je Seite (Klinker), Dollbord, zwei Duchten,
beide Enden spitz und hochgezogen. Ein Schiffer steht achtern und staket (die Figur setzt das Spiel an Anker_Schiffer).
Bug bei +X, Ursprung auf der Wasserlinie in der Mitte.

    Nachen          Rumpf mit Duchten und Leinenring am Bug, Boden unten geschlossen.
    Verdraenger     unsichtbarer Körper in Rumpfform: Das Spiel gibt ihm einen Stoff, der nur Tiefe schreibt, damit die Wasserfläche im Boot ausgespart bleibt
                    (der Boden liegt unter der Wasserlinie; ohne Verdränger stünde der Schiffer im Wasser).
    Stange          Stakstange im Gebrauch: schräg vom Schiffer zum Grund (nur beim Staken sichtbar).
    Stange_Liegend  dieselbe Stange längs im Boot (festgemacht).
    Anker_Bug, Anker_Heck, Anker_Schiffer (achtern, Blick +X).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import oberlaender as ob  # noqa: E402  (Verdränger)
import wasser as ws  # noqa: E402
from bausteine import Vector  # noqa: E402

LAENGE, BREITE = 5.0, 1.3
HALB = LAENGE / 2
BORD = 0.40
TIEF = 0.12
GAENGE = 3
DICKE = 0.03


def profil(x):
    """(halbe Breite am Dollbord, halbe Breite am Boden, Höhe Boden, Höhe Dollbord) bei x."""
    t = min(1.0, abs(x) / HALB)
    w = 0.10 + (BREITE / 2 - 0.10) * (1 - t ** 2.4)
    b = w * 0.8
    zb = -TIEF + 0.30 * t ** 2.6
    g = BORD + 0.22 * t ** 2.2
    return w, b, zb, g


def seite(x, u):
    w, b, zb, g = profil(x)
    return b + (w - b) * u, zb + (g - zb) * u


def rumpf(st):
    netz = bs.Netz("Nachen")
    xs = [-HALB + 0.25 * i for i in range(int(LAENGE / 0.25) + 1)]
    mitte = Vector((0, 0, 0.1))
    for xa, xb in zip(xs, xs[1:]):
        for s in (-1, 1):
            for k in range(GAENGE):
                u0, u1 = k / GAENGE, (k + 1) / GAENGE
                a0, b0, a1, b1 = seite(xa, u0), seite(xb, u0), seite(xa, u1), seite(xb, u1)
                p = [Vector((xa, s * (a0[0] + DICKE), a0[1])), Vector((xb, s * (b0[0] + DICKE), b0[1])),
                     Vector((xb, s * b1[0], b1[1])), Vector((xa, s * a1[0], a1[1]))]
                richtung = sum(p, Vector()) / 4 - mitte
                netz.vieleck_nach(p, richtung, st["Holz alt" if (k + xs.index(xa)) % 2 else "Teerholz" if k == 0 else "Holz"])
            # Innenseite
            wa, wb = profil(xa)[0], profil(xb)[0]
            za, zb_ = profil(xa)[2] + 0.04, profil(xb)[2] + 0.04
            ga, gb = profil(xa)[3], profil(xb)[3]
            netz.vieleck_nach([(xa, s * (wa * 0.8 - 0.03), za), (xb, s * (wb * 0.8 - 0.03), zb_), (xb, s * (wb - 0.05), gb), (xa, s * (wa - 0.05), ga)],
                              (0, -s, 0.3), st["Holz"])
            # Dollbord
            netz.vieleck_nach([(xa, s * (wa + 0.02), ga + 0.02), (xb, s * (wb + 0.02), gb + 0.02), (xb, s * (wb - 0.08), gb + 0.02), (xa, s * (wa - 0.08), ga + 0.02)],
                              (0, 0, 1), st["Holz alt"])
        # Unterseite geschlossen (Teer)
        netz.vieleck([(xa, profil(xa)[1], profil(xa)[2]), (xb, profil(xb)[1], profil(xb)[2]), (xb, -profil(xb)[1], profil(xb)[2]), (xa, -profil(xa)[1], profil(xa)[2])],
                     st["Teerholz"], immer=True)
        # Boden: Bretter längs
        za, zb_ = profil(xa)[2] + 0.04, profil(xb)[2] + 0.04
        fa, fb = profil(xa)[1] * 0.98, profil(xb)[1] * 0.98
        n = 4
        for k in range(n):
            t0, t1 = -1 + 2 * k / n + 0.02, -1 + 2 * (k + 1) / n - 0.02
            netz.vieleck_nach([(xa, fa * t0, za), (xb, fb * t0, zb_), (xb, fb * t1, zb_), (xa, fa * t1, za)], (0, 0, 1), st["Bretter"] if k % 2 else st["Holz"])
    # Stevenbalken an Bug und Heck, Leinenring am Bug
    for s in (-1, 1):
        w, b, zb, g = profil(s * HALB)
        netz.stab((s * (HALB - 0.05), 0, zb), (s * (HALB + 0.06), 0, g + 0.04), 0.12, 0.1, st["Holz alt"], quer=(0, 1, 0))
    netz.quader((HALB - 0.2, 0, profil(HALB - 0.2)[3] - 0.05), (0.08, 0.08, 0.08), st["Eisen"])
    # Duchten (Sitzbretter) und eine Bank achtern für den Schiffer
    for x in (-0.55, 0.85):
        w, _, _, g = profil(x)
        netz.stab((x, -(w - 0.05), g - 0.12), (x, w - 0.05, g - 0.12), 0.24, 0.05, st["Holz"], quer=(0, 0, 1))
    return netz.objekt()


def stange(st, liegend):
    netz = bs.Netz("Stange_Liegend" if liegend else "Stange")
    if liegend:
        netz.stab((-2.1, -0.28, 0.18), (2.2, -0.28, 0.2), 0.05, 0.05, st["Holz"])
    else:
        # vom Schiffer (Hände auf Brusthöhe) schräg nach hinten unten zum Grund
        netz.stab((-0.5, 0.28, 2.35), (-3.1, 0.55, -1.7), 0.05, 0.05, st["Holz"])
        netz.stab((-3.1, 0.55, -1.7), (-3.15, 0.56, -1.82), 0.07, 0.07, st["Eisen"])
    return netz.objekt()


def bauen(st):
    return [rumpf(st), ob.verdraenger(st, profil, HALB, innen=0.08, schritt=0.25), stange(st, False), stange(st, True),
            bs.anker("Anker_Bug", (HALB + 0.1, 0, 0), 270), bs.anker("Anker_Heck", (-HALB - 0.1, 0, 0), 90),
            bs.anker("Anker_Schiffer", (-1.35, 0, 0.02), 270)]


def vorschau(objekte, schnell):
    ws.buehne(9.0, 1.2, schnell)
    ws.gelaende([(-60, -3.0), (60, -3.0)], (-60, 60, 0.0), achse="y")
    zeigen = {o for o in objekte if o.name not in ("Stange_Liegend", "Verdraenger")} | {k for o in objekte for k in o.children_recursive}
    ws.stempel("Nachen mit Stakstange")
    a = bs.rendern("_wasser_nachen_a", lambda o: o in zeigen)
    zeigen = {o for o in objekte if o.name not in ("Stange", "Verdraenger")} | {k for o in objekte for k in o.children_recursive}
    ws.stempel("festgemacht: Stange liegt im Boot")
    b = bs.rendern("_wasser_nachen_b", lambda o: o in zeigen, 2)
    return ws.nebeneinander("wasser_nachen", [a, b])


def main():
    schnell = "--schnell" in sys.argv
    name = "wasser_nachen"
    bs.neue_szene()
    st = bs.Stoffe()
    objekte = bauen(st)
    netze = [o for o in objekte if o.type == "MESH"]
    alle = objekte + [k for o in objekte for k in o.children_recursive if k not in objekte]
    bs.bericht(name, [o for o in alle if o.type == "MESH"] + [o for o in alle if o.type != "MESH"], einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    import hafenbauten as hb  # noqa: E402
    vorher, nachher = bs.farben_zusammenlegen([o for o in alle if o.type == "MESH"], behalten=hb.BEHALTEN)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(name, objekte))
    if "--ohne-bilder" not in sys.argv:
        print(vorschau(netze + [o for o in objekte if o.type != "MESH"], schnell))


if __name__ == "__main__":
    main()
