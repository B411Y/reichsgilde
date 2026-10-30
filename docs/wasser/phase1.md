# Realistisches Wasser, Phase 1: Übernahme und Lauffähigkeit

Stand 29.10.2026, Branch `phase5/wasser-darstellung`. **Nur geprüft und lauffähig gemacht**; das Wasser des Spiels (Rhein, Bäche, Hafen, Furt) ist unverändert. Über die nächste Phase entscheidet der Nutzer.

## Herkunft und Lizenz

| Quelle | Stand | Lizenz | Ergebnis |
|---|---|---|---|
| `godot-extended-libraries/godot-realistic-water` (UnionBytes / Achim Menzel „AiYori“, Copyright 2019; Pflege K. S. Ernest „iFire“ Lee) | Commit `fa0fc478`, 15.04.2026; Shader zuletzt von clayjohn für Godot 4.2 überarbeitet (`1c706c48`, 17.09.2024) | MIT (`LICENSE.md`, im Shaderkopf wiederholt) | **übernommen** |
| `spicyhaydenc/Godot-4-Realistic-Water` (Hayden Connor, Port auf Godot 4.0) | Commit `31966284`, 07.05.2023 | MIT, gleicher Text | geprüft, **nicht übernommen** (Shader rechnet unter 4.7 falsch, siehe unten) |

- Beide Lizenztexte (`LICENSE.md`) und `AUTHORS.md` sind bis auf die Titelzeile („GODOT 3.1.1“ bzw. „4.0“) und den Abschnitt „Port to Godot 4“ gleich. Übernommen sind die Dateien des Originals, unverändert.
- **Die fünf Bilder** (zwei Normalkarten, Strömungskarte, Schaum, Kaustik-Atlas) sind in Original und Port **byte-gleich** (SHA-256 verglichen). Sie liegen seit dem 30.04.2019 (Commit `098f33e`, „Update paths“) im Original-Repository. Weder README, noch Commit-Texte, noch AUTHORS nennen eine **Einzelquelle** der Bilder; es gilt die MIT-Erlaubnis des ganzen Repositorys. Das ist ein Rest-Risiko (ob die Urheber der Bilder selbst die Rechte hatten, lässt sich aus dem Repository nicht belegen) und steht so im Protokoll. Der Teamleiter klärt, ob das für Steam genügt.
- **Nicht übernommen** (aus dem Port bzw. Original): die Beispielszene mit Boot, Fels, Boden und Seegras (`Boat`, `Rock`, `Ground`, `Seaweed` mit Normal-, Rauheits- und Farbbildern, `Noise.png`, `Seaweed.shader`), `icon.svg`, `Realistic Water Shader.jpg`, `.material`-Dateien (binär). Gründe: Die Herkunft dieser Modelle und Bilder ist **nirgends belegt** (kein Hinweis im Repository, kein Urheber im AUTHORS), und das Spiel braucht sie nicht.
- Ablage: nach unserer Ordnung nach Dateiart statt `game/addons/` (kein Godot-Plugin, nur ein Shader plus Bilder): `game/assets/shader/realistisches_wasser/` (Shader, `LICENSE.md`, `AUTHORS.md`, `HERKUNFT.md`) und `game/assets/texturen/realistisches_wasser/` (Bilder, `HERKUNFT.md`). Dadurch prüft `tools/assets/protokoll_pruefen.py` sie automatisch; unter `game/addons/` würde die Prüfung sie nicht erfassen.

## Fehlerprotokoll (Godot 4.7.2 mono, Forward+, Vulkan auf der T400)

