# KI-Inhalte für die Steam-Angabe

Erzeugt von `python3 tools/assets/protokoll_pruefen.py --steam-liste` aus `docs/assets/protokoll.csv` – nicht von Hand ändern, sondern das Protokoll ändern und neu erzeugen. Die CI prüft, dass beide übereinstimmen.

**Keine Rechtsberatung.** Die Liste sammelt, was das Protokoll belegt; was dort „unbekannt“ ist, ist auch hier offen.

## Valves Regel

Steam fragt im Content Survey nach KI-Inhalten (https://partner.steamgames.com/doc/gettingstarted/contentsurvey, Abschnitt zu KI; überarbeitet im Januar 2026, siehe z. B. https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/):

- **Pre-Generated:** Inhalte, die während der Entwicklung mit KI-Werkzeugen erzeugt wurden, mit dem Spiel ausgeliefert und von Spielern wahrgenommen werden (Grafik, Modelle, Audio, Text). Sie werden offengelegt und wie andere Inhalte geprüft.
- **Live-Generated:** Inhalte, die KI erst während des Spiels erzeugt; dann sind zusätzlich Schutzmaßnahmen gegen illegale Inhalte anzugeben. Reichsgilde erzeugt nichts während des Spiels.
- Effizienzgewinne durch KI-Werkzeuge in der Entwicklung (z. B. Code-Assistenz) stehen laut Valve nicht im Mittelpunkt der Angabe. Die Skripte, Shader und Werkzeuge dieses Projekts sind mit Claude geschrieben; ob das angegeben werden soll, entscheidet der Nutzer.

Wortlaut der Regel vor der Angabe an der Quelle prüfen; die Zusammenfassung stammt aus einer Recherche vom 29.10.2026.

## Ausgeliefert und für Spieler sichtbar (Pre-Generated)

Zusammen: 1689 × Bild, 2 × Modell, 3 × Textur.

| Inhalt | Anzahl | Art | Werkzeug | Modell | Bemerkung |
|---|---|---|---|---|---|
| `game/assets/oberflaeche/portraets/mensch/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/; erste Fassung mit Gemini (37ecc8aa) vollständig ersetzt |
| `game/assets/oberflaeche/portraets/elb/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/ |
| `game/assets/oberflaeche/portraets/halbling/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/ |
| `game/assets/oberflaeche/portraets/zwerg/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/ |
| `game/assets/oberflaeche/portraets/ork/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/ |
| `game/assets/oberflaeche/portraets/oger/*.webp` | 260 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | unbekannt (Abfrage läuft) | 120 Bilder (10 Gesichter × 4 Altersstufen × schlicht/vornehm/„zu“) und 140 Masken (80 Augen, 60 Haar); Quellen in tools/portraets/quellen/ |
| `game/assets/oberflaeche/portraets/hintergrund_*.webp` | 4 | Bild | unbekannt (Abfrage läuft) | unbekannt (Abfrage läuft) | Hintergründe Gemeiner, Bürger, Patrizier, Adel. Werkzeug nicht belegt: PR #211 sagt pauschal „lokale Bild-KI“; Hinweise auf Gemini (Quellen vor dem Wechsel zu Qwen-Image, Kommentar HG_RAND in portraets_bauen.gd) |
| `game/assets/oberflaeche/zuege/*.png` | 22 | Bild | Google Gemini (App/Web), vom Nutzer selbst bedient | unbekannt (Abfrage läuft) | Ausnahme vom Holzschnitt-Stil (kräftig gemalt); Rohbilder nicht im Repository; ohne pilger und gelehrt (eigene Zeile) |
| `game/assets/oberflaeche/zuege/{pilger,gelehrt}.png` | 2 | Bild | ChatGPT-Bilderzeugung (OpenAI, App/Web), vom Nutzer selbst bedient | unbekannt (Abfrage läuft) | Gemini-Limit erreicht, daher ChatGPT; Rohbilder nicht im Repository |
| `game/assets/oberflaeche/icons/*.png` | 63 | Bild | Google Gemini: zuerst über Antigravity (Bild-Subagent), danach über die bezahlte Gemini-API | gemini-3.1-flash-image | Waren-Knöpfe; Rahmen, Hintergründe und Motive von Gemini (Teile in tools/icons/quellen/). Zugangsweg gemischt: zuerst Antigravity (Bild-Subagent, gemini-3.1-flash-image; Kontingent nach etwa 11 Bildern erschöpft, 26.10.2026), danach bezahlte Gemini-API (gemini-3.1-flash-image); welche Datei über welchen Weg entstand, ist nicht festgehalten. Ohne einrichtung_* (eigene Zeile) |
| `game/assets/oberflaeche/icons/einrichtung_*.png` | 38 | Bild | Motive: ComfyUI auf dem Grafik-PC des Nutzers (Qwen-Image, Qwen-Image-Edit); Rahmen und Hintergründe: Google Gemini über die API (aus #210) | Qwen: unbekannt; Gemini: gemini-3.1-flash-image | Einrichtungs-Icons des Talentbaums |
| `game/assets/modelle/figuren/ki/schmied_ki.glb` | 1 | Modell | TRELLIS.2 (Microsoft, ComfyUI auf dem Grafik-PC des Nutzers) → AssetFurnace (Retopologie 20.000 Dreiecke) → SkinTokens (Skelett, 52 Knochen) → Kimodo (NVIDIA, Gehen) | unbekannt (Abfrage läuft) | Ausgangsbild „schmied_01“: vom Nutzer mit Google Gemini erzeugt, nach Vorlage seines eigenen Gesichts (Bild nicht im Repository); Gesicht des Nutzers als Vorlage – mit seiner Zustimmung; DINOv3 (Bildencoder von TRELLIS.2) macht die Ausgaben laut HERKUNFT.md „kommerziell bedingt“; Kimodo-Checkpoint unbekannt (Kimodo-SMPLX-RP-v1 wäre nur für Forschung) |
| `game/assets/modelle/figuren/ki/schmied_ki_*.jpg` | 3 | Textur | TRELLIS.2 → AssetFurnace (Textur 2048 px) | unbekannt (Abfrage läuft) | Farbe, Normalen, Metall/Rauheit des KI-Schmieds |
| `game/assets/modelle/figuren/ki/schmied_ki_stehen.glb` | 1 | Modell | Kimodo (NVIDIA, Clip „idle“) auf dem SkinTokens-Skelett, über AssetFurnace | unbekannt (Abfrage läuft) | Kimodo-Checkpoint unbekannt (Kimodo-SMPLX-RP-v1 wäre nur für Forschung) |

## KI-Inhalte im Repository, die das Spiel nicht zeigt

Zwischenstufen unter `tools/`; das Spiel liefert sie nicht aus.

| Inhalt | Anzahl | Art | Werkzeug | Bemerkung |
|---|---|---|---|---|
| `tools/portraets/quellen/personen/*.webp` | 720 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | Zwischenstufe der Porträts aller sechs Völker (Kürzel m/f, em/ef, hm/hf, zm/zf, om/of, gm/gf) |
| `tools/portraets/quellen/masken/*.webp` | 480 | Bild | ComfyUI auf dem Grafik-PC des Nutzers: Qwen-Image (Text zu Bild), Bearbeiten-Workflow Qwen-Image-Edit (u. a. Farbvarianten für die Masken) | Zwischenstufe der Haar- und Augenmasken |
| `tools/portraets/quellen/hintergruende/*.webp` | 4 | Bild | unbekannt | Zwischenstufe der Porträt-Hintergründe; Werkzeug siehe Zeile der Spiel-Dateien |
| `tools/icons/quellen/rahmen/*.png` | 4 | Bild | Google Gemini: Antigravity (Bild-Subagent) und bezahlte Gemini-API, Zuordnung je Datei nicht festgehalten | Rahmen der Waren- und Einrichtungs-Knöpfe |
| `tools/icons/quellen/hintergrund/*.png` | 8 | Bild | Google Gemini: Antigravity (Bild-Subagent) und bezahlte Gemini-API, Zuordnung je Datei nicht festgehalten | Hintergründe der Waren- und Einrichtungs-Knöpfe |
| `tools/icons/quellen/motiv/*.png` | 63 | Bild | Google Gemini: Antigravity (Bild-Subagent) und bezahlte Gemini-API, Zuordnung je Datei nicht festgehalten | Motive der Waren-Knöpfe; ohne einrichtung_* (eigene Zeile) |
| `tools/icons/quellen/motiv/einrichtung_*.png` | 38 | Bild | ComfyUI auf dem Grafik-PC des Nutzers (Qwen-Image, Qwen-Image-Edit) | Motive der Einrichtungs-Icons |
