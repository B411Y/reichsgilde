# Versuch Charakter (Phase 0, Phase 3: Belichtung 1,0 statt 1,15, Schlüssellicht 2,3 statt 2,6): Bühne für einen Kopf – eigenes SubViewport mit eigener Welt, Licht wie ein gemaltes
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
	umgebung.tonemap_exposure = 1.0          # Phase 3: 1,15 -> 1,0 (Haut war überbelichtet)
	var we := WorldEnvironment.new()
	we.environment = umgebung
	add_child(we)
	schluessel = DirectionalLight3D.new()
	schluessel.light_color = Color(1.0, 0.9, 0.78)
	schluessel.light_energy = 2.3
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

# Phase 3: freie Kamera (game/scripts/FreieKamera.cs) statt der festen, für die Regler-Ansicht (jede Testszene startet
# mit freier Kamera); ansicht() stellt sie dann nur neu hin. Gibt false zurück, wenn es die Kamera nicht gibt.
const FREIE_KAMERA := "res://scripts/FreieKamera.cs"
var freie := false
func freie_kamera_einsetzen() -> bool:
	if not ResourceLoader.exists(FREIE_KAMERA):
		return false
	var skript: Script = load(FREIE_KAMERA)
	var k: Node = skript.new() if skript and skript.can_instantiate() else null
	if not k is Camera3D:
		return false
	var alt := kamera
	k.fov = alt.fov
	k.near = alt.near
	add_child(k)
	k.make_current()
	k.set("Tempo", 0.5)
	kamera = k
	alt.queue_free()
	freie = true
	return true

# Licht von pos auf die Kopfmitte richten (geht auch, bevor die Bühne im Baum hängt)
static func _richten(n: Node3D, pos: Vector3, ziel := Vector3(0, 1.6, 0)) -> void:
	n.transform = Transform3D(Basis.looking_at(ziel - pos), pos)

func kopf_laden(datei: String) -> bool:
	kopf = Kopf.new()
	add_child(kopf)
	return kopf.laden(datei)

# Ansichten: portraet (Kopf, Kragen, Schulteransatz), buste, kopf, augen, mund; winkel = Grad nach rechts gedreht (¾-Ansicht etwa 35)
# spanne_m > 0: feste Bildhöhe in Metern (gleiche Kamera für Köpfe verschiedener Größe), ziel_m = Bildmitte über den Augen
func ansicht(art: String, winkel := 0.0, hoehe := 3.0, spanne_m := 0.0, ziel_m := 0.0) -> void:
	if kopf == null:
		return
	var e := kopf.augen_mitte
	var s := maxf(kopf.oben - e.y, 0.05)          # Augen bis Scheitel, Erwachsener etwa 0,12 m
	var ziel := Vector3(0.0, e.y, e.z)
	var spanne := 1.0
	match art:
		"portraet":
			ziel.y = e.y - 0.52 * s
			spanne = 3.2 * s
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
	if freie:
		kamera.call("Blicken", pos, blick)
	else:
		kamera.transform = Transform3D(Basis.looking_at(blick - pos), pos)

# Einstellungen der Umgebung aus einem Bogen ({"ssao_radius": 0.02, "ssao_enabled": false, …}); Unbekanntes wird
# gemeldet. Vor jeder Zelle gelten wieder die Grundwerte der Bühne.
var _umgebung_grund := {}
func umgebung_setzen(werte: Dictionary) -> void:
	for k in _umgebung_grund:
		umgebung.set(k, _umgebung_grund[k])
	for k in werte:
		if not k in umgebung:
			print("WARNUNG Umgebung kennt ", k, " nicht")
			continue
		if not _umgebung_grund.has(k):
			_umgebung_grund[k] = umgebung.get(k)
		umgebung.set(k, werte[k])

# Schlüssellicht aus einem Bogen ({"shadow_enabled": false, "light_energy": 2.0}), wie umgebung_setzen
var _licht_grund := {}
func licht_setzen(werte: Dictionary) -> void:
	for k in _licht_grund:
		schluessel.set(k, _licht_grund[k])
	for k in werte:
		if not k in schluessel:
			print("WARNUNG Schlüssellicht kennt ", k, " nicht")
			continue
		if not _licht_grund.has(k):
			_licht_grund[k] = schluessel.get(k)
		schluessel.set(k, werte[k])

func foto() -> Image:
	return get_texture().get_image()
