"""Prüft die Fingerknochen der Passanten (Nutzer 14.11.2026, „Finger C“): Durchdringung und Nahbilder der Hand.

    blender -b --factory-startup --python tools/modelle/passanten_finger_pruefen.py -- \
        --ordner=<Ordner mit passant_probe*.glb> [--nur=mann,frau] [--netz=Nah_cotte] [--bilder=<Ordner>] [--bericht=<datei.json>]

Durchdringung: Das gehäutete Nah-Netz wird im Mittelbild jedes Clips ausgewertet; Dreiecke, die sich schneiden (BVH-Überlappung, ohne
Nachbarn mit gemeinsamer Ecke), werden nach ihrem Hauptknochen gezählt: Finger/Finger (zwei verschiedene Finger), Finger/Hand
(Finger gegen Handfläche oder Handrücken). Dieselbe Zählung in der Ruhelage ist der Bezug (Durchdringungen, die das Netz schon hat).
Bild: rechte Hand nah, Netz halb durchsichtig, Gelenke der Fingerknochen als Kugeln, Ruhelage; dazu die Hand in einem Clip.
"""

import importlib.util
import json
import math
import os
import sys
import tempfile

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HIER = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("passanten_clips", os.path.join(HIER, "passanten_clips.py"))
C = importlib.util.module_from_spec(_spec)
sys.modules["passanten_clips"] = C
_spec.loader.exec_module(C)
P = C.P
OPT = P.OPT

FINGER = ("Thumb", "Index", "Middle", "Ring", "Little")


def finger_von(name):
    """'R:Index' für einen Fingerknochen, 'R:Hand' für die Hand, sonst None."""
    for s, k in (("Left", "L"), ("Right", "R")):
        if name == f"{s}Hand":
            return f"{k}:Hand"
        for f in FINGER:
            if name.startswith(s + f):
                return f"{k}:{f}"
    return None


def zaehlen(ob):
    """Schnittpaare im ausgewerteten Netz: (Finger/Finger, Finger/Hand)."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    lagen = [ob.matrix_world @ v.co for v in me.vertices]
    flaechen = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    gruppen = {g.index: g.name for g in ob.vertex_groups}
    teil_ecke = []
    for v in ob.data.vertices:
        beste = max(v.groups, key=lambda e: e.weight, default=None)
        teil_ecke.append(finger_von(gruppen[beste.group]) if beste else None)
    teil = []
    for f in flaechen:
        t = [teil_ecke[i] for i in f]
        teil.append(max(set(t), key=t.count) if any(t) else None)
    baum = BVHTree.FromPolygons(lagen, flaechen, all_triangles=False)
    ff = fh = 0
    for a, b in baum.overlap(baum):
        if a >= b or set(flaechen[a]) & set(flaechen[b]):
            continue
        ta, tb = teil[a], teil[b]
        if not ta or not tb or ta[0] != tb[0]:
            continue
        fa, fb = ta[2:] != "Hand", tb[2:] != "Hand"
        if fa and fb and ta != tb:
            ff += 1
        elif fa != fb:
            fh += 1
    return ff, fh


def hand_bild(rig, ob, seite, pfad, titel_aktion=None, bild=0):
    sc = bpy.context.scene
    for o in bpy.data.objects:
        if o.type == "MESH" and o is not ob and not o.name.startswith("Gelenk"):
            o.hide_render = True
    ob.hide_render = False
    ob.color = (0.85, 0.70, 0.58, 0.45)
    if titel_aktion is not None:
        P.aktion_setzen(rig, titel_aktion)
        sc.frame_set(int(titel_aktion.frame_range[0]) + bild)
    else:
        rig.animation_data.action = None
        for pb in rig.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
        sc.frame_set(1)
    bpy.context.view_layer.update()
    for o in [o for o in bpy.data.objects if o.name.startswith("Gelenk")]:
        bpy.data.objects.remove(o)
    S = "Left" if seite == "L" else "Right"
    punkte = []
    for pb in rig.pose.bones:
        t = finger_von(pb.name)
        if t and t[0] == seite:
            for p, r in ((pb.head, 0.006), (pb.tail, 0.004)):
                w = rig.matrix_world @ p
                punkte.append(w)
                bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=w, segments=10, ring_count=6)
                k = bpy.context.active_object
                k.name = "Gelenk"
                k.color = (0.75, 0.10, 0.10, 1) if t[2:] != "Hand" else (0.1, 0.1, 0.7, 1)
    mitte = sum(punkte, Vector()) / len(punkte)
    sh = sc.display.shading
    sh.light, sh.color_type = "STUDIO", "OBJECT"
    sh.show_xray = False
    ob.show_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 420
    hand = rig.pose.bones[f"{S}Hand"]
    quer = (rig.matrix_world.to_3x3() @ hand.matrix.to_3x3() @ Vector((0, 0, 1))).normalized()
    C.kamera_setzen(sc, tuple(mitte + quer * 2.0 + Vector((0, -0.6, 0.4))), tuple(mitte), 0.22)
    sc.render.filepath = pfad
    bpy.ops.render.render(write_still=True)


def art_pruefen(art):
    cfg = P.FIGUREN[art]
    C.szene_leeren()
    rig, netze = C.figur_laden(os.path.join(OPT["ordner"], cfg["datei"]))
    C.vorschau_vorbereiten(rig, netze, cfg)
    for spur in rig.animation_data.nla_tracks:
        spur.mute = True
    name = OPT.get("netz", f"Nah_{cfg['varianten'][0]}")
    ob = netze[name]
    erg = {"netz": name, "knochen": len(rig.data.bones), "dreiecke": {n: sum(len(p.vertices) - 2 for p in o.data.polygons)
                                                                         for n, o in netze.items() if n.startswith("Nah_")}}
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    erg["ruhelage"] = zaehlen(ob)
    clips = {}
    for act in [a for a in bpy.data.actions]:
        n = int(act.frame_range[1] - act.frame_range[0])
        P.aktion_setzen(rig, act)
        bpy.context.scene.frame_set(int(act.frame_range[0]) + n // 2)
        bpy.context.view_layer.update()
        clips[act.name.split(".")[0]] = zaehlen(ob)
    rig.animation_data.action = None
    erg["clips_mittelbild"] = clips
    if "bilder" in OPT:
        os.makedirs(OPT["bilder"], exist_ok=True)
        hand_bild(rig, ob, "R", os.path.join(OPT["bilder"], f"hand_{art}_ruhe.png"))
        geh = next((a for a in bpy.data.actions if a.name.split(".")[0] == "Gehen"), None)
        if geh is not None:
            hand_bild(rig, ob, "R", os.path.join(OPT["bilder"], f"hand_{art}_gehen.png"), geh, 8)
    return erg


def hauptteil():
    arten = [a for a in P.FIGUREN if a in OPT.get("nur", ",".join(P.FIGUREN)).split(",")]
    bericht = {a: art_pruefen(a) for a in arten}
    if "bericht" in OPT:
        with open(OPT["bericht"], "w", encoding="utf-8") as f:
            json.dump(bericht, f, indent=1, ensure_ascii=False)
    print("BERICHT", json.dumps(bericht, ensure_ascii=False))


if __name__ == "__main__":
    hauptteil()
