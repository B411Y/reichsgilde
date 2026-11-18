"""Prueft game/assets/modelle/oberflaeche/brief_klein.glb und brief_gross.glb: Dreiecke, Knoten-, Knochen-, Clip- und Materialnamen, Cliplaengen, keine Clipspur auf den
Streckknochen, Neigung konstant, Inhaltsflaeche mittig und deckungsgleich, Streck-Probe (Hoehe min/max, Falze unverformt, Naht ohne Spalt).
  python -I tools/modelle/brief3d/brief_pruefen.py [<ordner mit den .glb>] [<messwerte_brief.json>]
Die Messwerte schreibt brief_bau.py aus der ausgewerteten Pose; ohne die Datei werden nur die .glb geprueft."""
import json, os, struct, sys

HIER = os.path.dirname(os.path.abspath(__file__))
ordner = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HIER, "..", "..", "..", "game", "assets", "modelle", "oberflaeche")
mj = sys.argv[2] if len(sys.argv) > 2 else None
fehler = []


def pruefe(bed, text):
    print(("ok     " if bed else "FEHLER ") + text)
    if not bed:
        fehler.append(text)


BIN = {}


def lade(name):
    with open(os.path.join(ordner, name), "rb") as fh:
        daten = fh.read()
    magie, version, _ = struct.unpack("<4sII", daten[:12])
    pruefe(magie == b"glTF" and version == 2, f"{name}: glTF 2.0")
    jl = struct.unpack("<I", daten[12:16])[0]
    BIN[name] = daten[20 + jl + 8:]
    return json.loads(daten[20:20 + jl].decode("utf-8"))


