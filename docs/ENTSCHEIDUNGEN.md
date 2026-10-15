# Reichsgilde – Entscheidungsprotokoll

Begonnen am 13.10.2026 · Lebendes Dokument: Jede Grundsatzentscheidung wird hier mit Datum und Begründung festgehalten.
Seit 15.10.2026 im Repository (vorher im Claude-Projekt). Änderungen laufen wie Code über einen PR – so gehen gleichzeitige Einträge mehrerer Chats nicht verloren. Wie man einträgt: siehe `CLAUDE.md`.

---

## 1. Projektrahmen

| Thema | Entscheidung | Datum |
|---|---|---|
| Projektart | Hobby- und Lernprojekt. Eine spätere Veröffentlichung ist möglich, aber nicht geplant. | 13.10.2026 |
| Lizenz | **Code: MIT** (beim Anlegen des Repos gewählt). Für eigene Grafiken und Texte ist CC BY 4.0 vorgeschlagen, aber noch nicht bestätigt. | 13.10.2026 |
| Team / Zeit | Solo-Entwickler, ca. 10 Std./Woche | 13.10.2026 |
| Programmierkenntnisse | Gering. Claude schreibt den Code und erklärt jeden Baustein; der Lernfortschritt ist selbst ein Projektziel. | 13.10.2026 |
| Versionsverwaltung | GitHub, öffentliches Repository **github.com/B411Y/reichsgilde**, bedient über GitHub Desktop | 13.10.2026 |
| **Zielplattformen** | **Windows und Linux** (beide gleichwertig). Jede Änderung wird automatisch auf beiden Systemen gebaut und getestet (GitHub Actions). Exportvorlagen für beide Plattformen liegen im Projekt. | 13.10.2026 |

## 2. Technik

| Thema | Entscheidung | Begründung |
|---|---|---|
| Engine | **Godot 4.7.2 (.NET-Edition)** | Kostenlos, MIT-lizenziert, gute 2D/3D-Unterstützung, eingebautes Netzwerk |
| Sprache | **C# (.NET 8)** | Typensicher, gut lesbar, gute Werkzeuge (VS Code) |
| Architektur | Der Simulationskern (`src/Reichsgilde.Core`) ist strikt getrennt von der Darstellung (`game/`). Im Einzelspieler läuft der „Server“ im selben Prozess. | Multiplayer wird später ein Ausbau statt eines Neubaus |
| Projektstruktur | `game/` (Godot), `src/Reichsgilde.Core/` (Logik ohne Godot), `tests/` (Tests), `docs/`, gemeinsame Projektmappe `Reichsgilde.sln` im Hauptordner | Godot würde sonst alle .cs-Dateien im Projektordner doppelt kompilieren |
| Tests | Kleines Konsolenprogramm ohne Test-Framework (`dotnet run --project tests/Reichsgilde.Core.Tests`) | Keine externen Pakete nötig, leicht zu verstehen. Wechsel auf xUnit ist später möglich. |
| Simulationstakt | 1 Tick = 1 Spielminute. Ein Spieltag hat 1020 Ticks (05:00–22:00 Uhr, seit 15.10.2026; vorher 1440 ab 06:00). Der Taktgeber rechnet Echtzeit in Ticks um. | Die Simulation ist unabhängig von Tempo und Rundendauer. Das ist wichtig für den Multiplayer. |
| Namenskonvention | Fachbegriffe im Code auf Deutsch (`Spielzeit`, `Zunftglocke`), Umlaute im Code umschrieben, deutsche Kommentare | Passt zum Setting und ist für den Entwickler verständlich |
| Grafikansatz | **3D-Modelle mit orthografischer Schrägkamera** (40° Neigung). Blick **diagonal zu den Himmelsrichtungen**: Nordosten, Nordwesten, Südwesten, Südosten; Drehung nur in 90°-Schritten. (Kurz erprobt und verworfen: Blick genau nach N/O/S/W.) | 13.10.2026 |
| Nachtlicht | Mond wirft Schatten (kühl, etwas weicher), wenig blaues Umgebungslicht, weicher Übergang in der Dämmerung. Idee für später: Mondphasen je Jahr (Neumond = mehr Überfälle). | 13.10.2026 |
| Fenster und Oberfläche | Fenster **frei ziehbar ohne Mindestgröße**, Oberfläche in **fester Pixelgröße** (größeres Fenster = mehr Stadt, nicht größere Schrift). Tafeln, die nicht passen, **rollen** (senkrecht und waagrecht). **Vollbild per F11** und im Haupt- und Spielmenü, Wahl wird gespeichert. | 14.10.2026 |
| Innenansicht | ~~Diorama~~ → ersetzt durch die **Raumansicht** (Abschnitt 13); nur der Dom ist noch ein Diorama. Früher: **Diorama**: Gebäude wird aufgeschnitten – Dach und die zwei Wände zur Kamera ausgeblendet, Nachbarhäuser davor ebenfalls. Betreten per Doppelklick oder Knopf, Esc zurück. | 13.10.2026 |

## 3. Spielzeit & Pause

| Thema | Entscheidung | Datum |
|---|---|---|
| Pause | **Immer verfügbar, im Einzel- und Mehrspielermodus.** Ersetzt „Das Spiel pausiert nicht“ im Design-Dossier. Regeln für die Pause im Multiplayer (wer darf, wie lange, Pausen-Kontingent) folgen in Phase 6. Hinweis: Im Original konnten Clients im Netzwerkspiel nicht pausieren. | 13.10.2026 |
| Zeitmodell (vom Original übernommen) | **1 Runde = 1 Spieltag = 1 Spieljahr.** Die Jahreszeit wechselt von Jahr zu Jahr (Frühling 1400, Sommer 1401, Herbst 1402, Winter 1403 …). Der Tag läuft als Echtzeit-Uhr ab, am Rundenende folgt die Jahresabrechnung. | 13.10.2026 |
| Tagesphasen | Zunftglocke 05–10, Hoher Mittag 10–15, Vesper 15–20, Dunkle Nacht 20–22 (vorher 06–11 / 11–16 / 16–21 / 21–06) | 15.10.2026 |
| Spieltag (Wunsch des Nutzers, wie im Original) | **Gespielt wird von 05:00 bis 22:00 Uhr**, dann kommt die Jahresrechnung, das neue Jahr beginnt wieder um 05:00. 1020 statt 1440 Ticks. **15 Minuten Echtzeit je Jahr bleiben** (Spielstunde ≈ 53 s). **Arbeitszeit bleibt 8–20 Uhr.** Gauner ziehen um **20 Uhr** los. Spielstände mit 24-Stunden-Tag lassen sich nicht mehr laden (Speicherformat 2). | 15.10.2026 |
| Rundendauer in Echtzeit | **15 Minuten** (im Original bei Standard-Tempo gemessen). **Als Spieloption einstellbar** (1–120 Min.). Umgesetzt im `Taktgeber`. | 13.10.2026 |
| Flüssige Darstellung zwischen den Ticks | Die Simulation rechnet nur in ganzen Spielminuten. **Die Darstellung (Sonne, Schatten, Himmel, Karren) rechnet mit „Minute + angebrochener Bruchteil“** (`Taktgeber.AngebrocheneMinute`), damit sich nichts einmal pro Sekunde ruckartig bewegt. Neue bewegte Darstellungen nutzen denselben Wert. | 15.10.2026 |
| Tempo | Stufen 1× / 2× / 4× / 8×, Tasten 1–4, Pause mit der Leertaste. **Während der Entwicklung zusätzlich 16× / 32× (Tasten 5–6)** – im Code mit `TESTTEMPO` markiert, vor der Veröffentlichung entfernen. | 13.10.2026 |

## 3a. Spielregeln (Entscheidungen zum Mechanik-Katalog)

