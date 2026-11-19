# Symbole der Zunftlade

Dateien: `game/assets/oberflaeche/zunftlade/symbole/*.svg` (18 Zeichen, je 48 x 48 Pixel, Zeichenfläche 24 x 24). Protokoll: `docs/assets/protokoll.csv`.

**Herkunft.** Die Zeichen stammen aus dem HTML-Entwurf der Oberfläche „Zunftlade“ (Entwurf 3, November 2026), den der Nutzer ausgewählt hat. Ein Claude-Agent hat sie dort als SVG-Pfade von Hand als Code geschrieben (keine Bild-KI, keine Vorlage, kein Fremdmaterial). Welche Modellversion den Entwurf geschrieben hat, ist nicht belegt (im Protokoll „unbekannt“). Einen Prompt im Sinn einer Bild-KI gibt es nicht; der Auftrag an den Entwurf lautete, einfache einfarbige Zeichen für die Fächer und Knöpfe zu setzen.

**Übernahme ins Spiel (19.11.2026).** Je Zeichen eine Datei: der Inhalt des `<symbol>` aus dem Entwurf, mit `width="48" height="48"` (doppelte Größe, damit Godot es scharf rastert) und fester Füllfarbe `#e9d9ae` statt `currentColor`. Die Münze (`gulden.svg`) behält ihre eigenen Farben.

**Zuordnung** (Name im Thema `zunftlade_thema.tres`, Familie „LadeSymbole“): gulden, ansehen, familie, hammer (Betriebe und Produktion), bauen, personal, lager, markt, hinterzimmer, menue, rat, bericht, lexikon, karren, talentbaum, stadt (Zur Stadt), pause, weiter.

**Stil.** Es sind schlichte Platzhalter des Entwurfs, kein kolorierter Holzschnitt. Zeichen im Stil der Oberfläche folgen mit einem späteren Teil des Umbaus.
