"""Prüft, dass passanten_idles.py in den Passanten-Dateien nur „Stehen“ geändert hat: Netze, Skelett, Stoffe, Bilder und alle übrigen
Clips müssen Byte für Byte gleich sein (Inhalt jedes Accessors, unabhängig von seiner Lage im Puffer). Ohne Blender:

    python tools/modelle/passanten_idles_vergleich.py <Ordner alt> <Ordner neu> [--geaendert=Stehen] [--netze=<Muster>]

`--netze` (regulärer Ausdruck auf „<datei>/<netz>“) nennt Netze, die sich absichtlich ändern dürfen (passanten_netze_anpassen.py:
nur die Eckenlagen); sie werden mit „geändert“ aufgeführt, Aufbau, Gewichte und alles andere bleiben geprüft.
Ausgabe je Datei: gleich / Unterschiede; Rückgabewert 1, wenn etwas außer den erlaubten Clips und Netzen abweicht.
Mit --angehaengt=1 (für passanten_arbeit.py) dürfen hinten neue Clips, Netze und Knoten (Kinder des Skelett-Knotens) hinzukommen;
alles Vorhandene muss weiter gleich sein.
"""

import json
import os
import struct
import sys

_KOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
_BYTES = {5126: 4, 5125: 4, 5123: 2, 5122: 2, 5121: 1, 5120: 1}


def lesen(pfad):
    with open(pfad, "rb") as f:
        d = f.read()
    gesamt = struct.unpack("<I", d[8:12])[0]
    pos, j, b = 12, None, b""
    while pos < gesamt:
        n, art = struct.unpack("<II", d[pos:pos + 8])
        teil = d[pos + 8:pos + 8 + n]
        if art == 0x4E4F534A:
            j = json.loads(teil.decode("utf-8"))
        elif art == 0x004E4942:
            b = teil
        pos += 8 + n
    return j, b


def accessor(j, b, i):
    """Beschreibung (ohne Pufferlage) und Inhalt eines Accessors."""
    a = dict(j["accessors"][i])
    bv = j["bufferViews"][a.pop("bufferView")]
    start = bv.get("byteOffset", 0) + a.pop("byteOffset", 0)
    elem = _KOMP[a["type"]] * _BYTES[a["componentType"]]
    schritt = bv.get("byteStride", elem)
    roh = b"".join(b[start + k * schritt:start + k * schritt + elem] for k in range(a["count"]))
    return json.dumps(a, sort_keys=True), roh


def view(j, b, i):
    bv = j["bufferViews"][i]
    return b[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]


def ohne_verweise(j, knoten=None, kinder=None):
    """JSON ohne Accessoren, BufferViews, Puffer und Animationen (die werden einzeln über ihren Inhalt verglichen). Mit `knoten` nur die
    ersten so vielen Knoten, die Kinderlisten auf die ersten `kinder[i]` Einträge gekürzt (angehängte Knoten)."""
    rest = {k: v for k, v in j.items() if k not in ("accessors", "bufferViews", "buffers", "animations", "meshes", "skins", "images")}
    if knoten is not None:
        rest = dict(rest)
        rest["nodes"] = [dict(n) for n in j["nodes"][:knoten]]
        for i, n in enumerate(rest["nodes"]):
            if "children" in n:
                n["children"] = n["children"][:kinder[i]]
    return json.dumps(rest, sort_keys=True)


