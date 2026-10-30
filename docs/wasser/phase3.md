# Realistisches Wasser, Phase 3: Einbau ins Spiel und Grafikstufen

Stand 30.10.2026, Branch `phase5/wasser-darstellung` (Eis nach Gewässerart und freie Kamera seit 30.10.). Alle Gewässer des Spiels (Köln mit Rhein, Duffesbach und Hafen, Probestadt, Greybox, Kartenwerkstatt) zeichnet jetzt das realistische Wasser aus Phase 2; das alte `wasser.gdshader` ist entfernt. Phase 1 (Herkunft, Lizenz): [phase1.md](phase1.md), Phase 2 (Arten, Shader, Testszene): [phase2.md](phase2.md).

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
| Uferabstand | `COLOR.r` (m / 32) | Eis wächst von dort, ohne Tiefentextur (Niedrig) auch Farbe, Durchsicht und Schaum; Netzrand weich. Im Hafenbecken der Abstand zu Kaimauer und Seiten (höchstens die doppelte Tiefe) |
| Eiskanal | `COLOR.g` | 0,5 = wie die Art (`Wasserarten.EisKanal`). Darunter „offen“ = 1 − 2g: wie weit das Eis zurückbleibt (Eis reicht `eis · (1 − offen) · Reichweite` weit): an einer Furt so viel, dass nur Randeis bleibt (ab halber Wegbreite 3 m, Übergang bis 6 m); 0 = friert nie: Loch an einer Mündung und an einer schnellen Stelle eines Baches, Rinne der Fähre. Darüber „still“ = 2g − 1: Wasser vor einer Kaimauer friert wie das Hafenbecken (`eis_still`, `still_reichweite`) |
| Schaumanteil | `COLOR.b` | Faktor auf Stärke und Breite des Saums: 0 unter 26 m Breite, 1 ab 30 m; vor einem Hafenbecken (2–8 m) nur der Kaisaum (⅓) |
| Deckkraft | `COLOR.a` | 0 = hier übernimmt ein anderes Gewässer (Bach in der Mündung, Hafenbecken zum Fluss hin) |
| Laufachsen | `UV` = (quer, Stelle) in m, `UV2` = Strömung | Nur Flüsse: Die Normalkarten laufen entlang des Laufs, auch durch Kurven, ohne sich mit der Zeit zu verzerren |

Reihenfolge der durchsichtigen Flächen (`RenderPriority`): Flüsse und Seen zuerst, Bäche darüber, Hafenbecken zuletzt; so blenden Bach und Becken weich in den Fluss über.

## Gewässer der Karten

Die Art ergibt sich aus der Karte (`wasser`: Linie mit `breite`, Fläche mit `flaeche`). Es entscheidet die **Größe, nicht der Name** (`bach`/`fluss`, `teich`/`see`). Das Kartenformat ist **unverändert**; fehlende Angaben (Breite, Strömung) setzt wie bisher der Kern (`Wasserform.Vorgabebreite`, `VorgabeStroemung`).

| Karte | Gewässer | Karte sagt | Art | Uferschaum | Eis |
|---|---|---|---|---|---|
| Köln | Rhein | Fluss, 60–100 m, 1,2 m/s | Fluss | ja | Randeis 5 m und Schollen, Rinne an der Fähre Mülheim, Lücke an der Mündung des Duffesbachs |
| Köln | Duffesbach | Bach, 3 m | Bach | nein | ganz; an den vier Furten nur Randeis, Löcher an der Mündung (60, 124) und an einer schnellen Stelle (−43, 108) |
| Köln | Rheinhafen | Hafenbecken | Hafenbecken | nein; der Rhein davor nur schmal | früh und ganz, von der Kaimauer her |
| Probestadt | Sandfluss | Fluss, 16 m | Fluss | nein (unter 30 m) | Randeis 1,2 m und Schollen, Rinne an der Fähre, Lücke an der Mündung des Mühlbachs |
| Probestadt | Mühlbach | Bach, 1,6 m | Bach | nein | ganz; an den zwei Furten nur Randeis, Löcher an der Mündung (−114, 117) und an zwei schnellen Stellen oben am Hang (−305, −242 und −268, −217, 8 %) |
| Probestadt | Mühlenteich | Teich, 1.240 m² | Teich | nein | früh und ganz |
| Probestadt | Hafen1 | Hafenbecken | Hafenbecken | nein | früh und ganz |
| Greybox | Grenzfluss | Fluss, 36 m | Fluss | ja | Randeis 1,8 m und Schollen |
| Greybox | Hafen1 | Hafenbecken | Hafenbecken | nein | früh und ganz |

