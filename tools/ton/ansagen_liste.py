#!/usr/bin/env python3
"""Gibt die Satzliste der Haushofmeister-Ansagen (game/daten/ansagen.json) für die Spracherzeugung aus.

Der Teamleiter erzeugt die Aufnahmen auf dem Grafik-PC (Qwen3-TTS, Stimmklon). Dieses Skript ruft den Dienst NICHT selbst auf; es liefert nur die
Liste: je Zeile ein Satz mit Zieldatei. Die Datei liegt danach unter game/assets/audio/stimmen/haushofmeister/ (Protokollzeile in
docs/assets/protokoll.csv nicht vergessen).

Aufruf:
  python3 tools/ton/ansagen_liste.py                  # CSV auf die Standardausgabe
  python3 tools/ton/ansagen_liste.py --json           # JSON (Liste von Objekten)
  python3 tools/ton/ansagen_liste.py --fassung herrin # nur eine Fassung (herr|herrin|beide, Standard beide)
  python3 tools/ton/ansagen_liste.py --fehlende       # nur Sätze, deren Datei noch nicht existiert
  python3 tools/ton/ansagen_liste.py -o liste.csv     # in eine Datei schreiben
  python3 tools/ton/ansagen_liste.py --pruefen        # Satzliste prüfen (Schema der Dateinamen, Länge, doppelte Dateien), Rückgabe 1 bei Fehlern

Spalten: ereignis, nr, fassung (herr|herrin), prioritaet, text, datei (relativ zum Repository-Hauptordner), ziel_ogg_name.
"""
import argparse
import csv
import json
import sys
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
DATEI = WURZEL / "game" / "daten" / "ansagen.json"
PRAEFIX = "res://"
MAX_WOERTER = 14  # etwa 4 Sekunden gesprochen


def laden():
    with open(DATEI, encoding="utf-8") as f:
        return json.load(f)


def zeilen(daten):
    ordner = daten["ordner"]
    if not ordner.startswith(PRAEFIX):
        sys.exit(f"ansagen.json: 'ordner' muss mit {PRAEFIX} beginnen, ist aber {ordner!r}")
    relativ = Path("game") / ordner[len(PRAEFIX):]
    for e in daten["ereignisse"]:
        for v in e["varianten"]:
            for fassung, text, datei in (("herr", v["herr"], v["datei_herr"]), ("herrin", v["herrin"], v["datei_herrin"])):
                yield {
                    "ereignis": e["id"], "nr": v["nr"], "fassung": fassung, "prioritaet": e["prioritaet"],
                    "text": text, "datei": str(relativ / datei), "ziel_ogg_name": datei,
                }


def pruefen(daten):
    fehler = []
    gesehen = set()
    for z in zeilen(daten):
        schema = f"{z['ereignis']}_{z['nr']}{'_herrin' if z['fassung'] == 'herrin' else ''}.ogg"
        if z["ziel_ogg_name"] != schema:
            fehler.append(f"{z['ziel_ogg_name']}: erwartet {schema}")
        if z["ziel_ogg_name"] in gesehen:
            fehler.append(f"{z['ziel_ogg_name']}: doppelt")
        gesehen.add(z["ziel_ogg_name"])
        if len(z["text"].split()) > MAX_WOERTER:
            fehler.append(f"{z['ziel_ogg_name']}: mehr als {MAX_WOERTER} Wörter (über 4 s)")
        anrede = "Mein Herr, " if z["fassung"] == "herr" else "Meine Herrin, "
        if not z["text"].startswith(anrede):
            fehler.append(f"{z['ziel_ogg_name']}: Anrede {anrede!r} fehlt")
    return fehler


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--json", action="store_true", help="JSON statt CSV")
    p.add_argument("--fassung", choices=("herr", "herrin", "beide"), default="beide")
    p.add_argument("--fehlende", action="store_true", help="nur Sätze, deren Aufnahme noch fehlt")
    p.add_argument("--pruefen", action="store_true", help="Satzliste prüfen")
    p.add_argument("-o", "--ausgabe", help="Zieldatei statt Standardausgabe")
    a = p.parse_args()

    daten = laden()
    if a.pruefen:
        fehler = pruefen(daten)
        for f in fehler:
            print("FEHLER", f, file=sys.stderr)
        n = sum(1 for _ in zeilen(daten))
        print(f"{len(daten['ereignisse'])} Ereignisse, {n} Aufnahmen, {len(fehler)} Fehler")
        return 1 if fehler else 0

    ausgewaehlt = [z for z in zeilen(daten)
                   if (a.fassung in ("beide", z["fassung"])) and (not a.fehlende or not (WURZEL / z["datei"]).exists())]
    ziel = open(a.ausgabe, "w", encoding="utf-8", newline="") if a.ausgabe else sys.stdout
    try:
        if a.json:
            json.dump(ausgewaehlt, ziel, ensure_ascii=False, indent=2)
            ziel.write("\n")
        else:
            w = csv.DictWriter(ziel, fieldnames=["ereignis", "nr", "fassung", "prioritaet", "text", "datei", "ziel_ogg_name"])
            w.writeheader()
            w.writerows(ausgewaehlt)
    finally:
        if a.ausgabe:
            ziel.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
