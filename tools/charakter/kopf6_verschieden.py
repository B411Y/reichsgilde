#!/usr/bin/env python3
"""Verschiedenheit der Köpfe (Plan 01.11.2026), Etappe E1: Regeldaten eines Probekopfs neu eichen – ohne Neubau.

Liest die regeln6.json eines Probekopfs (z. B. ~/werkzeuge/charakter/glb6d) und schreibt eine neue in einen anderen
Kopfordner (z. B. glb6e, der übrige Inhalt dort sind Verknüpfungen auf glb6d). Die Eichung von kopf6_eichen.py ist
linear (Wirkung je Formgewicht bei 0,5 gemessen, Stärke = Ziel / Wirkung); deshalb lässt sich das Ziel je Gen aus
„wirkung_mm_je_1“ neu rechnen, ohne das Netz neu zu messen.

Was sich ändert (Nutzerentscheidung 01.11.2026: Augen und Brauen leicht spielhaft ≈ 1,3 × der natürlichen Streuung):
  - Augenpartie: Ziel je σ = 1,3 × natürliche Streuung (vorher 0,75 ×); Lidspalte mit der natürlichen Streuung aus der
    Literatur (≈ 1,2 mm statt 0,84); Augentiefe × 1,3 gegen vorher; Stärke dort höchstens 1,0 je σ (vorher 0,6).
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
# (Lidspaltenhöhe ps–pi, σ 1,0–1,7 mm); Augentiefe nur × 1,3 (Knochen, Fratzengefahr)
AUGEN = {
    "augen_groesse": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "lidspalte": UEBERZEICHNUNG / NATUR_FAKTOR_ALT * 1.2 / 0.84,
    "schlupflid": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "augen_neigung": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "brauen_stand": UEBERZEICHNUNG / NATUR_FAKTOR_ALT,
    "augen_tiefe": 1.3,
}
NASEN_ZIELE = ["nose/nose-point-down", "nose/nose-point-up", "nose/nose-scale-vert-incr", "nose/nose-scale-vert-decr",
               "nose/nose-septumangle-incr", "nose/nose-septumangle-decr", "nose/nose-hump-incr", "nose/nose-hump-decr"]


def main():
    opt = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    ein, aus = os.path.expanduser(opt["ein"]), os.path.expanduser(opt["aus"])
    R = json.load(open(ein, encoding="utf-8"))
    bericht = {}
    for gen, f in AUGEN.items():
        e = R["gene"][gen]
        alt = {g: dict(e["staerke"][g]) for g in "mw"}
        for g in "mw":
            ziel = e["ziel_mm_je_sigma"][g] * f
            for richt in ("plus", "minus"):
                d = abs(e["wirkung_mm_je_1"][g][richt])
                st = round(min(ziel / d, STAERKE_MAX_AUGEN), 4) if d > 1e-6 else 0.0
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
        "nasenbudget": {"gene": ["nase_laenge", "nasenspitze", "nase_hoecker"], "norm_max": 2.2},
        "brauen": {
            "v_nach_unten": True,          # braue6: v läuft in der Karte von oben (0) nach unten (Etappe D rechnete umgekehrt)
            "mitte_v": 0.72,               # Ankerlinie für die Dicke (Anteil der Kartenhöhe von unten): dicker wächst nach unten
            "stand_mm_je_sigma": 2.0,      # zusätzlich zur Hautform
            "stand_oben_max_mm": 1.5,      # weich begrenzt (über der Braue ist in der Karte 1,5–3 mm Platz)
            "winkel_gen": "zug_11", "winkel_mm_je_sigma": 2.2,
            "dicke_je_sigma": 0.25, "dicke_min": 0.6, "dicke_max": 1.5,
            "dichte_min": 0.62, "dichte_max": 1.2,
            "dunkel_gen": "zug_12", "dunkel_je_sigma": 0.2, "hell_je_sigma": 0.25,
            "haut_schatten": 0.22,         # Brauenschatten auf der Haut (Etappe D 0,42) – er wandert nicht mit
            # Frauen: Brauen in E0 von allen drei Prüfern „fast unsichtbar“ – etwas dichter, heller nur zu 60 %
            "frau_dichte": 1.3, "frau_dicke": 1.15, "frau_hell": 0.6,
        },
        # Nasenlöcher der Frau von vorn sichtbar (Etappe D, E0-Panel): Spitze und Steg etwas tiefer (Testbogen nasenloch)
        "zusatz_w": {"w": {"gen_nasenspitze_minus": 0.4}},
    }
    os.makedirs(os.path.dirname(aus), exist_ok=True)
    with open(aus, "w", encoding="utf-8") as f:
        json.dump(R, f, indent=1, ensure_ascii=False)
    for gen, b in bericht.items():
        print(f"{gen:14s} Ziel/erreicht mm je σ  Mann {b['m'][0]:.2f} / {b['m'][1]}  Frau {b['w'][0]:.2f} / {b['w'][1]}")
    print("geschrieben", aus)


if __name__ == "__main__":
    main()
