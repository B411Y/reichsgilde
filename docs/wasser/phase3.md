# Realistisches Wasser, Phase 3: Einbau ins Spiel und Grafikstufen

Stand 30.10.2026, Branch `phase5/wasser-darstellung` (Eis nach Gewässerart und freie Kamera seit 30.10.; Phase 3c – Eis, Schollen als Körper, Mündung, Flusswellen – am Ende; Schollen 2 – ruhiger Stau, Bruch, Schmelzen, Branch `phase5/schollen-2` – ganz am Ende). Alle Gewässer des Spiels (Köln mit Rhein, Duffesbach und Hafen, Probestadt, Greybox, Kartenwerkstatt) zeichnet jetzt das realistische Wasser aus Phase 2; das alte `wasser.gdshader` ist entfernt. Phase 1 (Herkunft, Lizenz): [phase1.md](phase1.md), Phase 2 (Arten, Shader, Testszene): [phase2.md](phase2.md).

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
| Köln | Duffesbach | Bach, 3 m | Bach | nein | ganz; an den zwei Furten nur Randeis, Löcher an der Mündung (60, 168) und an einer schnellen Stelle (13, 165) (seit 31.10.2026 als Graben vor der Südmauer; vorher vier Furten, Mündung (60, 124), schnelle Stelle (−43, 108)) |
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
| **Rhein und breite Flüsse samt Fährstelle**: Randeis und treibende Schollen, nie eine Decke | `Eisregel.Rand`, Randeis 5 % der Breite (1,2–5 m), von 0,3 bis 1; Schollen seit Phase 3c als Körper (`Schollenfeld`, siehe Phase 3c unten; vorher ein Muster im Shader). An der Fähre hält eine Rinne (Breite der Fähre + 1 m) das Randeis offen, weil der Prahm pendelt |
| Größere Seen nur Randeis 3–6 m (wie bisher) | `Eisregel.Rand`, von 0,1 bis 0,9 |
| Offenes Wasser im Winter (die Furt wirkte sommerlich) | `winter_ton` = 2,5 × Winterfortschritt (voll ab 0,4, kalt ist das Wasser, bevor es friert): Farbe bis 65 % zu einem dunklen, kühlen Grau, Lichtnetze bis −85 %; vor jeder Eiskante ein Saum Grieseis (gut 1 m, grau, matt, kaum Kräusel) |
| Das Eis wächst über den Winter und vergeht im Eisgang am Winterende (seit Phase 3e: Eis nur im Winter) | `Winterfortschritt`: Winter 0 → 1 über die ersten 55 % des Spieljahres (erst schnell, dann langsamer), dann Eisgang (`Tauwetter`, 72–96 % des Winters), im Frühling kein Eis mehr; je Art `EisFortschritt` = (Winterfortschritt − `EisAb`) / (`EisBis` − `EisAb`) |

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
- **Schollen:** seit Phase 3c Körper, die schaukeln, sich drehen und Pfeilern, Prahm und Kähnen ausweichen (siehe Phase 3c, dort auch, was offen bleibt).
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

**Wo welches Gewässer liegt (Köln, Meter):** Der Rhein fließt von Süden (z = +550) nach Norden (z = −500), seine Mitte bei x ≈ 90–110, 60–100 m breit. Der **Rheinhafen** mit Tretradkran liegt am linken Ufer bei (51, −54) (bis 1.11.2026 bei (51, −60)), das Hafenbecken davor ist schmal (4–6 m) und liegt unter den Kähnen; das stille Wasser vor der Kaimauer friert wie das Becken. Der **Duffesbach** (3 m) kommt von Westen (−215, 150), läuft seit dem 31.10.2026 als Graben vor der Südmauer und mündet bei (60, 168) hinter dem Bayentor in den Rhein; seine **Furten** liegen bei (−205, 147) (Luxemburger Straße) und (43, 167) (Leinpfad Süd), eine **Holzbrücke** bei (−19, 159) (Bonner Straße), eine **schnelle Stelle** bei (13, 165) (bis dahin lief er durch die Südstadt: Mündung (60, 124), Furten bei (−61, 109), (−34, 108) und (37, 121), schnelle Stelle bei (−43, 108)). Die **Fähre Mülheim** quert den Rhein bei z = −200 (von x = 51 nach 146).

1. **Winter im Verlauf (Köln):** `godot --path game -- --karte=koeln --jahreszeit=winter --startstunde=11 --wetter=klar --freiekamera=1 --winter=0.3` (dann `0.6` und `1.0`), jeweils mit
   - `--blick=58,-62,40` (Hafen), `--blick=58,166,32` (Mündung des Duffesbachs mit der Furt am Leinpfad), `--blick=10,164,30` (Duffesbach mit schneller Stelle vor der Südmauer), `--blick=98,-200,80` (Rhein mit Fähre).
   - Erwartet bei **0,3**: vor der Kaimauer schon Eis, der Bach nur dünne Ränder, der Rhein offen, das offene Wasser schon dunkler und kälter als im Sommer. Bei **0,6**: stilles Wasser ganz zu, der Bach zu gut der Hälfte zu, am Rhein ein schmaler Eisrand und die ersten Schollen. Bei **1,0**: der Bach zu, offen nur ein kleines Loch an der Mündung und an der schnellen Stelle, an den Furten nur Randeis (Mitte offen, dunkel, mit grauem Saum Grieseis), am Rhein 5 m Randeis mit Lücke an der Mündung und Rinne an der Fähre, dazu treibende Schollen in Feldern, die mit der Strömung nach Norden ziehen.
2. **Bodennah:** `--karte=koeln --jahreszeit=winter --winter=0.6 --startstunde=11 --freiekamera=1 --freiekamera-start=44,2.5,134:66,-1,121` – aus 2,5 m Höhe auf die Mündung; dann mit W/A/S/D am Ufer entlang fliegen.
3. **Teich (Probestadt):** `--karte=probestadt --blick=130,-100,35 --jahreszeit=winter --freiekamera=1 --winter=0.3` (schon größtenteils zu) und `--winter=0.15` (offene Mitte).
4. **Sommer zum Vergleich:** `--karte=koeln --blick=75,-60,45 --startstunde=10 --freiekamera=1` (Hafen), `--blick=70,40,90` (Rhein mit Brücke). Erwartet: blaugraues, fließendes Wasser mit Kräuseln, Schaumsaum an beiden Ufern des Rheins, am Hafen davor nur ein schmaler Saum, im Becken ruhiges Wasser.
5. **Sturm und Regen:** dasselbe mit `--wetter=sturm` (dunkler, unruhiger) und `--wetter=regen --blick=60,-70,25` (Ringe auf dem Wasser; nur mit den Wettereffekten Hoch, also Voreinstellung Hoch oder Ultra).
6. **Hochfliegen:** in Schritt 4 mit E und Shift auf 300–500 m steigen. Erwartet: die Stadt mit ihrem Rand, der in die Himmelsfarbe ausläuft, kein Loch und keine harte Kante; mit `--voreinstellung=Ultra --startstunde=6` auch im Morgendunst.
7. **Randeis an einem See:** `--kartenwerkstatt=probestadt --werkstatt-see=-180,20;-120,10;-110,60;-170,75 --blick=-145,40,50 --werkstatt-kamerastart=1 --werkstatt-ebenen=alle --winter=1 --freiekamera=1`: nur ein Eisrand von gut 3 m, die Mitte offen.
8. **Testszene:** `--wassertest=1 --jahreszeit=winter --winter=0.6` zeigt alle Arten nebeneinander (Fluss mit Schollen, Bach, Teich, See, Meer, Hafen, Furt), frei zu befliegen (Hilfe mit H aus).
9. **Eis über das Jahr:** eine Partie im Herbst bis in den Winter laufen lassen (Tempo hoch): erst wird das Wasser kälter, dann frieren Teich und Hafen, danach die Bäche, zuletzt bekommt der Rhein Randeis und Schollen; am Abend des Winters bricht alles im Eisgang auf und vergeht, der Frühling beginnt ohne Eis und Schnee.
10. **Grafikstufen:** Einstellungen → Voreinstellung Niedrig, Mittel, Hoch, Ultra umschalten, während Köln offen ist; das Wasser stellt sich sofort um. Oder `--wasserstufe=niedrig` (bis `ultra`).
11. **Bildrate:** `--karte=koeln --blick=75,-60,45 --vsync=Aus --wasservergleich=1` misst gut zwei Minuten lang reihum und schreibt `[wassermessung] …` ins Protokoll, dann beendet sich das Spiel.
12. **Zurückmelden:** (a) Eis je Gewässerart bei 0,3, 0,6, 1,0 – stimmt die Reihenfolge, gefallen Schollen und Randeis? (b) Wirkt das offene Wasser im Winter kalt genug (Furt)? (c) Freie Kamera: Steuerung, Tempo, stört etwas beim Hochfliegen (Nebel, Kanten, fehlendes Gras)? (d) Fehler (Kanten, Flackern, schwarzes oder fehlendes Wasser).

## Phase 3c: Eis, Schollen als Körper, Mündung, Flusswellen (30.10.2026)

Nach dem Test des Nutzers (Bilder bei Regen im Winter): Das Eis wirkte flach – das Randeis endete am Ufer mit einer Linie wie mit dem Lineal, die Risse waren dicke Comic-Linien, die Schollen flache Pillen; an der Mündung des Duffesbachs lag ein eckiger Schaumfleck über dem Eis, im Sommer endete der Bach an einem Schaumband mit harter Kante; der Rhein hatte kaum Wellen.

| Wunsch | Umsetzung | Dateien |
|---|---|---|
| Eis realistisch, keine geraden Kanten, keine dicken Risse | Das Eis ist ein **Mosaik gebrochener Platten** (Voronoi-Zellen, 1,3–3,6 m je nach Reichweite): Die Kante des Randeises folgt den Plattengrenzen (eckig gebrochen, leicht verzogen), großflächig bis ±42 % der Reichweite vor- und zurückspringend und je Platte etwas anders – nicht mehr parallel zum Ufer. **Feine Risse** sind Null-Linien von Rauschen, etwa einen Bildpunkt breit, nur stückweise, aus der Ferne ausgeblendet; die Fugen zwischen den Platten nur nah, an der Kante teils offen (bis 9 cm, Wasser darin). **Klares, dunkles Eis neben weißem** (großflächig wechselnd, zur Kante hin mehr klares, junges Eis; im klaren Luftblasen, nur ganz nah); im klaren Eis sind Risse weiß, im weißen grau. Ganz zufrierendes Wasser (Bach, Teich) bleibt eine zusammenhängende Decke mit wenigen, schwachen Fugen | `wasser_realistisch.gdshaderinc` (`eis_zellen`) |
| Eiskante mit Dicke | An der Bruchkante ein heller Streifen (die Stirn fängt das Licht), davor unter Wasser die Stirn der Platte (blaugrün, gut 20 cm) und direkt an der Kante ein dunkler Schattenstrich | wie oben |
| Übergänge sanfter, auch zum Ufer | **Zur Wasserseite:** vor der Kante ein löchriger Saum Grieseis (gut 1,5 m), der mit dem Wasser treibt. **Zum Ufer:** Ursache der Linie war, dass das Wasserband am Uferabstand 0 über 35 cm ausblendet und das Eis bis dorthin reichte. Jetzt hört das Eis davor unregelmäßig auf (`ufer_saum`: Fluss und See bis 1,3 m, Bach und Teich nach ihrer Größe 0,25–1 m, an der Kaimauer gar nicht) und mit Tiefentextur auch dort, wo das Wasser nur noch wenige Zentimeter tief ist; dazwischen liegt dünnes klares Eis auf dem Grund (entsättigt, bräunlich, mit Reif) – eine weiche, unregelmäßige Grenze statt einer Naht. Bei Schnee geht das Eis in den Schnee am Ufer über | `wasser_realistisch.gdshaderinc`, `Wasserarten.Anwenden` |
| Schollen als 3D-Körper, „fast wie Glas“ | Je Fluss ein **`Schollenfeld`**: alle Schollen sind Kopien einer runden Scheibe (fein 24 Ecken, fern 10) in einem `MultiMesh`; den **Umriss** rechnet der Shader je Scholle aus ihrem Startwert – ein Vieleck aus 5–9 Bruchkanten, bis 40 % länglich, bei der Hälfte ein ausgebrochenes Stück, zackiger Rand –, dazu **Dicke 8–32 cm** (12 % über Wasser), gerundete Oberkante, buckelige Oberseite, bei einem Teil ein aufgeworfener Rand (Pfannkucheneis). **Aussehen** (`eis_scholle.gdshaderinc`): Oberseite matt weiß mit Wolken, Reif, Schnee und klaren, dunklen Stellen, bei manchen Schwebstoff; der Rand nass, dunkel und glänzend; die **Stirn durchscheinend blaugrün** (unten satter: die Dicke färbt), leuchtet leicht aus dem Eis heraus, Fresnel-Spiegelung des Himmels, Licht von hinten (`BACKLIGHT`); in Hoch und Ultra **Luftblasen in zwei Tiefen** (gegeneinander verschoben mit dem Blick: Tiefe im Eis) und Schlieren in der Stirn. In Hoch zeigt die Brechung des Wassers den Teil unter Wasser. **Bewegung:** Das ganze Feld treibt mit 0,9 × Strömung (keine Scholle läuft durch eine andere), jede dreht sich langsam, **schaukelt und kippt mit denselben Wellen** wie die Wasserfläche (große träger). **Ausweichen:** Abstand vom Randeis (mit dem Winter breiter), seitlich an Brückenpfeilern (Rhein: 6), dem Prahm der Fähre und den Kähnen vorbei. **Verteilung:** von groß nach klein, ohne Berührung, in Feldern dicht und dazwischen fast frei (etwa 50 m), fester Startwert; Rhein 4.722 Schollen auf 1.051 m, je Scholle eine Schwelle: Sie wächst auf, sobald das Eis der Art sie erreicht. Gezeichnet werden nur die im Bild (Sichtkegel, sehr kleine nur nah) | `Schollenfeld.cs`, `Stadtansicht.Eisschollen.cs`, `eis_scholle*.gdshader*` |
| Mündung sauber, Bachwasser fließt sichtbar in den Fluss | **Löcher im Eis** gehen als Uniforms an den Shader (`loch_ort`, `loch_form`: Kapseln, rund, weich, unregelmäßiger Rand) statt über die 2-m-Netzecken (dort wurden sie eckig). Sie wirken je Platte (an deren Mitte), ihr Rand bricht also wie die übrige Eiskante. Im Bach ein Loch an der Mündung, im Fluss eine **Rinne stromab am Ufer** (6 + 3 × halbe Bachbreite lang, Duffesbach gut 10 m): das Bachwasser hält sie offen. Die Felder gehen als Zahlenreihe an den Shader (ein Farbfeld nimmt Godot für `vec4`-Felder nicht an). Der **Bach blendet** vom Rand des Flusses 1 m davor bis 3 m hinein weich aus und **fällt dabei auf den Spiegel des Flusses** (der Duffesbach liegt an der Mündung 0,55 m höher; vorher endete er als Platte). Im Fluss zieht das **Bachwasser als heller, trüber Streifen** am Ufer stromab (breiter werdend, in Schlieren, mit der Strömung treibend, nach etwa 60 m eingemischt; `muendung[]` in den Laufachsen des Flusses), mit **dünnen Schaumstreifen an der Mischkante**; der Uferschaum ist an der Mündung aus (dort war der flache Grund die Ursache des Flecks), und im Winter ist Schaum grauer Matsch statt weißem Sommerschaum. Schaum über Eis gibt es nicht | `Stadtansicht.Wasser.cs` (`Muendung`, `LoecherSetzen`, `MuendungenSetzen`, `EinMuendung`), Shader |
| Wellen auf dem Fluss wie beim Meer, je Abschnitt zufällig, wetterabhängig | Die Wellen stehen in **`wasser_wellen.gdshaderinc`** (gemeinsam für Wasser und Schollen). Flüsse rechnen sie in ihren **Laufachsen** (sie folgen den Kurven, Hauptrichtung längs mit der Strömung). Fluss jetzt mit fünf Zügen (Längen 9, 4,7, 19 und 7 m; kürzere fallen unter das 2-m-Netz und bleiben als Kräusel in den Normalkarten), deren Stärke als großflächiges Rauschen wandert (`Vielfalt` 0,8, Felder 40–110 m): **je Abschnitt andere Größe, anderes Tempo, andere Stärke**; der **Startwert** kommt aus dem Namen des Gewässers in der Karte (`Wasserarten.Saat`). Höhe ruhig etwa 6–10 cm, mit dem Wind höher (`wind_hoehe` 2,2 auf leichten Wind bis Sturm, dazu der allgemeine Windanteil): **Sturm etwa 25–35 cm**, Windrichtung lenkt die Züge. Bäche bleiben glatt (ihre Wellen sind kürzer als das Netz) | `wasser_wellen.gdshaderinc`, `Wasserarten.WellenSetzen` |

