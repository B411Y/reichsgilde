# Reichsgilde – Wirtschaftsmodell

Begonnen am 13.10.2026 (Phase 1, Entwurf 1, **freigegeben**) · Seit 15.10.2026 im Repository (vorher im Claude-Projekt). Jeder neue Beruf und jede Balance-Änderung bekommt hier einen Abschnitt mit Werten und Messung.
Die Werte selbst stehen in `game/daten/wirtschaft.json`. Grundsatzentscheidungen: `docs/ENTSCHEIDUNGEN.md`.
**Nur der Kern-Chat schreibt hier** (seit 16.10.2026; Messungen der Spiel-KI stehen in `docs/bereiche/spiel-ki.md`). Neue Abschnitte bekommen keine Nummer mehr, sondern eine Überschrift `## Thema (PR #…)`. Jeder neue Messabschnitt nennt den `main`-Commit, auf dem er beruht („gemessen auf main `abc1234` + PR #…“). Ältere Abschnitte ohne Commit beziehen sich auf den Stand ihres PRs.
Am Anfang stand ein interaktiver Balancing-Rechner (ein Claude-Artefakt, nicht öffentlich); sein JSON-Export wurde zu `game/data/wirtschaft.json`.

---

## 1. Ziele des Balancings

An diesen Zielen werden alle Zahlen gemessen. Ändert sich eine Zahl, muss das Ziel weiter erfüllt sein, oder wir passen das Ziel bewusst an.

| # | Ziel | Entwurf 1 erfüllt? |
|---|---|---|
| Z1 | Eine Gießerei mit 2 Gesellen ist bei ausgeglichenem Markt profitabel, mit etwa 600–900 Gulden pro Jahr | ✔ 627 (1 Rivale) bis 878 (kein Rivale) |
| Z2 | Den Titel **Bürger** (10.000 Gulden Vermögen) erreicht man mit einem einzigen Betrieb nach **8–12 Jahren** (bei 15 Min./Jahr also 2–3 Stunden Spielzeit). Wer expandiert, schafft es schneller. | ✔ 10 Jahre |
| Z3 | Konkurrenz ist spürbar: Mehr Anbieter drücken den Preis allmählich, ohne dass er sofort einbricht. Bei Überangebot lohnt es sich, auf ein anderes Produkt auszuweichen. | ✔ Silberring 878 → 627 → 437 → 163 bei 0/30/60/120 Konkurrenzstück |
| Z4 | Ein einzelner Spieler kann den Markt allein nicht beherrschen, weil die Stadt viele andere Anbieter hat | ✔ eine Werkstatt deckt ca. 20 % der Nachfrage |
| Z5 | **Statut B** (Ork-Fron im Umland) ist für Minenbesitzer deutlich lukrativer (+40–50 %). Das gibt ihnen ein starkes Motiv, im Rat dafür zu werben. | ✔ Mine 1.139 → 1.613 (+42 %) |
| Z6 | **Statut C** lohnt sich in Gulden mäßig (+15–25 %), kostet aber Ansehen und birgt Aufstandsgefahr | ✔ Gießerei 627 → 750 (+20 %), plus Risiken außerhalb der Gulden-Rechnung |
| Z7 | Zwischenprodukte (Beschlag) lohnen sich weniger als Fertigwaren, sind aber nicht defizitär. So entsteht ein Zulieferer-Markt zwischen den Berufen. | ✔ 393 statt 878 |

## 2. Zeitbasis

- **1 Spieljahr = 1 Spieltag = 15 Minuten Echtzeit** (siehe Entscheidungsprotokoll).
- **Arbeitszeit: 08:00–20:00 = 720 Arbeitsminuten pro Jahr.** Das deckt Zunftglocke, Hoher Mittag und einen Teil der Vesper ab. Die Nacht gehört den Gaunern.
- Produktionszeiten werden in **Spielminuten für einen Gesellen** angegeben (Tempo 1,0).

## 3. Marktmodell

### Preisformel

```
Angebot   = Nachfrage × NPC-Anteil  +  Angebot der Spieler (Stück/Jahr)
Faktor    = (Nachfrage / Angebot) ^ Elastizität        begrenzt auf [0,5 … 2,0]
Marktpreis = Grundpreis × Faktor
```

- **Grundpreis:** der Preis, wenn das Angebot genau der Nachfrage entspricht.
- **Nachfrage:** wie viele Stück die Stadt pro Jahr kauft (Bürger, andere Gewerbe, Export).
- **NPC-Anteil:** wie viel der Nachfrage Händler und Handwerker abdecken, die keine Spieler sind. Rohstoffe 90 % (Import), verarbeitete Waren 70 %.
- **Elastizität 0,5:** Doppeltes Angebot bedeutet etwa 71 % des Preises, halbes Angebot etwa 141 %.
- **Grenzen 0,5–2,0:** Der Preis fällt nie unter die Hälfte und steigt nie über das Doppelte des Grundpreises.

**Warum die NPC-Grundversorgung?** Ohne sie stieg der Preis im ersten Probelauf bei knappem Angebot auf das Doppelte, und die Mine war viermal so lukrativ wie die Gießerei. Die Stadt muss sich wie eine echte Stadt verhalten: Sie wird auch ohne Spieler versorgt. Spieler konkurrieren um den verbleibenden Anteil.

**Vereinfachung in Entwurf 1:** Die Rechnung betrachtet ein Jahr im Gleichgewicht. In Phase 2 bekommt jeder Marktstand einen echten Lagerbestand, der über den Tag schwankt: Verkäufe füllen ihn auf, die Nachfrage leert ihn. Dann gilt `Faktor = (Zielbestand / Bestand) ^ Elastizität`, mit demselben Ergebnis im Mittel.

## 4. Waren (Entwurf 1)

| Ware | Typ | Grundpreis | Zeit (Min.) | Zutaten | Nachfrage/Jahr | NPC-Anteil |
|---|---|---|---|---|---|---|
| Eisen | Rohstoff (Mine) | 12 | 12 | – | 600 | 90 % |
| Silber | Rohstoff (Mine) | 30 | 30 | – | 200 | 90 % |
| Beschlag | Zwischenprodukt | 48 | 35 | 2 Eisen | 150 | 70 % |
| Rapier | Fertigprodukt | 110 | 60 | 1 Eisen + 1 Beschlag | 120 | 70 % |
| Silberring | Fertigprodukt | 90 | 50 | 1 Eisen + 1 Silber | 150 | 70 % |

Rezepte nach dem Original, Zahlen eigene Festlegung. Der Balancing-Rechner kauft Zutaten vereinfacht zum Grundpreis.

**Umsetzung im Spiel (Phase 2):** Die Simulation kauft Zutaten zum **aktuellen Marktpreis**. Die Grundversorgung deckt bei Rohstoffen nur 90 % der Nachfrage, deshalb sind Eisen und Silber ohne Spieler-Mine rund 5 % teurer (Zutaten Silberring 44,27 statt 42 Gulden). Wer eine Mine betreibt, senkt die Rohstoffpreise für alle Schmiede. Käufe selbst verändern den Preis nicht, weil die Nachfrage der Stadt den Bedarf der Handwerker schon enthält. Die Simulation erzielt deshalb etwas niedrigere Gewinne als der Rechner (Gießerei allein: 812 statt 878).

**Wertschöpfung pro Arbeitsminute (Geselle, Grundpreise):**
- Silberring: (90 − 42) / 50 = **0,96** fl/Min.
- Rapier: (110 − 60) / 60 = **0,83** fl/Min. Mit selbst geschmiedetem Beschlag: (110 − 36) / 95 = 0,78 fl/Min.
- Beschlag: (48 − 24) / 35 = **0,69** fl/Min.

## 5. Arbeitskräfte und das Fron- und Sklavenstatut

| Arbeitskraft | Kosten/Jahr | Tempo Umland | Tempo Werkstatt | Risiko/Jahr | Schaden | Statut A | Statut B | Statut C |
|---|---|---|---|---|---|---|---|---|
| Lehrling | 120 (Lohn) | – | 0,67 | – | – | Werkstatt | Werkstatt | Werkstatt |
| Geselle | 220 (Lohn) | – | 1,00 | – | – | Werkstatt | Werkstatt | Werkstatt |
| Bergmann | 150 (Lohn) | 1,00 | 0,50 | – | – | Umland | Umland | Umland |
| Ork (Fronarbeiter) | 110 (Futter, Ketten, Wache) | 1,25 | 0,60 | 4 % Ausbruch | 800 | **verboten** | **nur Umland** | überall |
| Leibeigener | 50 (Unterhalt) | 0,85 | 0,85 | 6 % Aufstand | 600 | **verboten** | **verboten** | überall |

- Orks sind stark, aber grob: Im Umland arbeiten sie schneller (1,25), in der Werkstatt langsamer (0,6).
- **Erwarteter Schaden** = Risiko × Schaden. Er fließt als Durchschnittswert in die Jahresrechnung ein. Im Spiel wird daraus später ein zufälliges Ereignis (Ausbruch: Orks fliehen, verletzen Wachen, beschädigen den Betrieb).
- **Nicht in Gulden erfasst, aber spielentscheidend:**
  - Ansehensverlust bei Kirche und Humanisten
  - Generalstreik der freien Bürger
  - Kirchenbann
  - Eingreifen des kaiserlichen Reichsvogts bei Statut C

  Diese Effekte bauen wir mit den Säulen Gesellschaft und Politik.
- Lehrlinge werden **nach 4 Jahren** zu Gesellen befördert (Original-Mechanik). Das ist in Entwurf 1 noch nicht berücksichtigt.

## 6. Betriebe

| Betrieb | Arbeitsplätze | Fixkosten/Jahr | Besonderheit |
|---|---|---|---|
| Gießerei (Stufe 1) | 2 | 80 Unterhalt | Produziert Beschlag, Rapier, Silberring |
| Mine (Umland, Pacht) | 3 | 400 Pacht | Teilt die Arbeitszeit zwischen Eisen (12 Min.) und Silber (30 Min.) auf. Wird versteigert. |

**Jahresrechnung:** Umsatz − Zutaten − Löhne/Unterhalt − Fixkosten − Umsatzsteuer (5 %) − erwarteter Schaden = Gewinn

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

## 9. Logistik: Lager, Karren, Banditen (Phase 2, umgesetzt)

### Karren und Wege
| Karren | Ladeflächen × Stück | Preis | Startausstattung |
|---|---|---|---|
| Handkarren | 1 × 15 | 150 | Gießerei |
| Ochsenkarren | 2 × 20 | 400 | Mine |
| Kriegskarren | 3 × 25 | 900 | – |

- Ladeflächen sind frei belegbar (ein Ochsenkarren kann 40 Eisen laden).
- Fahrzeit vom Betrieb zum Markt: **30 Minuten** in der Stadt, **120 Minuten** ins Umland.
- Lager je Betrieb: 40 Stück je Ware.
- **Verwalter-Regel:** Der Karren fährt los, wenn eines davon zutrifft:
  - Er kann mindestens drei Viertel einer Ladung mitnehmen.
  - Ein Lagerplatz ist zu drei Vierteln voll.
  - Die Zutaten reichen für weniger als 5 Stück.
  - Ware wartet seit 4 Stunden.
- Ins Umland fährt der Karren nachts (21–06 Uhr) nur, wenn es ausdrücklich erlaubt ist.

### Eskorten und Überfälle
| Eskorte | Kosten je beladener Fahrt | Schutz |
|---|---|---|
| Keine | 0 | 0 % |
| Normal | 10 | 60 % |
| Schwer | 30 | 85 % |

- Eine beladene Umland-Fahrt wird mit **8 %** Wahrscheinlichkeit angegriffen, nachts mit 16 %.
- Erfolgschance des Überfalls = Stärke der Bande × (1 − Schutz), höchstens 100 %. Bei Erfolg ist die Ladung verloren.

**Banden (Banditen aller Völker, auch gemischt, plus wilde Bestien):**

| Bande | Zusammensetzung | Stärke | Häufigkeit |
|---|---|---|---|
| Wegelagerer | 4 Menschen | 0,8 | 3 |
| Gemischte Räuberbande | 2 Menschen, 1 Halbling, 1 Ork | 1,0 | 3 |
| Halblingsdiebe | 3 Halblinge | 0,6 | 2 |
| Fahnenflüchtige Zwerge | 3 Zwerge | 1,1 | 1 |
| Elbische Schmuggler | 2 Elben, 1 Mensch | 0,9 | 1 |
| Raubritter-Haufen | 1 Mensch, 1 Zwerg, 1 Elb, 2 Orks | 1,2 | 1 |
| Ork-Rotte (wild) | 5 Orks | 1,2 | 2 |
| Ogerhorde (wild) | 1 Oger, 2 Orks | 1,4 | 1 |

### Ergebnisse (Mine, 3 Bergleute, Durchschnitt über 100 Jahre)
| Karren | Eskorte | Gewinn/Jahr | Raub/Jahr |
|---|---|---|---|
| Ochsenkarren | keine | 1.016 | 138 |
| Ochsenkarren | normal | **1.040** | 70 |
| Ochsenkarren | schwer | 999 | 34 |
| Handkarren | keine | −215 | 51 |

**Spielerische Folge:**
- Welche Eskorte sich lohnt, hängt vom Karren ab. Beim Handkarren lohnt keine, beim Ochsenkarren die normale, beim Kriegskarren die schwere.
- Mit einem Handkarren kommt die Mine mit dem Abtransport nicht nach.

### Neue Referenzwerte mit Logistik (automatische Tests, Mittel über 20 Jahre)
| Szenario | Gewinn/Jahr |
|---|---|
| Gießerei allein (2 Gesellen) | 848 |
| 1 Lehrling | 289 |
| zwei konkurrierende Gießereien | je 611 |
| Mine mit 3 Bergleuten | 947 (ohne Eskorte, inkl. Banditen) |
| Mine mit 3 Orks | 1.365 |

**Warum die Gießerei etwas mehr verdient als der Rechner:** Ware wird in Fuhren verkauft. Unmittelbar vor einer Fuhre zählt das 24-Stunden-Fenster etwas weniger Angebot, deshalb fällt der Preis leicht höher aus (etwa +4 %). Das ist eine kleine, bewusst hingenommene Verzerrung.

**Fairness-Regel:** Kommen mehrere Karren in derselben Minute am Markt an, wird die Reihenfolge jede Minute ausgelost. Ohne diese Regel hatte der zuerst eröffnete Betrieb einen Vorteil von 9 %.

## 10. Stände, Schulden und Bankrott (Phase 4, umgesetzt)

| Stand | Schwelle (Vermögen) | Gebühr | Privilegien (vorerst nur beschrieben) |
|---|---|---|---|
| Gemeiner | – (Start) | – | Betrieb führen, Handel treiben |
| Bürger | 10.000 | 1.000 | Klage vor Gericht, Schöffen wählen, niedere Ämter |
| Patrizier | 20.000 | 2.500 | Wählbar in den Stadtrat, Steinhaus |
| Edler | 50.000 | 6.000 | Eigene Söldner, Fehde |
| Freiherr | 100.000 | 12.000 | Kaiserliche Privilegien |
| Reichsgraf | 200.000 | 25.000 | Höchster Stand der Stadt |

- Aufstieg im Rathaus, eine Stufe auf einmal. Die Gebühr zählt nicht als laufende Kosten (eigener Posten „Standesgebühren“, wie Investitionen außerhalb des Gewinns).
- **Messung (Startaufstellung, 4 Startwerte):** Ohne KI-Rivalen erreicht der Spieler 10.000 Gulden nach 7–8 Jahren, mit KI-Rivalen nach **9 Jahren** → Ziel Z2 (8–12 Jahre) erfüllt. Der KI-Rivale (Gießerei + Mine) wird nach 3 Jahren Bürger und ist nach 20 Jahren mindestens Patrizier.
- **Schulden:** Jahresabschluss im Minus → Mahnung; nach dem dritten in Folge → Bankrott (Betriebe stehen still, keine Befehle mehr). Einstellbar über `schulden.jahreBisBankrott`.

## 11. Expansion: Werkstatt kaufen, Grube pachten (Phase 4, umgesetzt)

| Liegenschaft | Art | Kosten | Erwarteter Gewinn bei voller Belegung (Start, Statut B) |
|---|---|---|---|
| Gießerei an der Südgasse | Werkstatt, 2 Plätze | 3.500 Kaufpreis + 80 Unterhalt/Jahr | ≈ 1.150 / Jahr |
| Grube am Silberberg | Grube, 3 Plätze | 400 Pacht/Jahr | ≈ 1.880 / Jahr |
| Grube im Tannenwald | Grube, 3 Plätze | 400 Pacht/Jahr | ≈ 1.880 / Jahr |

**Messung über 30 Jahre** (Startaufstellung; die KI-Regeln führen auch den „klugen“ Spieler):
- Haus Eisenbart wird nach 3 Jahren Bürger und pachtet sofort die Grube am Silberberg, Edler nach 17–20 Jahren.
- **Passiver Spieler** (nur die Startgießerei): ≈ 27.600 Gulden, bleibt Gemeiner (kauft das Bürgerrecht nie).
- **Kluger Spieler:** Bürger nach 9 Jahren, erwirbt sofort Grube im Tannenwald und Gießerei an der Südgasse, Patrizier nach 15–16 Jahren, ≈ 47.000 Gulden nach 30 Jahren.
- Der Rivale bleibt vorne (66–81 Tausend), weil er mit zwei Betrieben startet. Das ist der Vorsprung, den der Spieler aufholen soll.

## 12. Stadtrat (Phase 5, umgesetzt)

| Stufe | Amt | Amtseinkommen / Jahr | Mindeststand | Gewählt von |
|---|---|---|---|---|
| 4 | Bürgermeister | 800 | Patrizier | allen übrigen Ratsmitgliedern |
| 3 | Stadtrichter, Kämmerer | je 500 | Patrizier | Inhabern höherer Ämter |
| 2 | Stadthauptmann, Marktmeister | je 300 | Bürger | Inhabern höherer Ämter |
| 1 | Gefängniswärter, Zöllner | je 150 | Bürger | Inhabern höherer Ämter |

**Wahlformel:** Punkte = Ansehen + Gunst (+ 5 Amtsbonus für den Inhaber).
- Ansehen eines Hauses = Stand × 8 + Vermögen/1.000 × 0,4, höchstens 40.
- Ansehen eines Ratsherrn = 10 + Stufe seines Amtes × 6 (Zöllner 16, Bürgermeister 34).
- Gunst = Geschenk × 0,05 (200 Gulden → 10 Gunst), höchstens 100, verblasst um 30 % im Jahr.
- Ein Nachbarstatut (eine Stufe neben der Haltung) unterstützt ein Ratsherr ab Gunst 25.

Amtseinkommen und Geschenke stehen in der Jahresrechnung als eigene Posten außerhalb des Gewinns, wie Standesgebühren.

**Messung (Startaufstellung, KI-Rivale ausgewogen):**
- Haus Eisenbart wird nach etwa **10 Jahren Bürgermeister**; die Geschenke dafür kosten über die Jahre rund **830 Gulden**. Vorher hielt der Rivale das Amt zu billig, deshalb steigt das Ansehen eines Ratsherrn jetzt mit der Stufe seines Amtes, und das Ansehen eines Hauses ist bei 40 gedeckelt.
- Der Rivale stellt unter Statut B keinen Antrag: Statut C brächte ihm nur etwa 62 Gulden mehr im Jahr, weniger als die Schwelle von 200.
- **Rechenbeispiel niederes Amt:** Ein frischer Bürger mit 10.000 Gulden hat Ansehen 12. Der Gefängniswärter im Amt kommt auf 16 + 5 Amtsbonus = 21. Gewählt wird von den fünf höheren Ämtern; für die Mehrheit braucht man drei. Je 200 Gulden an drei Wähler (Gunst 10 → 22 Punkte) reichen, also **rund 600 Gulden** für ein Amt, das 150 Gulden im Jahr bringt. Das rechnet sich nach etwa vier Jahren und öffnet den Weg nach oben. Im Klicktest bestätigt: Mit Geschenken an nur zwei Wähler unterlag die Bewerbung 2 : 3.

## 13. Dynastie (Phase 5, umgesetzt)

| Volk | Startalter | häufigstes Sterbealter | volljährig | fruchtbar (Frau) | Geburtenrate/Jahr | Haushalt | Unterhalt je Kind | Hochzeit | Werben |
|---|---|---|---|---|---|---|---|---|---|
| Mensch | 24 | 64 | 16 | 16–40 | 30 % | 50 | 40 | 300 | ×1,0 |
| Halbling | 24 | 60 | 18 | 18–42 | 36 % | 40 | 30 | 250 | ×1,0 |
| Zwerg | 35 | 105 | 30 | 28–80 | 13 % | 60 | 50 | 900 | ×0,9 |
| Elb | 55 | 170 | 45 | 40–220 | 4,5 % | 150 | 120 | 600 | ×1,3 |

- **Sterberisiko je Jahr** (Mensch): 0,7 % mit 25, 3 % mit 50, 8 % mit 60, 25 % mit 70, 63 % mit 80.
- **Geburten:** Jedes lebende Kind der Mutter senkt die Rate um 15 %. Bei einer Ehe zwischen Völkern gilt die niedrigere Rate mal 0,5.
- **Kosten:** Haushalt für Oberhaupt und Ehepartner, Unterhalt für alle anderen Familienmitglieder, Brautgeschenke und Hochzeit stehen als eigener Posten „Familie“ außerhalb des Betriebsgewinns. Ein lediger Mensch kostet 50 Gulden im Jahr, ein Paar mit drei Kindern 220.
- **Rechenbeispiel Heirat:** Ein frischer Bürger mit 29.000 Gulden hat Ansehen 19,6. Für eine Gemeiner-Kandidatin (Schwelle 30) reichen 2 × 200 Gulden Geschenke (+20 Zuneigung) und 300 Gulden Hochzeit, die Heirat gelingt also im ersten Jahr. Ein Gemeiner mit 4.000 Gulden (Ansehen 1,6) braucht zwei Jahre, weil die Zuneigung je Jahr auf 20 begrenzt ist.

**Modellrechnung Dynastie-Risiko** (Heirat im 2. Jahr mit einem Partner des eigenen Volkes, 4.000 Durchläufe):

| Volk | erloschen bis 30 J. | bis 60 J. | bis 120 J. | Generationswechsel in 120 J. |
|---|---|---|---|---|
| Mensch | 1,3 % | 7 % | 43 % | 3,3 |
| Halbling | 1,2 % | 5 % | 22 % | 4,1 |
| Zwerg | 0,7 % | 2 % | 24 % | 2,2 |
| Elb | 0,9 % | 1 % | 3 % | 1,5 |

Ohne Witwenrecht wären es nach 30 Jahren 18–27 %. Das Dynastie-Risiko der Elben zeigt sich erst mit Pest, Mord und Duell, die auch Junge treffen. Dann fehlen ihnen die Erben.

**Messung im Spiel** (Haus Eisenbart, 20 Startwerte, 40 Jahre, `dotnet run --project tests/Reichsgilde.Core.Tests -- messen 40 20`):
- verheiratet in allen 20 Partien, im Schnitt im **Jahr 2,8**
- Ledige Zwergenfrauen sind in der Stadt selten. Die KI heiratet deshalb oft eine Menschen- oder Halblingsfrau, eine Ehe zwischen Völkern mit weniger Kindern.
- **2,1 Nachkommen** im Schnitt nach 40 Jahren, 0,3 Erbfälle, kein Haus erloschen
- Bürgermeister weiterhin nach etwa 11 Jahren (die Volksnähe der beiden zwergischen Ratsherren gleicht die Mischehe ungefähr aus)

## 14. Stadtgericht (Phase 5, umgesetzt)

| Wert | Zahl |
|---|---|
| Spitzel | 300 Gulden je Haus und Jahr, 50 % Chance je Spur |
| Gerichtsgebühr | 100 Gulden |
| Beweiskraft | 10 je Beweis, schuldig ab 15 |
| Gunstwirkung | ½ × (Gunst für Ankläger − Gunst für Beklagten), höchstens ±10 |
| sicherer Schuldspruch | ab Beweiskraft 25, also 3 Beweise |
| Buße | 3 × nachgewiesenes Bestechungsgeld, mindestens 200 |
| Pranger | 3 Jahre, −10 Ansehen |
| Entschädigung bei Freispruch | 200 Gulden |
| Verjährung | 5 Jahre |

**Messung** (Haus Eisenbart, 20 Startwerte, 30 Jahre):

| | Spieler passiv | Spieler bespitzelt jedes Jahr und klagt ab 3 Beweisen |
|---|---|---|
| Bürgermeister ab Jahr | 10,7 | 14,6 |
| Vermögen nach 30 Jahren | 91.200 | 76.300 |
| Bestechungsgeld über 30 Jahre | 1.800 | 2.100 |
| Schuldsprüche gegen den Rivalen | – | 81 in 20 Partien (≈ 4 je Partie), davon 73 mit Amtsverlust |

**Verlauf der Balance** (siehe ARCHITEKTUR, Lehrstück):
1. Ohne Obergrenze für die Gunstwirkung kaufte sich die KI frei: 62 Freisprüche zu 1 Schuldspruch.
2. Mit Obergrenze, aber ohne vorsichtige KI: 97 Schuldsprüche in 10 Partien, und die KI verlor 30.000 Gulden.
3. Endstand: Die KI rechnet unter Beobachtung mit erwarteter Buße (Kosten × 2,5) und besticht nur, wenn drei Jahre Nutzen das übersteigen.

Ein Spieler, der dauerhaft bespitzelt, zahlt 300 Gulden im Jahr. Er bremst den Rivalen spürbar, stoppt ihn aber nicht.

## 15. Gauner (Phase 5, umgesetzt)

| Gaunerstück | Preis | Erfolg | Wirkung | Vergehen |
|---|---|---|---|---|
| Diebstahl | 150 | 70 % (Wächter 35 %) | bis 12 Stück der wertvollsten Lagerware; Hehler zahlt 50 % | Diebstahl |
| Sabotage | 250 | 60 % (Wächter 30 %) | 360 Arbeitsminuten Stillstand, 150 Reparatur | Sabotage |
| Überfall | 300 | 0,8 × (1 − Eskortenschutz) | Ladung der nächsten beladenen Umlandfahrt; Hehler zahlt 50 % | Raub (Kerker) |
| Erpressung | – | Opfer entscheidet | Schweigegeld gegen die Beweise | Erpressung (Kerker) |

- **Zeugen:** 25 % je Gaunerstück, mit Wächter 60 %, misslungen doppelt so oft (höchstens 100 %).
- **Wächter:** 150 Gulden im Jahr je Betrieb (Posten Löhne).
- **Kerker:** 1 Jahr. Persönliche Befehle sind gesperrt, die Betriebe laufen weiter.

**Messung** (Haus Eisenbart, 20 Startwerte, 30 Jahre). „Passiv“ heißt, der Spieler tut nichts. „Wachsam“ heißt, er setzt jedes Jahr einen Spitzel an, klagt ab 3 Beweisen und stellt nach einem Angriff Wächter ein.

| KI-Charakter | Spieler | Gaunerstücke der KI je Partie | Raub und Schäden beim Spieler in 30 J. | Bürgermeister ab Jahr | Vermögen der KI |
|---|---|---|---|---|---|
| vorsichtig | passiv | 0 | 0 | 10,9 | 89.100 |
| vorsichtig | wachsam | 0 | 0 | 14,4 | 73.900 |
| ausgewogen | passiv | 1,4 | 370 | 10,4 | 92.600 |
| ausgewogen | wachsam | 0 | 0 | 14,6 | 76.300 |
| wagemutig | passiv | 27 | 7.400 | 10,7 | 91.600 |
| wagemutig | wachsam | 1 | 290 | 13,8 | 78.000 |

- Gegen den wagemutigen Rivalen verliert ein **ungeschützter** Spieler rund 7.400 Gulden in 30 Jahren, also etwa ein Viertel seines Vermögens.
- Ein **wachsamer** Spieler verliert dagegen fast nichts. Nach dem ersten Angriff macht sein Wächter weitere Diebstähle und Sabotagen für die KI unrentabel.
- Für die KI rechnet sich das Hinterzimmer kaum (Bilanz −344 je Partie). Sie schadet dem Konkurrenten, mehr gewinnt sie nicht.
- Damit macht der Charakter „wagemutig“ das Spiel wirklich schwerer, bleibt aber mit Wächtern beherrschbar.

## 16. Talente und Ausbildung (Phase 5, umgesetzt)

| Talent | Wirkung je Stern (Talent des Oberhaupts) |
|---|---|
| Verhandeln | +1,5 % Erlös beim Verkauf auf dem Markt |
| Handwerkskunst | +3 % Leistung aller Arbeitskräfte |
| Bei Nacht und Nebel | +4 Prozentpunkte Erfolg bei Diebstahl und Sabotage (höchstens 95 %), −8 % Zeugenchance |
| Kampf | +4 Prozentpunkte Stärke angeheuerter Wegelagerer, +3 Prozentpunkte Schutz der eigenen Karren (höchstens 95 %) |
| Rhetorik | +2 Punkte bei Ratswahlen, 1 Punkt Plädoyer vor Gericht (Differenz zwischen den Parteien, innerhalb ±10), +5 % Wirkung der Brautgeschenke |

| Volk | Verhandeln | Handwerk | Nacht und Nebel | Kampf | Rhetorik |
|---|---|---|---|---|---|
| Mensch | 2 | 1½ | 1 | 1½ | 2 |
| Zwerg | 1½ | 3 | ½ | 2½ | 1 |
| Elb | 2 | 2 | 1 | 1 | 3 |
| Halbling | 2 | 1½ | 3 | ½ | 1½ |

- **Neue Erwachsene:** Volksanlage ± 1 Stern. **Kinder:** ½ × Mittel der Eltern + ½ × Volksanlage ± ½. Höchstens 6 Sterne.
- **Ausbildung** ab der halben Kindheit bis zur Volljährigkeit (Mensch 8–16, Zwerg 15–30, Elb 22–45). Ein Hauptfach bringt insgesamt +4 Sterne, ein Nebenfach +2, für jedes Volk gleich viel.

| Weg | Gulden je Jahr | Talente |
|---|---|---|
| Zunftlehre | 60 | Handwerk, etwas Kampf |
| Kontorlehre | 30 | Verhandeln |
| Klosterschule | 40 | Rhetorik |
| Universität (ab Bürger) | 200 | Rhetorik und Verhandeln |
| Fechtmeister | 120 | Kampf, etwas Rhetorik |
| Gasse | 0 | Nacht und Nebel, etwas Kampf |

**Messung** (Haus Eisenbart, Zwerg, 20 Startwerte, 30 Jahre, Spieler passiv), ohne und mit Talenten:

| KI-Charakter | | Bürgermeister ab Jahr | Vermögen der KI | Hinterzimmer-Bilanz der KI | Schaden beim Spieler |
|---|---|---|---|---|---|
| ausgewogen | mit Talenten | 9,3 | 98.500 | +30 | 390 |
| wagemutig | ohne Talente | 10,7 | 91.600 | −340 | 7.400 |
| wagemutig | mit Talenten | 9,3 | 99.100 | +390 | 7.900 |

- Das Handwerkstalent der Zwerge (3 Sterne = +9 % Leistung) bringt der KI etwa **+8 % Vermögen**.
- Rhetorik 1 macht die KI rund ein Jahr früher zum Bürgermeister. Der Spieler (Mensch, Rhetorik 2) hätte bei einer Kandidatur selbst +4 Punkte.
- Mit **Kampf 2½** sind die Wegelagerer der KI 10 Prozentpunkte stärker. Ihre Überfälle **lohnen sich jetzt** (+390 statt −340 je Partie), der Schaden beim Spieler steigt aber nur um 8 %. Das ist so gewollt: Zwerge sind die gefährlicheren Raubritter.
- Zwergenkinder schließen ihre Ausbildung erst mit 30 ab, in 60 Jahren also rund ein Kind je Haus (KI: Universität oder Gasse).


## 17. Aktionspunkte (Phase 5, umgesetzt)

| Handlung | AP |
|---|---|
| Werben, Heiraten, Ratsherrn beschenken, Amt bewerben, Spitzel ansetzen, Bande anheuern | 1 |
| Statut-Antrag, Anklage, Erpresserbrief | 2 |
| Talent üben (+½ Stern, je Talent einmal im Jahr) | 2 unter 3 Sternen, 4 unter 5, 6 darüber |
| Wirtschaft, Stand, Liegenschaften, Wächter, Testament, Ausbildung, Erpressung beantworten | frei |

4 AP je Jahr, Vorrat höchstens 12.

**Messung** (Haus Eisenbart, 20 Startwerte, 30 Jahre). „Ohne AP“ heißt: dieselbe KI-Reihenfolge, nur ohne AP-Abschnitt.

| KI | Spieler | | Bürgermeister ab Jahr | Vermögen der KI | Talente Oberhaupt (Summe) | Schaden beim Spieler | Schuldsprüche |
|---|---|---|---|---|---|---|---|
| ausgewogen | passiv | ohne AP | 9,2 | 98.400 | 8,6 | 510 | – |
| ausgewogen | passiv | mit AP | 9,2 | 121.600 | 21,1 | 510 | – |
| ausgewogen | wachsam | ohne AP | 13,6 | 93.600 | 8,6 | 300 | 53 |
| ausgewogen | wachsam | mit AP | 13,3 | 114.100 | 21,1 | 300 | 40 |
| wagemutig | passiv | ohne AP | 9,2 | 99.000 | 8,6 | 8.200 | – |
| wagemutig | passiv | mit AP | 9,2 | 108.700 | 25,7 | 10.000 | – |
| wagemutig | wachsam | ohne AP | 14,2 | 91.700 | 8,6 | 1.200 | 73 |
| wagemutig | wachsam | mit AP | 15,8 (18/20) | 89.400 | 23,3 | 3.300 | 109 |

- **Die AP selbst bremsen die KI kaum.** Sie braucht im Jahr selten mehr als 2–3 AP: einmal bewerben und ein, zwei Geschenke.
- **Das Üben wirkt stark.** Übrige AP steckt die KI ins Üben, ihr Oberhaupt hat nach 30 Jahren rund 21–26 statt 9 Sterne. Allein Handwerk 6 (+18 % Leistung) und Verhandeln 6 (+9 % Erlös) machen die ausgewogene KI etwa 24 % reicher. Ein Spieler kann genauso üben.
- Der wagemutige Rivale übt Kampf und Nacht und Nebel. Seine Gaunerstücke lohnen sich deshalb auch gegen Wächter, und er landet öfter vor Gericht.
- **Entscheidung (14.10.2026):** Üben wird teurer, je höher das Talent schon ist: 2 AP unter 3 Sternen, 4 AP unter 5, 6 AP darüber.

**Nachmessung mit gestaffelten Kosten** (gleiche Partien):

| KI | Spieler | Bürgermeister ab Jahr | Vermögen der KI | Talente Oberhaupt | Schaden beim Spieler | Schuldsprüche |
|---|---|---|---|---|---|---|
| ausgewogen | passiv | 9,2 | 116.200 | 20,8 | 510 | – |
| ausgewogen | wachsam | 14,5 | 107.600 | 20,1 | 300 | 46 |
| wagemutig | passiv | 9,2 | 104.200 | 20,6 | 10.000 | – |
| wagemutig | wachsam | 16,9 | 90.700 | 20,1 | 3.150 | 99 |

- Die KI übt jetzt das **billigste** Wunschtalent zuerst. Sie verteilt ihre Sterne breiter, statt einzelne Talente auf 6 zu treiben. Die Summe bleibt bei etwa 20 Sternen, Spitzen gibt es kaum noch.
- Ihr Vorsprung im Vermögen sinkt von +24 % auf +18 % gegenüber der Partie ohne AP. Er bleibt spürbar, und ein Spieler kann ihn mit eigenem Üben ausgleichen.

## 18. Gesundheit (Phase 5, umgesetzt)

| Krankheit | Chance/Jahr | Dauer | Wirkung | tödlich | Kräuterschnaps |
|---|---|---|---|---|---|
| Husten | 5 % | 1 | Rhetorik −1 | – | heilt |
| Ruhr | 2 % | 1 | Verhandeln −1, Rhetorik −1 | 4 % | – |
| Lungenentzündung | 2 % | 1 | Handwerk −1, Kampf −1 | 8 % | – |
| Knochenbruch | 2 % | 1 | Nacht −1, Kampf −1½ | – | – |
| Vergesslichkeit | 1 % (ab 50: 3 %) | 2 | Verhandeln −1, Handwerk −1 | – | – |
| Angst im Dunkeln | 1 % | 1 | Nacht −2 | – | heilt |
| Haarausfall | 1 % | 2 | Ansehen −3 | – | – |
| Lachanfälle | 1 % | 1 | Ansehen −3 | – | heilt |
| **Pest** | alle 25–40 J., 35 % Ansteckung | Pestjahr | alle Talente −2, Krankenstand 30 % | 40 % (Medicus ×0,5, Schnaps ×0,8) | lindert |

Medicus 120 Gulden (Pest 300), Kräuterschnaps Grundpreis 25 (Marktware ohne Hersteller).

**Messung** (Haus Eisenbart, ausgewogen, 20 Startwerte, Spieler passiv):

| | 30 Jahre | 60 Jahre |
|---|---|---|
| Pestjahre (Summe über 20 Partien) | 8 | 26 |
| Krankheitstote im Haus Eisenbart (Summe) | 2 (alle Pest) | 4 (alle Pest) |
| erloschene Häuser | 1 / 20 (vorher 0) | 2 / 20 |
| Oberhaupt zu Jahresbeginn krank | 18 % der Jahre | 17 % der Jahre |
| Familienkosten der KI je Jahr (Haushalt, Medicus, Schnaps) | 264 | 299 |
| Vermögen der KI | 114.200 (vorher 116.200) | 244.600 |

- Die KI verliert niemanden an Ruhr oder Lungenentzündung, weil sie rechtzeitig den Medicus holt. Tödlich wird nur die Pest.
- Die Pest kostet die Wirtschaft ein Jahr lang 30 % Leistung, das Vermögen sinkt um etwa 2 %. Spürbar ist vor allem das Dynastie-Risiko: Stirbt ein kinderloses Oberhaupt an der Pest, erlischt das Haus.

## 19. Zweiter Beruf: Tischler (Phase 5, umgesetzt)

| Ware | Zutaten | Zeit (Min.) | Grundpreis | Gewinn je Arbeitsminute* |
|---|---|---|---|---|
| Holz (Holzschlag) | – | 10 | 10 | 0,95 |
| Holzzapfen | 2 Holz | 30 | 40 | 0,60 |
| Schemel | 2 Holz + 1 Holzzapfen | 45 | 95 | 0,67 |
| Truhe | 2 Holz + 1 Beschlag | 75 | 140 | 0,87 |

\* zu Grundpreisen nach Steuer, zum Vergleich mit dem Schmied: Eisen und Silber 0,95, Beschlag 0,62, Rapier 0,74, Silberring 0,87. Holz und Truhe entsprechen damit genau Eisen und Silberring.

Holzschlag und Tischlerei kosten so viel wie Grube und Gießerei (Pacht 400, Unterhalt 80, 3 bzw. 2 Plätze). Einen Beruf erlernen kostet 500 Gulden Lehrgeld und 4 AP.

**Messung** (20 Startwerte, 30 Jahre, KI ausgewogen, Spieler passiv, jetzt mit Haus Eichwald):

| | vorher (nur Eisenbart) | mit Eichwald |
|---|---|---|
| Eisenbart Bürgermeister ab Jahr | 9,2 | 14,3 |
| Eisenbart Vermögen | 114.200 | 88.800 |
| Eichwald Vermögen | – | 63.300 |
| Eichwald am Ende Bürgermeister | – | 7 / 20 |
| Eichwald erloschen | – | 2 / 20 |

- Beide Werkstätten leisten gleich viel: Eisenbart macht im Jahr etwa 30 Silberringe (1.500 Arbeitsminuten), Eichwald etwa 21 Truhen (1.575 Arbeitsminuten).
- Die beiden KI-Häuser machen sich vor allem im Rat Konkurrenz. Eisenbart wird gut fünf Jahre später Bürgermeister und bleibt ärmer, weil er für die Wahlen mehr bezahlen muss.
- Eichwald ist ärmer als Eisenbart: Elben sind schlechtere Handwerker (Handwerk 2 statt 3) und bekommen wenige Kinder (2 von 20 Häusern erloschen).

## 20. Meistergrade und Gebäudestufen (Phase 5, umgesetzt)

**Erfahrung:** Jedes Stück bringt so viele Stunden, wie ein Geselle dafür braucht – Werkstatt und Umland zählen für ihren Beruf. Ein Start mit voll belegter Gießerei bringt ≈ 24 Std. im Jahr, mit Grube ≈ 60.

| Grad | Erfahrung (Std.) | Gebühr | AP | schaltet frei |
|---|---|---|---|---|
| 2 | 60 | 200 | 2 | – |
| 3 | 250 | 600 | 2 | Stufe 2 |
| 4 | 650 | 1.200 | 3 | – |
| 5 | 1.300 | 2.000 | 3 | Stufe 3 |
| 6 | 2.300 | 3.000 | 4 | +1 AP je Runde |

| Stufe | Schmied | Tischler | Plätze | Unterhalt | Ausbau |
|---|---|---|---|---|---|
| 1 | Gießerei | Tischlerei | 2 | 80 | – |
| 2 | Schmiede | Drechslerei | 3 | 120 | 2.500 |
| 3 | Goldschmiede | Sägewerk | 4 | 180 | 5.000 |

| Ware (Stufe) | Zutaten | Zeit | Grundpreis | Gewinn/Min.* |
|---|---|---|---|---|
| Werkzeug (2) | 2 Eisen + 1 Holzzapfen | 50 | 125 | 1,10 |
| Silberkette (2) | 2 Silber + 1 Eisen + 1 Beschlag | 60 | 195 | 1,09 |
| Goldkette (3) | 2 Gold + 1 Silber | 80 | 265 | 1,27 |
| Prunkschwert (3) | 2 Eisen + 1 Gold + 2 Beschlag | 100 | 320 | 1,24 |
| Gehstock (2) | 3 Holz | 40 | 75 | 1,03 |
| Armbrust (2) | 2 Holz + 1 Holzzapfen + 1 Beschlag | 70 | 195 | 1,10 |
| Schrank (3) | 4 Holz + 2 Holzzapfen + 1 Beschlag | 100 | 310 | 1,27 |
| Prunktruhe (3) | 3 Holz + 2 Beschlag + 1 Gold | 100 | 330 | 1,28 |

\* zu Grundpreisen nach Steuer. Gold ist Marktware (Grundpreis 60). Die Nachfrage sinkt mit der Stufe (100 bzw. 50–60 Stück im Jahr) – wer viel davon macht, drückt den Preis schnell.

**Messung** (20 Startwerte, 30 Jahre, KI ausgewogen, Spieler passiv):

| | vorher (PR #28) | mit Meistergraden |
|---|---|---|
| Eisenbart Vermögen | 88.800 | 110.100 |
| Eisenbart Bürgermeister ab Jahr | 14,3 | 16,8 (19 / 20) |
| Eichwald Vermögen | 63.300 | 73.100 |
| Eichwald am Ende Bürgermeister | 7 / 20 | 2 / 20 |
| Eichwald erloschen | 2 / 20 | 1 / 20 |

| KI erreicht (Ø Jahr) | Grad 3 = Stufe 2 | Grad 5 = Stufe 3 | Grad 6 |
|---|---|---|---|
| Eisenbart | 5,0 | 13,2 | 20,6 |
| Eichwald | 5,0 | 15,6 | 23,7 |

- Die KI baut sofort aus, sobald der Grad reicht – der Umbau zahlt sich in 2–4 Jahren aus.
- Ein erster Entwurf (Grad 6 schon nach 1.250 Std.) brachte Grad 6 nach 13–15 Jahren; das war zu schnell, deshalb die höheren Schwellen.
- Mit mehr Waren wechselt die KI öfter das Produkt (die Märkte der teuren Waren sind klein); im Mittel unter 2 Wechsel im Jahr.

## 21. Dritter Beruf: Steinmetz (Phase 5, umgesetzt)

| Ware (Stufe) | Zutaten | Zeit | Grundpreis | Gewinn/Min.* |
|---|---|---|---|---|
| Bruchstein (Steinbruch) | – | 12 | 12 | 0,95 |
| Granit (Steinbruch) | – | 28 | 28 | 0,95 |
| Schleifziegel (1) | 1 Bruchstein + 1 Granit | 35 | 65 | 0,62 |
| Grabstein (1) | 2 Granit | 50 | 105 | 0,88 |
| Mühlstein (1) | 2 Bruchstein + 1 Schleifziegel | 60 | 140 | 0,73 |
| Säule (2) | 4 Bruchstein + 1 Schleifziegel | 60 | 190 | 1,13 |
| Taufbecken (2) | 3 Granit + 1 Schleifziegel | 70 | 240 | 1,13 |
| Steinfigur (3) | 3 Granit + 1 Schleifziegel + 1 Werkzeug | 100 | 420 | 1,25 |
| Grabmal (3) | 4 Granit + 2 Schleifziegel + 1 Gold | 100 | 450 | 1,26 |

\* zu Grundpreisen nach Steuer – die Stufen entsprechen Schmied und Tischler.

**Verflechtung:** Prunkschwert jetzt 2 Eisen + 1 Gold + 1 Beschlag + **1 Schleifziegel** (Grundpreis 340, 1,26 je Minute). Die Steinfigur braucht **Werkzeug** aus der Schmiede.

**Messung** (20 Startwerte, 30 Jahre, KI ausgewogen, Spieler passiv, jetzt drei KI-Häuser):

| | vorher (PR #29) | mit Kieselbach |
|---|---|---|
| Eisenbart Vermögen | 110.100 | 95.500 |
| Eisenbart Bürgermeister | 19 / 20 ab Jahr 16,8 | 15 / 20 ab Jahr 17,8 |
| Eichwald Vermögen | 73.100 | 74.600 |
| Kieselbach Vermögen | – | 90.600 |
| Kieselbach am Ende Bürgermeister | – | 8 / 20 |
| Gaunerstücke aller KI-Häuser (20 Partien) | 130 | 422 |
| Raub und Schäden beim Spieler | 587 | 324 |

- Kieselbach erreicht Stufe 2 nach ≈ 5 Jahren, Stufe 3 nach ≈ 13 – so schnell wie Eisenbart.
- Mit drei Rivalen verteilt sich die Konkurrenz: Eisenbart wird seltener Bürgermeister, der Spieler wird seltener bestohlen, weil die KI-Häuser öfter einander angreifen.

## 22. Vierter Beruf: Alchimist (Phase 5, umgesetzt)

| Ware (Stufe) | Zutaten | Zeit | Grundpreis | Gewinn/Min.* |
|---|---|---|---|---|
| Kräuter (Kräutergarten) | – | 10 | 10 | 0,95 |
| Kröte (Kräutergarten) | – | 25 | 25 | 0,95 |
| Farbe (1) | 2 Kräuter | 30 | 40 | 0,60 |
| Kräuterschnaps (1) | 1 Kräuter | 15 | 25 | 0,92 |
| Heilsalbe (1) | 2 Kräuter + 1 Kröte | 50 | 95 | 0,91 |
| Rosenwasser (2) | 4 Kräuter | 50 | 100 | 1,10 |
| Gegengift (2) | 2 Kräuter + 2 Kröte + 1 Silber | 70 | 185 | 1,08 |
| Theriak (3) | 3 Kräuter + 2 Kröte + 1 Gold | 100 | 280 | 1,26 |
| Zinnober (3) | 2 Farbe + 1 Silber + 1 Schleifziegel | 100 | 315 | 1,24 |

\* zu Grundpreisen nach Steuer. Kräuterschnaps hat einen kleinen Markt (Nachfrage 80, 95 % durch Händler gedeckt) – er lohnt nur in kleinen Mengen.

**Schrank:** jetzt 4 Holz + 1 Holzzapfen + **1 Farbe** + 1 Beschlag – gleiche Zutatenkosten wie vorher.

**Messung** (20 Startwerte, 30 Jahre, KI ausgewogen, Spieler passiv):

| | vorher (PR #30) | mit Alchimist und lernender KI |
|---|---|---|
| Eisenbart Vermögen | 95.500 | 121.400 |
| Eisenbart Bürgermeister | 15 / 20 ab Jahr 17,8 | 16 / 20 ab Jahr 18,0 |
| Eichwald Vermögen | 74.600 | 89.500 |
| Kieselbach Vermögen | 90.600 | 114.600 |
| Raub und Schäden beim Spieler | 324 | 324 |

Von der KI erlernte Berufe (in 20 Partien): Eisenbart → Alchimist 10×, → Steinmetz 2×; Eichwald → Schmied 6×, → Steinmetz 4×, → Alchimist 2×; Kieselbach → Alchimist 9×, → Schmied 5×.

- Ein erster Entwurf erlaubte das Erlernen schon als Bürger: Dann lernte fast jedes Haus früh einen zweiten Beruf. Jetzt erst als Patrizier (etwa ab dem 10.–15. Jahr).
- Die KI-Häuser werden um 20–27 % reicher: Sie führen einen Betrieb mehr, und mit Tinkturei und Kräutergarten gibt es zwei freie Betriebe mehr. Für den Spieler heißt das: Freie Liegenschaften sind ab der Spielmitte begehrt.

## 23. Fünfter Beruf: Wirt (Phase 5, umgesetzt)

| Ware (Stufe) | Zutaten | Zeit | Grundpreis | Gewinn/Min.* | Ausschank |
|---|---|---|---|---|---|
| Gerste (Bauernhof) | – | 10 | 10 | 0,95 | |
| Roggen (Bauernhof) | – | 12 | 12 | 0,95 | |
| Weingeist (1) | 1 Gerste + 1 Roggen | 30 | 43 | 0,63 | |
| Körnermahlzeit (1) | 1 Gerste + 1 Roggen | 30 | 50 | 0,85 | 62,5 |
| Dünnbier (1) | 1 Gerste + 1 Weingeist | 35 | 85 | 0,79 | 106 |
| Starkbier (2) | 2 Gerste + 1 Roggen + 1 Weingeist | 50 | 138 | 1,12 | 173 |
| Kräuterlikör (2) | 2 Weingeist + 1 Kräuter | 60 | 170 | 1,09 | 213 |
| Festbier (3) | 3 Gerste + 1 Weingeist + 1 Holzzapfen | 80 | 225 | 1,26 | 281 |
| Schädelbrand (3) | 1 Roggen + 3 Weingeist + 1 Beschlag + 1 Holzzapfen | 100 | 375 | 1,27 | 469 |

\* am Markt, zu Grundpreisen nach Steuer. Im Ausschank (Grundpreis × 1,25) bringt jede Ware rund ein Drittel mehr – aber nur so viel, wie Gäste kommen.

**Gäste:** 120 je Jahr für die ganze Stadt, Stunden 12–17 einfach, 18–23 doppelt gewichtet (40 nachmittags, 80 abends). Eine Schänke mit 2 Gesellen macht etwa 45 Körnermahlzeiten im Jahr – allein in der Stadt verkauft sie alles im Haus.

**Klicktest (ein Spieltag, Startberuf Wirt):** 43 Gäste bewirtet, 2.688 Gulden Ausschank, 5.351 Gulden am Abend (KI-Häuser ≈ 5.100).

**Messung** (20 Startwerte, 30 Jahre): Die KI lernt den Wirt als Patrizier in 20 von 60 Fällen (Eisenbart 11×, Kieselbach 9×). Vermögen praktisch unverändert (Eisenbart 121.600 statt 121.400, Eichwald 89.400, Kieselbach 115.100).

## 24. Sechster Beruf: Prediger (Phase 5, umgesetzt)

| Ware (Stufe) | Zutaten | Zeit | Grundpreis | Gewinn/Min.* |
|---|---|---|---|---|
| Lumpen (Klosterhof) | – | 10 | 8 | 1,00 |
| Wachs (Klosterhof) | – | 20 | 20 | 1,00 |
| Papier (1) | 2 Lumpen | 30 | 36 | 0,80 |
| Kerze (1) | 2 Wachs | 40 | 78 | 1,16 |
| Lehrschrift (1) | 3 Papier | 60 | 168 | 1,03 |
| Hetzschrift (2) | 2 Papier + 1 Farbe | 50 | 176 | 1,32 |
| Psalter (2) | 4 Papier + 1 Beschlag | 70 | 285 | 1,34 |
| Stundenbuch (3) | 5 Papier + 1 Gold + 2 Farbe | 100 | 470 | 1,61 |
| Reliquiar (3) | 2 Silber + 1 Gold + 1 Holzzapfen | 80 | 275 | 1,75 |

\* am Markt, zu Grundpreisen nach Steuer – im Band der anderen Berufe (Stufe 3: 1,54–1,83).

**Kirche:** Stufe 1–3 (2/3/4 Plätze, Fixkosten 80/120/180, Ausbau 2.500/5.000). Messe um 10 Uhr: 200 Gläubige × 2,5 Gulden = 500 Gulden Kollekte im Jahr für die ganze Stadt, verteilt nach Anziehung = Stufe (1/1,5/2) × (1 + Ansehen × 0,0125) × (1 + Rhetorik × 0,1). Ablass: 250 + 0,5 × Tatwert. Beichte 25 % je fremde Tat, Beichtbruch als Spur über 200 Gulden. Hetzschrift −5 Ansehen.

**Balance-Lehre:** Eine Lehrschrift ohne AP und ohne Obergrenze hob die Talente der KI nach 30 Jahren von 12,1 auf 20,3 Sterne und das Vermögen um ein Drittel. Darum: 1 AP, höchstens 3 Sterne, und die KI liest nur mit AP, die sonst verfielen.

**Klicktest (Startberuf Prediger):** Kirche am Weinmarkt produziert Kerzen, Kirchenabschnitt in der Verwaltung, Lehrschrift (Rhetorik 1 → 1½), Hetzschrift gegen Eisenbart (Ansehen 2 → −3), Ablass-Zeile im Gerichtssaal („keine fremde Kirche“).

**Messung** (20 Startwerte, 30 Jahre): Die KI lernt den Prediger in 16 von 60 Fällen. Vermögen Eisenbart 123.300 (vorher 121.600), Eichwald 93.400 (89.400), Kieselbach 113.400 (115.100); Talente 13,3 Sterne (12,1).

## 25. Siebter Beruf: Dieb (Phase 5, umgesetzt)

| Stufe | Betrieb | Diebe | Unterhalt | Beute je Geselle und Jahr | Fang je Diebesstunde | Aufträge je Nacht | Neu |
|---|---|---|---|---|---|---|---|
| 1 | Schmugglerloch | 4 | 80 | 576 | 1,2 % | 1 | Einbruch, Auskundschaften |
| 2 | Unterschlupf (2.500) | 6 | 120 | 634 | 0,96 % | 2 | Schutzgeld |
| 3 | Diebesgilde (5.000) | 8 | 180 | 749 | 0,72 % | 3 | Entführung |

Erwarteter Jahresgewinn voll mit Gesellen (Lohn 220): Schmugglerloch ≈ 1.344, also etwas über der Kirche (1.150); dafür −3 Ansehen, Fänge und Spuren.

**Einbruch** mit eigenen Leuten: Chance 0,7 + 0,1 (Wächter ×0,5), 12 / 18 / 24 Stück, Beute ins eigene Lager (Karren verkauft sie zum Marktpreis), Überlauf zum Hehler (50 %), Zeugen ×0,75. **Schutzgeld**: Brand im teuersten Betrieb des Opfers (Chance 0,6 + 0,1). **Entführung**: 55 %, nie das Oberhaupt.

**Probe** (Eisenbart bekommt zusätzlich ein Schmugglerloch, 20 Startwerte, 30 Jahre, Mediane): ausgewogen 127.000 statt 121.000, die anderen Häuser kaum verändert; Taschendiebe Ø 4.000 Gulden im Jahr (mit Ausbau), 0,64 Fänge im Jahr. Erste Fassung (Beweis für *alle* Häuser je Fang) ergab 143 Verurteilungen in 20 Partien und keinen Gewinn – deshalb nur noch ein Haus. Wagemutig: wegen der Erpressungsspirale (Entscheidungen §7.9) nicht aussagekräftig; 2 Entführungen und 7 Brände in 20 Partien.

**Messung** (Standard, ausgewogene Rivalen): unverändert – sie erlernen den Dieb nicht. Wagemutige Häuser wählen ihn selten, weil pachtfreie Umlandbetriebe ihre Plätze füllen.

**Klicktest (Startberuf Dieb):** Taschendiebe 576 Gulden bis 20 Uhr, Einbruch in Grube Eisenbart: 12 Silber (369) ins Schmugglerloch, Karren zum Markt.

## 26. Fix: Erpressungsspirale (umgesetzt)

Buße = max(200, 3 × Tatwerte), **höchstens 50 % des Vermögens** des Verurteilten (`justiz.busseHoechstensAnteil`).

| Messung wagemutig (20 Startwerte, 30 Jahre, Ø) | vorher | nachher |
|---|---|---|
| Haus Eisenbart | 49.468 | 77.931 |
| Haus Eichwald | −4.296.780 | 64.357 |
| Haus Kieselbach | 55.239 | 63.814 |

Standardmessung (ausgewogen) praktisch unverändert: 124.400 / 94.500 / 116.000 (vorher 123.300 / 93.400 / 113.400).

## 27. Achter Beruf: Gardist (Phase 5, umgesetzt)

| Stufe | Betrieb | Wachleute | Unterhalt | Sold je Geselle und Jahr (nach Steuer) | Razzien je Jahr | Beweischance je Tat |
|---|---|---|---|---|---|---|
| 1 | Wachhaus | 4 | 80 | 513 | 1 | 50 % |
| 2 | Stadtwache (2.500) | 6 | 120 | 564 | 2 | 60 % |
| 3 | Garnison (5.000) | 8 | 180 | 667 | 3 | 70 % |
| Umland | Wachturm (Pacht) | 3 | 400 | 513 | – | – |

Erwarteter Jahresgewinn voll mit Gesellen (Lohn 220): Wachhaus ≈ 1.090 (knapp unter der Kirche, ohne Fänge und Spuren), Wachturm ≈ 480 – er lohnt sich über den Schutz der eigenen Karren und den Geleitschutz.

**Patrouille:** 4 Gesellen im Wachhaus = 10 % weniger Erfolg für Diebstahl, Sabotage, Einbruch, Brand und Entführung in der Stadt, Zeugen +5 %. **Wachturm** voll besetzt: Überfallchance der Banden × 0,7.

**Messung** (Standard und wagemutig, 20 Startwerte, 30 Jahre): Wurf für Wurf identisch mit §26 – ohne Wache sind alle Faktoren neutral, und die KI wählt den Gardisten praktisch nie (pachtfreie Umlandbetriebe füllen ihre Plätze).

**Probe** (Eisenbart bekommt zusätzlich ein Wachhaus, Mediane):

| | ohne Wachhaus | mit Wachhaus |
|---|---|---|
| ausgewogen: Eisenbart | 126.000 | 132.000 |
| ausgewogen: Raub bei den anderen je Jahr | 499 | 242 |
| wagemutig: Eisenbart / Eichwald / Kieselbach | 78k / 67k / 61k | 95k / 90k / 95k |

Wache je Jahr: Sold Ø 4.383 (mit Ausbau), Geleitgeld 234, 1,56 Razzien. Die Wache schützt also alle Häuser, unter wagemutigen Rivalen deutlich.

**Klicktest (Startberuf Gardist):** Wachstube in der Verwaltung, Razzia im Beweisbuch, KI-Karren wechseln auf „Stadtwache“, Wachturm an der Landstraße mit eigenem Modell.

## 28. Neunter Beruf: Geldleiher (Phase 5, umgesetzt)

| Stufe | Betrieb | Schreiber | Unterhalt | Rahmen je Schreiber | Wechsel-Faktor | Kredite an Häuser bis | AP |
|---|---|---|---|---|---|---|---|
| 1 | Wechselstube | 2 | 80 | 2.000 | 1,0 | 3.000 | – |
| 2 | Geldleihe (2.500) | 3 | 120 | 2.500 | 1,2 | 6.000 | +1 |
| 3 | Bankhaus (5.000) | 4 | 180 | 3.000 | 1,4 | 12.000 | +2 |

Je Geselle und Jahr bei voller Kasse (Start, nach Steuer und Ausfall): ≈ 814 Gulden – davon Zins 240 − Ausfall 40, der Rest Wechsel. Erwarteter Jahresgewinn der Wechselstube voll besetzt (Lohn 220): ≈ 1.110, also wie Kirche (1.150) und Wachhaus (1.090); dafür bindet sie 4.000 Gulden in der Kasse.

**Kredit an Häuser:** Zins `Rest × Satz / 24` je Stunde, am Jahresende Zins + Rest / offene Jahre. Pfändung: teuerste Lagerware zu 75 % des Marktpreises. Wucher ab 20 %: Spurwert (Satz − 20 %) × Betrag × Jahre.

**Reinvermögen** = Bargeld + Kasse/Kredite an andere − eigene Kredite. Ohne Geldleiher identisch mit dem Bargeld. Die Messung zählt jetzt das Reinvermögen.

**Messung** (Standard und wagemutig, 20 Startwerte, 30 Jahre): identisch mit §26/§27 – die KI wählt den Geldleiher praktisch nie (pachtfreie Umlandbetriebe füllen ihre Plätze).

**Probe** (Eisenbart bekommt zusätzlich eine Wechselstube, Reinvermögen Ø):

| | ohne | mit Wechselstube |
|---|---|---|
| ausgewogen: Eisenbart / Eichwald / Kieselbach | 124.400 / 94.500 / 116.000 | 138.700 / 93.200 / 118.100 |
| wagemutig: Eisenbart / Eichwald / Kieselbach | 77.900 / 64.400 / 63.800 | 93.500 / 60.500 / 64.500 |

Bank je Jahr: Stadtzinsen 700, Wechsel 2.030, Ausfall 110, Kasse Ø 5.850, Stufe Ø 1,6. **Beobachtung:** +11 % für Eisenbart, etwa doppelt so viel wie Dieb oder Gardist in derselben Probe. Grund: Der Wechsel wächst mit dem Markt, und ein ausgebautes Bankhaus (5,6 Schreiber-Gewichte) nimmt fast den ganzen Bedarf der Stadt (6). Stellschraube, falls nötig: `wechsel.gebuehr` (0,7 %) oder `bedarfSchreiber`.

**Klicktest:** Startberuf Geldleiher (Verwaltung mit Kasse, Rahmen, Bankbuch, Zins; Wechsel bringt ab der ersten Stunde Umsatz), Spielstand mit Eisenbarts Wechselstube: Kredit 1.000 zu 14 % auf 2 Jahre im Kontor, Reinvermögen und Ablösung (Zinsen nach einer Stunde ≈ 6). Wechselstube mit Wechseltisch, Waage, Münzschild und Gitterfenstern.

## 29. Zehnter Beruf: Parfumeur (Phase 5, umgesetzt)

| Ware | Stufe | Zutaten | Zeit | Grundpreis |
|---|---|---|---|---|
| Lavendel / Jasmin | Duftgarten | – | 10 / 25 | 10 / 25 |
| Politur | 1 | Lavendel, Jasmin | 30 | 56 |
| Blume der Zwietracht | 1 | 2 Lavendel, Jasmin | 50 | 95 |
| Giftdolch | 1 | Eisen, Politur | 50 | 118 |
| Moschusduft | 2 | 2 Jasmin, Lavendel, Weingeist | 70 | 185 |
| Fechterduft | 2 | 2 Jasmin, Politur | 60 | 176 |
| Heiligenduft | 3 | 2 Lavendel, 2 Jasmin, Politur, Weingeist | 100 | 315 |
| Schattendolch | 3 | 2 Eisen, 2 Beschlag, Politur | 100 | 320 |

Goldkette 265 → 321 und Prunktruhe 330 → 386 (je + 1 Politur). Faustregel wie bei den anderen Berufen: Zutaten + ≈ 1 Gulden je Arbeitsminute (Stufe 3: ≈ 1,45).

**Wirkungen:** Moschus +10 Zuneigung (≈ 200 Gulden Werbegeschenke, aber am Jahreslimit von 20 vorbei) bzw. +12 Ratsgunst (≈ 240 Gulden Geschenk, ohne Bestechungsspur). Zwietracht −15 Gunst. Heiligenduft +4 Ansehen für bis zu zwei Jahre. Vergiftung: Handwerk −1, Kampf −1 (schwer: Kampf −2, Verhandeln −1), tödlich erst am Ende des Folgejahres (20 % / 45 %) – Gegengift (185) heilt.

**Messung** (20 Startwerte, 30 Jahre, Reinvermögen Ø):

| | vorher (§28) | mit Parfumeur |
|---|---|---|
| ausgewogen: Eisenbart / Eichwald / Kieselbach | 124.400 / 94.500 / 116.000 | 123.400 / 89.300 / 123.400 |
| wagemutig | 77.900 / 64.400 / 63.800 | 86.200 / 63.700 / 62.100 |

Die KI lernt den Parfumeur oft (ausgewogen 20× in 60 Fällen) und heiratet dank Moschus früher (Ø Jahr 1,3 statt 2,7). Gifttote: keine – die KI gibt rechtzeitig Gegengift. Erste Fassung (Gift tödlich schon am Jahresende des Anschlags, 21 Uhr) ergab 5 Gifttote bei Eisenbart in 20 wagemutigen Partien: Das Opfer hatte nur drei Stunden – deshalb wirkt das Gift jetzt bis Ende des Folgejahres.

**Klicktest (Startberuf Parfumeur):** Moschus bei Agnes (1 → 11 Zuneigung, Knopf danach gesperrt), Heiligenduft-Zeile in der Familientafel, Dolch- und Zwietracht-Zeile im Hinterzimmer, Giftdolch gegen Eisenbart: nachts „Giftanschlag im Haus Eisenbart“, Vergiftung in der Familienchronik. Siederei mit Kessel, Blumenkästen und Fensterläden; Duftgarten zwischen Grube Eisenbart und Silberberg.

## 30. Spieltag 05:00–22:00 Uhr (umgesetzt)

Ein Spieljahr hat jetzt 17 gespielte Stunden (1020 Ticks) statt 24. Was gleich bleibt: Arbeitszeit 8–20 Uhr (720 Arbeitsminuten – alle Jahreserträge unverändert), 15 Minuten Echtzeit je Jahr, Nachfrage je Jahr. Was sich anpasst:

| Größe | vorher | jetzt |
|---|---|---|
| Marktfenster | 24 Stunden (25 Fächer) | 17 Stunden (18 Fächer) – weiter genau ein Jahr |
| Gäste der Schänke | 12–24 Uhr, Gewichte 6 + 2 × 6 = 18 | 12–22 Uhr, Gewichte 6 + 2 × 4 = 14 – gleich viele Gäste je Jahr |
| Kreditzinsen | Rest × Satz / 24 je Stunde | Rest × Satz / 17 je Stunde |
| Hinterhalt gültig | 1440 Minuten | 1020 Minuten (ein Spieltag) |
| Gaunernacht | 21 Uhr | 20 Uhr |
| KI überlegt | 24× im Jahr | 17× im Jahr (Politik weiter um 11 Uhr) |

Die letzte Stunde (21–22 Uhr) rechnet die Jahresrechnung ab, bevor das Jahr schließt – sonst wären die Abendgäste ins neue Jahr gerutscht.

**Messung** (20 Startwerte, 30 Jahre, Reinvermögen Ø): ausgewogen 127.900 / 92.000 / 125.000 (vorher 123.400 / 89.300 / 123.400), wagemutig 84.000 / 60.600 / 63.100 (vorher 86.200 / 63.700 / 62.100). Kleine Verschiebungen durch das kürzere Marktfenster und den anderen KI-Takt; keine Neujustierung nötig.

**Klicktest:** Start um 05:00 im Morgenrot, Dunkle Nacht ab 20 Uhr, um 22:00 Jahresrechnung, danach 05:01 „Frühe Zunftglocke“. Die Ladeliste nennt alte Spielstände (24-Stunden-Tag), statt sie stumm wegzulassen.

## 31. Glaube und Konfessionen (Phase 5, umgesetzt)

| Wert | Zahl |
|---|---|
| Kirchgänger zu Spielbeginn | 55 % katholisch, 45 % evangelisch; Rückkehr 10 % je Jahr, mindestens 10 % |
| Wahlpunkte | gleich +5, anders −3, Heide −8, übergetretener Ehepartner −2 bei seinem alten Glauben |
| Fremde Kirche | Gläubige ohne eigene Kirche spenden 50 % |
| Konvertieren | 2 AP, 300 Gulden, −3 Ansehen bis Ende Folgejahr |
| Bekehrungspredigt | 1 AP, +4 Prozentpunkte × (1 + 0,1 × Rhetorik), +3 Gunst je Ratsherr des Glaubens |
| Stadtglaube | Antrag 2 AP; Glaubenszins 150 je Jahr; Freiheit ab Gunst 25, fremder Glaube ab Gunst 60; Ketzerei: Spur mit der Kollekte als Wert, Ketzerpredigt 200 |

**Messe-Rechnung (eine Kirche allein):** Eigene Gemeinde voll, die andere zur Hälfte – katholische Kirche 200 × 0,55 × 2,5 + 200 × 0,45 × 2,5 × 0,5 = **387,5** statt 500 Gulden; evangelische 362,5. Eine Bekehrungspredigt (Rhetorik 1) bringt etwa +11 Gulden Kollekte im Jahr – ihr Wert liegt vor allem in der Gunst (bei 7 Glaubensbrüdern 21 Gunstpunkte ≈ 420 Gulden Geschenke, ohne Bestechungsspur).

**Messung** (20 Startwerte, 30 Jahre, Reinvermögen Ø):

| | vorher (§30) | mit Glauben |
|---|---|---|
| ausgewogen: Eisenbart / Eichwald / Kieselbach | 127.900 / 92.000 / 125.000 | 129.600 / 93.600 / 127.700 |
| wagemutig | 84.000 / 60.600 / 63.100 | 76.800 / 68.200 / 65.200 |

In jeder Partie setzt der Rat einen Stadtglauben durch (ausgewogen 14× katholisch, 6× evangelisch; wagemutig 18× / 2×), danach treten die KI-Häuser über – am Ende haben alle drei denselben Glauben. Katholiken Ø 55 %: Bekehrungen sind selten (nur Häuser mit Kirche) und die Rückkehr zieht zum Start zurück. Ein passiver Spieler zahlt in 6 von 20 Partien Glaubenszins, wenn er beim falschen Glauben bleibt.

**Klicktest (Startberuf Prediger, evangelisch):** Hauptmenü mit Glaubenswahl; Kirchenstube „Eure Kirche ist evangelisch“, erwartet 363 Gulden; Bekehrungspredigt 45 → 49 % (374 Gulden), Knopf danach gesperrt; Konvertieren zu katholisch (Wendehals bis Ende 1401, Kirche jetzt katholisch, 377 Gulden); Ratsfenster mit Stadtglaube, Konfession je Inhaber und Antrag zum Stadtglauben.

## 32. Völkerboni je Beruf (umgesetzt)

| Volk | +5 % Leistung | −5 % Leistung |
|---|---|---|
| Mensch | Tischler, Gardist | – |
| Zwerg | Schmied, Steinmetz, Geldleiher | Dieb, Parfumeur |
| Elb | Alchimist, Prediger, Parfumeur | Steinmetz, Gardist |
| Halbling | Wirt, Dieb | Schmied, Gardist |

Es zählt das Volk des Oberhaupts. Der Bonus multipliziert die Leistung (mit Handwerkskunst und Krankenstand) und wirkt auch bei Dieb, Wache und Schreibern; Erfahrung wächst mit. Zum Vergleich: 5 % ≈ 1⅔ Sterne Handwerkskunst.

**Messung** (20 Startwerte, 30 Jahre, Reinvermögen Ø):

| | vorher (§31) | mit Völkerboni |
|---|---|---|
| ausgewogen: Eisenbart / Eichwald / Kieselbach | 129.600 / 93.600 / 127.700 | 139.200 / 95.200 / 132.100 |
| wagemutig | 76.800 / 68.200 / 65.200 | 85.600 / 68.100 / 63.000 |

Eisenbart (Zwerg, Schmied – der einzige Startberuf mit Bonus) gewinnt am meisten (+7 %). Die KI wählt ihre Zweitberufe nach dem Volk: Eisenbart lernt Prediger (12×) statt Parfumeur (Zwerge −5 %), Kieselbach öfter Wirt (14×), Eichwald Parfumeur (10×).

**Klicktest:** Tischler (Mensch): Kontor „Handwerk: Tischler (+5 %)“, Verwaltung „Volk (+5 %)“; Hauptmenü mit Zwergen: „Schmied, Steinmetz, Geldleiher +5 %; Dieb, Parfumeur −5 %“.

## 33. Handel von Hand per Karrenauftrag (umgesetzt)

| Wert | Zahl |
|---|---|
| Händlerspanne beim Einkauf | +10 % auf den Marktpreis (`handel.aufschlag`), Verhandeln −1,5 % je Stern |
| Preiswirkung | je gekauftes Stück −1 im Angebotsfenster des Jahres (symmetrisch zum Verkauf) |
| Marktzeit | 8–20 Uhr, maßgeblich ist die Ankunft des Karrens (Stadt 30 Min., Umland 120 Min.) |
| Ladung | je Ladefläche eine Ware, Hin- und Rückweg getrennt (Handkarren 1 × 15, Ochsenkarren 2 × 20, Kriegskarren 3 × 25) |

**Rechenbeispiel:** 10 Gold bei Grundpreis 60 (Preis 61,6) kosten von Hand ≈ 675 Gulden (Ø 67,5 je Stück); der Goldpreis steigt dadurch auf 63,2. Wer sofort wieder verkauft, bekommt nach Umsatzsteuer ≈ 600 – Spekulation lohnt nur, wenn der Preis inzwischen deutlich steigt (z. B. weil andere Häuser Gold brauchen und niemand verkauft).

**Messung** (20 Startwerte, 30 Jahre): identisch mit §32 – die KI handelt nicht von Hand, die Verwalter-Einkäufe bleiben ohne Preiswirkung.

**Klicktest (Schmied, 9 Uhr):** Handkarren unterwegs (Zutaten) → 10 Gold vorgemerkt → bei Rückkehr gestartet, „gekauft 10 Gold für 675 Gulden“, Preis 61,6 → 63,2, Gold automatisch zurückgehalten. Ein Verkaufsauftrag ohne Ware im Lager wird mit Grund abgelehnt.

## 34. Wappen und Hausnamen (umgesetzt)

Keine Balance-Änderung: Das Wappen ist reine Anzeige, und der Hausname „Haus Hammerschmid“ statt „Familie Hammerschmid“ ändert keine Würfe.

**Messung** (gemessen auf main `cfdb823` + PR #48, 20 Startwerte, 30 Jahre, Spieler Mensch): Eisenbart Ø 139.150, Eichwald Ø 95.155, Kieselbach Ø 132.085 Gulden – identisch mit §32/§33.

## 35. Bürger-KI, Schritt 1: Einwohner ohne Wirkung (PR #54)

Keine Balance-Änderung: Die Einwohner (25 je Haus, dazu 13 Ratsherren und 10 Kandidaten – bei 4 Häusern 123) lesen den Kern nur und würfeln mit eigenem Generator. Werte im Abschnitt `buerger` von `wirtschaft.json`.

**Messung** (gemessen auf main `c7ebbf5` + PR #54, 20 Startwerte, 30 Jahre, Spieler Mensch, KI ausgewogen und wagemutig): Ausgabe **zeichengleich** mit main. Rechenzeit 15 s / 17 s gegen 16 s / 16 s ohne Einwohner – im Rauschen der Messung, Budget +25 %.

**Tagesprofil** (Startwert 1400, 4 Häuser, ohne Schänke; Anteil der 123 Einwohner): 05 Uhr alle zu Hause · 06 Uhr Frühmesse (~75 %) · 08 Uhr Markt (~65 %) · 09–19 Uhr Arbeit 45–80 %, dazu Markt, Beten, Ruhen · ab 20 Uhr alle zu Hause (mit Schänke: ein Teil kehrt ab 12 Uhr und abends ein).

## 36. Bürger-KI: Wege, Wohnhäuser, Umland (PR #57)

Keine Balance-Änderung: Wege, Häuser und Umlandstellen würfeln nur mit dem Generator der Einwohner. Werte im Abschnitt `buerger` (`wege`, `haushaltVon/Bis`, `buergerhaeuser`, `umland`).

**Wegzeiten:** Abmarsch 0–10 Minuten nach der vollen Stunde; in der Stadt 10 ± 5 Minuten, Tor 15, Umland 30 – spätestens 40 Minuten nach der Stunde ist jeder da. Bei 4 Häusern: ~20 Wohnhäuser, 3 Bürgerhäuser, 13 Ratsherrenhäuser.

**Messung** (gemessen auf main `52c249c` + PR #57, 20 Startwerte, 30 Jahre, Spieler Mensch, KI ausgewogen und wagemutig): Ausgabe **zeichengleich** mit main. Rechenzeit 15–17 s wie ohne Einwohner.

## 37. Bürger-KI: Kopplung an die Schänke (PR #58)

**Regel:** Gäste je Stunde = bisherige Gäste (120 im Jahr nach Stundengewicht) × Faktor, Faktor = einkehrende Einwohner ÷ üblich zu dieser Uhrzeit, begrenzt auf 0,5–1,5. Verteilung auf die Schänken nach den Einwohnern darin; die Einwohner verteilen sich als Quote nach der Anziehung. Werte: `buerger.kopplung` = schaenke 1,0, spanne 0,5, glaettung 0,2.

**Messung 1** (`messen 30 20`, gemessen auf main `d7e49ee` + PR #58, Spieler Mensch, KI ausgewogen und wagemutig): **zeichengleich** mit main, 16–18 s. Grund: In der Messung baut kein Haus eine Schänke – die KI lernt den Wirt (Kieselbach 14×), pachtet aber nur den Bauernhof (offener Punkt unter 6b).

**Messung 2** (eigener Lauf, da `messen` keine Schänke enthält: Spieler als Wirt, passiv, 20 Startwerte × 30 Jahre, Jahr 2–30):

| Fall | Kopplung | Gäste der Spielerschänke je Jahr | Schwankung (σ, min–max) | Ausschank Ø | Vermögen Wirt Ø |
|---|---|---|---|---|---|
| Startaufstellung (Lager oft leer) | 0 | 120,0 | 0 (120–120) | 2.554 | 29.238 |
| Startaufstellung | 1,0 | 120,5 | 3,9 (110–131) | 2.554 | 29.238 |
| Volles Lager, dazu Wirtshaus Stufe 2 eines anderen Hauses | 0 | 47,9 (Wirtshaus 88,8) | 0,7 (45–49) | 4.748 | – |
| dito | 1,0 | 48,1 (Wirtshaus 89,0) | 1,5 (44–53) | 4.758 | – |

Ergebnis: Im Mittel bleiben Gäste, Verteilung nach Anziehung (1 : 1,85 mit Ansehen) und Tagesverlauf (nachmittags/abends 50/50 in den gemessenen Stunden 12–20 Uhr) gleich; neu ist eine Schwankung von Jahr zu Jahr um etwa ± 3 %. Der passive Wirt verkauft unverändert, weil sein Lager die Gäste begrenzt (79 von 120 abgewiesen). Ziele Z1–Z7 unberührt.

**Tagesprofil der Einwohner in den Schänken** (5 Startwerte × 6 Tage, Spieler Wirt, ~123 Einwohner): 12 Uhr 84 (Mittagstisch), 13–17 Uhr 11–31, 18–19 Uhr 24–33, 20 Uhr 48, 21 Uhr 76.

## 38. KI-Häuser: Werkstatt zum gelernten Handwerk (PR #60)

**Befund:** Die KI lernte Wirt, Prediger und Parfumeur nur für den Umlandbetrieb (Bauernhof, Klosterhof, Duftgarten, keine Kaufpreise). Danach war die Höchstzahl von 4 Betrieben (Patrizier) erreicht – Schänke, Kirche und Siederei kamen nie dazu. **Jetzt** kauft sie die Werkstatt zusätzlich und baut sie wie jede andere aus (in der Messung bis Festbier und Psalter, Stufe 3).

**Messung** (gemessen auf main `da23c2d` + PR #60, 20 Startwerte, 30 Jahre, Spieler Mensch passiv; Vermögen Ø am Ende):

| KI-Charakter | Eisenbart | Eichwald | Kieselbach | Eisenbart Bürgermeister |
|---|---|---|---|---|
| vorsichtig, vorher | 141.871 | 122.541 | 141.148 | 2/20 |
| vorsichtig, jetzt | 160.599 (+13 %) | 136.463 (+11 %) | 178.019 (+26 %) | 6/20 |
| ausgewogen, vorher | 139.150 | 95.155 | 132.085 | 14/20 |
| ausgewogen, jetzt | 168.899 (+21 %) | 100.968 (+6 %) | 169.483 (+28 %) | 10/20 |
| wagemutig, vorher | 85.600 | 68.142 | 63.028 | 13/20 |
| wagemutig, jetzt | 75.073 (−12 %) | 70.933 (+4 %) | 72.955 (+16 %) | 17/20 |

Erlernte Berufe unverändert (z. B. ausgewogen: Kieselbach → Wirt 14×, Eisenbart → Prediger 12×). Wagemutig schwankt durch Gaunerei und Kerker stark; Eisenbart verliert dort, weil die Wagemutigen sich gegenseitig mehr schaden (Raub und Schäden beim Spieler 519 → 869). Seit dieser Änderung gibt es KI-Schänken – damit wirkt die Kopplung der Bürger an die Schänke (§37) auch in `messen`. Rechenzeit 16–18 s.

Die Spielerziele Z1–Z7 betreffen den Spieler und bleiben unberührt; für den Spieler werden die KI-Häuser stärkere Gegner.

## 39. Bürger-KI: Kopplung an die Kirche (PR #61)

**Regel:** Zur Messe (10 Uhr) Gläubige und Kollekte jeder Kirche × Faktor, Faktor = Gebetsstunden der Einwohner seit Tagesbeginn ÷ üblich (gleitender Mittelwert, 20 % je Tag), begrenzt auf 0,5–1,5. Verteilung auf die Kirchen unverändert. Werte: `buerger.kopplung.kirche` = 1,0.

**Messung 1** (eigener Lauf: Standardaufstellung, 20 Startwerte × 30 Jahre, alle Jahre mit mindestens einer Kirche eines Hauses; gemessen auf PR #60 + diesem PR):

| KI-Charakter | Kopplung | Kirchenjahre | Gläubige je Jahr (alle Kirchen) | Schwankung σ (min–max) | Kollekte Ø |
|---|---|---|---|---|---|
| ausgewogen | 0 | 346 | 200,0 | 0 | 389 |
| ausgewogen | 1,0 | 346 | 200,9 | 8,8 (174–226) | 391 |
| vorsichtig | 0 | 234 | 200,0 | 0 | 396 |
| vorsichtig | 1,0 | 234 | 200,0 | 8,6 (179–224) | 396 |

Mit nur der Stunde vor der Messe als Maß schwankte es um σ 57–66 (100–300, an den Grenzen) – deshalb zählt der ganze Morgen.

**Messung 2** (`messen 30 20`, Mensch, gemessen auf PR #60 + diesem PR, gegen Kopplung 0): Vermögen der KI-Häuser bis auf wenige Gulden gleich (ausgewogen: Eisenbart 168.899 → 168.919, Eichwald 100.968 → 100.977; vorsichtig und wagemutig ebenso). Die Kollekte (~400 Gulden im Jahr) ist zu klein, um die Häuser spürbar zu verschieben. Rechenzeit 18 s (main ohne Bürger-KI 16 s, +12 %).

## 40. Bürger-KI: Kopplung an die Marktnachfrage (PR #63)

**Regel:** Für Fertigwaren und Heilmittel (42 Waren) gilt `Nachfrage = Nachfrage der Daten × Faktor`, Faktor = Einkaufsstunden der Einwohner in den letzten 17 Stunden ÷ üblich am Tag, begrenzt auf 0,8–1,2. Angebot (Grundversorgung + Verkäufe) unverändert, also `Preisfaktor = (Nachfrage × Faktor ÷ Angebot)^0,5`. Werte: `buerger.kopplung` = markt 1,0, marktSpanne 0,2, marktTypen [Fertigprodukt, Heilmittel].

**Wie stark der Faktor schwankt** (10 Startwerte × 28 Jahre, Standardaufstellung, gemessen jede Stunde): Ø 1,000, σ 0,028, Spanne 0,84–1,10; Tagesmittel σ 0,019. Der Preis schwankt dadurch um etwa ± 1,5 % (σ), selten bis ± 5 %. Die Einwohner kaufen recht gleichmäßig ein – die Grenzen 0,8 und 1,2 werden fast nie erreicht.

**Einschwingen:** Mit dem ersten Tag als Bezugsgröße lag der Faktor die ersten zehn Jahre bei 1,03–1,17 (der erste Tag hatte 64 statt ~79 Einkaufsstunden); die Gießerei aus Z1 verdiente 912 statt 848 (+7,5 %). Ohne den ersten Tag: Ø 0,999, Z1 wieder im Rahmen (Kern-Tests 848 / 611 ± 5 % grün).

**Messung** (`messen 30 20`, Mensch, gemessen auf main `da1599e` + PR #63; Vermögen Ø am Ende):

| KI-Charakter | Eisenbart | Eichwald | Kieselbach | Eisenbart Bürgermeister |
|---|---|---|---|---|
| vorsichtig, main | 160.597 | 136.466 | 178.019 | 6/20 |
| vorsichtig, jetzt | 155.796 (−3,0 %) | 135.296 (−0,9 %) | 179.193 (+0,7 %) | 7/20 |
| ausgewogen, main | 168.919 | 100.977 | 169.482 | 10/20 |
| ausgewogen, jetzt | 168.749 (−0,1 %) | 99.042 (−1,9 %) | 158.045 (−6,7 %) | 12/20 |
| wagemutig, main | 75.073 | 70.933 | 72.955 | 17/20 |
| wagemutig, jetzt | 81.917 (+9,1 %) | 61.725 (−13,0 %) | 65.549 (−10,2 %) | 18/20 |

Im Mittel kein Trend (Faktor Ø 1,0); die Abweichungen sind Streuung: Kleine Preisunterschiede ändern KI-Entscheidungen (Produkt, Ausbau, Gaunerei), und bei wagemutigen Häusern schaukelt sich das über Raub und Kerker auf. Die Spielerziele Z1–Z7 halten (Kern-Tests). Rechenzeit unverändert (19 s wie main auf derselben Maschine).

## 41. Stadttore schließen um 20 Uhr (PR #67)

**Regel:** `logistik.tore` = schliessenUm 20, torgeld 15. Umlandkarren (Fahrzeit 120 Min.) kommen nach 20 Uhr nur mit Nachtfahrt-Erlaubnis durchs Tor (15 Gulden je Durchfahrt, als Transportkosten); sonst warten sie ohne Gefahr bis 5 Uhr. Wer nach 18 Uhr ins Umland-zu-Markt losfährt, kommt erst am nächsten Morgen an – die Ware fehlt dem Markt in der Dunklen Nacht und steht morgens gebündelt da.

**Messung** (`messen 30 20`, Mensch, gemessen auf main `2f4d976` + PR #67; Vermögen Ø am Ende):

| KI-Charakter | Eisenbart | Eichwald | Kieselbach | Eisenbart Bürgermeister |
|---|---|---|---|---|
| ausgewogen, main | 168.749 | 99.042 | 158.045 | 12/20 |
| ausgewogen, jetzt | 170.235 (+0,9 %) | 101.958 (+2,9 %) | 152.002 (−3,8 %) | 9/20 |
| wagemutig, main | 81.917 | 61.725 | 65.549 | 18/20 |
| wagemutig, jetzt | 93.172 (+13,7 %) | 60.590 (−1,8 %) | 77.700 (+18,5 %) | 17/20 |

Ausgewogen im Rahmen der Streuung (vgl. §40: ± 7 % ohne Regeländerung). Wagemutig etwas besser: Weniger Nachtfahrten heißt weniger Überfälle mit doppelter Nachtgefahr; bei wagemutigen Häusern schaukelt sich Raub über Gaunerei und Kerker auf, deshalb die größere Schwankung. Die Spielerziele Z1–Z7 halten (Kern-Tests grün).

## 42. Bürger-KI: Kopplung an die Taschendiebe (PR #69)

**Regel:** Beute je Stunde = Diebesminuten × 0,8 × Stufenfaktor × Faktor, Faktor = Passanten der Stunde (Markt, Gassen, unterwegs) ÷ üblich zu dieser Uhrzeit, begrenzt auf 0,5–1,5. Fang und Spur wie bisher, nur mit dem Namen eines Passanten als Opfer. Werte: `buerger.kopplung` = diebe 1,0, bestohlen 20.

**Messung 1** (eigener Lauf: Spieler als Dieb, passiv, 20 Startwerte × 30 Jahre, Jahr 3–30; gemessen auf main `4894fe9` + PR #69):

| Kopplung | Beute je Jahr Ø | σ (min–max) | Fänge | Vermögen Spieler Ø |
|---|---|---|---|---|
| 0 | 698 | 267 (144–1.152) | 28 | 5.201 |
| 1,0 | 716 (+2,6 %) | 271 (143–1.343) | 28 | 5.325 |

Die große Streuung kommt schon ohne Kopplung von den Fängen (ein gefasster Dieb fehlt bis zur Neueinstellung). Die Kopplung legt im Mittel 2,6 % drauf: Viele Passanten in den Marktstunden fallen mit den Arbeitsstunden der Diebe zusammen.

**Messung 2** (`messen 30 20`, Mensch, alle drei Charaktere): **zeichengleich** mit main – seit #60 lernt kein KI-Haus mehr den Dieb, und ohne Diebesbetrieb wirkt die Kopplung nicht. Rechenzeit 18–19 s wie main.

## Wohnhaus je Haus (PR #75)

**Regel:** Abschnitt `wohnen`, Bauzeit 240 Min. Stufen (Ausbau / Unterhalt im Jahr / Ansehen / Pflicht ab Stand): Fachwerkhaus 0 / 0 / 0 / Gemeiner · Bürgerhaus 1.500 / 30 / +2 / Bürger · Steinhaus 4.000 / 80 / +4 / Patrizier · Stadtpalais 10.000 / 200 / +6 / Edler · Hof 25.000 / 500 / +8 / Freiherr. Alle Stufen bis zum Hof kosten zusammen 40.500 Gulden, danach 500 im Jahr. Ausbauen höchstens eine Stufe über dem Stand.

**Messung** (`messen 30 20`, Mensch, gemessen auf main `2e14225` + PR #75; Vermögen Ø am Ende, Stand von Haus Eisenbart am Ende):

| KI-Charakter | Eisenbart | Eichwald | Kieselbach | Eisenbart Bürgermeister | Eisenbart Stand |
|---|---|---|---|---|---|
| ausgewogen, main | 170.263 | 102.775 | 151.742 | 9/20 | 18 Freiherr, 2 Reichsgraf |
| ausgewogen, jetzt | 123.907 (−27 %) | 77.487 (−25 %) | 101.048 (−33 %) | 16/20 | 20 Freiherr |
| wagemutig, main | 91.843 | 60.133 | 75.793 | 16/20 | 18 Freiherr, 2 Edler |
| wagemutig, jetzt | 56.842 (−38 %) | 60.157 (±0 %) | 57.846 (−24 %) | 17/20 | 16 Edler, 3 Freiherr, 1 erloschen |

Das Wohnhaus bindet viel Geld: Wer bis Freiherr aufsteigt, zahlt 40.500 Gulden Ausbau plus bis zu 500 Gulden Unterhalt im Jahr – und das Geld fehlt danach in Betrieben und Zinsen. Ausgewogene Häuser erreichen weiterhin den Freiherrn, aber keiner mehr den Reichsgrafen; wagemutige Häuser bleiben meist beim Edlen hängen, weil sie ihr Geld ins Gaunern stecken und die Reserve für den Hof fehlt. Mehr Bürgermeister (16 statt 9) bei ausgewogen: Das Wohnhaus bringt Ansehen. Der Nutzer hat entschieden: **Die Preise bleiben** (16.10.2026).

## Gauner gegen das Wohnhaus (PR #77)

**Regel:** `gauner.wohnhaus` – Einbruch: Kosten 200, Chance 0,6, Beute = min(5 % des Bargelds, 300 × (Hausstufe + 1)), Bargeld ohne Hehler. Brand: Kosten 300, Chance 0,5, Reparatur = max(100, 20 % des Ausbaupreises der Stufe), das Ansehen des Hauses fehlt 1.020 Min. Hauswächter: 150 im Jahr, Chance × 0,5, Zeugen 0,6.

Was sich lohnt (Chance ohne Wächter und Streife): Ein Einbruch bei einem Haus mit 20.000 Gulden im Bürgerhaus bringt im Mittel 0,6 × 600 − 200 = 160 Gulden, im Fachwerkhaus 0,6 × 300 − 200 = −20 – gegen kleine Häuser lohnt er nicht, gegen reiche Häuser in großen Wohnhäusern schon (Hof: 0,6 × 1.500 − 200 = 700). Ein Brand trifft vor allem das Ansehen: im Stadtpalais 2.000 Reparatur und −6 Ansehen für einen Tag, kurz vor einer Ratswahl spürbar.

**Messung** (`messen 30 20`, Mensch, ausgewogen, gemessen auf main `1c2e7d2` + PR #77): **zeichengleich** mit main (Eisenbart 123.907, Eichwald 77.487, Kieselbach 101.048, Bürgermeister 16/20). Die KI-Häuser heuern noch keine Banden gegen Wohnhäuser an, also stellt auch niemand einen Hauswächter ein.

## Hauswert im Reinvermögen (PR #85)

**Regel:** Reinvermögen = Bargeld + Forderungen − Schulden + Hauswert. Hauswert = Ausbaupreise aller Stufen bis zur jetzigen (Bürgerhaus 1.500, Steinhaus +4.000, Stadtpalais +10.000, Stadthof +25.000), ein laufender Ausbau zählt schon mit.

**Messung** (`messen 30 20`, Mensch, gemessen auf main `2cd3222` + PR #85; Reinvermögen Ø am Ende, **jetzt mit Hauswert**, Stand von Haus Eisenbart am Ende):

| KI-Charakter | Eisenbart | Eichwald | Kieselbach | Eisenbart Bürgermeister | Eisenbart Stand |
|---|---|---|---|---|---|
| ausgewogen, main | 127.631 | 98.916 | 107.109 | 16/20 | 20 Freiherr |
| ausgewogen, jetzt | 174.720 | 106.067 | 144.125 | 16/20 | 19 Freiherr, 1 Reichsgraf |
| wagemutig, main | 80.665 | 47.197 | 63.970 | 18/20 | 16 Edler, 3 Freiherr, 1 erloschen |
| wagemutig, jetzt | 88.710 | 60.686 | 72.340 | 14/20 | 15 Freiherr, 5 Edler |

Ein Freiherr hat 40.500 Gulden Hauswert. Ohne ihn liegt Eisenbart (ausgewogen) bei etwa 134.000 Bargeld, also etwas über main: Die KI baut das Haus früher, weil sie nicht mehr auf Schwelle + Ausbau warten muss. Größte Wirkung bei den wagemutigen Häusern: 15 statt 3 werden Freiherr – bisher fraß der Bau ihr Reinvermögen auf, und sie blieben beim Edlen hängen. Ein Haus erreicht wieder den Reichsgrafen.

## Leerstand, Bauzonen und Neubau (PR #103)

**Regel:** Abschnitt `bauen` (Datenversion 34). Leerstand kaufen = max(0, 0,5 × Hauswert + 0,2 × Hauswert − Hauswert des eigenen Hauses), Sanierung 240 Min.; abreißen = 0,5 × Hauswert + max(100, 0,1 × Hauswert), 240 Min. Neues Wohnhaus = max(0, Hauswert der Zielstufe − Hauswert des alten), 1.020 Min. (Fachwerk-, Bürgerhaus) bzw. 2.040 Min. (ab Steinhaus). Neue Werkstatt = Kaufpreis der günstigsten freien Werkstatt der Art (3.500), 1.020 Min. ohne Löhne und Unterhalt. Hauswert = Ausbaupreise bis zur Stufe (Bürgerhaus 1.500, Steinhaus 5.500, Stadtpalais 15.500, Stadthof 40.500).

Was es bringt: Ein Patrizier im Steinhaus kauft ein leeres Stadtpalais für 0,7 × 15.500 − 5.500 = 5.350 Gulden statt 10.000 für den Ausbau, und sein Reinvermögen steigt um 0,3 × 15.500 = 4.650 – ein Schnäppchen, das es nur gibt, wenn ein Haus untergeht. Abriss lohnt nur für den Platz: Er kostet 60 % des Hauswerts (mindestens 100) und bringt nichts zurück.

**Messung** (`messen 30 20`, Mensch, gemessen auf main `38dd23b` + PR #103): Die KI-Häuser kaufen noch keinen Leerstand und bauen nicht; die Messung hat keine Karte, wirken kann also nur „Betriebe toter Häuser werden frei“. **Ausgewogen:** zeichengleich mit main (Eisenbart 170.059, Eichwald 96.175, Kieselbach 138.788, Bürgermeister 16/20). Kieselbach erlischt in einer von 20 Partien; seine Werkstätten stehen danach zum Verkauf, die Zahlen der anderen Häuser bleiben gleich – gekauft hat sie in dieser Partie niemand. Einziger Unterschied in der Ausgabe: Die Zeile „erlernte Berufe“ zählt nun „Kieselbach → Steinmetz 1×“, weil die Messung erlernte Berufe an den eigenen Betrieben abliest und das erloschene Haus keine mehr hat. **Wagemutig:** zeichengleich (kein Haus geht unter). Die Wirkung zeigt sich erst, wenn Häuser häufiger untergehen oder die KI Leerstand kauft (offener Punkt bei Spiel-KI).

## Abstimmen des Spielers im Rat (PR #111)

**Regel:** Ein Haus im Rat, das keine KI ist, legt seine Stimme bei Wahlen, zum Statut und zum Stadtglauben vorab fest (keine AP). Ohne Stimme gilt wie bisher: Bei Wahlen stimmt es für den bisherigen Inhaber, bei Anträgen enthält es sich. Keine neuen Werte in `wirtschaft.json`.

Was es bringt: Eine Stimme kippt knappe Fälle. Bei einer Wahl mit 5 Wählern macht sie aus 2 zu 3 ein 3 zu 2. Bei einem Antrag mit Gleichstand ohne den Spieler (der Bürgermeister dafür) kippt ein Nein den Antrag (3 zu 4 statt 3 zu 3).

**Messung** (`messen 30 20`, Mensch, ausgewogen, gemessen auf main `2f64149` + PR #111; Kern und Daten gleich main `5296754`): **zeichengleich** mit main (Eisenbart 170.396, Eichwald 97.581, Kieselbach 138.144, Bürgermeister 16/20). In der Messung legt der Spieler keine Stimme fest, also gilt überall die Vorgabe wie bisher.

## Werkstatt wie in Gilde 1 (PR #152)

**Regel:** Abschnitt `werkstatt` (Datenversion 36). Jeder Arbeiter hat eine eigene Ware, eine Werkstatt stellt mehrere Waren zugleich her. Lohnstufen knapp/üblich/gut = Lohn × 0,8 / 1 / 1,25, Laune-Ziel 25 / 50 / 75; die Laune läuft 3 Punkte je Arbeitsstunde aufs Ziel, Tempo = 1 + 0,2 × (Laune − 50) / 50. Prämie 30 Gulden je Arbeiter, +20 aufs Ziel für einen Spieltag. Unfall je Arbeiter und Jahr: Schmied 5 %, Steinmetz und Tischler 4 %, Alchimist und Parfumeur 3 %, Wirt und Prediger 2 %; Ausfall für den Rest des Tages (bis zum Jahreswechsel) mit Lohn, Lederschürzen halbieren die Gefahr. Einrichtung des Schmieds: Werkzeug −10 % Zeit (300), Meisterwerkzeug nochmals −10 % (800, ab Stufe 2), Blasebalg −10 % für Eisenwaren (350), Regale +20 Lager (200), Löschfass Feuer ×0,5 (100), Eisentür Diebstahl ×0,5 (300), Lederschürzen (80).

Was es bringt (Gießerei, 2 Gesellen, Silberring ≈ 48 Gulden Spanne je Stück, 14,4 Stück je Geselle und Jahr ≈ 690 Gulden):
- **Guter Lohn** kostet 2 × 55 = 110 Gulden mehr, Laune 75 bringt +10 % Tempo ≈ 140 Gulden – lohnt knapp, bei teuren Waren (Goldkette, Prunkschwert) deutlich. **Knapper Lohn** spart 88 Gulden, kostet −10 % Tempo ≈ 140 und droht nach einem Unfall mit Kündigung.
- **Gutes Werkzeug** (300) bringt +11 % Stück ≈ 150 Gulden im Jahr – nach zwei Jahren bezahlt. Meisterwerkzeug in der Schmiede (3 Plätze, teurere Waren) ähnlich nach zwei bis drei Jahren.
- **Unfälle:** Gewürfelt wird am Ende jeder der 12 Arbeitsstunden; wer verunglückt, verliert im Mittel 5,5 von 12 Stunden. Je Schmied-Arbeiter fehlen so 5 % × 0,46 ≈ 2,3 % eines Arbeitsjahres (≈ 16 Gulden beim Silberring); Lederschürzen (80) holen die Hälfte zurück – sie lohnen erst nach Jahren oder bei teuren Waren.

**Messung** (`messen 30 20`, Mensch, ausgewogen, gemessen auf main `3e206cd` + PR #152; die KI-Häuser zahlen üblichen Lohn und kaufen keine Einrichtung, nur die Unfälle wirken):

| Haus | vorher (main) | nachher | Änderung |
|---|---|---|---|
| Eisenbart (Schmied, von der KI geführt) | 163.070 | 161.736 | −0,8 % |
| Eichwald (Tischler) | 97.608 | 96.795 | −0,8 % |
| Kieselbach (Steinmetz) | 141.735 | 143.128 | +1,0 % |

Bürgermeister 18/20 → 16/20 (im Schnitt ab Jahr 14,2 → 13,4), Stand am Ende Freiherr 57 / Edler 3 → Freiherr 48 / Edler 11 / Reichsgraf 1, gekaufte Betriebe 168 → 164, kein Bankrott. Die Verschiebung liegt bei ±1 % – innerhalb der Streuung zwischen den Startwerten; erwartet war etwa −1 bis −2 % (Leistungsverlust Gefahr × 0,46). Die Referenztests der Gießerei (ein Startwert, 20 Jahre) bleiben in ihrer Toleranz: 848 → 822, 649 → 633. Nur der Vergleich der beiden Gießereien im Konkurrenztest bekommt ±8 % statt ±5 %, weil Unfälle die beiden zufällig verschieden oft treffen.

## Einrichtung des Tischlers (PR #154)

**Regel:** `werkstatt.einrichtung.Tischler` (Datenversion 37), gleiche Stücke und Preise wie beim Schmied, statt des Blasebalgs **Leimofen und Zwingen** (350): −10 % Zeit für Truhe, Schrank, Prunktruhe und Armbrust (nicht für Holzzapfen, Schemel, Gehstock). Mit gutem Werkzeug braucht eine Truhe 75 × 0,81 ≈ 61 statt 75 Minuten, ein Geselle schafft in 6 Stunden 5 statt 4. Das Holzgestell (`Regale`) gibt 60 statt 40 Lager je Ware. Gilt in Tischlerei, Drechslerei und Sägewerk.

**Keine Messung:** Die KI-Häuser kaufen keine Einrichtung, in `messen` ändert sich nichts.

## Einrichtung des Steinmetzen (PR #157)

**Regel:** `werkstatt.einrichtung.Steinmetz` (Datenversion 38), gleiche Stücke und Preise wie beim Schmied, statt des Blasebalgs der **Flaschenzug** am Dreibein (350): −10 % Zeit für Mühlstein, Säule, Taufbecken und Grabmal (nicht für Schleifziegel, Grabstein, Steinfigur). Mit gutem Werkzeug braucht ein Mühlstein 60 × 0,81 ≈ 49 statt 60 Minuten, ein Geselle schafft in 6 Stunden 7 statt 6. Das Steingestell (`Regale`) gibt 60 statt 40 Lager je Ware. Gilt in Maurerei, Steinschneiderei und Steinmetzhütte, nicht im Steinbruch.

**Keine Messung:** Die KI-Häuser kaufen keine Einrichtung, in `messen` ändert sich nichts.

## Einrichtung des Alchimisten (PR #160)

**Regel:** `werkstatt.einrichtung.Alchimist` (Datenversion 39), gleiche Stücke und Preise wie beim Schmied, statt des Blasebalgs der **kupferne Destillierhelm** (350): −10 % Zeit für Kräuterschnaps, Rosenwasser, Gegengift und Theriak (nicht für Farbe, Heilsalbe, Zinnober). Mit gutem Werkzeug und Destillierhelm braucht ein Kräuterschnaps 15 × 0,81 ≈ 12 statt 15 Minuten, ein Geselle schafft in 6 Stunden 29 statt 24. Die Kräuterregale (`Regale`) geben 60 statt 40 Lager je Ware, die Lederhandschuhe (`Handschuhe`, 80) halbieren die Unfälle wie die Schürzen der anderen Werkstätten (Gefahr des Alchimisten 3 % → 1,5 % je Arbeiter und Jahr). Gilt in Tinkturei, Kräuterladen und Alchimistenstube, nicht im Kräutergarten.

**Keine Messung:** Die KI-Häuser kaufen keine Einrichtung, in `messen` ändert sich nichts.

## Einrichtung des Parfumeurs (PR #165)

**Regel:** `werkstatt.einrichtung.Parfumeur` (Datenversion 41), gleiche Stücke und Preise wie beim Alchimisten, statt des Destillierhelms die **venezianischen Glasflakons** (350): −10 % Zeit für Moschusduft, Fechterduft und Heiligenduft (nicht für Politur, Blume der Zwietracht, Giftdolch, Schattendolch). Mit gutem Werkzeug und Glasflakons braucht ein Moschusduft 70 × 0,81 ≈ 57 statt 70 Minuten, ein Geselle schafft in 6 Stunden 6 statt 5. Die Flakonregale (`Regale`) geben 60 statt 40 Lager je Ware, die Lederhandschuhe (`Handschuhe`, 80) halbieren die Unfälle (Gefahr des Parfumeurs 3 % → 1,5 % je Arbeiter und Jahr). Gilt in Siederei, Duftwerkstatt und Parfumerie, nicht im Duftgarten.

**Keine Messung:** Die KI-Häuser kaufen keine Einrichtung, in `messen` ändert sich nichts.

## Einrichtung und Gäste des Wirts (PR #166)

**Regel:** `werkstatt.einrichtung.Wirt` (Datenversion 42), gleiche Stücke und Preise wie beim Parfumeur, statt der Glasflakons die **kupferne Braupfanne** (350): −10 % Zeit für Dünnbier, Starkbier und Festbier (nicht für Weingeist, Körnermahlzeit, Kräuterlikör, Schädelbrand). Mit gutem Werkzeug und Braupfanne braucht ein Festbier 80 × 0,81 ≈ 65 statt 80 Minuten. Die Fassregale (`Regale`) geben 60 statt 40 Lager je Ware, die Lederhandschuhe (`Handschuhe`, 80) halbieren die Unfälle (Gefahr des Wirts 2 % → 1 % je Arbeiter und Jahr). Gilt in Schänke, Wirtshaus und Gasthaus, nicht auf dem Bauernhof. Dazu kommt eine lesende Abfrage, wie viele Gäste gerade in einer Schänke sitzen (`Gastwirtschaft.GaesteJetzt`); sie ändert am Gästesystem nichts.

**Messung** (`messen 30 20`, Mensch, ausgewogen, gemessen auf main `f859c9f` + PR #166): **zeichengleich** mit main (Eisenbart Ø 129.647, Eichwald 88.479, Kieselbach 127.439, Bürgermeister 18/20). Die KI-Häuser kaufen keine Einrichtung, und die Abfrage liest nur.

**Arbeitszeit im Wirtshaus 8–22 Uhr** (Datenversion 43, Wunsch des Nutzers): `betriebsarten.<Art>.arbeitsminuten` = 840 für Schänke, Wirtshaus und Gasthaus (sonst `allgemein.arbeitsminuten` = 720). Herstellung ×14/12, Lohn je Stunde gleich (×14/12), Unfälle je Stunde gleich. Je Arbeitskraft ist der Gewinn damit genau 14/12 des bisherigen (Leistung und Lohn wachsen gleich, Risiko 0); der Gästeausschank bleibt, wie er war.

| Betrieb (Startpreise, voll belegt, `ErwarteterJahresgewinn`) | vorher | nachher | Vergleich (bester anderer Beruf derselben Stufe) |
|---|---|---|---|
| Schänke (Stufe 1) | 1.092 | 1.287 | Tinkturei, Siederei 1.221 |
| Wirtshaus (Stufe 2) | 2.312 | 2.717 | Steinschneiderei 2.499 |
| Gasthaus (Stufe 3) | 3.428 | 4.029 | Goldschmiede 4.081 |

Der Wirt rückt damit vom unteren Mittelfeld an die Spitze, ohne davonzuziehen.

**Messung** (gemessen auf main `f859c9f` + Funktions-Branch `f78f1e1` + diese Änderung): `messen 30 20` **zeichengleich** (in der Standardkarte baut kein Haus eine Schänke; die KI pachtet als Wirt nur den Bauernhof). `messen 30 20 greybox` (dort bauen die KI-Häuser Werkstätten und kommen bis zum Gasthaus, Schädelbrand 15 → 17 bei Eisenbart): Eisenbart Ø 156.891 → 162.279 (+3,4 %), Eichwald 100.413 → 102.862 (+2,4 %, ohne Wirt), Kieselbach 149.875 → 155.313 (+3,6 %; Gewinn im letzten Jahr 10.242 → 12.333), Bürgermeister 17/20 wie vorher, Stand am Ende Edler 1, Freiherr 59 → Edler 2, Freiherr 54, Reichsgraf 4 (bei Eisenbart als Spieler 2 von 20 Partien Reichsgraf). Die Unterschiede liegen nahe am Rauschen (andere Zufallsfolge, weil die Wirtshäuser abends weiter würfeln); kein Haus zieht davon, bankrott 0.

## Einrichtung und Gläubige des Predigers (PR #167)

**Regel:** `werkstatt.einrichtung.Prediger` (Datenversion 44), gleiche Stücke und Preise wie beim Wirt, statt der Braupfanne **Gänsekiele und Lapislazuli** (350): −10 % Zeit für Lehrschrift, Psalter und Stundenbuch (nicht für Papier, Kerze, Hetzschrift, Reliquiar). Mit gutem Werkzeug und Gänsekielen braucht ein Stundenbuch 100 × 0,81 = 81 statt 100 Minuten, mit Meisterwerkzeug (ab dem Dom) 72,9. Die Bücherregale (`Regale`) geben 60 statt 40 Lager je Ware, die Lederhandschuhe (`Handschuhe`, 80) halbieren die Unfälle (Gefahr des Predigers 2 % → 1 % je Arbeiter und Jahr). Gilt in Kirche, Dom und Kathedrale, nicht auf dem Klosterhof. Damit hat jeder Beruf mit Werkstatt Einrichtung. Dazu kommt eine lesende Abfrage, wie viele Gläubige gerade in einer Kirche sind (`Kirchenwesen.GlaeubigeJetzt`, dazu `MesseLaeuft`, `BeichteJetzt`); sie ändert an Messe und Kollekte nichts.

**Messung** (`messen 30 20`, Mensch, ausgewogen, gemessen auf main `f04ee7a` + Funktions-Branch `e7bec62` + diese Änderung): **zeichengleich** mit main (Eisenbart Ø 129.647, Eichwald 88.479, Kieselbach 127.439, Bürgermeister 18/20), obwohl die KI-Häuser in 17 von 60 Fällen Prediger werden: Sie kaufen keine Einrichtung, und die Abfrage liest nur (ohne den Messefaktor der Bürger-KI, der bei jedem Aufruf mitlernt).

## Einrichtung und Anwesenheit des Diebs (PR #171)

**Regel:** `werkstatt.einrichtung.Dieb` (Datenversion 45), Preise wie bei den Werkstätten. Der Dieb stellt nichts her, seine Stücke wirken auf die Gaunerei, und zwar aus dem Versteck (bester Diebesbetrieb mit Leuten):

| Stück (Kennung) | Preis | Wirkung |
|---|---|---|
| Dietriche (`Werkzeug1`) | 300 | Einbruchchance +0,10 absolut, wie `dieb.einbruch.chanceBonus`: (0,7 + 0,1 + 0,1) × Wächter 0,5 × Patrouille × Eisentür des Ziels, Deckel 95 %. Ohne Wächter 0,8 → 0,9, mit Wächter 0,4 → 0,45 |
| Meisterdietriche (`Werkzeug2`, ab Unterschlupf, braucht Dietriche) | 800 | nochmals +0,10: ohne Wächter 0,95 (Deckel, statt 1,0), mit Wächter 0,5 |
| Verkleidungstruhe (`Verkleidung`) | 350 | Fangchance der Taschendiebe × 0,75 (Schmugglerloch 1,2 % → 0,9 % je Diebesstunde) |
| Hehlerkammer (`Regale`) | 200 | Lager +20 je Ware (60 statt 40): mehr Einbruchsbeute bleibt im eigenen Lager statt beim Hehler (50 % des Preises) |
| Löschfass, Eisentür | 100, 300 | wie bei den Werkstätten: Feuer bzw. Diebstahl und Einbruch halb so oft |
| Lederhandschuhe (`Handschuhe`) | 80 | Spuren × 0,75: Zeugen der eigenen Aufträge (Einbruch 25 % × 0,75 × 0,75 ≈ 14 % statt 18,75 %), Beweise beim Auskundschaften (40 % → 30 % je Tat) und bei der Razzia gegen das Haus. Unfälle hat der Dieb nicht. |

Absolut statt ×1,1, weil der Bonus der eigenen Leute (`chanceBonus`) schon so rechnet; „+10 %“ heißt im ganzen Diebeswesen zehn Prozentpunkte. Die Meisterdietriche lohnen sich deshalb vor allem gegen Häuser mit Wächter oder Eisentür. Dazu eine lesende Abfrage, wer gerade daheim, am Markt oder auf Auftrag ist (`Diebeswesen.AnwesenheitJetzt`), nur fürs Bild: Die Beute rechnet weiter mit allen Arbeitern.

**Messung** (`messen 30 20` und `messen 30 20 greybox`, gemessen auf main `08eb15e` + Branch `0d823f8`): beide Ausgaben **zeichengleich** mit main (Eisenbart Ø 129.647 bzw. 162.279, Bürgermeister 18/20 bzw. 17/20). Ohne gekaufte Einrichtung sind die neuen Faktoren genau 1 bzw. der Bonus 0, die Abfrage würfelt nicht. Die KI kauft keine Einrichtung (und lernt den Dieb nur wagemutig, in den Messungen also nie).

## Einrichtung und Anwesenheit des Gardisten (PR #173)

**Regel:** `werkstatt.einrichtung.Gardist` (Datenversion 46), Preise wie bei den Werkstätten, für Wachhaus, Stadtwache und Garnison (nicht den Wachturm). Die Wache stellt nichts her, ihre Stücke wirken auf die Wache:

| Stück (Kennung) | Preis | Wirkung |
|---|---|---|
| Hellebarden (`Werkzeug1`) | 300 | Razzia-Chance je Tat +0,10 absolut, wie `wache.razzia.chanceJeStufe`; es zählt die Wachstube (beste eigene). Wachhaus 0,5 → 0,6, Stadtwache 0,6 → 0,7, Garnison 0,7 → 0,8 |
| Armbrüste (`Werkzeug2`, ab Stadtwache, braucht Hellebarden) | 800 | nochmals +0,10: mit beiden Stadtwache 0,8, Garnison 0,9; Deckel 95 % |
| Wachhunde (`Hunde`) | 350 | Die Leute des Hauses zählen × 1,25 im Schutz der Streife: 4 Gesellen 10 % → 12,5 % weniger Erfolg nächtlicher Taten in der Stadt, Zeugen +5 % → +6,25 %. Der Deckel `patrouille.hoechstens` (40 %) gilt danach |
| Kettenhemden (`Kettenhemd`) | 350 | Überfälle (Banden und Hinterhalte) auf Karren mit dem Geleit der Wache gelingen × 0,8: mit der Eskorte „Stadtwache“ (Schutz 0,8) Bande der Stärke 1,0 20 % → 16 %. Es zählt die Wachstube, die das Geleit stellt |
| Waffenkammer (`Regale`) | 200 | Lohn der eigenen Wachleute × 0,9 (eigene Ausrüstung, kein Ausrüstungsgeld): 4 Gesellen 880 → 792 Gulden im Jahr; der Sold der Stadt bleibt. Zuerst als „Lager +20“ geplant, aber die Wache lagert nichts |
| Löschfass, Eisentür | 100, 300 | wie bei den Werkstätten: Feuer bzw. Diebstahl und Einbruch im Wachhaus halb so oft |

Razzia absolut statt × 1,1, weil die Razzia je Stufe schon so rechnet. Kettenhemden auf den Erfolg der Räuber statt auf den Schutz: Schutz × 1,2 wäre 0,96 und läge am Deckel 95 %, Überfälle gelängen dann 75 % statt 20 % seltener. Dazu lesende Abfragen, wer gerade auf Streife, im Geleit oder daheim ist (`Wachwesen.AnwesenheitJetzt`), und wer in der Arrestzelle sitzt (`Gefangene`, die Taschendiebe des Jahres mit Beweis beim Haus) – nur fürs Bild: Der Schutz der Streife rechnet weiter mit allen Arbeitern.

**Messung** (`messen 30 20` und `messen 30 20 greybox`, gemessen auf main `1b3cb20` + Branch `b45018a`, mit der Waffenkammer nochmals auf `061c98c`): beide Ausgaben **zeichengleich** mit main (Eisenbart Ø 129.647 bzw. 162.279, Bürgermeister 18/20 bzw. 17/20). Ohne gekaufte Einrichtung sind die neuen Faktoren genau 1 bzw. der Bonus 0, die Abfragen würfeln nicht. Die KI kauft keine Einrichtung.

## Einrichtung und Kunden des Geldleihers (PR #175)

**Regel:** `werkstatt.einrichtung.Geldleiher` (Datenversion 47), Preise wie bei den Werkstätten, für Wechselstube, Geldleihe und Bankhaus. Der Geldleiher stellt nichts her, seine Stücke wirken auf das Bankgeschäft dieses Betriebs:

| Stück (Kennung) | Preis | Wirkung |
|---|---|---|
| Rechenbretter (`Werkzeug1`) | 300 | Rahmen je Schreiber × 1,1: Wechselstube mit 2 Gesellen 4.000 → 4.400 Gulden betreute Kasse, Stadtzinsen 480 → 528 im Jahr (volle Kasse) |
| Hauptbücher (`Werkzeug2`, ab Geldleihe, braucht Rechenbretter) | 800 | nochmals × 1,1, zusammen × 1,21: Geldleihe 2.500 → 3.025 je Schreiber, Bankhaus 3.000 → 3.630 (4 Gesellen 12.000 → 14.520, Zins 1.440 → 1.742) |
| Goldwaage (`Goldwaage`) | 350 | Wechselgebühr dieses Betriebs × 1,1 (nach dem Deckel der Stadt, anderen Geldleihern geht nichts verloren): je Geselle etwa +66 Gulden im Jahr vor Steuer (Markt wie zum Start) |
| Pfandkammer (`Pfandkammer`) | 350 | Verlust bei Ausfall × 0,75: Stadtkunden 10 % → 7,5 % der Kasse (erwartet bei 4.000 Kasse 80 → 60 im Jahr); geht ein Haus mit Kredit aus dem Spiel, bringt das Pfand 25 % des Rests zurück (2.000 Rest: 2.000 → 1.500 Verlust) |
| Kassenschrank (`Kassenschrank`) | 200 | Kredite an Häuser bis × 1,25 (`kredit.hoechstJeStufe` nach der Stufe des Kontors): Wechselstube 3.000 → 3.750, Geldleihe 6.000 → 7.500, Bankhaus 12.000 → 15.000. Es zählt der beste Kassenschrank des Hauses (wie die Pfandkammer). Entscheidung des Nutzers statt „Diebstahl aus der Kasse halbiert“ – den gibt es nicht |
| Löschfass, Eisentür | 100, 300 | wie bei den Werkstätten |

Rechenbretter und Hauptbücher multipliziert wie Werkzeug und Meisterwerkzeug der Werkstätten (1,21 statt 1,2). Dazu lesende Abfragen für den Raum: Kunden (`Bankwesen.KundenJetzt`: Kaufleute am Wechseltisch nach Marktumsatz und Tageszeit, Bürger am Schalter nach der genutzten Kasse, ab dem Bankhaus Kreditnehmer und Patrizier im Empfang) und was die Schreiber tun (`SchreiberJetzt`) – nur fürs Bild, Zins und Wechsel rechnen unverändert.

**Messung** (`messen 30 20` und `messen 30 20 greybox`, gemessen auf main `f2d7d60` + Branch `ee2174b`, mit dem Kassenschrank als Kreditfaktor nochmals auf `b7c9cd3`): beide Ausgaben **zeichengleich** mit main (Eisenbart Ø 129.647 bzw. 162.279, Bürgermeister 18/20 bzw. 17/20). Ohne gekaufte Einrichtung sind die neuen Faktoren genau 1, die Abfragen würfeln nicht. Die KI kauft keine Einrichtung.

## Keine Werkstätten zum Kauf in der Stadt (PR #162)

**Regel:** Datenversion 40. Unter `liegenschaften` stehen nur noch die neun Umlandbetriebe zur Pacht; die zehn Werkstätten der Stadt (Gießerei an der Südgasse … Wechselstube am Markt) sind gestrichen. Neue Werkstätten entstehen durch Bauen, frei werden welche durch Verkauf oder Untergang eines Hauses. Der Preis der Stufe 1 steht jetzt als `kaufpreis` an der Betriebsart (3.500, Schmugglerloch 3.000) – unverändert gegenüber den gestrichenen Liegenschaften, also gleicher Neubaupreis und gleicher Wert beim Verkauf.

**Messung** (gemessen auf main `3086bda` + PR #162, 30 Jahre, 20 Startwerte, Spieler Mensch passiv, KI ausgewogen; Vermögen Ø am Ende):

| Haus | ohne Karte vorher | ohne Karte nachher | Greybox vorher | Greybox nachher |
|---|---|---|---|---|
| Eisenbart (Schmied, von der KI geführt) | 161.736 | 129.647 (−20 %) | 161.916 | 156.555 (−3 %) |
| Eichwald (Tischler) | 96.795 | 88.479 (−9 %) | 96.186 | 111.613 (+16 %) |
| Kieselbach (Steinmetz) | 143.128 | 127.439 (−11 %) | 144.285 | 148.035 (+3 %) |
| Betrieb gekauft/gepachtet | 164 | 120 | 164 | 119 (+ 57 Werkstätten neu gebaut) |
| Stand am Ende | Edler 11, Freiherr 48, Reichsgraf 1 | Edler 7, Freiherr 53 | Edler 11, Freiherr 48, Reichsgraf 1 | Edler 17, Freiherr 43 |

- **Greybox** (die Karte des Spiels): Die KI baut die fehlenden Werkstätten neu, statt sie zu kaufen (57 Neubauten statt 45 Käufe). Im Mittel der drei Häuser +3 % – sie bleibt ähnlich, eine Anpassung der KI ist nicht nötig. Eichwald gewinnt am meisten (+16 %, er lernt öfter Parfumeur und Prediger); einzeln untersucht ist das nicht – die Neubauten stehen nah am Markt, und Reihenfolge und Zeitpunkt der Erwerbe verschieben sich. Eisenbart verliert etwas, weil ein Neubau ein Jahr ohne Einnahmen bleibt.
- **Mit den neuen Kernzonen K1–K3** (die frei gewordene Fläche in der Stadt wird Bauland, Entscheidung des Nutzers; gemessen auf main `3086bda` + Funktions-Branch `4c08c0e`, `messen 30 20 greybox`): Eisenbart 156.891 (−3 % gegen vorher), Eichwald 100.413 (+4 %), Kieselbach 149.875 (+4 %), im Mittel +1 %. Gekauft/gepachtet 119, Werkstätten neu gebaut 57, Stadthöfe neu gebaut 60 statt 42 (vorher 56), weil ein Stadthof nun auch in Kernlage Platz findet. Stand am Ende Edler 1, Freiherr 59 (vorher Edler 11, Freiherr 48, Reichsgraf 1): Mehr Häuser kommen zum Freiherrn, keins mehr zum Reichsgrafen. Bürgermeister 17/20. Das ist der Stand, mit dem PR #162 gemergt wird.
- **Kartenformat 2 und Gebäudekatalog** (PR #163; gemessen auf main `ac32342` + Funktions-Branch `824ceb2` mit der Karte aus dem Werkzeug, `messen 30 20 greybox`): Ausgabe zeichengleich mit main – Eisenbart 156.891, Eichwald 100.413, Kieselbach 149.875. Die verschobenen Gebäude (Steinbruch am Felsenhang 1,5 m, Kulisse 0,5 m), die Rathausgasse und die geknickten Wege berühren keine Bauzone und keine Stelle, an der die KI baut; Fahrzeiten rechnet der Kern noch nicht aus den Weglängen.
- **Ohne Karte** (`messen 30 20`): Hier wird nicht gebaut, die KI kann nur noch Umland pachten oder freie Werkstätten toter Häuser kaufen. Deshalb −9 bis −20 %. Das Spiel startet immer mit der Greybox-Karte; die Messung ohne Karte zeigt nur, wie viel der Werkstattkauf vorher ausgemacht hat.

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
