import bpy,sys,os,bmesh
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import v2_lib
glb,aus,ziel,tex,vox,modus=sys.argv[sys.argv.index("--")+1:][:6]
ziel=int(ziel); tex=int(tex); vox=float(vox)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
hi=[o for o in bpy.context.scene.objects if o.type=="MESH"][0]; hi.name="hoch"
def sel(o):
    bpy.ops.object.select_all(action="DESELECT"); bpy.context.view_layer.objects.active=o; o.select_set(True)
lo=hi.copy(); lo.data=hi.data.copy(); lo.name="niedrig"; bpy.context.scene.collection.objects.link(lo)
lo.data.materials.clear(); sel(lo)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.remove_doubles(threshold=0.00005); bpy.ops.object.mode_set(mode="OBJECT")
lo.data.remesh_voxel_size=vox; lo.data.remesh_voxel_adaptivity=0.0; bpy.ops.object.voxel_remesh()
print("NACHVOXEL",len(lo.data.polygons))
# lose Inseln weg (nur groesste behalten), Fransen glaetten
def inseln_weg():
    sel(lo); bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.separate(type="LOOSE"); bpy.ops.object.mode_set(mode="OBJECT")
    teile=sorted([o for o in bpy.context.scene.objects if o.type=="MESH" and o.name.startswith("niedrig")],key=lambda o:-len(o.data.polygons))
    print("INSELN",len(teile),[len(o.data.polygons) for o in teile[:5]])
    for o in teile[1:]: bpy.data.objects.remove(o,do_unlink=True)
    return teile[0]
lo=inseln_weg(); lo.name="niedrig"
m=lo.modifiers.new("s","SMOOTH"); m.factor=0.6; m.iterations=int(os.environ.get("GLAETTEN","4")); bpy.ops.object.modifier_apply(modifier=m.name)
ok=False
if modus=="quad":
    sel(lo)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.remove_doubles(threshold=1e-6); bpy.ops.mesh.fill_holes(sides=8)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.mesh.select_all(action="DESELECT"); bpy.ops.mesh.select_non_manifold(extend=False,use_wire=True,use_boundary=True,use_multi_face=True,use_non_contiguous=True,use_verts=True)
    bpy.ops.object.mode_set(mode="OBJECT"); nmv=len([v for v in lo.data.vertices if v.select]); print("NONMANIFOLD_VERTS",nmv)
    bpy.ops.object.mode_set(mode="EDIT")
    if nmv: bpy.ops.mesh.delete(type="VERT"); bpy.ops.mesh.fill_holes(sides=8)
    bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    try:
        bpy.ops.object.quadriflow_remesh(use_mesh_symmetry=False,use_preserve_sharp=False,use_preserve_boundary=False,preserve_attributes=False,smooth_normals=True,mode="FACES",target_faces=ziel//2,seed=1)
        ok=len(lo.data.polygons)<ziel
    except Exception as e: print("QUADFEHLER",e)
    print("QUAD",ok,len(lo.data.polygons))
if not ok:
    for _ in range(5):
        n=len(lo.data.polygons)
        if n<=ziel*1.05: break
        m=lo.modifiers.new("d","DECIMATE"); m.ratio=max(0.01,ziel/n*0.97); m.use_collapse_triangulate=True; bpy.ops.object.modifier_apply(modifier=m.name)
    print("DECIMATE",len(lo.data.polygons))
lo=inseln_weg(); lo.name="niedrig"
sel(lo); bpy.ops.object.shade_smooth()
print("MESS",v2_lib.netz_messen(lo))
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=1.45,island_margin=0.003); bpy.ops.object.mode_set(mode="OBJECT")
sc=bpy.context.scene; sc.render.engine="CYCLES"; sc.cycles.device="CPU"; sc.cycles.samples=1
img=bpy.data.images.new("farbe",tex,tex)
mat=bpy.data.materials.new("handschuh"); mat.use_nodes=True; nt=mat.node_tree
tn=nt.nodes.new("ShaderNodeTexImage"); tn.image=img
nt.nodes.active=tn; lo.data.materials.append(mat)
bpy.ops.object.select_all(action="DESELECT"); hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active=lo
bpy.ops.object.bake(type="DIFFUSE",pass_filter={"COLOR"},use_selected_to_active=True,cage_extrusion=0.02,max_ray_distance=0.05,margin=12,margin_type="EXTEND")
img.filepath_raw=aus+"_farbe.png"; img.file_format="PNG"; img.save()
bpy.data.objects.remove(hi,do_unlink=True)
# Dreiecke und Export (Material wird im Rigbau neu gesetzt)
sel(lo); bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.object.mode_set(mode="OBJECT")
import json
uvl=lo.data.uv_layers.active.data; fl=[]
for p_ in lo.data.polygons:
    c=p_.center; n=p_.normal
    fl.append(dict(uv=[[uvl[i].uv.x,uvl[i].uv.y] for i in p_.loop_indices],n=[round(n.x,3),round(n.y,3),round(n.z,3)],c=[round(c.x,3),round(c.y,3),round(c.z,3)]))
json.dump(fl,open(aus+"_flaechen.json","w"))
print("FINAL TRI",len(lo.data.polygons),"VERTS",len(lo.data.vertices))
img.pack(); bpy.ops.export_scene.gltf(filepath=aus+".glb",export_format="GLB")

