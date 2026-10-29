# Herkunft: Texturen des Realistic Water Shader

Normalkarten, Strömungskarte, Schaum und Kaustik-Atlas des **Realistic Water Shader** von **UnionBytes / Achim Menzel (AiYori)**
(https://github.com/godot-extended-libraries/godot-realistic-water, Ordner `realistic_water_shader/art/water/`, Commit `fa0fc478`, geholt am 29.10.2026).

- Lizenz: **MIT**. Lizenztext und Autorenliste liegen unverändert beim Shader: `game/assets/shader/realistisches_wasser/LICENSE.md` und `AUTHORS.md`.
- Die Dateien sind unverändert (Bytegleich mit denen im Port für Godot 4.0 von Hayden Connor). `Caustic.png` ist ein 4×4-Atlas aus 16 Bildern zu je 256 Pixeln; die Testszene (`game/scripts/Wassertest.cs`) schneidet sie zu einem Textur-Feld.
- Eine eigene Quellenangabe je Bild gibt es im Projekt nicht; das Protokoll vermerkt das.

Herkunft je Datei: `docs/assets/protokoll.csv`; Einzelheiten und Fehlerprotokoll: `docs/wasser/phase1.md`.
