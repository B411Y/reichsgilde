"""Fingerknochen für die fertigen Passanten-Dateien (Nutzer 14.11.2026, „Finger C mit 3 Stufen“), ohne Blender:

    python tools/modelle/passanten_finger.py <Ordner ein> <Ordner aus> --quelle=<Ordner mit Probe-Neubau mit Fingern> [--nur=<datei,…>]

Je Hand 10 Knochen (Daumen und vier Finger je 2: ThumbMetacarpal/ThumbProximal, Index/Middle/Ring/LittleProximal und …Intermediate;
Namen nach Godots SkeletonProfileHumanoid), Ruhelage = die locker gekrümmte Hand der Netze. Die Knochen und ihre Gewichte kommen aus
einem Neubau derselben Figur mit `passanten_probe.py` (das legt die Fingergewichte seit 14.11.2026 nicht mehr in die Hand):

  * Knoten: die 20 Fingerknoten mit ihrer Lage zum Handknoten werden angehängt (Kinder der Hand bzw. des ersten Glieds), ihre Gelenke
    kommen HINTER die vorhandenen in die Haut – die bisherigen Knochennummern bleiben, die Bindungsmatrizen der neuen werden aus der
    Ruhelage der Datei gerechnet.
  * Gewichte: Nur Ecken mit Handgewicht ändern sich: Die Ecke sucht im Neubau die nächste Ecke desselben Netzes (Lage im Raum des
    Handknochens) und teilt ihr Handgewicht im Verhältnis Hand/Finger dieser Ecke auf (höchstens 4 Knochen, normiert). Alle übrigen
    Netzdaten (Lagen, Normalen, Farben, Zonen, Dreiecke) bleiben Byte für Byte.
  * Clips: Jeder vorhandene Clip ohne Fingerspuren bekommt je Fingerknochen eine konstante Drehung (die Ruhelage), damit nach einem
    Greif-Clip keine Fingerhaltung stehen bleibt. Die Kanäle der übrigen Knochen bleiben Byte für Byte.

Reihenfolge: passanten_probe.py → passanten_clips.py → passanten_netze_anpassen.py → passanten_idles.py → dieses Skript →
passanten_arbeit.py. Hat eine Datei schon Fingerknochen, legt das Skript nur fehlende Ruhespuren an. Prüfung:
tools/modelle/passanten_finger_vergleich.py (alt gegen neu).
"""

import json
import math
import os
import struct
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from passanten_idles_vergleich import lesen  # noqa: E402

FINGER = ("Thumb", "Index", "Middle", "Ring", "Little")
_FMT = {5126: "f", 5121: "B", 5123: "H"}
_KOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def ist_finger(name):
    return any(name.startswith(s + f) for s in ("Left", "Right") for f in FINGER)


def werte(j, b, i):
    a = j["accessors"][i]
    bv = j["bufferViews"][a["bufferView"]]
    k = _KOMP[a["type"]]
    st = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    roh = struct.unpack_from("<" + _FMT[a["componentType"]] * (a["count"] * k), b, st)
    return [list(roh[x * k:(x + 1) * k]) for x in range(a["count"])]


def anhaengen(j, b, daten, ctype, typ, minmax=False, ziel=None):
    """Neuer Accessor mit eigenem BufferView am Ende des Puffers."""
    flach = [x for e in daten for x in e]
    roh = struct.pack("<" + _FMT[ctype] * len(flach), *flach)
    while len(b) % 4:
        b.append(0)
    bv = {"buffer": 0, "byteOffset": len(b), "byteLength": len(roh)}
    if ziel:
        bv["target"] = ziel
    j["bufferViews"].append(bv)
    b.extend(roh)
    a = {"bufferView": len(j["bufferViews"]) - 1, "componentType": ctype, "count": len(daten), "type": typ}
    if minmax:
        a["min"] = [min(e[i] for e in daten) for i in range(len(daten[0]))]
        a["max"] = [max(e[i] for e in daten) for i in range(len(daten[0]))]
    j["accessors"].append(a)
    return len(j["accessors"]) - 1


# ── 4×4-Matrizen (Zeilen) ───────────────────────────────────────────────────

