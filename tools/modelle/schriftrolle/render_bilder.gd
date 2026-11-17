extends SceneTree
# Rendert Schriftrolle (zu, halb, offen) mit Handschuh daneben und eine Bildfolge fuer das Video.
# godot --path <ordner> --script res://render.gd -- <ausordner>
# Pixel-Look wird nachgestellt: SubViewport in 1/ps Aufloesung, dann hart (nearest) hochgezogen.

const BREITE := 720
const HOEHE := 480

var aus := ""
var sv: SubViewport
var wurzel: Node3D
var rolle: Node3D
var hand: Node3D
var spieler_r: AnimationPlayer
var spieler_h: AnimationPlayer


func _initialize() -> void:
	var a := OS.get_cmdline_user_args()
	aus = a[0]
	DirAccess.make_dir_recursive_absolute(aus)
	_ablauf()


func _symbol() -> ImageTexture:
	var b := Image.create(128, 128, false, Image.FORMAT_RGBA8)
	b.fill(Color(0.60, 0.12, 0.10))
	for y in 128:
		for x in 128:
			var d := Vector2(x - 63.5, y - 63.5).length()
			if d < 52.0:
				b.set_pixel(x, y, Color(0.90, 0.78, 0.45) if d > 18.0 else Color(0.20, 0.30, 0.55))
			elif d < 58.0 and d >= 52.0:
				b.set_pixel(x, y, Color(0.05, 0.03, 0.02))
	return ImageTexture.create_from_image(b)


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
	var sf := n.find_child("Symbolflaeche", true, false) as MeshInstance3D
	if sf != null:
		var mat := StandardMaterial3D.new()
		mat.albedo_texture = _symbol()
		mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		sf.set_surface_override_material(0, mat)


func _aufbau(mit_hand: bool) -> void:
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
	rolle = (load("res://schriftrolle.glb") as PackedScene).instantiate()
	wurzel.add_child(rolle)
	_materialien(rolle)
	spieler_r = rolle.find_child("AnimationPlayer", true, false)
	spieler_r.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
	if mit_hand:
		hand = (load("res://handschuh_zeiger.glb") as PackedScene).instantiate()
		wurzel.add_child(hand)
		_materialien(hand)
		spieler_h = hand.find_child("AnimationPlayer", true, false)
		spieler_h.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		spieler_h.play("Zeigen")
		spieler_h.seek(0.3, true)
		var aabb := AABB()
		var erst := true
		for k in hand.find_children("*", "MeshInstance3D", true, false):
			var m := k as MeshInstance3D
			if String(m.name) == "Handschuh":
				aabb = m.global_transform * m.get_aabb()
				print("HANDSCHUH AABB ", aabb)
		var h := maxf(aabb.size.y, 0.001)
		var f := 0.22 / h
		hand.scale = Vector3.ONE * f
		hand.position = Vector3(-0.52, -0.02, 0.0) - (aabb.get_center() * f)
	var cam := Camera3D.new()
	cam.fov = 28.0
	cam.position = Vector3(0.0, 0.05, 1.85)
	cam.look_at_from_position(cam.position, Vector3(-0.02, 0.0, 0.0), Vector3.UP)
	sv.add_child(cam)


func _pose(clip: String, t: float) -> void:
	var sf := rolle.find_child("Symbolflaeche", true, false) as Node3D
	if sf != null:
		sf.visible = (clip == "aufrollen" and t >= 0.32) or (clip == "zurollen" and t <= 0.38)
	spieler_r.play(clip)
	spieler_r.seek(t, true)
	spieler_r.pause()
	spieler_r.seek(t, true)


func _bild(datei: String, ps: int) -> void:
	sv.size = Vector2i(BREITE / ps, HOEHE / ps)
	sv.msaa_3d = Viewport.MSAA_4X if ps == 1 else Viewport.MSAA_DISABLED
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	var img := sv.get_texture().get_image()
	if ps > 1:
		img.resize(BREITE, HOEHE, Image.INTERPOLATE_NEAREST)
	img.save_png(aus + "/" + datei)


func _ablauf() -> void:
	_aufbau(true)
	for ps in [1, 2, 3]:
		_pose("aufrollen", 0.0)
		await _bild("still_%d_zu.png" % ps, ps)
		_pose("aufrollen", 0.35)
		await _bild("still_%d_halb.png" % ps, ps)
		_pose("aufrollen", 0.7)
		await _bild("still_%d_offen.png" % ps, ps)
	# Bildfolge fuer das Video: Pixel 2, nur Rolle (Handschuh weg)
	hand.visible = false
	var n := 0
	var folge: Array = []
	for i in 22:
		folge.append(["aufrollen", i / 30.0])
	for i in 30:
		folge.append(["aufrollen", 0.7])
	for i in 22:
		folge.append(["zurollen", i / 30.0])
	for i in 30:
		folge.append(["zurollen", 0.7])
	for i in 64:
		folge.append(["aufrollen", minf(i / 90.0, 0.7)])
	for i in 30:
		folge.append(["aufrollen", 0.7])
	for i in 64:
		folge.append(["zurollen", minf(i / 90.0, 0.7)])
	for i in 30:
		folge.append(["zurollen", 0.7])
	for f in folge:
		_pose(f[0], f[1])
		await _bild("video/f_%04d.png" % n, 2)
		n += 1
	print("FERTIG ", n)
	quit()
