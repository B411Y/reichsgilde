# Versuch: HG-Frisur und -Bart als TressFX-Strähnen (Addon addons/tressfx, nur lokal installiert, nicht im Repository).
# Erzeugt die Knoten zur Laufzeit, damit die Testszene ohne das Addon weiter lädt.
# .tfx/.tfxbone aus tools/versuch_hg/hg_tfx.py liegen in res://versuch/hg/tfx/<kopf>/ (ebenfalls nur lokal):
#   frisur.tfx, bart.tfx            alle Leitsträhnen (16 bzw. 8 Punkte)
#   frisur_sparsam.tfx, bart_…      ein Viertel der Leitsträhnen (8 bzw. 4 Punkte), Lücken füllen Folgehaare
class_name HgTressFX
extends RefCounted

static func verfuegbar() -> bool:
	return ClassDB.class_exists("TressFXHair")

# Strähnen an einen geladenen HgKopf hängen; Haarkarten von Frisur und Bart ausblenden (Wimpern, Brauen bleiben).
# variante: "" (voll) oder "sparsam"; ruhe: Simulation aus (Haar steht in der Ruheform, für viele kleine Fenster)
static func anbringen(kopf: HgKopf, ordner: String, farbe: Color, variante := "", ruhe := false) -> Array:
	var knoten := []
	if not verfuegbar():
		push_warning("TressFX nicht installiert")
		return knoten
	var sk := kopf.skelett
	# Haarfarbe aus der HG-Frisur (Mittel der deckenden Pixel der Haarkarten-Textur), sonst die übergebene
	var aus_karten := Color(0, 0, 0, 0) if _schalter("--tfx-ohne-farbe") else _kartenfarbe(kopf)
	if aus_karten.a > 0.0:
		farbe = aus_karten
	_karten_ausblenden(kopf)
	var kol := _kopf_kollider(kopf)
	knoten.append(kol)
	var endung := "" if variante == "" else "_" + variante
	var sparsam := variante == "sparsam"
	for teil in ["frisur", "bart"]:
		var pfad := ordner.path_join(teil + endung + ".tfx")
		if not FileAccess.file_exists(pfad):
			continue
		var haar: Node3D = ClassDB.instantiate("TressFXHair")
		sk.get_parent().add_child(haar)
		haar.set("hair_skeleton", haar.get_path_to(sk))
		haar.set("tfxbone_path", ordner.path_join(teil + endung + ".tfxbone"))
		haar.set("tfx_path", pfad)
		var m := ShaderMaterial.new()
		m.shader = load("res://addons/tressfx/shaders/tressfx_strand.gdshader")
		var spitze := Color(minf(farbe.r * 1.35, 1), minf(farbe.g * 1.3, 1), minf(farbe.b * 1.25, 1))
		if teil == "frisur":
			m.set_shader_parameter("base_color", Color(farbe.r * 0.85, farbe.g * 0.85, farbe.b * 0.85, 0.6))
			m.set_shader_parameter("tip_color", Color(spitze.r, spitze.g, spitze.b, 0.35))
			m.set_shader_parameter("tip_percentage", 0.7)
			m.set_shader_parameter("fiber_radius", 0.00045)
			m.set_shader_parameter("fiber_ratio", 0.2)
			m.set_shader_parameter("tip_fade", 0.45)
		else:
			# Bart: kurz und locker, deutlich durchscheinend, sonst ein dunkler Fleck
			m.set_shader_parameter("base_color", Color(farbe.r, farbe.g, farbe.b, 0.3))
			m.set_shader_parameter("tip_color", Color(spitze.r, spitze.g, spitze.b, 0.12))
			m.set_shader_parameter("tip_percentage", 0.8)
			m.set_shader_parameter("fiber_radius", 0.00025)
			m.set_shader_parameter("fiber_ratio", 0.2)
			m.set_shader_parameter("tip_fade", 0.6)
		# Glanz: scharfes helles Band nahe der Wurzel plus breites farbiges zur Spitze; starke Farbstreuung je Strähne
		m.set_shader_parameter("k_diffuse", 0.24)
		m.set_shader_parameter("k_spec1", 0.03)
		m.set_shader_parameter("spec_exp1", 60.0)
		m.set_shader_parameter("k_spec2", 0.12)
		m.set_shader_parameter("spec_exp2", 14.0)
		m.set_shader_parameter("root_shadow", 0.35)
		m.set_shader_parameter("color_variation", 0.55)
		haar.set("material", m)
		if not _schalter("--tfx-ohne-kollider"):
			haar.set("collision_meshes", [kol])  # die Erweiterung erwartet die Knoten selbst, keine Pfade
		if teil == "frisur":
			# Formbindung: die gestylte Ruheform soll halten (global_range 1 = ganze Strähne an die Ruheform gebunden)
			haar.set("num_follow_hairs", 4 if sparsam else 1)
			haar.set("follow_radius", 0.007 if sparsam else 0.003)
			haar.set("tip_separation", 0.4 if sparsam else 0.2)
			haar.set("global_stiffness", 0.9)
			haar.set("global_range", 1.0)
			haar.set("local_stiffness", 0.95)
			haar.set("vsp_coeff", 0.85)
			haar.set("gravity", 0.12)
			haar.set("damping", 0.25)
			haar.set("collision_skip_root_vertices", 3 if sparsam else 5)
		else:
			haar.set("num_follow_hairs", 3 if sparsam else 0)
			haar.set("follow_radius", 0.003)
			haar.set("global_stiffness", 1.0)
			haar.set("global_range", 1.0)
			haar.set("local_stiffness", 1.0)
			haar.set("vsp_coeff", 1.0)
			haar.set("gravity", 0.0)
			haar.set("damping", 0.4)
			haar.set("collision_skip_root_vertices", 3)
		var folge := _zahl("--tfx-folge=", -1)
		if folge >= 0:
			haar.set("num_follow_hairs", folge)
		if _schalter("--tfx-ohne-bart") and teil == "bart":
			haar.visible = false
		if HgFenster.sparsam:
			# viele kleine Fenster: keine Folgehaare, Detailstufe schon ab 10 cm (Kamera steht ~0,9 m weg) -> deutlich
			# weniger Strähnen, die verbliebenen zeichnet der Shader breiter
			haar.set("num_follow_hairs", 0)
			haar.set("lod_enabled", true)
			haar.set("lod_start_distance", 0.1)
			haar.set("lod_end_distance", 0.6)
			haar.set("lod_percent", 0.25)
		# Ohne Simulation zeichnet das Addon die Strähnen gar nicht (ihre Lagen entstehen erst in der Simulation).
		# „Ruhe“ heißt daher: kurz simulieren, bis die Strähnen liegen, dann einfrieren (Lage bleibt stehen).
		if ruhe or (teil == "bart" and _schalter("--tfx-bart-ruhe")):
			_spaeter_einfrieren(haar, 0.6)
		knoten.append(haar)
	kopf.tressfx_aktiv = true
	kopf.set_meta("tressfx", knoten)
	return knoten

