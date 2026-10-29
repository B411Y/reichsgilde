# Realistisches Wasser, Phase 2: Gewässerarten, Strömung, Schaum, Wind, Eis

Stand 29.10.2026, Branch `phase5/wasser-darstellung`. **Nur Testszene** (`--wassertest=1`); Rhein, Bäche, Hafen und Furten des Spiels laufen weiter mit `wasser.gdshader`. Der Einbau ist Phase 3. Phase 1 (Herkunft, Lizenz, Fehlerprotokoll): [phase1.md](phase1.md).

## Was gebaut ist

| Teil | Datei | Inhalt |
|---|---|---|
| Shader | `game/assets/shader/wasser_realistisch.gdshader` | Ableitung des übernommenen Original-Shaders (Copyright-Hinweis im Kopf): Strömung, Schaum, Wind, Regenringe, Eis, Kaustik und Tiefe aus der Welt; alle Werte als Uniforms |
| Arten | `game/scripts/Wasserarten.cs` | Alle Gewässerarten als Parametersätze an einer Stelle, Zuordnung Kartenwasser → Art, Winterfortschritt, Bilder und `Anwenden` auf ein Material |
| Testszene | `game/scripts/Wassertest.cs`, `game/scenes/Wassertest.tscn` | Je Art ein Feld (60 × 60 m) mit Ufer und Hindernissen; Wetter, Jahreszeit, Eisfortschritt, Teile abschaltbar |

**Bilder austauschbar:** Normalkarten, Strömungskarte, Schaumbild und Kaustik sind Sampler-Uniforms (`normalmap_a_sampler`, `normalmap_b_sampler`, `uv_sampler`, `foam_sampler`, `caustic_sampler`). Die Pfade stehen an einer Stelle in `Wasserarten.cs` (`NormalA`, … `KaustikAtlas`); ein fertiges Textur-Feld mit 16 Lagen lässt sich mit `Wasserarten.KaustikFeld` einsetzen. Am Shader ändert sich dabei nichts. Die fünf Bilder sind jetzt die eigenen aus Blender (`tools/wasser/wasser_texturen.py`, Branch wasser-tex); der Wechsel brauchte nur die Pfade in `Wasserarten.cs`.

## Parameter je Gewässerart

Alle Zahlen in `Wasserarten.cs`. „Wellen“ ist der Faktor auf die Steilheit der drei Wellen des Shaders (Original: 1), „Länge“ der Faktor auf deren Länge (3 m, 1,55 m, 0,9 m), „Wind“ der Anteil des Weltwindes.

| Art | Wellen | Länge | Wind | Strömung | Farbe flach | Farbe tief | Klarheit¹ | Schaum (Stärke, Breite) | Kaustik | Eis |
|---|---|---|---|---|---|---|---|---|---|---|
| Fluss | 0,25 | 1,6 | 0,4 | 1,2 m/s | braungrün (0,40 0,45 0,32) | dunkel blaugrün (0,10 0,19 0,21) | 1,3 | 0,9; 0,9 m, **nur ab 30 m Breite** | 0,3 | nie |
| Bach | 0,14 | 0,5 | 0,1 | 0,8 m/s | klar grünlich (0,55 0,62 0,52) | (0,22 0,36 0,36) | 0,9 | kein | 1,2 | ganz (bis 5 m Breite) |
| Teich | 0,12 | 0,5 | 0,3 | 0 | grünbraun (0,35 0,42 0,30) | (0,12 0,22 0,20) | 1,6 | kein | 0,6 | ganz (bis 2.000 m²) |
| See | 0,38 | 1,3 | 0,7 | 0 | blaugrün (0,48 0,60 0,58) | blau (0,12 0,27 0,37) | 1,4 | kein | 0,6 | Rand, 3–6 m |
| Meer | 0,8 | 3,5 | 1,0 | 0 | (0,44 0,60 0,60) | tiefblau (0,05 0,17 0,29) | 0,8 | 0,8; 1,2 m, dazu Schaumkronen auf den Wellen (0,6) bei Wind | 0,5 | nie |
| Hafenbecken | 0,12 | 0,6 | 0,2 | 0 | (0,40 0,45 0,38) | (0,10 0,17 0,20) | 1,8 | **kein** | 0,2 | nie |
| Furt | 0,10 | 0,4 | 0,1 | 1,0 m/s | sehr klar (0,58 0,60 0,50) | (0,30 0,40 0,36) | 0,6 | kein | 1,0 | nie |
| Kaisaum (nur Schaumstufe, kein Gewässer) | wie Fluss | | | 1,2 m/s | | | | **0,3; 0,35 m** | | nie |

