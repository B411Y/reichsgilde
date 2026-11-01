#!/usr/bin/env python3
"""Verschiedenheit der Köpfe (Plan 01.11.2026), Etappe E1: Regeldaten eines Probekopfs neu eichen – ohne Neubau.

Liest die regeln6.json eines Probekopfs (z. B. ~/werkzeuge/charakter/glb6d) und schreibt eine neue in einen anderen
Kopfordner (z. B. glb6e, der übrige Inhalt dort sind Verknüpfungen auf glb6d). Die Eichung von kopf6_eichen.py ist
linear (Wirkung je Formgewicht bei 0,5 gemessen, Stärke = Ziel / Wirkung); deshalb lässt sich das Ziel je Gen aus
„wirkung_mm_je_1“ neu rechnen, ohne das Netz neu zu messen.

Was sich ändert (Nutzerentscheidung 01.11.2026: Augen und Brauen leicht spielhaft ≈ 1,3 × der natürlichen Streuung):
  - Augenpartie: Ziel je σ = 1,3 × natürliche Streuung (vorher 0,75 ×); Lidspalte mit der natürlichen Streuung aus der
    Literatur (≈ 1,2 mm statt 0,84); Stärke dort höchstens 1,0 je σ (vorher 0,6).
    Nase, Kinn, Kiefer und Umriss bleiben (Plan: dort streut es schon am stärksten).
  - Budget je MakeHuman-Ziel: begrenzt wird der Gen-Anteil allein (die Reifeformen verbrauchen den Spielraum nicht mehr),
    dazu eine weitere Grenze für die Summe; an den Nasenzielen zählt die Summe wie bisher (sonst stapelt sich
    nose-point-down aus Reife, Länge und Spitze).
  - Nasenbudget: √(Σ z²) über Nasenlänge, Spitze und Höcker höchstens 2,2, sonst die drei gemeinsam kleiner.
  - Oberlid: Ruhelage 0,22 → 0,18, Alterszuschlag 0,06 → 0,03 (Etappe D: Mann 70 „halb zu“).
  - Brauen je Person (Shader braue6, Abschnitt „verschieden.brauen“): Stand, Winkel, Dicke, Dunkelheit; Winkel und
    Dunkelheit vorläufig aus den freien Vorschlagsgenen zug_11 / zug_12 (erblich, σ 32; Vorschlag an kern: eigene Gene
    brauen_winkel und brauen_dunkel).
  - Nasenloch der Frau: feste Zugabe einer vorhandenen Form (Abschnitt „verschieden.zusatz“, × Reife oben).

    python3 tools/charakter/kopf6_verschieden.py --ein=~/werkzeuge/charakter/glb6d/regeln6.json \
        --aus=~/werkzeuge/charakter/glb6e/regeln6.json
"""
import json
import os
import sys

