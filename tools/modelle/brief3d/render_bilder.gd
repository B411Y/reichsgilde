extends SceneTree
# Rendert die Briefe (grosser Brief zu/halb/offen, kleiner Faltbrief in drei Siegelfarben, Streckung, Stilvergleich mit Handschuh und Schriftrolle)
# und eine Bildfolge fuer das Video (auffalten/zufalten in Echtzeit, dann 3x langsamer).
# godot --path <ordner> --rendering-driver vulkan --script res://render.gd -- <ausordner> <messwerte_brief.json>
# Im Ordner liegen brief_gross.glb, brief_klein.glb, schriftrolle.glb, handschuh_zeiger.glb samt Texturen (nach --import).
# Pixel-Look wird nachgestellt: SubViewport in 1/ps Aufloesung, dann hart (nearest) hochgezogen.
# Die Hilfsfunktionen _siegel, _symbol, _inhalt und _strecken zeigen zugleich, wie die Oberflaeche den Brief benutzt.

const BREITE := 720
const HOEHE := 480

var aus := ""
var mess: Dictionary = {}
var sv: SubViewport
var wurzel: Node3D
var kamera: Camera3D
var brief: Node3D
var spieler: AnimationPlayer
var inhalt_ab := 0.7
var inhalt_bis := 0.3


func _initialize() -> void:
	var a := OS.get_cmdline_user_args()
	aus = a[0]
	DirAccess.make_dir_recursive_absolute(aus)
	mess = JSON.parse_string(FileAccess.get_file_as_string(a[1]))
	inhalt_ab = mess["gross"]["zeiten_s"]["auffalten_inhaltsflaeche_sichtbar_ab"]
	inhalt_bis = mess["gross"]["zeiten_s"]["zufalten_inhaltsflaeche_sichtbar_bis"]
	_ablauf()


# ------------------------------------------------------------------ Symbole (Platzhalter, wie sie die Oberflaeche zeichnen koennte): Alpha-Bild, u nach rechts, v nach unten
func _symbol(art: String, farbe: Color) -> ImageTexture:
	var n := 96
	var b := Image.create(n, n, false, Image.FORMAT_RGBA8)
	b.fill(Color(0, 0, 0, 0))
	for y in n:
		for x in n:
			var p := Vector2((x + 0.5) / n * 2.0 - 1.0, (y + 0.5) / n * 2.0 - 1.0)
			var an := false
			if art == "gefahr":
				an = (absf(p.x) < 0.14 and p.y > -0.78 and p.y < 0.28) or (p.distance_to(Vector2(0, 0.58)) < 0.17)
			elif art == "herz":
				var q := Vector2(p.x * 1.15, -p.y * 1.15 + 0.15)
				var f := pow(q.x * q.x + q.y * q.y - 0.55, 3.0) - q.x * q.x * pow(q.y, 3.0)
				an = f <= 0.0
			elif art == "krone":
				an = p.y > 0.2 and p.y < 0.62 and absf(p.x) < 0.7
				if not an and p.y <= 0.2 and p.y > -0.65:
					for cx in [-0.5, 0.0, 0.5]:
						var breite := 0.22 * (p.y + 0.65) / 0.85
						if absf(p.x - cx) < breite:
							an = true
			if an:
				b.set_pixel(x, y, farbe)
	return ImageTexture.create_from_image(b)


func _siegel(n: Node, wachs: Color, art: String) -> void:
	var m := StandardMaterial3D.new()
	m.albedo_color = wachs
	m.roughness = 0.42
	for k in n.find_children("Siegel", "MeshInstance3D", true, false):
		(k as MeshInstance3D).set_surface_override_material(0, m)
	var s := StandardMaterial3D.new()
	s.albedo_texture = _symbol(art, wachs.darkened(0.45))
	s.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	s.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	s.roughness = 0.42
	for k in n.find_children("Siegel_Symbol", "MeshInstance3D", true, false):
		(k as MeshInstance3D).set_surface_override_material(0, s)


func _inhalt_textur() -> ImageTexture:
	var w := 262
	var h := 384
	var b := Image.create(w, h, false, Image.FORMAT_RGBA8)
	b.fill(Color(0, 0, 0, 0))
	var tinte := Color(0.16, 0.10, 0.06, 0.95)
	b.fill_rect(Rect2i(0, 0, w, 2), Color(0.35, 0.12, 0.08, 0.9))
	b.fill_rect(Rect2i(0, h - 2, w, 2), Color(0.35, 0.12, 0.08, 0.9))
	b.fill_rect(Rect2i(0, 0, 2, h), Color(0.35, 0.12, 0.08, 0.9))
	b.fill_rect(Rect2i(w - 2, 0, 2, h), Color(0.35, 0.12, 0.08, 0.9))
	b.fill_rect(Rect2i(20, 22, 160, 16), tinte)
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var y := 58
	while y < h - 40:
		b.fill_rect(Rect2i(14, y, rng.randi_range(190, 234), 6), tinte)
		y += 16
	return ImageTexture.create_from_image(b)


