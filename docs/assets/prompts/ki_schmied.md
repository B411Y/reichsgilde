# Ablauf: KI-Schmied (PR #225, 28.10.2026)

Dateien: `game/assets/modelle/figuren/ki/` (`schmied_ki.glb`, `schmied_ki_stehen.glb`, drei Texturen). Protokoll: `docs/assets/protokoll.csv`; Lizenzen der Werkzeuge: `game/assets/modelle/figuren/ki/HERKUNFT.md`.

Kein Text-Prompt: Das Modell entstand aus einem Bild.

1. **Ausgangsbild:** „schmied_01“ aus dem Eingabeordner des Nutzers. Der Nutzer hat es mit **Google Gemini** erzeugt, nach der Vorlage seines eigenen Gesichts (mit seiner Zustimmung). Das Bild liegt nicht im Repository.
2. **TRELLIS.2** (Microsoft) über ComfyUI auf dem Grafik-PC des Nutzers: Netz und Textur aus dem Bild.
3. **AssetFurnace** (laut Notiz des Teamleiters: Finish, Skelett, Bewegungen): Retopologie auf 20.000 Dreiecke, Textur 2048 px; Skelett (52 Knochen) von **SkinTokens**, Bewegungen „walk“ und „idle“ von **Kimodo** (NVIDIA) (laut `HERKUNFT.md`). Ob SkinTokens und Kimodo innerhalb von AssetFurnace oder getrennt liefen, ist nicht belegt.
4. Im Projekt: Texturen von 2048 auf 1024 px verkleinert; `schmied_ki_stehen.glb` (Clip „idle“) per Skript auf Skelett und Animation gekürzt (Skript nicht im Repository). Das Hämmern ist kein Kimodo-Clip, sondern im Code gebaut (`Stadtansicht.KiFiguren.cs`).

Modellversionen (Stand der Abfrage 29.10.2026): ComfyUI 3D v0.43.0 (Commit 426f5eea) mit den TRELLIS.2-Knoten (`comfy_extras/nodes_trellis2.py`) und den Modellen trellis_2_int8_convrot, trellis_2_shape_vae_bf16, trellis_2_texture_vae_bf16, dino_v3_L_naf_fp32 (Quelle unbekannt, DINOv3-Lizenz von Meta beachten), birefnet; AssetFurnace v0.5.3 (Commit 2cf9c7b7, Apache-2.0; Pixal3D liegt darin, wurde nicht benutzt); SkinTokens Commit 273b691d mit grpo_1400.ckpt und last.ckpt (VAST-AI/SkinTokens, Revision 79736cad, MIT); Animationen: fertige Clips aus Kimodo-SOMA-RP-v1.1 (NVIDIA Open Model License, „ready for commercial use“), nicht die Forschungsvariante SMPLX-RP.