¹ `beers_law` des Shaders: je kleiner, desto klarer (man sieht den Grund tiefer). Farben linear (Godot-Farbwerte).

**Kaisaum:** Der schmale, schwache Strömungssaum „dort, wo der offene Fluss an die Kaimauer strömt“ ist als eigene Stufe angelegt (`Wasserarten.KaiSaum`), kann aber nur über einen **eigenen Streifen** vor der Mauer mit eigenem Material wirken: Der Shader kennt keine Mauern. Im ruhigen Hafenbecken gibt es keinen Schaum. Gezeigt wird der Kaisaum in der Testszene noch nicht (Phase 3, mit den echten Hafenformen).

## Zuordnung Kartenwasser → Art (festgelegt, Einbau in Phase 3)

Die Karte kennt `wasser` mit `fluss`/`bach` als Linie mit `breite` und `see`/`teich` als Fläche (`docs/LEVELDESIGN.md`). `Wasserarten.AusKarte(art, breite, fläche)` setzt das um:

| Kartenwasser | Bedingung | Art | Schaum | Eis |
|---|---|---|---|---|
| `bach` | immer | Bach | nein | ganz |
| `fluss` | Breite ≤ 5 m | Bach | nein | ganz (Reichweite = halbe Breite · 1,2 + 0,5 m) |
| `fluss` | 5 m < Breite < 30 m | Fluss | **nein** | nie |
| `fluss` | Breite ≥ 30 m | Fluss | **ja** | nie |
| `teich` | immer | Teich | nein | ganz |
| `see` | Fläche ≤ 2.000 m² | Teich | nein | ganz (Reichweite aus dem Halbmesser · 1,2 + 0,5 m) |
| `see` | Fläche > 2.000 m² | See | nein | nur Rand: 3 m bei 2.000 m², 6 m ab 22.000 m² |
| Hafenbecken (Kern: `BeckenNetz` vor der Kaimauer eines Hafens) | ergibt sich aus dem Ort, nicht aus `art` | Hafenbecken | nein | nie |
| Furt (Kern: `Wasserplan`, Weg flacher als 0,5 m im Fluss) | ergibt sich aus dem Ort | Furt | nein | nie |
| Meer | kennt die Karte noch nicht (kein Kartenwasser mit dieser Art; Phase 3 oder spätere Küsten) | Meer | ja | nie |

Schwellen als Konstanten in `Wasserarten.cs`: `SchaumAbBreite` = 30 m, `BachBisBreite` = 5 m, `TeichBisFlaeche` = 2.000 m², `RandEisMin/-Max` = 3/6 m. Köln: der Rhein (60–100 m) bekommt Schaum, der Duffesbach (3 m) ist Bach und friert zu.

**Was Phase 3 dem Shader liefern muss:** Der Shader erwartet im Netz in `COLOR.r` den **Abstand zum Ufer in Metern geteilt durch 32** (0–1, 8 Bit, also bis 32 m, Schritte von 0,125 m) und `ufer_im_netz = 1`; ohne Netzwerte schätzt er den Abstand aus der Tiefe (grob, nur ein Notbehelf). Die alten Netze liefern in `COLOR` Tiefe und Breite; der Uferabstand lässt sich daraus rechnen (`halbe Breite · (1 − |quer|)`, im See aus der Tiefe oder dem Kern-Raster). Die Laufrichtung (`stroemung`, Meter je Sekunde nach x und z) ist ein Uniform **je Material**: Ein Fluss mit Kurven braucht entweder ein Material je Abschnitt oder Strömung als Eckenwert (`UV2`, wie im alten Shader) – das ist eine Phase-3-Entscheidung.

## Was der Shader kann