Keine Karte hat einen See über 2.000 m², einen Graben (stiller Lauf) oder Meer. Den See zeigt die Kartenwerkstatt (Testanleitung Schritt 7) oder die Testszene (`--wassertest=1 --wasserart=see --jahreszeit=winter`).

## Eis

| Regel (Nutzer 30.10.2026, nach Recherche: stilles Wasser friert zuerst, Strömung hält offen) | Umsetzung |
|---|---|
| **Teiche, Hafenbecken, Gräben** (stilles Wasser) frieren zuerst und ganz zu | `Eisregel.Voll`, Eis von 0 bis 0,5 des Winterfortschritts (`EisAb`/`EisBis`); Hafenbecken von Kaimauer und Seiten her (Reichweite 4,5 m). Das Becken ist in Köln nur 4–6 m breit und liegt unter den Kähnen: Auch das Flusswasser vor der Kaimauer (bis 2 m voll, weich bis 8 m) ist still und friert wie das Becken (Eiskanal „still“). Graben = ein Lauf mit Strömung unter 0,1 m/s |
| **Kleine Bäche** (bis 5 m) frieren zu, offen bleibt ein kleines Loch an der Mündung und an schnellen Stellen | `Eisregel.Voll`, Eis von 0,15 bis 1; `Muendungen` (wo die Mittellinie in ein anderes Gewässer tritt) und `SchnelleStellen` (Gefälle des Spiegels über ±4 m ab 3,5 %, je Stück die steilste, höchstens eine je 40 m): Loch mit dem halben Bach als Halbmesser, weich über 3 m |
| **Furten**: Randeis vom Ufer, die Mitte bleibt offen | Anteil „offen“ je Art (`Wasserarten.FurtOffen`): Randeis bis 45 % der halben Breite (0,3–3 m); Duffesbach knapp 0,7 m je Ufer |
| **Rhein und breite Flüsse samt Fährstelle**: Randeis und treibende Schollen, nie eine Decke | `Eisregel.Rand`, Randeis 5 % der Breite (1,2–5 m), von 0,3 bis 1; Schollen im Shader (`schollen`): Zellen von 3–8 m in den Laufachsen, die mit 0,9 × Strömung wandern, dazu in Hoch und Ultra eine Lage kleiner Brocken, in Feldern dichter, Rand nass und grau. An der Fähre hält eine Rinne (Breite der Fähre + 1 m) das Randeis offen, weil der Prahm pendelt |
| Größere Seen nur Randeis 3–6 m (wie bisher) | `Eisregel.Rand`, von 0,1 bis 0,9 |
| Offenes Wasser im Winter (die Furt wirkte sommerlich) | `winter_ton` = 2,5 × Winterfortschritt (voll ab 0,4, kalt ist das Wasser, bevor es friert): Farbe bis 65 % zu einem dunklen, kühlen Grau, Lichtnetze bis −85 %; vor jeder Eiskante ein Saum Grieseis (gut 1 m, grau, matt, kaum Kräusel) |
| Das Eis wächst über den Winter und taut im Frühling | `Winterfortschritt`: Winter 0 → 1 über die ersten drei Fünftel des Spieljahres (erst schnell, dann langsamer), Frühling 1 → 0 im ersten Drittel; je Art `EisFortschritt` = (Winterfortschritt − `EisAb`) / (`EisBis` − `EisAb`) |

Was `--winter` zeigt: **0,3** Teich und Hafen (Wasser vor der Kaimauer) größtenteils zu, Bach nur dünne Ränder, Rhein noch offen; **0,6** stilles Wasser ganz zu, Bach halb, Rhein schmales Randeis und erste Schollen; **1,0** Bach zu bis auf die Löcher, an den Furten nur Randeis, Rhein 5 m Randeis und dichtes Treibeis.

Den Fortschritt liest die Darstellung aus dem Kern (`Spielzeit.Jahreszeit`, `Spielzeit.MinuteImTag` und die angebrochene Minute des Taktgebers; ein Spieltag ist ein Spieljahr). Eine **Ergänzung im Kern war nicht nötig**. Ist die Jahreszeit vorgegeben (`--jahreszeit`, Entwicklerfenster) und weicht sie von der Uhr ab, steht das Eis im Winter voll; `--winter=0..1` setzt es fest. Der Eisrand ist großflächig unregelmäßig (bis ±¼ der Reichweite, höchstens ±3 m), damit die offene Stelle eines Teiches nicht genau dem Umriss folgt. Ein **strenger Winter, in dem der Rhein zufriert**, wäre ein Ereignis des Kerns (notiert in `docs/bereiche/kern.md`).

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

