# Versuch: HG-Frisur und -Bart als TressFX-Strähnen (Addon addons/tressfx, nur lokal installiert, nicht im Repository).
# Erzeugt die Knoten zur Laufzeit, damit die Testszene ohne das Addon weiter lädt.
# .tfx/.tfxbone aus tools/versuch_hg/hg_tfx.py liegen in res://versuch/hg/tfx/ (ebenfalls nur lokal).
class_name HgTressFX
extends RefCounted

static func verfuegbar() -> bool:
	return ClassDB.class_exists("TressFXHair")

# Strähnen an einen geladenen HgKopf hängen; Haarkarten von Frisur und Bart ausblenden (Wimpern bleiben)
static func anbringen(kopf: HgKopf, ordner: String, farbe: Color) -> Array:
	var knoten := []
	if not verfuegbar():
		push_warning("TressFX nicht installiert")
		return knoten
	var sk := kopf.skelett
	_karten_ausblenden(kopf)
	# Kopf als Kollisionskörper: Kugel am Kopfknochen (bewegt sich mit)
	var anh := BoneAttachment3D.new()
	anh.bone_name = "head"
	sk.add_child(anh)
	var kol: Node3D = ClassDB.instantiate("TressFXCollisionMesh")
	var kugel := SphereMesh.new()
	kugel.radius = 0.083
	kugel.height = 0.17
	kol.set("mesh", kugel)
	kol.set("num_cells_x", 48)
	kol.position = Vector3(0, 0.085, -0.01)
	anh.add_child(kol)
	knoten.append(kol)
	for teil in ["frisur", "bart"]:
		var pfad := ordner.path_join(teil + ".tfx")
		if not FileAccess.file_exists(pfad):
			continue
		var haar: Node3D = ClassDB.instantiate("TressFXHair")
		sk.get_parent().add_child(haar)
		haar.set("hair_skeleton", haar.get_path_to(sk))
		haar.set("tfxbone_path", ordner.path_join(teil + ".tfxbone"))
		haar.set("tfx_path", pfad)
		var m := ShaderMaterial.new()
		m.shader = load("res://addons/tressfx/shaders/tressfx_strand.gdshader")
		m.set_shader_parameter("base_color", Color(farbe.r, farbe.g, farbe.b, 0.63))
		m.set_shader_parameter("tip_color", Color(farbe.r * 1.25, farbe.g * 1.2, farbe.b * 1.15, 0.55))
		m.set_shader_parameter("tip_percentage", 0.5)
		m.set_shader_parameter("fiber_radius", 0.0011 if teil == "frisur" else 0.0007)
		m.set_shader_parameter("k_diffuse", 0.22)
		m.set_shader_parameter("k_spec1", 0.012)
		m.set_shader_parameter("k_spec2", 0.12)
		m.set_shader_parameter("root_shadow", 0.55)
		haar.set("material", m)
		haar.set("collision_meshes", [kol])  # die Erweiterung erwartet die Knoten selbst, keine Pfade
		haar.set("collision_skip_root_vertices", 5 if teil == "frisur" else 3)
		if teil == "frisur":
			haar.set("num_follow_hairs", 1)
			haar.set("local_stiffness", 0.3)
			haar.set("global_stiffness", 0.2)
			haar.set("global_range", 0.5)
			haar.set("gravity", 1.0)
			haar.set("damping", 0.2)
		else:
			# Bart: kurz und steif, folgt nur dem Kiefer
			haar.set("local_stiffness", 0.9)
			haar.set("global_stiffness", 0.9)
			haar.set("global_range", 1.0)
			haar.set("vsp_coeff", 0.9)
			haar.set("gravity", 0.1)
			haar.set("damping", 0.3)
		knoten.append(haar)
	kopf.tressfx_aktiv = true
	return knoten

static func _karten_ausblenden(kopf: HgKopf) -> void:
	var m := kopf.netz.mesh
	for i in m.get_surface_count():
		var mat := m.surface_get_material(i)
		if mat == null or not mat.resource_name.begins_with("Haar"):
			continue
		var a := m.surface_get_arrays(i)
		var pts: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
		var box := AABB(pts[0], Vector3.ZERO)
		for p in pts:
			box = box.expand(p)
		if box.size.y < 0.035:
			continue   # Wimpern
		var weg := StandardMaterial3D.new()
		weg.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		weg.albedo_color = Color(0, 0, 0, 0)
		kopf.netz.set_surface_override_material(i, weg)
