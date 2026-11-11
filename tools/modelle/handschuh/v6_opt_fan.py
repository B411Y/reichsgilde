"""Stufe 1: Adduktion (Kipp) und Rollung je Finger so suchen, dass die Fingerreihen wie in der Skizze zusammenlaufen (Zielwinkel), dann Durchdringung mitzaehlen.
blender -b hand.blend --python v5_opt_fan.py -- <params_in.json> <params_out.json>"""
import bpy, sys, os, json, copy, random
import numpy as np
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v6_rig_lib as L, v6_posen, v6_achsen_lib as AL
a = sys.argv[sys.argv.index("--") + 1:]; pin, pout = a[0], a[1]; GEW_D = float(a[2]) if len(a) > 2 else 0.0
random.seed(11)
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; gl = bpy.data.objects["Handschuh"]
par = json.load(open(pin))
for k in "2345":
    while len(par["f"][k]) < 5: par["f"][k].append(0.0)
ZIEL = {2: -3.9, 3: 9.0, 4: 26.0, 5: 36.0}; GW = {2: 3.0, 3: 30.0, 4: 3.0, 5: 3.0}     # Neigung der Reihe relativ zum Zeigefinger (Skizze des Nutzers: -3/+9/+24/+38 Grad)
def pose(p): L.pose_anwenden(rig, v6_posen.faust_aus(rig, p)); bpy.context.view_layer.update()
def pen(p):
    pose(p); dg = bpy.context.evaluated_depsgraph_get(); z = v2_lib.selbstschnitt_zerlegt(gl, dg)
    return sum((3.0 if "D" not in (a, b) else 3.0) * n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H") and {a, b} != {"F5", "H"})
def kost(p):
    pose(p); W, S = AL.hand_winkel(rig); rel = {k: W[k] for k in W}
    c = sum(GW[k] * (rel[k] - ZIEL[k]) ** 2 for k in (2, 3, 4, 5))
    dev = sum(abs(p["f"][k][i] - par0["f"][k][i]) for k in "2345" for i in range(3))
    c += 2.0 * dev
    if GEW_D > 0: c += GEW_D * pen(p)
    c += 4000 * spitze_palm(p)
    return c
Hidx = [i for i, v in enumerate(gl.data.vertices) if v.groups and gl.vertex_groups[max(v.groups, key=lambda g: g.weight).group].name.startswith(("wrist", "metacarpal"))]
def spitze_palm(p):
    pose(p); dg = bpy.context.evaluated_depsgraph_get(); me = gl.evaluated_get(dg).to_mesh(); V = np.array([v.co[:] for v in me.vertices])[Hidx]; s = 0.0
    for k, lim, wk in ((3, 0.006, 1.0), (4, 0.0075, 2.0), (5, 0.0045, 6.0)):     # v6: kleiner Finger (und Ring) tiefer in die Handflaeche
        t = np.array((rig.matrix_world @ rig.pose.bones[f"finger{k}-3.R"].tail)[:]); s += wk * max(0.0, np.linalg.norm(V - t, axis=1).min() - lim)
    return s
par0 = copy.deepcopy(par); best = kost(par); print("START", round(best, 1), flush=True)
for rd in range(int(a[3]) if len(a) > 3 else 8):
    for k in "2345":
        for i, st in ((3, 1.5), (4, 3.0), (0, 2), (1, 2), (2, 2)):
            for dv in (-st, st):
                q = copy.deepcopy(par); q["f"][k][i] += dv
                if i < 3 and abs(q["f"][k][i] - par0["f"][k][i]) > 28: continue
                c = kost(q)
                if c < best: par, best = q, c
    pose(par); W, S = AL.hand_winkel(rig); print("RUNDE", rd, round(best, 1), {k: round(W[k], 1) for k in W}, flush=True)
json.dump(par, open(pout, "w"), indent=1)
pose(par); W, S = AL.hand_winkel(rig); print("FAN", {k: round(W[k], 1) for k in W}, "S", np.round(S, 3), "PEN", pen(par)); print("PAR", json.dumps(par["f"]))
pose(par); dg = bpy.context.evaluated_depsgraph_get(); me = gl.evaluated_get(dg).to_mesh(); VV = np.array([v.co[:] for v in me.vertices])[Hidx]
print("SPITZE_PALM_MM", {k: round(float(np.linalg.norm(VV - np.array((rig.matrix_world @ rig.pose.bones[f"finger{k}-3.R"].tail)[:]), axis=1).min()) * 1000, 1) for k in (2, 3, 4, 5)})
