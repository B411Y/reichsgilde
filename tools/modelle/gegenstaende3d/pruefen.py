"""Prueft die drei 3D-Gegenstaende (game/assets/modelle/oberflaeche/<name>.glb) und ihre Messwerte aus bau.py:
Dreiecke, Knoten- und Clip-Namen, Cliplaengen und 30 Bilder je Sekunde, Skelett (Knochenzahl), Materialien (nur Albedo, Umriss mit Cull Back),
Umrissdicke, Inhaltsflaechen-UV 0..1, Neigung konstant (+-0,5 Grad), CustomAabb umfasst alle Bilder, eigene Pruefungen je Gegenstand.
  python -I tools/modelle/gegenstaende3d/pruefen.py [<ordner mit den .glb>] [<ordner mit messwerte_<name>.json>] [--gegenprobe]
--gegenprobe verfaelscht jede Datei im Speicher auf mehrere Arten und verlangt, dass jede Faelschung mindestens einen Fehler ausloest."""
import copy
import json
import os
import struct
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
args = [a for a in sys.argv[1:] if not a.startswith("--")]
GEGENPROBE = "--gegenprobe" in sys.argv
glbdir = args[0] if args else os.path.join(HIER, "..", "..", "..", "game", "assets", "modelle", "oberflaeche")
messdir = args[1] if len(args) > 1 else None
FPS = 30
DREIECKE = (700, 2500)

ERWARTET = {
    "kartentisch": dict(geruest="Kartentisch_Geruest", netze=["Kartentisch", "Kartentisch_Umriss", "Kartenflaeche"], knochen_min=40,
                        knoten=["Tisch", "Karte_00"], clips={"zu": 0, "offen": 0, "entrollen": (1.0, 1.5), "zusammenrollen": (0.8, 1.2)},
                        inhalt=["Kartenflaeche"], mats=["Tisch_Holz", "Karte_Pergament", "Karte_Inhalt", "Umriss"], paare=[("Kartentisch", "Kartentisch_Umriss")]),
    "geldtruhe": dict(geruest="Geldtruhe_Geruest", netze=["Geldtruhe", "Geldtruhe_Umriss"], knochen=["Koerper", "Deckel", "Ueberfall"],
                      knoten=["Fuellstufe_viertel", "Fuellstufe_halb", "Fuellstufe_voll", "Muenzen_viertel", "Muenzen_halb", "Muenzen_voll",
                              "Muenzhaufen_viertel", "Muenzhaufen_halb", "Muenzhaufen_voll"],
                      clips={"zu": 0, "offen": 0, "aufspringen": (0.8, 1.2), "zuklappen": (0.5, 1.0)}, inhalt=[],
                      mats=["Truhe_Holz", "Truhe_Eisen", "Truhe_Muenzen", "Umriss"], paare=[("Geldtruhe", "Geldtruhe_Umriss"), ("Muenzen_halb", "Muenzen_halb_Umriss")]),
    "siegelstempel": dict(geruest="Siegelstempel_Geruest", netze=["Siegelstempel", "Siegelstempel_Umriss", "Abdruck_Wappen"],
                          knochen=["Blatt", "Wachs_Mitte", "Wachs_Rand", "Abdruck", "Stempel"], knoten=[],
                          clips={"bereit": 0, "gesiegelt": 0, "siegeln": (1.0, 1.6)}, inhalt=["Abdruck_Wappen"],
                          mats=["Siegel_Wachs", "Siegel_Abdruck", "Siegel_Messing", "Siegel_Holz", "Siegel_Pergament", "Umriss"],
                          paare=[("Siegelstempel", "Siegelstempel_Umriss")]),
}
KOMPONENTEN = {5126: ("f", 4), 5123: ("H", 2), 5125: ("I", 4), 5121: ("B", 1)}
ANZ = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def lade(pfad):
    with open(pfad, "rb") as fh:
        d = fh.read()
    magie, version, _ = struct.unpack("<4sII", d[:12])
    jl = struct.unpack("<I", d[12:16])[0]
    j = json.loads(d[20:20 + jl].decode("utf-8"))
    bl = struct.unpack("<I", d[20 + jl:24 + jl])[0]
    binb = bytearray(d[28 + jl:28 + jl + bl])
    return (magie, version), j, binb


def lesen(j, binb, idx):
    a = j["accessors"][idx]
    bv = j["bufferViews"][a["bufferView"]]
    fmt, gr = KOMPONENTEN[a["componentType"]]
    n = ANZ[a["type"]]
    schritt = bv.get("byteStride", gr * n)
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * n, binb, start + i * schritt) for i in range(a["count"])]