**Kosten des Eises (30.10.2026).** Testszene, ein Fluss von 36 m über das ganze Bild (`--wassertest=1 --wasserart=fluss --jahreszeit=winter --winter=1 --wassersicht=oben --wassermessung=winter`, Stufe Hoch, T400, Bildzeit, Median aus drei Runden im selben Prozess): ohne Wasser 16,2 ms, Wasser ohne Eis 21,5 ms, mit Randeis und Schollen 25,0 ms – das Eis kostet hier **3,5 ms** (vor dem Sparen 6 ms: Feld-Rauschen je Bildpunkt statt je Zelle, nichts rechnen, wo weder Eis noch Grieseis hinreicht, keine Schollen im festen Randeis). Alle sieben Arten nebeneinander (weniger Wasser im Bild): +1,3 ms. Niedrig und Mittel zeichnen nur die großen Schollen (eine Lage statt zwei). Im Sommer, Herbst und Frühling nach der Schmelze kostet das Eis nichts (Shader überspringt es bei `eis` 0).

## Vorwärmen (für den Ladebildschirm)

```csharp
// Übersetzt den Wasser-Shader der aktuellen Stufe (oder aller drei) in einem eigenen kleinen SubViewport,
// zeichnet ihn vier Bilder lang, räumt auf und ruft dann fertig (Zeit im Log: „[wasser] vorgewärmt … in … ms“).
Wasserarten.Vorwaermen(Node eltern, bool alleStufen = false, Action? fertig = null);
```

Gedacht für `phase5/ladebildschirm`: aufrufen, solange der Ladebildschirm steht, z. B. `Wasserarten.Vorwaermen(this, fertig: () => _wasserBereit = true);`. Es braucht keine Stadt und keine Kamera. Ohne Aufruf wird der Shader dort übersetzt, wo das Wasser zuerst zu sehen ist – meist schon im Hauptmenü, dessen Hintergrundstadt (Greybox) einen Fluss hat. Gemessen in der Testszene (`--wassertest=1 --wasservorwaermen=1`, alle drei Shader, T400): **8,4 s** mit leerem Shader-Zwischenspeicher (erster Start), **0,9 s** mit gefülltem (das sind dann nur die vier Bilder). Die übrigen 10–20 s des ersten Starts sind die anderen Shader der Szene; dagegen hilft der Shader-Baker beim Export (siehe Grafik-Leistung, `docs/bereiche/darstellung.md`).

## Bilder

Alle selbst angesehen (T400, Vulkan, 1600 × 900). Phase 3 (Einbau, Stufen): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3/bilder/<name>/a.png`. Eis nach Gewässerart und freie Kamera (30.10.): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3b/<name>.png` (nicht im Repository). Alle Bilder vom 30.10. kommen aus der freien Kamera.

