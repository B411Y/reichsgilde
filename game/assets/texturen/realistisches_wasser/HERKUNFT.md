# Herkunft: Texturen des Wassers

Die fünf Bilder sind **eigen** (29.10.2026 ersetzen sie die Bilder des Original-Pakets, die keine belegte Einzelquelle hatten):
`wasser_normal_a.png`, `wasser_normal_b.png` (Normalkarten), `wasser_stroemung.png` (Verzerrung), `wasser_schaum.png`, `wasser_kaustik.png` (4×4-Atlas, 16 Bilder zu 256 Pixeln).

- Erzeugt von `tools/wasser/wasser_texturen.py` (Python mit numpy, ausgeführt in Blender 5.2; fester Startwert, daher jederzeit neu erzeugbar). Kein KI-Bild, keine fremde Vorlage.
- Lizenz: Alle Rechte vorbehalten (neue Inhalte, siehe `LICENSE-INHALTE.md`).
- **Nur der Shader** (`game/assets/shader/realistisches_wasser/Water_Original.gdshader`) stammt aus dem Realistic Water Shader von UnionBytes / Achim Menzel (AiYori), MIT; Lizenztext und Autorenliste liegen dort bei `LICENSE.md` und `AUTHORS.md`.

Herkunft je Datei: `docs/assets/protokoll.csv`; Einzelheiten zur Übernahme: `docs/wasser/phase1.md`.
