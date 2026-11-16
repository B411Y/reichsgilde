# Bereich sound – Archiv der gemergten Entscheidungen

Abschnitte gemergter Branches und PRs, **wörtlich** aus [../sound.md](../sound.md) verschoben (16.11.2026). Neue Einträge gehören nicht hierher.

## Entscheidungen (gemergt, verschoben 16.11.2026)

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

### Regen im Freien statt unter der Blechhütte (nach Nutzerurteil im Spiel, PR #277)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Nutzerurteil im Spiel: Der Regen war zu weit im Hintergrund und klang wie Tropfen auf nassem Blech. Befund: 836064 enthielt laut Titel „Metal Roof, Barbeque“, 184627 („Dach und Fenster“) und 213329 (Dach) waren Dachaufnahmen mit Schwerpunkt 0,6–1 kHz; die Regenschichten lagen mit −10/−8/−14 dB unter dem Stadtleben (−6 dB) | 31.10.2026 |
| Hauptschicht im Freien | `regen_leicht` aus 399277 (Mjeno, Regen im Garten auf Gras und Erde, 2–58 s) und `regen_stark` als Mischung aus 405630 (Anthousai, Regenschauer im Freien, Oregon, 14–58 s, −2 dB) und 812615 (Alex_hears_things, Platzregen auf Backsteinboden, 0,5–44,5 s) für Körper; beide **CC0**. Pegel angehoben: Grundpegel −4 bzw. −3 dB (vorher −10/−8), die Mischung bleibt unter dem Deckel (−30 LUFS). Der Mix-Zweig des Bauskripts bekommt bei Schleifen die Naht über die Summe | 31.10.2026 |
| Dächer nur bei Häusern | `regen_dach` (466241 richwise, „Soft rain on a tile roof“, Ziegeldach vom Dachboden, 100–190 s, Hochpass 180 Hz, Tiefpass 8 kHz) und neu `regen_traufe` (622847 ankealtd, Regen und Tropfen aus der Dachrinne, 0,3–19,7 s, Hochpass 400 Hz): Gewicht nur bei Stadtanteil im Umkreis (`Glatt(0,15; 0,65; Stadt)` bzw. `(0,3; 0,8)`, auf Wiese und im Wald null) und nach der Nähe der Kamera (Traufe mit dem Quadrat der Nähe, also nur ganz nah). Grundpegel −9 bzw. −10 dB | 31.10.2026 |
| Entfernt | Die drei CC-BY-Aufnahmen 184627 (Erbsland-Music), 213329 (LolitaPerdurabo) und 836064 (newlocknew) samt Namensnennung; die Regenschichten sind jetzt alle CC0, die Danksagungsliste wird kürzer | 31.10.2026 |
| Prüfung | Selbsttest: Dach und Traufe nahe an Häusern hörbar, auf Wiese und im Wald null, Traufe ohne Regen null; **Regen im Freien mindestens 6 dB (Regen 0,5), 9 dB (0,75) und 10 dB (Guss) über dem Rest der Umgebung**; Regen auch auf der Wiese weit herausgezoomt deutlich. Hörprobe: `--umgebungfahrt=5` (80 s: Wiese, Gassen, starker Regen) | 31.10.2026 |

### Feuerglocke beim Blitzschlag (PR #288)

| Frage | Entscheidung |
|---|---|
| Klang | `glocken/glocke_sturm.ogg` (50 s, nahtlose Schleife): **Klangprobe A** (Wahl des Nutzers, 2.11.2026) – ein Schlag der großen Glocke Noteman (klankbeeld, CC BY 4.0, Namensnennung in `HERKUNFT.md` und im Protokoll), über 1,8 kHz gedämpft, abwechselnd um den Faktor 1,12 und 1,06 höher gesetzt (tiefer als die erste Fassung mit 1,5), ein Schlag alle 1,15–1,3 s; Werkzeug `tools/ton/glocke_sturm_bauen.py` (Blender, audaspace + numpy). Verworfen: die erste, hochgesetzte Noteman-Fassung (zu penetrant) und Klangprobe B aus dem Geläut des Kölner Doms (CC0). |
| Wo, wann | Die nächste Kirche (Kirche, Dom, Kölner Dom; sonst der Markt) in 25 m Höhe, 5–9 s nach dem Einschlag, nur wenn es dann noch brennt; läutet als Schleife, bis kein Blitzfeuer mehr brennt, und klingt dann in 4 s aus. 3D (`AudioStreamPlayer3D` an der Stadt, Bus Umgebung, −4 dB, bis 45 m voll, dann leiser mit der Entfernung, Reichweite 1.000 m); verstummt beim Verlassen der Partie und beim Laden; nur ein Läuten zugleich. |