func _inhalt(n: Node) -> void:
	var m := StandardMaterial3D.new()
	m.albedo_texture = _inhalt_textur()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.roughness = 0.9
	for k in n.find_children("Inhaltsflaeche", "MeshInstance3D", true, false):
		(k as MeshInstance3D).set_surface_override_material(0, m)


# Hoehe des offenen Blattes (0,42 bis 0,60 m) setzen: Streck skaliert die Mitte, die Anker schieben die Falze, die Inhaltsflaeche wird mit gestreckt.
func _strecken(n: Node, hoehe: float) -> void:
	var s := (hoehe - 0.24) / 0.18
	var d := (s - 1.0) * 0.18 / 2.0
	var sk: Skeleton3D
	for k in n.find_children("*", "Skeleton3D", true, false):
		sk = k as Skeleton3D
	var i_streck := sk.find_bone("Streck")
	var i_o := sk.find_bone("Anker_oben")
	var i_u := sk.find_bone("Anker_unten")
	sk.set_bone_pose_scale(i_streck, Vector3(1, s, 1))
	sk.set_bone_pose_position(i_o, sk.get_bone_rest(i_o).origin + Vector3(0, d, 0))
	sk.set_bone_pose_position(i_u, sk.get_bone_rest(i_u).origin - Vector3(0, d, 0))
	var f := n.find_child("Inhaltsflaeche", true, false) as Node3D
	if f != null:
		f.scale = Vector3(1, (hoehe - 0.04) / 0.38, 1)


func _materialien(n: Node) -> void:
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
		if m.skin != null or m.skeleton != NodePath():
			m.custom_aabb = AABB(Vector3(-0.3, -0.45, -0.2), Vector3(0.6, 0.9, 0.4))


func _neu(pfad: String, pos: Vector3, nur_manuell := true) -> Node3D:
	var n := (load(pfad) as PackedScene).instantiate() as Node3D
	wurzel.add_child(n)
	n.position = pos
	_materialien(n)
	var sp := n.find_child("AnimationPlayer", true, false) as AnimationPlayer
	if sp != null and nur_manuell:
		sp.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	return n


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
	licht.rotation_degrees = Vector3(-45, -35, 0)
	licht.light_energy = 1.1
	sv.add_child(licht)
	wurzel = Node3D.new()
	wurzel.rotation_degrees = Vector3(14, -24, 0)
	sv.add_child(wurzel)
	kamera = Camera3D.new()
	kamera.fov = 28.0
	sv.add_child(kamera)


func _cam(pos: Vector3, ziel: Vector3, fov: float) -> void:
	kamera.fov = fov
	kamera.look_at_from_position(pos, ziel, Vector3.UP)


func _pose(n: Node3D, clip: String, t: float, mit_inhalt := true) -> void:
	var sp := n.find_child("AnimationPlayer", true, false) as AnimationPlayer
	sp.play(clip)
	sp.seek(t, true)
	sp.pause()
	sp.seek(t, true)
	var f := n.find_child("Inhaltsflaeche", true, false) as Node3D
	if f != null:
		f.visible = mit_inhalt and ((clip == "auffalten" and t >= inhalt_ab - 0.0001) or (clip == "zufalten" and t <= inhalt_bis + 0.0001))


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


func _alle_weg() -> void:
	for k in wurzel.get_children():
		k.queue_free()


