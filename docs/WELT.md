# Die Welt der Reichsgilde

Hintergrund, Weltgeschichte und Städte des Spiels. Texte unter CC BY 4.0 (wie alle Texte des Projekts). Die Zahlen und Regeln des Spiels stehen nicht hier, sondern in `game/daten/wirtschaft.json` und `docs/WIRTSCHAFTSMODELL.md`; diese Datei erzählt, *warum* die Welt so aussieht.

## Grundsatz

Die **wirkliche Geschichte um 1400** ist das Gerüst, die Fantasy ist die Zutat – nie der Ersatz. Wo die Geschichte etwas belegt (ein Bauwerk, ein Recht, ein Jahr), gilt der Beleg. Erfunden wird nur dort, wo kein Beleg dagegen spricht, und dann so, dass man die Erfindung erkennen kann.

Daraus folgt eine feste Regel für jede Stadt der Karten:

- **drei bis sechs echte Wahrzeichen** an ungefähr richtiger Lage, verkleinert und vereinfacht,
- dazu **ein bis zwei Spuren der Völker**: dezent, ohne die Stadt zur Fantasy-Kulisse zu machen (ein Chor von Elbenhand, eine Hütte mit zwergischem Meister, eine Weinstube der Halblinge).

Unsichere Angaben sind unten mit „(unsicher)“ oder „(Fiktion)“ gekennzeichnet. Die Regel für die Karten steht auch in `docs/LEVELDESIGN.md`.

## Kurze Weltgeschichte

- **Die Alten Völker.** Elben und Zwerge waren vor den Menschen da. Die Elben bauten in Wäldern und an Flüssen, die Zwerge in den Bergen; beide zogen sich zurück, als die Menschen zahlreicher wurden, und blieben doch Nachbarn.
- **Das Imperium.** Rom brachte Straßen, Kastelle und Städte an Rhein und Donau (Köln ist als *Colonia* eine römische Gründung). Mit den Städten kamen Münze, Recht und Schrift – und die ersten Menschen, die mit Zwergen und Elben handelten statt zu kämpfen.
- **Das Reich.** Aus den Trümmern wuchs unter Franken und Ottonen das Heilige Römische Reich: ein Flickenteppich aus Bistümern, Fürstentümern und **Reichsstädten**, die nur dem Kaiser unterstehen. Der Kaiser ist fern, die Städte regieren sich selbst.
- **Um 1400.** Das Spätmittelalter. Die Städte sind reich und selbstbewusst, die Zünfte drängen die Geschlechter (das Patriziat) aus dem Rat, die Dome wachsen in den Himmel, die Pest ist eine frische Erinnerung. Das Spiel beginnt in dieser Zeit; Kaiser und Kurfürsten bleiben im Hintergrund und werden nur sparsam beim Namen genannt.
- **Vor den Toren.** Orks und Oger hat das Imperium nie zähmen können. Sie leben in den Wäldern und Bergen, halten sich an den Naturglauben und kommen als Raubzüge, Söldner oder Knechte in die Nähe der Mauern.

## Die Völker im Reich

Die Tabelle beschreibt die Haltung der Welt gegenüber den Völkern. Die Spielwerte (Talente, Lebensalter, Glaubensverteilung) passen dazu, stehen aber in `wirtschaft.json` unter `dynastie` und `glaube`.

