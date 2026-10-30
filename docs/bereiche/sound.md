# Bereich Sound – Offen und Entscheidungen

Geräusche, Musik, Stimmen, Audio-Busse. Neue Einträge unten anhängen, keine Nummern.

## Offen

Plan (Schritte in dieser Reihenfolge):
- Schritt 3: Ansagen des Haushofmeisters (Nutzerentscheidung 29.10.2026): eine feste, förmlich-alte Stimme („Herr, Euer Sohn ist erkrankt.“, bei einer Spielerin „Herrin“), etwa 20 wichtige Ereignisse (Tod, Krankheit, Geburt, Heirat, Aufstieg in Stand oder Meisterstufe, Wahl, Werkstatt steht still, Lager leer, Kasse knapp, Diebstahl, Feuer …), Mindestabstand, abschaltbar. Sätze vorab erzeugt, ohne Namen (der Name steht in der Textmeldung). Vorbild nur das Prinzip des Beraters aus Stronghold, keine fremden Sätze. Im Gerüst vorbereitet: `KlangDienst.AnsageSpielen` (eine zur Zeit, Warteschlange bis 2, ältere verfallen, 8 s Abstand) auf dem Bus Stimmen und Schalter „Ansagen“ im Reiter Ton (Standard an)
- Schritt 4: Umgebung nach Zoom und Tageszeit (Schleifen mit Überblendung)
- Schritt 5: Wetter und Jahreszeiten
- Schritt 6: Glocken, Geräusche an Orten (Werkstätten, Markt, Hafen) und Innenräume
- Schritt 7: Lautheit angleichen und Ducking (z. B. Musik leiser bei Ansagen)
- Schritt 8: Musik (ACE-Step-Test)
- Quellen: freesound.org nur CC0, Kenney (CC0); Lücken mit Stable Audio Open (Grafik-PC, der Teamleiter erzeugt). Nicht: Sonniss (das Repository ist öffentlich, Rohdateien dürfen nicht weitergegeben werden), BBC-Sammlung, Modelle mit NC-Lizenz.
- Ein Test der Umrechnung `KlangDienst.ProzentZuDb` fehlt, weil sie im Spielordner liegt und die Tests nur den Kern kennen; ginge mit einer Kopie im Kern oder einer Testszene.

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
| Bearbeitung | keine: kein Tonwerkzeug im Container (kein ffmpeg/sox, Python-Pakete durften nicht nachgeladen werden); Lautstärke und Tonhöhe je Ereignis in der Tabelle von `KlangOberflaeche.cs`. Offen: Lautheit nach Gehör angleichen (Schritt 7) | 29.10.2026 |
| Ereignisse | Klick, Reiter, FensterAuf, FensterZu, Muenzen, Fehler, Meldung, Siegel, Bauen, Tempo; Hover stumm | 29.10.2026 |
| Einbau | zentral: `NodeAdded` für jeden Knopf, `Fensterrahmen`, `Hauptszene.Protokollieren` (✗, ⚠, Zahlungssätze); Siegel in `RatAnzeige`, Bauen bei `Beauftragt`, Tempo in `PauseUmschalten`/`TempoSetzen`. Münzen erkennt die Meldung am Betrag in der Währung plus Kaufwort (kein Kernereignis vorhanden) | 29.10.2026 |
| Prüfung | `--klangtest=1` schreibt je Ereignis die geladenen Varianten und Längen ins Log | 29.10.2026 |

**Zum Ausprobieren (Nutzer):** Spiel starten, Einstellungen, Reiter Ton: Regler „Oberfläche“ auf 80 %. Dann nacheinander: einen Knopf klicken (leiser Ledergriff); einen Reiter wechseln (Blättern); ein Fenster öffnen und mit dem Kreuz schließen (Pergament auf, zu); etwas kaufen, z. B. einen Karren oder Lehrling (Münzen); etwas Unmögliches versuchen, z. B. ohne Geld bauen (dumpfer Holzklopfer); eine Gefahrenmeldung abwarten (kleine Glocke); im Rat einen Antrag stimmen (Siegel); ein Gebäude setzen (Holzschlag); Pause mit Leertaste und Tempo mit 1 bis 3 (Metallklick). Alle Klänge am Stück ohne Spiel: Startoption `--klangtest=1`. Urteil bitte zu: zu laut oder zu leise, passt der Klang zum Ereignis, nervt etwas bei häufigem Klicken. Die Glocke ist ein schwerer Schlag, hochgestimmt; wenn sie zu blechern klingt, tauschen wir sie.

### Echtzeit-Sprach-NPCs (PR #263)

| Thema | Entscheidung | Datum |
|---|---|---|
| Echtzeit-Sprach-NPCs | **Vorerst nicht** (Nutzerentscheidung): Stimmen bleiben vorproduziert wie die Ansagen des Haushofmeisters. Laufende Kosten je Spieler (Cloud etwa 2–7 ct je Gesprächsminute), Online-Pflicht, Lockstep-Mehrspieler und Solo-Betrieb passen nicht zum Einmalkauf; lokal erst ab 12 GB Grafikspeicher. Nur als Hintergrund festgehalten mit Architektur, Kosten, Risiken und einem Versuch in drei Stufen: `docs/ton/sprach-npcs.md` (vor einer neuen Entscheidung die Preise der Anbieter prüfen) | 30.10.2026 |
