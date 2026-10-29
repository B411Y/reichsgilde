# Bereich Sound – Offen und Entscheidungen

Geräusche, Musik, Stimmen, Audio-Busse. Neue Einträge unten anhängen, keine Nummern.

## Offen

Plan (Schritte in dieser Reihenfolge):
- Schritt 2: Oberflächenklänge (Klick, Papier, Siegel) mit `AudioStreamRandomizer`-Varianten über `KlangDienst.OberflaecheSpielen`
- Schritt 3: Ansagen des Haushofmeisters (Nutzerentscheidung 29.10.2026): eine feste, förmlich-alte Stimme („Herr, Euer Sohn ist erkrankt.“, bei einer Spielerin „Herrin“), etwa 20 wichtige Ereignisse (Tod, Krankheit, Geburt, Heirat, Aufstieg in Stand oder Meisterstufe, Wahl, Werkstatt steht still, Lager leer, Kasse knapp, Diebstahl, Feuer …), Mindestabstand, abschaltbar. Sätze vorab erzeugt, ohne Namen (der Name steht in der Textmeldung). Vorbild nur das Prinzip des Beraters aus Stronghold, keine fremden Sätze. Im Gerüst vorbereitet: `KlangDienst.AnsageSpielen` (eine zur Zeit, Warteschlange bis 2, sonst verwerfen, 4 s Abstand) auf dem Bus Stimmen und Schalter „Ansagen“ im Reiter Ton (Standard an)
- Schritt 4: Umgebung nach Zoom und Tageszeit (Schleifen mit Überblendung)
- Schritt 5: Wetter und Jahreszeiten
- Schritt 6: Glocken, Geräusche an Orten (Werkstätten, Markt, Hafen) und Innenräume
- Schritt 7: Lautheit angleichen und Ducking (z. B. Musik leiser bei Ansagen)
- Schritt 8: Musik (ACE-Step-Test)
- Quellen: freesound.org nur CC0, Kenney (CC0); Lücken mit Stable Audio Open (Grafik-PC, der Teamleiter erzeugt). Nicht: Sonniss (das Repository ist öffentlich, Rohdateien dürfen nicht weitergegeben werden), BBC-Sammlung, Modelle mit NC-Lizenz.
- Ein Test der Umrechnung `KlangDienst.ProzentZuDb` fehlt, weil sie im Spielordner liegt und die Tests nur den Kern kennen; ginge mit einer Kopie im Kern oder einer Testszene.

## Entscheidungen

### Ton-Grundgerüst (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Busse | Master, Musik, Umgebung, Effekte, Oberflaeche, Stimmen in `default_bus_layout.tres`, alle an Master; Namen nur als Konstanten in `KlangDienst`, Prüfung beim Start | 29.10.2026 |
| Lautstärke | Prozent linear, 100 % = 0 dB, nie darüber; 0 % und Stumm schalten den Bus stumm; Standard 80 % | 29.10.2026 |
| Hintergrund | „Ton im Hintergrund stumm“ standardmäßig an, über die Fokus-Benachrichtigungen von Anwendung und Fenster | 29.10.2026 |
| Speicherort | `user://einstellungen.cfg`, Abschnitt `ton`, von `KlangDienst` selbst geschrieben (nicht über `Fenster`) | 29.10.2026 |
| Ansagen | `AnsageSpielen`: eine Ansage zur Zeit, höchstens 2 wartend, 4 s Abstand, Schalter „Ansagen“ (Standard an, Schlüssel `ansagen`) | 29.10.2026 |
| Reiter | „Ton“ als siebter Reiter der Einstellungen (kleine Änderung an `EinstellungenAnzeige.cs`, Bereich Oberfläche) | 29.10.2026 |