| Meldung / Beobachtung | Ursache | Behebung |
|---|---|---|
| Import (`--headless --import`): keine Warnung zu Shader oder Bildern | – | – |
| Original-Shader (`Water_Original.gdshader`): kein Kompilierfehler, keine Laufzeitmeldung | Die Godot-4-Fassung verwendet schon `hint_depth_texture` / `hint_screen_texture`, `source_color`, `hint_normal`, `hint_default_black`, `INV_PROJECTION_MATRIX` | **nichts zu ändern**; Shader unverändert |
| Port-Shader (4.0): kein Kompilierfehler, aber **das ganze Wasser ist weiß** (Schaum überall, Bild `bilder/p_schraeg`) | Der Port rechnet die Wassertiefe mit `depth_raw * 2.0 - 1.0` und `PROJECTION_MATRIX[3][2] / (depth_raw + PROJECTION_MATRIX[2][2])` (Tiefenbereich −1…1 wie in OpenGL); Godot 4 legt die Tiefe im Bereich 0…1 ab. Dazu nimmt er `vertex_height` (Clip-Raum-z) statt `-VERTEX.z` | Nicht geflickt. Die Fassung des Originals löst es mit `INV_PROJECTION_MATRIX` (Tiefe zurück in den Sichtraum, Abstand zum Boden als `distance`) und ist deshalb die übernommene |
| `uniform sampler2DArray caustic_sampler`: ohne Belegung nur schwarz, kein Fehler | Der Shader erwartet ein Textur-Feld (`Texture2DArray`) mit 16 Lagen; die Datei `Caustic.png` ist ein 4×4-Atlas aus 256-Pixel-Bildern | In der Testszene zerlegt `Wassertest.Kaustik()` den Atlas in 16 Bilder und baut das Feld (kein Eingriff im Shader) |
| `uniform mat4 projector` | Wird im Shader nirgends gelesen (Rest der Godot-3-Fassung) | bleibt, unwirksam |
| `uv_sampler : hint_anisotropy` | Merkwürdiger Hinweis für eine Strömungskarte, wirkt aber wie gewollt (lineare Filterung, Anisotropie) | bleibt |
| Shutdown: `BUG: Unreferenced static string …` | Kommt beim Beenden durch das Ende des Klicktests (`kill`), in allen Läufen, auch ohne Wasser | keine; nicht vom Wasser |
| `TREIBER=opengl3`: Shader lädt, Szene läuft (7,5 Bilder/s, Mesa-Software), aber das Wasser ist durchsichtig-glasig ohne Tiefe, ohne Schaum und ohne Brechung; im Log zusätzlich `shader type fog not supported in OpenGL renderer` und tausende „Too many instances using shader instance variables“ | Tiefen- und Bildschirmtextur gibt es im Kompatibilitätsrenderer nicht; die Fehlermeldungen kommen aus dem Spiel selbst, nicht aus diesem Shader (im Vulkan-Lauf fehlen sie), Ursache nicht weiter verfolgt | nicht behebbar, Wasser nur für Forward+ (der Renderer des Spiels) |
| Bildrand unten („hellblaues Band“ bei nahen Ansichten) | Rand der Testszene: Die Wasserfläche reicht über den Rand des Testbodens hinaus, dort sieht die Kamera Wasser über dem Nichts | Fehler der Testszene, kein Shaderfehler |

**Braucht der Shader Tiefe und Bildschirmtextur?** Ja, beides (`hint_depth_texture` und `hint_screen_texture`). Unter Forward+ wirkt er damit richtig (Tiefenfärbung, Brechung des Grundes, Schaum an Felsen, Pfahl und Ufer). Folgen: Godot kopiert für das Bildschirm-Bild den Bildpuffer, und Wasser sieht **keine anderen Wasserflächen** und keine Dinge, die später in der Durchsichtigen-Reihe gezeichnet werden (Nebel, Regenringe, Partikel hinter dem Wasser).

## Kosten (T400, Vulkan, 1600×900, V-Sync aus, Sonne mit Schatten, 200×200-Wellennetz, 80×80 m)

| Ansicht | Bilder/s mit Wasser | Bilder/s ohne Wasser |
|---|---|---|
| schräg | 42 | 70 |
| fern | 65 | – |
| oben | 46 | – |
| flach | 48 | – |
| nah | 37 | – |
| Ufer | 31 | – |

Das Wasser kostet bei dieser Fläche rund **6–14 ms** je Bild. Teuer ist der Pixel-Teil (fünf Bildzugriffe plus Tiefen- und Bildschirmtextur, viele Pixel bei nahen Ansichten), weniger das Netz. Für ein Fluss-Band (statt 80×80 m) und ohne die nahe Ansicht wäre es merklich billiger. Die Messung ist grob (eine Szene, ein Lauf je Ansicht).

