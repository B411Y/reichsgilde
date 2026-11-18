# Drei 3D-Bücher für die Innenansichten (Personal, Familie, Kontor) und ihre Pulte

```powershell
$env:BLENDER="C:\Blender_5.2\blender-5.2.3-windows-x64\blender.exe"
tools\modelle\handschuh\lauf.ps1 -Skript <repo>\tools\modelle\buecher3d\buch_bau.py -Argumente "<ausordner> alle"   # oder personal | familie | kontor
python -I tools/modelle/buecher3d/buch_pruefen.py game/assets/modelle/oberflaeche <ausordner>                       # prüft die .glb und die Messwerte
tools\modelle\handschuh\lauf.ps1 -Skript <repo>\tools\modelle\buecher3d\moebel_bau.py -Argumente ""                 # lesepult.glb und schreibpult.glb
```

`buch_bau.py` schreibt `buch_<art>.glb`, `.blend` und `messwerte_buch_<art>.json` (Dreiecke, Maße, Zeiten, Neigung) nach `<ausordner>`; die `.glb` kommen nach `game/assets/modelle/oberflaeche/` (Godot lagert die Bilder beim Import aus), die Pulte schreibt `moebel_bau.py` selbst nach `game/assets/modelle/requisiten/`. Formen und Maße je Buch: `buch_stile.py`; Texturen: `buch_texturen.py`. Knoten, Clips, Inhaltsflächen, Auflagepunkte: `docs/bereiche/darstellung.md` (Entscheidung „3D-Bücher für Innenansichten“).

Bilder und Videos (nicht im Spiel, Sichtprüfung): `render_bilder.gd` rendert in einem kleinen Godot-Projekt (`godot --path <ordner> --rendering-driver vulkan --script res://render.gd -- <rohordner> <messordner> [video]`; Pixel-Look 2 und 3 als Teilauflösung, hart hochgezogen; im Ordner liegen die `.glb` samt `--import`), `bilder_montage.py` beschriftet und nummeriert sie; die MP4 entstehen mit ffmpeg aus `video_<art>/f_%04d.png` (30/s, H.264, yuv420p). `render_bilder.gd` zeigt zugleich, wie Umriss-Material, Inhaltsflächen, Wappenfeld und die Sichtbarkeit der Inhaltsflächen nach den Zeiten der Messwerte im Code gesetzt werden.
