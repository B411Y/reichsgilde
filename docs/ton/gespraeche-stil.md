# Passanten-Gespräche – Stil und Bauleitfaden

Stand 31.10.2026. Nutzerwunsch: kleine **Gesprächsrunden von Passanten** (2–3 Leute), gesprochen mit der lokalen Stimm-KI, im Stil von Gothic 1 und dem Gothic-1-Remake – „ganz abrupt, ein eigener Stil“ – und passend zur **Lage im Spiel** (Preise, Wetter, Gerüchte, Haus des Spielers, Jahreszeit, Ämter). Dieser Leitfaden hält fest, was die Recherche ergab, wie wir schreiben, wie eine Runde gebaut ist und wie sie an das Spiel andockt. Es ist **Planung, noch kein Code**; die Aufnahmen gibt es noch nicht (die vorhandene Liste `game/daten/gespraeche.json` enthält nur unverständliche Fetzen, siehe unten).

## Kurzfassung

1. **Stil:** kurze, harte Sätze (3–9 Wörter), mitten in der Sache begonnen, ohne Gruß und ohne Namen, am Ende ein Schnauzer oder ein Weggehen statt eines Abschieds. Derb, aber nie plump: Spott, Fluchen, Misstrauen gegen „die da oben“.
2. **Aufbau:** Anstoß – Antwort – (Gegenantwort) – Abschluss, 3 bis 6 Zeilen, 5 bis 15 Sekunden. Jede Zeile hat einen Sprecher mit fester Rolle und Stimme.
3. **Lage:** Eine Runde trägt Bedingungen („Eisen teuer“, „Regen“, „Pest in der Stadt“, „Fest läuft“, „das große Haus am Markt ist aufgestiegen“). Es gewinnt die **spezifischste** passende Runde, nicht die erstbeste (Muster aus dem Antwortsystem von Valve).
4. **Wiederholung ist der Hauptfeind:** Das Remake wird gerade dafür kritisiert. Also viele Runden, Sperren je Runde, Thema und Paar, und Bausteine (ein Anstoß, mehrere Antworten).
5. **Technik:** Die Aufnahmen sind vorproduziert (keine Zahlen, keine Namen, nur Kategorien), ein Lagebild wird einmal je Spielstunde aus dem Kern gelesen, der Kern bleibt unberührt. Die Liste `gespraeche.json` wird um einen Block `runden` erweitert; der Code liest bisher nur `anlaesse`, alte Dateien laufen weiter.
6. **Rechtlich:** Ein Stil ist frei, Zeilen und Aufnahmen aus Gothic nicht. Alle Texte sind eigene; alle Stimmen sind KI-Stimmen ohne echtes Vorbild.

## 1. Was die Recherche ergab

Zur Ehrlichkeit: Die öffentlich auffindbaren Quellen beschreiben vor allem die **Mechanik** (wann und wie Gespräche ausgelöst werden), kaum den Wortlaut. Den Ton des Spiels (Abschnitt „Eindruck“) kennt der Nutzer besser als jede Webseite. Empfehlung: Vor dem Schreiben der ersten Runden eine halbe Stunde Gothic 1 mit Notizblock hören – **Haltung und Rhythmus notieren, keine Zeilen**.

### Belegt (mit Quelle)

| Beobachtung | Quelle |
|---|---|
| **Smalltalk ist ein Zustand im Tagesablauf:** Zeitfenster plus Wegpunkt; der NPC geht dorthin, belegt einen freien „Smalltalk-Punkt“, dreht sich zu ihm und redet mit **irgendeinem** Nachbarn in der Nähe (kein festes Paar). Ansprache oder Schaden durch den Spieler **bricht ihn ab**, danach läuft der Tagesablauf weiter. | Inside-Gothic, „DailyRoutine“ (gelesen) |
| Wie die Smalltalk-**Zeilen** gewählt und gespielt werden, steht dort nicht. In den Original-Skripten ist eine gesprochene Zeile auf den Besitzer und den Spieler beschränkt; Gespräche zwischen zwei NPCs sind nicht vorgesehen und kamen erst durch Erweiterungen der Mod-Gemeinde. | Inside-Gothic, „DialogueSystem“ und Suchtreffer zu zMultilogue |
| Für **Gothic 2** gibt es eine Skript-Konstante für die Häufigkeit des Smalltalks und eine Mod, die ihn abstellt, weil er als zu wiederholend empfunden wird. Ob Gothic 1 es gleich macht, ist nicht geprüft. | GOG-Forum, Suchtreffer (Gothic 2) |
| **Dialog-Bausteine:** Jede Auswahl ist ein Eintrag mit **Bedingungsfunktion** (wird bei jedem Öffnen neu geprüft), Rangzahl, Kennzeichen „dauerhaft“ (bleibt) oder „einmalig“ (verschwindet nach der Wahl) und Kennzeichen „wichtig“ (der NPC **spricht von sich aus**, so warnen Wachen). Der Held merkt sich, was gewählt wurde; andere Einträge fragen das ab. | Inside-Gothic, „DialogueSystem“ (gelesen) |
| **Remake:** Die Entwickler wollen die Tagesabläufe der Figuren und **zufällige, belauschbare Gespräche** bewahren; Figuren sollen je nach Handlungsstand, Aufträgen, jüngsten Ereignissen und ihrer Rolle verschieden reagieren. Näheres zur Umsetzung nennen die Quellen nicht. | Wccftech, Alkimia-Q&A (gelesen) |
| **Kritik am Remake:** Die Welt wirkt zuerst dicht bevölkert, doch die Gespräche wiederholen mit der Zeit dieselben Fragen und Antworten, „die Illusion zeigt sich“; die Sprecherleistung sei uneinheitlich. Gelobt wird, dass Gespräche nach und nach den Aufbau der Kolonie, ihre Lager und deren Feindschaften zeigen. | GameCritics-Test (gelesen) |
| Die deutsche Synchronfassung des Originals gilt als rau und bodenständig; das Remake hat eine neu aufgenommene Fassung, teils mit Original-Sprechern. | PC Games, PC Games Hardware (nur Suchtreffer) |

