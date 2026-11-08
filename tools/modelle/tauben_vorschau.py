"""Nahaufnahme der Füllungs-Tauben (Posen und Farben nebeneinander) als docs/modelle/tauben_posen.png.

    blender -b --factory-startup --python tools/modelle/tauben_vorschau.py -- [--schnell]
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fuellung_p2 as p2  # noqa: E402
import fuellung_p4 as p4  # noqa: E402
import fuellung_vorschau as fv  # noqa: E402


def posen(netz, st, saat, winter=False):
    for i, (pose, farbe) in enumerate([("sitzen", "grau"), ("picken", "grau"), ("fluegel", "grau"), ("sitzen", "weiss"), ("picken", "scheckig"), ("sitzen", "scheckig")]):
        p4.taube(netz, st, -0.9 + 0.36 * i, 0.0, 0.0, 20, pose, farbe)


sommer, _ = p2.knoten([("Tauben", posen, (1,), {}, False)])
import bpy  # noqa: E402
import bausteine as bs  # noqa: E402

schnell = "--schnell" in sys.argv
szene = bpy.context.scene
bs.vorschau_buehne(None, ziel_hoehe=0.12, ausschnitt=2.6, schnell=schnell)
szene.cycles.samples = 24 if schnell else 64
szene.render.resolution_x, szene.render.resolution_y = 1600, 900
szene.render.resolution_percentage = 50 if schnell else 100
bs.blick(0)
fv._sonne_links()
fv.texturieren()
sommer[0].location = (0, 0, 0)
os.makedirs(bs.VORSCHAU, exist_ok=True)
pfad = os.path.join(bs.VORSCHAU, "tauben_posen.png")
fv._rendern(pfad, sommer)
print("Bild:", pfad)