NATUR_FAKTOR_ALT = 0.75          # kopf6_eichen.py: +1σ = 0,75 × natürliche Streuung
UEBERZEICHNUNG = 1.3             # Nutzer 01.11.2026
STAERKE_MAX_AUGEN = 1.0          # Formgewicht je σ (bei 2σ = 2,0)
# Gen: Faktor auf das alte Ziel. Standard 1,3 / 0,75; Lidspalte zusätzlich mit der Literaturstreuung 1,2 statt 0,84 mm
# (Lidspaltenhöhe ps–pi, σ 1,0–1,7 mm)
AUGEN = {
    "augen_groesse": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "lidspalte": UEBERZEICHNUNG / NATUR_FAKTOR_ALT * 1.2 / 0.84,
    "schlupflid": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "augen_neigung": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "brauen_stand": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    # Augentiefe bleibt wie E0 (Runde 2: × 1,3 machte tief liegende Augen unter dem Brauenwulst zur Fratze)
    # Runde 2: Augenabstand auf 1,0 × der natürlichen Streuung (NIOSH 3,6 / 3,4 mm) – Lage der Augen ist ein starker
    # Anhaltspunkt für die Wiedererkennung; nicht 1,3 × (weit oder eng stehende Augen kippen schnell ins Karikierte)
    "augen_abstand": 1.0 / NATUR_FAKTOR_ALT,
}
# Runde 2: Höchstwert je Richtung (mm je σ). Lidspalte enger nur bis 1,3 mm/σ: in Runde 1 wirkte ein Kopf mit −2σ
# „wie mit dem Lid im Auge“ (alle drei Prüfer)
RICHTUNG_MAX = {("lidspalte", "minus"): 1.3}
# E3 (Option --e3=1; Prüfer E1: tiefe Braue + Brauenwulst = „Sims“): Hautform der Braue nach unten nur 1,0 × der
# natürlichen Streuung (nicht 1,3 ×), die Bildverschiebung nach unten weich auf 2 mm, am Umriss breiter ausblenden
E3_RICHTUNG_FAKTOR = {("brauen_stand", "minus"): 1.0 / NATUR_FAKTOR_ALT}
E3_BRAUEN = {"stand_unten_max_mm": 2.0, "rand_aus_kante": 0.55}
# Grundkopf 2 / E4 (Option --e4=1; Nutzer 01.11.2026: „immer noch zu fratzig, die erlaubten Werte zurückdrehen“):
# Augenpartie wieder 1,0 × der natürlichen Streuung statt 1,3 ×, dazu die Mäßigung im Kopf (charakter_kopf.gd,
# „verschieden.maessigung“): jedes σ weich gekappt (c · tanh(z / c)) und die Stärke der heiklen Gene kleiner; Karikatur-
# bremse, Budget, Nasenbudget, Asymmetrie, Züge und Brauen enger.
E4_UEBERZEICHNUNG = 1.0
E4_RICHTUNG_MAX = {("lidspalte", "minus"): 1.0}
HEIKEL = ["brauen_stand", "brauenwulst", "nase_laenge", "nase_breite", "nase_hoecker", "nasenspitze", "kiefer_breite",
          "kiefer_vorstand", "kinn", "kinn_breite", "augen_groesse", "lidspalte", "schlupflid", "augen_tiefe",
          "augen_neigung"]
# Umriss und Mund (nicht heikel, aber die Gesichtsbreite trägt bei Frauen 5,6 mm/σ in die Kieferbreite)
MITTEL = ["gesicht_breite", "gesicht_laenge", "wangen_fuelle", "wangenknochen", "mund_breite", "lippen", "mundwinkel"]
E4_MAESSIGUNG = {
    "beschreibung": "E4: z' = c · tanh(z / c) je Gen (kappe, sonst kappe_standard), danach Stärke × staerke_faktor",
    "kappe_standard": 1.6,
    "kappe": dict({g: 1.3 for g in HEIKEL}, **{g: 1.5 for g in MITTEL}),
    "staerke_faktor": dict({g: 0.8 for g in HEIKEL}, brauenwulst=0.7, nase_hoecker=0.75, **{g: 0.85 for g in MITTEL}),
}
E4_KARIKATUR = {"K_max": 1.15, "z_grenze": 1.8, "hoechstens_ueber": 0, "z_anheben": 1.0}
E4_BUDGET = {"gen_unten": -1.6, "gen_oben": 1.6}
E4_NASENBUDGET = {"norm_max": 1.8, "stupsnase_max": 1.3}
E4_ZUEGE_FAKTOR = 0.55
E4_ASYM_FAKTOR = {"kinn_seitlich": 0.7, "nasenspitze_seitlich": 0.6, "augenhoehe": 0.7, "brauen": 0.7,
                  "mundwinkel": 0.7, "ohrhoehe": 0.7}
