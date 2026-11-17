# Herkunft der Stoff-Texturen

| Datei | Quelle | Lizenz |
|---|---|---|
| `stoff_albedo_grau.png`, `stoff_normal.png`, `stoff_rauheit.png` | ambientCG, „Fabric 045“ (https://ambientcg.com/view?id=Fabric045), Dateien `Fabric045_1K-JPG_Color.jpg`, `…_NormalGL.jpg`, `…_Roughness.jpg` aus `Fabric045_1K-JPG.zip`, heruntergeladen am 17.11.2026 (ZIP SHA-256 `1e10799d505fcd239a0ae46b63e605ed8e3a243e966ea5fc795bb7cfdcbf4305`); mit `tools/oberflaeche/stofftextur.py` auf 512 × 512 verkleinert, Farbe zu Graustufen (Mittel 0,80, Kontrast × 2,2), Rauheit angehoben (Mittel 0,84), Normalkarte nur verkleinert | CC0 1.0 (https://docs.ambientcg.com/license/) |

Verwendet für den Wandbehang der Steinhaus-Stube (`game/scripts/Stadtansicht.Behang.cs`, `BehangStoffGeben`): Die graue Farbe wird mit dem gedämpften Hausfarbton (`Behangfarbe.cs`) malgenommen.