- **Strömung** (Fluss, Bach, Furt): `stroemung` in m/s verschiebt beide Normalkarten, die Strömungskarte und den Schaum in Laufrichtung; die zwei Karten laufen mit verschiedenem Tempo (1 : 1,35), so wirkt es wie fließendes, nicht verschobenes Wasser. Stehendes Wasser kräuselt nur langsam (`ruhe_drift`). **Aus Standbildern ist die Richtung nicht abzulesen**, beim Ausprobieren im laufenden Spiel ansehen.
- **Schaum neu:** Weicher, zerrissener Saum dort, wo hinter der Wasserfläche dicht darunter etwas liegt (Ufer, Fels, Pfahl), Breite `schaum_breite`, Kante mit Rauschen aufgerissen, mit Flocken verdichtet und mit der Strömung getragen. Das Käsemuster des Originals (Foam.png als harte Schwelle) ist weg; das Schaumbild geht nur noch weich (unscharf gelesen) als Störung ein. Nur wirksam bei `schaum_staerke > 0`.
- **Wind und Wetter:** Die Wellensteilheit wächst schwach mit dem Weltwind (`wetter_wind`, höchstens +60 % bei Windstärke 1 und vollem `wind_anteil`), die Wellenrichtungen neigen sich zu `wind_richtung`, die Kräuselung (`NORMAL_MAP_DEPTH`) wächst ebenfalls schwach. Bei Regen (`wetter_regen`) laufen Ringe über das Wasser (zwei Lagen Zellen, nur Normale, keine Geometrie). Es gibt keine Überschwemmung. Die Werte kommen aus denselben globalen Shader-Variablen wie bei Gras, Fahnen und Regen.
- **Eis:** Aus `eis` (Winterfortschritt 0–1 mal Eisregel) und `eis_reichweite` (Meter vom Ufer) wächst eine Eisfläche vom Ufer her: Rand mit Rauschen aufgerissen, durchsichtig am Wachstumsrand, Risse (verzogenes Voronoi), Wolken in Weiß-Blau, Schnee darauf nach `wetter_schnee`. Unter dem Eis liegt das Wasser still (Wellen und Normalkarte aus). Eisregeln siehe Tabelle. Nur Darstellung, ohne Wirkung auf das Spiel.
- **Winterfortschritt** (`Wasserarten.Winterfortschritt(Jahreszeit, Jahresanteil)`): Ein Spieljahr ist eine Jahreszeit (1140 Ticks, Winter 1403). Im **Winter** wächst das Eis im ersten Drittel des Jahres (bis 0,35) von 0 auf 1 und hält bis zum Jahresende; im **Frühling** taut es im ersten Drittel (bis 0,3) von 1 auf 0; Sommer und Herbst 0. Beim Jahreswechsel gleitet die Jahreszeit sonst in Echtzeit über (`jahreszeit_anteile`); der Winterfortschritt rechnet auf der Spielzeit und braucht dafür den Anteil im Spieljahr (Tick / 1140), den Phase 3 aus der Spielzeit holt.
- **Kaustik und Tiefe aus der Welt:** Der Original-Shader rechnete die Kaustik über eine eigene Projektionsmatrix (`inv_mvp`, 16 interpolierte Zahlen je Pixel); jetzt aus dem Weltpunkt des Grundes (`INV_VIEW_MATRIX`), das ist billiger und stimmt auch ohne Sonnenmatrix.
- **Teile abschaltbar** über `#define`-Zeilen am Shaderanfang (`MIT_BRECHUNG`, `MIT_TIEFE`, `MIT_KAUSTIK`, `MIT_SCHAUM`, `MIT_EIS`, `MIT_WIND`, `MIT_RINGE`): Ohne `MIT_BRECHUNG`/`MIT_TIEFE` liest der Shader weder Bildschirm- noch Tiefentextur. Das ist die Grundlage der Grafikstufen in Phase 3.

## Kosten (T400, Vulkan, 1600 × 900, V-Sync aus, Sonne mit Schatten)

**Wichtig zur Genauigkeit:** Die T400 teilt sich der Rechner mit anderen Aufgaben (Last 9–14); Einzelläufe schwankten um das Doppelte (dieselbe Szene ohne Wasser: 33–72 Bilder/s). Darum misst die Testszene **reihum in einem Prozess** (`--wassermessung=teile1|teile2|teile3|arten|winter|regen|sturm`, Konfigurationen je 5 s, drei Runden, Werte je Runde der Median der Bildzeit) und die Tabelle nennt das **beste** Ergebnis; nur die ruhigen Läufe sind verwertbar. Die Szene „alle Arten“ hat sieben Felder zu je 60 × 60 m (rund 25.000 m² Wasser) bei 1600 × 900.

| Konfiguration | beste Bildzeit | Bilder/s | Kosten gegenüber „ohne Wasser“ |
|---|---|---|---|
| ohne Wasser (Boden, Sonne, Schatten) | 11,7 ms | 86 | – |
| alle sieben Arten, volle Stufe | 16,0 ms | 62 | **+4,3 ms** |
| ohne Brechung (keine Bildschirmtextur) | 14,3 ms | 70 | −1,8 ms (**größter Einzelposten**) |
| ohne Tiefe (keine Tiefentextur) | 15,8 ms | 63 | −0,3 ms |
| ohne Kaustik | 16,1 ms | 62 | ±0 |
| ohne Schaum | 15,6 ms | 64 | −0,3 ms |
| ohne Wind | 16,7 ms (Vergleich 16,7) | 60 | ±0 |
| ohne Regenringe (nur bei Regen wirksam) | 16,7 ms (Vergleich 16,7) | 60 | ±0 bei klarem Wetter; im Regenlauf (unruhig) ebenfalls ohne erkennbaren Unterschied |
| ohne Eis (nur im Winter wirksam) | gleich (20,8 ms im unruhigen Winterlauf, beide) | – | ohne erkennbaren Unterschied |

