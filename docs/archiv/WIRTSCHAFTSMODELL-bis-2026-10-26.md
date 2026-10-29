# Wirtschaftsmodell – Archiv der älteren Balancing-Messungen (bis 26.10.2026)

Hier stehen die Messabschnitte „Balancing-Messung 1“ bis „6“ sowie die alten Abschnitte 7 (Ergebnisse von Entwurf 1) und 8 (nächste Schritte) **wörtlich**, aus [../WIRTSCHAFTSMODELL.md](../WIRTSCHAFTSMODELL.md) verschoben (Stand 29.10.2026). Sie sind von Messung 7 und den späteren Abschnitten abgelöst; das aktuelle Modell und die neuesten Messungen stehen in der Hauptdatei. Neue Messungen gehören dorthin, nicht hierher.

## Balancing-Messung 1 (PR #176)

**Kein Wert geändert** – nur gemessen, damit der Nutzer danach entscheidet (Entscheidungen zur Balancing-Runde in `docs/bereiche/kern.md`). Gemessen auf main `24cfdc8` + Branch `efeb7b7`, Datenversion 47.

**Aufruf:** `dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen bericht 20 30` (20 Jahre, 30 Startwerte 101 … 3.030; Vorgabe `messen bericht` = 20 Jahre, 20 Startwerte). Weitere Schalter: `--karte=<kennung>` (Karte der Abschnitte Berufe, KI-Stärke, Einrichtung, Gauner; Vorgabe `greybox`), `--teile=1,4` (nur diese Abschnitte), `--parallel=n`, `--datei=<pfad>`. **Laufzeit** auf dem Entwicklungsrechner (4 Kerne, 8 Fäden): 1.170 Partien in 12,4 Minuten (mit 20 Startwerten 780 Partien in 8,3 Minuten). Die Partien laufen parallel; jede liest eigene Daten und Karte, das Ergebnis ist Wurf für Wurf dasselbe wie nacheinander (Test). `messen 30 20` und `messen 30 20 greybox` sind unverändert (der Kern ist nicht angefasst).

**Ergebnis:** `docs/messungen/balancing-2026-10-22.json`. Oben `bericht`, `datum`, `mainCommit`, `commit`, `datenversion`, `aufruf`, `parameter` (Jahre, Startwerte, Stichjahre 5/10/20, Karte, Band ±15 %, Startvermögen), `partien`, `partienMitFehler`, `laufzeitSekunden`; dann je Abschnitt ein Objekt mit `erklaerung`: `berufe` (`statisch`, `statischMittelJeStufe`, `simuliert`, `simuliertMittel`), `voelker` (`zeilen`), `startfairness` (`karten` mit `start`, `jeHaus`, `jeWohnhausplatz`, Jahre bis Bürger), `haeuser` (`stufen` 1/3/5/8 mit `alleHaeuser`, `jeHaus`, `markt`, `preisindex`; `druck`), `kiStaerke`, `einrichtung` (`statisch`, `simuliert`), `gaunerWacheBank` (`dieb`, `wache`, `bank`, `gaunereiHinterzimmer`, `gaunereiEigeneDiebe`) und `befunde` (Sätze mit Zahlen). Jede Kennzahl hat `n`, `mittel`, `streuung`, `fehlerMittel` (Streuung ÷ √n), `min`, `q10`, `q25`, `median`, `q75`, `q90`, `max`; Werte nach Stichjahr unter `"5"`, `"10"`, `"20"`.

**Wie gemessen wird** (Einzelheiten in den Feldern `erklaerung`):
- **Spieler mit einem Betrieb:** Der Spieler beginnt mit der Werkstatt seines Berufs (Mensch, Startvermögen 4.000) und wird von der KI geführt wie ein KI-Haus (ausgewogen), aber ohne Expansion (`MaxBetriebe` 0): Personal, Ware, Meisterprüfung und **Ausbau nach den Regeln der KI** (sobald Grad und Rechnung es erlauben), Stand, Familie, Politik. Keine Einrichtung, keine Prämien. Gegen die drei KI-Häuser der Greybox.
- **Bezugsspieler** (KI-Stärke): der Spieler mit genau dem Profil der KI-Häuser (auch Expansion).
- **Messstadt** (`docs/LEVELDESIGN.md`, „Kartenskript“): die ersten n von acht KI-Häusern, der Spieler entlässt seine Arbeiter und zählt nicht.
- „Zuwachs“ = Reinvermögen − Startvermögen. „Gewinn“ = Betriebsgewinn des Jahres (Umsatz mit Beute, Sold, Zins, Kollekte, Ausschank − Zutaten, Löhne, Unterhalt, Steuer, Schaden, Transport). Amt, Familie, Gericht, Investitionen zerlegt `jeJahrImMittel`.

### Berufe

**Statisch** (erwarteter Jahresgewinn eines neuen Betriebs, voll mit Gesellen, Startpreise, `Kalkulation.ErwarteterJahresgewinn`; Prediger + Kollekte als einzige Kirche, Wirt + Ausschank als einzige Schänke, beides Schätzung):

| Beruf | Stufe 1 | Stufe 2 | Stufe 3 |
|---|---|---|---|
| Schmied | 1.148 (−10 %) | 2.393 (−3 %) | 4.081 (0 %) |
| Tischler | 1.026 (**−20 %**) | 2.156 (−13 %) | 3.647 (−11 %) |
| Steinmetz | 1.214 (−5 %) | 2.499 (+1 %) | 4.008 (−2 %) |
| Alchimist | 1.221 (−5 %) | 2.449 (−1 %) | 3.895 (−5 %) |
| Wirt (+ Ausschank 312 / 862 / 1.405) | 1.600 (**+25 %**) | 3.579 (**+45 %**) | 5.434 (**+33 %**) |
| Prediger (+ Kollekte 475) | 1.625 (**+27 %**) | 2.599 (+5 %) | 4.442 (+9 %) |
| Dieb (Taschendiebe) | 1.344 (+5 %) | 2.362 (−5 %) | 4.050 (−1 %) |
| Parfumeur | 1.221 (−5 %) | 2.164 (−13 %) | 3.708 (−9 %) |
| Geldleiher (volle Kasse) | 1.342 (+5 %) | 2.600 (+5 %) | 4.230 (+3 %) |
| Gardist (Sold) | 1.092 (−15 %) | 1.946 (**−21 %**) | 3.395 (**−17 %**) |
| Mittel | 1.283 | 2.475 | 4.089 |

**Simuliert** (ein Betrieb, 20 Jahre, 30 Startwerte; Fehler des Mittels beim Vermögen ±115 … ±1.010):

| Beruf | Vermögen 5 / 10 / 20 J. | Zuwachs gegen Mittel (11.603) | Gewinn Ø je Jahr | Bürger (10.000) Ø Jahr, erreicht | Stufe 2 Ø Jahr |
|---|---|---|---|---|---|
| Schmied | 4.046 / 6.804 / 10.693 | **−42 %** | 1.321 | 18,2 (22/30) | 11,3 |
| Tischler | 3.655 / 5.671 / 11.074 | **−39 %** | 1.316 | 18,1 (23/30) | 11,3 |
| Steinmetz | 4.625 / 8.529 / 14.246 | −12 % | 1.568 | 15,6 (30/30) | 11,4 |
| Alchimist | 4.894 / 5.909 / 10.189 | **−47 %** | 1.270 | 18,6 (18/30) | 11,2 |
| Wirt | 6.171 / 8.234 / 23.287 | **+66 %** | 2.196 | 13,8 (30/30) | 11,1 |
| Prediger | 7.686 / 13.427 / 15.669 | +1 % | 1.848 | 7,4 (30/30) | 11,7 |
| Dieb (ausgewogen: nur Taschendiebe) | 7.124 / 8.550 / 14.996 | −5 % | 2.073 | 12,9 (30/30) | 6,0 |
| Parfumeur | 4.048 / 4.791 / 6.645 | **−77 %** | 963 | 19,0 (3/30) | 13,0 |
| Geldleiher | 7.566 / 9.754 / 17.965 | **+20 %** | 1.735 | 11,9 (30/30) | 12,1 |
| Gardist | 8.745 / 13.218 / 31.262 | **+135 %** | 2.212 | 9,0 (30/30) | 6,0 |
| Dieb wagemutig (nicht im Mittel) | 7.815 / 7.862 / 16.245 | +6 % | 2.462 | 12,3 (30/30) | 6,1 |

Zum Vergleich die KI-Häuser in denselben Partien: Ø 60.000–67.000 nach 20 Jahren. Stufe 3 erreicht mit einem Betrieb niemand in 20 Jahren.

### Völker

Spieler mit einem Betrieb, jedes Volk, Vermögen nach 20 Jahren (Abstand des Zuwachses zum Menschen):

| Beruf | Mensch | Zwerg | Elb | Halbling |
|---|---|---|---|---|
| Schmied (Bonus 0 / +5 / 0 / −5 %) | 10.693 | 13.733 (+45 %) | 10.663 (0 %) | 9.667 (−15 %) |
| Parfumeur (Bonus 0 / −5 / +5 / 0 %) | 6.645 | 5.582 (−40 %) | 6.658 (+1 %) | 7.837 (+45 %) |

### Startfairness

| | Spieler | KI-Häuser |
|---|---|---|
| Start Greybox | 4.000 Gulden, 1 Werkstatt, 2 Gesellen, erwartet 1.148 im Jahr | je 4.000 Gulden, Werkstatt + Umland, 5 Arbeiter, erwartet 2.904–3.244 |
| nach 20 Jahren (120 Partien: 4 Rotationen × 30) | 10.693, Bürger 116, Gemeiner 4 | Eisenbart 74.130 (Edler), Kieselbach 68.416, Eichwald 51.725 |
| Probestadt (KI-Häuser nur mit Werkstatt, 90 Partien) | 9.838 | Ahlbeck (Tischler, Mensch) 14.678, Brendel (Schmied) 11.605 |

Wohnhausplätze rotieren (jedes Haus auf jedem Platz): Das Vermögen ist **auf jedem Platz Wurf für Wurf gleich** (Abstand 0 %). Der Platz wirkt im Kern heute nicht – Fahrzeiten hängen an Stadt/Umland, nicht am Weg; Bürger kaufen am Markt; die Lage des Wohnhauses zählt nur beim Bauen. Jahre bis Bürger (Spieler mit einem Betrieb): Greybox Ø 18,2 (in 8–12 Jahren 4 von 120), Probestadt Ø 19,1 (0 von 90).

### 1, 3, 5, 8 Häuser (Messstadt)

| KI-Häuser | Vermögen je Haus 5 / 10 / 20 J. | Gewinn je Haus im Jahr 5 / 10 / 20 | Summe aller Häuser (20 J.) | Preisindex | gegen 1 Haus (20 J.) |
|---|---|---|---|---|---|
| 1 | 9.738 / 32.871 / 111.605 | 3.401 / 8.900 / 9.609 | 111.605 | 0,91 | – |
| 3 | 6.547 / 17.389 / 67.718 | 2.081 / 5.114 / 9.264 | 203.155 | 0,885 | −39 % |
| 5 | 5.634 / 15.846 / 58.371 | 1.177 / 4.406 / 9.198 | 291.856 | 0,852 | −48 % |
| 8 | 5.548 / 15.022 / 51.453 | 1.337 / 4.267 / 8.075 | 411.627 | 0,811 | **−54 %** (gegen 3: −24 %) |

Preisindex = Marktpreis ÷ Grundpreis, gewichtet mit den Verkäufen der Häuser (letztes Jahr). Bei 8 Häusern fallen vor allem die **Rohstoffe**: Holz 0,66, Silber 0,68, Granit 0,70, Eisen 0,74 – die Häuser stellen dort 47–61 % des Angebots. Fertigwaren bleiben bei 0,89–1,01 (Anteil der Häuser 26–40 %). Die Häuser mit **demselben Beruf** teilen sich ihre Märkte: zwei Tischler 19.932 und 32.945, zwei Steinmetze 37.171 und 41.891, zwei Schmiede 62.318 und 39.341, dagegen der einzige Wirt 102.952 und der einzige Alchimist 75.076. Kein Bankrott, 2 erloschene Häuser (bei 8).

### KI-Stärke

