# Versuch: lebendiger Porträtkopf aus Human Generator 3D (Brustbild-GLB aus tools/versuch_hg/hg_kopf.py).
# Ein Netz „Kopf“ mit Formen (Blendshapes): ARKit-ähnliche Gesichtsformen (jawOpen, mouthFunnel …), Zunge,
# Regler (kiefer_breite, nase_ruecken …, alter) und Rassenformen (elb). Knochen nur für Kopf, Hals, Augen, Atmen.
# Sprechen über Viseme a/e/i/o/u/m/f, die aus den Gesichtsformen gemischt werden.
class_name HgKopf
extends Node3D

var quelle := ""
var animiert := true         # Atmen, Blinzeln, Blick, kleine Kopfbewegung
var blick_wechsel := true    # Blick springt ab und zu (Sakkaden)

var netz: MeshInstance3D
var skelett: Skeleton3D
var _formen := {}            # Name -> Index der Form
var _soll := {}              # Gesichtsformen: Zielwert (weich angefahren)
var _ist := {}
var _fest := {}              # Regler und Rassenformen: fester Wert
var _t := 0.0
var _blinzel_t := 2.0
var _blinzel := 0.0
var _blick := Vector2.ZERO        # x: rechts/links, y: hoch/runter (in Grad)
var _blick_soll := Vector2.ZERO
var _blick_t := 1.5
var _rede: Array = []             # [[viseme, dauer], …]
var _rede_t := 0.0
var _ausdruck := {}               # z. B. Lächeln, unabhängig vom Mund
var _ruhe := {}                   # Ruhelage der Knochen (Rotation)
var _knochen := {}
var _haut_mat: ShaderMaterial
var _haar_mats: Array[ShaderMaterial] = []

# Viseme: Mischung aus Gesichtsformen (Werte 0..1)
const VISEME := {
	"a": {"jawOpen": 0.5, "mouthLowerDown_L": 0.25, "mouthLowerDown_R": 0.25, "mouthUpperUp_L": 0.1, "mouthUpperUp_R": 0.1},
	"e": {"jawOpen": 0.22, "mouthStretch_L": 0.45, "mouthStretch_R": 0.45, "mouthSmile_L": 0.15, "mouthSmile_R": 0.15, "mouthLowerDown_L": 0.2, "mouthLowerDown_R": 0.2},
	"i": {"jawOpen": 0.1, "mouthSmile_L": 0.4, "mouthSmile_R": 0.4, "mouthStretch_L": 0.35, "mouthStretch_R": 0.35},
	"o": {"jawOpen": 0.32, "mouthFunnel": 0.65, "mouthPucker": 0.2},
	"u": {"jawOpen": 0.1, "mouthPucker": 0.75, "mouthFunnel": 0.35},
	"m": {"mouthPress_L": 0.55, "mouthPress_R": 0.55, "mouthRollLower": 0.15, "mouthRollUpper": 0.1},
	"f": {"jawOpen": 0.06, "mouthRollLower": 0.6, "mouthUpperUp_L": 0.2, "mouthUpperUp_R": 0.2},
	"l": {"jawOpen": 0.22, "tongue_up": 0.8, "mouthLowerDown_L": 0.1, "mouthLowerDown_R": 0.1},
	"s": {"jawOpen": 0.05, "mouthStretch_L": 0.3, "mouthStretch_R": 0.3, "mouthSmile_L": 0.15, "mouthSmile_R": 0.15},
	"_": {},
}

func _ready() -> void:
	var szene: PackedScene = load(quelle)
	var k := szene.instantiate()
	add_child(k)
	netz = _finde(k, "MeshInstance3D") as MeshInstance3D
	skelett = _finde(k, "Skeleton3D") as Skeleton3D
	for i in netz.mesh.get_blend_shape_count():
		_formen[String(netz.mesh.get_blend_shape_name(i))] = i
	for b in skelett.get_bone_count():
		_knochen[skelett.get_bone_name(b)] = b
		_ruhe[skelett.get_bone_name(b)] = skelett.get_bone_pose_rotation(b)
	_materialien()

