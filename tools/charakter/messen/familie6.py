"""Familienprüfung (Charakter Phase 6, S7, Bogen P5 als Zahl): Abstand Kind–Elternteil im Verhältnis zum Abstand
Kind–Fremder, für die Allelregeln „sichtbar“ (ein sichtbares Allel, wie heute) und „mittel“ (Gestalt-Gene und Züge als
Mittel beider Allele). Maße σ-normiert je Geschlecht (Mittel und Streuung der Eltern-Stichprobe dieses Geschlechts), damit
Vater–Tochter und Mutter–Sohn vergleichbar sind. Erwartung (Plan): ≈ 0,87 (r ≈ 0,25) bzw. ≈ 0,71 (r ≈ 0,5).

Aufruf: $BPY -I tools/charakter/messen/familie6.py <glb> <gewichte_sichtbar.json> <gewichte_mittel.json>
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from messen.glb import Glb  # noqa: E402
from messen.netz_masse import Netzmesser  # noqa: E402

MASSE = ["se_me", "zy", "unterkiefer", "mundbreite", "n_sn", "nasenbreite", "lippenrot", "kinnvorsprung", "b_kinnmitte",
         "braue_pupille", "nasenvorsprung", "ipd"]


def main():
    nm = Netzmesser(Glb(sys.argv[1]))
    aus = {}
    for regel, datei in (("sichtbar", sys.argv[2]), ("mittel", sys.argv[3])):
        d = json.load(open(datei, encoding="utf-8"))
        werte, geschl = {}, {}
        for p in d["personen"]:
            P = nm.netz.punkte(p["szene"])
            m = nm.messen(p["szene"], punkte=P, schnell=True)
            m["lippenrot"] = float((P["lm51"][1] - P["lm57"][1]) - (P["lm62"][1] - P["lm66"][1]))
            m["kinnvorsprung"] = float(P["pg"][2] - P["sm"][2])
            werte[p["name"]] = np.array([m[k] for k in MASSE])
            geschl[p["name"]] = "m" if p["geschlecht"] > 0.5 else "w"
        eltern = [n for n in werte if n[0] in "vm"]
        stat = {}
        for g in "mw":
            a = np.array([werte[n] for n in eltern if geschl[n] == g])
            stat[g] = (a.mean(0), a.std(0))
        z = {n: (werte[n] - stat[geschl[n]][0]) / stat[geschl[n]][1] for n in werte}
        n_fam = sum(1 for n in werte if n.startswith("k"))
        rng = np.random.default_rng(55)
        d_el, d_fr = [], []
        for i in range(n_fam):
            k = z[f"k{i}"]
            for e in (f"v{i}", f"m{i}"):
                d_el.append(np.sqrt(((k - z[e]) ** 2).mean()))
                j = int(rng.integers(n_fam - 1))
                j = j + 1 if j >= i else j
                d_fr.append(np.sqrt(((k - z[f"{e[0]}{j}"]) ** 2).mean()))
        # Korrelation Elternmittel–Kind je Maß (Erblichkeit sichtbar)
        r = [float(np.corrcoef([0.5 * (z[f"v{i}"][j] + z[f"m{i}"][j]) for i in range(n_fam)], [z[f"k{i}"][j] for i in range(n_fam)])[0, 1])
             for j in range(len(MASSE))]
        aus[regel] = {"familien": n_fam, "abstand_kind_eltern": round(float(np.mean(d_el)), 3),
                      "abstand_kind_fremde": round(float(np.mean(d_fr)), 3),
                      "verhaeltnis": round(float(np.mean(d_el) / np.mean(d_fr)), 3),
                      "r_elternmittel_kind_median": round(float(np.median(r)), 3)}
    print(json.dumps(aus, ensure_ascii=False))
    return aus


if __name__ == "__main__":
    main()
