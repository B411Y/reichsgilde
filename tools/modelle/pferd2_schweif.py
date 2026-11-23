"""Schweifspalt schliessen: zieht die Schweif-Haarkarten (Hair) in der Stehpose nach vorn an Kruppe und Hinterhand heran (Rohecken werden ueber die inverse Hautmatrix angepasst).

    python -I tools/modelle/pferd2_schweif.py <paket_in.npz> <stehen_J.npz> <paket_out.npz> [--dy=0.06] [--rest=0.005]
Der Schweifansatz (oben) bleibt, nach unten wird die Verschiebung so gross, dass der Abstand zur Hinterseite des Koerpers auf `rest` Meter schrumpft (hoechstens `dy`).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_gang as G  # noqa: E402

OPT = {a.split("=")[0][2:]: (a.split("=", 1)[1] if "=" in a else "1") for a in sys.argv[1:] if a.startswith("--")}
POS = [a for a in sys.argv[1:] if not a.startswith("--")]


def schliessen(d, J, rest=0.005, dmax=0.06, ausgabe=True):
    """Neue Rohecken des Netzes Hair (Schweif an die Kruppe gezogen) fuer das geladene Paket d in der Pose J; gibt (Index des Hair-Teils, neue Ecken) zurueck."""
    sk = G.Skelett()
    n = int(d["n"])
    prims = [dict(mat=str(d[f"mat{i}"]), pos=d[f"pos{i}"], uv=d[f"uv{i}"], jo=d[f"jo{i}"], w=d[f"w{i}"], idx=d[f"idx{i}"]) for i in range(n)]
    horse = next(p for p in prims if p["mat"] == "Horse")
    hair = next(p for p in prims if p["mat"] == "Hair")
    wk, _ = sk.H.haut(horse, J)
    K = B.anwenden(sk.mn, B.blender_koord(wk))
    wh, m3 = sk.H.haut(hair, J)
    P = B.anwenden(sk.mn, B.blender_koord(wh))
    # Hinterkante des Koerpers je Hoehe (z >= 0.95, hinten y > 0.3), geglaettet
    zs = np.arange(0.95, 1.6, 0.025)
    by = []
    for z in zs:
        s = K[(abs(K[:, 2] - z) < 0.0375) & (K[:, 1] > 0.3)]
        by.append(s[:, 1].max() if len(s) else np.nan)
    by = np.array(by)
    schweif = (P[:, 1] > 0.55) & (P[:, 2] > 0.5)
    dy = np.zeros(len(P))
    for k, z in enumerate(zs):
        s = np.where(schweif & (abs(P[:, 2] - z) < 0.0375))[0]
        if len(s) and not np.isnan(by[k]):
            luecke = P[s, 1].min() - by[k]
            dy_z = min(max(luecke - rest, 0.0), dmax)
            dy[s] = -dy_z
    # nach unten (z < 0.95) den Wert der Hoehe 0.95 halten und bis z = 0.5 ausblenden, oben (> 1.45) bis zum Ansatz ausblenden
    d95 = dy[schweif & (abs(P[:, 2] - 0.95) < 0.0375)]
    wert95 = float(d95.min()) if len(d95) else 0.0
    for i in np.where(schweif)[0]:
        z = P[i, 2]
        if z < 0.95:
            dy[i] = wert95 * min(1.0, (z - 0.5) / 0.3)
        if z > 1.45:
            dy[i] *= max(0.0, 1.0 - (z - 1.45) / 0.1)
    # Rohecken anpassen: Welt-Verschiebung (normiert -> glTF-Welt), dann inverse Hautmatrix
    dn = np.stack([np.zeros(len(P)), dy, np.zeros(len(P))], axis=1)
    dg = (sk.Rm.T @ dn.T).T / sk.sk
    neu = hair["pos"].copy()
    for i in np.where(np.abs(dy) > 1e-6)[0]:
        neu[i] = hair["pos"][i] + np.linalg.solve(m3[i], dg[i])
    hi = next(i for i in range(n) if str(d[f"mat{i}"]) == "Hair")
    neu = neu.astype(d[f"pos{hi}"].dtype)
    if not ausgabe:
        return hi, neu
    print("SCHWEIF verschoben:", int((np.abs(dy) > 1e-6).sum()), "Ecken, max %.1f mm" % (np.abs(dy).max() * 1000))
    w2, _ = sk.H.haut(dict(hair, pos=neu), J)
    P2 = B.anwenden(sk.mn, B.blender_koord(w2))
    for k, z in enumerate(zs[::2]):
        s = P2[(P2[:, 1] > 0.55) & (abs(P2[:, 2] - z) < 0.0375)]
        print("z %.2f Koerper %.3f Schweif min y %.3f" % (z, by[2 * k], s[:, 1].min() if len(s) else np.nan))
    return hi, neu


def main():
    d = np.load(POS[0], allow_pickle=True)
    J = np.load(POS[1])["J"][0].astype(float)
    hi, neu = schliessen(d, J, float(OPT.get("rest", 0.005)), float(OPT.get("dy", 0.06)))
    aus = {k: d[k] for k in d.files}
    aus[f"pos{hi}"] = neu
    np.savez_compressed(POS[2], **aus)


if __name__ == "__main__":
    main()
