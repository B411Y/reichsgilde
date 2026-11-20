# Modulares Wohnhaussystem: Häuserzeilen aus Modulen (Variante A, Bausatz im 2-m-Raster)

Keine Bild-KI und keine Image-to-3D-KI. Module, UV, gebackene Verdeckung und das Textur-Array entstehen aus Skripten, die Claude (Claude Code, Teammitglied „darstellung“; Außenstelle Grafik-PC und NAS) in den Sitzungen vom 20.11.2026 geschrieben hat.

**Vorgaben (Auftrag des Teamleiters, Nutzer 20.11.2026):** Konzept „Modulares Wohnhaussystem“, Variante A – Häuserzeilen aus Modulen im 2-m-Raster statt freistehender Einzelhäuser, nach dem Vorbild spätmittelalterlicher Städte (Giebelhäuser mit Traufgasse, traufständige Zinshäuser mit Brandwand, Eckhäuser). Das Konzept mit Quellen ist kein Teil des Repositorys; die Entscheidungen stehen in `docs/bereiche/darstellung.md` („Modulares Wohnhaussystem“).

**Entscheidungen des Nutzers (20.11.2026):** Giebel zur Gasse für Spieler- und Bürgerhäuser, Traufe für Zeilen der Passanten; das Spielerhaus darf seitlich an Nachbarn stoßen; Schmalhaus mindestens 4 m (3 m nur in Kulissenzeilen); Völker mischen; schräge Knicke mit Keilstück; Auskragung 0,4 m nur bei Fachwerk; Erker nur am Eckhaus ab Stufe 2.

**Stil und Texturen:** wie die Neubau-Wohnhäuser des jeweiligen Volks – dieselben Trim-Sheets (Bänder aus `trimsheet.py`), im Spiel die Ebene des Häuser-Arrays (`Steinebenen.Ebene`), Putz- und Akzentfarbe je Haus aus dem Style-Guide. Satz `mensch_s1`: Fachwerk auf Bruchsteinsockel, Strohdach, Traufhöhe 5,6 m, Dachneigung 50° (Giebelhäuser) bzw. 45° (Traufhäuser).

**Bau:** `tools/modelle/neubau/zeile_module.py --satz=<satz>` (Blender, baut die 15 Module, backt die Verdeckung je Modul mit den echten Nachbarmodulen als Schattenwerfer in eine Zelle des Atlas `zeile_<satz>_ao_atlas.png`, exportiert `zeile_<satz>.glb`), danach `tools/modelle/neubau/zeile_ao_ebenen.py` (packt die Atlanten aller Sätze zu `zeilen_ao_ebenen.png`). Prüfung: `tools/modelle/neubau/zeile_pruefung.py --satz=<satz>`; Kontrollbilder: `tools/modelle/neubau/zeile_bilder.py`.
