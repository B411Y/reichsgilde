#!/usr/bin/env python3
"""Prüft das Asset-Protokoll docs/assets/protokoll.csv und erzeugt die Steam-Liste der KI-Inhalte.

Aufruf (aus einem beliebigen Ordner im Repository):
  python3 tools/assets/protokoll_pruefen.py                 # prüfen (so läuft es in der CI)
  python3 tools/assets/protokoll_pruefen.py --selbsttest    # Stichtag-Regel an erfundenen Zeilen testen
  python3 tools/assets/protokoll_pruefen.py --steam-liste   # docs/assets/steam-ki-inhalte.md neu schreiben, dann prüfen

Geprüft wird:
  - Jede Datei unter den Prüfordnern (game/assets/ und den KI-Quellen unter tools/) ist von einer Zeile erfasst.
    Ausgenommen: .import, .uid, .gitkeep, HERKUNFT.md.
  - Jedes Muster trifft mindestens eine Datei, und „anzahl“ stimmt mit den Treffern überein.
  - Pflichtfelder sind nicht leer („unbekannt“ ist erlaubt), Art, Herkunft, Datum und „spieler_sieht“ haben gültige Werte.
  - Verweise auf Prompt-Dateien (docs/assets/prompts/…) zeigen auf vorhandene Dateien.
  - Zeilen mit „datum“ nach dem Stichtag (Konstante STICHTAG, None = aus) tragen kein „CC BY“ als Lizenz.
  - Die Steam-Liste entspricht dem Protokoll (sonst mit --steam-liste neu schreiben).

Muster (relativ zum Repository): * = beliebig viele Zeichen ohne „/“, ** = beliebig viele Ordner, ? = ein Zeichen,
{a,b} = a oder b. Trifft eine Datei mehrere Zeilen, gilt die **letzte** (wie bei CODEOWNERS) – so kann eine Einzelzeile
unter einer Gruppenzeile einen Sonderfall abweichend beschreiben. Gezählt wird je Zeile nur, was sie am Ende erfasst.

Nur Python-Standardbibliothek; Ausgabe deutsch, Rückgabewert 1 bei Fehlern.
"""
import csv
import os
import re
import subprocess
import sys
from pathlib import Path

WURZEL = Path(__file__).resolve().parents[2]
PROTOKOLL = WURZEL / "docs/assets/protokoll.csv"
STEAM_LISTE = WURZEL / "docs/assets/steam-ki-inhalte.md"

SPALTEN = ["pfad_muster", "anzahl", "art", "herkunft", "werkzeug", "modell_version", "prompt_oder_quelle", "datum",
           "commit_pr", "nachbearbeitung", "bedingungen_werkzeug", "lizenz_ergebnis", "spieler_sieht", "bemerkung"]
FREIWILLIG = {"bemerkung"}
ARTEN = {"Bild", "Modell", "Textur", "Shader", "Schrift", "Audio", "Text"}
HERKUENFTE = {"KI", "Nutzer", "gemischt", "Fremdpaket", "eigenes Skript", "unbekannt"}
KI_HERKUENFTE = {"KI", "gemischt"}

# Hier muss jede Datei einen Eintrag haben. Die KI-Quellen unter tools/ stehen im Repository und unter CC BY 4.0,
# auch wenn das Spiel sie nicht ausliefert.
PRUEFORDNER = ("game/assets/", "tools/icons/quellen/", "tools/portraets/quellen/")
# Stichtag der Lizenzänderung (Entscheidung 29.10.2026, LICENSE-INHALTE.md): Inhalte mit späterem Datum stehen unter
# „Alle Rechte vorbehalten“, nicht unter CC BY. Der Teamleiter trägt nach dem Merge das Merge-Datum "JJJJ-MM-TT" ein;
# bis dahin None = Prüfung aus.
STICHTAG = None

AUSGENOMMEN_ENDUNG = (".import", ".uid")
AUSGENOMMEN_NAME = {".gitkeep", "HERKUNFT.md"}