func _ablauf() -> void:
	_aufbau()
	var rot := Color(0.60, 0.09, 0.07)
	var gruen := Color(0.13, 0.42, 0.17)
	var gold := Color(0.80, 0.60, 0.14)
	# --- grosser Brief allein: zu, halb, offen
	brief = _neu("res://brief_gross.glb", Vector3.ZERO)
	_siegel(brief, rot, "gefahr")
	_inhalt(brief)
	_cam(Vector3(0.0, 0.03, 1.25), Vector3(0.0, 0.0, 0.0), 28.0)
	for ps in [1, 2, 3]:
		_pose(brief, "auffalten", 0.0)
		await _bild("still_%d_zu.png" % ps, ps)
		_pose(brief, "auffalten", 0.30)
		await _bild("still_%d_halb.png" % ps, ps)
		_pose(brief, "auffalten", 0.9)
		await _bild("still_%d_offen.png" % ps, ps)
		_pose(brief, "auffalten", 0.15)
		await _bild("still_%d_siegelbruch.png" % ps, ps)
	# --- Streckung: kleinste und groesste Hoehe nebeneinander (offen)
	_alle_weg()
	var b1 := _neu("res://brief_gross.glb", Vector3(-0.2, 0, 0))
	var b2 := _neu("res://brief_gross.glb", Vector3(0.2, 0, 0))
	for bb in [b1, b2]:
		_siegel(bb, gruen, "herz")
		_inhalt(bb)
		_pose(bb, "auffalten", 0.9)
	_strecken(b2, 0.60)
	_cam(Vector3(0.0, 0.0, 1.9), Vector3(0.0, 0.0, 0.0), 28.0)
	for ps in [1, 2, 3]:
		await _bild("streck_%d.png" % ps, ps)
	# Streckung gefaltet (Zustand zu mit gestreckter Mitte, zur Kontrolle)
	for bb in [b1, b2]:
		_pose(bb, "auffalten", 0.0)
	await _bild("streck_zu_1.png", 1)
	# --- kleiner Faltbrief in drei Siegelfarben
	_alle_weg()
	var arten := [["gefahr", rot], ["herz", gruen], ["krone", gold]]
	for i in 3:
		var k := _neu("res://brief_klein.glb", Vector3(-0.17 + 0.17 * i, 0, 0))
		_siegel(k, arten[i][1], arten[i][0])
	_cam(Vector3(0.0, 0.01, 0.62), Vector3(0.0, 0.0, 0.0), 28.0)
	for ps in [1, 2, 3]:
		await _bild("klein_%d.png" % ps, ps)
	# Nahaufnahme eines kleinen Briefes
	_alle_weg()
	var k1 := _neu("res://brief_klein.glb", Vector3.ZERO)
	_siegel(k1, rot, "gefahr")
	_cam(Vector3(0.0, 0.01, 0.30), Vector3(0.0, 0.0, 0.0), 28.0)
	for ps in [1, 2, 3]:
		await _bild("klein_nah_%d.png" % ps, ps)
	# --- Stilvergleich: Handschuh | Schriftrolle | grosser Brief (offen) | kleiner Brief
	_alle_weg()
	var hand := _neu("res://handschuh_zeiger.glb", Vector3.ZERO)
	var sp_h := hand.find_child("AnimationPlayer", true, false) as AnimationPlayer
	sp_h.play("Zeigen")
	sp_h.seek(0.3, true)
	var aabb := AABB()
	for k in hand.find_children("*", "MeshInstance3D", true, false):
		var m := k as MeshInstance3D
		if String(m.name) == "Handschuh":
			aabb = m.global_transform * m.get_aabb()
	var f := 0.22 / maxf(aabb.size.y, 0.001)
	hand.scale = Vector3.ONE * f
	hand.position = Vector3(-0.62, 0.0, 0.0) - (aabb.get_center() * f)
	var rolle := _neu("res://schriftrolle.glb", Vector3(-0.15, 0, 0))
	_pose(rolle, "aufrollen", 0.9, false)
	var rs := rolle.find_child("Symbolflaeche", true, false) as Node3D
	if rs != null:
		rs.visible = false
	var gb := _neu("res://brief_gross.glb", Vector3(0.28, 0, 0))
	_siegel(gb, rot, "gefahr")
	_inhalt(gb)
	_pose(gb, "auffalten", 0.9)
	var kb := _neu("res://brief_klein.glb", Vector3(0.58, -0.05, 0))
	_siegel(kb, gold, "krone")
	_cam(Vector3(0.0, 0.0, 2.05), Vector3(0.0, 0.0, 0.0), 28.0)
	for ps in [1, 2, 3]:
		await _bild("vergleich_%d.png" % ps, ps)
	# --- Video: grosser Brief, Pixel 2, 540 x 540: Echtzeit, dann 3x langsamer
	_alle_weg()
	brief = _neu("res://brief_gross.glb", Vector3.ZERO)
	_siegel(brief, rot, "gefahr")
	_inhalt(brief)
	_cam(Vector3(0.0, 0.0, 1.32), Vector3(0.0, 0.0, 0.0), 28.0)
	var folge: Array = []
	for i in 28:
		folge.append(["auffalten", i / 30.0])
	for i in 24:
		folge.append(["auffalten", 0.9])
	for i in 28:
		folge.append(["zufalten", i / 30.0])
	for i in 24:
		folge.append(["zufalten", 0.9])
	for i in 82:
		folge.append(["auffalten", minf(i / 90.0, 0.9)])
	for i in 24:
		folge.append(["auffalten", 0.9])
	for i in 82:
		folge.append(["zufalten", minf(i / 90.0, 0.9)])
	for i in 24:
		folge.append(["zufalten", 0.9])
	var n := 0
	for fo in folge:
		_pose(brief, fo[0], fo[1])
		await _bild("video/f_%04d.png" % n, 2, 540, 540)
		n += 1
	print("FERTIG ", n)
	quit()
