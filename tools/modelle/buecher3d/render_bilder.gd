extends SceneTree
# Rendert die drei Buecher (zu, halb, offen, umblaettern) einzeln, auf ihrem Moebel (Lesepult, Stubentisch-Platzhalter, Schreibpult), im Stilvergleich
# mit Handschuh, Schriftrolle und Brief, und auf Wunsch die Bildfolgen fuer die Videos (Echtzeit, dann 3x langsamer).
# godot --path <ordner> --rendering-driver vulkan --script res://render.gd -- <ausordner> <messordner mit messwerte_buch_*.json> [video]
# Im Ordner liegen buch_personal.glb, buch_familie.glb, buch_kontor.glb, lesepult.glb, schreibpult.glb, handschuh_zeiger.glb, schriftrolle.glb, brief_gross.glb (nach --import).
# Pixel-Look wird nachgestellt: SubViewport in 1/ps Aufloesung, dann hart (nearest) hochgezogen.
# Die Hilfsfunktionen _material_setzen, _tafel und _pose zeigen zugleich, wie die Oberflaeche die Buecher benutzt
# (Umriss-Material wie in HandschuhZeiger.MaterialienEinrichten, Inhaltsflaechen mit eigenem Material, Sichtbarkeit nach den Zeiten der Messwerte).

const BREITE := 720
const HOEHE := 480
const ARTEN := ["personal", "familie", "kontor"]
const MOEBEL := {"personal": "lesepult", "kontor": "schreibpult"}

var aus := ""
var messordner := ""
var mess := {}
var sv: SubViewport
var wurzel: Node3D
var kamera: Camera3D
var mit_video := false


func _initialize() -> void:
	var a := OS.get_cmdline_user_args()
	aus = a[0]
	messordner = a[1]
	mit_video = a.size() > 2 and a[2] == "video"
	DirAccess.make_dir_recursive_absolute(aus)
	for art in ARTEN:
		mess[art] = JSON.parse_string(FileAccess.get_file_as_string("%s/messwerte_buch_%s.json" % [messordner, art]))
	_ablauf()


# ------------------------------------------------------------------ Platzhalter-Tafel fuer die Inhaltsflaechen (so, wie die Oberflaeche ein Bild darauflegen koennte)
func _tafel(art: String) -> ImageTexture:
	var w := 262
	var h := 384
	var b := Image.create(w, h, false, Image.FORMAT_RGBA8)
	b.fill(Color(0.93, 0.88, 0.76, 0.0))
	var tinte := Color(0.16, 0.10, 0.06, 0.95)
	var rot := Color(0.55, 0.14, 0.10, 0.95)
	b.fill_rect(Rect2i(0, 0, w, 3), rot)
	b.fill_rect(Rect2i(0, h - 3, w, 3), rot)
	b.fill_rect(Rect2i(0, 0, 3, h), rot)
	b.fill_rect(Rect2i(w - 3, 0, 3, h), rot)
	b.fill_rect(Rect2i(24, 26, 150, 14), tinte)
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	var y := 60
	while y < h - 30:
		b.fill_rect(Rect2i(16, y, rng.randi_range(170, 230), 6), tinte)
		y += 18
	return ImageTexture.create_from_image(b)


func _material_setzen(n: Node, mit_tafel: bool, art: String) -> void:
	for k in n.find_children("*", "MeshInstance3D", true, false):
		var m := k as MeshInstance3D
		m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var umriss := String(m.name).contains("Umriss")
		for i in m.mesh.get_surface_count():
			var s := m.mesh.surface_get_material(i) as StandardMaterial3D
			if s == null:
				continue
			if umriss:
				s.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
				s.cull_mode = BaseMaterial3D.CULL_BACK
			elif String(m.name) == "Handschuh":
				s.vertex_color_use_as_albedo = true
		if m.skin != null:
			m.custom_aabb = AABB(Vector3(-0.42, -0.05, -0.25), Vector3(0.84, 0.45, 0.5))
		if String(m.name).begins_with("Inhaltsflaeche") and mit_tafel:
			var mat := StandardMaterial3D.new()
			mat.albedo_texture = _tafel(art)
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
			mat.roughness = 0.9
			m.set_surface_override_material(0, mat)
		if String(m.name) == "Wappenfeld":
			var wm := StandardMaterial3D.new()          # so setzt die Oberflaeche Hausfarbe bzw. Wappen
			wm.albedo_color = Color(0.17, 0.27, 0.58)
			wm.roughness = 0.8
			wm.albedo_texture = _wappen()
			wm.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
			m.set_surface_override_material(0, wm)


