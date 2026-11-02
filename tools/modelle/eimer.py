"""Holzeimer – Daubeneimer (Köln um 1400) für die Eimerkette beim Hausbrand.

    blender -b --python tools/modelle/eimer.py [-- --schnell] [-- --ohne-bilder] [-- --ansichten <ordner>]
    python tools/modelle/eimer.py [--schnell] [--ohne-bilder] [--ansichten <ordner>]

Schreibt game/assets/modelle/requisiten/eimer.glb und docs/modelle/eimer.png (Spielkamera).
`--ansichten <ordner>`: zusätzlich vier Ansichten (oben, vorne, Seite, schräg) als PNG in diesen Ordner – zum Vergleich
mit der Vorlage (Vorlage: docs/assets/prompts/eimer.md).

Knoten (Ursprung je mitten am Boden, Meter):
    Eimer_0  Hauptmodell (rund 200 Dreiecke): 12 Dauben als flache Flächen, leicht konisch (⌀ unten 0,25 m, oben 0,30 m,
             0,26 m hoch), zwei Eisenreifen als einfache Ringe mit Rost auf einzelnen Flächen, zwei längere Ohrdauben
             (links und rechts, ±X) und ein Henkel aus Rundeisen, der durch die Ohren greift
    Eimer_1  Fernstufe (rund 80 Dreiecke): 7 Flächen, Eisenband am Fuß und oberer Reifen, Henkel aus zwei Stücken – im Spiel ab einer
             Entfernung statt Eimer_0 zeigen (visibility_range), Silhouette und Ursprung gleich
    Wasser   Wasserspiegel knapp unter dem Rand, passt in beide Stufen – für einen leeren Eimer den Knoten ausblenden

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
R_OBEN = 0.15         # Außenradius oben (an den Kanten)
R_UNTEN = 0.125       # Außenradius unten
DAUBEN = 12           # Hauptmodell: je Daube eine flache Fläche, die Kanten zeichnen die Fugen (flach schattiert)
DAUBEN_FERN = 7       # Fernstufe (vier Reihen × 7 Flächen + Innenwand + Henkel ≈ 80 Dreiecke)
DICKE = 0.014         # Stärke einer Daube
OHR = 0.05            # so weit ragen die beiden Ohrdauben über den Rand
BODEN = 0.022         # Oberseite des Bodens innen
REIFEN = ((0.03, 0.054), (0.19, 0.214))   # Unter- und Oberkante der beiden Reifen
REIFEN_AUF = 0.004    # so weit steht ein Reifen über den Dauben
HENKEL_DICKE = 0.009
HENKEL_HOCH = 0.13    # Scheitel des Henkels über dem Loch in den Ohren
WASSER_TIEFE = 0.035  # Wasserspiegel unter dem Rand


def radius(z):
    """Außenradius des Eimers in der Höhe z (gerade Kegelfläche, an den Kanten der Dauben)."""
    return R_UNTEN + (R_OBEN - R_UNTEN) * z / HOEHE


def punkt(r, w, z):
    return Vector((r * math.cos(w), r * math.sin(w), z))


def bauen():
    bs.neue_szene()
    return teile_bauen(bs.Stoffe())


def teile_bauen(st):
    """Die drei Knoten: Eimer_0 (Hauptmodell), Eimer_1 (Fernstufe), Wasser."""
    return [eimer_nah(st), eimer_fern(st), wasser(st)]


def eimer_nah(st):
    """Hauptmodell (rund 200 Dreiecke): Mantel aus DAUBEN flachen Flächen, Ohrdauben bei ±X, zwei Reifen als einfache Ringe
    (nur außen), Henkel aus Rundeisen (dreikantig genähert, wenige Stücke). Maserung und Fugen zeichnet im Spiel die
    Material-Bibliothek („Holz alt“, „Bretter“ → bretter), die Kanten der Flächen die Daubenstöße."""
    z = nt.zufall(1400)
    netz = bs.Netz("Eimer_0")
    schritt = 2 * math.pi / DAUBEN
    for k in range(DAUBEN):
        w0, w1, mitte = (k - 0.5) * schritt, (k + 0.5) * schritt, k * schritt
        ohr = k in (0, DAUBEN // 2)
        holz = st["Bretter"] if k % 3 == 1 else st["Holz alt"]
        a0, a1 = punkt(R_UNTEN, w0, 0), punkt(R_UNTEN, w1, 0)
        i0, i1 = punkt(R_UNTEN - DICKE, w0, 0), punkt(R_UNTEN - DICKE, w1, 0)
        oben = HOEHE - z.uniform(0.0, 0.004)                     # abgenutzte, ungleiche Oberkanten
        b0, b1 = punkt(radius(oben), w0, oben), punkt(radius(oben), w1, oben)
        j0, j1 = punkt(radius(oben) - DICKE, w0, oben), punkt(radius(oben) - DICKE, w1, oben)
        if ohr:                                                   # Ohrdaube: läuft oben spitz-rund aus
            zo = HOEHE + OHR
            c0, c1 = punkt(radius(zo), w0 + schritt * 0.2, zo), punkt(radius(zo), w1 - schritt * 0.2, zo)
            d0, d1 = punkt(radius(zo) - DICKE, w0 + schritt * 0.2, zo), punkt(radius(zo) - DICKE, w1 - schritt * 0.2, zo)
            s, si = punkt(radius(zo + 0.012), mitte, zo + 0.012), punkt(radius(zo + 0.012) - DICKE, mitte, zo + 0.012)
            netz.vieleck_nach([a0, a1, b1, c1, s, c0, b0], punkt(1, mitte, 0), holz)          # außen
            netz.vieleck_nach([i0, i1, j1, d1, si, d0, j0], -punkt(1, mitte, 0), st["Holz"])  # innen
            for p, q, pi, qi in ((b0, c0, j0, d0), (c0, s, d0, si), (s, c1, si, d1), (c1, b1, d1, j1)):
                netz.vieleck_nach([p, q, qi, pi], ((p + q) / 2 - punkt(radius(zo) * 0.8, mitte, zo - 0.03)), holz)
        else:
            netz.vieleck([a0, a1, b1, b0], holz)
            netz.vieleck([i1, i0, j0, j1], st["Holz"])
            netz.vieleck([b0, b1, j1, j0], holz)
    netz.vieleck([punkt(R_UNTEN - DICKE, (k + 0.5) * schritt, BODEN) for k in range(DAUBEN)], st["Holz"])
    for nr, (z0, z1) in enumerate(REIFEN):                        # Reifen: ein Ring, Rost auf einzelnen Flächen
        r0, r1 = radius(z0) + REIFEN_AUF, radius(z1) + REIFEN_AUF
        for k in range(DAUBEN):
            w0, w1 = (k - 0.5) * schritt, (k + 0.5) * schritt
            rostig = (k + nr) % 3 == 0
            netz.vieleck([punkt(r0, w0, z0), punkt(r0, w1, z0), punkt(r1, w1, z1), punkt(r1, w0, z1)],
                         st["Rost"] if rostig else st["Eisen"])
    henkel(netz, st, teile=6, kanten=3)
    return netz.objekt()


def eimer_fern(st):
    """Fernstufe (rund 80 Dreiecke): Mantel aus DAUBEN_FERN Flächen in vier Reihen (Eisenband am Fuß, Holz, oberer Reifen,
    Holz), Innenwand ohne Randfläche (läuft oben auf der Außenkante aus), Henkel aus zwei Stücken; ohne Ohren und Boden."""
    netz = bs.Netz("Eimer_1")
    schritt = 2 * math.pi / DAUBEN_FERN
    # Unten ein Band in Eisen (unterer Reifen samt Fuß, aus der Ferne nicht zu unterscheiden), dann Holz, oberer Reifen, Holz
    reihen = [(0.0, REIFEN[0][1], "Eisen"), (REIFEN[0][1], REIFEN[1][0], "Holz alt"), (REIFEN[1][0], REIFEN[1][1], "Eisen"),
              (REIFEN[1][1], HOEHE, "Holz alt")]
    for k in range(DAUBEN_FERN):
        w0, w1 = (k - 0.5) * schritt, (k + 0.5) * schritt
        for z0, z1, stoff in reihen:
            netz.vieleck([punkt(radius(z0), w0, z0), punkt(radius(z0), w1, z0),
                          punkt(radius(z1), w1, z1), punkt(radius(z1), w0, z1)], st[stoff])
        netz.vieleck([punkt(R_UNTEN - DICKE, w1, 0), punkt(R_UNTEN - DICKE, w0, 0),
                      punkt(R_OBEN, w0, HOEHE), punkt(R_OBEN, w1, HOEHE)], st["Holz"])   # oben auf der Außenkante: kein Schlitz
    henkel(netz, st, teile=2, kanten=3, mit_enden=False)
    return netz.objekt()


def wasser(st):
    """Wasserspiegel: passt in beide Stufen (innerhalb der engsten Innenwand)."""
    netz = bs.Netz("Wasser")
    zw = HOEHE - WASSER_TIEFE
    r = (radius(zw) - DICKE) * math.cos(math.pi / DAUBEN_FERN) - 0.001
    schritt = 2 * math.pi / DAUBEN
    netz.vieleck([punkt(r, (k + 0.5) * schritt, zw) for k in range(DAUBEN)], st["Wasser"])
    return netz.objekt()


def henkel(netz, st, teile, kanten, mit_enden=True):
    """Henkel aus Rundeisen (`kanten`-kantig genähert): Bogen über dem Eimer aus `teile` Stücken; mit Enden greift er
    an beiden Seiten durch das Loch der Ohrdaube."""
    z_loch = HOEHE + OHR * 0.5
    r_loch = radius(z_loch)
    weit = r_loch + 0.012                                     # Henkel liegt außen an den Ohren an
    bahn = [Vector((r_loch - DICKE - 0.006, 0, z_loch))] if mit_enden else []
    for i in range(teile + 1):
        t = math.pi * i / teile
        bahn.append(Vector((weit * math.cos(t), 0, z_loch + HENKEL_HOCH * math.sin(t))))
    if mit_enden:
        bahn.append(Vector((-(r_loch - DICKE - 0.006), 0, z_loch)))
    ringe = []
    for i, p in enumerate(bahn):
        vor = bahn[min(i + 1, len(bahn) - 1)] - bahn[max(i - 1, 0)]
        vor.normalize()
        quer = Vector((0, 1, 0))
        senk = vor.cross(quer).normalized()
        ringe.append([p + HENKEL_DICKE / 2 * (math.cos(w) * senk + math.sin(w) * quer)
                      for w in (math.pi / 2 + k * 2 * math.pi / kanten for k in range(kanten))])
    for a, b in zip(ringe, ringe[1:]):
        m = sum(a, Vector()) / kanten
        for k in range(kanten):
            netz.vieleck_nach([a[k], a[(k + 1) % kanten], b[(k + 1) % kanten], b[k]],
                              (a[k] + a[(k + 1) % kanten]) / 2 - m, st["Eisen"])


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
    nah = [o for o in objekte if o.name != "Eimer_1"]
    bs.rendern(NAME, lambda o: o in nah)
    if "--ansichten" in sys.argv:
        ordner = sys.argv[sys.argv.index("--ansichten") + 1]
        os.makedirs(ordner, exist_ok=True)
        bpy.context.scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.5, 0.5, 1)
        ansichten(nah, ordner)


if __name__ == "__main__":
    main()
