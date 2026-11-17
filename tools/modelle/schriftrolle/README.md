# Schriftrolle für Meldungen

```powershell
$env:BLENDER="C:\Blender_5.2\blender-5.2.3-windows-x64\blender.exe"
tools\modelle\handschuh\lauf.ps1 -Skript <repo>\tools\modelle\schriftrolle\schriftrolle_bau.py -Argumente "<ausordner>"   # schreibt schriftrolle.glb, .blend, messwerte_schriftrolle.json
python -I tools/modelle/schriftrolle/schriftrolle_pruefen.py game/assets/modelle/oberflaeche/schriftrolle.glb <ausordner>/messwerte_schriftrolle.json
```

Die `.glb` kommt nach `game/assets/modelle/oberflaeche/schriftrolle.glb` (Godot lagert die Bilder beim Import aus). Knoten, Clips, Maße: `docs/bereiche/darstellung.md` (Entscheidung „3D-Schriftrolle für Meldungen“).

Bilder (nicht im Spiel, Sichtprüfung): `render_bilder.gd` rendert Rolle und Handschuh in einem kleinen Godot-Projekt (`godot --path <ordner> --rendering-driver vulkan --script res://render.gd -- <ausordner>`; Pixel-Look 2 und 3 als Teilauflösung, hart hochgezogen), `bilder_montage.py` beschriftet sie; das MP4 entsteht mit ffmpeg aus `video/f_%04d.png` (30/s, H.264, yuv420p).
