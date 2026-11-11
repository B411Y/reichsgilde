"""Faust-Pose v4: Winkel aus den Videos (Basis), nur kleine Abweichungen (+-12 Grad) zur Vermeidung von Durchdringung; Daumen per Suche."""
import bpy, sys, os, json, copy, random
import numpy as np
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v6_rig_lib as L, v6_posen as v2_posen
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; gl = bpy.data.objects["Handschuh"]
# Basis aus dem Video (Schaetzung, +-15 Grad): MCP, PIP, DIP, Kipp (Neigung zur Daumenseite)
BASIS = {"2": [85, 108, 62, 0], "3": [90, 108, 62, 2], "4": [95, 110, 64, 4], "5": [98, 112, 68, 6]}
a_ = sys.argv[sys.argv.index("--") + 1:]; par = json.load(open(a_[0])); PAUS = a_[1]; random.seed(int(a_[2]) if len(a_) > 2 else 5)
for k_ in "2345":
    while len(par["f"][k_]) < 5: par["f"][k_].append(0.0)
def wpos(pb, was): return np.array((rig.matrix_world @ getattr(rig.pose.bones[pb], was))[:])
def pose(p):
    L.pose_anwenden(rig, v2_posen.faust_aus(rig, p)); bpy.context.view_layer.update()
def mesh_kosten(p, gd=3.0):
    pose(p); dg = bpy.context.evaluated_depsgraph_get(); z = v2_lib.selbstschnitt_zerlegt(gl, dg); inter = 0
    for (a, b), n in z.items():
        if a == b or (a == "H" and b == "H"): continue
        inter += (gd if "D" in (a, b) else 3.0) * n
    return inter, z
def kostA(p):
    inter, z = mesh_kosten(p, 0.0)
    dev = sum(abs(p["f"][k][i] - BASIS[k][i]) for k in BASIS for i in range(3))
    pose(p); dg = bpy.context.evaluated_depsgraph_get(); me = gl.evaluated_get(dg).to_mesh(); V = np.array([v.co[:] for v in me.vertices]); pen = 0.0
    for k in (2, 3, 4, 5):
        pen += max(0.0, np.linalg.norm(V - wpos(f"finger{k}-3.R", "tail"), axis=1).min() - 0.004)
    return inter + 3.0 * dev + 3000 * pen
best = kostA(par); print("A START", best, flush=True)
for rd in range(0):
    for k in "2345":
        for i, st in ((0, 3), (1, 3), (2, 3), (3, 2)):
            lo, hi = (BASIS[k][i] - 12, BASIS[k][i] + 12) if i < 3 else (-6, 12)
            for dv in (-st, st):
                q = copy.deepcopy(par); v = q["f"][k][i] + dv
                if v < lo or v > hi: continue
                q["f"][k][i] = v; c = kostA(q)
                if c < best: par, best = q, c
    print("A RUNDE", rd, best, flush=True)
for hv in ():
    q = copy.deepcopy(par); q["hand"] = hv; c = kostA(q)
    if c < best: par, best = q, c
print("A hand", par["hand"], flush=True)
pose(par)
m2 = 0.5 * (wpos("finger2-2.R", "head") + wpos("finger2-2.R", "tail")); m3 = 0.5 * (wpos("finger3-2.R", "head") + wpos("finger3-2.R", "tail"))
T = 0.5 * (m2 + m3) + np.array([0, 0.0, -0.014]); print("ZIEL", np.round(T, 3))
def fk_dt(p): pose(p); return float(np.linalg.norm(wpos("finger1-3.R", "tail") - T))
kand = []
for s in range(5000):
    q = copy.deepcopy(par)
    q["d"] = {"swing": random.uniform(-100, 30), "flex": [random.uniform(-10, 75), random.uniform(-10, 90), random.uniform(-10, 80)], "dreh": [random.uniform(-70, 70), random.uniform(-50, 50), random.uniform(-50, 50)]}
    kand.append((fk_dt(q), q))
kand.sort(key=lambda t: t[0]); bestB = None
for dt, q in kand[:120]:
    inter, z = mesh_kosten(q, 3.0); c = inter + 3e6 * dt * dt
    if bestB is None or c < bestB[0]: bestB = (c, q, dt, inter)
print("B Wahl", bestB[0], "dt", round(bestB[2], 4), flush=True)
par = bestB[1]; best = bestB[0]
GRD = {("swing",): (-110, 40, 4)}
for i in range(3): GRD[("flex", i)] = (-15, 90, 3); GRD[("dreh", i)] = (-80, 80, 4)
def getd(p, key): return p["d"]["swing"] if key[0] == "swing" else p["d"][key[0]][key[1]]
def putd(p, key, v):
    if key[0] == "swing": p["d"]["swing"] = v
    else: p["d"][key[0]][key[1]] = v
for rd in range(3):
    for key, (lo, hi, st) in GRD.items():
        for dv in (-st, st):
            q = copy.deepcopy(par); v = getd(q, key) + dv
            if v < lo or v > hi: continue
            putd(q, key, v); inter, z = mesh_kosten(q, 3.0); dt = fk_dt(q); c = inter + 3e6 * dt * dt
            if c < best: par, best = q, c
    print("B RUNDE", rd, best, flush=True)
json.dump(par, open(PAUS, "w"), indent=1)
inter, z = mesh_kosten(par); print("ENDE", inter, z); print("PAR", json.dumps(par))
