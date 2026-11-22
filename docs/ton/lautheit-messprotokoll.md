# Messprotokoll der Lautheit (Ton Schritt 7)

Gemessen nach ITU-R BS.1770 / EBU R128 mit ffmpeg (Filter `ebur128`, True Peak 4-fach überabgetastet) durch `tools/ton/lautheit_messen.py`; Rohwerte: `docs/assets/lautheit-messwerte.csv`. Die Audiodateien sind **unverändert**; die Angleichung der Oberflächenklänge steht in `game/daten/klang_lautheit.json` (`tools/ton/lautheit_angleichen.py`) und wirkt als Lautstärke der Variante beim Abspielen.

Spalten: **LUFS** = integrierte Lautheit der Datei (Dateien unter 1 s: –), **M max** = höchste 400-ms-Lautheit, **TP** = True Peak in dBTP, **Angleichung** = dB, die das Spiel der Variante zugibt, **M nachher** = M max plus Angleichung.

## Je Gruppe

| Klasse | Gruppe | Dateien | LUFS min / Median / max | M max min / Median / max | TP höchste | M-Streuung nachher (max − min) |
|---|---|---|---|---|---|---|
| Oberflaeche | oberflaeche | 33 | −23.0 / −20.4 / −18.0 | −36.4 / −20.0 / −15.5 | 2.1 | 20.9 → 9.9 |
| Musik | event | 10 | −17.1 / −17.0 / −16.9 | −14.3 / −13.3 / −11.5 | −3.8 | 2.8 → 2.8 |
| Musik | glaube | 4 | −17.0 / −17.0 / −17.0 | −13.6 / −12.9 / −12.7 | −5.3 | 0.9 → 0.9 |
| Musik | menue | 1 | −17.1 / −17.1 / −17.1 | −12.7 / −12.7 / −12.7 | −3.7 | 0.0 → 0.0 |
| Musik | nacht | 4 | −17.1 / −17.0 / −17.0 | −13.1 / −12.1 / −11.2 | −2.7 | 1.9 → 1.9 |
| Musik | tag | 25 | −18.4 / −17.0 / −16.9 | −14.0 / −12.4 / −9.0 | −1.3 | 5.0 → 5.0 |
| Musik | tod | 4 | −17.0 / −16.9 / −16.9 | −12.9 / −11.6 / −11.6 | −3.8 | 1.3 → 1.3 |
| Stimmen | haushofmeister | 120 | −18.8 / −18.0 / −17.9 | −15.9 / −14.6 / −12.3 | −0.7 | 3.6 → 3.6 |
| Umgebung | glocken | 9 | −19.8 / −17.0 / −16.9 | −16.6 / −13.6 / −12.9 | −2.2 | 3.7 → 3.7 |
| Umgebung | karren | 2 | −26.2 / −26.1 / −26.1 | −22.3 / −21.5 / −20.7 | −7.4 | 1.6 → 1.6 |
| Umgebung | rufe | 73 | −26.4 / −20.1 / −14.1 | −23.7 / −17.1 / −5.4 | 1.2 | 18.3 → 18.3 |
| Umgebung | schichten | 17 | −28.1 / −26.2 / −26.1 | −25.7 / −20.8 / −14.3 | −3.1 | 11.4 → 11.4 |

Die Streuung der Gruppe „Oberflaeche“ vermischt verschiedene Ereignisse (Klick und Siegel liegen absichtlich weit auseinander); maßgebend ist die Streuung **innerhalb eines Ereignisses**:

| Ereignis | Varianten | M max vorher (min bis max) | Streuung vorher | M nachher | Streuung nachher |
|---|---|---|---|---|---|
| Klick | 3 | −36.4 bis −23.5 | 12.9 dB | −26.0 | 0.0 dB |
| Reiter | 3 | −30.8 bis −20.9 | 9.9 dB | −24.0 | 0.0 dB |
| FensterAuf | 3 | −35.1 bis −22.0 | 13.1 dB | −24.0 | 0.0 dB |
| FensterZu | 3 | −32.4 bis −18.6 | 13.8 dB | −23.0 | 0.0 dB |
| Muenzen | 2 | −28.5 bis −19.4 | 9.1 dB | −18.0 | 0.0 dB |
| Meldung | 3 | −21.9 bis −19.5 | 2.4 dB | −20.5 | 0.0 dB |
| Siegel | 3 | −18.2 bis −15.5 | 2.7 dB | −17.0 | 0.0 dB |
| Bauen | 4 | −19.3 bis −16.0 | 3.3 dB | −17.0 | 0.0 dB |
| Tempo | 2 | −28.0 bis −16.2 | 11.8 dB | −23.0 | 0.0 dB |

## Lautheitsziele je Klasse (Empfehlung, Schritt 7)

Der Plan nannte kein Ziel; die Werte sind die schon gebauten Pegel, auf eine Tabelle gebracht und ergänzt. Alle Zahlen: LUFS bzw. dB am Master bei Regler 100 % (= 0 dB), vor dem Ducking.

| Klasse (Bus) | Ziel der Dateien | im Spiel wirksam | Begründung |
|---|---|---|---|
| Stimmen (Stimmen) | −18 LUFS integriert, Spitze unter −0,7 dBTP | −18 (Spieler 0 dB) | Sprache soll am deutlichsten sein; die Ansagen sind selten und kurz, ein Abstand von mindestens 8 dB zur Musik und 12 dB zur Umgebung hält sie verständlich, ohne die Musik zu ducken (die Musik duckt dennoch um 9 dB bei einer Ansage) |
| Musik (Musik) | −17 LUFS integriert (Datei) | Partie −26 (Spieler −9 dB), Menü −21 (−4 dB), Ereignisstück −23 (−6 dB) | lange, gleichmäßige Hörzeit: lieber leise unter der Handlung; im Menü gibt es nichts darüber, dort lauter |
| Umgebung (Umgebung und Unterbusse) | −26 LUFS je Schicht und Ruf | Summe der Stadt gedeckelt bei −30 LUFS, Pause −8 dB, Innenraum −14 dB | Hintergrund ohne Dauerdruck; einzelne Ereignisse (Donner −16/−18, Einschlag −14, Glocken −17, Regen) liegen absichtlich darüber |
| Oberfläche (Oberflaeche) | höchste 400-ms-Lautheit der Datei, je Ereignis angeglichen | Bedienung (Klick, Reiter, Tempo) −32, Fenster −30, Zahlung und Münzen −24, Fehler −25, Meldungstöne −26 bis −23, Siegel und Bauen −22, Türen −22, Gefahrglocke −32,5 | häufige Klänge leise (Bedienung), seltene mit Bedeutung lauter (Handlung, Meldung); Maß der 400-ms-Spitze, weil die Klänge kürzer als die 3 Sekunden der integrierten Messung sind |

