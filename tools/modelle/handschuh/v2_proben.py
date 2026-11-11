"""Posen probeweise rendern + messen. blender -b <blend> --python v2_proben.py -- <ausordner> [pose,pose,...]"""
import bpy, sys, os, math, importlib
import numpy as np
from mathutils import Vector
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v2_rig_lib as L, v2_posen
importlib.reload(v2_posen)
aus = sys.argv[sys.argv.index("--") + 1]
namen = (sys.argv[sys.argv.index("--") + 2] if len(sys.argv) > sys.argv.index("--") + 2 else "ruhe,zeigen,faust").split(",")
rig = bpy.data.objects["Handschuh_Geruest"] if "Handschuh_Geruest" in bpy.data.objects else [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
gl = bpy.data.objects["Handschuh"]
cam = L.buehne(384)
ANS = os.environ.get("ANS","oben,seite,schraeg").split(",")
P = v2_posen.posen(rig)
os.makedirs(aus, exist_ok=True)
for n in namen:
    L.pose_anwenden(rig, P[n]); bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    print("POSE", n, "selbstschnitt", v2_lib.selbstschnitt(gl, dg), v2_lib.selbstschnitt_zerlegt(gl, dg))
    # Fingerspitzen: Abstand der Knochenenden zur Handflaeche (naechste Ecke einer Gruppe wrist/metacarpal/finger1)
    me = gl.evaluated_get(dg).to_mesh(); V = np.array([v.co[:] for v in me.vertices])
    for k in (2, 3, 4, 5):
        pb = rig.pose.bones[f"finger{k}-3.R"]; tip = np.array((rig.matrix_world @ pb.tail)[:])
        dd = np.linalg.norm(V - tip, axis=1)
        print("  Spitze", k, "Lage", np.round(tip, 3), "naechste Ecke", round(float(dd.min()), 4))
    pb = rig.pose.bones["finger1-3.R"]; print("  Daumenspitze", np.round(np.array((rig.matrix_world @ pb.tail)[:]), 3))
    for b_ in ("finger3-1.R","finger3-2.R","finger3-3.R"):
        pb = rig.pose.bones[b_]; print("  ", b_, np.round(np.array((rig.matrix_world @ pb.head)[:]),3), "->", np.round(np.array((rig.matrix_world @ pb.tail)[:]),3))
    for a in ANS:
        L.ansicht_rendern(cam, a, os.path.join(aus, f"{n}_{a}.png"))