### Eindruck (nicht belegt, aus Spielerfahrung und Beschreibungen; deshalb als Arbeitsannahme)

- Die Sätze sind **kurz und abgehackt**, oft Halbsätze, kaum Höflichkeit. Man redet sich **mit „du“** an, auch Fremde; Anreden und Titel sind selten und fast immer spöttisch gemeint.
- Jede **Gruppe hat ihre Sprechweise** (Handlanger derb, Wachen schroff und mit Dienstton, Gelehrte und Priester geschwollen, Außenseiter lakonisch). Man erkennt die Gruppe am ersten Satz.
- Die Gespräche sind **ohne Erklärung** in die Welt hineingeworfen: Man hört Mitte eines Gedankens zu und setzt sich den Rest selbst zusammen. Darum wirkt es abrupt.
- Ein Gespräch **endet scharf**: ein Befehl, ein Fluch, ein Abwimmeln, nie „Auf Wiedersehen“.
- Der Spieler wird **nicht gebraucht**: Die Leute reden miteinander; spricht man sie an, wimmeln sie ihn ab oder kommen zur Sache.

### Andere Spiele: Muster für reaktive Gespräche

| Muster | Herkunft | Für uns |
|---|---|---|
| **Fakten, Bedingungen, Regeln:** Das Spiel führt viele Fakten über die Welt; jede Zeile trägt Bedingungen; gespielt wird die Regel mit den **meisten zutreffenden** Bedingungen, allgemeine Zeilen sind der Rückfall; Pflichtbedingungen schließen eine Zeile ganz aus. | Valve, „AI-driven Dynamic Dialog through Fuzzy Pattern Matching“ (GDC 2012; Zusammenfassung bei Emily Short gelesen, Wiki und Folien nicht abrufbar) | Kern unseres Auswahlverfahrens (Abschnitt 5) |
| **Unterbrechbar:** Wer gerade plaudert, bricht für Dringendes ab. | derselbe Vortrag | Feuerglocke, Ansage des Haushofmeisters, Pest-Ausruf stoppen eine Runde |
| **Schutzraum um Gespräche:** Figuren, die zufällig in ein Gespräch laufen, werden ferngehalten („Deny Area“). | Witcher 3, GDC 2016 (Suchtreffer) | Nur wichtig, wenn Gespräche je ein Stand-Bild der Figuren brauchen; bei uns stehen die Gruppen schon (Abschnitt 6) |
| **Hintergrundgeplauder als eigene Klasse:** „Chitchat“, dicht und gleichmäßig, trägt die Stimmung einer Stadt; bei Infamous folgt es einem sich entwickelnden Stimmungsskript. | Assassin’s Creed 2, Infamous (Suchtreffer) | Gespräche formen das Bild der Stadt; unsere Stimmung steht im Kern (`Einwohnerschaft.Stimmung`) |

## 2. Stilregeln für Köln um 1400

### Sprache

| Regel | Ausführung |
|---|---|
| **Satzlänge** | 3–9 Wörter, höchstens 12. Ein Gedanke je Satz. Halbsätze, Ausrufe und abgebrochene Fragen sind erwünscht („Und der Preis?“). Keine Schachtelsätze, kein „weil … obwohl …“. |
| **Zeilenlänge** | 1–3,5 Sekunden gesprochen (etwa 3–12 Wörter). Eine Zeile über 4 s ist zu lang und wird geteilt. |
| **Wortwahl** | Alltag eines Kölner Gewerbetreibenden: Gulden, Heller, Zunft, Rat, Markt, Messe, Dom, Rhein, Karren, Kahn. **Höchstens ein altertümliches Wort je Zeile** (nit, traun, fürwahr, Gesell, wohlan, Hab acht, Meister, Base). Kein Kölsch-Dialekt, kein Hochmittelhochdeutsch: Alles muss beim ersten Hören verständlich sein. |
| **Anrede** | Unter Gleichen und nach unten **du**, nach oben und zu Fremden **ihr/Ihr**. Anreden selten und mit Absicht. **Nie Namen** (weder Personen noch Häuser): der Spieler wird „das große Haus am Markt“, „der Zugezogene“, „der Pfeffersack“. Das gilt wie bei den Ansagen des Haushofmeisters (`docs/bereiche/sound.md`). |
| **Zahlen, Beträge** | **Keine** gesprochenen Zahlen und Beträge (nicht vorab für jede Lage aufnehmbar). Statt „Eisen kostet vier Gulden“: „Eisen, wieder teurer.“ Datum, Uhrzeit und Jahr ebenso nicht. |
| **Orte** | Kölner Orte (Altermarkt, Heumarkt, Dom, Rhein, Eigelstein) nur in Runden, die ausdrücklich für die Karte `koeln` gelten (`"karte"` im Datensatz, Abschnitt 5); sonst „der Markt“, „die Gasse“, „der Fluss“, „der Dom“. |
| **Abruptheit** | Kein Gruß, keine Vorstellung. Anfangen mitten im Gedanken, aufhören ohne Schlusswort oder mit Schnauzer. Die Pointe liegt in der **letzten Zeile**, nicht der ersten. |
| **Betonung** | Nur über Satzzeichen (wie bei den Ansagen): „!“ derb oder laut, „…“ müde oder getragen, „?“ lauernd, „–“ Absetzen. Keine Regieanweisungen im Sprechtext. |

### Derbheit und Grenzen

