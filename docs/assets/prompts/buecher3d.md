# 3D-Bücher für die Innenansichten, Lesepult und Schreibpult (3D-Modelle, eigene Skripte)

Kein KI-Modell, kein Prompt: Die Modelle entstehen rein aus Rechnung und Formen in `tools/modelle/buecher3d/` (Blender 5.2.3, `-b`): `buch_bau.py` (gemeinsame Bauteile, Gerüst, Clips, Messung, Export), `buch_stile.py` (Maße, Formen und Materialien je Buch), `buch_texturen.py` (Texturen), `moebel_bau.py` (Lesepult und Schreibpult mit `tools/modelle/bausteine.py`).

Auftrag (Nutzer, 17.11.2026, über den Teamleiter): drei Bücher mit je eigenem Aussehen, die in der Innenansicht in 3D aufklappen, beim Reiterwechsel umblättern und zuklappen (Personalbuch auf dem Lesepult, Familienbuch auf dem Stubentisch, Kontorbuch am Schreibpult); gleiche Machart wie der Handschuh und die Schriftrolle (schwarzer Umriss als invertierte Hülle, nur Albedo); Clips, Inhaltsflächen, 1.500 bis 2.500 Dreiecke. Die Formen sind frei erfunden (Lederbuch mit Riemen, edler Einband mit Wappenfeld, Rechnungsbuch mit Pergamentdeckel), nicht von einer Vorlage abgepaust.

Texturen: Einband, Leder, Seiten, Schnitt, Riemen, Pergamentkante werden in `buch_texturen.py` mit numpy aus festen Saaten gemalt (Wertrauschen, Fasern, Nähte, Rahmen, Spaltenlinien), keine Fremdbilder, keine KI.