| Volk | Herkunft und Siedlung | Berufe und Gilden | Rechte | Vorurteile | Namen |
|---|---|---|---|---|---|
| **Menschen** | Die Mehrheit; Städte, Dörfer, Höfe überall im Reich | Alle Berufe; sie stellen die meisten Meister, Ratsherren und Pfarrer | Volle Bürgerrechte, wenn sie Stand und Geld haben | Misstrauen gegen alles Fremde, solange es nicht zahlt | Konrad, Heinrich, Ulrich; Agnes, Margarethe, Gertrud |
| **Zwerge** | Berge und Bergwerke; in der Stadt als Bergleute, Schmiede, Steinmetze | Schmied, Steinmetz, Gießer; ihre Meister sind gefragt | Bürgerrecht, aber oft in eigenen Gassen; als Erzhändler an das Stapelrecht gebunden | „Geizig, stur, rechnen jeden Pfennig nach“ | kurze, harte Namen mit Bart und Berg im Klang |
| **Elben** | Alte Waldvölker, heute in kleinen Gemeinden in und bei Städten | Steinmetzen von seltener Feinheit, Heiler, Alchimisten, Parfumeure; als Ratsherren und Prediger gehört | Bürgerrecht, hohes Ansehen, aber wenige | „Zu langlebig, um zuverlässig zu sein“; hochmütig, fremd | lange, weiche Namen |
| **Halblinge** | Kleine, bodenständige Leute aus den Flusstälern; Höfe, Gärten, Weinberge | Wirt, Weinhändler, Krämer; auch Diebe, die niemand bemerkt | Bürgerrecht; als Wirte beliebt, als Gäste unterschätzt | „Naschen und stehlen“ – und leider nicht immer zu Unrecht | gemütliche, rundliche Namen (Brombeer, Kerzenschein) |
| **Orks** | Wälder und Hügel vor den Mauern; Naturglaube | Kaum Gilden; als Söldner, Jäger, Knechte am Rand | Kein Bürgerrecht; die Kirchen führen sie als Heiden | Gelten als wild, aber nicht alle sind es | kurz und rau |
| **Oger** | Berge und Sümpfe; Naturglaube | Schwerste Arbeit: Lasten, Tretrad, Steinbruch | Keine Bürgerrechte; als Lohnarbeiter geduldet | Dumm und gefährlich, sagen die Menschen – die Oger sagen nichts | knappe, grollende Namen |

Die ersten vier Völker sind spielbar und leben als Bürger in der Stadt. Orks und Oger sind die Bestien vor den Toren; wer sie sieht, schließt das Tor.

## Stadtrecht und Gilden

- **Reichsstadt und Rat.** Eine Reichsstadt regiert sich selbst: der Rat wählt Bürgermeister, Zöllner, Richter, die Ämter im Spiel. Der Weg ins Amt führt über Stand und Ansehen (Gemeiner, Bürger, Patrizier, Edler, Freiherr, Reichsgraf).
- **Zünfte und Geschlechter.** Die Zünfte (die Gilden der Handwerker) haben das Patriziat vielerorts aus dem Rat verdrängt oder dazu gezwungen, die Macht zu teilen. Wer Meister werden will, muss Lehre, Gesellenjahre und Prüfung bestehen; die Zunft hält die Preise stabil und die Fremden draußen.
- **Stapelrecht.** Manche Städte dürfen Händler zwingen, ihre Waren auszuladen und eine Weile feilzubieten, ehe sie weiterfahren. Das bringt Zoll und Umschlag, macht aber auch Feinde (Köln, siehe unten).
- **Bann und Mauer.** Innerhalb des Banns gilt das Stadtrecht, außerhalb nur der Landfriede. Wer nachts vor dem Tor steht, steht außerhalb.
- **Völker in der Stadt.** Zwerge, Elben und Halblinge leben mitten unter den Menschen; manche Stadt gibt ihnen eigene Gassen, manche nicht. Zugang zu den Zünften hängt vom Beruf und vom Bürgerrecht ab, nicht vom Blut – das ist eine Welt, die sich um Gulden dreht.

## Glaube