func _finde(n: Node, typ: String) -> Node:
	if n.is_class(typ):
		return n
	for c in n.get_children():
		var r := _finde(c, typ)
		if r:
			return r
	return null

# Haare als Haarkarten mit Alpha-Test + Kantenglättung über MSAA, Haut mit etwas Streulicht
func _materialien() -> void:
	var m := netz.mesh
	for i in m.get_surface_count():
		var mat := m.surface_get_material(i) as StandardMaterial3D
		if mat == null:
			continue
		mat = mat.duplicate()
		var n := mat.resource_name
		if n.begins_with("Haar") and OS.get_cmdline_user_args().has("--rot"):
			mat.transparency = BaseMaterial3D.TRANSPARENCY_DISABLED
			mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			mat.albedo_texture = null
			mat.albedo_color = Color.from_hsv(float(n.hash() % 97) / 97.0, 1, 1)
			print("ROT ", n, " ", mat.albedo_color)
			mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		elif (n.begins_with("Haar") and OS.get_cmdline_user_args().has("--ohne-haar")) or (n == "Zaehne" and OS.get_cmdline_user_args().has("--ohne-zaehne")) or (n == "Hornhaut" and OS.get_cmdline_user_args().has("--ohne-hornhaut")):
			var weg := StandardMaterial3D.new()
			weg.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			weg.albedo_color = Color(0, 0, 0, 0)
			netz.set_surface_override_material(i, weg)
			continue
		elif n.begins_with("Haar"):
			var sm := ShaderMaterial.new()
			sm.shader = preload("res://versuch/hg/haar.gdshader")
			sm.set_shader_parameter("farbe", mat.albedo_texture)
			var saum := ShaderMaterial.new()
			saum.shader = preload("res://versuch/hg/haar_saum.gdshader")
			saum.set_shader_parameter("farbe", mat.albedo_texture)
			saum.render_priority = 1
			# Saum-Durchgang (weiche Kanten) nur im großen Porträt; viele kleine Fenster glättet MSAA allein
			if not OS.get_cmdline_user_args().has("--ohne-saum") and not HgFenster.sparsam:
				sm.next_pass = saum
			_haar_mats.append(sm)
			_haar_mats.append(saum)
			netz.set_surface_override_material(i, sm)
			continue
		elif n == "Haut":
			var hm := ShaderMaterial.new()
			hm.shader = preload("res://versuch/hg/haut.gdshader")
			hm.set_shader_parameter("farbe", mat.albedo_texture)
			hm.set_shader_parameter("normalen", mat.normal_texture)
			hm.set_shader_parameter("rauheit", mat.roughness_texture)
			_haut_mat = hm
			netz.set_surface_override_material(i, hm)
			continue
		elif n == "Hornhaut":
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.albedo_color = Color(1, 1, 1, 0.03)
			mat.roughness = 0.04
			mat.metallic_specular = 0.5
			mat.render_priority = 1
		elif n == "Augen":
			var am := ShaderMaterial.new()
			am.shader = preload("res://versuch/hg/augen.gdshader")
			am.set_shader_parameter("farbe", mat.albedo_texture)
			netz.set_surface_override_material(i, am)
			continue
		netz.set_surface_override_material(i, mat)

