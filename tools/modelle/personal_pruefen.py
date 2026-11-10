"""Prüft die neue Einrichtung (personal_einrichtung.py) in den gebauten Raummodellen auf Überschneidungen: Welcher Anteil ihrer Grundfläche liegt auf Flächen
anderer Teile des Raums (Höhen 0,1 … 2,0 m über dem Boden, Wandflächen und Türen wie in personal_raumvorschau.py).

    blender -b --factory-startup --python tools/modelle/personal_pruefen.py -- --raum=schmied
"""

import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import innenausbau as ia  # noqa: E402
import personal_einrichtung as pe  # noqa: E402
import personal_raumvorschau as rv  # noqa: E402

# Raum → [(Schlüssel, Rechteck-Namen oder None für Werkraum, Name der Bodenhöhe oder None)]
RAEUME = {
    "schmied": [("Werk", None, None), ("Lager", None, "Z_LAGER")],
    "tischler": [("Werk", None, None), ("Lager", None, "Z_LAGER")],
    "steinmetz": [("Werk", None, None), ("Lager", None, "Z_LAGER")],
    "alchimist": [("Werk", None, None), ("Lager", None, "Z_LAGER"), ("Laden", "LX0,LX1,LY0,LY1", None)],
    "parfumeur": [("Werk", None, None), ("Lager", None, "Z_LAGER"), ("Laden", "LX0,LX1,LY0,LY1", None)],
    "wirt": [("Werk", None, None), ("Saal", None, "Z_SAAL")],
    "prediger": [("Werk", "SX0,SX1,SY0,SY1", None), ("Lager", "CX0,CX1,CY0,CY1", "Z_KRYPTA")],
    "dieb": [("Werk", None, None)], "gardist": [("Werk", None, None)], "geldleiher": [("Werk", None, None)],
}


def main():
    raum = rv.arg("raum")
    mod = importlib.import_module(f"{raum}_innen")
    objekte = mod.bauen()
    for schluessel, rect_namen, z_name in RAEUME[raum]:
        rect = tuple(getattr(mod, n) for n in rect_namen.split(",")) if rect_namen else (mod.WX0, mod.WX1, mod.WY0, mod.WY1)
        z0 = float(getattr(mod, z_name)) if z_name else 0.0
        r = rv.raster_bauen(raum, schluessel, rect, objekte, z0, ohne_pe=True)
        for o in bpy.context.scene.objects:
            if o.type != "MESH" or not o.name.startswith("Einrichtung_") or o.name[12:].split(".")[0] not in pe.ALLE or ia.raum_von(o) != schluessel:
                continue
            me = o.data
            eigene, belegt = set(), set()
            for p in me.polygons:
                pts = [o.matrix_world @ me.vertices[i].co for i in p.vertices]
                if max(q.z for q in pts) < z0 + 0.1 or min(q.z for q in pts) > z0 + 2.0:
                    continue
                for k in range(1, len(pts) - 1):
                    a, b, c = pts[0], pts[k], pts[k + 1]
                    n = max(2, int(max((a - b).length, (b - c).length, (a - c).length) / rv.ZELLE) + 1)
                    for s in range(n + 1):
                        for t in range(n + 1 - s):
                            q = a + (b - a) * (s / n) + (c - a) * (t / n)
                            i, j = r.zelle(q.x, q.y)
                            if 0 <= i < r.nx and 0 <= j < r.ny:
                                eigene.add((i, j))
                                if r.belegt[i][j]:
                                    belegt.add((i, j))
            wer = {}
            for z in belegt:
                wer[r.quelle.get(z, "?")] = wer.get(r.quelle.get(z, "?"), 0) + 1
            print(f"PRUEF {raum} {schluessel} {o.name}: {len(eigene)} Zellen, davon auf anderen Teilen {len(belegt)} ({100 * len(belegt) / max(1, len(eigene)):.0f} %) {wer}")


main()
