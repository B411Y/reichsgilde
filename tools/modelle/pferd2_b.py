"""Zugpferd auf Basis B (henrysteve973, "Horse", CC BY 4.0): Rohdaten aus der glTF lesen, selbst skinnen (wie eine Engine), ausrichten, als Blender-Netze rendern, vermessen.

Dieses Modul ist die Grundlage fuer Form (pferd2_form_b.py): Der Blender-glTF-Import deformiert dieses Modell falsch (Bindepose), deshalb wird hier nur gelesen:
Ecken, UV, Gelenk-Indizes, Gewichte, Dreiecke aus scene.gltf/scene.bin; Gelenkmatrizen = Knotenwelt (Ruhepose oder Clip) mal inverse Bindematrix.
Koordinaten: glTF y oben, Einheit des Skeleton-Knotens (0,1) -> "Welt"; Blender-Koordinaten (x, -z, y); danach Ausrichtung Mn (Blick nach -Y, Widerrist = Zielhoehe, Boden z = 0).
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_bind as bd  # noqa: E402

QUELLE = os.environ.get("RG_PFERD3", "C:/Reichsgilde_Austausch/pferd3/scene.gltf")
KOERPER = ("Horse", "Hair", "Cornea", "Horseshoe")      # Saddle entfaellt


class Haut:
    def __init__(self, pfad=QUELLE):
        self.pfad = pfad
        self.j, self.buf = bd.lade_gltf(pfad)
        j = self.j
        self.prims = []
        for ni, nd in enumerate(j["nodes"]):
            if "mesh" not in nd or "skin" not in nd:
                continue
            for pi_, p in enumerate(j["meshes"][nd["mesh"]]["primitives"]):
                mat = j["materials"][p["material"]]["name"]
                if mat not in KOERPER:
                    continue
                at = p["attributes"]
                self.prims.append(dict(
                    mat=mat, knoten=ni, mesh=nd["mesh"], prim=pi_, material=p["material"], pos=bd.accessor(j, self.buf, at["POSITION"]).astype(float), uv=bd.accessor(j, self.buf, at["TEXCOORD_0"]).astype(float),
                    jo=bd.accessor(j, self.buf, at["JOINTS_0"]).astype(int), w=bd.accessor(j, self.buf, at["WEIGHTS_0"]).astype(float),
                    idx=bd.accessor(j, self.buf, p["indices"]).astype(int).reshape(-1, 3)))
        self.skin = j["skins"][0]
        self.ibm = bd.accessor(j, self.buf, self.skin["inverseBindMatrices"]).reshape(-1, 4, 4)
        self.eltern = {}
        for i, nd in enumerate(j["nodes"]):
            for c in nd.get("children", []):
                self.eltern[c] = i
        self.gelenknamen = [j["nodes"][i]["name"] for i in self.skin["joints"]]

    def gelenkmatrizen(self, clip=None, anteil=0.0):
        j = self.j
        ueber = bd.abtasten(j, self.buf, clip, anteil) if clip else {}
        glob = {}

        def g(i):
            if i not in glob:
                nd = dict(j["nodes"][i])
                if i in ueber:
                    nd.pop("matrix", None)
                    nd.update(ueber[i])
                m = np.array(bd.knoten_matrix(nd))
                glob[i] = (g(self.eltern[i]) @ m) if i in self.eltern else m
            return glob[i]
        return np.array([g(ji) @ self.ibm[k].T for k, ji in enumerate(self.skin["joints"])])

    def haut(self, p, J, pos=None):
        """Ecken des Netzes p in der Pose J (Gelenkmatrizen); pos optional andere Rohecken. Gibt (Welt-Ecken, gemischte 3x3-Matrizen je Ecke) zurueck."""
        pos = p["pos"] if pos is None else pos
        ph = np.hstack([pos, np.ones((len(pos), 1))])
        res = np.zeros((len(pos), 3))
        m3 = np.zeros((len(pos), 3, 3))
        for s in range(4):
            M = J[p["jo"][:, s]]
            res += p["w"][:, s:s + 1] * np.einsum("nij,nj->ni", M, ph)[:, :3]
            m3 += p["w"][:, s, None, None] * M[:, :3, :3]
        return res, m3


def blender_koord(P):
    return np.stack([P[:, 0], -P[:, 2], P[:, 1]], axis=1)


def schnitt(P, F, achse, wert):
    """Punkte, in denen Dreieckskanten die Ebene achse = wert schneiden."""
    kanten = np.vstack([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    a, b = P[kanten[:, 0]], P[kanten[:, 1]]
    da, db = a[:, achse] - wert, b[:, achse] - wert
    m = (da * db) < 0
    t = (da[m] / (da[m] - db[m]))[:, None]
    return a[m] + (b[m] - a[m]) * t


def oben_bei(P, F, y):
    s = schnitt(P, F, 1, y)
    return float(s[:, 2].max())


def ausrichtung(H, P, F, ziel_h):
    """4x4-Matrix Mn auf Blender-Koordinaten: Blick -Y (Hauptachse, Kopf = hoechster Punkt), Widerrist (eigene Landmarken: Brust und Gesaess aus dem Schnitt in 0,61 Zielhoehe,
    Widerrist 23 % der Rumpflaenge hinter der Brust) = ziel_h, Brustspitze auf y = -0,811 m * ziel_h / 1,806, Mitte x = 0, Boden z = 0."""
    xy = P[:, :2] - P[:, :2].mean(0)
    w, v = np.linalg.eigh(np.cov(xy.T))
    achse = v[:, 1]
    if np.dot(achse, P[np.argmax(P[:, 2]), :2] - P[:, :2].mean(0)) < 0:
        achse = -achse
    phi = math.atan2(achse[1], achse[0])
    c, s_ = math.cos(-math.pi / 2 - phi), math.sin(-math.pi / 2 - phi)
    R = np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1.0]])
    Q = P @ R.T
    sk = 1.0
    for _ in range(6):
        Z = Q * sk
        zmin = Z[:, 2].min()
        zh = 0.61 * ziel_h + zmin
        sz = schnitt(Z, F, 2, zh)
        sz = sz[np.abs(sz[:, 0] - np.median(sz[:, 0])) > 0.03 * ziel_h]
        yb, yg = sz[:, 1].min(), sz[:, 1].max()
        yw = yb + 0.23 * (yg - yb)
        sk *= ziel_h / (oben_bei(Z, F, yw) - zmin)
    Z = Q * sk
    zmin = Z[:, 2].min()
    sz = schnitt(Z, F, 2, 0.61 * ziel_h + zmin)
    sz = sz[np.abs(sz[:, 0] - np.median(sz[:, 0])) > 0.03 * ziel_h]
    cx = float(np.median(sz[:, 0]))
    ybrust = -0.811 * ziel_h / 1.806
    t = np.array([-cx, ybrust - float(sz[:, 1].min()), -zmin])
    M = np.eye(4)
    M[:3, :3] = R * sk
    M[:3, 3] = t
    return M


def anwenden(M, P):
    return P @ M[:3, :3].T + M[:3, 3]


def pose(H, J, mn, prims=None):
    """Je Netz: Ecken in ausgerichteten Blender-Koordinaten."""
    aus = []
    for p in (prims or H.prims):
        w, m3 = H.haut(p, J)
        aus.append(anwenden(mn, blender_koord(w)))
    return aus


def lade_norm(pfad):
    return np.array(json.load(open(pfad)))


def speichere_norm(pfad, M):
    json.dump(M.tolist(), open(pfad, "w"))



def weiche_normalen(P, idx, runden=4):
    """Vertex-Normalen: Flaechennormalen (flaechengewichtet) ueber Ecken gleicher Lage (UV-Naehte) gemittelt, danach `runden` mal mit den Nachbarn geglaettet
    (nimmt langen duennen Dreiecken der Reduktion die Kanten, Brustbein, Wange)."""
    idx = np.asarray(idx)
    fn = np.cross(P[idx[:, 1]] - P[idx[:, 0]], P[idx[:, 2]] - P[idx[:, 0]])
    key = np.round(P * 2000).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    nu = inv.max() + 1
    acc = np.zeros((nu, 3))
    for k in range(3):
        np.add.at(acc, inv[idx[:, k]], fn)
    kanten = np.vstack([inv[idx[:, [0, 1]]], inv[idx[:, [1, 2]]], inv[idx[:, [2, 0]]]])
    kanten = np.vstack([kanten, kanten[:, ::-1]])
    n = acc / np.maximum(np.linalg.norm(acc, axis=1, keepdims=True), 1e-12)
    for _ in range(runden):
        s = np.zeros_like(n)
        np.add.at(s, kanten[:, 0], n[kanten[:, 1]])
        n = n + 0.8 * s / np.maximum(np.bincount(kanten[:, 0], minlength=nu), 1)[:, None]
        n = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    return n[inv]