def pruefe_alles(name, kopf, j, binb, m):
    E = ERWARTET[name]
    erg = []

    def p(bed, text):
        erg.append((bool(bed), text))

    p(kopf == (b"glTF", 2), "glTF 2.0")
    knoten = {n.get("name"): n for n in j["nodes"]}
    for n in [E["geruest"]] + E["netze"] + E.get("knochen", []) + E["knoten"]:
        p(n in knoten, f"Knoten {n}")
    tris = {}
    for n in j["nodes"]:
        if "mesh" in n:
            tris[n.get("name")] = sum(j["accessors"][q["indices"]]["count"] // 3 for q in j["meshes"][n["mesh"]]["primitives"])
    gesamt = sum(tris.values())
    p(DREIECKE[0] <= gesamt <= DREIECKE[1], f"Dreiecke gesamt {gesamt} ({DREIECKE[0]} bis {DREIECKE[1]}, Rahmen der Buecher und Briefe)")
    clips = {}
    for a in j.get("animations", []):
        zeiten = [lesen(j, binb, s["input"]) for s in a["samplers"]]
        clips[a["name"]] = (max(z[-1][0] for z in zeiten), zeiten)
    for c, soll in E["clips"].items():
        p(c in clips, f"Clip {c}")
        if c not in clips:
            continue
        dauer, zeiten = clips[c]
        if soll == 0:
            p(dauer <= 0.05, f"Clip {c} ist eine Rast-Pose ({dauer:.3f} s)")
        else:
            p(soll[0] <= dauer <= soll[1], f"Clip {c} dauert {dauer:.3f} s ({soll[0]} bis {soll[1]})")
        # jede Schluesselzeit liegt auf dem 30er-Raster; mindestens eine Spur hat jedes Bild (gleichbleibende Spuren kuerzt der Export auf Anfang und Ende)
        raster = all(abs(t[0] * FPS - round(t[0] * FPS)) < 2e-3 for zz in zeiten for t in zz)
        voll = max(len(zz) for zz in zeiten) == round(dauer * FPS) + 1
        p(raster and voll, f"Clip {c} mit 30 Bildern je Sekunde ({max(len(zz) for zz in zeiten)} Schluessel)")
    skins = j.get("skins", [])
    soll_kn = len(E["knochen"]) if "knochen" in E else None
    if skins:
        nj = len(skins[0]["joints"])
        p(nj == soll_kn if soll_kn else nj >= E["knochen_min"], f"Skelett mit {nj} Knochen")
    else:
        p(False, "Skelett vorhanden")
    mats = {mm.get("name"): mm for mm in j.get("materials", [])}
    for mm in E["mats"]:
        p(mm in mats, f"Material {mm}")
    p("Umriss" in mats and mats["Umriss"].get("doubleSided", False) is False, "Umriss nur Vorderseiten (Cull Back, doubleSided false)")
    p(all("normalTexture" not in mm and "occlusionTexture" not in mm and "emissiveTexture" not in mm
          and "metallicRoughnessTexture" not in mm.get("pbrMetallicRoughness", {}) for mm in mats.values()), "nur Albedo (keine Normal-, Rauheits- oder Leuchttexturen)")
    p(len(j.get("images", [])) >= 1 and all(im.get("mimeType") == "image/png" for im in j["images"]), f"eingebettete PNG-Texturen: {len(j.get('images', []))}")
    # Umrissdicke: Huelle der Umrissnetze gegen die Huelle der Netze (Bindepose)
    um = m["umriss_m"]
    for a, b in E["paare"]:
        if a not in knoten or b not in knoten:
            p(False, f"Umrisspaar {a}/{b}")
            continue
        lo = [1e9] * 3
        hi = [-1e9] * 3
        lo2, hi2 = list(lo), list(hi)
        for q in j["meshes"][knoten[a]["mesh"]]["primitives"]:
            acc = j["accessors"][q["attributes"]["POSITION"]]
            lo = [min(x, y) for x, y in zip(lo, acc["min"])]
            hi = [max(x, y) for x, y in zip(hi, acc["max"])]
        for q in j["meshes"][knoten[b]["mesh"]]["primitives"]:
            acc = j["accessors"][q["attributes"]["POSITION"]]
            lo2 = [min(x, y) for x, y in zip(lo2, acc["min"])]
            hi2 = [max(x, y) for x, y in zip(hi2, acc["max"])]
        # die Unterseite (Standflaeche, glTF -Y) braucht keinen Umriss und zaehlt nicht
        d = [lo[0] - lo2[0], lo[2] - lo2[2]] + [hi2[i] - hi[i] for i in range(3)]
        u0, u1 = um[a]
        p(all(0.3 * u0 <= x <= 1.1 * u1 for x in d) and max(d) >= 0.5 * u1, f"Umriss {b}: Abstand je Seite {min(d) * 1000:.1f} bis {max(d) * 1000:.1f} mm (Soll {u0 * 1000:.1f} bis {u1 * 1000:.1f} mm)")
    # Inhaltsflaechen: UV genau 0..1
    for n in E["inhalt"]:
        if n not in knoten:
            continue
        uv = [t for q in j["meshes"][knoten[n]["mesh"]]["primitives"] for t in lesen(j, binb, q["attributes"]["TEXCOORD_0"])]
        us = [t[0] for t in uv]
        vs = [t[1] for t in uv]
        p(abs(min(us)) < 1e-4 and abs(max(us) - 1) < 1e-4 and abs(min(vs)) < 1e-4 and abs(max(vs) - 1) < 1e-4,
          f"Inhaltsflaeche {n}: UV u {min(us):.3f}..{max(us):.3f}, v {min(vs):.3f}..{max(vs):.3f} (0..1)")
    # Messwerte
    p(m["neigung_max_grad"] <= 0.5, f"Neigung in jedem Bild jedes Clips: groesste Abweichung {m['neigung_max_grad']} Grad (jeder Knochen dreht nur um seine erlaubte Achse; Grenze 0,5)")
    p(m["abgleich_blender_formel_m"] < 1e-4, f"Geruest folgt der Formel (Abweichung {m['abgleich_blender_formel_m']} m)")
    ab = m["custom_aabb_godot"]
    h = m["huelle_alle_bilder_blender"]
    g_lo = [h["min"][0], h["min"][2], -h["max"][1]]
    g_hi = [h["max"][0], h["max"][2], -h["min"][1]]
    p(all(ab["position"][i] <= g_lo[i] and ab["position"][i] + ab["groesse"][i] >= g_hi[i] for i in range(3)),
      f"CustomAabb {ab} umfasst alle Bilder aller Clips")
    p(m["dreiecke"]["gesamt"] == gesamt, f"Messwerte passen zur Datei (Dreiecke {m['dreiecke']['gesamt']} / {gesamt})")
    z = m.get("zeiten_s", {})
    if name == "kartentisch":
        p(m["karte_tiefster_punkt_ueber_platte_m"] >= -1e-4, f"Karte nie unter der Platte (tiefster Punkt {m['karte_tiefster_punkt_ueber_platte_m']} m ueber der Platte)")
        o = m["offen_kartenflaeche"]
        p(o["ebenheit_m"] < 1e-4 and 0.0012 < o["hoehe_ueber_platte_m"] < 0.004, f"Kartenflaeche offen eben ({o['ebenheit_m']} m) und knapp ueber dem Pergament ({o['hoehe_ueber_platte_m']} m)")
        p(m["offen_karte_dicke_m"] < 0.002, f"Karte offen flach (Dicke {m['offen_karte_dicke_m']} m)")
        p(mats.get("Karte_Inhalt", {}).get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])[3] == 0,
          "Karte_Inhalt voellig durchsichtig (Alpha 0, die Oberflaeche setzt die Kartentextur)")
        p(m["rolle_luft_zwischen_lagen_m"] > 0.001, f"Lagen der Rolle mit Luft ({m['rolle_luft_zwischen_lagen_m']} m)")
    if name == "geldtruhe":
        p(m["zuklappen_monoton"], "Zuklappen: Winkel faellt in jedem Bild (kein Huepfen)")
        p(m["ueberfall_nie_in_den_deckel"], "Ueberfall dreht nie in den Deckel")
        p(all(v < 0.30 for v in m["muenzen_hoechster_punkt_m"].values()), f"Muenzen bleiben unter dem Deckel {m['muenzen_hoechster_punkt_m']}")
        for st in ("viertel", "halb", "voll"):
            kinder = [j["nodes"][c].get("name") for c in knoten.get("Fuellstufe_" + st, {}).get("children", [])]
            p("Muenzen_" + st in kinder and "Muenzhaufen_" + st in kinder, f"Fuellstufe_{st} haelt ihre Netze {kinder}")
    if name == "siegelstempel":
        p(m["stempel_drueckt_nicht_durch"] and m["stempel_tiefster_hub_m"] >= -1e-6, "Stempel drueckt nie tiefer als das gepresste Wachs und nie unter den Tisch")
        p(m["abdruck_bereit_im_wachs"], "Abdruck liegt im Zustand bereit verdeckt im Wachs")
        p(m["wachs_gepresst"]["hoehe_m"] < m["wachs_frisch"]["hoehe_m"] * 0.6 and m["wachs_gepresst"]["halbmesser_m"] > m["wachs_frisch"]["halbmesser_m"] * 1.3,
          f"Wachs gepresst flacher und breiter ({m['wachs_frisch']} -> {m['wachs_gepresst']})")
        p(z["siegeln_stempel_beruehrt_wachs"] < z["siegeln_wachs_ganz_gepresst"] < z["siegeln_abdruck_sichtbar_ab"] < z["siegeln_dauer"], f"Zeiten in der richtigen Folge {z}")
    return erg


