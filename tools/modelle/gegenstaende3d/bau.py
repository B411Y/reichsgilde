"""Baut die drei 3D-Gegenstaende (Kartentisch, Geldtruhe, Siegelstempel).
  blender -b --python bau.py -- <ausordner> <kartentisch|geldtruhe|siegelstempel|alle>
(unter Windows ueber tools/modelle/handschuh/lauf.ps1: Idle-Prioritaet; Pfade absolut.)
Schreibt je Gegenstand <ausordner>/<name>.glb (mit Clips), <name>.blend, messwerte_<name>.json und die Texturen als PNG."""
import importlib
import os
import sys

HIER = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HIER)
import gemeinsam  # noqa: E402

ALLE = ["kartentisch", "geldtruhe", "siegelstempel"]

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    aus = args[0]
    wahl = args[1] if len(args) > 1 else "alle"
    for name in (ALLE if wahl == "alle" else wahl.split(",")):
        modul = importlib.import_module(name)
        gemeinsam.erzeuge(modul, aus)
