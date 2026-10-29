# Realistisches Wasser, Phase 3: Einbau ins Spiel und Grafikstufen

Stand 29.10.2026, Branch `phase5/wasser-darstellung`. Alle Gewässer des Spiels (Köln mit Rhein, Duffesbach und Hafen, Probestadt, Greybox, Kartenwerkstatt) zeichnet jetzt das realistische Wasser aus Phase 2; das alte `wasser.gdshader` ist entfernt. Phase 1 (Herkunft, Lizenz): [phase1.md](phase1.md), Phase 2 (Arten, Shader, Testszene): [phase2.md](phase2.md).

## Was eingebaut ist

| Teil | Datei | Inhalt |
|---|---|---|
| Shader | `game/assets/shader/wasser_realistisch.gdshaderinc` | Der Code aller Stufen (bisher `wasser_realistisch.gdshader`). Neu: Laufachsen der Flüsse, Netzwerte in `COLOR`, weicher Netzrand, Grundspiegelung des Himmels, Wellen klingen bei grobem Netz ab, großflächig unregelmäßiger Eisrand |
| Stufen | `wasser_niedrig.gdshader`, `wasser_mittel.gdshader`, `wasser_realistisch.gdshader` | Nur `#define MIT_…` und `#include` (wie beim Boden-Shader) |
| Einbau | `game/scripts/Stadtansicht.Wasser.cs` | Netze aus den Kartendaten, je Gewässer ein Material mit seiner Art, Eis nach Jahreszeit, Stufenwechsel |
| Arten, Stufen | `game/scripts/Wasserarten.cs` | `AusKarte` nach Breite bzw. Fläche, `Winterfortschritt`, `StufeAusGrafik`, `ShaderFuer`, `NetzAbstand`, `Vorwaermen` |
| Messung | `game/scripts/Stadtansicht.Wassermessung.cs` | `--wasservergleich=1`: Wasser im selben Prozess reihum aus und in allen vier Stufen |

**Netz.** Flüsse und Bäche sind ein Band entlang der Mittellinie, quer und längs in Ecken zu 2 m geteilt (Ultra 1 m), in Stücken zu 64 m (Godot lässt Stücke außerhalb des Bildes weg). Seen, Teiche und Hafenbecken liegen wie bisher genau über den Zellen des Bodens (in Ultra geviertelt). Jede Ecke trägt:

| Wert | Kanal | Bedeutung |
|---|---|---|
| Uferabstand | `COLOR.r` (m / 32) | Eis wächst von dort, ohne Tiefentextur (Niedrig) auch Farbe, Durchsicht und Schaum; Netzrand weich |
| offen | `COLOR.g` | 1 = friert nie: um Furten (ab halber Wegbreite 3 m, Übergang bis 6 m), Fährlinien, Hafenbecken, in einer Mündung |
| Schaumanteil | `COLOR.b` | Faktor auf Stärke und Breite des Saums: 0 unter 26 m Breite, 1 ab 30 m; vor einem Hafenbecken (2–8 m) nur der Kaisaum (⅓) |
| Deckkraft | `COLOR.a` | 0 = hier übernimmt ein anderes Gewässer (Bach in der Mündung, Hafenbecken zum Fluss hin) |
| Laufachsen | `UV` = (quer, Stelle) in m, `UV2` = Strömung | Nur Flüsse: Die Normalkarten laufen entlang des Laufs, auch durch Kurven, ohne sich mit der Zeit zu verzerren |

Reihenfolge der durchsichtigen Flächen (`RenderPriority`): Flüsse und Seen zuerst, Bäche darüber, Hafenbecken zuletzt; so blenden Bach und Becken weich in den Fluss über.

## Gewässer der Karten

Die Art ergibt sich aus der Karte (`wasser`: Linie mit `breite`, Fläche mit `flaeche`). Es entscheidet die **Größe, nicht der Name** (`bach`/`fluss`, `teich`/`see`). Das Kartenformat ist **unverändert**; fehlende Angaben (Breite, Strömung) setzt wie bisher der Kern (`Wasserform.Vorgabebreite`, `VorgabeStroemung`).