- **Katholisch und evangelisch.** Das Spiel kennt zwei Konfessionen im Reich (die Verteilung je Volk steht in `wirtschaft.json`). Im Hintergrund steht die Kirche des Spätmittelalters: Reliquien, Wallfahrten, Ablass, Bruderschaften, dazu Stimmen, die eine andere Frömmigkeit fordern.
- **Naturglaube.** Orks und Oger glauben an Wald und Berg. Für die Kirchen sind sie Heiden; Taufe ist der Weg ins Bürgerrecht.
- **Wunder ohne Magie.** Es gibt Reliquien, Wunderheilungen und fromme Legenden – aber **keine Zauberei, die Waren aus dem Nichts macht**. Der Alchimist mischt Kräuter, der Parfumeur Düfte; beide sind Handwerker. Magie, die die Wirtschaft kippen würde, gibt es nicht. Die Fantasy zeigt sich in den Völkern, nicht in der Produktion.

## Städte

Je Stadt ein Abschnitt in gleichem Raster: Lage, Geschichte um 1400, Wahrzeichen, Spuren der Völker. Weitere Städte kommen mit ihren Karten dazu.

### Köln

**Lage und Rang.** Köln liegt am linken Rheinufer, mit halbrunder Stadtmauer, die sich ans Ufer lehnt, und der Vorstadt dahinter. Es ist die größte Stadt des Reichs, **Rhein-, Handels- und Heilige Stadt** (Reliquien der Heiligen Drei Könige, Kirchen auf Schritt und Tritt). Seit der Schlacht von Worringen (1288) hat sich die Stadt vom Erzbischof gelöst; im Spiel ist Köln eine Freie Reichsstadt (vereinfacht).

**Stapelrecht (1259).** Der Erzbischof verlieh der Stadt das Stapelrecht, später bestätigt: Kaufleute, die auf dem Rhein durchfahren, müssen ihre Waren **ausladen und drei Tage lang feilbieten**. Köln wird damit zum Umschlagplatz zwischen Ober- und Niederländer Schiffen – wer rheinabwärts will, lädt um. Zoll und Handel reichen aus, um Dom und Rathaus zu bezahlen.

**Zunftherrschaft.** 1396 setzte der **Verbundbrief** die Herrschaft der Zünfte (Gaffeln) im Rat durch und beendete die Macht der alten Geschlechter. Um 1400 ist das frisch. Der **Ratsturm** wird 1406 beschlossen, 1407–14 gebaut, von den Zünften bezahlt; im Spiel steht er als Baustelle am Rathaus.

**Der Dom.** Der gotische Dom wird seit 1248 gebaut. Um 1400 ist der **Chor** fertig (1322 geweiht), das Langhaus ein niedriger Stumpf unter Notdächern, der **Südturm** wächst mit einem hölzernen **Tretrad-Baukran**, der **Nordturm** ist nur ein Mauerstumpf von rund fünf Metern. Im Spiel kommt Fantasy dazu:

- Der **Chor ist das Werk elbischer Steinmetze** nach den Plänen des Meisters Gerhard: heller, leicht silbriger Stein, rankenartiges Maßwerk, schlanke Fialen, grünlich-gläserne Fenster. „Zweiundsiebzig Jahre Bauzeit – für sie ein Sommer.“ Die Menschen baten sie wegen der Dreikönigsreliquien (Fiktion).
- Der **Südturm** mit dem Kran ist grobes Menschenwerk.
- Der **Nordturm** ist im Spiel **halb hoch** – eine bewusste Abweichung, die Elben bauten schneller, als es die Quellen belegen (Fiktion).
- Die **Dombauhütte** bekommt einen zwergischen Meister: ein Steinmetz-Betrieb am Dom trägt ein Zwergenwappen („Hütte Gerhard“, Name als Beispiel).

**Das Stapelhaus.** Das echte Fischkaufhaus stammt erst von 1558; hier steht um 1400 ein **Gemeinschaftswerk von Menschen und Zwergen** am Rhein (Fiktion): Die Zwerge müssen ihr **Erz und Eisen** unter dem Stapelrecht in Köln verkaufen und wiegen lassen. Eine lange Halle mit Treppengiebeln zum Rhein, zwergischer Basaltsockel, eiserne Beschläge, eine große **Stadtwaage**. **Halblinge** sind Weinhändler und Wirte darin – der Rheinwein ist belegt, die Halblinge sind unsere Zutat.

