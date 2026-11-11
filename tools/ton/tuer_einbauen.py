#!/usr/bin/env python3
"""Baut die zwei Tuerklaenge (tuer_auf, tuer_zu) fuer die Oberflaeche.

Aufruf: python tools/ton/tuer_einbauen.py <ordner_mit_quellen> [ffmpeg]
Quellen im Ordner: tuer_auf.wav (eigene Aufnahme des Nutzers), tuer_zu_2.wav (Freesound 402465, LampEight, CC0).
Nur Verstaerkung auf -18 LUFS (integriert, EBU R128), kein Begrenzer; Ausgabe OGG (libvorbis q6) nach
game/assets/audio/geraeusche/oberflaeche/ (Pfad relativ zum Repo). Danach True Peak pruefen (<= -1 dBTP).
"""
import re, subprocess, sys
from pathlib import Path

ZIEL_LUFS = -18.0
WURZEL = Path(__file__).resolve().parents[2]
AUSGABE = WURZEL / "game/assets/audio/geraeusche/oberflaeche"
DATEIEN = {"tuer_auf": "tuer_auf.wav", "tuer_zu": "tuer_zu_2.wav"}


def messen(ffmpeg, datei, filter_=""):
    af = (filter_ + "," if filter_ else "") + "ebur128=peak=true"
    text = subprocess.run([ffmpeg, "-nostats", "-i", str(datei), "-af", af, "-f", "null", "-"],
                          capture_output=True, text=True).stderr
    teil = text[text.rfind("Summary:"):]
    lufs = float(re.search(r"I:\s+(-?[\d.]+) LUFS", teil).group(1))
    spitze = float(re.search(r"Peak:\s+(-?[\d.]+) dBFS", teil).group(1))
    return lufs, spitze


def main():
    quellen = Path(sys.argv[1])
    ffmpeg = sys.argv[2] if len(sys.argv) > 2 else "ffmpeg"
    for name, quelle in DATEIEN.items():
        lufs, spitze = messen(ffmpeg, quellen / quelle)
        gewinn = ZIEL_LUFS - lufs
        ziel = AUSGABE / (name + ".ogg")
        subprocess.run([ffmpeg, "-y", "-v", "error", "-i", str(quellen / quelle), "-af", f"volume={gewinn:.3f}dB",
                        "-c:a", "libvorbis", "-q:a", "6", str(ziel)], check=True)
        l2, s2 = messen(ffmpeg, ziel)
        print(f"{name}: Quelle {lufs:.1f} LUFS / {spitze:.1f} dBFS, Gewinn {gewinn:+.2f} dB, Ergebnis {l2:.1f} LUFS / {s2:.1f} dBTP")


main()