| Bild | Zu sehen |
|---|---|
| `hafen_0.3`, `hafen_0.6`, `hafen_1.0` | Rheinhafen schräg von oben, Winter 0,3 / 0,6 / 1,0: Bei 0,3 liegt vor der Kaimauer und um die Kähne schon Eis, der Rhein ist offen und kalt-blau; bei 0,6 der Hafen ganz zu, am Rhein ein schmaler Eisrand und einzelne Schollen; bei 1,0 5 m Randeis und Treibeis in Feldern, mal dicht, mal fast frei. |
| `hafenoben_0.3` | Derselbe Hafen von oben: Eis von der Kaimauer bis an die Kähne, davor offenes Wasser. |
| `muendung_0.3`, `muendung_0.6`, `muendung_1.0` | Mündung des Duffesbachs in den Rhein: bei 0,3 nur dunkleres, kaltes Wasser; bei 0,6 der Bach halb zu (Ränder), am Rhein schmales Randeis mit Lücke an der Mündung; bei 1,0 der Bach zu bis auf ein kleines Loch an der Mündung, im Randeis des Rheins eine Lücke. |
| `furt_0.3`, `furt_0.6`, `furt_1.0` | Duffesbach mit zwei Furten und der schnellen Stelle dazwischen: bei 0,3 dunkles, kaltes Wasser ohne Sommerlicht; bei 1,0 der Bach zu, an den Furten offen mit schmalem Randeis und grauem Grieseis-Saum, an der schnellen Stelle ein Loch. |
| `faehre_0.3`, `faehre_0.6`, `faehre_1.0` | Rhein mit Fähre Mülheim: 0,3 offen; 0,6 schmales Randeis, wenige Schollen; 1,0 Randeis, an beiden Anlegern eine offene Rinne für den Prahm, dichtes Treibeis. |
| `bodennah_0.6` | Freie Kamera 2,5 m über dem Ufer an der Mündung: der zugefrorene Bach im Vordergrund, dahinter der Rhein, am Horizont Stadt, Brücke, Hügel. |
| `horizont`, `hoch_dunst`, `nebel_hoch` | Freie Kamera über Köln: aus 40 m Höhe über das Land zum Horizont (Sommer, 10 Uhr), aus 350 m im Morgendunst (Ultra, 6 Uhr) und bei Herbstnebel. Der Kartenrand läuft in den Himmel aus, ohne Loch und ohne Kante; der Bodennebel endet weich. Bei Nebel bleibt der ferne Boden etwas heller als der Himmel. |
| `hoch_dunst_vorher`, `horizont_vorher` | Dasselbe vor der Korrektur: Der Randdunst stand als hellblaue Platte mit gerader Kante vor dem dunkleren Himmel, der Bodennebel endete mit gerader Kante. |
| `teich_0.15`, `teich_0.3` | Mühlenteich (Probestadt): bei 0,15 Eisrand mit offener Mitte, bei 0,3 fast ganz zu. |
| `werkstatt_see` | See von etwa 3.000 m² in der Kartenwerkstatt mit freier Kamera: nur Randeis. |
| `klick_a`, `klick_b` | Freie Kamera auf der Spielkarte, dann Linksklick auf den Hafen: die Tafel öffnet sich wie mit der Spielkamera. |
| `faehre_1.0_mittel`, `faehre_1.0_niedrig` | Dasselbe in den Stufen Mittel und Niedrig: nur die großen Schollen, Randeis wie in Hoch. |
| `wassertest_0.6` | Testszene, alle Arten bei 0,6 (frei befliegbar, Hilfe mit H): Fluss mit Randeis und Schollen, Bach halb, Teich zu, See Randeis, Meer offen, Hafenbecken vom Kai her, Furt mit Randeis. |

## Was mir nicht gefällt, offen

- **Schmale Bäche** (unter dem 2-m-Raster des Bodens) liegen als Band knapp über der Wiese: im Sommer ein durchsichtig blauer Streifen ohne Bett und Böschung (wie schon beim alten Wasser). Ein eingeschnittenes Bett bräuchte das Gelände (Kern) oder Uferkanten als Modell.
- **Kosten:** Das neue Wasser kostet in der Hafenansicht in Hoch und Ultra etwa doppelt so viel GPU-Zeit wie das alte, in Niedrig gut 40 % mehr (Tabelle oben). Teuer ist die Fläche in Pixeln (durchsichtig, beleuchtet, mit Schatten); Brechung, Tiefe und feines Netz kommen je unter einer Millisekunde dazu.
- **Furt:** Im Spiel ist die Furt Teil des Bach- bzw. Flussbandes; die eigenen Werte der Art Furt (klarer, schwächere Lichtnetze) sind dort nicht eingebaut. Im Winter ist das offene Wasser jetzt kalt und dunkel; die offene Strecke um eine Furt ist aber lang (halbe Wegbreite + 3 m, Übergang bis 6 m – am 3 m breiten Duffesbach gut 15 m), zusammen mit der schnellen Stelle ist zwischen den Furten bei −61 und −34 fast alles offen.
- **Gleichförmigkeit:** Aus der Spielhöhe sind Kräusel und Schaumsaum recht gleichmäßig; Wellengruppen und Strömungsunterschiede (Mitte schneller als Ufer) sieht man kaum. Die Strömung je Ecke ist im Netz (UV2), wird aber nur für die Richtung genutzt.
- **Schollen** drehen sich nicht, stoßen nicht an Pfeiler, Ufer oder die Fähre und treiben unter der Brücke und unter dem Prahm durch; sie sind ein Muster auf dem Wasser, keine Körper. Ihre Dichte schwankt in Feldern, ist aber nicht mit dem Randeis verbunden (keine abbrechenden Stücke).
- **Hafenbecken in Köln:** Das Becken ist nur 4–6 m breit und liegt unter den Kähnen; zu sehen ist vor allem das stille Flusswasser vor der Kaimauer (friert wie das Becken). Die Kähne stehen im Eis ohne eigene Rinne.
- **Schnelle Stellen** kommen nur aus dem Gefälle des Spiegels (ab 3,5 %); der Duffesbach hat eine (4,5 %), der Mühlbach zwei (8 %). Die Schwelle ist geschätzt. In der Kartenwerkstatt nennt die Hilfezeile unten weiter „Q/E dreht“, mit freier Kamera fahren Q/E aber die Kamera.
- **Freie Kamera:** Gras, Feinheiten und die Bewegung der Figuren gelten nur um den Punkt, auf den sie blickt (die Spielkamera folgt ihm); weiter weg fehlt das Gras, ferne Figuren stehen. Wolkenschatten nur 1 km um diesen Punkt. Der Wetter-Nebel ist für 150 m Abstand gerechnet, von hoch oben wird der Boden heller als der Himmel (weicher Übergang, keine Kante). Der Bodennebel liegt von oben als flache Schicht über dem Rhein. Godot zeichnet die Hintergrundfarbe dunkler als dieselbe Farbe im Ring am Kartenrand; mit der freien Kamera bekommt der Ring die dunklere Farbe (die Spielkamera sieht den Himmel nie, dort bleibt alles wie bisher).
- **Mittel/Niedrig:** Der Flachwassersaum am Ufer ist gelblich-grün und breiter als in Hoch; die Stufen sehen nicht ganz gleich aus.
- **Grundspiegelung:** `himmel_grund` ist ein Ersatz für einen fehlenden Himmel. Bekäme die Stadt eine echte Himmelsquelle (`WorldEnvironment` mit Sky), sollte er kleiner werden.

