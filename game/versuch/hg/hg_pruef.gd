# Versuch: GLB-Kopf prüfen (Flächen, Materialien, Texturen) – headless: godot --headless --path game -s res://versuch/hg/hg_pruef.gd -- <res://…glb>
extends SceneTree

func _init() -> void:
	var p: String = OS.get_cmdline_user_args()[0]
	var s: Node = (load(p) as PackedScene).instantiate()
	_zeigen(s, "")
	quit()

func _zeigen(n: Node, e: String) -> void:
	print(e, n.name, " ", n.get_class())
	if n is MeshInstance3D:
		var m: Mesh = n.mesh
		print(e, "  Formen ", m.get_blend_shape_count(), " AABB ", m.get_aabb())
		for i in m.get_surface_count():
			var mat := m.surface_get_material(i)
			var a := m.surface_get_arrays(i)
			var info := "%s %s v=%d" % [mat.resource_name, mat.get_class(), (a[Mesh.ARRAY_VERTEX] as PackedVector3Array).size()]
			if mat is BaseMaterial3D:
				var t: Texture2D = mat.albedo_texture
				info += " transp=%d alb=%s" % [mat.transparency, t.get_image().detect_alpha() if t else "-"]
			print(e, "  ", info)
	for c in n.get_children():
		_zeigen(c, e + "  ")
