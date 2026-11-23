# Dreiecke je Bauteil: Zwerg- und Elbenhaus (Stand Patrizier-Entwurf)

Erzeugt von `tools/modelle/neubau/dreiecke_tabelle.py` aus den Logs der Bauskripte. Dreiecke der Aussenhuelle (Haus, Dach, Scheiben); Wappen, Fahne, Leerstand und Geruest zaehlen nicht. Die Grenze 8.000 gilt hier vorerst nicht (Nutzer 23.11.2026); die Tabelle ist die Grundlage fuer eine spaetere Optimierung. Bauteile sind die Bauteilfunktionen der Bauskripte (Messung mit REICHSGILDE_ZAEHLEN=1, `dreiecke_zaehler.py`; ausschliesslich gezaehlt: Dreiecke, die untergeordnete Funktionen erzeugen, stehen dort); der Rest steht unter „Haus ohne die einzeln gezaehlten Teile“ (z. B. Dachfunktionen, die nicht ueber `h` laufen).

## Zwerg Stufe 3 (Patrizier)

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| spitz_rahmen | 1247 | 1247 | 1247 | 1247 |
| treppengiebel | 902 | 902 | 902 | 902 |
| eckquader_l | 816 | 816 | 816 | 816 |
| _kreis_ring_alt | 576 | 576 | 576 | 576 |
| fensterstock | 576 | 576 | 576 | 528 |
| blendnische | 497 | 497 | 497 | 497 |
| steinhaus_fenster | 398 | 398 | 398 | 346 |
| schornstein | 190 | 190 | 190 | 190 |
| eisengitter | 132 | 132 | 132 | 132 |
| steingiebel | 61 | 61 | 61 | 61 |
| oeffnungen | 52 | 52 | 52 | 52 |
| treppengiebel_rueckseite | 48 | 48 | 48 | 48 |
| vordach | 41 | 41 | 41 | 41 |
| Haus ohne die einzeln gezaehlten Teile | 898 | 1128 | 1250 | 1147 |
| **Haus** | 6434 | 6664 | 6786 | 6583 |
| **Dach** | 383 | 457 | 429 | 435 |
| **Scheiben** | 52 | 52 | 52 | 60 |
| **Summe** | 6869 | 7173 | 7267 | 7078 |

## Elb Stufe 3 (Patrizier)

| Bauteil | G1 | G2 | G3 | G4 |
|---|---:|---:|---:|---:|
| _stange | 1376 | 1376 | 1376 | 1376 |
| fensterstock | 576 | 576 | 576 | 576 |
| gewaende | 476 | 476 | 476 | 476 |
| schornstein | 190 | 190 | 190 | 190 |
| spitzboegen | 128 | 128 | 128 | 128 |
| knospe | 120 | 120 | 120 | 120 |
| vorder_zier | 106 | 106 | 106 | 106 |
| oeffnungen | 93 | 93 | 93 | 93 |
| efeu_setzen | 80 | 80 | 80 | 80 |
| steingiebel | 61 | 61 | 61 | 61 |
| unterstand_links | 52 |  |  | 52 |
| steinhaus_fenster | 42 | 42 | 42 | 42 |
| fiale | 36 | 36 | 36 | 36 |
| vordach_platte | 8 | 8 | 8 | 8 |
| Haus ohne die einzeln gezaehlten Teile | 1950 | 2078 | 2166 | 1950 |
| **Haus** | 5294 | 5370 | 5458 | 5294 |
| **Dach** | 502 | 576 | 548 | 502 |
| **Scheiben** | 48 | 48 | 48 | 48 |
| **Summe** | 5844 | 5994 | 6054 | 5844 |

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

