# Bereich Sound – Offen und Entscheidungen

Geräusche, Musik, Stimmen, Audio-Busse. Neue Einträge unten anhängen, keine Nummern.

## Offen

Plan (Schritte in dieser Reihenfolge):
- Schritt 5: Wetter und Jahreszeiten
- Schritt 6: Glocken, Geräusche an Orten (Werkstätten, Markt, Hafen) und Innenräume
- Schritt 7: Lautheit angleichen und Ducking (z. B. Musik leiser bei Ansagen)
- Schritt 8: Musik (ACE-Step-Test)
- Quellen: freesound.org nur CC0, Kenney (CC0); Lücken mit Stable Audio Open (Grafik-PC, der Teamleiter erzeugt). Nicht: Sonniss (das Repository ist öffentlich, Rohdateien dürfen nicht weitergegeben werden), BBC-Sammlung, Modelle mit NC-Lizenz.
- Ein Test der Umrechnung `KlangDienst.ProzentZuDb` fehlt, weil sie im Spielordner liegt und die Tests nur den Kern kennen; ginge mit einer Kopie im Kern oder einer Testszene.
- (Umgebung) Hörurteil des Nutzers zur Hörprobe steht aus: Lautstärke, Natürlichkeit des Winds, Moderne Reste in Fes (469700: Moped, Touristen?) und in den anderen Aufnahmen, Häufigkeit der Rufe, Pferd und Handwerk (nur gemessen, nie gehört).
- (Umgebung) Lücken für Stable Audio Open (Grafik-PC): Pferdewiehern, Holzkarren, eine autofreie Gasse am Abend, Wind vom Turm, Schweine auf der Gasse; ein Stadtgrund aus Mitteleuropa statt Fes.
- (Umgebung) Vormerkung Schritt 5: Sommernacht mit Grillen und Kauz 203598, Dorfnacht 817087 (Hochpass 150 Hz), Mauersegler 478455 – nur im Sommer, über `KlangUmgebungModell.WetterFaktor` und die Jahreszeit-Tabelle.
- (Umgebung) Originale statt HQ-Vorschauen (Anmeldung bei freesound nötig): gleiche Nummern, neu bauen mit `tools/ton/umgebung_bauen.py`.
- (Umgebung) Die robots.txt von freesound.org sperrt ClaudeBot ganz; die Recherche hat einzelne, nutzerveranlasste Abrufe erlaubter Pfade gemacht. Bewertung durch den Teamleiter offen; der Bau braucht keine neuen Abrufe.
- (Umgebung) Arbeitsende 20 Uhr und Wirtshausschluss 22 Uhr stehen als Zahlen in den Kurven von `KlangUmgebungModell` (im Kern gibt es nur `Arbeitsbeginn`); falls der Kern Konstanten bekommt, dort lesen.

## Entscheidungen

### Ton-Grundgerüst (PR #246)

| Thema | Entscheidung | Datum |
|---|---|---|
| Busse | Master, Musik, Umgebung, Effekte, Oberflaeche, Stimmen in `default_bus_layout.tres`, alle an Master; Namen nur als Konstanten in `KlangDienst`, Prüfung beim Start | 29.10.2026 |
| Lautstärke | Prozent linear, 100 % = 0 dB, nie darüber; 0 % und Stumm schalten den Bus stumm; Standard 80 % | 29.10.2026 |
| Hintergrund | „Ton im Hintergrund stumm“ standardmäßig an, über die Fokus-Benachrichtigungen von Anwendung und Fenster | 29.10.2026 |
| Speicherort | `user://einstellungen.cfg`, Abschnitt `ton`, von `KlangDienst` selbst geschrieben (nicht über `Fenster`) | 29.10.2026 |
| Ansagen | `AnsageSpielen`: eine Ansage zur Zeit, höchstens 2 wartend (die älteste verfällt), 8 s Abstand, Schalter „Ansagen“ (Standard an, Schlüssel `ansagen`) | 29.10.2026 |
| Reiter | „Ton“ als siebter Reiter der Einstellungen (kleine Änderung an `EinstellungenAnzeige.cs`, Bereich Oberfläche) | 29.10.2026 |

### Oberflächenklänge (PR #252)

