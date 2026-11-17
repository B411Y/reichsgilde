"""Prueft game/assets/modelle/oberflaeche/schriftrolle.glb: Dreiecke, Knoten- und Clip-Namen, Cliplaenge, Materialien, Masse zu/offen, Symbolflaeche in der Mitte.
  python -I tools/modelle/schriftrolle/schriftrolle_pruefen.py [<schriftrolle.glb>] [<messwerte_schriftrolle.json>]
Die Masse zu/offen kommen aus messwerte_schriftrolle.json (schreibt schriftrolle_bau.py aus der ausgewerteten Pose); ohne die Datei wird nur die .glb geprueft."""
import json, os, struct, sys

HIER = os.path.dirname(os.path.abspath(__file__))
glb = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HIER, "..", "..", "..", "game", "assets", "modelle", "oberflaeche", "schriftrolle.glb")
mj = sys.argv[2] if len(sys.argv) > 2 else None

fehler = []


def pruefe(bed, text):
    print(("ok     " if bed else "FEHLER ") + text)
    if not bed:
        fehler.append(text)


with open(glb, "rb") as fh:
    daten = fh.read()
magie, version, laenge = struct.unpack("<4sII", daten[:12])
pruefe(magie == b"glTF" and version == 2, "glTF 2.0")
jl = struct.unpack("<I", daten[12:16])[0]
j = json.loads(daten[20:20 + jl].decode("utf-8"))

knoten = {n.get("name") for n in j["nodes"]}
for n in ("Schriftrolle_Geruest", "Schriftrolle", "Schriftrolle_Umriss", "Symbolflaeche", "Stab_oben", "Stab_unten") + tuple(f"Zeile_{i:02d}" for i in range(25)):
    pruefe(n in knoten, f"Knoten {n}")

tris = {}
for n in j["nodes"]:
    if "mesh" in n:
        t = 0
        for p in j["meshes"][n["mesh"]]["primitives"]:
            t += j["accessors"][p["indices"]]["count"] // 3
        tris[n.get("name")] = t
gesamt = sum(tris.values())
print("Dreiecke je Netz:", tris, "gesamt", gesamt)
pruefe(gesamt < 1500, f"Dreiecke gesamt {gesamt} < 1500")

clips = {}
for a in j.get("animations", []):
    dauer = max(j["accessors"][s["input"]]["max"][0] for s in a["samplers"])
    clips[a["name"]] = dauer
print("Clips:", clips)
for c in ("aufrollen", "zurollen", "zu"):
    pruefe(c in clips, f"Clip {c}")
for c in ("aufrollen", "zurollen"):
    pruefe(c in clips and 0.6 <= clips[c] <= 0.8, f"Clip {c} dauert {clips.get(c)} s (0,6 bis 0,8)")
pruefe(len(j.get("skins", [])) >= 1 and len(j["skins"][0]["joints"]) == 27, "Haut mit 27 Knochen")
mats = {m.get("name") for m in j["materials"]}
for m in ("Schriftrolle_Holz", "Schriftrolle_Pergament", "Schriftrolle_Symbol", "Umriss"):
    pruefe(m in mats, f"Material {m}")
pruefe(len(j.get("images", [])) == 2, f"zwei eingebettete Texturen ({len(j.get('images', []))})")

if mj and os.path.exists(mj):
    m = json.load(open(mj, encoding="utf-8"))
    zu, offen = m["zu"], m["offen"]
    b_zu = [a - b for a, b in zip(zu["schriftrolle_max"], zu["schriftrolle_min"])]
    b_of = [a - b for a, b in zip(offen["schriftrolle_max"], offen["schriftrolle_min"])]
    print("Masse zu (B x T x H):", [round(x, 3) for x in b_zu], " offen:", [round(x, 3) for x in b_of])
    pruefe(b_zu[2] < 0.15 and b_of[2] > 0.4, "zu niedrig (< 0,15 m), offen hoch (> 0,4 m)")
    mitte = offen["symbol_mitte"]
    pruefe(abs(mitte[0]) < 0.01 and abs(mitte[2]) < 0.01, f"Symbolflaeche offen in der Mitte {mitte}")
    pruefe(abs(offen["symbol_max"][0] - offen["symbol_min"][0] - m["symbolflaeche_m"][0]) < 0.002, "Symbolflaeche offen so breit wie angegeben")

print("FERTIG" if not fehler else f"{len(fehler)} Fehler")
sys.exit(1 if fehler else 0)