**Abweichung von der Vorgabe:** Lichtbrechung durch die Schollen über die Bildschirmtextur (Hoch/Ultra) ist nicht eingebaut: Ein Shader mit Bildschirmtextur zeichnet Godot im durchsichtigen Durchgang; die Schollen wären dann mit dem Wasser zu sortieren (ein MultiMesh für den ganzen Fluss), und die Bildschirmtextur enthielte das Wasser nicht – unter einer Scholle erschiene der Grund statt des Flusses. Stattdessen sind die Schollen undurchsichtige Körper (richtige Tiefe, kein Sortieren), die Tiefe im Eis kommt aus den gegeneinander verschobenen Luftblasen, das Durchscheinende aus `BACKLIGHT` und dem Leuchten der Stirn; den Teil unter Wasser bricht in Hoch das Wasser selbst.

### Kosten (T400, gegen ad72bb82)

Gemessen wie oben im selben Prozess reihum. Der Rechner war beim Nachher-Lauf stark belastet (Last 10–21, eine fremde Godot-Sitzung auf der Grafikkarte); die Runden schwankten deshalb um bis zu 30 ms. Angegeben ist die ruhigste Runde, die Werte sind entsprechend unsicher (etwa ±1 ms).

| Messung | vorher (ad72bb82) | nachher (Phase 3c) |
|---|---|---|
| Köln, Rhein mit Fähre (`--blick=98,-200,80 --jahreszeit=winter --winter=1.0 --wasservergleich=1`), GPU-Zeit ohne Wasser | 37,0 ms | 37,7 ms |
| – Wasser in Stufe Niedrig / Mittel / Hoch / Ultra | +6,4 / +8,2 / **+9,6** / +11,7 ms | +5,1 / +6,6 / **+7,3** / +10,2 ms |
| Testszene, Fluss über das ganze Bild (`--wassermessung=winter`, Bildzeit), Eis gegen ohne Eis | +2,8 ms (20,8 → 23,6) | +3,6 ms (22,2 → 25,8) bei Last 10; +1,4 ms bei Last 21 |

Im Spiel ist das Wasser mit Eis in Hoch damit eher **gut 2 ms billiger** als vorher: Das Muster der Schollen kostete jeden Bildpunkt des offenen Flusses (zwei Lagen Zellen), jetzt rechnet der Shader Eis nur nahe am Ufer, und die Schollen sind Netze (nah 264 Dreiecke, fern 50, gezeichnet nur die im Sichtkegel). Bildfüllend im Testfeld bleibt das Eis bei etwa +3 ms (Ziel: nicht deutlich über +3,5 ms). Die Lage der Schollen schreibt die CPU jedes Bild (alle Schollen über der Schwelle durchgehen, nur sichtbare schreiben); im Sommer, Herbst und nach der Schmelze kostet beides nichts.

### Testanleitung (Windows)

Wie oben bauen (`dotnet build Reichsgilde.sln`), dann aus der Eingabeaufforderung (`godot` = Pfad zu `Godot_v4.7.2-stable_mono_win64.exe`). **Wetter erzwingen:** `--wetter=klar|regen|sturm|schnee` (Regenringe nur mit Wettereffekten Hoch, also Voreinstellung Hoch oder Ultra). **Eis:** `--winter=0..1` (fest). Seit Phase 3e gibt es Eis nur im Winter: `--winter` über 0 zeigt ohne `--jahreszeit` den Winter, also mit Schnee am Boden (vorher lag ohne `--jahreszeit` kein Schnee, die Ansicht zeigte Frühling mit Eis).

**Wo die Mündung liegt:** Der Duffesbach mündet bei **(60, 168)** (bis 31.10.2026 bei (60, 124)) von Westen in den linken (westlichen) Rand des Rheins; der Rhein fließt nach Norden (z wird kleiner), das Bachwasser zieht also am linken Ufer nach Norden (Richtung Brücke, die bei z ≈ 20–40 über den Rhein geht).

1. **Randeis am Ufer, Regen (wie Bild 67/68):** `godot --path game -- --karte=koeln --startstunde=11 --freiekamera=1 --winter=1.0 --wetter=regen --freiekamera-start=50,7,100:63,0,86`, ganz nah: `--freiekamera-start=57,2,92:61.5,-0.5,88`. Erwartet: Randeis mit gebrochener, eckiger Kante, die vor- und zurückspringt; davor ein grauer, löchriger Grieseis-Saum; am Ufer hört das Eis unregelmäßig auf, dazwischen dünnes Eis auf dem schlammigen Grund – keine gerade Linie; feine Risse statt dicker Linien.
2. **Schollen bodennah (wie Bild 69):** `… --winter=1.0 --wetter=regen --freiekamera-start=72,2.2,70:80,-0.3,62`, mit Schnee: `--jahreszeit=winter --wetter=klar`. Erwartet: Schollen mit Dicke und gebrochenem Umriss, nasser, glänzender Rand, blaugrün durchscheinende Stirn, sie schaukeln, drehen sich langsam und treiben nach Norden; dichte Felder und freie Strecken.
3. **Mündung von oben (wie Bild 70):** `… --winter=1.0 --wetter=regen --freiekamera-start=58,16,130:61,0,123`. Erwartet: der Bach zu bis auf ein Loch an der Mündung (rund, weicher Rand), im Randeis des Rheins eine offene Rinne stromab am Ufer, kein Schaumfleck, nichts liegt über dem Eis.
4. **Mündung im Sommer (wie Bild 71):** `… --winter=0 --wetter=regen --freiekamera-start=42,9,142:62,0,122` und schräg von oben `--wetter=klar --freiekamera-start=40,32,150:66,0,112`. Erwartet: Der Bach läuft über die letzten Meter auf den Spiegel des Rheins hinab und blendet weich ein; am Ufer stromab ein hellerer, trüber Streifen mit Schlieren und dünnen Schaumlinien; an der Mündung kein Uferschaum.
5. **Flusswellen:** `… --winter=0 --wetter=klar --freiekamera-start=60,6,40:85,0,20` (ruhig) und dasselbe mit `--wetter=sturm` (deutlich höhere, unruhigere Wellen; mit Schollen: `--winter=1.0 --wetter=sturm`, sie schaukeln stärker). Mit W/A/S/D den Rhein entlang fliegen: Abschnitte mit größeren und kleineren, schnelleren und langsameren Wellen.
6. **Übersicht:** `… --jahreszeit=winter --winter=1.0 --wetter=klar --freiekamera-start=40,35,170:80,0,110` (Rhein mit Brücke, Randeis, Schollenfeldern; die Schollen umfahren die Pfeiler).
7. **Fähre:** `--blick=98,-200,80` mit `--winter=1.0`: Die Schollen weichen dem pendelnden Prahm aus.
8. **Grafikstufen:** `--wasserstufe=niedrig` bzw. `mittel` (Schollen ohne Luftblasen, Niedrig ohne die kleinsten), Testszene `--wassertest=1 --wasserart=fluss --jahreszeit=winter --winter=1 --wassersicht=nah`.
9. **Zurückmelden:** (a) Wirkt das Eis jetzt echt (Kante, Risse, klares und weißes Eis, Übergang zum Ufer)? (b) Schollen: Form, Dicke, Glas-Wirkung, Menge? (c) Mündung im Sommer und Winter? (d) Wellen im Sturm hoch genug, im Ruhigen zu viel?

### Bilder

