"""Sucht die Griffpose um die Klinke: Finger und Daumen um die Griffstange, Lage des Schlosses relativ zur Hand, wenig Durchdringung.
blender -b handschuh_pixal3d_v6_text.blend --python v6_opt_klinke.py -- <params_aus.json> [Runden] [Seed] [params_start.json]
Umgebung: TUERSCHLOSS = tuerschloss.blend. Kosten (in mm-Einheiten): Dreieckspaare Hand-Schloss + Abstand der Fingerspitzen zur Griffachse + Selbstdurchdringung."""
import sys, os, json, random, copy, math
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import numpy as np
import v6_klinke_lib as K
from v6_klinke_lib import rig, P
args = sys.argv[sys.argv.index("--") + 1:]
aus = args[0]; runden = int(args[1]) if len(args) > 1 else 6; random.seed(int(args[2]) if len(args) > 2 else 1)
start = args[3] if len(args) > 3 else None
S = K.schloss_laden()
GW = float(os.environ.get("GW_PAAR", "0.25")); GT = float(os.environ.get("GW_SPITZE", "1.0")); GS = 0.05

faust = json.load(open(os.path.join(HIER, "faust_params.json")))
def basis():
    return {"f": {k: [v[0] * 0.8, v[1] * 0.8, v[2] * 0.8] + list(v[3:5]) for k, v in faust["f"].items()},
            "daumen": {"swing": faust["d"]["swing"], "flex": [v * 0.6 for v in faust["d"]["flex"]], "dreh": [v * 0.6 for v in faust["d"]["dreh"]]},
            "hand": 8.0, "G": [0.027, 0.05, -0.03], "psi": 0.0}
def zu_liste(p):
    f = p["f"]; dm = p["daumen"]
    return [f[str(k)][i] for k in (2, 3, 4, 5) for i in range(3)] + [dm["swing"]] + dm["flex"] + dm["dreh"] + [p["hand"]] + list(p["G"]) + [p["psi"]]
def aus_liste(x, b):
    p = copy.deepcopy(b)
    for n, k in enumerate((2, 3, 4, 5)): p["f"][str(k)] = [x[3 * n + i] for i in range(3)] + list(b["f"][str(k)][3:5])
    p["daumen"] = {"swing": x[12], "flex": x[13:16], "dreh": x[16:19]}; p["hand"] = x[19]; p["G"] = x[20:23]; p["psi"] = x[23]
    return p
GRENZ = ([(10, 100), (10, 115), (0, 95)] * 4 + [(-40, 70), (-15, 95), (-15, 95), (-15, 95), (-70, 70), (-70, 70), (-70, 70), (-12, 30),
          (-0.01, 0.07), (0.0, 0.10), (-0.08, 0.04), (-25, 25)])
def kosten(x, b, detail=False):
    p = aus_liste(x, b); d = K.griff_pose(p); K.L.pose_anwenden(rig, d)
    G = Vector_(p["G"]); K.schloss_setzen(S, K.matrix_aus(G, p["psi"]))
    paare = K.paare_hand_schloss(S); n = sum(paare.values())
    u = np.array((K.rz(p["psi"]).to_3x3() @ K.Vector((1, 0, 0)))[:])
    gp = np.array(G[:])
    spitzen = [K.welt(f"finger{k}-3.R") for k in (2, 3, 4, 5)]
    sp = sum(max(0.0, K.achse_abstand(t, G, u) * 1000 - (K.R_GRIFF * 1000 + 4.5)) for t in spitzen)
    th = max(0.0, K.achse_abstand(K.welt("finger1-3.R"), G, u) * 1000 - (K.R_GRIFF * 1000 + 9.0))
    inter, _ = K.T.messen_pose(d); K.L.pose_anwenden(rig, d)
    # Umgreifen: Fingerspitzen unterhalb der Griffachse (Schlossnormale = Handruecken +Z), Mittelgelenke nahe, Handflaeche nicht zu weit weg
    um = sum(max(0.0, (t[2] - (G[2] - 0.002)) * 1000) for t in spitzen) * 0.8
    mg = sum(max(0.0, K.achse_abstand(K.welt(f"finger{k}-2.R", "head"), G, u) * 1000 - (K.R_GRIFF * 1000 + 14.0)) for k in (2, 3, 4)) * 0.5
    pm = 0.5 * (K.welt("wrist.R", "head") + K.welt("finger3-1.R", "head"))
    pa = max(0.0, K.achse_abstand(pm, G, u) * 1000 - 40.0) * 0.5
    c = GW * n + GT * (sp + th) + GS * inter + um + mg + pa
    return (c, n, round(sp, 1), round(th, 1), inter) if detail else (c, n, round(sp, 1), round(th, 1), inter)
def Vector_(v): return K.Vector(v)
if start and os.path.exists(start): b = json.load(open(start))
else: b = basis()
x = zu_liste(b)
if not (start and os.path.exists(start)):   # Grobsuche: Lage der Griffachse bei der Startpose
    best = None
    for _ in range(80):
        y = list(x); y[20] = random.uniform(0.0, 0.05); y[21] = random.uniform(0.03, 0.09); y[22] = random.uniform(-0.07, 0.01)
        c = kosten(y, b)
        if best is None or c[0] < best[0]: best, xb = c, y
    x = xb; print("GROB", [round(v, 1) for v in best], [round(v, 3) for v in x[20:24]], flush=True)
best = kosten(x, b)
print("START", [round(v, 1) for v in best], flush=True)
schritt = 1.0
for r in range(runden):
    for i in range(len(x)):
        sk = schritt * (0.004 if 20 <= i <= 22 else (3.0 if i == 23 else 12.0))
        for sgn in (1, -1):
            for _ in range(2):
                y = list(x); y[i] = min(GRENZ[i][1], max(GRENZ[i][0], y[i] + sgn * sk * random.uniform(0.3, 1.0)))
                c = kosten(y, b)
                if c[0] < best[0] - 1e-6: x, best = y, c
    schritt *= 0.7
    print("RUNDE", r, [round(v, 2) for v in best], flush=True)
    json.dump(aus_liste(x, b), open(aus, "w"), indent=1)
print("ENDE", [round(v, 2) for v in best], json.dumps(aus_liste(x, b)))
