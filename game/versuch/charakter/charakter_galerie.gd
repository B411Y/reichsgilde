# Versuch Charakter Phase 3: Galerie – viele Köpfe nebeneinander (Testszene, kein Spielcode).
# Drei Hauttöne (hell, mittel, dunkel) × Mann/Frau × 5, 20, 35, 50, 70 Jahre = 30 Köpfe, die sich ein Netz teilen
# (die .glb wird einmal geladen). Tag- und Innenlicht umschaltbar.
#   tools/charakter/schau.sh <bilder> 1600x900 (SZENE=charakter_galerie.tscn)    zum Ausprobieren
#   … --fotos [--ende]       feste Ansichten als Bilder (Tag und Innen, nah und weit) und Messung, dann Ende
# Tasten: L Licht (Tag/Innen) · V vorher/nachher (Stoffe der Phase 2) · U Ultra (Bildschirm-SSS) · M Mimik ·
#         F1 Hilfe · 1–6 springt zu einer Reihe
# Kamera: freie Kamera (game/scripts/FreieKamera.cs: rechte Maustaste halten = umsehen, WASD, Q/E runter/hoch, Shift
# schneller, Mausrad Tempo, H Hilfe); fehlt sie, eine feste Kamera mit Sprüngen (1–6).
extends Node3D

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")
const FREIE_KAMERA := "res://scripts/FreieKamera.cs"
const ALTER := [5, 20, 35, 50, 70]
# Hauttöne mit passendem Haar und Augen (Gene 0..255)
const TOENE := [
	{"name": "hell", "gene": {"haut_helligkeit": 215, "haut_ton": 95, "haar_helligkeit": 150, "haar_rot": 60, "augen_helligkeit": 190, "augen_ton": 200}},
	{"name": "mittel", "gene": {"haut_helligkeit": 150, "haut_ton": 120, "haar_helligkeit": 55, "haar_rot": 40, "augen_helligkeit": 110, "augen_ton": 120}},
	{"name": "dunkel", "gene": {"haut_helligkeit": 22, "haut_ton": 110, "haar_helligkeit": 10, "haar_rot": 10, "augen_helligkeit": 25, "augen_ton": 40}},
]
const MIMIK := [
	{},
	{"mouthSmileLeft": 0.7, "mouthSmileRight": 0.7, "cheekSquintLeft": 0.4, "cheekSquintRight": 0.4},
	{"browDownLeft": 0.8, "browDownRight": 0.8, "noseSneerLeft": 0.3, "noseSneerRight": 0.3},
	{"browInnerUp": 0.9, "browOuterUpLeft": 0.6, "browOuterUpRight": 0.6, "eyeWideLeft": 0.5, "eyeWideRight": 0.5},
	{"jawOpen": 0.35, "eyeBlinkLeft": 0.5, "eyeBlinkRight": 0.5},
]
const ABSTAND_X := 0.62
const ABSTAND_Z := 0.9
const STUFE := 0.26                  # jede Reihe höher (Tribüne), damit man alle sieht
const BODEN := 0.6

