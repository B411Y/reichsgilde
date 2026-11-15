"""Prüft den Neubau der Passanten mit Fingerknochen (Nutzer 14.11.2026, „Finger C“) gegen die Dateien ohne Finger. Ohne Blender:

    python tools/modelle/passanten_finger_vergleich.py <Ordner alt> <Ordner neu>

Gleich sein müssen (Byte für Byte, über Namen zugeordnet, weil die Fingerknochen die Knochennummern verschieben):
  * alle Knoten der alten Datei (Lage, Drehung, Größe, Kinder ohne die neuen Fingerknochen),
  * die Bindungsmatrizen der 23 alten Knochen,
  * jedes Netz in allem außer JOINTS_0/WEIGHTS_0 (Lagen, Normalen, Farben, Zonen, Dreiecke),
  * jeder Kanal der alten Clips (Knochen, Art, Zeiten, Werte); Clips, die Blender-Skripte nach dem Neubau aus der importierten Datei
    rechnen (passanten_clips.py, passanten_idles.py), dürfen um Rundung abweichen (höchstens RUNDUNG je Wert): Der Import legt die
    Handknochen mit Fingerkindern minimal anders an. Die Abweichung wird je Clip genannt.
Gewichte: Fingergewichte werden auf die Hand zurückgelegt und mit den alten verglichen (größte Abweichung, wegen der Grenze von 4
Knochen je Ecke nicht immer 0). Die neuen Fingerkanäle der Clips müssen konstant sein.
Rückgabewert 1 bei einem Unterschied.
"""

import json
import os
import struct
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from passanten_idles_vergleich import accessor, lesen  # noqa: E402

RUNDUNG = 1e-4
_FMT = {5126: "f", 5123: "H", 5121: "B"}
_KOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
FINGER = ("Thumb", "Index", "Middle", "Ring", "Little")


def ist_finger(name):
    return any(name.startswith(s + f) for s in ("Left", "Right") for f in FINGER)


def hand_von(name):
    return ("Left" if name.startswith("Left") else "Right") + "Hand" if ist_finger(name) else name


def werte(j, b, i):
    a = j["accessors"][i]
    bv = j["bufferViews"][a["bufferView"]]
    k = _KOMP[a["type"]]
    st = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    roh = struct.unpack_from("<" + _FMT[a["componentType"]] * (a["count"] * k), b, st)
    if a.get("normalized") and a["componentType"] == 5121:
        roh = [x / 255 for x in roh]
    elif a.get("normalized") and a["componentType"] == 5123:
        roh = [x / 65535 for x in roh]
    return [roh[x * k:(x + 1) * k] for x in range(a["count"])]


def gewichte(j, b, prim, namen):
    jo = werte(j, b, prim["attributes"]["JOINTS_0"])
    we = werte(j, b, prim["attributes"]["WEIGHTS_0"])
    aus = []
    for js, ws in zip(jo, we):
        d = {}
        for x, w in zip(js, ws):
            if w > 0:
                n = hand_von(namen[x])
                d[n] = d.get(n, 0.0) + w
        aus.append(d)
    return aus


