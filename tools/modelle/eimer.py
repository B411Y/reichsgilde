"""Holzeimer – Daubeneimer (Köln um 1400) für die Eimerkette beim Hausbrand.

    blender -b --python tools/modelle/eimer.py [-- --schnell] [-- --ohne-bilder] [-- --ansichten <ordner>]
    python tools/modelle/eimer.py [--schnell] [--ohne-bilder] [--ansichten <ordner>]

Schreibt game/assets/modelle/requisiten/eimer.glb und docs/modelle/eimer.png (Spielkamera).
`--ansichten <ordner>`: zusätzlich vier Ansichten (oben, vorne, Seite, schräg) als PNG in diesen Ordner – zum Vergleich
mit der Vorlage (Vorlage: docs/assets/prompts/eimer.md).

Knoten (Ursprung je mitten am Boden, Meter):
    Eimer    14 Dauben aus altem Holz mit schmalen Fugen, leicht konisch (⌀ unten 0,25 m, oben 0,30 m, 0,26 m hoch),
             zwei geschmiedete Eisenreifen mit Rost an den Kanten, zwei längere Ohrdauben (links und rechts, ±X)
             und ein Henkel aus Rundeisen, der durch die Ohren greift
    Wasser   Wasserspiegel knapp unter dem Rand – für einen leeren Eimer den Knoten ausblenden

Der Henkel steht aufrecht über dem Eimer (so wird er getragen); Blickrichtung wie bei allen Modellen: Vorderseite −Y
in Blender, +Z im Spiel. Maserung und Eisen kommen im Spiel aus der Material-Bibliothek („Holz alt“, „Bretter“, „Holz“ →
bretter/fachwerkholz, „Eisen“ → eisen); „Rost“ ist eine schlichte Farbe ohne Struktur.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
import natur as nt  # noqa: E402
from bausteine import Vector  # noqa: E402

NAME = "eimer"

HOEHE = 0.26          # Oberkante der Dauben
R_OBEN = 0.15         # Außenradius oben (an den Fugen)
R_UNTEN = 0.125       # Außenradius unten
DAUBEN = 14
DICKE = 0.014         # Stärke einer Daube
FUGE = 0.003          # Breite der Fuge zwischen zwei Dauben (außen)
OHR = 0.05            # so weit ragen die beiden Ohrdauben über den Rand
BODEN = 0.022         # Oberseite des Bodens innen
REIFEN = ((0.03, 0.054), (0.19, 0.214))   # Unter- und Oberkante der beiden Reifen
REIFEN_AUF = 0.004    # so weit steht ein Reifen über den Dauben
ROST = 0.004          # Rostsaum an den Kanten der Reifen
HENKEL_DICKE = 0.009
HENKEL_HOCH = 0.13    # Scheitel des Henkels über dem Loch in den Ohren
WASSER_TIEFE = 0.035  # Wasserspiegel unter dem Rand


def radius(z):
    """Außenradius des Eimers in der Höhe z (gerade Kegelfläche)."""
    return R_UNTEN + (R_OBEN - R_UNTEN) * z / HOEHE


def punkt(r, w, z):
    return Vector((r * math.cos(w), r * math.sin(w), z))


def bauen():
    bs.neue_szene()
    st = bs.Stoffe()
    z = nt.zufall(1400)
    schritt = 2 * math.pi / DAUBEN

    eimer = bs.Netz("Eimer")
    for k in range(DAUBEN):
        mitte = k * schritt                                   # Daube 0 bei +X, Daube DAUBEN/2 bei −X: die Ohren
        halb = schritt / 2 - FUGE / 2 / R_UNTEN
        w0, w1 = mitte - halb, mitte + halb
        ohr = k in (0, DAUBEN // 2)
        oben = HOEHE + (OHR if ohr else -z.uniform(0.0, 0.004))   # abgenutzte, ungleiche Oberkanten
        holz = st["Bretter"] if k % 3 == 1 else st["Holz alt"]
        a0, a1 = punkt(R_UNTEN, w0, 0), punkt(R_UNTEN, w1, 0)
        b0, b1 = punkt(radius(oben), w0, oben), punkt(radius(oben), w1, oben)
        i0, i1 = punkt(R_UNTEN - DICKE, w0, 0), punkt(R_UNTEN - DICKE, w1, 0)
        j0, j1 = punkt(radius(oben) - DICKE, w0, oben), punkt(radius(oben) - DICKE, w1, oben)
        if ohr:                                               # Ohrdaube: oben gerundet (Spitze in der Mitte)
            s = punkt(radius(oben + 0.012), mitte, oben + 0.012)
            si = punkt(radius(oben + 0.012) - DICKE, mitte, oben + 0.012)
            eimer.vieleck([a0, a1, b1, s, b0], holz)                    # außen
            eimer.vieleck([i1, i0, j0, si, j1], st["Holz"])             # innen
            eimer.vieleck_nach([b0, s, si, j0], (0, 0, 1), holz)        # Oberkante, zwei Schrägen
            eimer.vieleck_nach([s, b1, j1, si], (0, 0, 1), holz)
        else:
            eimer.vieleck([a0, a1, b1, b0], holz)
            eimer.vieleck([i1, i0, j0, j1], st["Holz"])
            eimer.vieleck([b0, b1, j1, j0], holz)
        eimer.vieleck([a0, b0, j0, i0], st["Holz"])           # Seiten in der Fuge (dunkel)
        eimer.vieleck([a1, i1, j1, b1], st["Holz"])
        # Fugengrund in halber Daubenstärke bis zur nächsten Daube: Die Fuge bleibt eine dunkle Rille, man sieht nicht hindurch
        f0, f1 = w1, mitte + schritt - halb
        rm, zf = R_UNTEN - DICKE / 2, HOEHE - 0.004
        eimer.vieleck([punkt(rm, f0, 0), punkt(rm, f1, 0), punkt(radius(zf) - DICKE / 2, f1, zf),
                       punkt(radius(zf) - DICKE / 2, f0, zf)], st["Holz"])

    boden = [punkt(R_UNTEN - DICKE + 0.002, (k + 0.5) * schritt, BODEN) for k in range(DAUBEN)]
    eimer.vieleck(boden, st["Holz"])

    for nr, (z0, z1) in enumerate(REIFEN):                     # Reifen: Ecken an den Fugen, flach über jeder Daube
        for k in range(DAUBEN):
            w0, w1 = (k - 0.5) * schritt, (k + 0.5) * schritt
            r0, r1 = radius(z0) + REIFEN_AUF, radius(z1) + REIFEN_AUF
            stufen = [z0, z0 + ROST, z1 - ROST, z1]
            for s in range(3):
                za, zb = stufen[s], stufen[s + 1]
                ra, rb = r0 + (r1 - r0) * (za - z0) / (z1 - z0), r0 + (r1 - r0) * (zb - z0) / (z1 - z0)
                rostig = s == 0 or (s == 2 and (k * 5 + nr * 3) % 4 != 0)   # unten immer, oben meist
                stoff = st["Rost"] if rostig else st["Eisen"]
                eimer.vieleck([punkt(ra, w0, za), punkt(ra, w1, za), punkt(rb, w1, zb), punkt(rb, w0, zb)], stoff)
            eimer.vieleck([punkt(r1, w0, z1), punkt(r1, w1, z1),                       # Oberkante des Reifens
                           punkt(radius(z1), w1, z1), punkt(radius(z1), w0, z1)], st["Rost"] if k % 3 == 0 else st["Eisen"])

    henkel(eimer, st)

    wasser = bs.Netz("Wasser")
    zw = HOEHE - WASSER_TIEFE
    wasser.vieleck([punkt(radius(zw) - DICKE + 0.001, (k + 0.5) * schritt, zw) for k in range(DAUBEN)], st["Wasser"])
    return [eimer.objekt(), wasser.objekt()]


def henkel(netz, st):
    """Henkel aus Rundeisen (vierkantig genähert): Bogen über dem Eimer, an beiden Enden durch das Loch der Ohrdaube."""
    z_loch = HOEHE + OHR * 0.5
    r_loch = radius(z_loch)
    weit = r_loch + 0.012                                     # Henkel liegt außen an den Ohren an
    bahn = [Vector((r_loch - DICKE - 0.006, 0, z_loch))]     # Ende innen, durch das Loch nach außen
    teile = 10
    for i in range(teile + 1):
        t = math.pi * i / teile
        bahn.append(Vector((weit * math.cos(t), 0, z_loch + HENKEL_HOCH * math.sin(t))))
    bahn.append(Vector((-(r_loch - DICKE - 0.006), 0, z_loch)))
    ringe = []
    for i, p in enumerate(bahn):
        vor = bahn[min(i + 1, len(bahn) - 1)] - bahn[max(i - 1, 0)]
        vor.normalize()
        quer = Vector((0, 1, 0))
        senk = vor.cross(quer).normalized()
        ringe.append([p + HENKEL_DICKE / 2 * (math.cos(w) * senk + math.sin(w) * quer)
                      for w in (math.pi / 4 + k * math.pi / 2 for k in range(4))])
    for a, b in zip(ringe, ringe[1:]):
        for k in range(4):
            netz.vieleck_nach([a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]],
                              (a[k] + a[(k + 1) % 4]) / 2 - (sum(a, Vector()) / 4), st["Eisen"])


def ansichten(objekte, ordner):
    """Vier Ansichten wie auf dem Ansichtsbogen der Vorlage: oben, vorne, Seite, schräg (orthografisch, neutraler Grund)."""
    import bpy
    szene = bpy.context.scene
    kam = szene.camera
    kam.data.ortho_scale = 0.62
    for o in szene.objects:
        if o.type == "MESH":
            o.hide_render = o not in objekte              # ohne Boden: neutraler Grund wie auf der Vorlage
    szene.render.resolution_x = szene.render.resolution_y = 700
    ziel = Vector((0, 0, 0.17))
    for name, az, neig in (("oben", -90, 89.9), ("vorne", -90, 4), ("seite", 0, 4), ("schraeg", -45, 30)):
        a, n = math.radians(az), math.radians(neig)
        kam.location = ziel + 10 * Vector((math.cos(a) * math.cos(n), math.sin(a) * math.cos(n), math.sin(n)))
        kam.rotation_euler = (ziel - kam.location).to_track_quat("-Z", "Y").to_euler()
        zum_licht = Vector((math.cos(a - math.radians(35)), math.sin(a - math.radians(35)), 1.2)).normalized()
        bpy.data.objects["Sonne"].rotation_euler = (-zum_licht).to_track_quat("-Z", "Y").to_euler()
        szene.render.filepath = os.path.join(ordner, f"modell_{name}.png")
        bpy.ops.render.render(write_still=True)


def main():
    objekte = bauen()
    bs.bericht(NAME, objekte, einzeln=True)
    bs.BIBLIOTHEK_JE_FLAECHE = True
    vorher, nachher = bs.farben_zusammenlegen(objekte)
    print(f"  Materialien: {vorher} → {nachher}")
    print("geschrieben:", bs.exportieren(NAME, objekte))
    if "--ohne-bilder" in sys.argv:
        return
    import bpy
    bs.vorschau_buehne(None, ziel_hoehe=0.15, ausschnitt=0.75, schnell="--schnell" in sys.argv)
    bs.rendern(NAME, lambda o: o in objekte)
    if "--ansichten" in sys.argv:
        ordner = sys.argv[sys.argv.index("--ansichten") + 1]
        os.makedirs(ordner, exist_ok=True)
        bpy.context.scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.5, 0.5, 1)
        ansichten(objekte, ordner)


if __name__ == "__main__":
    main()