## Testanleitung für den Nutzer (Windows)

Godot 4.7.2 (.NET) mit **Vulkan**; im Ordner des Branches `phase5/wasser-darstellung` erst bauen (`dotnet build Reichsgilde.sln`), dann aus der Eingabeaufforderung (`godot` = Pfad zu `Godot_v4.7.2-stable_mono_win64.exe`).

**Freie Kamera.** Alle Schritte gehen mit `--freiekamera=1`: Die Kamera beginnt im Blick von `--blick` (schräg von oben wie im Spiel), danach fliegst du frei:

| Taste | Wirkung |
|---|---|
| rechte Maustaste halten | umsehen (die Maus ist gefangen, solange die Taste gedrückt ist) |
| W A S D | vor, links, zurück, rechts in Blickrichtung (auch hinauf und hinab) |
| Q / E | senkrecht runter / hoch |
| Shift | viermal so schnell |
| Mausrad | Grundtempo (12 m/s zu Beginn, je Raste × 1,25) |
| F2 | Hilfe oben rechts aus und ein (in der Testszene `--wassertest=1`: H) |

Ohne gedrückte rechte Maustaste bleibt die Maus frei: Tafeln und Klicks auf Gebäude gehen wie gewohnt. Ganz nah an Eis und Wasser: bis auf 1–2 m über dem Wasser hinunter (Q), dann langsam (Mausrad zurück).

**Wo welches Gewässer liegt (Köln, Meter):** Der Rhein fließt von Süden (z = +550) nach Norden (z = −500), seine Mitte bei x ≈ 90–110, 60–100 m breit. Der **Rheinhafen** mit Tretradkran liegt am linken Ufer bei (51, −60), das Hafenbecken davor ist schmal (4–6 m) und liegt unter den Kähnen; das stille Wasser vor der Kaimauer friert wie das Becken. Der **Duffesbach** (3 m) kommt von Westen (−215, 150) und mündet bei (60, 124) in den Rhein; seine **Furten** liegen bei (−205, 147), (−61, 109), (−34, 108) und (37, 121), eine **schnelle Stelle** bei (−43, 108) zwischen zwei Furten. Die **Fähre Mülheim** quert den Rhein bei z = −200 (von x = 51 nach 146).