Bezugsspieler (Strategie der KI, Schmied) nach 20 Jahren 6.404 Gulden, die KI-Häuser 64.692 (Median des Verhältnisses 11). **Alle 90 KI-Häuser ziehen davon** (> 2×), keins bleibt zurück, keins geht bankrott oder erlischt; am Ende Patrizier 25, Edler 65 (Bezug: Bürger 28). Grund ist der Start, nicht die Strategie: Bis Jahr 10 gleicht der Bezug dem Spieler mit einem Betrieb (Liegenschaften und Neubau erst als Bürger), danach investiert er (Betriebe 1 → 2,5 in Jahr 20, Investitionen Ø 561 im Jahr) und liegt in Jahr 20 noch unter dem Spieler mit einem Betrieb (10.693). Die KI-Häuser führen 2,6 / 3,6 / 4,9 Betriebe (Jahr 5/10/20). Die KI kauft keine Einrichtung, zahlt keine Prämien und nimmt keine Kredite.

### Einrichtung

| Stück (statisch, Startpreise, voll mit Gesellen) | Preis | Mehrgewinn im Jahr | Amortisation |
|---|---|---|---|
| Gutes Werkzeug (alle sieben Werkstattberufe) | 300 | 172–209 | 1,4–1,7 J. |
| Meisterwerkzeug (Stufe 2) | 800 | 359–445 | 1,8–2,2 J. |
| Spezialstück (Blasebalg, Leimofen, Flaschenzug, Destillierhelm, Braupfanne, Gänsekiele) für die begünstigten Waren | 350 | 146–184 | 1,9–2,4 J. |
| Glasflakons (erst ab der Duftwerkstatt eine begünstigte Ware) | 350 | 327 | 1,1 J. |
| Lederschürzen/-handschuhe | 80 | 8–19 | 4,2–10,4 J. |
| Eisentür (gemessener Raub 76 Gulden je Betrieb und Jahr) | 300 | 38 | 7,9 J. |
| Löschfass (die KI gibt keine Sabotage in Auftrag) | 100 | 0 | – |
| Geldleiher: Goldwaage / Rechenbretter / Hauptbücher / Pfandkammer | 350 / 300 / 800 / 350 | 149 / 38 / 78 / 20 | 2,4 / 8 / 10,3 / 17,5 J. |
| Gardist: Waffenkammer | 200 | 88 | 2,3 J. |

Ohne Gulden-Wirkung (keine Zahl): Regale, Dietriche, Verkleidung, Hellebarden, Armbrüste, Wachhunde, Kettenhemden, Kassenschrank. **Simuliert** (Werkzeug + Spezialstück am Start, 650 Gulden, gegen dieselbe Partie ohne, Jahre 1–10): Mehrgewinn 127 (Alchimist) bis 284 (Parfumeur) im Jahr, Amortisation 2,3–5,1 Jahre; Geldleiher (Rechenbretter, Goldwaage, Pfandkammer, 1.000) 146 → 6,9 Jahre; Gardist (Waffenkammer) 107 → 1,9 Jahre. Weniger als statisch: Mehr Stück drücken den Preis, und das Spezialstück hilft nur, wenn die begünstigte Ware gemacht wird.

### Gauner, Wache, Bank

| | ohne Wachhaus | mit Wachhaus (vier Gesellen beim ersten KI-Haus) |
|---|---|---|
| Dieb ausgewogen (nur Taschendiebe): Gewinn je Jahr, Beute, Fänge | 2.073, 3.418, 0,60 | 2.126, 3.402, 0,59 |
| Dieb wagemutig (dazu Einbruch …): Gewinn je Jahr | 2.462 | 2.341 (−5 %) |

- **Wache** (Spieler als Gardist): Sold 3.251, Geleitgeld 433, Kopfgeld 41 im Jahr, 1,06 Razzien im Jahr, Streife 13,7 % weniger Erfolg. Raub und Schaden je KI-Haus und Jahr 411 statt 561 (−27 %). Die Taschendiebe trifft die Wache praktisch nicht (Beute und Fänge gleich).
- **Bank** (Spieler als Geldleiher, Jahr 20): Kasse 7.267, Stadtzinsen 887, Wechsel 2.997, Ausfall 175 – der Wechsel bringt drei Viertel. **Kredite: 0 in 1.170 Partien**, also auch kein Wucher – die KI nimmt keine Kredite.
- **Gaunerei je Auftrag** (Hinterzimmer der KI, Partien mit dem Bezugsspieler): Diebstahl 1,16 Aufträge je Partie und Jahr zu 51 %, Einbruch ins Wohnhaus 0,41 zu 50 %; Sabotage, Überfall, Krötenschleim und Brandstiftung gibt die ausgewogene KI nicht in Auftrag. Eigene Diebe (wagemutig): Einbruch 0,92 je Jahr zu 50 %, Auskundschaften 0,55 zu 44 %, Brand nach verweigertem Schutzgeld 0,04 zu 35 %.

### Befunde (ohne Änderung – Entscheidung beim Nutzer)

1. **Startnachteil des Spielers:** Gleiches Startgeld, aber die KI-Häuser beginnen mit Werkstatt **und** Umland (5 Arbeiter, erwartet 2,5–2,8× so viel Gewinn) – nach 20 Jahren 6–11× so reich wie der Spieler mit derselben Strategie. Der größte Hebel für Fairness ist der Start, nicht die Werte der Berufe.
2. **Z2 verfehlt:** Mit einem Betrieb wird man im Mittel erst nach 15–19 Jahren Bürger (Schmied 18,2, Ziel 8–12); nur Prediger (7,4), Gardist (9,0) und Geldleiher (11,9) liegen im oder am Ziel. Z2 stammt aus Entwurf 1 ohne KI-Konkurrenz.
3. **Berufe außerhalb ±15 %** (simuliert, Zuwachs in 20 Jahren): zu stark Gardist (+135 %: früher Ausbau – 4 Wachleute sammeln doppelt so schnell Erfahrung, Stufe 2 in Jahr 6 statt 11 –, Sold ohne Preisdruck, wenig Gerichtskosten), Wirt (+66 %, statisch +25…+45 % mit Ausschank), Geldleiher (+20 %); zu schwach Parfumeur (−77 %, nur 3 von 30 werden Bürger), Alchimist (−47 %), Schmied (−42 %), Tischler (−39 %). Statisch zusätzlich Prediger Stufe 1 (+27 % mit Kollekte), Tischler Stufe 1 (−20 %), Gardist Stufe 2/3 (−21/−17 %). Die drei Startberufe der KI (Schmied, Tischler, Steinmetz) leiden unter der direkten Konkurrenz der KI-Häuser; Berufe ohne Ware (Sold, Beute, Zins) kennen keinen Preisdruck.
4. **Völker wirken stärker als ±5 %:** Zwerg-Schmied +45 %, Halbling-Parfumeur +45 % (ohne Bonus), Zwerg-Parfumeur −40 % – Familie und Talente der Völker wirken mit (Fehler des Mittels etwa ±5 %).
5. **Startplatz fair:** Der Wohnhausplatz ändert nichts; die Werkstattlage wirkt im Kern nicht.
6. **8 Häuser:** Bei fester Nachfrage verdient ein Haus mit 8 Häusern 54 % weniger als allein und 24 % weniger als mit 3; die Rohstoffpreise fallen auf 0,66–0,74. Doppelte Berufe teilen sich den Markt (Tischler 20.000 gegen Wirt 103.000).
7. **KI-Stärke:** Die KI zieht immer davon und geht nie bankrott – Hauptursache ist der Start (Befund 1); sie nutzt weder Einrichtung noch Prämien noch Kredite.
8. **Einrichtung** macht sich schnell bezahlt (Werkzeug, Meisterwerkzeug, Spezialstücke 1–2,5 Jahre statisch, 2–5 Jahre simuliert); langsam Lederschürzen (4–10 J.), Eisentür (8 J.), Pfandkammer (17,5 J.), Hauptbücher (10 J.), Rechenbretter (8 J.); das Löschfass wirkt nie, weil die KI keine Sabotage in Auftrag gibt.
9. **Gauner, Wache, Bank:** Ein Wachhaus ändert den Gewinn des Diebs kaum (+3 % / −5 %, im Rauschen) – die Taschendiebe sind vor der Wache sicher; dafür −27 % Raub und Schaden bei den anderen Häusern. Kredite und Wucher kommen ohne Spieler nie vor.

## Balancing-Messung 2 (PR #177)

Die Werte nach den Entscheidungen des Nutzers zu „Balancing 1“ (`docs/bereiche/kern.md`), gemessen auf main `821bc97` + Branch `efecab2`, **Datenversion 48**. Aufruf wie bei Messung 1: `messen bericht 20 30` (1.200 Partien, 13,3 Minuten) → `docs/messungen/balancing-2026-10-22-2.json` (Messung 1 bleibt daneben). Neu im Bericht: die Schwierigkeit im Messaufbau, unter `kiStaerke.schwer` derselbe Vergleich mit dem Start „Schwer“, je Hausstufe der Messstadt der `einwohnerfaktorAmStart`.

