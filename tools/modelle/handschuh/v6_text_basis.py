"""Baut die Text-Fassung als Blend-Datei: fuehrt text_aus_blend.py aus (Pose des Nutzers, 15 Aktionen, Anker_Text, Export) und speichert danach die Szene
als <ausordner>/handschuh_pixal3d_v6_text.blend (Ausgangspunkt fuer v6_klinke.py).
blender -b handschuh_pixal3d_v6.blend --python v6_text_basis.py -- <ausordner> <text_haltung_fertig.blend> [bilder]"""
import bpy, sys, os, runpy
HIER = os.path.dirname(os.path.abspath(__file__))
aus = sys.argv[sys.argv.index("--") + 1]
runpy.run_path(os.path.join(HIER, "text_aus_blend.py"), run_name="__main__")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(aus, "handschuh_pixal3d_v6_text.blend"))
print("TEXT_BASIS gespeichert")