func _wappen() -> ImageTexture:
	var n := 64
	var b := Image.create(n, n, false, Image.FORMAT_RGBA8)
	for y in n:
		for x in n:
			var c := Color(0.15, 0.25, 0.58)
			if x > n / 2 - 1:
				c = Color(0.93, 0.90, 0.82)
			var d := Vector2(x - 31.5, y - 31.5).length()
			if d < 14.0:
				c = Color(0.80, 0.18, 0.15) if x < n / 2 else Color(0.80, 0.62, 0.20)
			b.set_pixel(x, y, c)
	return ImageTexture.create_from_image(b)


func _aufbau() -> void:
	sv = SubViewport.new()
	sv.own_world_3d = true
	sv.size = Vector2i(BREITE, HOEHE)
	sv.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(sv)
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.30, 0.28, 0.25)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.58, 0.52)
	env.ambient_light_energy = 0.9
	var we := WorldEnvironment.new()
	we.environment = env
	sv.add_child(we)
	var licht := DirectionalLight3D.new()
	licht.rotation_degrees = Vector3(-50, -35, 0)
	licht.light_energy = 1.1
	sv.add_child(licht)
	wurzel = Node3D.new()
	sv.add_child(wurzel)
	kamera = Camera3D.new()
	kamera.fov = 28.0
	sv.add_child(kamera)


func _leeren() -> void:
	for k in wurzel.get_children():
		wurzel.remove_child(k)
		k.queue_free()
	wurzel.rotation_degrees = Vector3.ZERO


func _cam(pos: Vector3, ziel: Vector3, fov: float) -> void:
	kamera.fov = fov
	kamera.look_at_from_position(pos, ziel, Vector3.UP)


func _box(gr: Vector3, pos: Vector3, farbe: Color) -> void:
	var mi := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = gr
	mi.mesh = bm
	var mat := StandardMaterial3D.new()
	mat.albedo_color = farbe
	mat.roughness = 0.9
	mi.material_override = mat
	mi.position = pos
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	wurzel.add_child(mi)


func _laden(pfad: String, eltern: Node3D, mit_tafel := false, art := "") -> Node3D:
	var n := (load(pfad) as PackedScene).instantiate() as Node3D
	eltern.add_child(n)
	_material_setzen(n, mit_tafel, art)
	var sp := n.find_child("AnimationPlayer", true, false) as AnimationPlayer
	if sp != null:
		sp.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	return n


# Tisch (Platte bei y = 0) mit dem Buch in der Mitte
func _buch_allein(art: String) -> Node3D:
	_leeren()
	_box(Vector3(1.4, 0.04, 0.9), Vector3(0, -0.02, 0), Color(0.36, 0.26, 0.17))
	wurzel.rotation_degrees = Vector3(0, -14, 0)
	return _laden("res://buch_%s.glb" % art, wurzel, true, art)


# Buch auf seinem Moebel: Lesepult, Schreibpult (Knoten Buch_Auflage) oder Stubentisch-Platzhalter (Platte bei 0,80 m)
func _buch_auf_moebel(art: String) -> Node3D:
	_leeren()
	_box(Vector3(3.0, 0.02, 2.4), Vector3(0, -0.01, 0), Color(0.30, 0.25, 0.20))
	wurzel.rotation_degrees = Vector3(0, -22, 0)
	if art == "familie":
		_box(Vector3(1.6, 0.06, 0.9), Vector3(0, 0.77, 0), Color(0.40, 0.29, 0.18))
		for sx in [-0.7, 0.7]:
			_box(Vector3(0.08, 0.74, 0.7), Vector3(sx, 0.37, 0), Color(0.34, 0.25, 0.16))
		var b := _laden("res://buch_familie.glb", wurzel, true, art)
		b.position = Vector3(0, 0.80, 0.05)
		return b
	var m := _laden("res://%s.glb" % MOEBEL[art], wurzel)
	var auf := m.find_child("Buch_Auflage", true, false) as Node3D
	var b2 := _laden("res://buch_%s.glb" % art, auf, true, art)
	return b2


