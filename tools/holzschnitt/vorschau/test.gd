extends Node3D
# Holzschnitt-Vorschau (ohne Spiel): ein paar echte Modelle, Bild „tag“, „nacht“ oder „aus“ (ohne Holzschnitt).
# Die Funktion umstellen() ist das Muster für den Einbau ins Spiel: welches Material welche Parameter bekommt.
var bild := 0
var modus := "tag"
var sh: Shader = load("res://holzschnitt.gdshader")
var texturen := {}
var arten := {}

func _ready():
	for a in OS.get_cmdline_user_args(): if a.begins_with("--modus="): modus = a.substr(8)
	for n in ["schraffur", "papier", "struktur_a", "struktur_b"]: texturen[n] = load("res://%s.png" % n)
	arten = JSON.parse_string(FileAccess.get_file_as_string("res://materialarten.json"))
	setzen("schmied", Vector3(-9, 0, -6), ["Stufe2", "Stufe3", "Leerstand", "Geruest"])
	setzen("fachwerkhaus", Vector3(5, 0, -10), ["Stufe2", "Stufe3", "Leerstand", "Geruest"])
	setzen("grube", Vector3(32, 0, 6), ["Leerstand"])
	setzen("berg_grube", Vector3(32, 0, 6), [])
	var b := setzen("natur_baeume", Vector3.ZERO, [])
	var lage := {"Eiche1": Vector3(-14, 0, 7), "Tanne1": Vector3(-6, 0, 10), "Linde1_Winter": Vector3(0, 0, 14)}
	for n in b.find_children("*", "MeshInstance3D", true, false):
		n.visible = lage.has(String(n.name))
		if n.visible: n.position = lage[String(n.name)]
	setzen("karren_ochse", Vector3(1, 0, 4), ["Ladung2", "Ladung3"], 0.6)
	var gewaender := [Color(0.55, 0.12, 0.12), Color(0.24, 0.42, 0.26), Color(0.20, 0.32, 0.62)]
	for i in 3:
		var f := setzen("figur_mensch", Vector3(-2.5 + i * 1.2, 0, 7.5 + i * 0.4), [], PI + i * 0.5)
		for n in f.find_children("*", "MeshInstance3D", true, false):
			n.visible = n.name == ("Koerper_Frau" if i == 1 else "Koerper_Mann") or (i == 2 and n.name == "Strohhut")
			for s in n.mesh.get_surface_count():   # Zonenfarben sind gewöhnliche Uniforms: je Figur ein eigenes Material
				var m = n.get_surface_override_material(s)
				if m is ShaderMaterial: m.set_shader_parameter("gewand", gewaender[i])
	var boden := MeshInstance3D.new(); boden.mesh = PlaneMesh.new(); boden.mesh.size = Vector2(120, 120); add_child(boden)
	boden.position.y = -0.02
	var bm := StandardMaterial3D.new(); bm.albedo_color = Color(0.40, 0.46, 0.27); bm.resource_name = "Gras"
	boden.mesh.surface_set_material(0, bm)
	if modus != "aus": umstellen(boden)
	var kam := Camera3D.new(); add_child(kam)
	kam.projection = Camera3D.PROJECTION_ORTHOGONAL; kam.size = 52.0; kam.far = 400
	kam.rotation_degrees = Vector3(-40, 45, 0); kam.position = Vector3(10, 0, 0) + kam.basis.z * 120
	var nacht := modus == "nacht"
	var licht := DirectionalLight3D.new(); add_child(licht); licht.rotation_degrees = Vector3(-48, 20, 0)
	licht.shadow_enabled = true; licht.directional_shadow_max_distance = 200
	licht.light_energy = 0.35 if nacht else 1.1
	licht.light_color = Color(0.55, 0.62, 0.95) if nacht else Color(1, 0.97, 0.9)
	var we := WorldEnvironment.new(); var e := Environment.new(); e.background_mode = Environment.BG_COLOR
	e.background_color = Color(0.08, 0.09, 0.15) if nacht else Color(0.47, 0.6, 0.72)
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.35, 0.4, 0.6) if nacht else Color(0.75, 0.75, 0.8)
	e.ambient_light_energy = 0.5; we.environment = e; add_child(we)
	# Grundhelligkeit für den Holzschnitt aus dem Umgebungslicht (das Spiel setzt das je Bild)
	var u := e.ambient_light_color * e.ambient_light_energy
	RenderingServer.global_shader_parameter_set("holzschnitt_umgebung", Vector3(u.r, u.g, u.b))
	if modus != "aus":
		var q := MeshInstance3D.new(); q.mesh = QuadMesh.new(); q.mesh.size = Vector2(2, 2); q.extra_cull_margin = 16384
		var um := ShaderMaterial.new(); um.shader = load("res://holzschnitt_umriss.gdshader"); q.material_override = um
		kam.add_child(q); q.position = Vector3(0, 0, -2)

func setzen(datei: String, ort: Vector3, aus: Array, dreh := 0.0) -> Node3D:
	var m: Node3D = load("res://%s.glb" % datei).instantiate(); add_child(m)
	m.position = ort; m.rotation.y = dreh
	for n in m.find_children("*", "MeshInstance3D", true, false):
		if String(n.name) in aus or String(n.name).begins_with("Winteraeste"): n.visible = false
		if modus != "aus": umstellen(n)
	var ap: AnimationPlayer = m.find_child("AnimationPlayer", true, false)
	if ap: ap.play("Stehen" if ap.has_animation("Stehen") else "Fahren")
	return m

func materialart(name: String) -> int:
	var art = arten["namen"].get(name)
	if art == null:
		art = "keine"
		for regel in arten["enthaelt"]:
			if name.contains(regel[0]): art = regel[1]; break
	return int(arten["arten"][art])

# Muster für den Einbau: jedes Material bekommt ein Holzschnitt-Material mit seiner Farbe und Materialart.
func umstellen(n: MeshInstance3D):
	for s in n.mesh.get_surface_count():
		var alt = n.mesh.surface_get_material(s)
		if not alt is BaseMaterial3D: continue
		var name := String(alt.resource_name)
		if name in ["Glut", "Fenster", "Glas", "Wasser"]: continue   # leuchten oder spiegeln: bleiben wie sie sind
		var m := ShaderMaterial.new(); m.shader = sh
		for t in texturen: m.set_shader_parameter(t, texturen[t])
		var ecke := name in ["Farben", "Figur"]                       # Farbe und Art je Fläche aus der GLB
		m.set_shader_parameter("farbe", alt.albedo_color)
		m.set_shader_parameter("eckenfarbe", ecke)
		m.set_shader_parameter("art_aus_uv", ecke)
		m.set_shader_parameter("art", materialart(name))
		m.set_shader_parameter("zonen", name == "Figur")
		n.set_surface_override_material(s, m)

func _process(_d):
	bild += 1
	if bild == 30:
		get_viewport().get_texture().get_image().save_png("res://bild_%s.png" % modus); get_tree().quit()
