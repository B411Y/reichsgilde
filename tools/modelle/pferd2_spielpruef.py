"""Pruefung des Spiel-GLB (pferd2_spiel.py) nur aus der Datei: Haut aus Knoten (Ruhepose bzw. Clip-Bild), Inverse-Bind-Matrizen und Netz "Nah_pferd" (Ecken mal Gelenkmatrix, wie ein Importer),
verglichen mit der Rechnung der Werkzeuge (pferd2_b.py, Paket, Gelenkmatrizen aus <Clip>_J.npz). Gibt aus: Hoehe (Widerrist/Kopf), tiefster Punkt der Hufeisen, Blickrichtung (Nase z) und die
groesste Abweichung (mm) je Clip und Bild. Mit Verschiebung nach glTF: y hoch, +z nach vorn.

    python -I tools/modelle/pferd2_spielpruef.py <glb> <paket_nah.npz> <etappe2-Ordner> [Clip ...]   (Umgebungsvariable PRUEF_PFADE wie pferd2_glbpruef.py)
"""
import json
import os
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_b as B  # noqa: E402
import pferd2_gang as GG  # noqa: E402


def lade(pfad):
    b = open(pfad, "rb").read()
    l = struct.unpack("<I", b[12:16])[0]
    return json.loads(b[20:20 + l]), b[20 + l + 8:]


def acc(j, buf, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}[a["componentType"]]
    o = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return np.frombuffer(buf, dtype=dt, count=a["count"] * n, offset=o).reshape(a["count"], n)


def knoten_matrix(nd, T=None, R=None):
    if "matrix" in nd:
        return np.array(nd["matrix"]).reshape(4, 4).T
    t = np.array(nd.get("translation", [0, 0, 0])) if T is None else T
    q = nd.get("rotation", [0, 0, 0, 1]) if R is None else R
    s = np.array(nd.get("scale", [1, 1, 1]))
    x, y, z, w = q
    Rm = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                   [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                   [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    M = np.eye(4)
    M[:3, :3] = Rm * s[None, :]
    M[:3, 3] = t
    return M


def main():
    glb, paket, e = sys.argv[1:4]
    clips = sys.argv[4:]
    j, buf = lade(glb)
    eltern = {}
    for i, nd in enumerate(j["nodes"]):
        for c in nd.get("children", []):
            eltern[c] = i
    skin = j["skins"][0]
    ibm = acc(j, buf, skin["inverseBindMatrices"]).reshape(-1, 4, 4)
    nah = next(m for m in j["meshes"] if m["name"] == "Nah_pferd")["primitives"][0]
    P = acc(j, buf, nah["attributes"]["POSITION"]).astype(np.float64)
    JO = acc(j, buf, nah["attributes"]["JOINTS_0"]).astype(int)
    W = acc(j, buf, nah["attributes"]["WEIGHTS_0"]).astype(np.float64)
    ani = {a["name"]: a for a in j["animations"]}

    def global_(frame=None, clip=None):
        ueber = {}
        if clip is not None:
            for c in clip["channels"]:
                s = clip["samplers"][c["sampler"]]
                v = acc(j, buf, s["output"])[frame]
                ueber.setdefault(c["target"]["node"], {})[c["target"]["path"]] = v
        glob = {}

        def g(i):
            if i not in glob:
                nd = j["nodes"][i]
                u = ueber.get(i, {})
                m = knoten_matrix(nd, u.get("translation"), u.get("rotation"))
                glob[i] = (g(eltern[i]) @ m) if i in eltern else m
            return glob[i]
        return np.array([g(ji) @ ibm[k].T for k, ji in enumerate(skin["joints"])])

    def haut(Jm):
        ph = np.hstack([P, np.ones((len(P), 1))])
        res = np.zeros((len(P), 3))
        for st in range(4):
            res += W[:, st:st + 1] * np.einsum("nij,nj->ni", Jm[JO[:, st]], ph)[:, :3]
        return res

    # Referenz: Paket (Koerper + Haar + Hufeisen + Augen + Geschirr) wie zusammenfassen() in pferd2_spiel.py, in der Welt von pferd2_b.py
    sk = GG.Skelett()
    d = np.load(paket, allow_pickle=True)
    prims = [dict(pos=d[f"pos{i}"], jo=d[f"jo{i}"], w=d[f"w{i}"]) for i in range(int(d["n"]))]
    Cb = np.array([[1.0, 0, 0], [0, 0, -1.0], [0, 1.0, 0]])
    ers = dict(x.split("=", 1) for x in os.environ.get("PRUEF_PFADE", "").split(";") if x)
    w0 = haut(global_())
    print("Ruhepose: Hoehe y %.3f..%.3f m, x %.3f..%.3f, z %.3f..%.3f (z nach vorn)" % (w0[:, 1].min(), w0[:, 1].max(), w0[:, 0].min(), w0[:, 0].max(), w0[:, 2].min(), w0[:, 2].max()))
    for nm in (clips or list(ani)):
        a = ani[nm]
        pf = ers.get(nm) or os.path.join(e, nm if nm not in ("Gehen", "Ziehen") else nm.lower(), ("gehen_J.npz" if nm in ("Gehen", "Ziehen") else nm + "_J.npz"))
        dat = np.load(pf)
        nf = dat["R"].shape[0]
        worst = 0.0
        for f in sorted(set(list(range(0, nf, max(1, nf // 6))) + [nf - 1])):
            ref = np.vstack([B.anwenden(sk.mn, B.blender_koord(sk.H.haut(p, dat["J"][f].astype(float))[0])) for p in prims]) @ Cb      # Q (Blender) -> glTF
            ist = haut(global_(f, a))
            worst = max(worst, float(np.abs(ist - ref).max()) * 1000)
        print("%-18s Bilder %3d  groesste Abweichung %.3f mm" % (nm, nf, worst))


if __name__ == "__main__":
    main()