1. **Winter im Verlauf (Köln):** `godot --path game -- --karte=koeln --jahreszeit=winter --startstunde=11 --wetter=klar --freiekamera=1 --winter=0.3` (dann `0.6` und `1.0`), jeweils mit
   - `--blick=58,-62,40` (Hafen), `--blick=60,122,32` (Mündung des Duffesbachs), `--blick=-47,109,30` (Duffesbach mit zwei Furten und schneller Stelle), `--blick=98,-200,80` (Rhein mit Fähre).
   - Erwartet bei **0,3**: vor der Kaimauer schon Eis, der Bach nur dünne Ränder, der Rhein offen, das offene Wasser schon dunkler und kälter als im Sommer. Bei **0,6**: stilles Wasser ganz zu, der Bach zu gut der Hälfte zu, am Rhein ein schmaler Eisrand und die ersten Schollen. Bei **1,0**: der Bach zu, offen nur ein kleines Loch an der Mündung und an der schnellen Stelle, an den Furten nur Randeis (Mitte offen, dunkel, mit grauem Saum Grieseis), am Rhein 5 m Randeis mit Lücke an der Mündung und Rinne an der Fähre, dazu treibende Schollen in Feldern, die mit der Strömung nach Norden ziehen.
2. **Bodennah:** `--karte=koeln --jahreszeit=winter --winter=0.6 --startstunde=11 --freiekamera=1 --freiekamera-start=44,2.5,134:66,-1,121` – aus 2,5 m Höhe auf die Mündung; dann mit W/A/S/D am Ufer entlang fliegen.
3. **Teich (Probestadt):** `--karte=probestadt --blick=130,-100,35 --jahreszeit=winter --freiekamera=1 --winter=0.3` (schon größtenteils zu) und `--winter=0.15` (offene Mitte).
4. **Sommer zum Vergleich:** `--karte=koeln --blick=75,-60,45 --startstunde=10 --freiekamera=1` (Hafen), `--blick=70,40,90` (Rhein mit Brücke). Erwartet: blaugraues, fließendes Wasser mit Kräuseln, Schaumsaum an beiden Ufern des Rheins, am Hafen davor nur ein schmaler Saum, im Becken ruhiges Wasser.
5. **Sturm und Regen:** dasselbe mit `--wetter=sturm` (dunkler, unruhiger) und `--wetter=regen --blick=60,-70,25` (Ringe auf dem Wasser; nur mit den Wettereffekten Hoch, also Voreinstellung Hoch oder Ultra).
6. **Hochfliegen:** in Schritt 4 mit E und Shift auf 300–500 m steigen. Erwartet: die Stadt mit ihrem Rand, der in die Himmelsfarbe ausläuft, kein Loch und keine harte Kante; mit `--voreinstellung=Ultra --startstunde=6` auch im Morgendunst.
7. **Randeis an einem See:** `--kartenwerkstatt=probestadt --werkstatt-see=-180,20;-120,10;-110,60;-170,75 --blick=-145,40,50 --werkstatt-kamerastart=1 --werkstatt-ebenen=alle --winter=1 --freiekamera=1`: nur ein Eisrand von gut 3 m, die Mitte offen.
8. **Testszene:** `--wassertest=1 --jahreszeit=winter --winter=0.6` zeigt alle Arten nebeneinander (Fluss mit Schollen, Bach, Teich, See, Meer, Hafen, Furt), frei zu befliegen (Hilfe mit H aus).
9. **Eis über das Jahr:** eine Partie im Herbst bis in den Winter laufen lassen (Tempo hoch): erst wird das Wasser kälter, dann frieren Teich und Hafen, danach die Bäche, zuletzt bekommt der Rhein Randeis und Schollen; im Frühling taut alles in den ersten Stunden.
10. **Grafikstufen:** Einstellungen → Voreinstellung Niedrig, Mittel, Hoch, Ultra umschalten, während Köln offen ist; das Wasser stellt sich sofort um. Oder `--wasserstufe=niedrig` (bis `ultra`).
11. **Bildrate:** `--karte=koeln --blick=75,-60,45 --vsync=Aus --wasservergleich=1` misst gut zwei Minuten lang reihum und schreibt `[wassermessung] …` ins Protokoll, dann beendet sich das Spiel.
12. **Zurückmelden:** (a) Eis je Gewässerart bei 0,3, 0,6, 1,0 – stimmt die Reihenfolge, gefallen Schollen und Randeis? (b) Wirkt das offene Wasser im Winter kalt genug (Furt)? (c) Freie Kamera: Steuerung, Tempo, stört etwas beim Hochfliegen (Nebel, Kanten, fehlendes Gras)? (d) Fehler (Kanten, Flackern, schwarzes oder fehlendes Wasser).
