"""Dreiecke je Bauteil der Patrizierhaeuser Zwerg und Elb (Stufe 3), Grundform 1-4: liest die Blender-Logs der Bauskripte
(Zeilen "DREIECKE <Bauteil>: <Zahl>" und "DREIECKE aussen: {...}") und schreibt eine Markdown-Tabelle.

    python tools/modelle/neubau/dreiecke_tabelle.py <ordner-mit-den-logs> <ausgabe.md>

Die Logs heissen z3_g<n>.log (Zwerg S3), e3_g<n>.log (Elb S3), z2_g<n>.log, z1_g<n>.log (Zwerg S2/S1). Grundlage fuer eine spaetere Optimierung;
die Grenze 8.000 gilt hier vorerst nicht (Nutzer 23.11.2026). Zahlen sind Dreiecke der Huelle ohne Wappen, Fahne, Leerstand, Geruest."""
import ast
import os
import re
import sys

GRUPPEN = (("z3", "Zwerg Stufe 3 (Patrizier)"), ("e3", "Elb Stufe 3 (Patrizier)"), ("z2", "Zwerg Stufe 2"), ("z1", "Zwerg Stufe 1"))


def lese(pfad):
    teile, aussen, zaehler = {}, {}, {}
    for z in open(pfad, encoding="utf-8", errors="replace").read().replace("\r", "\n").splitlines():
        m = re.match(r"DREIECKE aussen: (\{.*\}) Summe (\d+)", z)
        if m:
            aussen = ast.literal_eval(m.group(1)); aussen["Summe"] = int(m.group(2)); continue
        m = re.match(r"ZAEHLER (\S+) (\d+) (\d+)$", z.strip())
        if m: zaehler[m.group(1)] = int(m.group(2)); continue
        m = re.match(r"DREIECKE ([^:{]+): (\d+)$", z.strip())
        if m: teile[m.group(1).strip()] = int(m.group(2))
    if zaehler: teile = zaehler
    return teile, aussen


def main(ordner, ziel):
    zeilen = ["# Dreiecke je Bauteil: Zwerg- und Elbenhaus (Stand Patrizier-Entwurf)", "",
              "Erzeugt von `tools/modelle/neubau/dreiecke_tabelle.py` aus den Logs der Bauskripte. Dreiecke der Aussenhuelle (Haus, Dach, Scheiben); Wappen, Fahne, "
              "Leerstand und Geruest zaehlen nicht. Die Grenze 8.000 gilt hier vorerst nicht (Nutzer 23.11.2026); die Tabelle ist die Grundlage fuer eine "
              "spaetere Optimierung. Bauteile sind die Bauteilfunktionen der Bauskripte (Messung mit REICHSGILDE_ZAEHLEN=1, `dreiecke_zaehler.py`; ausschliesslich gezaehlt: Dreiecke, "
              "die untergeordnete Funktionen erzeugen, stehen dort); der Rest steht unter „Haus ohne die einzeln gezaehlten Teile“ (z. B. Dachfunktionen, die nicht ueber `h` laufen).", ""]
    for kenn, titel in GRUPPEN:
        daten = {}
        for g in (1, 2, 3, 4):
            p = os.path.join(ordner, f"{kenn}_g{g}.log")
            if os.path.exists(p): daten[g] = lese(p)
        if not daten: continue
        namen = []
        for g in daten:
            for n in daten[g][0]:
                if n not in namen: namen.append(n)
        zeilen += [f"## {titel}", "", "| Bauteil | G1 | G2 | G3 | G4 |", "|---|---:|---:|---:|---:|"]
        for n in namen:
            zeilen.append(f"| {n} | " + " | ".join(str(daten[g][0].get(n, "")) if g in daten else "" for g in (1, 2, 3, 4)) + " |")
        rest = {g: daten[g][1].get("Haus", 0) - sum(daten[g][0].values()) for g in daten}
        zeilen.append("| Haus ohne die einzeln gezaehlten Teile | " + " | ".join(str(rest.get(g, "")) for g in (1, 2, 3, 4)) + " |")
        for k in ("Haus", "Dach", "Scheiben", "Summe"):
            zeilen.append(f"| **{k}** | " + " | ".join(str(daten[g][1].get(k, "")) if g in daten else "" for g in (1, 2, 3, 4)) + " |")
        zeilen.append("")
    open(ziel, "w", encoding="utf-8").write("\n".join(zeilen) + "\n")
    print("geschrieben", ziel)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