| Thema | Entscheidung | Datum |
|---|---|---|
| Aktionspunkte | **Standard: 4 AP pro Runde** (= Spieljahr), nicht verbrauchte AP werden übertragen, **höchstens 12** (14.10.). Die Anzahl ist **pro Partie einstellbar** (Hauptmenü 1–12). | 13.10.2026 |
| Aktionspunkte: Kosten | **1 AP:** Werben, Heiraten, Ratsherrn beschenken, Amt bewerben, Spitzel ansetzen, Bande anheuern. **2 AP:** Statut-Antrag, Anklage, Erpresserbrief, Talent üben. **Frei:** Wirtschaft (Betriebe, Personal, Karren, Wächter, Stand, Liegenschaften), Testament, Ausbildung, Erpressung beantworten. **Training:** +½ Stern für das Oberhaupt, je Talent einmal im Jahr, bis 6; **Kosten steigen mit dem Talent**: 2 AP unter 3 Sternen, 4 AP unter 5, 6 AP darüber. **KI mit denselben AP**, Prioritäten nach Charakter. | 14.10.2026 |
| Ämter | **Start mit der schlanken 7er-Pyramide des Dossiers** (Bürgermeister, Stadtrichter, Kämmerer, Stadthauptmann, Marktmeister, Gefängniswärter, Zöllner). Weitere Kammern kommen später dazu. Die Datenstruktur muss Kammern und Stufen von Anfang an kennen. | 13.10.2026 |
| Glaube | **Zusätzlicher Faktor für die Gunst**, neben dem Volk. | 13.10.2026 |
| Glaubensrichtungen | **Katholisch und Evangelisch** sind die offiziellen, anerkannten Konfessionen. Beide sind auf die Kulturvölker (Menschen, Elben, Zwerge, Halblinge) zugeschnitten, d. h. sie haben eigene Ausprägungen und Traditionen je Volk. **Orks und Oger** gehören keiner anerkannten Religion an. Sie glauben an etwas Höheres in der Natur, an das Göttliche in der Natur und an die Energie, die sie umgibt (Naturglaube). Aus Sicht der Kirchen gelten sie damit als Heiden. **Beide Konfessionen existieren von Spielbeginn an** (keine Reformation als Ereignis). | 13.10.2026 |
| Glaube: Herkunft | **Nach Volk, im Hauptmenü wählbar** („wie das Volk“, katholisch, evangelisch). Startglaube = höchstes Gewicht des Volkes: Menschen (60/40) und Elben (70/30) katholisch, Zwerge (30/70) und Halblinge (45/55) evangelisch; Kandidaten würfeln danach. Orks und Oger: Naturglaube. Ratsherren: 7 katholisch, 6 evangelisch. Kinder im Glauben des Oberhaupts. | 15.10.2026 |
| Glaube: Wirkung | **Rat + Messe.** Wahlpunkte: Glaubensbruder +5, andere Konfession −3, Heide −8. Messe: 55 % Katholiken, 45 % Evangelische, jede Gemeinde geht in ihre Kirche; ohne eigene Kirche in eine fremde, aber nur mit halber Spende. | 15.10.2026 |
| Glaube: Aktionen | **Alle vier.** Konvertieren (2 AP, 300 Gulden, ganzes Haus, −3 Ansehen bis Ende Folgejahr). Bekehrungspredigt (Prediger, 1 AP, 1× im Jahr: +4 Prozentpunkte × (1 + 0,1 × Rhetorik), +3 Gunst je Ratsherr des Glaubens; Anteile kehren jährlich um 10 % zurück, mindestens 10 %). Mischehe (Partner tritt über, −2 bei Ratsherren seines alten Glaubens). Stadtglaube (Ratsantrag, 2 AP, eigener Antrag neben dem Statut): Andersgläubige zahlen 150 Gulden Glaubenszins, ihre Messe und Bekehrungspredigt ist Ketzerei (Spur, Gemeinde des Stadtglaubens hat den Beweis). | 15.10.2026 |
| Glaube: Heiden und KI | Heiden können sich **taufen lassen** (Konvertieren); Prediger braucht eine Konfession. KI tritt unter fremdem Stadtglauben über, beantragt ihren Glauben im Rat, wenn er durchginge (sonst Freiheit), predigt nur mit AP, die sonst verfielen. | 15.10.2026 |
| Stände | Rangordnung aus dem Dossier ohne Unfreie: **Gemeiner (Start) → Bürger 10.000 → Patrizier 20.000 → Edler 50.000 → Freiherr 100.000 → Reichsgraf 200.000** Gulden. Aufstieg **aktiv im Rathaus**, eine Stufe auf einmal, gegen eine **Gebühr von etwa 10 % der Schwelle** (Vorschlag, in `wirtschaft.json` einstellbar). Der Stand bleibt bei Verlusten erhalten (aberkennen kann ihn später nur ein Gericht). Privilegien vorerst nur als Beschreibung. | 13.10.2026 |
| Schulden | Jahresabschluss im Minus → Mahnung des Kämmerers; **3 Jahresabschlüsse in Folge im Minus → Bankrott** (geändert von 2 auf 3). Bankrott des Spielers beendet die Partie, ein bankrotter Rivale scheidet aus (zählt für „Letzte Dynastie gewinnt“). | 13.10.2026 |
| Expansion | **Ab Stand Bürger** kann man Liegenschaften erwerben: Werkstätten **kaufen** (Gießerei an der Südgasse, 3.500 Gulden), Gruben **pachten** (am Silberberg, im Tannenwald; 400 Gulden Pacht im Jahr, wie bisher). Kaufpreis plus erstes Jahr Unterhalt/Pacht müssen gedeckt sein. Jede Liegenschaft gibt es nur einmal. KI-Rivalen expandieren nach Rechnung (Amortisation ≤ 6 Jahre, Reserve bleibt) bis höchstens 3 Betriebe. | 13.10.2026 |
| Spielstände | Speichern jederzeit über das Spielmenü (Esc), **automatisch nach jedem Jahresbericht** (eine Datei, wird überschrieben). Lesbare JSON-Dateien unter `user://spielstaende`; die Wirtschaftsdaten werden mitgespeichert. Nach dem Laden ist die Partie pausiert. | 13.10.2026 |
| Hauptmenü | Neue Partie (Hausname, Siegbedingung, Jahresdauer, Startstatut, Startvermögen, KI-Charakter vorsichtig/ausgewogen/wagemutig, Zufallsstartwert), Partie laden, Beenden. Mit Startoptionen geht es ohne Menü direkt in die Stadt (für Tests). | 13.10.2026 |
| Jahresbericht | Nach jeder Jahresabrechnung erscheint ein Bericht (Jahresrechnung, Fortschritt zum nächsten Stand, Rangliste); das Spiel **pausiert, bis er geschlossen wird**. Im Multiplayer später eine Partie-Einstellung. | 13.10.2026 |
| Stadtrat | Die 7er-Pyramide ist zu Beginn mit **Ratsherren der Stadt** besetzt (Menschen, Zwerge, Elben, Halblinge; Orks und Oger haben kein Bürgerrecht), jeder mit fester Haltung zum Statut A, B oder C. Sechs weitere Ratsherren rücken bei freien Sitzen nach. **Bewerbung ab Bürger**, immer genau eine Stufe über dem eigenen Amt; die oberen Ämter (Bürgermeister, Stadtrichter, Kämmerer) erst ab Patrizier. Gewählt wird **am Jahresende von den Inhabern der höheren Ämter** (beim Bürgermeister von allen übrigen). Jedes Amt bringt **Amtseinkommen** (150 bis 800 Gulden im Jahr). **Gunst durch Geschenke**, sie verblasst um 30 % im Jahr. Ratsmitglieder können **einmal im Jahr eine Änderung des Statuts beantragen**; Abstimmung im ganzen Rat, bei Gleichstand entscheidet der Bürgermeister. Die Ratskammer zeigt eine **Prognose** für Wahl und Abstimmung. Alle Zahlen in `wirtschaft.json`, Abschnitt `politik`. | 13.10.2026 |
| Rat und KI | Haus Eisenbart macht Politik nach denselben Regeln: Es bewirbt sich, beschenkt Ratsherren nur, wenn sein Budget (15 % des Vermögens über der Reserve) für eine Mehrheit reicht, und beantragt ein Statut nur, wenn es seinen erwarteten Jahresgewinn um mehr als 200 Gulden erhöht. **Bekannte Schwäche, bewusst offen:** Häuser im Rat wählen den bisherigen Inhaber, ein Rivale als Bürgermeister ist deshalb kaum zu stürzen. Gegenmittel (Bestechung, Erpressung, Gericht) kommen in späteren Phasen. | 13.10.2026 |
| Dynastie: Volk | Das **Volk des eigenen Hauses ist im Hauptmenü wählbar** (Mensch, Zwerg, Elb, Halbling), dazu Geschlecht und Vorname des Oberhaupts. **Haus Eisenbart ist ein Zwergenhaus.** Beide Oberhäupter beginnen ledig. | 14.10.2026 |
| Dynastie: Ehen zwischen Völkern | **Erlaubt.** Die Geburtenrate ist die niedrigere der beiden Völker, mal 0,5. Ratsherren vom Volk des Oberhaupts geben 5 Punkte weniger, die vom Volk des Ehepartners 3 Punkte mehr. **Kinder gehören immer dem Volk des Hauses an.** | 14.10.2026 |
| Dynastie: Erbfolge | **Gleichberechtigt:** Oberhaupt kann Patriarch oder Matriarchin sein, jedes volljährige Kind kann erben. Reihenfolge: **Testament** (in der Familientafel bestimmt) → ältestes volljähriges Kind → **Witwenrecht** → das Haus erlischt. Der Erbe behält Vermögen, Betriebe und Stand; **Ämter und Gunst beginnen neu.** | 14.10.2026 |
| Dynastie: Witwenrecht | **Neu gegenüber dem Dossier**, nach dem historischen Witwenrecht der Zünfte: Ohne volljähriges Kind führt der Ehepartner das Haus weiter. Grund: Ohne diese Regel erlischt ein Menschenhaus in etwa 20 % der Partien schon in den ersten 30 Jahren, meist weil das Oberhaupt stirbt, bevor ein Kind mündig ist. Mit Witwenrecht sind es etwa 1 %. Abschaltbar über `dynastie.witwenrecht`. | 14.10.2026 |
| Dynastie: Brautschau | Zehn ledige Kandidaten aller vier Völker und beider Geschlechter, aus Gemeinern, Bürgern und Patriziern. Werben nur **standesgemäß** (Kandidat höchstens im eigenen Stand). **Geschenke bringen höchstens 20 Zuneigung im Jahr**, Zuneigung verblasst um 30 %. Heirat ab Zuneigung + Ansehen ≥ 30 (+10 je Stand des Kandidaten). Die Hochzeit kostet je nach Volk 250–900 Gulden, **Zwerge sind am teuersten** (hohe Mitgift). Jedes Jahr heiraten 15 % der Kandidaten anderweitig. | 14.10.2026 |
| Dynastie: Leben und Tod | Sterberisiko nach **Gompertz** (steigt exponentiell mit dem Alter), dazu 0,6 % Grundrisiko im Jahr, für Kinder 2 %. Pest, Mord, Duell und Hinrichtung folgen mit späteren Säulen. Die Oberfläche zeigt nur eine grobe Verfassung, keine genaue Lebenserwartung. **Erlischt das Haus des Spielers, ist die Partie verloren** (Zuschauer-Modus später). | 14.10.2026 |
| Dynastie: Talente | Fünf Talente, **0–6 Sterne** in halben Schritten: Verhandeln, Handwerkskunst, Bei Nacht und Nebel, Kampf, Rhetorik. **Es zählt das Oberhaupt.** Je Stern: +1,5 % Markterlös; +3 % Leistung; +4 Prozentpunkte Gaunerei-Erfolg und −8 % Zeugen; +4 Prozentpunkte Stärke angeheuerter Wegelagerer und +3 Prozentpunkte Karrenschutz; +2 Ratspunkte, 1 Punkt Plädoyer vor Gericht (Differenz Ankläger − Beklagter, innerhalb der ±10), +5 % Wirkung der Brautgeschenke. Völker-Anlagen: Zwerge Handwerk 3 und Kampf 2½, Elben Rhetorik 3, Halblinge Nacht 3, Menschen ausgewogen. Neue Erwachsene: Anlage ±1; Kinder: ½ Mittel der Eltern + ½ Anlage ± ½. Die Brautschau zeigt die Stärken der Kandidaten. *(Ohne Rückfrage entschieden, während der Nutzer testete – im PR dokumentiert.)* | 14.10.2026 |
| Dynastie: Ausbildung | Ab der Hälfte der Kindheit bis zur Volljährigkeit, Kosten je Jahr: **Zunftlehre** (60; Handwerk, etwas Kampf), **Kontorlehre** (30; Verhandeln), **Klosterschule** (40; Rhetorik), **Universität** (200, erst ab Bürger; Rhetorik und Verhandeln), **Fechtmeister** (120; Kampf, etwas Rhetorik), **Gasse** (0; Nacht und Nebel, etwas Kampf). Insgesamt +4 Sterne im Hauptfach, für jedes Volk gleich (Elben lernen länger, aber langsamer). Die KI schickt das älteste Kind auf die Universität oder Klosterschule, die anderen in die Zunft, wagemutige Häuser das jüngste auf die Gasse, und setzt das begabteste volljährige Kind als Erben ein. | 14.10.2026 |
| Gesundheit: Krankheiten | **Familienmitglieder** erkranken (etwa 15 % je Jahr): Husten (Rhetorik −1), Ruhr (Verhandeln, Rhetorik −1; 4 % tödlich), Lungenentzündung (Handwerk, Kampf −1; 8 % tödlich), Knochenbruch (Nacht −1, Kampf −1½), Vergesslichkeit (ab 50 dreifach, 2 Jahre; Verhandeln, Handwerk −1), Angst im Dunkeln (Nacht −2), Haarausfall (2 Jahre) und Lachanfälle (je Ansehen −3). Eine Krankheit dauert bis zum Jahresende. | 14.10.2026 |
| Gesundheit: Pest | **Seltene Stadtseuche** alle 25–40 Jahre: 35 % Ansteckung (Zwerge ×0,6, Elben ×0,5), 40 % der Erkrankten sterben unbehandelt, −2 auf alle Talente, **Krankenstand 30 %** in allen Betrieben für das Jahr. Auch die Brautschau trifft es. Ratsherren bleiben (vorerst) verschont. | 14.10.2026 |
| Gesundheit: Heilung | **Medicus** (120 Gulden, Pest 300; keine AP): heilt gewöhnliche Krankheiten sofort, halbiert das Pestrisiko. **Kräuterschnaps** als Marktware (Grundpreis 25) in der Hausapotheke: heilt Husten, Angst im Dunkeln, Lachanfälle; Pestrisiko ×0,8. Beides zusammen: ×0,4. | 14.10.2026 |
| Gesundheit: Krötenschleim | Gaunerstück im Hinterzimmer (200 Gulden, 1 AP, 60 % + Nacht und Nebel, Wächter helfen nicht): Das Oberhaupt erkrankt an Ruhr, Lungenentzündung, Haarausfall, Lachanfällen oder Vergesslichkeit. Vergehen **Giftmischerei** mit Spur und Zeugen; Buße auf Grundlage der Medicuskosten. | 14.10.2026 |
| Berufe: Tischler | Zweiter Beruf **Tischler**: Holzschlag (Umland, Pacht 400, 3 Plätze) fördert **Holz** (Grundpreis 10); Tischlerei (Werkstatt, 2 Plätze) macht **Holzzapfen** (2 Holz, 40), **Schemel** (2 Holz + Holzzapfen, 95), **Truhe** (2 Holz + **Beschlag vom Schmied**, 140). Gewinn je Arbeitsminute wie beim Schmied. | 14.10.2026 |
| Berufe: Wahl und Erlernen | **Startberuf im Hauptmenü** (Schmied oder Tischler). Weitere Berufe im Kontor **erlernen**: 500 Gulden Lehrgeld + 4 AP. Erst dann darf man Werkstätten und Umlandbetriebe dieses Berufs erwerben. | 14.10.2026 |
| Berufe: KI | **Zweiter Rivale Haus Eichwald** (Elben, Tischler: Tischlerei und Holzschlag), Charakter wie Haus Eisenbart, im Hauptmenü abwählbar. Haus Eisenbart bleibt Schmied; die KI erweitert sich nur im eigenen Handwerk. | 14.10.2026 |
| Meistergrade: Aufstieg | **Erfahrung + Prüfung**: Jedes hergestellte oder geförderte Stück bringt die Gesellenstunden dafür als Erfahrung im Beruf. Reicht sie, legt man die Meisterprüfung ab: Grad 2–6 bei 60 / 250 / 650 / 1.300 / 2.300 Std., Gebühr 200 / 600 / 1.200 / 2.000 / 3.000 Gulden, 2 / 2 / 3 / 3 / 4 AP. | 14.10.2026 |
| Meistergrade: Besitz | Grad und Erfahrung gehören **dem Haus** (je Beruf) und überstehen den Erbfall. Grad 6 bringt **+1 AP je Runde**. | 14.10.2026 |
| Gebäudestufen: Ausbau | **Ausbau am Platz**: Grad 3 → Stufe 2 (Schmiede / Drechslerei, 3 Plätze, Unterhalt 120, 2.500 Gulden), Grad 5 → Stufe 3 (Goldschmiede / Sägewerk, 4 Plätze, Unterhalt 180, 5.000 Gulden). Ein halber Arbeitstag Umbau. Zu kaufen gibt es nur Stufe 1. | 14.10.2026 |
| Gebäudestufen: Waren | Jede Stufe bringt **zwei neue, wertvollere Waren** (Gewinn je Minute ≈ 1,1 auf Stufe 2, ≈ 1,25 auf Stufe 3 gegen ≈ 0,8 auf Stufe 1): Werkzeug, Silberkette; Goldkette, Prunkschwert; Gehstock, Armbrust; Schrank, Prunktruhe. **Gold** gibt es nur auf dem Markt. | 14.10.2026 |
| Berufe: Steinmetz | Dritter Beruf **Steinmetz**: Steinbruch (Umland, Pacht 400, 3 Plätze) bricht **Bruchstein** (12) und **Granit** (28), aufteilbar wie Eisen/Silber. Maurerei → Steinschneiderei → Steinmetzhütte: Schleifziegel, Grabstein, Mühlstein; Säule, Taufbecken; Steinfigur, Grabmal. | 14.10.2026 |
| Berufe: Verflechtung | **Mittel**: Die Steinfigur braucht **Werkzeug** vom Schmied (Stufe 2), das Prunkschwert jetzt **Schleifziegel** vom Steinmetz statt eines zweiten Beschlags. Übrige Rezepte unverändert. | 14.10.2026 |
| Berufe: KI | **Dritter Rivale Haus Kieselbach** (Halblinge, Steinmetze: Maurerei und Steinbruch). Im Hauptmenü 1–3 Rivalen, Standard 3. **Völkerboni je Beruf** kommen später. | 14.10.2026 |
| Berufe: Völkerboni | **Tabelle nach Dossier, ±5 % Leistung** der Betriebe des Berufs (und damit Erfahrung), nach dem Volk des Oberhaupts, auch für Betriebe ohne Ware. Mensch: Tischler, Gardist + (keine Mali). Zwerg: Schmied, Steinmetz, Geldleiher + / Dieb, Parfumeur −. Elb: Alchimist, Prediger, Parfumeur + / Steinmetz, Gardist −. Halbling: Wirt, Dieb + / Schmied, Gardist −. Die KI rechnet den Bonus bei der Expansion mit. | 15.10.2026 |
| Berufe: Alchimist | Vierter Beruf **Alchimist**: Kräutergarten (Umland, Pacht 400, 3 Plätze) zieht **Kräuter** (10) und fängt **Kröten** (25), aufteilbar. Tinkturei → Kräuterladen → Alchimistenstube: Farbe, Kräuterschnaps, Heilsalbe; Rosenwasser, Gegengift; Theriak, Zinnober. Keine Magie – Arzneien, Farben, Gifte. | 14.10.2026 |
| Alchimist: Kräuterschnaps | **Ware der Tinkturei** (1 Kräuter, 15 Min.). Der Markt bleibt Quelle der Hausapotheke; wer viel herstellt, drückt den Preis für alle. | 14.10.2026 |
| Alchimist: Krötenschleim | **Alchimisten-Vorteil:** Jeder kann ihn weiter anheuern; wer Alchimist ist, mischt selbst – **halbe Kosten** (100 statt 200) und **+15 %** Erfolgschance. | 14.10.2026 |
| Alchimist: Verflechtung | Der **Schrank** braucht Farbe (statt eines zweiten Holzzapfens), Gegengift und Zinnober brauchen **Silber**, Zinnober zusätzlich **Schleifziegel**, Theriak Gold. | 14.10.2026 |
| KI: weitere Berufe | Kein vierter Rivale. **KI-Häuser erlernen Berufe:** Ab dem Stand **Patrizier** darf ein KI-Haus einen Betrieb mehr führen und dafür ein **zweites Handwerk** erlernen (Lehrgeld zählt zu den Kosten, das eigene Handwerk hat Vorrang). | 14.10.2026 |
| UI: Marktplatz | **Eigenes Marktfenster** wie im Original: Kacheln mit Icon und Preis, gruppiert nach Rohstoffen, Zwischenwaren, Fertigwaren; Preis grün/rot über/unter Grundpreis, eigener Bestand als Zahl im Icon, Einzelheiten im Tooltip. Öffnet per Klick auf den Marktplatz oder Knopf im Kontor; das Kontor zeigt **keine Preisliste** mehr. Erst nur Anzeige – Handel von Hand folgt als eigener Schritt. | 14.10.2026 |
| Handel von Hand | **Per Karrenauftrag** (Marktfenster oder „Handel am Markt …“ in der Verwaltung): Ein Karren lädt Verkaufsware, fährt zum Markt, verkauft, kauft ein und bringt es ins Lager. Ist er unterwegs, wird der Auftrag **vorgemerkt** und startet bei seiner Rückkehr (vor dem Verwalter). **Einkauf: Marktpreis +10 % Händlerspanne**, Verhandeln senkt ihn; **jedes Hand-Stück hebt den Preis**, wie ein Verkauf ihn senkt (Verwalter-Einkäufe weiter ohne Preiswirkung). **Markt 8–20 Uhr** (Ankunft des Karrens). **Ware zurückhalten** je Betrieb: Der Verwalter verkauft sie nicht; Gekauftes, das der Betrieb nicht verarbeitet, automatisch. KI handelt nicht von Hand. | 15.10.2026 |
| UI: Waren-Icons | **Pixel-Art 32 × 32**, selbst gezeichnet per Skript (`tools/icons/waren_icons.py`), MIT-Lizenz. Nichts aus dem Original. | 14.10.2026 |
| Berufe: Wirt | Fünfter Beruf, erster **mit eigener Mechanik**: Bauernhof (Gerste, Roggen) → Schänke → Wirtshaus → Gasthaus; Weingeist, Körnermahlzeit, Dünnbier; Starkbier, Kräuterlikör; Festbier, Schädelbrand (mit Beschlag und Holzzapfen). | 14.10.2026 |
| Wirt: Gäste | **Ausschank im Haus** zum Grundpreis + 25 %: 120 Gäste je Jahr in der Stadt, ab 12 Uhr, abends doppelt; verteilt nach Stufe und Ansehen; 15 Stück je Ausschankware bleiben für die Gäste, der Rest geht zum Markt. | 14.10.2026 |
| Wirt: Extras | **Dunkle Ecke** (Gerüchte = Beweise, 20 % je Tat), **Würfeltisch** ab Stufe 2 (4 Gulden je Gast, −2 Ansehen), **Stammtisch** ab Stufe 3 (+4 Gunst je Ratsherr, ohne Bestechung). | 14.10.2026 |
| Wirt: Barde | Idee des Nutzers: **Barde für den Abend** – 120 Gulden, einmal je Jahr, keine AP: abends +50 % Gäste, +10 % Zeche, Stammtisch-Gunst ×2. | 14.10.2026 |
| Berufe: Prediger | Sechster Beruf mit eigener Mechanik: Klosterhof (Lumpen, Wachs) → Kirche → Dom → Kathedrale; Papier, Kerze, Lehrschrift; Hetzschrift, Psalter; Stundenbuch, Reliquiar. **Messe + Schriften**; Glaube und Konfessionen kommen **später**. | 15.10.2026 |
| Prediger: Messe | Täglich um 10 Uhr: 200 Gläubige je Jahr in der Stadt, verteilt nach Stufe, Ansehen und Rhetorik; je 2,5 Gulden Kollekte, steuerfrei. | 15.10.2026 |
| Prediger: Ablass und Beichte | **Beides.** Ablass in fremder Kirche: 250 Gulden + halber Tatwert, eine Tat verfällt mit allen Beweisen (zuerst die bekannte). Beichte: 25 % je fremde Tat; Beichtgeheimnis brechen (2 AP) = Beweise, aber selbst ein Vergehen. | 15.10.2026 |
| Berufe: Dieb | Siebter Beruf, **ohne Ware und ohne Umland**: Schmugglerloch → Unterschlupf → Diebesgilde (4 / 6 / 8 Diebe, Ausbau 2.500 / 5.000). Freier Platz: Schmugglerloch am Osttor (3.000, Bürger). | 15.10.2026 |
| Dieb: Einkommen | **Taschendiebe am Markt** während der Arbeitszeit: 0,8 Gulden je Diebesminute × 1 / 1,1 / 1,3 nach Stufe, steuerfrei. Fang 1,2 % je Diebesstunde × 1 / 0,8 / 0,6: ein Dieb in den Turm, **ein zufälliges anderes Haus** bekommt einen Beweis. | 15.10.2026 |
| Dieb: Aktionen | **Alle vier**, mit eigenen Leuten (kein Geld, nur AP; 1 / 2 / 3 Aufträge je Nacht): Einbruch (1 AP, Chance +10 %, +6 Stück je Stufe, Beute ins eigene Lager), Auskundschaften (1 AP, 40 % je Tat, keine Spur), Schutzgeld ab Unterschlupf (1 AP, sonst Brand), Entführung ab Diebesgilde (2 AP, 55 %, Lösegeld bis Ende des Folgejahres, sonst Geisel verkauft; **Kerker**). | 15.10.2026 |
| Dieb: Ruf und KI | **−3 Ansehen** für ein Haus mit Dieben. **Nur wagemutige KI** erlernt den Dieb (Zuschlag ×1,5 auf den erwarteten Gewinn); ihre Diebeszüge zählen zum Gaunerei-Budget (2 im Jahr). Jede KI zahlt Lösegeld und zahlt Schutzgeld nur, wenn es billiger ist als der Brand. | 15.10.2026 |
| Berufe: Gardist | Achter Beruf, **ohne Ware**, das ehrliche Gegenstück zum Dieb: Wachhaus → Stadtwache → Garnison (4 / 6 / 8 Wachleute, Ausbau 2.500 / 5.000) in der Stadt, **Wachturm** im Umland (3 Plätze, Pacht 400). Freie Plätze: Wachhaus an der Torstraße (3.500, Bürger), Wachturm an der Landstraße (Pacht). | 15.10.2026 |
| Gardist: Einkommen | **Sold der Stadt** je Arbeitsminute: 0,75 Gulden × 1 / 1,1 / 1,3 nach Stufe, versteuert wie ein Umsatz. Dazu Kopfgeld (¼ der Buße bei gewonnener Anklage, 50 je gefasstem Taschendieb) und Geleitgeld. | 15.10.2026 |
| Gardist: Aktionen | **Alle vier.** Patrouille (passiv, 2,5 % je Wachmann, höchstens 40 % weniger Erfolg nächtlicher Taten in der Stadt, Zeugen +½ davon), Wachturm (bis 30 % weniger Überfälle im Umland), Razzia (2 AP, 1 / 2 / 3 im Jahr, 50 % + 10 % je Stufe je Tat ein Beweis, Diebesgut beschlagnahmt), Geleitschutz (Eskorte „Stadtwache“ 20 Gulden, 80 % Schutz, nur solange eine Wache sie stellt). Alles als Faktoren ohne zusätzliche Würfe. | 15.10.2026 |
| Gardist: Ruf und KI | **+3 Ansehen** für ein Haus der Wache. **Nur vorsichtige und ausgewogene KI** erlernt den Gardisten (Zuschlag ×1,5), wagemutige nie; sie durchsucht zuerst Diebesnester. | 15.10.2026 |
| Berufe: Geldleiher | Neunter Beruf, **ohne Ware und ohne Umland**: Wechselstube → Geldleihe → Bankhaus (**2 / 3 / 4 Schreiber**, Ausbau 2.500 / 5.000). Freier Platz: Wechselstube am Markt (3.500, Bürger). | 15.10.2026 |
| Geldleiher: Einkommen | **Kredite + Wechsel.** Kasse: je Schreiber 2.000 Gulden Rahmen (× 1 / 1,25 / 1,5) zu 12 % an Stadtkunden, 20 % Ausfallchance je Jahr (10 % der Kasse). Wechsel: 0,7 % des Marktumsatzes, Bedarf der Stadt 6 Schreiber (mehr teilen sich die Gebühr), × 1 / 1,2 / 1,4. | 15.10.2026 |
| Geldleiher: Kredite an Häuser | **Beim Geldleiher**, für jedes Haus (1 AP, ein Kredit zur Zeit, 1–5 Jahre, höchstens 50 % des Reinvermögens; Geldleiher verleiht 3.000 / 6.000 / 12.000). Zins setzt der Geldleiher (4–50 %, Standard 12 %), stündlich auflaufend, Zins + Rate am Jahresende; fehlt Bargeld, **Pfändung** der Lagerware zu 75 %. **Reinvermögen** (Bargeld + Verliehenes − Schulden) zählt für Stand, Rang, Ansehen, Bußen, Bankrott und Sieg. | 15.10.2026 |
| Geldleiher: Extras und KI | **+AP** (Geldleihe +1, Bankhaus +2 je Jahr). **Wucher** über 20 %: Spur „Wucher“, der Schuldner hat den Beweis, −5 Ansehen, solange der Kredit läuft. Kein Schuldturm. KI lernt den Beruf wie jeden anderen, Zins nach Charakter (10 / 14 / 25 %), leiht selbst nicht. | 15.10.2026 |
| Berufe: Parfumeur | Zehnter Beruf, gewöhnliches Handwerk: **Duftgarten** (Lavendel, Jasmin; Pacht 400) und Siederei → Duftwerkstatt → Parfumerie (2 / 3 / 4). Waren: Politur; Blume der Zwietracht, Giftdolch; Moschusduft, Fechterduft; Heiligenduft, Schattendolch. Wildrose und Moschus als Rohstoff entfallen. Freie Plätze: Siederei an der Seifengasse (3.500, Bürger), Duftgarten am Lavendelhang (Pacht). | 15.10.2026 |
| Parfumeur: Politur | **Verflochten wie im Original:** Goldkette (Schmied) und Prunktruhe (Tischler) brauchen je 1 Politur, ihr Grundpreis steigt um 56. | 15.10.2026 |
| Parfumeur: Wirkungen | **Alle vier**, für jedes Haus (Lager oder Markt): Moschusduft (+10 Zuneigung über das Jahreslimit, +12 Ratsgunst ohne Bestechungsspur; je Person 1× im Jahr, 1 AP), Blume der Zwietracht (Ratsherr −15 Gunst für ein Haus, Vergehen Intrige, 1 AP), Heiligenduft (+4 Ansehen bis Ende Folgejahr, 1 AP), Giftdolch/Schattendolch (Hinterzimmer, 2 AP, 60 %/80 %: Vergiftung des Oberhaupts, nur **Gegengift** heilt, tödlich 20 %/45 % am Ende des Folgejahres; Giftanschlag → Kerker). | 15.10.2026 |
| Parfumeur: KI | **Normaler Beruf**, jede KI kann ihn lernen; Moschus beim Werben, Gegengift bei Vergiftung. **Gift nur wagemutig**: nur wenn sich keine Bande lohnt, 1× im Jahr, nur gegen ein Haus mit 1,5-fachem Reinvermögen. | 15.10.2026 |
| Prediger: Schriften | **Zwei Schriften mit Wirkung**, für jeden Beruf, aus dem Lager oder vom Markt: Lehrschrift (1 AP, +½ Stern **bis 3 Sterne**, einmal im Jahr), Hetzschrift (1 AP, −5 Ansehen beim Ziel bis Ende nächsten Jahres, Vergehen Verleumdung). | 15.10.2026 |
| Gericht: Bestechung | **Jedes Geschenk an einen Ratsherrn ist Bestechung** und hinterlässt eine Spur, für Spieler und KI. Es ist das erste Vergehen; Diebstahl, Sabotage, Statutverstöße usw. folgen mit den Gaunern. Spuren verjähren nach 5 Jahren. | 14.10.2026 |
| Gericht: Beweise | Beweise sammelt ein **Spitzel** (300 Gulden je Haus und Jahr). Er deckt am Jahresende jede unbekannte Spur mit 50 % Chance auf. Anklage nur mit Beweisen, Gebühr 100 Gulden. | 14.10.2026 |
| Gericht: Urteil | Drei Richter: **Stadtrichter** (Vorsitz) und **zwei ausgeloste Schöffen** aus den Ratsherren ohne Amt. Ist der Stadtrichter Partei, führt der Bürgermeister den Vorsitz. **Keine Immunität.** Überzeugung = 10 je Beweis + ½ × Gunstdifferenz (höchstens ±10), schuldig ab 15, Mehrheit entscheidet. Ab 3 Beweisen hilft keine Bestechung mehr. **Urteil am Gerichtstag** (Jahresende, vor Amtseinkommen und Ratssitzung). Das Plädoyer mit Zeitlimit kommt mit Talenten und Multiplayer. | 14.10.2026 |
| Gericht: Strafen | Schuldig: **Buße** = 3 × nachgewiesenes Bestechungsgeld (mindestens 200), **Amtsverlust**, **3 Jahre Pranger** (−10 Ansehen); die bestochenen Ratsherren rücken ab (Gunst weg). Freispruch: Der Ankläger zahlt **200 Gulden Entschädigung**. Kerker und Hinrichtung folgen mit schweren Verbrechen. | 14.10.2026 |
| Gauner: Hinterzimmer | Banden werden angeheuert, nicht als eigener Beruf geführt (der Gaunerberuf kommt später). Bestellt wird jederzeit, ausgeführt **bei Nacht und Nebel um 21 Uhr**, bezahlt im Voraus. Gaunerstücke: **Diebstahl** (150, 70 %, bis 12 Stück, Hehler zahlt 50 %), **Sabotage** (250, 60 %, 6 Arbeitsstunden Stillstand + 150 Reparatur), **Überfall** (300, Hinterhalt auf die nächste beladene Umlandfahrt, Stärke 0,8 × (1 − Eskortenschutz)). | 14.10.2026 |
| Gericht: Bußdeckel | Eine Buße beträgt **höchstens die Hälfte des Vermögens** des Verurteilten (mindestens 200). Grund: Erpressungsspirale unter wagemutigen KI-Häusern (Bußen über 100.000, Ø −4,3 Mio. bei Eichwald). | 15.10.2026 |
| Gauner: Spuren und Zeugen | Jedes Gaunerstück ist ein Vergehen mit Spur (Diebstahl, Sabotage, Raub). Mit **25 % Chance** (Wächter: 60 %, misslungen: doppelt) gibt es einen **Zeugen**: Das Opfer hat sofort einen Beweis, und die Stadt erfährt, wer es war. Buße = 3 × Schaden. | 14.10.2026 |
| Gauner: Wächter | Je Betrieb anstellbar, **150 Gulden im Jahr**. Er halbiert die Erfolgschance von Diebstahl und Sabotage und erhöht die Zeugenchance. Gegen Überfälle hilft weiter die Eskorte. | 14.10.2026 |
| Gauner: Erpressung | Ab **3 Beweisen** kann man Schweigegeld verlangen. Der Brief ist selbst ein Beweis für das Opfer. Zahlt das Opfer, verbrennen die Beweise des Erpressers. Briefe gelten bis zum Ende des Folgejahres, danach heißt Schweigen Nein. | 14.10.2026 |
| Gauner: Kerker | **Überfall und Erpressung** bringen vor Gericht zusätzlich **ein Jahr Kerker**. Im Kerker sind Stand, Rat, Brautschau, Gericht und Hinterzimmer gesperrt, die Betriebe laufen weiter (Verwalter). | 14.10.2026 |
| Gauner: KI | Nach Charakter: **vorsichtig nie**; **ausgewogen** höchstens ein Diebstahl oder eine Sabotage im Jahr, nur gegen Häuser vor ihr und nie unter Beobachtung; **wagemutig** zwei Gaunerstücke im Jahr gegen jeden, auch Überfälle und Erpressung, auch unter Beobachtung. Jede KI stellt nach einem Angriff Wächter ein und zahlt Schweigegeld, wenn es billiger ist als die drohende Strafe. | 14.10.2026 |
| Zutatenpreise | Die Simulation kauft Zutaten zum **Marktpreis**, nicht zum Grundpreis wie der Rechner. Ohne Spieler-Mine sind Rohstoffe deshalb etwa 5 % teurer, eine Mine senkt die Preise für alle. | 13.10.2026 |
| Logistik | Karren mit frei belegbaren Ladeflächen, Verwalter fährt automatisch, Eskorten kosten nur bei beladener Fahrt, Überfälle nur auf beladenen Umland-Fahrten (8 %, nachts doppelt). Werte siehe Wirtschaftsmodell, Abschnitt 9. | 13.10.2026 |
| Fairness | Gleichzeitige Marktbesuche werden in zufälliger Reihenfolge abgewickelt, damit kein Spieler allein durch die Reihenfolge der Betriebe bevorzugt wird. | 13.10.2026 |
| Wirtschaftswerte | **Entwurf 1 des Wirtschaftsmodells ist freigegeben** (siehe `docs/WIRTSCHAFTSMODELL.md`). Die Werte stehen im Repository unter `game/data/wirtschaft.json`. | 13.10.2026 |
| Überfälle auf Transporte | Überfälle verüben nicht nur Orks, sondern vor allem **Banditen und Räuber aller Völker**: Menschen, Zwerge, Elben, Halblinge, Orks und Oger, auch in **gemischten Banden**. Wilde Ork-Rotten und Oger bleiben als zusätzliche Bedrohung aus der Wildnis bestehen. | 13.10.2026 |
| Siegbedingung | **Wählt der Spieler beim Start der Partie.** Vorgesehene Modi: Endjahr (Standard 1600 wie im Original), Zielvorgabe (z. B. Titel oder Vermögen), letzte Dynastie gewinnt, endlos. | 13.10.2026 |

