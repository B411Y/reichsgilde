# Herkunft: Props-Kit, Dorf-Zierteile und Tiere der Restflächen

- `props_gegenstaende.glb`: ausgewählte, bearbeitete Teile des Pakets **Fantasy Props MegaKit [Standard]** von **Quaternius**
  (Fässer, Kisten, Möbel, Werkzeug, Marktstände, Banner …).
- `props_dorf.glb`: ausgewählte Zierteile des Pakets **Medieval Village MegaKit [Standard]** von **Quaternius**
  (Treppen, Zäune, Türen, Fensterläden, Schornsteine, Handwagen, Balkone; keine Häuser, keine Dächer).
- `props_fuellung_kit.glb`: vier Ziegel-Mauerreste (Prop_Brick1–4) des Dorfpakets für die Restflächen-Füllung (Kirchhof, Bildstock).

Beide Pakete: https://quaternius.com, geladen über den Godot Asset Store. Lizenz: **CC0 1.0 Universal** (Public Domain Dedication),
wörtlich in der `License_Standard.txt` des jeweiligen Pakets: „Models by @Quaternius“.

Bearbeitung durch `tools/modelle/kit_import.py`: Auswahl, Vereinfachung großer Netze, Texturen verkleinert (WebP), Materialien neu aufgebaut.
Die Pakete selbst liegen nicht im Repository.

## fuellung_tiere.glb: Schwein und Ziege (CC0)

- **Schwein** (`SchweinStehend`, `SchweinWuehlend`): „Pig“ von **Quaternius**, geladen von https://poly.pizza/m/TNvG3QUFlp
  (die Seite nennt „Public Domain (CC0)“; Quaternius: https://quaternius.com/faq.html, alle seine Modelle CC0 1.0). Ein Dreiecksnetz mit
  562 Dreiecken und Skelett; die zweite Pose neigt Kopf und Hals mit den Skelett-Gewichten.
- **Ziege** (`ZiegeStehend`, `ZiegeFressend`): „Goat“ von **Copabrangorandel**, geladen von https://opengameart.org/node/142490 (Lizenz auf der
  Seite: CC0; Anmerkung des Urhebers: „CC0 intended“). Ein unbewegliches OBJ ohne Textur (in ZBrush modelliert), hier auf rund 2000 Dreiecke
  vereinfacht, der Schwanz gekürzt, für das Fressen der Kopf geneigt.
- **Huhn** (`Huhn`): das eigene Modell der Stadt (`tiere/tier_huhn_braun.glb`) ohne Skelett, auf Hofmaß (0,44 m) und mit drei statt acht Stoffen; kein Fremdmaterial.
- Bearbeitung: `tools/modelle/tiere_cc0.py` (Maßstab, Fuß auf den Boden, Stoffe je Fläche, glatt schattiert). Die Quelldateien liegen nicht im
  Repository (Ordner `RG_KITS`, `Quaternius_Pig_CC0/pig_TNvG3QUFlp.glb` und `OGA_Goat_CC0/goatlowrez.obj`).
- Geprüft am 07.11.2026 an den Seiten der Quellen. Eine eigene Lizenzdatei liegt den Downloads nicht bei.

Herkunft je Datei aller Assets (auch dieser): `docs/assets/protokoll.csv`.