func _pose(n: Node3D, art: String, clip: String, t: float) -> void:
	var sp := n.find_child("AnimationPlayer", true, false) as AnimationPlayer
	sp.play(clip)
	sp.seek(t, true)
	sp.pause()
	sp.seek(t, true)
	var z: Dictionary = mess[art]["zeiten_s"]
	var sichtbar := false
	match clip:
		"offen":
			sichtbar = true
		"aufklappen":
			sichtbar = t >= float(z["aufklappen_inhaltsflaeche_sichtbar_ab"]) - 0.0001
		"zuklappen":
			sichtbar = t <= float(z["zuklappen_inhaltsflaeche_sichtbar_bis"]) + 0.0001
		"umblaettern", "umblaettern_zurueck":
			sichtbar = t >= float(z["umblaettern_inhaltsflaeche_aus_von_bis"][1]) - 0.0001
	for nm in ["Inhaltsflaeche_links", "Inhaltsflaeche_rechts"]:
		var f := n.find_child(nm, true, false) as Node3D
		if f != null:
			f.visible = sichtbar and mit_inhalt


var mit_inhalt := false


func _bild(datei: String, ps: int, b := BREITE, h := HOEHE) -> void:
	sv.size = Vector2i(b / ps, h / ps)
	sv.msaa_3d = Viewport.MSAA_4X if ps == 1 else Viewport.MSAA_DISABLED
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var img := sv.get_texture().get_image()
	if ps > 1:
		img.resize(b, h, Image.INTERPOLATE_NEAREST)
	img.save_png(aus + "/" + datei)


func _poses(art: String) -> Array:
	var z: Dictionary = mess[art]["zeiten_s"]
	return [["zu", "zu", 0.0], ["halb", "aufklappen", float(z["aufklappen_halb"])], ["offen", "offen", 0.0],
			["umblaettern", "umblaettern", float(z["umblaettern_blatt_senkrecht"]) - 0.05]]