| Karte | Gewässer | Karte sagt | Art | Uferschaum | Eis |
|---|---|---|---|---|---|
| Köln | Rhein | Fluss, 60–100 m, 1,2 m/s | Fluss | ja | nie |
| Köln | Duffesbach | Bach, 3 m | Bach | nein | ganz, außer an den vier Furten und der Mündung |
| Köln | Rheinhafen | Hafenbecken | Hafenbecken | nein; der Rhein davor nur schmal | nie |
| Probestadt | Sandfluss | Fluss, 16 m | Fluss | nein (unter 30 m) | nie |
| Probestadt | Mühlbach | Bach, 1,6 m | Bach | nein | ganz, außer an den zwei Furten und der Mündung |
| Probestadt | Mühlenteich | Teich, 1.240 m² | Teich | nein | ganz |
| Probestadt | Hafen1 | Hafenbecken | Hafenbecken | nein | nie |
| Greybox | Grenzfluss | Fluss, 36 m | Fluss | ja | nie |
| Greybox | Hafen1 | Hafenbecken | Hafenbecken | nein | nie |

Keine Karte hat einen See über 2.000 m² oder Meer. Randeis zeigt deshalb ein See, der in der Kartenwerkstatt angelegt wird (Testanleitung Schritt 6), oder die Testszene (`--wassertest=1 --wasserart=see --jahreszeit=winter`).

## Eis

| Regel (Nutzer 29.10.2026) | Umsetzung |
|---|---|
| Bäche ≤ 5 m und Teiche/Tümpel ≤ 2.000 m² frieren ganz zu | `Wasserarten.AusKarte`: Art Bach bzw. Teich, Eisreichweite über die Mitte hinaus |
| Größere Seen nur Randeis 3–6 m | Art See, 3 m bei 2.000 m² bis 6 m ab 22.000 m² |
| Flüsse über 5 m, Furten und bewegtes Wasser frieren nie | Art Fluss friert nie; „offen“ um Furten, Fährlinien, Hafenbecken und in Mündungen |
| Das Eis wächst über den Winter und taut im Frühling | `Winterfortschritt`: Winter 0 → 1 über die ersten drei Fünftel des Spieljahres (erst schnell, dann langsamer), Frühling 1 → 0 im ersten Drittel |

Den Fortschritt liest die Darstellung aus dem Kern (`Spielzeit.Jahreszeit`, `Spielzeit.MinuteImTag` und die angebrochene Minute des Taktgebers; ein Spieltag ist ein Spieljahr). Eine **Ergänzung im Kern war nicht nötig**. Ist die Jahreszeit vorgegeben (`--jahreszeit`, Entwicklerfenster) und weicht sie von der Uhr ab, steht das Eis im Winter voll; `--winter=0..1` setzt es fest. Der Eisrand ist großflächig unregelmäßig (bis ±¼ der Reichweite, höchstens ±3 m), damit die offene Stelle eines Teiches nicht genau dem Umriss folgt.

## Wetter und Wind

Nichts Neues zu verdrahten: Der Shader liest dieselben globalen Werte wie Gras, Fahnen und Regen (`wetter_wind`, `wind_richtung`, `wetter_regen`, `wetter_schnee`, `wetter_himmel`, gesetzt in `Stadtansicht.Wetter.cs`). Sturm macht die Wellen bis 60 % steiler und die Kräusel unruhiger (Fluss mit Windanteil 0,4, also schwach); Regenringe gibt es nur, wenn `wetter_regen` gesetzt ist, also in den Wettereffekten Hoch (Voreinstellung Hoch und Ultra). Schnee liegt auf dem Eis. Die Spiegelung folgt der Himmelsfarbe (Wolken grau, Nacht dunkel).

