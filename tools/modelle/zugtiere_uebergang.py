"""Bildstreifen des Übergangs Gehen -> Ziehen (und zurück) mit Überblenden, auch für zwei Tiere mit Phasenversatz (Zweispänner).

    blender -b --python tools/modelle/zugtiere_uebergang.py -- --glb=tier_pferd_rotbraun --aus=<ordner> [--zwei] [--blend=0.4]

Das Spiel überblendet so: derselbe Abspielzeitpunkt in beiden Clips (Phase gleich), die Knochenstellungen werden mit dem Gewicht w(t) gemischt
(Verschiebung linear, Drehung kugelförmig). Hier wird das nachgebaut und Bild für Bild gezeichnet: <aus>/<glb>/Uebergang_Gehen_Ziehen[_zwei]/seite_NNN.png
und spiel_NNN.png (Kontaktbogen und GIF danach mit zugtiere_bogen.py). Mit --zwei laufen zwei Tiere nebeneinander (x = ±0,62 m), das zweite um 0,37
Schleifen versetzt, beide blenden zur gleichen Zeit; ein Fehler im Phasenabgleich würde sich als ineinander greifende Hufe zeigen.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OPT = dict(a[2:].split("=", 1) if "=" in a else (a[2:], "1") for a in ARGS)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import zugtiere_pruefen as zp  # noqa: E402

FPS = 24


def pose_lesen(ske, clip, frame):
    act = zp.aktion_setzen(ske, clip)
    bpy.context.scene.frame_set(int(act.frame_range[0]) + frame)
    bpy.context.view_layer.update()
    r = {}
    for pb in ske.pose.bones:
        loc, q, _ = pb.matrix_basis.decompose()
        r[pb.name] = (loc.copy(), q.copy())
    return r


def gemischt(a, b, w):
    out = {}
    for n in a:
        qa, qb = a[n][1], b[n][1]
        if qa.dot(qb) < 0:
            qb = -qb
        out[n] = (a[n][0].lerp(b[n][0], w), qa.slerp(qb, w))
    return out


def setzen(ske, pose):
    ske.animation_data.action = None
    for pb in ske.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location, pb.rotation_quaternion = pose[pb.name]


def eckenwelt(o):
    dg = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(dg)
    me = e.to_mesh()
    m = o.matrix_world
    pts = [m @ v.co for v in me.vertices][::5]
    e.to_mesh_clear()
    return pts


def main():
    glb = OPT["glb"]
    zwei = "zwei" in OPT
    blend = float(OPT.get("blend", 0.6 if "ochse" in glb else 0.4))
    aus = os.path.join(OPT["aus"], glb, "Uebergang_Gehen_Ziehen" + ("_zwei" if zwei else ""))
    os.makedirs(aus, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    kam = zp.buehne()
    tiere = []
    for k in range(2 if zwei else 1):
        vorher = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=os.path.join(zp.WURZEL, "game", "assets", "modelle", "tiere", glb + ".glb"))
        neu = [o for o in bpy.data.objects if o not in vorher]
        ske = next(o for o in neu if o.type == "ARMATURE")
        for o in neu:
            if o.type == "MESH" and o.name.startswith("Fern_"):
                o.hide_render = True
            if o.type == "MESH" and o.name.startswith("Nah_"):
                zp.farbmaterial_fuer(o)
        if zwei:
            ske.location.x = (0.62, -0.62)[k]
        tiere.append(ske)
    dauer = 1.25 if "pferd" in glb else 1.5
    n = int(round(dauer * FPS))
    gesamt = blend + 1.2                                              # 0,6 s Gehen, Überblenden, Rest Ziehen
    bilder = 10
    mindest = 1e9
    nahe = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Nah_")]
    for i in range(bilder):
        t = i * gesamt / (bilder - 1)
        w = max(0.0, min(1.0, (t - 0.4) / blend))
        w = w * w * (3 - 2 * w)
        for k, ske in enumerate(tiere):
            versatz = float(OPT.get("versatz", 0.5)) * n if k == 1 else 0.0
            f = int(round(t * FPS + versatz)) % n
            a = pose_lesen(ske, "Gehen", f)
            b = pose_lesen(ske, "Ziehen", f)
            setzen(ske, gemischt(a, b, w))
        bpy.context.view_layer.update()
        if len(nahe) == 2:
            a, b = eckenwelt(nahe[0]), eckenwelt(nahe[1])
            mindest = min(mindest, min((p - q).length for p in a for q in b))
        zp.aufnahme(kam, Vector((0, 0.0, 0.8)), 0, 0, True, 5.2 if "pferd" in glb else 4.4, os.path.join(aus, f"seite_{i:03d}.png"))
        zp.aufnahme(kam, Vector((0, -0.3, 0.7)), -135, 38, True, 6.0 if "pferd" in glb else 5.2, os.path.join(aus, f"spiel_{i:03d}.png"))
        zp.aufnahme(kam, Vector((0, 0.0, 0.0)), 0, 90, True, 4.2, os.path.join(aus, f"oben_{i:03d}.png"))
    if len(nahe) == 2:
        print("Kleinster Abstand zwischen den beiden Pferden im Streifen (m):", round(mindest, 3))
        open(os.path.join(aus, "abstand.txt"), "w", encoding="utf-8").write(f"{mindest:.3f}\n")
    print("Übergang:", aus)


main()
