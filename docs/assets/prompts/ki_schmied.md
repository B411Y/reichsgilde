# Ablauf: KI-Schmied (PR #225, 28.10.2026)

Dateien: `game/assets/modelle/figuren/ki/` (`schmied_ki.glb`, `schmied_ki_stehen.glb`, drei Texturen). Protokoll: `docs/assets/protokoll.csv`; Lizenzen der Werkzeuge: `game/assets/modelle/figuren/ki/HERKUNFT.md`.

Kein Text-Prompt: Das Modell entstand aus einem Bild.

1. **Ausgangsbild:** „schmied_01“ aus dem Eingabeordner des Nutzers. Wie dieses Bild entstand (Foto, Zeichnung, Bild-KI), ist **unbekannt** – beim Nutzer zu erfragen. Es liegt nicht im Repository.
2. **TRELLIS.2** (Microsoft) über ComfyUI auf dem Grafik-PC des Nutzers: Netz und Textur aus dem Bild.
3. **AssetFurnace** (laut Notiz des Teamleiters: Finish, Skelett, Bewegungen): Retopologie auf 20.000 Dreiecke, Textur 2048 px; Skelett (52 Knochen) von **SkinTokens**, Bewegungen „walk“ und „idle“ von **Kimodo** (NVIDIA) (laut `HERKUNFT.md`). Ob SkinTokens und Kimodo innerhalb von AssetFurnace oder getrennt liefen, ist nicht belegt.
4. Im Projekt: Texturen von 2048 auf 1024 px verkleinert; `schmied_ki_stehen.glb` (Clip „idle“) per Skript auf Skelett und Animation gekürzt (Skript nicht im Repository). Das Hämmern ist kein Kimodo-Clip, sondern im Code gebaut (`Stadtansicht.KiFiguren.cs`).

Modellversionen (TRELLIS.2-Gewichte, SkinTokens, Kimodo-Checkpoint): unbekannt. Beim Kimodo-Checkpoint kommt es auf die Lizenz an: Die meisten stehen unter der NVIDIA Open Model License, `Kimodo-SMPLX-RP-v1` nur für Forschung (siehe Protokoll, Bemerkung).