## Pegel je Bus

Alle Bus-Pegel bleiben bei **0 dB** (`game/default_bus_layout.tres`, unverändert); die Hauptbusse sind nur die Regler des Spielers. Begründung: Die Pegelverhältnisse zwischen den Klassen stecken schon in den Dateien (−17/−18/−26 LUFS) und in den Spielerpegeln des Codes (Musik −9/−4/−6 dB, Ansagen 0 dB, Umgebung nach `KlangUmgebungModell`), die Unterbusse der Umgebung ändern ihren Pegel laufend (Raum, Dom, Pause, Regen, Entfernung) und dürfen darum keinen festen Vorsatz tragen. Ein fester Bus-Pegel würde zwei Stellen zu einer machen und die Umgebungsrechnung (Deckel bei −30 LUFS) verschieben.

| Bus | Pegel | Dämpfung, die im Betrieb dazukommt |
|---|---|---|
| Master | 0 dB, Regler 80 % = −1,9 dB | – |
| Musik | 0 dB | Ducking −9 dB bei Ansagen (am Spieler, Einblenden 0,35 s, Zurück 1,5 s) |
| Umgebung | 0 dB | – (Ducking bei Ansagen gibt es nicht; Empfehlung: bleibt so, die Ansagen liegen schon mehr als 12 dB über der Umgebung) |
| Effekte | 0 dB | – |
| Oberflaeche | 0 dB | – |
| Stimmen | 0 dB | – |
| UmgebungDraussen / -Stadt / -RufNah / -RufFern / -Schenke / -Gespraech / -Glocke / -Innen / -Donner | 0 dB | Pegel laufend durch `KlangUmgebungModell`; Donner mit Begrenzer |

## Was Schritt 7 geändert hat

- Gemessen wurden alle 302 Audiodateien; **keine Datei wurde verändert**.
- Musik (48 Stücke) liegt bei −17,0 LUFS ±0,1 (Herbstrauschen −18,4 wegen der Spitze), Ansagen (120 Sätze) bei −18,0 LUFS ±0,1 (eine −18,8): schon einheitlich, kein Eingriff.
- Die Umgebung liegt je Gruppe bei dem Wert, den ihre Pegeltabelle im Code erwartet (Schichten −26, Rufe −24 bis −14, Glocken −17); die Rufe unterscheiden sich absichtlich.
- **Oberflächenklänge**: Die Varianten eines Ereignisses waren bis zu 14 dB verschieden laut (Klick: −23,5 bis −36,4 M max). Jetzt gleicht `game/daten/klang_lautheit.json` jede Variante auf das Ziel ihres Ereignisses an (Tabelle unten, „Angleichung“); die Pegel-Spalte der Tabelle in `KlangOberflaeche.cs` blieb unverändert.
- Spitzen nach der Angleichung und der Tabelle: höchstens −3,6 dBTP (rechnerisch, `docs/assets/lautheit-angleichung.csv`).

## Je Datei

