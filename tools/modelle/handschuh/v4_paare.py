"""Handschuh in den Video-Phasen rendern (oben und unten). blender -b handschuh_pixal3d_v4.blend --python v4_paare.py -- <ausordner>"""
import bpy, sys, os
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_rig_lib as L, v2_posen
from v2_rig_lib import AX_X, AX_Y, AX_Z
aus = sys.argv[sys.argv.index("--") + 1]; os.makedirs(aus, exist_ok=True)
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
rig.animation_data.action = None
P = v2_posen.posen(rig); alle = [b.name for b in rig.data.bones]
for k in P:
    P[k] = {n: P[k].get(n, [(AX_X, 0.0)]) for n in alle}
for n in alle:
    ln = max(len(P[k][n]) for k in P)
    for k in P:
        while len(P[k][n]) < ln: P[k][n].append((P[k][n][0][0], 0.0))
for k in P: P[k]["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]
for o in bpy.data.objects:
    if o.name.startswith("Hilfe"): o.hide_render = True
cam = L.buehne(420)
for i in range(0, 20):
    tv = 0.8 + 0.1 * i; t = max(0.0, min(1.0, (tv - 1.35) / 1.05))
    L.pose_anwenden(rig, L.gestaffelt(P["ruhe"], P["faust"], t, True)); bpy.context.view_layer.update()
    for a in ("oben", "unten"): L.ansicht_rendern(cam, a, os.path.join(aus, f"t{tv:.1f}_{a}.png"))
print("PAARE fertig")