## Vergleich mit `game/assets/shader/wasser.gdshader`

| | bisher (`wasser.gdshader`) | Realistic Water Shader |
|---|---|---|
| Wellen | nur Normale (Rauschen, in Fließrichtung gestreckt), Fläche bleibt eben | Gerstner-Wellen im Eckpunkt-Teil (drei Wellen, Fläche hebt sich) plus Normalkarten |
| Strömung | je Eckpunkt aus `UV2` (Richtung und Tempo), Wellen wandern flussab | eine feste Richtung für alle Wellen und Karten (`sampler_direction`), **keine Strömung je Stelle** |
| Tiefe | je Eckpunkt aus `COLOR` (Parabelbett im Fluss, Wert je Ecke im See), also **Daten aus der Karte** | aus der **Tiefentextur** (Abstand zum gerenderten Grund), unabhängig von der Karte |
| Ufer, Schaum | Saum nach Abstand zum Ufer (aus Breite bzw. Tiefe), Ufer läuft durchsichtig aus (ALPHA) | Schaum, wo der Grund nahe an der Fläche ist (Tiefentextur), Ufer deckend |
| Spiegelung | Himmel als Farbe `himmel` (das Spiel setzt sie jedes Bild), EMISSION mit Fresnel | Spiegelung der Umgebung/des Himmels über Godots Standardbeleuchtung (`ROUGHNESS 0,2`) |
| Brechung | keine | ja (Bildschirmtextur) |
| Kosten | sehr gering, kein Bildschirm-Zugriff | deutlich höher (siehe oben) |
| Render-Mode | `blend_mix, depth_draw_never, cull_disabled` | `blend_mix, cull_back` (schreibt Tiefe) |

Genutzt wird `wasser.gdshader` in `Stadtansicht.Wasser.cs` (Flüsse und Bäche als Band, Seen und Teiche als Fläche, Hafenbecken wie ein See) und `Stadtansicht.Wasser.Uebergaenge.cs` (Furten, Fähren, Hafen). Zu beachten beim späteren Ersetzen (nur festgestellt, nichts geändert):

- Die Netze liefern **Eckenwerte im Vertex-Format des alten Shaders** (`UV` = quer/Stelle, `UV2` = Strömung, `COLOR` = Tiefe, Breite, Fluss-Kennzeichen). Der neue Shader erwartet Weltkoordinaten (`UV = vertex.xz * sampler_scale`); ein Fluss-Band bräuchte Strömung und Randwerte in einem eigenen Shader-Abschnitt.
- Das **Band-Netz** ist grob (Ufer folgt dem Boden); Gerstner-Wellen brauchen ein fein unterteiltes Netz, sonst sieht man Ecken.
- Der Shader **schreibt Tiefe** (`depth_draw_opaque` ist Vorgabe) und **braucht den Boden darunter**; das Wasser des Spiels liegt „knapp über dem Boden“ (Furt, Bach), da würde die Tiefentextur kaum Unterschied liefern und das Wasser dort überall Schaum zeigen.
- `himmel` und die Nachtfarbe setzt das Spiel jedes Bild für das alte Wasser; der neue Shader spiegelt die Umgebung des Himmels und braucht dafür eine echte Himmelsquelle (`WorldEnvironment`), die das Spiel zurzeit nicht hat.
- Wetter (Nässe, Regenringe, Schnee, Frost), Nebel und Nachtlichter wirken heute über das alte Wasser; sie müssten in den neuen Shader.

## Notizen für die spätere Planung (nur notiert, nichts gebaut)

**Parameter des Shaders** (`uniform`):

