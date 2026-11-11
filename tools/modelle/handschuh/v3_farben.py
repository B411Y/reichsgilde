"""Beleg-Ansicht: Finger in Farben (Zeige rot, Mittel gelb, Ring gruen, Klein blau, Daumen weiss, Hand grau)."""
import bpy, sys, os, numpy as np
HIER = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HIER)
import v2_rig_lib as L, v2_posen
aus = sys.argv[sys.argv.index("--") + 1]; namen = sys.argv[sys.argv.index("--") + 2].split(",")
rig = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; gl = bpy.data.objects["Handschuh"]
farben = {"1": (0.95, 0.95, 0.95), "2": (0.9, 0.1, 0.1), "3": (0.95, 0.8, 0.1), "4": (0.1, 0.7, 0.2), "5": (0.15, 0.3, 0.95)}
gn = {g.index: g.name for g in gl.vertex_groups}
ca = gl.data.color_attributes.new("Debug", "FLOAT_COLOR", "POINT")
for v in gl.data.vertices:
    acc = {}
    for g in v.groups:
        n = gn[g.group]; k = n[6] if n.startswith("finger") else "0"; acc[k] = acc.get(k, 0) + g.weight
    k = max(acc.items(), key=lambda kv: kv[1])[0] if acc else "0"
    c = farben.get(k, (0.45, 0.45, 0.45)); ca.data[v.index].color = (*c, 1)
m = bpy.data.materials.new("Debug"); m.use_nodes = True; nt = m.node_tree
for n in list(nt.nodes): nt.nodes.remove(n)
cn = nt.nodes.new("ShaderNodeVertexColor"); cn.layer_name = "Debug"; b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.inputs["Roughness"].default_value = 0.8; o = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(cn.outputs["Color"], b.inputs["Base Color"]); nt.links.new(b.outputs["BSDF"], o.inputs["Surface"])
gl.data.materials.clear(); gl.data.materials.append(m)
for o_ in list(bpy.data.objects):
    if o_.name.startswith(("Handschuh_Umriss", "Hilfe")): o_.hide_render = True
cam = L.buehne(420); P = v2_posen.posen(rig); os.makedirs(aus, exist_ok=True)
for n in namen:
    L.pose_anwenden(rig, P[n]); bpy.context.view_layer.update()
    for a in ("oben", "unten", "vorn", "vorn_unten", "seite", "knoechel"):
        L.ansicht_rendern(cam, a, os.path.join(aus, f"{n}_{a}.png"))