Folgerungen für die Grafikstufen (Phase 3): **Brechung** abschalten spart am meisten (rund ein Siebtel des Bildes in dieser Szene); der Rest (Tiefe, Schaum, Kaustik, Wind, Ringe, Eis) zusammen unter einer halben Millisekunde und ist damit für alle Stufen vertretbar. Teuer ist vor allem die **Fläche in Pixeln** (Bildschirmtextur kopieren und lesen), nicht ein einzelner Effekt; ein breiter Fluss nahe an der Kamera kostet mehr als ein Bach. **Je Art** ließ sich die Zeit wegen der Last nicht sauber trennen (die Läufe „nur Fluss … nur Furt“ lagen zwischen 20 und 26 ms bei 20–22 ms ohne Wasser, ohne verwertbare Reihenfolge); die Messung je Art gehört in eine ruhige Stunde (`--wassermessung=arten`, rund 4 Minuten). Der Start ist langsam: Der Shader übersetzt bei der ersten Benutzung (20–30 Sekunden bis zum ersten Bild, mehr bei Last); in Phase 3 beim Laden der Stadt vorwärmen.

## Bilder und ehrliche Beschreibung

Alle Bilder habe ich selbst angesehen (T400, Vulkan, 1600 × 900). Ordner siehe unten. Namen: `s_…` und `z_…` sind die endgültigen Stände (die frühen `w_…`, `*_schraeg` zeigen Zwischenstände vor dem Abstimmen).

| Bild | Zu sehen |
|---|---|
| `s_alle_klar`, `z_alle` (Übersicht, mit den neuen Bildern) | Sieben Felder. Fluss: dunkelgrün-blau mit weißem, weichem Saum an beiden Ufern und an den zwei Felsen. Bach: schmaler heller Streifen ohne Schaum. Teich: dunkelgrün, ohne Schaum. See: blau, ohne Schaum, zarte Kräuselung. Meer: tiefblau mit lang laufender Dünung, Brandung am Strand und Schaumkronen. Hafenbecken: dunkel, still, ohne Schaum. Furt: heller Kies sichtbar. Aus dieser Entfernung wirken Fluss und Meer richtig, Teich und See flach; Einzelheiten nur in den Nahbildern. |
| `z_fluss` (schräg, neue Bilder) | Der Fluss: ruhige, feine Kräuselung, schwache, breite Wellenbänder quer zum Lauf (kaum noch Waschbrett), Schaum als weicher, zerrissener Saum am Ufer (rechts breiter, dichter) und als Kranz um die Felsen, kleiner Schaumfleck am Pfahl. Das Käsemuster ist weg. |
| `s_fluss_nah` (`t0`, `t1`, 1,5 s Abstand) | Blick flussauf aus Wasserhöhe; die Kräuselung hat sich zwischen den Bildern verschoben, die Richtung ist aus den Standbildern **nicht** zu erkennen. Mit dem Auge im laufenden Spiel zu prüfen. |
| `bach_schraeg`, `furt_schraeg` | Bach und Furt klar: Grund und Kaustik sichtbar (hellblau-weiße Lichtnetze), kein Schaum. Die Kaustik ist in der Furt stark; eher etwas dämpfen. |
| `s_meer_gewitter`, `meer_schraeg`, `s_meer_flach` | Meer: bei Gewitter dunkel mit Brandungsschaum und mehr Seegang, aber nicht übertrieben; die erste Fassung (Sturmfaktor 1,6) war mit Wellen über die Feldgrenze viel zu stark und ist auf +60 % gesenkt. Schaumkronen auf den Kämmen, Brandung an der Küste. Von der Küste aus flach gesehen wirkt es ruhig. |
| `s_alle_sturm` | Sturm: Meer deutlich unruhiger und dunkler, Fluss und See nur schwach wellig; dunklerer Himmel. |
| `s_teich_regen`, `s_meer_gewitter` | Regen: Ringe auf dem Wasser sichtbar; im Gewitter zu dicht und kraterartig (in der ersten Fassung), danach schwächer und enger gemacht; beim Teich jetzt feine Ringe zwischen den Wellen. Bleibt Geschmackssache. |
| `s_alle_winter`, `s_teich_winter`, `s_bach_winter` | Winter: Bach und Teich ganz zugefroren (weiß-blau, Risse, Schnee), See nur mit schmalem Eisrand (bei 3 m kaum zu sehen), Fluss, Meer, Hafenbecken und Furt offen. Der Teich bei 0,7 zeigt noch ein offenes Loch in der Mitte. Die Risse sind organisch, die ersten Blöcke durch den Hash sind behoben. Der Bach ist zu gleichmäßig weiß (Schnee auf dem Eis). |
| `w_alle_eis05`, `s_alle_tauwetter` | Halb gewachsenes und tauendes Eis: Eis wächst vom Ufer nach innen, beim Tauen bleibt ein Ring am Ufer. |
| `s_hafen_nah` | Hafenbecken an der Mauer: ruhiges Wasser ohne Schaum, ohne Saum an der Mauer; ein Kasten im Wasser zeigt Brechung. Der Kaisaum ist nicht gezeigt. |

