#!/usr/bin/env python3
"""Kurzer Überblick über eine .glb: Netze, Ecken, Formen (Blendshapes), Stoffe, Bilder und Größenanteile.

Aufruf: python3 -I tools/charakter/glb_info.py <datei.glb>
"""
import json
import struct
import sys

with open(sys.argv[1], "rb") as f:
    daten = f.read()
laenge = struct.unpack("<I", daten[12:16])[0]
gltf = json.loads(daten[20:20 + laenge])
ansichten = gltf.get("bufferViews", [])
zugriffe = gltf.get("accessors", [])


def bytes_von(acc_index):
    a = zugriffe[acc_index]
    return ansichten[a["bufferView"]]["byteLength"] if "bufferView" in a else 0


gesamt = {"ecken": 0, "formen": 0, "bilder": 0, "index": 0}
for m in gltf.get("meshes", []):
    for p in m["primitives"]:
        n = zugriffe[p["attributes"]["POSITION"]]["count"]
        tri = zugriffe[p["indices"]]["count"] // 3 if "indices" in p else 0
        attr_b = sum(bytes_von(i) for i in p["attributes"].values())
        ziel_b = sum(bytes_von(i) for t in p.get("targets", []) for i in t.values())
        gesamt["ecken"] += attr_b
        gesamt["formen"] += ziel_b
        gesamt["index"] += bytes_von(p["indices"]) if "indices" in p else 0
        stoff = gltf["materials"][p["material"]]["name"] if "material" in p else "-"
        print(f"{m['name']:<22} {n:>6} Ecken {tri:>6} Dreiecke {len(p.get('targets', [])):>3} Formen "
              f"({ziel_b / 1e6:5.2f} MB, Attribute {sorted(p['attributes'])}, Ziel {sorted(p['targets'][0]) if p.get('targets') else '-'}) Stoff {stoff}")
for b in gltf.get("images", []):
    gr = ansichten[b["bufferView"]]["byteLength"] if "bufferView" in b else 0
    gesamt["bilder"] += gr
    print(f"Bild {b.get('name', '?'):<30} {b.get('mimeType', '?')} {gr / 1e6:.2f} MB")
for s in gltf.get("materials", []):
    pbr = s.get("pbrMetallicRoughness", {})
    print(f"Stoff {s['name']:<28} alpha={s.get('alphaMode', 'OPAQUE')} farbe_textur={'baseColorTexture' in pbr} "
          f"normal={'normalTexture' in s} rauheit={pbr.get('roughnessFactor', 1)} metall={pbr.get('metallicFactor', 1)}")
print("Größe:", {k: round(v / 1e6, 2) for k, v in gesamt.items()}, "MB; Datei", round(len(daten) / 1e6, 2), "MB")
