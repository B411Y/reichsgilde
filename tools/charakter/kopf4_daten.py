"""Charakter Phase 4: Grunddaten für Haar und Bart (eigener Code, MIT; läuft in Blenders Python).

Liest den Kopf der Phase 3/4 (Backdaten des Baus: Hautnetz, Spiel-UV, Merkpunkte; die .glb: Formen der Haut) und
bereitet auf, was Frisuren und Bärte brauchen:
- die Haut je Ecke mit allen Formversätzen (Makros, Gene, Mimik) – die .glb hat je Stoff eigene, an UV-Nähten
  doppelte Ecken; sie werden über die Lage den Ecken der Backdaten zugeordnet,
- **Stützpunkte** (etwa 2 000 Hautecken, im Gesicht dichter): Jeder Haarpunkt folgt später den vier nächsten
  Stützpunkten (Gewichte nach Shepard, stetig beim Wechsel der Nachbarn). Zur Laufzeit rechnet Godot nur die
  Stützpunkte aus den Formgewichten (Alter, Geschlecht, Gene, Mimik) und verschiebt damit Karten (im Shader) und
  TressFX-Strähnen (beim Schreiben der .tfx),
- Maße und Merkpunkte des Kopfs (Augen, Ohren, Scheitel, Wirbel, Haaransatz).

Achsen wie im Bau: Blender (z oben, Gesicht nach -y, +x = linke Seite der Figur), Meter. Die Ausgabe für Godot
(kopf4_haar.py) dreht nach (x, z, -y).
"""
import json
import struct

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree


def glatt(x, a, b):
    t = np.clip((np.asarray(x, dtype=np.float64) - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def nach_godot(p):
    p = np.asarray(p)
    return np.stack([p[..., 0], p[..., 2], -p[..., 1]], -1)


def von_godot(p):
    p = np.asarray(p)
    return np.stack([p[..., 0], -p[..., 2], p[..., 1]], -1)


# ---------------------------------------------------------------- .glb lesen (nur, was gebraucht wird)
def _zugriff(gltf, binaer, i):
    a = gltf["accessors"][i]
    art = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
    breite = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
    n = a["count"]
    if "bufferView" in a:
        bv = gltf["bufferViews"][a["bufferView"]]
        start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        schritt = bv.get("byteStride", 0)
        if schritt and schritt != breite * np.dtype(art).itemsize:
            roh = np.frombuffer(binaer, np.uint8, count=schritt * n, offset=start).reshape(n, schritt)
            feld = roh[:, :breite * np.dtype(art).itemsize].copy().view(art).reshape(n, breite)
        else:
            feld = np.frombuffer(binaer, art, count=n * breite, offset=start).reshape(n, breite).copy()
    else:
        feld = np.zeros((n, breite), art)
    if "sparse" in a:
        sp = a["sparse"]
        iart = {5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[sp["indices"]["componentType"]]
        bi = gltf["bufferViews"][sp["indices"]["bufferView"]]
        bw = gltf["bufferViews"][sp["values"]["bufferView"]]
        idx = np.frombuffer(binaer, iart, count=sp["count"],
                            offset=bi.get("byteOffset", 0) + sp["indices"].get("byteOffset", 0)).astype(np.int64)
        werte = np.frombuffer(binaer, art, count=sp["count"] * breite,
                              offset=bw.get("byteOffset", 0) + sp["values"].get("byteOffset", 0)).reshape(-1, breite)
        feld[idx] = werte
    return feld


def glb_netz(pfad, netzname):
    """Primitive eines Netzes: Liste von dict(stoff, pos (Godot), uv, idx, formen {name: versatz (Godot)})."""
    with open(pfad, "rb") as f:
        daten = f.read()
    laenge = struct.unpack("<I", daten[12:16])[0]
    gltf = json.loads(daten[20:20 + laenge])
    rest = daten[20 + laenge:]
    binaer = rest[8:8 + struct.unpack("<I", rest[:4])[0]]
    aus = []
    for m in gltf["meshes"]:
        if m["name"] != netzname:
            continue
        namen = m.get("extras", {}).get("targetNames", [])
        for p in m["primitives"]:
            e = {"stoff": gltf["materials"][p["material"]]["name"],
                 "pos": _zugriff(gltf, binaer, p["attributes"]["POSITION"]).astype(np.float64),
                 "idx": _zugriff(gltf, binaer, p["indices"]).reshape(-1, 3).astype(np.int64),
                 "formen": {}}
            for i, t in enumerate(p.get("targets", [])):
                e["formen"][namen[i]] = _zugriff(gltf, binaer, t["POSITION"]).astype(np.float32)
            aus.append(e)
    return aus


# ---------------------------------------------------------------- Kopf
class Kopf:
    """Hautnetz der Backdaten mit Formversätzen, Merkpunkten, Maßen und Stützpunkten (alles Blender-Achsen)."""

    def __init__(self, backdaten, glb):
        d = np.load(backdaten)
        self.p0 = d["h_ecken"].astype(np.float64)          # Grundlage: Mann, 25 Jahre
        self.tri = d["h_tri"].astype(np.int64)
        self.tri_uv = d["h_tri_uv"].astype(np.float64)      # Spiel-UV je Dreiecksecke (Godot: v nach unten)
        self.tri_stoff = d["h_tri_stoff"].astype(np.int64)
        self.n_haut = int(d["n_haut"])
        self.mh = tuple(int(x) for x in d["mundhoehle"])
        self.ah = d["augenhoehlen"].astype(np.int64)
        self.m68 = d["merk68"].astype(np.float64)
        self.auge = {"l": d["auge_l"].astype(np.float64), "r": d["auge_r"].astype(np.float64)}
        self.lippe = d["lippennaht"].astype(np.float64)
        self.hals_achse = d["hals_achse"].astype(np.float64)
        self.kragen_mitte = d["kragen_mitte"].astype(np.float64)
        n = len(self.p0)
        idx = np.arange(n)
        self.ist_mund = (idx >= self.mh[0]) & (idx < self.mh[1])
        self.ist_hoehle = self.ist_mund.copy()
        for a, b in self.ah:
            self.ist_hoehle |= (idx >= a) & (idx < b)
        self.ist_bueste = idx >= self.n_haut
        # Formen aus der .glb über die Lage zuordnen
        prims = glb_netz(glb, "Haut")
        baum = KDTree(n)
        for i, p in enumerate(self.p0):
            baum.insert(Vector(p), i)
        baum.balance()
        self.formen = {}
        gesehen = np.zeros(n, bool)
        fehl = 0.0
        for pr in prims:
            pb = von_godot(pr["pos"])
            zu = np.zeros(len(pb), np.int64)
            for j, q in enumerate(pb):
                co, i, dist = baum.find(Vector(q))
                zu[j] = i
                fehl = max(fehl, dist)
            gesehen[zu] = True
            for name, v in pr["formen"].items():
                if name not in self.formen:
                    self.formen[name] = np.zeros((n, 3), np.float32)
                self.formen[name][zu] = von_godot(v)
        self.zuordnung_fehler_mm = fehl * 1000
        self.ohne_form = int((~gesehen).sum())
        self.namen = list(self.formen)
        a = self.auge["l"][:3]
        b = self.auge["r"][:3]
        self.mass = float(np.linalg.norm(a - b)) / 0.060

    # Lage für Formgewichte {name: gewicht}
    def lage(self, gewichte=None):
        p = self.p0.copy()
        for k, w in (gewichte or {}).items():
            if k in self.formen and w:
                p += w * self.formen[k]
        return p

    def versatz(self, gewichte):
        v = np.zeros_like(self.p0)
        for k, w in gewichte.items():
            if k in self.formen and w:
                v += w * self.formen[k]
        return v


class Huelle:
    """Haut in einer Lage: BVH (nächster Punkt, Strahl), Eckennormalen, Merkpunkte und Maße des Kopfs."""

    def __init__(self, kopf, p):
        self.k = kopf
        self.p = p
        # ohne Mund- und Augenhöhlen (Haare sollen nicht hinein)
        ok = ~(kopf.ist_hoehle[kopf.tri].any(1))
        self.tri = kopf.tri[ok]
        self.bvh = BVHTree.FromPolygons([Vector(v) for v in p], [tuple(int(i) for i in t) for t in self.tri])
        fn = np.cross(p[self.tri[:, 1]] - p[self.tri[:, 0]], p[self.tri[:, 2]] - p[self.tri[:, 0]])
        vn = np.zeros_like(p)
        for j in range(3):
            np.add.at(vn, self.tri[:, j], fn)
        self.vn = vn / np.maximum(np.linalg.norm(vn, axis=1), 1e-12)[:, None]
        self.fn = fn / np.maximum(np.linalg.norm(fn, axis=1), 1e-12)[:, None]
        self._merkpunkte()

    def naechste(self, q, weit=0.3):
        loc, nrm, i, d = self.bvh.find_nearest(Vector(q), weit)
        if loc is None:
            return None, None, -1, 1e9
        # glatte Normale (baryzentrisch aus den Eckennormalen), nicht die Flächennormale
        t = self.tri[i]
        a, b, c = self.p[t[0]], self.p[t[1]], self.p[t[2]]
        l = np.array(loc)
        v0, v1, v2 = b - a, c - a, l - a
        d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1
        d20, d21 = v2 @ v0, v2 @ v1
        den = max(d00 * d11 - d01 * d01, 1e-20)
        wv = (d11 * d20 - d01 * d21) / den
        ww = (d00 * d21 - d01 * d20) / den
        wu = 1.0 - wv - ww
        n = wu * self.vn[t[0]] + wv * self.vn[t[1]] + ww * self.vn[t[2]]
        n /= max(np.linalg.norm(n), 1e-12)
        return l, n, i, d

    def abstand(self, q):
        """Vorzeichenbehafteter Abstand zur Haut (außen positiv), Lage und Normale."""
        l, n, i, d = self.naechste(q)
        if l is None:
            return 1.0, None, None
        return float((np.asarray(q) - l) @ n), l, n

    def _merkpunkte(self):
        k, p, M = self.k, self.p, self.k.mass
        m68 = k.m68 + (self._knn_versatz(k.m68) if p is not k.p0 else 0)
        self.m68 = m68
        self.auge = {s: k.auge[s][:3] + self._knn_versatz(k.auge[s][None, :3])[0] for s in ("l", "r")}
        self.auge_z = (self.auge["l"][2] + self.auge["r"][2]) / 2
        self.auge_y = (self.auge["l"][1] + self.auge["r"][1]) / 2
        haut = ~k.ist_bueste & ~k.ist_hoehle
        self.haut = haut
        mitte_x = np.abs(p[:, 0]) < 0.01 * M
        oben = haut & mitte_x
        self.scheitel = p[oben][np.argmax(p[oben][:, 2])]
        self.kopfmitte = np.array([0.0, self.auge_y + 0.085 * M, self.auge_z])
        # Ohren: äußerste Punkte zwischen Augen- und Mundhöhe
        mund_z = k.lippe[:, 2].mean() + (self._knn_versatz(k.lippe).mean(0)[2] if p is not k.p0 else 0)
        self.mund_z = mund_z
        self.ohr = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            band = haut & (p[:, 2] < self.auge_z + 0.01) & (p[:, 2] > mund_z) & (np.sign(p[:, 0]) == sx)
            xmax = np.max(np.abs(p[band, 0]))
            self.ohr[s] = p[band & (np.abs(p[:, 0]) > xmax - 0.012 * M)].mean(0)
        self.ohr_z = self.auge_z - 0.012 * M
        # Wirbel: hinter dem Scheitel auf der Haut
        q = self.scheitel + np.array([0.0, 0.05 * M, -0.012 * M])
        l, n, _, _ = self.naechste(q)
        self.wirbel = l
        self.kinn = m68[8]
        self.nase_unten = m68[33]
        self.brauen_oben = m68[[19, 24], 2].mean()
        # Schultern: höchster Büstenpunkt bei |x| = 0,13 m
        self.schulter = {}
        for s, sx in (("l", 1.0), ("r", -1.0)):
            sel = k.ist_bueste & (np.abs(p[:, 0] - sx * 0.13 * M) < 0.015) & (np.abs(p[:, 1] - self.ohr[s][1]) < 0.05)
            if sel.any():
                self.schulter[s] = p[sel][np.argmax(p[sel][:, 2])]
            else:
                self.schulter[s] = np.array([sx * 0.13 * M, self.ohr[s][1], self.ohr_z - 0.17 * M])

    def _knn_versatz(self, q):
        """Versatz der Lage gegen die Grundlage an beliebigen Punkten (k nächste Hautecken der Grundlage)."""
        d = self.p - self.k.p0
        baum = getattr(self.k, "_baum0", None)
        if baum is None:
            baum = KDTree(len(self.k.p0))
            for i, v in enumerate(self.k.p0):
                baum.insert(Vector(v), i)
            baum.balance()
            self.k._baum0 = baum
        aus = np.zeros((len(q), 3))
        for j, x in enumerate(q):
            tr = baum.find_n(Vector(x), 6)
            w = np.array([1.0 / max(t[2], 1e-4) ** 2 for t in tr])
            aus[j] = (d[[t[1] for t in tr]] * w[:, None]).sum(0) / w.sum()
        return aus

    # Richtung um den Kopf (Grad): 0 vorn, 90 seitlich, 180 hinten
    def richtung(self, q):
        q = np.atleast_2d(q)
        rel = q - self.kopfmitte
        return np.degrees(np.arctan2(np.abs(rel[:, 0]), -rel[:, 1]))

    def haaransatz_z(self, richtung, tiefer=0.0):
        """Höhe des Haaransatzes je Richtung (wie kopf3_backen.py: Stirn, Schläfe, vor dem Ohr, Nacken)."""
        M = self.k.mass
        so = self.brauen_oben + 0.052 * M
        return np.interp(richtung, [0, 30, 55, 75, 95, 115, 135, 155, 180],
                         [so, so - 0.005 * M, self.auge_z + 0.028 * M, self.auge_z + 0.018 * M, self.auge_z + 0.012 * M,
                          self.auge_z - 0.01 * M, self.ohr_z - 0.03 * M, self.ohr_z - 0.05 * M, self.ohr_z - 0.06 * M]) - tiefer

    def kopfhaut(self, q, tiefer=0.0, weich=(-0.006, 0.008), ohr_frei=(0.022, 0.032)):
        """Anteil Kopfhaut (0..1) an den Punkten q: über dem Haaransatz, nicht am Ohr, nicht auf der Büste."""
        q = np.atleast_2d(q)
        M = self.k.mass
        w = glatt(q[:, 2] - self.haaransatz_z(self.richtung(q), tiefer), weich[0], weich[1])
        for s in ("l", "r"):
            w *= glatt(np.linalg.norm(q - self.ohr[s], axis=1), ohr_frei[0] * M, ohr_frei[1] * M)
        # Nacken: nicht unter den Halsansatz
        w *= glatt(q[:, 2] - (self.ohr_z - 0.075 * M), 0.0, 0.01 * M)
        return w

    def rang(self, q, rauschen=None):
        """Reihenfolge des Ausfalls bei Glatze (0 = fällt zuerst, 1 = bleibt): Geheimratsecken und Tonsur zuerst,
        dann die Platte; der Haarkranz an den Seiten und hinten bleibt."""
        q = np.atleast_2d(q)
        M = self.k.mass
        richt = self.richtung(q)
        # Kranz: unter der Linie Schläfe – Hinterkopf (über den Ohren)
        kranz_z = np.interp(richt, [0, 60, 90, 130, 180], [self.auge_z + 0.06 * M, self.auge_z + 0.045 * M,
                                                          self.auge_z + 0.03 * M, self.ohr_z + 0.035 * M, self.ohr_z + 0.025 * M])
        kranz = glatt(kranz_z - q[:, 2], -0.012 * M, 0.012 * M)
        # vorn: Abstand vom Stirnansatz nach hinten (über den Scheitel), Schläfen weichen früher
        y_vorn = self.auge_y + 0.01 * M
        y_wirbel = self.wirbel[1]
        vorn = np.clip((q[:, 1] - y_vorn) / max(y_wirbel - y_vorn, 1e-3), 0, 1)
        breite = 0.075 * M
        vorn = vorn - 0.35 * np.clip(np.abs(q[:, 0]) / breite, 0, 1) ** 2 * (1 - vorn)
        tonsur = np.clip(np.linalg.norm(q - self.wirbel, axis=1) / (0.085 * M), 0, 1)
        r = np.minimum(vorn * 0.95 + 0.05, tonsur * 0.85 + 0.12)
        if rauschen is not None:
            r = r + rauschen
        return np.clip(np.maximum(r, kranz), 0, 1)


# ---------------------------------------------------------------- Stützpunkte und Bindung
def stuetzpunkte(kopf, zelle=0.011, zelle_gesicht=0.0055):
    """Hautecken als Stützpunkte, auf ein Gitter verteilt (im unteren Gesicht feiner: Bart und Mimik)."""
    p, M = kopf.p0, kopf.mass
    ok = ~kopf.ist_hoehle
    auge_z = (kopf.auge["l"][2] + kopf.auge["r"][2]) / 2
    gesicht = (p[:, 2] < auge_z + 0.005) & (p[:, 2] > kopf.m68[8, 2] - 0.05 * M) & (p[:, 1] < kopf.auge["l"][1] + 0.05 * M) & ~kopf.ist_bueste
    aus = []
    for maske, z in ((ok & gesicht, zelle_gesicht), (ok & ~gesicht, zelle)):
        idx = np.nonzero(maske)[0]
        zellen = {}
        q = np.floor(p[idx] / z).astype(np.int64)
        mitte = (q + 0.5) * z
        d = np.linalg.norm(p[idx] - mitte, axis=1)
        for j, (c, dd) in enumerate(zip(map(tuple, q), d)):
            if c not in zellen or dd < zellen[c][1]:
                zellen[c] = (idx[j], dd)
        aus.extend(v[0] for v in zellen.values())
    return np.array(sorted(set(aus)), np.int64)


class Bindung:
    """Jeder Punkt folgt den vier nächsten Stützpunkten (in der Lage, in der er entstand)."""

    def __init__(self, stuetz_lage, k=4):
        self.k = k
        self.baum = KDTree(len(stuetz_lage))
        for i, v in enumerate(stuetz_lage):
            self.baum.insert(Vector(v), i)
        self.baum.balance()

    def __call__(self, punkte):
        n = len(punkte)
        idx = np.zeros((n, self.k), np.int32)
        gew = np.zeros((n, self.k), np.float32)
        for j, q in enumerate(punkte):
            tr = self.baum.find_n(Vector(q), self.k + 1)
            dd = np.array([max(t[2], 1e-5) for t in tr])
            # Shepard mit Abschneiden am (k+1)-ten Nachbarn: stetig, wenn die Nachbarn wechseln
            w = np.maximum(1.0 / dd[:self.k] - 1.0 / dd[-1], 0.0) ** 2 if len(tr) > self.k else 1.0 / dd ** 2
            if w.sum() <= 0:
                w = np.ones(self.k)
            idx[j, :len(w)] = [t[1] for t in tr[:self.k]]
            gew[j, :len(w)] = w / w.sum()
        return idx, gew
