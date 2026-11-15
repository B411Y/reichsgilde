"""Kleine Formkorrekturen an fertigen Passanten-Dateien (Nutzer 14.11.2026), ohne Blender: Es ändert nur die Eckenlagen (POSITION) einzelner
Netze in der .glb; Skelett, Gewichte, Stoffe und alle Clips bleiben Byte für Byte gleich.

    python tools/modelle/passanten_netze_anpassen.py <Ordner ein> <Ordner aus>

  * SCHUHE (Länge, Breite je Datei): Halb- und Holzschuh (Fuss_halb, Fuss_holz) der Menschen, Elben und Halblinge sind zu lang (Mann 0,325 m bei 1,72 m Körpergröße = 18,9 %;
    der nackte Fuß Fuss_barfuss misst 0,25 m): Sie werden in Fußrichtung um den Knöchel (Gelenk LeftFoot/RightFoot) gestaucht.
    Höhe bleibt, die Sohle bleibt auf derselben Höhe. Halblinge bekommen zusätzlich breitere Füße (alle drei Fußnetze, quer).
  * ROCK: Der Rock der Halblingin steht an der Hüfte breiter ab als die Figur: Gürtel und Hüfte des Netzes 0,53 m breit und 0,39 m tief
    (Frau 0,38 × 0,26, Zwergin 0,43 × 0,28) bei fast gleichem Abstand der Hüftgelenke, darüber die Taille nur 0,30 m – der Rock sitzt wie
    ein Wulst unter der Taille. Rock, Gürtel, Schürze und Tasche und der Körper an der Hüfte rücken zur Körpermitte (seitlich und
    nach vorn/hinten je ein Faktor, Verlauf in `anteil`). Ecken, die vor allem an Arm oder Hand hängen, bleiben.

Nicht wiederholt anwenden (jede Anwendung staucht weiter): Eingang sind die Dateien aus passanten_probe.py bzw. passanten_clips.py.
Reihenfolge nach einem Neubau: passanten_probe.py → passanten_clips.py → dieses Skript → passanten_idles.py.
glTF: Y oben, die Figur blickt nach +Z, links ist +X.
"""

import os
import struct
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
from passanten_idles_vergleich import lesen  # noqa: E402

# Datei → Faktor auf die Schuhlänge (Halb- und Holzschuh); Ziel etwa 16,5 % der Körpergröße
SCHUHE = {
    "passant_probe.glb": (0.88, 1.0), "passant_probe_frau.glb": (0.90, 1.0),
    # Nutzer 14.11.2026 (zweite Rückmeldung): Elben- und Halblingsschuhe ebenfalls kürzer, Halblinge mit etwas breiteren Füßen
    "passant_probe_elb.glb": (0.88, 1.0), "passant_probe_elbin.glb": (0.90, 1.0),
    "passant_probe_halbling.glb": (0.88, 1.12), "passant_probe_halblingin.glb": (0.90, 1.12),
}
SCHUH_NETZE = ("Fuss_halb", "Fuss_holz")          # werden kürzer
BREIT_NETZE = ("Fuss_halb", "Fuss_holz", "Fuss_barfuss")   # werden breiter (wenn der Breitenfaktor nicht 1 ist)

# Datei → (seitlich, vorn/hinten): Rock und Hüfte unterhalb der Taille
ROCK = {"passant_probe_halblingin.glb": (0.80, 0.82)}
ROCK_NETZE_OHNE = ("Fuss_", "Last", "Werkzeug", "Zub_stab", "Zub_kiepe", "Zub_buendel", "Zub_barett", "Zub_eisenhut", "Zub_stroh")
ARME = {"LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand"}


def mat4(werte):
    """glTF-Matrix (spaltenweise) → Zeilenliste."""
    return [[werte[c * 4 + r] for c in range(4)] for r in range(4)]


def invers_lage(m):
    """Lage des Gelenks im Bindungsraum: Translation der Inversen einer starren 4×4-Matrix (Rotation·Skalierung + Translation)."""
    import numpy as np
    return np.linalg.inv(np.array(m))[:3, 3]