- **Erlaubt:** Fluchen und Beschimpfen im Ton der Zeit: Lump, Esel, Schelm, Hundsfott, Pfeffersack, Bauer, Maulaffe, Schindmähre; „Teufel noch eins“, „Pest und Krätze“, „Verdammt“, „Heiliges Kreuz“; Spott über Stand, Zunft, Rat, Pfaffen, Alte, Dicke, Trinker; Zoten über Wirtshäuser und Liebschaften sind erlaubt, solange sie **Anspielung** bleiben.
- **Nicht erlaubt:** Fäkal- und Sexualderbheit im Klartext; Beschimpfungen echter Menschengruppen (Herkunft, Hautfarbe, Religion, Behinderung, Geschlecht); gewaltverherrlichende Zeilen. Die Spiegelung des **Gesellschaftszustands der Spielwelt** (Orks und Oger als Fron, Ketzer, Pest) ist erlaubt, aber als **Haltung einer Figur**, nicht als Meinung des Spiels: Jede Fron-Zeile braucht in der Nachbarschaft eine Gegenstimme. Spottnamen für die Fantasie-Völker nur milde und gegenseitig (Zwerg gegen Mensch, beide stechen).
- Keine echten Personen, Marken, Firmen. Keine Lieder und Verse aus fremden Werken.

### Rollen

Die Rollen sind die der Bürger-KI (`game/daten/wirtschaft.json`, `buerger.rollen`: Tagelöhner, Knecht/Magd, Krämer, Hirte, Feldarbeiter, Holzsammler, Bote, Bettler, Mönch/Nonne, Witwer/Witwe, Ratsherr, Bürgersohn/-tochter) und die Wache (Gardist, `src/Reichsgilde.Core/Ordnung/Wachwesen.cs`); dazu Handwerker aus den Berufen und die Völker (Mensch, Halbling, Zwerg, Elb).

| Rolle | Ton | Typische Themen | Stimme (Abschnitt 7) |
|---|---|---|---|
| **Handwerker** (Meister, Geselle) | derb, stolz, knapp; redet über Material und Preise | Eisen, Holz, Kunden, Zunft, Rat | M1 (mittel, rau), M2 (jung) |
| **Krämer / Händler** | mault und rechnet; Misstrauen gegen alles Neue | Preise, Steuer, Zoll, Pfeffersäcke | M3 (nasal, schnell) |
| **Tagelöhner, Knecht** | grob, trocken, selbstironisch | Lohn, Bier, Meister, Wetter | M4 (tief) |
| **Magd** | scharf, flink, wissend; Tratsch | Gerüchte, Häuser, Herrschaft | F1 |
| **Bettler** | lakonisch, gallig, hellsichtig | Almosen, Wetter, wer wen bestiehlt | M5 (heiser, alt) |
| **Wache (Gardist)** | schroff, Dienstton, Kurzbefehle | Ordnung, Nachtglocke, Gesindel, Sold | M6 (laut, hart) |
| **Mönch / Nonne** | ruhig, bildhaft, ein wenig streng | Messe, Sünde, Pest, Almosen | M7 / F2 (sanft) |
| **Bote** | atemlos, knapp | Wege, Tor, Zoll, Wetter auf der Straße | M2 |
| **Witwe / Witwer** | klagend, bissig | Früher, Preise, Jugend, Krankheit | F2 / M5 |
| **Bürgersohn / -tochter** | geschwollen, vorlaut | Ämter, Mode, Heirat | M2 / F1 |
| **Ratsherr** (selten) | gemessen, leere Worte | Ämter, Sitzung, Steuer | M7 |
| **Zwerg** | knapp, stur, Gold und Stein | Qualität, Handwerk, Preise | M8 (tief, grollend) |
| **Halbling** | gemütlich, schlau, ums Essen besorgt | Essen, Wirtshaus, Gerüchte | M9 (hell) |
| **Elb** | ruhig, kühl, lässt Sätze offen | Alterungslosigkeit, Hast der Menschen | F3 (klar) |

Orks und Oger arbeiten nur als Fron und reden **nicht** in diesen Runden; man redet über sie (Statut, Fron).

## 3. Aufbau einer Runde

| Stufe | Inhalt | Beispiel für die Wirkung |
|---|---|---|
| **Anstoß** (1 Zeile) | Beobachtung, Klage oder Neuigkeit, mitten in der Sache | „Eisen. Schon wieder teurer.“ |
| **Antwort** (1–2 Zeilen) | Widerspruch, Zustimmung mit Spitze, Gegenfrage | „Wer schmiedet, zahlt. Du schmiedest nit.“ |
| **Wechsel** (0–2 Zeilen) | Die Sache dreht sich, ein Dritter wirft ein | „Mein Nagelkasten schon.“ |
| **Abschluss** (1 Zeile) | Schnauzer, Befehl, Spruch oder Weggehen | „Dann klag bei den Schmieden, nicht bei mir.“ |

- **3 bis 6 Zeilen**, 2 bis 4 Wechsel der Sprecher, **5 bis 15 Sekunden** insgesamt (Pausen zwischen den Zeilen 0,2–0,6 s). Zweier-Runden sind die Regel, Dreier-Runden etwa jede dritte; der Dritte kommt mit einem **Einwurf** oder hat den **letzten Satz**.
- Der Ton ist **eine Haltung je Runde** (Klage, Spott, Neid, Furcht, Prahlerei, Streit). Kein Hin und Her ohne Ergebnis.
- Jede Runde lässt sich **ohne Bild** verstehen, trägt aber ein Stichwort der Lage („Eisen“, „Regen“, „Pest“).
- **Der Spieler** kommt nur als Gegenstand der Rede vor („das große Haus“), nie als Angesprochener: Die Kamera hat keine Figur. Ein Anklicken der Gruppe mit einem Abwimmeln-Satz wäre eine spätere Ergänzung (Offen).
- Unterbrechung: Eine laufende Runde wird **ausgeblendet** (0,3 s), wenn die Feuerglocke oder eine Ansage des Haushofmeisters beginnt, die Gruppe auseinandergeht oder die Kamera weit wegzoomt.

## 4. Bedingungen aus dem Spiel

Das Spiel kennt vieles; **nicht alles liegt als Abfrage bereit**. Die Tabelle nennt, was es im Kern gibt (Pfad im Repository, Stand `origin/main` vom 31.10.2026), und was fehlt.

### Vorhanden

