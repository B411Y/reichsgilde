# Bereich Spiel-KI – Offen, Entscheidungen, Messungen

> **Archiv:** Ältere und abgelöste Entscheidungen (bis 31.10.2026, PR #274) und ältere Messungen stehen in [archiv/spiel-ki.md](archiv/spiel-ki.md). Zum Arbeiten genügt diese Datei; das Archiv nur bei Bedarf lesen.

**Nur der Spiel-KI-Chat schreibt hier.** Ausnahme: Ein anderer Bereich darf unter „Offen“ eine Zeile mit „(von …)“ anhängen, wenn er hier etwas braucht.

So wird eingetragen (damit sich die Chats nie an derselben Stelle treffen):
- **Neue Einträge unten im passenden Abschnitt anhängen.** Keine Nummern – die Reihenfolge ist die Zeit.
- **Entscheidungen** als Unterabschnitt `### Thema (PR #…)` mit einer Tabelle `| Thema | Entscheidung | Datum |`.
- **Erledigtes:** die Zeile aus „Offen“ löschen. Was gemergt wurde, zeigt GitHub (Pull requests → Closed).
- Ältere Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`.

## Offen

- (von Kern) Die KI-Häuser nutzen die Werkstatt wie in Gilde 1 noch nicht ganz (PR #152): Sie zahlen üblichen Lohn ohne Prämie und teilen keine Arbeiter einzeln zu (Einrichtung kaufen sie seit PR #177, befördern seit PR #224). Abfragen: `Stadtwirtschaft.Werkstaetten` (`UnfallGefahr`), Befehl `LohnstufeSetzen`.
- (von Kern) Die KI setzt fast jedes Jahr einen Spitzel (300 Gulden) auf das stärkste andere Haus an, auch mit nur einem Betrieb: Beim Messspieler des Balancing-Berichts ≈ 210 im Jahr, rund ein Zehntel des Gewinns (PR #177, WIRTSCHAFTSMODELL „Balancing-Messung 2“). Vorschlag: an Gewinn oder Rang koppeln.
- (von Kern) Abfrage für den Kauf von Einrichtung (PR #177): `Werkstattwesen.ErwarteterMehrgewinnJeJahr(Betrieb, kennung)` und `Amortisationsjahre(…)`; Eisentür und Löschfass bringen nur etwas nach Raub bzw. Brand im Vorjahr (`Betriebsrechnung.Raub`, `Brandschaden`). Seit PR #177 holt ein Einbruch beim Geldleiher Geld aus der Kasse – `Unterwelt.ErwarteterGewinn` und `Diebeswesen.ErwarteterEinbruch` rechnen es schon mit.
- (von Kern) Teure Ware im Lager schützen (PR #232): Seit der Giftdolch einen Dolch des Schmieds braucht (≈ 170 Gulden je Stück), räumen die Diebe der KI-Häuser das Lager einer Siederei nachts aus (Startwert 202: fünf Einbrüche in zwei Nächten, 42 Dolche ≈ 8.800 Gulden, danach Bankrott). Die KI sollte Eisentür und Wächter nach dem Lagerwert kaufen bzw. einstellen, nicht erst nach einem Angriff (gilt auch für Gold, Goldketten, Prunkwaren).
- Mittagsandrang in der Schänke (PR #274): Um 12 Uhr, der ersten Stunde mit Einkehren, gehen fast alle Einwohner zugleich hin (bis 107 von 123 in der einen Schänke), abends um 21:45 bis 84. Eine Kapazität für Einwohner gibt es nicht. Designfrage beim Teamleiter, Empfehlung: Sitzplätze je Schänke nach Stufe im Abschnitt `buerger` (im Verhältnis von `anziehungJeStufe`, etwa 20/30/40); wer keinen Platz bekommt, nimmt die nächstbeste Handlung (mittags Arbeit, abends daheim); ohne Eintrag unbegrenzt wie heute. Vorher messen, wie sich die Gäste auf mehrere Schänken verteilen. Nur den Mittag zu dämpfen, verschiebt den Andrang auf 18 Uhr.
- Die Schätzung „Stillstand vorher ^ (Durchfluss nachher ÷ vorher)“ ist im ersten Jahr zu optimistisch: Die Prüfung sah in Köln nach dem ersten Kauf 7–12 % geschätzt und 14–41 % im Folgejahr gemessen (Grube 18 %, Bauernhof 41 %) – das erste Jahr hat Anlaufverluste (leere Karren, Lager füllt sich). Die KI kauft dann im Jahr darauf nach; schlimmstenfalls einen Karren zu spät. Bei Gelegenheit das erste Jahr eines Betriebs nicht als Eichung nehmen.
- (von Kern) Sitzplätze der Schänken (Datenversion 67): Die Einwohner finden in einer Schänke höchstens ⌈Plätze × Einwohner ÷ 17⌉ Plätze (`Gastwirtschaft.EinwohnerPlaetze`, `Einwohnerschaft.Quote` mit Grenze, Ersatzhandlung, Stillen nach der Verteilung; `OhneSchankplatz`). Entscheidung des Nutzers vom 2.11.2026; mit 17 und mindestens 20 Plätzen ist die Grenze immer größer als die Einwohnerzahl, greift also nie – die Kopplung wirkt wie bisher. Zur Kenntnis, nichts zu tun.
- (von Darstellung) Hafenarbeit plant Bündel zu 3 Stücken (`Zierfahrplan.HubGroesse`, `HubAnteil`, `Hafenarbeit.Planen`, Test angepasst), Teamleiter informiert. Bitte bei der nächsten Änderung an der Hafenarbeit prüfen, ob die Aufteilung (erster Hub kürzer, Träger ohne Wartezeit zwischen Bündeln) so bleiben soll.
- Bauort nach dem Hafen: Karrenkauf (`Durchfluss`, `KiRivale.Karren.cs`) und Pacht im Umland (`KiRivale.Umland.cs`) rechnen weiter nur mit der Fahrt zum Marktplatz: Für das Verhältnis der Karrenarten ändert das kaum etwas (außer an Höchst- und Mindestdauer und bei festen Minuten für Furt und Fähre); eine Umland-Liegenschaft näher am Hafen als am Markt gibt es auf den mitgelieferten Karten nicht. Die Bauortwahl nimmt die Fahrzeiten der Jahreszeit, in der sie entscheidet (Schlamm im Frühling, im strengen Winter die Fähre im Eis), für alle Jahreszeiten – genauer ginge es mit einer Abfrage des Kerns je Jahreszeit (Befund der Prüfung).
- Aufschlag am Hafen „spürbar“ (Ziel des Nutzers, Hafen v2): Mit 3–5 % ändert sich in Köln weder die Wahl der Bauorte noch das Vermögen messbar (Messung „Aufschlag am Hafen“). Der Engpass ist die Lage: Der beste Werkstatt-Bauort am Rheinhafen fährt 130 Minuten hin und zurück, die am Markt 20–120. Empfehlung an den Teamleiter: einige Werkstatt-Bauplätze direkt am Hafen (Leveldesign, Köln am Rheinhafen bzw. Stapelhaus) – dort lohnt der Aufschlag, ohne dass alle hinziehen (wenige Plätze). Weitere Hebel: mehr als 5 % (über der Grenze des Nutzers) oder Karren mit Ausfuhrware fahren auch bei etwas längerer Fahrt zum Hafen (Kern-Regel).
- (von kern) `KiRivale.Schaenke.cs:37` (`ProduktWert`) und `KiRivale.cs:310-311` (`FoerderungAufteilen`) rufen `GewinnJeArbeitsminute(ware)` ohne Betrieb auf: Die KI wählt die Ware einer Hafen-Werkstatt ohne Aufschlag (4,5–6,4 % je Minute bei Ausfuhrware), bewertet Ausbau bzw. Karren aber mit Aufschlag.

## Entscheidungen

### Bürgerdichte und Tagesplan (PR #323)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Nutzer 5.11.2026 zu PR #317 (namenlose Statisten): „Die Passanten müssen schon echt sein, genauso anklickbar wie die anderen. Köln ist größer als die Greybox, da müssen auch mehr Leute sein.“ Befund: 25 Einwohner je Haus, Köln 223 für die ganze Stadt, am Heumarkt 1–4 Figuren. Ursachen: die Zahl hing nur an den Häusern (Köln hat 82 Hausplätze, bewohnt waren rund 40), tagsüber ruhten viele daheim (10 und 15 Uhr 40 von 223) und Knechte/Mägde dienten drinnen; mittags sitzt ein Fünftel in der Schänke, die übrigen laufen zur halben Stunde meist irgendwo durch die große Stadt. | 5.11.2026 |
| Zahl | `buerger.jeHausplatz` (6): Mit Karte ein Haushalt je freiem Hausplatz (Einwohnerplätze + Kulissenhäuser − Ratsherren- und Bürgerhäuser) mit 5–7 Personen. Köln 82 Plätze → rund 395 gewöhnliche, 418 Einwohner insgesamt (vorher 223); Greybox 40 → 167 (123). Ohne Karte oder wenn die Plätze weniger fassen als jeHaus × Häuser: alte Regel. Kein neues Wohnhaus teilt sich mehr den letzten Vorstadtplatz. | 5.11.2026 |
| Tagesplan | `buerger.tagesplan` (8–20 Uhr, `rastDraussen` 0,7, `besorgungen` 0,5): Wer ruht, rastet am nächsten Markt; Knechte und Mägde machen dort Besorgungen. Fest aus Nummer, Tag und Doppelstunde (`Wurf`), ohne Generator. Nur der Ort ändert sich, die Handlung nie. Auswahltafel: „hält ein Schwätzchen“, „macht Besorgungen“ (kleine Änderung in `Stadtansicht.Passanten.cs`, Bereich darstellung). | 5.11.2026 |
| Wirtschaftsgewicht | Erste Fassung (alle zählen, Warenkorb anteilig gewichtet) machte den Spieler mit einem Betrieb in Köln 10–20 % reicher; die Gegenprobe `jeHaus` 50 nach alter Regel zeigte dasselbe – schon die Zahl wirkt über die Kopplungen. Jetzt zählen für Markt, Warenkorb, Schänke, Messe und Taschendiebe nur jeHaus × Häuser gewöhnliche Einwohner (`ZaehltFuerWirtschaft`); sie entstehen und würfeln wie vorher, die übrigen mit eigenem Generator. Ergebnis bitgleich (Test, Messung). | 5.11.2026 |
| Daten | Neue Felder nur im eigenen Abschnitt `buerger`, **keine Datenversion** nötig: Ohne die Felder rechnet alles wie bisher; alte Spielstände laden mit ihren mitgespeicherten Daten und Einwohnern unverändert (neue Felder im Spielstand fehlen dort und gelten als „wie vorher“). | 5.11.2026 |
| Darstellung | Ab 300 Einwohnern rechnet die Darstellung außerhalb des Bildes sparsamer (`VieleFiguren`, kleine Änderung im Bereich darstellung: `Stadtansicht.Passanten.cs`, `.Figuren.cs`, `.Marktbummel.cs`, `.Passantenausweichen.cs`). Das Ziel „+2 ms“ wird damit noch nicht erreicht (Messung unten); weitere Hebel bei darstellung. | 5.11.2026 |
| Tests | `BuergerDichteTests` (9): ohne Karte bzw. ohne Feld alte Zahl; Köln/Greybox ein Haus je Platz mit 5–7 Bewohnern; Zählende genau jeHaus × Häuser, auch nach Jahren; Tagesplan ändert nur den Ort (8–20 Uhr, Heim → Markt); tagsüber mehr am Markt, ab 20 Uhr niemand draußen rastend; Wurf fest und gleichverteilt; Köln und Greybox zwei Jahre mit und ohne – Vermögen, Nachfrage, Passanten, Handlungen gleich; Speichern/Laden mittags. Angepasst: zwei Prüfungen in `BuergerTests` (Dienst darf am Markt sein), `HafenRuhtTests` nimmt die neuen Felder heraus (Sollwerte vor dem Schalter). | 5.11.2026 |

### Torwache (PR #328)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Vorschlag „Wachen und Diebe“, Paket P1, vom Nutzer am 5.11.2026 mit allen Empfehlungen freigegeben: Torwächter als neue Einwohner-Rolle, echte anklickbare Figuren mit Namen, ohne Gewicht für die Wirtschaft; niemand bezahlt sie; Einfluss der Spieler nur wie bisher über den Stadthauptmann. | 5.11.2026 |
| Regel | Je Tor zwei Mannschaften zu je 2 Wächtern; Frühschicht vom Tagesbeginn bis 14 Uhr, Spätschicht ab 14 Uhr (Anmarsch 13:45–14:00, Abmarsch der Frühschicht 14:00–14:15, je Platz 3 min versetzt), ab 20 Uhr bleibt einer. Mannschaften tauschen täglich. Ruhestand am Jahresende, wenn älter als volljährig + 60 % der Jahre bis zum Sterbealter. Alles fest aus der Uhr bzw. eigenem Generator. | 5.11.2026 |
| Daten | `buerger.torwache` (`jeTorTag` 2, `jeTorNacht` 1, `wechsel` 14, `nachtAb` 20, `weg` 15) und Rolle „Torwächter“ (Gewicht 0, Arbeitsort Tor) im eigenen Abschnitt, **keine Datenversion**: ohne den Abschnitt keine Torwache. Alte Spielstände laden mit ihren mitgespeicherten Daten, also ohne Torwache. | 5.11.2026 |
| Wirtschaft | Nicht in `Einwohner`, keine Nummer aus der Zählung, eigener Generator: bitgleich (Test `BuergerTorwacheTests`: ganzer Spielstand ohne die Torwache gleich, Köln und Greybox, drei Jahre). Keine Messung nötig. | 5.11.2026 |
| Tests | `BuergerTorwacheTests` (7): Zahl je Karte (Köln 40, Greybox 12, ohne Karte/Daten 0); Namen eindeutig, nicht in der Einwohnerliste, per Nummer auffindbar; Schichtplan über einen Tag und Tausch am nächsten; Anteil des Weges; Wirtschaft bitgleich; Spielstand und Ruhestand nach 30 Jahren; Datenprüfung. | 5.11.2026 |
| Test gekürzt (PR „Torwache-Test kurz“) | „Spielstand geladen … Ruhestand“ lief 30 Spieljahre in Original und geladener Partie (208–233 s, ein Drittel der Testzeit), jetzt etwa 2 s: Am Stichtag (13:50) sind ein Drittel der Wächter eins über, ein Drittel genau an der Ruhestandsgrenze; der erste echte Jahreswechsel läuft in beiden Partien, die übrigen 29 nur über `Torwache.Jahresende`. Strenger als vorher: Grenze genau (Alter ≤ Grenze statt ≤ Grenze + 1, „genau an der Grenze“ bleibt noch ein Jahr), Nachfolger jung und mit fortlaufender Nummer, geladene Partie in allen Feldern (Nummer, Geburtsjahr, Volk, Glaube, Dienst) statt nur im Namen gleich, mindestens 5 natürlich Nachgerückte unter den unveränderten Wächtern. Gegenprobe: Würfelstand bzw. Nummernzähler nicht geladen, kein Jahresende der Torwache, Ruhestand ein Jahr zu früh, Nachfolger nicht jung – jeder Fehler macht den Test rot. | 7.11.2026 |

### Hafenarbeit (PR #334)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Entladen Stufe 3: Träger am Hafen sind echte Einwohner (Option „Kern-Zustand Hafenarbeit“, #317 mit Statisten abgelehnt). Ablauf nach Entscheidung des Nutzers vom 6.11.2026: gearbeitet wird, solange ein Schiff liegt, auch vor 8 Uhr; Träger Hand in Hand mit dem Kran, der Stapel nur Puffer, Nachlauf höchstens etwa eine Stunde, dann ist der Steg leer und die Ware im Stapelhaus; dazu ein Vorarbeiter, der beaufsichtigt und im Dunkeln eine Laterne trägt. | 6.11.2026 |
| Fahrplan | Der Fahrplan des Zierschiffs zog wörtlich aus der Darstellung in den Kern (`Zierfahrplan`): reine Funktion von Minute und Fahrzeit, Streuzahl der Fensternummer, ohne gemeinsamen Zufall. Test gegen die alte Rechnung über 40 Jahre (jede Dreiviertelminute, Fahrzeit 20/35/70); Bild vorher und nachher gleich. | 6.11.2026 |
| Wer | 2–4 Träger je Besuch (fest aus Tag und Fensterhälfte), aus der Rolle `rolle` (Tagelöhner) nach einem Rang aus Nummer, Tag und Fensterhälfte – jeden Besuch andere. Vorarbeiter: der älteste freie Tagelöhner (meist derselbe, „Hafenmeister“). Frei ist, wer ruht, flaniert, einkauft oder (bei Arbeitsort Markt) arbeitet; wer einkehrt oder betet, geht dorthin und wird abgelöst: Der Neue eilt herbei (Abmarsch zur vollen Stunde, kürzester Weg), der Abgelöste bleibt, bis er da ist, plus eine Runde, und ist vor der nächsten vollen Stunde am neuen Ort. Ein neuer Vorarbeiter kommt möglichst nicht aus den Trägern. Prüfung (Opus): Befunde zu Ablösung, Wegen über die volle Stunde, Laden zur vollen Stunde und Arbeitsort Hafen behoben. | 6.11.2026 |
| Wirtschaft | Keine Wirkung: Nur Ort (`Stadtort.Hafen`, Stelle „Stapel“ bzw. „Stegkopf“) und Weg ändern sich; `OhnePlan` bildet den Hafen auf den Ort ohne Hafenarbeit zurück, damit der Generator der Bürger dieselben Würfe macht. Test: Vermögen aller Häuser und der ganze Spielstand ohne Orte und Wege gleich, Köln und Greybox, drei Jahre. Kein Lohn, keine Ware. | 6.11.2026 |
| Lohn | Kein Lohn, reine Darstellung (Nutzer 06.11.) | 6.11.2026 |
| Daten, Spielstand | Eigener Abschnitt `buerger.hafenarbeit` (`rolle`, `traegerVon` 2, `traegerBis` 4, `stuecke` 18, `runde` 12), keine Datenversion; ohne Abschnitt keine Hafenarbeit (Stapel wie seit #332). Kein neues Feld im Spielstand: Ort, Stelle und Weg der Einwohner reichen, `Stadtort.Hafen` ist ein neuer Enum-Wert am Ende (als Text gespeichert). | 6.11.2026 |
| Zeitraffer | Ein Gang (Stapel – Lagerhaus und zurück, etwa 2 × 16 m) dauert 12 Spielminuten, bei Tempo 1 gut neun Sekunden: Die Träger eilen wie der Kran im Zeitraffer. Echtes Gehtempo bräuchte etwa 30 Spielminuten je Gang und ginge weit über die Liegezeit hinaus. Abgelegt wird am Lagerhaus des Hafenmodells; das Stapelhaus der Kölner Karte steht 82 m entfernt und ist nicht das Ziel. | 6.11.2026 |
| Bündel (PR „Hafen-Feinschliff“, von Darstellung) | Der Kran hebt je Hub ein Bündel von `Zierfahrplan.HubGroesse` = 3 Stücken (6 Hübe statt 18, damit das Kranhaus langsam schwenken kann). `Hafenarbeit.Planen`: Die Stücke eines Bündels setzt der Kran zugleich ab bzw. hebt sie zugleich (`AufStapel`/`VomStapel` gleich, `nimmt`/`ab` je Stück 0,05 min versetzt); die Träger tragen weiter einzeln. Hübe aus `Zierfahrplan.HubAnteil`: Entladen 2–88 % der Liegezeit mit kürzerem erstem Hub (`ErsterHubGewicht` 0,5, sonst läge der Nachlauf bei zwei Trägern bei 59 min), Beladen 8–90 % in gleichen Hüben. Beladen wählt den Träger mit den wenigsten Gängen, dann den am spätesten gebrauchten: Gänge je Träger 18 / n abgerundet bis aufgerundet, Nachlauf höchstens etwa 55 min. | 7.11.2026 |
| Tests | `BuergerHafenarbeitTests` (10): Fahrplan wie bisher; Hübe schließen lückenlos aneinander; Schichten (2 je Tag, keine im Winter, Gänge ohne Überschneidung und gleichmäßig verteilt, Stapel vor dem Niederländer leer, danach leer, Nachlauf ≤ 57 min); Einwohner am Hafen (n Träger und ein Vorarbeiter, auch vor 8 Uhr, ältester als Vorarbeiter, kommt zuerst und geht zuletzt, danach niemand); Wirtschaft bitgleich; in jeder Minute des Tragens alle da und Wege vom Hafen vor der nächsten vollen Stunde fertig (drei Startwerte); Rolle Knecht holt niemanden von der Arbeit; Spielstand mitten in der Schicht und zur vollen Stunde; ohne Abschnitt; Datenprüfung. | 6.11.2026 |

### Marktrunde der Streife (PR #…)

| Thema | Entscheidung | Datum |
|---|---|---|
| Befund | In Köln war die Marktrunde der Streife vom Wachhaus am Heumarkt nur 5 m lang. Die Runde ging von der Tür zum Knoten des Gassennetzes im Umkreis Halb + 25 m um den Platz, der von der Tür aus am kürzesten zu erreichen war (samt Rest bis zur Mitte), und auf demselben Weg zurück. Das Wachhaus (Werkstattplatz an der Rheingasse) hängt 31 m vor der Mitte des Heumarkts an der Rheingasse, also schon im Umkreis (37 m); auf einer geraden Straße zum Platz sind alle Knoten gleich teuer, gewählt wurde der erste, der vor der Tür. Getrennte Teile des Gassennetzes (Vermutung in `darstellung.md`) waren es nicht. | 08.11.2026 |
| Regel | `Marktrunde.Planen` (`game/scripts/Marktrunde.cs`, reine Rechnung ohne Godot, vom Test mitübersetzt): Um den nächsten Platz vier Seiten (Nord, Ost, Süd, West, je nachdem, ob ein Punkt mehr längs oder quer liegt); je Seite der äußerste Knoten auf einem Weg der Karte in einem Ring (15, 35, 60, 90, 130 m über den Rand des Pflasters), nur auf derselben Seite der Mauer wie der Platz. Die Streife geht von der Tür zuerst über den Platz (seine Mitte), dann reihum über die Seiten (beide Richtungen probiert), die Seite nahe der Tür zuletzt, und zurück zur Tür, jedes Stück auf dem kürzesten Weg des Gassennetzes. Über den Platz zuerst, weil ein Wachmann in einer Schicht (zwei Spielstunden, bei Tempo 1 etwa 150 s) nur rund 140 m weit kommt und danach an der Tür neu beginnt (Regel der Darstellung, `StreifeSchichtWeg`): So ist er in jeder Schicht auf dem Markt, auch wenn er die Runde nicht ganz schafft. Genommen wird der engste Ring mit 150–400 m, mindestens drei Seiten und höchstens einem Viertel doppelt gegangenem Weg (`HoechstDoppelt`; ohne diese Grenze ging die Runde im Spiel erst 15 m die Rheingasse hinauf und zurück, weil das Gassennetz an jedem Hofanschluss einen Knoten hat); gibt es keinen, in der richtigen Länge die mit drei Seiten und dem wenigsten doppelten Weg, sonst die kürzeste über 400 m, sonst die längste unter 150 m. Kein Sonderfall für Köln, keine Kartenangabe. Viertel- und Mauerrunde unverändert (fehlen ihre Punkte, geht der Wachmann wie bisher über den Markt, jetzt die neue Runde). | 08.11.2026 |
| Wirkung | Nur Bild (Darstellung, kein Kern, kein Spielstand, kein Zufall). Köln, Wachhaus am Heumarkt: 5 m → 328 m im Spiel (Klicktest, `[streife]`): die Rheingasse hinab über den Heumarkt bis zum Kartäuserwall, an der Mauer nach Westen, die Hohe Straße hinauf, die Bayenstraße nach Osten und die Rheingasse hinab zur Tür. In einer Schicht schafft der Wachmann davon etwa die ersten 90–140 m (über den Heumarkt und an der Mauer entlang). Im Test (Netz des Kerns) 295–351 m je nach Dichte der Knoten, doppelt gegangen 0–14 %; von allen Werkstattplätzen der Stadt 216–327 m. In Deutz (Neumarkt, Straßen nur als Stern ohne Ring) bleibt es beim Hin und Zurück, so lang wie nötig. | 08.11.2026 |
| Tests | `StreifeMarktrundeTests`: Köln, Heumarkt mit Knoten nur an den Stützpunkten, alle 10 m und alle 4 m: 150–400 m, höchstens ein Viertel doppelt, an der Tür begonnen und beendet, jeder Punkt und alle 2 m dazwischen auf einem Weg oder dem Pflaster, mindestens drei Seiten, innerhalb der Mauer (das Tor am Ende der Rheingasse zählt mit); von jedem Werkstattplatz der Stadt mindestens 150 m auf Wegen; Wahl der Haltepunkte fest und unabhängig von der Reihenfolge. Gegenprobe: mit der alten Regel nachgebaut sind die beiden Köln-Tests rot (71 bzw. 83 m im Netz des Kerns; im Spiel waren es 5 m, weil dort der Hofanschluss vor der Tür ein Knoten ist). | 08.11.2026 |

### KI nach Stress (PR „Kern: Charaktersystem Teil 3b – Stress“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Meiden | Ab der ersten Stufe (Stress des Oberhaupts ab 40) lässt `KiRivale.Befehlen` jeden Befehl, der dem Oberhaupt Stress brächte (`Familienbuch.StressDurch(konto, befehl)` > 0) – still, ohne Meldung (sonst käme jede Stunde dieselbe). So heuert ein ehrliches Oberhaupt keine Bande mehr an, ein scheues gibt kein Gastmahl, ein bescheidenes baut kein Stadtpalais, ein gieriges schenkt nicht über 200; Ventile (negativ) bleiben erlaubt. Eine Schleife mit Befehlen gibt es nur in der Lebensweise, und die bricht bei „nein“ ab. | 09.11.2026 |
| Ventil | In der Politikstunde nach allem anderen (`Ventil()` nach `Trainieren`), wenn das Oberhaupt mindestens `stress.kiVentilAb` (20) hat: unter allen Aktivitäten mit dem eigenen Ehepartner und den Ratsherren, die dem Oberhaupt Stress abbauen (nie eine, die es nicht mag), die mit dem meisten Abbau je AP für beide Beteiligten zusammen (`StressVorschau`: Hat der Ehepartner selbst Stress, zählt seiner mit), dann mehr Meinung, weniger Gulden; nur mit voller Reserve danach. Es nutzt nur Aktionspunkte, die nach Familie, Verteidigung, Rat, Gaunerei und Üben noch da sind. Erst gebaut: der Ehepartner nur, wenn er selbst 20 Stress hat, und dann ohne Rückfall auf die Ratsherren (Befund der Prüfung). | 09.11.2026 |
| Werbung | Muss die KI einen Ratsherrn umwerben (`BesteAktivitaet`) und sucht ihr Oberhaupt ein Ventil, nimmt sie zuerst eine Aktivität, die es abbaut („zählt doppelt“: Meinung und Ruhe); sonst wie bisher (was es mag, dann die beste Wirkung). | 09.11.2026 |
| Vorsicht und Pump | Ab „zermürbt“ Raubfurcht × `kiVorsicht` (1,2) über `Familienbuch.KiStressVorsicht` in `Vorsicht` (Eskorte, Schiffe). „Nicht mehr auf Pump“ ändert nichts: Die KI nimmt keine Kredite (Balancing-Bericht: 0 Kredite in 1.320 Partien). | 09.11.2026 |
| Tests | `KiStressTests` (3): angespannt meidet Bande (ehrlich) und Gastmahl (scheu), ein Ventil bleibt erlaubt; Gegenproben verschlagen und ruhig. Mit 30 Stress zecht ein geselliges Oberhaupt in der Politikstunde („sucht Ruhe vom Stress“, Eintrag „mag es (gesellig)“), mit 15 nicht. Zermürbt: `ErwarteteFahrtkosten` mit Vorsicht × 1,2, angespannt wie vorher. | 09.11.2026 |

### KI nach Geheimnissen (PR „Kern: Charaktersystem Teil 3c – Geheimnisse“, vom Kern mitgebaut)

| Thema | Entscheidung | Datum |
|---|---|---|
| Gegen Häuser | `Geheimnispolitik()` in der Politikstunde nach `Anklagen()`, für jedes nutzbare Geheimnis einer Person eines anderen Hauses (nach Nummer): Ein Haus, über das das Oberhaupt gut denkt (`Verschont`, ab `kiVerschontAb`), bleibt unberührt. Mord immer anzeigen; Ehebruch und Ketzerei anzeigen bei Hass (`Verhasst`), Ketzerei auch mit frommem, beides mit jähzornigem Oberhaupt – nur mit Reserve nach Gebühr und Entschädigung. Sonst gierig oder verschlagen ein Erpresserbrief: `geheimnisse.ki.anteil` (10 %) vom Bargeld des Opfers, auf 50 gerundet, höchstens `hoechstens` (1.500). Ist ein Brief beantwortet und das Wissen noch da (`GeheimnisWissen.BriefIm`, das Opfer hat nicht gezahlt), macht sie die Drohung wahr: Anzeige, wenn es ein Vergehen ist, sonst Bloßstellen. Sonst Bloßstellen, wenn das Haus ihr bei der Wahl im Weg steht (bewirbt sich um dasselbe Amt oder hält es). Sonst schweigt sie. | 09.11.2026 |
| Ratsstimme | `RatsstimmenErpressen()` in `Politik()` zweimal: nach der Bewerbung (vor `FuerWahlWerben`) und beim eigenen Statut-Antrag (vor `FuerStatutWerben`). Für jeden Ratsherrn, dessen Stimme fehlt (`BrauchtWahlstimme`/`BrauchtStatutstimme`: sitzt im Rat, nicht schon erpresst, stimmt bei der eigenen Wahl nicht sicher für sie bzw. beim eigenen Statut nicht dafür), das erste nutzbare Geheimnis – nie mit ehrlichem Oberhaupt, und nur so viele, wie zur Mehrheit fehlen (`FehlendeWahlstimmen`: Mehrheit der Wähler; `FehlendeStatutstimmen`: Mehrheit der besetzten Sitze; erpresste und sichere zählen schon – Befund der Prüfung: jede Erpressung ist eine Spur und kostet Meinung). `WahlKosten` und `FuerStatutWerben` zählen eine eigene erpresste Stimme als sicher (keine Geschenke dafür). Verschlagen und gierig „bevorzugt“ ist damit erfüllt, dass alle außer Ehrlich es tun (einfachere Lösung). | 09.11.2026 |
| Spitzel auf Ratsherren | Fehlt danach noch eine Stimme ohne bekanntes Geheimnis, ein Spitzel auf diesen Ratsherrn (`SpitzelAnsetzen("R:Name")`) – aus dem Budget der Spitzel auf Häuser (höchstens jedes zweite Jahr einer, PR #208; der Ratsherr geht vor, weil `Politik` vor `Anklagen` läuft), nur mit Aktionspunkten für Spitzel und Erpressung und mit Reserve. Erst gebaut: höchstens einer im Jahr – die Probe (`messen geheimnisse 20 5`) zeigte 17 je Partie, weil die meisten Ratsherren kein Geheimnis haben; jetzt 6,8 je Partie (`messen geheimnisse 20 20`), und die Spitzel auf Häuser sinken von 94 auf 68 Gulden im Jahr (ein Betrieb, KI geführt). | 09.11.2026 |
| Messung | Gemessen vom Kern (WIRTSCHAFTSMODELL „Geheimnisse“, main `5a7f6c51` + 3c `09b61b2c`): je Partie (20 Jahre, 80 Partien) 3,4 erpresste Ratsstimmen (74 % der Partien), 6,8 Spitzel auf Ratsherren, 0,8 Briefe (46 %, 0,56 bezahlt), 0,49 Anzeigen (0,35 schuldig), 0,55 Bloßstellen; KI ÷ Bezug 1,14 → 1,05, Berufe 9/10 im Band wie ohne Geheimnisse (Tischler −15,3 %). Ketzerei-Regel (Entscheidung des Nutzers): `GeheimnisSchaden` und die Anzeige folgen `Geheimniswesen.KeinVergehen`; kaum Wirkung, weil in KI-Partien selten ein Stadtglaube gilt. | 09.11.2026 |
| Opfer | `Unterweltschutz` zahlt einen Brief mit Geheimnis, wenn der Betrag unter `GeheimnisSchaden(nummer)` liegt und die halbe Reserve bleibt: das Schlimmere aus Anzeige (Buße der Art, gedeckelt wie am Gerichtstag, + Pranger 500 + Kerker 1.500 + drei Jahre Amtseinkommen – nur bei einem Vergehen – `Geheimniswesen.KeinVergehen`, Ketzerei nur mit Stadtglauben und Bekenntnis des Inhabers zu ihm) und Bloßstellen (Ansehen × `guldenJeAnsehen` (100) × Jahre der Schande). Angezeigt, rechnet `Verteidigen` Buße und Kerker des Geheimnisses zum Nutzen eines Freispruchs (Befund der Prüfung; Mord mit 25 Beweiskraft ist ohnehin nicht zu kippen). | 09.11.2026 |
| Tests | `KiGeheimnisTests` (4): Mord angezeigt und verurteilt, verschont ein Haus mit Meinung +100 (Gegenprobe); gierig ein Brief (≤ 1.500), still schweigt (Gegenprobe), nach der Ablehnung wird es öffentlich; Ratsstimme beim eigenen Antrag (verschlagen ja, ehrlich nein) und Spitzel auf den nächsten Ratsherrn ohne Wissen; als Opfer 100 Gulden gezahlt, 9.000 nicht. | 09.11.2026 |
### Vorarbeiter und Einrichtung nach jeder Wirkung (PR „Kern: Personal-Nacharbeit …“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Grundsatz | Die KI rechnet weiter nur mit dem Kern (`Personalwesen.MehrgewinnJeJahr`, `Werkstattwesen.Amortisationsjahre`); seit Datenversion 79 (`werkstatt.jedeWirkungRechnen`) bringen dort auch Lager, Fänge, Spuren, Einbruch, Hehlerei, Razzia, Streife und Geleit Gulden (kern.md, „Personal-Nacharbeit“). Der Gleichstand „erste der Wahl“ (PR #224) kommt dadurch seltener vor: Ein Meisterdieb ohne Einbrüche wird eher „Vorsichtig“ oder – wenn fremde Wachen Razzien machen – „Verschwiegen“ statt „Einbrecher“ (0). | 09.11.2026 |
| Durchfluss | `KiRivale.Durchfluss` ruft `Werkstattwesen.Durchfluss` (dieselbe Formel, jetzt im Kern; bitgleich). | 09.11.2026 |
| Lager nicht doppelt | `EigenschaftWert` im gebremsten Umland (ab `StillstandFuerKarrenkauf`, Fahrzeit nach Weglänge) rechnet das Lager wie bisher selbst und zieht den Lagerteil des Kerns (`Werkstattwesen.LagerMehrgewinn`) ab; ohne den Schalter ist er 0, also bitgleich. Werkstätten und das übrige Umland nehmen den Kernwert. | 09.11.2026 |
| Wache | Wachhunde, Kettenhemden, Streifengänger und Geleitmann bringen dem eigenen Haus kaum Gulden (sie schützen alle) – die KI kauft sie fast nie; Hellebarden nur mit viel Kopfgeld aus Urteilen nach eigenen Razzien. Entscheidung des Nutzers über den Teamleiter (9.11.2026): so lassen (Variante A). | 09.11.2026 |
| Tests | `KiPersonalNacharbeitTests` (3): Ein KI-Dieb kauft zum Jahresende die Lederhandschuhe, wenn eine fremde Wache Razzien macht (bezahlt in unter 4 Jahren); allein in der Stadt und ohne Schalter kein Stück; „Vorsichtig“ und „Verschwiegen“ bringen Gulden, „Hehlerfreund“ und „Einbrecher“ ohne Diebesgut nicht, ohne Schalter alle 0; im gebremsten Kölner Holzschlag gilt „Ordnungsliebend“ der KI mit und ohne Schalter gleich viel (Lager nicht doppelt). `KiUmlandTests`: der Kern rechnet „Ordnungsliebend“ im gebremsten Holzschlag jetzt selbst, die KI bleibt bei ihrer Rechnung. | 09.11.2026 |

### Bauort nach dem Hafen (PR „Spiel-KI: Bauplatzwahl berücksichtigt den Hafen“)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Offener Punkt „Hafenziel“ aus `kern.md`: Seit Datenversion 73 fährt ein Karren an den Hafen, wenn der offen ist und Hin- plus Rückfahrt dorthin kürzer dauern als zum Marktplatz (`Stadtwirtschaft.FaehrtZumHafen`); gehandelt wird dort mit demselben Markt. Die KI suchte Bauplätze nur unter den 20 nächsten am Markt und rechnete nur die Fahrt dorthin – in den Messungen fuhr kein Karren der KI zum Hafen (0 % auf Greybox und Köln). Auftrag des Teamleiters (9.11.2026): Bauplatzwahl um die Fahrzeit zum Hafen ergänzen, Winter und „Hafen ruht“ beachten. | 09.11.2026 |
| Regel | `KiRivale.Standort.cs`: Jeder Bauort zählt mit der Rundfahrt, die sein Startkarren im Mittel über die Jahre macht: `ErwarteteRundfahrt` = Markt − `HafenOffenAnteil` × max(0, Markt − Hafen) (Hin + Rück in Minuten, Fahrzeit am Bauort vom Kern: `FahrzeitAmBauort`, `FahrzeitZumHafenAmBauort`). Strikt kürzer wie im Kern, bei Gleichstand der Marktplatz. Gewählt wird das Minimum, bei Gleichstand der frühere Kandidat. | 09.11.2026 |
| Winter, Hafen ruht | `HafenOffenAnteil` = 3/4 mit Jahreszeiten (jedes Spieljahr ist eine Jahreszeit; im Winter ist der Hafen zu, Eis gibt es nur im Winter), ohne Jahreszeiten 1. „Hafen ruht“ (`schiffshandel.aktiv: false`) ändert nichts: Die Karren fahren trotzdem zum Hafen. Ohne Hafen oder mit `karrenZumHafen` aus ist `FahrzeitZumHafenAmBauort` null – dann wie vorher. | 09.11.2026 |
| Kandidaten | Zu den 20 nächsten Bauorten am Markt (Luftlinie, wie bisher) kommen die 20 nächsten am Hafen (Luftlinie zur Mitte des Hafengebäudes, bei Gleichstand in der Reihenfolge nach dem Markt), aber nur die, an denen der Hafen der nähere Handelsort ist. Sonst nähme die KI auch Orte, die nur zufällig einen kürzeren Weg zum Markt haben als die 20 nach Luftlinie – eine Änderung außerhalb des Auftrags; so bleibt die Greybox (an keinem Bauort ist der Hafen näher) bitgleich. | 09.11.2026 |
| Waren (Designfrage) | Nicht nach den Waren des Betriebs (Ein- und Ausfuhr) gewichtet: Am Hafen gilt derselbe Markt, der Landhandel bei ruhendem Schiffshandel wirkt auf den gemeinsamen Markt, und Schiffe laden ortsunabhängig aus dem Lager – die kürzere Fahrt hilft jeder Ware gleich. Dem Teamleiter als Designfrage gemeldet (Empfehlung: so lassen; ein echter Vorteil für Hafenwaren wäre eine Regel des Kerns). Abgelöst durch die Entscheidung des Nutzers „B“, siehe „Aufschlag am Hafen“. | 09.11.2026 |
| Meldung | „… der Handkarren braucht 65 Minuten zum Hafen (im Winter 117 zum Markt)“ statt „… Minuten zum Markt“, wenn der Karren des gewählten Orts zum Hafen fährt (ohne Jahreszeiten „(117 zum Markt)“). | 09.11.2026 |
| Prüfung | Unabhängiger Prüfer (Opus) über `0db497be`/`836ae970`: 0 hoch, 0 mittel, 5 niedrig. Umgesetzt: Orte am Hafen fragen zuerst den Hafen (ohne Weg dorthin keine Suche zum Markt), Meldung mit „im Winter“, Test prüft die Orte am Hafen nach den Abfragen des Kerns statt nach der eigenen Rechnung. Als Offen notiert: Fahrzeiten der Jahreszeit der Entscheidung, Karrenkauf und Umland nur mit dem Markt. | 09.11.2026 |
| Messung | Kaum Wirkung, Greybox und Probestadt bitgleich, Köln: ein KI-Haus erreicht den Hafen erst, wenn die schnellen Bauorte am Markt vergeben sind (Fahrten der KI-Häuser zum Hafen nach 20 Jahren 0,1 %, nach 40 Jahren 0,5 %; Vermögen im Rauschen). Einzelheiten unter „Messungen“. | 09.11.2026 |
| Tests | `KiHafenStandortTests` (3): erwartete Rundfahrt (3/4, ohne Jahreszeiten 1, Gleichstand und weiterer Hafen = Markt) mit Anker im Kern (`HafenOffen` in drei von vier Jahren); Fahrzeit zum Hafen am Bauort = Kern nach dem Bau (alle Karrenarten), Greybox und Messstadt wählen wie die alte Regel; Köln, die jeweils gewählten Orte am Markt mit Gießereien von „Haus Bau“ belegt, bis die Wahl am Hafen liegt (in der Probe 9): die KI baut am Rheinhafen, ihr Karren fährt im Sommer dorthin, Gegenprobe ohne `karrenZumHafen` baut am Markt mit längerer Fahrt und fährt zum Marktplatz. Mit der alten Regel sind alle drei rot. Der Köln-Test (6 s) steht in `LangsameTests.cs`. | 09.11.2026 |

### Aufschlag am Hafen (Hafen v2, gleicher PR; Kern-Regel im Auftrag des Teamleiters)

| Thema | Entscheidung | Datum |
|---|---|---|
| Anlass | Antwort auf die Designfrage „Waren“: Nutzerentscheidung „B“, aber „nicht allzu viel“. Ziel: Der Hafen lohnt sich spürbar, aber es darf nicht dazu kommen, dass alle Häuser nur noch am Hafen bauen. Die Kern-Regel hat der Teamleiter ausdrücklich an spiel-ki vergeben (sonst Sache von kern; Hinweis in `kern.md` unter „Offen“). | 09.11.2026 |
| Regel (Kern) | Verkauft ein Karren eine Ausfuhrware direkt am Hafen, zählt der Erlös × (1 + `schiffshandel.hafenAufschlag`): `Schiffahrt.HafenVerkaufsFaktor(ware)` in `Karren.Ankommen` – nur mit `ZielHafen`, Karren zum Hafen und offenem Hafen zur Verkaufsminute (wer nach Wintereinbruch ankommt, bekommt nichts). Gilt für den Verwalter und den Handelsauftrag, über `Betrieb.Verkaufen` auch für die Steuer; Preis und Vorrat des Markts bleiben. Schiffe und Handel ohne Karren bekommen keinen. Wohin der Karren fährt, entscheidet weiter nur die Zeit. | 09.11.2026 |
| Ausfuhrware | `SchiffshandelDaten.IstAusfuhrware`: eine ferne Stadt kauft sie (`staedte[].kauft`) – genau die 17 Waren der Nachfrage im Landhandel (Test). Keine eigene Liste in den Daten. | 09.11.2026 |
| Wert, Daten | 3 % (Vorschlag des Teamleiters), Datenprüfung 0–5 % (Grenze des Nutzers); ohne Angabe 0, alte Spielstände rechnen mit ihren Daten wie bisher. Datenversion 78 → 79 → 80 (main stand zuletzt auf 79 durch Personal, #401; dieser Branch bekam daher 80), Versionsprüfungen der Tests angepasst. | 09.11.2026 |
| KI | Die Fahrt zum Hafen zählt in `ErwarteteRundfahrt` um 1 + Aufschlag × `Ausfuhranteil(art)` kürzer (gleiche Annahme wie beim Karrenvergleich: je Fahrt eine Ladung; bringt sie am Hafen 3 % mehr, wiegt das wie eine um 3 % kürzere Fahrt). `Ausfuhranteil`: 1, wenn das einträglichste Produkt der neuen Werkstatt (wie `Kalkulation.ErwarteterJahresgewinn`) Ausfuhrware ist, sonst 0; die Schänke 0, weil sie den Großteil ausschenkt (Befund der Prüfung). Ohne Aufschlag bitgleich mit v1. | 09.11.2026 |
| Tests | Neu `HafenAufschlagTests` (3): Daten (80, 3 %, Ausfuhrwaren = Landhandel-Nachfrage, Prüfung 0–5 %, ohne Feld 0); Silberring am Hafen im Sommer Umsatz und Steuer genau × 1,03, Beschlag gleich, am Marktplatz gleich, Handelsauftrag × 1,03; Aufbruch im Herbst und Ankunft im Winter ohne, Herbst 9 Uhr mit Aufschlag. Gegenproben: Faktor immer 1 bzw. ohne Prüfung von Ware und Winter – je zwei Tests rot. `KiHafenStandortTests` um einen Test ergänzt (Formel mit 5 % und Anteil ½, ohne Aufschlag wie vorher, Anteil fest: Gießerei 1, Tischlerei, Siederei, Schänke 0; Kandidaten in Köln am Hafen um genau den Aufschlag kürzer, die übrigen gleich). `Hafenmessung`: `--hafenaufschlag=x`, neue Werkstätten am Hafen, Umsatz. | 09.11.2026 |
| Prüfung | Unabhängiger Prüfer (Opus) über `d3498c54`: 0 hoch, 2 mittel, 5 niedrig. Umgesetzt: Doku (Spielbuch, ARCHITEKTUR, WIRTSCHAFTSMODELL, hier), Schänke mit Anteil 0, fester Test mit Anteil 1, Handelsauftrag im Test. An Oberfläche gemeldet (`oberflaeche.md`, Offen): Hafentafel sagt noch „dieselben Preise“, Aufschlag und Ausfuhrwaren sind für den Spieler unsichtbar. Als Näherung belassen: Erlös-Vorschauen ohne Aufschlag (`kern.md`, Offen). | 09.11.2026 |
| Ergebnis | Gemessen (unten, main `5e61a1c0`): Mit 0, 3 und 5 % wählt die KI in Köln dieselben Bauorte (KI-Häuser: 7 von 104 neuen Werkstätten am Hafen in 20 Jahren, 4 von 54 in 40 Jahren), Fahrten zum Hafen 0,3 bzw. 1,0 %, Vermögen im Rauschen; Greybox, Probestadt und Messstadt bitgleich. Weit unter „die Hälfte am Hafen“ – kein Grund, den Aufschlag zu senken. „Spürbar“ ist er aber nicht: Er wirkt nur für die wenigen Werkstätten, die ohnehin am Hafen stehen (Empfehlung unter „Offen“). | 09.11.2026 |

## Messungen

Neue Messungen der Spiel-KI stehen hier (mit main-Commit), nicht mehr in `docs/WIRTSCHAFTSMODELL.md`. Ältere: dort bis §42 (Taschendiebe, PR #69).

### Karren und Bauort nach dem Karrenweg (PR #282)

Gemessen auf main `880e3387` und phase5/gehtempo `dd347586` („vorher“) + dieser Branch (`5314f99d`; `ad482322` ändert nur den Tausch in der Not), `messen bericht 20 20 --teile=5 --karte=greybox`. KI-Stärke: Der Bezugsspieler (Schmied, Mensch) wird von der KI geführt; Reinvermögen nach 20 Jahren, Mittel ± Fehler des Mittels:

| Greybox | main | vorher | nachher |
|---|---|---|---|
| Normal: Bezugsspieler | 68.066 ± 3.060 | 56.004 ± 2.730 | 62.268 ± 2.434 |
| Normal: KI-Häuser | 78.461 ± 2.826 | 69.017 ± 2.921 | 74.507 ± 2.966 (−5,0 % gegen main) |
| Normal: Median KI ÷ Bezug, davongezogen | 1,08, 3/60 | 1,14, 9/60 | 1,11, 3/60 |
| Schwer: Bezugsspieler | 68.162 ± 1.821 | 46.818 ± 2.960 | 63.499 ± 2.890 |
| Schwer: KI-Häuser | 119.993 ± 2.666 | 110.814 ± 2.747 | 116.029 ± 2.112 (−3,3 %) |
| Schwer: Median, davongezogen | 1,81, 14/60 | 2,27, 43/60 | 1,83, 24/60 |
| je Haus Normal (Eisenbart, Eichwald, Kieselbach) | 86.760, 63.364, 85.260 | 79.036, 56.492, 71.524 | 86.999, 60.847, 75.675 |

Kein Bankrott. Woher der Rückgang kam (Greybox, Normal, 4 Startwerte, Jahr 15, je KI-Werkstatt): vorher lagen 7 von 23 Werkstätten über 60 min vom Markt (gebaut „an der Ringgasse“ und „am Felsenhang“, 99–174 min, drei Handkarren, 44 % Stillstand, Gewinn Ø 435 statt 4.072); mit Karrenwahl allein 31 % Stillstand, Gewinn 621; mit Bauort nach Karrenweg nur noch 2 weite, alle Werkstätten zusammen 73.613 (vorher 68.197, main 73.025). Einzelne Versuche (8 Startwerte, Greybox): immer Pferdekarren im Umland KI Schwer +1,8 %, Ochse und Tausch +1,3 %, immer Handkarren −10 %. **Köln** (Teil 5, 20 Startwerte) lief bei Abgabe noch (Rechner mit Last um 30); nachtragen.

### Umland nach dem Karrenweg (PR #286)

Gemessen auf main `ec36ff33` + dieser Branch (`97001082`, mit den Häusern in Deutz), `messen bericht 20 20 --teile=5 --karte=koeln|greybox --parallel=3`, 20 Startwerte, Reinvermögen nach 20 Jahren (Mittel ± Fehler). Keine Partie mit Fehler, kein Bankrott, kein Haus erloschen.

| | main ec36ff33 | Branch |
|---|---|---|
| Köln Normal: ausbauender Spieler (Bezug) | 56.503 ± 3.987 | 54.240 ± 4.070 |
| Köln Normal: KI-Häuser | 65.793 ± 1.576 | 66.805 ± 1.782 (+1,5 %) |
| Köln Normal: Median KI ÷ Bezug, davongezogen | 1,18, 15/140 | 1,26, 16/140 |
| Köln Schwer: Bezug / KI-Häuser | 77.280 / 95.778 | 79.314 / 100.162 (+4,6 %) |
| Köln Schwer: Median, davongezogen | 1,24, 18/140 | 1,23, 25/140 |
| Köln Umland Normal: Betriebe, Gewinn je Jahr, Stillstand | 287, 1.322, 16 % | 265, 1.405, 13,6 % |
| Köln: Holzschlag im Eichenforst gepachtet (Betriebsjahre, Normal / Schwer) | 230 / 291 | 12 / 0 |
| Greybox Normal: Bezug / KI-Häuser, davongezogen | 62.268 / 74.507, 3/60 | 61.749 / 75.344, 4/60 |
| Greybox Schwer: Bezug / KI-Häuser, davongezogen | 63.499 / 116.029, 24/60 | 61.613 / 117.546, 25/60 |

Die KI-Häuser in Köln gewinnen etwas (Normal innerhalb des Fehlers, „Schwer“ +4.400), das Ziel 75.482 (alter main, Teil 5, 10 Startwerte) erreichen sie nicht: Die Umlandbetriebe der KI-Häuser liegen auf der Karte 220–290 Minuten vom Markt, ihr Stillstand bleibt (Holzschläge 21–29 %), und der Gewinn je KI-Haus aus dem Umland in 20 Jahren bleibt bei ≈ 26.400 (alter main 32.971). Was die Änderung bringt, ist das Meiden des Eichenforsts (75 Gulden im Jahr) und eine bessere Wahl der Liegenschaften (Umland 1.405 statt 1.322 je Jahr, Neubauten +2.600 je Haus). „Schwer“: 25 statt 18 von 140 KI-Häusern ziehen davon (> 2× Bezug) – der Bezugsspieler schwankt dort stark (± 7.365). Der Rest ist Karte (Lage des Umlands) oder Kern (Höchstdauer, Karren). Messdateien im Notizordner (`…/scratchpad/deutz-ki/mess/`).

### Karren-Höchstzahl: Umland bis 5 Karren (PR #286)

Gemessen auf main `ec36ff33` + dieser Branch (`adfd5f2d`), wie oben (`messen bericht 20 20 --teile=5`, 20 Startwerte; die Messdatei nennt als main `4220c631`, den Stand von origin/main zur Messzeit – der Branch ist darauf nicht neu aufgesetzt). „Vorher“ = derselbe Branch mit 3 Karren (`97001082`). Keine Partie mit Fehler, kein Bankrott.

| | main ec36ff33 | 3 Karren | 5 Karren im Umland |
|---|---|---|---|
| Köln Normal: Bezug / KI-Häuser | 56.503 / 65.793 | 54.240 / 66.805 | 55.547 / 66.339 |
| Köln Normal: davongezogen | 15/140 | 16/140 | 11/140 |
| Köln Schwer: Bezug / KI-Häuser | 77.280 / 95.778 | 79.314 / 100.162 | 78.584 / 99.893 |
| Köln Schwer: davongezogen | 18/140 | 25/140 | 26/140 |
| Köln Umland Normal: Gewinn je Jahr, Stillstand | 1.322, 16 % | 1.405, 13,6 % | 1.427, 11,7 % |
| Holzschlag Hardevust / Kleingedank, Stillstand Normal | 28,1 / 23,8 % | 28,8 / 23,3 % | 20,4 / 19,9 % |
| dieselben, Stillstand Schwer | 26,8 / 22,2 % | 25,8 / 21,5 % | 16,6 / 16,9 % |
| Holzschlag Hardevust / Kleingedank, Gewinn je Jahr Normal | 705 / 839 | 799 / 871 | 980 / 938 |
| Greybox Normal: Bezug / KI-Häuser, davongezogen | 62.268 / 74.507, 3/60 | 61.749 / 75.344, 4/60 | 61.592 / 75.071, 6/60 |
| Greybox Schwer: KI-Häuser, davongezogen | 116.029, 24/60 | 117.546, 25/60 | 117.502, 25/60 |

Die Holzschläge stehen mit dem vierten (und fünften) Karren gut 8 Punkte weniger still und verdienen mehr; die KI kauft die Karren nur nach Stillstand. Das Vermögen der KI-Häuser ändert sich im Rahmen des Fehlers (± 1.700), „Schwer“ zieht die KI nicht öfter davon als mit 3 Karren. Ein erster Lauf mit der Schätzung beim Pachten für 5 Karren pachtete den Holzschlag im Eichenforst wieder (204 statt 12 Betriebsjahre, 43 % Stillstand) und ließ in „Schwer“ 34 von 140 KI-Häusern davonziehen; deshalb schätzt die KI das Pachten weiter mit 3 Karren (Befund H1 der Prüfung). Die Nachtfahrt ist damit nicht nötig.

### Ware der Schänke nach dem Ausschank – Kurzprobe (PR #292)

Gemessen auf main `6f80481b` + Branch phase5/jahreszeiten `e666edf6`, nur Köln, nur Wirt, **4 Startwerte** (Richtungsprobe, Fehler des Mittels weit über den Unterschieden): `messen jahreszeiten 20 4 --karten=koeln --beruf=Wirt --parallel=4` mit/ohne `--ohne-jahreszeiten` und `--ki-schaenke[=0.75]`. Reinvermögen J20 des Spielers, Gewinn der Betriebe (ohne Schiffe) über 20 Jahre, hergestellt je Jahr (Körnermahlzeit / Dünnbier / Starkbier):

| | ohne Jahreszeiten | mit Jahreszeiten | Δ |
|---|---|---|---|
| vorher (ohne `ki.schaenke`) | 39.694, Betriebe 53.577, 35 / 14 / 16 | 25.677, Betriebe 48.551, 54 / 4 / 8 | −35,3 % |
| `--ki-schaenke` (Anteil 1) | 38.476, Betriebe 49.884, 67 / 0 / 5 | 27.608, Betriebe 47.700, 70 / 0 / 2 | −28,2 % |
| `--ki-schaenke=0.75` | – | 30.676, Betriebe 49.962, 57 / 0 / 10 | – |

Mit Anteil 1 sieht die KI fast jede Ausschankware zum Ausschankpreis; je Arbeitsminute ist dann die Körnermahlzeit am besten, und die Schänke macht fast nur noch sie – der Gewinn der Betriebe sinkt (−2 / −7 %), weil der Überschuss doch zum Markt geht (bewirtet 55 von ≈ 70 Stück im Jahr, 16 Gäste je Jahr abgewiesen, weil der Karren alles über dem Vorrat von 15 mitnimmt). Mit 0,75 bleiben Starkbier und Marktanteil, die Betriebe verdienen mehr als vorher (+3 %). Der Abstand im Reinvermögen hängt stark an den Schiffen (je Lauf −0,8 bis +7,4 Tsd.) – erst die volle Messung entscheidet.

Befehle für den Grafik-PC (80 Startwerte, beide Karten, alle Aufbauten; jeweils auf demselben Stand):

```bash
P="dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen jahreszeiten 20 80 --karten=greybox,koeln --parallel=12"
$P --ohne-jahreszeiten                        --datei=schaenke-ohne.json        # Bezug ohne ki.schaenke (= Endmessung)
$P                                            --datei=schaenke-mit.json
$P --ohne-jahreszeiten --ki-schaenke          --datei=schaenke1-ohne.json
$P                     --ki-schaenke          --datei=schaenke1-mit.json
$P --ohne-jahreszeiten --ki-schaenke=0.75     --datei=schaenke75-ohne.json
$P                     --ki-schaenke=0.75     --datei=schaenke75-mit.json
```

### Bürgerdichte und Tagesplan (PR #323)

Gemessen auf main `3518c72f` + dieser PR: `messen buerger 20 8` (Köln und Greybox, alle KI-Häuser, Spieler KI-geführt: Schmied und Wirt mit einem Betrieb, Ausbau = Schmied wie ein KI-Haus; „alt“ = Abschnitt `buerger` ohne `jeHausplatz` und `tagesplan`). Reinvermögen Jahr 20 (± Fehler des Mittels), Jahr bis 10.000 Gulden:

| Karte | Aufbau | alt | neu | Bürger Ø Jahr alt / neu |
|---|---|---|---|---|
| Greybox | Schmied | 36.804 (± 3.008) | 36.804 (± 3.008) | 9,1 / 9,1 |
| Greybox | Wirt | 35.668 (± 2.574) | 35.668 (± 2.574) | 8,6 / 8,6 |
| Greybox | Ausbau | 63.712 (± 3.496) | 63.712 (± 3.496) | 12,3 / 12,3 |
| Köln | Schmied | 32.691 (± 2.962) | 32.691 (± 2.962) | 9,0 / 9,0 |
| Köln | Wirt | 28.110 (± 2.298) | 28.110 (± 2.298) | 9,0 / 9,0 |
| Köln | Ausbau | 63.234 (± 9.569) | 63.234 (± 9.569) | 10,6 / 10,6 |

Wert für Wert gleich, auch KI-Häuser, Marktfaktor und Warenkorb. Zur Einordnung die verworfenen Fassungen (Köln, Schmied / Wirt): alle zählen 38.248 / 32.571, dazu anteiliges Gewicht im Warenkorb 37.648 / 32.127, feste Auswahl ohne eigenen Generator 36.676 / 29.631; Gegenprobe `jeHaus` 50 nach alter Regel 39.163 / 30.929, `jeHaus` 26 32.462 / 29.907 (Rauschen eines anderen Laufs der Bürger: je Startwert bis ± 12.000).

Einwohner und wer zur halben Stunde in der Stadt zu sehen ist (Markt, Gasse, Tor, unterwegs in der Stadt; Mittel über 8 Startwerte × 3 Aufbauten × 20 Tage):

| Karte | | Einwohner | 8 Uhr | 10 Uhr | 12 Uhr | 15 Uhr | 18 Uhr | 20 Uhr | 22 Uhr | davon am Markt 10 / 15 / 18 Uhr |
|---|---|---|---|---|---|---|---|---|---|---|
| Köln | vorher | 223 | 206 | 122 | 127 | 123 | 151 | 123 | 94 | 39 / 35 / 35 |
| Köln | nachher | 418 | 387 | 268 | 240 | 264 | 300 | 240 | 177 | 153 / 119 / 97 |
| Greybox | vorher | 123 | 114 | 65 | 65 | 66 | 77 | 58 | 27 | 20 / 17 / 21 |
| Greybox | nachher | 167 | 155 | 105 | 97 | 105 | 115 | 92 | 36 | 59 / 49 / 48 |

Rechenzeit Kern (Köln, ein Lauf über 6 Jahre, eingeschwungen): 2,7 s gegen 2,8 s, also rund 0,9 ms mehr je voller Stunde (die Planung), im Mittel 0,015 ms je Takt.

Bilder (Vulkan auf der Grafikkarte, Pixel-Look, Köln, Alter Markt am Dom, Zoom 35 und 70, Tempo 2, vier Spielstunden Vorlauf; Ordner im Scratchpad des Auftrags, `buerger/`): Figuren im Bild bei Zoom 35 etwa 9 Uhr 11 → 10 (die Darstellung holt nach dem Start noch Wege nach), 12 Uhr 22 → 45, 18 Uhr 8 → 30, 19 Uhr 5 → 35, 22 Uhr 0 → 15 (Nachzügler mit Laterne, siehe darstellung.md). Der Heumarkt liegt am Südrand: Wer dort wohnt, geht zum nächsten Markt, die meisten zum Alten Markt.

Darstellung (Köln, 12 Uhr, Zoom 70, Tempo 1, NAS mit RTX 2000): Passanten 5,4 → 10,8 ms je Bild, mit der Sparstufe ab 300 Einwohnern 9,6 ms; Bildzeit 46 → 76 ms, mit Sparstufe 64 ms. **Das Ziel „höchstens +2 ms“ ist in der Darstellung nicht erreicht** – Hebel siehe `docs/bereiche/darstellung.md`, „Offen“; wer es leichter will, senkt `buerger.jeHausplatz` (wirkt nicht auf die Wirtschaft).

### KI nach Stress (PR „Kern: Charaktersystem Teil 3b – Stress“, vom Kern gemessen)

Gemessen auf main `432ceee2` + `a3ed79df` (Einzelheiten in `kern.md` und WIRTSCHAFTSMODELL „Stress“): KI ÷ Bezug (Median) 1,15 → 1,13, „Schwer“ 1,83 → 1,80, Gaunerei der KI 0,45 / 0,80 / 0,17 → 0,44 / 0,79 / 0,17 je Jahr, Karren in Not 0 → 1. `messen stress 20 30`: Die Oberhäupter sind in 99,8 % der Jahre ruhig, keines bricht zusammen; Ventile etwa 7 je 100 Oberhaupt-Jahre (Abbau zusammen etwa 110), keine Aktivität, die das Oberhaupt nicht mag. Gemieden wird darum selten – die KI kommt kaum über 40. Nachtrag mit den Quellen von außen (main `047cabd9` + `1961140a`, `messen stress 40 20`): KI ÷ Bezug 1,14, „Schwer“ 1,85, Gaunerei 0,44 / 0,77 / 0,17; in 16,3 % der Oberhaupt-Jahre wird es angespannt oder mehr (vorher 2,6 %), je Leben gut 4 Jahre angespannt, 23 % der Oberhäupter einmal zermürbt, 2,8 % brechen zusammen. Die Ventile kommen damit öfter zum Zug (Abbau zusammen etwa 450 je 100 Oberhaupt-Jahre); das Meiden ab 40 bleibt wie gebaut.

### Bauort nach dem Hafen (PR „Spiel-KI: Bauplatzwahl berücksichtigt den Hafen“)

Gemessen auf main `d9c96643` (vorher) gegen main `d9c96643` + dieser Branch (`0db497be`; die Nacharbeit nach der Prüfung ändert keine Wahl, nur Rechenweg und Meldung), Arbeits-PC mit 24 Fäden, nur CPU, niedrige Priorität.

- **Balancing-Bericht** `messen bericht 20 20` (880 Partien, Greybox, Probestadt, Messstadt): Ausgabe Zeile für Zeile gleich. Berufe im Band 8/10 wie vorher (Tischler −16 %, Parfumeur +15 %), KI ÷ Bezug (Median) 1,00, „Schwer“ 1,71, davongezogen 4/60 bzw. 18/60, Hafen-Abschnitt der Greybox und Probestadt gleich. Auf diesen Karten liegt kein Bauort näher am Hafen, oder die KI kommt nicht so weit.
- **KI-Stärke Köln** `messen bericht 20 20 --teile=5 --karte=koeln` (40 Partien): Normal Bezug 69.655 → 69.498, KI-Häuser 72.337 → 72.351, Median KI ÷ Bezug 1,03 → 1,04, davongezogen 6/140 → 6/140; „Schwer“ Bezug 83.682 → 83.134, KI-Häuser 105.699 → 105.640, Median 1,26 → 1,28, davongezogen 7/140 → 8/140. Kein Bankrott. Im Rauschen.
- **Fahrten zum Hafen** `messen hafen 20 20` (Greybox und Köln, je Schmied, Wirt und Ausbau, 120 Partien): Greybox und Köln mit Schmied bzw. Wirt Zahl für Zahl gleich (zum Hafen 0 %). Köln „Ausbau“: KI-Häuser 0,0 → 0,1 % der Fahrten zum Hafen (Frühling 0,2, Sommer 0,2, Herbst 0,3, Winter 0 %), der KI-geführte ausbauende Spieler 0,0 → 1,2 %; Rundfahrt der KI-Häuser 153,0 → 152,9 min, des Spielers 95,8 → 95,7.
- **40 Jahre Köln** `messen hafen 40 10 --karten=koeln --beruf=Ausbau`: KI-Häuser 0,0 → 0,5 % der Fahrten zum Hafen (Winter 0 %), Spieler 0,0 → 1,6 %; Rundfahrt KI 146,8 → 146,4 min, Spieler 90,9 → 90,4; Reinvermögen J40 KI-Häuser 373.246 → 371.908 (± 38.000 Streuung), Spieler 388.962 → 390.189.

Befund: Die Regel wirkt, aber selten. In Köln liegen die besten Bauorte am Markt (10–60 Minuten hin); der beste Ort am Rheinhafen (26 | −11; hin zum Hafen 65–76 Minuten, zum Markt 117–138, je nach Jahreszeit der Entscheidung) zählt über die Jahre mit etwa 155–185 Minuten Rundfahrt und gewinnt erst, wenn die schnellen Orte am Markt vergeben sind. Die KI-Häuser bauen in 20 Jahren nur wenige Werkstätten in der Stadt (Umland und Orte am Markt gehen vor). Mehr Fahrten zum Hafen gäbe es nur mit einem Vorteil des Hafens, den der Kern nicht hat (Designfrage unter „Offen“).

### Aufschlag am Hafen (Hafen v2)

Gemessen auf main `5e61a1c0` (nach dem zweiten Rebase; „main“ = ohne KI am Hafen und ohne Aufschlag, mit der Zählung der neuen Werkstätten aus `Hafenmessung.cs` dieses Branches) gegen main `5e61a1c0` + dieser Branch (Code-Stand `100b5e7b` nach dem Rebase). Aufschlag über `--hafenaufschlag=0|0.05`, 3 % sind die Spieldaten. Arbeits-PC, nur CPU, niedrige Priorität. Ein erster Lauf auf main `f8419a28` (vor den Kölner Kirchen, PR #398) zeigte dasselbe Bild mit etwas weniger Hafen (KI 5/105 bzw. 2/54 neue Werkstätten am Hafen, Fahrten 0,1 bzw. 0,5 %).

`messen hafen 20 20 --karten=koeln` (20 Startwerte je Aufbau, Reinvermögen ± Streuung der Partien). Spieler als Wirt (ein Betrieb): alle vier Spalten Zahl für Zahl gleich (KI-Häuser 73 neue Werkstätten, keine am Hafen). Spieler als Schmied: mit KI am Hafen 1 von 84 KI-Werkstätten am Hafen, Fahrten 0,1 %, in allen drei Spalten +0/+3/+5 gleich. Beim ausbauenden Spieler („Ausbau“, Strategie der KI):

| Köln, 20 Jahre, Ausbau | main | +0 % | +3 % | +5 % |
|---|---|---|---|---|
| KI-Häuser: neue Werkstätten, davon näher am Hafen | 104, 0 | 104, 7 (6,7 %) | 104, 7 (6,7 %) | 104, 7 (6,7 %) |
| KI-Häuser: Fahrten zum Hafen | 0,0 % | 0,3 % | 0,3 % | 0,3 % |
| Spieler: neue Werkstätten, davon am Hafen | 28, 0 | 28, 7 (25 %) | 28, 7 | 28, 7 |
| Spieler: Fahrten zum Hafen | 0,0 % | 1,5 % | 1,5 % | 1,5 % |
| Umsatz je Jahr KI-Haus / Spieler | 13.119 / 12.860 | 13.131 / 12.844 | 13.131 / 12.847 | 13.132 / 12.849 |
| Reinvermögen J20 KI-Häuser | 71.346 ± 6.383 | 71.586 | 71.599 | 71.607 ± 6.381 |
| Reinvermögen J20 Spieler | 67.679 ± 16.221 | 67.875 | 67.931 | 67.969 ± 16.446 |

`messen hafen 40 10 --karten=koeln --beruf=Ausbau`:

| Köln, 40 Jahre, Ausbau | main | +0 % | +3 % | +5 % |
|---|---|---|---|---|
| KI-Häuser: neue Werkstätten, davon am Hafen | 54, 0 | 54, 4 (7,4 %) | 54, 4 | 54, 4 |
| KI-Häuser: Fahrten zum Hafen | 0,0 % | 1,0 % | 1,0 % | 1,0 % |
| Spieler: neue Werkstätten, davon am Hafen / Fahrten zum Hafen | 14, 0 / 0,0 % | 14, 4 / 6,2 % | 14, 4 / 6,3 % | 14, 4 / 6,3 % |
| Umsatz je Jahr KI-Haus / Spieler | 27.902 / 32.601 | 27.912 / 32.675 | 27.923 / 32.621 | 27.927 / 32.647 |
| Reinvermögen J40 KI-Häuser | 373.978 ± 30.168 | 374.227 | 373.929 | 374.090 ± 32.640 |
| Reinvermögen J40 Spieler | 388.931 ± 116.558 | 392.476 | 393.556 | 394.554 ± 116.160 |

- `messen bericht 20 30` (1.320 Partien, Greybox, Probestadt, Messstadt; gemessen auf `f8419a28`, PR #398 ändert nur Köln): Ausgabe Zeile für Zeile gleich wie main. Berufe im Band 9/10 (Tischler −15 %), KI ÷ Bezug (Median) 1,05, davongezogen 7/90; „Schwer“ 1,82, 35/90.
- `messen bericht 20 30 --teile=5 --karte=koeln` (60 Partien, `5e61a1c0`): Normal Bezug 63.501 → 63.988, KI-Häuser 72.421 → 72.526, KI ÷ Bezug 1,13 → 1,11, davongezogen 17/210 → 17/210; „Schwer“ Bezug 77.898 → 77.976, KI-Häuser 105.592 → 105.617, Median 1,38 → 1,39, davongezogen 16/210 → 21/210. Kein Bankrott.

Befund: Der Aufschlag ändert keine Bauortwahl – 3–5 % verkürzen die beste Hafenfahrt (130 Minuten hin und zurück) in der Rechnung der KI um 4–6 Minuten, über die Jahre (× 3/4) um 3–5, die Orte am Hafen liegen aber 30–100 Minuten hinter den freien Orten am Markt. Er bringt nur den Werkstätten am Hafen mehr Umsatz (je Haus und Jahr wenige Gulden, weil es kaum solche gibt). Was den Hafen nutzt, kommt aus der Bauortwahl selbst (v1): KI-Häuser bauen 7 % ihrer neuen Werkstätten dort, der ausbauende Spieler (KI-geführt) 25–29 %. Ziel „nicht alle am Hafen“ erfüllt (höchstens etwa 7 % der KI-Neubauten), „spürbar“ nicht – Empfehlung unter „Offen“.

### Vorarbeiter und Einrichtung nach jeder Wirkung (PR „Kern: Personal-Nacharbeit …“)

Gemessen auf main `31b9a9b0` + dieser PR (`1b9e9a0a`), `messen bericht 20 30 --parallel=8` gegen main (je 1.320 Partien; Tabellen in WIRTSCHAFTSMODELL „Personal-Nacharbeit“): Berufe 9/10 im Band wie main (Tischler schon außerhalb), Dieb −5,3 → −9,4 %, Gardist −8,3 → −6,6 %, Dieb wagemutig −31,0 → −26,5 %; KI ÷ Bezug (Median) 1,05 → 1,06, „Schwer“ 1,82 → 1,81. Probe (drei Startwerte, Greybox, 12 Jahre, Haus frei von der KI geführt): Der Dieb kauft die Lederhandschuhe nur, wenn jemand nach Spuren sucht (eine von drei Partien, elftes Jahr); sein Meisterdieb bleibt ohne Suchende „Einbrecher“ (alle drei 0). Der Gardist kauft Waffenkammer und in einer von drei Partien die Hellebarden (Kopfgeld aus Urteilen nach eigenen Razzien), Vorarbeiter Antreiber.