def gelenke(j, b):
    import numpy as np
    skin = j["skins"][0]
    a = j["accessors"][skin["inverseBindMatrices"]]
    bv = j["bufferViews"][a["bufferView"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    werte = struct.unpack_from(f"<{16 * a['count']}f", b, start)
    aus = {}
    for k, idx in enumerate(skin["joints"]):
        aus[j["nodes"][idx]["name"]] = invers_lage(mat4(werte[16 * k:16 * k + 16]))
    namen = [j["nodes"][idx]["name"] for idx in skin["joints"]]
    return aus, namen


def feld(j, b, idx, art):
    """Liest einen Accessor als Liste von Tupeln (dicht gepackt oder mit Schrittweite); `art` nur zur Lesbarkeit."""
    a = j["accessors"][idx]
    bv = j["bufferViews"][a["bufferView"]]
    komp = {"VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
    fmt = {5126: "f", 5121: "B", 5123: "H"}[a["componentType"]]
    groesse = struct.calcsize(fmt) * komp
    schritt = bv.get("byteStride", groesse)
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from(f"<{komp}{fmt}", b, start + i * schritt) for i in range(a["count"])], (start, schritt, fmt, komp)


def schreiben(j, b, idx, werte, lage):
    start, schritt, fmt, komp = lage
    for i, v in enumerate(werte):
        struct.pack_into(f"<{komp}{fmt}", b, start + i * schritt, *v)
    a = j["accessors"][idx]
    a["min"] = [min(v[k] for v in werte) for k in range(3)]
    a["max"] = [max(v[k] for v in werte) for k in range(3)]


def netze(j):
    for n in j["nodes"]:
        if "mesh" in n:
            yield n["name"], j["meshes"][n["mesh"]]


def schuhe_stauchen(j, b, faktoren, bericht):
    """Länge (Fußrichtung, um den Knöchel) für Halb- und Holzschuh, Breite (quer, um die Linie Knöchel–Zehen) für alle Fußnetze.
    Die Breite wirkt voll bis 3 cm unter dem Knöchel und läuft bis zum Knöchel aus, damit der Schaft am Bein bleibt; Höhen bleiben."""
    faktor, breit = faktoren
    g, namen = gelenke(j, b)
    erledigt = set()
    for name, m in netze(j):
        laenger = name.startswith(SCHUH_NETZE)
        breiter = breit != 1.0 and name.startswith(BREIT_NETZE)
        if not (laenger or breiter):
            continue
        for p in m["primitives"]:
            if p["attributes"]["POSITION"] in erledigt:
                continue
            erledigt.add(p["attributes"]["POSITION"])
            pos, lage = feld(j, b, p["attributes"]["POSITION"], "pos")
            gel, _ = feld(j, b, p["attributes"]["JOINTS_0"], "j")
            gew, _ = feld(j, b, p["attributes"]["WEIGHTS_0"], "w")
            neu, laenge_vor = [], {}
            for v, jj, ww in zip(pos, gel, gew):
                seite = "Left" if v[0] > 0 else "Right"
                knoechel = g[f"{seite}Foot"]
                zehe = g[f"{seite}Toes"]
                rx, rz = zehe[0] - knoechel[0], zehe[2] - knoechel[2]          # Fußrichtung waagerecht (X, Z)
                l = (rx * rx + rz * rz) ** 0.5
                rx, rz = rx / l, rz / l
                dx, dz = v[0] - knoechel[0], v[2] - knoechel[2]
                t = dx * rx + dz * rz                                          # Anteil in Fußrichtung
                q = -dx * rz + dz * rx                                         # Anteil quer dazu
                t2 = t * faktor if laenger else t
                fb = 1.0
                if breiter:
                    fb = 1 + (breit - 1) * _weich((knoechel[1] - v[1]) / 0.03)
                q2 = q * fb
                neu.append((v[0] + (t2 - t) * rx - (q2 - q) * rz, v[1], v[2] + (t2 - t) * rz + (q2 - q) * rx))
            schreiben(j, b, p["attributes"]["POSITION"], neu, lage)
            links_alt = [v for v in pos if v[0] > 0]
            links_neu = [v for v in neu if v[0] > 0]
            bericht[name] = {"laenge_z_vorher": round(max(v[2] for v in links_alt) - min(v[2] for v in links_alt), 4),
                             "laenge_z_nachher": round(max(v[2] for v in links_neu) - min(v[2] for v in links_neu), 4),
                             "breite_x_vorher": round(max(v[0] for v in links_alt) - min(v[0] for v in links_alt), 4),
                             "breite_x_nachher": round(max(v[0] for v in links_neu) - min(v[0] for v in links_neu), 4),
                             "sohle_vorher": round(min(v[1] for v in pos), 4), "sohle_nachher": round(min(v[1] for v in neu), 4)}


def _weich(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def anteil(y, zone, huefte, taille):
    """Wie stark eine Ecke zur Mitte rückt (0 gar nicht, 1 voller Faktor). Rock, Gürtel und Zubehör (Zone 1 und 0): von unten bis zur
    Taille voll, darüber bis 6 cm über der Taille auslaufend (dort setzt der Rock breit am Gürtel an). Körper darunter (Beine, Haut):
    nur um die Hüfte voll, zum Oberschenkel hin (25 cm unter dem Hüftgelenk) auslaufend, damit Unterschenkel und Knöchel über den
    Schuhen bleiben."""
    oben = _weich((taille + 0.06 - y) / 0.06)
    if zone in (0, 1):
        return oben
    return min(oben, _weich((y - (huefte - 0.25)) / 0.15))


def rock_enger(j, b, faktoren, bericht):
    seitlich, tiefe = faktoren
    g, namen = gelenke(j, b)
    huefte = (g["LeftUpperLeg"][1] + g["RightUpperLeg"][1]) / 2
    taille = g["Spine"][1]
    mitte_z = g["Hips"][2]
    erledigt = set()
    for name, m in netze(j):
        if name.startswith(ROCK_NETZE_OHNE):
            continue
        for p in m["primitives"]:
            if p["attributes"]["POSITION"] in erledigt:
                continue
            erledigt.add(p["attributes"]["POSITION"])
            pos, lage = feld(j, b, p["attributes"]["POSITION"], "pos")
            gel, _ = feld(j, b, p["attributes"]["JOINTS_0"], "j")
            gew, _ = feld(j, b, p["attributes"]["WEIGHTS_0"], "w")
            zone, _ = feld(j, b, p["attributes"]["TEXCOORD_0"], "uv")
            figur = name.startswith(("Nah_", "Fern_"))
            neu, geaendert = [], 0
            for v, jj, ww, uv in zip(pos, gel, gew, zone):
                haupt = namen[jj[max(range(4), key=lambda k: ww[k])]]
                s = anteil(v[1], round(uv[0]) if figur else 1, huefte, taille)
                if haupt in ARME or s <= 0.0:
                    neu.append(v)
                    continue
                fx = 1 + (seitlich - 1) * s
                fz = 1 + (tiefe - 1) * s
                neu.append((v[0] * fx, v[1], mitte_z + (v[2] - mitte_z) * fz))
                geaendert += 1
            schreiben(j, b, p["attributes"]["POSITION"], neu, lage)
            if geaendert:
                bericht[name] = {"ecken_geaendert": geaendert, "breite_vorher": round(max(v[0] for v in pos) - min(v[0] for v in pos), 4),
                                 "breite_nachher": round(max(v[0] for v in neu) - min(v[0] for v in neu), 4)}


def glb_schreiben(pfad, j, b):
    import json
    b = bytes(b) + b"\0" * (-len(b) % 4)
    j["buffers"][0]["byteLength"] = len(b)
    text = json.dumps(j, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    text += b" " * (-len(text) % 4)
    with open(pfad, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(text) + 8 + len(b)))
        f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
        f.write(struct.pack("<II", len(b), 0x004E4942) + b)


def hauptteil():
    import json
    ein, aus = sys.argv[1], sys.argv[2]
    os.makedirs(aus, exist_ok=True)
    bericht = {}
    for datei in sorted(set(SCHUHE) | set(ROCK)):
        j, b = lesen(os.path.join(ein, datei))
        b = bytearray(b)
        bericht[datei] = {}
        if datei in SCHUHE:
            schuhe_stauchen(j, b, SCHUHE[datei], bericht[datei])
        if datei in ROCK:
            rock_enger(j, b, ROCK[datei], bericht[datei])
        glb_schreiben(os.path.join(aus, datei), j, b)
    print(json.dumps(bericht, indent=1, ensure_ascii=False))


if __name__ == "__main__":
    hauptteil()
