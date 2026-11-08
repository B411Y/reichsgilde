# Prompts: Lebensweise-Icons (Entwurf, 08.11.2026)

Dateien: 50 runde Medaillen; im Spiel `game/assets/oberflaeche/lebensweise/*.png` (128 px, Dateiname = Kennung; 64 px entstehen durch Mipmaps). Rohbilder und die 64-px-Fassung liegen nicht im Repository.

## Werkzeug

Lokale Bild-KI auf dem Grafik-PC des Nutzers: ComfyUI v0.42.0, Text zu Bild mit Qwen-Image 2512 (`qwen-image-2512-Q4_K_M.gguf`, ComfyUI-GGUF), Text-Encoder `qwen_2.5_vl_7b_fp8_scaled`, VAE `qwen_image_vae`, **ohne** Lightning-LoRA, mit der eigenen Stil-LoRA **holzschnitt_v1** (Stand 2250, Stärke 1.0; LoRA-Datei nicht im Repository). Sampler KSampler euler/simple, cfg 4.0, ModelSamplingAuraFlow shift 3.0. Pfad-Zeichen: 1024 x 1024, 30 Schritte; Knoten: 768 x 768, 26 Schritte. Negativ-Prompt: `text, letters, writing, signature, watermark, photo, 3d render, blurry, modern, frame, border, multiple objects, cropped`.

Nachbearbeitung (Skript `nach.py`, außerhalb des Repositorys): Motiv auf Papierton suchen, quadratisch zuschneiden, zur runden Scheibe maskieren, schwarzer Ring (Pfad-Zeichen: zusätzlich goldener Doppelring), Verkleinern auf 128 und 64 px. Keine Handarbeit.

## Prompt-Rahmen

Pfad-Zeichen: `rgholzschnitt, hand-coloured woodcut emblem. <Motiv>. Rich ornate heraldic symbol, strong black outlines, cross-hatching in the shadows, red, blue and gold hand colour slightly off the lines, single emblem centered on plain old paper background, no text, no border.`

Knoten: `rgholzschnitt, hand-coloured woodcut. <Motiv>. Simple icon, one motif centered, strong black outlines, cross-hatching in the shadows, soft hand colour slightly off the lines, plain old paper background, no text, no border.`

## Motive und gewählte Seeds