### Pferdeklang: Schnauben in der Stadt, Hufe am Karren nach Untergrund (Nutzer, 04.11.2026, PR #304)

| Frage | Entscheidung |
|---|---|
| Vorher | Drei 3,5-s-Ausschnitte aus „Paso Fino Horse“ (173159), vermutlich eine laute, nahe Aufnahme mit starkem Grundrauschen und Wiehern, auf −20 LUFS hochgezogen; die Rufart stand mit −4 dB nah und −10 dB fern 8–11 dB über allen anderen Tieren, Mittel 120 s. |
| Umgebungsruf „Pferd“ | Nur noch Schnauben und Wiehern (Wahl des Nutzers nach den Hörproben): `rufe/pferd_1..6.ogg` = drei leise Schnauber (437111 craigsmith), zwei weitere (538697 bruno.auzet), ein fernes weiches Wiehern (636554 L.Finck), alle CC0; −24 LUFS (Wiehern −26), Spitze höchstens −3 dBTP. Keine Hufe im Umgebungsruf. |
| Abspielwerte | `KlangUmgebungModell`: 6 Varianten, Pegel nah −14 dB, fern −19 dB, Mindestabstand 90 s, Mittel 240 s (vorher 3, −4, −10, 40, 120; erste Fassung dieses PR 60/150/−12/−18). Das Wiehern ist eine von sechs Dateien, also selten. |
| Hufe am Karren | Nur ein **fahrender Pferdekarren** (`KarrenArt.Pferd`; früher Kriegskarren genannt) macht Hufe; Ochsen- und Handkarren bleiben still (es gibt dafür keine Klänge). Der Klang kommt vom Karren: `AudioStreamPlayer3D` auf dem Bus UmgebungDraussen, Schleife (`ogg.Loop`), Ort um den Versatz Karren minus Blickpunkt neben der Kamera (so stimmen Abstand und Seite mit dem Bild, auch mit freier Kamera). Voll bis 10 m, dann umgekehrt zum Abstand, hörbar bis 0,9 × Zoom Meter vom Blickpunkt (30–90 m), −5 dB Basis plus bis −10 dB bei der größten Ansicht. **Höchstens drei Karren zugleich** (die nächsten, wer einen Platz hat, behält ihn); Ein- und Ausblenden 0,6 s; in Innenansichten (Raum, Dom) still. Code: `KlangKarren.cs` (Klang), `Stadtansicht.KarrenKlang.cs` (Auswahl, Untergrund). Prüfen: `--karrenklangtest=1` (Protokollzeilen `[karrenklang]`, siehe `docs/STARTOPTIONEN.md`). |
| Untergrund | Gelesen wird nur der Kern: `Wegebau.WegstelleBei` (nächster Weg, höchstens halbe Breite + 2,5 m daneben) und `BelagBei`; alle 0,25 s. **Pflaster** → `karren/hufe_pflaster.ogg` (Hufe auf Pflaster, 437103 craigsmith, 15,1 s); **Kies, Fahrweg, Trampelpfad** und alles abseits der Wege (Wiese, Feldweg ohne Weg der Karte, Furt) → `karren/hufe_erde.ogg` (gedämpfte Hufe auf Erde, 479698 craigsmith, 14,5 s). Abseits aller Wege bleibt der letzte Untergrund. Wechselt er, blendet der Klang aus und mit der anderen Datei wieder ein. Kies zählt zu Erde (weicher Schotter), eine Entscheidung dieses PR; leicht zu ändern in `UntergrundUnter`. |
| Hörproben | Pflaster, Erde und nur Schnauben/Wiehern lagen dem Nutzer vor; Wahl am 04.11.2026 wie oben. Der Klang selbst wurde nicht angehört: Klicktest in Köln (Vulkan, Grafikkarte) mit Tonmitschnitt und Protokoll – Pferdekarren auf Fahrweg (Rheingasse) → Erde, nach dem Pflastern derselben Stelle → Pflaster; Umland-Pferdekarren auf der Bensberger Straße und der Deutzer Freiheit (Fahrweg) → Erde; Hörurteil des Nutzers steht aus. |

