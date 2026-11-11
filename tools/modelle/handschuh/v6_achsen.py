"""Ansicht von der Handflaeche (unten): Faust-Pose aus Parameterdatei, Fingerlinien messen und einzeichnen.
blender -b hand.blend --python v5_achsen.py -- <posen_modul> <params.json|-> <ausprefix> [px]"""
import bpy, sys, os, json, importlib
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_rig_lib as L, v6_achsen_lib as AL
a = sys.argv[sys.argv.index("--") + 1:]; modul, pfad, pre = a[0], a[1], a[2]; px = int(a[3]) if len(a) > 3 else 800
posen = importlib.import_module(modul)
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
par = json.load(open(pfad)) if pfad != "-" else posen.parameter()
L.pose_anwenden(rig, posen.faust_aus(rig, par)); bpy.context.view_layer.update()
cam = L.buehne(px)
res = {}
for ansicht in ("unten", "oben"):
    L.ansicht_rendern(cam, ansicht, pre + f"_{ansicht}.png")
    res[ansicht] = {r: AL.messen(rig, cam, px, r) for r in ("pip", "mcp")}
W, S = AL.hand_winkel(rig); res["hand"] = {"winkel": {k: round(v, 1) for k, v in W.items()}, "schnitt_xy_m": [round(float(x), 4) for x in S]}
W2, S2 = AL.hand_winkel(rig, "mcp"); res["hand_mcp"] = {"winkel": {k: round(v, 1) for k, v in W2.items()}}
json.dump(res, open(pre + "_achsen.json", "w"), indent=1)
print("ACHSEN", {r: {k: round(v, 1) for k, v in res["unten"][r]["rel_zeige"].items()} for r in ("pip", "mcp")}, "S", [round(x) for x in res["unten"]["pip"]["schnitt"]], res["hand"])