## 4. Grafik: 2D-Sprites vs. 3D-Modelle (Analyse)

**Empfehlung für einen Solo-Entwickler: 3D-Modelle (Low-Poly) mit fester Schrägkamera.**

| Kriterium | 2D-Sprites (Pixel-Art / vorgerendert) | 3D-Modelle mit Iso-Kamera |
|---|---|---|
| Figuren in 8 Blickrichtungen | Jede Animation × 8 Richtungen einzeln zeichnen | Einmal modellieren und animieren, alle Richtungen automatisch |
| Tag-Nacht, Fackeln, Schatten | Muss vorgezeichnet oder aufwendig per Shader gelöst werden | Kommt durch die Engine-Beleuchtung fast ohne Zusatzaufwand |
| Diorama-Innenansicht (Schnitt) | Separate Zeichnung je Gebäude | Dach und Vorderwand ausblenden, gleiches Modell |
| Freie Assets (CC0) | Vorhanden, Stil schwer einheitlich zu halten | Viel Auswahl an Low-Poly-Mittelalter-Paketen, gut kombinierbar |
| Lernkurve | Zeichenprogramm (z. B. Aseprite) | Blender (kostenlos), steiler Einstieg |
| Look | Charmant, „klassisch 2,5D“ | Das Original „Die Gilde“ ist ebenfalls 3D |

