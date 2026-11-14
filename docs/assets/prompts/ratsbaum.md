# Ablauf: Grafiken des Ratsbaums (14.11.2026)

Dateien: `game/assets/oberflaeche/ratsbaum/` (sieben Amtszeichen 256 x 256 durchsichtig, Pergament 1024 x 768, Spruchband 512 x 192 leer und durchsichtig, Linie 124 x 54 wiederholbare Kachel). Protokoll: `docs/assets/protokoll.csv`.

Werkzeug: ComfyUI v0.42.0 auf dem Grafik-PC des Nutzers (ohne Fenster), Text zu Bild mit Qwen-Image 2512 (`qwen-image-2512-Q4_K_M.gguf`, ComfyUI-GGUF), Text-Encoder `qwen_2.5_vl_7b_fp8_scaled`, VAE `qwen_image_vae`, **ohne** Lightning-LoRA, dazu die eigene Stil-LoRA **holzschnitt_v1** (Stand 2250 von 3000 Schritten, Stärke 1.0; LoRA und Trainingsdatensatz nicht im Repository). Sampler: KSampler euler/simple, 30 Schritte, cfg 4.0, ModelSamplingAuraFlow shift 3.0. Negativ-Prompt (alle): `text, letters, writing, signature, watermark, photo, 3d render, blurry, modern, frame, border, flat colors, pixel art`. Je Motiv wurden zwei Varianten erzeugt, der Nutzer hat gewählt; die verworfenen Varianten liegen nur im Austausch-Ordner.
Nachbearbeitung: `tools/oberflaeche/ratsbaum_freistellen.py` (Hintergrund am Rand durchsichtig, Zuschneiden, Verkleinern mit Lanczos; Pergament unverändert; Linie: eine Seilwindung als nahtlose Kachel mit 24 px Überblendung). Aufruf mit dem Ordner der Rohbilder (1024 px) als Argument.

| Datei | Entwurf | Größe roh | Seed |
|---|---|---|---|
| amt_buergermeister.png | amt_buergermeister_1 | 1024 x 1024 | 7137 |
| amt_stadtrichter.png | amt_stadtrichter_1 | 1024 x 1024 | 7411 |
| amt_kaemmerer.png | amt_kaemmerer_2 | 1024 x 1024 | 7822 |
| amt_stadthauptmann.png | amt_stadthauptmann_2 | 1024 x 1024 | 8096 |
| amt_marktmeister.png | amt_marktmeister_1 | 1024 x 1024 | 8233 |
| amt_gefaengniswaerter.png | amt_gefaengniswaerter_1 | 1024 x 1024 | 8507 |
| amt_zoellner.png | amt_zoellner_2 | 1024 x 1024 | 8918 |
| ratsbaum_pergament.png | pergament_2 | 1024 x 768 | 9311 |
| ratsbaum_spruchband.png | spruchband_1 | 1024 x 384 | 9522 |
| ratsbaum_linie.png | linie_2 | 1024 x 256 | 10155 |

Prompts (Stichwort `rgholzschnitt` ist das Auslösewort der LoRA):

1. amt_buergermeister.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a mayor's staff of office with a carved knob and a heavy ornate city key crossed over it, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
2. amt_stadtrichter.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a long sword upright with a pair of balance scales hanging beside it, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
3. amt_kaemmerer.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a small iron-bound treasury chest with a key, and a stack of coins, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
4. amt_stadthauptmann.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a steel helmet with a halberd and a round shield, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
5. amt_marktmeister.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a large merchant balance scale with two pans holding grain sacks and weights, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
6. amt_gefaengniswaerter.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a heavy iron ring with three big keys and a padlock, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
7. amt_zoellner.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A single emblem icon, a customs barrel with a coin purse and a marked measuring rod, centred on a plain white background, thick black ink outlines, cross-hatching in the shadows, soft watercolor colour slightly offset from the lines, no text, no frame.`
8. ratsbaum_pergament.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A blank aged parchment page with a narrow hand-drawn black ink border line and small simple woodcut corner ornaments, cream coloured, blank centre, no text, no picture`
9. ratsbaum_spruchband.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. An empty ribbon banner (scroll banner) with curled and folded ends, drawn as a medieval woodcut, cream coloured ribbon with black outline and hatching shadows, completely blank and empty surface, on a plain white background, no text, no letters`
10. ratsbaum_linie.png: `rgholzschnitt, hand-coloured woodcut, Cologne around 1400. A horizontal rope with a simple twisted pattern, straight and long, spanning the full width of the image, medieval woodcut, black ink outline, cross-hatching, plain white background, no text`
