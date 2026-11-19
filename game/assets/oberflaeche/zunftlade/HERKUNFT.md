# Herkunft der Texturen der Zunftlade

Stoffe der neuen Oberfläche (UI-Umbau „Zunftlade“, ab 18.11.2026): Holz der Lade und der Fächer, Steinrand, Steinbogen ums Porträt, Pergament. Alle Quellen sind fremde Aufnahmen unter **CC0 1.0 Universal (Public Domain)**; die Dateien hier bleiben CC0. Gebaut mit `tools/oberflaeche/zunftlade_texturen.py` (Python 3 mit numpy und Pillow); die Quelldateien liegen nicht im Repository.

Lizenz Poly Haven, wörtlich von https://polyhaven.com/license (Zitat wie in `game/assets/texturen/modelle/HERKUNFT.md`): alle Assets stehen unter CC0. Wikimedia Commons: Die Dateiseite nennt CC0 1.0 (Abruf 16.11.2026).

| Datei | Quelle | Urheber | Seite | Änderung (alles im Skript) |
|---|---|---|---|---|
| `holz.webp` | Poly Haven, „Dark Wooden Planks“ (1K-JPG: Farbe, Normale OpenGL, Verdeckung) | Amal Kumar | https://polyhaven.com/a/dark_wooden_planks | Farbkarte warm eingefärbt, mit Verdeckungs- und Normalkarte derselben Textur schattiert; 1024 px, kachelt |
| `fachholz.webp` | Poly Haven, „Wood Table Worn“ (1K-JPG: Farbe, Normale OpenGL) | Dimitrios Savva (Foto), Rico Cilliers (Aufbereitung) | https://polyhaven.com/a/wood_table_worn | entsättigt und abgedunkelt, mit der Normalkarte derselben Textur schattiert; auf 512 px verkleinert, kachelt |
| `rahmen.webp` | Poly Haven, „Large Sandstone Blocks“ (1K-JPG: Farbe) | Rob Tuytel | https://polyhaven.com/a/large_sandstone_blocks | nur das Gesimsband (Zeilen 38–122) ausgeschnitten, 20 % aufgehellt, verkleinert, für vier Seiten gedreht und auf Gehrung gesetzt; 454 px, neun Felder mit 32 px Rand, mit Transparenz |
| `bogen.webp` | Poly Haven, „Large Sandstone Blocks“ (1K-JPG: Farbe) | Rob Tuytel | https://polyhaven.com/a/large_sandstone_blocks | dasselbe Gesimsband um einen Rundbogen gebogen; 464 × 428 px, mit Transparenz |
| `pergament.webp` | Wikimedia Commons, „Pergament.1.jpg“ (Oberfläche eines mittelalterlichen Pergaments) | Membeth | https://commons.wikimedia.org/wiki/File:Pergament.1.jpg | auf einen Streifen 1024 × 512 px zugeschnitten, etwas wärmer und dunkler eingefärbt |

**Keine hinzugerechneten Effekte** (Entscheidung des Nutzers, 18.11.2026): keine Kerben, Flecken, Verläufe oder Schatten – nur Zuschnitt, Verkleinern, Drehen, Biegen, Farbe und die Schattierung des Holzes aus den Karten derselben Textur. Keine Bild-KI.

## Symbole (`symbole/*.svg`)

Kein Fremdmaterial: schlichte Zeichen aus dem HTML-Entwurf der Zunftlade, dort als SVG-Pfade von Hand als Code geschrieben. Einzelheiten: `docs/assets/prompts/zunftlade_symbole.md`.
