# Zugtiere (Pferd und Ochse)

Kein KI-Inhalt, ein Fremdpaket mit Eigenbau-Nachbearbeitung. Diese Datei hält Herkunft und Ablauf fest (Prompt entfällt).

## Quelle

- Paket: „LowPoly Animated Farm Animal Pack“, Autor quaternius, 08.06.2018, https://opengameart.org/content/lowpoly-animated-farm-animal-pack
- Archiv `Farm Animals by @Quaternius.zip` (Größe rund 7 MB), SHA-256 `9ff42b7e2445eaf259e4734c08172637db92e4b59a6145909ac56d946152c39b`.
- Verwendet: `Blends/Horse.blend` (SHA-256 `decc922bd8d1c1a57d58b7cb3fcfa5a6138bebaab522b8dbc3ca750f7e9790ed`) und `Blends/Cow.blend` (SHA-256 `07ad993b793fd9661a73331e1875e5021fa9b36d3228f11a7e316be249e537ea`) mit Netz und 28-Knochen-Skelett (samt IK der Beine). Die Bewegungen des Pakets sind **nicht** verwendet (Walk, WalkSlow, Run, Jump, Death, Idle): Die Fußfolge der Vorlage ist ein Passgang (Hinterhuf und Vorderhuf einer Seite dicht hintereinander), nicht der Viergang; alle Bewegungen sind Eigenbau. Die übrigen Tiere des Pakets (Zebra, Lama, Schaf, Schwein, Mops) sind nicht verwendet.
- Lizenz, an der Seite des Pakets: „License(s): CC0“. In der mitgelieferten `License.txt`: „CC0 1.0 Universal (CC0 1.0) Public Domain Dedication https://creativecommons.org/publicdomain/zero/1.0/ … Credit not necessary, but if you want to credit me, you can write it like this: Farm Animals Pack was created by Quaternius“. Beides am 10.11.2026 gelesen; die Seite ließ sich mit curl abrufen (ein anderer Abruf bekam 403).
- Die Quelldateien liegen nicht im Repository. Das Skript sucht sie über die Umgebungsvariable `RG_FARM_BLENDS` (Ordner mit den .blend-Dateien), sonst unter `$RG_KITS/Quaternius_Farm_Animals_CC0/Blends`.

## Ablauf (tools/modelle/zugtiere.py)

Blender 5.2.3 im Hintergrund (`blender -b --python tools/modelle/zugtiere.py`), Cycles nur für die Vorschaubilder.

1. Bewegungen der Vorlage (Idle, WalkSlow) mit eingerechneter IK in Knochendrehungen übertragen.
2. Netz umformen: Pferd zum Kaltblut (tieferer, breiterer Rumpf, dickere Beine und Hals), Kuh zum Ochsen (Euter entfernt, Hals und Widerrist kräftiger).
3. Einmal unterteilt (Catmull-Clark), glatt schattiert, Maßstab Meter (Pferd Widerrist rund 1,55 m, Ochse rund 1,15 m), Füße am Boden.
4. Fell nach Fläche (Fell, Dunkel, Maul, Huf) als Eckenfarbe mit Verlauf und leichtem Rauschen, ein Material „Farben“; zwei Fellvarianten je Tier (Pferd rotbraun und schwarzbraun, Ochse rotbraun und falb).
5. Eigenbau-Zubehör im selben Netz: Pferd Kummet, Hölzer, Bauchgurt; Ochse Hörner und Nackenjoch. Haarkarten (verjüngte Bänder, nur im Nahnetz): Pferd Mähne, Stirnschopf, Schweifhaar, Fesselbehang, Ochse Schwanzquaste. Fell: Schicht „fell“ der Material-Bibliothek (tools/oberflaeche/fell_schicht.py, rein rechnerisch). Leder und Holz tragen die Schichten der Material-Bibliothek.
6. Bewegungen (tools/modelle/zugtiere_anim.py, Eigenbau): eine Planung der Hufe in der Welt (Fußfolge links hinten, links vorn, rechts hinten, rechts vorn; im Stand fest, im Schwung auf einem Bogen), der Rumpf senkt sich so weit, dass jedes Bein erreichbar bleibt, Hals und Kopf nicken, der Schweif schwingt nach; Clips Stehen, Stehen_Ruhen, Stehen_Umschauen, Fressen, Gehen, Ziehen, Anfahren_Gehen, Anhalten_Gehen, Anfahren_Ziehen, Anhalten_Ziehen. Tempo Pferd 1,3 m/s, Ochse 0,8 m/s bei Tempo 1.
7. Netze `Nah_<art>` (Pferd 4.286, Ochse 3.914 Dreiecke) und `Fern_<art>` (597 bzw. 545 Dreiecke, ohne Haar, Decimate); zwei Ohrknochen (Ohr_L, Ohr_R), Anker `Anker_Geschirr` (Zugpunkt).

Nicht benutzt: Human Generator, Material aus „Die Gilde“, KI-Werkzeuge.