Alle selbst angesehen (T400, Vulkan, 1600 × 900, freie Kamera). Vorher (ad72bb82): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3c/vorher/<name>/a.png`, nachher: `…/wasser3c/nachher/<name>/a.png` (nicht im Repository).

| Bild | Zu sehen |
|---|---|
| `ufer_1.0_regen`, `nah_1.0_regen` | Randeis bei Regen ohne Schnee (wie Bild 67/68): vorher gerade Kante am Ufer und dicke Risse; nachher gebrochene Kante, Grieseis, am Ufer unregelmäßiger Übergang auf den Grund, feine Risse |
| `schollen_1.0_regen`, `schollen_schnee_1.0` | Schollen bodennah (wie Bild 69): vorher flache Flecken; nachher Körper mit Dicke, nassem Rand, durchscheinender Stirn, mit Schnee weiß |
| `muendungoben_1.0_regen` | Mündung von oben (wie Bild 70): vorher Schaumfleck und Bach über dem Flusseis; nachher Loch im Bach, Rinne im Randeis, kein Schaum |
| `muendung_0_regen`, `muendung_spiel_0` | Mündung im Sommer (wie Bild 71) |
| `sturm_0` | Rhein im Sturm: höhere Wellen |
| `ueberblick_1.0`, `bodennah_0.6` | Übersicht mit Schnee; Mündung bodennah bei 0,6 |
| `niedrig_1.0`, `mittel_1.0` | Stufen Niedrig und Mittel |

### Was offen bleibt

- **Schollen** sind ein starres Feld: alle gleich schnell (sonst liefen sie durcheinander), sie stoßen nicht aneinander, und das Randeis bricht keine Stücke ab. Sie ragen wirklichkeitsnah nur wenige Zentimeter aus dem Wasser – aus der Spielhöhe bleiben sie helle Flecken mit leichtem Rand, die Dicke sieht man erst aus der Nähe. Ihre Glaswirkung kommt ohne echte Lichtbrechung (siehe Abweichung oben); den Teil unter Wasser zeigt nur Hoch und Ultra. Weichen sie einem Pfeiler aus, können sie sich mit einem Nachbarn überschneiden.
- **Mündung:** Der trübe Streifen ist von schräg oben bei Licht zu sehen, flach über das Wasser und bei Regen kaum (dort überwiegt die Spiegelung); im Winter ist er bewusst fast aus. Der Uferschaum setzt stromab nach gut 30 m wieder ein. Der Bach liegt an der Mündung höher als der Rhein (Kern, Duffesbach 0,55 m): Seine Fläche fällt über die letzten Meter hinab, ohne Schaum oder Kaskade; ein eingeschnittenes Bett bis zum Fluss wäre Sache des Kerns.
- **Ufer:** Wo das Eis vor dem Ufer aufhört, zeigt der Grund im Flachen die Wiese des Bodens (entsättigt und bräunlich getönt, aber keine eigene Kies- oder Schlammfläche, offen seit Schritt 5c).
- **Bach-Eis:** Aus der Höhe sieht man die Plattengrenzen der Decke noch als feines Mosaik.
- **Wellenhöhe** ist aus den Parametern gerechnet (Steilheit, Länge, Netz), nicht im Bild gemessen. Aus der Spielhöhe sind 6–10 cm kaum zu sehen; im Sturm deutlich.
- **Klicktest:** Der Rechner war stark belastet (Last bis 14, eine fremde Godot-Sitzung auf der Grafikkarte); mehrere Läufe brauchten über 85 s zum Laden und wurden mit längerer Wartezeit wiederholt.

## Phase 3d: ein Eismaterial, feste Schollen im Randeis, Stöße, starres Eis, träger Seegang (30.10.2026)

Nach dem Test von 3c (Nutzer: „etwas besser, aber die Schwächen müssen weg“): Das Randeis sah nicht aus wie die Schollen, der zugefrorene Bach und die Mündung gefielen nicht (von oben ein Mosaik aus Platten), die Schollen stießen nicht an Pfeiler und aneinander. Dazu: Randeis und Bach-Eis bewegten sich mit den Wellen „wie ein Schwamm“, und beim Wetterwechsel sprang die Struktur der Wellen um.

### Recherche (30.10.2026)

**Fotos von Flusseis** (Wikimedia Commons, nur angesehen, nichts übernommen; Lizenz der Fotos zur Nachprüfung):

| Foto | Lizenz | Was es zeigt |
|---|---|---|
| Dömitz, Elbe, Eisgang (5.2.2012) | CC BY-SA 3.0 | dichtes Treibeis aus Pfannkuchen und Brocken, weiß, buckelig, mit aufgeworfenen Rändern; dunkle Rinnen dazwischen |
| Eisgang auf der Elbe (30.1.2010) | CC0 | Schollen 20–40 cm dick, schräg aufeinandergeschoben; am Ufer ein Wall aus Brocken mit Schnee; Bruchflächen grau, durchscheinend, mit Schmutz |
| Treibeis an der Behelfsbrücke Mainz–Rhein (3.1.1947, Landesarchiv BW) | CC BY 4.0 | runde Schollen mit hellem Wulst auf schwarzem Wasser, an den Pfeilern gesammelt; am Ufer ein Band aus Brocken |
| Eisablagerungen an der Mosel (2.2.1954, Landesarchiv BW), von oben | CC BY 4.0 | Randeis als **durchgehendes, helles Band** an beiden Ufern, in der Mitte ein Mosaik dicht treibender Schollen, dazwischen eine dunkle offene Rinne |
| Eisgang auf der Elbe (5.3.2018) | CC BY-SA 4.0 | feines Treibeis in Streifen, grau-weiß; Fahrrinne eines Schiffes |
| Hudson River with ice floes | CC BY 4.0 | Schollen in Streifen entlang der Strömung, am Ufer dichter |
| Ice floes in River Dee (geograph 1640266) | CC BY-SA 2.0 | Randeis von oben: weiß, Kante glatt gerundet, davor ein Streifen dünnes, graues Eis; Grieseis als weiße Flocken auf dunklem Wasser |
| Small Ice Floes, Afon Tywi (geograph 1661607) | CC BY-SA 2.0 | graue, fast untergetauchte Breischollen; dünnes Randeis kaum sichtbar, nur seine Kante |
| Ice formations at Zdobnice–Říčka confluence | CC BY-SA 4.0 | Mündung zweier Bäche: dickes Ufereis mit gerundeter, überhängender Kante (10–40 cm), darunter Eiszapfen, schwarzes offenes Wasser |
| Regensburg, Eisgang der Donau vor der Steinernen Brücke (1893) | gemeinfrei | Eisstoß stromauf einer Steinbrücke: 30–50 cm dicke Blöcke, schräg gestellt und übereinandergeschoben, verschneit |
| Pancake ice on the Allagash River, Maine | gemeinfrei | Pfannkucheneis am Ufer: runde Scheiben mit hellem, aufgeworfenem Rand, dicht an dicht, davor schwarzes Wasser mit Flocken |
| A strip of ice cover near the shore of the pond | CC BY-SA 4.0 | junges Ufereis aus der Nähe: dunkel, mit Nadeln und gefrorenen Breiklumpen |
| Bronte Creek in Ice | CC BY 2.0 | zugefrorene Fläche mit Schneefahnen und grauen, nassen Streifen – kein Plattenmuster |

**Was Flusseis echt macht** (für unsere Bilder): (1) Flusseis ist fast immer **weiß bis grau und matt** – gefrorener Eisbrei; klares, schwarzes Eis nur, wo das Wasser still war, mit weißen Rissen und Blasen. (2) **Schnee** liegt auf fast allem alten Eis, in Fahnen; nasse Stellen grau. (3) **Kanten** sind dick (10–40 cm), gerundet, oben hell, darunter blaugrau durchscheinend, direkt davor dunkles Wasser im Schatten. (4) **Treibeis** sind Pfannkuchen mit aufgeworfenem Rand und buckelige Brocken, viele kleine, wenige große, in Streifen entlang der Strömung, am Ufer dichter. (5) **Grieseis** sind weiße Flocken und grauer Brei auf dem dunklen Wasser. (6) Das **Wasser** im Winter ist sehr dunkel und spiegelt – großer Kontrast zum Eis. (7) An **Brückenpfeilern** sammeln sich Schollen stromauf (Eisstoß), schieben sich übereinander und gleiten dann vorbei; stromab ist es ruhiger. Am Ufer frieren Pfannkuchen zu einem dichten Feld zusammen – das Vorbild für die festen Schollen im Randeis. (8) **Von oben** ist Randeis ein durchgehendes helles Band, kein Mosaik; die Kante ist großflächig glatt, im Kleinen gebrochen. (9) **Zugefrorene Bäche:** zusammenhängende Decke, meist verschneit, an offenen Stellen dicke, gerundete Kanten. (10) **Mündung:** offenes Wasser, wo der Bach einströmt, rundum Ufereis, Brei treibt hinaus.

**Freie Texturen und Shader** (nur Lizenzen, die ein verkauftes Spiel erlauben):

| Quelle | Lizenz | Ergebnis |
|---|---|---|
| ambientCG „Snow 014“ (gefrorener, buckeliger Schnee/Eis) | CC0 | **übernommen:** die Normalkarte (1K, OpenGL) als Relief des Eises, `game/assets/texturen/eis/eis_relief_normal.jpg` (Protokoll, `HERKUNFT.md`); die Farbkarte nicht (grüne Blätter darin) |
| ambientCG Ice001–Ice004 (gerissenes Seeeis, grün) | CC0 | nicht übernommen: stilisierte Zellen – brächten das Plattenmosaik zurück, das weg sollte |
| ambientCG Snow005, Snow006; Poly Haven snow_01 bis snow_05, snow_field_aerial | CC0 | nicht nötig: Schnee rechnet der Shader, das Relief kommt aus Snow 014 |
| godotshaders.com „Spatial Ice Shader“ (Bildschirmtextur-Brechung, Fresnel) | CC0 | nicht übernommen: Brechung durch die Schollen bleibt aus (Begründung Phase 3c), Fresnel ist schon da |
| godotshaders.com „Ice Covering“ (2D-Überlagerung), „Frostbite“ (Nachbearbeitung) | MIT bzw. CC0 | passen nicht (Bildschirmeffekte) |
| GitHub/Asset Library: WaterBox, FloatableBody (Auftrieb für RigidBody) | MIT | nicht übernommen: keine Physik-Engine für Hunderte Schollen (Empfehlung Teamleiter) – eigene 2D-Simulation |
| Dragonforge „Godot 3D Ice Material Shaders“ (itch.io) | CC0, kostenpflichtig | nicht gekauft |

### Umsetzung

| Wunsch | Umsetzung | Dateien |
|---|---|---|
| **Ein Eismaterial** für Randeis, Bach, Teich, Hafenbecken und Schollen | `eis_material.gdshaderinc`: eine Funktion `eis_oberseite(Ort, Zusatz klar, Schnee, darunter, Bildpunktgröße)` für jede Eisoberseite – weiß bis grau und matt (gefrorener Brei, großflächig 3 und 15 m wechselnd, keine Platten), Schwebstoff-Schlieren, wenig klares Eis (je Art `eis_klar`: Fluss 0,12, Bach 0,32, See 0,45, Teich und Hafenbecken 0,5; zur Kante hin mehr), Blasen und Reif nur nah, feine Risse vor allem im klaren Eis, **Relief** aus der Normalkarte (zwei gedrehte Lagen), Schnee in Fahnen, Himmelsspiegelung und Leuchten aus dem Eis (`eis_himmel`). Dieselben Farben (`eis_hell`, `eis_dunkel`, `eis_stirn`, `eis_schlamm`) für Wasser-Shader und Schollen. Schnee auf den Schollen nur, wo Schnee liegt (vorher hatten sie ohne Schneedecke eigenen Schnee und sahen anders aus als das Eis daneben) | `eis_material.gdshaderinc`, `wasser_realistisch.gdshaderinc`, `eis_scholle.gdshaderinc`, `Wasserarten.EisKlar`, `EisStoffSetzen` |
| **Randeis mit Dicke** wie die Schollen | **Feste Schollen im Randeis:** dieselben Körper wie die treibenden, im Band vom Ufer bis zur weitesten Eiskante festgefroren (Rhein 2.900, Halbmesser 0,45–1,75 m, dicker, mit mehr Schnee; kleine Überlappung, keine Durchdringung). So sieht Randeis großer Flüsse auf den Fotos aus: angefrorenes Treibeis und Brei. Jede hat eine Schwelle: Sie wächst auf, sobald die Kante des Shaders (Reichweite · Eis + dieselben Buchten, das Rauschen `w_fbm` ist in C# nachgebaut) knapp über ihre Mitte reicht – sie wächst mit dem Winter mit, die CPU schreibt sie nur einmal (Uniform `eis_fest`). Keine festen Schollen in der Fährrinne, an Furten, in der Rinne stromab einer Mündung, am Kai und an Pfeilern. Gezeichnet in Stücken zu 32 m (nah 16 Ecken, ab 60 m 10), in Niedrig keine. Zwischen ihnen liegt das flache Eis des Wasser-Shaders aus demselben Material; seine Bruchkante hat jetzt wie die Schollen eine blaugrün glänzende Lippe und einen nassen, dunklen Rand | `Schollenfeld.FesteBauen`, `Stadtansicht.Eisschollen.FesteSchollenFrei`, `eis_scholle.gdshaderinc` |
| **Kein Plattenmosaik** von oben | Eine Decke (Bach, Teich, Hafenbecken) hat keine Fugen mehr; die Platten bestimmen nur noch, wie die Kante bricht (an Löchern, Furten, beim Zufrieren). Klar/weiß wechselt stetig über die Fläche statt je Platte. Im Bach lag mitten im Eis dunkler Grund, wo das Wasser flach ist: Das Auslaufen auf den Grund gilt jetzt nur in Ufernähe | `wasser_realistisch.gdshaderinc` |
| **Festes Eis starr** („wie ein Schwamm“) | Unter festem Eis keine Wellen mehr, auch nicht unter den Platten, die über die glatte Kante hinausragen; die Wellen setzen erst vor der Kante über 2,5 m ein (dieselbe Kante wie im Bildpunkt-Teil, mit Buchten und Löchern). Vorher klangen die Wellen unter dem Eis nur in Ufernähe ab (am Rhein schaukelten die äußeren 3 m des Randeises voll mit, in der Bachmitte noch über 80 %), und das Muster des Eises lag fest in der Welt, während die Ecken mit der Welle hin- und herwanderten – das sah aus wie ein Schwamm. Unter dem Eis bleiben auch keine Kräusel mehr (dort liegt das Relief des Eises). Treibende Schollen bleiben starre Körper, die als Ganzes schaukeln; feste Schollen bewegen sich gar nicht | `wasser_realistisch.gdshaderinc` (`vertex`), `eis_scholle.gdshaderinc` |
| **Mündung** | Folgt aus den Punkten oben: Das Bach-Eis ist eine glatte, verschneite Decke bis zum Loch an der Mündung, dessen Rand wie jede Kante bricht; im Rhein schließt die offene Rinne stromab an, rechts und links Randeis mit festen Schollen (keine in der Rinne) | wie oben |
| **Stöße** an Pfeilern, Prahm, Kähnen und untereinander | Eigene 2D-Simulation in `Schollenfeld` (keine Physik-Engine): Jede Scholle ist ein Kreis (78 % ihres Halbmessers: der Umriss ist ein Vieleck, meist innerhalb des Halbmessers) in den Laufachsen. Die Strömung zieht sie (Profil quer: Mitte 1,12 ×, am Ufer langsamer; Zeitkonstante 1,2 s + 0,7 s je m Halbmesser), dazu ein leichtes Pendeln quer. **Untereinander:** Nachbarn über ein Gitter zu 2 m (Felder, einmal angelegt, ohne Speicheranforderung je Bild), Überlappung zu 80 % nach Masse auseinander, unelastischer Stoß (Eis federt kaum), Reibung längs der Berührung dreht beide. **Pfeiler** (Kapsel entlang der längeren Seite), Prahm und Kähne: herausschieben, Tempo in die Wand umkehren (15 %), längs der Wand leichte Reibung und Drehung. Trifft eine Scholle einen Pfeiler von vorn, hängt sie 1–4 s fest; wer auf sie auffährt, bremst mit (bis 1 s, nur die erste Reihe; die übrigen stauen sich nur durch die Stöße) – **ein kurzer Stau**, danach gleiten sie seitlich am Pfeilerkopf vorbei. Ist eine zwischen Pfeiler und Nachbarn eingekeilt und drückt länger als 4 s, schiebt sie sich 2,5 s über bzw. unter die Nachbarn (ohne Stöße mit ihnen) und gleitet seitlich weg – so löst sich der Stau, statt dass sich eine Eisbrücke bildet. Am Randeis prallen sie ab. In Teilschritten zu höchstens 1/30 s (höchstens zwei je Bild); Stöße nur im Fenster ±160 m um die Kamera, weiter weg treiben sie nur (gleich schnell, sie laufen nicht ineinander). Abbrechende Stücke vom Randeis: nicht gebaut (siehe offen) | `Schollenfeld.Schritt`, `HindernisPruefen`, `Abprallen` |
| **Wetterwechsel ohne Umspringen** | Ursache: Der Wind lenkte die **Richtung** der Wellenzüge; die Phase k · (d · Ort − c · t) rechnet mit Orten bis über 1 km, schon eine kleine Drehung verschob das ganze Muster auf einen Schlag. Jetzt sind die Richtungen fest, der Wind ändert nur die **Höhe** der Züge (mit dem Wind laufende bis +90 % × Windanteil). Und der Seegang ist **träge:** die neue globale Uniform `wellen_seegang` folgt dem Wind des Wetters mit Zeitkonstante 40 s beim Aufbau und 80 s beim Abklingen (bei höherem Spieltempo geteilt durch dessen Wurzel); Wellenhöhe und Kräusel lesen sie, Regenringe, Schaum und Farbe folgen weiter schnell `wetter_wind`. Testszene: sofort | `wasser_wellen.gdshaderinc`, `Stadtansicht.Wetter.cs` (`SeegangNachfuehren`), `project.godot` (`wellen_seegang`), `Wassertest.cs` |

### Kosten (T400, gegen 80db99b2)

Gemessen wie in 3c (`--wasservergleich=1` im selben Prozess, Testszene `--wassermessung=winter`), beide Stände nacheinander am selben Nachmittag. Der Rechner war wieder belastet (Last 5–19, bis zu zwei fremde Godot-Sitzungen auf derselben Grafikkarte): Die drei Runden der Köln-Messung lagen bis 40 ms auseinander, diese Werte sind **nicht verwertbar** (Wasser in Stufe Hoch vorher +18,6, nachher +6,9 ms, jeweils die beste Runde – das ist Rauschen, kein Gewinn). Verwertbar sind die Testszene und die Rechenzeit der Schollen auf dem Prozessor:

| Messung | vorher (80db99b2) | nachher (Phase 3d) |
|---|---|---|
| Testszene, Fluss über das ganze Bild, Eis gegen ohne Eis (Bildzeit, beste Runde / Median) | +1,5 / +4,8 ms | +5,1 / +3,7 ms |
| Schollen-Simulation auf dem Prozessor, Köln, Rhein mit Fähre (4.722 treibende, 3.909 feste; `--schollenmessung=1`, Mittel über 10 s) | – (nur Verschieben, unter 0,5 ms) | 1,4–2,2 ms je Bild, davon Stöße 0,4–1,1 ms (2 Teilschritte, 3.500–11.600 Paarprüfungen) |
| dasselbe nah an den Brückenpfeilern, Stau (`pfeiler_1.0`) | – | 2,0–3,0 ms je Bild (bis 1.000 Berührungen) |

Das Eis kostet auf der Grafikkarte etwa so viel wie vorher (im Rauschen, ±2 ms): Das gemeinsame Material liest zwei Bildzugriffe mehr (Relief), dafür fallen in einer Decke die Fugen weg, und die festen Schollen sind fertige Netze (nah 16 Ecken je Ring, ab 60 m 10), die die CPU nur einmal schreibt. Neu ist die **Simulation**: etwa 2 ms je Bild auf diesem geteilten Rechner (optimiert gebaut wie ein exportiertes Spiel; der Debug-Bau, wie ihn der Godot-Editor startet, brauchte mit ±220 m 5–11 ms); auf einem Spiele-PC weniger. Sie läuft nur im Winter, wenn Schollen da sind, ohne Speicheranforderung je Bild; die Stöße nur ±160 m um die Kamera (die erste Fassung mit bis zu vier Teilschritten und ohne Fenster brauchte über 40 ms).

### Bilder

Alle selbst angesehen (T400, Vulkan, 1600 × 900, freie Kamera, Stufe Hoch wenn nicht anders genannt). Vorher (80db99b2): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3d/vorher/<name>/<bild>.png`, nachher: `…/wasser3d/nachher/<name>/<bild>.png`; zu den übrigen Ansichten liegen die Vorher-Bilder von 3c unter `…/wasser3c/nachher/` (nicht im Repository). Bei Bildfolgen nennt `zeiten.txt` die Sekunden seit dem Start.

