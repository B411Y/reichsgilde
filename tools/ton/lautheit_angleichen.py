#!/usr/bin/env python3
"""Berechnet die Lautheits-Angleichung der Oberflaechenklaenge (Ton Schritt 7) aus den Messwerten. Die Audiodateien bleiben unveraendert.

Eingabe:  docs/assets/lautheit-messwerte.csv (tools/ton/lautheit_messen.py) und tools/ton/lautheit_gruppen.json (Ziele je Ereignis)
Ausgabe:  game/daten/klang_lautheit.json  {"angleichung_db": {Datei: dB}}   -- das Spiel setzt die Zahl als Lautstaerke der Variante
                                                                               im AudioStreamRandomizer (KlangOberflaeche.cs)
          docs/assets/lautheit-angleichung.csv  (je Datei vorher, Angleichung, nachher, Spitze danach)
Aufruf (nur Standardbibliothek): python -I tools/ton/lautheit_angleichen.py
Angleichung = Ziel - momentan_max der Datei (auf 0,1 dB gerundet). Warnung, wenn die Spitze danach (ohne die Pegel-Spalte der Tabelle,
die immer negativ ist) ueber 0 dBTP laege."""
import csv
import json
import sys
from pathlib import Path


def main():
    gruppen = json.loads(Path("tools/ton/lautheit_gruppen.json").read_text(encoding="utf-8"))["gruppen"]
    zeilen = {Path(r["datei"]).stem: r for r in csv.DictReader(open("docs/assets/lautheit-messwerte.csv", encoding="utf-8"))
              if r["klasse"] == "Oberflaeche"}
    ergebnis, tabelle = {}, []
    for name, g in gruppen.items():
        for datei in g["dateien"]:
            z = zeilen[datei]
            m, tp = float(z["momentan_max"]), float(z["true_peak_dbtp"])
            off = round(g["ziel"] - m, 1)
            ergebnis[datei] = off
            if tp + off > 0:
                print(f"Warnung: {datei} Spitze danach {tp + off:+.1f} dBTP (vor der Pegel-Spalte der Tabelle)")
            tabelle.append((name, datei, m, off, m + off, tp + off))
    Path("game/daten/klang_lautheit.json").write_text(
        json.dumps({"angleichung_db": dict(sorted(ergebnis.items()))}, indent=2) + "\n", encoding="utf-8")
    with open("docs/assets/lautheit-angleichung.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["ereignis", "datei", "momentan_max_vorher", "angleichung_db", "momentan_max_nachher", "true_peak_nachher"])
        for t in tabelle:
            w.writerow([t[0], t[1], f"{t[2]:.1f}", f"{t[3]:+.1f}", f"{t[4]:.1f}", f"{t[5]:.1f}"])
    print(f"{len(ergebnis)} Dateien angeglichen")


if __name__ == "__main__":
    sys.exit(main())
