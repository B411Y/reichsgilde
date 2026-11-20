# Herkunft der Texturen des Hinterzimmers („Das Schwarze Buch“)

Beide Dateien sind fremde CC0-Aufnahmen und liegen **unverändert** hier (Byte für Byte die Quelldatei, nur umbenannt): kein Zuschnitt, keine Umfärbung, keine hinzugerechneten Effekte. Dunkler bzw. wärmer wird nur beim Zeichnen über die Farbe (`modulate_color`) der Flächen in `game/scenes/hinterzimmer_thema.tres`; dort stehen auch die Ausschnitte (`region_rect`) für die linke und die rechte Seite.

| Datei | Quelle | Seite | Urheber | Lizenz | Prüfsumme | Abruf |
|---|---|---|---|---|---|---|
| `papier.jpg` (794 × 1024) | ambientCG, „Paper 002“, Farbkarte `Paper002_1K-JPG_Color.jpg` aus `Paper002_1K-JPG.zip` | https://ambientcg.com/a/Paper002 | ambientCG (Lennart Demes) | CC0 1.0 | Datei SHA-256 `15e45b65948f054d9c783dee72067b0a8029975de1b22984a2c3c084a4c6d460`, ZIP SHA-256 `15b07f4abef6960de4e8b890228cf96dbadd86522c2053b46f6eaa5000426110` | 20.11.2026 |
| `leder.jpg` (1024 × 1024, nahtlos kachelbar) | Poly Haven, „Brown Leather“, Farbkarte 1K-JPG (`brown_leather_albedo_1k.jpg`, über `api.polyhaven.com/files/brown_leather`) | https://polyhaven.com/a/brown_leather | Rob Tuytel | CC0 1.0 | MD5 `080b3bf1372766ff2c7ff121ed07b5e8` (so auch in der Poly-Haven-API) | 19.11.2026 |

**Lizenz: CC0 1.0 Universal (Public Domain).** ambientCG, Zitat wörtlich von https://docs.ambientcg.com/license/: „All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License.“ Poly Haven, Zitat wörtlich von https://polyhaven.com/license: „Our assets are all licensed as CC0, which is effectively Public Domain even in jurisdictions that do not support the Public Domain.“

Verwendet für: Einband (Leder, gekachelt) und die zwei Seiten (Papier mit feinen Einschlüssen, im Thema pergamentfarben getönt) der Hinterzimmer-Tafel `GaunerAnzeige`.

**Ersetzt (20.11.2026):** Bis dahin lag als Seite `pergament.jpg` hier (Wikimedia Commons, „Pergament.1.jpg“, Membeth, CC0). Die Aufnahme zeigt die Haarseite eines Pergaments mit kräftigem Fleckenmuster und wirkte hinter der Schrift zu unruhig (Nutzer, 20.11.2026); die Datei ist entfernt.

Die Zeichen unter `zeichen/*.svg` sind kein Fremdmaterial; ihre Herkunft steht in `docs/assets/prompts/hinterzimmer_zeichen.md`.
