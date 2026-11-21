# Reichsgilde – Leveldesign

Begonnen am 16.10.2026 · Bereich **Leveldesign** (siehe `CLAUDE.md`). Entscheidungen bis 16.10.2026 stehen im Archiv `docs/ENTSCHEIDUNGEN.md`, Abschnitt 17, neuere in `docs/bereiche/leveldesign.md`. Dieses Dokument sammelt die **Kartenregeln** für alle Städte und den Stand der einzelnen Karten.

**Stand:** Köln ist spielbar (`--karte=koeln`; seit PR #213 aus dem Stadtskript `Koeln.cs`, `docs/leveldesign/kartendatei.md`, „Köln“). Die Greybox-Stadt bleibt Platzhalter für die Mechaniken; seit PR #163 ist ihre Kartendatei (Kartenformat 2, `docs/leveldesign/kartendatei.md`, „Greybox-Karte“) die einzige Quelle dafür, wo in ihr etwas steht – die Stadtansicht zeichnet sie nur noch aus der Karte.

**Status je Regel:** **entschieden** (mit Datum in ENTSCHEIDUNGEN) · **Vorschlag** (Leveldesign) · **Kern** (wirkt auf die Simulation, entscheidet der Kern-Chat).

---

## 1. Grundsätze

| Grundsatz | Inhalt | Status |
|---|---|---|
| Entfernung ist Wirtschaft | Die Fahrzeit ergibt sich **nur aus der Weglänge**. Damit ist die Kartengröße selbst ein Balancing-Wert; jede neue oder geänderte Karte braucht eine Messung durch den Kern. | entschieden |
| Die Karte verschiebt nur | Lage wirkt als **Faktor um 1,0** auf bestehende Werte (Gefahr, Gäste, Nachfrage), wie bei der Bürger-KI. Im Mittel bleibt die Balance. | Vorschlag, Kern |
| Immer 8 Häuser | Jede Karte ist für 8 Häuser ausgelegt, auch wenn eine Partie weniger hat. | entschieden |
| Fairer Start | Startplätze sind gleichwertig (Marktweg ±10 %, Torweg ±15 %). Freie Flächen dürfen bewusst besser oder schlechter liegen. | Vorschlag |
| Vorlage mit einem Hauch Fiktion | Fluss, Mauer, Tore, Hauptkirche, Märkte, Straßen und Landschaft folgen der realen Stadt. Abweichungen werden in der Karte vermerkt. | entschieden |
| Einzelstücke mit gleichen Regeln | Etwa **8 Städte**, jede mit eigener Kartendatei und eigenem Gesicht, alle nach diesen Regeln. | entschieden |
| Lesbar aus vier Blickrichtungen | Eingänge und Plätze sind aus NO, SO, SW und NW erkennbar; hohe Bauten stehen nicht vor Eingängen. | Vorschlag |

## 2. Zonen

| Zone | Lage | Bebauung | Gefahr |
|---|---|---|---|
| Kern | um Markt, Rathaus, Hauptkirche | Sonderbauten, große Wohnhäuser | Taschendiebe, Einbruch |
| Mauerring | innerhalb der Mauer, außen | Werkstätten, Wohnhäuser, Schänken, Kirchen | Einbruch, Sabotage |
| Vorstadt | vor der Mauer, Brückenkopf, Hafen | wie Mauerring, ohne Mauerschutz | nachts höher |
| Umland | Felder, Gruben, Wälder | Umlandbetriebe, Wachtürme | Überfälle |
| Wildnis | Kartenrand, Berge, tiefer Wald | nichts | Ork-Rotten, Oger |

## 3. Bauen (entschieden 16.10.2026)

- **Straßen sind fest** und folgen der Vorlage. Spieler und KI bauen keine Wege.
- Zwischen den Straßen liegen **Bauzonen**. Darin setzt man Gebäude frei auf ein **Raster** mit Drehung in 90°-Schritten. Jedes Gebäude braucht eine Seite an einer Straße (Eingang).
- **Kein Grundstückskauf:** Bezahlt wird nur der Bau oder der Kauf eines Gebäudes. Keine Flächengrenze je Haus.
- **Abriss und Umbau** sind erlaubt.
- **Rasterfeld 2 × 2 m.** *(entschieden 16.10.2026)*
- **Gewerbe überall:** Jede Werkstatt darf in jede Stadt-Bauzone (Kernlage, Mauerring, Vorstadt), egal ob laut oder schmutzig. *(entschieden 16.10.2026)*
- **Anschluss:** Wer im Spiel baut, braucht für eine Werkstatt den Eingang an einer **Karrenstraße** (Haupt-, Ring-, Wallgasse). Wohnhäuser dürfen auch an einer **Gasse** stehen. Gassen bringt die Karte mit, Spieler bauen keine. *(entschieden 16.10.2026)* Die **Plätze der Karte** (Werkstatt- und Wohnhausplätze, auch die Startplätze) sind davon frei: jede Drehung, jeder Abstand zum Weg; Karren und Figuren gehen über ein gerades Stück zum nächsten Weg. *(Entscheidung des Nutzers 26.10.2026, PR #209)*
- **Nur noch Bauzonen** *(Entscheidung des Nutzers 15.11.2026; Karten seit 16.11.2026)*: Es gibt **keine Hofstellen, Startplätze und Bauplätze** mehr. Jedes Haus beginnt mit Wohnhaus und Werkstatt in den **Bauzonen im Stadtinneren** (Kern und Mauerring): Der Spieler wählt beide Stellen zu Spielbeginn selbst (auch in der Probepartie der Kartenwerkstatt), die KI-Häuser werden gelost (`Startortlos` im Kern). Für die Karten heißt das:
  - Die Flächen der alten Werkstatt- und Wohnhausplätze **gehören zur Bauzone**: Die Rasterrechnung und die Regel `zone-gebaeude` behandeln die Plätze nicht mehr als Hindernis (wie das Bauwesen). Greybox, Probestadt und Köln sind so neu gerechnet.
  - Eine Karte soll im Stadtinneren Platz für **8 Häuser** (Wohnhaus 8 × 10 m und Werkstatt 16 × 14 m je Haus) haben. `pruefen` nennt die Zahl („Startorte im Stadtinneren“) und gibt einen Hinweis, wenn es weniger sind – die Partie lässt dann weniger KI-Häuser zu. Stand: Probestadt 8, Köln 8, Greybox 4, Messstadt 0 (nur Vorstadt-Zonen).
  - Die Plätze selbst (`werkstattplatz`, `wohnhausplatz`), `start.hofstellen` und die Plätze von Spieler und KI-Häusern unter `start` sind seit dem Aufräumen des Kerns (Teil 4, 16.11.2026) **aus den Karten entfernt**. Eine ältere Kartendatei lädt weiter (die Felder werden still vergessen), ein alter Spielstand bringt seine Karte mit. `start` nennt nur noch die Farbe des Spielers und die KI-Häuser; nur Umlandbetriebe haben einen `platz`. Die beiden folgenden Punkte beschreiben den Stand davor.
- **Fest bleiben** *(Stand bis 16.11.2026, heute ohne die Hofstellen)*: Hofstellen (je ein Werkstattplatz und ein Wohnhausplatz der Karte; die Start-Hofstellen gleich weit vom Markt; bis 9.11.2026 die Startgrundstücke, entschieden 16.10.2026), Sonderbauten (Rathaus, Hauptkirche, Markt, Hafen, Tore), Ruinen und Leerstand.
- **Hofstellen** *(Entscheidung des Nutzers 9.11.2026; abgelöst am 15.11.2026, aus Karten und Kern entfernt am 16.11.2026)*: Eine Hofstelle ist der Platz eines Hauses in der Stadt – ein **Werkstattplatz** (16 × 14 m) und ein **Wohnhausplatz** (8 × 10 m) der Karte, die zusammengehören (`start.hofstellen`, Kartenformat in `docs/leveldesign/kartendatei.md`). Sie ersetzt die festen Startgrundstücke (`S<n>_…`) und die Werkstatt-Bauplätze (Bauzonen mit `nur: werkstatt`). Eine **Start-Hofstelle** (`start: true`) taugt als Startplatz: Der Spieler wählt eine oder bekommt eine geloste, jedes KI-Haus eine geloste; für sie gilt die Fairness (Karrenweg zum nächsten Markt ±10 %, Regel `startweg`). Eine **freie Hofstelle** (ohne `start`) wird im Spiel ein Bauplatz: der Werkstattteil nur für eine Werkstatt, der Wohnhausteil nur für ein Wohnhaus, jeweils genau eingepasst – etwa am Hafen, wo Plätze knapp sind. Der Wohnhausteil steht vorn bündig neben der Werkstatt, sonst so nah wie die Stadt es zulässt (auch gegenüber an einer Straße). Zwischen zwei Werkstattplätzen bleiben 2 m (Regel `werkstattabstand`), auch zwischen freien Hofstellen. Der übrige Abschnitt `start` (Spieler und KI-Häuser mit Wohnhaus und Werkstatt) bleibt die **Vorgabe ohne Los** (Tests, Messungen).
- **Umland:** Rohstoffbetriebe sind an Vorkommen gebunden (Erz, Stein, Wald). Höfe und Gärten stehen frei in Umland-Bauzonen. *(Vorschlag)*
- **Zustände:** frei → bebaut → leerstehend (Haus erloschen oder bankrott) → verfallen. Leerstand lässt sich kaufen und sanieren: **fest 50 % + 20 % des Hauswerts** (Entscheidung des Nutzers 17.10.2026, statt „Kosten nach Zustand“). *(Kern)*

**Offenes Risiko:** Ohne Flächenpreis und Grenze kann ein Haus gute Zonen mit billigen Bauten zustellen. Mögliche Gegenmittel, falls es in Tests auffällt: Unterhalt je Gebäude, Verfall ungenutzter Gebäude, Mindestabstand gleicher Betriebe. *(Kern)*

## 4. Wohnhäuser (entschieden 16.10.2026)

Jedes Haus wohnt in einem **eigenen Wohnhaus**, getrennt von seinen Betrieben. Das löst die Regel „Stammhaus = erste Werkstatt“ ab. Der **Stand** bestimmt, wie groß man bauen oder kaufen darf.

| Stand | Wohnhaus (Vorschlag) |
|---|---|
| Gemeiner | Fachwerkhaus |
| Bürger | Bürgerhaus |
| Patrizier | Steinhaus |
| Edler | Stadtpalais |
| Freiherr, Reichsgraf | Stadthof (Kernlage oder Mauerring) |

Wohnstube und weitere Räume liegen im Wohnhaus. Das Wohnhaus eines erloschenen Hauses wird Leerstand. Umsetzung, Preise und Unterhalt: Kern.

### Gebäudegrößen (entschieden 16.10.2026)

**Grundsatz:** Ein Gebäude belegt beim Bau sofort die Fläche seiner **größten Ausbaustufe am selben Platz**. So kann es später wachsen, ohne dass Nachbarn im Weg stehen. Die Fläche zählt in Rasterfeldern (2 × 2 m) und schließt den Vorplatz zur Straße (2 m) ein.

| Gebäude | Belegte Fläche | Felder | Ausbau am Platz |
|---|---|---|---|
| Werkstatt, Stufe 1–3 | 16 × 14 m | 8 × 7 = 56 | Stufe 1 → 2 → 3; **jede Stufe sichtbar größer** (Anbau, Hof), alles innerhalb der Fläche |
| Fachwerkhaus → Bürgerhaus → Steinhaus | 8 × 10 m | 4 × 5 = 20 | Gemeiner bis Patrizier am selben Platz |
| Stadtpalais (Edler) | 12 × 10 m | 6 × 5 = 30 | **neuer Bau** an anderer Stelle oder Abriss und Neubau |
| Stadthof (Freiherr, Reichsgraf) | 16 × 14 m | 8 × 7 = 56 | **neuer Bau** in Kernlage oder Mauerring; Palais → Stadthof am selben Platz nur, wenn die Fläche des Stadthofs schon belegt war |
| Einwohnerhäuser | 6 × 6 / 8 × 8 / 8 × 8 m | 9 / 16 / 16 | – (Wohnhaus, Bürgerhaus, Ratsherrenhaus) |
| Mine, Steinbruch, Holzschlag, Gärten | 16 × 14 m | 8 × 7 = 56 | – |
| Klosterhof / Bauernhof | 18 × 16 / 20 × 16 m | 72 / 80 | – |

**Aussehen:** Die Gebäude behalten ihr heutiges Aussehen, ihre Maße werden auf **glatte Meter** gesetzt (z. B. Einwohner-Wohnhaus 6,2 → 6 m, Bürgerhaus 7,2 → 7 m, Ratsherrenhaus 7,6 → 8 m). Die belegte Fläche rundet auf ganze Rasterfelder auf. Umsetzung: Darstellung.

## 5. Wege und Wegfindung

**Straßenbreite (entschieden 16.10.2026):** Bauzonen beginnen **3,3 m neben der Straßenmitte**, Straßen in der Stadt sind also gut **6,5 m** breit – genug für zwei Karren nebeneinander, eng wie in einer mittelalterlichen Stadt.

| Wegklasse | Wer | Regel |
|---|---|---|
| Landstraße | Karren, Fußgänger | endet an einem Tor |
| Hauptstraße | Karren, Fußgänger | verbindet Tore, Markt, Hafen, Bauzonen |
| Gasse | nur Fußgänger | keine Karren |
| Platz | alle | Wende- und Begegnungsfläche |
| Leinpfad | Fußgänger, Treidelpferde | am Ufer |
| Tor, Brücke | alle | Engstelle |
| Fähre | alle | setzt im Takt über |
| Feldweg | Karren, Fußgänger | unbefestigt im Umland, zu Hof, Grube, Feld (seit der Kartenwerkstatt, Schritt 2) |
| Fußweg | nur Fußgänger | Pfad zu Weide und Wald (seit der Kartenwerkstatt, Schritt 2) |

**Breiten der Wegklassen** (Kern, `Kartenweg.Standardbreite` und `Mindestbreite`, seit PR #170): Ohne `breite` zeichnet das Spiel einen Weg in der Standardbreite seiner Klasse; die Kartenwerkstatt lässt keinen Weg schmaler werden als die Mindestbreite, und die Prüfung meldet einen schmaleren als Fehler. Die Mindestbreiten sind so gewählt, dass Greybox und Köln sie einhalten (Landstraßen der Greybox 4 m, ihre Fußwege als Gasse 2 m).

| Klasse (in der Datei) | Standard | Mindestens | Karren |
|---|---|---|---|
| Hauptstraße (`haupt`) | 5 m | 4 m | ja |
| Landstraße (`land`), Wallstraße (`wall`) | 4 m | 4 m | ja |
| Ringstraße (`ring`) | 3 m | 3 m | ja |
| Feldweg (`feldweg`) | 3 m | 3 m | ja |
| Gasse (`gasse`) | 3 m | 2 m | nein |
| Leinpfad (`leinpfad`), Fußweg (`fussweg`) | 2 m | 1,5 m | nein |

**Belag** (Kern, `Bauen/Wegbelag.cs`, `Bauen/Wegebau.cs`, Abschnitt `wegebau` in `wirtschaft.json`, Datenversion 52; Kartenwerkstatt, Schritt 5d): Jeder Weg hat je Abschnitt eine Stufe – **Trampelpfad**, **Fahrweg** (mit Spurrillen), **Kies** oder **Pflaster**. Ohne Angabe in der Karte gilt die Vorgabe der Klasse: Karrenstraßen (Land-, Haupt-, Ring-, Wallstraße, Feldweg) sind Fahrwege, Gassen, Leinpfade und Fußwege Trampelpfade.

| Stufe | Tempo der Karren | Fahrzeit gegenüber heute | entsteht |
|---|---|---|---|
| Trampelpfad | 70 % | × 1,21 | Vorgabe der Fußwege; ein Fahrweg ohne Verkehr verwildert |
| Fahrweg | 85 % | × 1 (Bezug) | Vorgabe der Karrenstraßen; Verkehr tritt einen Trampelpfad aus |
| Kies | 95 % | × 0,89 | Bauauftrag (6 Gulden je Meter, 1 Minute je Meter + 60) |
| Pflaster | 100 % | × 0,85 | Bauauftrag (15 Gulden je Meter, 2 Minuten je Meter + 60) |

- **Bezug Fahrweg:** Auf dem Fahrweg fährt ein Karren das Tempo seiner Art (seit Datenversion 65 rechnet der Kern die Fahrzeit nach Weglänge: Handkarren 1 m/s ≈ 0,79 m je Spielminute, also 24 m für die alte feste Werkstattzeit von 30 Minuten; bis Datenversion 64 galten feste 30 / 120 Minuten für Fahrwege). Jedes Stück der Fahrt dauert Bezugstempo ÷ Tempo so lang, zusammen mit der Steigung; Brücken, Furten und Fähren rechnen ohne Belag; gewählt wird die Strecke weiter nach der Länge. **Für Karten heißt das:** Der Weg von einer Werkstatt zum nächsten Marktplatz ist Wirtschaft – Startgrundstücke gehören nahe an einen Marktplatz (Köln: 20–24 m Karrenweg), Umlandbetriebe so, dass der Ochsenkarren unter der Höchstdauer bleibt (Köln: 150–193 m), weite Wege kosten das Haus Jahre (siehe WIRTSCHAFTSMODELL, „Mehrere Marktplätze“).
- **Im Spiel** ist jeder Weg in **Stücke zu 5 m** geteilt (`wegebau.stueck`). Zum Jahreswechsel zählt jede Karrenfahrt eines Betriebs auf jedem Stück seiner Strecke zum Markt; dazu kommen **Grundverkehr** (Land- und Hauptstraßen 40 Fahrten im Jahr: Fernhandel, Bauern) und **Fußverkehr** (Gebäude bis 40 m um die Mitte des Stücks: Einwohnerplatz 1, Wohnhaus- und Werkstattplatz 2, Sonderbau 5). Der **Austritt** (0–1) eines Trampelpfads oder Fahrwegs ändert sich je Jahr um (Verkehr − 5) ÷ 50, höchstens +0,5 und −0,2: Ein Trampelpfad mit Austritt 1 wird Fahrweg, ein Fahrweg mit 0 Trampelpfad. Ohne Zufall.
- **Verfall:** Kies verliert 5 %, Pflaster 3 % Zustand im Jahr; das Tempo sinkt dabei anteilig zur Stufe darunter (Pflaster mit Zustand 0,5: 97,5 %). Unter 25 % fällt der Belag eine Stufe zurück (Pflaster → Kies, Kies → Fahrweg). Ausbessern ist ein Bauauftrag mit derselben Stufe und kostet, was am Zustand fehlt.
- **Bauaufträge** (`WegAusbauen`): Spieler und KI-Häuser bezahlen selbst; auf ganze Stücke erweitert, höchstens 300 m; was schon besser ist, bleibt. Die KI baut, wenn sich die gesparten Karrenminuten in wenigen Jahren bezahlt machen (`ki.wegebau`, Bereich Spiel-KI). Der **Stadtrat** legt jedes Jahr 600 Gulden in die Wegekasse (höchstens 3.000), bessert damit sein Kies und Pflaster an Hauptstraßen und Markt (30 m um den Markt) unter Zustand 0,6 aus und pflastert dort den Abschnitt mit dem meisten Verkehr (höchstens 100 m im Jahr). Ein Ratsmitglied kann beantragen, einen Weg pflastern zu lassen (`WegPflasterBeantragen`, 2 AP, ein Antrag je Jahr): Ratsherren stimmen dafür bei Gunst ab 10 oder Verkehr ab 30 Fahrten; angenommen zahlt die Wegekasse, und der Antragsteller gewinnt 5 Ansehen für 5 Jahre.

- **Zwei Ebenen** *(Vorschlag, Kern)*: Ein **grober Wegegraph** (Knoten, Kanten mit Länge, Klasse, Gefahr) liefert der Simulation die Fahrzeit, ohne Godot und deterministisch. Die **feine Bewegung** (Ausweichen, Spur) macht nur die Darstellung, ohne Rückwirkung. Passt zur Regel „Der Kern legt Wege mit Zeiten fest“ (ENTSCHEIDUNGEN 15, Passanten).
- **Tore schließen nachts** (Dunkle Nacht, 20 Uhr); Karren draußen warten. *(entschieden; Umsetzung Kern)*
- **Fußgänger:** Die Bürger-KI wählt das Ziel; die Karte liefert Zielpunkte (Eingänge) und Verweilpunkte (Brunnen, Marktstände, Kirchenportal, Bänke vor Schänken). *(Vorschlag)*

## 6. Wasser

- **Flüsse, Häfen, Brücken** gehören dazu (entschieden). Flüsse quert man nur über Brücke, Furt oder Fähre. Seit PR #180 (Kartenwerkstatt, Schritt 5c) sind Flüsse, Bäche, Seen und Teiche Teil der Karte: Brücken und Furten entstehen von selbst, wo ein Weg einen Fluss quert, Fähren und Häfen setzt man; auf Wasser wird nichts gebaut (§9, „Wasser“).
- **Hafen** ist Umschlagplatz Schiff ↔ Karren. Er wird jetzt mitgeplant; die Mechanik (z. B. Stapelrecht, Schiffe) folgt später. *(entschieden)*
- **Bäche** in der Stadt mit kleinen Brücken als Engstellen. *(Vorschlag)*
- **Hochwasser** als Ereignis sperrt Ufer, Hafen und Leinpfad. *(Vorschlag, Kern)*

## 7. Gelände und Gefahr

- Drei Höhenstufen: Ebene, Hang, Berg. Berge sind nicht befahrbar, Wege folgen den Tälern.
- **Höhen** (seit PR #179): Jede Karte kann ein Höhenraster haben (alle 2 m, −20 bis +60 m), gemalt mit den Geländepinseln. Plätze und Bauzonen stehen auf höchstens 15 % Gefälle (ab 8 % ein Hinweis), Gebäude bekommen einen Sockel, Wege und Mauern liegen quer eingeebnet auf dem Gelände (§9, „Gelände“).
- Rohstoffe liegen dort, wo die Vorlage sie hatte.
- **Gefahrenfaktor je Wegabschnitt** um 1,0, im Mittel aller Umlandfahrten 1,0. Höher: Wald, Engtal, Nähe zur Wildnis. Niedriger: Wachturm, Tornähe. Ork-Rotten kommen nur von der Wildnisseite. *(Vorschlag, Kern)*

## 8. Karten

| Stadt | Stand |
|---|---|
| Köln (Pilot, erste Startstadt) | **Kartendatei Format 2** aus dem Stadtskript `tools/Kartenbau/Staedte/Koeln.cs` (`game/karten/koeln.json`, Vorschau `docs/karten/koeln.svg`, PR #213; `docs/leveldesign/kartendatei.md`, „Köln“): 700 × 450 m um (−50 \| 25) mit 300 m Rand, Rhein 60–100 m nach Norden, Mauerhalbkreis (Radius 150 m) mit fünf Toren und zwei Ufertoren, drei Marktplätze (seit 31.10.2026: Alter Markt und Heumarkt an den Enden der Mauer am Rhein, seit 1.11.2026 mit einer Hausreihe vor dem Tor, Neumarkt in Deutz), Rathaus, Dom (seit 21.11.2026 die einzige Kirche, mitten in der Altstadt an einem gepflasterten Domplatz), Hafen mit Kran, Zoll und Stapelhaus, Deutz mit Mauer, Brücke (Fiktion) und Fähre, Duffesbach als Graben vor der Südmauer, Terrasse mit Hochufer; 8 Startgrundstücke, 7 KI-Häuser, 68 Einwohnerplätze, 16 Umlandbetriebe |
| Greybox (Testkarte) | **Kartendatei Format 2** (`game/karten/greybox.json`, Vorschau `docs/karten/greybox.svg`): die Platzhalter-Stadt in Weltkoordinaten mit allem, was in Stadt und Umland steht (bis PR #163 fest im Code gezeichnet), mit 16 Bauzonen aus dem 2-m-Raster für das freie Bauen (seit 16.11.2026 mit den Flächen der alten Plätze, vorher 9; bis 4.11.2026 11 von Hand), davon 4 in Kernlage; seit PR #199 mit dem Grenzfluss am Südrand, Südtor und Hafen (`docs/leveldesign/kartendatei.md`, „Greybox-Karte“) |
| Probestadt (Muster) | **Kartendatei Format 2** aus dem Stadtskript `tools/Kartenbau/Staedte/Probestadt.cs` (`game/karten/probestadt.json`, Vorschau `docs/karten/probestadt.svg`): kleines Muster für alle Stadtskripte, `docs/leveldesign/kartendatei.md`, „Kartenskript“ |
| Messstadt (Messung) | **Kartendatei Format 2** aus dem Stadtskript `tools/Kartenbau/Staedte/Messstadt.cs` (`game/karten/messstadt.json`, Vorschau `docs/karten/messstadt.svg`): Platz für acht KI-Häuser und den Spieler, für den Balancing-Bericht (1/3/5/8 Häuser), `docs/leveldesign/kartendatei.md`, „Kartenskript“ |
| weitere ca. 7 | offen |

**Wahrzeichen und Spuren der Völker (27.10.2026):** Jede Stadt bekommt **3–6 echte Wahrzeichen** an ungefähr richtiger Lage und dazu **1–2 Spuren der Völker**, dezent (ein elbischer Chor, ein Steinmetz-Betrieb mit zwergischem Meister, Halblinge als Wirte), ohne die Stadt zur Fantasy-Kulisse zu machen. Die Geschichte dahinter, die Völker und Köln als Beispiel stehen in `docs/WELT.md`; neue Städte bekommen dort einen eigenen Abschnitt.

Später: **Reichskarte**, über die man zwischen den Städten wechselt (Idee festgehalten, nicht jetzt).

## 9. Kartendatei

Ausgelagert (16.11.2026) nach **`docs/leveldesign/kartendatei.md`**: Aufbau der Kartendatei `game/karten/<kennung>.json`, „Kartenformat 2“, „Gebäudekatalog“, „Kartenwerkstatt“, „Kartenskript“ (`tools/Kartenbau`: Befehle, Stadtskripte, Köln, Probestadt, Messstadt) und „Greybox-Karte“. Eine Karte entsteht nur aus der Kartenwerkstatt oder dem Kartenskript, nie von Hand.

## 10. Zuständigkeit

| Bereich | Zuständig für |
|---|---|
| Leveldesign | Kartenregeln, Kartendateien, Straßen, Bauzonen, Gelände, feste Plätze |
| Kern | jede Regel mit Wirkung: Wohnhaus, Fahrzeit aus dem Wegegraphen, Tore, Hafen, Gefahrenfaktor, Sanierung, Baukosten |
| Darstellung | Modelle, feine Bewegung, Lesbarkeit |
| Spiel-KI | Zielwahl der Bürger, Bauplatzwahl der KI-Häuser |
