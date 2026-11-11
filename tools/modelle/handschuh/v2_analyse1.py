import bpy,sys,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import v2_lib
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[sys.argv.index("--")+1])
ob=[o for o in bpy.context.scene.objects if o.type=="MESH"][0]
bpy.context.view_layer.objects.active=ob
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.remove_doubles(threshold=1e-5); bpy.ops.object.mode_set(mode="OBJECT")
print("MESS",v2_lib.netz_messen(ob)); print("SELBST",v2_lib.selbstschnitt(ob))
