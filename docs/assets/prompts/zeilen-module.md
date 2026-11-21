# Häuserzeilen: Module je Volk und Stufe (modulares Wohnhaussystem, Variante A)

Kein KI-Werkzeug: Die Module baut das eigene Blender-Skript `tools/modelle/neubau/zeile_module.py` (Blender 5.2.3, Python bpy), die
Verdeckung backt Cycles auf UV2 (`wohnhaus_mensch_s1_g1.uv2_und_ao`). Die Texturen sind die vorhandenen Trim-Sheets der Neubau-Häuser
(im Spiel die Ebenen des Häuser-Arrays, `Steinebenen.cs`); neue Bilder entstehen nur als AO-Atlas.

## Auftrag (Nutzer persönlich, 20.11.2026, über den Teamleiter)

Modulares Wohnhaussystem, Variante A (Bausatz mit festen Breiten auf dem 2-m-Raster), Konzept
`C:\Reichsgilde_Austausch\wohnhaus_modular\konzept.md`. Stufe A2: die Module für Mensch Stufe 2 und 3, Zwerg, Elb und Halbling
Stufe 1 bis 3, stilgleich zu den Neubau-Wohnhäusern des Volks (Trim-Sheet, Steinebenen, Bänder, AO-Atlas je Satz, reine Bänder im
Pixel-Look), Eckhäuser mit Erker ab Stufe 2, Auskragung 0,4 m nur bei Fachwerk.

Verbindliche Entscheidungen des Nutzers (20.11.2026): Spieler- und Bürgerhäuser Giebel zur Gasse, Wohnzeilen der Passanten Traufe zur
Gasse; Schmalhaus mindestens 4 m; Völker in einer Zeile mischen; schräge Knicke mit Keilstück statt verzerrter Häuser; Auskragung
0,4 m nur bei Fachwerk, Erker nur am Eckhaus ab Stufe 2.

## Module je Satz (`zeile_<volk>_s<stufe>.glb`)

k_g4, k_g6, k_g8 (giebelständig), k_t6, k_t8 (traufständig), seite_blind, seite_offen (Traufseiten der Giebelhäuser), kappe_brand,
kappe_offen (Giebelseiten der Traufhäuser), gasse (halbe Traufgasse), eck (Eckhaus 8 × 8), mauer, pfeiler, tor (Keilstück),
hinterhaus (Schuppen); AO-Atlas `zeile_<volk>_s<stufe>_ao_atlas.png` mit 4 × 4 Zellen zu 512 Bildpunkten.

## Bau, Array und Prüfung

`tools/modelle/neubau/zeile_module.py --satz=<satz>` baut die 15 Module, backt die Verdeckung je Modul mit den echten Nachbarmodulen
als Schattenwerfer und exportiert die GLB; `tools/modelle/neubau/zeile_ao_ebenen.py` (Python 3 mit numpy und Pillow) packt die Atlanten
aller Sätze in der Reihenfolge von `SAETZE` zum Array `zeilen_ao_ebenen.png`; `tools/modelle/neubau/zeile_pruefung.py --satz=<satz>`
prüft die exportierte GLB (Dreiecke, Rahmen, Verdeckung, Bandüberlauf, Nähte aller Nachbarschaften). Stufe A1 (Satz `mensch_s1`,
Planung `Zeilenplan.cs`, Darstellung `Stadtansicht.Zeilen.cs`, Probe `--zeilenprobe`) entstand am selben Tag auf der NAS.