**Grundspiegelung.** Die Stadt hat keinen Himmel, den Godot im Wasser spiegeln könnte (nur eine Hintergrundfarbe), und die Kamera schaut 40° von oben, wo die echte Spiegelung schwach ist; tiefes Wasser war deshalb fast schwarz. Neu ist `himmel_grund` (im Spiel 0,2, Testszene 0): ein Anteil der Himmelsfarbe, der mit der Neigung der Kräusel schwankt und im Flachen schwächer wird. Dazu wiederholen sich die Normalkarten im Spiel alle 6,7 m statt 4 m (`sampler_scale` 0,15): aus der Höhe der Kamera wirkten kleinere Kräusel wie Rauschen.

## Grafikstufen

Kein eigener Regler: Die Stufe folgt der Grafik-Voreinstellung (`Wasserarten.StufeAusGrafik`); steht sie auf „Eigene“, gilt die Voreinstellung, mit der die meisten Regler übereinstimmen. Startoption für Tests: `--wasserstufe=niedrig|mittel|hoch|ultra`. Ändert der Spieler die Voreinstellung, baut `WasserStufeNachfuehren` nur die Wasserflächen neu (Brücken, Fähren und Häfen bleiben).

| Stufe | Shader | Brechung | Tiefentextur | Kaustik | Schaum | Eis, Wind | Regenringe | Netz |
|---|---|---|---|---|---|---|---|---|
| Niedrig | `wasser_niedrig` (eine Normalkarte, ohne Strömungskarte) | – | – (Farbe und Durchsicht aus dem Uferabstand) | – | am Ufer (aus dem Uferabstand) | ja | – | 2 m |
| Mittel | `wasser_mittel` | – (das Flache wird durchsichtig) | ja | ja | an Ufer, Pfeilern, Steinen | ja | – | 2 m |
| Hoch | `wasser_realistisch` | ja | ja | ja | ja | ja | ja | 2 m |
| Ultra | `wasser_realistisch` | ja | ja | ja | ja | ja | ja | 1 m: auch die kürzeren Wellen bewegen die Fläche |

Den Schaum rechnet der Shader nur nahe am Hindernis (Rauschen sonst übersprungen), das Eis nur im Winter. Bei 2 m Netz klingen Wellen unter 3–6 m Länge im Eckpunkt-Teil ab (`netz_abstand`, sonst zitterten die Ecken); sie bleiben als Kräusel in den Normalkarten. Im Rhein sind die Wellen ohnehin flach, der Unterschied von Hoch zu Ultra ist klein.

## Messung (Köln, T400)

Ansicht Hafen (`--karte=koeln --blick=75,-60,45 --startstunde=10 --wetter=klar --vsync=Aus`, Wasser etwa zwei Drittel des Bildes), 1600 × 900. Gemessen **im selben Prozess reihum** (`--wasservergleich=1`: ohne Wasser und die vier Stufen je 4 s, drei Runden, Median), weil Einzelläufe auf der geteilten Grafikkarte um das Doppelte schwanken; die drei Runden lagen meist innerhalb von ±1 %. „Altes Wasser“: derselbe Stand vor dem Einbau (`4c78e0e6`, `wasser.gdshader`) mit derselben Messung (nur ohne/mit Wasser). Die Bildzeit begrenzt hier der Prozessor (Simulation, geteilter Rechner); aussagekräftig ist die **GPU-Zeit**.

| Voreinstellung | ohne Wasser: GPU, Bilder/s | vorher (altes Wasser): GPU (Kosten), Bilder/s | nachher (neues Wasser, gleiche Stufe): GPU (Kosten), Bilder/s |
|---|---|---|---|
| Niedrig | 9,5 ms, 34,3 | 11,3 ms (+1,8), 31,7 | 12,4 ms (+2,6), 31,3 |
| Mittel | 17,1 ms, 25,7 | 19,8 ms (+2,7), 23,6 | 22,5 ms (+5,0), 23,2 |
| Hoch | 26,8 ms, 19,5 | 30,3 ms (+3,5), 18,5 | 34,0 ms (+6,5), 18,2 |
| Ultra | 49,0 ms, 13,3 | 53,2 ms (+4,2), 12,6 | 59,0 ms (+8,7), 12,2 |

