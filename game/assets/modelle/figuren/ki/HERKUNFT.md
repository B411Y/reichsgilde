# Herkunft: KI-Schmied

`schmied_ki.glb` ist ein mit KI erzeugtes Modell eines Schmieds (Bild des Nutzers). Die Werkzeugkette läuft außerhalb des Repositorys:

| Schritt | Werkzeug | Lizenz und Hinweis |
|---|---|---|
| Netz und Textur aus dem Bild | TRELLIS.2 (Microsoft) | MIT; der Bildencoder DINOv3 steht unter der DINOv3-Lizenz, die Ausgaben sind laut Lizenz kommerziell nutzbar (bedingt) |
| Retopologie auf 20.000 Dreiecke, Textur 2048 | AssetFurnace | Apache-2.0 |
| Skelett (52 Knochen) und Gewichte | SkinTokens | MIT |
| Gehen-Animation | Kimodo (NVIDIA) | NVIDIA Open Model License; sie erhebt keine Ansprüche an die Ausgaben |

Bearbeitung im Projekt: Die beiden großen Texturen (Albedo, Normalen) sind von 2048 auf 1024 Pixel verkleinert (Bildvergleich im Spiel: kein sichtbarer Unterschied, 12,6 MB statt 5,2 MB im Grafikspeicher); Mesh, Skelett und Animation sind unverändert. Das Modell steht wie die übrigen Inhalte unter CC BY 4.0 (`LICENSE-INHALTE.md`).

Weitere Bewegungen (Stehen, Hämmern …) kommen als `schmied_ki_<bewegung>.glb` mit demselben Skelett in diesen Ordner; das Spiel nimmt sie beim Start automatisch auf (`Stadtansicht.KiFiguren.cs`).

`schmied_ki_stehen.glb` ist der Kimodo-Clip „idle“ für dasselbe Skelett; die Datei enthält nur Skelett und Animation (Netz, Material und Texturen entfernt).

Versionen (Stand 29.10.2026): ComfyUI 3D v0.43.0 mit TRELLIS.2-Knoten, AssetFurnace v0.5.3, SkinTokens 273b691d, Animationen aus Kimodo-SOMA-RP-v1.1 (kommerziell nutzbar; nicht die Forschungsvariante SMPLX-RP). Einzelheiten: `docs/assets/prompts/ki_schmied.md`.

Herkunft je Datei aller Assets (auch dieser): `docs/assets/protokoll.csv`.