**Vorgehen unabhängig von der Wahl:** Bis Phase 4 arbeiten wir mit Platzhaltern („Greybox“: Klötze und farbige Kapseln). So blockiert die Grafik nie die Programmierung.

## 5. Referenzspiel „The Guild Gold“ – Nutzungsregeln

- Das Original dient **nur als Vorlage für Mechaniken** (Handbücher, lesbare .ini-Daten). Die Auswertung steht im **Mechanik-Katalog**, der nur im privaten Claude-Projekt liegt.
- **Keine Übernahme von Grafiken, Sounds, Musik, Texten oder Modellen** aus den .bin-Archiven (Urheberrecht; das Projekt ist Open Source).
- Der Mechanik-Katalog bleibt im Projekt und kommt nicht ins öffentliche Repository.

## 6. Phasenplan

| Phase | Inhalt | Ergebnis | Status |
|---|---|---|---|
| 0 – Fundament | Entscheidungen, Entwicklungsumgebung, GitHub-Repo, Mechanik-Katalog | Dieses Dokument, Katalog, Godot-Grundgerüst mit Zeitsystem | **fertig** ✔ |
| 1 – Wirtschaftsmodell | Warenketten, Preise, Löhne je Statut A/B/C | Balancing-Rechner + Modell-Dokument | **fertig** ✔ (Werte freigegeben) |
| 2 – Simulationskern | Tick, Markt, Betriebe, Bürger-KI, ohne Grafik, mit automatischen Tests | Testbare Simulation | **weitgehend fertig** (offen: Bürger-KI) |
| 3 – 2,5D-Prototyp | Isometrische Stadt, Gebäude anklicken, Diorama-Innenansicht (Greybox) | Klickbarer Prototyp | **in Arbeit** |
| 4 – Vertical Slice | 1 Stadt, 1 Beruf (Schmied, Gießerei), 1 KI-Rivale, 1 Jahreszyklus | Erstes spielbares Spiel | **weitgehend fertig** |
| 5+ – Säulen | Dynastie → Stände → Rat → Gericht → Gauner | Spieltiefe | **in Arbeit** |
| 6 – Multiplayer | 8 Spieler, Timer, Pausenregeln, Zuschauer-Modus | Netzwerkpartie | |

