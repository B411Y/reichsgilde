# Echtzeit-Sprach-NPCs – Hintergrund

Stand 30.10.2026. **Nutzerentscheidung 30.10.2026: vorerst nicht weiter**, nur als Hintergrund festgehalten (Bereichsdatei `docs/bereiche/sound.md`). Grundlage war eine technische Bewertung nach dem Vorbild der Unreal-Demo von Daily (Pipecat C++ SDK über WebRTC): Der Spieler spricht ins Mikrofon, die Figur antwortet hörbar und löst Spielaktionen aus.

## Urteil

Für Reichsgilde jetzt **nein**. Technisch machbar, aber laufende Kosten je Spieler, Online-Pflicht, Lockstep-Mehrspieler und ein Solo-Projekt passen nicht zu einer Wirtschaftssimulation zum Einmalpreis. Die Stimmen bleiben **vorproduziert** (wie die Ansagen des Haushofmeisters): kostet nichts je Spieler, läuft offline, ist deterministisch. Mehr Abwechslung entsteht durch mehr vorab erzeugte Sätze. Sinnvoll wäre höchstens ein abgegrenzter Versuch („Audienz beim Ratsherrn“) nach dem Plan unten.

## Architektur in Kürze

- Die Intelligenz läuft nie im Spiel, sondern in einem **Pipecat-Bot** (Python) auf einem Server: Sprechende-Erkennung (VAD) → Spracherkennung (STT) → LLM mit Werkzeugen → Sprachausgabe (TTS). Das Spiel ist ein dünner Client.
- **Godot (C#):** Mikrofon über einen eigenen Bus „Mikro“ mit `AudioEffectCapture` (nicht an Master), auf 16 kHz mono; Stimme der Figur über `AudioStreamGenerator` an einem `AudioStreamPlayer3D` mit 60–120 ms Jitterpuffer. Godot hat keine Echounterdrückung: Headset oder Push-to-Talk.
- **Transport:** WebSocket aus .NET (`System.Net.WebSockets`, kein nativer Code) für den Anfang; WebRTC (C++ SDK über GDExtension oder P/Invoke, nativer Bau je Plattform) erst, wenn Messungen mit Paketverlust hörbare Aussetzer zeigen.
- **Aktionen:** Ein Werkzeugaufruf des LLM ist ein **Antrag, kein Befehl**: Positivliste in C#, daraus ein Kern-Befehl über den Befehlsboten, der Kern prüft; erst das Ergebnis geht zurück ans LLM („erst handeln, dann sprechen“).
- **Mehrspieler (Lockstep):** Nur eine Stelle fragt das LLM (Host oder Sprach-Server), ihr Ergebnis geht als gewöhnlicher Befehl mit Takt in den Lockstep; Audio ist reine Darstellung.

## Kosten (Größenordnung)

| Weg | Größenordnung |
|---|---|
| Cloud je Gesprächsminute | ca. 2–7 ct (TTS 1–5 ct ist der größte Posten, STT 0,5–1 ct, LLM etwa 0,15 ct, Transport 0,1–0,5 ct) |
| Je Spielstunde (15 min Gespräch) | ca. 0,30–1,05 € |
| 10.000 Spieler × 20 Stunden | ca. 60.000–210.000 € laufend, ohne Obergrenze, ohne Einnahmen nach dem Kauf |
| Lokal beim Spieler | Grafikkarte mit 12 GB oder mehr, 32 GB RAM; die Bildrate bricht ein, weil Spiel und Modelle dieselbe Karte teilen; auf der T400 (4 GB) nicht machbar |

Ziel-Latenz bis zum ersten Ton unter 800 ms – erreichbar nur mit Push-to-Talk oder sehr guter Turn-Erkennung (üblich 1,2–2,2 s).

## Risiken

- Erfundenes: Das Spiel ist die einzige Wahrheit; das LLM handelt nur über Werkzeuge mit festen Grenzen, Zusagen erst nach dem Ergebnis des Kerns.
- Gedächtnis und Spielstand: Versprechen und Stimmung als Daten im Spielstand; LLM-Antworten sind nicht reproduzierbar, gespeichert wird nur der Befehl.
- Inhalt und Plattform: Spieler können Figuren zu unpassenden Aussagen treiben; Steam verlangt bei live erzeugten KI-Inhalten Angabe und Schutzmaßnahmen, Moderation wird laufende Aufgabe.
- Betrieb: Server, Online-Pflicht, nativer Bau je Plattform (bei WebRTC) – für ein Solo-Projekt der größte Dauerposten.
- Wartezeit: verdecken mit Zuhör-Animation, vorproduzierten Füllsätzen („Hm …“), Gesten, Unterbrechen erlauben, Spielzeit während der Audienz anhalten.

## Versuch in drei Stufen (falls je wieder aufgegriffen)

1. **Kette ohne Spiel** (1–2 Tage): Pipecat-Bot mit Streaming-STT, Claude Haiku und deutscher Streaming-TTS im Browser-Client; eine Figur (Händler), drei Werkzeuge, Spielzustand als JSON-Attrappe, 30 Gespräche (10 Trickversuche). Weiter nur bei Median unter 1 s bis zum ersten Ton, mindestens 95 % richtigen Werkzeugaufrufen, keinem erfolgreichen Trick, unter 5 ct je Minute.
2. **Godot über WebSocket** (2–3 Tage, Versuchs-Branch): Mikrofon, Rückweg in 3D, Positivliste gegen einen Kern-Stub, Push-to-Talk, Füllsätze. Weiter nur ohne Knackser bei 60 Bildern/s, höchstens 100 ms Zusatzlatenz und 0,5 ms Bildzeit.
3. **Härtetest und Entscheidung** (2–3 Tage): 2–5 % Paketverlust, Test ohne Headset (Echo), Kostenprotokoll und Hochrechnung; Ergebnis eine Seite mit Ja/Nein. Bei Nein bleibt nur der Versuchs-Branch.

**Vor jeder Entscheidung Preise prüfen:** Die Claude-Preise stammen aus der Preisliste vom 30.10.2026; Preise und Latenzen der STT-, TTS- und WebRTC-Anbieter sind Schätzungen und beim Anbieter neu zu prüfen, ebenso die Lizenzen der nativen Daily-Bibliothek, der Piper-Stimmen und lokaler Modelle.