| Thema | Entscheidung | Datum |
|---|---|---|
| Quelle | Kenney „RPG Audio“ und „Impact Sounds“ (CC0), 29 Dateien unverändert als OGG; Original-Downloads von kenney.nl mit SHA-256-Vergleich aller Dateien (`HERKUNFT.md`), `License.txt` daneben; freesound.org nicht genutzt (Original nur mit Anmeldung). „Interface Sounds“ und „UI Audio“ klingen elektronisch und blieben draußen | 29.10.2026 |
| Bearbeitung | keine: damals kein Tonwerkzeug im Container (seit Ton Schritt 3 gibt es ffmpeg und Python mit Tonpaketen unter `~/werkzeuge/stimme/`); Lautstärke und Tonhöhe je Ereignis in der Tabelle von `KlangOberflaeche.cs`. Offen: Lautheit nach Gehör angleichen (Schritt 7) | 29.10.2026 |
| Ereignisse | Klick, Reiter, FensterAuf, FensterZu, Muenzen, Fehler, Meldung, Siegel, Bauen, Tempo; Hover stumm | 29.10.2026 |
| Einbau | zentral: `NodeAdded` für jeden Knopf, `Fensterrahmen`, `Hauptszene.Protokollieren` (✗, ⚠, Zahlungssätze); Siegel in `RatAnzeige`, Bauen bei `Beauftragt`, Tempo in `PauseUmschalten`/`TempoSetzen`. Münzen erkennt die Meldung am Betrag in der Währung plus Kaufwort (kein Kernereignis vorhanden) | 29.10.2026 |
| Prüfung | `--klangtest=1` schreibt je Ereignis die geladenen Varianten und Längen ins Log | 29.10.2026 |

**Zum Ausprobieren (Nutzer):** Spiel starten, Einstellungen, Reiter Ton: Regler „Oberfläche“ auf 80 %. Dann nacheinander: einen Knopf klicken (leiser Ledergriff); einen Reiter wechseln (Blättern); ein Fenster öffnen und mit dem Kreuz schließen (Pergament auf, zu); etwas kaufen, z. B. einen Karren oder Lehrling (Münzen); etwas Unmögliches versuchen, z. B. ohne Geld bauen (dumpfer Holzklopfer); eine Gefahrenmeldung abwarten (kleine Glocke); im Rat einen Antrag stimmen (Siegel); ein Gebäude setzen (Holzschlag); Pause mit Leertaste und Tempo mit 1 bis 3 (Metallklick). Alle Klänge am Stück ohne Spiel: Startoption `--klangtest=1`. Urteil bitte zu: zu laut oder zu leise, passt der Klang zum Ereignis, nervt etwas bei häufigem Klicken. Die Glocke ist ein schwerer Schlag, hochgestimmt; wenn sie zu blechern klingt, tauschen wir sie.

