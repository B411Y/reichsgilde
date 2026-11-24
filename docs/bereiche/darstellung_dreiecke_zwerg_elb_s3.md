# Dreiecke je Bauteil: Zwerg- und Elbenhaus (Stand Patrizier-Entwurf)

Erzeugt von `tools/modelle/neubau/dreiecke_tabelle.py` aus den Logs der Bauskripte. Dreiecke der Aussenhuelle (Haus, Dach, Scheiben); Wappen, Fahne, Leerstand und Geruest zaehlen nicht. Die Grenze 8.000 gilt hier vorerst nicht (Nutzer 23.11.2026); die Tabelle ist die Grundlage fuer eine spaetere Optimierung. Bauteile sind die Bauteilfunktionen der Bauskripte (Messung mit REICHSGILDE_ZAEHLEN=1, `dreiecke_zaehler.py`; ausschliesslich gezaehlt: Dreiecke, die untergeordnete Funktionen erzeugen, stehen dort); der Rest steht unter „Haus ohne die einzeln gezaehlten Teile“ (z. B. Dachfunktionen, die nicht ueber `h` laufen).

## Zwerg Stufe 3 (Patrizier)

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| spitz_rahmen | 1247 | 1247 | 1247 | 1247 |
| eckquader_l | 816 | 816 | 816 | 816 |
| kreis_ring | 576 | 576 | 576 | 576 |
| fensterstock | 576 | 576 | 576 | 528 |
| treppengiebel | 510 | 510 | 510 | 510 |
| zinnen_fiale | 480 | 480 | 480 | 480 |
| hammer_schlaegel | 384 | 384 | 384 | 384 |
| gewaende_zwerg | 356 | 356 | 356 | 316 |
| blendnische | 281 | 281 | 281 | 281 |
| steinwand | 276 | 276 | 276 | 252 |
| tuerbeschlaege | 216 | 216 | 216 | 216 |
| schornstein | 190 | 190 | 190 | 190 |
| eisengitter | 132 | 132 | 132 | 132 |
| kragsteinfries | 106 | 106 | 106 | 106 |
| steingiebel | 61 | 61 | 61 | 61 |
| oeffnungen | 52 | 52 | 52 | 52 |
| treppengiebel_rueckseite | 48 | 48 | 48 | 48 |
| steinhaus_fenster | 42 | 42 | 42 | 30 |
| vordach | 41 | 41 | 41 | 41 |
| Haus ohne die einzeln gezaehlten Teile | 516 | 746 | 868 | 789 |
| **Haus** | 6906 | 7136 | 7258 | 7055 |
| **Dach** | 383 | 457 | 429 | 435 |
| **Scheiben** | 52 | 52 | 52 | 60 |
| **Summe** | 7341 | 7645 | 7739 | 7550 |

## Elb Stufe 3 (Patrizier)

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| _stange | 1376 | 1376 | 1376 | 1376 |
| fensterstock | 576 | 576 | 576 | 576 |
| gewaende | 476 | 476 | 476 | 476 |
| holz_zier | 290 | 290 | 290 | 290 |
| steinwand | 278 | 278 | 278 | 278 |
| schornstein | 190 | 190 | 190 | 190 |
| spitzboegen | 128 | 128 | 128 | 128 |
| schildgiebel | 126 | 126 | 126 | 126 |
| knospe | 120 | 120 | 120 | 120 |
| vorder_zier | 106 | 106 | 106 | 106 |
| efeu_setzen | 80 | 80 | 80 | 80 |
| steingiebel | 61 | 61 | 61 | 61 |
| oeffnungen | 52 | 52 | 52 | 52 |
| unterstand_links | 52 |  |  | 52 |
| steinhaus_fenster | 42 | 42 | 42 | 42 |
| vordach | 41 | 41 | 41 | 41 |
| fiale | 36 | 36 | 36 | 36 |
| kielbogenfries | 10 | 10 | 10 | 10 |
| vordach_platte | 8 | 8 | 8 | 8 |
| Haus ohne die einzeln gezaehlten Teile | 1248 | 1376 | 1464 | 1248 |
| **Haus** | 5296 | 5372 | 5460 | 5296 |
| **Dach** | 502 | 576 | 548 | 502 |
| **Scheiben** | 48 | 48 | 48 | 48 |
| **Summe** | 5846 | 5996 | 6056 | 5846 |

## Zwerg Stufe 2

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| fachwerk | 1734 | 1666 | 1700 | 1734 |
| fensterstock | 816 | 816 | 816 | 768 |
| giebel_lage | 404 | 404 | 404 | 404 |
| schornstein | 190 | 190 | 190 | 190 |
| oeffnungen | 160 | 160 | 160 | 160 |
| giebel | 122 | 122 | 122 | 122 |
| vordach | 41 | 41 | 41 | 41 |
| Haus ohne die einzeln gezaehlten Teile | 1238 | 1670 | 1488 | 1421 |
| **Haus** | 4705 | 5069 | 4921 | 4840 |
| **Dach** | 431 | 505 | 477 | 483 |
| **Scheiben** | 56 | 56 | 56 | 64 |
| **Summe** | 5192 | 5630 | 5454 | 5387 |

## Zwerg Stufe 1

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| Haus ohne die einzeln gezaehlten Teile | 2688 | 1892 | 2824 | 1934 |
| **Haus** | 2688 | 1892 | 2824 | 1934 |
| **Dach** | 1154 | 1322 | 1154 | 1211 |
| **Scheiben** | 28 | 20 | 28 | 16 |
| **Summe** | 3870 | 3234 | 4006 | 3161 |

