"""Gespann: Karren und Zugtier(e) zusammen in der Ziehen-Pose, mit Zahlen (nur Blender, nur Bilder und Messung, schreibt nichts ins Spiel).

    blender -b --python tools/modelle/zugtiere_gespann.py -- --tier=tier_ochse_rotbraun --karren=<Pfad zur karren_*.glb> --aus=<ordner> [--zwei]

Setzt das Tier so, dass sein Anker_Geschirr auf den Anker_Deichsel des Karrens fällt (beim Pferdekarren mit --zwei zwei Pferde auf Anker_Pferd_L und
_R, das zweite um 0,37 Schleifen versetzt), spielt Ziehen und misst:
  * Abstand des (an den Knochen gebundenen) Ankers vom Anker des Karrens über alle Bilder von Ziehen und Gehen (mm)
  * Deichselwinkel der Karren (Grad) aus Anker_Deichsel und der Höhe der Deichsel am Wagen
Bilder: <aus>/<tier>_gespann_<ansicht>.png
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zugtiere_pruefen as zp  # noqa: E402

FPS = 24


def importieren(pfad):
    vorher = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=pfad)
    return [o for o in bpy.data.objects if o not in vorher]


def farbmaterial(ob):
    """Material nur mit Eckenfarbe (wie das Spiel ohne Struktur), damit die Bilder die Farben der GLB zeigen; die Grundfarbe des Ursprungsmaterials
    (z. B. dunkles Eisen mit weißen Ecken) wird daraufgerechnet, sonst erscheinen Eisenteile weiß."""
    ca = ob.data.color_attributes
    if not len(ca):
        return
    neu = []
    if os.environ.get("RB_DEBUG"):
        print("FARBMAT", ob.name, [m.name if m else None for m in ob.data.materials], [a.name for a in ca])
    for orig in (list(ob.data.materials) or [None]):
        basis = (1.0, 1.0, 1.0, 1.0)
        if orig is not None and orig.name.startswith("Eisen"):
            basis = (0.011, 0.011, 0.012, 1.0)                           # glTF-Grundfarbe des Eisens (das Blender-Material trägt sie nur verknüpft)
        m = bpy.data.materials.new("_Farbe")
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        aus = nt.nodes.new("ShaderNodeOutputMaterial")
        b = nt.nodes.new("ShaderNodeBsdfPrincipled")
        b.inputs["Roughness"].default_value = 0.9
        e = nt.nodes.new("ShaderNodeVertexColor")
        e.layer_name = "Farbe" if "Farbe" in ca else ca[0].name
        g = nt.nodes.new("ShaderNodeGamma")
        g.inputs["Gamma"].default_value = 2.2
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = basis
        nt.links.new(e.outputs["Color"], g.inputs["Color"])
        nt.links.new(g.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        nt.links.new(b.outputs["BSDF"], aus.inputs["Surface"])
        neu.append(m)
    index = [p.material_index for p in ob.data.polygons]
    ob.data.materials.clear()
    for m in neu:
        ob.data.materials.append(m)
    for p, i in zip(ob.data.polygons, index):
        p.material_index = i


def knoten(objs, name):
    return next((o for o in objs if o.name.split(".")[0] == name), None)


def main():
    aus = OPT["aus"]
    os.makedirs(aus, exist_ok=True)
    tier_glb = OPT["tier"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    kam = zp.buehne()
    karren = importieren(OPT["karren"])
    for o in karren:
        if o.type == "MESH":
            farbmaterial(o)
    for o in karren:                                                  # der Stützbock gehört in den Stand, nicht in die Fahrt
        if o.name.startswith("Stuetze"):
            o.hide_render = True
    ank_d = knoten(karren, "Anker_Deichsel")
    ziele = [ank_d]
    zwei = "zwei" in OPT
    if zwei:
        ziele = [knoten(karren, "Anker_Pferd_L"), knoten(karren, "Anker_Pferd_R")]
    bpy.context.view_layer.update()
    tiere = []
    for k, ziel in enumerate(ziele):
        objs = importieren(os.path.join(WURZEL, "game", "assets", "modelle", "tiere", tier_glb + ".glb"))
        ske = next(o for o in objs if o.type == "ARMATURE")
        for o in objs:
            if o.type == "MESH":
                o.hide_render = o.name.startswith("Fern_")
                if o.name.startswith("Nah_"):
                    farbmaterial(o)
        anker = knoten(objs, "Anker_Geschirr")
        tiere.append((ske, anker, objs, ziel))
    ergebnis = {"tier": tier_glb, "karren": os.path.basename(OPT["karren"]), "anker_abstand_mm": {}}
    art_ = "pferd" if "pferd" in tier_glb else "ochse"
    ziehen_versatz = {"pferd": Vector((0.003, 0.003, -0.083)), "ochse": Vector((0.004, 0.007, -0.120))}[art_]   # Blender-Koordinaten: x, vor (−y), Höhe
    for k, (ske, anker, objs, ziel) in enumerate(tiere):
        # Ruhepose: Anker des Tiers an den Anker des Karrens legen
        zp.aktion_setzen(ske, "Stehen")
        bpy.context.scene.frame_set(0)
        bpy.context.view_layer.update()
        a0 = anker.matrix_world.translation.copy()
        z0 = ziel.matrix_world.translation.copy()
        ausricht = z0 - a0                                               # der Anker des Tiers liegt in jeder Bewegung fest auf der mittleren Lage im Ziehen
        ske.location += ausricht
        for o in objs:
            if o.parent is None and o is not ske:
                o.location += ausricht
        bpy.context.view_layer.update()
        a_stehen = anker.matrix_world.translation.copy()
        for clip in ("Stehen", "Gehen", "Ziehen"):
            act = zp.aktion_setzen(ske, clip)
            n = int(round(act.frame_range[1] - act.frame_range[0]))
            dmax = 0.0
            summe = Vector((0, 0, 0))
            anzahl = 0
            for i in range(0, n + 1, max(1, n // 36)):
                bpy.context.scene.frame_set(int(act.frame_range[0]) + i)
                bpy.context.view_layer.update()
                d = anker.matrix_world.translation - ziel.matrix_world.translation
                dmax = max(dmax, d.length)
                summe += d
                anzahl += 1
            ergebnis["anker_abstand_mm"][f"{k}_{clip}"] = round(dmax * 1000, 1)
            m = summe / anzahl
            ergebnis["anker_mittel_mm"] = ergebnis.get("anker_mittel_mm", {})
            ergebnis["anker_mittel_mm"][f"{k}_{clip}"] = [round(m.x * 1000), round(m.y * 1000), round(m.z * 1000)]
    # Durchdringung: kleinster Abstand der Tiernetze zum Karren (alle Netze des Karrens), über die Bilder von Gehen und Ziehen
    from mathutils import kdtree
    wagen = [o for o in karren if o.type == "MESH" and not o.hide_render]
    pts = []
    for o in wagen:
        dg = bpy.context.evaluated_depsgraph_get()
        me = o.evaluated_get(dg).to_mesh()
        pts += [o.matrix_world @ v.co for v in me.vertices]
        o.evaluated_get(dg).to_mesh_clear()
    # Zugstränge und Holme enden absichtlich am Joch bzw. Kummet: Wagenecken in 0,3 m Umkreis der Ankerpunkte zählen nicht
    pts = [q for q in pts if all((q - z.matrix_world.translation).length > 0.30 for z in ziele)]
    kd = kdtree.KDTree(len(pts))
    for i_, q in enumerate(pts):
        kd.insert(q, i_)
    kd.balance()
    mindest = {}
    for clip in ("Gehen", "Ziehen"):
        kl = 1e9
        for k, (ske, anker, objs, ziel) in enumerate(tiere):
            act = zp.aktion_setzen(ske, clip)
            n = int(round(act.frame_range[1] - act.frame_range[0]))
            nah = next(o for o in objs if o.name.startswith("Nah_"))
            for i in range(0, n + 1, max(1, n // 12)):
                bpy.context.scene.frame_set(int(act.frame_range[0]) + i)
                bpy.context.view_layer.update()
                dg = bpy.context.evaluated_depsgraph_get()
                me = nah.evaluated_get(dg).to_mesh()
                m = nah.matrix_world
                for v in list(me.vertices)[::3]:
                    kl = min(kl, kd.find(m @ v.co)[2])
                nah.evaluated_get(dg).to_mesh_clear()
        mindest[clip] = round(kl, 3)
    ergebnis["min_abstand_tier_karren_m"] = mindest
    zw = Vector((0, 0, 0))
    # Deichselwinkel: Anker_Deichsel gegen die Mitte des Wagens (Höhe des Wagenbodens 0,845 bzw. 0,62)
    ankerpos = ank_d.matrix_world.translation
    ergebnis["deichsel_anker_hoehe_m"] = round(ankerpos.z, 3)
    ergebnis["deichsel_anker_vor_m"] = round(-ankerpos.y, 3)
    if "zwei" in OPT:
        ergebnis["pferd_abstand_x_m"] = round(abs(ziele[0].matrix_world.translation.x - ziele[1].matrix_world.translation.x), 3)
    # Bilder in der Ziehen-Pose (Bild 0 und Bild halb), das zweite Pferd versetzt
    for k, (ske, anker, objs, ziel) in enumerate(tiere):
        act = zp.aktion_setzen(ske, "Ziehen")
        n = int(round(act.frame_range[1] - act.frame_range[0]))
        if k == 1:                                                    # das zweite Pferd läuft versetzt: NLA-Streifen, früher gestartet
            ske.animation_data.action = None
            spur = ske.animation_data.nla_tracks.new()
            st = spur.strips.new("Ziehen", int(-n - round(0.37 * n)), act)
            st.repeat = 6
    for ansicht, bild_nr, az, el, ziel_pt, mass in (("seite", 0, 0, 0, Vector((0, -3.7 if "pferd" in tier_glb else -2.2, 1.0)), 12.5 if "pferd" in tier_glb else 9.0), ("seite_b", 15, 0, 0, Vector((0, -3.7 if "pferd" in tier_glb else -2.2, 1.0)), 12.5 if "pferd" in tier_glb else 9.0),
                                                 ("spielkamera", 0, -135, 38, Vector((0, -2.0, 0.8)), 11.0), ("von_vorn", 0, -90, 20, Vector((0, -2.0, 1.0)), 6.0), ("von_vorn_schraeg", 0, -60, 20, Vector((0, -4.5, 1.0)), 5.0),
                                                 ("hinten_schraeg", 0, 55, 22, Vector((0, -3.2, 0.9)), 7.0),
                                                 ("von_hinten", 0, 90, 30, Vector((0, -3.2, 0.9)), 7.0)):
        act0 = bpy.data.actions["Ziehen"]
        bpy.context.scene.frame_set(int(act0.frame_range[0]) + bild_nr)
        bpy.context.view_layer.update()
        zp.aufnahme(kam, ziel_pt, az, el, True, mass, os.path.join(aus, f"{tier_glb}_gespann_{ansicht}.png"), 1100, 700)
    if "versteck" in OPT:
        for o in karren:
            if o.name.startswith(OPT["versteck"]):
                o.hide_render = True
        bpy.context.scene.frame_set(int(bpy.data.actions["Ziehen"].frame_range[0]))
        zp.aufnahme(kam, Vector((0, -2.0, 1.0)), -90, 12, True, 6.0, os.path.join(aus, "_test_vorn.png"), 1100, 700)
    if zwei:                                                          # nur das nahe Pferd (das ferne verdeckt sonst Strang und Geschirr) und Blick von hinten schräg
        for o in tiere[1][2]:
            o.hide_render = True
        bpy.context.scene.frame_set(int(bpy.data.actions["Ziehen"].frame_range[0]))
        bpy.context.view_layer.update()
        zp.aufnahme(kam, Vector((0, -6.2, 1.0)), 0, 0, True, 4.5, os.path.join(aus, f"{tier_glb}_gespann_seite_ein.png"), 1100, 700)
        zp.aufnahme(kam, Vector((0, -6.2, 1.0)), 30, 18, True, 5.0, os.path.join(aus, f"{tier_glb}_gespann_dreiviertel_ein.png"), 1100, 700)
        for o in tiere[1][2]:
            if not o.name.startswith("Fern_"):
                o.hide_render = False
    if "folge" in OPT:                                                # Bildfolge Ziehen mit rollenden Rädern und wandernden Streifen (Übersicht)
        meta = json.load(open(os.path.join(WURZEL, "game", "daten", "zugtiere.json"), encoding="utf-8"))["tiere"]["pferd" if "pferd" in tier_glb else "ochse"]
        weg = meta["klips"]["Ziehen"]["weg"]
        ordner = os.path.join(aus, tier_glb + "_gespann", "Ziehen_mit_Karren")
        os.makedirs(ordner, exist_ok=True)
        raeder = [o for o in karren if o.name.split(".")[0].startswith("Rad_")]
        act0 = bpy.data.actions["Ziehen"]
        n0 = int(round(act0.frame_range[1] - act0.frame_range[0]))
        for i in range(0, n0 + 1, 3):
            bpy.context.scene.frame_set(int(act0.frame_range[0]) + i)
            x = weg[min(i, len(weg) - 1)]
            for o in bpy.data.objects:
                if o.name.startswith("_Streifen"):
                    o["y0"] = o.get("y0", o.location.y)
                    o.location.y = ((o["y0"] + x + 6.0) % 12.0) - 6.0
            for o in raeder:
                r = float(o.get("radius", 0.6))
                o.rotation_mode = "XYZ"
                o.rotation_euler[0] = x / r
            bpy.context.view_layer.update()
            zp.aufnahme(kam, Vector((0, -3.7 if "pferd" in tier_glb else -2.2, 1.0)), 0, 0, True, 12.5 if "pferd" in tier_glb else 9.0, os.path.join(ordner, f"seite_{i:03d}.png"), 900, 520)
            zp.aufnahme(kam, Vector((0, -2.0, 0.8)), -135, 38, True, 11.0, os.path.join(ordner, f"spiel_{i:03d}.png"), 900, 520)
    json.dump(ergebnis, open(os.path.join(aus, f"{tier_glb}_gespann.json"), "w", encoding="utf-8"), indent=1)
    print("Gespann:", ergebnis)


main()
