#!/usr/bin/env python3
"""Normalen einzelner Formen (Morph Targets) aus einer .glb entfernen und die Datei neu packen (eigener Code, MIT).

Blenders glTF-Export kann Normalen nur für alle Formen oder für keine schreiben. Für den Basiskopf lohnen sie sich
nur bei der Mimik (Wangen blähen, Lächeln, Kiefer: Licht und Schatten folgen der Form); die Alters- und Genformen
sehen ohne sie gleich aus und machen die Datei doppelt so groß.

Aufruf: python3 -I tools/charakter/glb_formnormalen.py <ein.glb> <aus.glb> [ohne_vorsilben=makro_,gen_]
Nur Standardbibliothek; liest die Formnamen aus mesh.extras.targetNames (so schreibt sie Blender).
"""
import json
import struct
import sys


def lesen(pfad):
    with open(pfad, "rb") as f:
        daten = f.read()
    assert daten[:4] == b"glTF", "keine .glb"
    laenge = struct.unpack("<I", daten[12:16])[0]
    gltf = json.loads(daten[20:20 + laenge])
    rest = daten[20 + laenge:]
    bin_laenge = struct.unpack("<I", rest[:4])[0]
    assert rest[4:8] == b"BIN\x00"
    return gltf, bytearray(rest[8:8 + bin_laenge])


def schreiben(pfad, gltf, binaer):
    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    binaer = bytes(binaer) + b"\x00" * ((4 - len(binaer) % 4) % 4)
    gesamt = 12 + 8 + len(js) + 8 + len(binaer)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, gesamt))
        f.write(struct.pack("<I4s", len(js), b"JSON") + js)
        f.write(struct.pack("<I4s", len(binaer), b"BIN\x00") + binaer)


def main():
    ein, aus = sys.argv[1], sys.argv[2]
    optionen = dict(a.split("=", 1) for a in sys.argv[3:])
    vorsilben = tuple(optionen.get("ohne_vorsilben", "makro_,gen_").split(","))
    gltf, binaer = lesen(ein)
    entfernt = 0
    for netz in gltf.get("meshes", []):
        namen = netz.get("extras", {}).get("targetNames", [])
        for prim in netz["primitives"]:
            for i, ziel in enumerate(prim.get("targets", [])):
                if i < len(namen) and namen[i].startswith(vorsilben) and "NORMAL" in ziel:
                    del ziel["NORMAL"]
                    entfernt += 1
    # Noch benutzte Zugriffe und Pufferansichten sammeln
    benutzt_acc = set()
    for netz in gltf.get("meshes", []):
        for prim in netz["primitives"]:
            benutzt_acc.update(prim["attributes"].values())
            if "indices" in prim:
                benutzt_acc.add(prim["indices"])
            for ziel in prim.get("targets", []):
                benutzt_acc.update(ziel.values())
    for haut in gltf.get("skins", []):
        if "inverseBindMatrices" in haut:
            benutzt_acc.add(haut["inverseBindMatrices"])
    for anim in gltf.get("animations", []):
        for s in anim["samplers"]:
            benutzt_acc.update((s["input"], s["output"]))
    accs = gltf["accessors"]
    neu_acc = {alt: neu for neu, alt in enumerate(sorted(benutzt_acc))}
    benutzt_bv = set()
    for alt in neu_acc:
        a = accs[alt]
        if "bufferView" in a:
            benutzt_bv.add(a["bufferView"])
        if "sparse" in a:
            benutzt_bv.add(a["sparse"]["indices"]["bufferView"])
            benutzt_bv.add(a["sparse"]["values"]["bufferView"])
    for bild in gltf.get("images", []):
        if "bufferView" in bild:
            benutzt_bv.add(bild["bufferView"])
    # Puffer neu zusammensetzen (je Ansicht auf 4 Byte ausgerichtet)
    ansichten = gltf["bufferViews"]
    neu_bv, neuer_puffer, liste_bv = {}, bytearray(), []
    for alt in sorted(benutzt_bv):
        v = dict(ansichten[alt])
        neuer_puffer += b"\x00" * ((4 - len(neuer_puffer) % 4) % 4)
        start = v.get("byteOffset", 0)
        stueck = binaer[start:start + v["byteLength"]]
        v["byteOffset"] = len(neuer_puffer)
        v["buffer"] = 0
        neuer_puffer += stueck
        neu_bv[alt] = len(liste_bv)
        liste_bv.append(v)
    liste_acc = []
    for alt in sorted(benutzt_acc):
        a = dict(accs[alt])
        if "bufferView" in a:
            a["bufferView"] = neu_bv[a["bufferView"]]
        if "sparse" in a:
            sp = json.loads(json.dumps(a["sparse"]))
            sp["indices"]["bufferView"] = neu_bv[sp["indices"]["bufferView"]]
            sp["values"]["bufferView"] = neu_bv[sp["values"]["bufferView"]]
            a["sparse"] = sp
        liste_acc.append(a)
    for netz in gltf.get("meshes", []):
        for prim in netz["primitives"]:
            prim["attributes"] = {k: neu_acc[v] for k, v in prim["attributes"].items()}
            if "indices" in prim:
                prim["indices"] = neu_acc[prim["indices"]]
            prim["targets"] = [{k: neu_acc[v] for k, v in z.items()} for z in prim.get("targets", [])]
            if not prim["targets"]:
                del prim["targets"]
    for haut in gltf.get("skins", []):
        if "inverseBindMatrices" in haut:
            haut["inverseBindMatrices"] = neu_acc[haut["inverseBindMatrices"]]
    for anim in gltf.get("animations", []):
        for s in anim["samplers"]:
            s["input"], s["output"] = neu_acc[s["input"]], neu_acc[s["output"]]
    for bild in gltf.get("images", []):
        if "bufferView" in bild:
            bild["bufferView"] = neu_bv[bild["bufferView"]]
    gltf["accessors"] = liste_acc
    gltf["bufferViews"] = liste_bv
    gltf["buffers"] = [{"byteLength": len(neuer_puffer)}]
    schreiben(aus, gltf, neuer_puffer)
    print(f"Normalen von {entfernt} Formen entfernt (Vorsilben {', '.join(vorsilben)}): "
          f"{len(binaer) / 1e6:.1f} MB -> {len(neuer_puffer) / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