| Gruppe | Parameter | Wirkung |
|---|---|---|
| Wellen | `wave_speed`, `wave_a`, `wave_b`, `wave_c` (xy = Richtung, z = Steilheit, w = Länge) | drei Gerstner-Wellen im Eckpunkt-Teil; Länge und Steilheit je Gewässerart |
| Strömung / Oberfläche | `sampler_scale`, `sampler_direction`, `uv_sampler`, `uv_sampler_scale`, `uv_sampler_strength`, `normalmap_a_sampler`, `normalmap_b_sampler` | Richtung und Tempo der Wellenkarten (Strömung nur als eine Richtung für die ganze Fläche), Verwirbelung durch die Strömungskarte |
| Schaum | `foam_sampler`, `foam_level` | Schaum nahe an Dingen im Wasser und am Ufer (Abstand aus der Tiefentextur) |
| Farbe / Tiefe | `color_deep`, `color_shallow`, `beers_law`, `depth_offset`, `refraction` | Farbverlauf und Durchsicht nach Wassertiefe, Stärke der Brechung |
| Kaustik | `caustic_sampler` (Textur-Feld mit 16 Lagen), `projector` (ungenutzt) | helle Muster auf dem Grund im flachen Wasser |

**Eignung je Gewässerart:** Meer / See / Hafen: gut (Wellen, Tiefenfarbe, Kaustik am Ufer). Rhein: bedingt (breiter, träger Fluss; es fehlt die Strömung je Stelle, die der alte Shader hat). Bach und Furt: schlecht (knapp über dem Boden, zu seicht für Tiefenfärbung, kleine Fläche; der alte Shader mit Ufer-Durchsichtigkeit passt besser). Teich: ruhig, kleine Wellen (`wave_*` klein), Kaustik an.

**Was fehlt** (Nutzerentscheidung 29.10.2026: es gibt nur den gemalt-realistischen Stil der 3D-Welt, keine Stilwahl):

- **Eis** (zugefrorene Gewässer im Winter): keine Eisfläche, kein Übergang, keine Schollen. Müsste ein Zustand oder eigener Shader sein (Spiel: Jahreszeit).
- Strömung je Stelle, Ufer-Durchsichtigkeit, Anbindung an Wetter, Nacht und Mondlicht (Spiegelung ist Godots normale Beleuchtung, also kein eigener Himmel).
- Kein Rückwasser, keine Wellen am Rand eines Beckens.

## Testanleitung für den Nutzer

1. Im Ordner `game/` des Branches `phase5/wasser-darstellung` mit **Vulkan** starten: `godot --rendering-driver vulkan --path game -- --wassertest=1` (oder im Editor die Startoptionen setzen). Das Hauptmenü wird übersprungen.
2. Zu sehen sein sollte ein rundes Becken mit Sand, drei Felsen, Pfahl und Kiste im Wasser und einer Sandbank rechts hinten, von schräg oben. Das Wasser bewegt sich.
3. Andere Blickpunkte: `--wassersicht=nah|fern|schraeg|oben|flach|ufer`, z. B. `-- --wassertest=1 --wassersicht=ufer`.
4. Worauf achten: **Wellen** (sind sie sichtbar und ruhig genug, wandern sie?), **Tiefe** (flach hellgrün, tief blaugrau; Grund sichtbar am Rand), **Schaum** an Fels, Pfahl, Ufer (Blasen- oder Käsemuster wirkt wie?), **Spiegelung** (Himmel, Sonnenglitzer), **Brechung** (verzerrt sich der Grund?), **Kanten** (harte Linien, Flackern).
5. Zum Vergleich ohne Wasser: `--wasserart=keins`. Im Log steht jede Sekunde `[wassertest] … Bilder/s`.
6. Mit dem alten Wasser vergleichen: Kartenwerkstatt oder eine Stadt mit Rhein (z. B. Köln) öffnen und schauen, ob das neue Wasser besser, gleich oder schlechter wirkt.
7. Zurückmelden: (a) Gefällt es? (Gesamteindruck, Stil zur Spielwelt) (b) Bildrate auf deinem Rechner (Zahl aus dem Log) (c) Fehler oder seltsame Stellen (Beschreibung oder Bild) (d) ob es für Rhein, Bach, Teich, Hafen weiterverfolgt werden soll.

Bilder dieser Phase: `/tmp/claude-1000/-home-claude-projekte-reichsgilde/4c541d35-c849-4e58-bcde-35f887f98cda/scratchpad/wasser/bilder/` (nicht im Repository).