def dateien_im_repository():
    """Alle Dateien, die Git kennt oder kennen würde (eingecheckt und neu, ohne .gitignore), die es auch gibt."""
    try:
        ausgabe = subprocess.run(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
                                 cwd=WURZEL, capture_output=True, check=True).stdout.decode("utf-8")
        pfade = [p for p in ausgabe.split("\0") if p]
    except (OSError, subprocess.CalledProcessError):
        # Ohne Git (z. B. entpacktes Archiv): Ordner durchgehen.
        pfade = []
        for ordner in PRUEFORDNER:
            for wurzel, _, namen in os.walk(WURZEL / ordner):
                pfade += [(Path(wurzel) / n).relative_to(WURZEL).as_posix() for n in namen]
    return sorted({p for p in pfade if (WURZEL / p).is_file() and not ausgenommen(p)})


def ausgenommen(pfad):
    name = pfad.rsplit("/", 1)[-1]
    return name in AUSGENOMMEN_NAME or name.endswith(AUSGENOMMEN_ENDUNG)


def klammern_aufloesen(muster):
    """{a,b} -> [a, b] (auch mehrfach, nicht verschachtelt)."""
    treffer = re.search(r"\{([^{}]*)\}", muster)
    if not treffer:
        return [muster]
    ergebnis = []
    for teil in treffer.group(1).split(","):
        ergebnis += klammern_aufloesen(muster[:treffer.start()] + teil + muster[treffer.end():])
    return ergebnis


def als_regex(muster):
    teile = []
    i = 0
    while i < len(muster):
        if muster.startswith("**/", i):
            teile.append("(?:.*/)?")
            i += 3
        elif muster.startswith("**", i):
            teile.append(".*")
            i += 2
        elif muster[i] == "*":
            teile.append("[^/]*")
            i += 1
        elif muster[i] == "?":
            teile.append("[^/]")
            i += 1
        elif muster[i] == "[":
            ende = muster.find("]", i + 1)
            if ende < 0:
                teile.append(re.escape("["))
                i += 1
            else:
                inhalt = muster[i + 1:ende]
                if inhalt.startswith("!"):
                    inhalt = "^" + inhalt[1:]
                teile.append("[" + inhalt.replace("\\", "\\\\") + "]")
                i = ende + 1
        else:
            teile.append(re.escape(muster[i]))
            i += 1
    return re.compile("".join(teile) + r"\Z")


def protokoll_lesen(fehler):
    if not PROTOKOLL.is_file():
        fehler.append(f"{PROTOKOLL.relative_to(WURZEL)} fehlt")
        return []
    with open(PROTOKOLL, encoding="utf-8", newline="") as datei:
        leser = csv.reader(datei)
        kopf = next(leser, None)
        if kopf != SPALTEN:
            fehler.append(f"Kopfzeile stimmt nicht. Erwartet: {','.join(SPALTEN)}")
            return []
        zeilen = []
        for nummer, werte in enumerate(leser, start=2):
            if not any(w.strip() for w in werte):
                continue
            if len(werte) != len(SPALTEN):
                fehler.append(f"Zeile {nummer}: {len(werte)} Felder statt {len(SPALTEN)} (Komma im Text ohne Anführungszeichen?)")
                continue
            zeile = {s: w.strip() for s, w in zip(SPALTEN, werte)}
            zeile["_nummer"] = nummer
            zeilen.append(zeile)
        return zeilen