Was erledigt und was offen ist, steht in den beiden Listen darunter – **eine Zeile je PR bzw. Punkt**. So ändern zwei Chats nie dieselbe Zeile, und ihre PRs lassen sich ohne Konflikt mergen.

### 6a. Erledigt (je gemergtem PR eine Zeile, neue Zeilen unten anhängen)

Format: `- PR #Nummer (Phase) – Inhalt`

- PR #1 (0) – Grundgerüst, Zeitsystem, Partie-Einstellungen, Windows + Linux
- PR #3 (2) – Wirtschaft im Simulationskern: Markt, Gießerei, Mine, Fronstatut
- PR #4 (2) – Lager, Karren, Eskorten, Banditen
- PR #5 (3) – Greybox-Stadt mit Schrägkamera, fahrenden Karren, Tag/Nacht, Gebäudeauswahl
- PR #6 (3) – Diagonale Kamera, Diorama-Innenansicht der Gießerei
- PR #7 (2) – KI-Rivale mit eigenen Entscheidungen
- PR #8 (4) – Befehle und Bedienfeld für eigene Betriebe
- PR #9 (4) – Stände, Rangliste, Bankrott, Jahresbericht
- PR #11 (4) – Expansion: Werkstatt kaufen, Grube pachten
- PR #12 (4) – Speichern und Laden
- PR #13 (4) – Hauptmenü, Spielmenü, automatisches Speichern
- PR #14 (4) – Fix Jahresbericht
- PR #15 (5) – Stadtrat mit Ämtern, Wahlen, Gunst, Statut-Anträgen
- PR #16 (5) – Dynastie Teil 1: Völker, Brautschau, Kinder, Erbfolge
- PR #17 (5) – Fix Familientafel
- PR #18 (5) – Gericht Teil 1: Bestechung, Spitzel, Anklage, Urteil
- PR #19 (5) – Gauner Teil 1: Diebstahl, Sabotage, Überfall, Erpressung, Wächter, Kerker
- PR #20 (5) – Fix Zahlenfelder
- PR #22 (5) – Dynastie Teil 2: Talente und Ausbildung
- PR #23 (5) – Fenster frei ziehbar, Scrollbalken, Vollbild
- PR #25 (5) – Aktionspunkte mit Training
- PR #26 (5) – Gestaffelte Übungskosten
- PR #27 (5) – Gesundheit: Krankheiten, Pest, Medicus, Kräuterschnaps, Krötenschleim
- PR #28 (5) – Zweiter Beruf Tischler, Berufe erlernen, Haus Eichwald
- PR #29 (5) – Meistergrade und Gebäudestufen
- PR #30 (5) – Dritter Beruf Steinmetz, Haus Kieselbach
- PR #31 (5) – Vierter Beruf Alchimist, KI erlernt Berufe
- PR #32 (5) – Marktfenster mit Waren-Icons
- PR #33 (5) – Wirt mit Gästen, Barde, Würfeltisch, Stammtisch, dunkler Ecke
- PR #34 (5) – Prediger mit Messe, Ablass, Beichte, Lehr- und Hetzschrift
- PR #35 (5) – Dieb mit Taschendieben, Einbruch, Auskundschaften, Schutzgeld, Entführung
- PR #36 (5) – Fix Erpressungsspirale: Buße höchstens das halbe Vermögen
- PR #37 (5) – Gardist mit Sold, Patrouille, Wachturm, Razzia, Kopfgeld, Geleitschutz
- PR #38 (5) – Geldleiher mit Kasse, Wechsel, Krediten, Pfändung, Wucher, Reinvermögen
- PR #39 (5) – Parfumeur mit Politur, Moschus, Zwietracht, Heiligenduft, Giftdolch, Gegengift
- PR #40 (5) – Spieltag 05–22 Uhr wie im Original
- PR #41 (5) – Glaube und Konfessionen: Rat, Messe, Konvertieren, Bekehrung, Mischehe, Stadtglaube
- PR #42 (3) – Innenansichten aller Werkstätten mit Zuständen aus der Simulation, Fix Ausbau ohne Ware
- PR #43 (5) – Völkerboni je Beruf
- PR #44 (–) – Zusammenarbeit: CLAUDE.md, Entwurfs-PRs, Protokolle im Repo
- PR #45 (5) – Handel von Hand per Karrenauftrag
- PR #47 (–) – Phasenplan als Liste, Tests automatisch finden, Regeln für Messungen
- PR #48 (5) – Wappen für jedes Haus (Wahl im Hauptmenü, Anzeige in den Tafeln), Hausname ohne „Familie“
- PR #49 (3) – Innenräume für Rathaus (Ratssaal, Gerichtssaal, Amtsstube, Kerker) und Dom nach dem Stadtglauben
- PR #50 (–) – Flüssige Bewegung von Sonne, Schatten und Karren zwischen den Spielminuten
- PR #51 (3) – Namen in der Stadt nur bei Hover, Auswahl oder Alt; Schild an freien Plätzen; Schriften Germania One und Alegreya
- PR #52 (3) – Raumansicht: Werkstätten (Werkraum, Lager, Kontor, Wohnstube) und Rathaus (Ratssaal, Gericht, Amtsstube, Kerker) betreten, feste Ansicht je Raum
- PR #53 (–) – Vierter Chat „Spiel-KI“: Bereich Ki/ und Buerger/, Andockstellen im Kern
- PR #54 (2) – Bürger-KI Schritt 1: Einwohner mit Tagesablauf nach Bedürfnissen, noch ohne Wirkung auf die Wirtschaft

