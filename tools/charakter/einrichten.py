# MPFB als Erweiterung einschalten, Datenordner setzen, Einstellungen speichern (läuft in Blender).
import bpy, addon_utils, sys

daten = sys.argv[sys.argv.index("--") + 1]
addon_utils.enable("bl_ext.user_default.mpfb", default_set=True, persistent=True)
mpfb = sys.modules["bl_ext.user_default.mpfb"]
mpfb.set_preference("mpfb_user_data", daten)
mpfb.set_preference("mh_auto_user_data", False)
bpy.ops.wm.save_userpref()
print("MPFB", mpfb.VERSION, "eingerichtet, Daten:", daten)
