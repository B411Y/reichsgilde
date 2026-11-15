# Prompts: Versehrungs-Medaillons (14.11.2026)

Dateien: `game/assets/oberflaeche/zuege/{entstellt,einaeugig,lahm,verkrueppelt}.png` (128 px). Protokoll: `docs/assets/protokoll.csv`. Rohbilder (1024 px) liegen nicht im Repository.

## Werkzeug

- **Bilder:** lokale Bild-KI, ComfyUI auf dem Grafik-PC des Nutzers (über den Startdienst gestartet), Text zu Bild mit Qwen-Image, Aufrufskript `bild.py` außerhalb des Repositorys (Workflow „reichsgilde_text_api“, 1024 x 1024, Standard mit Lightning-LoRA). Genaue Modelldatei in diesem Lauf nicht abgefragt: unbekannt. Kein Gemini, keine Holzschnitt-LoRA (die Medaillons sind kräftig gemalt, Ausnahme vom Holzschnitt).
- **Nachbearbeitung:** rund zugeschnitten und mit geschmiedetem Eisenring (Art „erworben“) von `tools/zugmedaillons/schmiede.gd`; keine Handarbeit.
- Stil-Vorsatz wie bei den übrigen Zug-Medaillons (`zug_medaillons.md`), zusätzlich „stylized (not photorealistic)“, weil der erste Versuch fotorealistisch wurde.

## Prompt-Rahmen

> Square 1:1 image, boldly painted stylized fantasy game icon illustration (not photorealistic), thick dark outlines, glowing light effects and sparks, medieval mood. Motif: `<Motiv>`, large and exactly in the center, everything important inside a central circle, corners only background. Background: `<Farbe>` glow. No text, no frame, no border.

Negativ (je nach Zug): `text, letters, writing, watermark, photo, photorealistic, blurry, modern, frame, border, ring, circle, multiple objects, cropped, gore` plus Zugsspezifisches (Krallen, Gesicht, Loch, Augenbinde).

## Gewählte Motive

| Datei | Motiv | Farbe | Entwurf (von mehreren) |
|---|---|---|---|
| entstellt | scorched cracked clay half-mask with melted, blistered burn marks, glowing embers inside the cracks, a single strip of smoke | ember orange | Lauf 1 |
| einaeugig | a pirate eye patch, a small rounded black leather patch with a thin black strap, and a single clouded eyeball resting next to it | dark teal | Lauf 5 (Läufe 1–4: sahen wie Augenbinde bzw. Maske aus) |
| lahm | a worn wooden crutch-stick with a bandaged crooked leg splint beside it | dusty brown | Lauf 1 |
| verkrueppelt | a human right hand from the back, scarred and stiff, ring finger and little finger permanently curled inward, ordinary short fingernails, a few old burn scars, a dropped smith hammer below | pale green | Lauf 3 (Läufe 1–2: Monsterkrallen) |

Die Hand zeigt noch fünf gerade wirkende Finger mit zwei schiefen; als Medaillon bei 64 px gut genug, kann bei Bedarf neu gewürfelt werden.