var args := {}
var glb_ordner := OS.get_environment("HOME").path_join("werkzeuge/charakter/glb3")
var bilder := OS.get_environment("HOME").path_join("werkzeuge/charakter/bilder/phase3")
var koepfe: Array = []            # [Kopf, ton, mann, alter]
var umgebung: Environment
var tag: Node3D
var innen: Node3D
var kamera: Camera3D
var hilfe: Label
var ist_tag := true
var vorher := false
var ultra := false
var mimik_nr := 0
var freie := false
var _reihe_y := {}

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--") and a.contains("="):
			var kv := a.substr(2).split("=", true, 1)
			args[kv[0]] = kv[1]
		elif a.begins_with("--"):
			args[a.substr(2)] = "1"
	glb_ordner = args.get("glb", glb_ordner)
	bilder = args.get("bilder", bilder)
	get_viewport().msaa_3d = Viewport.MSAA_4X
	_buehne()
	var datei := glb_ordner.path_join(args.get("kopf", "kopf3.glb"))
	var basis := Kopf.new()
	add_child(basis)
	if not basis.laden(datei):
		_meldung(basis.fehler)
		return
	var t := Time.get_ticks_usec()
	for ti in TOENE.size():
		for g in 2:
			for ai in ALTER.size():
				var k := Kopf.new()
				add_child(k)
				k.kopieren_von(basis)
				var gene := Kopf.mittelwerte("Mensch")
				gene.merge(TOENE[ti]["gene"], true)
				k.aussehen_setzen(gene, ALTER[ai], 1.0 if g == 0 else 0.0)
				var reihe := ti * 2 + g
				k.position = Vector3((ai - 2) * ABSTAND_X, reihe * STUFE - k.augen_mitte.y + 1.6, -reihe * ABSTAND_Z - k.augen_mitte.z)
				_reihe_y[reihe] = 1.6 + reihe * STUFE
				koepfe.append([k, ti, g == 0, ALTER[ai]])
				# Sockel unter der Büste (bis zum Boden): Unterkante der Büste aus Augenhöhe und Scheitel geschätzt
				var s_kopf: float = k.oben - k.augen_mitte.y
				var oben_y: float = k.position.y + k.augen_mitte.y - 3.1 * s_kopf
				var sockel := MeshInstance3D.new()
				var bm := BoxMesh.new()
				bm.size = Vector3(0.3, oben_y - BODEN, 0.22)
				sockel.mesh = bm
				sockel.material_override = _sockel_stoff()
				sockel.position = Vector3(k.position.x, BODEN + (oben_y - BODEN) / 2.0, k.position.z + k.augen_mitte.z - 0.75 * s_kopf)
				add_child(sockel)
	basis.queue_free()
	print("GALERIE %d Köpfe, Laden %.0f ms, Kopieren %.0f ms, Bilder %.0f ms" % [koepfe.size(), basis.ladezeit_ms,
		(Time.get_ticks_usec() - t) / 1000.0, basis.bilder_ms])
	_kamera()
	print("GALERIE Kamera: ", "frei (FreieKamera.cs)" if freie else "fest (FreieKamera.cs fehlt)")
	_hilfe()
	_licht_setzen(true)
	if args.has("fotos"):
		if freie:
			kamera.set("HilfeSichtbar", false)
		await _fotos()
		get_tree().quit()

func _meldung(text: String) -> void:
	print("MELDUNG ", text)
	var l := Label.new()
	l.text = text
	l.position = Vector2(30, 30)
	add_child(l)
	if args.has("ende"):
		await get_tree().create_timer(2.0).timeout
		get_tree().quit(1)

# ---------------------------------------------------------------- Bühne: Boden, Sockel, Licht
func _buehne() -> void:
	umgebung = Environment.new()
	umgebung.tonemap_mode = Environment.TONE_MAPPER_AGX
	umgebung.ssao_enabled = true
	umgebung.ssao_radius = 0.05
	umgebung.ssao_intensity = 1.2
	var we := WorldEnvironment.new()
	we.environment = umgebung
	add_child(we)
	var boden := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(12, 12)
	boden.mesh = pm
	var bs := StandardMaterial3D.new()
	bs.albedo_color = Color(0.32, 0.26, 0.2)
	bs.roughness = 0.9
	boden.material_override = bs
	boden.position = Vector3(0, BODEN, -2.5)
	add_child(boden)
	# Tageslicht: Sonne schräg von vorn links, Himmel als Umgebung
	tag = Node3D.new()
	add_child(tag)
	var sonne := DirectionalLight3D.new()
	sonne.light_color = Color(1.0, 0.95, 0.86)
	sonne.light_energy = 2.0
	sonne.shadow_enabled = true
	sonne.shadow_normal_bias = 1.5
	sonne.shadow_blur = 1.5
	sonne.directional_shadow_max_distance = 12.0
	sonne.rotation_degrees = Vector3(-38, -35, 0)
	tag.add_child(sonne)
	# Innenlicht: Herdfeuer links vorn, Kerze rechts, kaum Umgebungslicht
	innen = Node3D.new()
	add_child(innen)
	for l in [[Vector3(-1.6, 1.3, 1.2), Color(1.0, 0.58, 0.28), 3.2, 6.0], [Vector3(1.9, 1.9, -1.5), Color(1.0, 0.7, 0.42), 1.4, 4.5],
			[Vector3(-0.5, 1.8, -4.8), Color(1.0, 0.62, 0.35), 1.2, 4.0]]:
		var o := OmniLight3D.new()
		o.position = l[0]
		o.light_color = l[1]
		o.light_energy = l[2]
		o.omni_range = l[3]
		o.shadow_enabled = true
		o.shadow_normal_bias = 1.5
		innen.add_child(o)