| Bedingung | Werte (Vorschlag) | Abfrage und Datei |
|---|---|---|
| **Tageszeit** | Zunftglocke 4–10, Hoher Mittag 10–15, Vesper 15–20, Dunkle Nacht 20–23; Stunde | `Stadtwirtschaft.Uhr.Tagesphase`, `.UhrzeitInMinuten` (`Zeit/Spielzeit.cs`, `Zeit/Kalender.cs`) |
| **Jahreszeit** | Frühling, Sommer, Herbst, Winter | `Spielzeit.Jahreszeit` (jedes Spieljahr eine; `Zeit/Spielzeit.cs`) |
| **Wetter** | Klar, Wolkig, Regen, Sturm, Gewitter, Schnee, Nebel | **nicht im Kern**: `Wetterablauf.Lage(minute)` in `game/scripts/Wetter.cs`, fest aus Partie-Startwert und Zeit gerechnet (gleich nach dem Laden und beim Mitspieler) |
| **Ort der Gruppe** | Markt, Gasse, später Tor, vor Kirche, vor Schänke | `KlangStadtlebenModell.GruppenOrte` (Branch `phase5/ton-umgebung`, `game/scripts/KlangStadtlebenModell.cs`); heute nur Markt gegen Gasse |
| **Rolle und Volk der Sprecher** | siehe Rollentabelle | `Einwohner.Rolle`, `.Volk`, `.Geschlecht`, `Einwohnerschaft.RollenName` (`Buerger/Einwohner.cs`, `Buerger/Einwohnerschaft.cs`); in der Stadtansicht je Passant bekannt (`Stadtansicht.Passanten.cs`), im Ton noch nicht verknüpft (siehe Lücken) |
| **Preis einer Ware(ngruppe)** | billig / normal / teuer (z. B. Preisfaktor unter 0,85, über 1,2) | `Markt[ware].Preis` und `.Grundpreis`, `.Preisfaktor` (`Wirtschaft/Markt.cs`); Warengruppe: `Einwohnerschaft.Warengruppe(ware)` (z. B. „Schmiedewaren“, „Speis und Trank“, „Holzwaren“) |
| **Vorschau Preis** | „steigt gleich“ | `Marktstand.PreisNaechsteStunde` (`Wirtschaft/Markt.cs`) |
| **Steuer** | niedrig / üblich / hoch | `Stadtwirtschaft.Kasse.SteuersatzProzent`, `.Umsatzsteuer` (`Politik/Stadtkasse.cs`) |
| **Stadtkasse** | leer / ausreichend | `Stadtwirtschaft.Kasse.Stand` (`Politik/Stadtkasse.cs`) |
| **Fest** | läuft / läuft nicht | `Stadtwirtschaft.Ratsantraege.FestLaeuft`; Bauvorhaben in Planung: dort (`Politik/Ratsantraege.cs`) |
| **Statut und Stadtglaube** | Freie Stadt, Bestienfron, Leibeigenschaft; Glaubensfreiheit oder Konfession | `Stadtwirtschaft.Statut` (`Wirtschaft/Wirtschaftsdaten.cs`), `Stadtwirtschaft.Glaube.Stadtglaube` (`Glaube/Glaubenswesen.cs`) |
| **Ämter und Rat** | wer sitzt, ob der Spieler ein Amt hat, Wahl | `Stadtwirtschaft.Rat.Aemter`, `.AmtVon(konto)`, `.Ansehen(konto)`, `.LetzteSitzung`, `.Antrag` (`Politik/Stadtrat.cs`) |
| **Pest, Krankheit** | Pestjahr ja/nein, Krankenstand | `Stadtwirtschaft.Gesundheit.IstPestjahr`, `.Krankenstand` (`Gesundheit/Gesundheitswesen.cs`) |
| **Gericht, Pranger, Kerker** | letzte Urteile, Verfahren, Haus am Pranger | `Stadtwirtschaft.Justiz.LetzteUrteile`, `.Verfahren`, `.PrangerBis(konto)`, `.ImKerker(konto)`, `.LetzterGerichtstag` (`Justiz/Stadtgericht.cs`) |
| **Unterwelt** | Einbruch, Brandstiftung, Überfall in der letzten Nacht | `Stadtwirtschaft.Gauner.LetztesJahr`, `.DiesesJahr` (`Nachtereignis(Fuer, Text)`; `Gauner/Unterwelt.cs`), Brand: `Betrieb.Brandschaden` (`Wirtschaft/Betrieb.cs`) |
| **Wache** | Razzien, Streife, Hauptmann | `Stadtwirtschaft.Wache.DiesesJahr(konto).Razzien` u. a. (`Ordnung/Wachwesen.cs`) |
| **Messe, Kirchen** | zur Messe unterwegs; Prediger-Kirchen | `Kirchenwesen.Daten.MesseUm`, `Einwohnerschaft.Betende`, `.MesseErwartet` (`Kirche/Kirchenwesen.cs`, `Buerger/Einwohnerschaft.cs`) |
| **Hafen, Schiffe, Zoll** | Schiff unterwegs/angekommen, Flussfahrt (Winter: Eis) | `Stadtwirtschaft.Schiffahrt.Schiffe`, `.ZustandText`, `.Staedte` (`Wirtschaft/Schiffahrt.cs`) |
| **Haus des Spielers** | Stand, Rang (Vermögen), Ansehen, Zahl der Betriebe, Amt, Pranger, Aufstieg, neue Werkstatt | `Stadtwirtschaft.StandVon(konto)`, `.Rangliste`, `Konto.Reinvermoegen`, `.Rat.Ansehen(konto)`, `.Betriebe` (Filter `Konto`), Ereignisse `Aufgestiegen`, `BetriebErworben` (`Wirtschaft/Stadtwirtschaft.cs`) |
| **Familie des Spielers** | Geburt, Hochzeit, Tod, Erbe | `Stadtwirtschaft.Familien.DiesesJahr`/`.LetztesJahr` (`FamilienEreignis(Konto, Text)`, `Dynastie/Familienbuch.cs`); Text wird schon von `KlangAnsagenErkennung` einer Art zugeordnet |
| **Stimmung der Stadt** | 0–100 | `Stadtwirtschaft.Einwohnerschaft.Stimmung`, `.ZugezogenImVorjahr`, `.VerstorbenImVorjahr` (`Buerger/Einwohnerschaft.cs`) |
| **Mehrere Häuser** | Spieler gegen KI-Häuser | `Stadtwirtschaft.Konten`, `.Rivalen` – Gerüchte über „das Haus des Spielers“ gegen „ein fremdes Haus“ |