**Was sich geändert hat** (Einzelheiten in `kern.md`, „Balancing 1 (PR #177)“):
1. **Gleicher Start** (Schwierigkeit „Normal“, Vorgabe): Jedes KI-Haus beginnt wie der Spieler mit einer Werkstatt und zwei Gesellen; seine Umlandbetriebe stehen zur Pacht. „Schwer“ ist der alte Start.
2. **Nachfrage nach Einwohnern** (Abschnitt `stadt`):
   ```
   Einwohner        = einwohnerGrund (100) + einwohnerJeHaus (25) × Häuser, die wirtschaften
   Einwohnerfaktor  = Einwohner ÷ (100 + 25 × bezugHaeuser 4) = Einwohner ÷ 200
   Nachfrage        = Nachfrage der Ware × Einwohnerfaktor × Faktor der Bürger-KI
   Grundversorgung  = Nachfrage der Ware × Einwohnerfaktor × NPC-Anteil
   ```
   „Wirtschaften“ heißt: im Spiel und mindestens ein Arbeiter. Häuser 1 / 2 / 3 / 4 / 5 / 8: Faktor 0,625 / 0,75 / 0,875 / 1 / 1,125 / 1,5. Weil die Grundversorgung mitwächst, ändert eine größere Stadt die Preise ohne Spieler nicht; sie hat Platz für mehr Häuser. Ein Haus bringt im Mittel 25 Einwohner mit – so viele, wie die Bürger-KI je Haus ansiedelt; die wirklichen Arbeiter zählen nicht einzeln, sonst schüfe jeder neue Arbeiter mehr Nachfrage, als er herstellt (ein Geselle macht Ware für ≈ 1.000 Gulden, ein Einwohner von 200 trüge ≈ 0,5 % des Marktes, ≈ 3.500 Gulden). Mit `einwohnerGrund` 50 (erster Versuch) verdiente ein Haus unter 8 Häusern 32 % **mehr** als unter 3 – 100 hält beide nah beieinander und eine Stadt mit einem Rivalen nicht zu klein.
3. **Preise der Fertigwaren** je Beruf (Waren der Stufe 1 / Waren ab Stufe 2): Schmied ×1,28 / ×1,12, Tischler ×1,28 / ×1,2, Steinmetz ×1,18 / ×1,08, Alchimist ×1,22 / ×1,15, Parfumeur ×1,3 / ×1,22, Prediger ×1 / ×1,05, Wirt ×1. Rohstoffe, Zwischenwaren und Heilmittel bleiben.
4. **Berufe ohne Ware:** Ausschank ×1,2 statt ×1,25, Kollekte 2,2 statt 2,5 je Gläubigem, Wechselgebühr 0,68 % statt 0,7 %, Taschendiebe 1,0 statt 0,8 je Minute, Sold 0,68 statt 0,75 je Minute; Diebe und Wache sammeln je Mann halb so viel Erfahrung (`erfahrungFaktor` 0,5).
5. **Einrichtung:** Eisentür 150 statt 300, Hauptbücher 350 statt 800, Pfandkammer 150 statt 350 und Verlust × 0,5 statt × 0,75.
6. **Einbruch in die Kasse:** 5 % der Kasse, höchstens 500 Gulden (`geldleiher.einbruch`).

### Die vier Leitzahlen

| Kennzahl | Messung 1 (PR #176) | Messung 2 (PR #177) |
|---|---|---|
| Jahre bis Bürger (Schmied, ein Betrieb, Ziel 8–12) | Ø 18,2, erreicht 22 von 30; Greybox im Ziel 4 von 120 | **Ø 9,8**, erreicht 30 von 30; Greybox im Ziel **116 von 120**, Probestadt Ø 11,9 (60 von 90) |
| Berufe im Band ±15 % (simuliert, 20 Jahre) | 3 von 10 (−77 … +135 %) | **10 von 10** (−9 … +9 %) |
| KI ÷ gleiche Strategie (Median nach 20 Jahren) | 11; 90 von 90 KI-Häusern über 2× | **1,46**; 16 von 90 über 2×, keins unter ½, keins bankrott. Bei „Schwer“ 2,34 (67 von 90) |
| 8 gegen 3 Häuser (Vermögen je Haus, 20 Jahre) | −24 % (feste Nachfrage) | **−13 %** (Einwohnerfaktor 1,5 gegen 0,875) |

### Berufe

**Simuliert** (ein Betrieb, KI geführt, 20 Jahre, 30 Startwerte; Mittel des Zuwachses 14.818 statt 11.603):

| Beruf | Vermögen 5 / 10 / 20 J. | Zuwachs gegen Mittel | Gewinn Ø je Jahr | Bürger Ø Jahr | Stufe 2 Ø Jahr |
|---|---|---|---|---|---|
| Schmied | 6.552 / 10.999 / 19.038 | +1,5 % (vorher −42 %) | 1.963 | 9,8 | 11,6 |
| Tischler | 6.958 / 9.515 / 18.838 | +0,1 % (−39 %) | 1.966 | 10,9 | 11,4 |
| Steinmetz | 4.854 / 8.898 / 18.528 | −2,0 % (−12 %) | 1.840 | 12,6 | 12,1 |
| Alchimist | 7.688 / 10.478 / 17.846 | −6,6 % (−47 %) | 1.875 | 9,9 | 11,2 |
| Wirt | 5.849 / 7.116 / 19.405 | +4,0 % (+66 %) | 1.966 | 14,1 | 11,0 |
| Prediger | 7.083 / 12.064 / 17.833 | −6,6 % (+1 %) | 1.832 | 9,5 | 11,7 |
| Dieb (ausgewogen: Taschendiebe) | 8.869 / 11.983 / 20.081 | +8,5 % (−5 %) | 2.347 | 7,0 | 12,0 |
| Parfumeur | 7.680 / 10.596 / 17.486 | −9,0 % (−77 %) | 1.896 | 9,4 | 12,2 |
| Geldleiher | 8.302 / 10.456 / 19.708 | +6,0 % (+20 %) | 1.852 | 9,2 | 12,1 |
| Gardist | 6.484 / 9.871 / 19.412 | +4,0 % (+135 %) | 1.648 | 11,8 | 11,0 (vorher 6,0) |
| Dieb wagemutig (nicht im Mittel) | 7.625 / 4.668 / 14.891 | −26,5 % | 2.658 | 10,9 | 12,2 |

Der Wirt kommt langsamer zum Bürger (14,1 Jahre), holt aber nach dem Ausbau auf; der Dieb kommt schnell hin (7,0), hat dann aber mehr Gerichtskosten (−729 im Jahr).

**Statisch** (Startpreise, voll mit Gesellen; Mittel 1.694 / 3.073 / 5.037): außerhalb ±15 % liegen noch Schmied Stufe 1 (+16 %), Tischler Stufe 3 (+22 %), Parfumeur (+29 / +20 / +18 %), Wirt Stufe 1 und 3 (−22 / −18 %) und der Gardist (−47 / −47 / −42 %). Die Rechnung lässt beim Gardisten Geleitgeld (≈ 317 im Jahr) und Kopfgeld (≈ 60) weg und beim Wirt den Ausschank fast ganz (Ausschankpreis = Grundpreis × 1,2 ≈ Startpreis am Markt); der Parfumeur verdient in der Partie weniger, als die Startpreise versprechen. Simuliert liegen alle drei im Band – deshalb wurde nicht weiter verschoben.

**Gericht und Familie** (Auftrag zu Z2: Fehler oder gewollt?): Kein Fehler. Ein Haus mit einem Betrieb zahlt im Mittel ≈ 350 im Jahr für die Familie (Haushalt Mensch 50 je Erwachsenem, 40 je Kind, Werbung um eine Braut ≈ 600 und die Hochzeit 300 einmalig) und ≈ 470 für das Gericht. Das Gericht sind fast ganz die eigenen Entscheidungen der KI, die den Messspieler führt (Diagnose über 5 × 20 Jahre): 70 Spitzel zu je 300 Gulden (≈ 210 im Jahr, „auf das stärkste andere Haus, einmal im Jahr“), 11 Anklagen (Gebühr 100), 9 Bußen nach eigenen Diebstählen gegen reichere Häuser und 2 Entschädigungen. Ein Mensch, der keine Spitzel bezahlt, spart den größten Teil davon. → mögliche Designfrage an die Spiel-KI.

### Völker

| Beruf | Mensch | Zwerg | Elb | Halbling |
|---|---|---|---|---|
| Schmied (Bonus 0 / +5 / 0 / −5 %) | 15.038 | +6,1 % | −2,0 % | −16,8 % |
| Parfumeur (Bonus 0 / −5 / +5 / 0 %) | 13.486 | −9,5 % | +17,1 % | −2,3 % |

Deutlich näher an den Boni als in Messung 1 (dort bis ±45 %); der Halbling-Schmied und der Elb-Parfumeur weichen noch um das Dreifache ihres Bonus ab (Fehler des Mittels etwa ±5 %). Die Völkerboni blieben unverändert (Entscheidung).

### Startfairness, Häuser und KI-Stärke

- **Startfairness:** Greybox 120 Partien – Spieler mit einem Betrieb 19.038, KI-Häuser (gleicher Start, mit Expansion) Eisenbart 64.600, Eichwald 53.292, Kieselbach 63.728; Probestadt Spieler 17.493, Ahlbeck 53.543, Brendel 48.823. Der Wohnhausplatz ändert weiter nichts (0 %).
- **1, 3, 5, 8 Häuser** (Messstadt, nur Werkstatt am Start):

| KI-Häuser | Einwohnerfaktor | Vermögen je Haus (20 J.) | Gewinn je Haus im Jahr 20 | Preisindex | gegen 3 Häuser |
|---|---|---|---|---|---|
| 1 | 0,625 | 101.870 | 13.930 | 0,847 | +68 % |
| 3 | 0,875 | 60.690 | 11.822 | 0,859 | – |
| 5 | 1,125 | 58.355 | 10.253 | 0,867 | −4 % |
| 8 | 1,5 | 52.749 | 9.897 | 0,875 | **−13 %** |

  Kein Bankrott, 4 erloschene Häuser bei 8.
- **KI-Stärke** (Greybox, Bezugsspieler mit der Strategie der KI): Bezug 41.678, KI-Häuser 61.030 (Median 1,46), Stand am Ende Bezug Patrizier 23 / Edler 7, KI Patrizier 25 / Edler 65; die KI-Häuser führen 1 / 3 / 5 Betriebe in Jahr 5 / 10 / 20 (erst als Bürger wird gepachtet und gebaut). Bei „Schwer“: Bezug 40.481, KI 89.910 (Median 2,34).

### Einrichtung

| Stück (statisch) | Preis | Mehrgewinn im Jahr | bis bezahlt |
|---|---|---|---|
| Eisentür (alle Berufe) | 150 (300) | 37 (gemessener Raub 73 Gulden je Betrieb und Jahr × (1 − 0,5)) | **4,1 J.** (vorher 7,9) |
| Hauptbücher (Geldleihe, Rahmen nochmals × 1,1: 3 Schreiber × 2.500 × 1,1 = 8.250 → 9.075 betreute Kasse, Zins 12 % nach Steuer − Ausfall 2 %) | 350 (800) | 78 | **4,5 J.** (10,3) |
| Pfandkammer (Wechselstube, volle Kasse 4.000: Ausfall 20 % × 10 % = 2 % = 80 im Jahr, davon die Hälfte) | 150 (350) | 40 | **3,7 J.** (17,5) |

Die **Eisentür** wirkt nur, wenn eingebrochen wird: Ihr Wert ist der gemessene Raub je Betrieb und Jahr (Diebstähle des Hinterzimmers und Einbrüche der Diebe unter der Strategie der KI) mal 0,5. Die Abfrage für die KI (`Werkstattwesen.ErwarteterMehrgewinnJeJahr`) rechnet dasselbe mit dem Raub des eigenen Betriebs im Vorjahr – ein Haus, das nie bestohlen wurde, kauft keine. **Simuliert** (Werkzeug und Spezialstück am Start, 650): 2,7–5,0 Jahre bis bezahlt, Geldleiher (Rechenbretter, Goldwaage, Pfandkammer, 800) 4,7, Gardist (Waffenkammer) 2,7. Über 5 Jahre bleiben statisch Rechenbretter (8 J., nicht Teil der Entscheidung), Lederhandschuhe von Wirt und Prediger (9–10 J.) und das Löschfass (die KI gibt fast keine Sabotage in Auftrag).

### Gauner, Wache, Bank

Dieb ausgewogen 2.347 im Jahr ohne, 2.489 mit Wachhaus; wagemutig 2.658 / 2.685. Wache: Sold 2.612, Geleitgeld 317, Kopfgeld 60 im Jahr; Raub und Schaden je KI-Haus 461 ohne, 389 mit Wachhaus (−16 %). Bank (Jahr 20): Kasse 7.325, Stadtzinsen 895, Wechsel 3.211. Kredite weiter 0 in 1.200 Partien. Hinterzimmer: Diebstahl 1,32 je Partie und Jahr zu 54 %.

### Ziele Z1–Z7

| Ziel | Messung 2 |
|---|---|
| Z1 Gießerei 2 Gesellen (am 22.10.2026 angehoben auf 1.300–1.700, vorher 600–900) | erfüllt: Referenzprobe 1.551 allein, 1.317 mit einem Rivalen (vorher 848 / 649); statisch zu Startpreisen 1.966 |
| Z2 Bürger mit einem Betrieb in 8–12 Jahren | **erfüllt:** Schmied Ø 9,8, Greybox 116 von 120 im Ziel |
| Z3 Konkurrenz drückt den Preis allmählich | erfüllt (Formel unverändert; zwei Gießereien je 1.317 statt 1.551 allein) |
| Z4 ein Spieler beherrscht den Markt nicht | erfüllt (Mengen unverändert; bei 4 Häusern Einwohnerfaktor 1) |
| Z5 Statut B für Minen +40–50 % | erfüllt (Grube 947 → 1.365, +44 %, Rohstoffe unverändert) |
| Z6 Statut C +15–25 % | verfehlt wie schon vorher (statisch +2 %, jetzt 0 %: bei höheren Warenwerten bleibt der Geselle die beste Kraft) |
| Z7 Zwischenwaren weniger, nicht defizitär | erfüllt (Beschlag 682 gegen Silberring 1.966 im Jahr) |

Die Erwartung „eine Grube unter Statut B bringt mehr als eine Werkstatt“ (LiegenschaftTests) ist kein Ziel mehr (Entscheidung des Nutzers, Option A: Rohstoffe bleiben): Grube 1.878, Gießerei 1.966 zu Startpreisen. Der Test prüft nun Z5 – die Grube bringt unter B deutlich mehr als unter A (1.878 gegen 1.313).

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler Mensch passiv, KI ausgewogen, jetzt mit dem Normal-Start; gemessen auf main `821bc97` + Branch `efecab2`), Vermögen Ø am Ende:

| | Eisenbart | Eichwald | Kieselbach | Bürgermeister | Stand am Ende |
|---|---|---|---|---|---|
| ohne Karte, vorher (zuletzt zeichengleich seit PR #166) | 129.647 | 88.479 | 127.439 | 18/20 | |
| ohne Karte, jetzt | 129.713 | 113.081 | 123.643 | 13/20 | Freiherr 58, Edler 2; kein Bankrott |
| Greybox, vorher | 162.279 | | | 17/20 | |
| Greybox, jetzt | 148.254 | 142.556 | 154.716 | 13/20 | Freiherr 60; kein Bankrott |

Die drei Häuser liegen jetzt dichter beieinander (Eichwald holt auf, die Tischlerwaren sind teurer). Sie beginnen ohne Umland (Meistergrad 3 und Stufe 2 nach 8–9 Jahren); nach 30 Jahren hat Eisenbart ohne Karte gleich viel wie mit dem alten Start, in der Greybox 9 % weniger. Ohne Karte bauen sie weiter nicht (offener Punkt in `kern.md`).

## Balancing-Messung 3 (PR #177)

Nach Messung 2 kauft die KI Einrichtung (Spiel-KI, „KI kauft Einrichtung (PR #177)“ in `docs/bereiche/spiel-ki.md`: zum Jahresende, wenn sich ein Stück in höchstens 4 Jahren bezahlt macht) – die KI-Häuser **und** der von der KI geführte Messspieler. Die gemeinsame Messung des Funktions-Branches (`125a073`, Datenversion 48) zeigte vier Berufe außerhalb des Bandes; sie sind hier nachjustiert. Gemessen auf main `821bc97` + Branch `f5674fd`, **Datenversion 49**: `messen bericht 20 30` (1.200 Partien, 15 Minuten) → `docs/messungen/balancing-2026-10-22-3.json` (Messung 1 und 2 bleiben daneben).

**Ursache** (Diagnose je Beruf, 16 Startwerte, dieselbe Fassung mit und ohne Abschnitt `ki`; Zuwachs des Vermögens nach 20 Jahren durch den Einrichtungskauf): Schmied +6.850, Wirt +9.830, Tischler +12.750, Dieb +1.180, Gardist +1.060. Jede Werkstatt kauft Gutes Werkzeug (Jahr ≈ 2), Lederschürzen bzw. -handschuhe, ihr Sonderstück und mit Stufe 2 das Meisterwerkzeug – ihre Waren entstehen dann in 0,81 bzw. 0,73 der Zeit. Tischler und Wirt verkaufen das Mehr ohne großen Preisdruck (Armbrust, Gehstock; Starkbier vor allem im eigenen Ausschank). Kein einzelnes Stück ist der Grund: Leimofen und Braupfanne mit Zeit × 0,95 statt × 0,9 nähmen nur 1.500 bzw. 400 Gulden weg. Dieb und Gardist haben fast keine Einrichtung mit Gulden-Wirkung (Dietriche, Verkleidung, Hunde, Kettenhemden ohne Rechnung; nur die Waffenkammer, 88 im Jahr) – sie bleiben zurück, wenn alle anderen zulegen.

**Was sich geändert hat** (vorher → nachher):
1. **Tischler**, Waren ab Stufe 2 ×1,08 statt ×1,2 (gegen die Preise vor PR #177): Gehstock 90 → 81, Armbrust 234 → 211, Schrank 372 → 335, Prunktruhe 463 → 417. Waren der Stufe 1 bleiben (×1,28).
2. **Wirt**, Waren ab Stufe 2 ×0,97: Starkbier 138 → 134, Kräuterlikör 170 → 165, Festbier 225 → 218, Schädelbrand 375 → 364. Der Ausschank folgt dem Grundpreis (× 1,2). Stufe 1 bleibt – der Wirt kommt ohnehin spät zum Bürger.
3. **Dieb**, Beute der Taschendiebe je Stufe 1 / 1,2 / 1,4 statt 1 / 1,1 / 1,3 (je Minute weiter 1,0): Der Dieb ist früh schnell (Bürger 7,5 Jahre) und fällt nach dem Ausbau zurück; deshalb nur die Stufen 2 und 3.
4. **Gardist**, Sold 0,73 statt 0,68 je Minute (Stufen weiter × 1 / 1,1 / 1,3): Er liegt in jedem Stichjahr unter dem Mittel.

Probeläufe (30 Startwerte, Zuwachs nach 20 Jahren): Tischler ×1,1 −4.070; Wirt ×0,97 −2.840; Dieb Stufen 1,2 / 1,4 +3.270; Gardist Sold 0,72 +2.740, Sold je Stufe 1,2 / 1,4 +2.280.

### Die vier Leitzahlen

| Kennzahl | Messung 2 (ohne KI-Einrichtung) | gemeinsame Messung (`125a073`, mit KI-Einrichtung) | Messung 3 (Datenversion 49) |
|---|---|---|---|
| Jahre bis Bürger (Schmied, ein Betrieb, Ziel 8–12) | Ø 9,8; Greybox im Ziel 116 von 120 | Ø 8,2; Greybox im Ziel 80 von 120 | **Ø 8,2**, erreicht 30 von 30; Greybox im Ziel 80 von 120 (die übrigen schon in Jahr 6–7), Probestadt Ø 9,5 (78 von 90) |
| Berufe im Band ±15 % (simuliert, 20 Jahre) | 10 von 10 (−9 … +9 %) | 6 von 10 (−23 … +31 %) | **10 von 10** (−12,5 … +8,5 %) |
| KI ÷ gleiche Strategie (Median nach 20 Jahren) | 1,46 | 1,60; 25 von 90 über 2× | **1,47**; 17 von 90 über 2×, keins unter ½, keins bankrott. Bei „Schwer“ 1,79 (vorher 2,03) |
| 8 gegen 3 Häuser (Vermögen je Haus, 20 Jahre) | −13 % | −12 % | **−9 %** (69.797 gegen 77.001) |

### Berufe

**Simuliert** (ein Betrieb, KI geführt mit Einrichtungskauf, 20 Jahre, 30 Startwerte; Mittel des Zuwachses 21.124, vorher 21.296):

| Beruf | Vermögen 5 / 10 / 20 J. | Zuwachs gegen Mittel | vorher (`125a073`) | Gewinn Ø je Jahr | Bürger Ø Jahr | Stufe 2 Ø Jahr |
|---|---|---|---|---|---|---|
| Schmied | 6.513 / 12.495 / 26.410 | +6,1 % | +2,1 % | 2.434 | 8,2 | 11,6 |
| Tischler | 6.788 / 11.836 / 26.916 | **+8,5 %** | +31,2 % | 2.466 | 8,9 | 11,3 |
| Steinmetz | 4.853 / 9.735 / 25.356 | +1,1 % | −0,4 % | 2.335 | 11,1 | 12,2 |
| Alchimist | 7.966 / 11.959 / 26.520 | +6,6 % | +8,9 % | 2.472 | 8,4 | 11,3 |
| Wirt | 5.936 / 8.269 / 26.019 | **+4,2 %** | +19,5 % | 2.345 | 13,4 | 10,9 |
| Prediger | 6.839 / 13.434 / 22.475 | −12,5 % | −10,2 % | 2.216 | 8,5 | 11,8 |
| Dieb (ausgewogen: Taschendiebe) | 8.835 / 12.312 / 24.104 | **−4,8 %** | −19,9 % | 2.534 | 7,5 | 12,0 |
| Parfumeur | 7.340 / 11.831 / 25.779 | +3,1 % | −2,8 % | 2.403 | 8,6 | 12,5 |
| Geldleiher | 8.345 / 11.746 / 23.410 | −8,1 % | −5,6 % | 2.141 | 8,2 | 12,0 |
| Gardist | 7.322 / 11.978 / 24.253 | **−4,1 %** | −22,7 % | 1.918 | 9,0 | 11,1 |
| Dieb wagemutig (nicht im Mittel) | 8.657 / 5.644 / 19.867 | −24,9 % | −36,9 % | 2.688 | 9,9 | 12,2 |

Der Fehler des Mittels liegt nach 20 Jahren bei 490–880 Gulden (2–4 %). Prediger (−12,5 %) und Geldleiher (−8,1 %) wurden nicht verändert: Der Prediger hat 18.475 statt 19.113 Zuwachs – innerhalb des Fehlers des Mittels (875); er lag schon in Messung 2 unter dem Mittel (−6,6 %). Der Wirt kommt weiter spät zum Bürger (13,4 Jahre, im Ziel 3 von 30), holt nach dem Ausbau auf.

**Statisch** (Startpreise, voll mit Gesellen; Mittel 1.706 / 3.042 / 4.942): außerhalb ±15 % liegen Schmied Stufe 1 (+15 %), Wirt (−23 / −16 / −23 %), Parfumeur (+28 / +22 / +21 %), Geldleiher Stufe 1 (−15 %), Gardist (−39 / −39 / −34 %, ohne Geleit- und Kopfgeld) und neu der Dieb ab Stufe 2 (+23 / +24 %; die Rechnung kennt weder Fang noch Gericht – der Dieb zahlt im Mittel 686 im Jahr ans Gericht). Der Tischler ist jetzt in allen Stufen im Band (Stufe 3 vorher +22 %, jetzt −6 %).

### Völker, Startfairness, Häuser, Gauner

- **Völker** (Zuwachs gegen Menschen): Schmied Zwerg +23,1 %, Elb +8,3 %, Halbling −13,1 % (Bonus +5 / 0 / −5); Parfumeur Zwerg −12,6 %, Elb +13,8 %, Halbling −3,5 % (Bonus −5 / +5 / 0). Seit dem Einrichtungskauf verstärkt sich der Bonus beim Schmied (Messung 2: Zwerg +6 %); die Völkerboni bleiben (Entscheidung).
- **Startfairness:** Greybox Spieler mit einem Betrieb 26.410, KI-Häuser 79.083 (−67 %); Probestadt 25.154 gegen 70.265. Wohnhausplatz weiter 0 %.
- **1, 3, 5, 8 Häuser** (Messstadt): je Haus 118.807 / 77.001 / 77.537 / 69.797 nach 20 Jahren, Preisindex 0,846 / 0,865 / 0,869 / 0,875 – 8 gegen 3 Häuser −9 %.
- **KI-Stärke:** Bezug 54.061, KI-Häuser 79.558 (Median 1,47); die KI führt nach 20 Jahren Ø 5 Betriebe; Stand am Ende Bezug Patrizier 4 / Edler 26, KI Patrizier 1 / Edler 89.
- **Gauner, Wache, Bank:** Dieb ausgewogen 2.534 im Jahr ohne, 2.655 mit Wachhaus; wagemutig 2.688 / 2.814. Wache: Sold 2.797 (vorher 2.614), Geleitgeld 328, Kopfgeld 39 im Jahr; Raub und Schaden je KI-Haus 450 ohne, 365 mit Wachhaus (−19 %). Bank: Kasse 7.412, Zins 898, Wechsel 3.550. Kredite weiter 0.
- **Einrichtung simuliert:** Der Vergleich „mit Einrichtung am Start – ohne“ misst seit dem Einrichtungskauf der KI nur noch den Vorsprung durch den früheren Kauf (die Vergleichspartie kauft dieselben Stücke zum Jahresende nach); die kleinen Mehrgewinne dort (−3 bis 121 im Jahr) sind deshalb kein Befund über die Stücke.

### Ziele Z1–Z7

| Ziel | Messung 3 |
|---|---|
| Z1 Gießerei 2 Gesellen, 1.300–1.700 | erfüllt, unverändert (Schmiedewaren nicht verändert): 1.551 allein, 1.317 mit einem Rivalen (Referenzprobe, Test) |
| Z2 Bürger mit einem Betrieb in 8–12 Jahren | erfüllt: Schmied Ø 8,2 (30 von 30); Greybox 80 von 120 im Ziel, die übrigen schneller |
| Z3 Konkurrenz drückt den Preis allmählich | erfüllt (Formel unverändert) |
| Z4 ein Spieler beherrscht den Markt nicht | erfüllt (Mengen unverändert; bei 4 Häusern Einwohnerfaktor 1) |
| Z5 Statut B für Minen +40–50 % | erfüllt (Rohstoffe unverändert, Grube 947 → 1.365, +44 %) |
| Z6 Statut C +15–25 % | verfehlt wie vorher (offener Punkt) |
| Z7 Zwischenwaren weniger, nicht defizitär | erfüllt (Beschlag und Silberring unverändert, 682 gegen 1.966) |

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler Mensch passiv, KI ausgewogen mit Einrichtungskauf; gemessen auf main `821bc97` + Branch `6b27965`, dieselben Werte), Vermögen Ø am Ende:

| | Eisenbart | Eichwald | Kieselbach | Bürgermeister | Stand am Ende | Einrichtung je KI-Haus |
|---|---|---|---|---|---|---|
| ohne Karte, Messung 2 (ohne KI-Einrichtung) | 129.713 | 113.081 | 123.643 | 13/20 | Freiherr 58, Edler 2 | – |
| ohne Karte, jetzt | 165.223 | 118.112 | 158.764 | 16/20 | Freiherr 60; kein Bankrott | Ø 4,8 Stücke |
| Greybox, Messung 2 | 148.254 | 142.556 | 154.716 | 13/20 | Freiherr 60 | – |
| Greybox, jetzt | 179.507 | 166.212 | 185.639 | 15/20 | Freiherr 34, Reichsgraf 26; kein Bankrott | Ø 8,8 Stücke |

Der Einrichtungskauf hebt alle drei Häuser um 20–30 %; Eichwald (Tischler) legt ohne Karte am wenigsten zu, weil seine Waren ab Stufe 2 billiger sind.

## Balancing-Messung 4 (PR #208)

**Kein Wert geändert.** Der Bericht zeigt den Stand nach Hafen und Schiffshandel (#199), Stadtkasse und Anträgen (#200), Wetter (#195) und Karrenverkauf (#205), damit der Nutzer danach entscheidet. Gemessen auf main `90916dc` + Branch `5ce0d97` (nur das Messwerkzeug), **Datenversion 55**. Aufruf `messen bericht 20 30` (1.320 Partien, **24,1 Minuten** auf 8 Fäden) → `docs/messungen/balancing-2026-10-25.json`. Die Messungen 1–3 liegen daneben.

**Neu im Bericht** (nur `tests/**`):
- Abschnitt 8 `hafenKasse`:
  - Exportnachfrage: der passive Spieler (Werkstatt mit den Startgesellen, ohne Führung, ohne Schiff) auf Greybox und Probestadt, dieselben Startwerte mit und ohne Abschnitt `schiffshandel`
  - Schiffe der KI
  - Stadtkasse: Stand, Steuersatz, Posten
  - Lasten je Haus: Steuer, Spitzel, Gericht
  - Karrenverkäufe der KI
- Abschnitt 9 `statutC`: Z6 je Betriebsart, mit Ertrag je Kopf und Gleichstand.
- Abschnitt Berufe:
  - Jahre bis Bürger je Beruf, mit „im Ziel“ und „vor 8“
  - in `jeJahrImMittel` dazu `davonSteuer`, `davonSpitzel` und `schiffeAmEnde`
- Schalter `--ohne=<abschnitt>`: Alle Partien laufen ohne diesen Abschnitt der Daten. Die Diagnose unten nutzt `--ohne=schiffshandel --teile=1,2,5` (570 Partien, 5,6 Minuten).
- **Wetter:** Es wirkt nicht auf den Kern (Entscheidung zu PR #195; der Kern kennt die Jahreszeit nur als Namen). Deshalb gibt es dazu keine Kennzahl.

**Wichtig für alle Abschnitte auf der Greybox:** Die Greybox hat seit #199 Fluss und Hafen. Die KI-Häuser kaufen dort Schiffe, ebenso der von der KI geführte Messspieler „mit einem Betrieb“. `MaxBetriebe` 0 begrenzt Betriebe, keine Schiffe. Am Ende von 20 Jahren hat er im Mittel 0,5–0,8 Schiffe, der Geldleiher 0,03.

### Die vier Leitzahlen

| Kennzahl | Messung 3 (Datenversion 49) | Messung 4 | Messung 4 ohne Schiffshandel (Diagnose) |
|---|---|---|---|
| Jahre bis Bürger (Schmied, ein Betrieb, Ziel 8–12) | Ø 8,2 (30/30); Greybox im Ziel 80/120, Probestadt Ø 9,5 | Ø **8,0** (30/30, im Ziel 17, vorher schon 13); Greybox im Ziel 68/120, Probestadt Ø 8,5 (69/90) | Ø 8,4 (im Ziel 25/30) |
| Berufe im Band ±15 % (simuliert, 20 Jahre) | 10 von 10 (−12,5 … +8,5 %) | **4 von 10** (−28,0 … +46,6 %) | 10 von 10 (−14,6 … +10,4 %) |
| KI ÷ gleiche Strategie (Median nach 20 Jahren) | 1,47; 17/90 über 2× | **1,15**; 2/90 über 2×, keins unter ½, keins bankrott; „Schwer“ 1,35 (vorher 1,79) | 1,49; 15/90; „Schwer“ 1,59 |
| 8 gegen 3 Häuser (Messstadt, Vermögen je Haus, 20 Jahre) | −9 % (69.797 gegen 77.001) | **−3 %** (78.561 gegen 80.791) | – (die Messstadt hat keinen Fluss) |

### Berufe

**Simuliert:** ein Betrieb, von der KI geführt, Greybox, 20 Jahre, 30 Startwerte. Mittel des Zuwachses 27.632 (Messung 3: 21.124; ohne Schiffshandel: 23.419). Der Fehler des Mittels beim Vermögen liegt bei ±680 … ±1.750.

| Beruf | Vermögen 5 / 10 / 20 J. | Zuwachs gegen Mittel | Messung 3 | ohne Schiffshandel | Gewinn Ø je Jahr | Bürger Ø Jahr (im Ziel / vor 8) | Schiffe am Ende |
|---|---|---|---|---|---|---|---|
| Schmied | 6.145 / 13.065 / 43.656 | **+43,5 %** | +6,1 % | +10,4 % | 3.262 | 8,0 (17 / 13) | 0,53 |
| Tischler | 6.716 / 11.403 / 32.700 | +3,9 % | +8,5 % | +4,6 % | 2.775 | 9,7 (23 / 3) | 0,70 |
| Steinmetz | 5.461 / 9.957 / 25.034 | **−23,9 %** | +1,1 % | +3,5 % | 2.429 | 10,9 (25 / 0) | 0,53 |
| Alchimist | 6.934 / 11.566 / 35.409 | +13,7 % | +6,6 % | +8,6 % | 2.907 | 9,4 (22 / 3) | 0,67 |
| Wirt | 5.999 / 9.156 / 32.136 | +1,8 % | +4,2 % | +5,9 % | 2.648 | **12,8** (6 / 0) | 0,50 |
| Prediger | 6.916 / 14.363 / 27.509 | −14,9 % | −12,5 % | −14,6 % | 2.436 | 8,0 (22 / 8) | 0,63 |
| Dieb (ausgewogen) | 7.580 / 13.299 / 27.436 | **−15,2 %** | −4,8 % | −12,3 % | 2.709 | **7,2** (10 / 19) | 0,70 |
| Parfumeur | 6.539 / 12.921 / 44.522 | **+46,6 %** | +3,1 % | +7,0 % | 3.386 | 8,4 (23 / 7) | 0,77 |
| Geldleiher | 8.320 / 11.436 / 24.017 | **−27,6 %** | −8,1 % | −5,7 % | 2.103 | 8,5 (26 / 4) | 0,03 |
| Gardist | 6.826 / 10.778 / 23.906 | **−28,0 %** | −4,1 % | −7,3 % | 1.879 | 10,0 (23 / 1) | 0,43 |
| Dieb wagemutig (nicht im Mittel) | 4.895 / 8.543 / 25.290 | −23,0 % | −24,9 % | −36,1 % | 3.272 | 7,7 (6 / 19) | 1,00 |

**Statisch** (Startpreise, voll mit Gesellen) gleicht Messung 3 Zahl für Zahl, denn am Start hat noch niemand ein Schiff. Außerhalb ±15 % liegen weiter:
- Schmied, Stufe 1: +15 %
- Wirt: −23 / −16 / −23 %
- Parfumeur: +28 / +22 / +21 %
- Geldleiher, Stufe 1: −15 %
- Gardist: −39 / −39 / −34 %
- Dieb ab Stufe 2: +23 / +24 %

**Ursache der Verschiebung:** die Schiffe. Ohne Abschnitt `schiffshandel` liegen alle zehn Berufe wieder im Band, nahe an Messung 3. Die fernen Städte kaufen nur Waren einiger Berufe:
- **Schmied:** Silberring, Silberkette, Goldkette, Rapier in Weidenmünd, Werkzeug in Eisenfurt
- **Parfumeur:** Moschusduft, Fechterduft
- **Tischler:** Armbrust
- **Alchimist:** Rosenwasser
- **Wirt:** Körnermahlzeit, Dünnbier, Starkbier
- **Prediger:** Kerze

Die KI-Schiffe kaufen diese Waren daheim am Markt als Hinfracht. Das hebt dort die Preise für alle Anbieter, und der Messspieler hat dazu oft ein eigenes Schiff. Der **Steinmetz** hat keine Exportware und verliert auch absolut (25.034 gegen 28.232 ohne Schiffe, −11 %); die Rückfracht bringt Bruchstein und Granit billig in die Stadt, die Ursache ist aber nicht weiter zerlegt. Berufe ohne Ware (**Geldleiher, Gardist**, auch der **Dieb**) haben vom Export nichts. Absolut ändert sich bei ihnen wenig (Geldleiher −8 %, Gardist −7 %, Dieb +12 % gegen ohne Schiffe); sie fallen vor allem zurück, weil das Mittel um 18 % steigt.

### Neue Kennzahlen

**Exportnachfrage** (passiver Spieler, Schmied, ohne Schiff; 20 Jahre, 30 Startwerte):

| | Greybox | Probestadt |
|---|---|---|
| Spieler mit / ohne Schiffshandel | 43.079 / 36.480: **+18,1 %** (je Partie q10–q90 +11,5 … +25,7 %) | 39.995 / 33.490: **+19,4 %** (+13,8 … +24,5 %) |
| KI-Häuser mit / ohne | Eisenbart (Schmied) +38,2 %, Eichwald (Tischler) +27,2 %, Kieselbach (Steinmetz) +5,9 % | Ahlbeck (Tischler) +44,8 %, Brendel (Schmied) +38,1 % |
| Schiffe je Partie am Ende, erstes Schiff Ø Jahr | 2,63; Jahr 4,6 | 1,97; Jahr 4,4 |
| Ertrag je Schiff und Jahr nach Unterhalt | 595 | 1.221 |

Mit 30 Jahren (`messen schiffe`, PR #199) waren es +20,7 % bzw. +25,7 % für den passiven Spieler. Der Nutzer hat entschieden, dass die Exportnachfrage gewollt ist. Neu ist, dass sie das Berufsband sprengt (siehe oben).

**Steuer je Haus** (Umsatzsteuer und Glaubenszins, beides geht in die Stadtkasse):

| | Greybox | Probestadt |
|---|---|---|
| KI-Haus | 688 im Jahr (2,3 % des Umsatzes) bei 7.550 Gewinn | 950 (2,6 %) bei 8.411 |
| passiver Spieler | 198 (5,0 %) bei 2.004 | 186 (5,1 %) bei 1.847 |
| Spieler mit einem Betrieb (Berufe) | 259 im Mittel der Berufe, Geldleiher 124, Gardist 136, Dieb 40 | – |

Die KI zahlt im Verhältnis weniger Steuer. Gründe nach den Regeln (nicht einzeln gemessen):
- Die fernen Städte erheben keine Steuer.
- Als Marktmeister zahlt sie die halbe Steuer.
- Der Glaubenszins zählt mit; der passive Spieler zahlt ihn, wenn er nicht den Stadtglauben hat.

Dass der passive Spieler 5,0 % seines Umsatzes zahlt, mehr als den mittleren Satz von 4,35 %, ist nicht zerlegt (Glaubenszins nur 27 im Jahr für die ganze Stadt).

**Stadtkasse** (Partien mit Schiffshandel):

| | Greybox | Probestadt |
|---|---|---|
| Stand Ø Jahr 1 / 5 / 10 / 20 | 1.138 / −5.495 / −396 / 4.652 | 862 / −6.746 / −7.905 / 4.130 |
| Jahre im Minus je Partie | 8,7 | 15,2 |
| Steuersatz am Ende (Ø über die Jahre) | 3 % in allen 30 (4,35 %) | 3 % in 6, 4 % in 13, 5 % in 9, 6 % in 2 (5,02 %) |
| Einnahmen je Jahr | Steuer 2.235, Hafenzoll 665, Bußen 395, Glaubenszins 27, Gebühren 20 | Steuer 2.086, Hafenzoll 649, Bußen 196, Gebühren 12 |
| Ausgaben je Jahr | Gehälter 2.700, Bau 310, Feste 250 | Gehälter 2.700, Bau 119, Feste 68 |

In der Probestadt mit zwei KI-Häusern decken die Einnahmen die Gehälter (2.700) lange nicht. Die Kasse ist 15 von 20 Jahren im Minus, und die Steuer steht am Ende in 24 von 30 Partien über 3 %.

**Spitzel und Gericht je Haus** (Mittel je Jahr; „Gericht“ = Spitzel + Gebühren + Bußen + Entschädigungen, negativ = Einnahme):

| | Spitzel | Gericht gesamt | Gewinn | Familie |
|---|---|---|---|---|
| Spieler mit einem Betrieb (Mittel der Berufe) | 196 | 430 | 2.653 | ≈ 360 |
| … Dieb ausgewogen / wagemutig | 213 / 78 | 731 / 1.367 | 2.709 / 3.272 | 403 / 349 |
| … Gardist | 126 | 195 | 1.879 | 352 |
| KI-Haus Greybox / Probestadt | 179 / 196 | 340 / 299 | 7.550 / 8.411 | ≈ 390 |

**Jahre bis Bürger je Beruf:** siehe Tabelle Berufe. Im Ziel 8–12 Jahre liegen:
- 26 von 30 beim Geldleiher, 25 beim Steinmetz, 23 bei Tischler, Parfumeur und Gardist, 22 bei Alchimist und Prediger, 17 beim Schmied
- 10 beim Dieb (19 schon vor 8 Jahren)
- **6 beim Wirt** (keiner vor 8)

**Z6, Statut C gegen B** (Startpreise, Kalkulation): Statut C bringt in 19 von 21 Werkstätten **0 %** mehr, Kirche +1,5 %, Schänke +3,3 %; die Gießerei hat unter B und C je 1.966. Ertrag je Kopf und Jahr in der Gießerei:

| Geselle | Leibeigener | Lehrling | Ork |
|---|---|---|---|
| 1.023 | 970 | 713 | 604 |

Der Leibeigene kostet weniger (Unterhalt 50 + 6 % × 600 Aufstand = 86 statt 220 Lohn), arbeitet aber nur mit Tempo 0,85. Gleichstand ist bei einer Wertschöpfung von **893** Gulden je Kopf und Jahr (Wirtsberufe mit der längeren Schicht: 1.082). Heute schafft ein Geselle in den Werkstätten 1.126–1.756, nur in der Kirche (835) und in der Schänke (940) weniger – dort gewinnt der Leibeigene knapp. Im Umland bleibt der Ork die beste Kraft (759 gegen Leibeigener 527).

**Karren verkaufen:** In 1.320 Partien verkauft die KI 9 Karren in Not und 2 nach einem Jahr ohne Fahrt (in 7 Partien). Die Regel wirkt in der Messung kaum.

### Diagnose zu den offenen Balancing-Punkten (Entscheidungen beim Nutzer)

- **Z6 (Statut C +15–25 %):** verfehlt, 0 %. Ursache: Der Leibeigene spart einen festen Betrag (134 Gulden je Kopf und Jahr), verliert aber 15 % der Leistung. Ab 893 Gulden Wertschöpfung je Kopf gewinnt der Geselle, und seit den höheren Warenpreisen von PR #177 liegen alle Werkstätten darüber. Für +20 % in der Gießerei bräuchte der Leibeigene Tempo ≈ 1,05 (schneller als der Geselle) oder Kosten unter null. Mit den heutigen Größen ist Z6 also nur über eine andere Art von Vorteil zu erreichen (z. B. ein Anteil statt eines festen Betrags), sonst muss das Ziel neu gefasst werden.
- **Statisch außerhalb ±15 %:** unverändert gegen Messung 3, die Ursachen ebenso. Beim Gardisten fehlen in der Rechnung Geleit- (344 im Jahr) und Kopfgeld (32), beim Dieb Fang und Gericht (731 im Jahr), beim Wirt fast der ganze Ausschank. Simuliert lagen sie in Messung 3 im Band. Jetzt liegen sie nur ohne Schiffshandel im Band; mit Schiffen ist der Gardist −28 %, siehe Exportnachfrage.
- **Exportnachfrage:** Der passive Spieler hat +18 % (Greybox) bzw. +19 % (Probestadt), das ist gewollt. Neu sichtbar: Der Export hebt **Schmied (+44 %) und Parfumeur (+47 %)** weit über das Band und drückt Steinmetz (−24 %), Geldleiher (−28 %) und Gardist (−28 %) darunter. Ohne Schiffe sind alle zehn im Band. Ursache ist, welche Waren die fernen Städte kaufen: Schmied 5 Waren, Parfumeur 2 teure, Steinmetz, Geldleiher, Gardist und Dieb keine. Dazu kauft der Messspieler „mit einem Betrieb“ Schiffe (0,5–0,8 je Haus), weil `MaxBetriebe` keine Schiffe begrenzt. Zu entscheiden sind zwei Dinge: ob jede Berufsgruppe eine Exportware bekommt bzw. die Faktoren für Schmied- und Parfumeurwaren sinken, und ob der Messspieler ohne Schiff gemessen werden soll (Spiel-KI). Die KI ÷ Bezug sinkt auf 1,15, weil der Bezugsspieler Schmied ist.
- **Einrichtung über 5 Jahre:** Lederhandschuhe Wirt 9,2 J., Prediger 10,4 J. (Alchimist 4,8, Parfumeur 4,3), Rechenbretter 8 J. Das Löschfass zahlt sich nie aus, weil 0,0014 Sabotagen je Betrieb und Jahr gemessen werden. Wie in Messung 3.
- **Völker** (Zuwachs gegen Menschen):

  | Volk und Beruf | Bonus | Messung 4 | Messung 3 | ohne Schiffe |
  |---|---|---|---|---|
  | Zwerg-Schmied | +5 % | **+11,0 %** | +23,1 % | +12,7 % |
  | Halbling-Schmied | −5 % | **−17,0 %** | −13,1 % | −24,0 % |
  | Elb-Parfumeur | +5 % | +13,2 % | +13,8 % | +17,4 % |
  | Zwerg-Parfumeur | −5 % | −10,6 % | −12,6 % | −14,8 % |

  Der Fehler des Abstands liegt bei etwa ±4 %. Weiter wirkt das Volk zwei- bis dreimal so stark wie sein Bonus. Ursache: Der Bonus wirkt auf die Leistung, und der Gewinn ist nur ein Teil des Umsatzes – +5 % Leistung sind deshalb mehr als +5 % Gewinn; dazu kommen Familie und Talente. Der Zwerg-Schmied liegt näher am Bonus als in Messung 3, der Halbling-Schmied weiter weg.
- **Familie, Gericht, Spitzel:**
  - Ein Haus mit einem Betrieb zahlt im Jahr ≈ 360 für die Familie, 196 für Spitzel und 430 fürs Gericht insgesamt. Das sind 14 %, 7 % und 16 % seines Gewinns (2.653).
  - Die Spitzel kauft die KI selbst, die den Messspieler führt: 196 im Jahr, also in zwei von drei Jahren ein Spitzel zu 300.
  - Die KI-Häuser zahlen 179–196 für Spitzel, bei rund dreimal so viel Gewinn.
  - Spitzel kosten nur den Auftraggeber: Ein Mensch, der keine ansetzt, spart fast die Hälfte der Gerichtskosten (der passive Spieler zahlt gar nichts ans Gericht).
  - Weniger Spitzel der KI änderten also vor allem die KI-Häuser und den von der KI geführten Messspieler.
- **Wirt bis zum Bürger:** Ø 12,8 Jahre (Messung 3: 13,4), im Ziel 6 von 30 (vorher 3). Ursache: Stufe 1 ist schwach (statisch −23 %), der Gewinn liegt in Jahr 5 bei −25 % und in Jahr 10 bei −42 % gegen das Mittel. Stufe 2 erreicht der Wirt erst nach Ø 11,0 Jahren, danach liegt er vorn (+16 % Gewinn in Jahr 20). Ohne Schiffe ist es gleich (12,9).
- **Dieb bis zum Bürger:** Ø 7,2 Jahre (Messung 3: 7,5), 19 von 30 schon vor 8 Jahren. Er ist früh stark (Gewinn in Jahr 5 +30 % gegen das Mittel, Beute 2.879 im Jahr) und fällt nach dem Ausbau zurück (Jahr 20: −32 %). Das Gericht kostet ihn 731 im Jahr, das meiste aller Berufe.
- **Dieb und Gardist ohne Einrichtung mit Gulden-Rechnung:** unverändert. Der Dieb hat kein Ertragsstück, der Gardist nur die Waffenkammer (statisch 88 im Jahr). Fänge des Diebs 0,59 im Jahr, mit Wachhaus 0,56.
- **„Einrichtung simuliert“:** misst weiter nur den Vorsprung durch den früheren Kauf: 6,8–34 Jahre bis bezahlt, Parfumeur −7 im Jahr. Das ist kein Befund über die Stücke; offen wie in Messung 3.
- **Gauner, Wache, Bank** (zur Einordnung):
  - Dieb ausgewogen 2.709 im Jahr ohne, 2.954 mit Wachhaus
  - Wache: Sold 2.801, Geleitgeld 344, Kopfgeld 32 im Jahr
  - Raub und Schaden je KI-Haus 541 ohne, 462 mit Wachhaus (−15 %)
  - Hinterzimmer: Diebstahl 0,74 je Partie und Jahr zu 61 %, Einbruch ins Wohnhaus 0,57 zu 47 %
  - Kredite weiter 0

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler Mensch passiv, KI ausgewogen; gemessen auf main `90916dc`, Messwerkzeug unverändert, 35 bzw. 60 Sekunden), Vermögen Ø am Ende:

| | Eisenbart | Eichwald | Kieselbach | Bürgermeister | Stand der KI am Ende | Stadtkasse Jahr 30 | Steuersatz am Ende |
|---|---|---|---|---|---|---|---|
| ohne Karte, Messung zu PR #200 | 169.450 | 126.362 | 163.933 | 16/20 | | | |
| ohne Karte, jetzt | 167.686 | 127.672 | 167.165 | 12/20 ab 18,2 | Freiherr 60; kein Bankrott | 3.741 | 3 % in 18, 4 % in 2 |
| Greybox, Messung zu PR #200 | 270.476 | 199.521 | 208.188 | 14/20 | | | |
| Greybox, jetzt | 256.097 | 211.623 | 205.020 | 14/20 ab 16,2 | Reichsgraf 54, Freiherr 5, Edler 1; kein Bankrott | 7.671 | 3 % in 20 |

Gegen die Messung zu PR #200 weichen die Werte um −5 … +6 % ab; die Ursache ist hier nicht untersucht (PR #202 und #205 melden ihre Messungen zeichengleich mit main). Die Greybox hat im Mittel 2,6 Schiffe der KI je Partie; Hafenzoll 816, Steuer 2.640 und Bußen 347 im Jahr.

## Balancing-Messung 5 (PR #208, Spieltag 4–23 Uhr)

**Was sich geändert hat:** nur der Spieltag (Entscheidung des Nutzers vom 26.10.2026, Umsetzung in `docs/bereiche/kern.md`, „Spieltag 4–23 Uhr (Umsetzung)“). Gespielt wird von 04:00 bis 23:00 Uhr, 1140 statt 1020 Minuten je Jahr. Was je Jahr gilt, bleibt je Jahr gleich (Datenversion 56, Abschnitt „Zeitbasis“ oben). Kein Wert des Balancings geändert.

Gemessen auf main `8fd6fa3` (darin seit Messung 4 nur die Nachtlichter, kein Kern) + Branch `3aaa048`, **Datenversion 56**. `messen bericht 20 30`: 1.320 Partien in 28,8 Minuten → `docs/messungen/balancing-2026-10-26-spieltag.json`. `messen 30 20` lief 37 Sekunden, `messen 30 20 greybox` 63 Sekunden.

### Was der längere Tag bewirkt

**Gaunerei: praktisch nichts.** Alle Gaunerstücke geschehen weiter einmal je Nacht um 20 Uhr: Hinterzimmer, eigene Diebe und Giftanschläge. Die Taschendiebe stehlen in der Arbeitszeit, der Sold zählt die Arbeitsminuten. Die drei Stunden Dunkle Nacht verlängern nur den Faktor 2 auf Überfälle. Er trifft Umlandkarren, die nachts nur mit Nachtfahrt-Erlaubnis fahren (Torschluss 20 Uhr). Alle Unterschiede liegen im Rauschen:

| | Messung 4 | Messung 5 |
|---|---|---|
| Hinterzimmer je Partie und Jahr (Greybox, Strategie der KI) | Diebstahl 0,735 zu 61 %, Einbruch ins Wohnhaus 0,568 zu 47 %, Sabotage 0,033 | 0,728 zu 61 %, 0,578 zu 45 %, 0,018 |
| Eigene Diebe (wagemutig) je Partie und Jahr | Einbruch 0,96, Auskundschaften 0,36, Brand 0,056 | 0,95, 0,35, 0,063 |
| Raub je Betrieb und Jahr | 79 | 82 |
| Dieb ausgewogen: Gewinn, Beute, Fänge, Gericht je Jahr | 2.709, 3.865, 0,59, 731 | 2.846, 3.872, 0,59, 696 |
| Dieb wagemutig: Gewinn, Gericht | 3.272, 1.367 | 3.375, 1.369 |
| Gardist: Sold, Geleit, Kopfgeld, Razzien, Gericht | 2.801, 344, 32, 0,74, 195 | 2.803, 345, 21, 0,72, 206 |
| Raub und Schaden je KI-Haus ohne / mit Wachhaus | 541 / 462 | 551 / 471 |
| KI-Haus Greybox: Steuer, Spitzel, Gericht je Jahr | 688, 179, 340 | 687, 181, 337 |
| Stadtkasse Greybox: Bußen je Jahr, Stand Jahr 20 | 395, 4.652 | 407, 4.740 |
| `messen 30 20`: Gaunerstücke der KI in 20 Partien, Raub beim Spieler | 593, 2.884 | 597, 2.886 |
| `messen 30 20 greybox`: dasselbe | 555, 3.195 | 584, 3.373 |

**Schiffe fahren mehr:** Schiffe legen zu jeder Stunde ab, ihre Fahrtzeiten in Minuten bleiben. Im längeren Jahr schaffen sie also mehr Fahrten:
- Ertrag je Schiff und Jahr: Greybox 596 (vorher 595), Probestadt 1.300 (vorher 1.221).
- KI-Häuser mit Schiffshandel in der Probestadt: +50,9 % (vorher +41,4 %).
- `messen 30 20 greybox`, Eisenbart: 273.621 statt 256.097 (+6,8 %).

### Die vier Leitzahlen

| Kennzahl | Messung 4 | Messung 5 |
|---|---|---|
| Jahre bis Bürger (Schmied, ein Betrieb) | Ø 8,0, im Ziel 17/30 | Ø 7,9, im Ziel 17/30; Greybox 68/120, Probestadt Ø 8,6 (69/90) |
| Berufe im Band ±15 % | 4 von 10 (−28,0 … +46,6 %) | **6 von 10** (−28,9 … +47,1 %) |
| KI ÷ gleiche Strategie (Median) | 1,15; 2/90 über 2× | 1,11; 2/90 über 2×, 4/90 unter ½, keins bankrott; „Schwer“ 1,19 (vorher 1,35) |
| 8 gegen 3 Häuser | −3 % | −1 % (80.183 gegen 80.675) |

### Berufe

Simuliert, ein Betrieb, Greybox, 20 Jahre, 30 Startwerte. Mittel des Zuwachses 28.854 (Messung 4: 27.632). Fehler des Mittels beim Vermögen ±800 … ±2.400.

| Beruf | Vermögen 20 J. | Zuwachs gegen Mittel | Messung 4 | Gewinn Ø | Gericht Ø (davon Spitzel) | Bürger Ø (im Ziel) | Schiffe am Ende |
|---|---|---|---|---|---|---|---|
| Schmied | 42.604 | **+33,8 %** | +43,5 % | 3.244 | 385 (203) | 7,9 (17) | 0,70 |
| Tischler | 36.111 | +11,3 % | +3,9 % | 2.861 | 434 (201) | 8,8 (27) | 0,43 |
| Steinmetz | 28.703 | −14,4 % | −23,9 % | 2.509 | 413 (200) | 11,0 (25) | 0,33 |
| Alchimist | 36.603 | +13,0 % | +13,7 % | 2.967 | 426 (208) | 9,9 (18) | 0,53 |
| Wirt | 28.683 | −14,5 % | +1,8 % | 2.515 | 388 (199) | **13,3** (4) | 0,50 |
| Prediger | 29.809 | −10,6 % | −14,9 % | 2.541 | 426 (212) | 7,6 (18) | 0,43 |
| Dieb | 30.066 | −9,7 % | −15,2 % | 2.846 | 696 (211) | 8,7 (9) | 0,67 |
| Parfumeur | 46.450 | **+47,1 %** | +46,6 % | 3.409 | 403 (202) | 8,2 (17) | 0,60 |
| Geldleiher | 24.524 | **−28,9 %** | −27,6 % | 2.096 | 438 (213) | 8,6 (29) | 0,00 |
| Gardist | 24.989 | **−27,3 %** | −28,0 % | 1.924 | 206 (128) | 9,4 (26) | 0,27 |

Die Verschiebungen bei Steinmetz (+3.700) und Wirt (−3.500) liegen bei etwa zwei Fehlern des Mittels. Ein Grund im Spieltag ist nicht zu sehen: Gäste, Arbeitszeit und Markt sind je Jahr gleich. Statisch ändert sich nichts (Startpreise).

### Die Befunde aus Messung 4, neu geprüft

- **(a) Export und Berufsband:** weiter außerhalb ±15 % sind Schmied +34 %, Parfumeur +47 %, Geldleiher −29 % und Gardist −27 %. Der Steinmetz liegt jetzt knapp im Band (−14 %). Die Ursache bleibt: Die fernen Städte kaufen nur Waren einiger Berufe.
- **(b) Messspieler mit Schiff:** Er hat am Ende 0,3–0,7 Schiffe je Beruf, der Geldleiher keines.
- **(c) Passiver Spieler ohne Schiff:** Greybox +16,1 %, Probestadt +22,1 % (vorher +18,1 % bzw. +19,4 %). Ertrag je Schiff 596 bzw. 1.300.
- **(d) Z6:** 0 %, unverändert.
- **(e) Wirt:** im Mittel 13,3 Jahre bis zum Bürger, im Ziel 4 von 30 (vorher 12,8 und 6). Der Gewinn liegt in Jahr 5 bei −26 % und in Jahr 10 bei −46 % gegen das Mittel; Stufe 2 erreicht er nach 11,0 Jahren.
- **(f) Dieb:** im Mittel 8,7 Jahre, 14 von 30 vor dem achten Jahr (vorher 7,2 und 19). Die Gewinne der Stichjahre streuen stark (Fehler des Mittels ±560–810), die Beute ist gleich (2.881 in Jahr 5).
- **(g) Spitzel und Gericht:** Ein Haus mit einem Betrieb zahlt 198 für Spitzel und 422 fürs Gericht insgesamt, bei 2.691 Gewinn.
- **(h) Stadtkasse der Probestadt:** 14,3 von 20 Jahren im Minus, die Steuer steht am Ende in 14 von 30 Partien über 3 % (vorher 15,2 Jahre und 24 Partien).
- **Völker:**
  - Schmied: Zwerg +13,3 %, Elb +3,7 %, Halbling −12,5 %
  - Parfumeur: Zwerg −16,8 %, Elb +5,0 %, Halbling −4,4 %
- **Karren verkaufen:** In Not verkauft die KI 19 Karren, ungenutzt 3, in 21 Partien (vorher 9 und 2).

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler Mensch passiv), Vermögen Ø am Ende:

| | Eisenbart | Eichwald | Kieselbach | Bürgermeister | Stand der KI am Ende | Stadtkasse Jahr 30 |
|---|---|---|---|---|---|---|
| ohne Karte, Messung 4 | 167.686 | 127.672 | 167.165 | 12/20 | Freiherr 60 | 3.741 |
| ohne Karte, jetzt | 164.501 | 125.484 | 162.812 | 13/20 ab 16,2 | Freiherr 59, Reichsgraf 1; kein Bankrott | 2.064 |
| Greybox, Messung 4 | 256.097 | 211.623 | 205.020 | 14/20 | Reichsgraf 54, Freiherr 5, Edler 1 | 7.671 |
| Greybox, jetzt | 273.621 | 223.767 | 193.792 | 15/20 ab 15,5 | Reichsgraf 54, Freiherr 5, Edler 1; kein Bankrott | 9.847 |

## 7. Ergebnisse von Entwurf 1

| Szenario | Gewinn/Jahr |
|---|---|
| Gießerei, 1 Lehrling, Silberring | 338 |
| Gießerei, 2 Gesellen, Silberring, keine Konkurrenz | 878 |
| … mit 30 Stück Konkurrenz (≈ 1 Rivale) | 627 |
| … mit 120 Stück Konkurrenz (≈ 4 Rivalen) | 163 |
| Gießerei, 2 Gesellen, Rapier, 30 Stück Konkurrenz | 379 |
| Gießerei, 2 Leibeigene (Statut C), Silberring, 30 Stück Konkurrenz | 750 |
| Mine, 3 Bergleute (Statut A) | 1.139 |
| Mine, 3 Orks (Statut B) | 1.613 |

Geprüft mit einem Python-Probelauf und identisch nachgerechnet im JavaScript-Modell des Rechners.

**Nachgerechnet in der Simulation (Phase 2, Zutaten zum Marktpreis, ±3 %):**
- Gießerei allein: 812
- 1 Lehrling: 316
- zwei konkurrierende Gießereien: je 570
- Mine mit 3 Bergleuten: 1.139
- Mine mit 3 Orks vor Ausbrüchen: 1.709

Das sind automatische Tests im Repository.

## 8. Bewusste Vereinfachungen und nächste Schritte

1. ~~**Transport und Überfälle fehlen.**~~ → umgesetzt, siehe Abschnitt 9 ✔. Karren, Wege und Überfälle auf Umland-Transporte machen die Mine später riskanter. Das ist ihr Ausgleich für den höheren Gewinn. Die Überfälle verüben **Banditen und Räuber aller Völker**, auch gemischte Banden, dazu wilde Ork-Rotten und Oger aus der Wildnis.
2. ~~Gleichgewichts-Rechnung statt Tagesverlauf~~ → Phase 2 simuliert Minute für Minute mit gleitendem 24-Stunden-Fenster ✔. Lager und Karren folgen.
3. **Behandlung („Knute“)** aus dem Original fehlt noch. Sie soll Tempo, Risiko und Ansehen beeinflussen.
4. **Weitere Gebäudestufen** (Schmiede, Goldschmiede) und Waren (Silberkette, Großschwert …) kommen nach dem Vertical Slice.
5. **Startvermögen 4.000** (Haus, Gießerei, Bargeld) ist eine Annahme. Sie wird mit den Titelschwellen zusammen festgelegt.

**Phase 2** setzt dieses Modell im Simulationskern um:
- `data/wirtschaft.json` aus dem Rechner-Export
- Klassen `Ware`, `Marktstand`, `Betrieb` und `Arbeitskraft`
- Tests, die die Ergebnisse aus Abschnitt 7 nachrechnen

## Balancing-Messung 6 (PR #208, Balancing 2)

**Was sich geändert hat** (Entscheidungen des Nutzers nach Messung 5, `docs/bereiche/kern.md`, „Balancing 2“; **Datenversion 57**):
1. **Gaunerei je Nachtstunde:** Jeder Auftrag aus dem Hinterzimmer und jeder Einbruch der eigenen Diebe wird um 20, 21, 22 und 23 Uhr versucht, jedes Mal mit ⅓ der Chance (`gauner.nachtVersuche` 4, `versuchFaktor` 0,3333). Jeder Erfolg wirkt und hinterlässt eine Spur; gelingt die ganze Nacht nichts, bleibt eine Spur des Versuchs. Überfall, Auskundschaften, Entführung, Schutzgeld-Brand und Gift bleiben einmalig um 20 Uhr.
2. **Schiffe:** Fahrtzeiten × 1140/1020 (flussab 67, flussauf 112, Beladen 22, Liegen 34 Minuten): Eisenfurt 270, Weidenmünd 324 Minuten.
3. **Export:**
   - Weidenmünd zahlt für Silberring, Silberkette und Rapier ×1,4 (vorher 1,6), Goldkette ×1,35 (1,55), Rosenwasser ×1,45 (1,6), Moschusduft ×1,25 (1,6), Fechterduft ×1,2 (1,55).
   - Eisenfurt zahlt für Werkzeug ×1,4 (1,6).
   - Neu kaufen die fernen Städte Steinmetzwaren: Eisenfurt Grabstein und Mühlstein ×1,4, Weidenmünd Taufbecken und Steinfigur ×1,35.
   - Ohne Export leicht angehoben: Wechselgebühr 0,78 % (0,68 %), Sold 0,84 je Minute (0,73), Taschendiebe 1,08 je Minute (1,0).
4. **Heimatmarkt:** Hinfracht, die ein Schiff daheim am Markt kauft, fehlt dort nur zu 85 % im Angebot (`schiffshandel.heimatmarktAnteil`). Ihr Preis steigt dadurch langsamer.
5. **Wirt Stufe 1:** Körnermahlzeit 54 (vorher 50), Dünnbier 91 (85). Die Waren ab Stufe 2 bleiben.
6. **Spitzel der KI** (Spiel-KI): nur gegen ein Haus vor ihr in der Rangliste oder einen Feind, höchstens jedes zweite Jahr.
7. **Stadtkasse:** Volle Amtsgehälter zahlt erst eine Stadt mit sechs Häusern. Darunter zahlt sie nach den Einwohnern (`politik.kasse.gehaltVolleHaeuser`): drei Häuser 0,7, vier 0,8, fünf 0,9.
8. **Z6 neu gefasst:** Statut C senkt die Lohnkosten bei gleicher Arbeit im Umland (siehe Ziele oben). Die Werte bleiben.

Die Werte unter 3 und 5 sind in fünf Probeläufen gefunden (`--teile=1,8`, Berufe und Hafen). Einmal waren Steinmetz und Parfumeur außerhalb, einmal Dieb und Gardist, einmal Schmied und Parfumeur. Die Streuung der Berufe zwischen zwei Läufen mit denselben Werten liegt bei ±5 Punkten (Fehler des Mittels ±650 … ±1.900).

Gemessen auf main `ee3c346` + Branch `c82f079`, Datenversion 57:
- `messen bericht 20 30`: 1.320 Partien in 23,6 Minuten → `docs/messungen/balancing-2026-10-26-balancing2.json`
- `messen 30 20`: 35 Sekunden
- `messen 30 20 greybox`: 57 Sekunden

### Gegen die Ziele

| Ziel (Entscheidung) | Messung 5 | Messung 6 |
|---|---|---|
| Berufe 10/10 im Band ±15 % mit Schiffshandel | 6/10 (−28,9 … +47,1 %) | **10/10** (−14,1 … +8,0 %) |
| Passiver Spieler +8 bis +10 % durch die Exportnachfrage | Greybox +16,1 %, Probestadt +22,1 % | **+8,2 %**, **+7,1 %** |
| Wirt bis zum Bürger Ø ≈ 11 Jahre | 13,3 (4/30 im Ziel) | **10,1** (16/30 im Ziel) |
| Spitzel je Haus mit einem Betrieb ≈ 100 im Jahr | 198 | **101** |
| Stadtkasse im Mittel 0 bis leicht plus, Steuer meist 3 % | Greybox: Stand Jahr 5/10/20 −5.086 / 630 / 4.740, Steuer 3 % in 30/30. Probestadt: −6.704 / −7.134 / 3.421, Steuer 3 % in 16/30 | Greybox: Mittel aller Jahre **+1.720**, Steuer 3 % in 30/30. Probestadt: **+346**, Steuer 3 % in 30/30 |
| Gaunerei etwa ein Drittel mehr | – | siehe unten |
| Schiffe: je Jahr gleich viele Fahrten | Ertrag je Schiff 596 / 1.300 | 555 / 968 (Fahrtzeiten länger, Fernpreise gedämpft) |

### Gaunerei

| | Messung 5 | Messung 6 |
|---|---|---|
| Hinterzimmer der KI, Aufträge je Partie und Jahr (Greybox) | Diebstahl 0,73 zu 61 %, Einbruch ins Wohnhaus 0,58 zu 45 % | 0,63 zu 63 %, 0,77 zu 41 % (Chance je Versuch ⅓ davon) |
| Bilanz des Hinterzimmers je Partie (`messen 30 20` / Greybox) | 410 / 729 | **1.134 / 1.135** |
| Gaunerstücke der KI in 20 Partien (`messen 30 20` / Greybox) | 597 / 584 | 686 / 709 |
| Raub und Schäden beim passiven Spieler (`messen 30 20` / Greybox) | 2.886 / 3.373 | 2.890 / 2.999 |
| Raub je Betrieb und Jahr (Bericht, Strategie der KI) | 82 | 69 |
| Dieb ausgewogen: Gewinn, Beute, Fänge, Gericht je Jahr | 2.846, 3.872, 0,59, 696 | 3.003, 4.190 (Beute 1,08), 0,58, 591 |
| Dieb wagemutig: Gewinn, Gericht | 3.375, 1.369 | 3.385, 1.302 |
| Gardist: Sold, Geleit, Kopfgeld, Razzien, Gericht | 2.803, 345, 21, 0,72, 206 | 3.227 (Sold 0,84), 317, 47, 1,01, 134 |
| Raub und Schaden je KI-Haus ohne / mit Wachhaus | 551 / 471 | 513 / 425 |
| Gericht je Haus mit einem Betrieb (davon Spitzel) | 422 (198) | 320 (101) |

**Zur Hinterzimmer-Bilanz:** Sie zählt nur Haus Eisenbart, das erste KI-Haus. Gerechnet werden Hehlererlös und Beute minus Bandenkosten und Schweigegeld, Mittel je Partie über 30 Jahre, also nur etwa 14 → 38 Gulden im Jahr. Weil die Kosten je Auftrag gleich bleiben, landet das Drittel mehr Beute ganz in diesem kleinen Netto-Rest. Deshalb wirkt es fast wie das Dreifache, und die Opfer trifft es kaum (Raub und Schaden beim passiven Spieler 2.886 → 2.890 bzw. 3.373 → 2.999).

**Was das zeigt:**
- Die Banden haben mehr Erfolg: Die Bilanz des Hinterzimmers ist um gut die Hälfte bis fast das Dreifache gestiegen, und die KI beauftragt mehr Gaunerstücke (+15 bzw. +21 %).
- Beim Opfer kommt davon wenig mehr an. Der zweite und dritte Diebstahl einer Nacht holt aus einem schon geplünderten Lager weniger, und der Raub je Betrieb sinkt sogar (82 → 69).
- Die KI wählt öfter den Einbruch ins Wohnhaus (Bargeld). Ihre Erwartung rechnet weiter mit einer Chance je Nacht; das ⅓ je Versuch und die vier Versuche kennt sie nicht.
- Das Gericht kostet weniger, weil die Spitzel halbiert sind.

### Berufe

Simuliert: ein Betrieb, Greybox, 20 Jahre, 30 Startwerte. Mittel des Zuwachses 30.542 (Messung 5: 28.854).

| Beruf | Vermögen 20 J. | Zuwachs gegen Mittel | Messung 5 | Gewinn Ø | Gericht (Spitzel) | Bürger Ø (im Ziel / vor 8) | Schiffe am Ende |
|---|---|---|---|---|---|---|---|
| Schmied | 32.992 | −5,1 % | +33,8 % | 2.719 | 342 (103) | 8,7 (27 / 3) | 0,77 |
| Tischler | 35.445 | +3,0 % | +11,3 % | 2.786 | 346 (106) | 8,8 (24 / 4) | 0,43 |
| Steinmetz | 36.984 | +8,0 % | −14,4 % | 2.794 | 267 (106) | 9,7 (26 / 1) | 0,70 |
| Alchimist | 36.109 | +5,1 % | +13,0 % | 2.793 | 316 (108) | 9,0 (20 / 6) | 0,60 |
| Wirt | 33.990 | −1,8 % | −14,5 % | 2.705 | 295 (102) | **10,1** (16 / 5) | 0,57 |
| Prediger | 35.604 | +3,5 % | −10,6 % | 2.709 | 306 (103) | 8,4 (19 / 9) | 0,43 |
| Dieb | 35.224 | +2,2 % | −9,7 % | 3.003 | 591 (110) | 6,4 (7 / 23) | 0,63 |
| Parfumeur | 36.172 | +5,3 % | +47,1 % | 2.808 | 319 (102) | 8,3 (18 / 11) | 0,57 |
| Geldleiher | 30.237 | −14,1 % | −28,9 % | 2.293 | 287 (108) | 8,2 (18 / 12) | 0,17 |
| Gardist | 32.667 | −6,1 % | −27,3 % | 2.284 | 134 (57) | 8,1 (12 / 16) | 0,40 |
| Dieb wagemutig (nicht im Mittel) | 28.242 | −20,6 % | −23,5 % | 3.385 | 1.302 (62) | 9,1 (19 / 8) | 0,93 |

**Statisch** (Startpreise) liegen jetzt außerhalb ±15 %:
- Dieb: +19 / +31 / +31 % (die Rechnung kennt weder Fang noch Gericht)
- Parfumeur: +21 / +17 / +16 %
- Gardist: −26 / −26 / −22 % (ohne Geleit- und Kopfgeld)
- Wirt ab Stufe 2: −19 / −26 %

Der Geldleiher liegt mit −14,1 % knapp im Band. Der Dieb kommt früher zum Bürger (6,4 Jahre, 23 von 30 vor dem achten Jahr), weil die Beute um 8 % gestiegen ist.

### Übrige Leitzahlen

| | Messung 5 | Messung 6 |
|---|---|---|
| Schmied bis Bürger (Greybox: im Ziel von 120; Probestadt Ø) | Ø 7,9 (68/120), Probestadt Ø 8,6 | Ø 8,7 (**108/120**), Probestadt Ø 9,6 (81/90) |
| KI ÷ gleiche Strategie (Median) | 1,11; 2/90 über 2×; „Schwer“ 1,19 | 1,24; 9/90 über 2×, 0 unter ½; „Schwer“ 1,30 |
| 8 gegen 3 Häuser | −1 % | −3 % (83.859 gegen 86.695) |
| Exportnachfrage, KI-Häuser mit / ohne Schiffshandel | Greybox +25,1 %, Probestadt +50,9 % | +8,5 %, +23,6 % |
| Karren verkauft (in Not / ein Jahr ohne Fahrt) | 19 / 3 | 10 / 45 |

**Völker** (Zuwachs gegen Menschen):
- Schmied: Zwerg +31,0 %, Elb +24,9 %, Halbling −1,0 %
- Parfumeur: Zwerg −12,8 %, Elb +14,3 %, Halbling −10,1 %

Die Spanne ist größer geworden. Beim Elb-Schmied ohne Bonus (+25 %) ist kein Grund im Volk zu sehen. Das Mittel des Menschen-Schmieds liegt mit 32.992 niedrig (Fehler ±1.245), der Abstand ist also zu einem guten Teil Streuung.

**Z6 neu:** Kosten je Arbeit im Umland (Lohn und Risiko ÷ Tempo):
- Leibeigener 101
- Bergmann 150 (Leibeigener −33 %)
- Ork 114 (Leibeigener −11 %)

In den Werkstätten bringt Statut C weiter 0 % (Gleichstand Leibeigener/Geselle bei 893 Wertschöpfung).

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler passiv), Vermögen Ø am Ende:

| | Eisenbart (Schmied) | Eichwald (Tischler) | Kieselbach (Steinmetz) | Bürgermeister | Stand der KI | Stadtkasse Jahr 5 / 10 / 30 | Steuer am Ende |
|---|---|---|---|---|---|---|---|
| ohne Karte, Messung 5 | 164.501 | 125.484 | 162.812 | 13/20 | Freiherr 59, Reichsgraf 1 | −6.136 / −5.455 / 2.064 | 3 % in 17 |
| ohne Karte, jetzt | 166.973 | 137.560 | 165.420 | 19/20 ab 17,3 | Freiherr 60; kein Bankrott | −3.424 / −255 / 7.360 | 3 % in 20 |
| Greybox, Messung 5 | 273.621 | 223.767 | 193.792 | 15/20 | Reichsgraf 54 | −5.233 / 69 / 9.847 | 3 % in 20 |
| Greybox, jetzt | 204.378 | 203.154 | 220.566 | 6/20 ab 15,7 | Reichsgraf 55, Freiherr 5; kein Bankrott | −3.204 / 1.730 / 12.735 | 3 % in 20 |

In der Greybox liegen die drei KI-Häuser jetzt dicht beieinander: Der Schmied verliert 25 %, weil die fernen Städte für seine Waren weniger zahlen, und der Steinmetz legt 14 % zu, weil sie seine Waren jetzt kaufen.
