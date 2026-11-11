# Tuerschloss mit Klinke (3D-Zeiger)

Kein KI-Inhalt und kein Prompt: das Modell ist komplett selbst gebaut (Blender 5.2.3, Python-Skript), Entscheidung und Auftrag vom 11.11.2026 (3D-Handschuh Teil 3a).

- Skript: `tools/modelle/tuerschloss/tuerschloss_bau.py` (Modell, Aktionen, glb), `tuerschloss_bilder.py` (Standbilder).
- Formvorlage: der 2D-Mauszeiger `game/assets/oberflaeche/zeiger/tuer_schloss_*.png` (nur zum Abmalen der Form und Farbe; das 3D-Modell ist eigene Arbeit).
- Inhalt: Beschlagplatte aus Messing (Langschild mit Lilienenden, 16 cm, Schluesselloch, vier Naegel), Rosette und S-foermige Klinke (ca. 10 cm, Drehpunkt an der Welle, Knoten `Klinke`), Umriss-Huelle wie beim Handschuh.
- Texturen: aus Rauschen berechnet (Patina, dunkle Flecken), keine Vorlage.
- Aktionen: `Druecken` (20 Bilder bei 24/s) und `Loslassen` (12 Bilder); Ruhe ist die Grundstellung.
- Klinke_bereit/-greifen/-druecken/-loslassen und Uebergaenge der Hand: `tools/modelle/handschuh/v6_klinke.py`.