### Töne der Benachrichtigungen (PR #322)

| Thema | Entscheidung | Datum |
|---|---|---|
| Töne (ersetzt am 11.11.2026, siehe „Meldungstöne neu“) | Vier neue Töne rein rechnerisch erzeugt (`tools/ton/meldungstoene.py`, Standardbibliothek): `siegel_neu_1/2.wav` (weicher Wachsklopfer) und `pergament_1/2.wav` (Rascheln); Ereignisse `MeldungSiegel`, `MeldungBrief` in `KlangOberflaeche`, die jetzt auch `.wav` lädt. Brief bei Gefahr dazu die leise Glocke, „✗“ bleibt der Holzklopfer. Lizenz „Alle Rechte vorbehalten“, Protokollzeile und `docs/assets/prompts/meldungstoene.md`. Nicht abgehört (Hörurteil offen). | 05.11.2026 |

### Türklang beim Betreten und Verlassen (Wahl des Nutzers, 11.11.2026, Branch phase5/tuerklang)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wahl | Tür auf = `tuer_auf` (eigene Aufnahme des Nutzers, bearbeitet in SounCraft); Tür zu = `tuer_zu_2` (Freesound 402465, LampEight, CC0) | 11.11.2026 |
| Einbau | Zwei neue Ereignisse `TuerAuf`/`TuerZu` in `KlangOberflaeche` (Bus Ui wie die übrigen Oberflächenklänge), je eine Datei, −5 dB, Streuung 0,02; Dateien `tuer_auf.ogg`, `tuer_zu.ogg` im Ordner der Oberflächenklänge, gebaut mit `tools/ton/tuer_einbauen.py` | 11.11.2026 |
| Pegel | Beide auf −18,0 LUFS (nur Verstärkung, kein Begrenzer; wie drop_leather −18,2, planken −19,3, Meldungstöne −20), True Peak −3,4 und −1,5 dBTP; tuer_auf wurde dafür um 3,7 dB leiser, tuer_zu blieb | 11.11.2026 |
| Auslöser | Eine Stelle: `Uebergangsblende.Starten`, nach angenommenem Start. Betreten = Tür auf (mit Handschuh fällt das auf das Drücken der Klinke, weil `KlinkeStarter` im selben Aufruf davor läuft; ohne Handschuh auf den Start der Blende hinein), Verlassen = Tür zu beim Start der Blende heraus. Zuordnung in `KlangTuerModell.Fuer` (Test) | 11.11.2026 |
| Lizenz | tuer_auf: eigenes Werk des Nutzers, Alle Rechte vorbehalten; tuer_zu: CC0 | 11.11.2026 |
| Offen | Nicht abgehört im Spiel: Hörprobe und Pegelvergleich zu Klick und Fenster (Nutzer) | 11.11.2026 |

