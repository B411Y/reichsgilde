# Architektur – wie Reichsgilde aufgebaut ist

Dieses Dokument erklärt die Grundidee hinter dem Code. Es wächst mit dem Projekt. Was das Spiel kann, steht im [Spielbuch](spielbuch/README.md); Projektstruktur, Tests und Export in [ENTWICKLUNG.md](ENTWICKLUNG.md).

## Die wichtigste Regel: Simulation und Darstellung sind getrennt

```
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  game/  (Godot)              │        │  src/Reichsgilde.Core        │
│                              │ ruft   │                              │
│  • zeigt an                  │ ─────▶ │  • entscheidet, was passiert │
│  • nimmt Eingaben entgegen   │        │  • kennt Godot NICHT         │
│  • Szenen, Grafik, Sound     │ ◀───── │  • Zeit, Markt, Bürger, ...  │
│                              │ liest  │                              │
└──────────────────────────────┘        └──────────────────────────────┘
                                                      ▲
                                                      │ prüft
                                        ┌──────────────────────────────┐
                                        │  tests/ (automatische Tests) │
                                        └──────────────────────────────┘
```

**Warum?**

1. **Multiplayer:** Später läuft der Simulationskern auf einem Server, die Spieler sehen nur die Darstellung. Weil der Kern Godot nicht kennt, können wir ihn dort unverändert einsetzen.
2. **Testbarkeit:** Spiellogik lässt sich in Sekunden prüfen, ohne das Spiel zu starten (z. B. „Ergeben 15 Minuten wirklich genau ein Jahr?“). Der Testlauf (`tests/Reichsgilde.Core.Tests/Program.cs`) findet alle Testklassen selbst: jede statische Klasse mit `public static IEnumerable<(string, Action)> Alle()` – per Reflection, nach Klassenname sortiert. Eine neue Testdatei genügt. Die Auswahl (`--filter`, `--klasse`, `--ohne-langsam`, `--liste`, `--zeiten`) und den Lauf mit Zeitmessung übernimmt `Testauswahl.cs` (`Testauswahl.Lesen`, `Testlauf.KlassenTests`, `Testlauf.Ausfuehren`; jeder Test ist ein `Testfall` mit Klasse, die Tests oben in `Program.cs` heißen „Program“); welche Tests als langsam gelten, steht in `LangsameTests.cs` (Klasse, Namensmuster oder `null` für die ganze Klasse, Anzahl der Treffer), `TestfilterTests` prüft die Anzahl je Eintrag. Ohne Schalter laufen wie bisher alle Tests nacheinander. `--parallel[=N]` (`Parallellauf.cs`) verteilt die Testklassen auf N eigene Fäden (Tests einer Klasse nacheinander), puffert die Ausgabe je Klasse – `Console.Out` geht über eine Weiche mit `AsyncLocal` in den Puffer der Klasse des Fadens – und schreibt die Blöcke in der seriellen Reihenfolge; die Klassen aus `SeriellTests.cs` (Zeitgrenzen) laufen danach allein. Das geht, weil der Kern keinen veränderlichen statischen Zustand hat und jede Simulation ihren eigenen Zufall. Neben den Tests stehen dort die **Messungen** (`-- messen …`, kein Test): `Messung.cs` spielt eine Partie nach der anderen, der Balancing-Bericht (`Balancingbericht.cs`, `Messpartie.cs`, PR #176) viele zugleich – jede Messpartie liest ihre eigenen Daten und ihre eigene Karte und hat ihren eigenen Zufall, deshalb ist das Ergebnis parallel Wurf für Wurf dasselbe wie nacheinander.
3. **Übersicht:** Wer eine Spielregel sucht, schaut in `src/`. Wer wissen will, wie etwas aussieht, schaut in `game/`.

## Namenskonventionen

- Fachbegriffe im Code sind **deutsch** (`Spielzeit`, `Jahreszeit`, `Zunftglocke`), passend zum Setting.
- Umlaute werden in Bezeichnern umschrieben (`Fruehling`), in Texten für Spieler bleiben sie erhalten.
- Kommentare sind deutsch und erklären das **Warum**, nicht nur das Was.

## Ordner im Godot-Projekt

Die Dateien unter `game/` liegen nach Art getrennt (PR #164), wie in Spielen üblich:

| Ordner | Inhalt |
|---|---|
| `daten/` | Datentabellen: `wirtschaft.json` (alle Zahlen), `katalog.json` (Gebäudekatalog), `materialarten.json` (Materialart je Materialname für die Oberflächendetails; liest das Spiel und `tools/modelle/bausteine.py`), `materialbibliothek.json` (Schichten der Material-Bibliothek und Materialname → Schicht) |
| `karten/` | Kartendateien `<kennung>.json`, nur von der Kartenwerkstatt oder dem Kartenskript `tools/Kartenbau` geschrieben |
| `assets/modelle/` | 3D-Modelle (`.glb`) aus `tools/modelle/`, in Unterordnern: `gebaeude/` (Wohn- und Einwohnerhäuser, Werkstätten von außen, Dom, Rathaus, Stadtmauer, Markt), `innen/` (`*_innen`), `umland/` (Umlandbetriebe, Felsberg, Umlandstellen), `natur/`, `figuren/`, `fahrzeuge/` (Karren, alt mit Tieren und Skelett; wird beim Umbau gelöscht) und `karren/` (neue Karren ohne Tier und Figur mit eigenen Rädern `Rad_*` und Ankern, aus `tools/modelle/karren.py`), `bruecke/` (Klappbrücke von Köln, Entwurf aus `tools/modelle/klappbruecke.py` mit den Maßen in `klappbruecke_masse.py`: Klappen, Ketten, Gegengewichte, Zugwerk und Torhäuser als eigene Knoten mit Animationen `Oeffnen` und `Schliessen`; noch nicht im Spiel eingebaut; Texturen unter `assets/texturen/bruecke/`: eigenes Trim-Sheet `bruecke_trim_*` aus `klappbruecke_trim.py` und die gebackene Verdeckung `klappbruecke_ao_atlas.png`; Prüfung `klappbruecke_pruefen.py`; eingebaut in `game/scripts/Stadtansicht.Klappbruecke.cs`: Köln-Brücke nach den Pfeilern des Wasserplans, Aufteilung `Klappbrueckenlage` im Kern, Stoffe über `building_trim.gdshader`, Prüfhilfe `--klappbruecke`), `tiere/` (Vögel, Hund, Katze, Huhn und die Zugtiere Pferd und Ochse aus `tools/modelle/zugtiere.py`: Skelett mit Stehen, Gehen, Ziehen, Fressen, Nahnetz `Nah_<art>`, Fernnetz `Fern_<art>`, Anker `Anker_Geschirr`), `kulisse/` (Kulissenberge hinter dem Kartenrand, je Karte ein Modell: `kulisse_koeln.glb` aus `tools/modelle/kulisse_hoehen.py` und `kulisse_koeln.py` nach dem DGM NRW, Namen und Lage der Berge in `game/daten/berge.json`, Herkunft in `kulisse/HERKUNFT.md`), `wasser/` (seit PR #180: Holz- und Steinbrücke als Baukästen, Fähre mit Anlegesteg, Hafen, Furt- und Ufersteine, `wasser_*.glb`), `requisiten/` (einzelne Gegenstände für Ereignisse: `eimer.glb`, Holzeimer der Löschkette mit den Knoten „Eimer_0“ (Hauptmodell), „Eimer_1“ (Fernstufe) und „Wasser“) |
| `assets/texturen/` | Strukturen der Oberflächendetails (`strukturen.png` aus `tools/oberflaeche/texturen.py`), Bodentexturen (`boden/`), Material-Bibliothek der Modelle (`modelle/`, aus `tools/oberflaeche/materialien_blender.py`), Strukturkarten der Felsstücke (`felsen/`: Normale und Höhlung als Atlas 4 × 2 zu `natur/natur_felsen.glb`, aus `tools/modelle/natur_felsen.py`) und des Felswand-Satzes (`felswand/`: Normale und Höhlung, Atlas 4096 × 2048 zu `natur/natur_felswand.glb`, aus `tools/modelle/natur_felswand.py`; noch nicht im Spiel gesetzt, das kommt mit Werkstatt und „Wand ziehen“, Felsen R3/R5) |
| `assets/shader/` | Shader: Fahnen, Figuren, Oberflächendetails, Nachschärfen und Farbfilter (über dem fertigen Bild); `realistisches_wasser/`: fremder Wasser-Shader (MIT, UnionBytes) mit Lizenz und Herkunft, dazu das Wasser des Spiels: `wasser_realistisch.gdshaderinc` (eigene Ableitung mit Strömung, Schaum, Wind, Regenringen, Eis) mit den Grafikstufen `wasser_niedrig`, `wasser_mittel`, `wasser_realistisch.gdshader` (Hoch, Ultra), die Wellen in `wasser_wellen.gdshaderinc` (gemeinsam mit den Eisschollen `eis_scholle.gdshaderinc`, Stufen `eis_scholle.gdshader` und `eis_scholle_einfach.gdshader`) und die Gewässerarten in `scripts/Wasserarten.cs`; Testszene `scenes/Wassertest.tscn` (Startoption `--wassertest=1`, Doku `docs/wasser/`); das Fels-Material `fels.gdshaderinc` mit den Stufen `fels.gdshader` (dreiseitig, ab Mittel) und `fels_niedrig.gdshader` (eine Richtung, Niedrig), Werte aus `daten/gestein.json` über `scripts/Gesteinsarten.cs`, Testszene `scenes/Felstest.tscn` (`--felstest=1`) |
| `assets/oberflaeche/` | Spieloberfläche: `icons/` (Waren-Icons aus `tools/icons/`), `schriften/` (mit Lizenztexten), `wappen/` (noch leer), `ratsbaum/` (Amtszeichen, Pergament, Spruchband, Seil-Linie; über `rat_thema.tres` und `RatStil.Grafik/AmtZeichen/Pergament/Spruchband/Seil` im Ratsbaum angezeigt, `tools/oberflaeche/ratsbaum_freistellen.py`) |
| `assets/audio/` | `musik/`, `geraeusche/` (noch leer) |
| `scenes/`, `scripts/` | Szenen und C#-Skripte |

**Herkunft der Assets:** Für jede Datei unter `assets/` steht in `docs/assets/protokoll.csv`, wie sie entstand (eigenes Skript, Fremdpaket, KI mit Werkzeug, Modell und Prompt) und unter welcher Lizenz sie steht – eine Zeile je Dateigruppe mit Glob-Muster. `tools/assets/protokoll_pruefen.py` prüft in der CI (Job „Build & Tests (ubuntu-latest)“), dass jede Datei erfasst ist und die Anzahlen stimmen, und erzeugt daraus `docs/assets/steam-ki-inhalte.md`. Warum: Lizenzfragen und die KI-Angabe bei Steam brauchen belegte Herkunft, nicht Erinnerung (Entscheidung des Nutzers, 29.10.2026; Aufbau in `docs/assets/README.md`).

**Modelle mit Unterordner:** Der Gebäudekatalog und der Code nennen ein Modell mit seinem Unterordner, ohne Endung (`"modell": "gebaeude/schmied"`, `Modell("tiere/vogel_taube")`); `Stadtansicht.Modell` lädt `res://assets/modelle/<datei>.glb`, gesucht wird nicht. Die Blender-Skripte legen jede Datei über `bausteine.modellpfad(name)` in ihren Unterordner (`unterordner`: nach Namen – `…_innen`, `natur_…`, `figur_…`, `karren_…`, `vogel_…`, `tier_…` – und den Listen `UMLAND_MODELLE` und `REQUISITEN_MODELLE`, sonst `gebaeude/`); ein neues Umland- oder Requisitenmodell gehört in die passende Liste. Der Kern bekommt nur Texte, nie Pfade; die Tests finden die Dateien über `Datenpfad.Wirtschaft()` und `Datenpfad.Karte(kennung)`, `Stadtkarte.AusDatei(pfad)` den Katalog unter `daten/` neben `karten/`.

## Teildateien nach Bereich

Die ausführliche Beschreibung liegt in `docs/architektur/`. Ein Mitglied liest diese Übersicht und die Datei seines Bereichs, die übrigen nur bei Bedarf. Neues kommt in die passende Teildatei, diese Übersicht bleibt kurz.

| Datei | Inhalt | Bereich |
|---|---|---|
| [kern.md](architektur/kern.md) | Zeitsystem, Wirtschaft, Befehle, Stände, Wohnen, Schiffahrt, Jahreszeiten, Bauen, Stadtrat, Mehrspieler, Speichern und Laden | kern |
| [kern-gesellschaft.md](architektur/kern-gesellschaft.md) | Dynastie, Stadtgericht, Berufe und Betriebsarten, Gesundheit, Aktionspunkte, Wachwesen, Bankwesen, Glauben, Unterwelt, Wappen | kern |
| [spiel-ki.md](architektur/spiel-ki.md) | KI-Rivalen, Bürger-KI (Einwohner) | spiel-ki |
| [darstellung.md](architektur/darstellung.md) | Kamera, Licht, Räume und Innenansichten, Sonderbauten, Pixel-Look, Wetter, Vögel, private Texturen | darstellung |
| [modelle.md](architektur/modelle.md) | Blender-Skripte, Material-Bibliothek, Relief-Texturen, Kanten | darstellung (Modelle) |
| [stadtplan.md](architektur/stadtplan.md) | Liegenschaften und Stadtplan, Kartenwerkstatt, Gelände, Wasser, Felsen, Wege, Karren, Natur | darstellung, leveldesign, kern |
| [oberflaeche.md](architektur/oberflaeche.md) | Tafeln, Fenster, Einstellungen, Ton, Meldungen, Ladebildschirm | oberflaeche, sound |
| [infra.md](architektur/infra.md) | Fremd-Erweiterungen (GDExtension) selbst bauen | infra |
