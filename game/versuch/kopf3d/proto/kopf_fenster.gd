# Kleines Porträtfenster: SubViewport mit Kopf, warmer Lichtführung und Rahmen im Stil der Oberfläche
# (dunkles Holz, schwarze Doppellinie, Gold). Eine Instanz = ein Kopf mit eigener 3D-Welt.
class_name KopfFenster
extends PanelContainer

var kopf: Node3D
var ansicht: SubViewport
var kamera: Camera3D
var hintergrund := Color(0.80, 0.74, 0.64)
var kamera_ziel := Vector3(0.40, 1.64, 1.0)
var dy := 0.0
static var sparsam := false     # viele kleine Fenster (Stammbaum): MSAA 2x, ohne SSAO

var _quelle := ""
var _gemalt := false

func _init(groesse := Vector2i(384, 448), hg: Color = Color(0.80, 0.74, 0.64), quelle := "", gemalt := false) -> void:
	hintergrund = hg
	_quelle = quelle
	_gemalt = gemalt
	var rahmen := StyleBoxFlat.new()
	rahmen.bg_color = Color(0.16, 0.15, 0.13)
	rahmen.set_border_width_all(3)
	rahmen.border_color = Color(0.04, 0.03, 0.02)
	rahmen.set_content_margin_all(7)
	rahmen.shadow_size = 4
	rahmen.shadow_color = Color(0, 0, 0, 0.4)
	add_theme_stylebox_override("panel", rahmen)
	var innen := PanelContainer.new()
	var linie := StyleBoxFlat.new()
	linie.bg_color = Color(0, 0, 0, 0)
	linie.set_border_width_all(2)
	linie.border_color = Color(0.95, 0.82, 0.45)
	linie.set_content_margin_all(3)
	innen.add_theme_stylebox_override("panel", linie)
	add_child(innen)
	var innen2 := PanelContainer.new()
	var linie2 := StyleBoxFlat.new()
	linie2.bg_color = Color(0, 0, 0, 0)
	linie2.set_border_width_all(2)
	linie2.border_color = Color(0.04, 0.03, 0.02)
	linie2.set_content_margin_all(0)
	innen2.add_theme_stylebox_override("panel", linie2)
	innen.add_child(innen2)
	var behaelter := SubViewportContainer.new()
	behaelter.stretch = true
	behaelter.custom_minimum_size = Vector2(groesse)
	innen2.add_child(behaelter)
	ansicht = SubViewport.new()
	ansicht.size = groesse
	ansicht.own_world_3d = true
	ansicht.msaa_3d = Viewport.MSAA_2X if sparsam else Viewport.MSAA_4X
	ansicht.positional_shadow_atlas_size = 0
	RenderingServer.directional_shadow_atlas_set_size(1024, false)
	ansicht.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	behaelter.add_child(ansicht)
	_welt_bauen()

func _ready() -> void:
	kamera.look_at_from_position(kamera_ziel + Vector3(0, dy, 0), Vector3(0.0, 1.595 + dy, 0.0))

func _welt_bauen() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = hintergrund
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.62, 0.56, 0.5)
	env.ambient_light_energy = 0.38 if not _gemalt else 0.8
	env.ssao_enabled = not sparsam
	env.ssao_radius = 0.05
	env.ssao_intensity = 2.5
	env.ssao_power = 1.6
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 1.05
	var we := WorldEnvironment.new()
	we.environment = env
	ansicht.add_child(we)
	# Hauptlicht: warm, von links oben vorn, weiche Schatten
	var key := DirectionalLight3D.new()
	key.light_color = Color(1.0, 0.88, 0.72)
	key.light_energy = 2.6 if not _gemalt else 1.5
	key.shadow_enabled = true
	key.shadow_blur = 1.2
	key.shadow_normal_bias = 1.0
	key.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	key.directional_shadow_max_distance = 2.0
	ansicht.add_child(key)
	key.look_at_from_position(Vector3(-1.9, 2.7, 1.4), Vector3(0, 1.6, 0))
	# Fülllicht: kühl, schwach, von rechts
	var fuell := DirectionalLight3D.new()
	fuell.light_color = Color(0.65, 0.76, 1.0)
	fuell.light_energy = 0.2 if not _gemalt else 0.5
	ansicht.add_child(fuell)
	fuell.look_at_from_position(Vector3(2.0, 1.4, 1.6), Vector3(0, 1.6, 0))
	# Kantenlicht: warm von hinten rechts, hebt Haar und Schulter vom Grund
	var kante := DirectionalLight3D.new()
	kante.light_color = Color(1.0, 0.8, 0.55)
	kante.light_energy = 0.9
	ansicht.add_child(kante)
	kante.look_at_from_position(Vector3(1.4, 2.2, -2.0), Vector3(0, 1.6, 0))
	kamera = Camera3D.new()
	kamera.fov = 24.0
	ansicht.add_child(kamera)
	if _quelle.ends_with("_d.glb"):
		kopf = KopfD.new()
		kopf.quelle = _quelle
	elif _quelle.ends_with("_c.glb") or _quelle.ends_with("_cp.glb"):
		kopf = KopfC.new()
		kopf.quelle = _quelle
	else:
		kopf = KopfAnsicht.new()
		if _quelle != "":
			kopf.quelle = _quelle
		kopf.gemalt = _gemalt
	ansicht.add_child(kopf)