# Startoptionen zum Eingrenzen der Kosten (Messung): --tfx-ohne-kollider, --tfx-ohne-farbe, --tfx-folge=<n>,
# --tfx-ohne-bart, --tfx-bart-ruhe
static func _schalter(name: String) -> bool:
	return OS.get_cmdline_user_args().has(name)

static func _zahl(praefix: String, standard: int) -> int:
	for a in OS.get_cmdline_user_args():
		if a.begins_with(praefix):
			return int(a.substr(praefix.length()))
	return standard

static func _spaeter_einfrieren(haar: Node3D, sekunden: float) -> void:
	var t: SceneTreeTimer = null
	if haar.is_inside_tree():
		t = haar.get_tree().create_timer(sekunden)
	if t == null:
		haar.ready.connect(func(): _spaeter_einfrieren(haar, sekunden), CONNECT_ONE_SHOT)
		return
	t.timeout.connect(func():
		if is_instance_valid(haar):
			haar.set("simulation_distance", 0.05))

# Volumen-Regler (0..1): Folgehaare weiter streuen und an den Spitzen auffächern
static func setze_volumen(kopf: HgKopf, wert: float) -> void:
	for n in kopf.get_meta("tressfx", []):
		if n.is_class("TressFXHair") and String(n.get("tfx_path")).get_file().begins_with("frisur"):
			n.set("follow_radius", lerpf(0.002, 0.012, wert))
			n.set("tip_separation", lerpf(0.0, 1.0, wert))

# Kollider: Kapsel über Schädel und Gesicht (aus den Hautpunkten oberhalb des Kinns), am Kopfknochen
static func _kopf_kollider(kopf: HgKopf) -> Node3D:
	var sk := kopf.skelett
	var m := kopf.netz.mesh
	var pts := PackedVector3Array()
	for i in m.get_surface_count():
		var mat := m.surface_get_material(i)
		if mat and mat.resource_name == "Haut":
			pts = m.surface_get_arrays(i)[Mesh.ARRAY_VERTEX]
	var augen_y := sk.get_bone_global_rest(sk.find_bone("eyeball.L")).origin.y
	var box := AABB()
	var erst := true
	for p in pts:
		if p.y < augen_y - 0.07:
			continue
		if erst:
			box = AABB(p, Vector3.ZERO)
			erst = false
		else:
			box = box.expand(p)
	var mitte := box.get_center()
	var r := minf(box.size.x, box.size.z) * 0.5 * 0.92
	var kapsel := CapsuleMesh.new()
	kapsel.radius = r
	kapsel.height = maxf(box.size.y * 0.95, 2.0 * r + 0.01)
	# Nicht am Kopfknochen: ein bewegter Kollider baut sein Abstandsfeld jedes Bild neu (bei 8 Köpfen teuer); der Kopf
	# dreht sich im Porträt nur um wenige Grad, die Wurzeln bleiben ohnehin außerhalb (collision_skip_root_vertices)
	var kol: Node3D = ClassDB.instantiate("TressFXCollisionMesh")
	kol.set("mesh", kapsel)
	kol.set("num_cells_x", 32)
	kol.position = mitte
	sk.get_parent().add_child(kol)
	return kol

static func _kartenfarbe(kopf: HgKopf) -> Color:
	var m := kopf.netz.mesh
	var summe := Vector3.ZERO
	var n := 0
	for i in m.get_surface_count():
		var mat := m.surface_get_material(i) as BaseMaterial3D
		if mat == null or not mat.resource_name.begins_with("Haar") or mat.albedo_texture == null:
			continue
		var img := mat.albedo_texture.get_image()
		if img == null:
			continue
		if img.is_compressed():
			img.decompress()
		img.resize(64, 64, Image.INTERPOLATE_BILINEAR)
		for y in 64:
			for x in 64:
				var c := img.get_pixel(x, y)
				if c.a > 0.5:
					summe += Vector3(c.r, c.g, c.b)
					n += 1
	if n == 0:
		return Color(0, 0, 0, 0)
	summe /= n
	return Color(summe.x, summe.y, summe.z, 1.0)

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
		var weg := ShaderMaterial.new()
		var sh := Shader.new()
		sh.code = "shader_type spatial;\nrender_mode unshaded, cull_disabled;\nvoid vertex() { POSITION = vec4(2.0, 2.0, 2.0, 1.0); }\n"
		weg.shader = sh
		kopf.netz.set_surface_override_material(i, weg)