def faelschungen(name, kopf, j, binb, m):
    """(Beschreibung, kopf, j, binb, m) – jede soll mindestens einen Fehler ausloesen."""
    E = ERWARTET[name]
    aus = []
    j1 = copy.deepcopy(j)
    j1["animations"] = [a for a in j1["animations"] if a["name"] != [c for c, s in E["clips"].items() if s][0]]
    aus.append(("Clip fehlt", kopf, j1, binb, m))
    j2 = copy.deepcopy(j)
    b2 = bytearray(binb)
    a = j2["animations"][0]["samplers"][0]["input"]
    acc = j2["accessors"][a]
    bv = j2["bufferViews"][acc["bufferView"]]
    st = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    for i in range(acc["count"]):
        v = struct.unpack_from("<f", b2, st + 4 * i)[0]
        struct.pack_into("<f", b2, st + 4 * i, v * 2.0)
    aus.append(("Clip mit 15 Bildern je Sekunde", kopf, j2, b2, m))
    j3 = copy.deepcopy(j)
    j3["materials"] = [dict(mm, doubleSided=True) if mm.get("name") == "Umriss" else mm for mm in j3["materials"]]
    aus.append(("Umriss doppelseitig", kopf, j3, binb, m))
    j4 = copy.deepcopy(j)
    j4["materials"][0]["normalTexture"] = {"index": 0}
    aus.append(("Normaltextur", kopf, j4, binb, m))
    m5 = copy.deepcopy(m)
    m5["neigung_max_grad"] = 2.0
    aus.append(("Neigung 2 Grad", kopf, j, binb, m5))
    j6 = copy.deepcopy(j)
    for mesh in j6["meshes"]:
        for q in mesh["primitives"]:
            j6["accessors"][q["indices"]] = dict(j6["accessors"][q["indices"]], count=j6["accessors"][q["indices"]]["count"] * 3)
    aus.append(("dreimal so viele Dreiecke", kopf, j6, binb, m))
    m7 = copy.deepcopy(m)
    m7["custom_aabb_godot"] = {"position": [x + 0.05 for x in m["custom_aabb_godot"]["position"]], "groesse": m["custom_aabb_godot"]["groesse"]}
    aus.append(("CustomAabb zu klein", kopf, j, binb, m7))
    m8 = copy.deepcopy(m)
    m8["umriss_m"] = {k: [v[0] * 4, v[1] * 4] for k, v in m["umriss_m"].items()}
    aus.append(("Umriss viermal so dick angegeben", kopf, j, binb, m8))
    if E["inhalt"]:
        j9 = copy.deepcopy(j)
        b9 = bytearray(binb)
        kn = {n.get("name"): n for n in j9["nodes"]}
        q = j9["meshes"][kn[E["inhalt"][0]]["mesh"]]["primitives"][0]
        acc = j9["accessors"][q["attributes"]["TEXCOORD_0"]]
        bv = j9["bufferViews"][acc["bufferView"]]
        st = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
        for i in range(acc["count"] * 2):
            v = struct.unpack_from("<f", b9, st + 4 * i)[0]
            struct.pack_into("<f", b9, st + 4 * i, v * 0.5)
        aus.append(("Inhaltsflaeche UV 0..0,5", kopf, j9, b9, m))
    if "knochen" in E:
        j10 = copy.deepcopy(j)
        j10["skins"][0]["joints"] = j10["skins"][0]["joints"][:-1]
        aus.append(("ein Knochen fehlt", kopf, j10, binb, m))
    return aus


fehler = []
for name in ERWARTET:
    print(f"--- {name}")
    kopf, j, binb = lade(os.path.join(glbdir, name + ".glb"))
    m = json.load(open(os.path.join(messdir or glbdir, f"messwerte_{name}.json"), encoding="utf-8"))
    for ok, text in pruefe_alles(name, kopf, j, binb, m):
        print(("ok     " if ok else "FEHLER ") + text)
        if not ok:
            fehler.append(f"{name}: {text}")
    if GEGENPROBE:
        for text, k2, j2, b2, m2 in faelschungen(name, kopf, j, binb, m):
            n = sum(1 for ok, _ in pruefe_alles(name, k2, j2, b2, m2) if not ok)
            print(("ok     " if n else "FEHLER ") + f"Gegenprobe '{text}': {n} Fehler erkannt")
            if not n:
                fehler.append(f"{name}: Gegenprobe {text} nicht erkannt")

print("FERTIG" if not fehler else f"{len(fehler)} Fehler")
sys.exit(1 if fehler else 0)
