# Innenraum-Wetter (Regen und Sturm von drinnen)

Kein KI-Prompt: eigene Synthese und Mischung aus dem Bestand mit `tools/ton/innen_wetter_bauen.py` (Python, numpy, ffmpeg; feste Zufallssaaten 101, 202, 303, 404, byte-gleich wiederholbar).

Ziel (Nutzerwunsch 27.11.2026): In der Innenansicht bei Regen oder Sturm hört man das Wetter von drinnen, tags und nachts, als eigene, freie Fassung.
Das fremde Vorbild (Hörvergleich, nichts davon im Repository, keine Datei daraus abgeleitet) gab nur Zielwerte vor: Regenbett etwa -34 LUFS, Energie 200 Hz bis 1 kHz, Nacht etwas heller (1 bis 4 kHz), kein fester Donner im Loop.

Quellen: nur eigene vorhandene Schichten (`regen_leicht`, `regen_stark`, `regen_dach`, `regen_traufe`, `wind_mittel`, ihrerseits aus CC0-Aufnahmen, siehe `game/assets/audio/umgebung/HERKUNFT.md`) und Synthese.

| Datei | Zusammensetzung |
|---|---|
| `innen_regen_tag` | Regenbett (regen_leicht, Bandpass 150-900 Hz), Dachprasseln (regen_dach 250-1500 Hz, -5 dB), Traufe (-17 dB), Tropfen am Fenster (Poisson 3,5/s, 1,6-3,4 kHz; große Tropfen um 900 Hz, -24 dB), Raumton unter 180 Hz (-23 dB) |
| `innen_regen_nacht` | wie Tag, dazu Bett 1-4 kHz (+0,9), Tropfen 4,5/s (-20 dB), Raumton -27 dB |
| `innen_sturm_tag` | Guss (regen_stark, 120 Hz-1,6 kHz), Wind (wind_mittel unter 700 Hz mit Böenkurve, -3 dB), Pfeifen in den Ritzen (drei schmale Bänder 380, 560, 840 Hz, je eigene Böenkurve), Luft, Knarzen der Balken (sieben Ereignisse, Gleiten 75-150 Hz mit Rucken, -14 dB), Raumton; kein Donner |
| `innen_sturm_nacht` | wie Tag, dazu Guss 1-4 kHz (+0,55), Pfeifen +2 dB |

Alle: 60 s, Stereo (links und rechts mit eigenem Zufall), -26 LUFS integriert wie jede Schicht, OGG Vorbis q4 (bitexact). Nahtlos: Synthese exakt periodisch (Frequenzbereich, Böen mit ganzzahligen Vielfachen von 1/60 Hz), Regenquellen mit Naht gleicher Leistung 3-4 s.