### Ansagen des Haushofmeisters (Ton Schritt 3)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ereignisse | 20 Ereignisse, die der Kern schon meldet (keine neuen erfunden): Tod (Oberhaupt, Familie), Feuer, Pest in der Stadt, Krankheit, Überfall, Einbruch, Anklage, Schulden, Wahl gewonnen/verloren, Aufstieg im Stand, Meisterprüfung, Geburt, Hochzeit, Ratssitzung, Kasse knapp, Lager leer, Lager voll, Jahresende; je 3 Sätze, je in Fassung Herr und Herrin = 60 Sätze, 120 Aufnahmen. Liste: `game/daten/ansagen.json` | 29.10.2026 |
| „Werkstatt steht still“ | Kein eigenes Ereignis: Der Kern meldet den Stillstand mit Grund; gemeldet werden nur „Rohstoff fehlt“ (Lager leer) und „Lager voll“. Umbau und Reparatur nach Anschlag sind gewollt bzw. schon als Feuer angesagt | 29.10.2026 |
| Fassung | Das Spiel kennt das Geschlecht der Person (`Person.Geschlecht`), nicht „der Spielerfigur“ – gilt das Oberhaupt des Hauses zur Zeit der Meldung. Nach einem Erbfall also die Anrede des Erben. Die zwei Fassungen unterscheiden sich nur in der Anrede; Sätze meiden gegenderte Wörter (Sohn, Gemahlin, ihm/ihr) | 29.10.2026 |
| Erkennung | Der Kern meldet nur Text; `KlangAnsagenErkennung` ordnet Wortlaute per Regex zu (ohne Änderung im Kern). Eigenes Haus: Regexgruppe `wer` gegen Betriebe und Inhaber des Spielers, bei Familienereignissen das Konto aus dem Familienbuch. Aufstieg im Stand, Jahresende, Ratssitzung und Wahl kommen als Ereignis aus der `Hauptszene`. Ein Test lässt echte Kernmeldungen erkennen, damit eine geänderte Formulierung im Kern auffällt | 29.10.2026 |
| Rang und Pause | Rang 1 Tod, 2 Feuer/Pest, 3 Krankheit/Überfall/Einbruch/Anklage/Schulden, 4 Wahl/Aufstieg/Meister/Geburt/Hochzeit, 5 Ratssitzung/Kasse/Lager leer, 6 Lager voll/Jahresende. 20 s Echtzeit Pause nach dem Ende einer Ansage, nur die wichtigste wartet (gleicher Rang: die neuere), Verfall nach 45 s, nichts wird unterbrochen. Je Ereignis eine Wiederholsperre (60–180 s). Ersetzt die alte Warteschlange (2 Plätze, 8 s) im `KlangDienst` | 29.10.2026 |
| Fehlende Datei | still übersprungen, Zeile im Log nur im Entwicklermodus (Entwicklerbefehle erlaubt) oder mit `--ansageplatzhalter=1` | 29.10.2026 |
| Platzhalter | `--ansageplatzhalter=1`: Kenney-Glocke (`impact_bell_heavy_003`) statt der fehlenden Aufnahme, keine Fremdaufnahme | 29.10.2026 |
| Werkzeug | `tools/ton/ansagen_liste.py` gibt die Sätze als CSV oder JSON aus (`--fassung`, `--fehlende`, `--pruefen`), ruft den Sprachdienst nicht auf | 29.10.2026 |
| Prüfung | `--ansagetest=<liste|alle|dateien>[:herrin]` löst Ansagen aus und schreibt die `ANSAGE …`-Zeilen ins Log. Im Entwicklerfenster gibt es keine Schaltfläche (das gehört in `EntwicklerAnzeige`, Bereich Oberfläche); die dortigen Knöpfe Tod, Krankheit, Geburt, Heirat, Diebstahl, Feuer und Hausbrand lösen die echten Meldungen aus und damit die Ansagen | 29.10.2026 |

**Zum Ausprobieren (Nutzer):** Solange keine Aufnahmen da sind: Spiel mit `--ansageplatzhalter=1` starten, F12 (Entwicklerfenster), Reiter Familie: „Sterben lassen“ bei einem Angehörigen, „Sohn“, „Heiraten“, dann „Feuer im Betrieb“ und „Hausbrand“. Pro Ereignis klingt eine Glocke, nie öfter als alle 20 Sekunden; bei dichter Folge entfällt Unwichtiges (siehe Log). Der Schalter steht unter Einstellungen, Ton, „Ansagen des Haushofmeisters“.

### Echtzeit-Sprach-NPCs (PR #263)

| Thema | Entscheidung | Datum |
|---|---|---|
| Echtzeit-Sprach-NPCs | **Vorerst nicht** (Nutzerentscheidung): Stimmen bleiben vorproduziert wie die Ansagen des Haushofmeisters. Laufende Kosten je Spieler (Cloud etwa 2–7 ct je Gesprächsminute), Online-Pflicht, Lockstep-Mehrspieler und Solo-Betrieb passen nicht zum Einmalkauf; lokal erst ab 12 GB Grafikspeicher. Nur als Hintergrund festgehalten mit Architektur, Kosten, Risiken und einem Versuch in drei Stufen: `docs/ton/sprach-npcs.md` (vor einer neuen Entscheidung die Preise der Anbieter prüfen) | 30.10.2026 |

