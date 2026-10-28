# Versuch: Porträtfenster für einen HG-Kopf. SubViewport mit eigener 3D-Welt, Licht wie ein gemaltes Porträt
# (warmes Hauptlicht von links oben, kühles schwaches Fülllicht, Kantenlicht von hinten), Rahmen wie die Oberfläche.
class_name HgFenster
extends PanelContainer

var kopf: HgKopf
var ansicht: SubViewport
var kamera: Camera3D
var hintergrund := Color(0.42, 0.38, 0.32)
var blick_ziel := Vector3.ZERO     # Mitte des Gesichts (aus dem Kopfknochen)
var abstand := 0.95
var winkel := Vector2(12.0, 2.0)   # Kamera: Grad seitlich, Grad von oben
static var sparsam := false        # viele kleine Fenster: MSAA 2x, ohne SSAO

func _init(groesse := Vector2i(384, 448), quelle := "", hg: Color = Color(0.42, 0.38, 0.32)) -> void:
	hintergrund = hg
	var rahmen := StyleBoxFlat.new()
	rahmen.bg_color = Color(0.16, 0.15, 0.13)
	rahmen.set_border_width_all(3)
	rahmen.border_color = Color(0.04, 0.03, 0.02)
	rahmen.set_content_margin_all(6)
	add_theme_stylebox_override("panel", rahmen)
	var innen := PanelContainer.new()
	var linie := StyleBoxFlat.new()
	linie.bg_color = Color(0, 0, 0, 0)
	linie.set_border_width_all(2)
	linie.border_color = Color(0.85, 0.7, 0.38)
	linie.set_content_margin_all(2)
	innen.add_theme_stylebox_override("panel", linie)
	add_child(innen)
	var behaelter := SubViewportContainer.new()
	behaelter.stretch = true
	behaelter.custom_minimum_size = Vector2(groesse)
	innen.add_child(behaelter)
	ansicht = SubViewport.new()
	ansicht.size = groesse
	ansicht.own_world_3d = true
	ansicht.msaa_3d = Viewport.MSAA_2X if sparsam else Viewport.MSAA_4X
	ansicht.positional_shadow_atlas_size = 0
	# Schattenkarte des Hauptlichts: Standard 4096 ist für ein Porträt viel zu groß (kostet bei 8 Fenstern die Hälfte der Zeit)
	RenderingServer.directional_shadow_atlas_set_size(1024 if sparsam else 2048, true)
	ansicht.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	behaelter.add_child(ansicht)
	_welt_bauen()
	kopf = HgKopf.new()
	kopf.quelle = quelle
	ansicht.add_child(kopf)

func _ready() -> void:
	var sk := kopf.skelett
	var hi := sk.find_bone("head")
	var hp := sk.global_transform * sk.get_bone_global_rest(hi).origin
	blick_ziel = hp + Vector3(0, 0.075, 0.03)
	kamera_setzen()

func kamera_setzen() -> void:
	var w := deg_to_rad(winkel.x); var h := deg_to_rad(winkel.y)
	var pos := blick_ziel + Vector3(sin(w) * cos(h), sin(h), cos(w) * cos(h)) * abstand
	kamera.look_at_from_position(pos, blick_ziel - Vector3(0, 0.035, 0))

func _welt_bauen() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = hintergrund
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.6, 0.58, 0.56)
	env.ambient_light_energy = 0.45
	env.ssao_enabled = not sparsam
	env.ssao_radius = 0.06
	env.ssao_intensity = 1.6
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 1.0
	var we := WorldEnvironment.new()
	we.environment = env
	ansicht.add_child(we)
	var key := DirectionalLight3D.new()
	key.light_color = Color(1.0, 0.92, 0.82)
	key.light_energy = 2.4
	key.shadow_enabled = not OS.get_cmdline_user_args().has("--ohne-schatten")
	key.shadow_blur = 1.5
	key.shadow_normal_bias = 1.2
	key.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	key.directional_shadow_max_distance = 2.0
	ansicht.add_child(key)
	key.look_at_from_position(Vector3(-1.8, 2.8, 1.5), Vector3(0, 1.6, 0))
	# Fülllicht nur im großen Porträt (jedes Richtungslicht kostet in Haut- und Haar-Shader eine ganze Lichtrechnung)
	if sparsam:
		env.ambient_light_energy = 0.6
	var fuell := DirectionalLight3D.new()
	fuell.visible = not sparsam
	fuell.light_color = Color(0.92, 0.92, 0.95)
	fuell.light_energy = 0.35
	ansicht.add_child(fuell)
	fuell.look_at_from_position(Vector3(2.0, 1.5, 1.6), Vector3(0, 1.6, 0))
	var kante := DirectionalLight3D.new()
	kante.light_color = Color(1.0, 0.82, 0.58)
	kante.light_energy = 1.1
	ansicht.add_child(kante)
	kante.look_at_from_position(Vector3(1.4, 2.3, -2.0), Vector3(0, 1.6, 0))
	kamera = Camera3D.new()
	kamera.fov = 22.0
	kamera.near = 0.02
	ansicht.add_child(kamera)