func _ablauf() -> void:
	_aufbau()
	for art in ARTEN:
		# Einzelbilder: ohne Inhaltsflaechen (die Seiten selbst) und offen mit Platzhalter-Tafel
		_cam(Vector3(0, 0.62, 0.62), Vector3(0, 0.03, 0.0), 30.0)
		for ps in [1, 2, 3]:
			mit_inhalt = false
			var n := _buch_allein(art)
			for p in _poses(art):
				_pose(n, art, p[1], p[2])
				await _bild("%s_%d_%s.png" % [art, ps, p[0]], ps)
			mit_inhalt = true
			_pose(n, art, "offen", 0.0)
			await _bild("%s_%d_inhalt.png" % [art, ps], ps)
		# auf dem Moebel
		mit_inhalt = false
		for ps in [1, 2, 3]:
			var n2 := _buch_auf_moebel(art)
			if art == "personal":
				_cam(Vector3(0.0, 1.55, 1.9), Vector3(0, 0.98, 0), 34.0)
			elif art == "kontor":
				_cam(Vector3(0.0, 1.55, 2.2), Vector3(0, 0.84, 0), 36.0)
			else:
				_cam(Vector3(0.0, 1.3, 1.9), Vector3(0, 0.78, 0), 34.0)
			for p in [["zu", "zu", 0.0], ["offen", "offen", 0.0]]:
				_pose(n2, art, p[1], p[2])
				await _bild("%s_%d_moebel_%s.png" % [art, ps, p[0]], ps)
	# Stilvergleich: Handschuh, Schriftrolle, Brief und die drei Buecher (zu und offen)
	mit_inhalt = false
	for ps in [1, 2, 3]:
		_leeren()
		_box(Vector3(3.2, 0.02, 1.0), Vector3(0, -0.01, 0), Color(0.36, 0.26, 0.17))
		var hs := _laden("res://handschuh_zeiger.glb", wurzel)
		var hsp := hs.find_child("AnimationPlayer", true, false) as AnimationPlayer
		hsp.play("Zeigen")
		hsp.seek(0.3, true)
		hs.scale = Vector3.ONE * 0.9
		hs.position = Vector3(-1.38, 0.1, 0.1)
		var sr := _laden("res://schriftrolle.glb", wurzel)
		sr.position = Vector3(-0.92, 0.22, 0.0)
		sr.rotation_degrees = Vector3(-35, 0, 0)
		var srp := sr.find_child("AnimationPlayer", true, false) as AnimationPlayer
		srp.play("aufrollen")
		srp.seek(0.9, true)
		var sf := sr.find_child("Symbolflaeche", true, false) as Node3D
		if sf != null:
			sf.visible = false
		var br := _laden("res://brief_gross.glb", wurzel)
		br.position = Vector3(-0.5, 0.0, 0.0)
		br.rotation_degrees = Vector3(-90, 0, 0)
		var brp := br.find_child("AnimationPlayer", true, false) as AnimationPlayer
		if brp != null:
			brp.play("auffalten")
			brp.seek(0.9, true)
		var buecher := {}
		var x := -0.05
		for art in ARTEN:
			var b := _laden("res://buch_%s.glb" % art, wurzel, false, art)
			b.position = Vector3(x + (0.1 if art == "personal" else 0.0), 0.0, 0.0)
			_pose(b, art, "zu", 0.0)
			buecher[art] = b
			x += 0.32
		_cam(Vector3(0.2, 1.2, 1.45), Vector3(-0.1, 0.0, 0.0), 30.0)
		await _bild("vergleich_%d_zu.png" % ps, ps, BREITE * 2, HOEHE)
		for art in ARTEN:
			_pose(buecher[art], art, "offen", 0.0)
		await _bild("vergleich_%d_offen.png" % ps, ps, BREITE * 2, HOEHE)
	if mit_video:
		for art in ARTEN:
			await _video(art)
	print("FERTIG")
	quit()


func _video(art: String) -> void:
	mit_inhalt = true
	var z: Dictionary = mess[art]["zeiten_s"]
	_buch_allein(art)
	var n := wurzel.get_child(wurzel.get_child_count() - 1) as Node3D
	_cam(Vector3(0, 0.95, 0.95), Vector3(0, 0.02, 0.0), 30.0)
	var folge: Array = []
	var auf: float = z["aufklappen_dauer"]
	var blatt: float = z["umblaettern_dauer"]
	var zu: float = z["zuklappen_dauer"]
	# Echtzeit (30/s), dann 3x langsamer (90/s Zeitschritte als 30/s gespielt)
	for faktor in [1, 3]:
		var dt: float = 1.0 / (30.0 * float(faktor))
		var t: float = 0.0
		for i in 8:
			folge.append(["zu", 0.0])
		t = 0.0
		while t <= auf + 0.0001:
			folge.append(["aufklappen", t])
			t += dt
		for i in 10 * int(faktor):
			folge.append(["offen", 0.0])
		for k in 2:
			t = 0.0
			while t <= blatt + 0.0001:
				folge.append(["umblaettern", t])
				t += dt
			for i in 8 * int(faktor):
				folge.append(["offen", 0.0])
		t = 0.0
		while t <= blatt + 0.0001:
			folge.append(["umblaettern_zurueck", t])
			t += dt
		for i in 8 * int(faktor):
			folge.append(["offen", 0.0])
		t = 0.0
		while t <= zu + 0.0001:
			folge.append(["zuklappen", t])
			t += dt
		for i in 12:
			folge.append(["zu", 0.0])
	var nr := 0
	DirAccess.make_dir_recursive_absolute(aus + "/video_" + art)
	for f in folge:
		_pose(n, art, f[0], minf(f[1], 100.0))
		await _bild("video_%s/f_%04d.png" % [art, nr], 2, 540, 540)
		nr += 1
	print("VIDEO ", art, " ", nr)