def felder_pruefen(zeile, fehler):
    ort = f"Zeile {zeile['_nummer']} ({zeile['pfad_muster'] or 'ohne Muster'})"
    for spalte in SPALTEN:
        if spalte not in FREIWILLIG and not zeile[spalte]:
            fehler.append(f"{ort}: Feld „{spalte}“ ist leer – unbelegt bitte „unbekannt“ eintragen")
    if zeile["anzahl"] and not zeile["anzahl"].isdigit():
        fehler.append(f"{ort}: „anzahl“ muss eine Zahl sein, steht dort: {zeile['anzahl']}")
    if zeile["art"] and zeile["art"] not in ARTEN:
        fehler.append(f"{ort}: unbekannte Art „{zeile['art']}“ (erlaubt: {', '.join(sorted(ARTEN))})")
    if zeile["herkunft"] and zeile["herkunft"] not in HERKUENFTE:
        fehler.append(f"{ort}: unbekannte Herkunft „{zeile['herkunft']}“ (erlaubt: {', '.join(sorted(HERKUENFTE))})")
    if zeile["spieler_sieht"] and zeile["spieler_sieht"] not in ("ja", "nein"):
        fehler.append(f"{ort}: „spieler_sieht“ muss ja oder nein sein")
    if zeile["datum"] and zeile["datum"] != "unbekannt" and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", zeile["datum"]):
        fehler.append(f"{ort}: Datum als JJJJ-MM-TT oder „unbekannt“, steht dort: {zeile['datum']}")
    if STICHTAG and re.fullmatch(r"\d{4}-\d{2}-\d{2}", zeile["datum"]) and zeile["datum"] > STICHTAG \
            and "CC BY" in zeile["lizenz_ergebnis"]:
        fehler.append(f"{ort}: Datum {zeile['datum']} liegt nach dem Stichtag {STICHTAG}; neue Inhalte tragen "
                      "„Alle Rechte vorbehalten“ statt CC BY (Fremdmaterial: seine eigene Lizenz)")
    nb = zeile["nachbearbeitung"]
    if nb and not (nb.startswith("nein") or nb.startswith("ja:") or nb.startswith("unbekannt")):
        fehler.append(f"{ort}: „nachbearbeitung“ beginnt mit „nein“, „ja: <Umfang>“ oder „unbekannt“")
    if zeile["herkunft"] in KI_HERKUENFTE:
        for spalte in ("werkzeug", "modell_version", "bedingungen_werkzeug"):
            if zeile[spalte] == "entfällt":
                fehler.append(f"{ort}: Bei KI-Herkunft darf „{spalte}“ nicht „entfällt“ sein (unbelegt: „unbekannt“)")
    for verweis in re.findall(r"docs/assets/prompts/[^\s;,)]+", zeile["prompt_oder_quelle"]):
        if not (WURZEL / verweis).is_file():
            fehler.append(f"{ort}: Prompt-Datei {verweis} fehlt")


def zuordnen(zeilen, dateien, fehler):
    """Jede Datei bekommt die letzte passende Zeile. Gibt {Zeilennummer: [Dateien]} zurück."""
    regeln = []
    for zeile in zeilen:
        if zeile["pfad_muster"].startswith("/"):
            fehler.append(f"Zeile {zeile['_nummer']}: Muster ohne führenden „/“ schreiben (relativ zum Repository)")
        regeln.append((zeile, [als_regex(m) for m in klammern_aufloesen(zeile["pfad_muster"])]))
    erfasst = {zeile["_nummer"]: [] for zeile in zeilen}
    roh = {zeile["_nummer"]: 0 for zeile in zeilen}
    for pfad in dateien:
        gewinner = None
        for zeile, ausdruecke in regeln:
            if any(a.match(pfad) for a in ausdruecke):
                roh[zeile["_nummer"]] += 1
                gewinner = zeile
        if gewinner is not None:
            erfasst[gewinner["_nummer"]].append(pfad)
        elif pfad.startswith(PRUEFORDNER):
            fehler.append(f"Ohne Protokolleintrag: {pfad}")
    for zeile in zeilen:
        nummer = zeile["_nummer"]
        ort = f"Zeile {nummer} ({zeile['pfad_muster']})"
        if not erfasst[nummer]:
            if roh[nummer]:
                fehler.append(f"{ort}: alle {roh[nummer]} Treffer sind von späteren Zeilen überdeckt")
            else:
                fehler.append(f"{ort}: Muster trifft keine Datei")
        elif zeile["anzahl"].isdigit() and int(zeile["anzahl"]) != len(erfasst[nummer]):
            fehler.append(f"{ort}: „anzahl“ ist {zeile['anzahl']}, das Muster erfasst aber {len(erfasst[nummer])} Dateien")
    return erfasst


def tabellenzelle(text):
    return text.replace("|", "\\|").replace("\n", " ")


