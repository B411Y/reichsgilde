"""Baut die Feuerglocke beim Blitzschlag: game/assets/audio/umgebung/glocken/glocke_sturm.ogg (Schleife, 50 s).

Quelle ist die große Glocke Noteman (Aufnahme 204899 von klankbeeld, CC BY 4.0, Namensnennung verlangt; der vorhandene Ausschnitt
glocke_feuer_1.ogg, siehe game/assets/audio/umgebung/HERKUNFT.md). Klangprobe A, vom Nutzer gewählt (2.11.2026): ein Schlag aus
17,85–21,85 s des Ausschnitts, nach 0,6 s schneller ausklingend, über 1,8 kHz gedämpft, abwechselnd um den Faktor 1,12 und 1,06
höher gesetzt (zwei Seiten der Glocke; tiefer als die erste Fassung mit 1,5), ein Schlag alle 1,15–1,3 s mit leicht wechselnder Stärke (feste Saat 7, gleiche Datei bei jedem Bau).
Die Schläge liegen im Kreis: Was über das Ende hinausklingt, läuft am Anfang weiter, darum hat die Schleife keine Naht. Die Abstände
sind so gestreckt, dass sie genau 50 s füllen. Lautheit RMS 0,12, Spitze höchstens 0,85, OGG Vorbis 112 kbit/s, mono, 48 kHz.
Die Datei läuft als Schleife, bis das Feuer aus ist (KlangSturmlaeuten).

Läuft in Blender (dessen Python hat numpy und audaspace „aud“ zum Lesen und Schreiben von OGG):
  ~/werkzeuge/blender/blender.sh -b --python tools/ton/glocke_sturm_bauen.py -- game/assets/audio/umgebung/glocken/glocke_sturm.ogg
"""
import os, sys
import aud, numpy as np
r = 48000
DAUER = 50.0
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
G = os.path.join(WURZEL, "game/assets/audio/umgebung/glocken/")
def lesen(n): return aud.Sound(G + n + ".ogg").data()[:, 0].astype(np.float64)
def tiefpass(x, hz):
    X = np.fft.rfft(x); f = np.fft.rfftfreq(len(x), 1 / r)
    X *= 1 / np.sqrt(1 + (f / hz) ** 4)
    return np.fft.irfft(X, len(x))
def tonhoehe(x, f):
    idx = np.arange(0, len(x) - 1, f); return np.interp(idx, np.arange(len(x)), x)
# Ein Schlag der großen Glocke
q = lesen("glocke_feuer_1"); s = q[int(17.85 * r):int(21.85 * r)].copy(); t = np.arange(len(s)) / r
s *= np.where(t < 0.6, 1.0, np.exp(-(t - 0.6) * 1.2)); s[:200] *= np.linspace(0, 1, 200); s[-2000:] *= np.linspace(1, 0, 2000)
s = tiefpass(s, 1800)
k = [tonhoehe(s, 1.12), tonhoehe(s, 1.06)]
# Abstände und Stärken würfeln, gerade Zahl der Schläge (die Seiten wechseln auch über die Naht), dann auf DAUER strecken
z = np.random.default_rng(7)
abstaende, staerken = [], []
while sum(abstaende) < DAUER or len(abstaende) % 2:
    abstaende.append(1.15 + 0.15 * z.random()); staerken.append(0.85 + 0.15 * z.random())
abstaende = np.array(abstaende) * DAUER / sum(abstaende)
n = int(DAUER * r); x = np.zeros(n); zeit = 0.0
for i, (d, st) in enumerate(zip(abstaende, staerken)):
    schlag = k[i % 2] * st
    p = (int(zeit * r) + np.arange(len(schlag))) % n   # im Kreis: der Ausklang am Ende läuft am Anfang weiter
    np.add.at(x, p, schlag)
    zeit += d
x *= 0.12 / np.sqrt((x ** 2).mean()); x *= min(1, 0.85 / np.abs(x).max())
print("Länge", len(x) / r, "s, Schläge", len(abstaende), ", Spitze", float(np.abs(x).max()))
ziel = sys.argv[-1]
aud.Sound.buffer(x.astype(np.float32).reshape(-1, 1), r).write(ziel, rate=r, channels=aud.CHANNELS_MONO, format=aud.FORMAT_FLOAT32,
    container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=112000)
print("geschrieben", ziel)
