# Erkundung: Mensch anlegen, Vertexgruppen und Objekte auflisten.
import bpy, sys
mp = "bl_ext.user_default.mpfb"
from importlib import import_module
HumanService = import_module(mp + ".services.humanservice").HumanService
AssetService = import_module(mp + ".services.assetservice").AssetService
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
b = HumanService.create_human()
print("OBJ", b.name, len(b.data.vertices), len(b.data.polygons))
print("VG", [g.name for g in b.vertex_groups])
print("SK", [k.name for k in b.data.shape_keys.key_blocks] if b.data.shape_keys else None)
print("MOD", [(m.name, m.type) for m in b.modifiers])
for sub in ["eyes", "eyebrows", "eyelashes", "hair", "clothes", "skins", "proxymeshes"]:
    print("ASSET", sub, AssetService.list_mhclo_assets(sub)[:3] if hasattr(AssetService, "list_mhclo_assets") else "")