def steam_liste(zeilen):
    """Markdown-Liste aller KI-Inhalte aus dem Protokoll – ohne Datum, damit die Prüfung sie vergleichen kann."""
    ki = [z for z in zeilen if z["herkunft"] in KI_HERKUENFTE]
    sichtbar = [z for z in ki if z["spieler_sieht"] == "ja"]
    intern = [z for z in ki if z["spieler_sieht"] != "ja"]
    offen = [z for z in zeilen if z["herkunft"] == "unbekannt"]
    summe = {}
    for z in sichtbar:
        if z["anzahl"].isdigit():
            summe[z["art"]] = summe.get(z["art"], 0) + int(z["anzahl"])

    z = []
    z.append("# KI-Inhalte für die Steam-Angabe")
    z.append("")
    z.append("Erzeugt von `python3 tools/assets/protokoll_pruefen.py --steam-liste` aus `docs/assets/protokoll.csv` – "
             "nicht von Hand ändern, sondern das Protokoll ändern und neu erzeugen. Die CI prüft, dass beide übereinstimmen.")
    z.append("")
    z.append("**Keine Rechtsberatung.** Die Liste sammelt, was das Protokoll belegt; was dort „unbekannt“ ist, ist auch hier offen.")
    z.append("")
    z.append("## Valves Regel")
    z.append("")
    z.append("Steam fragt im Content Survey nach KI-Inhalten (https://partner.steamgames.com/doc/gettingstarted/contentsurvey, "
             "Abschnitt zu KI; überarbeitet im Januar 2026, siehe z. B. "
             "https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/):")
    z.append("")
    z.append("- **Pre-Generated:** Inhalte, die während der Entwicklung mit KI-Werkzeugen erzeugt wurden, mit dem Spiel "
             "ausgeliefert und von Spielern wahrgenommen werden (Grafik, Modelle, Audio, Text). Sie werden offengelegt und wie "
             "andere Inhalte geprüft.")
    z.append("- **Live-Generated:** Inhalte, die KI erst während des Spiels erzeugt; dann sind zusätzlich Schutzmaßnahmen "
             "gegen illegale Inhalte anzugeben. Reichsgilde erzeugt nichts während des Spiels.")
    z.append("- Effizienzgewinne durch KI-Werkzeuge in der Entwicklung (z. B. Code-Assistenz) stehen laut Valve nicht im Mittelpunkt "
             "der Angabe. Die Skripte, Shader und Werkzeuge dieses Projekts sind mit Claude geschrieben; ob das angegeben werden "
             "soll, entscheidet der Nutzer.")
    z.append("")
    z.append("Wortlaut der Regel vor der Angabe an der Quelle prüfen; die Zusammenfassung stammt aus einer Recherche vom 29.10.2026.")
    z.append("")
    z.append("## Ausgeliefert und für Spieler sichtbar (Pre-Generated)")
    z.append("")
    if summe:
        z.append("Zusammen: " + ", ".join(f"{summe[a]} × {a}" for a in sorted(summe)) + ".")
        z.append("")
    z.append("| Inhalt | Anzahl | Art | Werkzeug | Modell | Bemerkung |")
    z.append("|---|---|---|---|---|---|")
    for zeile in sichtbar:
        z.append(f"| `{zeile['pfad_muster']}` | {zeile['anzahl']} | {zeile['art']} | {tabellenzelle(zeile['werkzeug'])} | "
                 f"{tabellenzelle(zeile['modell_version'])} | {tabellenzelle(zeile['bemerkung'])} |")
    z.append("")
    z.append("## KI-Inhalte im Repository, die das Spiel nicht zeigt")
    z.append("")
    z.append("Zwischenstufen unter `tools/`; das Spiel liefert sie nicht aus.")
    z.append("")
    z.append("| Inhalt | Anzahl | Art | Werkzeug | Bemerkung |")
    z.append("|---|---|---|---|---|")
    for zeile in intern:
        z.append(f"| `{zeile['pfad_muster']}` | {zeile['anzahl']} | {zeile['art']} | {tabellenzelle(zeile['werkzeug'])} | "
                 f"{tabellenzelle(zeile['bemerkung'])} |")
    if offen:
        z.append("")
        z.append("## Herkunft unbekannt")
        z.append("")
        z.append("Ob hier KI beteiligt war, ist nicht belegt.")
        z.append("")
        z.append("| Inhalt | Anzahl | Spieler sieht | Bemerkung |")
        z.append("|---|---|---|---|")
        for zeile in offen:
            z.append(f"| `{zeile['pfad_muster']}` | {zeile['anzahl']} | {zeile['spieler_sieht']} | {tabellenzelle(zeile['bemerkung'])} |")
    return "\n".join(z) + "\n"


