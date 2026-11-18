# Briefe für Meldungen

```powershell
$env:BLENDER="C:\Blender_5.2\blender-5.2.3-windows-x64\blender.exe"
tools\modelle\handschuh\lauf.ps1 -Skript <repo>\tools\modelle\brief3d\brief_bau.py -Argumente "<ausordner>"   # schreibt brief_klein.glb, brief_gross.glb, .blend, messwerte_brief.json
python -I tools/modelle/brief3d/brief_pruefen.py <ausordner> <ausordner>/messwerte_brief.json
```

Die `.glb` kommen nach `game/assets/modelle/oberflaeche/` (Godot lagert die Bilder beim Import aus). Knoten, Clips, Maße, Streckung, Siegel: `docs/bereiche/darstellung.md` (Entscheidung „3D-Briefe für Meldungen“).

Bilder (nicht im Spiel, Sichtprüfung): `render_bilder.gd` rendert Briefe, Handschuh und Schriftrolle in einem kleinen Godot-Projekt (`godot --path <ordner> --rendering-driver vulkan --script res://render.gd -- <rohordner> <messwerte_brief.json>`; Pixel-Look 2 und 3 als Teilauflösung, hart hochgezogen; im Ordner liegen die `.glb` samt `--import`), `bilder_montage.py` beschriftet sie; das MP4 entsteht mit ffmpeg aus `video/f_%04d.png` (30/s, H.264, yuv420p). `render_bilder.gd` zeigt zugleich, wie Siegelfarbe, Symbol, Inhaltsfläche und Streckung im Code gesetzt werden.
