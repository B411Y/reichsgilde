"""glTF lesen und Clips abtasten, nur numpy (laeuft in Blender und in normalem Python). Grundlage fuer pferd2_b.py: der Blender-glTF-Import deformiert das Pferd B
("Horse", henrysteve973) in jeder Pose falsch (Bindepose), Godot zeigt den Koerper sauber. Deshalb liest pferd2_b.py die Rohdaten und skinnt selbst.
Nur Lesen der glTF-Datei.
"""
import json
import os

import numpy as np


def lade_gltf(pfad):
    j = json.load(open(pfad, encoding="utf8"))
    buf = [open(os.path.join(os.path.dirname(pfad), b["uri"]), "rb").read() for b in j["buffers"]]
    return j, buf


def accessor(j, buf, idx):
    a = j["accessors"][idx]
    bv = j["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    dt = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}[a["componentType"]]
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", 0)
    isz = np.dtype(dt).itemsize * n
    if stride and stride != isz:
        roh = np.frombuffer(buf[bv["buffer"]], dtype=np.uint8, count=stride * a["count"], offset=off).reshape(a["count"], stride)[:, :isz].copy()
        arr = roh.view(dt)
    else:
        arr = np.frombuffer(buf[bv["buffer"]], dtype=dt, count=a["count"] * n, offset=off)
    return arr.reshape(a["count"], n).astype(np.float64) if a["componentType"] == 5126 else arr.reshape(a["count"], n)


def quat_matrix(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def slerp(a, b, f):
    a, b = np.array(a, float), np.array(b, float)
    d = float(np.dot(a, b))
    if d < 0:
        b, d = -b, -d
    if d > 0.9995:
        r = a + f * (b - a)
    else:
        th = np.arccos(d)
        r = (np.sin((1 - f) * th) * a + np.sin(f * th) * b) / np.sin(th)
    return r / np.linalg.norm(r)


def knoten_matrix(nd):
    if "matrix" in nd:
        return np.array(nd["matrix"]).reshape(4, 4).T            # spaltenweise abgelegt
    M = np.eye(4)
    M[:3, :3] = quat_matrix(nd.get("rotation", (0, 0, 0, 1))) * np.array(nd.get("scale", (1, 1, 1)))[None, :]
    M[:3, 3] = nd.get("translation", (0, 0, 0))
    return M


def abtasten(j, buf, name, anteil):
    """Knoten-TRS-Ueberschreibungen des Clips `name` am Anteil (0..1) der Clipdauer (linear, Slerp): {Knoten: {"translation"|"rotation"|"scale": Wert}}."""
    an = next(a for a in j["animations"] if a["name"] == name)
    ende = max(float(j["accessors"][s["input"]]["max"][0]) for s in an["samplers"])
    zeit = anteil * ende
    aus = {}
    for c in an["channels"]:
        s = an["samplers"][c["sampler"]]
        ts = accessor(j, buf, s["input"])[:, 0]
        vs = accessor(j, buf, s["output"])
        i = max(0, min(int(np.searchsorted(ts, zeit, side="right")) - 1, len(ts) - 1))
        k = min(i + 1, len(ts) - 1)
        f = 0.0 if k == i else (zeit - ts[i]) / (ts[k] - ts[i])
        pfad = c["target"]["path"]
        wert = tuple(slerp(vs[i], vs[k], f)) if pfad == "rotation" else tuple((1 - f) * vs[i] + f * vs[k])
        aus.setdefault(c["target"]["node"], {})[pfad] = wert
    return aus
