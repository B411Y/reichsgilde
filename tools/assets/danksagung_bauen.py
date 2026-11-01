#!/usr/bin/env python3
"""Baut game/daten/danksagung.json (die Danksagung im Spiel) und prüft sie gegen das Asset-Protokoll.

Aufruf (aus einem beliebigen Ordner im Repository):
  python3 tools/assets/danksagung_bauen.py             # game/daten/danksagung.json neu schreiben
  python3 tools/assets/danksagung_bauen.py --pruefen   # nichts schreiben; Rückgabewert 1, wenn etwas fehlt (so läuft es in der CI)

Quellen (nur lesen):
  - tools/assets/danksagung_quelle.json   handgepflegt: Gruppen, Texte, Bibliotheken, Pakete, Schriften
  - game/assets/audio/umgebung/HERKUNFT.md   Tabelle der Freesound-Aufnahmen (Nummer, Titel, Urheber, Lizenz, Adresse)
  - docs/assets/protokoll.csv   welche Dateien es gibt und woher sie stammen

Geprüft wird (Pflicht aus den Lizenzen: Namensnennung bei CC BY, MIT, OFL, Apache):
  1. Jede Protokollzeile mit Herkunft „Fremdpaket“ ist von einem Eintrag (oder einer Gruppe) der Quelle gedeckt
     (Anfang des Musters in „deckt“) – oder liegt unter game/assets/audio/umgebung/ (dann zählt Punkt 2).
  2. Jede Aufnahme der Freesound-Tabelle steht in der Danksagung: CC BY mit Werk, Urheber, Adresse und Lizenz einzeln,
     CC0 als Dank an den Urheber (Sammelzeile).
  3. Jeder in einer Protokoll-Bemerkung mit „Namensnennung: <Name> (freesound.org/…/sounds/<Nr>/)“ genannte Urheber
     steht in der Tabelle der Quelle und damit in der Danksagung.
  4. Jeder Pflicht-Eintrag hat Werk, Urheber, Adresse und Lizenz.
  5. game/daten/danksagung.json entspricht dem, was aus den Quellen entsteht (sonst neu bauen).

Nur Python-Standardbibliothek; Ausgabe deutsch.
"""
import csv
import json
import re
import sys
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
QUELLE = WURZEL / "tools/assets/danksagung_quelle.json"
HERKUNFT_UMGEBUNG = WURZEL / "game/assets/audio/umgebung/HERKUNFT.md"
PROTOKOLL = WURZEL / "docs/assets/protokoll.csv"
AUSGABE = WURZEL / "game/daten/danksagung.json"
UMGEBUNG = "game/assets/audio/umgebung/"
PFLICHT_LIZENZEN = ("CC BY", "MIT", "OFL", "Apache")


def freesound_tabelle(fehler):
    """Zeilen der Quellentabelle: (nummer, titel, urheber, lizenz, adresse)."""
    zeilen = []
    for zeile in HERKUNFT_UMGEBUNG.read_text(encoding="utf-8").splitlines():
        teile = [t.strip() for t in zeile.strip().strip("|").split("|")]
        if len(teile) >= 5 and teile[0].isdigit() and teile[4].startswith("https://freesound.org/"):
            zeilen.append((teile[0], teile[1], teile[2], teile[3], teile[4]))
    if not zeilen:
        fehler.append(f"{HERKUNFT_UMGEBUNG.relative_to(WURZEL)}: keine Tabellenzeilen der Freesound-Aufnahmen gefunden")
    return zeilen