### Kirchenglocken nach Entfernung (Befund 10.11.2026, Branch fix/glocken-entfernung)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | Es läutete eine einzige Glocke am Dom, auf dem Land nur 12 dB leiser (Tiefpass bis 3,5 kHz): Im Umland klang sie fast wie in der Stadt; die übrigen Kirchen Kölns läuteten nie | 10.11.2026 |
| Hörer | Blickpunkt der Kamera am Boden (wie bei allen Umgebungsklängen und dem Regen), Abstand in Spielmetern; die Karte ist etwa 1 : 10 verkleinert | 10.11.2026 |
| Kurve | `KlangGlockenModell.cs` (reine Rechnung, kein Godot): je Kirche Pegel = Quellpegel − 9 dB · log2(Abstand / Nahbereich), im Nahbereich voll. 9 statt 6 dB (Freifeld) je Verdopplung: Häuser, Boden und Luft schlucken zusätzlich, und die Karte ist kurz; bei 6 dB wäre das Umland kaum leiser als die Stadt. Pfarrkirche Nahbereich 30 m, Dom 60 m und +3 dB (trägt weiter) | 10.11.2026 |
| Summe | Alle Kirchen der Karte (Dom, `kirche`, `kirche_…`) addieren sich nach **Leistung**, nicht nach Pegel (zwei gleiche +3 dB), gedeckelt bei 0 dB (Glocke aus nächster Nähe) und nie leiser als **−36 dB**: Glocken tragen weit, im Umland sind sie leise, aber nicht weg. Seite = nach Leistung gewichtet | 10.11.2026 |
| Tiefpass | Je Kirche 16 kHz im Nahbereich bis 2,5 kHz beim Sechsfachen (Pfarrkirche 180 m, Dom 360 m), mehrere nach Leistung im Logarithmus gemittelt | 10.11.2026 |
| Nachführen | Während des Läutens (bis 30 s) folgen Pegel (12 dB/s), Seite und Tiefpass (6 kHz/s) dem Blickpunkt; Zeiten und Auslöser unverändert | 10.11.2026 |
| Werte (Köln, sechs Kirchen) | Stadtmitte (0\|0) −1,2 dB, Stadtrand (0\|200) −13,5 dB, Umland (0\|400) −22,7 dB, fernes Umland (0\|800) −30,8 dB; nur eine Kirche (Martin) −6,4/−27,3/−35,0/−36,0; nur der Dom −5,1/−18,4/−24,8/−32,3 | 10.11.2026 |
| Prüfung | `--glockentest=1` (dB-Werte, Tiefpass), Tests `KlangGlockenTests` (Kurve, Dom, Summe, Mindestpegel, Seite), der Selbsttest der Umgebung prüft nah gegen 400 m. **Nicht gehört** (kein Fenster, keine Ohren): Hörurteil des Nutzers zu Pegel im Umland (−23 dB gewollt „leise“?) und zum Stadtrand steht aus | 10.11.2026 |

### Wolfsheulen bei Einbruch der Nacht (Branch phase5/wolfsheulen, PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wunsch | Einmal je Nacht beim Wechsel zur Nacht, das ganze Jahr, fern, von außerhalb der Stadt; keine weiteren Wolfslaute, kein Rudel, keine Einstellung (Nutzer) | 14.11.2026 |
| Klang | `rufe/wolf_nacht.ogg`: NaturesTemper 398430 (CC0), Ausschnitt „wolf_k1“, vom Nutzer gewählt; −26 LUFS, ohne Begrenzer, Spitze −15,8 dBFS | 14.11.2026 |
| Auslöser | `KlangUmgebungModell.WolfPlaner`: Die Bezugsstunde des Lichts steigt über 21 („dunkel“, folgt der Jahreszeit: 18:45 Frühling, 21:00 Sommer, 18:30 Herbst, 16:35 Winter). Höchstens einmal je Spieltag (Tageszeichen fällt erst vor 12 Uhr), nicht bei der ersten Messung (Laden), nicht im Hintergrund, nicht in der Sprungblende, nicht bei mehr als 20 Spielminuten je Bild (Spulen); bei 32x genau eine Überquerung | 14.11.2026 |
| Wiedergabe | Eigener Spieler auf dem Fernbus der Rufe (`UmgebungRufFern`, unter Draußen: in Räumen leiser und dumpf wie die übrigen Außenklänge); Seite zufällig 0,5–0,9 links oder rechts, −4 dB mit Panner-Ausgleich, Tonhöhe ±2 % | 14.11.2026 |
| Prüfung | `KlangWolfTests` (7 Tests, alle Jahreszeiten: einmal, nicht früher, nicht nach Laden, nicht doppelt, Zeitraffer, Sprung/Blende/Hintergrund); `--umgebungfahrt=6` headless: eine Zeile `UMGEBUNG WOLF` bei 18:45, beim Rückspulen am selben Abend keine zweite. `--klangtest=1` gehört nur den Oberflächenklängen und zeigt das Ereignis nicht (dafür `--umgebungfahrt=6`). **Nicht gehört** (Pegel im Nachthintergrund: Nutzerurteil) | 14.11.2026 |