E4_BRAUEN = {"stand_mm_je_sigma": 1.6, "stand_oben_max_mm": 1.2, "stand_unten_max_mm": 1.5, "winkel_mm_je_sigma": 1.2,
             "dicke_je_sigma": 0.3, "dicke_min": 0.85, "dichte_min": 0.9, "dunkel_je_sigma": 0.25,
             # Frauen: Brauen waren dünne, gepunktete Striche – dicker und dichter; Hautschatten unter der Braue schwächer
             # (er liegt fest und wirkt „aufgemalt“)
             "frau_dicke": 1.35, "frau_dichte": 1.35, "haut_schatten": 0.12}
# Haut und Brauen des Grundkopfs 2 (charakter_kopf.gd liest „verschieden.grundkopf2“)
E4_GRUNDKOPF2 = {"brauen_haerchen": 1.0, "brauen_haerchen_rest": 0.4, "poren_faktor": 1.35, "uneben": 0.85, "kontrast": 1.22,
                 "augen": {"schatten_oben": 0.7, "schatten_oben_weite": 0.5, "kontakt": 0.7, "traene": 0.45}}
NASEN_ZIELE = ["nose/nose-point-down", "nose/nose-point-up", "nose/nose-scale-vert-incr", "nose/nose-scale-vert-decr",
               "nose/nose-septumangle-incr", "nose/nose-septumangle-decr", "nose/nose-hump-incr", "nose/nose-hump-decr"]


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    ein, aus = os.path.expanduser(opt["ein"]), os.path.expanduser(opt["aus"])
    e3 = opt.get("e3", "0") == "1"
    e4 = opt.get("e4", "0") == "1"
    R = json.load(open(ein, encoding="utf-8"))
    bericht = {}
    for gen, f in AUGEN.items():
        if e4 and gen != "augen_abstand":      # Augenabstand steht schon auf 1,0 × der natürlichen Streuung
            f = f * E4_UEBERZEICHNUNG / UEBERZEICHNUNG
        e = R["gene"][gen]
        alt = {g: dict(e["staerke"][g]) for g in "mw"}
        for g in "mw":
            ziel = e["ziel_mm_je_sigma"][g] * f
            for richt in ("plus", "minus"):
                d = abs(e["wirkung_mm_je_1"][g][richt])
                z_r = min(ziel, RICHTUNG_MAX.get((gen, richt), ziel))
                if e4:
                    z_r = min(z_r, E4_RICHTUNG_MAX.get((gen, richt), z_r))
                if e3 and (gen, richt) in E3_RICHTUNG_FAKTOR:
                    z_r = min(z_r, ziel / f * E3_RICHTUNG_FAKTOR[(gen, richt)])
                st = round(min(z_r / d, STAERKE_MAX_AUGEN), 4) if d > 1e-6 else 0.0
                e["staerke"][g][richt] = st
                e["erreicht_mm_je_sigma"][g][richt] = round(st * d, 3)
            e["ziel_mm_je_sigma"][g] = round(ziel, 3)
        e["eichung_e1"] = {"faktor": round(f, 3), "staerke_vorher": alt}
        bericht[gen] = {g: (e["ziel_mm_je_sigma"][g], e["erreicht_mm_je_sigma"][g]) for g in "mw"}
    R["budget"] = {"unten": -1.0, "oben": 1.2, "modus": "gen", "gen_unten": -2.0, "gen_oben": 2.0,
                   "gesamt_unten": -1.6, "gesamt_oben": 2.4, "ziele_gesamt": NASEN_ZIELE,
                   "beschreibung": "E1: Gen-Anteil je Ziel in [gen_unten; gen_oben], Summe mit den Reifeformen in "
                                   "[gesamt_unten; gesamt_oben]; an ziele_gesamt (Nase) die Summe in [unten; oben] wie bisher"}
    R["lid_ruhe"]["reife"] = 0.18
    R["lid_ruhe"]["greis"] = 0.03
    R["verschieden"] = {
        "beschreibung": "Verschiedenheit E1 (tools/charakter/kopf6_verschieden.py): Brauen je Person, Nasenbudget, Zugaben",
        "nasenbudget": {"gene": ["nase_laenge", "nasenspitze", "nase_hoecker"], "norm_max": 2.2,
                        "stupsnase_max": 1.6},   # Runde 3: Spitze⁺ + Kürze⁻ in σ (Schweinsnase von vorn)
        "brauen": {
            "v_nach_unten": True,          # braue6: v läuft in der Karte von oben (0) nach unten (Etappe D rechnete umgekehrt)
            "mitte_v": 0.72,               # Ankerlinie für die Dicke (Anteil der Kartenhöhe von unten): dicker wächst nach unten
            "stand_mm_je_sigma": 2.5,      # zusätzlich zur Hautform (Runde 1: 2,0)
            "stand_oben_max_mm": 1.5,      # weich begrenzt (über der Braue ist in der Karte 1,5–3 mm Platz)
            "winkel_gen": "zug_11", "winkel_mm_je_sigma": 2.2,
            # Runde 2: Brauen tragen die Wiedererkennung (alle Prüfer) – Dicke und Dunkelheit breiter; Dichte nicht unter
            # 0,75 (darunter zerfiel die Braue in Striche: „gestrichelt, zerrissen“)
            "dicke_je_sigma": 0.35, "dicke_je_sigma_minus": 0.15, "dicke_min": 0.75, "dicke_max": 1.8,   # Runde 3
            "dichte_min": 0.75, "dichte_max": 1.2,
            "dunkel_gen": "zug_12", "dunkel_je_sigma": 0.3, "hell_je_sigma": 0.3,
            "rand_aus": 1.0,               # Karte am Umriss ausblenden (schwebende hintere Braue)
            "haut_schatten": 0.22,         # Brauenschatten auf der Haut (Etappe D 0,42) – er wandert nicht mit
            # Frauen: Brauen in E0 von allen drei Prüfern „fast unsichtbar“ – etwas dichter, heller nur zu 60 %
            "frau_dichte": 1.3, "frau_dicke": 1.15, "frau_hell": 0.6,
        },
        # Nasenlöcher der Frau von vorn sichtbar (Etappe D, E0-Panel): Spitze und Steg etwas tiefer (Testbogen nasenloch)
        "zusatz_w": {"w": {"gen_nasenspitze_minus": 0.5}},
    }
    if e3:
        R["verschieden"]["brauen"].update(E3_BRAUEN)
        R["verschieden"]["beschreibung"] += "; E3: Braue nach unten begrenzt, breiter ausgeblendet"
    if e4:
        R["verschieden"]["maessigung"] = E4_MAESSIGUNG
        R["karikatur"].update(E4_KARIKATUR)
        R["budget"].update(E4_BUDGET)
        R["verschieden"]["nasenbudget"].update(E4_NASENBUDGET)
        R["zuege"]["faktor"] = E4_ZUEGE_FAKTOR
        a = R["asymmetrie"]
        a["grenzen_mm"] = [round(g * E4_ASYM_FAKTOR[m], 3) for g, m in zip(a["grenzen_mm"], a["merkmale"])]
        R["verschieden"]["brauen"].update(E4_BRAUEN)
        R["verschieden"]["grundkopf2"] = E4_GRUNDKOPF2
        R["verschieden"]["beschreibung"] += "; E4: Mäßigung (Augenpartie 1,0 × natürlich, weiche Kappen, engere Grenzen)"
    os.makedirs(os.path.dirname(aus), exist_ok=True)
    with open(aus, "w", encoding="utf-8") as f:
        json.dump(R, f, indent=1, ensure_ascii=False)
    for gen, b in bericht.items():
        print(f"{gen:14s} Ziel/erreicht mm je σ  Mann {b['m'][0]:.2f} / {b['m'][1]}  Frau {b['w'][0]:.2f} / {b['w'][1]}")
    print("geschrieben", aus)


if __name__ == "__main__":
    main()