„Ohne Wasser“ aus dem Lauf des alten Standes; im Lauf des neuen lag es 0,1–1,2 ms höher (Niedrig 9,8, Mittel 17,5, Hoch 27,4, Ultra 50,2 ms), die Kosten sind je Lauf gegen das eigene „ohne Wasser“ gerechnet. Das neue Wasser kostet also in Niedrig gut 0,8 ms mehr als das alte, in Hoch 3 ms und in Ultra (MSAA 4×, 1-m-Netz) 4,5 ms mehr.

Kosten der Wasserstufen unter derselben Voreinstellung Hoch (Hafen): Niedrig +4,7 ms, Mittel +5,8, Hoch +6,5, Ultra +7,6 ms. Daraus: Tiefe, Kaustik und die zweite Normalkarte zusammen rund 1,1 ms, **Brechung rund 0,8 ms** (in der Testszene 1,8 ms bei mehr Wasserfläche im Bild), das 1-m-Netz von Ultra rund 1 ms. Unter allen Stufen liegt ein Sockel von etwa 4–5 ms: eine durchsichtige, beleuchtete, beschattete Fläche über zwei Dritteln des Bildes; in Niedrig mit kleineren Schatten ist er kleiner (2,6 ms). Ansicht Rhein (`--blick=70,40,90`, Voreinstellung Hoch, weiter weg; gemessen vor den letzten Einsparungen beim Schaum und in Niedrig): ohne Wasser 31,4 ms; neu Niedrig +3,6, Mittel +4,8, Hoch +5,6, Ultra +7,4 ms; alt +3,4 ms.

Zum Vergleich die einzelnen Läufe mit `--messung=15` vor dem Einbau (je Voreinstellung ein Lauf, GPU-Zeit und Bilder/s; Hafen / Rhein): Niedrig 14,8 / 19,1 ms, 20,0 / 20,5 Bilder/s; Mittel 20,1 / 22,3 ms, 19,9 / 14,1; Hoch 30,3 / 55,9 ms, 13,0 / 10,6; Ultra 54,0 / 78,3 ms, 12,1 / 8,8. Sie schwanken zu stark (Hoch Rhein 56 ms gegen 31 ms im Vergleichslauf), deshalb gilt die Tabelle oben.

## Vorwärmen (für den Ladebildschirm)

```csharp
// Übersetzt den Wasser-Shader der aktuellen Stufe (oder aller drei) in einem eigenen kleinen SubViewport,
// zeichnet ihn vier Bilder lang, räumt auf und ruft dann fertig (Zeit im Log: „[wasser] vorgewärmt … in … ms“).
Wasserarten.Vorwaermen(Node eltern, bool alleStufen = false, Action? fertig = null);
```

Gedacht für `phase5/ladebildschirm`: aufrufen, solange der Ladebildschirm steht, z. B. `Wasserarten.Vorwaermen(this, fertig: () => _wasserBereit = true);`. Es braucht keine Stadt und keine Kamera. Ohne Aufruf wird der Shader dort übersetzt, wo das Wasser zuerst zu sehen ist – meist schon im Hauptmenü, dessen Hintergrundstadt (Greybox) einen Fluss hat. Gemessen in der Testszene (`--wassertest=1 --wasservorwaermen=1`, alle drei Shader, T400): **8,4 s** mit leerem Shader-Zwischenspeicher (erster Start), **0,9 s** mit gefülltem (das sind dann nur die vier Bilder). Die übrigen 10–20 s des ersten Starts sind die anderen Shader der Szene; dagegen hilft der Shader-Baker beim Export (siehe Grafik-Leistung, `docs/bereiche/darstellung.md`).

## Bilder

Alle selbst angesehen (T400, Vulkan, 1600 × 900, Miniatur-Unschärfe aus). Ordner: `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3/bilder/<name>/a.png` (nicht im Repository).

