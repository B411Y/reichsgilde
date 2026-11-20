# Zeichen des Hinterzimmers („Das Schwarze Buch“)

Dateien: `game/assets/oberflaeche/hinterzimmer/zeichen/*.svg` (20 Dateien, je 64 x 64 Pixel, Zeichenfläche 32 x 32). Protokoll: `docs/assets/protokoll.csv`.

**Herkunft.** Die Zeichen stammen aus dem HTML-Entwurf des Hinterzimmers (Variante C „Das Schwarze Buch“, 18.11.2026), den der Nutzer ausgewählt hat. Ein Claude-Agent hat sie dort als SVG-Pfade von Hand als Code geschrieben (keine Bild-KI, keine Vorlage, kein Fremdmaterial). Welche Modellversion den Entwurf geschrieben hat, ist nicht belegt (im Protokoll „unbekannt“). Einen Prompt im Sinn einer Bild-KI gibt es nicht; der Auftrag war der Entwurf der Tafel als Ganzes.

**Übernahme ins Spiel (20.11.2026).** Je Zeichen eine Datei, gebaut wie die Funktion `ic()` des Entwurfs: ein farbiger Kreis (Farbe je Zeichen aus der Tabelle `FARBE` des Entwurfs, Deckkraft 0,6) und darüber die Striche aus der Tabelle `PFAD` (Breite 2, runde Enden), unverändert. 16 Zeichen mit dunklem Strich `#1a120a` für die Seiten; vier davon zusätzlich mit hellem Strich `#f3e6c4` für die Lesebänder (`mond_hell`, `sack_hell`, `leute_hell`, `schild_hell`). Größe 64 x 64 statt der 27 bis 30 Pixel des Entwurfs, damit Godot scharf rastert.

**Zuordnung** (Name im Thema `hinterzimmer_thema.tres`, Familie „HzZeichen“):

| Zeichen | Verwendet für |
|---|---|
| sack | Diebstahl; hell: Leseband „Die Häuser“ |
| flamme | Sabotage |
| leute | Einbruch (eigene Leute); hell: Leseband „Eigene Leute“ |
| schluessel | Einbruch ins Wohnhaus |
| brand | Brand am Wohnhaus |
| karren | Überfall auf Karren |
| flasche | Krötenschleim |
| dolch | Giftdolche |
| blatt | Hetzschrift |
| blume | Blume der Zwietracht |
| auge | Auskundschaften |
| muenze | Schutzgeld |
| kette | Entführung |
| brief | Erpresserbrief |
| schild | hell: Leseband „Wachen, Briefe“ (dunkel: noch frei) |
| mond | hell: Leseband „Heute Nacht“ (dunkel: noch frei) |

**Stil.** Es sind schlichte Strichzeichen des Entwurfs, kein kolorierter Holzschnitt. Zeichen im Stil der Oberfläche brauchen die lokale Bild-KI und folgen später.
