# GLB verschlanken: Normalen der Formen (Morph-Targets) ausdünnen.
# Blender schreibt für jede Form die Normalen-Änderung jedes Punkts, auch winzige – das macht über die Hälfte der Datei aus.
#  - Haare, Augen, Zähne: Normalen-Änderungen ganz weg (dort sieht man sie nicht).
#  - Haut: Änderungen unter <schwelle> (Länge des Normalen-Deltas) auf null, dann dünn besetzt (sparse) speichern.
# Aufruf: python3 glb_schlank.py <ein.glb> <aus.glb> [schwelle=0.03]
import json
import struct
import sys

import numpy as np

ein, aus = sys.argv[1], sys.argv[2]
schwelle = float(sys.argv[3]) if len(sys.argv) > 3 else 0.03
roh = open(ein, "rb").read()
jl = struct.unpack("<I", roh[12:16])[0]
j = json.loads(roh[20:20 + jl])
bin_start = 20 + jl + 8
b = roh[bin_start:bin_start + struct.unpack("<I", roh[20 + jl:24 + jl])[0]]
views, accs = j["bufferViews"], j["accessors"]
KOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
TYP = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8, 5122: np.int16, 5120: np.int8}


def view_bytes(vi):
    v = views[vi]
    return b[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]


def lesen(a):
    A = accs[a]
    n, k = A["count"], KOMP[A["type"]]
    if "bufferView" in A:
        raw = view_bytes(A["bufferView"])[A.get("byteOffset", 0):]
        out = np.frombuffer(raw, TYP[A["componentType"]], n * k).reshape(n, k).astype(np.float32)
    else:
        out = np.zeros((n, k), np.float32)
    if "sparse" in A:
        s = A["sparse"]
        idx = np.frombuffer(view_bytes(s["indices"]["bufferView"])[s["indices"].get("byteOffset", 0):], TYP[s["indices"]["componentType"]], s["count"])
        val = np.frombuffer(view_bytes(s["values"]["bufferView"])[s["values"].get("byteOffset", 0):], np.float32, s["count"] * k).reshape(-1, k)
        out[idx.astype(np.int64)] = val
    return out


neu_b = bytearray()
neu_views = []
view_map = {}


def anhaengen(data, ziel=None):
    while len(neu_b) % 4:
        neu_b.append(0)
    v = {"buffer": 0, "byteOffset": len(neu_b), "byteLength": len(data)}
    if ziel:
        v["target"] = ziel
    neu_b.extend(data)
    neu_views.append(v)
    return len(neu_views) - 1


def view_kopieren(vi):
    if vi not in view_map:
        alt = views[vi]
        nv = anhaengen(view_bytes(vi), alt.get("target"))
        for k in ("byteStride",):
            if k in alt:
                neu_views[nv][k] = alt[k]
        view_map[vi] = nv
    return view_map[vi]


def sparse_schreiben(a, arr):
    A = accs[a]
    nz = np.nonzero(np.abs(arr).max(1) > 0)[0].astype(np.uint32)
    for k in ("bufferView", "byteOffset", "sparse"):
        A.pop(k, None)
    if len(nz) == 0:
        nz = np.array([0], np.uint32)
    vals = arr[nz].astype(np.float32)
    A["sparse"] = {"count": int(len(nz)),
                   "indices": {"bufferView": anhaengen(nz.tobytes()), "componentType": 5125},
                   "values": {"bufferView": anhaengen(vals.tobytes())}}
    A["min"] = vals.min(0).tolist(); A["max"] = vals.max(0).tolist()


geaendert = set()
vorher = len(roh)
for m in j["meshes"]:
    for p in m["primitives"]:
        mat = j["materials"][p["material"]]["name"] if "material" in p else ""
        haut = mat in ("Haut",)
        for t in p.get("targets", []):
            if "NORMAL" in t:
                a = t["NORMAL"]
                if not haut:
                    # Accessor bleibt, aber leer (glTF verlangt gleiche Attribute je Target in allen Primitives nicht – Godot liest es so)
                    arr = np.zeros((accs[a]["count"], 3), np.float32)
                else:
                    arr = lesen(a)
                    arr[np.linalg.norm(arr, axis=1) < schwelle] = 0
                geaendert.add(a)
                accs[a]["_neu"] = arr

# Neuen Puffer bauen: unveränderte Accessoren/Bilder kopieren ihre Views, geänderte werden dünn neu geschrieben
for i, A in enumerate(accs):
    if "_neu" in A:
        sparse_schreiben(i, A.pop("_neu"))
        continue
    if "bufferView" in A:
        A["bufferView"] = view_kopieren(A["bufferView"])
    if "sparse" in A:
        s = A["sparse"]
        s["indices"]["bufferView"] = view_kopieren(s["indices"]["bufferView"])
        s["values"]["bufferView"] = view_kopieren(s["values"]["bufferView"])
for im in j.get("images", []):
    if "bufferView" in im:
        im["bufferView"] = view_kopieren(im["bufferView"])
j["bufferViews"] = neu_views
while len(neu_b) % 4:
    neu_b.append(0)
j["buffers"] = [{"byteLength": len(neu_b)}]
js = json.dumps(j, separators=(",", ":")).encode()
js += b" " * ((4 - len(js) % 4) % 4)
with open(aus, "wb") as f:
    f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(neu_b)))
    f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
    f.write(struct.pack("<II", len(neu_b), 0x004E4942)); f.write(neu_b)
print(f"GLB {vorher/1e6:.1f} MB -> {(28 + len(js) + len(neu_b))/1e6:.1f} MB, Normalen-Formen dünner: {len(geaendert)}")
