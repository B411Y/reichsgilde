# Wirtschaftsmodell – Archiv der Messberichte (Balancing 7 bis Völkerboni ±3 %)

Hier stehen fünf abgeschlossene Messabschnitte **wörtlich**, aus [../WIRTSCHAFTSMODELL.md](../WIRTSCHAFTSMODELL.md) hierher verschoben (16.11.2026). Die älteren Messungen 1 bis 6 liegen in [WIRTSCHAFTSMODELL-bis-2026-10-26.md](WIRTSCHAFTSMODELL-bis-2026-10-26.md). Die Regeln und Werte stehen weiter in `game/daten/wirtschaft.json`.

## Balancing-Messung 7 (PR #219, Fehler und Kleinkram)

**Was sich geändert hat** (Umsetzung in `docs/bereiche/kern.md` und `docs/bereiche/spiel-ki.md`, „Fehler und Kleinkram (PR #219)“; **Datenversion 58**):
1. **Gaunerei-Erwartung über alle Versuche der Nacht** (Spiel-KI): Die KI und die Hinterzimmer-Tafel rechnen mit vier Versuchen zu je ⅓ statt mit einem Versuch und der vollen Chance. Beim Einbruch ins Wohnhaus sind das 4/3 so viele Erfolge, denn jeder holt wieder die Beute aus der Truhe. Beim Diebstahl ist es weniger, weil der zweite Griff ein geplündertes Lager findet.
2. **Sabotage höchstens einmal je Nacht** (Kern), wie der Brand am Wohnhaus. Ohne diese Regel lohnte die Sabotage mit der richtigen Erwartung: Jeder Erfolg brachte wieder 6 Stunden Stillstand. Im Probelauf (Teil 1) stiegen die Sabotage-Aufträge von 30 auf 235, das Mittel der Berufe fiel um 8 %, der Geldleiher auf −27 %.
3. **Reparatur nach einem Anschlag zählt nicht als Stillstand**, deshalb kauft die KI keine Karren mehr, die nie fahren.
4. **Taschendiebe 1,015 Gulden je Minute** (vorher 1,08): Der Dieb soll mit einem Betrieb nach 8–12 Jahren Bürger werden (Entscheidung des Nutzers). Probeläufe mit Teil 1:

| Taschendiebe | Dieb bis Bürger (im Ziel / vor 8) | Zuwachs gegen Mittel |
|---|---|---|
| 1,08 | 7,4 (11 / 18) | −1,7 % |
| 1,02 | 8,3 (15 / 13) | −8,3 % |
| 1,01 | 8,8 (17 / 9) | −12,9 % |
| 0,95 | 10,4 (16 / 6) | −34,9 % |

   Gewählt ist 1,015, der Wert zwischen Ziel und Band. Die übrigen Berufe ändern sich dadurch nicht, Wert für Wert gleich. Nur ihr Abstand zum Mittel verschiebt sich, weil der Dieb im Mittel mitzählt.

Gemessen auf main `2589c5f` + Branch `5213d1a`, Datenversion 58:
- `messen bericht 20 30`: 1.320 Partien in 21,4 Minuten → `docs/messungen/balancing-2026-10-27-kleinkram.json`
- `messen 30 20`: 39 Sekunden
- `messen 30 20 greybox`: 60 Sekunden

### Gegen die Ziele

| | Messung 6 | Messung 7 |
|---|---|---|
| Dieb bis Bürger, ein Betrieb (im Ziel / vor Jahr 8) | Ø 6,4 (7 / 23) | **Ø 8,6** (16 / 11) |
| Berufe im Band ±15 % | 10/10 (−14,1 … +8,0 %) | 9/10 (−20,5 … +12,6 %), außerhalb nur der Geldleiher |
| Mittel des Zuwachses | 30.542 | 29.354 |
| Schmied bis Bürger (Greybox, im Ziel von 120) | Ø 8,7 (108) | Ø 8,6 (104) |
| Wirt bis Bürger | Ø 10,1 | Ø 10,2 |
| Hinterzimmer der KI je Partie und Jahr | Diebstahl 0,63, Einbruch ins Wohnhaus 0,77, Sabotage 0,01 | 0,60, **1,06**, 0,02 |
| Raub und Schaden je KI-Haus ohne / mit Wachhaus | 513 / 425 | 499 / 440 |
| KI ÷ gleiche Strategie (Median); „Schwer“ | 1,24; 1,30 | 1,17; 1,44 |
| Karren verkauft (in Not / ein Jahr ohne Fahrt) | 10 / 45 | 13 / **1** |

### Berufe (simuliert, ein Betrieb, Greybox, 20 Jahre, 30 Startwerte)

| Beruf | Vermögen 20 J. | gegen Mittel | Messung 6 | Gewinn Ø | Gericht Ø | Bürger Ø |
|---|---|---|---|---|---|---|
| Schmied | 33.066 | +12,6 % | −5,1 % | 2.807 | 288 | 8,6 |
| Tischler | 29.763 | +1,4 % | +3,0 % | 2.696 | 303 | 9,4 |
| Steinmetz | 32.372 | +10,3 % | +8,0 % | 2.764 | 308 | 9,4 |
| Alchimist | 31.011 | +5,6 % | +5,1 % | 2.788 | 370 | 9,8 |
| Wirt | 29.653 | +1,0 % | −1,8 % | 2.670 | 319 | 10,2 |
| Prediger | 30.838 | +5,1 % | +3,5 % | 2.663 | 297 | 8,3 |
| Dieb | 25.785 | −12,2 % | +2,2 % | 2.705 | 602 | 8,6 |
| Parfumeur | 29.064 | −1,0 % | +5,3 % | 2.703 | 383 | 8,5 |
| Geldleiher | 23.339 | **−20,5 %** | −14,1 % | 2.285 | 404 | 8,0 |
| Gardist | 28.651 | −2,4 % | −6,1 % | 2.257 | 146 | 7,9 |
| Dieb wagemutig (nicht im Mittel) | 19.426 | −33,8 % | −20,6 % | 3.024 | 1.245 | 11,9 |

Der Geldleiher hat denselben Gewinn wie in Messung 6 (2.285 gegen 2.293). Ihm fehlen das Gericht (404 statt 287 im Jahr) und das Bargeld, das die häufigeren Einbrüche ins Wohnhaus holen. Er liegt schon in Messung 6 knapp am Rand, ein Wert ist dafür nicht geändert (Offen-Punkt in `kern.md`).

Statisch (Startpreise) liegt der Dieb jetzt in Stufe 1 im Band, in Stufe 2 und 3 bei +22 % (vorher +19 / +31 / +31 %).

**Völker** (Zuwachs gegen Menschen; Messung 6 in Klammern):
- Schmied: Zwerg +9,7 % (+31,0), Elb +4,9 % (+24,9), Halbling −20,8 % (−1,0)
- Parfumeur: Zwerg −6,8 % (−12,8), Elb +20,0 % (+14,3), Halbling −7,4 % (−10,1)

**`messen 30 20` und `messen 30 20 greybox`** (30 Jahre, 20 Startwerte, Spieler passiv):

| | Eisenbart | Eichwald | Kieselbach | Bürgermeister | Gaunerstücke der KI | Raub beim Spieler | Stadtkasse Jahr 30 |
|---|---|---|---|---|---|---|---|
| ohne Karte, Messung 6 | 166.973 | 137.560 | 165.420 | 19/20 | 686 | 2.890 | 7.360 |
| ohne Karte, jetzt | 171.737 | 135.897 | 168.888 | 15/20 ab 15,8 | 688 | 2.736 | 6.219 |
| Greybox, Messung 6 | 204.378 | 203.154 | 220.566 | 6/20 | 709 | 2.999 | 12.735 |
| Greybox, jetzt | 213.700 | 203.317 | 215.450 | 11/20 ab 15,0 | 707 | 3.082 | 13.347 |

Kein KI-Haus geht bankrott. Am Ende sind ohne Karte 57 Freiherren und 3 Reichsgrafen, in der Greybox 56 Reichsgrafen.

## Balancing 3 (PR #244)

Auftrag des Nutzers (29.10.2026): prüfen, wie fair das Spiel gegen die anderen Häuser ist – Chancengleichheit der Berufe, Völker, Startplätze und KI-Häuser – und ausgleichen. **Nur Zahlenwerte geändert, keine Regel**; Regelvorschläge stehen in `docs/bereiche/kern.md` unter „Offen“, die Werte unter „Balancing 3 (PR #244)“. **Datenversion 63** (main hat seit #231/#232 die 62; die Messung lief mit denselben Werten unter der Nummer 61 – die Nummer wirkt nicht auf die Simulation).

**Gemessener Stand:** main `9022b49` + #231 (`phase5/meinungen-kern` `b757446`) + #232 (`phase5/dolch-kern` `4c55d7d`, mit dem Zutateneinkauf nach Kasse) = „vorher“; dazu die Werte dieses Branches (`3ff0d04`) = „nachher“. Beide `messen bericht 20 30` (je 1.320 Partien, keine mit Fehler) → `docs/messungen/balancing-2026-10-29-balancing3-vorher.json` und `…-balancing3.json`. Die erste Analyse lief noch auf main `cc72d5c` + `e672515` + `2e8949d` (vor dem Zutateneinkauf); ihre Befunde zu Völkern und Gaunerei gelten weiter, die Berufe haben sich durch den Zutateneinkauf verschoben (Schmied +16 → +22 %, Gardist −13 → −21 %).

**Neu im Messbericht** (Testcode, kein Wert): `--fairness=greybox,probestadt,koeln` wählt die Karten der Startfairness (Köln mit 8 Häusern; eine Partie dort braucht etwa zehnmal so lange wie in der Greybox), `--voelkerberufe=alle` misst jeden Beruf mit jedem Volk (mit Mittel je Volk über alle Berufe), Abschnitt Einrichtung zeigt den Mehrgewinn jeder Vorarbeiter-Eigenschaft je Betrieb und Stufe (`eigenschaftenStatisch`), dazu Zerlegung je Jahr für jedes Haus und Volk, Stufe und Personal (Köpfe, Leistung je Minute) je Jahr.

**Messfehler:** Zwei Läufe mit fast gleichen Werten unterscheiden sich je Beruf um ±5 Punkte (eine Wertänderung irgendwo verschiebt die Würfe aller Häuser). Unterschiede unter etwa zwei Fehlern des Mittels (≈ ±1.300 … ±2.400 Gulden je Beruf, 4–7 %) sind Rauschen; nachgesteuert wurde nur, was in mehreren Läufen gleich lag.

### Befunde vorher

- **Berufe** (ein Betrieb, 20 Jahre): 8/10 im Band, Schmied +21,7 %, Gardist −21,3 %; am Rand Parfumeur +13,9 %, Dieb −13,8 %, Geldleiher −10,8 %.
- **Vorarbeiter-Eigenschaften sind eine Pflichtwahl:** Von drei gezogenen Eigenschaften (je eine aus Ware, Führung, Betrieb) bringt die aus der Gruppe Ware in 74 % aller Ziehungen am meisten, oft das Zwei- bis Dreifache (Schmied Stufe 2: Guter Ruf 793, Flinke Hand 646 Gulden im Jahr gegen Antreiber 220 und Frühaufsteher 305). In 24 von 30 Betrieben gewinnt immer die Ware.
- **Völker:** Im Mittel über alle zehn Berufe fair (15 Startwerte je Beruf und Volk, 600 Partien): Zwerg −0,9 %, Elb −4,6 %, Halbling −4,1 % gegen Menschen. Einzelne Paare weit draußen (40 Startwerte): **Dieb Halbling +58,7 %** (der beste Weg im ganzen Spiel), Gardist Elb −29,7 % und Halbling −28,1 %, Geldleiher Zwerg +27,7 %, Schmied Zwerg +18 … +24 %. Gegenproben ohne Völkerbonus bzw. zusätzlich ohne Talentunterschiede: Der Bonus von ±5 % wirkt bei Handwerken etwa ±10–15 % aufs Vermögen, bei den Betrieben ohne Ware (Dieb, Gardist, Geldleiher, schmale Marge) drei- bis zehnfach. Gegenproben mit „Nacht und Nebel“ der Halblinge 2 statt 3 bzw. halber Wirkung auf Zeugen (0,04 statt 0,08 je Stern) ändern den Halbling-Dieb kaum (+54 / +53 %). Die Elben verlieren rund 8 % durch Familie und Volk, die Talente geben ihnen etwa 4 % zurück.
- **Startplätze:** Der Wohnhausplatz wirkt im Kern nicht (alle Rotationen 0,0 %, Greybox, Probestadt und Köln). Zwischen den KI-Häusern entscheidet Beruf und Volk: Greybox Zwerg-Schmied 98.662, Halbling-Steinmetz 87.731, Elb-Tischler 67.159.
- **KI gegen Spieler:** KI ÷ Bezugsspieler (Median) 1,05, „Schwer“ 1,65 – fair.
- **1/3/5/8 Häuser:** 8 gegen 3 Häuser −10 % (Ziel höchstens −15 %). Auf dem Stand vor dem Zutateneinkauf waren es −16 %; mit 35 statt 25 Einwohnern je Haus −12 %. Nach dem Zutateneinkauf brachte die Erhöhung nichts mehr (−10 %) und senkte nur die Amtsgehälter kleiner Städte – darum zurückgenommen.
- **Gauner und Wache:** Ein Wachhaus in der Stadt senkt Raub und Schaden der KI-Häuser um 9 %, bremst aber den Dieb nicht (Gewinn mit Wachhaus +6 %). Die aktive Gaunerei lohnt nicht: Der wagemutig geführte Dieb (Einbruch, Schutzgeld) hat 23 % weniger als der Mittelwert, sein Gericht kostet 1.149 Gulden im Jahr (ausgewogen 431). Gegenprobe mit Buße 2× statt 3× des Schadens: wagemutig +14 %, ausgewogen +2 %.
- **Einrichtung:** Kein Stück ist eine Pflichtwahl gegen ein anderes (es gibt keine ausschließende Wahl), die Ertragsstücke der Werkstätten machen sich in 1–3 Jahren bezahlt. Über 5 Jahre: Löschfass (die KI gibt fast keine Sabotage in Auftrag, 0,06 im Jahr), Eisentür 5–6 Jahre, Lederhandschuhe von Wirt und Prediger 8–10 Jahre, Rechenbretter 8 Jahre.
- **Meinungen und Aktivitäten:** Die KI nutzt dieselben Aktivitäten (Bezugsspieler und KI gleich), Geschenke an Nicht-Ratsherren sind gedeckelt (+15). Eine Ausnutzung zeigt sich in den Zahlen nicht: Geschenke 12–62 Gulden im Jahr je Haus, Gericht und Spitzel wie vor #231.

### Änderungen

| Was | vorher | nachher | Grund |
|---|---|---|---|
| Vorarbeiter Werkstatt: Flinke Hand, Guter Ruf, Sparsam | Zeit ×0,85, Erlös ×1,1, Zutaten ×0,9 | ×0,9, ×1,06, ×0,92 | Ware nicht mehr zwei- bis dreimal so gut wie die anderen Gruppen |
| Gutes Gemüt, Umsichtig, Antreiber (alle Laufbahnen) | Laune +5, Unfälle ×0,5, Tempo ×1,1 | +15, ×0,25, ×1,15 | Führung als echte Wahl |
| Frühaufsteher (Werkstatt, Umland), Genügsam (alle) | +60 Minuten, Lohn ×0,95 | +90, ×0,85 | Betrieb als echte Wahl |
| Umland: Kräftig, Gutes Auge; Diebe: Langfinger | ×0,85, ×1,1; ×1,15 | ×0,9, ×1,06; ×1,1 | wie die Werkstatt |
| Schmied-Waren | Silberring 115, Werkzeug 140, Silberkette 218, Goldkette 360, Prunkschwert 381 | 112, 137, 213, 351, 372 (−2,5 %) | Schmied +21,7 % |
| Wirt Stufe 1 | Körnermahlzeit 54, Dünnbier 91 | 56, 94 | Wirt Bürger Ø 11,2 Jahre (19/30 im Ziel) |
| Wechselgebühr | 0,9 % | 0,95 % | Geldleiher −10,8 % |
| Sold | 0,875 je Minute | 0,9 | Gardist −21,3 % |

Ausprobiert und verworfen: 35 Einwohner je Haus (siehe oben); Lederhandschuhe 40 und Rechenbretter 150 (alle Berufe zahlen für dasselbe Stück dasselbe – so festgelegt, die Datentests prüfen es). Die Völkerboni bleiben (Entscheidung des Nutzers zu PR #177), der Vorschlag steht in kern.md.

### Ergebnis

Reinvermögen nach 20 Jahren (ein Betrieb, Greybox, Mensch, 30 Startwerte), in Klammern der Abstand zum Mittel der zehn Berufe; Jahre bis Bürger (Reinvermögen 10.000) und im Ziel 8–12:

| Beruf | vorher | nachher | Bürger vorher | Bürger nachher |
|---|---|---|---|---|
| Schmied | 42.182 (+21,7 %) | 35.564 ± 1.264 (+6,6 %) | 9,2 (30/30) | 9,9 (29/30) |
| Tischler | 36.915 (+4,9 %) | 33.850 ± 1.292 (+0,8 %) | 9,7 (29/30) | 9,8 (29/30) |
| Steinmetz | 36.120 (+2,4 %) | 32.788 ± 1.362 (−2,8 %) | 10,6 (27/30) | 11,6 (23/30) |
| Alchimist | 36.007 (+2,0 %) | 33.824 ± 1.274 (+0,7 %) | 10,2 (23/30) | 10,4 (24/30) |
| Wirt | 36.972 (+5,1 %) | 34.812 ± 2.383 (+4,0 %) | 11,2 (19/30) | 9,9 (27/30) |
| Prediger | 34.043 (−4,2 %) | 30.680 ± 1.628 (−9,9 %) | 9,2 (27/30) | 9,7 (26/30) |
| Dieb | 31.035 (−13,8 %) | 30.675 ± 1.485 (−9,9 %) | 8,1 (13/30) | 8,2 (13/30) |
| Parfumeur | 39.730 (+13,9 %) | 34.466 ± 1.797 (+2,9 %) | 9,7 (25/30) | 9,3 (25/30) |
| Geldleiher | 31.971 (−10,8 %) | 33.723 ± 884 (+0,4 %) | 8,2 (22/30) | 8,2 (22/30) |
| Gardist | 28.680 (−21,3 %) | 35.787 ± 1.158 (+7,3 %) | 9,6 (27/30) | 8,9 (30/30) |
| Dieb (wagemutig geführt, nicht im Mittel) | 28.132 (−23,1 %) | 28.783 ± 2.160 (−16,3 %) | 9,6 (13/30) | 11,2 (12/30) |

| | vorher | nachher |
|---|---|---|
| Berufe im Band ±15 % | 8/10 (−21,3 … +21,7 %) | **10/10 (−9,9 … +7,3 %)** |
| Mittel des Zuwachses, Gewinn im Jahr 20 | 31.365, 5.272 | 29.617 (−5,6 %), 4.633 – die Vorarbeiter bringen im Mittel weniger (siehe unten) |
| Vorarbeiter: beste Gruppe in allen Ziehungen (Ware / Führung / Betrieb) | 74 / 19 / 7 % | **57 / 24 / 19 %** |
| Erwarteter Mehrgewinn der gewählten Eigenschaft, Stufe 2 (Schmied; Gardist) | 622; 216 | 412; 349 |
| KI ÷ Bezugsspieler (Median); „Schwer“ | 1,05; 1,65 | 1,10; 1,82 |
| davongezogen (> 2×) / abgeschlagen (< ½) | 3 / 0 von 90 | 9 / 2 von 90 |
| 8 gegen 3 Häuser (Vermögen je KI-Haus) | −10 % | −11 % |
| Völker, Schmied / Parfumeur (gegen Menschen) | −16 … +8 % / −20 … +1 % | −13 … +22 % / −9 … +13 % |
| KI-Häuser Greybox: Zwerg-Schmied / Elb-Tischler / Halbling-Steinmetz | 98.662 / 67.159 / 87.731 | 90.193 / 69.702 / 85.127 |
| Stadtkasse, Mittel aller Jahre (Greybox / Probestadt) | −413 / −2.806 | −830 / −3.501 |
| Karren in Not verkauft | 5 | 5 |

**Köln** (`--teile=3 --fairness=koeln`, 10 Startwerte, je 80 Partien → `…-balancing3-koeln-vorher.json`, `…-balancing3-koeln.json`). Die acht Rotationen der Wohnhausplätze geben Wurf für Wurf dasselbe (der Platz wirkt im Kern nicht, jeder Platz 0,0 %) – unabhängig sind nur die 10 Startwerte, die Fehler in der Tabelle sind darum etwa √8-mal zu klein angegeben.

| Haus | Volk, Beruf | vorher | nachher |
|---|---|---|---|
| Spieler (ein Betrieb) | Mensch, Schmied | 36.358 | 32.290 |
| Overstolz | Zwerg, Schmied | 91.959 | 89.487 |
| Gryn | Mensch, Alchimist | 84.975 | 83.585 |
| Lyskirchen | Halbling, Steinmetz | 86.582 | 80.412 |
| Mommersloch | Halbling, Wirt | 77.726 | 80.754 |
| Scherfgin | Mensch, Schmied | 72.215 | 62.848 |
| Kleingedank | Mensch, Tischler | 60.348 | 62.485 |
| Hardevust | Elb, Tischler | 70.086 | 56.899 |
| Spanne der KI-Häuser um ihr Mittel | | −22 … +18 % (Mittel 77.699) | −23 … +21 % (Mittel 73.781) |
| Spieler: Bürger Ø Jahr (im Ziel 8–12 von 10 Startwerten) | | 9,8 (10/10) | 10,0 (7/10) |

In Köln gibt es drei Schmiede (Spieler, Overstolz, Scherfgin): Die billigeren Schmiedewaren treffen den Spieler dort stärker (−11 %) als in der Greybox. Zwischen den KI-Häusern entscheiden Volk und Beruf der Startbelegung – Zwerg-Schmied vorn, Tischler hinten (Elb-Tischler −23 %); mit den Völkerboni bleibt das so (Vorschlag in kern.md), die Belegung selbst ist Sache des Leveldesigns.

**Völker, alle Berufe** (`--teile=2 --voelkerberufe=alle`, 15 Startwerte, je 600 Partien → `…-balancing3-voelker-vorher.json`, `…-balancing3-voelker.json`; Fehler je Paar etwa ±5–8 %, ein Paar weicht erst ab etwa ±15 % sicher ab):

| Volk | Mittel über die zehn Berufe gegen Menschen, vorher | nachher | bester Beruf nachher |
|---|---|---|---|
| Zwerg | +3,3 % | +6,6 % | 42.016 |
| Elb | −1,6 % | −5,3 % | 38.531 |
| Halbling | +3,2 % | −0,1 % | 44.544 (Dieb) |
| Mensch | 0 | 0 | 36.809 |

Paare, die in beiden Läufen mehr als 12 % vom Menschen abweichen (vorher → nachher): Dieb Halbling +68 → +57 %, Steinmetz Zwerg +36 → +47 %, Geldleiher Zwerg +34 → +25 %, Prediger Zwerg (ohne Bonus) +30 → +24 %, Gardist Elb −29 → −28 %, Gardist Halbling −16 → −21 %, Gardist Zwerg −13 → −16 %, Tischler Elb −18 → −26 %, Tischler Halbling −21 → −14 %, Dieb Zwerg −18 → −15 %. Die Werte dieses Branches ändern daran nichts Wesentliches: Im Mittel sind die Völker fair, einzelne Paare nicht – das hängt an den Völkerboni und der Handwerkskunst der Zwerge (3 Sterne = +9 % Leistung in jedem Handwerk). Vorschlag in kern.md: Probe mit ±2 % statt ±5 % für Dieb, Gardist und Geldleiher (30 Startwerte, nicht übernommen, `…-balancing3-voelker-vorschlag.json` gegen `…-voelker-dgg.json`) – Dieb Zwerg −11,5 → +0,2 %, Halbling +41,6 → +32,8 %; Gardist Elb −28,0 → −16,3 %, Halbling −23,2 → −11,5 %; Geldleiher Zwerg +27,6 → +21,1 %, Halbling (ohne Bonus) +18,6 → +17,5 %.

**Greybox über 30 Jahre** (`messen 30 20 greybox`, Spieler passiv): Vermögen der KI-Häuser Eisenbart (Zwerg, Schmied) 224.429 → 215.331, Eichwald (Elb, Tischler) 204.350 → 190.747, Kieselbach (Halbling, Steinmetz) 230.616 → 208.569; Spanne um ihr Mittel −6 … +6 % → −6 … +5 %. Stand am Ende Reichsgraf 57 → 55 von 60, kein Haus bankrott, Bürger des Spielers weiter ab Jahr 7,5. Auf lange Sicht bleibt die KI unter sich ausgeglichen; die Wirtschaft ist etwa 7 % kleiner (Vorarbeiter).

**Befund nachher:** Alle zehn Berufe liegen im Band, die Spanne ist von 43 auf 17 Punkte geschrumpft, alle erreichen den Bürger im Mittel nach 8,2–11,6 Jahren (Z2). Die Vorarbeiter-Wahl ist keine Pflichtwahl mehr: Ware gewinnt noch in 57 % der Ziehungen, weil Ordnungsliebend und Wachsam keinen Gulden-Wert vor dem ersten Diebstahl haben; die besten drei liegen meist nahe beieinander (Schmied Stufe 2: Guter Ruf 476, Frühaufsteher 458, Antreiber 403). Der Preis dafür: Die gewählte Eigenschaft bringt einer Werkstatt etwa ein Drittel weniger, das Mittel aller Berufe sinkt um 5,6 %; der Gardist und der Geldleiher, deren beste Eigenschaft der Antreiber ist, gewinnen. Der Bezugsspieler (Mensch-Schmied) verliert mit dem Schmied mehr als die KI-Häuser (KI ÷ Bezug 1,05 → 1,10); der Abstand liegt im Fehler (± 0,05). Die Stadtkasse hat etwas weniger Steuer (Mittel −400 bzw. −700, etwa ein bis zwei Fehler) – sie lag schon vorher unter dem Ziel „0 bis leicht im Plus“ aus Balancing 2 (offen in kern.md).

## Endabnahme gegen main und Ausbau (PR #282, Prüfung 1.11.2026)

Eine unabhängige Prüfung (1.11.2026) fand: Die Abschnitte oben verglichen die KI-Häuser mit Zwischenständen des Branches statt mit main (Befund M1), die KI-Stärke war am Endstand nicht gemessen (M2), Lyskirchen verlor in Köln 31 % (M3), und gemessen war nur ein Spieler mit genau einem Betrieb – wer ausbaut, bekam in Köln fast nur Bauorte drei Stunden vom Markt (H2). Hier der Vergleich gegen **main 880e3387** (feste Fahrzeit 30 / 120 Minuten) für den Endstand des Branches. Die Simulation des Branches ist seit 1b598be0 unverändert (danach nur Messcode, Kartenprüfung, Darstellung, Doku; die Greybox gibt auf den Gulden dieselben Zahlen wie die Prüfung auf dd347586). Die Karrenwahl der KI (Befund H1) bessert spiel-ki gerade nach; diese Zahlen sind vor H1.

**Messung** (`messen bericht 20 n --teile=… --karte=<karte> --fairness=<karte> --rotationen=1 --betriebe=<datei.csv>`; main mit dem Messcode dieses Branches, sonst unverändert; keine Partie mit Fehler) → `docs/messungen/balancing-2026-11-01-endabnahme-{main,branch}-{greybox,koeln-t3,koeln-t5}.json`. Greybox 20 Startwerte (Teil 3 und 5), Köln Teil 3 mit 20 Startwerten, Teil 5 mit 10.

### Spieler und KI-Häuser (Teil 3: Spieler mit einem Betrieb, KI-Häuser bauen aus)

| Köln (20 Startwerte) | main 880e3387 | Branch | Unterschied |
|---|---|---|---|
| Spieler (Mensch Schmied, ein Betrieb) | 32.895 (±1.997) | 31.798 (±1.230) | −3,3 % (z −0,5) |
| Haus Overstolz (Zwerg Schmied) | 88.161 (±2.371) | 75.626 (±3.434) | −14,2 % (z −3,0) |
| Haus Hardevust (Elb Tischler) | 63.235 (±3.521) | 56.261 (±2.973) | −11,0 % (z −1,5) |
| Haus Lyskirchen (Halbling Steinmetz) | 73.460 (±4.886) | 55.712 (±3.918) | −24,2 % (z −2,8) |
| Haus Gryn (Mensch Alchimist) | 87.819 (±3.323) | 73.423 (±4.688) | −16,4 % (z −2,5) |
| Haus Scherfgin (Mensch Schmied) | 58.286 (±4.138) | 55.494 (±4.266) | −4,8 % (z −0,5) |
| Haus Mommersloch (Halbling Wirt) | 84.814 (±3.203) | 68.779 (±2.646) | −18,9 % (z −3,9) |
| Haus Kleingedank (Mensch Tischler) | 64.003 (±3.375) | 53.859 (±3.642) | −15,8 % (z −2,0) |
| **KI-Häuser Ø** (kleinstes) | **74.254** (58.286) | **62.736** (53.859) | **−15,5 %** |
| Bankrott / erloschen | 0 / 0 | 0 / 0 | |
| Spieler Bürger: Ø Jahr (im Ziel 8–12) | 10,1 (17 von 20) | 10,1 (19 von 20) | |

(Reinvermögen nach 20 Jahren, Mittel mit Fehler des Mittels; z = Unterschied ÷ Fehler des Unterschieds.) **Greybox** (20): Spieler 34.779 → 35.151 (+1 %), KI-Häuser 79.030 → 72.543 (−8 %; Eisenbart +2 %, Eichwald −17 %, Kieselbach −12 %), Bürger im Ziel 19 → 20 von 20, kein Bankrott.

**Befund:** Der Spieler mit einem Betrieb bleibt in beiden Städten im Ziel (Köln 31.798, Greybox 35.151; um 30.000, kein Bankrott, Bürger im Ziel). Die KI-Häuser verlieren gegen main 16 % in Köln und 8 % in der Greybox – nicht „im bisherigen Rahmen“, wie die Abschnitte oben gegen ihre Zwischenstände schrieben (M1). Der Verlust kommt ganz aus dem Ausbau: Startwerkstätten bringen auf beiden Ständen dasselbe, Neubauten und Umland weniger (unten). Mit 10 Startwerten (Prüfung) lag Köln bei 31.222 / 63.526 gegen 31.199 / 73.589; mit 20 ändern sich die Mittel um einige Prozent, die Aussage nicht.

### KI-Stärke (Teil 5: der Spieler mit der Strategie der KI, „Bezugsspieler“)

| Karte, Schwierigkeit | Bezugsspieler main → Branch | KI-Häuser main → Branch | Median KI ÷ Bezug | KI davongezogen (> 2×) |
|---|---|---|---|---|
| Greybox, Normal (20) | 68.066 → 56.004 (−18 %) | 78.461 → 69.017 (−12 %) | 1,08 → 1,14 | 3/60 → 9/60 |
| Greybox, Schwer (20) | 68.162 → 46.818 (−31 %) | 119.993 → 110.814 (−8 %) | 1,81 → 2,27 | 14/60 → 43/60 |
| Köln, Normal (10) | 58.520 → 52.465 (−10 %) | 75.482 → 61.594 (−18 %) | 1,45 → 1,15 | 9/70 → 14/70 |
| Köln, Schwer (10) | 85.074 → 57.257 (−33 %) | 109.190 → 85.067 (−22 %) | 1,32 → 1,64 | 0/70 → 30/70 |

Bei „Normal“ verlieren Bezugsspieler und KI-Häuser ähnlich viel, das Verhältnis bleibt nahe 1. Bei „Schwer“ (die KI-Häuser starten mit Werkstatt und Umlandbetrieb) zieht die KI davon: Der Bezugsspieler baut erst aus und trifft dann auf die weiten Bauorte und das weite Umland (unten), die KI hat ihr Umland vom Start an. Ob „Schwer“ so schwer sein soll, ist eine Frage an den Nutzer; die Karrenwahl der KI (H1) ändert die Zahlen noch.

### Ausbau (Befund H2)

**Bauorte** (`messen bauorte`, Fahrzeit des Handkarrens zum nächsten Platz an jedem Ort, an dem man jetzt eine Gießerei bauen dürfte, je Stelle die beste Drehung; `Stadtwirtschaft.FahrzeitAmBauort`, dieselbe Zahl wie nach dem Bau):

| Karte | Stellen | Median | bis 30 min | bis 60 min | bis 120 min | an der Höchstdauer | zehn Neubauten nacheinander, Reihenfolge der KI (Luftlinie) | … der schnellste freie Ort |
|---|---|---|---|---|---|---|---|---|
| Greybox | 54 | 123 min | 8 | 10 | 26 | 0 | 131, 113, 189, 108, 130, 154, 198, 118, 133 | 21, 33, 103, 108, 118, 130, 133, 186, 198 |
| Köln | 602 | 179 min | 1 | 1 | 87 | 54 | 10, 103, 145, 165, 71, 91, 135, 109, 89, 163 | 10, 71, 89, 91, 101, 107, 109, 117, 135, 137 |

In der Greybox passen nur neun Neubauten nebeneinander. Die Startwerkstätten fahren 25–30 Minuten (Köln) bzw. 15–38 (Greybox). In Köln liegt ein einziger Bauort unter einer Stunde (direkt am Platz); wer die zweite Werkstatt baut, fährt mindestens 71 Minuten, im Mittel der KI-Reihenfolge zwei Stunden. In der Greybox gibt es zwei gute Stellen (21 und 33 Minuten), die KI wählt nach Luftlinie aber zuerst 131 und 113.

**Was Neubauten bringen** (Abschnitt „ausbau“ der Messdateien; Neubau = Werkstatt, die ein Haus im Lauf der Partie baut; „danach“ = jedes volle Jahr nach dem Bau; Teil-5-Partien „Normal“, alle Häuser):

| | Neubauten je Haus | Baujahr (Median) | Fahrzeit hin (Median) | Gewinn je Jahr danach | Stillstand | Fahrten zum Markt je Jahr |
|---|---|---|---|---|---|---|
| Greybox main | 1,3 | 14 | 28 min | 2.292 | 6 % | 7,2 |
| Greybox Branch | 1,3 | 14 | 116 min | 1.116 (−51 %) | 27 % | 5,0 |
| Köln main | 1,4 | 14 | 26 min | 2.665 | 5 % | 5,0 |
| Köln Branch | 1,4 | 14 | 97 min | 1.985 (−26 %) | 15 % | 4,6 |
| Köln main, nur der ausbauende Spieler | 2,3 | 13 | 26 min | 2.665 | 9 % | 8,5 |
| Köln Branch, nur der ausbauende Spieler | 1,6 | 14 | 97 min | 1.810 (−32 %) | 20 % | 5,9 |

Köln Branch nach Fahrzeit des Neubaus: bis 45 Minuten (10 Neubauten, die eine Stelle am Platz) 3.714 Gulden im Jahr, 46–120 Minuten (51) 1.877, 121–240 Minuten (51) 1.415. Die Startwerkstätten bringen auf beiden Ständen dasselbe (Köln 3.265 / 3.229, Greybox 3.186 / 3.290). Das Umland verliert ebenfalls: Köln 116 → 263 Minuten, Stillstand 6 → 23 %, Gewinn −21 %.

**Woran es hängt:** nicht am Vorrat, sondern an den Fahrten. Ein Karren mit 2 × 2 Stunden Weg schafft vier bis fünf Fahrten im Jahr; jede Fahrt verkauft und kauft ein. Mit einem Vorrat für zwei Hin- und Rückfahrten (V1 unten) bleibt der Stillstand der Greybox-Neubauten bei 25 % (Branch 27 %).

**Varianten** (Versuche im Code, nicht gebaut; je eine Kopie des Branches, Teil 3 und Teil 5 „Normal“, Greybox 20 Startwerte, Köln 10; Reinvermögen nach 20 Jahren):

| Stand | Greybox: Spieler mit einem Betrieb / KI-Häuser | Greybox: ausbauender Spieler (±Fehler) / KI-Häuser, KI davongezogen | Köln: Spieler / KI-Häuser | Köln: ausbauender Spieler (±Fehler) / KI-Häuser, KI davongezogen | Neubauten Köln: Fahrzeit / Gewinn je Jahr (alle; nur der Spieler) |
|---|---|---|---|---|---|
| main (feste Fahrzeit) | 34.779 / 79.030 | 68.066 (±3.060) / 78.461, 3 von 60 | 31.199 / 73.589 | 58.520 (±5.749) / 75.482, 9 von 70 | 26 min / 2.665; 2.665 |
| Branch | 35.151 / 72.543 | 56.004 (±2.730) / 69.017, 9 von 60 | 31.222 / 63.526 | 52.465 (±5.953) / 61.594, 14 von 70 | 97 min / 1.985; 1.810 |
| V1 Vorrat für zwei Hin- und Rückfahrten (mindestens 4 Stunden) | 33.506 / 72.476 | 55.560 (±2.735) / 70.382, 8 von 60 | 31.053 / 64.086 | 56.459 (±5.564) / 62.151, 6 von 70 | 97 min / 1.939; 1.856 |
| V2 Höchstdauer 60 Minuten für Werkstätten in der Stadt (Umland 360) | 33.681 / 74.023 | 64.160 (±2.444) / 73.721, 3 von 60 | 31.205 / 64.101 | 58.997 (±4.427) / 64.161, 3 von 70 | 60 min / 2.277; 2.248 |
| V3 Bauort nach Fahrzeit (`FahrzeitAmBauort` statt Luftlinie, KI und Bezugsspieler) | 34.245 / 75.143 | 67.222 (±2.389) / 71.546, 1 von 60 | 32.145 / 63.433 | 55.631 (±4.944) / 62.779, 7 von 70 | 96 min / 2.170; 2.135 |

Köln mit 10 Startwerten (Teil 3 von main und Branch: dieselben Partien wie in der Prüfung, 31.199 / 73.589 bzw. 31.222 / 63.526); der Fehler des Mittels des Bezugsspielers liegt dort bei ±4.400–6.000 – Unterschiede unter etwa 8.000 sind Zufall. Belastbar sind in Köln die Gewinne der Neubauten (Fehler ±50–200) und die Greybox (20 Startwerte). Greybox-Neubauten der Häuser: Branch 116 Minuten / 1.116 Gulden im Jahr, Stillstand 27 %; V1 117 / 1.128, 25 %; V2 60 / 1.742, 13 %; V3 94 / 1.912, 13 % (main 28 / 2.292, 6 %).

**Befund und Empfehlung (Designfrage an den Nutzer, nichts davon ist gebaut):**

- **V1, Vorrat nach Fahrzeit, hilft nicht.** Der Stillstand der fernen Neubauten bleibt (Greybox 25 % statt 27 %), ihr Gewinn auch. Es fehlen Fahrten, nicht Zutaten im Lager: Jede Fahrt verkauft und kauft ein, und ein Karren mit 2 × 2 Stunden Weg kommt vier- bis fünfmal im Jahr zum Markt.
- **V2, eigene Höchstdauer für Werkstätten (60 Minuten), wirkt am stärksten.** Neubauten in Köln bringen 2.277 statt 1.985 Gulden im Jahr (main 2.665), der ausbauende Spieler ist in beiden Städten fast wieder auf main (Greybox 64.160 gegen 68.066), die KI zieht nicht öfter davon als auf main. Der Preis: Kern und Bild stimmen für ferne Werkstätten nicht mehr – der Karren käme im Bild später an, als die Ware verkauft ist, oder führe sichtbar schneller. Eine feste Deckelung hatte der Nutzer am 31.10.2026 nicht gewählt.
- **V3, Bauort nach Fahrzeit, hilft nur, wo es gute Bauorte gibt.** In der Greybox (zwei Stellen unter 35 Minuten) holt der ausbauende Spieler damit main fast ein (67.222), in Köln kaum (Neubauten 96 statt 97 Minuten): Dort liegt ein einziger Bauort unter einer Stunde.
- **Empfehlung des Kerns:** Kern und Bild gleich lassen (keine Deckelung) und den Ausbau über die Karte lösen – **in Köln Bauplätze nahe den drei Märkten** (Leveldesign: Bauzonen an den Straßen, die auf die Plätze führen; ein Haus baut in 20 Jahren im Mittel 1,4 Werkstätten, für acht Häuser genügen etwa 10 bis 15 Bauorte unter 45 Minuten, also bis etwa 35 m Karrenweg), dazu **die Fahrzeit im Bauvorschlag** (Oberfläche, mit `FahrzeitAmBauort`) und **die KI wählt nach Fahrzeit** (spiel-ki, V3). Wenn das in Köln nicht genug Platz findet, als Rückfall V2 (Höchstdauer nur für Werkstätten in der Stadt, das Bild streckt dann nur diese Fahrten).

### Lyskirchen (Befund M3)

Mit 20 Startwerten verliert Lyskirchen (Halbling Steinmetz) in Köln 24 % (z −2,8) – nicht mehr allein: Mommersloch −19 % (z −3,9), Overstolz −14 % (z −3,0). Die −31 % der Prüfung (10 Startwerte) waren zum Teil Streuung. Die Ursache ist belegt (Gewinn über 20 Jahre je Partie aus `--betriebe`, Teil 3):

| Lyskirchen, Gewinn in 20 Jahren | main | Branch | Unterschied |
|---|---|---|---|
| Startwerkstatt (Maurerei, 25–27 Minuten) | 67.436 | 68.027 | +591 |
| Neubauten | 17.088 | 4.470 | −12.618 |
| Umland (zwei Steinbrüche) | 37.754 | 33.246 | −4.507 |

- **Neubauten:** Lyskirchen erweitert vor allem um Steinmetzhütte und Steinschneiderei (je 0,75 je Partie, ab Jahr 14). Sie liegen im Median 133 Minuten vom Markt (main 26), stehen 30 bzw. 35 % still und bringen 410 bzw. 1.103 statt 3.880 bzw. 2.393 Gulden im Jahr. Der Steinmetz braucht für jedes Stück viel Stein; mit sechs bis acht Fahrten im Jahr kommt nicht genug nach, und fertige Ware kommt nicht schnell genug weg.
- **Umland:** Seine beiden Steinbrüche sind das fernste Umland der Kölner Häuser (Median 326 Minuten, einer an der Höchstdauer; die anderen Häuser 174–275), Stillstand 22 % statt 9 %.
- **Gegenprobe:** Die Neubauten der Tischler Hardevust und Kleingedank liegen im Median 10 Minuten vom Markt – sie bekommen meist den einen Bauort am Platz – und verdienen wie auf main (3.154 bzw. 3.171 Gulden im Jahr). Darum fielen die Tischler im Zwischenstand gegen f7d0b501 „+30 %“ auf. Gryn, Scherfgin und Overstolz bauen bei 79–81 Minuten und verdienen 344–1.587.

Der Beruf ist also nicht benachteiligt, sondern wer weit draußen ausbaut, und der Steinmetz am stärksten, weil er die meisten Zutaten je Stück braucht. Der Weg dorthin ist H2 (Ausbau).

### Weitere Befunde der Prüfung

- **Alte Spielstände** rechnen weiter wie auf main: Der Greybox-Stand von Herbst 1406 (Datenversion 55) läuft drei Jahre bitgleich mit main 880e3387 (SHA-256 des Spielstands ohne Daten und Karte, Test `AlterSpielstandSollwertTests`). Die Daten- und Kartenabschnitte des Spielstands unterscheiden sich nur um neue, leere Felder.
- **Gleichheitstest** (Linux/Windows, Speichern und Laden, deutsche Ländereinstellung) jetzt auch in Köln (vier Jahre, „Schwer“, mit drei Marktplätzen und den Wegen über das Pflaster).
- **Fairness der Startgrundstücke** misst die Kartenprüfung nach dem Karrenweg statt der Luftlinie (`Startwegpruefung`, Hinweis „startweg“); Köln: der Spieler 23,6 m gegen 21,1 m der KI-Werkstätten (+10 %), Grube Scherfgin und Bauernhof Mommersloch −13 % bzw. +12 % gegen das Mittel des Start-Umlands.

## Balancing nach dem Handgeld (Datenversion 75, 9.11.2026)

**Auftrag** (Teamleiter 8.11.2026): Dieb und Parfumeur über ihre Grundwerte zurück ins Band ±15 %, nicht über Knoten der Lebensweise; dazu die Völker nach 40 Jahren beurteilen. Entscheidungen in `docs/bereiche/kern.md`, „Balancing nach dem Handgeld“.

**Ursache Dieb:** das Handgeld (#371). Jeder gefasste Taschendieb fällt weg, die KI stellt einen neuen ein und zahlt 100 Gulden; dazu kommen fünf statt zwei bezahlte Plätze beim Einstellen (Schmugglerloch 4, Unterschlupf 6, einer frei zum Start). Von main `18c40e7a` zu `b5444c09` sank sein Gewinn um 59 Gulden im Jahr (0,6 Fänge × 100), die Fänge blieben gleich. Eine Probe ohne Handgeld für Taschendiebe (nur Diagnose, nicht übernommen) brachte ihn von −21,1 auf −11,0 %. Jeder Fang bringt außerdem eine Spur vor Gericht: Der Dieb zahlte 460 Gulden Gericht im Jahr, die anderen Berufe 150–200.

**Ursache Parfumeur:** nicht das Handgeld. Er lag schon lange am oberen Rand (+13,9 % in Balancing 3, +15,7 % nach den Charakterzügen, +14,2 % auf main `18c40e7a`). Ab der Duftwerkstatt verdient er zu viel: statisch +21 % (Fechterduft 2,28, Moschusduft 2,22 Gulden je Minute, fast gleich gut), simuliert nach 20 Jahren 5.771 gegen 4.859 Gulden Gewinn im Mittel (+19 %). In den ersten Jahren liegt er dagegen hinten (Jahr 5: −29 %, Diebe und Blume der Zwietracht), deshalb bleibt die Siederei unverändert.

**Proben** (`messen bericht 20 30 --teile=1`, nur Berufe, 330 Partien je Probe; Abstand des Vermögenszuwachses zum Mittel):

| Probe | Dieb | Parfumeur | Steinmetz | Mittel |
|---|---|---|---|---|
| main `efbd166d` | −21,1 % | +16,7 % | +14,3 % | 33.081 |
| ohne Handgeld für Taschendiebe (Diagnose) | −11,0 % | +15,4 % | +13,1 % | 33.445 |
| Fang 0,010; Moschus 218, Fechter 207 (−3,5 %) | −13,0 % | +5,4 % | +14,8 % | 32.986 |
| Fang 0,009; Moschus 214, Fechter 204 (−5 %) | −9,5 % | +1,5 % | +15,7 % | 32.900 |
| **Fang 0,009; Moschus 220, Fechter 210 (−2,5 %) – übernommen** | **−10,6 %** | **+9,0 %** | **+14,0 %** | 33.233 |
| Fang 0,009; Moschus 221, Fechter 211 (−2 %) | −10,9 % | +10,2 % | +13,8 % | 33.374 |

**Geändert:**

| Was | vorher | nachher | Grund |
|---|---|---|---|
| `dieb.taschendiebstahl.fangJeStunde` | 0,012 | 0,009 | Weniger Fänge heißt weniger Ersatz mit Handgeld und weniger Spuren vor Gericht. Mit 0,009 steht der Dieb wie ohne Handgeld für Taschendiebe (−10,6 gegen −11,0 %). 0,010 gliche nur das Handgeld je Fang aus, nicht das für die Plätze beim Einstellen (−13,0 %, knapp im Band). Beute und Handgeld-Regel bleiben: Mehr Beute höbe auch seinen statischen Wert (Stufe 1 schon +22 %), und das Handgeld gilt nach Entscheidung des Nutzers für alle gleich. |
| Grundpreis Moschusduft | 226 | 220 | Beide Waren der Duftwerkstatt gleich stark senken, sonst wiche die KI auf die andere aus. Mit −2,5 % sinkt der Parfumeur auf +9,0 % (statisch Stufe 2 von +21 auf +16 %). −3,5 % und −5 % ziehen den Mittelwert und damit den Steinmetz über +15 %. |
| Grundpreis Fechterduft | 215 | 210 | wie Moschusduft |

**Messung** (gemessen auf main `efbd166d` + diesem PR, `b52370ca`): `messen bericht 20 30` (1.320 Partien, keine mit Fehler, 22,6 Min.) → `docs/messungen/balancing-2026-11-09-handgeld.json`, und `messen bericht 40 30 --teile=2` (240 Partien, 11,6 Min.) → `…-voelker40-handgeld.json`. Zum Vergleich dienen die Messungen „Lebensweise: Nachsteuerung“ (`…-lebensweise-nachgesteuert.json`, `…-voelker40-lebensweise-nachgesteuert.json`). Sie rechnen genau wie main `efbd166d`: Seitdem kamen nur Rasen und Bodenstempel der Darstellung dazu, und die Teilmessung Berufe auf `efbd166d` ergab Zahl für Zahl dasselbe. Die NAS war geteilt (8 Kerne, Last 4 bis 8, `--parallel=5` und `3` nebeneinander).

| | main `efbd166d` | Balancing nach dem Handgeld |
|---|---|---|
| Berufe im Band ±15 % (simuliert, ein Betrieb, 20 Jahre) | 8/10 (Dieb −21,1 %, Parfumeur +16,7 %) | **10/10** (−11,4 … +14,0 %) |
| Mittel des Zuwachses | 33.081 | 33.233 |
| Statisch außerhalb ±15 % | Stufe 2: Parfumeur +21 % (dazu wie bisher Wirt, Dieb, Geldleiher, Gardist) | Stufe 2: Parfumeur +16 % |
| KI ÷ Bezugsspieler (Median); „Schwer“ | 1,20; 1,79 | 1,25; 1,78 |
| Messstadt: Vermögen je KI-Haus allein / 3 / 8 Häuser | 99.410 / 60.571 / 51.192 | 99.449 / 61.164 / 50.319 |
| Dieb: Fänge je Jahr (ohne / mit Wachhaus) | 0,58 / 0,73 | 0,42 / 0,55 |
| Dieb: Gewinn ohne / mit Wachhaus | 2.518 / 2.537 (+1 %) | 2.551 / 2.556 (0 %) |
| Gaunerei der KI: Diebstahl / Einbruch / Sabotage je Jahr | 0,43 / 0,81 / 0,17 | 0,44 / 0,80 / 0,16 |
| Spitzel / Gericht gesamt je Jahr (ein Betrieb, KI geführt) | 95 / 202 (Dieb 460) | 96 / 186 (Dieb 341) |
| Stadtkasse, Mittel aller Jahre (Greybox / Probestadt) | −3.526 / −6.690 | −3.612 / −6.719 |
| Karren in Not / nach einem Jahr ohne Fahrt verkauft | 0 / 74 | 0 / 69 |

Reinvermögen nach 20 Jahren je Beruf (Mensch, ein Betrieb, Greybox, 30 Startwerte; ± Fehler des Mittels; in Klammern der Abstand des Zuwachses zum Mittel):

| Beruf | main `efbd166d` | nachher | Änderung | Bürger Ø Jahr (im Ziel 8–12) |
|---|---|---|---|---|
| Schmied | 40.128 ± 1.127 (+9,2 %) | 39.931 ± 1.199 (+8,1 %) | −0,5 % | 9,2 (30/30) |
| Tischler | 33.407 ± 1.239 (−11,1 %) | 33.433 ± 1.191 (−11,4 %) | +0,1 % | 9,9 (28/30) |
| Steinmetz | 41.812 ± 1.373 (+14,3 %) | 41.895 ± 1.248 (+14,0 %) | +0,2 % | 10,0 (30/30) |
| Alchimist | 37.635 ± 1.261 (+1,7 %) | 37.626 ± 1.097 (+1,2 %) | 0,0 % | 9,3 (29/30) |
| Wirt | 35.614 ± 1.199 (−4,4 %) | 35.787 ± 1.169 (−4,4 %) | +0,5 % | 9,5 (26/30) |
| Prediger | 37.587 ± 1.449 (+1,5 %) | 38.017 ± 1.306 (+2,4 %) | +1,1 % | 8,0 (22/30) |
| **Dieb** | 30.107 ± 1.435 (−21,1 %) | 33.719 ± 1.152 (−10,6 %) | **+12,0 %** | 7,6 (10/30) |
| **Parfumeur** | 42.607 ± 1.295 (+16,7 %) | 40.224 ± 1.282 (+9,0 %) | **−5,6 %** | 8,9 (28/30) |
| Geldleiher | 37.735 ± 1.239 (+2,0 %) | 37.556 ± 1.266 (+1,0 %) | −0,5 % | 8,5 (25/30) |
| Gardist | 34.175 ± 882 (−8,8 %) | 34.145 ± 824 (−9,3 %) | −0,1 % | 9,1 (28/30) |
| Dieb (wagemutig, nicht im Mittel) | 25.108 ± 1.195 | 27.838 ± 1.110 | +10,9 % | 8,4 (10/30) |

**Völker** (Reinvermögen nach 40 Jahren, Abstand zum Menschen desselben Berufs; Fehler eines Mittels 2.300–4.600, eines Unterschieds etwa 5 Punkte):

| Beruf, Volk | main `efbd166d` | nachher | gegen Mensch (main → nachher) |
|---|---|---|---|
| Schmied, Mensch | 111.160 | 109.261 | 0 → 0 |
| Schmied, Zwerg | 124.702 | 123.036 | +12,2 → +12,6 % |
| Schmied, Elb | 113.334 | 112.870 | +2,0 → +3,3 % |
| Schmied, Halbling | 98.319 | 99.919 | −11,6 → −8,6 % |
| Parfumeur, Mensch | 130.643 | 121.055 | 0 → 0 |
| Parfumeur, Zwerg | 125.559 | 123.992 | −3,9 → +2,4 % |
| Parfumeur, Elb | 145.656 | 137.309 | +11,5 → +13,4 % |
| Parfumeur, Halbling | 124.921 | 120.777 | −4,4 → −0,2 % |

**Befund Völker** (Empfehlung als offener Punkt in `docs/bereiche/kern.md`): Die Knoten der Lebensweise folgen der Lebenszeit. Nach 40 Jahren hat ein Haus je Partie (`messen lebensweg 40 30 --berufe=Schmied --voelker=Mensch,Zwerg,Elb,Halbling` auf main `efbd166d`) als Elb 19,9 Knoten, als Zwerg 16,9, als Mensch 12,6 und als Halbling 9,0. Das Vermögen folgt den Knoten aber kaum: Der Elb-Schmied hat die meisten Knoten und liegt trotzdem nur +2 bis +3 % über dem Menschen. Den Abstand von Zwerg und Halbling als Schmied macht vor allem der Völkerbonus (±5 % Leistung, etwa ±10–15 % aufs Vermögen, siehe Balancing 3). Ohne Lebensweise lag er auf main schon bei +4,6 … +15,1 bzw. −5,5 … −7,9 %. Empfehlung: Das ist eine gewollte Volksfolge, nicht glätten. Würde die Erfahrung je Jahr mit der Lebenszeit des Volkes skaliert, fielen Zwerge und Elben in Partien von 20–30 Jahren zurück, in denen ihr Oberhaupt nie stirbt.

**Vermächtnis 35 %** (Entscheidung des Nutzers 09.11.2026: „ausprobieren und schauen, was die Zahlen sagen“): `lebensweise.vermaechtnis` 0,2 → 0,35. Der Erbe bekommt also 35 statt 20 % der Erfahrung des Toten in dessen Pfad, das soll den kurzlebigen Völkern helfen. Gemessen auf main `efbd166d` + 67dcd65d + diesem Wert, gleiche Startwerte (`messen bericht 40 30 --teile=2`, 20,2 Min. → `…-voelker40-vermaechtnis35.json`; `messen bericht 20 30 --teile=1` → `…-berufe-vermaechtnis35.json`; NAS mit Last 2 bis 6):

| Schmied, Volk | 40 Jahre, Vermächtnis 0,2 | 0,35 | Änderung | gegen Mensch (0,2 → 0,35) |
|---|---|---|---|---|
| Mensch | 109.261 | 110.475 ± 2.661 | +1,1 % | 0 → 0 |
| Zwerg | 123.036 | 123.815 ± 3.436 | +0,6 % | +12,6 → +12,1 % |
| Elb | 112.870 | 113.543 ± 3.551 | +0,6 % | +3,3 → +2,8 % |
| Halbling | 99.919 | 99.883 ± 2.679 | 0,0 % | −8,6 → −9,6 % |

Beim Parfumeur ändert sich jedes Volk um höchstens 1,2 % (Halbling gegen Mensch −0,2 → +1,1 %). Nach 20 Jahren liegen weiter alle zehn Berufe im Band (Mittel 33.233 → 33.133, jeder Beruf höchstens ±1,5 %). Auch die Knoten nehmen nicht sichtbar zu. Nach 40 Jahren hat ein Haus je Partie als Mensch 10,8 und als Halbling 9,0 Knoten (`messen lebensweg 40 30 --berufe=Schmied --voelker=Mensch,Halbling`). Die Messung auf main mit 0,2 ergab 12,6 / 9,0; sie lief auf einem anderen Stand, die Zahlen schwanken.
**Befund:** Die Wirkung liegt im Rauschen. Ein größerer Wert würde genauso wenig helfen, denn die Knoten bewegen das Vermögen kaum (siehe den Elb oben). 0,35 kann bleiben, weil es nicht schadet, oder auf 0,2 zurück. Wer den Abstand des Schmieds verkleinern will, muss beim Völkerbonus ansetzen (offener Punkt in `docs/bereiche/kern.md`).

**Nebenbefund Spielstand:** Mit den neuen Preisen lief der Gleichheitstest bei Startwert 4711 nach dem Laden auseinander. Ursache: Das Jahresende lernt das Übliche der Einkäufe neu, den Faktor der Bürger am Markt setzt die Bürger-KI aber erst zur nächsten vollen Stunde. Beim Laden in dieser Stunde wurde der Faktor schon mit dem neuen Üblichen gerechnet, und ein Karren, der um 4 Uhr verkaufte, bekam 0,08 % weniger. Der Fehler ist behoben (`EinkaufErwartetImMarkt` im Spielstand der Einwohnerschaft). Durchgehende Partien rechnen bitgleich weiter: Die Berufe der Endmessung gleichen der Probe ohne die Behebung Zahl für Zahl.

## Völkerboni ±3 % (Datenversion 76, 9.11.2026)

**Auftrag** (Entscheidung des Nutzers 9.11.2026): Vermächtnis zurück auf 20 %, Völkerboni der Berufe ±3 % statt ±5 %. Entscheidungen in `docs/bereiche/kern.md`, „Völkerboni ±3 %“.

**Geändert:**

| Was | vorher | nachher |
|---|---|---|
| `berufe.Schmied.voelker` | Zwerg +0,05, Halbling −0,05 | Zwerg +0,03, Halbling −0,03 |
| `berufe.Tischler.voelker` | Mensch +0,05 | Mensch +0,03 |
| `berufe.Steinmetz.voelker` | Zwerg +0,05, Elb −0,05 | Zwerg +0,03, Elb −0,03 |
| `berufe.Alchimist.voelker` | Elb +0,05 | Elb +0,03 |
| `berufe.Wirt.voelker` | Halbling +0,05 | Halbling +0,03 |
| `berufe.Prediger.voelker` | Elb +0,05 | Elb +0,03 |
| `berufe.Parfumeur.voelker` | Elb +0,05, Zwerg −0,05 | Elb +0,03, Zwerg −0,03 |
| `lebensweise.vermaechtnis` | 0,35 | 0,2 |

Unverändert: Dieb, Gardist und Geldleiher (±2 % seit den Gauner-Regeln), Lebenszeit, Haushalt, Talente und alles andere je Volk. 3 % entsprechen etwa einem Stern Handwerkskunst.

**Messung** (gemessen auf main `7143a4f6` + diesem PR, `f33947e4`; main stand bei der Messung auf `57964daf`, dazwischen nur Darstellung). Vorher ist main `fcbc7db4` (#380: ±5 %, Vermächtnis 0,35) mit denselben Startwerten; die beiden Vorher-Dateien `…-vermaechtnis35.json` tragen `commit 67dcd65d` (main `fb089712`) mit Vermächtnis 0,35 – Kern und Daten rechnen dort wie `fcbc7db4`:

- `messen bericht 40 30 --teile=2` (240 Partien, 13,3 Min.) → `docs/messungen/balancing-2026-11-09-voelker40-voelkerbonus3.json`, gegen `…-voelker40-vermaechtnis35.json`;
- `messen bericht 20 30` (1.320 Partien, keine mit Fehler, 39 Min.; NAS mit anderen Läufen geteilt) → `…-voelkerbonus3.json`; die Berufe gegen `…-berufe-vermaechtnis35.json` (main), die übrigen Teile gegen `…-handgeld.json` (±5 %, Vermächtnis 0,2 – main hat nur Berufe und Völker mit 0,35 gemessen);
- Dieb und Geldleiher je Volk (`messen bericht 20 30 --teile=2 --voelkerberufe=Dieb,Geldleiher`, 240 Partien) auf main `fcbc7db4` und auf diesem PR → `…-voelker-gauner-main-fcbc7db4.json`, `…-voelker-gauner-voelkerbonus3.json`.

**Völker** (Reinvermögen nach 40 Jahren, Abstand zum Menschen desselben Berufs; Fehler eines Mittels 2.800–5.200, eines Unterschieds etwa 4 Punkte):

| Beruf, Volk | Bonus | main `fcbc7db4` | nachher | gegen Mensch (main → nachher) |
|---|---|---|---|---|
| Schmied, Mensch | 0 | 110.475 | 106.368 ± 3.152 | 0 → 0 |
| Schmied, Zwerg | +5 → +3 | 123.815 | 117.009 ± 5.174 | +12,1 → +10,0 % |
| Schmied, Elb | 0 | 113.543 | 111.246 ± 2.809 | +2,8 → +4,6 % |
| Schmied, Halbling | −5 → −3 | 99.883 | 101.151 ± 2.777 | −9,6 → −4,9 % |
| Parfumeur, Mensch | 0 | 120.902 | 124.139 ± 3.591 | 0 → 0 |
| Parfumeur, Zwerg | −5 → −3 | 123.473 | 120.754 ± 3.185 | +2,1 → −2,7 % |
| Parfumeur, Elb | +5 → +3 | 137.421 | 135.444 ± 4.074 | +13,7 → +9,1 % |
| Parfumeur, Halbling | 0 | 122.205 | 124.060 ± 4.296 | +1,1 → −0,1 % |

Der Abstand zwischen Zwerg- und Halbling-Schmied schrumpft von 21,7 auf 14,9 Punkte, der zwischen Elb- und Zwerg-Parfumeur von 11,6 auf 11,8 Punkte nicht (der Mensch-Parfumeur liegt diesmal 2,7 % höher, der Mensch-Schmied 3,7 % tiefer – beides in der Streuung). Nach 20 Jahren (Teil 2 im vollen Bericht, Abstand des Zuwachses): Schmied −14 … +5 %, Parfumeur −9 … +6 % (vorher aus `…-handgeld.json` mit Vermächtnis 0,2: −14 … +10 % und −8 … +12 %).

**Berufe** (Mensch, ein Betrieb, Greybox, 20 Jahre; ± Fehler des Mittels; in Klammern der Abstand des Zuwachses zum Mittel):

| Beruf | main `fcbc7db4` | nachher | Änderung |
|---|---|---|---|
| Schmied | 39.348 (+6,7 %) | 40.001 ± 1.218 (+9,3 %) | +1,7 % |
| **Tischler** (Mensch +5 → +3 %) | 33.351 (−11,4 %) | 31.756 ± 1.523 (**−15,7 %**) | −4,8 % |
| Steinmetz | 41.545 (+13,3 %) | 40.400 ± 1.267 (+10,5 %) | −2,8 % |
| Alchimist | 37.585 (+1,4 %) | 36.820 ± 1.229 (−0,4 %) | −2,0 % |
| Wirt | 35.965 (−3,5 %) | 36.767 ± 1.081 (−0,5 %) | +2,2 % |
| Prediger | 37.928 (+2,4 %) | 37.548 ± 1.287 (+1,8 %) | −1,0 % |
| Dieb | 33.673 (−10,4 %) | 34.346 ± 1.138 (−7,9 %) | +2,0 % |
| Parfumeur | 40.312 (+9,6 %) | 41.776 ± 1.303 (+14,7 %) | +3,6 % |
| Geldleiher | 37.500 (+1,1 %) | 36.242 ± 1.121 (−2,1 %) | −3,4 % |
| Gardist | 34.123 (−9,1 %) | 33.782 ± 823 (−9,6 %) | −1,0 % |
| Dieb (wagemutig, nicht im Mittel) | 27.621 (−28,7 %) | 23.577 ± 1.196 (−40,6 %) | −14,6 % |

Mittel des Zuwachses 33.133 → 32.944. **9 von 10 im Band ±15 %**: Der Tischler fällt knapp heraus – er ist der einzige Startberuf, den der Mensch im Bericht mit Bonus spielt, und verliert zwei Punkte Leistung. Die übrigen bewegen sich über die KI-Häuser (Eisenbart als Zwergen-Schmied, Eichwald als Elb, Kieselbach als Halbling), die selbst ihren Bonus verlieren. Der wagemutige Dieb (nicht im Mittel, schon vorher außerhalb) verliert 4.000 Gulden; seine Opfer sind die KI-Häuser, er streut stark (Gericht).

**Übrige Teile** (gegen `…-handgeld.json`, ±5 %, Vermächtnis 0,2): KI ÷ Bezugsspieler (Median) 1,25 → 1,15 (KI-Häuser im Mittel 80.309 → 79.195, Bezug 66.447 → 66.403), „Schwer“ 1,78 → 1,83; Messstadt je KI-Haus allein / 3 / 8 Häuser 99.449 / 61.164 / 50.319 → 96.119 / 59.059 / 49.834; Gaunerei der KI 0,44 / 0,80 / 0,16 → 0,45 / 0,80 / 0,17; Spitzel / Gericht 96 / 186 → 94 / 190; Karren in Not 0 → 0. Statische Berufe unverändert (sie rechnen ohne Volk).

**Dieb und Geldleiher je Volk** (20 Jahre, ihr Bonus bleibt ±2 %; Abstand des Zuwachses zum Menschen; Fehler eines Mittels 800–1.300, eines Unterschieds etwa 5 Punkte):

| Volk | Dieb main → nachher | Geldleiher main → nachher |
|---|---|---|
| Zwerg | −14,9 → −17,6 % (Bonus −2) | +20,1 → +25,7 % (Bonus +2) |
| Elb | −11,0 → −15,6 % | −7,9 → −2,4 % |
| Halbling | +23,4 → +21,8 % (Bonus +2) | +15,9 → +21,7 % |

Die Änderung trifft beide nur über das Vermächtnis und die KI-Häuser; die Unterschiede liegen im Rauschen. Der Abstand des Halbling-Diebs und des Zwerg-Geldleihers bleibt bei +20 bis +25 %; der Halbling-Geldleiher ohne Bonus liegt ebenso hoch – der Bonus erklärt ihn nicht (offener Punkt in `kern.md`).