| Bild | Zu sehen |
|---|---|
| `koeln_hafen_hoch` | Rhein am Hafen bei Tag: blaugraues Wasser mit feinen Kräuseln (Grundspiegelung des Himmels), am Ufer ein grünlicher Flachwassersaum, an der Uferböschung vor den Häusern ein weicher, weißer, zerrissener Schaumsaum; vor dem Hafen nur ein schmaler Saum, das Becken an der Kaimauer ruhig und ohne Schaum. |
| `koeln_rhein_hoch` | Rhein mit Steinbrücke (Zoom 90): Schaum an beiden Ufern, gleichmäßige Kräusel; aus dieser Höhe wirkt das Wasser ruhig. |
| `koeln_hafen_sturm`, `koeln_regen_nah` | Sturm: dunkler, grauer, deutlich unruhiger, Regen fällt; Regen nah (Zoom 25): Ringe überall auf dem Wasser, das Becken an der Kaimauer ohne Schaum. |
| `koeln_hafen_nacht` | 22 Uhr: das Wasser fast schwarz-blau, der Schaum am Ufer bleibt schwach sichtbar; Fensterlicht spiegelt sich hier nicht (zu weit weg). |
| `koeln_hafen_niedrig`, `_mittel`, `_ultra` | Die Stufen am selben Ort: Niedrig etwas heller und gleichmäßiger, breiterer grünlicher Uferstreifen (aus dem Uferabstand geschätzt); Mittel fast wie Hoch, der Flachwassersaum etwas gelblicher (durchsichtig statt gebrochen); Ultra wie Hoch, die längeren Wellen als breite Bänder sichtbar. |
| `koeln_duffesbach_winter`, `_halb` | Duffesbach im Winter: ganz zugefroren (weiß-blau mit Rissen, Schnee), an den zwei gepflasterten Furten offenes, grünes Wasser mit weichem Übergang ins Eis. Bei 0,4 schmale Eisränder an beiden Ufern, die Mitte offen. |
| `koeln_muendung_winter` | Mündung des Duffesbachs in den Rhein: der Bach gefroren bis an die Uferböschung, der Rhein offen. |
| `probe_teich_winter`, `_halb` | Mühlenteich (1.240 m²): ganz zu, mit Rissen und Schneeflecken; bei 0,45 eine unregelmäßige offene Stelle in der Mitte. |
| `probe_furt_tag`, `probe_furt_winter` | Mühlbach (1,6 m) mit Furt: im Sommer ein klarer, bläulicher Streifen mit Lichtnetzen auf der Wiese, im Winter zugefroren, an der Furt offen. |
| `probestadt_tag` | Sandfluss (16 m, ohne Schaum), Hafen mit Kähnen, Fähre, Mühlbach mündet weich in den Fluss. |
| `werkstatt_see_winter` | See von etwa 3.000 m² in der Kartenwerkstatt: nur ein unregelmäßiger Eisrand von gut 3 m, die Mitte offen. |

## Was mir nicht gefällt, offen

- **Schmale Bäche** (unter dem 2-m-Raster des Bodens) liegen als Band knapp über der Wiese: im Sommer ein durchsichtig blauer Streifen ohne Bett und Böschung (wie schon beim alten Wasser). Ein eingeschnittenes Bett bräuchte das Gelände (Kern) oder Uferkanten als Modell.
- **Kosten:** Das neue Wasser kostet in der Hafenansicht in Hoch und Ultra etwa doppelt so viel GPU-Zeit wie das alte, in Niedrig gut 40 % mehr (Tabelle oben). Teuer ist die Fläche in Pixeln (durchsichtig, beleuchtet, mit Schatten); Brechung, Tiefe und feines Netz kommen je unter einer Millisekunde dazu.
- **Furt:** Im Spiel ist die Furt Teil des Bach- bzw. Flussbandes; die eigenen Werte der Art Furt (klarer, schwächere Lichtnetze) sind dort nicht eingebaut. Im Winter wirken die offenen, grünen Stellen mit hellen Lichtnetzen etwas sommerlich.
- **Gleichförmigkeit:** Aus der Spielhöhe sind Kräusel und Schaumsaum recht gleichmäßig; Wellengruppen und Strömungsunterschiede (Mitte schneller als Ufer) sieht man kaum. Die Strömung je Ecke ist im Netz (UV2), wird aber nur für die Richtung genutzt.
- **Randeis** gibt es in keiner Karte (kein See über 2.000 m²).
- **Mittel/Niedrig:** Der Flachwassersaum am Ufer ist gelblich-grün und breiter als in Hoch; die Stufen sehen nicht ganz gleich aus.
- **Grundspiegelung:** `himmel_grund` ist ein Ersatz für einen fehlenden Himmel. Bekäme die Stadt eine echte Himmelsquelle (`WorldEnvironment` mit Sky), sollte er kleiner werden.