### Lücken (Vorschläge, noch nicht gebaut)

| Lücke | Vorschlag |
|---|---|
| **Ereignisse sind nur Text.** `Stadtwirtschaft.Meldung` liefert Zeichenketten; die Ansagen ordnen sie per Regex einer Art zu (`game/scripts/KlangAnsagenErkennung.cs`, Test: `AnsagenErkennungTests`). | Dieselbe Erkennung auch für die Gespräche nutzen (kein Kernumbau; ein Test fällt auf, wenn jemand einen Wortlaut ändert). Später ein strukturiertes Kernereignis (Art, betroffenes Konto) – Auftrag an kern, mit empfohlener Option „erst Regex, dann Kernereignis, wenn es sich lohnt“. |
| **Preisverlauf fehlt.** Der Kern liefert den Preis jetzt und die Vorschau auf die nächste Stunde, keinen Trend. | Die Darstellung merkt sich je Warengruppe den Preisfaktor zur vollen Stunde und leitet „steigt“/„fällt“ über die letzten 3 Stunden ab (reine Darstellung, nicht gespeichert). |
| **Gerüchte sind kein Objekt.** In der „dunklen Ecke“ der Schänke hört nur ein Haus am Gerichtstag Gerüchte als Textzeile (`Justiz/Stadtgericht.cs`, `Gastwirtschaft.cs`). | Ein **Gerüchtepool** in der Darstellung: aus Spuren, Urteilen, Nachtereignissen und Aufstiegen entstehen Kategorien („ein Haus ist aufgestiegen“, „ein Einbruch“, „Brand“, „Pranger“) mit dem Ziel *Spielerhaus / fremdes Haus / unbekannt* und dem **Beruf** des Betriebs („der Schmied“), nie dem Namen. |
| **Gruppe → Einwohner.** Die Gruppen im Ton (`Quelle`) tragen nur Ort und Köpfe, nicht Rolle oder Volk. | Der Ton bekommt von der Stadtansicht je Gruppe die Rollen der Köpfe (Passanten sind dort mit Einwohnernummer erfasst). Kleine Ergänzung in `Stadtansicht.Passanten.cs`/`Stadtansicht.Klang.cs`, Bereich darstellung, Auftrag über den Teamleiter. |
| **Wetter liegt im Spielordner**, nicht im Kern. | Kein Problem: Die Gespräche sind reine Darstellung. Die Bedingung liest `Wetterablauf` aus `game/scripts/Wetter.cs`. |
| **Ein Lagebild fehlt.** Alle obigen Abfragen sind einzeln verstreut. | Ein Godot-freier Datensatz `Stadtlage` (z. B. `game/scripts/KlangGespraecheLage.cs`, wie `KlangAnsagenErkennung` von den Tests mitübersetzt): rund 25 diskrete Felder (Phase, Jahreszeit, Wetter, Preisklassen je Warengruppe, Steuerklasse, Fest, Pest, Statut, Gerüchte-Arten, Hausstufe, …), **einmal je Spielstunde** und bei Ereignissen berechnet, nicht in jedem Bild. |

Zufall (Auswahl, Reihenfolge, Sprecherbesetzung) kommt von einem **eigenen** Zufallsgenerator in der Darstellung, nicht aus der Simulation (`Zufallsquelle.cs`): Die Gespräche sind reine Darstellung, verändern den Spielzustand nie und gehören nicht in den Lockstep des Mehrspielers.

## 5. Auswahl und Wiederholung

### Auswahl

1. Hat eine Gruppe von 2–3 Leuten beieinandergestanden (Bedingung des vorhandenen Gesprächsrufs) und ist der Takt für ein Gespräch dran, werden alle Runden geprüft: `ort`, `karte`, Rollen und Völker der Köpfe, dann die Bedingungen `wenn` und `ohne` gegen das Lagebild.
2. Von den passenden Runden gewinnt die mit den **meisten Bedingungen** (Spezifität); unter gleichen entscheidet das Gewicht und der Zufall. Allgemeine Runden (nur Ort) sind der Rückfall und stellen sicher, dass es nie still bleibt.
3. Eine Runde passt nur, wenn die Gruppe **genug Köpfe** und die **nötigen Rollen** hat (eine Wachen-Runde nur mit Wachmann).

### Wiederholungsregeln

| Regel | Wert (Vorschlag, einstellbar in der Datei) |
|---|---|
| Dieselbe Runde nie zweimal hintereinander | immer |
| Sperre je Runde | 20 Minuten Echtzeit |
| Sperre je Thema (Stichwort der Lage, z. B. `eisen`) | 5 Minuten Echtzeit |
| Sperre je Sprechpaar/Gruppe | 60 Sekunden |
| Abstand zwischen zwei Gesprächen insgesamt | 20–60 Sekunden Echtzeit (zufällig) |
| Nur ein Gespräch zur Zeit | immer; hört man zwei Gruppen, spricht nur die nähere |
| **Einmalig** (Kennzeichen `einmal`) | höchstens einmal je Sitzung (nicht im Spielstand gespeichert) |
| Mindestvorrat | **Ziel: 40 Runden zum Start, 100 ausgebaut**, davon höchstens ein Viertel mit sehr engen Bedingungen |
| Bausteine | Ein Anstoß mit mehreren Antworten und Abschlüssen verdreifacht die Vielfalt, ohne dass jede Runde neu geschrieben wird (Abschnitt 6) |
| Rückfall | Allgemeine Alltagsrunden (Wetter, Lohn, Bier, Wache) tragen den Großteil der Zeit; die „großen“ Lagerunden sind Würze und nicht Takt |

