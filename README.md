# Reichsgilde

*Arbeitstitel* – eine Wirtschaftssimulation und Dynastie-Sandbox im 2,5D-Stil, inspiriert von **„Die Gilde“ (Europa 1400)**.
Schauplatz ist ein düster-bodenständiges Heiliges Römisches Reich des Spätmittelalters, in dem Menschen, Zwerge, Elben und Halblinge als Bürger leben und Orks und Oger als Bestien vor den Toren lauern.

> Hobby- und Lernprojekt · Open Source (MIT) · Godot 4.7 + C# · **Windows & Linux**

[![Build, Tests & Export](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml/badge.svg)](https://github.com/B411Y/reichsgilde/actions/workflows/build.yml)

## Aktueller Stand

**Phase 3 – 2,5D-Stadtansicht (Greybox).** Die Reichsstadt ist als begehbare Schrägansicht zu sehen: Mauer, Osttor, Markt, Rathaus, Dom, Gießereien und die Grube vor der Stadt – noch aus einfachen Grundformen. Die orthografische Kamera blickt diagonal (nach Nordosten, Nordwesten, Südwesten oder Südosten) und dreht in 90°-Schritten. Karren fahren sichtbar zum Markt, Licht und Himmel folgen der Spieluhr, die Esse glüht bei Arbeit, Gebäude lassen sich anklicken.

**Diorama-Innenansicht.** Ein Doppelklick auf eine Gießerei schneidet sie wie ein Puppenhaus auf: Dach und die beiden Wände zur Kamera verschwinden, Nachbarhäuser davor werden ausgeblendet. Innen sieht man Esse, Amboss und Werkbank, das Lager als Kistenstapel (eine Kiste je 5 Stück) und die Arbeiter, die während der Arbeitszeit hämmern – alles direkt aus der Simulation.

**Hauptmenü, Speichern und Laden.**
- Das Spiel startet im Hauptmenü. Unter „Neue Partie“ wählst du Hausname, Siegbedingung, Dauer eines Spieljahres, Startstatut, Startvermögen, den Charakter des Rivalen und den Zufallsstartwert.
- Im Spiel öffnet **Esc** das Spielmenü: Weiterspielen, Speichern, Laden, Hauptmenü, Beenden.
- Nach jedem Jahresbericht wird automatisch gespeichert.
- Ein geladener Spielstand läuft exakt so weiter, wie er gespeichert wurde.

**Expansion.** Als Bürger kannst du eine zweite Werkstatt kaufen (Gießerei an der Südgasse) oder eine Grube pachten (am Silberberg oder im Tannenwald). Freie Plätze sind in der Stadt mit Zaun und Schild „zu verkaufen“ bzw. „zu verpachten“ markiert. Ein Klick darauf zeigt Preis, Pacht und den erwarteten Gewinn. Jede Liegenschaft gibt es nur einmal, und Haus Eisenbart greift ebenfalls zu.

**Dynastie.** Dein Haus besteht aus Menschen, Zwergen, Elben oder Halblingen. Volk, Geschlecht und Vorname des Oberhaupts wählst du im Hauptmenü. Jedes Jahr altern alle; mit dem Alter steigt das Sterberisiko. Über „Familie“ im Kontor öffnest du die Familientafel. In der **Brautschau** wirbst du mit Geschenken um ledige Bürger der Stadt, nur standesgemäß und mit etwas Geduld, denn mehr als 20 Zuneigung im Jahr gewinnt niemand. Neben jedem Kandidaten steht, wie viele Kinder die Ehe erwarten lässt. Ehen zwischen Völkern sind erlaubt, aber weniger fruchtbar, und sie wirken sich im Rat aus. Stirbt das Oberhaupt, erbt das im **Testament** bestimmte Kind, sonst das älteste volljährige. Gibt es keines, führt nach dem Witwenrecht der Ehepartner das Haus weiter. Sonst erlischt das Haus, und die Partie ist verloren. Die Völker unterscheiden sich deutlich: Elben leben sehr lange, bekommen aber selten Kinder. Halblinge bekommen schnell Kinder, sterben aber früher. Haus Eisenbart ist ein Zwergenhaus und sucht nach denselben Regeln eine Partie.

**Talente und Ausbildung.** Jede Person hat fünf Talente mit 0 bis 6 Sternen: **Verhandeln** (bessere Markterlöse), **Handwerkskunst** (mehr Leistung aller Arbeitskräfte), **Bei Nacht und Nebel** (Gaunerstücke gelingen öfter, Zeugen sehen weniger), **Kampf** (stärkere Wegelagerer, besser geschützte eigene Karren) und **Rhetorik** (Ratswahlen, Plädoyer vor Gericht, Brautgeschenke). Es zählen die Talente des Oberhaupts, der Erbe ist also eine echte Entscheidung. Die Völker bringen Anlagen mit: Zwerge sind die besten Handwerker, Elben die besten Redner, Halblinge die geschicktesten Gauner. Kinder erben die Hälfte vom Mittel der Eltern, den Rest von ihrem Volk, und die Brautschau zeigt die Stärken jedes Kandidaten. In der zweiten Hälfte der Kindheit lernen Kinder in einer **Ausbildung** dazu: Zunftlehre, Kontorlehre, Klosterschule, Fechtmeister, Universität (ab Bürger) oder die Gasse. Haus Eisenbart bildet seine Kinder ebenfalls aus und setzt das begabteste als Erben ein.

**Berufe: Schmied, Tischler, Steinmetz, Alchimist, Wirt, Prediger, Dieb, Gardist und Geldleiher.** Im Hauptmenü wählt man den Startberuf. Der **Schmied** fördert in der Grube Eisen und Silber und macht in der Gießerei Beschläge, Rapiere und Silberringe. Der **Tischler** schlägt im Holzschlag Holz und macht in der Tischlerei Holzzapfen, Schemel und Truhen – für eine Truhe braucht er einen Beschlag vom Schmied. Der **Steinmetz** bricht im Steinbruch Bruchstein und Granit und macht in der Maurerei Schleifziegel, Grabsteine und Mühlsteine; seine Schleifziegel braucht die Goldschmiede für das Prunkschwert, er selbst braucht in der Steinmetzhütte Werkzeug vom Schmied. Der **Alchimist** zieht im Kräutergarten Kräuter und fängt Kröten und macht in der Tinkturei Farbe, Kräuterschnaps und Heilsalbe; seine Farbe braucht der Tischler für den Schrank. Wer das Handwerk beherrscht, mischt Krötenschleim im Hinterzimmer selbst (halber Preis, höhere Chance). Einen weiteren Beruf erlernt man im Kontor (500 Gulden Lehrgeld, 4 AP); erst dann darf man dessen Werkstätten und Umlandbetriebe erwerben. Neben Haus Eisenbart (Zwerge, Schmiede) spielen **Haus Eichwald** (Elben, Tischler) und **Haus Kieselbach** (Halblinge, Steinmetze) mit; im Hauptmenü lassen sich 1–3 Rivalen wählen. Ist ein KI-Haus Patrizier geworden, darf es einen Betrieb mehr führen und dafür ein zweites Handwerk erlernen. Werkstätten sind in `wirtschaft.json` unter `betriebsarten` und `berufe` beschrieben – ein weiterer Beruf ist vor allem ein Dateneintrag plus ein Platz in der Stadt.

**Wirt.** Der Bauernhof liefert Gerste und Roggen, die Schänke (→ Wirtshaus → Gasthaus) macht Speisen und Bier – und **bewirtet Gäste**: Von 12 Uhr bis Mitternacht (abends doppelt so viele) kehren Gäste ein und bestellen, was im Lager ist, zum Ausschankpreis (Grundpreis + 25 %). Die Gäste verteilen sich nach Stufe und Ansehen auf alle Schänken der Stadt; je Ausschankware bleiben 15 Stück für sie im Lager, nur der Rest fährt zum Markt. **Barde** (120 Gulden, einmal je Jahr): abends 50 % mehr Gäste, 10 % höhere Zeche, doppelte Stammtisch-Gunst. **Würfeltisch** (ab Wirtshaus): 4 Gulden je Gast, aber 2 Ansehen weniger. **Stammtisch** (Gasthaus): am Jahresende Gunst bei jedem Ratsherrn – ohne Bestechungsspur. **Dunkle Ecke:** Wer eine Schänke führt, hört am Gerichtstag Gerüchte über die Taten anderer Häuser (Beweise wie vom Spitzel). Alles in der Betriebsverwaltung der Schänke.

**Prediger.** Der Klosterhof liefert Lumpen und Wachs, die Kirche (→ Dom → Kathedrale) macht Papier, Kerzen und Lehrschriften, später Hetzschriften, Psalter, Stundenbücher und Reliquiare. **Messe** um 10 Uhr: Die 200 Gläubigen des Jahres verteilen sich nach Stufe, Ansehen und Rhetorik auf alle Kirchen und spenden je 2,5 Gulden (steuerfrei). **Ablass:** Andere Häuser kaufen im Gerichtssaal eine ihrer Taten frei (250 Gulden + halber Wert der Tat, an den Prediger) – die Tat verfällt mit allen Beweisen; zuerst die, von der andere wissen. **Beichte:** Am Jahresende hört der Prediger von fremden Taten (25 % je Tat); bricht er das Beichtgeheimnis (2 AP), werden daraus Beweise – und er begeht selbst ein Vergehen. **Lehrschrift** (Familientafel, 1 AP, einmal im Jahr): +½ Stern, aber nur bis 3 Sterne. **Hetzschrift** (Hinterzimmer, 1 AP): 5 Ansehen weniger für ein anderes Haus bis Ende nächsten Jahres – eine Verleumdung. Beide Schriften kommen aus dem eigenen Lager oder vom Markt, nutzbar für jeden Beruf.

**Dieb.** Kein Handwerk, keine Ware, kein Umland: Schmugglerloch → Unterschlupf → Diebesgilde (4 / 6 / 8 Diebe). **Taschendiebe** ziehen während der Arbeitszeit über den Markt (≈ 0,8 / 0,88 / 1,04 Gulden je Diebesminute, steuerfrei, Erfahrung wie bei jedem Handwerk). Je Diebesstunde 1,2 % Fangchance (weniger auf höheren Stufen und mit „Bei Nacht und Nebel“): Der Dieb kommt in den Turm, ein anderes Haus erfährt, wem er diente (Spur mit Beweis). Nachts arbeiten die eigenen Leute im Hinterzimmer (1 / 2 / 3 Aufträge je Nacht, kein Geld, nur AP): **Einbruch** (besser als eine Bande, die Beute kommt ins eigene Lager), **Auskundschaften** (Beweise ohne eigene Spur), ab Unterschlupf **Schutzgeld** (wer nicht zahlt, dem brennt ein Betrieb), in der Diebesgilde **Entführung** (Lösegeld bis Ende des Folgejahres, sonst wird die Geisel in die Fremde verkauft; Kerker). Ein Haus mit Dieben hat 3 Ansehen weniger. Nur wagemutige KI-Häuser erlernen den Dieb; jede KI zahlt Lösegeld und wägt Schutzgeld gegen den drohenden Brand ab.

**Gardist.** Das ehrliche Gegenstück zum Dieb – kein Handwerk, keine Ware: Wachhaus → Stadtwache → Garnison (4 / 6 / 8 Wachleute) in der Stadt, dazu ein **Wachturm** an der Landstraße. **Sold:** Die Stadt zahlt für jede Arbeitsminute (0,75 / 0,83 / 0,98 Gulden vor Steuer). **Patrouille:** Jeder Wachmann in der Stadt senkt nachts die Erfolgschance von Diebstahl, Sabotage, Einbruch, Brandstiftung und Entführung in der Stadt um 2,5 % (höchstens 40 %) und macht Zeugen häufiger – zum Schutz aller Häuser. Der **Wachturm** senkt die Überfallchance der Banden auf allen Umlandwegen um bis zu 30 %. **Razzia** (Gerichtssaal → Beweisbuch, 2 AP, 1 / 2 / 3 im Jahr): Die Wache durchsucht ein Haus, findet zu jeder seiner Taten mit 50 % (+10 % je Stufe) einen Beweis und beschlagnahmt das Diebesgut aus seinen Verstecken. **Kopfgeld:** Gewinnt ein Haus der Wache eine Anklage, zahlt die Stadt ein Viertel der Buße; jeder gefasste Taschendieb bringt 50 Gulden. **Geleitschutz:** Die Eskorte „Stadtwache“ (20 Gulden je Fahrt, 80 % Schutz) gibt es nur, solange ein Haus ein Wachhaus führt – das Geld geht an dieses Haus. Ein Haus der Wache hat 3 Ansehen mehr. Nur vorsichtige und ausgewogene KI-Häuser erlernen den Gardisten; sie durchsuchen zuerst Diebesnester.

**Geldleiher.** Kein Handwerk, keine Ware: Wechselstube → Geldleihe → Bankhaus (2 / 3 / 4 Schreiber). **Kasse:** Der Geldleiher legt Bargeld in die Kasse; jeder Schreiber verleiht während der Arbeitszeit bis zu 2.000 Gulden (× 1 / 1,25 / 1,5) an Bürger der Stadt zu 12 % im Jahr. Jedes Jahr fallen mit 20 % Kunden aus – dann sind 10 % der Kasse verloren. **Wechsel:** Auswärtige Kaufleute wechseln ihre Münzen – 0,7 % des Marktumsatzes gehen an die Schreiber der Stadt (≈ 800 Gulden je Geselle und Jahr mit voller Kasse). **Kredite an Häuser:** Im Kontor leiht jedes Haus bei einem Geldleiher (1 AP, ein Kredit zur Zeit, 1–5 Jahre, höchstens die Hälfte des eigenen Reinvermögens; der Geldleiher verleiht 3.000 / 6.000 / 12.000). Den Zins setzt der Geldleiher (4–50 %, Standard 12 %); Zinsen laufen stündlich auf, Zins und Rate sind am Jahresende fällig. Fehlt das Bargeld, pfändet der Büttel Lagerware zu 75 % des Marktpreises. **Wucher:** Über 20 % ist der Schuldschein ein Beweis gegen den Geldleiher, und er hat 5 Ansehen weniger, solange der Kredit läuft. Geldleihe +1 AP, Bankhaus +2 AP je Jahr. **Reinvermögen:** Stand, Rangliste, Ansehen, Bußen, Bankrott und Sieg richten sich nach Bargeld + Verliehenem − Schulden; geliehenes Geld macht nicht reicher. KI-Häuser lernen den Geldleiher wie jeden Beruf, setzen den Zins nach Charakter (10 / 14 / 25 %) und füllen ihre Kasse; selbst leihen sie nicht.

**Marktplatz.** Ein Klick auf den Marktplatz in der Stadt (oder der Knopf „Marktplatz“ im Kontor) öffnet das Marktfenster: alle Waren als Kacheln mit Pixel-Icon und Preis, nach Rohstoffen, Zwischenwaren und Fertigwaren geordnet. Grün ▲ heißt über dem Grundpreis, rot ▼ darunter; die gelbe Zahl im Icon ist der eigene Lagerbestand, der Tooltip zeigt Herkunft, Nachfrage, Zutaten und Gewinn je Arbeitsminute. Neue Icons: Zeichenfunktion in `tools/icons/waren_icons.py` ergänzen und `python3 tools/icons/waren_icons.py` ausführen.

**Meistergrade und Gebäudestufen.** Jedes Stück, das die Betriebe eines Hauses herstellen oder fördern, bringt Erfahrung im Beruf. Reicht sie, legt man im Kontor die Meisterprüfung ab (Gebühr und 2–4 AP) – bis zum 6. Grad. Ab dem 3. Grad lässt sich die Werkstatt am Platz zur Stufe 2 ausbauen (Schmiede bzw. Drechslerei, 3 Plätze), ab dem 5. Grad zur Stufe 3 (Goldschmiede bzw. Sägewerk, 4 Plätze); der Umbau kostet Gulden und einen halben Arbeitstag. Höhere Stufen stellen wertvollere Waren her: Werkzeug, Silberkette, Goldkette, Prunkschwert; Gehstock, Armbrust, Schrank, Prunktruhe; Säule, Taufbecken, Steinfigur, Grabmal; Rosenwasser, Gegengift, Theriak, Zinnober (Gold gibt es nur auf dem Markt). Der Steinmetz baut zur Steinschneiderei und Steinmetzhütte aus. Der 6. Grad bringt einen Aktionspunkt mehr je Jahr. Grad und Erfahrung gehören dem Haus und gehen beim Erbfall nicht verloren.

**Gesundheit und Krankheiten.** Jedes Jahr erkrankt etwa jede siebte Person: Husten, Ruhr, Lungenentzündung, Knochenbruch, Vergesslichkeit, Angst im Dunkeln, Haarausfall oder Lachanfälle. Krankheiten kosten Talente oder Ansehen, Ruhr und Lungenentzündung können tödlich sein. Alle 25–40 Jahre kommt die **Pest**: Viele erkranken, ein Teil stirbt (Zwerge und Elben sind weniger anfällig), und alle Betriebe arbeiten ein Jahr lang 30 % langsamer. Ein **Medicus** (Gulden) heilt sofort bzw. halbiert das Pestrisiko; **Kräuterschnaps** vom Markt kommt in die Hausapotheke und heilt leichte Krankheiten. Im Hinterzimmer lässt sich einem fremden Oberhaupt **Krötenschleim** ins Essen mischen – das Vergehen heißt Giftmischerei.

**Aktionspunkte.** Jedes Haus bekommt zum Jahreswechsel **4 Aktionspunkte** (im Hauptmenü 1–12 einstellbar) und kann bis zu 12 ansparen. Persönliche Handlungen kosten AP: Werben, Heiraten, einen Ratsherrn beschenken, sich bewerben, einen Spitzel ansetzen, eine Bande anheuern (je 1), Statut-Antrag, Anklage, Erpresserbrief (je 2). Mit übrigen AP übt das Oberhaupt ein Talent: ½ Stern je Übung, je Talent einmal im Jahr; es kostet 2 AP unter 3 Sternen, 4 AP unter 5 und 6 AP darüber. Die Wirtschaft (Betriebe, Personal, Karren, Wächter, Stand, Liegenschaften) bleibt frei. Haus Eisenbart hat dieselben AP und setzt Prioritäten nach seinem Charakter.

**Gauner.** Über „Hinterzimmer“ im Kontor heuerst du Banden gegen andere Häuser an. Ausgeführt wird bei Nacht und Nebel um 21 Uhr:
- **Diebstahl:** Die Bande holt die wertvollste Ware aus einem Lager, der Hehler zahlt dir die Hälfte des Marktwerts.
- **Sabotage:** Ein Betrieb steht einen halben Arbeitstag still, und das Opfer zahlt die Reparatur.
- **Überfall:** Wegelagerer lauern der nächsten beladenen Umlandfahrt auf. Die Eskorte schützt wie gewohnt.

Hast du mindestens 3 Beweise, kannst du **Schweigegeld erpressen**. Der Brief selbst ist allerdings ein Beweis gegen dich. Jedes Gaunerstück hinterlässt eine Spur fürs Gericht, und manchmal erkennt dich ein Zeuge. Überfall und Erpressung bringen vor Gericht ein Jahr **Kerker**. In dieser Zeit laufen nur deine Betriebe weiter. Gegen Gauner hilft ein **Wächter** je Betrieb: Er halbiert die Chance der Diebe und sieht öfter, wer es war. Haus Eisenbart greift je nach Charakter selbst zu solchen Mitteln: vorsichtig nie, ausgewogen selten, wagemutig oft.

**Stadtgericht.** Jedes Geschenk an einen Ratsherrn ist Bestechung und hinterlässt eine Spur, bei dir wie bei Haus Eisenbart. Im Rathaus öffnet „Gerichtssaal öffnen“ das Gericht. Dort setzt du einen **Spitzel** auf ein anderes Haus an. Er deckt am Jahresende Spuren auf, die in deinem **Beweisbuch** landen. Mit Beweisen erhebst du **Anklage**. Den Vorsitz führt der Stadtrichter, dazu kommen zwei ausgeloste Schöffen. Eine Prognose zeigt, wie jeder Richter heute urteilen würde. Am **Gerichtstag** zum Jahresende, noch vor der Ratssitzung, fällt das Urteil. Bei einem Schuldspruch gibt es eine Buße (das Dreifache des Tatwerts, höchstens das halbe Vermögen), der Verurteilte verliert sein Amt und steht drei Jahre am Pranger, was ihn Ansehen kostet. Bei einem Freispruch entschädigt der Ankläger den Beklagten. Geschenke an die Richter können einen knappen Fall kippen, ab drei Beweisen aber nicht mehr. So wird auch ein bestechlicher Bürgermeister angreifbar. Haus Eisenbart setzt ebenfalls Spitzel an, klagt, wenn die Beweise sicher reichen, verteidigt sich und besticht vorsichtiger, wenn man ihm auf die Finger schaut.

**Stadtrat.** Im Rathaus öffnet „Ratskammer öffnen“ den Stadtrat: sieben Ämter in einer Pyramide, vom Zöllner bis zum Bürgermeister, jedes mit Amtseinkommen. Die Sitze halten zu Beginn Ratsherren der Stadt (Menschen, Zwerge, Elben, Halblinge), jeder mit einer eigenen Haltung zum Fron- und Sklavenstatut. Ab dem Stand Bürger kannst du dich um ein Amt bewerben, immer eine Stufe über deinem jetzigen. Gewählt wird am Jahresende, und zwar von den Inhabern der höheren Ämter. Mit Geschenken gewinnst du die Gunst einzelner Ratsherren; sie verblasst jedes Jahr zum Teil. Eine Prognose zeigt dir, wie die Wahl heute ausginge. Wer im Rat sitzt, kann eine Änderung des Statuts beantragen, über die der ganze Rat abstimmt. Haus Eisenbart spielt dabei mit: Es bewirbt sich, verschenkt Gulden und stellt Anträge, wenn sich das für seine Betriebe rechnet. Was in der Ratssitzung geschah, steht im Jahresbericht.

**Stände, Rangliste und Bankrott.** Jedes Haus beginnt als Gemeiner. Ab 10.000 Gulden kann man im Rathaus das Bürgerrecht erwerben, danach geht es weiter über Patrizier und Edler bis zum Reichsgrafen. Nach jedem Jahr zeigt ein Jahresbericht Gewinn und Verlust, den Fortschritt zum nächsten Stand und die Rangliste gegen Haus Eisenbart. Wer drei Jahresabschlüsse in Folge im Minus steht, ist bankrott.

**Bedienung (Phase 4 beginnt).** Ein Klick auf deine Gießerei (oder „⚒ Betrieb verwalten“ im Kontor) öffnet das Bedienfeld: Produkt wählen, Personal einstellen und entlassen, Karren kaufen und für Umland-Fahrten Eskorte und Nachtfahrt festlegen. Neben jeder Wahl steht, was sie bringt (Gulden je Arbeitsminute, Jahresertrag je Kopf, erwartete Kosten je Fahrt) – dieselben Rechnungen, nach denen die KI entscheidet. Jede Aktion ist ein **Befehl**, den der Simulationskern prüft und mit Begründung ausführt oder ablehnt; die KI benutzt denselben Weg.

**Tag und Nacht.** Sonne und Mond wandern über den Himmel und werfen beide Schatten; in der Dämmerung blenden sie weich über.

**KI-Rivale.** Haus Eisenbart wird von einer KI geführt, die nach denselben Regeln und mit denselben Informationen spielt wie du (keine Schummelei). Sie stellt ihre Gießerei auf das jeweils einträglichste Produkt um, teilt die Förderung der Grube nach den Preisen auf, besetzt freie Plätze mit der besten erlaubten Arbeitskraft (auch nach einem neuen Statut), wählt Eskorten nach erwartetem Raubverlust und kauft Karren, wenn ein Betrieb zu oft stillsteht. Jede Entscheidung erscheint im Protokoll und im Kontor.

**Phase 2 – Simulationskern.** Die Spielzeit läuft (1 Runde = 1 Spieltag = 1 Spieljahr, Standard 15 Minuten), mit Pause, Tempo und wählbarer Siegbedingung. Die **Wirtschaft** ist eingebaut: Marktpreise nach Angebot und Nachfrage, Gießerei und Mine produzieren während der Arbeitszeit, Löhne, Pacht und Steuern laufen in die Jahresrechnung, und das Fron- und Sklavenstatut bestimmt, wer wo arbeiten darf. **Lager und Karren** bringen die Waren zum Markt; auf Umland-Wegen lauern **Banditen aller Völker**, gegen die Eskorten helfen. Das Kontor im Spiel zeigt Vermögen, Lager, Karren, Jahresrechnung und Marktpreise.

## Projektstruktur

```
reichsgilde/
├── Reichsgilde.sln                 Projektmappe – fasst alle drei C#-Projekte zusammen
├── game/                           Das Godot-Projekt (Darstellung, Szenen, Eingabe)
│   ├── project.godot
│   ├── Reichsgilde.csproj
│   ├── data/wirtschaft.json        Alle Zahlen des Wirtschaftsmodells (aus dem Balancing-Rechner)
│   ├── assets/waren/               Pixel-Icons der Waren (32 × 32, erzeugt von tools/icons)
│   ├── scenes/                     Szenen (.tscn)
│   └── scripts/                    C#-Skripte, die an Szenen hängen
├── src/Reichsgilde.Core/           Simulationskern – die reine Spiellogik, OHNE Godot
│   ├── Simulation.cs
│   ├── Bank/                       Geldleiher: Kasse, Wechsel, Kredite, Pfändung, Wucher
│   ├── Dynastie/                   Personen, Familien, Brautschau, Geburt, Tod und Erbfolge
│   ├── Gauner/                     Unterwelt: Diebstahl, Sabotage, Überfall, Erpressung, Wächter
│   ├── Justiz/                     Stadtgericht: Spuren, Spitzel, Beweise, Anklage, Urteil, Kerker
│   ├── Ki/                         KI-Rivalen (Entscheidungen und ihr Charakter)
│   ├── Ordnung/                    Stadtwache: Sold, Patrouille, Wachturm, Razzia, Kopfgeld, Geleitschutz
│   ├── Partie/                     Partie-Einstellungen, Siegbedingung, Aktionspunkte
│   ├── Politik/                    Stadtrat: Ämter, Wahlen, Gunst, Anträge zum Statut
│   ├── Speichern/                  Spielstände als JSON
│   ├── Wirtschaft/                 Markt, Betriebe, Lager, Karren, Überfälle, Konten, Fronstatut
│   └── Zeit/                       Spielzeit, Taktgeber, Kalender
├── tests/Reichsgilde.Core.Tests/   Automatische Tests für den Simulationskern
├── tools/icons/waren_icons.py      Zeichnet die Waren-Icons (Python + Pillow)
├── docs/                           Erklärungen und Entscheidungen
└── .github/workflows/              Automatische Prüfung auf Windows und Linux
```

Warum diese Aufteilung? → [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md)

## Loslegen

### Voraussetzungen
- [Godot 4.7.x – .NET-Version](https://godotengine.org/download)
- [.NET SDK 8 oder neuer](https://dotnet.microsoft.com/download)
- VS Code mit der C#-Erweiterung (empfohlen)

### Spiel starten
1. Godot starten → **Importieren** → `game/project.godot` auswählen.
2. Oben rechts auf **▶ (Projekt ausführen)** klicken oder **F5** drücken – das Hauptmenü erscheint.

Spielstände liegen unter `%APPDATA%\Godot\app_userdata\Reichsgilde\spielstaende` (Windows) bzw. `~/.local/share/godot/app_userdata/Reichsgilde/spielstaende` (Linux).

Steuerung:
- **Leertaste** = Pause · **1–4** = Tempo 1×/2×/4×/8× · **5–6** = Testtempo 16×/32× (nur während der Entwicklung)
- **Mausrad** = Zoom · **rechte Maustaste ziehen** oder **WASD/Pfeiltasten** = verschieben · **Q/E** = drehen
- **Linksklick** auf ein Gebäude = Informationen (bei eigenen Betrieben mit Bedienfeld) · **Doppelklick** auf eine Gießerei = Innenansicht
- **Esc** = Innenansicht verlassen → Auswahl schließen → Spielmenü (Speichern, Laden, Hauptmenü)
- **F11** = Vollbild ein/aus (auch im Haupt- und Spielmenü; wird gespeichert). Das Fenster lässt sich frei ziehen: Ein größeres Fenster zeigt mehr Stadt, Tafeln, die nicht hineinpassen, bekommen Scrollbalken (Mausrad über der Tafel rollt sie).
  Hinweis: Startet man das Spiel im Godot-Editor, läuft es im Reiter „Game“ eingebettet. Dort bestimmt der Editor die Fenstergröße, und Vollbild ist nicht möglich. Zum Testen im eigenen Fenster das Einbetten im Reiter „Game“ ausschalten.

### Tests ausführen
Im Terminal (in VS Code: *Terminal → Neues Terminal*) im Ordner `reichsgilde`:

```
dotnet run --project tests/Reichsgilde.Core.Tests
```

Am Ende muss stehen: `Alle … Tests bestanden.`

### Spiel exportieren (Windows / Linux)
1. Einmalig in Godot: **Editor → Exportvorlagen verwalten → Herunterladen und installieren**
2. **Projekt → Exportieren…** → Vorlage **Windows** oder **Linux** wählen → **Projekt exportieren**
3. Das fertige Spiel landet im Ordner `build/` (wird nicht ins Repository übernommen)

## Plattformen und automatische Prüfung

Reichsgilde läuft auf **Windows und Linux**. Bei jedem Pull Request prüft GitHub automatisch:

1. Bauen und Tests – auf Windows **und** Linux
2. Export für beide Systeme und ein kurzer Startversuch des Linux-Builds

Die fertigen Builds kann man unter **Actions → jeweiliger Lauf → Artifacts** herunterladen.
Die Konfiguration steht in [`.github/workflows/build.yml`](.github/workflows/build.yml).

## Lizenz

Code: [MIT](LICENSE). Die Waren-Icons sind selbst gezeichnet (`tools/icons/waren_icons.py`) und stehen unter derselben Lizenz. Dieses Projekt verwendet **keine** Grafiken, Sounds, Texte oder sonstigen Inhalte aus „Die Gilde“ – das Original dient nur als Vorbild für Spielmechaniken.