### 6b. Offen (eine Zeile je Punkt; erledigt → Zeile löschen und unter 6a eintragen)

- (3) Raumansicht für den Dom und Feinschliff der Werkstatträume je Beruf (zweiter Teil von PR #52)
- (2) Bürger-KI: Kopplung an Schänke → Kirche → Markt → Taschendiebe (Einwohner und Tagesablauf: PR #54)
- (5) Orks und Oger spielbar machen
- (5) Bauplätze und Sanierung (siehe Abschnitt 7, Punkt 8)
- (5) Gaunerberuf, Schmuggel und Schwarzarbeit unter dem Statut
- (6) Multiplayer, Zuschauer-Modus

## 7. Offene Punkte

1. ~~Rundendauer im Original messen~~ → 15 Min. ✔
2. Lizenz für Grafiken und Texte (CC BY 4.0?) bestätigen.
3. ~~Grafikansatz final entscheiden~~ → 3D-Modelle, orthografische Schrägkamera, diagonal (NO/NW/SW/SO) ✔
4. Pausenregeln im Multiplayer (Phase 6).
5. ~~Design-Fragen aus dem Mechanik-Katalog~~ → siehe Abschnitt 3a ✔
6. ~~Glaubensrichtungen festlegen~~ → Katholisch, Evangelisch, Naturglaube (Orks/Oger) ✔
7. ~~Evangelisch ab 1400 oder durch ein Reformations-Ereignis?~~ → beide Konfessionen von Anfang an ✔
8. **Idee für später (Nutzer, 14.10.2026):** **Leere Bauplätze** kaufen und darauf das passende Gebäude bauen; außerdem **leerstehende Gebäude** (z. B. aufgegebene Betriebe) günstig aufkaufen und **sanieren** – je nach Zustand günstiger oder teurer als ein Neubau.
9. ~~**Fehler (gefunden 15.10.2026): Erpressungsspirale.**~~ → behoben (PR #36): Buße höchstens das halbe Vermögen des Verurteilten ✔ Wagemutige KI-Häuser erpressen sich gegenseitig mit wachsenden Beträgen (60 % der drohenden Strafe, die frühere Erpressungsbeträge mitzählt); die Buße (3 × alle verlangten Beträge) treibt Häuser in den Bankrott (z. B. 120.900 Gulden, im Extrem Ø −4,3 Mio. bei Eichwald). Besteht seit dem Hinterzimmer, eigener Fix nötig.
10. ~~**Fehler (gefunden 15.10.2026): Ausbau ohne Ware.**~~ → behoben (PR #42) ✔ Beim Ausbau von Schmugglerloch, Wachhaus und Wechselstube suchte `Betrieb.Ausbauen` ein Produkt in einer leeren Liste. Der Ausbau geschah, der Befehl meldete aber „Sequence contains no elements“. Jetzt wird das Produkt nur bei Werkstätten mit Ware angepasst; Test „Ausbau ohne Ware“.

## 8. Innenansichten der Werkstätten (PR #42)

| Thema | Entscheidung | Datum |
|---|---|---|
| Umfang | **Alle zehn Werkstätten** haben einen Innenraum (Diorama), gebaut in einem PR. Je Werkstatt 3–4 Einrichtungsstücke aus Grundformen (Greybox), Lager als Kistenstapel an der Nordwand, Arbeiter in der Tracht ihres Berufs. | 15.10.2026 |
| Gebäudestufen | **Je Stufe ein Zusatzstück** und ein Arbeitsplatz mehr (z. B. Drechselbank / Sägegatter, Würfeltisch / Stammtisch, zweites Pult / Tresor). Kein eigener Raum je Stufe. | 15.10.2026 |
| Zustände | Nur **lesende Abfragen** des Kerns: Gäste der letzten Stunde, Abgewiesene und Barde (Schänke), Gläubige zur Messe und Kollekte (Kirche), Kasse, Schuldscheine und Kunden (Wechselstube), Diebesgut, Aufträge, Beute und Geisel (Diebesnest), gefasste Taschendiebe in der Zelle (Wache), Werkstück bzw. Sud in der Farbe der Ware (Handwerke). | 15.10.2026 |
| Anwesenheit | Arbeiter von **8 bis 20 Uhr**. **Diebesnest:** tagsüber bleibt nur der Hehler, die anderen stehlen am Markt; abends sind alle da. **Schänke:** Der Wirt schenkt nach 20 Uhr weiter aus, solange Gäste kommen (bis 22 Uhr). | 15.10.2026 |

## 9. Zusammenarbeit mehrerer Chats

| Thema | Entscheidung | Datum |
|---|---|---|
| Regeln | **`CLAUDE.md` im Repository** – jeder Chat liest sie zu Beginn automatisch: Start-Pflicht (neue Commits auf `main`, offene PRs), Arbeitsweise, Bereiche, feste Regeln, Bauen und Klicktest. | 15.10.2026 |
| Laufende Arbeit | **Kein eigenes Änderungsregister.** Erledigtes zeigt Git (`main`, PRs). Laufendes zeigt ein **Entwurfs-PR**, den jeder Chat gleich nach dem ersten Commit öffnet (Aufgabe, Bereich, angefasste Dateien; Vorlage `.github/pull_request_template.md`). | 15.10.2026 |
| Protokolle | **Entscheidungsprotokoll und Wirtschaftsmodell liegen öffentlich im Repository** (`docs/ENTSCHEIDUNGEN.md`, `docs/WIRTSCHAFTSMODELL.md`) und werden im selben PR wie die Änderung gepflegt. Grund: Im Claude-Projekt schrieb jeder Chat die ganze Datei neu, gleichzeitige Einträge hätten sich still überschrieben; im Repo zeigt Git einen Konflikt. Mechanik-Katalog und Design-Dossier bleiben privat im Claude-Projekt. | 15.10.2026 |
| Bereiche | Zwei Chats: **Hauptchat (Kern)** und ein Chat für Darstellung/Aufgaben je Prompt. Später voraussichtlich mehr (Darstellung, Sound, Spiel-KI …), je mit eigenem Bereich; die Tabelle in `CLAUDE.md` wird dann ergänzt. | 15.10.2026 |
| Phasenplan | **Als Liste statt Tabellenzelle** (Abschnitt 6a/6b): je PR eine Zeile unten anhängen, offene Punkte je eine Zeile. Vorher wuchs eine einzige Tabellenzelle, die jeder PR änderte – sicherer Merge-Konflikt. | 15.10.2026 |
| Tests | **Testklassen werden automatisch gefunden** (jede statische Klasse mit `Alle()`), keine Liste mehr in `Program.cs`. | 15.10.2026 |
| Messungen | **Nur der Kern-Chat misst.** Jede Messung nennt den `main`-Commit, gegen den gemessen wurde – so lassen sich Messungen aus verschiedenen PRs vergleichen. | 15.10.2026 |

## 10. Wappen, Hausnamen, Berufe

| Thema | Entscheidung | Datum |
|---|---|---|
| Berufe | **Zehn Berufe reichen vorerst** (Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger, Dieb, Gardist, Geldleiher, Parfumeur). Kein Händler als eigener Beruf; Handel läuft über den Handel von Hand (PR #45). | 15.10.2026 |
| Hausname | Eingabe **nur der Name** („Hammerschmid“); das Spiel nennt jedes Haus einheitlich **„Haus …“**, wie die KI-Häuser. „Familie …“ entfällt. | 15.10.2026 |
| Wappen: Wahl | Unter „Neue Partie“ ganz oben: **zwölf Vorlagen + Farben für Grund und Figur + Würfeln**, mit Vorschau und Wappenbeschreibung. Kein voller Baukasten (Teilung und Figur kommen über Vorlagen oder Würfeln). | 15.10.2026 |
| Wappen: Aufbau | Teilung (10: ohne, geteilt, gespalten, schräg, geviert, Schildhaupt, Balken, Pfahl, Sparren, Bord) × zwei Tinkturen (Gold, Silber, Rot, Blau, Grün, Schwarz, Purpur) × Figur (16). Die Figur steht in Farbe 2 und wechselt über Farbe 2 in Farbe 1 („verwechselte Farben“). Selbst gezeichnete Vektorformen, nichts aus dem Original. | 15.10.2026 |
| Wappen: KI-Häuser | Feste, nicht wählbare Wappen: Eisenbart – in Schwarz ein goldener Amboss; Eichwald – in Grün mit silbernem Bord ein silberner Baum; Kieselbach – in Blau unter silbernem Schildhaupt ein silberner Wellenbalken. | 15.10.2026 |
| Wappen: Anzeige | Kontor (eigenes Haus), Rangliste im Jahresbericht, Amtsinhaber im Rat, Beweisbuch im Gericht, Gebäudefenster (Besitzer). Fahnen an Gebäuden in der Stadt wären später Sache der Darstellung. | 15.10.2026 |

## 11. Innenräume für Rathaus und Dom (PR #49)

| Thema | Entscheidung | Datum |
|---|---|---|
| Rathaus | **Drei Räume nebeneinander** im Diorama: Ratssaal, Gerichtssaal, Amtsstube; dazu der **Kerker als Gittergrube** im Gerichtssaal. | 15.10.2026 |
| Belebung | **Tagsüber Amtsgeschäfte** (8–20 Uhr): Ratsherren an ihren Sitzen, Richter und Parteien bei einem Verfahren, Stadtschreiber in der Amtsstube. Nachts ist das Rathaus leer. | 15.10.2026 |
| Bedienung | **Raum anklicken öffnet seine Tafel** (Ratssaal → Stadtrat, Gerichtssaal → Stadtgericht). | 15.10.2026 |
| Dom | **Hauptkirche des Stadtglaubens:** katholisch prunkvoll, evangelisch schlicht, bei Glaubensfreiheit Simultankirche. Gläubige ohne Kirche eines Hauses besuchen hier die Messe. | 15.10.2026 |

## 12. Schriften und Namen in der Stadt

| Thema | Entscheidung | Datum |
|---|---|---|
| Namen in der Stadt | **Nur unter der Maus, beim ausgewählten Gebäude und mit gedrückter Alt-Taste alle.** Vorher standen alle Namen dauerhaft über der Stadt und verdeckten sich gegenseitig. | 15.10.2026 |
| Freie Plätze | **Schild am Pfosten ohne Text**: ocker = zu verkaufen, grün = zu verpachten, leicht leuchtend für die Nacht. Name und „zu verkaufen/verpachten“ beim Hover. | 15.10.2026 |
| Schrift Überschriften | **Germania One** (gebrochen, kantig; SIL OFL) für Überschriften ab 20 px und die Namen in der Stadt. Aus 10 Mustern gewählt (u. a. Grenze Gotisch, Pirata One, UnifrakturMaguntia, Almendra, Cinzel). | 15.10.2026 |
| Schrift Text | **Alegreya** (Buchschrift; SIL OFL) für allen übrigen Text, mit Normalziffern gleicher Breite (`lnum`, `tnum`), 1 px größer als vorher. | 15.10.2026 |

## 13. Raumansicht statt Diorama (PR #52)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | **Gebäude betritt man wie im Vorbild**: eigene Ansicht je Raum, zwischen den Räumen wechseln. Das **Diorama wird ersetzt** (nicht beides pflegen). | 15.10.2026 |
| Kamera | **Feste Ansicht je Raum**: Die Kamera steht vor der offenen Vorderseite und blickt leicht von oben hinein, kein Zoomen, Drehen oder Verschieben. Q/E wechseln im Gebäude den Raum. | 15.10.2026 |
| Räume | **Räume mit Tafeln**: Werkstatt = Werkraum, Lager, Kontor (nur eigene; zeigt die Verwaltung), Wohnstube (nur Stammhaus; öffnet die Familie). Rathaus = Ratssaal, Gerichtssaal, Amtsstube, Kerker. Dom später: Kirchenschiff, Sakristei. | 15.10.2026 |
| Umfang | **Erst Prototyp**: dieser PR baut Werkstätten und Rathaus; Dom und der Feinschliff je Beruf folgen im zweiten PR. | 15.10.2026 |
| Stammhaus | Die Familie wohnt in der **ersten eigenen Werkstatt in der Stadt**; dort gibt es die Wohnstube, das Oberhaupt sitzt tagsüber im Kontor, abends am Tisch. | 15.10.2026 |
| Bildschirm | Im Raum bleibt das Bild frei: Gebäudefenster und Kontor des Hauses erscheinen nur in Kontor und Amtsstube. Die Raumleiste steht unten rechts. | 15.10.2026 |

## 14. Chat für die Spiel-KI

| Thema | Entscheidung | Datum |
|---|---|---|
| Vierter Chat | **Spiel-KI** übernimmt die Bürger-KI (Phase 2, neu in `src/Reichsgilde.Core/Buerger/`) und die KI-Häuser (`Ki/`, `KiRivale.cs`, bisher Kern). Der Kern behält Wirtschaft, Regeln und Daten. | 15.10.2026 |
| Andocken | Der Spiel-KI-Chat darf kleine, im PR genannte Andockstellen in `Stadtwirtschaft.cs`, `wirtschaft.json` (eigener Abschnitt) und `Speicherstand.cs` (eigene Felder) ergänzen. Bestehende Werte und Regeln bleiben beim Kern. | 15.10.2026 |
| Messungen | Spiel-KI misst seine eigenen Änderungen selbst (mit main-Commit), wie der Kern. | 15.10.2026 |

## 15. Bürger-KI (PR #54)

| Thema | Entscheidung | Datum |
|---|---|---|
| Einwohner | **Einzelpersonen, wenige:** 25 je Haus (Spieler und KI), beim Spielstart festgelegt; bleibt gleich, wenn ein Haus erlischt. Kein eigener Betrieb, nur oberflächlich simuliert, damit die Stadt lebt. | 15.10.2026 |
| Völker | Mensch 5, Halbling 2, Zwerg 2, Elb 1 (wie die Brautschau). **Orks und Oger nicht** (kein Bürgerrecht; Fronarbeiter bleiben Personal der Häuser). Bedürfnisse je Volk leicht gewichtet (±20 %): Halblinge mehr Vergnügen, Zwerge mehr Auskommen, Elben mehr Glaube. | 15.10.2026 |
| Ratsherren und Kandidaten | **Von Anfang an Einwohner** mit eigenem Tagesablauf. Wahl, Gunst, Heirat und Weggang regeln weiter Rat und Familienbuch; die Bürger-KI gleicht nur ab. | 15.10.2026 |
| Lebenslauf | Gewöhnliche Einwohner altern und sterben nach den Regeln ihres Volkes; für jeden zieht ein neuer zu. Keine eigenen Familien. | 15.10.2026 |
| Rollen | Eigene Stadtrollen ohne Bezug zum Personal der Häuser: Tagelöhner, Knecht/Magd, Krämer, Hirte, Bote, Bettler, Mönch/Nonne, Witwer/Witwe; dazu Ratsherr und Bürgersohn/-tochter (Kandidaten ab Stand Bürger). | 15.10.2026 |
| Bedürfnisse | **Fünf:** Auskommen, Versorgung, Vergnügen, Glaube, Ruhe. Gesundheit und Sicherheit später. | 15.10.2026 |
| Kopplung | **Erst Kulisse (keine Wirkung), dann als Stellvertreter:** Das Verhalten der Einwohner wirkt als **Faktor um 1,0** auf die bestehenden Zahlen (Gäste, Gläubige, Nachfrage, Beute), damit die Balance im Mittel bleibt. Reihenfolge **Schänke → Kirche → Markt → Taschendiebe**, je ein PR mit Messung. | 15.10.2026 |
| Zufall | Fast deterministisch: eigener Generator ×71+53, Rauschen ±20 % auf den Nutzen, Streuung beim Aufwachen; Stammschänke und Stammkirche fest nach der Nummer. | 15.10.2026 |
| Rechenzeit | Höchstens **+25 %** auf `messen 30 20` (Ausgangswert 16 s je Charakter). | 15.10.2026 |
