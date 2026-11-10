"""Die Einrichtung des Wohnhauses (Vorratskammer, Küche, Hauswappen, Gute Stube, Bibliothek, Riegel, Hund) in einer nachgebauten Wohnstube.

    blender -b --factory-startup --python tools/modelle/personal_wohnstube.py -- [--aus=Ordner] [--schnell]

Die Wohnstube des Spiels (Stadtansicht.Raeume.cs, `WohnstubeEinrichten`) ist aus Grundkörpern gebaut: Raum 8,4 × 6,6 m, 4,5 m hoch, Herd hinten
links, Tisch mit Bänken in der Mitte, Bett hinten rechts, Truhe vorn rechts, Wiege vorn links. Hier stehen sie als einfache Kästen (Spiel: Z nach vorn,
Blender: Y nach hinten), damit die Stücke ihren Platz bekommen. Schreibt wohnstube_hinten.png und wohnstube_vorn.png. Nur Vorschau.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bausteine as bs  # noqa: E402
from bausteine import Matrix, Vector  # noqa: E402
import personal_einrichtung as pe  # noqa: E402

SCHNELL = "--schnell" in sys.argv
AUS = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--aus=")), os.path.join(bs.WURZEL, "docs", "modelle", "personal_einrichtung"))
os.makedirs(AUS, exist_ok=True)
bs.VORSCHAU = AUS
BX, BY, H = 4.2, 3.3, 4.5            # halbe Breite, halbe Tiefe, Höhe (Blender: Y nach hinten)


def rahmen(x, y, drehung, z=0.0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(drehung), 4, "Z")


def raum(st, ohne_wand):
    """Boden (Dielen), Wände aus Fachwerk und Putz, ohne die Wand `ohne_wand` (die Kamera steht davor)."""
    n = bs.Netz("Raum_Wohnstube", alle=True)
    for k in range(int(2 * BY / 0.28)):
        n.quader((0, -BY + 0.14 + 0.28 * k, -0.02), (2 * BX, 0.27, 0.04), st["Holz alt"] if k % 3 else st["Holz"], ohne=("-z",))
    wand = {"hinten": ((-BX, BY), (BX, BY), (0, -1, 0)), "links": ((-BX, -BY), (-BX, BY), (1, 0, 0)), "rechts": ((BX, -BY), (BX, BY), (-1, 0, 0)),
            "vorn": ((-BX, -BY), (BX, -BY), (0, 1, 0))}
    for name, (a, b, nrm) in wand.items():
        if name == ohne_wand:
            continue
        n.vieleck_nach([(a[0], a[1], 0), (b[0], b[1], 0), (b[0], b[1], H), (a[0], a[1], H)], nrm, st["Putz"])
        for z in (0.0, 1.3, H - 0.15):                                       # Fachwerk-Balken
            dx, dy = b[0] - a[0], b[1] - a[1]
            l = math.hypot(dx, dy)
            n.stab((a[0] + nrm[0] * 0.03, a[1] + nrm[1] * 0.03, z + 0.08), (b[0] + nrm[0] * 0.03, b[1] + nrm[1] * 0.03, z + 0.08), 0.15, 0.06, st["Fachwerk"],
                   quer=nrm)
        for t in range(1, 6):
            x, y = a[0] + (b[0] - a[0]) * t / 6, a[1] + (b[1] - a[1]) * t / 6
            n.stab((x + nrm[0] * 0.03, y + nrm[1] * 0.03, 0.0), (x + nrm[0] * 0.03, y + nrm[1] * 0.03, H), 0.15, 0.06, st["Fachwerk"], quer=nrm)
    n.objekt()


def kaesten(st):
    """Herd, Tisch mit Bänken, Bett, Truhe, Wiege wie im Spiel (Grundkörper)."""
    n = bs.Netz("_Stand", alle=True)
    n.quader((-3.1, 2.5, 0.5), (1.8, 1.3, 1.0), st["Bruchstein"])
    n.quader((-3.1, 2.85, 2.9), (1.6, 0.6, 3.2), st["Bruchstein"])
    n.quader((0.4, 0.0, 0.78), (2.6, 1.1, 0.1), st["Holz alt"])
    n.quader((0.4, 0.0, 0.38), (2.2, 0.5, 0.7), st["Fachwerk"])
    for dy in (-0.95, 0.95):
        n.quader((0.4, dy, 0.42), (2.4, 0.35, 0.1), st["Holz"])
    n.quader((3.1, 2.0, 0.3), (1.7, 2.4, 0.5), st["Holz"])
    n.quader((3.1, 1.75, 0.62), (1.6, 1.8, 0.14), st["Decke rot"])
    n.quader((3.3, -1.4, 0.35), (1.1, 0.7, 0.7), st["Holz"])
    n.quader((-1.9, -1.9, 0.3), (0.9, 0.55, 0.4), st["Holz alt"])
    n.objekt()


def kamera_licht(szene, eye, ziel):
    import bpy
    szene.render.engine = "CYCLES"
    bs.cycles_geraet(szene)
    szene.cycles.samples = 24 if SCHNELL else 64
    szene.cycles.use_denoising = True
    szene.view_settings.view_transform = "Standard"
    szene.view_settings.exposure = -0.5
    welt = bpy.data.worlds.new("Himmel")
    welt.use_nodes = True
    welt.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.44, 0.5, 1)
    welt.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    szene.world = welt
    for name, ort, e, f, g in (("L1", (-2.5, 0.0, 4.2), 700, (1.0, 0.85, 0.65), 5.0), ("L2", (2.5, 0.0, 4.2), 500, (0.9, 0.93, 1.0), 5.0)):
        l = bpy.data.lights.new(name, "AREA")
        l.energy, l.color, l.size = e, f, g
        o = bpy.data.objects.new(name, l)
        o.location = ort
        szene.collection.objects.link(o)
    kam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    kam.data.sensor_fit = "VERTICAL"
    kam.data.angle_y = math.radians(55)
    kam.data.clip_start = 0.05
    szene.collection.objects.link(kam)
    szene.camera = kam
    vor = (Vector(ziel) - Vector(eye)).normalized()
    rechts = vor.cross(Vector((0, 0, 1))).normalized()
    oben = rechts.cross(vor)
    m = Matrix((rechts, oben, -vor)).transposed().to_4x4()
    m.translation = Vector(eye)
    kam.matrix_world = m
    szene.render.resolution_x, szene.render.resolution_y = 1100, 620


def teil(name, hinten):
    import bpy
    bs.neue_szene()
    st = bs.Stoffe()
    raum(st, "vorn" if hinten else "hinten")
    kaesten(st)
    plan = []
    if hinten:
        plan = [("Kueche", rahmen(-0.3, BY, 180)), ("Vorratskammer", rahmen(-BX, -0.4, -90)), ("Riegel", rahmen(BX, -0.4, 90)),
                ("Hauswappen", rahmen(BX, -0.4, 90, 1.9)), ("Hund", rahmen(-2.2, 0.9, 180))]
    else:
        plan = [("Bibliothek", rahmen(-2.4, -BY, 0)), ("Stube", rahmen(1.9, -BY, 0))]
    for n, M in plan:
        netz = bs.Netz("Einrichtung_" + n, alle=True)
        pe.ALLE[n][0](netz, st, M)
        netz.objekt()
    try:
        import fuellung_vorschau as fv
        fv.texturieren()
    except Exception as fehler:  # noqa: BLE001
        print("keine Texturen:", fehler)
    szene = bpy.context.scene
    if hinten:
        kamera_licht(szene, (-1.2, -BY + 0.15, 3.6), (0.4, 1.5, 1.0))
    else:
        kamera_licht(szene, (-0.6, BY - 0.15, 3.6), (0.2, -1.5, 1.0))
    return bs.rendern_einzeln(name, "Wohnstube – " + ("Blick nach hinten: Küche, Vorratskammer, Tür mit Riegeln und Wappen, Hund" if hinten else "Blick nach vorn: Bibliothek, Gute Stube"))


def main():
    print("BILD", teil("wohnstube_hinten", True))
    print("BILD", teil("wohnstube_vorn", False))


main()
