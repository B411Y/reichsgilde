"""Bibliothek fuer die Haltung "Text": Posen, Atmen, Messung. Aufgerufen von v6_text.py und v6_opt_text.py (innerhalb Blenders)."""
import bpy, sys, os, math, json
import numpy as np
from mathutils import Vector, Matrix
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_lib, v6_rig_lib as L, v6_posen as v2_posen
from v6_rig_lib import AX_X, AX_Y, AX_Z

PARAM = {"f": {"2": [-2, -1, 0, 0.0], "3": [-2, -1, 0, 0.0], "4": [-2, -1, 0, 0.0], "5": [-2, -1, 0, 0.0]},
         "daumen": {"swing": 0.0, "flex": [0, 0, 0], "dreh": [0, 0, 0]},
         "mc": [0, 0, 0, 0], "rollen": -90.0, "wurzel": [0.0, 0.0, 0.0], "gegenhand": 0.0}
if os.environ.get("TEXT"):
    PARAM.update(json.loads(os.environ["TEXT"]))
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
gl = bpy.data.objects["Handschuh"]
bpy.context.view_layer.update()
alle = [b.name for b in rig.data.bones]
if rig.animation_data: rig.animation_data.action = None   # sonst ueberschreibt die gespeicherte Aktion (Ruhe) jede handgesetzte Pose
#------------- Posen (wie v6_final: alle Knochen, gleiche Listenlaengen, Wurzel mit drei Achsen)
P = v2_posen.posen(rig)
def voll(d): return {n: d.get(n, [(AX_X, 0.0)]) for n in alle}
for k in P: P[k] = voll(P[k])
for n in alle:
    ln = max(len(P[k][n]) for k in P)
    for k in P:
        while len(P[k][n]) < ln: P[k][n].append((P[k][n][0][0], 0.0))
for k in P: P[k]["Wurzel"] = [(-AX_X, 0.0), (AX_Z, 0.0), (AX_Y, 0.0)]

# Mittelhandknochen: gleiche Achse (Beugen zur Handflaeche) in allen Posen, damit sich die Listen mischen lassen
for k in P:
    for i in (1, 2, 3, 4):
        P[k][f"metacarpal{i}.R"] = [(L.flexachse(rig, f"metacarpal{i}.R"), 0.0)]

def text_pose(PARAM=PARAM):
    d = {k: list(v) for k, v in P["ruhe"].items()}
    for k in (2, 3, 4, 5):
        a = PARAM["f"][str(k)]
        L.finger_pose(rig, d, k, a[:3], a[3], a[4] if len(a) > 4 else 0.0)
        d[f"Knoechel{k}.R"] = [(AX_X, max(0.0, d[f"finger{k}-1.R"][2][1]))]
    dm = PARAM["daumen"]
    L.daumen_pose(rig, d, swing=dm["swing"], flex=dm["flex"], dreh=dm["dreh"])
    for i, w in enumerate(PARAM.get("mc", [0, 0, 0, 0]), 1):   # Mittelhand flachlegen: Knoechelreihe liegt hintereinander in der Blickrichtung
        d[f"metacarpal{i}.R"] = [(L.flexachse(rig, f"metacarpal{i}.R"), w)]
    d["wrist.R"] = [(-AX_X, PARAM["gegenhand"]), (AX_Y, PARAM["rollen"])]
    d["Wurzel"] = [(-AX_X, PARAM["wurzel"][0]), (AX_Z, PARAM["wurzel"][1]), (AX_Y, PARAM["wurzel"][2])]
    return d
def mit_rollachse(d):   # Handgelenk bekommt in allen Posen die zweite Achse (Rollen), damit sich Listen mischen lassen
    d = {k: list(v) for k, v in d.items()}
    w = d["wrist.R"]
    if len(w) < 2: d["wrist.R"] = w + [(AX_Y, 0.0)]
    return d
PT = text_pose(PARAM); PR = mit_rollachse(P["ruhe"]); PZ = mit_rollachse(P["zeigen"])

def atmen(basis, t, amp=1.0):    # wie v6_final, hier mit Handgelenk-Rollen unveraendert
    w = 2 * math.pi * t
    d = {k: [(ax, a) for (ax, a) in v] for k, v in basis.items()}
    d["Wurzel"] = [(-AX_X, basis["Wurzel"][0][1] + 1.6 * amp * math.sin(w)), (AX_Z, basis["Wurzel"][1][1] + 2.2 * amp * math.sin(w + 1.2)), (AX_Y, basis["Wurzel"][2][1] + 1.4 * amp * math.sin(w + 2.4))]
    wr = d["wrist.R"]; d["wrist.R"] = [(wr[0][0], wr[0][1] + 1.5 * amp * math.sin(w + 0.8))] + wr[1:]
    for k in range(2, 6):
        f1 = d[f"finger{k}-1.R"]; d[f"finger{k}-1.R"] = f1[:2] + [(f1[2][0], f1[2][1] + 2.0 * amp * math.sin(w + 0.35 * k))]
        f2 = d[f"finger{k}-2.R"]; d[f"finger{k}-2.R"] = [(f2[0][0], f2[0][1] + 2.5 * amp * math.sin(w + 0.35 * k + 0.6))] + f2[1:]
    return d


def messen_pose(d):
    L.pose_anwenden(rig, d); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    z = v2_lib.selbstschnitt_zerlegt(gl, dg)
    inter = sum(n for (a, b), n in z.items() if a != b and not (a == "H" and b == "H"))
    return inter, z

def hand_punkte(d):
    """Weltkoordinaten der Netzecken in Pose d."""
    L.pose_anwenden(rig, d); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get()
    me = gl.evaluated_get(dg).to_mesh()
    return np.array([(gl.matrix_world @ v.co)[:] for v in me.vertices])