**Wahrzeichen im Spiel.** Die Kennungen stammen aus `game/karten/koeln.json`; die Modelle kommen in drei Paketen (siehe `docs/bereiche/leveldesign.md`, „Köln-Wahrzeichen“).

| Wahrzeichen | Kennung | Um 1400 | Spur der Völker |
|---|---|---|---|
| Dom | `Dom` | Chor fertig, Südturm mit Kran, Nordturm Stumpf | Elben-Chor, zwergische Dombauhütte |
| Rathaus | `Rathaus` | Hansasaal (um 1330), Ratsturm im Bau (1407–14) | – |
| Groß St. Martin | `Martin` | Vierungsturm, Kleeblattchor, am Rhein | – |
| St. Gereon | `Gereon` | Dekagon (1219–27), langer Chor | – |
| St. Severin | `Severin` | Westturm im Bau (1393–1411) | – |
| St. Aposteln | `Aposteln` | Kleeblattchor, wuchtiger Westturm (unsicher in den Einzelheiten) | – |
| St. Kunibert | `Kunibert` | am Rhein, Chor von zwei Türmen flankiert | – |
| Rheinkran | `Rheinkran` | Tretradkran am Ufer, seit dem 14. Jahrhundert belegt | Oger am Tretrad (Vorschlag) |
| Stapelhaus | `Stapelhaus` | Lagerhalle mit Waage am Rhein (Fiktion) | Zwerge, Halblinge |
| Torburgen, Bayenturm | Tore der Mauer | Doppelturmtore des 13. Jahrhunderts, Bayenturm am Rheinende | – |

Die steinerne **Rheinbrücke** nach Deutz ist eine Erfindung des Spiels (in Wirklichkeit gab es um 1400 keine feste Brücke).

## Ereignisse und Namen

Ein Vorrat für Texte, Meldungen und Namen von Häusern:

- **Heilige und Feste:** Martin (11. November), Gereon, Severin, Kunibert, die Heiligen Drei Könige (6. Januar), Ursula und ihre Gefährtinnen.
- **Hausnamen** nach Zeichen: Haus zum güldenen Hammer, zum weißen Schwan, zum schwarzen Raben – so heißen die leeren Häuser der Kölner Karte.
- **Straßen:** Hohe Straße, Breite Straße, Severinstraße, Hahnenstraße, Ehrenstraße, Weyerstraße, Rheingasse.
- **Sprüche:** „Wer den Dom fertig sieht, ist lange tot“ (erfunden), „Stapel ist Stapel“ (erfunden, Kaufmannsspott).
- **Sammelstellen für Namen:** Vornamen und Familiennamen je Volk stehen in `wirtschaft.json` unter `dynastie`.

## Quellen und Abgrenzung

- **Nichts aus dem Original-Spiel.** Weder Texte noch Namen, Karten oder Geschichten stammen aus „Die Gilde“; das Original dient nur als Vorbild für Mechaniken.
- **Reale Geschichte.** Köln und der Dom: Kölner Dombauhütte (Geschichte), Stadt Köln (Ratsturm, Rathaus), Wikipedia (Dom, Tore, Kirchen), KuLaDig (Fischkaufhaus, Groß St. Martin). Bildquellen: Woensam-Ansicht 1531 und Mercator-Plan 1571 zeigen Köln 130 bis 170 Jahre nach dem Spielstand – Dom-Stumpf mit Kran stimmt, Fischkaufhaus 1559 und die Renaissance-Laube des Rathauses sind schon da und werden nicht übernommen.
- **Was Fiktion ist:** die Völker selbst, ihre Anteile an Dom und Stapelhaus, die Brücke nach Deutz, der halb hohe Nordturm. Alles andere folgt der Geschichte, soweit sie belegt ist; wo Zweifel bleiben, steht „unsicher“.
