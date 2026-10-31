# Herkunft der Umgebungsklänge (Ton Schritt 4)

Alle Fremdaufnahmen stammen von **freesound.org**. Die Lizenz steht auf jeder Klangseite und ist am 30./31.10.2026 geprüft; sie steht in der Tabelle:

- **CC0 1.0** (Public Domain Dedication, https://creativecommons.org/publicdomain/zero/1.0/): keine Namensnennung nötig; wir nennen die Urheber trotzdem.
- **CC BY 4.0** (https://creativecommons.org/licenses/by/4.0/): **Namensnennung verlangt, Bearbeitung erlaubt** (Schnitte, Filter, Pegel). Diese Quellen sind: klankbeeld (große Glocke Noteman, Niederlande: Abendglocke und Feuerglocke), poots (Frösche), newlocknew (leichter Regen), LolitaPerdurabo (Regen auf dem Dach), Erbsland-Music (starker Regen), juskiddink (Horn der Streife). Die Nennung steht in dieser Datei, im Protokoll (`docs/assets/protokoll.csv`, Spalte Bemerkung) und muss in einer späteren Liste der Mitwirkenden im Spiel (Danksagung) wiederkehren.
- Nichts aus dem Spiel „Die Gilde“, keine craigsmith-Aufnahmen (unklare Rechtekette), nichts mit NC-Lizenz, kein Sonniss.

- **Quelle ist die öffentliche HQ-Vorschau** (OGG Vorbis, etwa 192 kbit/s) der Klangseite, nicht das Original (das gibt es nur mit Anmeldung). Die SHA-256 der Vorschau steht unten und in `tools/ton/umgebung_schnitte.json`; mit derselben Nummer lässt sich später das Original einsetzen und neu bauen.
- **Bearbeitung** für alle: `tools/ton/umgebung_bauen.py` – auf 48 kHz umgerechnet (soxr), gefiltert (Hochpass 2. Ordnung, Tiefpass 4. Ordnung, Butterworth), ausgeschnitten; Schichten mit Schleifennaht gleicher Leistung (Ende über den Anfang geblendet), Stereo, −26 LUFS; Rufe mono, Ränder ein- und ausgeblendet, −20 LUFS (Spitze höchstens −1 dBTP, darum sind Pferd, Passanten und Handwerk etwas leiser); OGG Vorbis Qualität 4. Jede Datei ist ein Ausschnitt, nicht die ganze Aufnahme. **Neu seit der Nachbesserung 31.10.2026:** die Gesprächsfetzen werden vorher entrauscht (stationäres Grundrauschen der Quelle um 12 dB abgesenkt, spektrale Subtraktion); die Glocken sind echte Geläute aus dem Gebiet des Heiligen Römischen Reiches (Kölner Dom, Kathedrale Fribourg, die große Glocke „Noteman“ eines niederländischen Doms), nur ausgeschnitten (die Abendglocke ist eine Mischung zweier Aufnahmen), mit weichem Begrenzer, −17 LUFS; die Einschlag-Aufnahmen (direkter Blitzeinschlag, 243614) sind brachial (−14 LUFS, Tiefbass unter 120 Hz angehoben, Begrenzer −9 dBFS), der normale Donner kräftiger, aber leiser (−17 LUFS, Spitze −2,5 dBTP); der starke und der leichte Regen laufen durch einen weichen Begrenzer, damit einzelne Tropfenspitzen die Lautheit nicht begrenzen.
- **Eigene Synthese:** `schichten/nacht.ogg` und `schichten/wind_leicht.ogg` (gefiltertes Rauschen mit Böenkurve, Funktion `wind` im Bauskript, feste Saat), keine Fremdquelle.
- **Gesprächsfetzen** (`rufe/gespraeche/`): kurze Ausschnitte arabischer Gespräche auf der Straße (Aufnahmen von sounds_from_palestine, CC0) – für deutsche Hörer unverständlich, Sprache am 31.10.2026 mit Whisper bestimmt (Arabisch, wo erkennbar); die Liste steht in `game/daten/gespraeche.json` und lässt sich später durch Aufnahmen der Stimm-KI ersetzen. Gehört wurden sie nicht, nur gemessen und im Spektrogramm angesehen.
- Das frühere **Gemurmel** (`schichten/stadt_gemurmel.ogg`, Aufnahme 360699, Halle von oben) ist entfernt: Es hatte Hintergrundrauschen. Die Gesprächsfetzen ersetzen es.

## Quellen

| Nummer | Titel | Urheber | Lizenz | Klangseite | Vorschau | SHA-256 der Vorschau |
|---|---|---|---|---|---|---|
| 116791 | Blackbird 252 | nigelcoop | CC0 1.0 | https://freesound.org/people/nigelcoop/sounds/116791/ | https://cdn.freesound.org/previews/116/116791_1088850-hq.ogg | `8eed9fb602ea6bdaebb35fbc396d3c30ce40735060cd9454fe24945ab6b5779e` |
| 135193 | Wind blowing in the bush on the top of Kitt Peak mountain | felix.blume | CC0 1.0 | https://freesound.org/people/felix.blume/sounds/135193/ | https://cdn.freesound.org/previews/135/135193_1661766-hq.ogg | `9baa931f58e0ebf8cf1613377739bf13c260c4f7927a1ecc35da95dd3b0c6a1a` |
| 137836 | Blacksmith Workshop.WAV | thefilmbakery | CC0 1.0 | https://freesound.org/people/thefilmbakery/sounds/137836/ | https://cdn.freesound.org/previews/137/137836_977564-hq.ogg | `59da72624c011df1895768e5ec63f6cf42c6ad31f2374443258b7368b6f4503f` |
| 149192 | geese honking | videog | CC0 1.0 | https://freesound.org/people/videog/sounds/149192/ | https://cdn.freesound.org/previews/149/149192_2688492-hq.ogg | `a8e9955aaa4ecbcb26c90496d46cc3daf0480285717907d3c18280c25355ccc6` |
| 166067 | Tuohitorvi.wav | juskiddink | CC BY 4.0 | https://freesound.org/people/juskiddink/sounds/166067/ | https://cdn.freesound.org/previews/166/166067_649468-hq.ogg | `36581867e3ed8a3ac65bf98ea6fc9567d498ae115a95fa5862cf9e7bf5cdc9fd` |
| 166421 | Some dogs barking a village in Anatolia (Turkey) | felix.blume | CC0 1.0 | https://freesound.org/people/felix.blume/sounds/166421/ | https://cdn.freesound.org/previews/166/166421_1661766-hq.ogg | `b9b0767239600f01797a422055b26f58f55b1fed179821d8fbcb635fa61d7f29` |
| 173159 | Paso Fino Horse | AlecCorday | CC0 1.0 | https://freesound.org/people/AlecCorday/sounds/173159/ | https://cdn.freesound.org/previews/173/173159_1591616-hq.ogg | `a63618b798b572686273a16164bb7d968e971fae85ee46029b25a367b227bbbd` |
| 184627 | Heavy Rain in the Night on Roof and Window | Erbsland-Music | CC BY 4.0 | https://freesound.org/people/Erbsland-Music/sounds/184627/ | https://cdn.freesound.org/previews/184/184627_522747-hq.ogg | `01fb068d9899bee8f7f38956c9185c8fca0d781d05c7c884aae36711f2464981` |
| 187199 | walla 01 | pushkin | CC0 1.0 | https://freesound.org/people/pushkin/sounds/187199/ | https://cdn.freesound.org/previews/187/187199_336074-hq.ogg | `543235e369c3f3658e968f3615084177bd201e95fe8551ab88cbecf053638f47` |
| 196125 | distant thunders meadow | Yuval | CC0 1.0 | https://freesound.org/people/Yuval/sounds/196125/ | https://cdn.freesound.org/previews/196/196125_770707-hq.ogg | `c8281cfea53e228ec6456a784f6bef1817a8b8d62bd2f2a2afc93628e7e3bf6d` |
| 203598 | CricketsandTawnyOwl | raoul_slayer | CC0 1.0 | https://freesound.org/people/raoul_slayer/sounds/203598/ | https://cdn.freesound.org/previews/203/203598_548893-hq.ogg | `587baf79c58be6fe875710ffebe2ed02ef207eb43795de6932799f2330a165f8` |
| 204899 | church bell 10 o clock EQ Yuval.wav | klankbeeld | CC BY 4.0 | https://freesound.org/people/klankbeeld/sounds/204899/ | https://cdn.freesound.org/previews/204/204899_1648170-hq.ogg | `d074f8a26a3940fc1aab82dc7f7592c72e24990c5482dc8536e15a2026c89833` |
| 212454 | Barking | unfa | CC0 1.0 | https://freesound.org/people/unfa/sounds/212454/ | https://cdn.freesound.org/previews/212/212454_1038806-hq.ogg | `7fbca7ad13c985cfe7c261f744631876cf8e0997a079d1fdf789047c3930b4f3` |
| 213329 | Rain-On-The-Roof.wav | LolitaPerdurabo | CC BY 4.0 | https://freesound.org/people/LolitaPerdurabo/sounds/213329/ | https://cdn.freesound.org/previews/213/213329_1854719-hq.ogg | `1737d6de2d68d2266fd72fce15ba71dd9917ecde80e742517ab12ac9dd8b7dc2` |
| 233167 | Rooster - Crow 3 - 96kHz | JarredGibb | CC0 1.0 | https://freesound.org/people/JarredGibb/sounds/233167/ | https://cdn.freesound.org/previews/233/233167_4056007-hq.ogg | `6aea501b36f9933a17a129120675f9c7e202baa218322cd72030d073b48ffc57` |
| 240587 | Freyburg Forest Evening R | blaukreuz | CC0 1.0 | https://freesound.org/people/blaukreuz/sounds/240587/ | https://cdn.freesound.org/previews/240/240587_623488-hq.ogg | `7afdd4a718c473d91b17aafa6eb37f84a1da5f3bad6d6dcd6744eada78ac0a83` |
| 243614 | Thunderstorm lightning strike | foad | CC0 1.0 | https://freesound.org/people/foad/sounds/243614/ | https://cdn.freesound.org/previews/243/243614_260969-hq.ogg | `a3bcc4c1980108db7ffcd81957b18d863da908ffc6c8c424fd99e4dd7088191a` |
| 266601 | Sparrows.WAV | otad | CC0 1.0 | https://freesound.org/people/otad/sounds/266601/ | https://cdn.freesound.org/previews/266/266601_2151664-hq.ogg | `c84f32775361efec23cfea992d2745aa191f5e5c8a793918d6bb1b12594a2564` |
| 316921 | Chicken Alarm Call full with Occasional bird sound | Rudmer_Rotteveel | CC0 1.0 | https://freesound.org/people/Rudmer_Rotteveel/sounds/316921/ | https://cdn.freesound.org/previews/316/316921_4921277-hq.ogg | `2946a0261cd98b21339380d58417ce77f903be61128ff3411fde316c49fdbb28` |
| 326313 | Tavern_Ambience_Inside_Laughter.WAV | ivolipa | CC0 1.0 | https://freesound.org/people/ivolipa/sounds/326313/ | https://cdn.freesound.org/previews/326/326313_3474310-hq.ogg | `dfb2a98232563b28c8fe77a1afd87d0e9c43d336f45dc5ced4201f26cf16ecc2` |
| 367964 | TAWNY OWL in calm Environment | BlueDelta | CC0 1.0 | https://freesound.org/people/BlueDelta/sounds/367964/ | https://cdn.freesound.org/previews/367/367964_1790434-hq.ogg | `dccc47ea2740c14f48912bf576ca02dd6983ee9c98a9dad22a8766941bca0ad5` |
| 383930 | Rooster crowing | Navadaux | CC0 1.0 | https://freesound.org/people/Navadaux/sounds/383930/ | https://cdn.freesound.org/previews/383/383930_5576856-hq.ogg | `9e73d7f49a6188d6863cc431773477cce3be8840ebbf66d5ded6622b9eb64f71` |
| 429706 | Cockcrow - Hahnenschrei | IchBinChrist | CC0 1.0 | https://freesound.org/people/IchBinChrist/sounds/429706/ | https://cdn.freesound.org/previews/429/429706_3216899-hq.ogg | `2d718821eb8d6b341a42ee63dd9c716b40fc280f9d089d7f9760e25ea2be9798` |
| 434074 | Churchbells recorded from the top of the Cathedral at noon in Fribourg | felix.blume | CC0 1.0 | https://freesound.org/people/felix.blume/sounds/434074/ | https://cdn.freesound.org/previews/434/434074_1661766-hq.ogg | `07983d7aa8b5612d11d0812c906d313a48760a02e21a50250d31602597d2486b` |
| 437345 | Crows Cawing | giddster | CC0 1.0 | https://freesound.org/people/giddster/sounds/437345/ | https://cdn.freesound.org/previews/437/437345_1738686-hq.ogg | `4365726bd1e0f1cc579bc702238bbe52df32476c13fce84b639bf0b1be45121b` |
| 446019 | Gentle Stream Natural Stream Sound | BurghRecords | CC0 1.0 | https://freesound.org/people/BurghRecords/sounds/446019/ | https://cdn.freesound.org/previews/446/446019_7241289-hq.ogg | `f534a3bb1dd414dcd7bb8be8f6ae13a99614201021143dce3ad85245b47fe90d` |
| 469700 | Ambience Sound of the Chouara Tannery in Fez, Morocco | florianreichelt | CC0 1.0 | https://freesound.org/people/florianreichelt/sounds/469700/ | https://cdn.freesound.org/previews/469/469700_6253486-hq.ogg | `0c8723ee5413cf492b2950bf78e1bb3e1eb207dd866e157a65be83ce560cf270` |
| 511546 | Kölner Dom (Cologne Cathedral) bells | polymorpheva | CC0 1.0 | https://freesound.org/people/polymorpheva/sounds/511546/ | https://cdn.freesound.org/previews/511/511546_1171107-hq.ogg | `98ad2e5ca6484dd1fa71bed56e42277251ffa05184376360e208774561874c61` |
| 573080 | Dawn Chorus Birdsong | squashy555 | CC0 1.0 | https://freesound.org/people/squashy555/sounds/573080/ | https://cdn.freesound.org/previews/573/573080_1149179-hq.ogg | `3239fd174f196593541403e2dd235dddac9dc9c456595b73e4deb86c14c4a9ff` |
| 594343 | Dog barking and growling.wav | GH0STY_XD | CC0 1.0 | https://freesound.org/people/GH0STY_XD/sounds/594343/ | https://cdn.freesound.org/previews/594/594343_12596088-hq.ogg | `bff05710ddf484d7f4dc2e397da5f560f9c5b1b82155f673602f9f07d94458c0` |
| 613548 | Kristoffer_Andersson_Amb_City_Forest_Leaves_Wind_ST_ECM680S.wav | Kristoffer_Andersson | CC0 1.0 | https://freesound.org/people/Kristoffer_Andersson/sounds/613548/ | https://cdn.freesound.org/previews/613/613548_8407191-hq.ogg | `8ebafdb525785f5be666fbe3fa875796693cc5af986ffd0ea837c27f3c7c137d` |
| 627785 | Frog sounds in a pond with bird song.wav | poots | CC BY 4.0 | https://freesound.org/people/poots/sounds/627785/ | https://cdn.freesound.org/previews/627/627785_1038119-hq.ogg | `fd46ca28c64a017cef36946f6e64dbde47a69f5cd8a2d64e8f9d73939c3fa945` |
| 705299 | Dogs barking far with echo in a small village at night | felix.blume | CC0 1.0 | https://freesound.org/people/felix.blume/sounds/705299/ | https://cdn.freesound.org/previews/705/705299_1661766-hq.ogg | `c67a5101c312f0ee5f0f73412acc36ec107e4abf384bba182cc1556622964c73` |
| 719572 | CROWD FOOTSTEP | hadescolossus | CC0 1.0 | https://freesound.org/people/hadescolossus/sounds/719572/ | https://cdn.freesound.org/previews/719/719572_8385093-hq.ogg | `f9417da9807049d722edde8c96ce2704089bdd4245c98dd46349c65e4c488e8d` |
| 823212 | Late Night Street Ambience Conversations and City Atmosphere (in Arabic) | sounds_from_palestine | CC0 1.0 | https://freesound.org/people/sounds_from_palestine/sounds/823212/ | https://cdn.freesound.org/previews/823/823212_17247322-hq.ogg | `4d5f2239fc462d1569db645c10a3c42363bf07b173d597ce457f85ea21938791` |
| 832832 | Family Astronomy Night in Ramallah – Saturn and Moon Observation at Qattan Foundation (Field Recording) | sounds_from_palestine | CC0 1.0 | https://freesound.org/people/sounds_from_palestine/sounds/832832/ | https://cdn.freesound.org/previews/832/832832_17247322-hq.ogg | `975e0e2165332ad420b62e0edfe82ba10e93915962146b142a234e57e3225b99` |
| 836064 | RAINVege_Summer Rain.Garden.Under The Apple Tree.Drops On Leaves,Grass,Metal Roof,Barbeque 1_EM | newlocknew | CC BY 4.0 | https://freesound.org/people/newlocknew/sounds/836064/ | https://cdn.freesound.org/previews/836/836064_5828667-hq.ogg | `3d301297098d394ecb4c051f0cc8c09f0b04bb4adf08d0d3b0505f7c299a8c82` |
| 844420 | Extreme Thunder Impact - Munich Trudering (Raw Excerpt from Field Recording) - 250702_001 | TSP-Talk | CC0 1.0 | https://freesound.org/people/TSP-Talk/sounds/844420/ | https://cdn.freesound.org/previews/844/844420_14079354-hq.ogg | `ecfbd0afd70af939d934be4ebb9de732e171082b85233751361f07b076668c23` |

## Dateien

| Datei | Quelle | Ausschnitt (s) | Filter | Zweck |
|---|---|---|---|---|
| schichten/stadt_tag.ogg | 469700 | Folge 1.0–59.0, 33.0–47.5, 5.0–19.5, 46.5–61.0, 19.0–33.5, 27.0–41.5, 39.0–53.5 (Übergänge 1,5 s) | Hochpass 80 Hz, 14.4–15.1 s und 25.7–26.2 s der Quelle −7 dB (zwei herausstehende Stimmen), Naht 3.0 s | Stadtleben am Tag: Arbeit, Wasser, ferne Stimmen (Gerberei in der autofreien Altstadt von Fes); 133 s aus der 61-s-Quelle, damit kein fester Takt hörbar ist |
| schichten/morgen_voegel.ogg | 573080 | 20.0–120.0 | Naht 3.0 s | Vogelchor im Morgengrauen |
| schichten/tag_spatzen.ogg | 266601 | 0.5–60.5 | Hochpass 250 Hz, Naht 3.0 s | Spatzen am Tag (Stadtvögel), leise |
| schichten/abend_voegel.ogg | 240587 | 1.0–87.0 | Hochpass 200 Hz, Naht 3.0 s | Abendgesang (Wald bei Freyburg/Unstrut) |
| schichten/stadt_abend.ogg | 187199 | 30.0–135.0 | Tiefpass 1000 Hz, Naht 4.0 s | Gedämpftes Gemurmel am Abend, wie aus Schenken hinter Wänden |
| schichten/wind_mittel.ogg | 135193 | 2.0–77.0 | Hochpass 120 Hz, Naht 4.0 s | Kräftigerer Wind, nur ganz weit herausgezoomt (ohne die Pfeifbö bei 79,5 s) |
| schichten/nacht.ogg | eigene Synthese (Stufe nacht, Saat 11) | 61 s, exakt periodisch | – | Stiller Nachtgrund: sehr ferner, dunkler Wind (eigene Synthese) |
| schichten/wind_leicht.ogg | eigene Synthese (Stufe leicht_boeig, Saat 23) | 67 s, exakt periodisch | – | Leichter Wind in der Höhe mit unregelmäßigen Böen (eigene Synthese) |
| rufe/hahn_1.ogg | 429706 | 4.3–7.4 | – |  |
| rufe/hahn_2.ogg | 233167 | 0.4–2.4 | – |  |
| rufe/hahn_3.ogg | 383930 | 0.0–2.0 | – |  |
| rufe/hund_1.ogg | 166421 | 54.3–56.2 | Hochpass 150 Hz |  |
| rufe/hund_2.ogg | 166421 | 66.1–68.8 | Hochpass 150 Hz |  |
| rufe/hund_3.ogg | 166421 | 70.9–71.7 | Hochpass 150 Hz |  |
| rufe/hund_4.ogg | 212454 | 0.6–1.3 | Hochpass 150 Hz |  |
| rufe/hund_5.ogg | 594343 | 0.7–2.0 | Hochpass 150 Hz |  |
| rufe/hund_fern_1.ogg | 705299 | 13.2–15.9 | Hochpass 250 Hz |  |
| rufe/hund_fern_2.ogg | 705299 | 35.6–37.4 | Hochpass 250 Hz |  |
| rufe/hund_fern_3.ogg | 705299 | 76.0–78.1 | Hochpass 250 Hz |  |
| rufe/hund_fern_4.ogg | 166421 | 34.0–38.8 | Hochpass 250 Hz |  |
| rufe/kauz_1.ogg | 367964 | 6.0–9.1 | Hochpass 300 Hz |  |
| rufe/kauz_2.ogg | 367964 | 64.7–67.8 | Hochpass 300 Hz |  |
| rufe/kauz_3.ogg | 367964 | 98.2–101.1 | Hochpass 300 Hz |  |
| rufe/kauz_4.ogg | 367964 | 123.2–126.4 | Hochpass 300 Hz |  |
| rufe/kauz_5.ogg | 367964 | 20.4–22.2 | Hochpass 300 Hz |  |
| rufe/amsel_1.ogg | 116791 | 9.6–12.2 | Hochpass 600 Hz |  |
| rufe/amsel_2.ogg | 116791 | 26.5–28.7 | Hochpass 600 Hz |  |
| rufe/amsel_3.ogg | 116791 | 51.4–53.5 | Hochpass 600 Hz |  |
| rufe/amsel_4.ogg | 116791 | 82.0–83.9 | Hochpass 600 Hz |  |
| rufe/amsel_5.ogg | 116791 | 92.2–95.4 | Hochpass 600 Hz |  |
| rufe/pferd_1.ogg | 173159 | 1.0–4.5 | Hochpass 120 Hz |  |
| rufe/pferd_2.ogg | 173159 | 5.0–8.5 | Hochpass 120 Hz |  |
| rufe/pferd_3.ogg | 173159 | 10.0–13.5 | Hochpass 120 Hz |  |
| rufe/gefluegel_1.ogg | 149192 | 0.0–1.7 | Hochpass 150 Hz |  |
| rufe/gefluegel_2.ogg | 149192 | 5.3–6.9 | Hochpass 150 Hz |  |
| rufe/gefluegel_3.ogg | 316921 | 0.4–1.6 | Hochpass 200 Hz |  |
| rufe/gefluegel_4.ogg | 316921 | 18.4–19.9 | Hochpass 200 Hz |  |
| rufe/passant_1.ogg | 719572 | 5.0–8.0 | Hochpass 120 Hz, Tiefpass 5000 Hz |  |
| rufe/passant_2.ogg | 719572 | 14.0–17.0 | Hochpass 120 Hz, Tiefpass 5000 Hz |  |
| rufe/passant_3.ogg | 719572 | 26.0–29.0 | Hochpass 120 Hz, Tiefpass 5000 Hz |  |
| rufe/passant_4.ogg | 719572 | 37.0–40.0 | Hochpass 120 Hz, Tiefpass 5000 Hz |  |
| rufe/passant_5.ogg | 719572 | 47.0–50.0 | Hochpass 120 Hz, Tiefpass 5000 Hz |  |
| rufe/handwerk_1.ogg | 137836 | 13.1–18.7 | Hochpass 150 Hz, Tiefpass 4000 Hz |  |
| rufe/handwerk_2.ogg | 137836 | 20.6–22.4 | Hochpass 150 Hz, Tiefpass 4000 Hz |  |
| rufe/handwerk_3.ogg | 137836 | 4.6–6.4 | Hochpass 150 Hz, Tiefpass 4000 Hz |  |
| rufe/handwerk_4.ogg | 137836 | 72.7–73.7 | Hochpass 150 Hz, Tiefpass 4000 Hz |  |
| schichten/natur_laub.ogg | 613548 | 40.0–115.0 | Hochpass 150 Hz, Tiefpass 6000 Hz, Naht 4.0 s | Laubrauschen und Wind in den Bäumen (Wald bei Stockholm im Sturm, ruhiger Teil), Wald und Gehölz; im Winter kahl |
| schichten/natur_grillen.ogg | 203598 | Folge 8.0–28.0, 40.0–58.0, 68.0–88.0, 98.0–138.0 (Übergänge 1.5 s) | Hochpass 800 Hz, Naht 3.0 s | Grillen und Heuschrecken auf der Wiese (Sommernacht, die Waldkauz-Rufe herausgeschnitten) |
| schichten/natur_froesche.ogg | 627785 | 60.0–135.0 | Hochpass 300 Hz, Naht 4.0 s | Frösche am Teich (Wales, März, mit Vogelgesang), nur am Wasser im Frühling |
| schichten/natur_fluss.ogg | 446019 | 2.0–62.0 | Hochpass 100 Hz, Naht 4.0 s | Plätschern eines sanften Bachs (Schottland), nur am Wasser |
| schichten/regen_leicht.ogg | 836064 | 60.0–150.0 | Hochpass 100 Hz, weicher Begrenzer -9.0 dBFS, Naht 4.0 s | Leichter Regen auf Laub und Gras (Garten unter einem Apfelbaum) |
| schichten/regen_stark.ogg | 184627 | 5.0–135.0 | Hochpass 150 Hz, weicher Begrenzer -9.0 dBFS, Naht 4.0 s | Starker Regen, Guss auf Dach und Fenster (Nacht) |
| schichten/regen_dach.ogg | 213329 | 60.0–125.0 | Hochpass 200 Hz, Tiefpass 9000 Hz, Naht 4.0 s | Regen auf einem Dach mit Wind, nah in der Stadt (herangezoomt) |
| schichten/schenke_innen.ogg | 326313 | 0.5–37.0 | Hochpass 100 Hz, Naht 3.0 s | Schankstube von innen (Wirtshaus in Istrien: Stimmen, Lachen, Geschirr; Sprache nicht verständlich) |
| rufe/kraehe_1.ogg | 437345 | 0.9–2.7 | Hochpass 400 Hz |  |
| rufe/kraehe_2.ogg | 437345 | 5.9–7.7 | Hochpass 400 Hz |  |
| rufe/kraehe_3.ogg | 437345 | 8.9–10.7 | Hochpass 400 Hz |  |
| rufe/kraehe_4.ogg | 437345 | 14.9–17.0 | Hochpass 400 Hz |  |
| rufe/kraehe_5.ogg | 437345 | 23.4–25.9 | Hochpass 400 Hz |  |
| rufe/horn_1.ogg | 166067 | 0.0–3.3 | Hochpass 150 Hz |  |
| rufe/horn_2.ogg | 166067 | 4.45–8.5 | Hochpass 150 Hz |  |
| rufe/gespraeche/gasse/gasse_1.ogg | 823212 | 13.2–15.7 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_2.ogg | 823212 | 15.7–18.2 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_3.ogg | 823212 | 18.2–20.8 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_4.ogg | 823212 | 20.7–23.2 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_5.ogg | 823212 | 23.2–25.7 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_6.ogg | 823212 | 25.7–28.1 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_7.ogg | 832832 | 105.0–108.0 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/gasse/gasse_8.ogg | 832832 | 113.5–116.5 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 5500 Hz |  |
| rufe/gespraeche/markt/markt_1.ogg | 823212 | 36.5–39.5 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |
| rufe/gespraeche/markt/markt_2.ogg | 823212 | 40.5–43.5 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |
| rufe/gespraeche/markt/markt_3.ogg | 823212 | 50.5–53.5 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |
| rufe/gespraeche/markt/markt_4.ogg | 823212 | 86.5–89.5 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |
| rufe/gespraeche/markt/markt_5.ogg | 823212 | 116.0–119.0 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |
| rufe/gespraeche/markt/markt_6.ogg | 823212 | 122.0–125.0 | Entrauschen 12 dB, Hochpass 180 Hz, Tiefpass 6500 Hz |  |

| glocken/glocke_morgen_1.ogg | 511546 | 8.0–38.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Morgen: Geläut des Kölner Doms, 30 s |
| glocken/glocke_morgen_2.ogg | 511546 | 60.0–90.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Morgen: Geläut des Kölner Doms, 30 s (andere Stelle) |
| glocken/glocke_messe_2.ogg | 511546 | 84.0–120.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Messe: Geläut des Kölner Doms, 36 s |
| glocken/glocke_abend_2.ogg | 511546 | 36.0–66.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Abend: Geläut des Kölner Doms, langsam ausklingend, 30 s |
| glocken/glocke_messe_1.ogg | 434074 | 88.0–128.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Messe: Geläut vom Dach der Kathedrale Fribourg (Schweiz), viele Glocken, 40 s |
| glocken/glocke_abend_1.ogg | Mischung | 204899 0.0–34.0 s ab 0.0 s (0 dB); 434074 78.0–104.0 s ab 14.0 s (-5 dB) | weicher Begrenzer -8 dBFS | Abend: die große Glocke (Noteman, Niederlande) allein, ab 14 s kommt das Geläut von Fribourg dazu, ausklingend, 40 s |
| glocken/glocke_feuer_1.ogg | 204899 | 0.0–24.0 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Feuer: die große Glocke (Noteman, Niederlande) allein, langsame schwere Schläge, 24 s |
| glocken/glocke_feuer_2.ogg | 204899 | 17.8–41.8 | Hochpass 45 Hz, weicher Begrenzer -8 dBFS | Feuer: wie 1, andere Stelle, 24 s |
| rufe/donner_fern_1.ogg | 196125 | 120.5–132.5 | Hochpass 35 Hz, Tiefpass 600 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer -9 dBFS, -18 LUFS | Donner fern |
| rufe/donner_fern_2.ogg | 196125 | 220.5–232.5 | Hochpass 35 Hz, Tiefpass 600 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer -9 dBFS, -18 LUFS | Donner fern |
| rufe/donner_fern_3.ogg | 196125 | 351.0–363.0 | Hochpass 35 Hz, Tiefpass 600 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer -9 dBFS, -18 LUFS | Donner fern |
| rufe/donner_fern_4.ogg | 196125 | 470.0–482.0 | Hochpass 35 Hz, Tiefpass 600 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer -9 dBFS, -18 LUFS | Donner fern |
| rufe/einschlag_1.ogg | 243614 | 6.5–24.0 | Hochpass 28 Hz, Tiefpass 7000 Hz, Tiefbass unter 120 Hz +7 dB, weicher Begrenzer -9 dBFS, -14 LUFS | Einschlag: direkter Blitzschlag in ein Gebäude, brachialer Knall mit langem Rollen (foad, Stockholm), -14 LUFS, Spitze -0,5 dBTP |
| rufe/einschlag_2.ogg | 243614 | 6.5–40.0 | Hochpass 28 Hz, Tiefpass 7000 Hz, Tiefbass unter 120 Hz +7 dB, weicher Begrenzer -9 dBFS, -14 LUFS | Einschlag: direkter Blitzschlag in ein Gebäude, brachialer Knall mit langem Rollen (foad, Stockholm), -14 LUFS, Spitze -0,5 dBTP |
| rufe/donner_1.ogg | 196125 | 6.5–20.5 | Hochpass 30 Hz, Tiefpass 6000 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer −10 dBFS, −17 LUFS, Spitze −2,5 dBTP | Donner nah (normales Gewitter, nie näher als 1 km) |
| rufe/donner_2.ogg | 196125 | 199.5–213.5 | Hochpass 30 Hz, Tiefpass 6000 Hz, Tiefbass unter 120 Hz +5 dB, weicher Begrenzer −10 dBFS, −17 LUFS, Spitze −2,5 dBTP | Donner nah (normales Gewitter, nie näher als 1 km) |
| rufe/donner_3.ogg | 844420 | 2.3–20.0 | Hochpass 28 Hz, Tiefpass 7000 Hz, Tiefbass unter 120 Hz +7 dB, weicher Begrenzer −10 dBFS, −17 LUFS, Spitze −2,5 dBTP | Donner nah (normales Gewitter, nie näher als 1 km) |
| rufe/donner_4.ogg | 844420 | 2.3–34.0 | Hochpass 28 Hz, Tiefpass 7000 Hz, Tiefbass unter 120 Hz +7 dB, weicher Begrenzer −10 dBFS, −17 LUFS, Spitze −2,5 dBTP | Donner nah (normales Gewitter, nie näher als 1 km) |
