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
