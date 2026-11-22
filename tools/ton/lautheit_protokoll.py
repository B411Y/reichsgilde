#!/usr/bin/env python3
"""Schreibt das Messprotokoll der Lautheit (Ton Schritt 7) als Markdown: je Gruppe und je Datei vorher und nachher.

Eingabe: docs/assets/lautheit-messwerte.csv (lautheit_messen.py), game/daten/klang_lautheit.json (lautheit_angleichen.py)
Aufruf: python -I tools/ton/lautheit_protokoll.py [--aus docs/ton/lautheit-messprotokoll.md]   (nur Standardbibliothek)
Der Abschnitt "Pegel je Bus" steht fest in BUS_TEXT unten (aus den Konstanten des Codes zusammengetragen) und wird mit ausgegeben."""
import argparse
import csv
import json
import statistics
import sys
from collections import OrderedDict
from pathlib import Path

BUS_TEXT = Path("tools/ton/lautheit_busse.md")


def zahl(s):
    return float(s) if s else None


def f(x):
    return "–" if x is None else f"{x:.1f}".replace("-", "−")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--aus", default="docs/ton/lautheit-messprotokoll.md")
    a = p.parse_args()
    zeilen = list(csv.DictReader(open("docs/assets/lautheit-messwerte.csv", encoding="utf-8")))
    ang = json.loads(Path("game/daten/klang_lautheit.json").read_text(encoding="utf-8"))["angleichung_db"]
    gruppen = OrderedDict()
    for z in zeilen:
        gruppen.setdefault((z["klasse"], z["gruppe"]), []).append(z)
    o = ["# Messprotokoll der Lautheit (Ton Schritt 7)", "",
         "Gemessen nach ITU-R BS.1770 / EBU R128 mit ffmpeg (Filter `ebur128`, True Peak 4-fach überabgetastet) durch `tools/ton/lautheit_messen.py`; "
         "Rohwerte: `docs/assets/lautheit-messwerte.csv`. Die Audiodateien sind **unverändert**; die Angleichung der Oberflächenklänge steht in "
         "`game/daten/klang_lautheit.json` (`tools/ton/lautheit_angleichen.py`) und wirkt als Lautstärke der Variante beim Abspielen.", "",
         "Spalten: **LUFS** = integrierte Lautheit der Datei (Dateien unter 1 s: –), **M max** = höchste 400-ms-Lautheit, **TP** = True Peak in dBTP, "
         "**Angleichung** = dB, die das Spiel der Variante zugibt, **M nachher** = M max plus Angleichung.", "",
         "## Je Gruppe", "",
         "| Klasse | Gruppe | Dateien | LUFS min / Median / max | M max min / Median / max | TP höchste | M-Streuung nachher (max − min) |",
         "|---|---|---|---|---|---|---|"]
    for (k, g), v in gruppen.items():
        I = [zahl(x["integriert_lufs"]) for x in v if zahl(x["integriert_lufs"]) is not None]
        M = [zahl(x["momentan_max"]) for x in v]
        N = [m + ang.get(Path(x["datei"]).stem, 0.0) if k == "Oberflaeche" else m for x, m in zip(v, M)]
        P = [zahl(x["true_peak_dbtp"]) for x in v]
        li = f"{f(min(I))} / {f(statistics.median(I))} / {f(max(I))}" if I else "–"
        o.append(f"| {k} | {g} | {len(v)} | {li} | {f(min(M))} / {f(statistics.median(M))} / {f(max(M))} | {f(max(P))} | "
                 f"{max(M) - min(M):.1f} → {max(N) - min(N):.1f} |")
    ev = OrderedDict()
    for r in csv.DictReader(open("docs/assets/lautheit-angleichung.csv", encoding="utf-8")):
        ev.setdefault(r["ereignis"], []).append(r)
    o += ["", "Die Streuung der Gruppe „Oberflaeche“ vermischt verschiedene Ereignisse (Klick und Siegel liegen absichtlich weit auseinander); maßgebend ist die Streuung **innerhalb eines Ereignisses**:", "",
          "| Ereignis | Varianten | M max vorher (min bis max) | Streuung vorher | M nachher | Streuung nachher |", "|---|---|---|---|---|---|"]
    for e, v in ev.items():
        vor = [float(x["momentan_max_vorher"]) for x in v]
        nach = [float(x["momentan_max_nachher"]) for x in v]
        o.append(f"| {e} | {len(v)} | {f(min(vor))} bis {f(max(vor))} | {max(vor) - min(vor):.1f} dB | {f(statistics.mean(nach))} | {max(nach) - min(nach):.1f} dB |")
    o += ["", Path(BUS_TEXT).read_text(encoding="utf-8").rstrip(), "", "## Je Datei", "",
          "| Datei | LUFS | M max | TP | Angleichung | M nachher |", "|---|---|---|---|---|---|"]
    for z in zeilen:
        st = Path(z["datei"]).stem
        m = zahl(z["momentan_max"])
        an = ang.get(st) if z["klasse"] == "Oberflaeche" else None
        o.append(f"| {z['datei']} | {f(zahl(z['integriert_lufs']))} | {f(m)} | {f(zahl(z['true_peak_dbtp']))} | "
                 f"{'–' if an is None else format(an, '+.1f').replace('-', '−')} | {f(m + (an or 0.0))} |")
    Path(a.aus).parent.mkdir(parents=True, exist_ok=True)
    Path(a.aus).write_text("\n".join(o) + "\n", encoding="utf-8")
    print(f"{len(zeilen)} Dateien -> {a.aus}")


if __name__ == "__main__":
    sys.exit(main())