| Bild | Zu sehen |
|---|---|
| `nah_1.0_regen`, `nah_1.0_schnee` | Randeis nah: festgefrorene Schollen im selben Material wie die treibenden, dazwischen flaches Eis; ohne Schnee grau-weiß und nass, mit Schnee weiß (vorher: flaches Plattenmosaik, anders als die Schollen) |
| `ufer_0.6` | Randeis bei 0,6: ein schmales Band, die festen Schollen wachsen mit der Eiskante |
| `pfeiler_1.0` (a–h, je 1–12 s Abstand), `pfeiler_spiel_1.0` (a–d) | Schollen an den Pfeilern der Steinbrücke, nah und aus Spielhöhe: Stoß, kurzes Hängen am Pfeilerkopf, Stau dahinter, Vorbeigleiten; stromauf der Brücke dicht gepackt (wie ein Eisstoß), stromab lockerer. Vorher (a–h): Die Schollen wichen weit vorher seitlich aus und überschnitten sich |
| `bachoben_1.0`, `bachoben_1.0_regen` | Duffesbach komplett vereist von oben: eine Decke ohne Plattenmosaik (vorher: Mosaik mit Fugen, mit Schnee dunkle offene Platten) |
| `muendungoben_1.0_regen`, `muendung_spiel_0.6`, `muendung_spiel_1.0` | Mündung: Bach-Decke bis zum Loch, Rinne im Rhein stromab, Randeis aus festen Schollen links und rechts |
| `schollen_1.0_regen`, `ueberblick_1.0` | Treibeis bodennah bei Regen und Übersicht mit Brücke |
| `niedrig_1.0`, `mittel_1.0` | Stufen: Niedrig ohne feste Schollen (nur das flache Eis), Mittel mit |
| `wetter` (a–j) | Wetterwechsel klar → Sturm (bei 92,8 s): Licht, Wolken und Regen springen, die Wellen wachsen über die nächsten zwei Minuten (Protokoll `[wetter] … Seegang` 0,25 → 0,55 nach 22 s → 0,80 nach 62 s → 0,91 nach 117 s). Vorher (a–j) wechselten Höhe und Muster im selben Augenblick |

### Testanleitung (Windows)

Wie oben bauen (`dotnet build Reichsgilde.sln`), dann aus der Eingabeaufforderung (`godot` = Pfad zu `Godot_v4.7.2-stable_mono_win64.exe`), immer mit `--karte=koeln --startstunde=11 --freiekamera=1`.

**Wo der Brückenpfeiler-Stoß zu sehen ist:** Die Steinbrücke quert den Rhein bei z ≈ 8; ihre sechs Pfeiler stehen bei x = 66, 76, 85, 95, 104 und 114. Der Rhein fließt nach Norden (z wird kleiner): Die Schollen kommen von Süden (größeres z) und stoßen an die Südseite der Pfeiler.

1. **Pfeiler, nah:** `… --jahreszeit=winter --winter=1.0 --wetter=klar --freiekamera-start=89,7,27:87,0,11` (über dem Wasser südlich der Brücke, Blick stromab auf die Pfeiler bei x = 85 und 95). Bei Tempo 1× ein, zwei Minuten zusehen. Erwartet: Schollen stoßen an den Pfeilerkopf, bleiben 1–4 s hängen, die folgenden fahren auf und stauen sich, dann gleitet die vordere seitlich vorbei und dreht sich dabei; untereinander schieben sie sich weg, keine läuft durch eine andere; in der Mitte treiben sie schneller als am Ufer. **Aus Spielhöhe:** `--freiekamera-start=96,40,62:90,0,12`.
2. **Randeis nah:** `… --winter=1.0 --wetter=regen --freiekamera-start=57,2,92:61.5,-0.5,88` (ohne Schneedecke) und dasselbe mit `--jahreszeit=winter --wetter=klar` (mit Schnee). Erwartet: Das Randeis besteht aus festgefrorenen Schollen derselben Art wie die treibenden (Dicke, gebrochener Umriss, blaugrüne Stirn), dazwischen flaches Eis im selben Material; ohne Schnee grau-weiß, mit Schnee weiß; die Kante ist gebrochen und hat Dicke.
3. **Starres Eis:** Schritt 2 mit `--wetter=sturm`: Randeis und feste Schollen bewegen sich nicht, nur das offene Wasser davor und die treibenden Schollen (als Ganzes). Ebenso der zugefrorene Duffesbach (Schritt 4).
4. **Bach komplett vereist, von oben:** `… --jahreszeit=winter --winter=1.0 --wetter=klar --freiekamera-start=-130,40,150:-125,0,128`, ohne Schnee `--winter=1.0 --wetter=regen`. Erwartet: eine zusammenhängende Decke (weiß bzw. grau-weiß), kein Mosaik aus Platten, keine dunklen Flecken mitten im Bach.
5. **Mündung:** von oben `… --winter=1.0 --wetter=regen --freiekamera-start=58,16,130:61,0,123`, aus Spielhöhe `… --jahreszeit=winter --winter=0.6` bzw. `1.0 --wetter=klar --freiekamera-start=40,32,150:66,0,112`. Erwartet: Bach-Decke bis zum offenen Loch an der Mündung, dessen Rand wie jede Eiskante bricht; im Rhein schließt die offene Rinne stromab an, links und rechts Randeis mit festen Schollen.
6. **Wetterwechsel:** `… --winter=0 --wetter=klar --wetterspaeter=sturm@30 --freiekamera-start=60,6,40:85,0,20`. Nach 30 s schlägt das Wetter zum Sturm um: Wolken, Licht und Schaum sofort, die Wellen bauen sich über etwa eine Minute auf, ohne dass das Muster springt (vorher sprang es im selben Augenblick um). Das Protokoll schreibt alle 5 s `[wetter] … Seegang …`. Oder im Entwicklerfenster (F12) das Wetter umschalten, auch zurück (Abklingen etwa doppelt so lang).
7. **Grafikstufen:** `--wasserstufe=niedrig` (keine festen Schollen, nur große treibende) und `mittel` mit Schritt 2.
8. **Zurückmelden:** (a) Sieht das Randeis jetzt aus wie die Schollen? (b) Bach von oben und Mündung? (c) Stoß und Stau an den Pfeilern: zu lang, zu kurz, zu selten? (d) Fühlt sich der Seegang beim Wetterwechsel richtig an (Aufbau, Abklingen)? (e) Ruckeln bei vielen Schollen?

### Was offen bleibt

- **Kein Abbrechen vom Randeis:** Stücke, die vom Randeis abbrechen und treiben, sind nicht gebaut (das Randeis ist fest, die Schwellen wachsen nur). Es wäre ein Übergang einer festen Scholle in die Simulation – machbar, aber mit eigenem Puffer.
- **Stau nur in der Ebene:** Die Schollen schieben sich an Pfeilern nicht sichtbar übereinander (Aufpressungen wie auf den Fotos von Regensburg); eingekeilte gleiten 2,5 s durch ihre Nachbarn hindurch, das sieht man nah als kurze Überschneidung. *(Seit Schollen 2: kein Durchgleiten mehr; Schollen tauchen an der Staukante und unter Druck am Pfeiler unter, Aufschieben fehlt weiter.)*
- **Simulation nur um die Kamera:** Stöße rechnet sie ±160 m um die Kamera; fährt die Kamera schnell den Fluss entlang, holen die Schollen dort ihre Stöße erst nach, dabei können sich ein, zwei Bilder lang welche überschneiden. Rechenzeit (T400-Rechner, belastet): siehe Kosten. *(Seit Schollen 2: Fenster am Blickpunkt auf dem Wasser, neu hineinkommende Schollen werden nur über die Lage getrennt, draußen treibt kein eingefrorener Stau.)*
- **Feste Schollen nur an Flüssen:** Bach, Teich und Hafenbecken haben eine flache Decke (aus demselben Material, aber ohne Körper an der Kante); an einer Furt und an der Mündung des Baches sieht man die Kante deshalb noch flach.
- **Spiegelung:** Die festen Schollen spiegeln sich nicht im Wasser (keine Bildschirmspiegelung), ebenso die treibenden.
- **Wellenstruktur beim Wetterwechsel** ist an den Werten im Protokoll und an den Bildfolgen geprüft, nicht an einem Video; ein Ruck einzelner Bilder ließe sich nur im laufenden Spiel sehen.

## Phase 3e: Eis nur im Winter, Eisgang am Winterende (30.10.2026)

Der Nutzer und ein anderes Mitglied sahen **im Frühling noch Eisschollen auf dem Rhein**. Entscheidung des Nutzers: Eis und Schollen nur im Winter.

