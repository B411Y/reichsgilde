#!/usr/bin/env python3
"""Misst die Lautheit aller Audiodateien nach ITU-R BS.1770 / EBU R128 (ton Schritt 7). Verändert nichts.

Aufruf (nur Python-Standardbibliothek und ffmpeg; ffmpeg im Pfad oder über --ffmpeg):
  python -I tools/ton/lautheit_messen.py [--audio game/assets/audio] [--aus docs/assets/lautheit-messwerte.csv] [--ffmpeg <pfad>]

Je Datei (ffmpeg-Filter ebur128 mit true peak, K-Gewichtung, absolutes Tor -70 LUFS, relatives Tor -10 LU):
  integriert_lufs  Lautheit der ganzen Datei; bei Dateien unter 1 s leer (dort gilt momentan_max)
  momentan_max     höchste 400-ms-Lautheit (Maß der Meldungstöne und Türklänge), auch für Kurzes
  kurzzeit_max     höchste 3-s-Lautheit
  lra              Lautheitsumfang in LU
  true_peak_dbtp   Spitze, 4-fach überabgetastet
Klasse und Gruppe ergeben sich aus dem Pfad (siehe docs/spielbuch/ton.md, Kapitel Lautheit)."""
import argparse
import csv
import re
import subprocess
import sys
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor


def klasse_von(rel: str):
    t = rel.split("/")
    if t[0] == "musik":
        return "Musik", t[1]
    if t[0] == "stimmen":
        return "Stimmen", t[1]
    if t[0] == "geraeusche":
        return "Oberflaeche", t[1]
    if t[0] == "umgebung":
        return "Umgebung", t[1]
    return "sonstige", t[0]


def messen(ffmpeg: str, datei: Path):
    # Dateien unter 1 s werden mit Stille auf 1 s aufgefüllt, damit die 400-ms-Messung etwas liefert (die Lautheit der ganzen Datei
    # bleibt dann ohne Aussagekraft; maßgebend ist „momentan_max“). Die Spitze ändert das Auffüllen nicht.
    r = subprocess.run([ffmpeg, "-hide_banner", "-nostats", "-v", "verbose", "-i", str(datei), "-af",
                        "apad=whole_dur=1.0,ebur128=peak=true:framelog=verbose", "-f", "null", "-"],
                       capture_output=True, text=True, errors="replace")
    t = r.stderr
    m_max = max((float(x) for x in re.findall(r"\bM:\s*(-?\d+\.?\d*)", t)), default=None)
    zus = t[t.rfind("Summary:"):]
    def zahl(muster):
        g = re.search(muster, zus)
        return float(g.group(1)) if g else None
    d = re.search(r"Duration: (\d+):(\d+):(\d+\.?\d*)", t)
    dauer = int(d.group(1)) * 3600 + int(d.group(2)) * 60 + float(d.group(3)) if d else 0.0
    return {
        "dauer_s": dauer,
        "integriert_lufs": None if dauer < 1.0 else zahl(r"\bI:\s+(-?\d+\.?\d*) LUFS"),
        "momentan_max": m_max,
        "kurzzeit_max": max((float(x) for x in re.findall(r"\bS:\s*(-?\d+\.?\d*)", t) if float(x) > -120), default=None),
        "lra": zahl(r"LRA:\s+(-?\d+\.?\d*) LU"),
        "true_peak_dbtp": zahl(r"Peak:\s+(-?\d+\.?\d*) dBFS"),
    }


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--audio", default="game/assets/audio")
    p.add_argument("--aus", default="docs/assets/lautheit-messwerte.csv")
    p.add_argument("--ffmpeg", default="ffmpeg")
    a = p.parse_args()
    wurzel = Path(a.audio)
    dateien = sorted(wurzel.rglob("*.ogg"), key=lambda x: x.relative_to(wurzel).as_posix())
    with ThreadPoolExecutor(max_workers=3) as ex:
        werte = list(ex.map(lambda d: messen(a.ffmpeg, d), dateien))
    Path(a.aus).parent.mkdir(parents=True, exist_ok=True)
    felder = ["datei", "klasse", "gruppe", "dauer_s", "integriert_lufs", "momentan_max", "kurzzeit_max", "lra", "true_peak_dbtp"]
    with open(a.aus, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, felder, lineterminator="\n")
        w.writeheader()
        for d, v in zip(dateien, werte):
            rel = d.relative_to(wurzel).as_posix()
            k, g = klasse_von(rel)
            w.writerow({"datei": rel, "klasse": k, "gruppe": g, **{x: ("" if y is None else f"{y:.2f}" if x == "dauer_s" else f"{y:.1f}") for x, y in v.items()}})
    print(f"{len(dateien)} Dateien gemessen -> {a.aus}")


if __name__ == "__main__":
    sys.exit(main())
