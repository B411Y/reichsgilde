# Prompt: Vorlage Holzeimer (Eimerkette beim Hausbrand, 02.11.2026)

Modell: `game/assets/modelle/requisiten/eimer.glb`, gebaut von `tools/modelle/eimer.py` (Blender-Skript, keine KI-Geometrie).
Das KI-Bild ist **nur Vorlage** für Maße, Daubenzahl, Reifen und Henkel; es ist kein Asset und liegt nicht im Repository.

## Werkzeug

- Lokale Bild-KI auf dem Grafik-PC des Nutzers: ComfyUI v0.42.0, Text-zu-Bild-Workflow des Projekts über ein Aufrufskript außerhalb des Repositorys.
- Modell: **Qwen-Image 2512** (`qwen-image-2512-Q4_K_M.gguf`, Q4_K_M) mit Text-Encoder `qwen_2.5_vl_7b_fp8_scaled`, VAE `qwen_image_vae` und LoRA `Qwen-Image-2512-Lightning-4steps-V1.0-bf16` (4 Schritte). Lizenz Apache-2.0 (kommerzielle Nutzung erlaubt): https://huggingface.co/Qwen/Qwen-Image , Lightning-LoRA https://huggingface.co/lightx2v/Qwen-Image-Lightning (Apache-2.0). Stand der Abfrage 02.11.2026.
- Ein Lauf, 1024 × 1024, zufälliger Startwert; als Ansichtsbogen (2 × 2) in einem Bild.

## Prompt

> Product reference sheet of one medieval wooden bucket, Cologne around 1400: stave bucket made of 14 vertical oak staves with thin visible joints, slightly tapered (wider at the top), two hand-forged dark iron hoops, one near the top and one near the bottom, a simple arched hand-forged iron bail handle attached with two iron ears. Old used weathered wood, grey-brown, water stains, slight rust on the iron edges. Four views of the same bucket arranged in a 2x2 grid: top view looking straight down into the empty bucket, front view, side view, three-quarter view from above. Orthographic, even soft studio light, plain neutral light grey background, realistic painted texture, no text, no labels, no people.

## Was davon ins Modell ging

Konische Form aus gut einer Handvoll Dauben mit Fugen, zwei Reifen nahe Rand und Fuß, Rost an den Reifenkanten, vergrautes Holz, Henkel als Bogen aus Rundeisen. Statt der Eisenösen der Vorlage greift der Henkel durch zwei längere Ohrdauben (übliche Bauart um 1400, weniger Dreiecke).
