# Erbgut

*Gene, Vererbung und Völker – die Grundlage der 3D-Köpfe*

**Wozu.** Jede Person der Dynastien trägt ein **Erbgut**. Daraus baut der kommende Charakter-Editor ihren 3D-Kopf: Nase, Kinn, Augen, Ohren, Haar- und Hautfarbe, Statur. Bisher gibt es nur die Rechnung dahinter (Phase 1); zu sehen ist das Erbgut noch nirgends. Das gemalte Porträt (Gesicht, Haar- und Augenfarbe, siehe [Haus und Dynastie](haus-und-dynastie.md)) bleibt bis dahin, wie es ist. Auf Wirtschaft, Rat oder Talente wirkt das Erbgut nicht.

**Gene.** Ein Gen ist eine Eigenschaft mit einem Wert von 0 bis 255 – zum Beispiel „Nasenlänge“: 0 ist eine Stupsnase, 255 ein Zinken. Alle Völker teilen dieselbe Skala, deshalb ist ein Zwerg bei „Körpergröße“ um 45, ein Mensch um 120 und ein Oger um 225. Es gibt 40 Gene in vier Gruppen:

| Gruppe | Gene |
|---|---|
| Körperbau | Körpergröße, Leibesfülle, Muskelkraft, Schulterbreite, Kopfgröße, Beinlänge |
| Gesicht | Gesichtslänge und -breite, Stirnhöhe, fliehende Stirn, Brauenwulst, Augenabstand, -größe, schräge und tiefliegende Augen, Nasenlänge, -breite, Nasenhöcker, Wangenknochen, volle Wangen, Mundbreite, Lippen, Kieferbreite, vorstehender Unterkiefer, Kinn, Hauer, Ohrengröße, spitze Ohren |
| Farben | Hautfarbe (hell – dunkel), Hautton (rosig – grünlich), Haarfarbe (schwarz – weißblond), rotes Haar, Augenfarbe (dunkel – hell), Augenfarbton |
| Zubehör | Frisur, Bartform, Bartwuchs (bei Männern und bei Frauen je ein eigenes Gen), Neigung zur Glatze, Ergrauen |

Das Alter ist kein Gen: Es kommt aus dem Geburtsjahr. Einige Gene ändern sich im Alter (die Nase und die Ohren wachsen, Wangen und Lippen fallen ein); das zeigt später die Darstellung.

**Zwei Allele.** Wie in der Natur – und wie in *Crusader Kings 3*, dem Vorbild – hat jede Person je Gen **zwei** Werte, einen vom Vater und einen von der Mutter. Man nennt sie Allele. Nur eines davon ist **sichtbar**, das andere trägt die Person verdeckt mit sich herum.

**Vererbung.** Ein Kind bekommt je Gen ein Allel vom Vater und eines von der Mutter. Welches der zwei Allele ein Elternteil weitergibt, entscheidet der Zufall – auch das verdeckte kann es sein. Dazu kommt eine kleine **Mutation**: Das Allel weicht um ein paar Punkte ab (meist höchstens 3). Welches der beiden Allele beim Kind sichtbar wird, entscheidet die **Dominanz** des Gens:

- Bei den meisten Genen ist es Zufall, halb und halb.
- Bei der **Haarfarbe** und der **Augenfarbe** setzt sich das dunklere meist durch (zu 75 %), **rotes Haar** ist ebenfalls rezessiv, ein **Nasenhöcker** setzt sich eher durch.

So entsteht, was man aus jeder Familie kennt: Zwei dunkelhaarige Eltern, die beide ein blondes Allel verdeckt tragen, bekommen ab und zu ein blondes Kind – das Haar der Großmutter taucht beim Enkel wieder auf.

**Völker.** Jedes Volk hat je Gen eine eigene **Glockenkurve**: einen Mittelwert, eine Streuung und feste Grenzen. Elben haben fast immer spitze Ohren und eine schmale Nase, Zwerge einen mächtigen Brauenwulst, breite Kiefer und Vollbart, Halblinge volle Wangen, Orks Hauer und grünliche Haut, Oger sind riesig. Kein Allel liegt je außerhalb der Grenzen seines Volkes. Wer von außen ins Spiel kommt (dein Oberhaupt zu Spielbeginn, die KI-Häuser, die Heiratskandidaten der Brautschau), bekommt ein Erbgut aus der Glockenkurve seines Volkes. Gewürfelt wird höchstens zwei Streuungen vom Mittel weg; die Regeln unter „Was zusammengehört“ können einen Wert noch etwas weiter schieben (ein großes Kinn unter einem breiten Kiefer), über die Grenzen des Volkes aber nie.

**Bärte.** Männer zeigen das Gen „Bartwuchs“, Frauen ein eigenes Gen „Bartwuchs der Frauen“ – beide trägt und vererbt jede Person, sichtbar ist je Geschlecht eines. **Zwerginnen tragen einen Bart**, meist einen leichten (kurz oder geflochten), selten einen vollen (bei etwa 2 von 100); Frauen der anderen Völker sind bartlos. Eine Zwergin mit kräftigem Bart vererbt ihn auch über ihre Söhne an die Enkelinnen.

**Ehe zwischen Völkern.** Ein Kind gehört immer dem Volk des Hauses an (siehe [Haus und Dynastie](haus-und-dynastie.md)). Ein Allel aus dem anderen Volk, das nicht in die Grenzen des Kindes passt, rückt an die nächste Grenze: Das Menschenkind einer Elbin hat keine Elbenohren, aber Ohren am spitzen Rand dessen, was es bei Menschen gibt.

**Was zusammengehört.** Ein paar Regeln sorgen dafür, dass Gesichter stimmig wirken: Wer kräftig ist, hat meist breite Schultern; ein breiter Kiefer trägt ein großes Kinn; Beleibte haben vollere Wangen; unter einem Brauenwulst liegen die Augen tiefer; helles Haar geht eher mit hellen Augen. Die Regeln wirken nur beim Würfeln – Kinder erben sie mit den Allelen ihrer Eltern.

**Immer gleich.** Das Erbgut hängt nur am Startwert der Partie, an der Nummer der Person und an den Eltern. Zwei Partien mit demselben Startwert haben dieselben Gesichter, und im Mehrspieler sieht jeder Rechner dasselbe. Ein Spielstand speichert jedes Allel. Spielstände von vor dem Erbgut laden: Jede Person bekommt beim Laden ihr Erbgut aus ihrem festen Startwert, Kinder von ihren Eltern – bei jedem Laden dasselbe. Kommen später neue Gene dazu, bekommen alte Spielstände sie auf dieselbe Weise; Gene, die es nicht mehr gibt, fallen weg.

**Für Neugierige.** Alle Zahlen stehen in `game/daten/erbgut.json`: je Gen Name, Beschreibung, Dominanz (Chance in Prozent, dass das höhere Allel sichtbar wird), Mutation, die Alterskurve und je Volk `min`, `mittel`, `max` und `streuung`; dazu die Korrelationen mit ihrer Stärke. Im Entwicklerfenster gibt es den Befehl „Erbgut neu würfeln“ für eine Person (für Tests der 3D-Köpfe; der Knopf dafür kommt mit der Darstellung).

[Zurück zum Inhaltsverzeichnis](README.md)
