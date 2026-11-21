"""Prueft die 3D-Buecher (Personal, Familie, Kontor, Schwarzes Buch) (game/assets/modelle/oberflaeche/buch_<art>.glb): Dreiecke, Knoten- und Clip-Namen, Cliplaengen, Materialien,
Neigung konstant (+-0,5 Grad), Inhaltsflaechen deckungsgleich, kein Huepfen beim Zuklappen.
  python -I tools/modelle/buecher3d/buch_pruefen.py [<ordner mit den .glb>] [<ordner mit messwerte_buch_<art>.json>]
Ohne zweiten Ordner werden nur die .glb geprueft; die Messwerte schreibt buch_bau.py aus der ausgewerteten Pose (Blender)."""
import json
import os
import struct
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
glbdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HIER, "..", "..", "..", "game", "assets", "modelle", "oberflaeche")
messdir = sys.argv[2] if len(sys.argv) > 2 else None
ANHANG = {"personal": "Riemen", "familie": "Schliesse", "kontor": None, "schwarz": None}
SEG = 6
fehler = []


def pruefe(bed, text):
    print(("ok     " if bed else "FEHLER ") + text)
    if not bed:
        fehler.append(text)


for art in ("personal", "familie", "kontor", "schwarz"):
    print(f"--- Buch {art}")
    with open(os.path.join(glbdir, f"buch_{art}.glb"), "rb") as fh:
        daten = fh.read()
    magie, version, _ = struct.unpack("<4sII", daten[:12])
    pruefe(magie == b"glTF" and version == 2, "glTF 2.0")
    jl = struct.unpack("<I", daten[12:16])[0]
    j = json.loads(daten[20:20 + jl].decode("utf-8"))
    knoten = {n.get("name") for n in j["nodes"]}
    erwartet = ["Buch_Geruest", "Buch", "Buch_Umriss", "Inhaltsflaeche_links", "Inhaltsflaeche_rechts", "Deckel_hinten", "Deckel_vorn"]
    erwartet += [f"Blatt_{i + 1}" for i in range(SEG)]
    if ANHANG[art]:
        erwartet.append(ANHANG[art])
    if art == "familie":
        erwartet.append("Wappenfeld")
    for n in erwartet:
        pruefe(n in knoten, f"Knoten {n}")
    pruefe(("Wappenfeld" in knoten) == (art == "familie"), "Wappenfeld nur beim Familienbuch")
    tris = {}
    for n in j["nodes"]:
        if "mesh" in n:
            tris[n.get("name")] = sum(j["accessors"][p["indices"]]["count"] // 3 for p in j["meshes"][n["mesh"]]["primitives"])
    gesamt = sum(tris.values())
    print("Dreiecke je Netz:", tris, "gesamt", gesamt)
    pruefe(1400 <= gesamt <= 2500, f"Dreiecke gesamt {gesamt} (1.500 bis 2.500, Untergrenze mit Spielraum)")
    clips = {a["name"]: max(j["accessors"][s["input"]]["max"][0] for s in a["samplers"]) for a in j.get("animations", [])}
    print("Clips:", clips)
    for c in ("zu", "offen", "aufklappen", "zuklappen", "umblaettern", "umblaettern_zurueck"):
        pruefe(c in clips, f"Clip {c}")
    for c in ("aufklappen", "zuklappen", "umblaettern", "umblaettern_zurueck"):
        pruefe(c in clips and 0.5 <= clips[c] <= 1.2, f"Clip {c} dauert {clips.get(c)} s (0,5 bis 1,2)")
    pruefe(len(j.get("skins", [])) >= 1 and len(j["skins"][0]["joints"]) == 2 + SEG + (1 if ANHANG[art] else 0),
           f"Haut mit {len(j['skins'][0]['joints']) if j.get('skins') else 0} Knochen")
    mats = {m.get("name"): m for m in j["materials"]}
    for m in ("Buch_Einband", "Buch_Leder", "Buch_Seiten", "Buch_Schnitt", "Buch_Inhalt", "Umriss"):
        pruefe(m in mats, f"Material {m}")
    pruefe(art != "familie" or "Buch_Wappenfeld" in mats, "Material Buch_Wappenfeld beim Familienbuch")
    pruefe(art != "schwarz" or all(f"Buch_Band_{k}" in mats for k in (1, 2, 3, 4)), "vier Lesebaender (Buch_Band_1 bis 4) beim Schwarzen Buch")
    pruefe(mats.get("Buch_Inhalt", {}).get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])[3] == 0, "Buch_Inhalt ist voellig durchsichtig (Alpha 0, die Oberflaeche setzt ein eigenes Material)")
    pruefe(len(j.get("images", [])) >= 4, f"eingebettete Texturen: {len(j.get('images', []))}")
    if messdir:
        mp = os.path.join(messdir, f"messwerte_buch_{art}.json")
        m = json.load(open(mp, encoding="utf-8"))
        pruefe(m["neigung_max_grad"] <= 0.5, f"Neigung in jedem Bild jedes Clips: groesste Abweichung {m['neigung_max_grad']} Grad (Rueckdeckel gegen die Ruhe, alle Knochen: Drehung nur um die Rueckenachse; Grenze 0,5)")
        pruefe(m["hoehe_rueckdeckel_schwankung_m"] < 1e-5, f"Hoehe des Rueckdeckels bleibt gleich (Schwankung {m['hoehe_rueckdeckel_schwankung_m']} m)")
        pruefe(m["zuklappen_monoton"], "Zuklappen: Winkel faellt in jedem Bild (kein Huepfen)")
        pruefe(m["tiefster_punkt_m"] >= -0.006, f"tiefster Punkt {m['tiefster_punkt_m']} m (nur Zierteile der Aussenseite tauchen offen in die Auflage, <= 6 mm)")
        l, r = m["offen"]["Inhaltsflaeche_links"], m["offen"]["Inhaltsflaeche_rechts"]
        pruefe(abs(l["min"][2] - r["min"][2]) < 1e-4 and abs(l["max"][2] - l["min"][2]) < 1e-4 and abs(r["max"][2] - r["min"][2]) < 1e-4,
               f"Inhaltsflaechen offen auf gleicher Hoehe ({l['min'][2]} / {r['min'][2]}) und eben")
        pruefe(abs(l["min"][0] + r["max"][0]) < 1e-4 and abs(l["max"][0] + r["min"][0]) < 1e-4 and abs(l["min"][1] - r["min"][1]) < 1e-4 and abs(l["max"][1] - r["max"][1]) < 1e-4,
               "Inhaltsflaechen offen spiegelgleich zur Falz (gleiche Groesse, gleiche y-Ausdehnung)")
        pruefe(abs((r["max"][0] - r["min"][0]) - m["masse"]["inhaltsflaeche_m"][0]) < 1e-3, "Inhaltsflaeche so breit wie angegeben")
        z = m["zeiten_s"]
        print("Zeiten:", json.dumps(z))
        pruefe(z["aufklappen_inhaltsflaeche_sichtbar_ab"] < z["aufklappen_dauer"], "Inhaltsflaeche beim Aufklappen vor dem Clip-Ende sichtbar")
        pruefe(0.0 <= z["zuklappen_inhaltsflaeche_sichtbar_bis"] < 0.3, "Inhaltsflaeche beim Zuklappen nur ganz am Anfang")

print("FERTIG" if not fehler else f"{len(fehler)} Fehler")
sys.exit(1 if fehler else 0)
