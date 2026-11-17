# Schriftrolle für Meldungen (3D-Modell, eigenes Skript)

Kein KI-Modell, kein Prompt: Das Modell entsteht rein aus Rechnung und Formen in `tools/modelle/schriftrolle/schriftrolle_bau.py` (Blender 5.2.3, `-b`).

Auftrag (Nutzer, 17.11.2026, über den Teamleiter): aufgerollte Schriftrolle mit zwei Holzstäben oben und unten, gedrechselten Knäufen an beiden Enden und Pergament dazwischen (offen: ausgerollt, zu: beide Stäbe zusammen, Pergament aufgewickelt); gleiche Machart wie der 3D-Handschuh (schwarzer Umriss als invertierte Hülle); Animationen `aufrollen` und `zurollen`; Symbolfläche in der Mitte; unter 1.500 Dreiecken. Die Form ist nachgebaut, nicht von der gezeigten Zeichnung abgepaust.

Texturen: Pergament (192 x 384) und Holz (128 x 128) werden im Skript mit numpy aus festen Saaten gemalt (Wertrauschen, Fasern, dunkler Rand), keine Fremdbilder.
