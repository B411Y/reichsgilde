"""Faust (oder Pose) aus Parameterdatei in mehreren Ansichten rendern. blender -b hand.blend --python v5_ansichten.py -- <params.json> <ausprefix> [px] [pose=faust|zeigen]"""
import bpy, sys, os, json
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v6_rig_lib as L, v6_posen
a = sys.argv[sys.argv.index("--") + 1:]; pfad, pre = a[0], a[1]; px = int(a[2]) if len(a) > 2 else 600; was = a[3] if len(a) > 3 else "faust"
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; par = json.load(open(pfad))
P = v6_posen.posen(rig, par); alle = [b.name for b in rig.data.bones]
L.pose_anwenden(rig, P[was]); bpy.context.view_layer.update()
cam = L.buehne(px)
for an in ("oben", "seite", "schraeg", "vorn", "unten", "daumen", "unten_schraeg"): L.ansicht_rendern(cam, an, f"{pre}_{an}.png")
