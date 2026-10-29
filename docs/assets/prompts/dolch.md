# Prompt: Waren-Knopf Dolch (PR #232, 29.10.2026)

Datei: `game/assets/oberflaeche/icons/dolch.png`, Motiv `tools/icons/quellen/motiv/dolch.png` (kommen mit PR #232). Protokoll: `docs/assets/protokoll.csv` (Zeile kommt mit PR #232).

## Werkzeug

- Lokale Bild-KI auf dem Grafik-PC des Nutzers: ComfyUI, Bearbeiten-Workflow (**Qwen-Image-Edit**), über ein Aufrufskript außerhalb des Repositorys. Modellversion: unbekannt.
- Vorlage: das Waren-Motiv `tools/icons/quellen/motiv/rapier.png` (Gemini, PR #210) auf weißem Grund, 1024 px.
- Zwei Läufe, gewählt wurde Lauf 2.
- Danach `tools/icons/knoepfe_bauen.gd` (`quellen`, dann `bauen dolch`): Rahmen Stein, Hintergrund Glutrot – beide aus PR #210 (Gemini).

## Prompt

> Replace the rapier with a late-medieval dagger (rondel dagger): a short straight double-edged blade about as long as the grip, a simple small iron crossguard, wooden grip wrapped with leather, round disc pommel. No basket hilt, no long blade, no curved guard. Keep exactly the same drawing style: hand-colored woodcut with black ink outline, cross-hatching in the shadows, light watercolor, same colors, vertical, centered, plain white background, no text.