**Ursache** (zwei Wege zum Frühlingseis):
1. **Regel:** `Wasserarten.Winterfortschritt` ließ das Eis erst im Frühling tauen (Frühling 1 → 0 über das erste Drittel des Jahres). Am ersten Frühlingstag stand also das volle Winter-Eis samt Schollen, ein Drittel des Frühlings lang (bei Tempo 1 gut fünf Minuten).
2. **Startoption:** `--winter` setzte das Eis unabhängig von der Jahreszeit. Ohne `--jahreszeit` beginnt jede Partie im Frühling 1400 – die Testbefehle der Phasen 3–3d (`--winter=1.0 --wetter=regen …`) zeigten deshalb Frühling mit Eis und ohne Schnee.

Laden war nicht die Ursache: Das Eis steht nicht im Spielstand, die Ansicht rechnet es beim Aufbau und jedes Bild aus der Uhr (Protokoll „[wasser] Eis beim Aufbau …“).

**Lösung**

| Teil | Umsetzung | Dateien |
|---|---|---|
| Eis nur im Winter | `Winterfortschritt` ist außerhalb des Winters 0; im Winter wächst das Eis über die ersten 55 % des Jahres und steht dann | `Wasserarten.cs` |
| **Eisgang** im letzten Stück des Winters | Neu `Wasserarten.Tauwetter`: 0 → 1 von 72 % bis 96 % des Winterjahres (im Spiel etwa 17:40–22:15 Uhr, bei Tempo 1 knapp vier Minuten, auf dem belasteten Testrechner gut fünf). Uniform `eis_tauen` für Wasser und Schollen (`eis_material.gdshaderinc`). **Decken von Bach, Teich und Hafenbecken** brechen auf: Alle Fugen öffnen sich (bis gut ein Zehntel der Plattengröße, grauer Brei darin), die Platten vergehen eine nach der anderen (zufällig je Platte, bei 0,9 alle). **Randeis** ebenso, die festen Schollen darin vergehen nach und nach. **Treibende Schollen:** erst mehr (von drei Vierteln auf alle), das Randeis gibt die Breite frei, dann schmelzen sie (kleiner, weniger), bei 1 sind keine mehr da. Das Eis wird morsch: grau, nass, ohne Schnee. Die Wellen kommen zurück, das Wasser verliert die Wintertönung. Ab 96 % des Winters ist kein Eis mehr zu sehen; der Frühling beginnt ohne Eis | `Wasserarten.cs`, `wasser_realistisch.gdshaderinc`, `eis_material.gdshaderinc`, `eis_scholle.gdshaderinc`, `Schollenfeld.Bild`, `Stadtansicht.Wasser.cs` |
| **Schnee** | Die Schneedecke (Boden, Dächer) schmilzt im Eisgang mit (ab Tauwetter 0,15, bei 0,9 weg); fällt gerade Schnee, bleibt er bis Tauwetter 0,6 liegen. Im Frühling liegt keiner (das Wetter kennt dort keinen Schnee; nur `--wetter=schnee` legt ihn hin). Vorher verschwand er erst beim Jahreswechsel, in gut einer Sekunde | `Stadtansicht.Wetter.cs` |
| Startoptionen | `--winter` über 0 zeigt ohne `--jahreszeit` den Winter (mit Schnee und Winterlicht); mit einer anderen vorgegebenen Jahreszeit bleibt das Wasser offen. Neu `--tauwetter=0..1` (Eisgang fest, ohne `--winter` mit vollem Eis) | `Stadtansicht.Wasser.cs`, `Stadtansicht.Wetter.cs` (`GezeigteJahreszeit`), `docs/STARTOPTIONEN.md` |
| Protokoll | `[wasser] Eis beim Aufbau: Winter …, Tauwetter … (gezeigt …, Uhr …)` und im Übergang alle 20 s bzw. bei jedem Sprung `[wasser] Eis: Winter …, Tauwetter … (… des Jahres)` | `Stadtansicht.Wasser.cs` |

Die Wintertage des Spiels gehen von 4 bis 23 Uhr; der Eisgang liegt damit am Abend, meist im Dunkeln. Wer ihn bei Licht sehen will: `--tauwetter` oder `--lichtuhr=13`.

### Testanleitung (Windows)

1. **Eisgang bei Licht:** `godot --path game -- --karte=koeln --startstunde=11 --freiekamera=1 --winter=1 --tauwetter=0.3 --freiekamera-start=40,32,150:66,0,112` (Mündung), dann `0.6` und `0.9`; Brücke: `--tauwetter=0.5 --freiekamera-start=96,40,62:90,0,12`, Bach von oben: `--freiekamera-start=-130,40,150:-125,0,128`. Erwartet: bei 0,3 offene Fugen in Bach und Randeis, mehr Schollen; bei 0,6 viele Platten weg, grau und nass, Schnee schmilzt; bei 0,9 nur noch Reste, kaum Schollen.
2. **Über die Jahresgrenze:** eine Partie bis in den Winter spielen (oder `--spulen=4200`: Winter 1403, 17 Uhr; mit `--speichern=winterende.json` einen Stand schreiben und mit `--laden=winterende.json --tempo=1 --lichtuhr=13` laden), Tempo 1, an den Rhein: Ab etwa 17:40 bricht das Eis auf, gegen 22:15 ist es weg, der Frühling 1404 beginnt ohne Eis und Schnee.
3. **Frühling laden:** einen Spielstand aus dem Frühling laden (z. B. die automatische Sicherung nach dem Jahreswechsel): kein Eis, kein Schnee; im Protokoll `Eis beim Aufbau: Winter 0.000, Tauwetter 0.000 (gezeigt Fruehling, Uhr Fruehling)`.

### Bilder und Protokoll

Alle selbst angesehen (T400, Vulkan, 1600 × 900, freie Kamera), unter `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser3d/tau/<name>/<bild>.png` (nicht im Repository).

| Bild | Zu sehen |
|---|---|
| `grenze3` (a–n, je gut 20 s; `zeiten.txt`) | Spielstand Winter 1403, 17 Uhr, Tempo 1, über die Mündung: a (17:14) volles Eis, Schnee; h (20:02) Decken aufgebrochen, Randeis in Stücken, Schollen grau und nass, Schnee fleckig; n (22:38) kein Eis, kein liegender Schnee (es schneit, bleibt aber nicht liegen) |
| `grenze2` (a–d) | Derselbe Stand mit Tempo 4 über die Jahresgrenze: Frühling 1404 beginnt ohne Eis und ohne Schnee (Jahresbericht im Bild) |
| `fruehling_laden` | Spielstand aus dem Frühling 1404 geladen: kein Eis, kein Schnee |
| `tau_0.3`, `tau_0.6`, `tau_0.9` | Eisgang fest (`--tauwetter`) bei Mittag: Fugen offen, dann Platten weg und Schnee fleckig, dann nur noch einzelne Schollen |
| `tau_0.5_pfeiler`, `tau_0.5_bach`, `tau_0.5_nah` | Mitten im Eisgang: dichtes Treibeis vor der Brücke; Bach-Decke in Platten aufgebrochen; Randeis nah |

Protokoll des Laufs `grenze3` (`[wasser] Eis: …`): Tauwetter 0,002 bei 0,725 des Winterjahres, 0,51 bei 0,841, 0,99 bei 0,943; nach dem Jahreswechsel (Lauf `grenze2`) `Winter 0.000, Tauwetter 0.000 (Fruehling, Fruehling 0.000 des Jahres)`. Beim Laden im Frühling: `Eis beim Aufbau: Winter 0.000, Tauwetter 0.000 (gezeigt Fruehling, Uhr Fruehling)`.

**Offen:** Der Eisgang liegt durch die Länge des Spieljahres am Abend, meist im Dunkeln. Treibende Schollen schmelzen gleichmäßig kleiner, sie brechen nicht in Stücke *(seit Schollen 2: Schmelzen je Scholle, Bruch mit Chance)*; auf Bach und Teich treiben die aufgebrochenen Platten nicht ab.

## Schollen 2: ruhiger Stau, Bruch, Schmelzen (30.10.2026)

Nach dem Test des Nutzers (Bild der Brücke nachts mit Stau): Staut sich das Eis an oder vor der Brücke, verhalten sich die Schollen komisch – große Brocken drehen sich auf der Stelle, bei Zusammenstößen hüpfen sie voneinander weg. Wünsche: realistischer; beim Aufprall sollen Schollen mit einer gewissen Wahrscheinlichkeit zerbrechen (nicht immer); sie sollen leicht schmelzen. Branch `phase5/schollen-2` von `a0575fba`; Plan des Teamleiters mit zwei Kritiken, umgesetzt in einem Branch statt drei PRs.

### Ursachen (gelesen und gemessen auf main `a0575fba`)

| Erscheinung | Ursache im alten `Schollenfeld.cs` | Gemessen |
|---|---|---|
| Drehen auf der Stelle | Die Reibung bei jeder Berührung addierte in jedem Teilschritt Drehung (`_om += tang · 0,25 / r · dt · 4`) – ohne Trägheit, ohne Grenze durch den Andruck, ohne den Drehanteil am Berührpunkt; zurück zur Grunddrehung nur mit 5 s. Im Stau gleiten Nachbarn ständig aneinander vorbei, und eine am Pfeiler festgehaltene Scholle steht, während die anderen mit gut 1 m/s an ihr vorbeiziehen | im Spiel (Brücke, große Schollen): P50 17–24 °/s, P95 67–91 °/s; „Dreher“ (fast still, aber über 15 °/s) 10–13 % (Bildfolgen A–E, Tabelle unten) |
| Hüpfen | Überlappungen wurden je Teilschritt zu 80 % (Pfeiler 100 %) herausgeschoben, ohne das Tempo zu ändern; nach 4 s Druck am Pfeiler glitt eine Scholle 2,5 s durch ihre Nachbarn (mit 0,6 m/s Seitenschub) und sprang danach heraus; Seitenschübe am Pfeilerkopf; Rückprall 1,15; der Stoßkreis um den Netz-Ursprung ragte mit 28 % seiner Fläche über den Umriss | Sprünge des Scheintempos über 0,5 m/s 0,9–8,1 je 1000 Schollenbilder, über 1,5 m/s bis 2,5, größter 13,9 m/s (Eisgang); Stoßkreise durchdrangen sich bis P99 1,1–1,6 des kleineren Halbmessers |
| Stau springt nach Kamerafahrt | Außerhalb von ±160 m um die Kamera blieb ein Stau mit Tempo 0 stehen; bei der Rückkehr lösten sich die Überlappungen in einem Bild | im Nachbau bis 2,7 m in einem Bild |

Nicht die Ursache: Spieltempo (die Schollen treiben in Echtzeit), Bildrate, der Wechsel nah/fern. Recherche zu echtem Treibeis (Notizordner): Es ist träge und federt nicht, dreht sich mit 0,5–2 °/s, im dichten Eis kaum; ein Stau entsteht als Bogen zwischen Pfeilerköpfen und baut Druck durch Untertauchen, Aufschieben, Brechen und Schübe ab, nie durch Wegspringen; große Schollen spalten am Pfeiler meist in zwei Stücke; beim Schmelzen werden sie kleiner, dünner, runder, grau und nass.

### Umsetzung

