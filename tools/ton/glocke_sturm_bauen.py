"""Baut die Feuerglocke beim Blitzschlag: game/assets/audio/umgebung/glocken/glocke_sturm.ogg (Schleife).

Quelle ist das Geläut des Kölner Doms (Aufnahme 511546 von polymorpheva, CC0; die vorhandenen Ausschnitte glocke_morgen_1.ogg und
glocke_morgen_2.ogg, siehe game/assets/audio/umgebung/HERKUNFT.md). Wunsch des Nutzers (2.11.2026): tiefer und wärmer als das erste
Sturmläuten aus der Noteman-Glocke, weniger schrill, etwas langsamer. Beide Ausschnitte werden aneinandergeblendet, um den Faktor 1,12
schneller (und damit etwas höher) gesetzt, im Takt von 0,9 s leicht pochend betont (Sturm statt Feiertag) und über 2,5 kHz gedämpft.
Das Ende ist mit gleicher Leistung über den Anfang geblendet: Die Datei läuft als Schleife, bis das Feuer aus ist (KlangSturmlaeuten).
Lautheit RMS 0,12 (etwas leiser als die anderen Glocken), Spitze höchstens 0,85, OGG Vorbis 112 kbit/s, mono, 48 kHz.

Läuft in Blender (dessen Python hat numpy und audaspace „aud“ zum Lesen und Schreiben von OGG):
  ~/werkzeuge/blender/blender.sh -b --python tools/ton/glocke_sturm_bauen.py -- game/assets/audio/umgebung/glocken/glocke_sturm.ogg
"""
import os, sys
import aud, numpy as np
r = 48000
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
G = os.path.join(WURZEL, "game/assets/audio/umgebung/glocken/")
def lesen(n): return aud.Sound(G + n + ".ogg").data()[:, 0].astype(np.float64)
def tiefpass(x, hz):
    X = np.fft.rfft(x); f = np.fft.rfftfreq(len(x), 1 / r)
    X *= 1 / np.sqrt(1 + (f / hz) ** 4)
    return np.fft.irfft(X, len(x))
def tonhoehe(x, f):
    idx = np.arange(0, len(x) - 1, f); return np.interp(idx, np.arange(len(x)), x)
blende = int(2.0 * r)
a, b = lesen("glocke_morgen_1"), lesen("glocke_morgen_2")
w = np.linspace(0, np.pi / 2, blende)
x = np.concatenate([a[:-blende], a[-blende:] * np.cos(w) + b[:blende] * np.sin(w), b[blende:]])
x = tonhoehe(x, 1.12)
t = np.arange(len(x)) / r
x = tiefpass(x * (0.65 + 0.35 * np.exp(-((t % 0.9) / 0.25))), 2500)
# Schleifennaht: das Ende über den Anfang blenden, die Datei wird um die Blende kürzer
x[:blende] = x[:blende] * np.sin(w) + x[-blende:] * np.cos(w)
x = x[:-blende]
x *= 0.12 / np.sqrt((x ** 2).mean()); x *= min(1, 0.85 / np.abs(x).max())
print("Länge", len(x) / r, "s, Spitze", float(np.abs(x).max()))
ziel = sys.argv[-1]
aud.Sound.buffer(x.astype(np.float32).reshape(-1, 1), r).write(ziel, rate=r, channels=aud.CHANNELS_MONO, format=aud.FORMAT_FLOAT32,
    container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=112000)
print("geschrieben", ziel)
