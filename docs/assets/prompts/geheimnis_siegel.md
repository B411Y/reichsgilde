# Prompts: Geheimnis-Siegel (Entwurf Runde 2, 09.11.2026)

Dateien: vier runde Medaillen; im Spiel `game/assets/oberflaeche/geheimnisse/<art>.png` (128 px; 64 px entstehen durch Mipmaps). Rohbilder und die 64-px-Fassung liegen nicht im Repository.

## Werkzeug

Lokale Bild-KI auf dem Grafik-PC des Nutzers: ComfyUI v0.42.0, Text zu Bild mit Qwen-Image 2512 (`qwen-image-2512-Q4_K_M.gguf`, ComfyUI-GGUF), Text-Encoder `qwen_2.5_vl_7b_fp8_scaled`, VAE `qwen_image_vae`, **ohne** Lightning-LoRA, mit der eigenen Stil-LoRA **holzschnitt_v1** (Stand 2250, Stärke 1.0; LoRA-Datei nicht im Repository). Sampler KSampler euler/simple, 26 Schritte, cfg 4.0, ModelSamplingAuraFlow shift 3.0, 768 x 768. Kein Gemini.

Prompt-Rahmen (Runde 1, auch in Runde 2 gleich): `rgholzschnitt, hand-coloured woodcut seal. <Motiv>. Round wax seal emblem with a thick ring border, strong black outlines, cross-hatching in the shadows, red, deep blue and gold hand colour slightly off the lines, one clear symbol centered on plain old paper background, no text.`

Negativ: `text, letters, writing, signature, watermark, photo, 3d render, blurry, modern, frame, border, multiple objects, cropped, upside-down cross, satanic`

Nachbearbeitung (Skript `nach.py`, außerhalb des Repositorys): Motiv suchen, quadratisch zuschneiden, rund maskieren, schwarzer Ring, 128 und 64 px. Keine Handarbeit.

## Gewählte Motive und Seeds

| Datei im Spiel | Entwurf | Seed | Motiv |
|---|---|---|---|
| ehebruch.png | ehebruch_a2_s2 | 2 | two interlocked golden wedding rings, both rings broken, each snapped in two pieces with a gap, a small red rose petal falling |
| ketzerei.png | ketzerei_a3_s1 | 1 | a stack of logs on fire, flames leaping from the timber on all sides, a thick book lying open on the burning pyre, its pages aflame |
| mord.png | mord_c2_s1 | 1 | a human skull in the center with two daggers crossed behind it forming an X, bold simple symmetrical emblem, a few blood stains on the blades |
| schulden.png | schulden_b2_s1 | 1 | a plump money purse resting on an open outstretched palm of a hand demanding payment, the hand reaching up from below |