| Teil | Umsetzung | Dateien |
|---|---|---|
| Eigene Simulation | Neue Datei `Schollensimulation.cs` ohne Knoten (nur `Mathf`/`Vector2`; der Nachbau im Notizordner bindet sie unverändert ein). `Schollenfeld` legt sie an, setzt das Stoßfenster an den Blickpunkt auf dem Wasser und zeichnet | `Schollensimulation.cs`, `Schollenfeld.cs` |
| Weiche Kontakte | Nach dem „Soft Step“ von Box2D v3 (eigene Umsetzung der Formeln): fester Schritt 1/15 s mit Sammler (höchstens 0,1 s je Bild; gezeichnet wird zwischen zwei Schritten gemittelt), Kontakte alle drei Schritte gesucht, Kontaktfrequenz 10 Hz, Dämpfung 5; Ausschieben höchstens 0,3 m/s (Hindernisse 0,5), danach ein Durchgang ohne Ausschieben (Relax), damit kein Tempo übrig bleibt; **kein Rückprall**; Warmstart über einen Schlüssel je Kontakt; vorausschauende Kontakte | `Schollensimulation.Loesen` |
| Reibung, keine Kreisel | Reibung am Berührpunkt mit der Drehung beider, begrenzt durch den Andruck, mit Haften und Gleiten (Eis–Eis 0,45/0,30, Pfeilerflanke 0,35/0,12, glatter Eisbrecher 0,2/0,06); Rollwiderstand 0,4 (eckige Schollen rollen nicht); ab zwei belasteten Nachbarn klingt die Drehung in 0,5 s ab; Grenze 0,08 rad/s frei, 0,25 rad/s am Hindernis (über 1 m geteilt durch den Halbmesser). Die alten Kunstgriffe – Reibungsdrehung, harte Lagekorrektur, Stoßzahl 1,15, zufälliges Haften und Kette, Durchgleiten, Seitenschübe, Abprallen am Ufer – sind weg | `Schollensimulation.Loesen`, `Zug` |
| Wasser | Zug quadratisch im Tempounterschied (Form- und Reibungswiderstand nach Damsgaard u. a. 2018) mit kleinem linearen Anteil: große Schollen sind träge; im dichten Eis 8 % langsamer, Formwiderstand halb; Drehung zur halben Wirbelstärke des Querprofils (die Uferseiten drehen gegensinnig, höchstens etwa 1 °/s). An der Brücke: in den Bögen 15 % schneller, vor dem Kopf teilt sich die Strömung mit leichten Wirbeln (keine Scholle balanciert auf der Spitze), im Lee Kehrwasser | `Zug`, `BrueckenStroemung` |
| Form | Umriss wie im Shader nach C# übertragen (Saat 0–2047, im Compatibility-Renderer genau): je Saat einmal Fläche, Schwerpunkt, Trägheit, Hauptachse und 1–2 Stoßkreise (länglich: zwei auf der Hauptachse); gerechnet wird um den Schwerpunkt, der Ursprung des Netzes liegt daneben; Winkel in Laufachsen, gezeichnet mit Gier = −Winkel − Fließrichtung | `FormBerechnen`, `FormAusVieleck`, `Ursprung` |
| Pfeiler, Prahm, Kähne | Steinpfeiler als Vieleck mit spitzem Eisbrecher stromauf (Maße aus `tools/modelle/wasser.py`), Joche als Kapsel; Prahm und Kähne mit ihrem Tempo (aus dem Weg im letzten Bild, gekappt auf 2 m/s, Sprünge über 2 m als Stillstand); Eiskante mit der Neigung der Uferlinie | `Stadtansicht.Eisschollen.cs`, `Stadtansicht.Wasser.Uebergaenge.cs` |
| Stau | Ein Bogen zwischen zwei Pfeilerköpfen entsteht von selbst. Er löst sich durch **Bruch** (unten), durch **Untertauchen** an der Staukante (eine Scholle stößt schneller als 1,3 · √(0,08 · g · Dicke) – 0,37/0,52/0,64 m/s bei 10/20/30 cm – an eine stehende Stau-Scholle: mit 30–50 % Chance taucht sie mit der Vorderkante voran ab, treibt unter dem Stau durch und taucht hinter der Brücke wieder auf, wenn über ihr frei ist) und unter hohem Druck am Pfeiler (Stücke und kleine Schollen werden untergedrückt). Rückfall: Steht ein Pfeiler 20–40 s unter Dauerlast, gibt die am stärksten gedrückte Scholle nach (bricht oder taucht) | `TauchenPruefen`, `TauchSchritt`, `NachDenSchritten` |
| Bruch | Mit einer Chance an Pfeiler, Joch, Prahm oder Kahn: Aufprall ab 0,6 m/s (im Eisgang 0,4), nur bei Schollen länger als 18 × Dicke, mittig 40 %, sonst 15 %, mal Größe (smoothstep 0,9–2,5 m) und (1 + 0,6 · Schmelzgrad), gedeckelt bei 60 bzw. 20 %; unter Druck Rate 0,5/s × (Kraft / (4 kPa · Dicke · 2R) − 1). Der Riss läuft vom Berührpunkt zum Ursprung des Netzes und knickt dort um ±20°; 80 % zwei, 20 % drei Stücke (Keil zum Pfeiler). Jedes Stück ist ein **Ausschnitt derselben Scholle**: Der Shader legt die Ecken außerhalb auf den Bruchstrahl, die Stücke liegen genau aneinander, die Bruchfläche ist glasig blaugrün wie die Stirn, daneben ein nasser Streifen ohne Schnee. Die Lage springt nicht; die Stücke behalten Tempo und Drehung und gehen mit 0,07 m/s auseinander. Vorrat 15 % Plätze, 10 s Sperre, höchstens 3 Brüche je Sekunde, keine zweite Generation | `BruchPruefen`, `Brechen`, `eis_scholle.gdshaderinc` |
| Schmelzen | Hochwinter (Frost): keine Größenänderung, nur Abrieb durch Stöße (Schmelzgrad bis 0,35: Ecken runder, weniger Zacken, Rand nasser). Eisgang: jede Scholle folgt einem Ziel aus dem Tauwetter (Halbmesser × 1 → 0,5 von Tauwetter 0,3 bis 1, kleine schneller) mit 4 mm/s plus der Verschiebung ihres Ziels seit dem letzten Nachführen – so hält sie mit dem Tauwetter Schritt, auch bei hohem Spieltempo; ein fester `--tauwetter` stellt einen festen Zustand ein, nichts schmilzt ohne Ende; Schmelzgrad folgt dem Tauwetter (frisch bis 0,45) ebenso mit 1 %/s plus Verschiebung. Ohne Dichte (Ende des Eisgangs) treiben die letzten Schollen aus, bis keine mehr da ist; dann beginnt die Simulation für den nächsten Winter von vorn (`Zuruecksetzen`). Im Shader: weiches statt hartes Minimum über die Bruchkanten (runde Ecken), weniger Zacken, Freibord × (1 − 0,5 · Schmelzgrad), grau und nass, Schnee weg. Unter 0,15 m sinkt eine Scholle in 6 s bündig ab und vergeht; sie kommt erst wieder (oben am Lauf, wachsend), wenn eine Scholle ihrer Größe beim Tauwetter bestehen kann – sonst entstünde ein Kreislauf aus Vergehen und Nachschub; wer das Ende des Laufs erreicht, kommt oben neu herein. Beim Laden stehen Größe und Schmelzgrad gleich im Zustand des Tauwetters | `Schollensimulation.PlaetzeNachfuehren`, `Ende`, `Nachschub`, Shader |
| Fenster | Stöße im Fenster ±160 m um den Blickpunkt auf dem Wasser (aus Spielhöhe liegt der sichtbare Fluss vor der Kamera; höchstens 150 m voraus) und ±60 m um die Kamera, eine Brücke näher als 220 m von 60 m stromauf bis 10 m stromab; neu ins Fenster kommende Schollen werden nur über die Lage getrennt (am Rand sieht niemand hin; nach einem Kamerasprung mit vielen neuen gleich im selben Bild); draußen treiben die Schollen zum Tempo der Strömung (kein eingefrorener Stau), weichen Pfeilern seitlich aus und werden wie im Fenster zwischen zwei Schritten gemittelt gezeichnet | `PlaetzeNachfuehren`, `FrischeTrennen`, `Draussen` |
| Aufschieben (Nachbesserung) | Wo sich zwei Umrisse überdecken (Umriss je Richtung aus der Tabelle, auf der Linie zwischen den Netz-Ursprüngen), liegt die dünnere sichtbar auf der anderen: ihre Kante dort um die Dicke der unteren angehoben, schräg bis 7°, geglättet über 0,25 s; nur gezeichnet. Eine kleine Scholle (unter 0,6 m und unter 0,4 der anderen), die länger als eine halbe Sekunde tief in einer großen steckt, wird unter sie gedrückt (taucht ab, auf, wo über ihr frei ist) | `Aufschieben`, `Schollenfeld` (Kippen um eine beliebige waagrechte Achse) |
| Shader | Kanäle je Kopie ohne instance uniforms: `COLOR` = (Saat, Dicke, Ende des Ausschnitts β + 8 oder 0, −Schmelzgrad), `CUSTOM` = (quer, Stelle, Winkel der Fließrichtung, Anfang des Ausschnitts α + 8 oder 0); feste Schollen unverändert (Schnee und Schwelle in b und a). Ein Stück kippt mit den Wellen um seinen Schwerpunkt. Die Scheibe hat einen Boden (beim Tauchen sichtbar). Der Schnee treibender Schollen kommt aus der Saat | `eis_scholle.gdshaderinc`, `Schollenfeld.ScheibenNetz` |
| Messung, Prüfhilfen | `--schollenmessung=1` schreibt zusätzlich alle 10 s „[wasser] Schollen-Verhalten“ (`Schollenmessung.cs`); `--schollenumriss=1` zeichnet die C#-Umrisse (gelb) und Stoßkreise (rot) über die Schollen; `--schollenbruch=<Faktor>` mit Protokollzeile je Bruch. Speicheranforderung je Bild 0 Byte (der Sichtkegel kommt aus der Projektion statt aus `GetFrustum`) | `Schollenmessung.cs`, `Stadtansicht.Eisschollen.cs` |

### Messung vorher/nachher (Spiel, T400)

Aus den Bildfolgen (unten; je ein Lauf, `--schollenmessung=1`, die sieben Zeilen „Schollen-Verhalten“ 80–150 s nach dem Start), vorher `794eb0e4` (= main `a0575fba` plus Messung), nachher `40ca7135`, optimiert gebaut (`dotnet build game/Reichsgilde.csproj -p:Optimize=true`), T400-Rechner mit Vulkan. Brückenzone: 30 m stromauf bis 6 m stromab der Pfeilerachse; „groß“ ab 1,5 m Halbmesser; „Dreher“: große Scholle fast still (unter 0,25 m/s), aber schneller als 15 °/s gedreht; Sprung: das Tempo aus der gezeichneten Lage ändert sich von Bild zu Bild um mehr als 0,5 bzw. 1,5 m/s (Ruckler über 0,1 s ausgenommen, bei D vorher war deshalb kein Bild gültig); Durchdringung: Tiefe der Stoßkreise in Halbmessern der kleineren Scholle; stehend: Schollen in der Brückenzone unter 0,1 m/s.

| Fall | Stand | Drehen große an der Brücke P50 / P95 / max (°/s) | „Dreher“ | Sprünge des Scheintempos > 0,5 / > 1,5 m/s je 1000 Schollenbilder | Durchdringung P99 | stehend | Durchsatz je min | Brüche / Taucher in 70 s |
|---|---|---|---|---|---|---|---|---|
| A nah, Winter 1,0 | vorher | 17,0 / 69,3 / 267 | 10,2 % | 2,67 / 0,00 (größter 1,31 m/s) | 1,13 | 8,0 | 148 | 0 / 0 |
| A nah, Winter 1,0 | nachher | 0,5 / 2,4 / 14 | 0,0 % | 0,22 / 0,00 (größter 0,91 m/s) | 0,26 | 8,2 | 187 | 5 / 2 |
| B nah, Eisgang 0,5 | vorher | 22,2 / 80,7 / 170 | 9,7 % | 8,14 / 2,46 (größter 13,92 m/s) | 1,56 | 9,0 | 194 | 0 / 0 |
| B nah, Eisgang 0,5 | nachher | 0,9 / 3,0 / 63 | 0,0 % | 0,20 / 0,00 (größter 0,92 m/s) | 0,21 | 27,0 | 220 | 5 / 3 |
| C Spielhöhe, Winter 1,0 | vorher | 18,3 / 66,7 / 130 | 12,9 % | 0,89 / 0,17 (größter 3,56 m/s) | 1,26 | 9,7 | 154 | 0 / 0 |
| C Spielhöhe, Winter 1,0 | nachher | 0,5 / 2,2 / 10 | 0,0 % | 0,24 / 0,00 (größter 0,94 m/s) | 0,37 | 6,4 | 205 | 4 / 5 |
| E Spielhöhe, Eisgang 0,5 | vorher | 23,6 / 91,3 / 284 | 10,1 % | 3,56 / 1,08 (größter 6,68 m/s) | 1,36 | 12,1 | 209 | 0 / 0 |
| E Spielhöhe, Eisgang 0,5 | nachher | 0,7 / 2,9 / 37 | 0,0 % | 0,23 / 0,00 (größter 1,03 m/s) | 0,38 | 33,7 | 206 | 6 / 7 |
| D Spielhöhe, Winter, 21 Uhr | vorher | 19,5 / 68,7 / 133 | 11,5 % | – (kein gültiges Bild) | 1,56 | 9,8 | 149 | 0 / 0 |
| D Spielhöhe, Winter, 21 Uhr | nachher | 0,5 / 2,5 / 21 | 0,0 % | 0,21 / 0,00 (größter 0,89 m/s) | 0,32 | 8,5 | 170 | 4 / 7 |

Die einzelnen Höchstwerte beim Drehen nachher (bis 63 °/s) sind einzelne Bilder (P95 2–3 °/s). Vermutlich stammen sie aus der Messstelle: Gemessen wird das Drehtempo nach dem Relax-Durchgang, die Drehgrenze (0,25 rad/s = 14 °/s für große Schollen am Hindernis) greift erst im nächsten Schritt vor dem Fortschreiben; tauchende Schollen haben keine Drehgrenze, neu wachsende eine höhere (sie hängt am jetzigen Halbmesser). Der Stau im Eisgang ist nachher größer (stehend 27–34 statt 9–12): Die Schollen schieben sich nicht mehr ineinander, der Bogen reicht weiter stromauf; er löst sich durch Brüche und Taucher (in 70 s 5–6 Brüche, 3–7 Taucher), ein Stau hielt 3–33 s.

Der Nachbau im Notizordner (gleiche Verteilung, gleiche Pfeiler, ohne Godot) bestätigt das bei 10, 15 und 60 Bildern/s: Drehen großer Schollen an der Brücke P95 1–4 °/s (vorher 67–73), keine Dreher, Sprünge über 1,5 m/s praktisch keine; im 10-Minuten-Lauf bilden sich immer wieder Bögen und lösen sich (Dauer bis gut 50 s), kein Dauerstau.

### Kosten