def mat_mal(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def lokal(n):
    """Matrix eines glTF-Knotens aus translation/rotation/scale."""
    if "matrix" in n:
        m = n["matrix"]
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    x, y, z, w = n.get("rotation", [0, 0, 0, 1])
    sx, sy, sz = n.get("scale", [1, 1, 1])
    tx, ty, tz = n.get("translation", [0, 0, 0])
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[0][0] * sx, r[0][1] * sy, r[0][2] * sz, tx], [r[1][0] * sx, r[1][1] * sy, r[1][2] * sz, ty],
            [r[2][0] * sx, r[2][1] * sy, r[2][2] * sz, tz], [0, 0, 0, 1]]


def invers(m):
    """Inverse einer affinen Matrix (Drehung·Größe + Lage) über die allgemeine 3×3-Inverse."""
    a = [row[:3] for row in m[:3]]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1]) - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
           + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    inv = [[(a[(j + 1) % 3][(i + 1) % 3] * a[(j + 2) % 3][(i + 2) % 3] - a[(j + 1) % 3][(i + 2) % 3] * a[(j + 2) % 3][(i + 1) % 3]) / det
            for j in range(3)] for i in range(3)]
    t = [m[i][3] for i in range(3)]
    it = [-sum(inv[i][k] * t[k] for k in range(3)) for i in range(3)]
    return [inv[0] + [it[0]], inv[1] + [it[1]], inv[2] + [it[2]], [0, 0, 0, 1]]


def punkt(m, p):
    return [sum(m[i][k] * p[k] for k in range(3)) + m[i][3] for i in range(3)]


def welt_matrizen(j):
    eltern = {c: i for i, n in enumerate(j["nodes"]) for c in n.get("children", [])}
    cache = {}

    def g(i):
        if i not in cache:
            cache[i] = lokal(j["nodes"][i]) if i not in eltern else mat_mal(g(eltern[i]), lokal(j["nodes"][i]))
        return cache[i]
    return g


def ibm_lesen(j, b):
    return [[[m[c * 4 + r] for c in range(4)] for r in range(4)] for m in werte(j, b, j["skins"][0]["inverseBindMatrices"])]


def basisname(name):
    return name.split(".")[0]


# ── glTF schreiben (wie passanten_clips.glb_schreiben / passanten_idles.unbenutztes_entfernen, hier ohne Blender) ──

def glb_schreiben(pfad, j, b):
    b = bytes(b) + b"\0" * (-len(b) % 4)
    j["buffers"][0]["byteLength"] = len(b)
    text = json.dumps(j, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    text += b" " * (-len(text) % 4)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(text) + 8 + len(b)))
        f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
        f.write(struct.pack("<II", len(b), 0x004E4942) + b)


def unbenutztes_entfernen(j, b):
    """Accessoren und BufferViews, auf die nichts mehr zeigt (ersetzte Gewichte und Bindungen), entfernen; Puffer neu packen. Die Bytes
    jedes übrigen BufferViews bleiben gleich."""
    benutzt = set()
    for m in j.get("meshes", []):
        for p in m["primitives"]:
            benutzt.update(p["attributes"].values())
            if "indices" in p:
                benutzt.add(p["indices"])
            for t in p.get("targets", []):
                benutzt.update(t.values())
    for s in j.get("skins", []):
        if "inverseBindMatrices" in s:
            benutzt.add(s["inverseBindMatrices"])
    for a in j.get("animations", []):
        for s in a["samplers"]:
            benutzt.update((s["input"], s["output"]))
    acc_neu = {alt: neu for neu, alt in enumerate(sorted(benutzt))}
    bv_benutzt = {j["accessors"][i]["bufferView"] for i in benutzt if "bufferView" in j["accessors"][i]}
    bv_benutzt |= {img["bufferView"] for img in j.get("images", []) if "bufferView" in img}
    bv_neu = {alt: neu for neu, alt in enumerate(sorted(bv_benutzt))}
    puffer, views = bytearray(), []
    for alt in sorted(bv_benutzt):
        v = dict(j["bufferViews"][alt])
        start = v.get("byteOffset", 0)
        roh = b[start:start + v["byteLength"]]
        while len(puffer) % 4:
            puffer.append(0)
        v["byteOffset"] = len(puffer)
        puffer.extend(roh)
        views.append(v)
    accs = []
    for alt in sorted(benutzt):
        a = dict(j["accessors"][alt])
        if "bufferView" in a:
            a["bufferView"] = bv_neu[a["bufferView"]]
        accs.append(a)
    j["accessors"], j["bufferViews"] = accs, views
    for m in j.get("meshes", []):
        for p in m["primitives"]:
            p["attributes"] = {k: acc_neu[v] for k, v in p["attributes"].items()}
            if "indices" in p:
                p["indices"] = acc_neu[p["indices"]]
            if "targets" in p:
                p["targets"] = [{k: acc_neu[v] for k, v in t.items()} for t in p["targets"]]
    for s in j.get("skins", []):
        if "inverseBindMatrices" in s:
            s["inverseBindMatrices"] = acc_neu[s["inverseBindMatrices"]]
    for a in j.get("animations", []):
        for s in a["samplers"]:
            s["input"], s["output"] = acc_neu[s["input"]], acc_neu[s["output"]]
    for img in j.get("images", []):
        if "bufferView" in img:
            img["bufferView"] = bv_neu[img["bufferView"]]
    return j, puffer