def vergleichen(alt, neu):
    ja, ba = lesen(alt)
    jn, bn = lesen(neu)
    fehler, info = [], {}
    kn_a = {n["name"]: n for n in ja["nodes"]}
    kn_n = {n["name"]: n for n in jn["nodes"]}
    for name, na in kn_a.items():
        nn = kn_n.get(name)
        if nn is None:
            fehler.append(f"Knoten {name} fehlt")
            continue
        for k in set(na) | set(nn):
            if k in ("children", "mesh", "skin"):
                continue
            if na.get(k) != nn.get(k):
                fehler.append(f"Knoten {name}: {k} verschieden")
        ka = [ja["nodes"][i]["name"] for i in na.get("children", [])]
        kn = [jn["nodes"][i]["name"] for i in nn.get("children", []) if not ist_finger(jn["nodes"][i]["name"])]
        if ka != kn:
            fehler.append(f"Knoten {name}: Kinder verschieden")
    neue_knochen = [n for n in kn_n if n not in kn_a]
    info["neue_knochen"] = len(neue_knochen)
    # Haut
    gel_a = [ja["nodes"][i]["name"] for i in ja["skins"][0]["joints"]]
    gel_n = [jn["nodes"][i]["name"] for i in jn["skins"][0]["joints"]]
    ibm_a = werte(ja, ba, ja["skins"][0]["inverseBindMatrices"])
    ibm_n = werte(jn, bn, jn["skins"][0]["inverseBindMatrices"])
    for i, n in enumerate(gel_a):
        if n not in gel_n or ibm_a[i] != ibm_n[gel_n.index(n)]:
            fehler.append(f"Bindung {n} verschieden")
    info["knochen"] = f"{len(gel_a)} -> {len(gel_n)}"
    # Netze
    me_n = {m["name"]: m for m in jn["meshes"]}
    max_gew = 0.0
    for ma in ja["meshes"]:
        mn = me_n.get(ma["name"])
        if mn is None:
            fehler.append(f"Netz {ma['name']} fehlt")
            continue
        for pa, pn in zip(ma["primitives"], mn["primitives"]):
            for attr in pa["attributes"]:
                if attr in ("JOINTS_0", "WEIGHTS_0"):
                    continue
                if accessor(ja, ba, pa["attributes"][attr]) != accessor(jn, bn, pn["attributes"][attr]):
                    fehler.append(f"Netz {ma['name']}: {attr} verschieden")
            if accessor(ja, ba, pa["indices"]) != accessor(jn, bn, pn["indices"]):
                fehler.append(f"Netz {ma['name']}: Dreiecke verschieden")
            if "JOINTS_0" in pa:
                ga, gn = gewichte(ja, ba, pa, gel_a), gewichte(jn, bn, pn, gel_n)
                for da, dn in zip(ga, gn):
                    for k in set(da) | set(dn):
                        max_gew = max(max_gew, abs(da.get(k, 0.0) - dn.get(k, 0.0)))
    info["gewichte_max_abweichung"] = round(max_gew, 4)
    if max_gew > 0.02:
        fehler.append(f"Gewichte weichen um {max_gew:.3f} ab")
    # Clips
    an_n = {a["name"]: a for a in jn["animations"]}

    def kanaele(j, b, a):
        aus = {}
        for c in a["channels"]:
            s = a["samplers"][c["sampler"]]
            aus[(j["nodes"][c["target"]["node"]]["name"], c["target"]["path"])] = (
                s.get("interpolation", "LINEAR"), accessor(j, b, s["input"]), accessor(j, b, s["output"]), s["output"])
        return aus

    nicht_konstant, gerundet = [], {}
    for aa in ja["animations"]:
        an = an_n.get(aa["name"])
        if an is None:
            fehler.append(f"Clip {aa['name']} fehlt")
            continue
        ka, kn = kanaele(ja, ba, aa), kanaele(jn, bn, an)
        for schl, wert in ka.items():
            if schl not in kn:
                fehler.append(f"Clip {aa['name']}: Kanal {schl} fehlt")
                break
            if kn[schl][:3] == wert[:3]:
                continue
            wa, wn = werte(ja, ba, wert[3]), werte(jn, bn, kn[schl][3])
            if kn[schl][:2] != wert[:2] or len(wa) != len(wn):
                fehler.append(f"Clip {aa['name']}: Kanal {schl} verschieden")
                break
            d = max(abs(x - y) for u, v in zip(wa, wn) for x, y in zip(u, v))
            if d > RUNDUNG:
                fehler.append(f"Clip {aa['name']}: Kanal {schl} weicht um {d:.2e} ab")
                break
            gerundet[aa["name"]] = max(gerundet.get(aa["name"], 0.0), d)
        for (knoten, pfad), wert in kn.items():
            if ist_finger(knoten) and pfad == "rotation":
                w = werte(jn, bn, wert[3])
                if any(max(abs(x - y) for x, y in zip(v, w[0])) > 1e-6 for v in w):
                    nicht_konstant.append(f"{aa['name']}/{knoten}")
    if nicht_konstant:
        fehler.append(f"Fingerkanäle nicht konstant: {nicht_konstant[:5]}")
    info["clips"] = len(ja["animations"])
    info["clips_bitgleich"] = len(ja["animations"]) - len(gerundet)
    info["clips_gerundet"] = {k: float(f"{v:.1e}") for k, v in gerundet.items()}
    return fehler, info


def hauptteil():
    alt, neu = sys.argv[1], sys.argv[2]
    schlecht = 0
    for name in sorted(os.listdir(alt)):
        if not name.endswith(".glb") or not os.path.exists(os.path.join(neu, name)):
            continue
        fehler, info = vergleichen(os.path.join(alt, name), os.path.join(neu, name))
        if fehler:
            schlecht += 1
            print(f"{name}: UNTERSCHIEDE {fehler[:12]} {json.dumps(info)}")
        else:
            print(f"{name}: gleich bis auf die Finger {json.dumps(info, ensure_ascii=False)}")
    sys.exit(1 if schlecht else 0)


if __name__ == "__main__":
    hauptteil()