Optimiert gebaut (`dotnet build game/Reichsgilde.csproj -p:Optimize=true`), Köln, Ansicht A (nah an der Brücke), Rechenzeit der Simulation je Bild (`[wasser] Schollen-Simulation`, ohne das Schreiben der Puffer) 80–140 s nach dem Start, vorher (`794eb0e4` = main `a0575fba` plus Messung) und nachher (`40ca7135`) abwechselnd, 23:15–00:31 Uhr. Der Rechner war stark belastet: Andere Sitzungen ließen eigene Godot-Läufe auf derselben Grafikkarte (in 11 von 14 Läufen, mit * markiert; Last bis 16); die Bildrate schwankt deshalb zwischen 7,6 und 11 Bildern/s, und die Zeit je Bild hängt an ihr: Die neue Simulation rechnet je Bild so viele Schritte zu 1/15 s, wie Zeit vergangen ist (bei 8 Bildern/s knapp 2), die alte immer zwei Teilschritte und unter 10 Bildern/s nur 0,1 s je Bild (sie lief dann langsamer als die Uhr). Ein Eisgang-Lauf nachher (`nachher_B_2`) lud so langsam, dass sein Messfenster fehlt; dafür kam ein weiteres Paar dazu (`_B_4`).

| Fall | vorher: ms je Bild (Bilder/s) | nachher: ms je Bild (Bilder/s) | Vergleich |
|---|---|---|---|
| Winter 1,0 | 1,82 (10,4) · 1,63 (11,0)* · 1,86 (9,2)* | 2,02 (8,1)* · **1,45 (10,8)** · 2,38 (8,6)* | ohne fremde Last bei gleicher Bildrate (10,4–11): **1,45 gegen 1,63–1,82 ms (−10 bis −20 %)**; Median aller Läufe 2,02 gegen 1,82 ms (+0,2 ms, aber bei 8–9 statt 10–11 Bildern/s); Rechenzeit je Sekunde 16,1 gegen 17,8 ms (−10 %) |
| Eisgang 0,5 | 2,03 (8,0)* · 1,96 (10,9) · 2,67 (9,1)* · 3,25 (7,7)* | 2,69 (7,6)* · 3,88 (8,3)* · 4,70 (7,6)* | **teurer**: Paare bei gleicher Bildrate und Last +32 % (2,69 gegen 2,03 ms) und +45 % (4,70 gegen 3,25 ms; in dieses Fenster fielen auch Läufe des Nachbaus); je Sekunde 32 gegen 23 ms (+40 %). Im Eisgang sind mehr Schollen im Fenster (1.190, Winter 910) und der Stau ist größer; die Suche läuft unter 7,5 Bildern/s in jedem Bild |
| Schreiben der Puffer (nur nachher gemessen) | – | 1,9–2,4 ms Winter, 3,4–3,8 ms Eisgang | vorher nicht getrennt gemessen; gleiche Art Arbeit (Sichtkegel, Lage, 20 Werte je gezeichneter Scholle, zwei `SetBuffer`), neu dazu Ursprung, Kippen und eine zweite Laufpunkt-Abfrage je Scholle |
| Speicher je Bild | 0 B | 0 B (nur mit `--schollenbruch` die Protokollzeile je Bruch) | – |
| Bau beim Laden | nicht gemessen (Verteilung allein 50–95 ms) | 165–253 ms (einmal 369 ms bei Last 16): Formen der 2048 Saaten 50–70 ms, Verteilung wie bisher 50–95 ms, der Rest Übersetzen beim ersten Aufruf | Ziel aus dem Plan (Formen ≤ 50 ms) knapp verfehlt |

Nachbau (gleiche Schollen und Pfeiler ohne Godot, alt und neu abwechselnd, je drei Läufe, 00:13 Uhr bei Last 11–15): Winter 10 Bilder/s 2,0–2,2 gegen 1,5–1,7 ms (+35 %), Eisgang 10 Bilder/s 2,4–2,7 gegen 2,0 ms (+20 bis +37 %), Eisgang 8 Bilder/s 2,7–3,1 gegen 1,75–1,9 ms (+56 bis +65 %), Winter 15 Bilder/s 1,6–1,7 gegen 1,5–2,0 ms (gleich). Der Nachbau der alten Simulation rechnet weniger als das alte Spiel (im Spiel war nachher im Winter billiger, im Nachbau teurer); die Richtung im Eisgang bei wenigen Bildern/s stimmt aber überein. Aufteilung je Bild im Nachbau (Eisgang, 8 Bilder/s): Plätze nachführen 0,75 ms, Suche 1,05 ms, Schritte 1,19 ms, Bruch und Tauchen 0,05 ms, Abschluss 0,26 ms.

**Grenze aus dem Plan (höchstens +10 % und +0,3 ms):** im Winter eingehalten (billiger), **im Eisgang bei 8 Bildern/s auf dem T400 nicht** (+0,7 bis +1,5 ms, +30 bis +45 %). Die Empfehlung (Suche alle drei Schritte, höchstens 0,1 s je Bild, weniger Nachführen) ist in der Nachbesserung unten umgesetzt und neu gemessen.

GPU, Testszene Fluss mit Schollen von oben (`--wassertest=1 --wasserart=fluss --jahreszeit=winter --winter=1 --wassersicht=oben --wassermessung=winter`), je zwei Läufe bei Last 9–18: Eis gegen ohne Eis (Median der Runden) vorher +3,4 / +2,8 ms, nachher +4,3 / +6,5 ms; beste Runden vorher +3,4 / +2,1, nachher +5,0 / −0,4 ms. Bei dieser Last nicht zu unterscheiden; im Spiel war die Bildrate ohne fremde Last gleich (10,4–11,0 gegen 10,8 Bilder/s). Neu im Shader: Ausschnitt und Bodenvertex im Vertex-Teil (je Ecke ein paar Rechenschritte), weiches Minimum beim Schmelzen, der nasse Streifen an der Bruchkante nur für Bruchstücke; die feine Scheibe hat einen Boden (24 Dreiecke mehr).

### Bilder

