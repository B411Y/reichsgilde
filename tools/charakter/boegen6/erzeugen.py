#!/usr/bin/env python3
"""Charakter Phase 6: Prüfplan als Bögen (Abschnitt 5 des Plans) – schreibt p01_*.json … p16_*.json in diesen Ordner.

Eine Quelle für Kameras (5.1), Lichter (5.2) und Personen (5.3); die Bögen werden nie von Hand geändert, sondern hier.
Gerendert wird mit tools/charakter/schau.sh (Stand 5 bzw. 6 über --stand=, Zellen mit eigenem „stand“ behalten ihn),
ausgewertet mit tools/charakter/messen/pruefen6.py.

    python3 tools/charakter/boegen6/erzeugen.py

Schlüssel, die erst spätere Schritte auswerten (die Testszene meldet sie bis dahin nicht als Fehler, sie wirken nur
noch nicht): "wetter" (S9), "stufe" (S14), "allelregel" (S7), "zuege" (S6), "leben" (S13).
"""
import json
import os

HIER = os.path.dirname(os.path.abspath(__file__))
MESSEN = os.path.join(HIER, "..", "messen")

# ------------------------------------------------------------------ 5.1 Kameras
KAMERA = {
    "K1": {"ansicht": "kopf", "winkel": 0, "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02},
    "K2": {"ansicht": "kopf", "winkel": 35, "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02},
    "K3": {"ansicht": "kopf", "winkel": 88, "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02},
    "K4": {"ansicht": "kopf", "winkel": 25, "hoehe": 3, "spanne_m": 0.34, "ziel_m": -0.03},
    "K5": {"ansicht": "kopf", "winkel": 35, "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02, "stufe": "niedrig"},
    # N1: Lichtseite (L-V: Schlüssellicht von links im Bild, also rechte Wange der Figur) – Kamera links (−30°)
    "N1": {"ansicht": "kopf", "winkel": -30, "hoehe": 0, "spanne_m": 0.07, "ziel_m": -0.035, "ziel_x": -0.035},
    "N2": {"ansicht": "augen", "winkel": 12, "hoehe": 0, "spanne_m": 0.075, "ziel_m": 0.0},
    "N3": {"ansicht": "kopf", "winkel": 15, "hoehe": 0, "spanne_m": 0.12, "ziel_m": 0.0},
    "N4": {"ansicht": "mund", "winkel": 15, "hoehe": 0, "spanne_m": 0.09, "ziel_m": -0.07},
    # N5: wie N1, Schlüssellicht 80° zur Blickachse (Streiflicht über die Wange)
    "N5": {"ansicht": "kopf", "winkel": -30, "hoehe": 0, "spanne_m": 0.07, "ziel_m": -0.035, "ziel_x": -0.035,
           "schluessel_richtung": [-110, 15]},
    # N6: wie K2, Schlüssellicht aus, Gegenlicht 1,6 (Porträtlicht)
    "N6": {"ansicht": "kopf", "winkel": 35, "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02, "licht": "spiel",
           "schluessel": {"visible": False}},
    # Kinder (nur P7): automatische Spanne
    "K2_kind": {"ansicht": "kopf", "winkel": 35, "hoehe": 0},
}
ZELLE = {"K1": [600, 720], "K2": [600, 720], "K3": [600, 720], "K4": [760, 860], "K5": [128, 160], "N1": [720, 720],
         "N2": [720, 504], "N3": [720, 720], "N4": [720, 720], "N5": [720, 720], "N6": [600, 720]}

# ------------------------------------------------------------------ 5.3 Personen
HAUT = {"H1": {"haut_helligkeit": 235}, "H2": {"haut_helligkeit": 185}, "H3": {"haut_helligkeit": 135}}
for h in HAUT.values():
    h.update({"haut_ton": 105, "haar_helligkeit": 60, "haar_rot": 40})
HAAR_M = {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "mann_kurz", "bart": ""}}
HAAR_W = {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "frau_knoten", "bart": ""}}
WARTEN_TFX = 45


def mann(**extra):
    d = {"geschlecht": "m", "haar": HAAR_M, "gene": {"bartwuchs": 130}, "warten": WARTEN_TFX}
    d.update(extra)
    return d


def frau(**extra):
    d = {"geschlecht": "w", "haar": HAAR_W, "warten": WARTEN_TFX}
    d.update(extra)
    return d


def zelle(kamera, **extra):
    d = dict(KAMERA[kamera])
    d.update(extra)
    return d


def schreiben(name, bogen):
    bogen.setdefault("name", name)
    with open(os.path.join(HIER, name + ".json"), "w", encoding="utf-8") as f:
        json.dump(bogen, f, indent=1, ensure_ascii=False)
        f.write("\n")
    return name


erzeugt = []

# ------------------------------------------------------------------ P1 alter_ton
for kam in ("K1", "K2"):
    for licht, lname in (("vorlage", "lv"), ("spiel", "ls")):
        zeilen = []
        for g, gname in (("m", "Mann"), ("w", "Frau")):
            for h, hname in (("H1", "hell"), ("H2", "mittel"), ("H3", "dunkel")):
                z = mann() if g == "m" else frau()
                z = dict(z, titel=f"{gname} {hname} ({h})")
                z["gene"] = dict(z.get("gene", {}), **HAUT[h])
                zeilen.append(z)
        erzeugt.append(schreiben(f"p01_alter_ton_{kam.lower()}_{lname}", {
            "titel": f"P1 Alter und Hautton – {kam}, L-{'V' if licht == 'vorlage' else 'S'} (Mittelgesicht)",
            "pruefplan": "P1", "ab": "A", "zelle": ZELLE[kam],
            "alle": dict(zelle(kam), kopf="standard", licht=licht),
            "spalten": [{"titel": f"{a} Jahre", "alter": a} for a in (20, 35, 50, 70)],
            "zeilen": zeilen}))

# ------------------------------------------------------------------ P2 haut_nah (ab C)
zeilen = []
for titel, z in (("Mann 35", mann(alter=35)), ("Mann 70", mann(alter=70)), ("Frau 35", frau(alter=35)), ("Frau 70", frau(alter=70))):
    z = dict(z, titel=titel)
    z["gene"] = dict(z.get("gene", {}), **HAUT["H2"])
    zeilen.append(z)
for w in (0, 1):
    z = mann(alter=50, wetter=w, titel=f"Mann 50, Wetter {w}")
    z["gene"] = dict(z["gene"], **HAUT["H2"])
    zeilen.append(z)
erzeugt.append(schreiben("p02_haut_nah", {
    "titel": "P2 Haut nah (N1–N6, L-V); Wetter wirkt ab S9", "pruefplan": "P2", "ab": "C", "zelle": [720, 720],
    "alle": {"kopf": "standard", "licht": "vorlage"},
    "spalten": [dict(zelle(k), titel=k) for k in ("N1", "N2", "N3", "N4", "N5", "N6")],
    "zeilen": zeilen}))

# ------------------------------------------------------------------ P3 vorlagen
vm_pfad = os.path.join(MESSEN, "vorlagen_merkpunkte.json")
vorlagen = json.load(open(vm_pfad, encoding="utf-8"))["vorlagen"] if os.path.exists(vm_pfad) else {}
zeilen = []
for vid, v in vorlagen.items():
    if not v.get("im_p3", True):
        continue
    g = v["geschlecht"]
    haar = {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": v.get("frisur", "mann_kurz" if g == "m" else "frau_knoten"),
                                                          "bart": v.get("bart", "")}}
    gene = dict(v.get("gene", {}))
    gene.setdefault("haar_helligkeit", 65)
    gene.setdefault("haar_rot", 40)
    if g == "m":
        gene.setdefault("bartwuchs", 130)
    basis = {"titel": v.get("titel", vid), "geschlecht": g, "alter": v["alter"], "haar": haar, "gene": gene,
             "warten": WARTEN_TFX}
    for k in ("wetter", "gewicht_vorlage"):
        if k in v:
            basis[k] = v[k]
    ordner = v["ordner"]
    zellen = []
    vorn = v.get("vorn", {}).get("punkte", {})
    drei = v.get("dreiviertel", {}).get("punkte", {})
    def vorlage(art, p):
        if "ps_r" not in p:
            return {"kopf": None, "titel": "(keine Merkpunkte)"}
        return {"vorlage": {"bild": os.path.join(ordner, v[art]["bild"]), "pupillen": [p["ps_r"], p["ps_l"]]}}
    # Drehung der ¾-Vorlage: aus dem verkürzten Pupillenabstand (cos θ = IPD ¾ / IPD vorn), sonst wie eingetragen; die
    # Vorlagen sind meist nach rechts gedreht (K2 gespiegelt, negativer Winkel)
    w34 = v.get("dreiviertel", {}).get("winkel", -35)
    if "ps_r" in drei and "ps_r" in vorn:
        import math
        c = min(1.0, math.dist(drei["ps_r"], drei["ps_l"]) / math.dist(vorn["ps_r"], vorn["ps_l"]))
        w34 = -5 * round(math.degrees(math.acos(c)) / 5)
    zellen = [dict(zelle("K1"), **vorlage("vorn", vorn)), dict(zelle("K1"), stand=5), dict(zelle("K1"), stand=6),
              dict(zelle("K2"), winkel=w34, **vorlage("dreiviertel", drei)), dict(zelle("K2"), winkel=w34, stand=6)]
    basis["zellen"] = zellen
    zeilen.append((v.get("satz", ""), basis))
# P3 in Bögen zu höchstens 6 Zeilen (passt in ein Fenster 3200 × 4560): der alte Satz (Haar-Vorlagen der Phase 5) als
# p03_vorlagen, der neue Satz aus S1 in Gruppen zu fünf als p03_vorlagen_s1a, _s1b, …
P3_KOPF = {
    "titel": "P3 Vorlagen: Vorlage vorn | Stand 5 | Stand 6 | Vorlage ¾ | Stand 6 ¾ (L-V, auf den IPD der Vorlage gelegt; ¾ wie die Vorlagen nach rechts gedreht = K2 gespiegelt)",
    "pruefplan": "P3", "ab": "A", "zelle": ZELLE["K1"], "alle": {"kopf": "standard", "licht": "vorlage"},
    "spalten": [{"titel": "Vorlage vorn"}, {"titel": "Stand 5"}, {"titel": "Stand 6"}, {"titel": "Vorlage ¾"}, {"titel": "Stand 6 ¾"}]}
alt = [b for s, b in zeilen if s != "phase6-s1"]
neu = [b for s, b in zeilen if s == "phase6-s1"]
erzeugt.append(schreiben("p03_vorlagen", dict(P3_KOPF, zeilen=alt)))
for i in range(0, len(neu), 5):
    erzeugt.append(schreiben(f"p03_vorlagen_s1{chr(ord('a') + i // 5)}", dict(P3_KOPF, zeilen=neu[i:i + 5])))

# ------------------------------------------------------------------ P4 vielfalt16 (ab B, A als Vorher)
for g, saaten in (("m", range(6001, 6017)), ("w", range(6101, 6117))):
    for art, titel, arten in (("a", "alle Gene gewürfelt", []), ("b", "nur Gestalt-Gene (und Züge) gewürfelt", ["Merkmal"])):
        for kam in ("K1", "K2", "K5"):
            spalten = [{"titel": str(s), "saat": s} for s in saaten]
            if art == "a":      # alles aus der Saat, auch Frisur und Bart
                person = {"geschlecht": g, "haar": {"modus": "tressfx", "ruhe": True}, "warten": WARTEN_TFX}
            else:               # Haut, Haar, Augen, Frisur und Bart fest
                person = dict(mann() if g == "m" else frau())
                person["gene"] = dict(person.get("gene", {}), **HAUT["H2"])
            erzeugt.append(schreiben(f"p04_vielfalt16_{g}_{art}_{kam.lower()}", {
                "titel": f"P4 Vielfalt 16 {'Männer' if g == 'm' else 'Frauen'} 35 – {titel}, {kam}, L-S",
                "pruefplan": "P4", "ab": "B", "zelle": ZELLE[kam],
                "alle": dict(zelle(kam), kopf="standard", licht="spiel", alter=35, saat_arten=arten, **person),
                "spalten": spalten[:8], "zeilen": [{"titel": "1–8"}, {"titel": "9–16", "zellen": spalten[8:]}]}))

# ------------------------------------------------------------------ P5 familie (ab B)
FAMILIE = {"6211": [6201, 6202], "6212": [6203, 6204], "6221": [6211, 6212], "6222": [6211, 6212],
           "6223": [6211, 6212], "6224": [6211, 6212]}
GESCHL = {6201: "m", 6202: "w", 6203: "m", 6204: "w", 6211: "m", 6212: "w", 6221: "m", 6222: "w", 6223: "m", 6224: "w"}
for regel in ("sichtbar", "mittel"):
    zeilen = []
    for titel, saaten in (("Großeltern", [6201, 6202, 6203, 6204]), ("Eltern", [6211, 6212]), ("Kinder", [6221, 6222, 6223, 6224])):
        zellen = [dict(mann() if GESCHL[s] == "m" else frau(), saat=s, titel=str(s)) for s in saaten]
        zeilen.append({"titel": titel, "zellen": zellen})
    erzeugt.append(schreiben(f"p05_familie_{regel}", {
        "titel": f"P5 Familie (Allelregel „{regel}“, wirkt ab S7) – K2, L-S, alle 35 Jahre",
        "pruefplan": "P5", "ab": "B", "zelle": ZELLE["K2"],
        "alle": dict(zelle("K2"), kopf="standard", licht="spiel", alter=35, familie=FAMILIE, allelregel=regel),
        "spalten": [{"titel": ""}] * 4, "zeilen": zeilen}))

# ------------------------------------------------------------------ P6 haut_alter (ab C)
for kam in ("K4", "N1"):
    zeilen = []
    for g in ("m", "w"):
        for w in (0, 1):
            z = (mann() if g == "m" else frau())
            z = dict(z, titel=f"{'Mann' if g == 'm' else 'Frau'}, Wetter {w}", wetter=w)
            z["gene"] = dict(z.get("gene", {}), **HAUT["H2"])
            zeilen.append(z)
    erzeugt.append(schreiben(f"p06_haut_alter_{kam.lower()}", {
        "titel": f"P6 Haut und Alter – {kam}, L-S (für die Blindschätzung gemischt ab S11)",
        "pruefplan": "P6", "ab": "C", "zelle": ZELLE[kam], "alle": dict(zelle(kam), kopf="standard", licht="spiel"),
        "spalten": [{"titel": f"{a} Jahre", "alter": a} for a in (20, 35, 50, 70)], "zeilen": zeilen}))

# ------------------------------------------------------------------ P7 formpruefung
MIMIK = [("neutral", {}), ("jawOpen 0,7", {"jawOpen": 0.7}), ("eyeBlink 1", {"eyeBlinkLeft": 1.0, "eyeBlinkRight": 1.0}),
         ("mouthSmile 0,8", {"mouthSmileLeft": 0.8, "mouthSmileRight": 0.8}), ("browInnerUp 0,8", {"browInnerUp": 0.8})]
PERS7 = [("Säugling 1", {"alter": 1, "geschlecht": "m", "haar": {"modus": "aus"}}, True),
         ("Kind 5", {"alter": 5, "geschlecht": "w", "haar": {"modus": "aus"}}, True),
         ("Mann 35", {"alter": 35, "geschlecht": "m", "haar": {"modus": "aus"}}, False),
         ("Frau 35", {"alter": 35, "geschlecht": "w", "haar": {"modus": "aus"}}, False)]
for kam in ("K2", "N2", "N4", "N5"):
    zeilen = []
    for titel, p, kind in PERS7:
        z = dict(p, titel=titel)
        if kind and kam == "K2":
            z.update({"spanne_m": 0, "ziel_m": 0})
        zeilen.append(z)
    erzeugt.append(schreiben(f"p07_formpruefung_{kam.lower()}", {
        "titel": f"P7 Formprüfung – {kam}, L-V, ohne Haar (Kinder in K2 mit automatischer Spanne)",
        "pruefplan": "P7", "ab": "A", "zelle": ZELLE[kam],
        "alle": dict(zelle(kam), kopf="standard", licht="vorlage"),
        "spalten": [{"titel": t, "mimik": m} for t, m in MIMIK], "zeilen": zeilen}))
# P7b Grat- und Nahtbogen: Formnormalen aus (kopf5.glb) und an (kopf5_mn.glb), Streiflicht, Büstennaht
zeilen = []
for titel, p, kind in PERS7:
    zeilen.append(dict(p, titel=titel))
erzeugt.append(schreiben("p07b_grat_naht", {
    "titel": "P7b Grat und Naht: Formnormalen aus | an (Streiflicht N5) · Büstennaht aus | an (K2, Ansicht Büste)",
    "pruefplan": "P7b", "ab": "A", "zelle": [600, 720], "alle": {"licht": "vorlage"},
    "spalten": [dict(zelle("N5"), titel="N5 ohne Formnormalen", kopf="kopf5.glb"),
                dict(zelle("N5"), titel="N5 mit Formnormalen", kopf="kopf5_mn.glb"),
                {"titel": "Büste ohne", "kopf": "kopf5.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0,
                 "schluessel_richtung": [-100, 20]},
                {"titel": "Büste mit", "kopf": "kopf5_mn.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0,
                 "schluessel_richtung": [-100, 20]}],
    "zeilen": zeilen}))
# P7b für Stand 6 (Prüferbefund Etappe A: der Bogen oben zeigt nur glb5 mit haut3): kopf6_ohne.glb (alle Formnormalen
# entfernt, glb_formnormalen.py ohne_vorsilben=makro_,gen_,alter_) gegen den ausgelieferten kopf6.glb, haut6
erzeugt.append(schreiben("p07b_grat_naht6", {
    "titel": "P7b Stand 6: Formnormalen aus | an (Streiflicht N5) · Büstennaht aus | an (K2, Büste) – kopf6_ohne | kopf6, haut6",
    "pruefplan": "P7b", "ab": "A", "zelle": [600, 720], "alle": {"licht": "vorlage", "stand": 6},
    "spalten": [dict(zelle("N5"), titel="N5 ohne Formnormalen", kopf="kopf6_ohne.glb"),
                dict(zelle("N5"), titel="N5 mit Formnormalen", kopf="kopf6.glb"),
                {"titel": "Büste ohne", "kopf": "kopf6_ohne.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0,
                 "schluessel_richtung": [-100, 20]},
                {"titel": "Büste mit", "kopf": "kopf6.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0,
                 "schluessel_richtung": [-100, 20]}],
    "zeilen": zeilen}))

# ------------------------------------------------------------------ P8 frisuren10 (ab B)
FRISUREN = {"m": ["topfschnitt", "mann_pagen", "haarrolle", "schulterlang", "mann_kurz", "stirnfransen", "krauskopf",
                  "tonsur", "zerzaust", "kranz"],
            "w": ["frau_zoepfe", "ein_zopf", "frau_offen", "flechtkranz", "schnecken", "zopfschlaufen", "frau_knoten",
                  "haarnetz", "hohe_stirn", "krause_locken"]}
for g in ("m", "w"):
    zeilen = [dict(mann() if g == "m" else frau(), titel=f, haar={"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": f, "bart": ""}})
              for f in FRISUREN[g]]
    erzeugt.append(schreiben(f"p08_frisuren10_{g}", {
        "titel": f"P8 Zehn Frisuren {'Männer' if g == 'm' else 'Frauen'} – vorn, ¾, Profil, hinten (L-S)",
        "pruefplan": "P8", "ab": "B", "zelle": [420, 480], "alle": {"kopf": "standard", "licht": "spiel", "alter": 35,
                                                                     "ansicht": "kopf", "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02},
        "spalten": [{"titel": "vorn", "winkel": 0}, {"titel": "¾", "winkel": 35}, {"titel": "Profil", "winkel": 88},
                    {"titel": "hinten", "winkel": 160}], "zeilen": zeilen}))

# ------------------------------------------------------------------ P9 stufen (ab D)
erzeugt.append(schreiben("p09_stufen", {
    "titel": "P9 Stufen Niedrig | Mittel | Hoch | Ultra (wirkt ab S14) – K4, L-S", "pruefplan": "P9", "ab": "D",
    "zelle": ZELLE["K4"], "alle": dict(zelle("K4"), kopf="standard", licht="spiel", alter=35),
    "spalten": [{"titel": "Niedrig", "stufe": "niedrig"}, {"titel": "Mittel", "stufe": "mittel"},
                {"titel": "Hoch", "stufe": "hoch"}, {"titel": "Ultra", "stufe": "ultra", "ultra": True}],
    "zeilen": [dict(mann(), titel="Mann 35"), dict(frau(), titel="Frau 35")]}))

# ------------------------------------------------------------------ P10 kalibrier
erzeugt.append(schreiben("p10_kalibrier", {
    "titel": "P10 Kalibrierung: Graukarte 18 % | Hautfeld Typ III | Farbtest (Albedo) | Prüfkugeln (Wrap 0, Wrap 0,4, Standard) L-V | dieselben L-S",
    "pruefplan": "P10", "ab": "A", "zelle": ZELLE["K1"], "alle": dict(zelle("K1"), kopf="standard", licht="vorlage"),
    "spalten": [{"titel": "Graukarte", "pruefkoerper": "graukarte"}, {"titel": "Hautfeld III", "pruefkoerper": "hautfeld"},
                {"titel": "Farbtest (Albedo)", "pruefkoerper": "farbtest", "anzeige": "albedo"},
                {"titel": "Kugeln L-V", "pruefkoerper": "kugel"}, {"titel": "Kugeln L-S", "pruefkoerper": "kugel", "licht": "spiel"}],
    "zeilen": [{"titel": "Stand 5", "stand": 5}, {"titel": "Stand 6", "stand": 6}]}))
# S2: Prüfkugeln (Lichtart „kugeltest“: nur das Schlüssellicht, linear, schwarzer Grund, orthografische Kamera – alle
# drei Kugeln sehen genau dieselbe Blickrichtung). Spalten: Winkel zwischen Licht und Blick 0/30/60/80/150°; je Zelle
# links haut6 (Wrap 0, eine Keule), Mitte StandardMaterial3D, rechts haut3 (Phase 5). Zeile Glanz: Albedo 0 (Energie 8,
# damit die Glanzspitze gut über dem 8-Bit-Raster liegt); Streulicht: Glanz 0; Streiflicht: Glanz mit Energie 0,3, als
# Summe über die ganze Kugel. Gewertet wird 80° (N5 laut Plan 5.1); 150° (Licht von hinten seitlich) ist ein Zusatz.
# Prüferbefund Etappe A: mit Energie 1 übersteuerte der Rand bei 150° (Spitze 1,0), die Summe war nicht belastbar.
erzeugt.append(schreiben("p10b_kugeln", {
    "titel": "P10b Prüfkugeln: haut6 (Wrap 0, eine Keule) | StandardMaterial3D | haut3 – Glanz (Albedo 0) und Streulicht (Glanz 0), linear",
    "pruefplan": "P10", "ab": "A", "zelle": ZELLE["K1"],
    "alle": dict(zelle("K1"), kopf="standard", licht="kugeltest", stand=6, kamera="ortho"),
    "spalten": [{"titel": f"Licht {w}° zur Blickachse", "schluessel_richtung": [-w, 0]} for w in (0, 30, 60, 80, 150)],
    "zeilen": [{"titel": "Glanz", "pruefkoerper": "kugel6_glanz", "schluessel": {"light_energy": 8.0}},
               {"titel": "Streulicht", "pruefkoerper": "kugel6_streu", "schluessel": {"light_energy": 1.0}},
               {"titel": "Streiflicht (Glanz, Energie 0,3)", "pruefkoerper": "kugel6_glanz", "schluessel": {"light_energy": 0.3}}]}))
# S2: Formnormalen A/B am ausgelieferten Kopf (Prüferbefund Etappe A; bis dahin kopf5.glb gegen kopf5_mn.glb mit haut3):
# kopf6_ohne.glb (alle Formnormalen entfernt) gegen kopf6.glb (Normalen für gen_ und alter_, makro_ ohne), Stand 6.
# Bogen a: Mann 30 unter L-V vorn (Messung Kiefer und Kinn gegen die obere Wange); Bogen b: Grat- und Dellenprüfung
# über alle Alter samt Säugling, Kind und Büstennaht (Streiflicht).
erzeugt.append(schreiben("s2_ab_mann30", {
    "titel": "S2 A/B Formnormalen: Mann 30, L-V – K1 ohne | K1 mit | K2 ohne | K2 mit (kopf6_ohne.glb | kopf6.glb, haut6)",
    "pruefplan": "S2", "ab": "A", "zelle": ZELLE["K1"], "alle": {"licht": "vorlage", "stand": 6, "alter": 30, "geschlecht": "m",
                                                                  "haar": {"modus": "aus"}},
    "spalten": [dict(zelle("K1"), titel="K1 ohne", kopf="kopf6_ohne.glb"), dict(zelle("K1"), titel="K1 mit", kopf="kopf6.glb"),
                dict(zelle("K2"), titel="K2 ohne", kopf="kopf6_ohne.glb"), dict(zelle("K2"), titel="K2 mit", kopf="kopf6.glb")],
    "zeilen": [{"titel": "Mann 30"}]}))
PERS_AB = [("Säugling 1", {"alter": 1, "geschlecht": "m"}, True), ("Kind 5", {"alter": 5, "geschlecht": "w"}, True)] + \
    [(f"{'Mann' if g == 'm' else 'Frau'} {a}", {"alter": a, "geschlecht": g}, False) for g in ("m", "w") for a in (20, 35, 70)]
erzeugt.append(schreiben("s2_ab_formen", {
    "titel": "S2 A/B Formnormalen über alle Alter: K2 ohne | K2 mit | Büste ohne | Büste mit (Streiflicht, L-V, Stand 6: kopf6_ohne | kopf6)",
    "pruefplan": "S2", "ab": "A", "zelle": [600, 720], "alle": {"licht": "vorlage", "stand": 6, "haar": {"modus": "aus"}},
    "spalten": [dict(zelle("K2_kind"), titel="K2 ohne", kopf="kopf6_ohne.glb", schluessel_richtung=[-100, 20]),
                dict(zelle("K2_kind"), titel="K2 mit", kopf="kopf6.glb", schluessel_richtung=[-100, 20]),
                {"titel": "Büste ohne", "kopf": "kopf6_ohne.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0, "schluessel_richtung": [-100, 20]},
                {"titel": "Büste mit", "kopf": "kopf6.glb", "ansicht": "buste", "winkel": 35, "hoehe": 0, "schluessel_richtung": [-100, 20]}],
    "zeilen": [dict(p, titel=t) for t, p, _ in PERS_AB]}))
# S2: dünne Formdaten – kopf6.glb (dünn) gegen dieselben Formen dicht gespeichert (arbeit/phase6/probe/dicht/kopf6.glb,
# gleiche Normalen), Stand 6; die Bilder müssen gleich sein (mittlere Abweichung ≤ 1/255)
DICHT = "~/werkzeuge/charakter/arbeit/phase6/probe/dicht/kopf6.glb"
erzeugt.append(schreiben("s2_duenn", {
    "titel": "S2 Dünne Formdaten: kopf6.glb dünn | dieselben Formen dicht (Stand 6)", "pruefplan": "S2", "ab": "A",
    "zelle": ZELLE["K1"], "alle": {"stand": 6, "haar": {"modus": "aus"}},
    "spalten": [{"titel": "dünn (kopf6.glb)", "kopf": "kopf6.glb"}, {"titel": "dicht", "kopf": DICHT}],
    "zeilen": [dict(zelle("K1"), titel="Mann 30, K1, L-V", alter=30, geschlecht="m", licht="vorlage"),
               dict(zelle("K2"), titel="Frau 30, K2, L-S", alter=30, geschlecht="w", licht="spiel"),
               dict(zelle("N4"), titel="Mann 35 Mimik, N4", alter=35, geschlecht="m", licht="vorlage",
                    mimik={"jawOpen": 0.5, "mouthSmileLeft": 0.6, "mouthSmileRight": 0.6}),
               dict(zelle("K2_kind"), titel="Säugling 1, K2", alter=1, geschlecht="m", licht="vorlage")]}))
# S3/E5: Entscheidungshilfe für Halt A – Stand 6 unter AgX wie bisher und mit Sättigung (Environment.adjustment_*),
# der einfachsten Form von „Sättigung bzw. LUT der Porträtbühne“; dazu AgX-Kontrast. Nur zum Vergleich, nicht Standard.
erzeugt.append(schreiben("s3_e5_agx", {
    "titel": "S3/E5 Entscheidungshilfe: AgX wie bisher | Sättigung 1,25 | Sättigung 1,5 | AgX-Kontrast 1,5 – Stand 6, Mann und Frau 35 (H2)",
    "pruefplan": "S3", "ab": "A", "zelle": ZELLE["K1"], "alle": dict(zelle("K1"), kopf="standard", stand=6, alter=35,
                                                                      haar={"modus": "aus"}, gene={"haut_helligkeit": 185, "haut_ton": 105}),
    "spalten": [{"titel": "AgX wie bisher"},
                {"titel": "Sättigung 1,25", "umgebung": {"adjustment_enabled": True, "adjustment_saturation": 1.25}},
                {"titel": "Sättigung 1,5", "umgebung": {"adjustment_enabled": True, "adjustment_saturation": 1.5}},
                {"titel": "AgX-Kontrast 1,5", "umgebung": {"tonemap_agx_contrast": 1.5}}],
    "zeilen": [{"titel": "Mann 35, L-V", "geschlecht": "m", "licht": "vorlage"}, {"titel": "Frau 35, L-V", "geschlecht": "w", "licht": "vorlage"},
               {"titel": "Mann 35, L-S", "geschlecht": "m", "licht": "spiel"}, {"titel": "Frau 35, L-S", "geschlecht": "w", "licht": "spiel"}]}))
# S2: Ultra (Bildschirm-SSS) ohne grauen Saum – Stand 5 (haut3_ultra, Allzweck-Kern) gegen Stand 6 (haut6_ultra, Hautkern)
erzeugt.append(schreiben("s2_ultra", {
    "titel": "S2 Ultra: Stand 5 Hoch | Stand 5 Ultra | Stand 6 Hoch | Stand 6 Ultra – K4 und N1 (Wange nah), L-S, ohne Haar",
    "pruefplan": "S2", "ab": "A", "zelle": ZELLE["K4"], "alle": {"kopf": "standard", "licht": "spiel", "alter": 35,
                                                                  "haar": {"modus": "aus"}},
    "spalten": [{"titel": "Stand 5 Hoch", "stand": 5}, {"titel": "Stand 5 Ultra", "stand": 5, "ultra": True},
                {"titel": "Stand 6 Hoch", "stand": 6}, {"titel": "Stand 6 Ultra", "stand": 6, "ultra": True}],
    "zeilen": [dict(zelle("K4"), titel="Mann 35 K4", geschlecht="m"), dict(zelle("K4"), titel="Frau 35 K4", geschlecht="w"),
               dict(zelle("N1"), titel="Mann 35 N1", geschlecht="m")]}))

# ------------------------------------------------------------------ P11 farbtafel
zeilen = []
for rot in (0, 70, 170):
    zeilen.append({"titel": f"haar_rot {rot}", "zellen": [{"gene": {"haar_helligkeit": hh, "haar_rot": rot, "bartwuchs": 190}}
                                                         for hh in (0, 60, 100, 160, 225)]})
erzeugt.append(schreiben("p11_farbtafel_haar", {
    "titel": "P11 Farbtafel Haar, Bart und Brauen (Spalten haar_helligkeit 0/60/100/160/225) – K4, L-S",
    "pruefplan": "P11", "ab": "A", "zelle": ZELLE["K4"],
    "alle": dict(zelle("K4"), kopf="standard", licht="spiel", alter=35, geschlecht="m", warten=WARTEN_TFX,
                 haar={"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "mann_pagen", "bart": "vollbart"}}),
    "spalten": [{"titel": f"helligkeit {hh}"} for hh in (0, 60, 100, 160, 225)], "zeilen": zeilen}))
zeilen = []
for at in (30, 80, 135, 185, 235):
    zeilen.append({"titel": f"augen_ton {at}", "zellen": [{"gene": {"augen_helligkeit": ah, "augen_ton": at}} for ah in (25, 75, 130, 180, 230)]})
erzeugt.append(schreiben("p11_farbtafel_augen", {
    "titel": "P11 Farbtafel Augen (Spalten augen_helligkeit 25/75/130/180/230) – N2 (Augen nah), L-S",
    "pruefplan": "P11", "ab": "A", "zelle": ZELLE["N2"],
    "alle": dict(zelle("N2"), kopf="standard", licht="spiel", alter=35, geschlecht="m", haar={"modus": "aus"}),
    "spalten": [{"titel": f"helligkeit {ah}"} for ah in (25, 75, 130, 180, 230)], "zeilen": zeilen}))

# ------------------------------------------------------------------ P12 zuordnung (ab B; Etappe B: Blindtest)
# Strenger als im Plan: nur Gestalt-Gene, Züge und Brauen aus der Saat (Haut, Haar- und Augenfarbe fest H2), damit die
# Zuordnung an der Gesichtsform hängt. Blatt A vorn (K1, groß), Blatt B ¾ gemischt (K2), Blatt C Stammbaumgröße ¾
# gemischt (K5, 128 × 160), Blatt D 10 Personen mit anderer Frisur und Innenlicht (L-I, K2) gemischt. Der Schlüssel
# (~/werkzeuge/charakter/bilder/phase6/B/schluessel_p12.json, nicht im Repository) entsteht hier mit festem Startwert; sein SHA-256 steht im Bericht, bevor jemand urteilt.
import hashlib  # noqa: E402
import random  # noqa: E402
schluessel = {"beschreibung": "P12 Zuordnung: je Blatt Ziffer -> Person (Blatt A Ziffer)", "geschlechter": {}}
for g, saaten in (("m", list(range(7001, 7017))), ("w", list(range(7101, 7117)))):
    person = dict(mann() if g == "m" else frau())
    person["gene"] = dict(person.get("gene", {}), **HAUT["H2"])
    person["saat_arten"] = ["Merkmal"]
    zz = random.Random(1212 + (0 if g == "m" else 1))
    reihen = {}
    for blatt, kam, extra in (("A", "K1", {}), ("B", "K2", {}), ("C", "K5", {}), ("D", "K2", {"licht": "innen"})):
        auswahl = saaten if blatt != "D" else saaten[:10]
        ordnung = list(range(len(auswahl)))
        if blatt != "A":
            zz.shuffle(ordnung)
        zellen = []
        for i, j in enumerate(ordnung):
            z = {"titel": f"{blatt}{i + 1}", "saat": auswahl[j]}
            if blatt == "D":
                z["haar"] = {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "topfschnitt" if g == "m" else "ein_zopf", "bart": ""}}
            zellen.append(z)
        reihen[blatt] = {f"{blatt}{i + 1}": f"A{saaten.index(auswahl[j]) + 1}" for i, j in enumerate(ordnung)}
        halb = (len(zellen) + 1) // 2
        erzeugt.append(schreiben(f"p12_zuordnung_{g}_{blatt.lower()}", {
            "titel": f"P12 Zuordnung {'Männer' if g == 'm' else 'Frauen'} – Blatt {blatt} "
                     + {"A": "vorn (K1)", "B": "¾ gemischt (K2)", "C": "Stammbaumgröße ¾ gemischt (K5)",
                        "D": "andere Frisur, Innenlicht, gemischt (K2)"}[blatt],
            "pruefplan": "P12", "ab": "B", "zelle": ZELLE[kam],
            "alle": dict(dict(zelle(kam), kopf="standard", licht="spiel", alter=35, **person), **extra),
            "spalten": [{"titel": ""}] * halb,
            "zeilen": [{"titel": "", "zellen": zellen[:halb]}, {"titel": "", "zellen": zellen[halb:]}]}))
    schluessel["geschlechter"][g] = {"saaten_A": saaten, **reihen}
roh = json.dumps(schluessel, sort_keys=True, ensure_ascii=False, indent=1).encode("utf-8")
_mappe = os.path.expanduser("~/werkzeuge/charakter/bilder/phase6/B")      # nicht ins Repository (Blindtest)
os.makedirs(_mappe, exist_ok=True)
with open(os.path.join(_mappe, "schluessel_p12.json"), "wb") as f:
    f.write(roh)
print("P12-Schlüssel SHA-256", hashlib.sha256(roh).hexdigest())

# ------------------------------------------------------------------ P13 zuege_2s (ab B): jeder Zug bei −2σ und +2σ
# Gewicht = (Wert − 128) / 32 × 0,7, also ±2σ = ±1,4 (Zusatzform); Mittelgesicht 30 Jahre, ohne Haar, L-V
for g in ("m", "w"):
    zeilen = [{"titel": f"zug_{k:02d}", "zellen": [
        {"formen": {f"zug_{k:02d}": -1.4}, "winkel": 0}, {"winkel": 0}, {"formen": {f"zug_{k:02d}": 1.4}, "winkel": 0},
        {"formen": {f"zug_{k:02d}": -1.4}, "winkel": 35}, {"formen": {f"zug_{k:02d}": 1.4}, "winkel": 35}]} for k in range(1, 11)]
    erzeugt.append(schreiben(f"p13_zuege_2s_{g}", {
        "titel": f"P13 Gesichtszüge ±2σ – {'Mann' if g == 'm' else 'Frau'} 30, L-V, ohne Haar", "pruefplan": "P13", "ab": "B",
        "zelle": [360, 430], "alle": dict(zelle("K1"), kopf="standard", licht="vorlage", alter=30, geschlecht=g, haar={"modus": "aus"}),
        "spalten": [{"titel": "−2σ vorn"}, {"titel": "Mittel vorn"}, {"titel": "+2σ vorn"}, {"titel": "−2σ ¾"}, {"titel": "+2σ ¾"}],
        "zeilen": zeilen}))

# ------------------------------------------------------------------ P14 extreme (ab B): Auswahl aus der 1000er-Prüfung
# Die Saaten schreibt tools/charakter/messen/stichprobe6.py (extreme_20, zufall_10); Bogen p14_extreme_<g> erzeugt
# boegen6/p14_aus_stichprobe.py.

# ------------------------------------------------------------------ P15 referenz_stil (ab C)
erzeugt.append(schreiben("p15_referenz_stil", {
    "titel": "P15 Stilvergleich BG3/CK3 (Referenztafel nur lokal, ~/werkzeuge/charakter/referenz_stil/)",
    "pruefplan": "P15", "ab": "C", "zelle": ZELLE["K4"], "alle": dict(zelle("K4"), kopf="standard", licht="spiel", alter=35),
    "spalten": [dict(mann(), titel="Mann 35"), dict(frau(), titel="Frau 35")], "zeilen": [{"titel": "K4"}]}))

# ------------------------------------------------------------------ P16 ruhegesicht (ab D)
ZUEGE = ["Jaehzornig", "Gesellig", "Feige", "Zynisch", "Hochmuetig", "Scheu", "Trinker", "Kraenklich", "Schoen", "Stark",
         "Gesellig", "Jaehzornig"]
erzeugt.append(schreiben("p16_ruhegesicht", {
    "titel": "P16 Ruhegesicht und Lebensspuren (wirkt ab S12) – K4, L-S", "pruefplan": "P16", "ab": "D",
    "zelle": ZELLE["K4"], "alle": dict(zelle("K4"), kopf="standard", licht="spiel", alter=40),
    "spalten": [{"titel": z, "saat": 6301 + i, "leben": {"zuege": [z]}, **(mann() if i % 2 == 0 else frau())}
                for i, z in enumerate(ZUEGE[:6])],
    "zeilen": [{"titel": "1–6"}, {"titel": "7–12", "zellen": [{"titel": z, "saat": 6307 + i, "leben": {"zuege": [z]},
                                                               **(mann() if i % 2 == 0 else frau())} for i, z in enumerate(ZUEGE[6:])]}]}))

# ------------------------------------------------------------------ M0 Bild gegen Netz (S0)
# Mittelgesicht ohne Haar orthografisch und perspektivisch, vorn und genau im Profil (90°); Licht L-S (dunkler Grund für
# den Umriss). Ausgewertet von pruefen6.py (Umrissbreiten, Pupillen, Profilpunkte im Bild gegen netz_masse).
erzeugt.append(schreiben("m0_bild_netz", {
    "titel": "M0 Bild gegen Netz: vorn ortho | vorn perspektivisch | Profil ortho | Profil perspektivisch (L-S, ohne Haar)",
    "pruefplan": "S0", "ab": "A", "zelle": [900, 1080],
    "alle": {"kopf": "standard", "licht": "spiel", "ansicht": "kopf", "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02,
             "haar": {"modus": "aus"}, "alter": 30},
    "spalten": [{"titel": "vorn ortho", "winkel": 0, "kamera": "ortho"}, {"titel": "vorn perspektivisch", "winkel": 0},
                {"titel": "Profil ortho", "winkel": 90, "kamera": "ortho"}, {"titel": "Profil perspektivisch", "winkel": 90}],
    "zeilen": [{"titel": "Mann 30", "geschlecht": "m"}, {"titel": "Frau 30", "geschlecht": "w"}]}))

# ------------------------------------------------------------------ M1 Gewichte in der Szene (S0)
# 20 Zufallspersonen (wie personen_s0.json) klein und ohne Haar: Die Begleitdateien tragen die Formgewichte aus der
# Szene; pruefen6.py vergleicht sie mit dem Gewichteausdruck (--gewichte, ohne Grafikkarte).
erzeugt.append(schreiben("m1_gewichte20", {
    "titel": "M1 Gewichte in der Szene: 20 Zufallspersonen (6001–6010 Männer, 6101–6110 Frauen), 35 Jahre",
    "pruefplan": "S0", "ab": "A", "zelle": [200, 240],
    "alle": dict(zelle("K1"), kopf="standard", licht="spiel", alter=35, haar={"modus": "aus"}),
    "spalten": [{"titel": str(6001 + i), "saat": 6001 + i} for i in range(10)],
    "zeilen": [{"titel": "Männer", "geschlecht": "m"},
               {"titel": "Frauen", "geschlecht": "w", "zellen": [{"titel": str(6101 + i), "saat": 6101 + i} for i in range(10)]}]}))

print(len(erzeugt), "Bögen:", " ".join(erzeugt))