var _sockel: StandardMaterial3D
func _sockel_stoff() -> StandardMaterial3D:
	if _sockel == null:
		_sockel = StandardMaterial3D.new()
		_sockel.albedo_color = Color(0.22, 0.16, 0.11)
		_sockel.roughness = 0.8
	return _sockel

func _licht_setzen(an_tag: bool) -> void:
	ist_tag = an_tag
	tag.visible = an_tag
	innen.visible = not an_tag
	if an_tag:
		var himmel := Sky.new()
		var hm := ProceduralSkyMaterial.new()
		hm.sky_top_color = Color(0.35, 0.5, 0.75)
		hm.sky_horizon_color = Color(0.72, 0.74, 0.76)
		hm.ground_bottom_color = Color(0.25, 0.22, 0.18)
		hm.ground_horizon_color = Color(0.6, 0.58, 0.54)
		himmel.sky_material = hm
		umgebung.background_mode = Environment.BG_SKY
		umgebung.sky = himmel
		umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
		umgebung.ambient_light_energy = 0.7
		umgebung.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
		umgebung.tonemap_exposure = 1.0
	else:
		umgebung.background_mode = Environment.BG_COLOR
		umgebung.background_color = Color(0.03, 0.02, 0.015)
		umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		umgebung.ambient_light_color = Color(0.5, 0.38, 0.28)
		umgebung.ambient_light_energy = 0.12
		umgebung.reflected_light_source = Environment.REFLECTION_SOURCE_BG
		umgebung.tonemap_exposure = 1.25
	_hilfe_text()

# ---------------------------------------------------------------- Kamera und Hilfe
func _kamera() -> void:
	kamera = Camera3D.new()
	kamera.fov = 40.0
	kamera.near = 0.02
	if ResourceLoader.exists(FREIE_KAMERA):
		var skript: Script = load(FREIE_KAMERA)
		var k: Node = skript.new() if skript and skript.can_instantiate() else null
		if k is Camera3D:
			kamera = k
			freie = true
	add_child(kamera)
	kamera.make_current()
	kamera.fov = 40.0
	if freie:
		kamera.set("Tempo", 1.5)          # Köpfe stehen 0,6 m auseinander (Vorgabe der Kamera: 12 m/s)
	_blick(Vector3(0.0, 2.6, 3.0), Vector3(0.0, 2.05, -2.2))

func _blick(pos: Vector3, ziel: Vector3) -> void:
	if freie:
		kamera.call("Blicken", pos, ziel)
	else:
		kamera.global_transform = Transform3D(Basis.looking_at(ziel - pos), pos)

# Nah an einen Kopf (Ton, Mann, Alter), leicht von der Seite
func _nah(ti: int, mann: bool, jahre: int, winkel := 25.0, abstand := 0.55) -> void:
	for e in koepfe:
		if e[1] == ti and e[2] == mann and e[3] == jahre:
			var k: Kopf = e[0]
			var ziel: Vector3 = k.to_global(k.augen_mitte - Vector3(0, 0.035, 0))
			var w := deg_to_rad(winkel)
			_blick(ziel + Vector3(sin(w), 0.08, cos(w)) * abstand, ziel)
			return

func _hilfe() -> void:
	var c := CanvasLayer.new()
	add_child(c)
	hilfe = Label.new()
	hilfe.position = Vector2(14, 10)
	hilfe.add_theme_font_size_override("font_size", 16)
	hilfe.add_theme_color_override("font_color", Color(0.95, 0.9, 0.78))
	hilfe.add_theme_color_override("font_outline_color", Color(0, 0, 0))
	hilfe.add_theme_constant_override("outline_size", 4)
	c.add_child(hilfe)
	_hilfe_text()

func _hilfe_text() -> void:
	if hilfe == null:
		return
	var kam := "Kamera: rechte Maustaste halten = umsehen, WASD, Q/E runter/hoch, Shift schneller, Mausrad Tempo, H Hilfe der Kamera" if freie \
		else "Kamera: 1–6 springt zu einer Reihe (freie Kamera fehlt noch: game/scripts/FreieKamera.cs)"
	hilfe.text = "Charakter Phase 3 – Galerie (Reihen von vorn: hell Mann, hell Frau, mittel Mann, mittel Frau, dunkel Mann, dunkel Frau; 5/20/35/50/70 Jahre)\n" \
		+ kam + "\nL Licht: %s · V %s · U Ultra-SSS: %s · M Mimik %d · F1 Hilfe aus" % [
			"Tag" if ist_tag else "Innen", "vorher (Phase 2)" if vorher else "nachher (Phase 3)", "an" if ultra else "aus", mimik_nr]

