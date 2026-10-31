# Ablauf: Ladebilder (Holzschnitt-Platzhalter, 31.10.2026)

Dateien: `game/assets/oberflaeche/ladebilder/*.webp` (acht Bilder, 1024 x 1024). **Platzhalter**, werden später ersetzt. Protokoll: `docs/assets/protokoll.csv`.

Werkzeug: ComfyUI auf dem Grafik-PC des Nutzers, Text zu Bild mit Qwen-Image 2512 (`qwen-image-2512-Q4_K_M.gguf`, ComfyUI-GGUF), Text-Encoder `qwen_2.5_vl_7b_fp8_scaled`, VAE `qwen_image_vae`, **ohne** Lightning-LoRA. Zusätzlich die eigene Stil-LoRA **holzschnitt_v1** (Rang 16, trainiert mit musubi-tuner auf Qwen-Image 2512 mit einem eigenen Datensatz; **LoRA-Datei und Trainingsdatensatz sind nicht im Repository**). Sampler: KSampler euler/simple, 30 Schritte, cfg 4.0, ModelSamplingAuraFlow shift 3.0, 1024 x 1024. Seed = 1000 + Motivnummer x 7919. Negativ-Prompt: `text, letters, writing, signature, watermark, photo, 3d render, blurry, modern, frame, border`.
Nachbearbeitung: nur Umwandlung PNG nach WebP (Qualität 0,92, Godot `Image.save_webp`).

| Datei | LoRA-Stand | Stärke | Motiv | Seed |
|---|---|---|---|---|
| schmied.webp | 1250 | 1.0 | 1 | 8919 |
| markt.webp | 2250 | 1.0 | 2 | 16838 |
| zimmerleute.webp | 1250 | 1.0 | 3 | 24757 |
| mahlzeit.webp | 2250 | 1.0 | 4 | 32676 |
| kaufmann.webp | 2250 | 0.8 | 5 | 40595 |
| stadtansicht.webp | 2250 | 1.0 | 6 | 48514 |
| ochsenkarren.webp | 2250 | 0.8 | 7 | 56433 |
| prediger.webp | 2250 | 1.0 | 8 | 64352 |

Prompts (Stichwort `rgholzschnitt` ist das Auslösewort der LoRA):

1. schmied: `rgholzschnitt, hand-coloured woodcut. A blacksmith and his journeyman at the forge, the master holding glowing iron with tongs on the anvil, the journeyman pulling the bellows; tools on the wall, simple faces drawn with few lines, heavy folds in their aprons.`
2. markt: `rgholzschnitt, hand-coloured woodcut. Market women at a wooden stall selling eggs, cabbages and cheese, a customer with a basket, other stalls and gabled houses behind, plain paper sky.`
3. zimmerleute: `rgholzschnitt, hand-coloured woodcut. Carpenters building a timber-framed house, one man on the roof beams with a hammer, two below sawing a log on trestles, another carrying a beam on his shoulder, wood shavings on the ground.`
4. mahlzeit: `rgholzschnitt, hand-coloured woodcut. A family at their meal around a wooden table, the father breaking bread, the mother pouring from a jug, two children on a bench, a hearth and a shelf with pots in the room.`
5. kaufmann: `rgholzschnitt, hand-coloured woodcut. A merchant in a fur-trimmed gown sitting at a desk in his counting house, weighing coins on scales, an open ledger, barrels and bales of cloth behind him, a clerk writing at his side.`
6. stadtansicht: `rgholzschnitt, hand-coloured woodcut. View of a walled city on a broad river, many churches and towers, a large unfinished cathedral with one stump of a tower carrying a wooden crane, boats on the river, hills in the background.`
7. ochsenkarren: `rgholzschnitt, hand-coloured woodcut. A peasant driving an ox cart loaded with sacks along a country road, a stick in his hand, fields, a fence and a small village with a church in the distance.`
8. prediger: `rgholzschnitt, hand-coloured woodcut. A preacher in a dark habit speaking from a wooden pulpit in a town square to a crowd of townspeople, men in hoods and caps, women with veils, some kneeling, one man pointing upward.`

Quelle der Prompts: Testprompts 1 bis 8 aus `start.txt` der LoRA-Arbeit; Seeds und Workflow aus dem Skript `lauf.py` (beides nicht im Repository). Die Bilder stammen aus dem Testlauf der Zwischenstände der LoRA (Stand 1250 bzw. 2250 von 3000 Schritten); ob der Auflauf-Commit der ComfyUI-Version dabei derselbe war wie am 29.10.2026, ist nicht belegt.