Die Sperren laufen in **Echtzeit** und sind nicht im Spielstand, wie bei den Ansagen und Rufen: Nach dem Laden darf wieder alles gesprochen werden.

## 6. Technik-Skizze

### Daten

`game/daten/gespraeche.json` (Branch `phase5/ton-umgebung`) hat bisher nur `anlaesse` (Name → Liste von Aufnahmen unter `game/assets/audio/umgebung/rufe/`, ohne Endung, verlangt 2–4 s, nicht verständlich; Voreinstellungen in `KlangUmgebungModell.Gespraeche`). `KlangUmgebung.GespraecheLaden` liest nur diesen Block; **unbekannte Schlüssel sind ihm gleichgültig**. Die Runden kommen deshalb als **zusätzlicher Block `runden`**, bestehende Dateien laufen weiter. Die Aufnahmen liegen in einem eigenen Ordner, z. B. `game/assets/audio/stimmen/passanten/<runde>_<nr>.ogg`.

```json
{
  "anlaesse": { "gasse": ["…"], "markt": ["…"] },
  "runden": [
    {
      "id": "markt_eisen_teuer_1",
      "ort": ["markt"],
      "karte": ["*"],
      "wenn": { "preis": { "Schmiedewaren": "teuer" } },
      "ohne": { "pest": true },
      "thema": "eisen",
      "gewicht": 1.0,
      "einmal": false,
      "sperre_min": 20,
      "rollen": ["Kraemer", "Tagelohner"],
      "zeilen": [
        { "sprecher": 0, "datei": "passanten/markt_eisen_teuer_1_a", "text": "Eisen. Schon wieder teurer.", "pause": 0.3 },
        { "sprecher": 1, "datei": "passanten/markt_eisen_teuer_1_b", "text": "Wer was schmiedet, zahlt. Du schmiedest nit.", "pause": 0.25 }
      ]
    }
  ]
}
```

- **Schlüssel in `wenn`/`ohne`** (Vorschlag, alle optional): `phase`, `stunde_ab`/`stunde_bis`, `jahreszeit`, `wetter`, `preis` (Warengruppe → billig/normal/teuer/steigt/faellt), `steuer`, `fest`, `pest`, `statut`, `stadtglaube`, `gerucht` (Art), `haus` (Stufe, Aufstieg, Pranger, Rang), `kasse`, `stimmung`, `hafen`, `messe`. Die Menge wird in einer Godot-freien Klasse geprüft; ein Test lässt jede `wenn`-Taste gegen die Schlüsselliste prüfen (Tippfehler fallen sofort auf).
- **Spezifität** = Zahl der Schlüssel in `wenn`. `ort`/`rollen` filtern nur.
- **Bausteine:** Optional `"anstoss": "<id>"`, `"antworten": ["<id>", …]`, `"abschluss": [...]` für Runden, die aus Teilstücken zusammengesetzt werden; die Teile haben nur Ausdruck, keine Bedingung. Erst bauen, wenn die ersten 40 Runden stehen.
- **Zeilendateien statt einer Datei je Runde:** Jede Zeile ist eine eigene Aufnahme. Gewinn: Jeder Sprecher kommt aus dem Mund **seiner Figur** (3D-Ort, Panner, Tiefpass wie `UmgebungGespraech`), Zeilen lassen sich in neuer Folge zusammensetzen, ein Satz lässt sich nachbessern, Überlappung („Unterbrechen“) ist möglich. Preis: viele kleine Dateien (40 Runden × 4 Zeilen = 160; 100 Runden = 400; etwa 25 KB je Zeile = rund 10 MB).
- **Untertitel:** `text` steht im Datensatz auch für eine kleine Textzeile über der Gruppe (Barrierefreiheit; das Remake wurde für fehlende Untertitel getadelt, GameCritics). Eigener Schalter im Reiter Ton, Vorschlag: nah gezoomt an.
- **Verständlichkeit nach Abstand:** Nah (Zoom wie `GespraechUmkreis`, etwa 26 m) verständlich, weiter weg nur Murmeln: derselbe Satz wird über **Pegel und Tiefpass** unverständlich gemacht oder durch die vorhandenen Fetzen ersetzt. Kein zusätzlicher Dateisatz.

### Anbindung

| Teil | Wo | Bereich |
|---|---|---|
| Lagebild (Godot-frei, Test) | `game/scripts/KlangGespraecheLage.cs` (neu) + Test in `tests/Reichsgilde.Core.Tests` | sound (Test: kern fragen, wie bei `AnsagenErkennungTests`) |
| Auswahl und Sperren | `game/scripts/KlangGespraeche.cs` (neu), Aufruf in `KlangUmgebung` beim Gesprächsruf | sound |
| Rollen der Gruppe | `Stadtansicht.Passanten.cs`/`Stadtansicht.Klang.cs` liefern je Gruppe Rollen und Köpfe | darstellung (kleine Ergänzung, über den Teamleiter) |
| Untertitel | kleine Textzeile über der Gruppe | oberflaeche |
| Schalter und Regler | Reiter Ton (`EinstellungenAnzeige.cs`) | oberflaeche |
| Entwicklerreiter Klänge | Knöpfe je Runde, wie die Ansagen (`EntwicklerAnzeige.Klaenge.cs`) | oberflaeche |
| Startoption | `--gespraechtest=<id\|alle\|lage>` löst Runden aus und schreibt ins Log, welche gewählt oder verworfen wurde (Eintrag in `docs/STARTOPTIONEN.md`) | sound |
| Erzeugen | `tools/ton/gespraeche_erzeugen.py` (aus `tools/ton/ansagen_erzeugen.py` abgeleitet): Sprechtext je Zeile, Stimme je Sprecher, gleiche Auswahl nach Whisper und Stimmähnlichkeit | sound |

Kern und `wirtschaft.json` bleiben unberührt, solange nur gelesen wird.

## 7. Hinweise für die Stimm-KI