func _unhandled_input(ev: InputEvent) -> void:
	if not (ev is InputEventKey and ev.pressed and not ev.echo):
		return
	match ev.keycode:
		KEY_L:
			_licht_setzen(not ist_tag)
		KEY_V:
			vorher = not vorher
			for e in koepfe:
				e[0].vorher_setzen(vorher)
		KEY_U:
			ultra = not ultra
			for e in koepfe:
				e[0].ultra_setzen(ultra)
		KEY_M:
			mimik_nr = (mimik_nr + 1) % MIMIK.size()
			for e in koepfe:
				e[0].mimik_setzen(MIMIK[mimik_nr])
		KEY_F1:
			hilfe.visible = not hilfe.visible
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6:
			if not freie:
				var reihe: int = ev.keycode - KEY_1
				_nah(reihe / 2, reihe % 2 == 0, 35, 15.0, 1.6)
	_hilfe_text()

# ---------------------------------------------------------------- Fotos und Messung (--fotos)
func _warte(n: int) -> void:
	for i in n:
		await RenderingServer.frame_post_draw

func _foto(name: String) -> void:
	await _warte(6)
	var bild := get_viewport().get_texture().get_image()
	bild.save_png(bilder.path_join(name + ".png"))
	print("FOTO ", bilder.path_join(name + ".png"))

func _fotos() -> void:
	DirAccess.make_dir_recursive_absolute(bilder)
	hilfe.visible = false
	await _warte(10)
	var ansichten := [
		["weit", func(): _blick(Vector3(0.0, 2.6, 3.0), Vector3(0.0, 2.05, -2.2))],
		["vorn_hell", func(): _blick(Vector3(0.0, 1.75, 1.45), Vector3(0.0, 1.62, 0.0))],
		["nah_m70_hell", func(): _nah(0, true, 70)],
		["nah_w20_hell", func(): _nah(0, false, 20, -20.0)],
		["nah_m50_mittel", func(): _nah(1, true, 50, 30.0)],
		["nah_w35_dunkel", func(): _nah(2, false, 35, -25.0)],
		["nah_m70_dunkel", func(): _nah(2, true, 70, 20.0)],
		["nah_m5_mittel", func(): _nah(1, true, 5, 15.0, 0.45)],
	]
	for licht in [true, false]:
		_licht_setzen(licht)
		for a in ansichten:
			a[1].call()
			await _foto("galerie_%s_%s" % ["tag" if licht else "innen", a[0]])
	# vorher/nachher nah (Tag)
	_licht_setzen(true)
	for v in [true, false]:
		for e in koepfe:
			e[0].vorher_setzen(v)
		_nah(0, true, 70)
		await _foto("galerie_tag_nah_m70_hell_%s" % ("vorher" if v else "nachher"))
	# Messung: GPU-Zeit des ganzen Bilds (30 Köpfe), Tag, still; dann mit Ultra-SSS
	if args.has("messen"):
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
		_blick(Vector3(0.0, 2.6, 3.0), Vector3(0.0, 2.05, -2.2))
		RenderingServer.viewport_set_measure_render_time(get_viewport().get_viewport_rid(), true)
		for lauf in [["tag_30", false, true], ["tag_30_ultra", true, true], ["innen_30", false, false]]:
			for e in koepfe:
				e[0].ultra_setzen(lauf[1])
			_licht_setzen(lauf[2])
			await _warte(60)
			var gpu := 0.0
			var n := 0
			var t0 := Time.get_ticks_msec()
			while Time.get_ticks_msec() - t0 < 5000:
				await RenderingServer.frame_post_draw
				gpu += RenderingServer.viewport_get_measured_render_time_gpu(get_viewport().get_viewport_rid())
				n += 1
			print("MESSUNG ", JSON.stringify({"name": "galerie_" + lauf[0], "gpu_ms": snappedf(gpu / maxf(n, 1), 0.01),
				"bilder": n, "fenster": str(get_viewport().get_visible_rect().size),
				"vram_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED) / 1048576.0, 0.1),
				"puffer_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED) / 1048576.0, 0.1),
				"textur_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED) / 1048576.0, 0.1),
				"zeichenaufrufe": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
				"last": snappedf(float(FileAccess.get_file_as_string("/proc/loadavg").split(" ")[0]), 0.1)}))