### Aufnahmen des Haushofmeisters (PR #267)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stimme | KI-Stimme statt eigener Aufnahme (Nutzer): reiner **Klon** mit Qwen3-TTS, Vorlage `haushofmeister_probe2.wav` (eigene KI-Stimme, keine echte Person) mit ihrem Text als `ref_text`. Keine Stimmbeschreibung (VoiceDesign) und kein Seed-VC: beides klang in der Probe nach Nachhall bzw. Roboter | 30.10.2026 |
| Betonung | nur über Satzzeichen im Sprechtext (`tools/ton/ansagen_sprechtext.json`, Fassung „B-zeichen“ der Probe): „…“ getragen (Tod, Krankheit, Pest, Anklage, Schulden, Wahl verloren, Kasse), „!“ freudig (Wahl gewonnen, Stand, Meister, Geburt, Hochzeit), „–“ und „!!“ drängend (Feuer, Überfall, Einbruch), sachlich mit Komma (Rat, Lager, Jahresende). Die Wörter bleiben die der Satzliste; das Skript prüft das | 30.10.2026 |
| Tempo | ×1,15 für alle mit ffmpeg `atempo` (WSOLA), nicht rubberband; danach mono, 48 kHz, −18 LUFS, 0,2 s Stille | 30.10.2026 |
| Auswahl | je Satz 3 Takes, bei Fehlschlag bis zu 3 weitere; gültig nur mit Whisper-WER 0 (turbo), ohne Übersteuerung, ohne Pause über 1,2 s und mit plausibler Länge; davon die höchste ECAPA-Ähnlichkeit zur Vorlage. Whisper schreibt „darnieder“ (Krankheit 2) immer als „danieder“ oder „da nieder“; das zählt als gleich (`GLEICHWERTIG` im Skript), sonst wäre der Satz nie bestanden. Ergebnis: alle 120 im ersten Anlauf (3 Takes), WER 0, Ähnlichkeit 0,62–0,83 (Mittel 0,75) | 30.10.2026 |
| Format | OGG Vorbis Qualität 5, mono, 48 kHz; die WAVs liegen nur auf dem Entwicklungsrechner (`~/werkzeuge/stimme/haushofmeister_wav/`) | 30.10.2026 |
| Vorlage im Repository | `tools/ton/quellen/haushofmeister_probe2.wav` mit eigener Protokollzeile (Prüfordner `tools/ton/quellen/` im Protokollskript ergänzt): ohne sie ließe sich kein einzelner Satz mit derselben Stimme nachträglich neu erzeugen. Wird nicht ausgeliefert (liegt außerhalb von `game/`) | 30.10.2026 |
| Nachbessern | Gefällt ein Satz nicht, nur diesen neu erzeugen: `tools/ton/ansagen_erzeugen.py --nur <name> --neu` (Spielbuch „Ton und Ansagen“) | 30.10.2026 |

