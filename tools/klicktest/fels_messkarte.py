"""Messkarte für die Leistung der Felsen (Felsen R6, vorbereitet in R7): nimmt eine Karte aus game/karten/ (Vorgabe koeln) und setzt
einen dichten Block aus Stücken des Felswand-Satzes an eine freie Stelle im Umland (weit weg von Gebäuden und Wegen).

    python tools/klicktest/fels_messkarte.py <ziel.json> [--karte=koeln] [--anzahl=48] [--gestein=grauwacke]

Schreibt die Karte nach <ziel.json> (für das Spiel unter user://karten/, Start mit --karte=<name>.json) und gibt eine Zeile
„MITTE <x> <z>“ aus: dort steht der Block, dorthin richtet tools/klicktest/fels_messung.ps1 die Kamera. Die Stücke stehen in
Reihen zu 12 (Abstand 12 m, Reihen 20 m), die Formen wechseln reihum (alle elf), jede zweite Reihe zeigt der Spielkamera den
Rücken (geschlossene Rückseiten, R7). Nur Python-Standardbibliothek, gleiche Eingabe gleiche Karte (fester Zufall).
"""
import json
import math
import os
import random
import sys

WURZEL = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FORMEN = ("wand_gross_1", "wand_mittel_1", "pfeiler_1", "wand_gross_2", "gesims_1", "wand_mittel_2", "schuttkegel",
          "wand_gross_3", "pfeilerbuendel", "wand_mittel_3", "gesims_2")
REIHE, ABSTAND_X, ABSTAND_Z = 12, 12.0, 20.0


def argumente():
    a = sys.argv[1:]
    wert = lambda n, d: next((x.split("=", 1)[1] for x in a if x.startswith(f"--{n}=")), d)  # noqa: E731
    ziel = next((x for x in a if not x.startswith("--")), None)
    if not ziel:
        print(__doc__)
        sys.exit(2)
    return ziel, wert("karte", "koeln"), int(wert("anzahl", "48")), wert("gestein", "grauwacke")


def punkte_der_wege(karte):
    """Punkte entlang aller Wege (alle 4 m), dazu die Mittelpunkte der Gebäude."""
    wege = []
    for w in karte.get("wege", []):
        linie = w.get("linie") or []
        for (x0, z0), (x1, z1) in zip(linie, linie[1:]):
            n = max(1, int(math.hypot(x1 - x0, z1 - z0) / 4))
            wege += [(x0 + (x1 - x0) * i / n, z0 + (z1 - z0) * i / n) for i in range(n + 1)]
    bauten = [tuple(g["punkt"]) for g in karte.get("gebaeude", []) if "punkt" in g]
    bauten += [tuple(b["punkt"]) for b in karte.get("bauzonen", []) if "punkt" in b]
    return wege, bauten


def freie_stelle(karte, breite, tiefe):
    """Die Stelle (Mitte des Blocks), die am weitesten von Gebäuden und Wegen weg liegt, mit dem Block ganz in der Karte."""
    (gx, gz), (mx, mz) = karte["groesse"], karte["mitte"]
    wege, bauten = punkte_der_wege(karte)
    rand = 30.0
    x0, x1 = mx - gx / 2 + breite / 2 + rand, mx + gx / 2 - breite / 2 - rand
    z0, z1 = mz - gz / 2 + tiefe / 2 + rand, mz + gz / 2 - tiefe / 2 - rand
    beste, wert = (mx, mz), -1.0
    schritt = 8.0
    x = x0
    while x <= x1:
        z = z0
        while z <= z1:
            ecken = [(x + dx, z + dz) for dx in (-breite / 2, 0, breite / 2) for dz in (-tiefe / 2, 0, tiefe / 2)]
            db = min((math.hypot(px - bx, pz - bz) for px, pz in ecken for bx, bz in bauten), default=1e9)
            dw = min((math.hypot(px - wx, pz - wz) for px, pz in ecken for wx, wz in wege), default=1e9)
            w = min(db, 2 * dw)
            if w > wert:
                beste, wert = (x, z), w
            z += schritt
        x += schritt
    return beste, wert


def main():
    ziel, kennung, anzahl, gestein = argumente()
    with open(os.path.join(WURZEL, "game", "karten", f"{kennung}.json"), encoding="utf-8") as f:
        karte = json.load(f)
    reihen = math.ceil(anzahl / REIHE)
    breite, tiefe = REIHE * ABSTAND_X + 10, reihen * ABSTAND_Z + 10
    (cx, cz), frei = freie_stelle(karte, breite, tiefe)
    zufall = random.Random(7)
    stuecke = []
    for i in range(anzahl):
        reihe, spalte = divmod(i, REIHE)
        x = cx - (REIHE - 1) * ABSTAND_X / 2 + spalte * ABSTAND_X + zufall.uniform(-1.5, 1.5)
        z = cz - (reihen - 1) * ABSTAND_Z / 2 + reihe * ABSTAND_Z + zufall.uniform(-1.5, 1.5)
        drehung = (180.0 if reihe % 2 else 0.0) + zufall.uniform(-12, 12)
        stuecke.append({"form": FORMEN[i % len(FORMEN)], "punkt": [round(x, 2), round(z, 2)],
                        "drehung": round(drehung % 360, 1), "massstab": 1.0, "neigung": 0.0, "einsinken": 0.1})
    # die Kennung bleibt die der Vorlage: so steht auch ihre Kulisse (berge.json) dahinter, wie im Spiel
    karte["felsen"] = {"grundgestein": gestein, "stuecke": stuecke}
    os.makedirs(os.path.dirname(os.path.abspath(ziel)), exist_ok=True)
    with open(ziel, "w", encoding="utf-8", newline="\n") as f:
        json.dump(karte, f, ensure_ascii=False, indent=1)
    print(f"{anzahl} Stücke in {reihen} Reihen, Abstand zu Gebäuden/Wegen mindestens {frei:.0f} m")
    print(f"MITTE {cx:.0f} {cz:.0f}")


if __name__ == "__main__":
    main()