# ── Datei bearbeiten ────────────────────────────────────────────────────────

def bearbeiten(ein, aus, quelle):
    j, b = lesen(ein)
    b = bytearray(b)
    namen = [n.get("name") for n in j["nodes"]]
    bericht = {}
    if not any(ist_finger(n or "") for n in namen):
        qj, qb = lesen(quelle)
        qb = bytearray(qb)
        qnamen = [n.get("name") for n in qj["nodes"]]
        q_eltern = {c: i for i, n in enumerate(qj["nodes"]) for c in n.get("children", [])}
        # 1. Knoten anhängen (in der Reihenfolge der Quelle, Eltern vor Kindern)
        neu_idx = {}
        for qi, n in enumerate(qj["nodes"]):
            if not ist_finger(n.get("name") or ""):
                continue
            neu = {k: v for k, v in n.items() if k in ("name", "rotation", "translation", "scale")}
            j["nodes"].append(neu)
            neu_idx[n["name"]] = len(j["nodes"]) - 1
        for name, i in neu_idx.items():
            qi = qnamen.index(name)
            eltern_name = qnamen[q_eltern[qi]]
            ei = neu_idx.get(eltern_name, namen.index(eltern_name) if eltern_name in namen else None)
            j["nodes"][ei].setdefault("children", []).append(i)
        # 2. Gelenke und Bindungen
        g = welt_matrizen(j)
        skin = j["skins"][0]
        alt_gelenke = list(skin["joints"])
        ibm = ibm_lesen(j, b)
        # Bezug: die Bindung der Hand muss zur Weltmatrix passen (sonst stimmt der Raum der Haut nicht)
        hand = namen.index("RightHand")
        pruef = mat_mal(ibm[alt_gelenke.index(hand)], g(hand))
        bericht["bindung_hand_fehler"] = round(max(abs(pruef[r][c] - (1.0 if r == c else 0.0)) for r in range(4) for c in range(4)), 6)
        for name, i in neu_idx.items():
            skin["joints"].append(i)
            ibm.append(invers(g(i)))
        skin["inverseBindMatrices"] = anhaengen(j, b, [[m[r][c] for c in range(4) for r in range(4)] for m in ibm], 5126, "MAT4")
        gelenk_name = [j["nodes"][i]["name"] for i in skin["joints"]]
        q_gelenk_name = [qj["nodes"][i]["name"] for i in qj["skins"][0]["joints"]]
        q_ibm = ibm_lesen(qj, qb)
        # 3. Gewichte
        q_netze = {basisname(m["name"]): m for m in qj["meshes"]}
        geaendert = 0
        groesster_abstand = 0.0
        for m in j["meshes"]:
            qm = q_netze.get(basisname(m["name"]))
            for pi, p in enumerate(m["primitives"]):
                if "JOINTS_0" not in p["attributes"] or qm is None:
                    continue
                qp = qm["primitives"][pi]
                lagen = werte(j, b, p["attributes"]["POSITION"])
                gel = werte(j, b, p["attributes"]["JOINTS_0"])
                gew = werte(j, b, p["attributes"]["WEIGHTS_0"])
                q_lagen = werte(qj, qb, qp["attributes"]["POSITION"])
                q_gel = werte(qj, qb, qp["attributes"]["JOINTS_0"])
                q_gew = werte(qj, qb, qp["attributes"]["WEIGHTS_0"])
                for seite in ("Left", "Right"):
                    h = gelenk_name.index(f"{seite}Hand")
                    qh = q_gelenk_name.index(f"{seite}Hand")
                    hand_m, q_hand_m = ibm[h], q_ibm[qh]
                    # Quellecken mit Hand-/Fingergewicht dieser Seite, im Raum der Hand
                    kandidaten = []
                    for qi2, (js_, ws_) in enumerate(zip(q_gel, q_gew)):
                        teile = {}
                        for x, w in zip(js_, ws_):
                            n = q_gelenk_name[x]
                            if w > 0 and (n == f"{seite}Hand" or (ist_finger(n) and n.startswith(seite))):
                                teile[n] = teile.get(n, 0.0) + w
                        if teile and any(ist_finger(n) for n in teile):
                            kandidaten.append((punkt(q_hand_m, q_lagen[qi2]), teile))
                    if not kandidaten:
                        continue
                    for vi, (js_, ws_) in enumerate(zip(gel, gew)):
                        wh = sum(w for x, w in zip(js_, ws_) if x == h)
                        if wh <= 0:
                            continue
                        lp = punkt(hand_m, lagen[vi])
                        best, bd = None, 1e9
                        for kp, teile in kandidaten:
                            d = (kp[0] - lp[0]) ** 2 + (kp[1] - lp[1]) ** 2 + (kp[2] - lp[2]) ** 2
                            if d < bd:
                                best, bd = teile, d
                        bd = math.sqrt(bd)
                        if bd > 0.01:          # weiter als 1 cm: keine Fingerecke (Handgelenk, Ärmel)
                            continue
                        groesster_abstand = max(groesster_abstand, bd)
                        summe = sum(best.values())
                        neu = {}
                        for x, w in zip(js_, ws_):
                            if w > 0 and x != h:
                                neu[x] = neu.get(x, 0.0) + w
                        for n, w in best.items():
                            x = gelenk_name.index(n)
                            neu[x] = neu.get(x, 0.0) + wh * w / summe
                        top = sorted(neu.items(), key=lambda t: -t[1])[:4]
                        s = sum(w for _, w in top)
                        top = [(x, w / s) for x, w in top] + [(0, 0.0)] * (4 - len(top))
                        gel[vi] = [x for x, _ in top]
                        gew[vi] = [w for _, w in top]
                        geaendert += 1
                p["attributes"]["JOINTS_0"] = anhaengen(j, b, gel, 5121, "VEC4", ziel=34962)
                p["attributes"]["WEIGHTS_0"] = anhaengen(j, b, gew, 5126, "VEC4", ziel=34962)
        bericht["ecken_mit_fingergewicht"] = geaendert
        bericht["groesster_abstand_m"] = round(groesster_abstand, 5)
        bericht["knochen"] = f"{len(alt_gelenke)} -> {len(skin['joints'])}"
    # 4. Ruhespuren in Clips ohne Fingerspuren
    finger_knoten = [i for i, n in enumerate(j["nodes"]) if ist_finger(n.get("name") or "")]
    ergaenzt = 0
    for a in j.get("animations", []):
        da = {c["target"]["node"] for c in a["channels"] if c["target"]["path"] == "rotation"}
        fehlen = [i for i in finger_knoten if i not in da]
        if not fehlen:
            continue
        ende = max(j["accessors"][s["input"]]["max"][0] for s in a["samplers"])
        zeit = anhaengen(j, b, [[0.0], [ende]], 5126, "SCALAR", minmax=True)
        for i in fehlen:
            r = j["nodes"][i].get("rotation", [0.0, 0.0, 0.0, 1.0])
            a["samplers"].append({"input": zeit, "output": anhaengen(j, b, [r, r], 5126, "VEC4"), "interpolation": "LINEAR"})
            a["channels"].append({"sampler": len(a["samplers"]) - 1, "target": {"node": i, "path": "rotation"}})
        ergaenzt += 1
    bericht["clips_mit_ruhespuren"] = ergaenzt
    j, b = unbenutztes_entfernen(j, b)
    glb_schreiben(aus, j, b)
    return bericht


def hauptteil():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    ein, aus = args
    os.makedirs(aus, exist_ok=True)
    nur = opt.get("nur")
    erg = {}
    for name in sorted(os.listdir(ein)):
        if not name.endswith(".glb") or (nur and name not in nur.split(",")):
            continue
        erg[name] = bearbeiten(os.path.join(ein, name), os.path.join(aus, name), os.path.join(opt["quelle"], name))
        print(name, json.dumps(erg[name], ensure_ascii=False))


if __name__ == "__main__":
    hauptteil()