Erzeugt wird auf dem Grafik-PC (Qwen3-TTS im Klonmodus, wie bei den Ansagen: `docs/assets/prompts/haushofmeister.md`). Die Erzeugung macht der Teamleiter, das Mitglied sound baut ein.

| Punkt | Vorschlag |
|---|---|
| **Stimmen** | Neun bis elf **KI-Stimmen** als Klonvorlagen, je eine für M1 bis M9 und F1 bis F3 (siehe Rollentabelle), jede einmal aus einer Stimmbeschreibung (VoiceDesign) erzeugt wie die Vorlage des Haushofmeisters; **keine echten Personen, keine Nachahmung bekannter Sprecher** (auch nicht der Gothic-Sprecher). Der Haushofmeister bleibt davon getrennt (anderer Ton, anderes Alter). |
| **Sprechweise** | Natürlich, **kein** Hörbuch- und Theaterton: leicht gepresst, schnell, mit Atem, Schlucken, Brummen. Das Tempo bleibt natürlich (kein `atempo` ×1,15 wie bei den Ansagen; höchstens 1,05). Betonung über Satzzeichen; „–“ für Abbrüche. |
| **Länge** | Zeile 1–3,5 s, Runde höchstens 15 s; Pausen 0,2–0,6 s werden vom Spiel gesetzt, nicht in die Aufnahme gebaut (0,1 s Rand). |
| **Trocken aufnehmen** | Keine Raumsimulation, kein Hall: Raum und Entfernung setzt das Spiel (Bus `UmgebungGespraech`, Panner, Tiefpass). |
| **Lautheit** | Etwa −20 LUFS wie die Fetzen im Bus `UmgebungGespraech`; Zeilen innerhalb einer Runde untereinander gleich laut, Sprecher mit klarer Betonung (Wache) um 2 dB lauter, Murmler (Bettler) um 2 dB leiser. |
| **Qualitätsprüfung** | Wie die Ansagen: je Zeile drei Takes, gültig nur mit Whisper-WER 0, ohne Übersteuerung und ohne lange Pause; unter den gültigen die höchste Ähnlichkeit zur Vorlage. Dazu **Hörprobe** je zehn Runden durch den Nutzer: Gesprochen-wie-geschrieben ist bei kurzen, derben Sätzen riskanter als bei den förmlichen Ansagen. |
| **Zahlen und Namen** | Nicht im Sprechtext (siehe oben); Zahlen und Namen sind die häufigste Fehlerquelle der KI-Stimme. |
| **Protokoll** | Jede Datei unter `game/assets/` bekommt ihre Zeile in `docs/assets/protokoll.csv` und der Prompt eine Datei in `docs/assets/prompts/`; die Steam-Liste (`--steam-liste`) wegen KI-Inhalten. |
| **Strata (lokales Textmodell)** | Darf als Ideengeber für **Rohfassungen** nach diesem Leitfaden dienen; jede Zeile wird von einem Menschen/Claude umgeschrieben. Nicht mit Gothic-Zeilen füttern (Abschnitt 8). |

## 8. Rechtliches

- **Stil ist frei.** Ein Ton – kurz, derb, direkt – ist keine geschützte Ausdrucksform. Wir übernehmen **Haltung und Rhythmus**, nicht Texte.
- **Nicht frei:** Dialogzeilen, Aufnahmen, Figuren- und Ortsnamen sowie eigene Begriffe aus Gothic (und aus dem Remake). Darum steht in diesem Dokument **keine Zeile aus Gothic**, und die Beispiele sind eigene für Köln um 1400. Auch in den Prompts für Strata oder die Stimm-KI stehen keine fremden Zeilen.
- Keine Stimmen, die nach bestimmten echten Sprechern klingen; die Klonvorlagen sind KI-Stimmen (siehe oben).
- Die Aufnahmen und Texte sind Inhalte des Projekts: „Alle Rechte vorbehalten“ (Entscheidung 29.10.2026, `LICENSE-INHALTE.md`). Hinweis KI-Inhalte für Steam: `tools/assets/protokoll_pruefen.py --steam-liste`.
- Das ist kein Rechtsrat. Wer sich unsicher ist, ob eine Zeile zu nah am Vorbild liegt, schreibt sie um.

## 9. Eigene Beispielzeilen (Köln um 1400)

Alles frei erfunden, ohne Namen und Zahlen. Das Format: Sprecher (Rolle) – Zeile.

**Runde 1 – Markt, Eisen teuer** (`wenn`: `preis.Schmiedewaren = teuer`; Krämer, Tagelöhner)

1. Krämer: Eisen. Schon wieder teurer.
2. Tagelöhner: Wer was schmiedet, zahlt. Du schmiedest nit.
3. Krämer: Mein Nagelkasten schon.
4. Tagelöhner: Dann klag bei den Schmieden, nicht bei mir.

**Runde 2 – Gasse, Regen** (`wenn`: `wetter = Regen`; Magd, Knecht)

5. Magd: Wieder Regen. Mein Zeug wird nie trocken.
6. Knecht: Dein Zeug? Meine Stiefel schwimmen.
7. Magd: Dann tritt nit in jede Pfütze, Esel.

**Runde 3 – Gasse, nach Feierabend** (`wenn`: `phase = Dunkle Nacht`; Wache, Handwerker)

8. Wache: He, du! Es hat schon geläutet.
9. Handwerker: Ich bin gleich daheim, Herr Wächter.
10. Wache: Gleich zählt nit. Heim, oder ich helfe nach.
11. Handwerker: Ich geh ja. Ich geh.

**Runde 4 – Markt, Gerücht über das Haus des Spielers** (`wenn`: `gerucht = haus_aufgestiegen`, `haus.ziel = spieler`; Magd, Krämer)

12. Magd: Hast du’s gehört? Das große Haus kauft schon die dritte Werkstatt.
13. Krämer: Pfeffersack. Wo hat der das Geld her?
14. Magd: Arbeit, sagen sie.
15. Krämer: Arbeit. Das sagt jeder, der’s nit sagen will.

