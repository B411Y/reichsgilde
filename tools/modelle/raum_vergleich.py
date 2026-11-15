"""Vergleicht zwei Fassungen eines Raummodells (glb) Knoten für Knoten: Namen, Extras, Lage/Drehung/Maßstab und den Inhalt der Netze.
Ohne Blender:

    python tools/modelle/raum_vergleich.py <alt.glb> <neu.glb>

Ausgabe: welche Knoten neu, weg oder anders sind (Lage, Extras, Netz); alles andere ist gleich. Gedacht für bewusst kleine Änderungen
an den Raumskripten (tools/modelle/<beruf>_innen.py), z. B. verschobene Sitzanker: Brand-, Licht-, Fenster- und Bedienknoten müssen gleich
bleiben.
"""
import hashlib
import json
import struct
import sys


def lesen(pfad):
    d = open(pfad, "rb").read()
    n = struct.unpack("<I", d[12:16])[0]
    j = json.loads(d[20:20 + n])
    off = 20 + n
    m = struct.unpack("<I", d[off:off + 4])[0]
    return j, d[off + 8:off + 8 + m]


def acc_hash(j, b, i):
    a = j["accessors"][i]
    bv = j["bufferViews"][a["bufferView"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    groesse = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]] * {5126: 4, 5125: 4, 5123: 2, 5121: 1}[a["componentType"]]
    return hashlib.sha1(b[start:start + groesse * a["count"]]).hexdigest()


def knoten(j, b):
    erg = {}
    for nd in j["nodes"]:
        netz = None
        if "mesh" in nd:
            m = j["meshes"][nd["mesh"]]
            netz = [sorted((k, acc_hash(j, b, v)) for k, v in p["attributes"].items()) + [("i", acc_hash(j, b, p["indices"]) if "indices" in p else "")]
                    for p in m["primitives"]]
        erg[nd.get("name", "")] = {"lage": [nd.get("translation"), nd.get("rotation"), nd.get("scale")], "extras": nd.get("extras"), "netz": netz}
    return erg


def main():
    a, b = (knoten(*lesen(p)) for p in sys.argv[1:3])
    neu = sorted(set(b) - set(a))
    weg = sorted(set(a) - set(b))
    anders = []
    for k in sorted(set(a) & set(b)):
        was = [f for f in ("lage", "extras", "netz") if a[k][f] != b[k][f]]
        if was:
            anders.append(f"{k} ({', '.join(was)})")
    print(f"{sys.argv[2]}: {len(a)} Knoten vorher, {len(b)} nachher")
    print("  neu:", ", ".join(neu) or "–")
    print("  weg:", ", ".join(weg) or "–")
    print("  anders:", ", ".join(anders) or "–")
    print(f"  gleich: {len(set(a) & set(b)) - len(anders)}")


if __name__ == "__main__":
    main()