# Hauttypen je Volk (nur Material; die Grundtextur kommt aus HG). Werte: Shader-Parameter von haut.gdshader.
const HAUTTYPEN := {
	"mensch_hell": {},
	"mensch_mittel": {"toenung": Color(0.86, 0.74, 0.62), "saettigung": 1.05},
	"mensch_dunkel": {"toenung": Color(0.44, 0.34, 0.28), "saettigung": 0.85, "glanz": 0.34, "streu_farbe": Color(0.6, 0.3, 0.2)},
	"elb": {"toenung": Color(1.04, 1.0, 0.98), "saettigung": 0.82, "poren": 0.55, "rauh_versatz": 0.0, "glanz": 0.36, "streu": 0.6},
	"zwerg": {"toenung": Color(0.95, 0.8, 0.68), "saettigung": 1.12, "gegerbt": 0.8, "poren": 1.4, "glanz": 0.26},
	"ork": {"toenung": Color(0.62, 0.72, 0.5), "saettigung": 0.6, "gegerbt": 0.5, "poren": 1.3, "streu_farbe": Color(0.5, 0.55, 0.25)},
	"oger": {"toenung": Color(0.66, 0.68, 0.62), "saettigung": 0.45, "gegerbt": 0.9, "poren": 1.6, "streu_farbe": Color(0.6, 0.4, 0.3)},
}

func setze_hauttyp(typ: String) -> void:
	if _haut_mat == null or not HAUTTYPEN.has(typ):
		return
	var werte: Dictionary = HAUTTYPEN[typ]
	for k in ["toenung", "saettigung", "helligkeit", "poren", "rauh_versatz", "glanz", "streu", "streu_farbe", "gegerbt"]:
		_haut_mat.set_shader_parameter(k, _haut_standard(k))
	for k in werte:
		var v = werte[k]
		if v is Color:
			v = Vector3(v.r, v.g, v.b)
		_haut_mat.set_shader_parameter(k, v)

func _haut_standard(k: String) -> Variant:
	match k:
		"toenung": return Vector3.ONE
		"saettigung", "helligkeit", "poren": return 1.0
		"rauh_versatz": return 0.12
		"glanz": return 0.26
		"streu": return 0.38
		"streu_farbe": return Vector3(0.9, 0.42, 0.32)
	return 0.0

func setze_haarfarbe(toenung: Color) -> void:
	for m in _haar_mats:
		m.set_shader_parameter("toenung", Vector3(toenung.r, toenung.g, toenung.b))

func hat_form(name: String) -> bool:
	return _formen.has(name)

func formen() -> Array:
	return _formen.keys()

# Regler/Rasse: bleibt stehen, bis neu gesetzt
func setze_fest(name: String, wert: float) -> void:
	_fest[name] = wert
	if _formen.has(name):
		netz.set_blend_shape_value(_formen[name], wert)

func setze_ausdruck(werte: Dictionary) -> void:
	_ausdruck = werte

# Ein Satz: Buchstaben -> Viseme (grob, reicht für den Eindruck)
func sprich(text: String) -> void:
	_rede.clear()
	var t := text.to_lower()
	var i := 0
	while i < t.length():
		var c := t[i]
		var v := "_"
		var d := 0.075
		if c in "aä": v = "a"; d = 0.11
		elif c in "eé": v = "e"; d = 0.09
		elif c in "iyj": v = "i"; d = 0.08
		elif c in "oö": v = "o"; d = 0.11
		elif c in "uü": v = "u"; d = 0.1
		elif c in "mbp": v = "m"; d = 0.08
		elif c in "fvw": v = "f"; d = 0.08
		elif c in "lntdr": v = "l"; d = 0.06
		elif c in "szßcx": v = "s"; d = 0.07
		elif c in "ghkq": v = "e"; d = 0.06
		elif c in ",;": v = "_"; d = 0.22
		elif c in ".!?": v = "_"; d = 0.4
		elif c == " ": v = "_"; d = 0.05
		_rede.append([v, d])
		i += 1
	_rede.append(["_", 0.3])
	_rede_t = 0.0

func spricht() -> bool:
	return not _rede.is_empty()

