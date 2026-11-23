"""Zehenkorrektur fuer die uebernommenen Clips des Autors (Idle, Eat, ...): EINE gemeinsame Funktion `zehen_korrektur`, plus Messung (Gleiten Ecke fuer Ecke und Hufneigung) vorher/nachher.

    python -I tools/modelle/pferd2_clipkorr.py <Ausgabeordner> [--clips="Skeleton|1 Ilde,Skeleton|6 Eat"]

Hintergrund: Die Hufe des Modells stehen in der Ruhepose links/rechts verschieden geneigt (Hufeisen-Ebene LF 0,2, RF 4,5, LH -4,3, RH -2,0 Grad). Die Korrektur dreht den
Zehenknochen jedes Beins in jedem Bild um die Gegenneigung (Drehung um die Querachse, Kopf des Zehenknochens als Drehpunkt); Netz und Skelett bleiben unveraendert.
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bind as bd  # noqa: E402
import pferd2_gang as G  # noqa: E402

ZEHEN = {"LF": "BN_L_Toe_042_043", "RF": "BN_R_Toe_047_049", "LH": "BN_L_Toe_2_055_058", "RH": "BN_R_Toe_2_059_064"}
KORR_GRAD = {"LF": -0.2, "RF": -4.5, "LH": 4.3, "RH": 2.0}               # Drehung des Hufs (Grad, positiv = Sohle neigt sich vorn nach unten ... siehe pferd2_gang)


def skelett_pose(sk, clip, anteil):
    """Lokale Drehung/Verschiebung aller Knoten im Clip `clip` am Anteil (0..1) der Dauer."""
    ueber = bd.abtasten(sk.j, sk.H.buf, clip, anteil)
    T, R = sk.T.copy(), sk.R.copy()
    for i, werte in ueber.items():
        if "translation" in werte:
            T[i] = werte["translation"]
        if "rotation" in werte:
            R[i] = bd.quat_matrix(werte["rotation"])
    return T, R


def zehen_korrektur(sk, T, R, korr=None):
    """Dreht die Zehenknochen um KORR_GRAD (EINE Funktion fuer alle Clips). Gibt die neuen lokalen Drehungen zurueck."""
    korr = KORR_GRAD if korr is None else korr
    R = R.copy()
    for bein, name in ZEHEN.items():
        i = sk.id[name]
        Gm = sk.global_(T, R)
        w = math.radians(korr[bein])
        c, s = math.cos(-w), math.sin(-w)
        Rx = np.array([[1, 0, 0], [0, c, -s], [0, s, c]])             # Drehung um die Querachse (normierter Raum), +w kippt die Sohle vorn nach unten
        Rg = sk.Rm.T @ Rx @ sk.Rm
        R[i] = np.linalg.solve(Gm[sk.eltern[i]][:3, :3], Rg @ Gm[i][:3, :3]) / sk.S[i][None, :]
    return R


def messen(sk, clip, fps=24, mit_korr=False):
    ende = max(float(sk.j["accessors"][s["input"]]["max"][0]) for s in next(a for a in sk.j["animations"] if a["name"] == clip)["samplers"])
    n = int(round(ende * fps))
    shoe = next(p for p in sk.H.prims if p["mat"] == "Horseshoe")
    w0, _ = sk.H.haut(shoe, np.array([G0[ji] @ sk.ibm[k].T for k, ji in enumerate(sk.joints)]))
    Q0 = B.anwenden(sk.mn, B.blender_koord(w0))
    xm = float(np.median(Q0[:, 0]))
    ix = {}
    for bein in ZEHEN:
        seite = 1 if bein[0] == "L" else -1
        vorn = bein[1] == "F"
        ix[bein] = np.where((np.sign(Q0[:, 0] - xm) == seite) & ((Q0[:, 1] < 0.0) == vorn) & (Q0[:, 2] < 0.06))[0]
    zz = {b: [] for b in ZEHEN}
    xx = {b: [] for b in ZEHEN}
    neig = {b: [] for b in ZEHEN}
    for f in range(n):
        T, R = skelett_pose(sk, clip, f / max(n, 1))
        if mit_korr:
            R = zehen_korrektur(sk, T, R)
        Gm = sk.global_(T, R)
        J = np.array([Gm[ji] @ sk.ibm[k].T for k, ji in enumerate(sk.joints)])
        w, _ = sk.H.haut(shoe, J)
        Q = B.anwenden(sk.mn, B.blender_koord(w))
        for b in ZEHEN:
            q = Q[ix[b]]
            zz[b].append(q[:, 2])
            xx[b].append(-q[:, 1])
            neig[b].append(math.degrees(math.atan(np.polyfit(-q[:, 1], q[:, 2], 1)[0])))
    erg = {}
    for b in ZEHEN:
        Z, X = np.array(zz[b]), np.array(xx[b])
        zmin_frame = Z.min(1)
        grund = float(np.median(zmin_frame))                              # Standhoehe: Zustand mit Bodenkontakt (Boden fuer diese Probe = tiefster Punkt im Clip)
        boden = float(zmin_frame.min())
        worst = 0.0
        for k in range(Z.shape[1]):
            kont = Z[:, k] < boden + 0.004
            f = 0
            while f < n:
                if kont[f]:
                    e = f
                    while e + 1 < n and kont[e + 1]:
                        e += 1
                    worst = max(worst, float((X[f:e + 1, k].max() - X[f:e + 1, k].min()) * 1000))
                    f = e + 1
                else:
                    f += 1
        erg[b] = {"gleiten_mm": round(worst, 1), "hufneigung_mittel_grad": round(float(np.mean(neig[b])), 2), "tiefster_punkt_min_mm": round(boden * 1000, 1),
                  "tiefster_punkt_spanne_mm": round(float(zmin_frame.max() - zmin_frame.min()) * 1000, 1)}
    return erg


if __name__ == "__main__":
    aus = G.POS[0]
    os.makedirs(aus, exist_ok=True)
    sk = G.Skelett()
    G0 = sk.global_()
    clips = G.OPT.get("clips", "Skeleton|1 Ilde,Skeleton|6 Eat").split(",")
    res = {}
    for c in clips:
        res[c] = {"ohne": messen(sk, c, mit_korr=False), "mit": messen(sk, c, mit_korr=True)}
        print("CLIP", c)
        for b in ZEHEN:
            print("  ", b, "ohne", res[c]["ohne"][b], "| mit", res[c]["mit"][b])
    json.dump(res, open(os.path.join(aus, "zehenkorrektur_messung.json"), "w"), indent=1)