**Fehler und Auffälligkeiten:** Das Meer rechts am Feldrand überragt die Feldgrenze (Wellen heben die Fläche); der Strand des Meeres und die Kanten der Testfelder sind eckig (Testszene, kein Shaderfehler). Bei Wind aus einer Richtung zeigen See und Meer noch regelmäßige Wellenzüge. Die Himmelsspiegelung ist schwach, weil die Testszene nur einen einfachen Himmel hat.

## Testanleitung für den Nutzer

1. Mit **Vulkan** im Ordner `game/` des Branches `phase5/wasser-darstellung` starten: `godot --rendering-driver vulkan --path game -- --wassertest=1`. Das Hauptmenü wird übersprungen. **Der erste Start braucht etwa 20–30 Sekunden**, bis das Bild steht (der Shader wird übersetzt); bis dahin zeigt das Fenster das Godot-Logo.
2. Zu sehen sind die sieben Gewässerarten nebeneinander (oben Fluss, Bach, Teich, See; unten Meer, Hafenbecken, Furt). Die Kamera schaut von schräg oben: `--wassersicht=oben` von oben, `fern` weiter weg.
3. Einzelne Art groß: `--wasserart=fluss` (auch `bach`, `teich`, `see`, `meer`, `hafen`, `furt`) mit `--wassersicht=schraeg|nah|flach|ufer|oben`. **Strömung** am besten bei `--wasserart=fluss --wassersicht=nah` oder `--wasserart=furt` ansehen: Das Wasser muss zur Kamera hin laufen, der Schaum an Pfahl und Felsen mit.
4. **Schaum:** nur der Fluss (36 m breit) und das Meer haben ihn; er soll weich und zerrissen am Ufer und um Fels und Pfahl liegen, **nicht** wie Blasen oder Käse. Teich, See, Bach, Furt und Hafenbecken dürfen keinen Schaum zeigen.
5. **Wetter:** `--wetter=sturm`, `gewitter` und `regen` (z. B. `--wasserart=teich --wetter=regen --wassersicht=nah`): Die Wellen sollen sichtbar, aber nur schwach stärker werden, bei Regen laufen Ringe über das Wasser.
6. **Eis:** `--jahreszeit=winter` (Eis voll), `--winter=0.5` (halb gewachsen), `--jahreszeit=fruehling --winter=0.25` (taut). Erwartet: Bach und Teich frieren ganz zu, der See bekommt nur einen schmalen Rand, Fluss, Meer, Hafenbecken und Furt bleiben offen; auf dem Eis liegen Risse und etwas Schnee.
7. **Bildrate:** Im Log steht jede Sekunde `Bilder/s`. Zum Vergleich ohne Wasser `--wasserart=keins`; Teile abschalten mit `--wasserteile=-brechung,-kaustik` (siehe `docs/STARTOPTIONEN.md`).
8. **Zurückmelden:** (a) Gefallen Schaum und Eis im gemalt-realistischen Stil der Spielwelt? (b) Wirkt die Strömung richtig (Richtung, Tempo)? (c) Sind die Wellen bei Sturm „schwach genug“ oder immer noch zu stark? (d) Bildrate auf deinem Rechner (Zahl aus dem Log, mit `--wasserart=alle` und `--wasserart=fluss`). (e) Fehler oder seltsame Stellen (Bild oder Beschreibung).

Bilder dieser Phase: `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser/phase2/` (nicht im Repository).