## Testanleitung für den Nutzer (Windows)

Godot 4.7.2 (.NET) mit **Vulkan**; im Ordner des Branches `phase5/wasser-darstellung` erst bauen (`dotnet build Reichsgilde.sln`), dann aus der Eingabeaufforderung (`godot` = Pfad zu `Godot_v4.7.2-stable_mono_win64.exe`):

1. **Köln am Rhein, Tag:** `godot --path game -- --karte=koeln --blick=75,-60,45 --startstunde=10` (Hafen) und `--blick=70,40,90` (Rhein mit Brücke). Erwartet: blaugraues, fließendes Wasser mit Kräuseln, ein weicher, zerrissener Schaumsaum an beiden Ufern des Rheins, am Hafen davor nur ein schmaler Saum, im Hafenbecken ruhiges Wasser ohne Schaum. Die Strömung (nach Norden) im laufenden Spiel ansehen, nicht im Standbild.
2. **Sturm und Regen:** dasselbe mit `--wetter=sturm` (dunkler, unruhiger) und `--wetter=regen --blick=60,-70,25` (Ringe auf dem Wasser; nur mit den Wettereffekten Hoch, also Voreinstellung Hoch oder Ultra).
3. **Winter, Bach:** `--karte=koeln --blick=-48,110,30 --jahreszeit=winter --winter=1`: Der Duffesbach ist zugefroren (weiß-blau mit Rissen und etwas Schnee), an den zwei Furten bleibt das Wasser offen. Mit `--winter=0.4` wächst das Eis gerade vom Ufer her.
4. **Winter, Teich:** `--karte=probestadt --blick=130,-100,35 --jahreszeit=winter --winter=1` (ganz zugefroren), `--winter=0.45` (offene Stelle in der Mitte).
5. **Furt:** `--karte=probestadt --blick=-109,97,22` (Sommer) und mit `--jahreszeit=winter --winter=1` (Bach zu, Furt offen).
6. **Randeis an einem See:** `--kartenwerkstatt=probestadt --werkstatt-see=-180,20;-120,10;-110,60;-170,75 --blick=-145,40,50 --werkstatt-kamerastart=1 --werkstatt-ebenen=alle --winter=1`: nur ein Eisrand von gut 3 m, die Mitte offen.
7. **Eis über das Jahr:** eine Partie im Herbst bis in den Winter laufen lassen (Tempo hoch): Mit Winterbeginn wächst das Eis vom Ufer her und steht nach drei Fünfteln des Jahres voll; im Frühling taut es in den ersten Stunden.
8. **Grafikstufen:** Einstellungen → Voreinstellung Niedrig, Mittel, Hoch, Ultra umschalten, während Köln offen ist; das Wasser stellt sich sofort um. Oder als Startoption `--wasserstufe=niedrig` (bis `ultra`). Erwartet: Niedrig etwas heller und ohne Lichtnetze, Mittel ohne Verzerrung des Grundes, Hoch und Ultra mit.
9. **Bildrate:** `--karte=koeln --blick=75,-60,45 --vsync=Aus --wasservergleich=1` misst gut zwei Minuten lang reihum ohne Wasser und in den vier Stufen und schreibt `[wassermessung] …` ins Protokoll (Godot-Konsole bzw. `user://logs`), dann beendet sich das Spiel.
10. **Zurückmelden:** (a) Gefällt das Wasser im Spiel (Farbe, Kräusel, Schaum am Rhein, Hafen)? (b) Eis an Bach, Teich und Furt, Randeis am See? (c) Unterschied der Stufen und die Zahlen aus Schritt 9 auf deinem Rechner. (d) Fehler (Kanten, Flackern, schwarzes oder fehlendes Wasser).