| Datei | LUFS | M max | TP | Angleichung | M nachher |
|---|---|---|---|---|---|
| geraeusche/oberflaeche/belt_handle_1.ogg | – | −23.5 | −2.9 | −2.5 | −26.0 |
| geraeusche/oberflaeche/book_close.ogg | – | −18.6 | 0.9 | −4.4 | −23.0 |
| geraeusche/oberflaeche/book_flip_1.ogg | – | −20.9 | 0.8 | −3.1 | −24.0 |
| geraeusche/oberflaeche/book_flip_2.ogg | – | −30.8 | −8.1 | +6.8 | −24.0 |
| geraeusche/oberflaeche/book_flip_3.ogg | – | −28.4 | −4.6 | +4.4 | −24.0 |
| geraeusche/oberflaeche/book_open.ogg | – | −35.1 | −11.5 | +11.1 | −24.0 |
| geraeusche/oberflaeche/book_place_1.ogg | – | −16.5 | 0.8 | −0.5 | −17.0 |
| geraeusche/oberflaeche/book_place_2.ogg | – | −15.5 | 0.8 | −1.5 | −17.0 |
| geraeusche/oberflaeche/chop.ogg | – | −16.0 | 0.6 | −1.0 | −17.0 |
| geraeusche/oberflaeche/cloth_1.ogg | – | −22.0 | −3.5 | −2.0 | −24.0 |
| geraeusche/oberflaeche/cloth_2.ogg | – | −26.6 | −7.9 | +2.6 | −24.0 |
| geraeusche/oberflaeche/cloth_3.ogg | – | −27.8 | −10.1 | +4.8 | −23.0 |
| geraeusche/oberflaeche/cloth_4.ogg | – | −32.4 | −14.6 | +9.4 | −23.0 |
| geraeusche/oberflaeche/drop_leather.ogg | – | −18.2 | −0.1 | +1.2 | −17.0 |
| geraeusche/oberflaeche/handle_coins.ogg | – | −19.4 | 1.0 | +1.4 | −18.0 |
| geraeusche/oberflaeche/handle_coins_2.ogg | – | −28.5 | −10.5 | +10.5 | −18.0 |
| geraeusche/oberflaeche/handle_small_leather.ogg | – | −33.9 | −11.6 | +7.9 | −26.0 |
| geraeusche/oberflaeche/handle_small_leather_2.ogg | – | −36.4 | −16.4 | +10.4 | −26.0 |
| geraeusche/oberflaeche/impact_bell_heavy_002.ogg | – | −19.5 | −0.8 | −1.0 | −20.5 |
| geraeusche/oberflaeche/impact_bell_heavy_003.ogg | – | −20.4 | −1.0 | −0.1 | −20.5 |
| geraeusche/oberflaeche/impact_bell_heavy_004.ogg | – | −21.9 | −1.1 | +1.4 | −20.5 |
| geraeusche/oberflaeche/impact_plank_medium_000.ogg | – | −19.3 | −1.2 | +2.3 | −17.0 |
| geraeusche/oberflaeche/impact_plank_medium_001.ogg | – | −16.3 | −1.0 | −0.7 | −17.0 |
| geraeusche/oberflaeche/impact_plank_medium_002.ogg | – | −16.7 | −0.9 | −0.3 | −17.0 |
| geraeusche/oberflaeche/meldung_brief.ogg | −23.0 | −20.0 | −12.3 | – | −20.0 |
| geraeusche/oberflaeche/meldung_entscheidung.ogg | −22.7 | −20.0 | −12.7 | – | −20.0 |
| geraeusche/oberflaeche/meldung_fehler.ogg | – | −19.9 | −12.3 | – | −19.9 |
| geraeusche/oberflaeche/meldung_mahnung.ogg | – | −19.9 | −13.5 | – | −19.9 |
| geraeusche/oberflaeche/meldung_siegel.ogg | – | −20.0 | −12.0 | – | −20.0 |
| geraeusche/oberflaeche/metal_click.ogg | – | −28.0 | −6.9 | +5.0 | −23.0 |
| geraeusche/oberflaeche/metal_latch.ogg | – | −16.2 | 2.1 | −6.8 | −23.0 |
| geraeusche/oberflaeche/tuer_auf.ogg | −18.1 | −16.1 | −3.4 | – | −16.1 |
| geraeusche/oberflaeche/tuer_zu.ogg | −18.0 | −16.9 | −1.5 | – | −16.9 |
| musik/event/event_aufstand_orks_und_oger.ogg | −17.0 | −12.4 | −6.8 | – | −12.4 |
| musik/event/event_dieb_einbruch.ogg | −17.1 | −13.2 | −4.7 | – | −13.2 |
| musik/event/event_geburt_geburt.ogg | −17.0 | −14.3 | −6.8 | – | −14.3 |
| musik/event/event_gefahr_nahe_bedrohung.ogg | −17.1 | −11.5 | −3.8 | – | −11.5 |
| musik/event/event_hochzeit_hochzeit.ogg | −16.9 | −12.6 | −4.8 | – | −12.6 |
| musik/event/event_kampf_jetzt_aber_schnell.ogg | −17.0 | −12.5 | −5.1 | – | −12.5 |
| musik/event/event_levelup_glueckwunsch.ogg | −17.0 | −14.2 | −5.3 | – | −14.2 |
| musik/event/event_levelup_glueckwunsch_v2.ogg | −17.0 | −13.5 | −5.4 | – | −13.5 |
| musik/event/event_sklaven_schwere_ketten.ogg | −17.0 | −13.4 | −6.1 | – | −13.4 |
| musik/event/event_sklaven_sklaven_an_die_arbeit.ogg | −17.0 | −13.6 | −6.3 | – | −13.6 |
| musik/glaube/glaube_ahtnisch.ogg | −17.0 | −12.8 | −6.0 | – | −12.8 |
| musik/glaube/glaube_ahtnisch_v2.ogg | −17.0 | −12.7 | −6.0 | – | −12.7 |
| musik/glaube/glaube_evangelisch.ogg | −17.0 | −13.6 | −6.3 | – | −13.6 |
| musik/glaube/glaube_katholisch.ogg | −17.0 | −12.9 | −5.3 | – | −12.9 |
| musik/menue/menue_reichsgilde.ogg | −17.1 | −12.7 | −3.7 | – | −12.7 |
| musik/nacht/nacht_augen_im_dunkeln.ogg | −17.0 | −12.5 | −4.7 | – | −12.5 |
| musik/nacht/nacht_es_lauert.ogg | −17.0 | −11.2 | −2.7 | – | −11.2 |
| musik/nacht/nacht_herumtreiber.ogg | −17.1 | −13.1 | −5.1 | – | −13.1 |
| musik/nacht/nacht_unheimlich.ogg | −17.0 | −11.7 | −4.5 | – | −11.7 |
| musik/tag/tag_adels_schmeich.ogg | −16.9 | −11.7 | −5.0 | – | −11.7 |
| musik/tag/tag_arbeitruf.ogg | −17.0 | −13.1 | −5.6 | – | −13.1 |
| musik/tag/tag_beruf_dienstleistung.ogg | −17.0 | −12.1 | −3.9 | – | −12.1 |
| musik/tag/tag_beruf_handwerk.ogg | −17.0 | −12.9 | −4.3 | – | −12.9 |
| musik/tag/tag_elb_elben.ogg | −16.9 | −11.3 | −3.0 | – | −11.3 |
| musik/tag/tag_elb_elbengefuehle.ogg | −17.0 | −12.4 | −5.0 | – | −12.4 |
| musik/tag/tag_fruehling_fruehlingsauftakt.ogg | −16.9 | −13.4 | −3.7 | – | −13.4 |
| musik/tag/tag_fruehling_fruehlingstanz.ogg | −16.9 | −12.6 | −4.5 | – | −12.6 |
| musik/tag/tag_fruehling_neuanfang.ogg | −17.0 | −11.4 | −5.2 | – | −11.4 |
| musik/tag/tag_halbling_halblinge.ogg | −17.0 | −14.0 | −4.8 | – | −14.0 |
| musik/tag/tag_halbling_halblingsruh.ogg | −17.0 | −12.4 | −5.1 | – | −12.4 |
| musik/tag/tag_herbst_erster_laub.ogg | −17.0 | −11.4 | −4.7 | – | −11.4 |
| musik/tag/tag_herbst_herbstrauschen.ogg | −18.4 | −12.5 | −1.3 | – | −12.5 |
| musik/tag/tag_hungerlohn.ogg | −16.9 | −12.4 | −5.4 | – | −12.4 |
| musik/tag/tag_marktplatz.ogg | −16.9 | −13.2 | −4.0 | – | −13.2 |
| musik/tag/tag_marsch_der_ritter.ogg | −17.0 | −11.2 | −3.9 | – | −11.2 |
| musik/tag/tag_mensch_menschen.ogg | −17.0 | −13.2 | −5.3 | – | −13.2 |
| musik/tag/tag_mensch_menschenstolz.ogg | −17.0 | −12.7 | −5.1 | – | −12.7 |
| musik/tag/tag_sommer_sonnengang.ogg | −16.9 | −12.9 | −3.9 | – | −12.9 |
| musik/tag/tag_sommer_warme_geduld.ogg | −17.0 | −10.5 | −4.7 | – | −10.5 |
| musik/tag/tag_winter_frost.ogg | −17.0 | −12.6 | −5.6 | – | −12.6 |
| musik/tag/tag_winter_kaltes_glockenspiel.ogg | −16.9 | −9.0 | −2.1 | – | −9.0 |
| musik/tag/tag_winter_letzter_winter.ogg | −17.0 | −11.3 | −3.3 | – | −11.3 |
| musik/tag/tag_zwerg_zwerge.ogg | −17.0 | −12.2 | −4.6 | – | −12.2 |
| musik/tag/tag_zwerg_zwergentreiben.ogg | −17.1 | −13.4 | −5.0 | – | −13.4 |
| musik/tod/tod_abschied.ogg | −16.9 | −11.6 | −4.8 | – | −11.6 |
| musik/tod/tod_der_letzte_wunsch.ogg | −16.9 | −11.6 | −5.0 | – | −11.6 |
| musik/tod/tod_der_tod.ogg | −17.0 | −12.9 | −5.6 | – | −12.9 |
| musik/tod/tod_verlorene_seele.ogg | −16.9 | −11.7 | −3.8 | – | −11.7 |
| stimmen/haushofmeister/anklage_1.ogg | −17.9 | −15.9 | −1.9 | – | −15.9 |
| stimmen/haushofmeister/anklage_1_herrin.ogg | −18.0 | −14.1 | −1.3 | – | −14.1 |
| stimmen/haushofmeister/anklage_2.ogg | −18.7 | −14.0 | −3.2 | – | −14.0 |
| stimmen/haushofmeister/anklage_2_herrin.ogg | −18.1 | −14.1 | −1.0 | – | −14.1 |
| stimmen/haushofmeister/anklage_3.ogg | −18.0 | −15.3 | −1.8 | – | −15.3 |
| stimmen/haushofmeister/anklage_3_herrin.ogg | −18.0 | −15.0 | −1.7 | – | −15.0 |
| stimmen/haushofmeister/diebstahl_1.ogg | −18.1 | −15.1 | −2.4 | – | −15.1 |
| stimmen/haushofmeister/diebstahl_1_herrin.ogg | −18.0 | −15.1 | −1.2 | – | −15.1 |
| stimmen/haushofmeister/diebstahl_2.ogg | −18.0 | −13.8 | −2.1 | – | −13.8 |
| stimmen/haushofmeister/diebstahl_2_herrin.ogg | −17.9 | −14.3 | −1.4 | – | −14.3 |
| stimmen/haushofmeister/diebstahl_3.ogg | −18.0 | −14.6 | −2.2 | – | −14.6 |
| stimmen/haushofmeister/diebstahl_3_herrin.ogg | −18.0 | −15.6 | −1.5 | – | −15.6 |
| stimmen/haushofmeister/feuer_1.ogg | −18.0 | −13.0 | −2.2 | – | −13.0 |
| stimmen/haushofmeister/feuer_1_herrin.ogg | −18.0 | −12.9 | −1.2 | – | −12.9 |
| stimmen/haushofmeister/feuer_2.ogg | −18.1 | −13.0 | −2.2 | – | −13.0 |
| stimmen/haushofmeister/feuer_2_herrin.ogg | −17.9 | −15.0 | −1.4 | – | −15.0 |
| stimmen/haushofmeister/feuer_3.ogg | −17.9 | −14.9 | −2.4 | – | −14.9 |
| stimmen/haushofmeister/feuer_3_herrin.ogg | −18.6 | −14.5 | −3.2 | – | −14.5 |
| stimmen/haushofmeister/geburt_1.ogg | −18.0 | −13.7 | −2.5 | – | −13.7 |
| stimmen/haushofmeister/geburt_1_herrin.ogg | −18.0 | −13.5 | −2.6 | – | −13.5 |
| stimmen/haushofmeister/geburt_2.ogg | −17.9 | −14.6 | −1.4 | – | −14.6 |
| stimmen/haushofmeister/geburt_2_herrin.ogg | −18.0 | −14.1 | −1.2 | – | −14.1 |
| stimmen/haushofmeister/geburt_3.ogg | −18.0 | −14.1 | −2.9 | – | −14.1 |
| stimmen/haushofmeister/geburt_3_herrin.ogg | −17.9 | −13.0 | −1.5 | – | −13.0 |
| stimmen/haushofmeister/heirat_1.ogg | −18.0 | −14.0 | −2.1 | – | −14.0 |
| stimmen/haushofmeister/heirat_1_herrin.ogg | −18.0 | −15.1 | −2.3 | – | −15.1 |
| stimmen/haushofmeister/heirat_2.ogg | −18.0 | −15.8 | −1.3 | – | −15.8 |
| stimmen/haushofmeister/heirat_2_herrin.ogg | −18.0 | −14.2 | −1.3 | – | −14.2 |
| stimmen/haushofmeister/heirat_3.ogg | −18.0 | −13.8 | −1.2 | – | −13.8 |
| stimmen/haushofmeister/heirat_3_herrin.ogg | −18.0 | −14.9 | −0.9 | – | −14.9 |
| stimmen/haushofmeister/jahresende_1.ogg | −18.0 | −14.4 | −1.4 | – | −14.4 |
| stimmen/haushofmeister/jahresende_1_herrin.ogg | −17.9 | −14.4 | −1.9 | – | −14.4 |
| stimmen/haushofmeister/jahresende_2.ogg | −17.9 | −15.5 | −2.5 | – | −15.5 |
| stimmen/haushofmeister/jahresende_2_herrin.ogg | −18.0 | −14.0 | −1.6 | – | −14.0 |
| stimmen/haushofmeister/jahresende_3.ogg | −18.0 | −14.0 | −1.9 | – | −14.0 |
| stimmen/haushofmeister/jahresende_3_herrin.ogg | −17.9 | −12.6 | −1.9 | – | −12.6 |
| stimmen/haushofmeister/kasse_knapp_1.ogg | −18.0 | −14.0 | −0.7 | – | −14.0 |
| stimmen/haushofmeister/kasse_knapp_1_herrin.ogg | −18.7 | −15.5 | −2.8 | – | −15.5 |
| stimmen/haushofmeister/kasse_knapp_2.ogg | −18.0 | −14.9 | −1.5 | – | −14.9 |
| stimmen/haushofmeister/kasse_knapp_2_herrin.ogg | −17.9 | −15.0 | −1.0 | – | −15.0 |
| stimmen/haushofmeister/kasse_knapp_3.ogg | −18.7 | −15.0 | −2.9 | – | −15.0 |
| stimmen/haushofmeister/kasse_knapp_3_herrin.ogg | −18.0 | −15.2 | −1.9 | – | −15.2 |
| stimmen/haushofmeister/krankheit_1.ogg | −18.6 | −13.6 | −2.7 | – | −13.6 |
| stimmen/haushofmeister/krankheit_1_herrin.ogg | −18.0 | −13.4 | −0.7 | – | −13.4 |
| stimmen/haushofmeister/krankheit_2.ogg | −18.0 | −15.3 | −1.9 | – | −15.3 |
| stimmen/haushofmeister/krankheit_2_herrin.ogg | −17.9 | −15.2 | −2.2 | – | −15.2 |
| stimmen/haushofmeister/krankheit_3.ogg | −18.0 | −14.7 | −0.7 | – | −14.7 |
| stimmen/haushofmeister/krankheit_3_herrin.ogg | −18.6 | −14.8 | −2.9 | – | −14.8 |
| stimmen/haushofmeister/lager_leer_1.ogg | −18.0 | −13.0 | −1.1 | – | −13.0 |
| stimmen/haushofmeister/lager_leer_1_herrin.ogg | −17.9 | −15.3 | −1.8 | – | −15.3 |
| stimmen/haushofmeister/lager_leer_2.ogg | −18.7 | −14.8 | −3.3 | – | −14.8 |
| stimmen/haushofmeister/lager_leer_2_herrin.ogg | −18.0 | −14.5 | −1.2 | – | −14.5 |
| stimmen/haushofmeister/lager_leer_3.ogg | −17.9 | −13.4 | −2.2 | – | −13.4 |
| stimmen/haushofmeister/lager_leer_3_herrin.ogg | −18.0 | −12.3 | −1.7 | – | −12.3 |
| stimmen/haushofmeister/lager_voll_1.ogg | −18.0 | −14.9 | −1.3 | – | −14.9 |
| stimmen/haushofmeister/lager_voll_1_herrin.ogg | −18.0 | −13.6 | −1.0 | – | −13.6 |
| stimmen/haushofmeister/lager_voll_2.ogg | −18.0 | −14.4 | −1.3 | – | −14.4 |
| stimmen/haushofmeister/lager_voll_2_herrin.ogg | −18.0 | −14.7 | −2.1 | – | −14.7 |
| stimmen/haushofmeister/lager_voll_3.ogg | −18.0 | −14.9 | −1.8 | – | −14.9 |
| stimmen/haushofmeister/lager_voll_3_herrin.ogg | −18.0 | −14.8 | −2.0 | – | −14.8 |
| stimmen/haushofmeister/meisterpruefung_1.ogg | −17.9 | −14.6 | −1.0 | – | −14.6 |
| stimmen/haushofmeister/meisterpruefung_1_herrin.ogg | −17.9 | −13.9 | −1.4 | – | −13.9 |
| stimmen/haushofmeister/meisterpruefung_2.ogg | −18.0 | −14.1 | −1.4 | – | −14.1 |
| stimmen/haushofmeister/meisterpruefung_2_herrin.ogg | −18.0 | −14.2 | −2.4 | – | −14.2 |
| stimmen/haushofmeister/meisterpruefung_3.ogg | −18.0 | −15.5 | −2.1 | – | −15.5 |
| stimmen/haushofmeister/meisterpruefung_3_herrin.ogg | −18.0 | −13.4 | −1.4 | – | −13.4 |
| stimmen/haushofmeister/pest_stadt_1.ogg | −18.1 | −14.4 | −1.2 | – | −14.4 |
| stimmen/haushofmeister/pest_stadt_1_herrin.ogg | −18.5 | −15.1 | −3.1 | – | −15.1 |
| stimmen/haushofmeister/pest_stadt_2.ogg | −17.9 | −14.6 | −1.4 | – | −14.6 |
| stimmen/haushofmeister/pest_stadt_2_herrin.ogg | −18.6 | −14.1 | −3.0 | – | −14.1 |
| stimmen/haushofmeister/pest_stadt_3.ogg | −17.9 | −15.6 | −1.1 | – | −15.6 |
| stimmen/haushofmeister/pest_stadt_3_herrin.ogg | −18.0 | −15.1 | −1.7 | – | −15.1 |
| stimmen/haushofmeister/ratssitzung_1.ogg | −18.6 | −14.8 | −2.7 | – | −14.8 |
| stimmen/haushofmeister/ratssitzung_1_herrin.ogg | −18.6 | −14.6 | −3.2 | – | −14.6 |
| stimmen/haushofmeister/ratssitzung_2.ogg | −18.0 | −14.6 | −1.3 | – | −14.6 |
| stimmen/haushofmeister/ratssitzung_2_herrin.ogg | −18.6 | −13.9 | −2.8 | – | −13.9 |
| stimmen/haushofmeister/ratssitzung_3.ogg | −18.0 | −15.0 | −1.8 | – | −15.0 |
| stimmen/haushofmeister/ratssitzung_3_herrin.ogg | −17.9 | −15.0 | −1.4 | – | −15.0 |
| stimmen/haushofmeister/schulden_1.ogg | −18.0 | −14.3 | −1.0 | – | −14.3 |
| stimmen/haushofmeister/schulden_1_herrin.ogg | −18.0 | −14.3 | −1.2 | – | −14.3 |
| stimmen/haushofmeister/schulden_2.ogg | −18.6 | −13.9 | −3.3 | – | −13.9 |
| stimmen/haushofmeister/schulden_2_herrin.ogg | −18.0 | −14.9 | −1.4 | – | −14.9 |
| stimmen/haushofmeister/schulden_3.ogg | −18.0 | −14.5 | −1.7 | – | −14.5 |
| stimmen/haushofmeister/schulden_3_herrin.ogg | −18.0 | −15.0 | −1.7 | – | −15.0 |
| stimmen/haushofmeister/stand_aufstieg_1.ogg | −18.0 | −13.5 | −1.4 | – | −13.5 |
| stimmen/haushofmeister/stand_aufstieg_1_herrin.ogg | −17.9 | −14.2 | −1.4 | – | −14.2 |
| stimmen/haushofmeister/stand_aufstieg_2.ogg | −18.0 | −13.6 | −1.7 | – | −13.6 |
| stimmen/haushofmeister/stand_aufstieg_2_herrin.ogg | −17.9 | −14.3 | −1.8 | – | −14.3 |
| stimmen/haushofmeister/stand_aufstieg_3.ogg | −17.9 | −13.9 | −2.5 | – | −13.9 |
| stimmen/haushofmeister/stand_aufstieg_3_herrin.ogg | −18.0 | −15.6 | −2.4 | – | −15.6 |
| stimmen/haushofmeister/tod_familie_1.ogg | −17.9 | −15.1 | −1.4 | – | −15.1 |
| stimmen/haushofmeister/tod_familie_1_herrin.ogg | −18.0 | −15.6 | −1.0 | – | −15.6 |
| stimmen/haushofmeister/tod_familie_2.ogg | −18.5 | −13.7 | −2.9 | – | −13.7 |
| stimmen/haushofmeister/tod_familie_2_herrin.ogg | −18.6 | −14.5 | −3.3 | – | −14.5 |
| stimmen/haushofmeister/tod_familie_3.ogg | −17.9 | −15.9 | −2.0 | – | −15.9 |
| stimmen/haushofmeister/tod_familie_3_herrin.ogg | −18.0 | −15.2 | −1.8 | – | −15.2 |
| stimmen/haushofmeister/tod_oberhaupt_1.ogg | −18.0 | −13.2 | −1.5 | – | −13.2 |
| stimmen/haushofmeister/tod_oberhaupt_1_herrin.ogg | −18.0 | −14.0 | −1.1 | – | −14.0 |
| stimmen/haushofmeister/tod_oberhaupt_2.ogg | −18.0 | −14.5 | −1.7 | – | −14.5 |
| stimmen/haushofmeister/tod_oberhaupt_2_herrin.ogg | −18.6 | −15.7 | −3.0 | – | −15.7 |
| stimmen/haushofmeister/tod_oberhaupt_3.ogg | −18.0 | −14.8 | −2.6 | – | −14.8 |
| stimmen/haushofmeister/tod_oberhaupt_3_herrin.ogg | −18.6 | −14.8 | −3.2 | – | −14.8 |
| stimmen/haushofmeister/ueberfall_1.ogg | −18.0 | −12.8 | −0.9 | – | −12.8 |
| stimmen/haushofmeister/ueberfall_1_herrin.ogg | −17.9 | −13.7 | −1.1 | – | −13.7 |
| stimmen/haushofmeister/ueberfall_2.ogg | −17.9 | −14.0 | −2.2 | – | −14.0 |
| stimmen/haushofmeister/ueberfall_2_herrin.ogg | −18.0 | −14.7 | −2.2 | – | −14.7 |
| stimmen/haushofmeister/ueberfall_3.ogg | −18.0 | −14.6 | −1.8 | – | −14.6 |
| stimmen/haushofmeister/ueberfall_3_herrin.ogg | −18.0 | −14.9 | −2.1 | – | −14.9 |
| stimmen/haushofmeister/wahl_gewonnen_1.ogg | −18.8 | −14.6 | −2.8 | – | −14.6 |
| stimmen/haushofmeister/wahl_gewonnen_1_herrin.ogg | −17.9 | −14.7 | −1.4 | – | −14.7 |
| stimmen/haushofmeister/wahl_gewonnen_2.ogg | −18.0 | −15.7 | −1.0 | – | −15.7 |
| stimmen/haushofmeister/wahl_gewonnen_2_herrin.ogg | −18.0 | −15.3 | −2.3 | – | −15.3 |
| stimmen/haushofmeister/wahl_gewonnen_3.ogg | −18.0 | −14.6 | −1.6 | – | −14.6 |
| stimmen/haushofmeister/wahl_gewonnen_3_herrin.ogg | −18.6 | −15.3 | −3.2 | – | −15.3 |
| stimmen/haushofmeister/wahl_verloren_1.ogg | −18.0 | −15.2 | −1.1 | – | −15.2 |
| stimmen/haushofmeister/wahl_verloren_1_herrin.ogg | −18.6 | −14.8 | −3.1 | – | −14.8 |
| stimmen/haushofmeister/wahl_verloren_2.ogg | −18.0 | −14.8 | −1.1 | – | −14.8 |
| stimmen/haushofmeister/wahl_verloren_2_herrin.ogg | −18.0 | −14.8 | −1.5 | – | −14.8 |
| stimmen/haushofmeister/wahl_verloren_3.ogg | −18.0 | −15.0 | −1.1 | – | −15.0 |
| stimmen/haushofmeister/wahl_verloren_3_herrin.ogg | −18.0 | −15.1 | −2.1 | – | −15.1 |
| umgebung/glocken/glocke_abend_1.ogg | −17.0 | −13.0 | −2.4 | – | −13.0 |
| umgebung/glocken/glocke_abend_2.ogg | −17.0 | −14.7 | −2.8 | – | −14.7 |
| umgebung/glocken/glocke_feuer_1.ogg | −16.9 | −13.0 | −2.2 | – | −13.0 |
| umgebung/glocken/glocke_feuer_2.ogg | −16.9 | −13.3 | −2.9 | – | −13.3 |
| umgebung/glocken/glocke_messe_1.ogg | −17.0 | −12.9 | −2.8 | – | −12.9 |
| umgebung/glocken/glocke_messe_2.ogg | −17.0 | −13.6 | −2.8 | – | −13.6 |
| umgebung/glocken/glocke_morgen_1.ogg | −17.0 | −13.7 | −3.0 | – | −13.7 |
| umgebung/glocken/glocke_morgen_2.ogg | −17.0 | −13.7 | −3.0 | – | −13.7 |
| umgebung/glocken/glocke_sturm.ogg | −19.8 | −16.6 | −6.4 | – | −16.6 |
| umgebung/karren/hufe_erde.ogg | −26.1 | −20.7 | −11.3 | – | −20.7 |
| umgebung/karren/hufe_pflaster.ogg | −26.2 | −22.3 | −7.4 | – | −22.3 |
| umgebung/rufe/amsel_1.ogg | −20.1 | −14.8 | −6.4 | – | −14.8 |
| umgebung/rufe/amsel_2.ogg | −20.1 | −16.7 | −4.8 | – | −16.7 |
| umgebung/rufe/amsel_3.ogg | −20.2 | −16.3 | −7.0 | – | −16.3 |
| umgebung/rufe/amsel_4.ogg | −20.1 | −17.6 | −8.5 | – | −17.6 |
| umgebung/rufe/amsel_5.ogg | −20.2 | −17.1 | −6.0 | – | −17.1 |
| umgebung/rufe/donner_1.ogg | −17.0 | −8.6 | −1.3 | – | −8.6 |
| umgebung/rufe/donner_2.ogg | −17.0 | −9.1 | −1.9 | – | −9.1 |
| umgebung/rufe/donner_3.ogg | −17.0 | −10.2 | −2.0 | – | −10.2 |
| umgebung/rufe/donner_4.ogg | −17.0 | −9.6 | −2.0 | – | −9.6 |
| umgebung/rufe/donner_fern_1.ogg | −18.0 | −9.9 | −0.3 | – | −9.9 |
| umgebung/rufe/donner_fern_2.ogg | −18.0 | −10.8 | −0.3 | – | −10.8 |
| umgebung/rufe/donner_fern_3.ogg | −18.0 | −9.1 | 0.4 | – | −9.1 |
| umgebung/rufe/donner_fern_4.ogg | −18.0 | −11.2 | −0.0 | – | −11.2 |
| umgebung/rufe/einschlag_1.ogg | −14.1 | −5.8 | 1.2 | – | −5.8 |
| umgebung/rufe/einschlag_2.ogg | −14.1 | −5.4 | 1.0 | – | −5.4 |
| umgebung/rufe/gefluegel_1.ogg | −20.2 | −18.4 | −6.3 | – | −18.4 |
| umgebung/rufe/gefluegel_2.ogg | −20.1 | −16.3 | −6.8 | – | −16.3 |
| umgebung/rufe/gefluegel_3.ogg | −20.0 | −16.4 | −7.0 | – | −16.4 |
| umgebung/rufe/gefluegel_4.ogg | −20.0 | −17.3 | −6.9 | – | −17.3 |
| umgebung/rufe/gespraeche/gasse/gasse_1.ogg | −20.0 | −14.5 | −2.6 | – | −14.5 |
| umgebung/rufe/gespraeche/gasse/gasse_2.ogg | −20.0 | −15.8 | −1.7 | – | −15.8 |
| umgebung/rufe/gespraeche/gasse/gasse_3.ogg | −21.7 | −17.0 | −1.0 | – | −17.0 |
| umgebung/rufe/gespraeche/gasse/gasse_4.ogg | −20.0 | −15.5 | −1.9 | – | −15.5 |
| umgebung/rufe/gespraeche/gasse/gasse_5.ogg | −20.2 | −16.2 | −2.0 | – | −16.2 |
| umgebung/rufe/gespraeche/gasse/gasse_6.ogg | −21.8 | −16.1 | −1.2 | – | −16.1 |
| umgebung/rufe/gespraeche/gasse/gasse_7.ogg | −20.0 | −15.4 | −3.4 | – | −15.4 |
| umgebung/rufe/gespraeche/gasse/gasse_8.ogg | −21.1 | −15.6 | −1.1 | – | −15.6 |
| umgebung/rufe/gespraeche/markt/markt_1.ogg | −20.0 | −16.2 | −2.3 | – | −16.2 |
| umgebung/rufe/gespraeche/markt/markt_2.ogg | −20.0 | −14.9 | −1.7 | – | −14.9 |
| umgebung/rufe/gespraeche/markt/markt_3.ogg | −20.0 | −14.7 | −2.5 | – | −14.7 |
| umgebung/rufe/gespraeche/markt/markt_4.ogg | −20.1 | −16.3 | −4.0 | – | −16.3 |
| umgebung/rufe/gespraeche/markt/markt_5.ogg | −20.1 | −16.0 | −3.7 | – | −16.0 |
| umgebung/rufe/gespraeche/markt/markt_6.ogg | −20.1 | −17.0 | −3.9 | – | −17.0 |
| umgebung/rufe/hahn_1.ogg | −20.1 | −17.0 | −9.9 | – | −17.0 |
| umgebung/rufe/hahn_2.ogg | −20.0 | −17.2 | −11.6 | – | −17.2 |
| umgebung/rufe/hahn_3.ogg | −20.2 | −17.8 | −11.3 | – | −17.8 |
| umgebung/rufe/handwerk_1.ogg | −26.4 | −23.1 | −1.6 | – | −23.1 |
| umgebung/rufe/handwerk_2.ogg | −24.8 | −22.9 | −1.3 | – | −22.9 |
| umgebung/rufe/handwerk_3.ogg | −25.6 | −22.2 | −0.7 | – | −22.2 |
| umgebung/rufe/handwerk_4.ogg | −20.1 | −18.7 | −3.3 | – | −18.7 |
| umgebung/rufe/horn_1.ogg | −19.9 | −18.4 | −10.7 | – | −18.4 |
| umgebung/rufe/horn_2.ogg | −19.9 | −18.4 | −9.9 | – | −18.4 |
| umgebung/rufe/hund_1.ogg | −19.9 | −18.7 | −5.8 | – | −18.7 |
| umgebung/rufe/hund_2.ogg | −20.0 | −18.4 | −2.7 | – | −18.4 |
| umgebung/rufe/hund_3.ogg | – | −19.8 | −6.0 | – | −19.8 |
| umgebung/rufe/hund_4.ogg | – | −20.0 | −5.0 | – | −20.0 |
| umgebung/rufe/hund_5.ogg | −20.0 | −18.5 | −4.3 | – | −18.5 |
| umgebung/rufe/hund_fern_1.ogg | −19.9 | −16.7 | −5.1 | – | −16.7 |
| umgebung/rufe/hund_fern_2.ogg | −20.0 | −17.0 | −3.3 | – | −17.0 |
| umgebung/rufe/hund_fern_3.ogg | −20.0 | −17.1 | −3.1 | – | −17.1 |
| umgebung/rufe/hund_fern_4.ogg | −20.1 | −17.2 | −2.4 | – | −17.2 |
| umgebung/rufe/kauz_1.ogg | −19.9 | −16.3 | −8.1 | – | −16.3 |
| umgebung/rufe/kauz_2.ogg | −19.9 | −17.2 | −9.4 | – | −17.2 |
| umgebung/rufe/kauz_3.ogg | −19.9 | −16.0 | −8.4 | – | −16.0 |
| umgebung/rufe/kauz_4.ogg | −19.9 | −17.1 | −7.5 | – | −17.1 |
| umgebung/rufe/kauz_5.ogg | −19.9 | −17.0 | −7.6 | – | −17.0 |
| umgebung/rufe/kraehe_1.ogg | −20.0 | −16.6 | −8.2 | – | −16.6 |
| umgebung/rufe/kraehe_2.ogg | −20.4 | −18.1 | −6.8 | – | −18.1 |
| umgebung/rufe/kraehe_3.ogg | −20.3 | −18.1 | −8.2 | – | −18.1 |
| umgebung/rufe/kraehe_4.ogg | −20.3 | −17.4 | −5.6 | – | −17.4 |
| umgebung/rufe/kraehe_5.ogg | −20.4 | −17.8 | −6.8 | – | −17.8 |
| umgebung/rufe/passant_1.ogg | −22.4 | −19.9 | −1.4 | – | −19.9 |
| umgebung/rufe/passant_2.ogg | −20.2 | −18.2 | −1.4 | – | −18.2 |
| umgebung/rufe/passant_3.ogg | −20.2 | −17.4 | −1.5 | – | −17.4 |
| umgebung/rufe/passant_4.ogg | −22.9 | −20.2 | −1.9 | – | −20.2 |
| umgebung/rufe/passant_5.ogg | −20.7 | −17.6 | −1.1 | – | −17.6 |
| umgebung/rufe/pferd_1.ogg | −24.3 | −22.5 | −8.8 | – | −22.5 |
| umgebung/rufe/pferd_2.ogg | −24.3 | −22.7 | −9.1 | – | −22.7 |
| umgebung/rufe/pferd_3.ogg | −24.4 | −22.4 | −11.0 | – | −22.4 |
| umgebung/rufe/pferd_4.ogg | −24.2 | −22.4 | −8.4 | – | −22.4 |
| umgebung/rufe/pferd_5.ogg | −24.2 | −21.9 | −8.3 | – | −21.9 |
| umgebung/rufe/pferd_6.ogg | −26.1 | −23.7 | −15.3 | – | −23.7 |
| umgebung/rufe/wolf_nacht.ogg | −25.9 | −23.7 | −15.8 | – | −23.7 |
| umgebung/schichten/abend_voegel.ogg | −26.2 | −17.3 | −9.8 | – | −17.3 |
| umgebung/schichten/morgen_voegel.ogg | −26.2 | −17.9 | −9.9 | – | −17.9 |
| umgebung/schichten/nacht.ogg | −26.1 | −24.6 | −13.5 | – | −24.6 |
| umgebung/schichten/natur_fluss.ogg | −26.4 | −17.5 | −9.6 | – | −17.5 |
| umgebung/schichten/natur_froesche.ogg | −26.1 | −14.3 | −6.4 | – | −14.3 |
| umgebung/schichten/natur_grillen.ogg | −26.3 | −23.5 | −15.4 | – | −23.5 |
| umgebung/schichten/natur_laub.ogg | −26.2 | −23.4 | −13.1 | – | −23.4 |
| umgebung/schichten/regen_dach.ogg | −26.3 | −19.0 | −5.0 | – | −19.0 |
| umgebung/schichten/regen_leicht.ogg | −26.3 | −24.6 | −6.1 | – | −24.6 |
| umgebung/schichten/regen_stark.ogg | −26.3 | −24.5 | −3.1 | – | −24.5 |
| umgebung/schichten/regen_traufe.ogg | −28.1 | −25.7 | −4.1 | – | −25.7 |
| umgebung/schichten/schenke_innen.ogg | −26.1 | −17.5 | −8.7 | – | −17.5 |
| umgebung/schichten/stadt_abend.ogg | −26.1 | −20.8 | −11.0 | – | −20.8 |
| umgebung/schichten/stadt_tag.ogg | −26.2 | −20.7 | −8.3 | – | −20.7 |
| umgebung/schichten/tag_spatzen.ogg | −26.4 | −18.2 | −7.2 | – | −18.2 |
| umgebung/schichten/wind_leicht.ogg | −26.2 | −21.6 | −11.2 | – | −21.6 |
| umgebung/schichten/wind_mittel.ogg | −26.2 | −21.0 | −11.2 | – | −21.0 |
