# Reichsgilde – Leveldesign

Begonnen am 16.10.2026 · Bereich **Leveldesign** (siehe `CLAUDE.md`). Grundsatzentscheidungen stehen in `docs/ENTSCHEIDUNGEN.md`, Abschnitt 16. Dieses Dokument sammelt die **Kartenregeln** für alle Städte und den Stand der einzelnen Karten.

**Stand:** Planungsphase. Die heutige Greybox-Stadt bleibt Platzhalter für die Mechaniken und wird vorerst nicht ersetzt.

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
- **Fest bleiben:** Startplätze, Sonderbauten (Rathaus, Hauptkirche, Markt, Hafen, Tore), Ruinen und Leerstand.
- **Umland:** Rohstoffbetriebe sind an Vorkommen gebunden (Erz, Stein, Wald). Höfe und Gärten stehen frei in Umland-Bauzonen. *(Vorschlag)*
- **Zustände:** frei → bebaut → leerstehend (Haus erloschen oder bankrott) → verfallen. Leerstand lässt sich kaufen und sanieren (Kosten nach Zustand). *(Kern)*

**Offenes Risiko:** Ohne Flächenpreis und Grenze kann ein Haus gute Zonen mit billigen Bauten zustellen. Mögliche Gegenmittel, falls es in Tests auffällt: Unterhalt je Gebäude, Verfall ungenutzter Gebäude, Mindestabstand gleicher Betriebe. *(Kern)*

## 4. Wohnhäuser (entschieden 16.10.2026)

Jedes Haus wohnt in einem **eigenen Wohnhaus**, getrennt von seinen Betrieben. Das löst die Regel „Stammhaus = erste Werkstatt“ ab. Der **Stand** bestimmt, wie groß man bauen oder kaufen darf.

| Stand | Wohnhaus (Vorschlag) |
|---|---|
| Gemeiner | Fachwerkhaus |
| Bürger | Bürgerhaus |
| Patrizier | Steinhaus |
| Edler | Stadtpalais |
| Freiherr, Reichsgraf | Hof (nur Kern-Zone) |

Wohnstube und weitere Räume liegen im Wohnhaus. Das Wohnhaus eines erloschenen Hauses wird Leerstand. Umsetzung, Preise und Unterhalt: Kern.

## 5. Wege und Wegfindung

| Wegklasse | Wer | Regel |
|---|---|---|
| Landstraße | Karren, Fußgänger | endet an einem Tor |
| Hauptstraße | Karren, Fußgänger | verbindet Tore, Markt, Hafen, Bauzonen |
| Gasse | nur Fußgänger | keine Karren |
| Platz | alle | Wende- und Begegnungsfläche |
| Leinpfad | Fußgänger, Treidelpferde | am Ufer |
| Tor, Brücke | alle | Engstelle |
| Fähre | alle | setzt im Takt über |

- **Zwei Ebenen** *(Vorschlag, Kern)*: Ein **grober Wegegraph** (Knoten, Kanten mit Länge, Klasse, Gefahr) liefert der Simulation die Fahrzeit, ohne Godot und deterministisch. Die **feine Bewegung** (Ausweichen, Spur) macht nur die Darstellung, ohne Rückwirkung. Passt zur Regel „Der Kern legt Wege mit Zeiten fest“ (ENTSCHEIDUNGEN 15, Passanten).
- **Tore schließen nachts** (Dunkle Nacht, 20 Uhr); Karren draußen warten. *(entschieden; Umsetzung Kern)*
- **Fußgänger:** Die Bürger-KI wählt das Ziel; die Karte liefert Zielpunkte (Eingänge) und Verweilpunkte (Brunnen, Marktstände, Kirchenportal, Bänke vor Schänken). *(Vorschlag)*

## 6. Wasser

- **Flüsse, Häfen, Brücken** gehören dazu (entschieden). Flüsse quert man nur über Brücke oder Fähre.
- **Hafen** ist Umschlagplatz Schiff ↔ Karren. Er wird jetzt mitgeplant; die Mechanik (z. B. Stapelrecht, Schiffe) folgt später. *(entschieden)*
- **Bäche** in der Stadt mit kleinen Brücken als Engstellen. *(Vorschlag)*
- **Hochwasser** als Ereignis sperrt Ufer, Hafen und Leinpfad. *(Vorschlag, Kern)*

## 7. Gelände und Gefahr

- Drei Höhenstufen: Ebene, Hang, Berg. Berge sind nicht befahrbar, Wege folgen den Tälern.
- Rohstoffe liegen dort, wo die Vorlage sie hatte.
- **Gefahrenfaktor je Wegabschnitt** um 1,0, im Mittel aller Umlandfahrten 1,0. Höher: Wald, Engtal, Nähe zur Wildnis. Niedriger: Wachturm, Tornähe. Ork-Rotten kommen nur von der Wildnisseite. *(Vorschlag, Kern)*

## 8. Karten

| Stadt | Stand |
|---|---|
| Köln (Pilot) | Planungsskizze (noch nicht im Repo); linkes Rheinufer, Mauerhalbkreis mit Toren, Dom als Baustelle, Hafen, **Rheinbrücke nach Deutz (Fiktion)**, Fähre, Umland Kölner Bucht, Ville, Königsforst, Bergische Höhen, Siebengebirge, Eifel |
| weitere ca. 7 | offen |

Später: **Reichskarte**, über die man zwischen den Städten wechselt (Idee festgehalten, nicht jetzt).

## 9. Zuständigkeit

| Bereich | Zuständig für |
|---|---|
| Leveldesign | Kartenregeln, Kartendateien, Straßen, Bauzonen, Gelände, feste Plätze |
| Kern | jede Regel mit Wirkung: Wohnhaus, Fahrzeit aus dem Wegegraphen, Tore, Hafen, Gefahrenfaktor, Sanierung, Baukosten |
| Darstellung | Modelle, feine Bewegung, Lesbarkeit |
| Spiel-KI | Zielwahl der Bürger, Bauplatzwahl der KI-Häuser |
