"""Charakter Phase 2: Rechenbausteine für den Basiskopf (eigener Code, MIT; läuft in Blenders Python mit numpy).

- OBJ lesen ohne Blender-Import (Eckreihenfolge bleibt, Achsen wie Blender: x, -z, y)
- Kugel, Ähnlichkeits- und Thin-Plate-Spline-Anpassung, Glätten über Netzkanten
- Bindung von Punkten an eine Dreiecksfläche (Baryzentrisch) und Übertragung von Versätzen
Keine Fremddaten, kein MPFB-Code.
"""
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


# ---------------------------------------------------------------- Einlesen
def obj_lesen(pfad, mass=1.0):
    """OBJ lesen: Ecken (n,3) in Blender-Achsen, Flächen (Liste von Eckindizes), UV-Indizes je Fläche, UV (m,2),
    Stoffname je Fläche. mass: Faktor auf die Koordinaten (ICT: 0,01 für Zentimeter)."""
    ecken, uv, flaechen, flaechen_uv, stoffe = [], [], [], [], []
    stoff = None
    with open(pfad, encoding="utf-8") as f:
        for z in f:
            if z.startswith("v "):
                ecken.append(z.split()[1:4])
            elif z.startswith("vt "):
                uv.append(z.split()[1:3])
            elif z.startswith("usemtl"):
                stoff = z.split()[1]
            elif z.startswith("f "):
                teile = [t.split("/") for t in z.split()[1:]]
                flaechen.append([int(t[0]) - 1 for t in teile])
                flaechen_uv.append([int(t[1]) - 1 if len(t) > 1 and t[1] else -1 for t in teile])
                stoffe.append(stoff)
    v = np.array(ecken, dtype=np.float64)
    v = np.stack([v[:, 0], -v[:, 2], v[:, 1]], axis=1) * mass
    return v, flaechen, flaechen_uv, np.array(uv, dtype=np.float64), stoffe


def obj_ecken(pfad, mass=1.0):
    """Nur die Ecken einer OBJ (für Formen mit gleicher Reihenfolge)."""
    with open(pfad, encoding="utf-8") as f:
        v = np.array([z.split()[1:4] for z in f if z.startswith("v ")], dtype=np.float64)
    return np.stack([v[:, 0], -v[:, 2], v[:, 1]], axis=1) * mass


def dreiecke(flaechen):
    """Vielecke in Dreiecke (Fächer)."""
    aus = []
    for f in flaechen:
        for k in range(1, len(f) - 1):
            aus.append((f[0], f[k], f[k + 1]))
    return np.array(aus, dtype=np.int64)


# ---------------------------------------------------------------- Geometrie
def kugel(p):
    """Kugel durch Punkte (kleinste Quadrate): Mitte, Halbmesser."""
    a = np.hstack([2.0 * p, np.ones((len(p), 1))])
    b = (p ** 2).sum(axis=1)
    x = np.linalg.lstsq(a, b, rcond=None)[0]
    mitte = x[:3]
    return mitte, float(np.sqrt(x[3] + mitte @ mitte))


def umeyama(x, y, mit_massstab=True):
    """Ähnlichkeit (s, R, t) mit y ≈ s·R·x + t (Umeyama 1991)."""
    mx, my = x.mean(axis=0), y.mean(axis=0)
    xc, yc = x - mx, y - my
    kov = yc.T @ xc / len(x)
    u, d, vt = np.linalg.svd(kov)
    s_mat = np.eye(3)
    if np.linalg.det(u) * np.linalg.det(vt) < 0:
        s_mat[2, 2] = -1
    r = u @ s_mat @ vt
    s = (d * np.diag(s_mat)).sum() / ((xc ** 2).sum() / len(x)) if mit_massstab else 1.0
    t = my - s * r @ mx
    return s, r, t


def affin(x, y):
    """Affine Abbildung y ≈ A·x + b (kleinste Quadrate); gibt (A, b)."""
    xh = np.hstack([x, np.ones((len(x), 1))])
    m = np.linalg.lstsq(xh, y, rcond=None)[0]
    return m[:3].T, m[3]


class Tps:
    """Thin-Plate-Spline im Raum (Kern r) von Merkpunkten quelle -> ziel; glatte Verformung des ganzen Netzes."""

    def __init__(self, quelle, ziel, glaett=1e-6):
        self.q = np.asarray(quelle, dtype=np.float64)
        n = len(self.q)
        k = np.linalg.norm(self.q[:, None] - self.q[None], axis=2) + glaett * np.eye(n)
        p = np.hstack([np.ones((n, 1)), self.q])
        a = np.zeros((n + 4, n + 4))
        a[:n, :n] = k
        a[:n, n:] = p
        a[n:, :n] = p.T
        b = np.zeros((n + 4, 3))
        b[:n] = ziel
        self.w = np.linalg.solve(a, b)

    def __call__(self, x):
        x = np.asarray(x, dtype=np.float64)
        aus = np.empty_like(x)
        for a in range(0, len(x), 4096):
            t = x[a:a + 4096]
            k = np.linalg.norm(t[:, None] - self.q[None], axis=2)
            n = len(self.q)
            aus[a:a + 4096] = k @ self.w[:n] + self.w[n] + t @ self.w[n + 1:]
        return aus


# ---------------------------------------------------------------- Netz
def kanten(flaechen, n):
    """Kanten (i, j) in beiden Richtungen und Grad je Ecke."""
    paare = set()
    for f in flaechen:
        for k in range(len(f)):
            a, b = f[k], f[(k + 1) % len(f)]
            paare.add((a, b))
            paare.add((b, a))
    e = np.array(sorted(paare), dtype=np.int64)
    grad = np.bincount(e[:, 0], minlength=n).astype(np.float64)
    return e, grad


