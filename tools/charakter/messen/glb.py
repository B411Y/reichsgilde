"""glTF-2.0-Binärdateien (.glb) lesen und Formen anwenden – mit numpy, ohne Blender und ohne Godot.

Charakter Phase 6 (Messbasis S0). Liest Lage, Normalen, Dreiecke und die Formziele (auch dünne Accessoren, „sparse“)
eines Primitivs und mischt Formgewichte wie Godot: Lage = Grundlage + Summe(Gewicht × Versatz). Normalen eines Ziels
ohne NORMAL bleiben die des Grundnetzes (wie Godots glTF-Import).

    from messen.glb import Glb
    g = Glb("~/werkzeuge/charakter/glb5/kopf5.glb")
    haut = g.teil("Haut", 0)               # Primitiv 0 des Netzes „Haut“ (Stoff Haut)
    p = haut.lage({"alter_reife_m": 1.0})  # (N, 3) in Metern
"""
import json
import os
import struct

import numpy as np

_TYP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
_ANZ = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Glb:
    def __init__(self, pfad):
        pfad = os.path.expanduser(pfad)
        self.pfad = pfad
        with open(pfad, "rb") as f:
            daten = f.read()
        magie, _, _ = struct.unpack_from("<III", daten, 0)
        if magie != 0x46546C67:
            raise ValueError(f"keine .glb: {pfad}")
        l0, _ = struct.unpack_from("<II", daten, 12)
        self.js = json.loads(daten[20:20 + l0])
        o = 20 + l0
        l1, _ = struct.unpack_from("<II", daten, o)
        self.bin = memoryview(daten)[o + 8:o + 8 + l1]
        self._acc = {}

    # ------------------------------------------------------------------ Accessoren
    def _ansicht(self, bv_index, versatz, typ, anzahl, komp):
        bv = self.js["bufferViews"][bv_index]
        start = bv.get("byteOffset", 0) + versatz
        groesse = np.dtype(typ).itemsize * komp
        schritt = bv.get("byteStride", groesse)
        if schritt == groesse:
            roh = np.frombuffer(self.bin, dtype=typ, count=anzahl * komp, offset=start)
            return roh.reshape(anzahl, komp)
        aus = np.empty((anzahl, komp), dtype=typ)
        for i in range(anzahl):
            aus[i] = np.frombuffer(self.bin, dtype=typ, count=komp, offset=start + i * schritt)
        return aus

    def accessor(self, index):
        """Ein Accessor als (Anzahl, Komponenten)-Feld (float32 bzw. Ganzzahl); dünne Accessoren werden aufgefüllt."""
        if index in self._acc:
            return self._acc[index]
        a = self.js["accessors"][index]
        typ = _TYP[a["componentType"]]
        komp = _ANZ[a["type"]]
        n = a["count"]
        if "bufferView" in a:
            werte = self._ansicht(a["bufferView"], a.get("byteOffset", 0), typ, n, komp).copy()
        else:
            werte = np.zeros((n, komp), dtype=typ)
        sp = a.get("sparse")
        if sp:
            c = sp["count"]
            ix = sp["indices"]
            idx = self._ansicht(ix["bufferView"], ix.get("byteOffset", 0), _TYP[ix["componentType"]], c, 1)[:, 0]
            va = sp["values"]
            val = self._ansicht(va["bufferView"], va.get("byteOffset", 0), typ, c, komp)
            werte[idx.astype(np.int64)] = val
        if a.get("normalized") and typ != np.float32:
            werte = werte.astype(np.float32) / np.iinfo(typ).max
        self._acc[index] = werte
        return werte

    def ist_duenn(self, index):
        return "sparse" in self.js["accessors"][index]

    # ------------------------------------------------------------------ Netze
    def netze(self):
        return [m.get("name", "") for m in self.js["meshes"]]

    def teil(self, netz, primitiv=0):
        for m in self.js["meshes"]:
            if m.get("name") == netz:
                namen = (m.get("extras") or {}).get("targetNames", [])
                return Teil(self, m["primitives"][primitiv], namen, f"{netz}[{primitiv}]")
        raise KeyError(f"Netz {netz} fehlt in {self.pfad}")

    def teile(self, netz):
        for m in self.js["meshes"]:
            if m.get("name") == netz:
                namen = (m.get("extras") or {}).get("targetNames", [])
                return [Teil(self, p, namen, f"{netz}[{i}]") for i, p in enumerate(m["primitives"])]
        raise KeyError(f"Netz {netz} fehlt in {self.pfad}")


class Teil:
    """Ein Primitiv: Grundlage, Normalen, Dreiecke, Formziele; mischt Gewichte wie Godot."""

    def __init__(self, glb, prim, namen, name):
        self.glb = glb
        self.prim = prim
        self.name = name
        self.formnamen = list(namen)
        self.grund = glb.accessor(prim["attributes"]["POSITION"]).astype(np.float64)
        n_idx = prim["attributes"].get("NORMAL")
        self.normalen = glb.accessor(n_idx).astype(np.float64) if n_idx is not None else None
        uv = prim["attributes"].get("TEXCOORD_0")
        self.uv = glb.accessor(uv).astype(np.float64) if uv is not None else None
        ind = prim.get("indices")
        self.dreiecke = glb.accessor(ind).reshape(-1, 3).astype(np.int64) if ind is not None else None
        self._ziele = {nm: prim["targets"][i] for i, nm in enumerate(namen) if i < len(prim.get("targets", []))}

    def hat(self, form):
        return form in self._ziele

    def versatz(self, form, art="POSITION"):
        z = self._ziele.get(form)
        if z is None or art not in z:
            return None
        return self.glb.accessor(z[art]).astype(np.float64)

    def lage(self, gewichte, nur=None):
        """Lage aller (oder der Ecken `nur`) nach den Formgewichten {Form: Gewicht}."""
        p = self.grund.copy() if nur is None else self.grund[nur].copy()
        for form, w in gewichte.items():
            if abs(w) < 1e-12:
                continue
            d = self.versatz(form)
            if d is None:
                continue
            p += w * (d if nur is None else d[nur])
        return p

    def normale(self, gewichte):
        """Normalen nach den Gewichten (Ziele ohne NORMAL tragen nichts bei – wie Godot), normiert."""
        if self.normalen is None:
            return None
        n = self.normalen.copy()
        for form, w in gewichte.items():
            if abs(w) < 1e-12:
                continue
            d = self.versatz(form, "NORMAL")
            if d is not None:
                n += w * d
        laenge = np.linalg.norm(n, axis=1, keepdims=True)
        return n / np.maximum(laenge, 1e-12)

    def flaechennormalen(self, lage):
        """Normalen aus der verformten Lage neu gerechnet (Flächenmittel je Ecke) – zum Vergleich mit den gespeicherten."""
        n = np.zeros_like(lage)
        a, b, c = lage[self.dreiecke[:, 0]], lage[self.dreiecke[:, 1]], lage[self.dreiecke[:, 2]]
        f = np.cross(b - a, c - a)
        for k in range(3):
            np.add.at(n, self.dreiecke[:, k], f)
        return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