def vergleichen(alt, neu, geaendert, netze_erlaubt=None, angehaengt=False):
    import re
    ja, ba = lesen(alt)
    jn, bn = lesen(neu)
    fehler, netze_geaendert = [], []
    if angehaengt:
        n = len(ja["nodes"])
        kinder = [len(k.get("children", [])) for k in ja["nodes"]]
        if ohne_verweise(ja, n, kinder) != ohne_verweise(jn, n, kinder):
            fehler.append("Knoten, Szene oder Stoffe verschieden")
    elif ohne_verweise(ja) != ohne_verweise(jn):
        fehler.append("Knoten, Szene oder Stoffe verschieden")
    # Netze
    if len(ja.get("meshes", [])) != len(jn.get("meshes", [])) and not (angehaengt and len(jn.get("meshes", [])) >= len(ja.get("meshes", []))):
        fehler.append("Zahl der Netze verschieden")
    for ma, mn in zip(ja.get("meshes", []), jn.get("meshes", [])):
        if ma.get("name") != mn.get("name") or len(ma["primitives"]) != len(mn["primitives"]):
            fehler.append(f"Netz {ma.get('name')}: Aufbau verschieden")
            continue
        for pa, pn in zip(ma["primitives"], mn["primitives"]):
            if sorted(pa["attributes"]) != sorted(pn["attributes"]) or pa.get("material") != pn.get("material") or pa.get("mode") != pn.get("mode"):
                fehler.append(f"Netz {ma.get('name')}: Attribute/Stoff verschieden")
                continue
            paare = [(pa["attributes"][k], pn["attributes"][k]) for k in pa["attributes"]]
            if "indices" in pa:
                paare.append((pa["indices"], pn["indices"]))
            knoten = next(n["name"] for n in ja["nodes"] if n.get("mesh") == ja["meshes"].index(ma))
            erlaubt = netze_erlaubt is not None and re.fullmatch(netze_erlaubt, f"{os.path.basename(alt)}/{knoten}")
            for (x, y), attr in zip(paare, list(pa["attributes"]) + ["indices"]):
                ax, ay = accessor(ja, ba, x), accessor(jn, bn, y)
                if ax == ay:
                    continue
                if erlaubt and attr == "POSITION" and json.loads(ax[0])["count"] == json.loads(ay[0])["count"]:
                    netze_geaendert.append(knoten)
                    continue
                fehler.append(f"Netz {knoten}: {attr} verschieden")
                break
    # Skelett
    for sa, sn in zip(ja.get("skins", []), jn.get("skins", [])):
        if sa["joints"] != sn["joints"] or accessor(ja, ba, sa["inverseBindMatrices"]) != accessor(jn, bn, sn["inverseBindMatrices"]):
            fehler.append("Skelett verschieden")
    # Bilder
    for ia, inn in zip(ja.get("images", []), jn.get("images", [])):
        if {k: v for k, v in ia.items() if k != "bufferView"} != {k: v for k, v in inn.items() if k != "bufferView"} or \
                ("bufferView" in ia and view(ja, ba, ia["bufferView"]) != view(jn, bn, inn["bufferView"])):
            fehler.append(f"Bild {ia.get('name')} verschieden")
    # Animationen
    namen_a = [a["name"] for a in ja.get("animations", [])]
    namen_n = [a["name"] for a in jn.get("animations", [])]
    if namen_a != (namen_n[:len(namen_a)] if angehaengt else namen_n):
        fehler.append(f"Clips verschieden: {namen_a} / {namen_n}")
    gleich_clips = []
    for aa in ja.get("animations", []):
        if aa["name"] in geaendert:
            continue
        an = next((a for a in jn.get("animations", []) if a["name"] == aa["name"]), None)
        if an is None:
            continue

        def kanaele(j, b, a):
            aus = {}
            for c in a["channels"]:
                s = a["samplers"][c["sampler"]]
                aus[(j["nodes"][c["target"]["node"]]["name"], c["target"]["path"])] = (
                    s.get("interpolation", "LINEAR"), accessor(j, b, s["input"]), accessor(j, b, s["output"]))
            return aus

        if kanaele(ja, ba, aa) != kanaele(jn, bn, an):
            fehler.append(f"Clip {aa['name']} verschieden")
        else:
            gleich_clips.append(aa["name"])
    if angehaengt:
        netze_geaendert = netze_geaendert + [f"angehängt: {len(namen_n) - len(namen_a)} Clips, "
                                             f"{len(jn.get('meshes', [])) - len(ja.get('meshes', []))} Netze"]
    return fehler, gleich_clips, netze_geaendert


def hauptteil():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    geaendert, netze_erlaubt, angehaengt = {"Stehen"}, None, False
    for a in sys.argv[1:]:
        if a.startswith("--geaendert="):
            geaendert = set(x for x in a.split("=", 1)[1].split(",") if x)
        if a == "--angehaengt=1":
            angehaengt = True
        if a.startswith("--netze="):
            netze_erlaubt = a.split("=", 1)[1]
    alt, neu = args
    schlecht = 0
    for name in sorted(os.listdir(alt)):
        if not name.endswith(".glb") or not os.path.exists(os.path.join(neu, name)):
            continue
        fehler, gleich, netze_geaendert = vergleichen(os.path.join(alt, name), os.path.join(neu, name), geaendert, netze_erlaubt, angehaengt)
        if fehler:
            schlecht += 1
            print(f"{name}: UNTERSCHIEDE {fehler}")
        else:
            extra = f"; Eckenlagen absichtlich geändert / angehängt: {', '.join(netze_geaendert)}" if netze_geaendert else ""
            print(f"{name}: gleich (Netze, Skelett, Stoffe, {len(gleich)} Clips: {', '.join(gleich)}); geändert: {', '.join(sorted(geaendert))}{extra}")
    sys.exit(1 if schlecht else 0)


if __name__ == "__main__":
    hauptteil()