def bauen(fehler):
    quelle = json.loads(QUELLE.read_text(encoding="utf-8"))
    tabelle = freesound_tabelle(fehler)
    gruppen = []
    for g in quelle["gruppen"]:
        eintraege = [{k: e[k] for k in ("werk", "autor", "quelle", "lizenz", "hinweis", "pflicht")} for e in g["eintraege"]]
        dank = g.get("dank", "")
        aufnahmen = []
        if g["name"] == "Klänge":
            urheber0 = []
            for nr, titel, urheber, lizenz, adresse in tabelle:
                if lizenz.startswith("CC0"):
                    if urheber not in urheber0:
                        urheber0.append(urheber)
                else:
                    aufnahmen.append({"werk": re.sub(r"\.(wav|WAV|ogg|flac)$", "", titel), "autor": urheber, "quelle": adresse, "lizenz": lizenz,
                                      "hinweis": "Freesound.org, zugeschnitten und gefiltert", "pflicht": True})
            urheber0.sort(key=str.lower)
            sammel = ("Dank an die Urheber der freien (CC0) Aufnahmen auf Freesound.org: " + ", ".join(urheber0) +
                      ".")
            dank = (sammel + " " + dank).strip()
        gruppen.append({"name": g["name"], "text": g["text"], "eintraege": eintraege + aufnahmen, "dank": dank})
    return quelle, tabelle, {"version": 1, "gruppen": gruppen}


def pruefen_gegen_protokoll(quelle, tabelle, fehler):
    praefixe = []
    for g in quelle["gruppen"]:
        praefixe += g.get("deckt", [])
        for e in g["eintraege"]:
            praefixe += e.get("deckt", [])
            if e.get("pflicht"):
                for feld in ("werk", "autor", "quelle", "lizenz"):
                    if not e.get(feld):
                        fehler.append(f"Pflicht-Eintrag „{e.get('werk', '?')}“: Feld „{feld}“ fehlt")
    with PROTOKOLL.open(encoding="utf-8", newline="") as f:
        zeilen = list(csv.DictReader(f))
    for z in zeilen:
        muster = z["pfad_muster"]
        if z["herkunft"] == "Fremdpaket" and not muster.startswith(UMGEBUNG) and not any(muster.startswith(p) for p in praefixe):
            fehler.append(f"Protokoll: Fremdmaterial „{muster}“ ({z['lizenz_ergebnis']}) steht nicht in der Danksagung – "
                          f"Eintrag in tools/assets/danksagung_quelle.json ergänzen (Feld „deckt“)")
        if muster.startswith(UMGEBUNG):
            for name, nr in re.findall(r"Namensnennung: ([\w.\-]+) \(freesound\.org/people/[^/]+/sounds/(\d+)/?\)", z["bemerkung"]):
                if not any(t[0] == nr and t[2] == name for t in tabelle):
                    fehler.append(f"Protokoll „{muster}“ nennt {name} (Aufnahme {nr}); in der Tabelle von "
                                  f"{HERKUNFT_UMGEBUNG.relative_to(WURZEL)} fehlt sie – nicht in der Danksagung")
    # Jede CC-BY-Aufnahme des Protokolls (Lizenzspalte) braucht mindestens eine CC-BY-Zeile in der Tabelle
    if any(z["pfad_muster"].startswith(UMGEBUNG) and "CC BY" in z["lizenz_ergebnis"] for z in zeilen) \
            and not any(t[3].startswith("CC BY") for t in tabelle):
        fehler.append("Protokoll nennt CC-BY-Klänge, die Tabelle der Quellen enthält keine")


def main():
    nur_pruefen = "--pruefen" in sys.argv[1:]
    fehler = []
    quelle, tabelle, daten = bauen(fehler)
    pruefen_gegen_protokoll(quelle, tabelle, fehler)
    text = json.dumps(daten, ensure_ascii=False, indent=2) + "\n"
    if nur_pruefen:
        if not AUSGABE.exists() or AUSGABE.read_text(encoding="utf-8") != text:
            fehler.append(f"{AUSGABE.relative_to(WURZEL)} ist nicht aktuell – `python3 tools/assets/danksagung_bauen.py` ausführen")
    elif not fehler:
        AUSGABE.write_text(text, encoding="utf-8")
        eintraege = sum(len(g["eintraege"]) for g in daten["gruppen"])
        print(f"{AUSGABE.relative_to(WURZEL)} geschrieben: {len(daten['gruppen'])} Gruppen, {eintraege} Einträge mit Namensnennung.")
    if fehler:
        print("Danksagung: Fehler")
        for f in fehler:
            print("  -", f)
        return 1
    if nur_pruefen:
        print("Danksagung: in Ordnung")
    return 0


if __name__ == "__main__":
    sys.exit(main())
