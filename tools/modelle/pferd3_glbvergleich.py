"""Zugpferd, Folge 3: Prueft, dass sich zwischen zwei Spiel-GLB nur die Bilder unterscheiden (Netze, Haut, Knoten, Clips byte-gleich)
und haeutet alle Clips in allen Bildern in beiden Dateien (groesste Abweichung der Ecken in mm, Soll 0).

    python -I tools/modelle/pferd3_glbvergleich.py <alt.glb> <neu.glb>
"""
import json
import os
import struct
import sys
import types

import numpy as np

for _n in ("bpy", "bmesh", "mathutils"):
    sys.modules.setdefault(_n, types.ModuleType(_n))
sys.modules["mathutils"].Vector = object
sys.modules["mathutils"].Matrix = object
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
_argv = sys.argv
sys.argv = sys.argv[:1]
import pferd2_spielrender as R  # noqa: E402
sys.argv = _argv


def teile(pfad):
    raw = open(pfad, "rb").read()
    jl = struct.unpack("<I", raw[12:16])[0]
    j = json.loads(raw[20:20 + jl])
    b = raw[28 + jl:]
    return j, b


def main():
    a, b = sys.argv[1], sys.argv[2]
    ja, ba = teile(a)
    jb, bb = teile(b)
    bild_bv = {im["bufferView"] for im in ja["images"]}
    fehler = 0
    for k in ("nodes", "meshes", "skins", "animations", "accessors", "materials", "textures", "samplers", "scenes"):
        if ja.get(k) != jb.get(k):
            print("JSON unterschiedlich:", k)
            fehler += 1
    for k, (va, vb) in enumerate(zip(ja["bufferViews"], jb["bufferViews"])):
        if k in bild_bv:
            continue
        da = ba[va.get("byteOffset", 0):va.get("byteOffset", 0) + va["byteLength"]]
        db = bb[vb.get("byteOffset", 0):vb.get("byteOffset", 0) + vb["byteLength"]]
        if da != db:
            print("Puffer unterschiedlich:", k)
            fehler += 1
    pa, pb = R.Pferd(a), R.Pferd(b)
    groesste, bilder = 0.0, 0
    for name, an in pa.ani.items():
        acc0 = ja["accessors"][an["samplers"][0]["input"]]["count"]
        for i in range(acc0):
            qa, _ = pa.pose(name, i)
            qb, _ = pb.pose(name, i)
            groesste = max(groesste, float(np.abs(qa - qb).max()) * 1000)
            bilder += 1
    qa, _ = pa.pose(None, 0)
    qb, _ = pb.pose(None, 0)
    groesste = max(groesste, float(np.abs(qa - qb).max()) * 1000)
    print(f"{os.path.basename(b)}: Clips {len(pa.ani)}, Bilder {bilder}, groesste Abweichung {groesste:.4f} mm, Unterschiede ausser Bildern: {fehler}")
    sys.exit(1 if fehler or groesste > 0.001 else 0)


if __name__ == "__main__":
    main()