def glaetten(feld, kanten_, grad, schritte, gewicht=None, fest=None):
    """Feld (n,k) über Netzkanten mitteln. gewicht: normierte Faltung (nur gültige Werte zählen).
    fest: Bool-Maske der Ecken, die ihren Wert behalten."""
    f = np.array(feld, dtype=np.float64)
    eindim = f.ndim == 1
    if eindim:
        f = f[:, None]
    i, j = kanten_[:, 0], kanten_[:, 1]
    n = len(f)
    w = np.ones(n) if gewicht is None else np.asarray(gewicht, dtype=np.float64)
    fw = f * w[:, None]
    ww = w.copy()
    alt_f, alt_w = fw.copy(), ww.copy()
    for _ in range(schritte):
        summe = np.zeros_like(fw)
        for k in range(fw.shape[1]):
            summe[:, k] = np.bincount(i, weights=fw[j, k], minlength=n)
        sw = np.bincount(i, weights=ww[j], minlength=n)
        fw = (summe + fw) / (grad[:, None] + 1.0)
        ww = (sw + ww) / (grad + 1.0)
        if fest is not None:
            fw[fest] = alt_f[fest]
            ww[fest] = alt_w[fest]
    aus = fw / np.maximum(ww, 1e-12)[:, None]
    return aus[:, 0] if eindim else aus


def ecken_normalen(p, tri):
    """Flächengewichtete Eckennormalen."""
    fn = np.cross(p[tri[:, 1]] - p[tri[:, 0]], p[tri[:, 2]] - p[tri[:, 0]])
    n = np.zeros_like(p)
    for k in range(3):
        np.add.at(n, tri[:, k], fn)
    return n / np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]


# ---------------------------------------------------------------- Bindung
class Flaeche:
    """Dreiecksfläche mit BVH: nächster Punkt, Baryzentrische Bindung, Übertragung von Versätzen."""

    def __init__(self, ecken, tri):
        self.p = np.asarray(ecken, dtype=np.float64)
        self.tri = np.asarray(tri, dtype=np.int64)
        self.bvh = BVHTree.FromPolygons([Vector(v) for v in self.p], [tuple(int(i) for i in t) for t in self.tri])
        self.fn = np.cross(self.p[self.tri[:, 1]] - self.p[self.tri[:, 0]], self.p[self.tri[:, 2]] - self.p[self.tri[:, 0]])
        self.fn /= np.maximum(np.linalg.norm(self.fn, axis=1), 1e-12)[:, None]

    def naechste(self, punkte, weit=1.0):
        """Je Punkt: Lage, Flächennormale, Dreieck, Abstand (Dreieck -1, wenn nichts in Reichweite)."""
        n = len(punkte)
        lage = np.zeros((n, 3))
        nrm = np.zeros((n, 3))
        dr = np.full(n, -1, dtype=np.int64)
        ab = np.full(n, np.inf)
        for k, q in enumerate(punkte):
            loc, normal, idx, dist = self.bvh.find_nearest(Vector(q), weit)
            if loc is not None:
                lage[k] = loc
                nrm[k] = self.fn[idx]
                dr[k] = idx
                ab[k] = dist
        return lage, nrm, dr, ab

    def binden(self, punkte, weit=1.0):
        """Baryzentrische Bindung (Eckindizes (n,3), Gewichte (n,3)) und Höhe über der Fläche."""
        lage, nrm, dr, ab = self.naechste(punkte, weit)
        dr = np.where(dr < 0, 0, dr)
        t = self.tri[dr]
        a, b, c = self.p[t[:, 0]], self.p[t[:, 1]], self.p[t[:, 2]]
        v0, v1, v2 = b - a, c - a, lage - a
        d00 = (v0 * v0).sum(1)
        d01 = (v0 * v1).sum(1)
        d11 = (v1 * v1).sum(1)
        d20 = (v2 * v0).sum(1)
        d21 = (v2 * v1).sum(1)
        nenner = np.maximum(d00 * d11 - d01 * d01, 1e-20)
        wv = (d11 * d20 - d01 * d21) / nenner
        ww = (d00 * d21 - d01 * d20) / nenner
        gew = np.clip(np.stack([1.0 - wv - ww, wv, ww], axis=1), 0.0, 1.0)
        gew /= np.maximum(gew.sum(1, keepdims=True), 1e-12)
        hoehe = ((np.asarray(punkte) - lage) * nrm).sum(1)
        return t, gew, hoehe, ab


def uebertragen(bindung, versatz):
    """Versatz (m,3) der Fläche über die Bindung auf die gebundenen Punkte."""
    idx, gew = bindung[0], bindung[1]
    return (versatz[idx] * gew[..., None]).sum(axis=1)


def k_naechste(quelle, ziel, k=4):
    """Für jeden Zielpunkt die k nächsten Quellpunkte mit Gewichten ~ 1/Abstand² (Indizes, Gewichte)."""
    from mathutils.kdtree import KDTree
    baum = KDTree(len(quelle))
    for i, p in enumerate(quelle):
        baum.insert(Vector(p), i)
    baum.balance()
    idx = np.zeros((len(ziel), k), dtype=np.int64)
    gew = np.zeros((len(ziel), k))
    for j, p in enumerate(ziel):
        tr = baum.find_n(Vector(p), k)
        w = np.array([1.0 / max(t[2], 1e-5) ** 2 for t in tr])
        idx[j, :len(tr)] = [t[1] for t in tr]
        gew[j, :len(tr)] = w / w.sum()
    return idx, gew


def glatt_stufe(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)
