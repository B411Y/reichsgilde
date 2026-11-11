"""Posendefinitionen fuer den Pixal3D-Handschuh (Handraum). posen(rig) -> {name: pose}"""
import json, os
import v2_rig_lib as L
from v2_rig_lib import AX_X, AX_Y, AX_Z

PFAD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "faust_params.json")
STANDARD = {"f": {"2": [80, 95, 55, 2], "3": [84, 98, 58, 3], "4": [86, 98, 56, 4], "5": [88, 96, 52, 6]},
            "d": {"swing": -40, "flex": [28, 40, 35], "dreh": [0, 0, 0]}, "hand": 6}

def parameter():
    if os.path.exists(PFAD):
        return json.load(open(PFAD))
    return STANDARD

def faust_aus(rig, par):
    d = {}
    for k in (2, 3, 4, 5):
        a = par["f"][str(k)]
        L.finger_pose(rig, d, k, a[:3], a[3])
    L.daumen_pose(rig, d, swing=par["d"]["swing"], flex=par["d"]["flex"], dreh=par["d"]["dreh"])
    d["wrist.R"] = [(-AX_X, par["hand"])]
    return d

def posen(rig, par=None):
    par = par or parameter()
    k0 = {k: L.kipp(rig, k) for k in (2, 3, 4, 5)}
    P = {}
    # Ruhe: locker, leicht gekruemmt, Faecher halb zurueck
    d = {}
    for k, w in ((2, (10, 12, 6)), (3, (14, 18, 8)), (4, (18, 22, 10)), (5, (22, 24, 12))):
        L.finger_pose(rig, d, k, w, 0.55 * k0[k])
    L.daumen_pose(rig, d, swing=0, flex=(6, 8, 6))
    d["wrist.R"] = [(-AX_X, 0)]
    P["ruhe"] = d
    P["faust"] = faust_aus(rig, par)
    # Zeigen: Zeigefinger gestreckt (gerade aus dem Knoechel), Rest Faust
    d = faust_aus(rig, par)
    for g in (1, 2, 3):   # Daumen nur halb eingeschlagen (liegt erst in der Faust ganz quer)
        n = f"finger1-{g}.R"; d[n] = [(ax, w * 0.85) for (ax, w) in d[n]]
    L.finger_pose(rig, d, 2, (2, 1, 1), par.get("zeige_kipp", 3))
    d["wrist.R"] = [(-AX_X, 3)]
    P["zeigen"] = d
    return P
