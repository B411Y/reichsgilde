# Human Generator aktivieren, Inhaltsordner setzen, Einstellungen speichern.
import bpy, os, addon_utils
addon_utils.enable("HumGen3D", default_set=True, persistent=True)
p = bpy.context.preferences.addons["HumGen3D"].preferences
p.filepath_ = os.path.expanduser("~/werkzeuge/humgen/inhalt") + os.sep
bpy.ops.wm.save_userpref()
print("HG-PFAD", p.filepath)
from HumGen3D.backend.content.content_packs import cpacks_refresh
try:
    cpacks_refresh(None, bpy.context)
except Exception as e:
    print("refresh", e)
print("PAKETE", [c.pack_name for c in bpy.context.scene.contentpacks_col] if hasattr(bpy.context.scene,'contentpacks_col') else '?')
