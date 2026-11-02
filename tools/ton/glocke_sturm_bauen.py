"""Baut das Sturmläuten der Feuerglocke beim Blitzschlag: game/assets/audio/umgebung/glocken/glocke_sturm.ogg.

Quelle ist ein einzelner Schlag der großen Glocke „Noteman“ aus der vorhandenen Feuerglocke (glocke_feuer_1.ogg, Ausschnitt 17,85–20,85 s;
Aufnahme 204899 von klankbeeld, CC BY 4.0, siehe game/assets/audio/umgebung/HERKUNFT.md). Der Schlag klingt schneller aus, wird um den Faktor
1,50 bzw. 1,42 höher gesetzt (eine kleinere Glocke, die beim Schwingen an beiden Seiten anschlägt) und 95 s lang schnell und unregelmäßig
wiederholt (alle 0,66–0,78 s, am Anfang langsamer, die Glocke kommt in Schwung, zum Ende hin langsamer); Lautheit wie die Feuerglocke
(gleicher RMS), Spitze höchstens 0,89, OGG Vorbis 112 kbit/s, mono, 48 kHz. Der Zufall hat einen festen Startwert: gleicher Klang bei jedem Lauf (die Datei unterscheidet sich nur in der Seriennummer des Ogg-Stroms).

Läuft in Blender (dessen Python hat numpy und audaspace „aud“ zum Lesen und Schreiben von OGG; ffmpeg oder soxr braucht es nicht):
  ~/werkzeuge/blender/blender.sh -b --python tools/ton/glocke_sturm_bauen.py -- game/assets/audio/umgebung/glocken/glocke_sturm.ogg
"""
import os, sys
import aud, numpy as np
r = 48000
WURZEL = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
quelle = aud.Sound(os.path.join(WURZEL, "game/assets/audio/umgebung/glocken/glocke_feuer_1.ogg")).data()[:, 0].astype(np.float64)
schlag = quelle[int(17.85 * r):int(20.85 * r)].copy()
n = len(schlag); t = np.arange(n) / r
schlag *= np.where(t < 0.5, 1.0, np.exp(-(t - 0.5) * 2.2))   # Ausklang kürzer: schnelle Schläge sollen nicht verschwimmen
schlag[:int(0.004 * r)] *= np.linspace(0, 1, int(0.004 * r))
schlag[-int(0.05 * r):] *= np.linspace(1, 0, int(0.05 * r))
def tonhoehe(x, f):
    idx = np.arange(0, len(x) - 1, f)
    return np.interp(idx, np.arange(len(x)), x)
klein = [tonhoehe(schlag, 1.50), tonhoehe(schlag, 1.42)]   # eine kleinere Glocke, die beim Schwingen an beiden Seiten anschlägt
dauer = 95.0
aus = np.zeros(int((dauer + 3) * r))
zufall = np.random.default_rng(1402)
zeit, i, schlaege = 0.25, 0, 0
while zeit < dauer - 4:
    s = klein[i % 2] * (0.85 + 0.15 * zufall.random()) * (0.55 + 0.45 * min(1, zeit / 4))   # die ersten Schläge leiser: die Glocke kommt in Schwung
    a = int(zeit * r); aus[a:a + len(s)] += s[:len(aus) - a]
    abstand = 0.66 + 0.12 * zufall.random()
    if zeit < 4: abstand += 0.5 * (1 - zeit / 4)              # am Anfang langsamer
    if zeit > dauer - 12: abstand += 0.35 * (zeit - (dauer - 12)) / 8   # zum Ende hin langsamer
    zeit += abstand; i += 1; schlaege += 1
aus = aus[:int(dauer * r)]
aus[-int(1.5 * r):] *= np.linspace(1, 0, int(1.5 * r))
rms_ziel = float(np.sqrt((quelle ** 2).mean()))
aus *= rms_ziel / float(np.sqrt((aus ** 2).mean()))
spitze = float(np.abs(aus).max())
if spitze > 0.89: aus *= 0.89 / spitze
print("Schläge", schlaege, "Spitze", float(np.abs(aus).max()), "RMS", float(np.sqrt((aus ** 2).mean())), "Ziel", rms_ziel)
ziel = sys.argv[-1]
klang = aud.Sound.buffer(aus.astype(np.float32).reshape(-1, 1), r)
klang.write(ziel, rate=r, channels=aud.CHANNELS_MONO, format=aud.FORMAT_FLOAT32, container=aud.CONTAINER_OGG, codec=aud.CODEC_VORBIS, bitrate=112000)
print("geschrieben", ziel)
