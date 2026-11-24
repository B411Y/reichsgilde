"""Godot-unabhaengiger Beweis der Clip-Konvention: rekonstruiert die Skinning-Matrizen eines Clip-Bildes nur aus dem GLB (Knoten-Ruhepose + Kanaele + IBM)
und vergleicht Hufeisen- und Koerperpunkte mit den Messdaten (<Clip>_J.npz, die auch die Messungen von Gleiten/Schweben tragen).

    python -I tools/modelle/pferd2_glbpruef.py <glb> <etappe2-Ordner> [Clip ...]
Ausgabe: je Clip groesste Abweichung (mm) der Hufeisenpunkte (alle vier Hufe) und der Haut (Kopf, Rumpf, Beine) ueber die geprueften Bilder, sowie Ruhepose ohne Clip.
"""
import json
import os
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_bind as bd  # noqa: E402
import pferd2_hufhalter as HH  # noqa: E402


def lade_glb(pfad):
    b = open(pfad, "rb").read()
    l = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + l])
    off = 20 + l + 8
    return j, [b[off:]]


def lokal(nd):
    if "matrix" in nd:
        return np.array(nd["matrix"]).reshape(4, 4).T
    return bd.knoten_matrix(nd)


def welt(j, ersatz):
    n = len(j["nodes"])
    eltern = {}
    for i, nd in enumerate(j["nodes"]):
        for c in nd.get("children", []):
            eltern[c] = i
    G, fertig = [None] * n, set()

    def holen(i):
        if i in fertig:
            return G[i]
        nd = dict(j["nodes"][i])
        if i in ersatz and "matrix" not in nd:
            nd.update(ersatz[i])
        m = lokal(nd)
        G[i] = (holen(eltern[i]) @ m) if i in eltern else m
        fertig.add(i)
        return G[i]
    for i in range(n):
        holen(i)
    return G


def main():
    glb, e = sys.argv[1], sys.argv[2]
    clips = sys.argv[3:]
    j, buf = lade_glb(glb)
    h = HH.Halter()
    sk = h.sk
    horse = next(p for p in sk.H.prims if p["mat"] == "Horse")

    def punkte(J):
        w, _ = sk.H.haut(h.shoe, J)
        s = B.anwenden(sk.mn, B.blender_koord(w))
        w2, _ = sk.H.haut(horse, J)
        return s, B.anwenden(sk.mn, B.blender_koord(w2))

    def skin_aus_glb(ersatz):
        G = welt(j, ersatz)
        return np.array([G[ji] @ sk.ibm[k].T for k, ji in enumerate(sk.joints)])
    # Ruhepose ohne Clip
    J_ruhe = skin_aus_glb({})
    st = np.load(os.path.join(e, "Stehen", "Stehen_J.npz"))
    s_r, h_r = punkte(J_ruhe)
    s_m, h_m = punkte(st["J"][0].astype(float))
    print("Ruhepose ohne Clip gegen Stehen Bild 0: Hufeisen %.3f mm, Haut %.3f mm" % (np.abs(s_r - s_m).max() * 1000, np.abs(h_r - h_m).max() * 1000))
    ani = {a["name"]: a for a in j["animations"]}
    for nm in (clips or list(ani)):
        a = ani[nm]
        ers_pfade = dict(x.split("=", 1) for x in os.environ.get("PRUEF_PFADE", "").split(";") if x)       # Name=Pfad der Messdaten, falls abweichend
        dat = np.load(ers_pfade.get(nm) or os.path.join(e, nm if nm not in ("Gehen", "Ziehen") else nm.lower(), ("gehen_J.npz" if nm in ("Gehen", "Ziehen") else nm + "_J.npz")))
        nf = dat["R"].shape[0]
        kan = []
        for c in a["channels"]:
            s = a["samplers"][c["sampler"]]
            kan.append((c["target"]["node"], c["target"]["path"], bd.accessor(j, buf, s["output"])))
        dmax_s = dmax_h = 0.0
        for f in range(0, nf, max(1, nf // 12)):
            ers = {}
            for node, pfad, werte in kan:
                ers.setdefault(node, {})[pfad] = tuple(werte[min(f, len(werte) - 1)])
            s1, h1 = punkte(skin_aus_glb(ers))
            s2, h2 = punkte(dat["J"][f].astype(float))
            dmax_s = max(dmax_s, np.abs(s1 - s2).max() * 1000)
            dmax_h = max(dmax_h, np.abs(h1 - h2).max() * 1000)
        print("%-18s Bilder %3d  Kanaele %3d  max Abweichung Hufeisen %.3f mm, Haut %.3f mm" % (nm, nf, len(kan), dmax_s, dmax_h))


main()
