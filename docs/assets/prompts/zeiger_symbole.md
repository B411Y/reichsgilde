# Zeiger-Symbole: Windrose und Messing-I (3D-Zeiger)

Kein KI-Inhalt und kein Prompt: beide Symbole sind komplett selbst gebaut (Blender 5.2.3, Python-Skript), Auftrag vom 14.11.2026 (Nutzerwunsch Handschuh-Zeiger).

- Skript: `tools/modelle/handschuh/zeiger_symbole.py` (Netze, Umriss-Hüllen, Material, glb); Rechenvorschau `zeiger_symbole_bilder.py` und `zeiger_symbole_blatt.py`.
- Windrose: vier gleiche Spitzen (N/O/S/W) mit Grat, kleiner Ring um die Mitte, 6,8 cm Spannweite (im Spiel 1,9-fach wie das Fragezeichen), 336 Dreiecke plus 336 für die Umriss-Hülle.
- Römisches I: aufrecht, 11 cm hoch, schmaler Balken (1,1 cm), waagerechte Serifen (4,2 cm) mit kleiner Kehle, angefaste Kante; 220 Dreiecke plus 220 für die Umriss-Hülle; Ursprung = Mitte des Balkens (Zeigerpunkt).
- Material: aus `game/assets/modelle/zeiger/tuerschloss_zeiger.glb` übernommen (Messing: Normalkarte unverändert, Farbtextur mit denselben Patina-Flecken je Farbkanal aufgehellt, damit die Symbole vor dem dunklen Leder lesbar sind; Umriss: schwarzbraun, invertierte Hülle 3,6 mm im Bild).
- Godot legt die beiden Texturen beim Import aus der glb heraus (`zeiger_symbole_Messing_farbe.png`, `zeiger_symbole_Messing_normal.png`; die Normalkarte ist bitgleich mit der des Türschlosses).