### Umgebung nach Zoom und Tageszeit (Ton Schritt 4, PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wo | Nur in der Partie (Kind der Hauptszene, auch Probepartie), nicht im Hauptmenü, nicht in der Kartenwerkstatt. Laden im Hintergrund schon während des Ladebildschirms, Start nach dem Ladebildschirm mit 3 s Einblendung | 31.10.2026 |
| Schichten | 9 Schleifen, Längen ohne gemeinsamen Takt (53–101 s), Start an zufälliger Stelle: Stadtleben Tag (Fes-Gerberei 469700, −6 dB), Gemurmel nur nah (360699, Tiefpass 3 kHz, −16 dB), Abend wie aus Schenken (187199, Tiefpass 1 kHz, −13 dB, nah voll), Morgenchor (573080, −12 dB), Spatzen (266601, −20 dB), Abendgesang (240587, −15 dB), Nacht (Synthese, −17 dB), Wind leicht (Synthese mit unregelmäßigen Böen, −8 dB), Wind mittel (135193 ohne die Pfeifbö, −10 dB); alle Dateien −26 LUFS | 31.10.2026 |
| Uhren | Stadtleben nach der Uhr (schwach 0,3 ab der Dämmerung – die Stadt erwacht –, voll 08:30–19:00, Abend 20:30–22:00, danach 0,15), Vögel und Nacht nach der Bezugsstunde des Lichts. Kurven stetig (Trapeze mit Smoothstep), keine harte Schwelle | 31.10.2026 |
| Glättung | Tageszeit ungeglättet (sonst hinkt der Klang bei 32x eine Spielstunde dem Licht nach); nur die Amplitude je Schicht höchstens 2 je Sekunde. Zoom 0,3 s, Innenart 0,6 s, Pause 1 s. Sprung (rückwärts, über max(45, 3 × Vorschub + 5) Spielminuten im Bild, neue Jahreszeit): 3 s Blende | 31.10.2026 |
| Zoom | Nähe N = 1 − smoothstep(45, 135, S) (Startzoom 95: 0,42); Höhe H = smoothstep(min(95, Smax − 20), Smax, S) am wirklichen Höchstzoom der Karte; Stadt bis −8 dB und Tiefpass 3,5 kHz; Wind leicht 0,05 + 0,95 H, Wind mittel ab H 0,55. In Raum und Dom N = 0,5, H = 0 | 31.10.2026 |
| Jahreszeit | Morgenchor und Abendgesang Frühling 1, Sommer 0,8, Herbst 0,3, Winter 0,1; Spatzen Herbst 0,8, Winter 0,6; Hahn und Stadt unverändert. Anschluss Schritt 5: `KlangUmgebungModell.WetterFaktor` je Schicht | 31.10.2026 |
| Pegel | Bei Regler 80 %: Tag am Startzoom etwa −33,5 LUFS, Morgen −36, Abend −39, Nacht −42,5 (9 dB unter dem Tag); Summe gedeckelt bei −30 LUFS vor dem Regler; Ansagen (−18 LUFS) bleiben gut 13 dB darüber | 31.10.2026 |
| Innen, Pause | Raum −14 dB, Tiefpass 500 Hz (24 dB/Okt.); Dom (Diorama) „innen leicht“ −6 dB, 1,5 kHz; jede andere Innenansicht zählt als Raum. Pause −8 dB, Tageszeit steht, Rufe dreimal seltener (Empfehlung der Kritik statt „Rufe aus“, damit lange Pausen nicht nur die nackte Schleife zeigen). Fokus verloren und Hintergrund stumm: keine Rufe | 31.10.2026 |
| Rufe | 8 Arten, 38 Dateien: Hahn (Morgen, tagsüber selten), Hund (am Tag nah selten, abends/nachts nur fern, im Mittel 150 s), Waldkauz (Nacht, nicht solange die Stadt lebt, 90 s), Amsel (nur abends), Pferd und Geflügel (Tag, bis Zoom 100, ab 70 fern), Passanten (Schritte, nah bis Zoom 105, alle ~15 s), fernes Handwerk (Schmiede 137836, Arbeitszeit). Abstände in Echtzeit (Mindestabstand, Mittel / Fenstergewicht), 5 s zwischen allen Rufen, je ein Platz nah und fern, Seite ±0,8, Tonhöhe ±4 %, Pegel ±3 dB; eigener Zufall (Lockstep unberührt) | 31.10.2026 |
| Abspielen | Rufe über zwei feste Spieler auf eigenen Bussen mit Panner und Tiefpass statt AudioStreamPlayer3D (unabhängig von Raum- und freier Kamera); Schleifen über `KlangDienst.SchleifePegelSetzen` (nur bei Änderung über 0,1 dB), 10 s stumme Schichten angehalten | 31.10.2026 |
| Quellen | freesound nur CC0, Lizenz auf jeder Klangseite geprüft, HQ-Vorschau mit SHA-256; keine craigsmith-Aufnahmen (unklare Rechtekette); kein Grillen- oder Sommermaterial in Schritt 4; Ordner `game/assets/audio/umgebung/` (Auftrag) | 31.10.2026 |
| Bau | `tools/ton/umgebung_bauen.py` aus `tools/ton/umgebung_schnitte.json`: Resampling der ganzen Datei, Filter, Ausschnitt, Naht gleicher Leistung, lineare Verstärkung (kein dynamisches loudnorm), OGG q4 bitexact (byte-gleich wiederholbar); zusammen 9,4 MB | 31.10.2026 |
| Prüfung | `--umgebungtest=1` (Rückgabewert, headless), `--umgebungtest=2` (Log), `--umgebungfahrt=1` (Hörprobe); Entwicklerreiter Klänge, Abschnitt Umgebung | 31.10.2026 |

**Zum Ausprobieren (Nutzer):** Im Godot-Editor unter Projekt → Projekteinstellungen → Ausführen das Feld „Main-Run Argumente“ setzen (mit ` -- ` davor), dann F5:
- Morgen nah: ` -- --startstunde=5 --tempo=1` und mit dem Mausrad ganz hineinzoomen (Vogelchor, Hähne, leise erwachende Stadt).
- Mittag herausgezoomt: ` -- --startstunde=12` und ganz herauszoomen (Stadt fern und dumpf, Wind), dann wieder hinein (Gemurmel, Schritte, Pferd, Gänse).
- Abend und Nacht: ` -- --startstunde=20 --tempo=4` (Schenkengemurmel, Amsel, ferner Hund, später Kauz).
- Innen: ` -- --startstunde=12 --innen=1 --raum=1` (dumpf und leise); Pause mit der Leertaste (leiser, seltener).
- Entwicklerfenster F12 → Reiter Klänge → Abschnitt Umgebung: was gerade wie laut läuft, jede Datei mit ▶, „jetzt rufen“ je Art (mit ` -- --entwickler=1`).
Urteil bitte zu: Lautstärke gegenüber den Ansagen, Wind natürlich?, hört man Modernes (Moped, Motor, Sprache) im Stadtleben, zu viele oder zu wenige Rufe, nervt eine Wiederholung bei langer Pause.
