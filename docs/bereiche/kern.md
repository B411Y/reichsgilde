# Bereich Kern – Offen und Entscheidungen

**Nur der Kern-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (5) Bauen im Umland (Höfe und Gärten an Vorkommen) und in Köln (die Kölner Karte hat noch keine Wohnhausplätze); Verfall von Leerstand und Sanierungskosten nach Zustand (LEVELDESIGN §3) – Rest aus „Bauzonen, Bauplätze und Sanierung von Leerstand“ nach PR #103
- (3) Fahrzeit aus der Weglänge (Wegegraph), sobald es die erste Kartendatei gibt (`docs/LEVELDESIGN.md`)
- (–) „Kirche an der Westmauer“ umbenennen – steht seit der Stadterweiterung nicht mehr an der Mauer (Name in `wirtschaft.json`, Schlüssel in der Stadtansicht)
- (6) Multiplayer, Zuschauer-Modus
- (Nutzer) Pausenregeln im Multiplayer
- (–) Sporadisches Leck beim Beenden im Rauchtest finden: „10 ObjectDB instances leaked“ und „10 RID allocations of type DummyTexture leaked“, im zweiten Start (mit Tafeln), nur auf GitHub, lokal nicht nachzustellen. Der Rauchtest wiederholt solche Funde seit PR #83; die Warnung „1. Versuch“ im Lauf zeigt, wie oft es vorkommt. Befund vom 16.10.: Vor dem Merge von PR #77 kein Fall in 40 Läufen, danach etwa jeder dritte Lauf, auch zweimal hintereinander im selben Lauf. In Schleifen im selben Job (Branch `experiment/leck`, mit und ohne Hinterzimmer, mit Menü-Start davor, mit und ohne `tee`) dagegen 0 von 152 Starts, und mit `--verbose` nie – es hängt am Zeitablauf
- (von Darstellung) Karren kehren am Markt sofort um; ein kurzer Halt zum Abladen würde die Stehen-Bewegung zeigen.
- (von Infra) Zum Leck beim Beenden, Befund vom 16.10.2026: Einmal kam statt des Lecks ein Absturz (Rückgabewert 139, „36 ObjectDB instances leaked“, „1 resources still in use“) ganz ohne Ausgabe von Godots Absturzbehandlung. Die Fälle häuften sich am 16.10. zwischen 18:10 und 19:30 UTC, im Vergleich danach 0 von 16 Läufen (mit und ohne PR #100). Ausgeschlossen: Waren-Icons (im Rauchtest wird keins geladen) und Wappen. PR #100 gibt die Schriften beim Beenden frei – eine Absicherung, nicht bewiesen. Seit PR #101 zeigt ein Absturz oder Hänger die Rückverfolgung aller Fäden (Anmerkung im Lauf, Artefakt „Rauchtest-Protokoll“): Steht dort der Faden „.NET Finalizer“, gibt die Müllabfuhr von C# ein Godot-Objekt zu spät frei
- (–) Einrichtung für die anderen Berufe (Tischler, Steinmetz, Alchimist, Parfumeur, Wirt, Prediger) – bisher nur der Schmied hat Stücke; Schürzen und Löschfass fehlen dort also noch (PR #152)
- (–) Unfälle und Laune im Umland (Grube, Steinbruch …) und in Betrieben ohne Ware – bisher nur in Werkstätten mit Ware (PR #152)

## Entscheidungen

### Konfliktfreie Protokolle (PR #70)

| Thema | Entscheidung | Datum |
|---|---|---|
| Protokolle | **Eine Datei je Bereich** in `docs/bereiche/` (Offen, Entscheidungen, bei der Spiel-KI auch Messungen). Einträge ohne Nummern, unten anhängen. `docs/ENTSCHEIDUNGEN.md` wird Archiv; die Liste „Erledigt“ entfällt (zeigt GitHub). Grund: Alle Chats schrieben ans selbe Dateiende, an dieselben Listen und nahmen dieselben Abschnittsnummern – fast jeder zweite PR hatte einen Konflikt. | 16.10.2026 |
| Automatik | Action **„PRs aktuell halten“**: nach jedem Merge auf main den neuen Stand in alle fertigen PRs mergen und die Prüfung neu starten; `docs/bereiche/*.md` mit Git-Regel `union` (beide Einträge bleiben). Echte Konflikte → Label „Konflikt mit main“ und ein Kommentar. Entwürfe bleiben unberührt. | 16.10.2026 |
| Datenversion | Die Zeile `"version"` in `wirtschaft.json` erhöht **nur der Kern**. | 16.10.2026 |
| Mergen | Der Nutzer mergt weiterhin selbst (kein Auto-Merge). | 16.10.2026 |
| PR-Titel | Beginnen mit dem Bereich („Kern: …“). | 16.10.2026 |

### Wohnhaus je Haus (PR #75)

| Thema | Entscheidung | Datum |
|---|---|---|
| Wirkung | Das Wohnhaus ist **Pflicht für den Stand** und bringt **Ansehen** (Rat, Brautschau, Gericht). Bürger braucht das Bürgerhaus, Patrizier das Steinhaus, Edler das Stadtpalais, Freiherr den Hof. | 16.10.2026 |
| Ausbau | **Am selben Ort** Stufe für Stufe ausbauen, höchstens eine Stufe über dem eigenen Stand. Bauzeit 4 Stunden, bis dahin gilt die alte Stufe. | 16.10.2026 |
| Kosten | **Ausbau + jährlicher Unterhalt**: Bürgerhaus 1.500 / 30, Steinhaus 4.000 / 80, Stadtpalais 10.000 / 200, Hof 25.000 / 500 Gulden (Unterhalt 2 % des Ausbaus, mit den Haushaltskosten). Ansehen +2 / +4 / +6 / +8. | 16.10.2026 |
| Wohnen und Betriebe | Ins Wohnhaus ziehen Wohnstube und Familie sowie das Kontor. Die Werkstatt ist damit frei und soll verkauft werden können (Befehl folgt). | 16.10.2026 |
| Gauner | Das Wohnhaus wird ein Ziel für Gauner, die Betriebe bleiben es weiterhin (eigener PR). | 16.10.2026 |
| KI-Häuser | `KiRivale` baut das nötige Wohnhaus vor dem Aufstieg, wenn danach die Reserve bleibt (kleine Änderung im Bereich Spiel-KI). | 16.10.2026 |
| Alte Spielstände | Bekommen beim Laden das Haus, das ihr Stand verlangt. | 16.10.2026 |
| Preise nach der Messung | **Bleiben** (1.500 / 4.000 / 10.000 / 25.000, Unterhalt 2 %). Der Aufstieg soll Geld kosten; dass wagemutige KI-Häuser beim Edlen bleiben, verfeinert die Spiel-KI. | 16.10.2026 |

### Gauner gegen das Wohnhaus (PR #77)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gaunerstücke | **Einbruch und Brand** am Wohnhaus, zusätzlich zu den Stücken gegen Betriebe (die bleiben Ziel). | 16.10.2026 |
| Einbruch | **Anteil vom Bargeld, gedeckelt**: 5 %, höchstens 300 Gulden je Hausstufe (Fachwerkhaus 300 … Hof 1.500). Bargeld ohne Hehler. Kosten 200, Chance 60 %. Vergehen: Diebstahl. | 16.10.2026 |
| Brand | **Reparatur + Ansehen fehlt**: 20 % des Ausbaupreises der Stufe (mindestens 100), das Ansehen des Hauses fehlt einen Tag (1.020 Min.), so lange kein Ausbau. Die Stufe bleibt, der Stand ist nie in Gefahr. Kosten 300, Chance 50 %. Vergehen: Sabotage. | 16.10.2026 |
| Schutz | **Eigener Hauswächter** wie im Betrieb: 150 Gulden im Jahr, halbiert die Chance, Zeugen 60 % statt 25 %. Die Streife der Wache wirkt wie bei Betrieben in der Stadt. | 16.10.2026 |
| KI-Häuser | Stellen nach einem Anschlag auf ihr Wohnhaus einen Hauswächter ein (kleine Änderung im Bereich Spiel-KI). Selbst heuern sie noch keine Bande gegen Wohnhäuser an – Punkt für die Spiel-KI. | 16.10.2026 |

### Hauswert im Reinvermögen (PR #85)

| Thema | Entscheidung | Datum |
|---|---|---|
| Hauswert | Was in den Ausbau des Wohnhauses geflossen ist, zählt **ganz** zum Reinvermögen – ab der Bezahlung, also schon während des Baus. Grund: Der Ausbau senkte das Reinvermögen, wer an der Schwelle baute, fiel darunter und konnte nicht aufsteigen (gemeldet von Spiel-KI). | 16.10.2026 |
| Wirkung | Gilt überall, wo das Reinvermögen zählt: Stand, Rangliste, Ansehen, Bußdeckel, Kreditwürdigkeit, Bankrott, Sieg. Bezahlt wird weiter mit Bargeld; Brand und Unterhalt ändern den Hauswert nicht. | 16.10.2026 |
| KI-Häuser | `KiRivale` spart nur noch auf die Schwelle selbst und baut das Haus, sobald Ausbau, Gebühr und Reserve bar da sind (kleine Änderung im Bereich Spiel-KI, zwei Tests angepasst). | 16.10.2026 |
| Stadthof | Die oberste Stufe heißt „Stadthof“ statt „Hof“ (Wunsch Leveldesign: Verwechslung mit Bauernhof und Klosterhof). Spielstände speichern nur die Stufennummer, laden also weiter. | 16.10.2026 |

### Werkstatt verkaufen (PR #95)

| Thema | Entscheidung | Datum |
|---|---|---|
| Erlös | **75 % des Werts**. Wert = Kaufpreis Stufe 1 (3.500 wie die Liegenschaften) **plus alle Ausbaukosten** bis zur aktuellen Stufe: Gießerei 3.500, Schmiede 6.000, Goldschmiede 11.000 → Erlös 2.625 / 4.500 / 8.250. Startbetriebe ohne eigene Liegenschaft nehmen den Preis der gleichnamigen, sonst der günstigsten Liegenschaft derselben Art – kein neues Datenfeld, die Preise bleiben an einer Stelle. | 16.10.2026 |
| Danach | Der Betrieb wird **freie Liegenschaft mit seiner Stufe** (bleibt ausgebaut), Kaufpreis = **voller Wert**. Der Käufer braucht Mindeststand, Beruf und den **Meistergrad** der Stufe (wie beim Ausbau) und bekommt ihn leer mit Startkarren. Gilt auch für die Startbetriebe. Gespeichert im Spielstand; alte Spielstände laden weiter. In `wirtschaft.json` gibt es weiter nur Stufe 1 zu kaufen. | 16.10.2026 |
| Inventar | **Alles inklusive**: Personal, Wächter und Karren gehen mit (kein Erlös). **Lagerware** zum aktuellen Marktpreis **−20 %**, ohne Preiswirkung auf den Markt. Ist ein Karren unterwegs, wird der Verkauf abgelehnt. Die Kasse eines Geldleihers kommt zurück ins Bargeld. | 16.10.2026 |
| Laufende Kosten | Löhne, Wächter und Unterhalt bzw. Pacht werden beim Verkauf **anteilig bis zum Verkaufstag** fällig (sonst erst am Jahresende – wer kurz davor verkaufte, ließe das Jahr umsonst arbeiten). Vom Nutzer bestätigt am 17.10.2026. | 16.10.2026 |
| Umfang | Derselbe Befehl **kündigt Umlandpachten** (kein Erlös, nur die Lagerware). **Mindestens ein Betrieb bleibt**, **während eines Umbaus** kein Verkauf. | 16.10.2026 |
| Buchung | Erlös als eigener Posten **„Verkäufe“** in der Jahresrechnung, außerhalb des Betriebsgewinns (Gegenstück zu den Anschaffungen). Werte im Abschnitt `verkauf` (`anteil`, `warenAbschlag`), Datenversion 33. | 16.10.2026 |
| KI | Die KI-Häuser verkaufen noch nicht (offener Punkt bei Spiel-KI). Sie kaufen verkaufte Werkstätten nur mit dem nötigen Meistergrad (eine Zeile in `KiRivale.Expandieren`). Keine Messung nötig, solange die KI nicht verkauft. | 16.10.2026 |

### Leerstand, Bauzonen und Neubau (PR #103)

| Thema | Entscheidung | Datum |
|---|---|---|
| Umfang | Leerstand kaufen, sanieren, abreißen und freies Bauen in Bauzonen – zuerst in der Greybox-Stadt (Kartendatei `greybox.json`), Köln später. Im Umland wird noch nicht gebaut. | 17.10.2026 |
| Karte im Kern | Der Kern liest die Kartendatei (`Bauen/Stadtkarte.cs`): Wege mit Klasse, Bauzonen, feste Bauten; Wohnhausplätze als feste Bauten der Art `wohnhaus` in Konto-Reihenfolge, Werkstätten als `werkstatt` mit Betriebsnamen. Die Karte liegt im Spielstand wie die Wirtschaftsdaten. | 17.10.2026 |
| Bauregeln | LEVELDESIGN §3: Raster 2 m, Drehung in Vierteln, Fläche inkl. 2 m Vorplatz ganz in passenden Bauzonen oder auf Bauplätzen, keine Überdeckung. Mindestens zwei Vorplatzfelder höchstens 7 m von der Mitte einer Karrenstraße (Werkstatt) bzw. Straße oder Gasse (Wohnhaus). Gewerbe in jeder Stadt-Bauzone, Stadthof nur Kernlage oder Mauerring. Größen: Werkstatt 16 × 14, Fachwerk- bis Steinhaus 8 × 10, Stadtpalais 12 × 10, Stadthof 16 × 14. Kein Grundstückskauf. | 17.10.2026 |
| Leerstand kaufen | Nur Wohnhäuser bankrotter/erloschener Häuser, nur größer als das eigene, höchstens eine Stufe über dem Stand (wie beim Ausbau; vom Teamleiter bestätigt). Preis = max(0, 50 % Kauf + 20 % Sanierung des Hauswerts − Hauswert des eigenen Hauses), keine Auszahlung. Sanierung 4 Stunden, danach Umzug; das alte Haus wird Bauplatz. | 17.10.2026 |
| Leerstand abreißen | 50 % Kauf + 10 % Abriss (mindestens 100 Gulden, vom Teamleiter bestätigt), 4 Stunden, danach Bauplatz für jeden – in der Größe des alten Grundstücks, auch außerhalb der Bauzonen. | 17.10.2026 |
| Neubau | Wohnhaus: Zielstufe ≥ eigene Stufe, Stand-Regel wie beim Ausbau, Preis = max(0, Hauswert der Zielstufe − Hauswert des alten Hauses); Umzug bei Fertigstellung, das alte wird Bauplatz. Werkstatt (Stufe 1): Preis und Voraussetzungen wie beim Kauf einer freien Werkstatt (3.500, Stand, Handwerk, ein Jahr Unterhalt bar), Name nach der nächsten Karrenstraße. Bauzeit 1 Spieljahr (Werkstatt, Fachwerk-, Bürgerhaus) bzw. 2 (ab Steinhaus); bezahlt beim Baubeginn, während des Baus keine Löhne, kein Unterhalt. Ein Neubau verbraucht jeden Bauplatz ganz, den er berührt – auch einen Rest daneben (vom Teamleiter bestätigt). | 17.10.2026 |
| Wohnhausplätze | Ein Bauplatz, der aus einem Wohnhausplatz der Karte entsteht (Abriss oder Umzug), nimmt jedes Wohnhaus jeder Stufe auf – eingepasst in genau seine Fläche und Drehung (in der Greybox 17 × 8 m, der Ring ist nicht tiefer), wie der Ausbau am Platz. Keine Werkstatt, kein Haus halb darauf. Die Normmaße gelten nur in den Bauzonen. Was dort stehen darf, sagt die `lage` des Platzes (sonst die nächste Bauzone). Entschieden vom Teamleiter. | 17.10.2026 |
| Greybox | M1 vor der Westmauer zählt bewusst als Mauerring (innerhalb der Mauer ist kein Platz für einen Stadthof) – keine Sonderregel im Kern. Der `probeplan` der Karte (15 Gebäude) ist ein Test: alle gültig, Abwandlungen abgewiesen. | 17.10.2026 |
| Ausbau am Platz | Bleibt unverändert (4 Stunden, alle Stufen) – sonst kommt die KI nicht mehr zu ihrem Stand. Während eines Umzugs kein Ausbau. | 17.10.2026 |
| Hauswert | Ab der Bezahlung zählt das neue Haus (wie beim Ausbau). Der Kauf von Leerstand hebt das Reinvermögen um bis zu 30 % des Hauswerts (Schnäppchen). Ein totes Haus, dessen Wohnhaus verkauft oder abgerissen ist, hat keinen Hauswert mehr. | 17.10.2026 |
| Betriebe toter Häuser | Werkstätten und Pachten bankrotter oder erloschener Häuser stehen zum Verkauf wie verkaufte (mit Stufe, vollem Wert, Meistergrad), ohne Erlös. Alte Spielstände geben sie beim Laden frei. | 17.10.2026 |
| Werte | Abschnitt `bauen` (raster, strassenAbstand, werkstatt, wohnhaus je Stufe mit breite, tiefe, bauzeitJahre, lagen; leerstand mit kauf, sanierung, abriss, abrissMindestens, abrissMinuten), Datenversion 34. | 17.10.2026 |
| KI | Kauft frei gewordene Werkstätten schon (`Expandieren`); Leerstand kaufen und neu bauen später (Punkt bei Spiel-KI). | 17.10.2026 |

### Stadtpalais und Stadthof nur als Neubau (PR #108)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ausbau am Platz | Fachwerkhaus → Bürgerhaus → Steinhaus wie bisher am Platz. **Stadtpalais und Stadthof nur als Neubau** oder durch Kauf eines Leerstands, nicht als Ausbau (LEVELDESIGN, Gebäudegrößen; bestätigt vom Nutzer). Ersetzt die Zeile „Ausbau am Platz“ aus PR #103. | 17.10.2026 |
| Feld | `bauen.wohnhaus[i].nurNeubau` (Stadtpalais, Stadthof), Datenversion 35 – im Abschnitt `bauen`, weil die Regel nur gilt, wo gebaut wird. Das Startwohnhaus und die Werkstatt dürfen es nicht haben (Prüfung). | 17.10.2026 |
| Ohne Karte | Die Regel gilt nur bei `Bauen.IstAktiv`. Ohne Karte bzw. Bauzonen (Köln) bleibt der Ausbau zu allen Stufen erlaubt, sonst käme dort niemand mehr zum Palais. Ein Ausbau, der in einem alten Spielstand schon läuft, wird fertig. | 17.10.2026 |
| Platz in der Greybox | Gierig gezählt passen gleichzeitig **9 Stadtpalais** (12 × 10, Kern, Mauerring, Vorstadt) bzw. **2 Stadthöfe** (16 × 14, nur in M1 am Mauerring; K1 ist zu schmal) in die Bauzonen, solange dort nichts anderes steht. Dazu nimmt jeder frei gewordene Wohnhausplatz (Umzug, Abriss) ein Wohnhaus jeder Stufe auf. | 17.10.2026 |
| Seit wann leer | `Bauen.LeerSeit(konto)`: Spielminute des Bankrotts bzw. Erlöschens (gesetzt in `Stilllegen`), im Spielstand. **Alte Spielstände** ohne Zeitpunkt zählen **ab dem Laden** – so bekommt der Spieler sein Jahr Vorrang vor der KI. Gekauft oder abgerissen: null. | 17.10.2026 |
| Bauorte | `Bauen.Bauorte(konto, vorlage)` für die KI: alle gültigen eingerasteten Befehle, fest geordnet (Bauplätze nach Nummer, dann Rasterstellen von Nord nach Süd, West nach Ost, je Stelle Drehung 0–3). Etwa 25 ms je Abfrage auf der Greybox – die KI fragt nur selten (einmal am Tag, wenn sie bauen will). | 17.10.2026 |
| Gewinn je Betrieb | Neu: `Betriebsrechnung` je Betrieb (`Betrieb.Laufend`, `Betrieb.Vorjahr`, `Gewinn`, `Ganzjaehrig`) mit den Posten des Betriebsgewinns, soweit sie dem Betrieb gehören; im Spielstand. Das erste, angebrochene Jahr eines Betriebs (und das Jahr eines geladenen alten Spielstands) ist nicht `Ganzjaehrig`. Die Zählung „zwei Jahre in Folge Verlust“ führt die KI selbst. | 17.10.2026 |
| Bankrott | Unverändert: am 3. Jahresabschluss in Folge mit negativem Reinvermögen (`schulden.jahreBisBankrott`), in der Jahresabrechnung vor `KiRivale.Jahresende`. Betriebe zählen nicht zum Reinvermögen – ein Verkauf vor diesem Abschluss hebt es um den Erlös (Test). | 17.10.2026 |

### Abstimmen des Spielers im Rat (PR #111)

| Thema | Entscheidung | Datum |
|---|---|---|
| Ablauf | Der Spieler legt seine Stimme **vorab** fest, im Ratsfenster, und kann sie bis zur Ratssitzung am Jahresende jederzeit ändern; das Spiel läuft weiter. Die Prognosen (Wahl, Statut, Glaube) zählen seine Stimme mit. Entschieden vom Nutzer. | 17.10.2026 |
| Vorgabe | Ohne festgelegte Stimme wie bisher: bei Wahlen für den Amtsinhaber, bei Anträgen Enthaltung. Entschieden vom Nutzer. | 17.10.2026 |
| Umfang | Alle drei Abstimmungen der Sitzung: Ämterwahlen, Antrag auf ein Fron- und Sklavenstatut, Antrag auf einen Stadtglauben. Entschieden vom Nutzer. | 17.10.2026 |
| Kosten | Abstimmen kostet keine Aktionspunkte. Entschieden vom Nutzer. | 17.10.2026 |
| Befehle | `WahlStimme(string Amt, string? Kandidat)`, `StatutStimme(bool? Ja)`, `StadtglaubeStimme(bool? Ja)`. `null` nimmt die Stimme zurück: bei der Wahl gilt dann die Vorgabe, beim Antrag ist es die Enthaltung. Erlaubt für jedes Haus im Rat, das keine KI ist. | 17.10.2026 |
| Kandidaten | Ein Kandidat heißt wie in `Stadtrat.Kandidaten(amt)`: Hausname (Bewerber oder bisheriger Inhaber) oder der Name des Ratsherrn im Amt. Die Liste entspricht der Sitzung: Bewerber (aktiv, Stand erfüllt, nach Namen), dann der bisherige Inhaber; leer = keine Wahl. | 17.10.2026 |
| Stimmrecht | Abfragen nach dem `…Verboten`-Muster: `Stadtrat.WahlStimmeVerboten(konto, amt)`, `Stadtrat.StatutStimmeVerboten(konto)`, `Glaubenswesen.StimmeVerboten(konto)`. Abgewiesen wird, wer nicht im Rat sitzt, eine KI ist, bei dieser Wahl nicht wählt (nur höhere Ämter) oder selbst Kandidat ist; ohne Bewerber bzw. Antrag gibt es keine Stimme; der Antragsteller stimmt ohnehin dafür. | 17.10.2026 |
| Gültigkeit | Eine Stimme verfällt, sobald sie nicht mehr passt: Der Kandidat scheidet aus (Bankrott, Erbfall), der Ratsherr ist nicht mehr im Amt, der Antrag fällt weg oder ein neuer kommt, das Haus verliert sein Amt oder wird selbst Kandidat. Dann gilt die Vorgabe. Eine verfallene Stimme lebt nicht wieder auf. Nach der Sitzung sind alle Stimmen gelöscht. | 17.10.2026 |
| Prognosen | `WahlPrognose` und die beiden `AbstimmungsPrognose` zählen die Stimme. Beim Antrag gilt sie nur für den vorliegenden Antrag, gedachte Anträge zählen das Haus als Enthaltung. Die KI sieht die Stimme über die Prognosen mit. | 17.10.2026 |

### Völker, Berufe und Lizenz (PR #119)

| Thema | Entscheidung | Datum |
|---|---|---|
| Orks und Oger | **Nicht spielbar.** Sie bleiben Bestien vor den Toren, Banden und Arbeitskräfte. Entschieden vom Nutzer. | 18.10.2026 |
| Berufe | Es bleibt bei den **zehn Berufen**, einen Gaunerberuf gibt es nicht. Schmuggel und Schwarzarbeit unter dem Statut gibt es auch nicht. Entschieden vom Nutzer. | 18.10.2026 |
| Lizenz | Code und Skripte (auch die Zeichen- und Blender-Skripte unter `tools/`) **MIT**; Grafiken, Modelle, Icons, Bilder und Texte (`game/assets/` außer den Schriften, `docs/`) **CC BY 4.0** (`LICENSE-INHALTE.md`). Entschieden vom Nutzer. | 18.10.2026 |

### Grafikstil Holzschnitt (PR #130)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stil | Das ganze Spiel folgt dem **kolorierten Holzschnitt** des Spätmittelalters (Vorbild: Schedelsche Weltchronik, um 1480): schwarzer Umriss, Schraffur im Schatten, von Hand aufgetragene Farbe, die leicht neben den Linien sitzt. Entschieden vom Nutzer (zuerst für die Waren-Icons, PR #113, dann für das ganze Spiel). | 18.10.2026 |
| Geltung | Oberfläche (Tafeln, Fenster, Rahmen, Schmuck), Icons, Wappen, Bilder und alle weiteren Grafiken. Ersetzt die Pixel-Art-Entscheidung für die Waren-Icons im Archiv (14.10.2026). Neue Grafiken nur noch in diesem Stil; Bestehendes wird nach und nach umgestellt (offene Punkte bei Oberfläche und Darstellung). Eine Zeile dazu steht in `CLAUDE.md`, damit jeder Chat sie liest. | 18.10.2026 |
| Herkunft | Grafiken dürfen auch mit anderen Werkzeugen entstehen (etwa die Marktplatz-Icons mit Gemini, beim Nutzer in Arbeit). Es gilt die Lizenz CC BY 4.0 und die feste Regel: nichts aus dem Original-Spiel. | 18.10.2026 |

### Werkstatt wie in Gilde 1: Waren je Arbeiter, Einrichtung, Unfälle, Lohn (PR #152)

| Thema | Entscheidung | Datum |
|---|---|---|
| Waren je Arbeiter | Ein Betrieb stellt mehrere Waren zugleich her: Jeder Arbeiter wird einer Ware zugeteilt (bisher eine Ware je Betrieb für alle). Die KI-Häuser und der Spielstand werden mit umgestellt, alte Spielstände laden weiter (alle Arbeiter an der bisherigen Ware). | 19.10.2026 |
| Einrichtung | Gekaufte Einrichtung mit Wirkung, je Beruf eigene Stücke; beim Schmied: Werkzeug (weniger Zeit je Stück), Regale (mehr Lager je Ware als 40), Löschfass (Brand), Eisentür (Einbruch), Schürzen (Unfälle). | 19.10.2026 |
| Unfälle | Neues System: Selten fällt ein Arbeiter nach einem Unfall für den Rest des Tages aus (bis zum Jahreswechsel, im neuen Jahr arbeitet er wieder) und bekommt weiter Lohn. Die Gefahr hängt vom Beruf ab (Feuer in der Schmiede höher), Schürzen halbieren sie. | 19.10.2026 |
| Lohn und Laune | Drei Lohnstufen (knapp, üblich, gut) und eine einmalige Prämie. Die Laune steigt und fällt langsam und ändert das Tempo um bis zu ±20 %; bei schlechter Laune kündigt jemand. Lehrlinge lassen sich zum Gesellen befördern. | 19.10.2026 |
| Werte Lohn und Laune (Vorschlag Kern) | Lohnfaktor knapp 0,8 / üblich 1 / gut 1,25; Laune-Ziel 25 / 50 / 75. Laune läuft 3 Punkte je Arbeitsstunde auf ihr Ziel, Tempo linear ±20 % (Laune 0 → 0,8, 50 → 1, 100 → 1,2). Kündigung nach 6 Arbeitsstunden unter 20 (knapp allein reicht nicht, knapp und ein Unfall schon). Prämie 30 Gulden je Arbeiter, +20 aufs Laune-Ziel für 1 Spieltag. Der Lohn am Jahresende gilt mit der Lohnstufe im Mittel der Arbeitsminuten des Jahres. Lohnstufen, Laune, Unfälle und Einrichtung gelten nur in Werkstätten mit Ware. | 19.10.2026 |
| Werte Unfälle (Vorschlag Kern) | Gefahr je Arbeiter und Spieljahr: Schmied 5 %, Steinmetz und Tischler 4 %, Alchimist und Parfumeur 3 %, Wirt und Prediger 2 %. Ausfall bis zum Jahreswechsel (im Mittel ein halber Arbeitstag, Leistungsverlust ≈ Gefahr ÷ 2: Schmied ≈ 2,5 %), Lohn läuft weiter, danach 1 Spieltag Laune −15 für alle im Betrieb. Gewürfelt wird am Ende jeder Arbeitsstunde mit eigenem Zufall (fester Startwert). | 19.10.2026 |
| Beförderung | Lehrling → Geselle nach 2 Spieljahren Dienst, per Befehl; ab dann Gesellenlohn. Alte Spielstände: Dienstzeit ab Spielbeginn. | 19.10.2026 |
| Einrichtung des Schmieds (Vorschlag Kern) | Kennungen wie die Knoten `Einrichtung_<Kennung>` im Raummodell: `Werkzeug1` Gutes Werkzeug 300 Gulden, Zeit je Stück −10 %; `Werkzeug2` Meisterwerkzeug 800, ab Stufe 2, braucht Werkzeug1, nochmals −10 %; `Blasebalg` Großer Blasebalg 350, −10 % für Beschlag, Rapier, Werkzeug, Prunkschwert (nicht für Schmuck); `Regale` 200, +20 Lager je Ware (60); `Loeschfass` Löschfass 100, Feuer (Sabotage, Brand nach Schutzgeld) halb so oft; `Eisentuer` Eisentür 300, Diebstahl und Einbruch halb so oft; `Schuerzen` Lederschürzen 80, Unfälle halb so oft. Gekauft als Anschaffung; bleibt beim Ausbau; beim Verkauf zählt sie zum Wert (75 % Erlös) und bleibt im Haus für den nächsten Käufer. | 19.10.2026 |
| Arbeiter und Waren | Jeder Arbeiter hat eine feste Nummer im Betrieb. Wer keine Ware zugeteilt hat, arbeitet an der Ware des Betriebs; `ProduktWaehlen` setzt sie und teilt alle Arbeiter ihr zu (so arbeiten KI und alte Abläufe weiter). Jede Ware hat ihren eigenen Fortschritt, ein angefangenes Stück bleibt liegen, wenn niemand mehr daran arbeitet. `PersonalSetzen` entlässt die zuletzt eingestellten Arbeiter dieser Art. | 19.10.2026 |

### Einrichtung des Tischlers (PR #154)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stücke | Wie beim Schmied, mit Leimofen und Zwingen statt Blasebalg: Gutes Werkzeug (−10 % Zeit), Meisterwerkzeug (ab Stufe 2, braucht Gutes Werkzeug, −10 %), Leimofen und Zwingen (−10 % für Truhe, Schrank, Prunktruhe, Armbrust), Holzgestell (60 statt 40 je Ware), Löschfass (Feuer halb so oft), Eisentür (Diebstahl und Einbruch halb so oft), Lederschürzen (Unfälle halb so oft). Gleiche Preise wie beim Schmied. Nur Daten, keine neue Art Wirkung. | 19.10.2026 |
| Daten | `werkstatt.einrichtung.Tischler` mit den Kennungen `Werkzeug1`, `Werkzeug2`, `Leimzwingen` (350 Gulden), `Regale` (Name „Holzgestell“), `Loeschfass`, `Eisentuer`, `Schuerzen` – wie die Knoten `Einrichtung_<Kennung>` im Raummodell. Gilt für Tischlerei, Drechslerei und Sägewerk, nicht für den Holzschlag (keine Werkstatt). **Datenversion 37.** Keine Messung: Die KI-Häuser kaufen keine Einrichtung. | 19.10.2026 |

### Einrichtung des Steinmetzen (PR #157)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stücke | Wie beim Schmied und Tischler, mit Flaschenzug am Dreibein statt Blasebalg: Gutes Werkzeug (−10 % Zeit), Meisterwerkzeug (ab Stufe 2, braucht Gutes Werkzeug, −10 %), Flaschenzug (−10 % für Mühlstein, Säule, Taufbecken, Grabmal), Steingestell (60 statt 40 je Ware), Löschfass, Eisentür, Lederschürzen. Gleiche Preise. Nur Daten. | 20.10.2026 |
| Daten | `werkstatt.einrichtung.Steinmetz` mit den Kennungen `Werkzeug1`, `Werkzeug2`, `Flaschenzug` (350 Gulden), `Regale` (Name „Steingestell“), `Loeschfass`, `Eisentuer`, `Schuerzen` – wie die Knoten `Einrichtung_<Kennung>` im Raummodell. Gilt für Maurerei, Steinschneiderei und Steinmetzhütte, nicht für den Steinbruch (keine Werkstatt). **Datenversion 38.** Keine Messung: Die KI-Häuser kaufen keine Einrichtung. | 20.10.2026 |

### Einrichtung des Alchimisten (PR #160)

| Thema | Entscheidung | Datum |
|---|---|---|
| Stücke | Wie bei den anderen Werkstätten, mit kupfernem Destillierhelm statt Blasebalg: Gutes Werkzeug (Mörser und Spatel, −10 % Zeit), Meisterwerkzeug (ab Stufe 2, braucht Gutes Werkzeug, −10 %), Destillierhelm (−10 % für Kräuterschnaps, Rosenwasser, Gegengift, Theriak), Kräuterregale (60 statt 40 je Ware), Löschfass, Eisentür, Lederhandschuhe (Unfälle halb so oft). Gleiche Preise. Nur Daten. | 20.10.2026 |
| Daten | `werkstatt.einrichtung.Alchimist` mit den Kennungen `Werkzeug1`, `Werkzeug2`, `Destillierhelm` (Name „Kupferner Destillierhelm“, 350 Gulden), `Regale` (Name „Kräuterregale“), `Loeschfass`, `Eisentuer`, `Handschuhe` (Name „Lederhandschuhe“, 80 Gulden, neue Kennung statt `Schuerzen`) – wie die Knoten `Einrichtung_<Kennung>` im Raummodell. Gilt für Tinkturei, Kräuterladen und Alchimistenstube, nicht für den Kräutergarten (keine Werkstatt). **Datenversion 39.** Keine Messung: Die KI-Häuser kaufen keine Einrichtung. | 20.10.2026 |

## Messungen

Messungen des Kerns stehen in `docs/WIRTSCHAFTSMODELL.md` (nur der Kern schreibt dort).
