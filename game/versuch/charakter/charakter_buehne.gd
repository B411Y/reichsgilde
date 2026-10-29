# Versuch Charakter Phase 0: Bühne für einen Kopf – eigenes SubViewport mit eigener Welt, Licht wie ein gemaltes
# Porträt (warmes Schlüssellicht links oben, kühles schwaches Fülllicht rechts, warmes Gegenlicht hinten rechts),
# AgX, gleiche Kamera für alle Köpfe. Licht und Kamera stammen aus dem eigenen Porträt-Versuch (hg_fenster.gd).
extends SubViewport

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")

var kopf: Kopf
var kamera: Camera3D
var umgebung: Environment
var schluessel: DirectionalLight3D

func _init(groesse := Vector2i(640, 640)) -> void:
	size = groesse
	own_world_3d = true
	msaa_3d = Viewport.MSAA_4X
	render_target_update_mode = SubViewport.UPDATE_ALWAYS
	positional_shadow_atlas_size = 0
	umgebung = Environment.new()
	umgebung.background_mode = Environment.BG_COLOR
	umgebung.background_color = Color(0.2, 0.18, 0.16)
	umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	umgebung.ambient_light_color = Color(0.62, 0.6, 0.58)
	umgebung.ambient_light_energy = 0.35
	umgebung.ssao_enabled = true
	umgebung.ssao_radius = 0.05
	umgebung.ssao_intensity = 1.4
	umgebung.tonemap_mode = Environment.TONE_MAPPER_AGX
	umgebung.tonemap_exposure = 1.15
	var we := WorldEnvironment.new()
	we.environment = umgebung
	add_child(we)
	schluessel = DirectionalLight3D.new()
	schluessel.light_color = Color(1.0, 0.9, 0.78)
	schluessel.light_energy = 2.6
	schluessel.shadow_enabled = true
	schluessel.shadow_blur = 1.2
	schluessel.shadow_normal_bias = 1.0
	schluessel.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	schluessel.directional_shadow_max_distance = 2.0
	add_child(schluessel)
	_richten(schluessel, Vector3(-1.6, 2.6, 1.6))
	var fuell := DirectionalLight3D.new()
	fuell.light_color = Color(0.85, 0.9, 1.0)
	fuell.light_energy = 0.45
	add_child(fuell)
	_richten(fuell, Vector3(2.0, 1.7, 1.4))
	var gegen := DirectionalLight3D.new()
	gegen.light_color = Color(1.0, 0.82, 0.6)
	gegen.light_energy = 1.6
	add_child(gegen)
	_richten(gegen, Vector3(1.3, 2.2, -2.0))
	kamera = Camera3D.new()
	kamera.fov = 20.0
	kamera.near = 0.02
	add_child(kamera)

# Licht von pos auf die Kopfmitte richten (geht auch, bevor die Bühne im Baum hängt)
static func _richten(n: Node3D, pos: Vector3, ziel := Vector3(0, 1.6, 0)) -> void:
	n.transform = Transform3D(Basis.looking_at(ziel - pos), pos)

func kopf_laden(datei: String, stoff: String) -> bool:
	kopf = Kopf.new()
	add_child(kopf)
	return kopf.laden(datei, stoff)

# Ansichten: buste, kopf, augen, mund; winkel = Grad nach rechts gedreht (¾-Ansicht etwa 35)
# spanne_m > 0: feste Bildhöhe in Metern (gleiche Kamera für Köpfe verschiedener Größe), ziel_m = Bildmitte über den Augen
func ansicht(art: String, winkel := 0.0, hoehe := 3.0, spanne_m := 0.0, ziel_m := 0.0) -> void:
	if kopf == null:
		return
	var e := kopf.augen_mitte
	var s := maxf(kopf.oben - e.y, 0.05)          # Augen bis Scheitel, Erwachsener etwa 0,12 m
	var ziel := Vector3(0.0, e.y, e.z)
	var spanne := 1.0
	match art:
		"buste":
			# Kopf und Schulteransatz (der Zuschnitt unten bleibt außerhalb des Bildes)
			ziel.y = e.y - 0.95 * s
			spanne = 4.3 * s
		"kopf":
			ziel.y = e.y - 0.45 * s
			spanne = 2.75 * s
		"augen":
			spanne = 0.85 * s
		"mund":
			ziel = Vector3(0.0, kopf.mund_mitte.y + 0.01 * s, kopf.mund_mitte.z)
			spanne = 0.85 * s
	if spanne_m > 0.0:
		spanne = spanne_m
		ziel.y = e.y + ziel_m
	# Drehpunkt in Kopfmitte (hinter den Augen), damit das Gesicht in der ¾-Ansicht in der Mitte bleibt
	var dreh := Vector3(0.0, ziel.y, e.z - 0.75 * s)
	if art in ["augen", "mund"]:
		dreh = Vector3(ziel.x, ziel.y, ziel.z - 0.35 * s)   # nah: um den Mund bzw. die Augen drehen
	var abstand := (ziel.z - dreh.z) + spanne * 0.5 / tan(deg_to_rad(kamera.fov * 0.5))
	var w := deg_to_rad(winkel)
	var h := deg_to_rad(hoehe)
	var pos := dreh + Vector3(sin(w) * cos(h), sin(h), cos(w) * cos(h)) * abstand
	var blick := dreh + (ziel - dreh).rotated(Vector3.UP, w)
	kamera.transform = Transform3D(Basis.looking_at(blick - pos), pos)

func foto() -> Image:
	return get_texture().get_image()
