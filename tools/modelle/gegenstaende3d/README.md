# Drei 3D-Gegenstände: Kartentisch, Geldtruhe, Siegelstempel

```powershell
$env:BLENDER="C:\Blender_5.2\blender-5.2.3-windows-x64\blender.exe"
tools\modelle\handschuh\lauf.ps1 -Skript <repo>\tools\modelle\gegenstaende3d\bau.py -Argumente "<bauordner> alle"   # oder kartentisch | geldtruhe | siegelstempel
python -I tools/modelle/gegenstaende3d/pruefen.py game/assets/modelle/oberflaeche <bauordner> --gegenprobe       # prüft die .glb und die Messwerte
```

`bau.py` schreibt je Gegenstand `<name>.glb`, `<name>.blend`, `messwerte_<name>.json` (Dreiecke, Clips, Neigung, Hülle über alle Bilder, CustomAabb, Zeiten und Schwellen) und die Texturen nach `<bauordner>`; die `.glb` kommen nach `game/assets/modelle/oberflaeche/` (Godot lagert die Bilder beim Import als `<name>_<bild>.png` aus). Gemeinsame Teile (Teil-Klasse, Drehkörper, Umriss-Hülle, Gerüst, Clips, Messung, Export): `gemeinsam.py`; je Gegenstand ein Modul mit Maßen, Knochen und Clips (`kartentisch.py`, `geldtruhe.py`, `siegelstempel.py`); Texturen: `texturen.py`. Knoten, Clips, Füllstufen, Schwellen, CustomAabb: `docs/bereiche/darstellung.md` (Entscheidung „3D-Gegenstände“).

Prüfung in Godot (ohne Fenster): ein kleines Projekt mit den `.glb` unter `res://assets/modelle/oberflaeche/`, einmal `godot --headless --path <projekt> --import`, dann `godot --headless --path <projekt> --script res://godot_pruefen.gd`. Das Skript zeigt zugleich, wie die Oberfläche das Umriss-Material setzt, die Füllstufen der Truhe umschaltet und Knochenlagen abfragt.

Bilder und Videos (nicht im Spiel, Sichtprüfung): `bilder.py` rendert in Blender (`-b`, EEVEE) aus den `.blend` (`blender -b --python bilder.py -- <bauordner> <rohordner> alle [video]`; Pixel 2 und 3 als Teilauflösung), `montage.py` beschriftet und setzt sie zusammen (Vorschau, Zustände, Clipphasen, Pixel-Vergleich, hart hochgezogen); die MP4 entstehen mit ffmpeg aus `video_<name>/f_%04d.png` (30/s, H.264, yuv420p).