def selbsttest():
    """Prüft die Stichtag-Regel an erfundenen Zeilen: python3 tools/assets/protokoll_pruefen.py --selbsttest"""
    global STICHTAG
    alt = STICHTAG
    try:
        zeile = {s: "x" for s in SPALTEN}
        zeile.update(_nummer=2, pfad_muster="a", anzahl="1", art="Bild", herkunft="Nutzer", spieler_sieht="ja",
                     nachbearbeitung="nein", prompt_oder_quelle="x")
        faelle = [  # (Stichtag, Datum, Lizenz, erwartet Fehler)
            (None, "2030-01-01", "CC BY 4.0", False),
            ("2026-11-01", "2026-10-31", "CC BY 4.0", False),
            ("2026-11-01", "2026-11-01", "CC BY 4.0", False),
            ("2026-11-01", "2026-11-02", "CC BY 4.0", True),
            ("2026-11-01", "2026-11-02", "Alle Rechte vorbehalten", False),
            ("2026-11-01", "2026-11-02", "CC0 1.0", False),
            ("2026-11-01", "unbekannt", "CC BY 4.0", False),
        ]
        ok = True
        for stichtag, datum, lizenz, soll in faelle:
            STICHTAG = stichtag
            zeile.update(datum=datum, lizenz_ergebnis=lizenz)
            fehler = []
            felder_pruefen(zeile, fehler)
            ist = any("Stichtag" in f for f in fehler)
            if ist != soll:
                ok = False
                print(f"Selbsttest FEHLER: Stichtag {stichtag}, Datum {datum}, Lizenz {lizenz}: erwartet {soll}, war {ist}")
        print("Selbsttest in Ordnung." if ok else "Selbsttest fehlgeschlagen.")
        return 0 if ok else 1
    finally:
        STICHTAG = alt


def main():
    if sys.argv[1:] == ["--selbsttest"]:
        return selbsttest()
    schreiben = "--steam-liste" in sys.argv[1:]
    unbekannt = [a for a in sys.argv[1:] if a != "--steam-liste"]
    if unbekannt:
        print(f"Unbekannte Option: {' '.join(unbekannt)}\n{__doc__}")
        return 2

    fehler = []
    zeilen = protokoll_lesen(fehler)
    for zeile in zeilen:
        felder_pruefen(zeile, fehler)
    dateien = dateien_im_repository()
    erfasst = zuordnen(zeilen, dateien, fehler)

    if zeilen:
        soll = steam_liste(zeilen)
        if schreiben:
            STEAM_LISTE.write_text(soll, encoding="utf-8")
            print(f"Steam-Liste geschrieben: {STEAM_LISTE.relative_to(WURZEL)}")
        elif not STEAM_LISTE.is_file() or STEAM_LISTE.read_text(encoding="utf-8") != soll:
            fehler.append(f"{STEAM_LISTE.relative_to(WURZEL)} passt nicht zum Protokoll – neu erzeugen mit: "
                          "python3 tools/assets/protokoll_pruefen.py --steam-liste")

    if fehler:
        print(f"Asset-Protokoll: {len(fehler)} Fehler")
        for f in fehler:
            print(f"  - {f}")
        print("Regel: Kein Asset ohne Protokolleintrag im selben PR (Spalten und Muster: docs/assets/README.md).")
        return 1
    anzahl = sum(len(v) for v in erfasst.values())
    print(f"Asset-Protokoll in Ordnung: {len(zeilen)} Zeilen, {anzahl} Dateien erfasst.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