def werte(j, name, acc):
    """Float-Werte eines Accessors (nur VEC3/VEC4/SCALAR, nicht verschachtelt) als Liste von Zeilen."""
    a = j["accessors"][acc]
    bv = j["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[a["type"]]
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    flat = struct.unpack("<" + "f" * (a["count"] * n), BIN[name][off:off + 4 * a["count"] * n])
    return [flat[i * n:(i + 1) * n] for i in range(a["count"])]


def dreiecke(j):
    t = {}
    for n in j["nodes"]:
        if "mesh" in n:
            t[n.get("name")] = sum(j["accessors"][p["indices"]]["count"] // 3 for p in j["meshes"][n["mesh"]]["primitives"])
    return t


# ---------------------------------------------------------------- kleiner Faltbrief
jk = lade("brief_klein.glb")
kn = {n.get("name") for n in jk["nodes"]}
for n in ("Brief_klein", "Siegel", "Siegel_Symbol", "Brief_klein_Umriss"):
    pruefe(n in kn, f"klein: Knoten {n}")
tk = dreiecke(jk)
gk = sum(tk.values())
print("klein: Dreiecke je Netz", tk, "gesamt", gk)
pruefe(700 <= gk <= 1300, f"klein: Dreiecke gesamt {gk} (Ziel ca. 1000)")
mk = {m.get("name") for m in jk["materials"]}
for m in ("Brief_Papier", "Brief_Siegel", "Brief_Siegel_Symbol", "Umriss"):
    pruefe(m in mk, f"klein: Material {m}")
pruefe(len(jk.get("images", [])) == 1, f"klein: eine eingebettete Textur ({len(jk.get('images', []))})")
pruefe(not jk.get("animations"), "klein: keine Clips (statisches Symbol)")

# ---------------------------------------------------------------- grosser Brief
jg = lade("brief_gross.glb")
gn = {n.get("name") for n in jg["nodes"]}
KNOCHEN = ("Streck", "Anker_oben", "Anker_unten", "Falz_oben", "Falz_unten", "Siegel_oben", "Siegel_unten", "Symbolknochen")
for n in ("Brief_Geruest", "Brief_gross", "Siegel", "Siegel_Symbol", "Brief_gross_Umriss", "Inhaltsflaeche") + KNOCHEN:
    pruefe(n in gn, f"gross: Knoten {n}")
tg = dreiecke(jg)
gg = sum(tg.values())
print("gross: Dreiecke je Netz", tg, "gesamt", gg)
pruefe(gg < 1500, f"gross: Dreiecke gesamt {gg} < 1500")
mg = {m.get("name") for m in jg["materials"]}
for m in ("Brief_Papier", "Brief_Siegel", "Brief_Siegel_Symbol", "Brief_Inhalt", "Umriss"):
    pruefe(m in mg, f"gross: Material {m}")
pruefe(len(jg.get("images", [])) == 1, f"gross: eine eingebettete Textur ({len(jg.get('images', []))})")
img = jg["images"][0] if jg.get("images") else {}
clips = {}
ziele = {}
for a in jg.get("animations", []):
    clips[a["name"]] = max(jg["accessors"][s["input"]]["max"][0] for s in a["samplers"])
    ziele[a["name"]] = set()
    for c in a["channels"]:
        nm = jg["nodes"][c["target"]["node"]].get("name")
        if nm in ("Streck", "Anker_oben", "Anker_unten"):
            z = werte(jg, "brief_gross.glb", a["samplers"][c["sampler"]]["output"])
            if any(max(sp) - min(sp) > 1e-6 for sp in zip(*z)):
                ziele[a["name"]].add(nm)
print("gross: Clips", clips)
for c in ("auffalten", "zufalten", "zu"):
    pruefe(c in clips, f"gross: Clip {c}")
for c in ("auffalten", "zufalten"):
    pruefe(c in clips and 0.8 <= clips[c] <= 1.0, f"gross: Clip {c} dauert {clips.get(c)} s (0,8 bis 1,0)")
frei = {"Streck", "Anker_oben", "Anker_unten"}
pruefe(all(not (ziele.get(c, set()) & frei) for c in ziele), "gross: die Spuren von Streck, Anker_oben, Anker_unten sind in jedem Clip unveraendert (Godot wirft sie beim Import weg; die setzt der Code)")
pruefe(len(jg.get("skins", [])) >= 1 and len(jg["skins"][0]["joints"]) == 8, "gross: Haut mit 8 Knochen")

if mj and os.path.exists(mj):
    m = json.load(open(mj, encoding="utf-8"))
    k, g = m["klein"], m["gross"]
    bk = [a - b for a, b in zip(k["papier_max"], k["papier_min"])]
    print("klein: Masse B x T x H", [round(x, 3) for x in bk])
    pruefe(0.08 < bk[0] < 0.16 and bk[2] < 0.1, "klein: klein und leicht (Breite 0,08 bis 0,16 m, Hoehe unter 0,1 m)")
    pruefe(abs(k["siegel_mitte"][0]) < 1e-4, "klein: Siegel mittig")
    pruefe(k["umriss_min"][0] < k["papier_min"][0] and k["umriss_max"][0] > k["papier_max"][0], "klein: Umriss-Huelle liegt aussen")
    zu, offen = g["zu"], g["offen"]
    b_zu = [a - b for a, b in zip(zu["papier_max"], zu["papier_min"])]
    b_of = [a - b for a, b in zip(offen["papier_max"], offen["papier_min"])]
    print("gross: Masse zu (B x T x H)", [round(x, 3) for x in b_zu], " offen", [round(x, 3) for x in b_of])
    pruefe(b_zu[2] < 0.2 and b_of[2] > 0.4, "gross: zu niedrig (< 0,2 m), offen hoch (> 0,4 m)")
    pruefe(b_of[1] < 0.004 and abs((offen["papier_max"][1] + offen["papier_min"][1]) / 2) < 1e-3, "gross: offen flach in der Ebene y = 0 (Tiefe < 4 mm)")
    pruefe(g["neigung_mitte_max_grad"] <= 0.5, f"gross: Neigung der Mitte in jedem Bild jedes Clips gleich: groesste Abweichung {g['neigung_mitte_max_grad']} Grad (<= 0,5)")
    pruefe(g["falz_achse_max_grad"] <= 0.5, f"gross: Falzachsen bleiben X-parallel (groesste Abweichung {g['falz_achse_max_grad']} Grad)")
    ib, ih = g["inhaltsflaeche_m"]
    mi = g["inhaltsflaeche_mitte"]
    pruefe(abs(mi[0]) < 1e-3 and abs(mi[2]) < 1e-3, f"gross: Inhaltsflaeche mittig {mi}")
    pruefe(abs(g["inhaltsflaeche_y"] - (offen["papier_min"][1])) > 0.0005 and abs(g["inhaltsflaeche_y"] - offen["papier_min"][1]) < 0.002,
           f"gross: Inhaltsflaeche {abs(g['inhaltsflaeche_y'] - offen['papier_min'][1]) * 1000:.2f} mm vor dem Papier (kein Z-Fighting, kein Spalt)")
    pruefe(ib < b_of[0] and ih < b_of[2], f"gross: Inhaltsflaeche {ib} x {ih} m liegt innerhalb des Blattes")
    for s in ("1", "2"):
        sk = g["streck_" + s]
        pruefe(abs(sk["z_mitte_oben"] - sk["z_falz_oben_unten"]) < 1e-4 and abs(sk["z_mitte_unten"] - sk["z_falz_unten_oben"]) < 1e-4, f"gross: Streckung {s}: Naht zwischen Falz und Mitte ohne Spalt")
    pruefe(abs(g["streck_1"]["hoehe"] - g["streck"]["hoehe_min"]) < 0.01 and abs(g["streck_2"]["hoehe"] - g["streck"]["hoehe_max"]) < 0.01,
           f"gross: Hoehe {g['streck_1']['hoehe']} m (Streckung 1) bis {g['streck_2']['hoehe']} m (Streckung 2)")
    pruefe(max(g["streck_falz_verzerrung_m"].values()) < 1e-6, f"gross: Falze beim Strecken unverformt {g['streck_falz_verzerrung_m']}")
    pruefe(abs(g["streck_mitte_faktor"] - g["streck"]["s_max"]) < 1e-3 and g["streck_mitte_xy_aenderung_m"] < 1e-6, "gross: Mitte streckt sich nur in der Hoehe")
    pruefe(max(g["ueberschwinger_grad"].values()) <= 6.0 and g["ueberschwinger_grad"]["zufalten"] == 0.0, f"gross: Nachfedern gedaempft {g['ueberschwinger_grad']} Grad, beim Zufalten keins")
    print("gross: Zeiten", g["zeiten_s"])
    z = g["zeiten_s"]
    pruefe(z["auffalten_inhaltsflaeche_sichtbar_ab"] < 0.9 and z["zufalten_inhaltsflaeche_sichtbar_bis"] >= 0.0, "gross: Inhaltsflaeche ab/bis liegt im Clip")

print("FERTIG" if not fehler else f"{len(fehler)} Fehler")
sys.exit(1 if fehler else 0)