func _process(dt: float) -> void:
	_t += dt
	# Mund: aktuelles Viseme aus dem Satz
	var mund := {}
	if not _rede.is_empty():
		_rede_t += dt
		while not _rede.is_empty() and _rede_t > _rede[0][1]:
			_rede_t -= _rede[0][1]
			_rede.pop_front()
		if not _rede.is_empty():
			mund = VISEME[_rede[0][0]]
	var soll := {}
	for k in _ausdruck:
		soll[k] = _ausdruck[k]
	for k in mund:
		soll[k] = maxf(soll.get(k, 0.0), mund[k])
	# Blinzeln
	if animiert:
		_blinzel_t -= dt
		if _blinzel_t <= 0.0:
			_blinzel_t = randf_range(2.0, 5.5)
			_blinzel = 0.001
		if _blinzel > 0.0:
			_blinzel += dt / 0.17
			if _blinzel >= 1.0:
				_blinzel = 0.0
	var b := sin(_blinzel * PI) if _blinzel > 0.0 else 0.0
	soll["eyeBlink_L"] = maxf(soll.get("eyeBlink_L", 0.0), b)
	soll["eyeBlink_R"] = maxf(soll.get("eyeBlink_R", 0.0), b)
	# Weich anfahren (Mund schnell, Rest ruhiger)
	var alle := {}
	for k in _ist: alle[k] = true
	for k in soll: alle[k] = true
	for k in alle:
		var z: float = soll.get(k, 0.0)
		var a: float = _ist.get(k, 0.0)
		var tempo := 40.0 if k.begins_with("eyeBlink") else 18.0
		a = lerpf(a, z, 1.0 - exp(-tempo * dt))
		_ist[k] = a
		if _formen.has(k):
			netz.set_blend_shape_value(_formen[k], a)
	_blick_und_haltung(dt)

func _blick_und_haltung(dt: float) -> void:
	if animiert and blick_wechsel:
		_blick_t -= dt
		if _blick_t <= 0.0:
			_blick_t = randf_range(0.8, 3.0)
			_blick_soll = Vector2(randf_range(-9, 9), randf_range(-5, 4)) if randf() < 0.6 else Vector2.ZERO
	_blick = _blick.lerp(_blick_soll, 1.0 - exp(-30.0 * dt))
	# Lider folgen dem Blick (eyeLook*-Formen)
	var hoch := clampf(_blick.y / 20.0, 0, 1); var runter := clampf(-_blick.y / 20.0, 0, 1)
	for s in ["Left", "Right"]:
		_form_setzen("eyeLookUp" + s, hoch)
		_form_setzen("eyeLookDown" + s, runter)
	for seite in ["L", "R"]:
		var bn: String = "eyeball." + seite
		if _knochen.has(bn):
			_drehe(bn, Vector3(deg_to_rad(-_blick.y), deg_to_rad(_blick.x), 0))
	if not animiert:
		return
	# Atmen: Brustkorb hebt sich, Schultern leicht
	var atem := sin(_t * TAU / 4.2)
	_drehe("spine.003", Vector3(deg_to_rad(atem * 0.8), 0, 0))
	_drehe("neck", Vector3(deg_to_rad(-atem * 0.5 + sin(_t * 0.37) * 1.2), deg_to_rad(sin(_t * 0.23) * 2.0), 0))
	_drehe("head", Vector3(deg_to_rad(sin(_t * 0.31 + 1.0) * 1.5), deg_to_rad(sin(_t * 0.19 + 2.0) * 2.5 + _blick.x * 0.15), deg_to_rad(sin(_t * 0.27) * 1.2)))

func _form_setzen(name: String, wert: float) -> void:
	if _formen.has(name):
		netz.set_blend_shape_value(_formen[name], wert)

# Drehung im Modellraum (x: nicken, y: drehen, z: neigen) auf die Ruhelage des Knochens
func _drehe(bn: String, euler: Vector3) -> void:
	if not _knochen.has(bn):
		return
	var i: int = _knochen[bn]
	var glob := skelett.get_bone_global_rest(i).basis.orthonormalized()
	var q_modell := Quaternion.from_euler(euler)
	var q_lokal := Quaternion(glob.inverse() * Basis(q_modell) * glob)
	skelett.set_bone_pose_rotation(i, (_ruhe[bn] as Quaternion) * q_lokal)