| Kennung | Seed | Motiv |
|---|---|---|
| pfad_kaufmann | 11 | a merchant's golden balance scale held above a heavy money chest overflowing with gulden coins, a ledger book and a sealed trade letter beside it |
| pfad_meister | 11 | a master craftsman's hammer and a pair of tongs crossed over an anvil, with a guild master's wreath of oak leaves around them |
| pfad_schatten | 11 | a hooded figure's face in shadow, holding a dagger and a skeleton key, a crescent moon above |
| pfad_schwert | 11 | a sword standing upright with a round shield and a laurel wreath, a knight's helmet above |
| pfad_redner | 11 | an open mouth-speaking head in profile, an orator's raised hand and a scroll with a ribbon seal, a laurel wreath around |
| marktschreier | 1 | a market crier shouting with a hand beside his mouth in front of a market stall |
| feilscher | 1 | two hands haggling over a few coins above a small sack of grain |
| handelsherr | 1 | a fat merchant's money purse and a bale of cloth with a trade seal |
| rechenkunst | 1 | an abacus counting board with beads and a quill |
| haushaelter | 1 | a household key ring and a small purse hanging from a belt, a loaf of bread |
| steuerkniff | 1 | a tax collector's chest with a coin slipping out of it through a hand, a fox's tail peeking out |
| offene_hand | 1 | an open hand giving out a gift, a coin and a small ribbon-tied present |
| geschaeftsfreund | 1 | two hands clasped in a handshake over a signed contract |
| goenner | 1 | a patron's coat of arms shield with a golden crown above, a small ribbon |
| sorgfalt | 1 | a careful craftsman's hand holding a small chisel above a finished wooden joint, a magnifying glass |
| kniffe | 1 | a clever carpenter's plane and a hand saw crossed with a small hourglass |
| meisterstueck | 1 | a masterpiece goblet of fine goldsmith work on a cushion with a small guild seal |
| lehrherr | 1 | a master's hand guiding a young apprentice's hand holding a hammer |
| gutes_wort | 1 | a mug of beer and a kind pat on a shoulder, a smiling workman's face |
| zunftmeister | 1 | a guild master's chain of office with a medal, and a guild banner |
| ordnung | 1 | a tidy wooden shelf with neatly stacked barrels, sacks and crates |
| baumeister | 1 | a builder's set square and compasses over a stone block with a small timber-framed house |
| hueter_des_hauses | 1 | a house with a key on a thick iron ring and a watching lantern before its door |
| leise_sohlen | 3 | a pair of soft medieval pointed leather poulaine shoes with a footprint in the dust |
| saubere_arbeit | 2 | a hand holding a broom sweeping away tracks, a rag and a snuffed candle |
| hehlerring | 1 | a fence's secret ring with a gem in a hand beside a stolen bundle in a sack |
| ohr_an_der_wand | 1 | a large ear pressed against a wall, a keyhole beside it |
| geruechtekueche | 1 | a witches' cooking pot over a fire with whispering mouths in the steam and speech scrolls |
| schwarzes_buch | 1 | a black bound book with an iron clasp and a quill and a wax seal |
| wachsames_auge | 1 | a large watchful eye above a locked door with a padlock |
| verschwiegen | 1 | a face with a finger pressed against the lips and a sealed mouth with a wax seal |
| falsche_faehrte | 1 | crossed footprints and a signpost pointing in two directions with a fox |
| bewaffnete_knechte | 1 | armed servants with spears escorting a cart wheel, a pike and a crossbow |
| wegekundig | 1 | a traveller's staff and a rolled road map with a compass and a signpost |
| hauptmann | 1 | a captain's helmet with a plume and a commander's baton above two crossed spears |
| hauswehr | 1 | a heavy oak door with iron bolt and a halberd on each side, a bucket of water |
| wehrhafte_werkstatt | 1 | a blacksmith's workshop door with a mace and a shield beside a bucket of water and a flame |
| eisern | 1 | an iron breastplate over a heart, with a small healing herb sprig |
| weidmann | 1 | a hunting horn and a bow with a quiver of arrows, a small deer behind |
| jagdherr | 1 | a nobleman's hunting hawk on a gloved fist and a hunting horn, a stag's antler |
| jagdgesellschaft | 2 | a huntsman blowing a hunting horn with two hounds at his feet, a boar spear and a forest tree |
| wohlgesetzte_worte | 1 | a speaker's feather quill writing on a scroll, a speech bubble with gentle flowers |
| fuersprecher | 1 | a man standing up in a council chamber and speaking for a petition scroll |
| volksredner | 1 | a public orator on a barrel before a cheering crowd of townspeople, a raised hand |
| plaedoyer | 1 | a lawyer's raised hand and the scales of justice before a judge's bench |
| rechtskundig | 1 | a thick law book with a ribbon and a bailiff's staff, a small scale |
| gnadengesuch | 1 | a kneeling petitioner with folded hands before a judge, a pillory in the distance and a clemency letter |
| gastgeber | 1 | a banquet table set with a roast, jugs and goblets, a host raising a cup |
| brautwerber | 1 | a young suitor's hand offering a bouquet of flowers and a ring, a heart on a ribbon |
| liebenswuerdig | 3 | a charming smiling young man with a rose in his hand and a small heart-shaped pendant on his chest |

Neu gewürfelt (Rohbilder in `roh/alt/`): leise_sohlen (erst moderne Schnürschuhe, dann Poulaines), jagdgesellschaft (Jagdszene zu unruhig), liebenswuerdig (Sonnengesicht), saubere_arbeit (Fuß statt Besen); bei diesen vier wurde das Motiv im Prompt umformuliert, die genannten Texte sind die neuen.
