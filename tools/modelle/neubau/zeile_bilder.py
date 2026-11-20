"""Kontrollbilder der Haeuserzeilen (Blender): baut die Module eines Satzes (zeile_module.py) und stellt daraus die Probezeile mit Ecke
nach derselben Nachbarregel wie das Spiel (game/scripts/Stadtansicht.Zeilen.cs) zusammen.

    blender -b --python tools/modelle/neubau/zeile_bilder.py -- --satz=mensch_s1 --bilder=<ordner> [--pixel=1,2,3] [--naht=1]

Bilder: Zeile mit Ecke von der Kreuzung (Zoom 25, 50, 100 m), Ego-Blick an der Ecke; mit --pixel zusaetzlich eine Rechenvorschau des
Pixel-Looks (Bild in 1/2 bzw. 1/3 der Aufloesung gerechnet und hart hochgezogen – nur die Aufloesung, ohne Stufenlicht, Umrisse,
Dunst und Vignette des Spiels). --naht=1: Nahtbilder aller Nachbarfaelle (je zwei Koerper nebeneinander).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import zeile_module as Z                 # noqa: E402
import wohnhaus_mensch_s1_g1 as g1       # noqa: E402

# ── Nachbarregel (wie Stadtansicht.Zeilen.cs, ZeilenRegel) ─────────────────────────────────────────────────────────────


def stellung(p):
    return "eck" if p["modul"] == "eck" else ("giebel" if p["modul"].startswith("k_g") else "trauf")


def profil_hoeher(p, n):
    """Ragt der Seitenumriss von p ueber den des Nachbarn n hinaus? (Giebel- und Eckhaus: flach in Traufhoehe, Traufhaus: Giebeldreieck)"""
    if n is None: return True
    sp, sn = stellung(p), stellung(n)
    zp, zn = p.get("z_traufe", Z.S["z_traufe"]), n.get("z_traufe", Z.S["z_traufe"])
    if sp != "trauf": return zp > zn + 0.05
    if sn != "trauf": return True
    return zp > zn + 0.05


def stuecke(parzellen):
    """Parzellen (Modul, Breite) einer geraden Zeile -> Liste (Modul, x0, gespiegelt). Links und rechts: Nachbarn in der Zeile oder None."""
    aus, x = [], 0.0
    for i, p in enumerate(parzellen):
        w = Z.BREITE[p["modul"]]
        aus.append((p["modul"], x, False))
        for seite, n in (("l", parzellen[i - 1] if i > 0 else p.get("links")), ("r", parzellen[i + 1] if i + 1 < len(parzellen) else p.get("rechts"))):
            spiegel = seite == "r"
            st = stellung(p)
            if st == "giebel":
                aus.append(("seite_blind" if n is not None else "seite_offen", x, spiegel))
                if n is not None: aus.append(("gasse", x, spiegel))
            elif st == "trauf" and profil_hoeher(p, n):
                stein = Z.S["og"] == "stein"
                aus.append(("kappe_brand" if (n is not None or stein) else "kappe_offen", x, spiegel))
        x += w
    return aus, x


def setzen(objekte, liste, ursprung, ex, ey, breite_von):
    """Stellt die Module als verknuepfte Kopien: ursprung + mx*ex + my*ey (Modulrahmen: x entlang der Gasse, y in die Tiefe)."""
    neu = []
    for modul, x0, spiegel in liste:
        quelle = objekte[modul]
        o = quelle.copy(); bpy.context.scene.collection.objects.link(o)
        w = breite_von.get(modul_von_x(liste, x0), 0)
        sx = -1.0 if spiegel else 1.0
        tx = x0 + (w if spiegel else 0.0)
        m = Matrix(((ex.x * sx, ey.x, 0, 0), (ex.y * sx, ey.y, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
        o.matrix_world = Matrix.Translation(ursprung + ex * tx) @ m
        neu.append(o)
    return neu


def modul_von_x(liste, x0):
    for modul, x, _ in liste:
        if abs(x - x0) < 1e-6 and modul in Z.BREITE: return modul
    return None


def rendern(datei, auge, ziel, brennweite, pixel=1, breite=1664, hoehe=936):
    szene = bpy.context.scene
    kam = bpy.data.objects.get("Kontrollkamera")
    if kam is None:
        kam = bpy.data.objects.new("Kontrollkamera", bpy.data.cameras.new("Kontrollkamera"))
        szene.collection.objects.link(kam)
        sonne = bpy.data.objects.new("Sonne", bpy.data.lights.new("Sonne", "SUN"))
        sonne.data.energy = 3.2; sonne.rotation_euler = (math.radians(50), 0, math.radians(-40))
        szene.collection.objects.link(sonne)
        szene.world.use_nodes = True
        hg = next(n for n in szene.world.node_tree.nodes if n.type == "BACKGROUND")
        hg.inputs[0].default_value = (0.62, 0.66, 0.72, 1); hg.inputs[1].default_value = 0.9
    kam.location = auge
    kam.rotation_euler = (Vector(ziel) - Vector(auge)).to_track_quat("-Z", "Y").to_euler()
    kam.data.lens = brennweite
    szene.camera = kam
    szene.render.engine = "CYCLES"; szene.cycles.device = "CPU"; szene.cycles.samples = 32
    szene.render.resolution_x, szene.render.resolution_y = breite // pixel, hoehe // pixel
    szene.render.resolution_percentage = 100
    szene.render.image_settings.file_format = "PNG"
    szene.render.filepath = datei
    bpy.ops.render.render(write_still=True)
    if pixel > 1:
        import numpy as np
        bild = bpy.data.images.load(datei)
        w, h = bild.size
        feld = np.empty(w * h * 4, dtype=np.float32); bild.pixels.foreach_get(feld)
        feld = feld.reshape(h, w, 4).repeat(pixel, axis=0).repeat(pixel, axis=1)
        gross = bpy.data.images.new("gross", w * pixel, h * pixel, alpha=True)
        gross.pixels.foreach_set(feld.ravel())
        gross.filepath_raw = datei; gross.file_format = "PNG"; gross.save()
        bpy.data.images.remove(bild); bpy.data.images.remove(gross)
    print("BILD", datei)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = dict((a[2:].split("=", 1) + ["1"])[:2] for a in args if a.startswith("--"))
    satz = opt.get("satz", "mensch_s1")
    Z.stil(satz)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if bpy.context.scene.world is None: bpy.context.scene.world = bpy.data.worlds.new("Welt")
    stoffe = Z.stoffe_anlegen(Z.S["blatt"])
    objekte = {}
    for m in Z.MODULE:
        t, _ = Z.BAUER[m](m)
        o = t.objekt(stoffe); o.hide_render = True; o.hide_set(True)
        objekte[m] = o
    ordner = opt["bilder"]; os.makedirs(ordner, exist_ok=True)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0))
    boden = bpy.context.active_object; boden.data.materials.append(g1.stoff("Kontrollboden", farbe=(0.36, 0.33, 0.27)))
    # Probezeile (Konzept Kapitel 6): Schenkel A von links nach rechts bis zur Ecke, Schenkel B von der Ecke weg
    a = [{"modul": "k_g6"}, {"modul": "k_g4"}, {"modul": "k_t6"}, {"modul": "k_t6"}, {"modul": "eck"}]
    b = [{"modul": "k_g6"}, {"modul": "k_t8"}, {"modul": "k_g4"}]
    b[0]["links"] = a[-1]
    liste_a, laenge_a = stuecke(a)
    liste_b, _ = stuecke(b)
    breiten = dict(Z.BREITE)
    stehen = setzen(objekte, liste_a, Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), breiten)
    ecke = Vector((laenge_a, 0, 0))
    stehen += setzen(objekte, liste_b, ecke + Vector((0, Z.TIEFE, 0)), Vector((0, 1, 0)), Vector((-1, 0, 0)), breiten)
    for o in stehen: o.hide_render = False; o.hide_set(False)
    dreiecke = sum(len(o.data.polygons) for o in stehen)
    print("ZEILE Stuecke", len(stehen), "Flaechen", dreiecke)
    k = ecke + Vector((0, 0, 0))
    ziel = (k.x - 6, k.y + 6, 4.0)
    blicke = {"zoom25": 25.0, "zoom50": 50.0, "zoom100": 100.0}
    pixel = [int(p) for p in opt.get("pixel", "1").split(",")]
    for name, abstand in blicke.items():
        richtung = Vector((0.55, -0.75, 0.62)).normalized()
        auge = Vector(ziel) + richtung * abstand
        for px in pixel:
            vor = "" if px == 1 else f"_pixel{px}_rechenvorschau"
            rendern(os.path.join(ordner, f"{satz}_{name}{vor}.png"), auge, ziel, 35, pixel=px)
    rendern(os.path.join(ordner, f"{satz}_ego_ecke.png"), (k.x + 4.5, k.y - 5.5, 1.7), (k.x - 6, k.y + 4, 3.6), 18)
    rendern(os.path.join(ordner, f"{satz}_ruecken.png"), (k.x - 14, k.y + 40, 16), (k.x - 12, k.y + 6, 3.0), 28)


if __name__ == "__main__":
    main()
