"""Zugpferd, Folge 3: Umgebungsverdeckung (AO) des Fells aus der Ruhepose des Spiel-GLB backen (Cycles, nur das eigene Netz; Geschirr ausgelassen,
damit Kummet und Gurt keine festen Schatten ins Fell legen). Ergebnis: Feld 0..1 im Fellfeld des Atlas (UV oben 2048^2), Groesse --gr (Standard 1024).

    blender -b -t 2 --python tools/modelle/pferd3_ao.py -- --glb=<tier_pferd_*.glb> --aus=<ao.npy> [--gr=1024] [--proben=64] [--abstand=0.25]
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pferd2_bilder as pb  # noqa: E402
import pferd2_spielrender as R  # noqa: E402

OPT = pb.OPT


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    p = R.Pferd(OPT["glb"])
    Q, Nw = p.pose(None, 0)
    tv = p.UV[p.F].mean(1)
    geschirr = (tv[:, 0] >= 1024 / 2048) & (tv[:, 0] < 1536 / 2048) & (tv[:, 1] >= 2560 / 3072)
    p.F = p.F[~geschirr]
    gr = int(OPT.get("gr", 1024))
    szene = bpy.context.scene
    szene.render.engine = "CYCLES"
    szene.cycles.device = "CPU"
    szene.cycles.samples = int(OPT.get("proben", 64))
    szene.world = bpy.data.worlds.new("W")
    szene.world.light_settings.distance = float(OPT.get("abstand", 0.25))
    bild = bpy.data.images.new("ao", gr, gr * 3 // 2, alpha=False, float_buffer=True)
    m = bpy.data.materials.new("AO")
    m.use_nodes = True
    tn = m.node_tree.nodes.new("ShaderNodeTexImage")
    tn.image = bild
    m.node_tree.nodes.active = tn
    ob = R.netz("Pferd", p, Q, Nw, m)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    szene.render.bake.margin = 16
    bpy.ops.object.bake(type="AO")
    a = np.array(bild.pixels[:], np.float32).reshape(gr * 3 // 2, gr, 4)[::-1, :, 0]   # Blender: Zeile 0 unten
    np.save(OPT["aus"], a[:gr, :gr])                                   # als .npy (Blenders Python hat kein Pillow); pferd3_karten.py liest --ao=<datei.npy>
    print("AO", OPT["aus"], float(a[:gr, :gr].mean()))


if __name__ == "__main__":
    main()
