"""Bogen P14 (Extreme) aus der 1000er-Prüfung (Charakter Phase 6, S5): je Geschlecht die 20 extremsten Köpfe nach dem
Karikaturmaß K der Gene und 10 zufällige aus den Startwerten 10001–11000, vorn (K1) und ¾ (K2), L-S.

Aufruf: python3 tools/charakter/boegen6/p14_aus_stichprobe.py <stichprobe.json>   (von messen/stichprobe6.py)
"""
import json
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)


def main():
    st = json.load(open(sys.argv[1], encoding="utf-8"))
    for g, b in st["geschlechter"].items():
        saaten = [(s, "E") for s in b["extreme_20"]] + [(s, "Z") for s in b["zufall_10"]]
        haar = {"modus": "tressfx", "ruhe": True, "vorgabe": {"frisur": "mann_kurz" if g == "m" else "frau_knoten", "bart": ""}}
        zeilen = []
        for blk in range(3):
            teil = saaten[blk * 10:(blk + 1) * 10]
            for winkel, kam in ((0, "vorn"), (35, "¾")):
                zeilen.append({"titel": ("extrem" if teil[0][1] == "E" else "zufällig") + f" {kam}",
                               "zellen": [{"titel": f"{s}", "saat": s, "winkel": winkel} for s, _ in teil]})
        bogen = {"titel": f"P14 Extreme – {'Männer' if g == 'm' else 'Frauen'} 35: 20 extremste nach K (Zeilen 1–4), 10 zufällige (5–6), L-S",
                 "pruefplan": "P14", "ab": "B", "zelle": [300, 360], "name": f"p14_extreme_{g}",
                 "alle": {"ansicht": "kopf", "hoehe": 0, "spanne_m": 0.34, "ziel_m": -0.02, "kopf": "standard", "licht": "spiel",
                          "alter": 35, "geschlecht": g, "haar": haar, "warten": 45, **({"gene": {"bartwuchs": 130}} if g == "m" else {})},
                 "spalten": [{"titel": ""}] * 10, "zeilen": zeilen}
        with open(os.path.join(HIER, f"p14_extreme_{g}.json"), "w", encoding="utf-8") as f:
            json.dump(bogen, f, indent=1, ensure_ascii=False)
            f.write("\n")
        print("geschrieben", f"p14_extreme_{g}.json", len(saaten))


if __name__ == "__main__":
    main()