**Runde 5 – Gasse, Pest in der Stadt** (`wenn`: `pest = true`; Bettler, Mönch)

16. Bettler: Wieder ein Kreuz an einer Tür. Hört man.
17. Mönch: Betet, mein Sohn. Und meidet die engen Gassen.
18. Bettler: Meiden? Ich wohne in der engsten.
19. Mönch: Dann betet doppelt.

**Runde 6 – Markt, Steuer hoch** (`wenn`: `steuer = hoch`; Handwerker, Krämer)

20. Handwerker: Der Rat hat die Marktsteuer erhöht.
21. Krämer: Der Rat. Sitzt warm und zählt unsere Gulden.
22. Handwerker: Wer hat die Herren denn gewählt?
23. Krämer: Die Herren selbst.

**Runde 7 – Markt, Fest läuft** (`wenn`: `fest = true`; Tagelöhner, Magd, Knecht)

24. Tagelöhner: Freibier! Heute trink ich für drei.
25. Magd: Du trinkst immer für drei.
26. Knecht: Und zahlst für keinen.
27. Tagelöhner: Heute zahlt der Rat. Prost.

**Runde 8 – Markt, Zwerg und Schmied** (`wenn`: `volk = Zwerg` und `preis.Schmiedewaren`; Zwerg, Handwerker)

28. Zwerg: Menschenstahl. Weich wie Butter.
29. Handwerker: Dann spar dir das Kaufen, Bartträger.
30. Zwerg: Ich schmiede. Du jammerst. So bleibt’s.

Die Zeilen sind eine Stilprobe, nicht der Spielstand der Dinge: Wo im Kern etwas anderes steht (z. B. wer im Rat wählt), gilt der Kern, und die Zeile wird geändert.

## 10. Offene Fragen an den Nutzer

| Frage | Empfehlung |
|---|---|
| **Verständlich oder Gemurmel?** Sollen die Runden überall zu verstehen sein oder nur nah gezoomt? | Nur nah gezoomt (etwa die Hälfte des Startzooms) deutlich, sonst Murmeln wie bisher; dazu ein Schalter für Untertitel im Reiter Ton. |
| **Umfang zum Start** | 40 Runden (rund 160 Zeilen), dann nach Gehör ausbauen. Mehr nur, wenn die Wiederholung stört. |
| **Derbheit** | Die Grenzen aus Abschnitt 2 (Fluchen ja, Fäkal- und Sexualklartext nein, keine Beschimpfung echter Menschengruppen). Soll die Altersfreigabe-Rücksicht noch enger sein? |
| **Mundart** | Hochdeutsch mit einem Hauch Altsprache (nit, Gesell, traun), **kein Kölsch**: bleibt verständlich und die KI-Stimme spricht es sauberer. |
| **Anklicken einer Gruppe** (Abwimmeln wie im Vorbild) | Später und nur, wenn sich das Anklicken im Spiel ohnehin lohnt; für den Anfang nicht. |
| **Namen im Gerücht** | Nein (Beruf und „das große Haus“); sonst müsste jeder Name aufgenommen werden. |

## Quellen

Gelesen (Seite abgerufen):

- Inside-Gothic, Tagesablauf (Smalltalk als Zustand): <https://ataulien.github.io/Inside-Gothic/DailyRoutine>
- Inside-Gothic, Dialogsystem (Bedingungen, „wichtig“, dauerhaft/einmalig): <https://ataulien.github.io/Inside-Gothic/DialogueSystem>
- Wccftech, Gothic-Remake-Q&A mit Alkimia (Tagesabläufe, belauschbare Gespräche, Reaktionen): <https://wccftech.com/gothic-remake-qa-alkimia-talks-about-expanding-the-classic-rpg/>
- GameCritics, Test des Gothic-1-Remakes (Gespräche gelobt, Wiederholung kritisiert): <https://gamecritics.com/gc-staff/gothic-1-remake-review/>
- Emily Short, Zusammenfassung des GDC-Vortrags „Dynamic Dialogue“ (Valve): <https://emshort.blog/2012/03/16/gdc-2012-talk-on-dynamic-dialogue/>

Nur als Suchtreffer gesehen (Inhalt nicht selbst geprüft, deshalb vorsichtig behandelt):

- Valve, Response System (Wiki; beim Abruf 403): <https://developer.valvesoftware.com/wiki/Response_System>
- Valve, GDC 2012, Folien (zu groß für den Abruf): <https://cdn.fastly.steamstatic.com/apps/valve/2012/GDC2012_Ruskin_Elan_DynamicDialog.pdf>
- GDC Vault, „AI-driven Dynamic Dialog through Fuzzy Pattern Matching“: <https://gdcvault.com/play/1015317/AI-driven-Dynamic-Dialog-through>
- GDC Vault, „Behind the Scenes of Cinematic Dialogues in The Witcher 3“ (Sperrbereich um Gespräche): <https://gdcvault.com/play/1022988/Behind-the-Scenes-of-Cinematic>
- GOG-Forum, Häufigkeit der NPC-Gespräche (Gothic 2): <https://gog.com/forum/gothic_series/can_i_reduce_the_frequency_of_npc_dialogue>
- PC Games, Xardas-Sprecher im Remake (Synchron-Eindruck): <https://www.pcgames.de/Gothic-Remake-Spiel-73829/News/xardas-sprecher-bodo-henkel-1479247/>
- PC Games Hardware, Originalsprecher im Remake: <https://www.pcgameshardware.de/Gothic-Remake-Spiel-73829/News/Deutsche-Synchro-holt-Originalsprecher-zurueck-1486132/>
- SoundInGames, „Chitchat“ (Hintergrundgespräche): <https://soundingames.dei.uc.pt/index.php/Chitchat>

Im Projekt: `docs/ton/sprach-npcs.md` (Echtzeit-Sprach-NPCs, vorerst nein), `docs/spielbuch/ton.md`, `docs/bereiche/sound.md`, `docs/assets/prompts/haushofmeister.md`.