Alle selbst angesehen (T400, Vulkan, 1600 × 900, freie Kamera, Fotos mit `xwd` während des Laufs, Zeiten in `zeiten.txt`). Vorher (`794eb0e4`): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/schollen2/vorher/<folge>/`, nachher: `…/schollen2/nachher/<folge>/` (nicht im Repository). A–E: nach 90 s Einlauf acht Bilder im Abstand von 0,42 s (`a1`–`a8`), dann sechs im Abstand von 10 s (`b1`–`b6`); nachher mit dem Bau `40ca7135` (der End-Commit `313f0250` ändert nur Kamerasprung, Laden im Tauwetter und geschmolzene Schollen, deshalb G noch einmal). Grundoptionen: `--karte=koeln --startstunde=11 --jahreszeit=winter --winter=1.0 --wetter=klar --freiekamera=1 --ui=0 --schollenmessung=1`.

| Folge | Ansicht (zu den Grundoptionen) | vorher (`794eb0e4`) | nachher |
|---|---|---|---|
| `A_nah_winter` | nah an der Brücke wie das Bild des Nutzers, bei Tag, Winter 1,0 (`--freiekamera-start=89,7,27:87,0,11`) | dichtes Feld großer Schollen vor den Pfeilern, rechts liegen zwei halb ineinander (a1, a8) | die Schollen treiben ruhig auf die Pfeiler zu und legen sich an Kopf und Flanke an, keine Überschneidung (a1–a8); b1–b6: kleine Staus in einzelnen Bögen, die sich lösen |
| `B_nah_eisgang` | wie A, Eisgang (`--tauwetter=0.5`) | dicht gepackter Stau großer, glatter Schollen, viele halb übereinander | kleinere, rundere, graue und nasse Schollen mit Lücken; Stau vor dem linken und mittleren Bogen; b6 (63 s später): an der Brücke frei |
| `C_spiel_winter` | aus Spielhöhe (`--freiekamera-start=96,40,62:90,0,12`) | dichter Stau über die ganze Breite vor der Brücke | lockeres Feld; b4: ein Stau vor den linken Bögen, rechts frei, er löst sich wieder |
| `E_spiel_eisgang` | wie C, Eisgang (`--tauwetter=0.5`) | dichter Stau großer Schollen über die ganze Breite vor der Brücke, alle gleich groß und gleich grau | mehr, graue Schollen in verschiedenen Größen, eine Staukante entlang der Pfeilerköpfe |
| `D_spiel_nacht` | wie C, 21 Uhr (`--lichtuhr=21`) wie das Bild des Nutzers | dicht gepackter Stau vor der Brücke | Schollen mit Lücken, einzelne an den Pfeilern |
| `F_bruch` | nah am Pfeiler bei x = 77 (`--freiekamera-start=77,7,19:76,0,9 --schollenbruch=10`), 30 Bilder im Abstand von 0,52 s | – | a1: eine große Scholle rechts am Pfeiler ist eben in drei Stücke gebrochen (Y-förmiger Riss mit Keil), die Stücke liegen noch genau aneinander (vergrößert: `a1_bruch_nah.png`); a9: die Stücke sind unter dem Bogen durch; a22–a25: zwei Stücke gleiten links und rechts am Eisbrecher vorbei |
| `G_schmelzen` | wie A, spätes Tauwetter (`--tauwetter=0.8`) | – | kleine, flache, runde, graue und nasse Schollen ohne Schnee, tief im Wasser. Mit dem End-Commit `313f0250` (Ordner `G_schmelzen_ende`): Die Schollen stehen beim Laden gleich im Zustand des Tauwetters, die bei 0,8 nicht bestehen können, sinken in den ersten 6 s ab (1.263) und bleiben weg; danach vergeht keine mehr im Kreis (im Nachbau vorher etwa 900 je 10 s). a1: wenige kleine, runde, graue Schollen; b2 (63 s später): an der Brücke frei, stromab treiben flache Reste |

### Nachbesserung nach der Prüfung (31.10.2026)

Unabhängige Prüfer fanden sechs Befunde. Je Befund die Ursache (geprüft), was geändert ist und das Ergebnis; Bilder mit dem Bau `8ee0b74a`, Messung und die Folgen `…_ende` mit `d70ac491` (siehe unten).

| Befund | Ursache (geprüft) | Änderung | Ergebnis |
|---|---|---|---|
| Stromauf der Brücke nach 20–60 s fast leer | **Kein Versiegen des Nachschubs**, sondern eine Lücke der Verteilung (`Verteilen`/`Feld`): Zwischen den Feldern nahm die Verteilung nur 5 % der Schollen an, eine 80–120 m lange, fast leere Strecke lag beim Laden 100–200 m stromauf der Brücke und trieb mit dem Strom heran. Vorher gab es dieselbe Lücke; die alte Simulation rechnete unter 10 Bildern/s nur 0,1 s je Bild (bei 7,8 Bildern/s 78 % der Uhr), die Lücke kam deshalb später an (im langen Lauf vorher: gezeichnet 633 → 486 nach gut 4 Minuten). Nachbau: dieselbe Verteilung mit dem Fenster an der Brücke und ganz woanders – gleich | Lücken mit 20 % statt 5 % Annahme (`FeldLuecke`; große Schollen dort ebenso), 10 % mehr Schollen (5.178 statt 4.722; im Winter 3.907 statt 3.548 aktiv) | Nachbau, Mitte des Flusses, je 40 m vor der Brücke über 200 s: kleinste Zahl 52–62 statt 12–20 Schollen. Spiel, Folge C über gut 6 Minuten: gezeichnet 628–800, nie unter 628 (vorher 633 → 486 → 573), Nachschub 16–48 je 10 s |
| Rechenzeit im Eisgang über der Grenze | Die Simulation rechnete in Echtzeit (bei 8 Bildern/s knapp 2 Schritte je Bild), die Suche lief unter 13 Bildern/s in jedem Bild (Bedingung `_seitSuche + dt > 0,15 s` mit dt = 0,133 s), und das Nachführen las und schrieb je Bild alle Plätze | höchstens 0,1 s je Bild (wie vor Schollen 2; darunter Zeitlupe); Suche alle drei Schritte zwischen den Schritten (Vorlauf 0,2 s = 3 Schritte); Plätze reihum zu einem Viertel je Bild nachführen; Schollen draußen in einem schlanken Durchgang treiben und zeichnen; Aufschieben nur für Schollen mit nahen Kontakten, Paare jedes zweite Bild; im `Schollenfeld` das Stück des Laufs im Sichtkegel einmal je Bild vorab | Messung unten |
| Schollen liegen im Stau flach übereinander | Die Stoßkreise decken gut 86 % des zackigen Umrisses; die Ecken überdecken die Nachbarn. Die hohen P99-Werte (0,9–1,5) kamen fast alle von kleinen Schollen (Halbmesser 0,2–0,4 m), die eine große gegen einen Pfeiler presste und die sich in sie hineinschoben (Nachbau: 218 Bilder mit einer Tiefe über 0,6 in 120 s Eisgang) | **Aufschieben (gezeichnet):** Wo sich zwei Umrisse auf der Linie zwischen ihren Netz-Ursprüngen überdecken (Umriss je Richtung aus der Tabelle der 24 Ecken), liegt die dünnere angehoben und schräg auf der anderen – die Kante zur unteren um deren Dicke gehoben, höchstens 7°, geglättet über 0,25 s; das Netz kippt dazu um eine beliebige waagrechte Achse (`ZKippW`). **Unterdrücken:** Eine kleine Scholle (unter 0,6 m und unter 0,4 der anderen), die länger als 0,5 s tiefer als 0,6 ihres Stoßkreises in einer großen steckt, taucht ab (weit von einer Brücke nach 1 s wieder auf, wo über ihr frei ist). Die Messung schreibt den Höchstwert je Fenster dazu | Bilder: die obere liegt sichtbar schräg auf der unteren (`final/A_nah_winter/b6`, `final/B_nah_eisgang/b3`). Nachbau: Bilder mit einer Tiefe über 0,6 in 120 s 21–26 statt 218. Spiel (A–E, 70–140 s): P99 im Mittel 0,22–0,39, höchster P99 eines Fensters 0,42–0,70 (vorher 0,9–1,5), Höchstwert 0,8–1,8 (Einzelfälle: eine Scholle im Augenblick des Abtauchens oder Auftauchens) |
| Schmelzen in Echtzeit, Tauwetter in Spielzeit | Schmelzen 4 mm/s, Schmelzgrad 1 %/s und Schrumpfen 0,15 m/s waren auf echte Sekunden gedrosselt, das Tauwetter läuft mit der Spieluhr; bei Dichte 0 rief `Schollenfeld` die Simulation nicht mehr auf, der Zustand blieb bis zum nächsten Winter stehen | Jede Größe folgt ihrem Ziel mit der Grundrate **plus der Verschiebung des Ziels** seit dem letzten Nachführen (Tauwetter: Ziel = r0 · f − 0,4 · (1 − f), Dichte: Schwelle); ohne Dichte treiben die letzten Schollen weiter, bis keine mehr aktiv ist, dann `Zuruecksetzen` (Bruchstücke frei, Stammschollen an die Ausgangsstellen, Zustände leer) | Nachbau, Tauwetter 0 → 1 in 216 s (Tempo 1): große Schollen (über 2,8 m) R/R0 0,95 → 0,72 → 0,30 bei Tauwetter 0,925, am Ende 0; in 6,75 s (Tempo 32): R/R0 folgt ebenso (0,61 bei 0,76), die letzten vergehen in 0,4 s nach Dichte 0 statt auf einen Schlag; danach aktiv 0, Neubeginn |
| Kontaktsuche bei unter 13 Bildern/s in jedem Bild | wie oben | Suche nach Schritten statt nach Bildzeit | wie oben |
| Stoßfenster nur um den Blickpunkt, draußen Ruckeln | Der Blickpunkt lag bei flachem Blick bis 400 m entfernt; draußen setzte `Treiben` die alte Lage gleich der neuen, bei 60 Bildern/s sprangen die Schollen nur in jedem vierten Bild | Blickpunkt höchstens 150 m voraus, dazu ±60 m um die Kamera; draußen wird aus dem Tempo zwischen zwei Schritten gemittelt gezeichnet | im Nachbau keine Sprünge; aus der Nähe nicht mehr sichtbar |

Außerdem: Eine oben neu eingesetzte Scholle behielt die Durchdringung ihres letzten Umlaufs, draußen misst sie niemand neu, und ihr Wachsen blieb stehen (`d70ac491`). Die Messung zählt beim Drehen als „groß“ nur noch Schollen, die auch jetzt mindestens 1,5 m groß sind – eine nach ihrer Schwelle nur halb aufgewachsene Scholle (Halbmesser 0,14 m, Ausgangshalbmesser 3,2 m) erschien sonst mit bis zu 140 °/s als „große“.

**Kosten nach der Nachbesserung** (Spiel, T400, optimiert gebaut mit `--no-incremental`, Ansicht A, `[wasser] Schollen-Simulation` 70–140 s nach dem Bau, sieben Fenster je Lauf; vorher = `794eb0e4` = main `a0575fba` plus Messung, nachher = `d70ac491`; abwechselnd, 03:23–03:52 Uhr, Last 3–13, in einem Lauf vorher noch eine fremde Godot-Sitzung). Neu ist „Zeichnen Ø“ (Schreiben der Puffer, in beiden Ständen gleich gemessen) über alle Bilder:

| Fall | vorher: Simulation / Zeichnen (Bilder/s) | nachher: Simulation / Zeichnen (Bilder/s) | Vergleich |
|---|---|---|---|
| Winter 1,0 | 2,03 / 2,65 ms (9,4) · 2,22 / 2,80 ms (9,5) | 1,49 / 1,52 ms (10,9) · 1,84 / 1,77 ms (10,1) | Simulation −17 bis −27 %, Zeichnen −37 bis −43 %, zusammen −28 bis −36 % |
| Eisgang 0,5 | 2,78 / 3,93 ms (8,8) · 1,90 / 2,67 ms (11,1) | 2,52 / 2,40 ms (9,4) · 1,96 / 2,04 ms (10,3) | Simulation je Paar −9 % und +3 % (je Sekunde −3 und −4 %), Zeichnen −24 bis −39 %, zusammen −12 bis −27 % |

Speicheranforderung je Bild 0 B (nur im ersten Fenster nach dem Laden einmal 312 B, wie vorher). Bau beim Laden 223 ms (Formen und Verteilung; 10 % mehr Schollen). **Die Grenze aus dem Plan ist damit eingehalten**, im Eisgang gleich, im Winter billiger, das Zeichnen in beiden deutlich billiger. Nachbau (gleiche Schollen und Pfeiler ohne Godot, gegen `5cba5cd6`, je drei Läufe, Last 3–10): 8 Bilder/s Winter −17 %, Eisgang −23 %; 10 Bilder/s +7 % / −13 %; 15 Bilder/s gleich; 60 Bilder/s +12 % / +30 % (0,45 → 0,5 bzw. 0,6 ms je Bild: das Aufschieben und das Nachführen laufen je Bild, die Schritte nur jedes vierte). Aufteilung je Bild im Nachbau (Eisgang, 8 Bilder/s): Suche 0,3–0,5 ms, Schritte 0,5–0,9 ms, Aufschieben 0,07–0,15 ms, Nachführen und Zeichnen der Lage 0,4–0,6 ms. Die Simulation läuft unter 10 Bildern/s wie vor Schollen 2 langsamer als die Uhr (bei 8 Bildern/s mit 80 %).

**Bilder** (selbst angesehen; T400, Vulkan, 1600 × 900, Bau `8ee0b74a`, A und B zusätzlich mit `d70ac491` in `A_nah_winter_ende` und `B_nah_eisgang_ende`): `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/schollen2/final/<folge>/` (nicht im Repository), Grundoptionen wie oben.

| Folge | Ansicht | zu sehen |
|---|---|---|
| `A_nah_winter` | wie A oben | a1: dichtes Feld vor den Pfeilern; b6: zwei Schollen schräg auf ihren Nachbarn (Mitte, links), stromauf weiter Eis |
| `B_nah_eisgang` | wie B oben | b3: links liegt eine Scholle schräg auf der nächsten; b6 (63 s später): stromauf dicht, Stau vor den Bögen |
| `A_nah_winter_ende`, `B_nah_eisgang_ende` | wie A und B, `d70ac491` | B b4: dicht bis zur Brücke, aufgeschobene Schollen, ein kleiner Brocken taucht unter eine große; Messung (70–140 s): Drehen große P95 2,4 °/s, höchstens 14–15 °/s, Sprünge über 1,5 m/s keine, Durchdringung P99 0,33–0,46 |
| `C_spiel_winter_lang` / `…_vorher` | wie C, vier Bilder, dann zehn im Abstand von 30 s (gut 6 Minuten; die Spieluhr läuft, b5 Abend, b10 Nacht) | nachher: stromauf bis zum Ende Eis; vorher: dieselbe Ansicht mit der Lücke nach gut 4 Minuten |
| `E_spiel_eisgang`, `D_spiel_nacht` | wie E und D oben | E b6: stromauf voll, Staukante an den Pfeilerköpfen |
| `F_bruch` | wie F oben | a28: eine große Scholle eben am Pfeiler in zwei Stücke gebrochen, daneben eine schräg aufgeschobene |
| `G_schmelzen` | wie G oben | a1/b2: wenige, kleine, graue Schollen auf beiden Seiten; stromab sieht man mehr, weil das Bild dort eine viel größere Fläche zeigt (Nachbau bei Tauwetter 0,8: je 100 m 60–200 Schollen, ohne Gefälle stromauf–stromab) |

### Testanleitung (Windows)

Branch `phase5/schollen-2` holen und das Projekt im Godot-Editor öffnen (er baut beim Start selbst). Unter Projekt → Projekteinstellungen → Editor → Ausführen ins Feld **„Main-Run Argumente“** (englisch „Main Run Args“; ist es nicht zu sehen, oben rechts „Erweiterte Einstellungen“ einschalten) jeweils eine Zeile eintragen – **mit ` -- ` davor**, genau wie hier – und mit F5 starten. Die Hilfe der freien Kamera blendet F2 aus, mit gehaltener rechter Maustaste umsehen, W A S D fahren.

1. **Stau an der Brücke, nah (wie das Bild des Nutzers bei Tag):** ` -- --karte=koeln --startstunde=11 --jahreszeit=winter --winter=1.0 --wetter=klar --freiekamera=1 --ui=0 --freiekamera-start=89,7,27:87,0,11`. Zwei, drei Minuten bei Tempo 1 zusehen. Erwartet: Die Schollen legen sich vor den Pfeilerköpfen aneinander, ein Bogen spannt sich zwischen zwei Pfeilern, dahinter staut es sich; die großen drehen sich nicht mehr auf der Stelle und hüpfen nicht voneinander weg; nach einer Weile bricht eine große Scholle am Eisbrecher (die zwei Stücke passen genau aneinander, die Bruchkante ist glasig blaugrün, die Stücke gleiten links und rechts am Pfeiler vorbei) oder eine Scholle taucht an der Staukante mit der Vorderkante voran unter; dann rückt der Stau nach. Wo sich zwei Schollen im Gedränge überdecken, liegt die dünnere sichtbar schräg auf der anderen; kleine Brocken werden unter große gedrückt. Stromauf bleibt auch nach Minuten Eis auf dem Fluss.
2. **Aus Spielhöhe und nachts (wie das Bild des Nutzers):** dieselbe Zeile mit `--freiekamera-start=96,40,62:90,0,12`, nachts zusätzlich `--lichtuhr=21`.
3. **Eisgang (Schmelzen):** Zeile 1 mit `--tauwetter=0.5` (mitten im Eisgang: mehr Schollen, etwas kleiner und schon grauer), mit `--tauwetter=0.8` (deutlich kleiner, runde Ecken, grau und nass, tiefer im Wasser, kleine sinken ab und vergehen; ohne Schnee). Im Hochwinter schmelzen sie nicht, nur Schollen, die oft aneinanderstoßen, werden an den Ecken etwas runder und am Rand nasser.
4. **Bruch öfter ansehen:** Zeile 1 mit `--schollenbruch=10` (zehnfache Chance; je Bruch eine Zeile `[wasser] Bruch: …` im Ausgabefenster).
5. **Prüfhilfe:** Zeile 1 mit `--schollenumriss=1`: Der gelbe Umriss muss auf der Kante jeder Scholle liegen und mit ihr drehen, auch bei Bruchstücken; rot die Stoßkreise.
6. **Messung:** `--schollenmessung=1`: alle 10 s zwei Zeilen „Schollen-Simulation“ (Rechenzeit) und „Schollen-Verhalten“ im Ausgabefenster.
7. **Zurückmelden:** (a) Drehen sich große Schollen im Stau noch auf der Stelle? (b) Hüpfen sie noch voneinander weg? (c) Wie lange steht ein Stau – zu lang, zu kurz? (d) Brechen Schollen zu oft, zu selten? (e) Ist das Schmelzen im Eisgang zu sehen, und ist es „leicht“ genug? (f) Ruckelt es mit vielen Schollen? (g) Wirkt das Aufschieben (schräg aufliegende Scholle) echt, oder zu stark/zu schwach?

### Was offen bleibt

- **Aufschieben nur gezeichnet, Schrägstellen fehlt:** Wo sich zwei Umrisse überdecken, liegt die dünnere angehoben und schräg auf der anderen; gerechnet wird weiter in der Ebene (die Stoßkreise decken gut 86 % des Umrisses). Dicke Schollen stellen sich am Pfeiler nicht schräg (Pressrücken). Grus und Eisbrei in den Fugen des Staus fehlen: Zwischen den Schollen bleibt dunkles Wasser.
- **Schaukeln im Stau:** Verkeilte Schollen schaukeln weiter einzeln mit den Wellen; bei Sturm kippen Nachbarn im Stau gegeneinander.
- **Bruch:** Ein Stück bricht nicht noch einmal (es taucht unter Druck am Pfeiler unter). Der Riss geht durch den Ursprung des Netzes (nur so kann der Shader ohne neue Netze schneiden) und knickt dort; bei sehr länglichen Schollen beginnt er deshalb nicht ganz am Pfeilerkopf. Brüche zwischen zwei Schollen gibt es nicht (dort bricht in der Natur fast nur die Kante).
- **Kosten:** nach der Nachbesserung im Eisgang gleich, im Winter billiger (siehe dort); bei 60 Bildern/s im Nachbau 0,1 ms je Bild teurer als vor der Nachbesserung. Unter 10 Bildern/s läuft der Fluss langsamer als die Uhr (wie vor Schollen 2). Der Bau beim Laden dauert gut 200 ms.
- **Prahm:** Er fährt zu einem wartenden Karren mit 8 m/s und setzt sich bei der Mitfahrt auf dessen Stelle (`FaehrenBewegen`); als Hindernis gilt sein Tempo nur bis 2 m/s, schneller schiebt er sich durch die Schollen.
- **Compatibility-Renderer:** Die Stelle der Scholle (bis gut 1.000 m) kommt dort als 16-Bit-Zahl im Shader an; Wellen und Kippen springen in Stufen von 0,5–1 m (wie vor Schollen 2). Die Saat ist jetzt auf 0–2047 begrenzt und kommt genau an; Bruchkanten in opengl3 nicht eigens angesehen.
- **Eisgang nachts:** Der Eisgang liegt im Spiel am Abend; im Dunkeln sieht man das Grau und die glasigen Bruchkanten kaum.
