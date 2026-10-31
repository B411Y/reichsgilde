"""Abnahme S5: die Genrechnung aus regeln6.json in Python nachrechnen und mit dem Gewichteausdruck von Godot vergleichen
(Charakter Phase 6, Etappe B). Verglichen werden die Gen- und Zugformen (ohne Asymmetrie: deren Zufall kommt aus Godots
RandomNumberGenerator); Formen, die das Budget gekürzt hat, stehen getrennt.

Aufruf: python3 -I tools/charakter/messen/regeln6_nachrechnen.py <gewichte.json>
"""
import json
import math
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
SPIEL = os.path.join(HIER, "..", "..", "..", "game")


def kurve(k, j):
    if j <= k[0][0]:
        return k[0][1]
    for a, b in zip(k, k[1:]):
        if j <= b[0]:
            return a[1] + (b[1] - a[1]) * (j - a[0]) / (b[0] - a[0])
    return k[-1][1]


def main():
    R = json.load(open(os.path.join(SPIEL, "versuch", "charakter", "regeln6.json"), encoding="utf-8"))
    E = {g["kennung"]: g for g in json.load(open(os.path.join(SPIEL, "versuch", "charakter", "erbgut_vorschlag.json"), encoding="utf-8"))["gene"]}
    d = json.load(open(sys.argv[1], encoding="utf-8"))
    groesst, n, n_budget, schlecht = 0.0, 0, 0, []
    for p in d["personen"]:
        if p.get("stand", 6) < 6:
            continue
        mann, alter, z = p["geschlecht"], p["alter"], p["gene_z"]
        ku = R["alterskurven"]
        ro = kurve(ku["reife_oben_m"], alter) * mann + kurve(ku["reife_oben_w"], alter) * (1 - mann)
        ru = kurve(ku["reife_unten_m"], alter) * mann + kurve(ku["reife_unten_w"], alter) * (1 - mann)
        gekuerzt = set(p.get("karikatur", {}).get("budget_gekuerzt", {}))
        mf = p["mimikfaktor"]
        erwartet = {}
        for k, e in R["gene"].items():
            if e.get("absolut"):
                continue
            if "aus_gen" in e:
                g = E[e["aus_gen"]]["voelker"]["Mensch"]
                zk = (p["gene"].get(e["aus_gen"], g["mittel"]) - g["mittel"]) / g["streuung"]
            else:
                zk = z.get(k, 0.0)
            richt = "plus" if zk >= 0 else "minus"
            s = e["staerke"]["m"][richt] * mann + e["staerke"]["w"][richt] * (1 - mann)
            reife = {"oben": ro, "unten": ru}.get(e.get("reife", ""), 1.0)
            erwartet[e["formen"][richt]] = abs(zk) * s * reife * mf
        for k, form in R["zuege"]["gene"].items():
            erwartet[form] = z.get(k, 0.0) * R["zuege"]["faktor"] * ru * mf
        for form, w in erwartet.items():
            if form in gekuerzt:
                n_budget += 1
                continue
            a = abs(p["gewichte"].get(form, 0.0) - w)
            n += 1
            if a > groesst:
                groesst = a
            if a > 1e-5:
                schlecht.append((p["name"], form, w, p["gewichte"].get(form, 0.0)))
    print(json.dumps({"verglichen": n, "budget_gekuerzt_ausgelassen": n_budget, "groesste_abweichung": groesst,
                      "ueber_1e-5": len(schlecht), "beispiele": schlecht[:5]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
