# Plan D: TRELLIS-Kopf mit Porträt als zweiter UV, eigenen Formen (Lippen, Lächeln, Brauen, Augen zu) und Skelett (Hals, Kopf, Kiefer).
# Gleiche Schnittstelle wie KopfC, soweit Tafeln und Fenster sie brauchen.
class_name KopfD
extends Node3D

const SHADER := preload("res://versuch/kopf3d/proto/kopf_d.gdshader")

var quelle := ""
var animiert := true
var blick_wechsel := true
var kiefer_max := 0.13       # größte Kieferöffnung in Bogenmaß (rund 7,5 Grad): kleine Öffnungen beim Sprechen
var ausdruck := {}           # Grundausdruck, z. B. {"laecheln": 0.6}; Sprechen legt sich darüber

var _skel: Skeleton3D
var _netz: MeshInstance3D
var _mat: ShaderMaterial
var _i_hals := -1
var _i_kopf := -1
var _i_kief := -1
var _base := {}
var _zeit := randf() * 10.0
var _blink_t := 1.5
var _blink_phase := -1.0
var _blink_fix := -1.0
var _blick_t := 2.0
var _blick := Vector2.ZERO
var _blick_ziel := Vector2.ZERO
var _sprech: Array = []
var _sprech_t := 0.0
var _sprech_i := -1
var _brauen_puls := 0.0
var _halte := {}
var _ist := {}

const FORMEN := ["lippen_auf", "mund_rund", "mund_breit", "mund_press", "lippe_f", "laecheln_l", "laecheln_r", "augen_zu", "brauen_hoch"]

# Viseme: kleine Öffnungen, Lippen über die volle Breite, Kiefer wenig
const VISEM := {
	"a": {"kiefer": 0.55, "lippen_auf": 0.9},
	"e": {"kiefer": 0.3, "lippen_auf": 0.55, "mund_breit": 0.55},
	"i": {"kiefer": 0.15, "lippen_auf": 0.4, "mund_breit": 0.85},
	"o": {"kiefer": 0.4, "lippen_auf": 0.5, "mund_rund": 0.7},
	"u": {"kiefer": 0.15, "lippen_auf": 0.2, "mund_rund": 1.0},
	"m": {"mund_press": 1.0},
	"f": {"kiefer": 0.08, "lippe_f": 1.0},
	"s": {"kiefer": 0.1, "lippen_auf": 0.3, "mund_breit": 0.35},
	"t": {"kiefer": 0.2, "lippen_auf": 0.4},
	"-": {},
}

func _ready() -> void:
	var szene: PackedScene = load(quelle)
	var wurzel := szene.instantiate()
	add_child(wurzel)
	_skel = _finde_skelett(wurzel)
	_i_hals = _skel.find_bone("hals"); _i_kopf = _skel.find_bone("kopf"); _i_kief = _skel.find_bone("kiefer")
	var basis := quelle.get_base_dir()
	var name_ := quelle.get_file().get_basename()
	_mat = ShaderMaterial.new()
	_mat.shader = SHADER
	_mat.set_shader_parameter("tex_atlas", load(basis.path_join(name_ + "_atlas.jpg")))
	_mat.set_shader_parameter("tex_portraet", load(basis.path_join(name_ + "_portraet.jpg")))
	_mat.set_shader_parameter("tex_portraet_zu", load(basis.path_join(name_ + "_portraet_zu.jpg")))
	_mat.set_shader_parameter("tex_augen", load(basis.path_join(name_ + "_augen.png")))
	var info: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(basis.path_join(name_ + ".json")))
	_mat.set_shader_parameter("achse_u", Vector3(info["achse_u"][0], info["achse_u"][1], info["achse_u"][2]))
	_mat.set_shader_parameter("achse_v", Vector3(info["achse_v"][0], info["achse_v"][1], info["achse_v"][2]))
	for m in _alle_netze(wurzel):
		var n := String(m.name)
		if n.begins_with("gesicht"):
			m.material_override = _mat
			_netz = m
		elif n.begins_with("zaehne"):
			var zm := StandardMaterial3D.new()
			zm.vertex_color_use_as_albedo = true
			zm.roughness = 0.4
			zm.albedo_color = Color(0.92, 0.9, 0.86)
			m.material_override = zm
		elif n.begins_with("zunge"):
			var tm := StandardMaterial3D.new()
			tm.albedo_color = Color(0.3, 0.09, 0.08)
			tm.roughness = 0.5
			m.material_override = tm
	for i in [_i_hals, _i_kopf, _i_kief]:
		if i >= 0:
			_base[i] = _skel.get_bone_pose_rotation(i)

func _finde_skelett(n: Node) -> Skeleton3D:
	if n is Skeleton3D:
		return n
	for k in n.get_children():
		var r := _finde_skelett(k)
		if r:
			return r
	return null

func _alle_netze(n: Node) -> Array:
	var l := []
	if n is MeshInstance3D:
		l.append(n)
	for k in n.get_children():
		l.append_array(_alle_netze(k))
	return l

# Knochen um sein Gelenk mit einer Weltdrehung r drehen, ausgehend von der importierten Grundhaltung.
func _drehe(i: int, r: Basis) -> void:
	if i < 0:
		return
	var par := _skel.get_bone_parent(i)
	var bp := Basis.IDENTITY if par < 0 else _skel.get_bone_global_pose(par).basis
	var rl := _skel.get_bone_rest(i).basis
	var lokal := bp.inverse() * r * bp * rl * Basis(_base.get(i, Quaternion.IDENTITY))
	_skel.set_bone_pose_rotation(i, (rl.inverse() * lokal).get_rotation_quaternion())

# Kompatibilität zu den Tafeln (KopfC): Blinzeln fest setzen
func _form(name: String, wert: float) -> void:
	if name == "eyeBlinkLeft" or name == "eyeBlinkRight":
		_blink_fix = wert

# Feste Mundhaltung; ARKit-Namen der alten Tafeln werden übersetzt.
func mund_haltung(ziel: Dictionary) -> void:
	var h := {}
	for k in ziel:
		var v: float = ziel[k]
		match String(k):
			"jawOpen": h["kiefer"] = v
			"mouthLowerDownLeft", "mouthLowerDownRight": h["lippen_auf"] = maxf(h.get("lippen_auf", 0.0), v * 2.0)
			"mouthSmileLeft": h["laecheln_l"] = minf(v, 1.0)
			"mouthSmileRight": h["laecheln_r"] = minf(v, 1.0)
			"mouthFunnel", "mouthPucker": h["mund_rund"] = v
			"mouthClose", "mouthPressLeft", "mouthPressRight": h["mund_press"] = maxf(h.get("mund_press", 0.0), v)
			_: h[k] = v
	if h.has("laecheln_l") and h.has("kiefer"):
		h["kiefer"] = minf(h["kiefer"], 0.1)
	_halte = h

func blinzeln() -> void:
	_blink_phase = 0.0

func sprechen_text(text: String, tempo := 0.075) -> void:
	_sprech.clear()
	for ch in text.to_lower():
		var k := "t"
		var d := tempo
		match ch:
			"a", "ä": k = "a"; d = tempo * 1.5
			"e": k = "e"; d = tempo * 1.3
			"i", "y": k = "i"; d = tempo * 1.3
			"o", "ö": k = "o"; d = tempo * 1.5
			"u", "ü": k = "u"; d = tempo * 1.4
			"m", "b", "p": k = "m"
			"f", "v", "w": k = "f"
			"s", "z", "c", "x", "ß": k = "s"
			" ": k = "-"; d = tempo * 0.6
			",", ";": k = "-"; d = tempo * 3.0
			".", "!", "?": k = "-"; d = tempo * 5.0
			"h": k = "-"; d = tempo * 0.5
		_sprech.append([d, VISEM[k]])
	_sprech_t = 0.0
	_sprech_i = 0
	_brauen_puls = 1.0

func spricht() -> bool:
	return _sprech_i >= 0

func _ziel() -> Dictionary:
	var z := {}
	for k in ausdruck:
		match String(k):
			"laecheln":
				z["laecheln_l"] = ausdruck[k]; z["laecheln_r"] = ausdruck[k]
			_: z[k] = ausdruck[k]
	for k in _halte:
		z[k] = _halte[k]
	if _sprech_i >= 0:
		var v: Dictionary = _sprech[_sprech_i][1]
		for k in ["kiefer", "lippen_auf", "mund_rund", "mund_breit", "mund_press", "lippe_f"]:
			z[k] = v.get(k, 0.0)
		# Lächeln beim Sprechen etwas zurücknehmen, sonst zieht es die Lippen auseinander
		z["laecheln_l"] = z.get("laecheln_l", 0.0) * 0.6
		z["laecheln_r"] = z.get("laecheln_r", 0.0) * 0.6
	return z

func _process(dt: float) -> void:
	if _skel == null:
		return
	_zeit += dt
	if _sprech_i >= 0:
		_sprech_t += dt
		while _sprech_i >= 0 and _sprech_t >= _sprech[_sprech_i][0]:
			_sprech_t -= _sprech[_sprech_i][0]
			_sprech_i += 1
			if _sprech_i >= _sprech.size():
				_sprech_i = -1
	var z := _ziel()
	# Blinzeln: schnell zu (60 ms), langsamer auf (100 ms)
	var b := 0.0
	if animiert:
		_blink_t -= dt
		if _blink_t <= 0.0 and _blink_phase < 0.0:
			_blink_phase = 0.0
			_blink_t = randf_range(2.0, 5.0)
	if _blink_phase >= 0.0:
		_blink_phase += dt
		var p := _blink_phase
		b = clampf(p / 0.06, 0.0, 1.0) if p < 0.06 else clampf(1.0 - (p - 0.09) / 0.1, 0.0, 1.0)
		if p > 0.19:
			_blink_phase = -1.0
	if _blink_fix >= 0.0:
		b = _blink_fix
	z["augen_zu"] = b
	_brauen_puls = maxf(0.0, _brauen_puls - dt * 1.2)
	z["brauen_hoch"] = maxf(float(z.get("brauen_hoch", 0.0)), 0.6 * sin(clampf(_brauen_puls, 0.0, 1.0) * PI))
	var k := clampf(dt * 18.0, 0.0, 1.0)
	for f in FORMEN + ["kiefer"]:
		var ziel: float = z.get(f, 0.0)
		var ist: float = _ist.get(f, 0.0)
		ist = ziel if f == "augen_zu" else lerpf(ist, ziel, k)
		_ist[f] = ist
		if f != "kiefer" and _netz:
			var bi := _netz.find_blend_shape_by_name(f)
			if bi >= 0:
				_netz.set_blend_shape_value(bi, ist)
	_mat.set_shader_parameter("blink", b)
	var kief: float = _ist.get("kiefer", 0.0)
	_drehe(_i_hals, Basis.IDENTITY)
	_drehe(_i_kopf, Basis.IDENTITY)
	_drehe(_i_kief, Basis(Vector3.RIGHT, kief * kiefer_max))
	if not animiert:
		_mat.set_shader_parameter("blick", Vector2.ZERO)
		return
	_blick_t -= dt
	if _blick_t <= 0.0 and blick_wechsel:
		_blick_t = randf_range(1.5, 3.5)
		_blick_ziel = Vector2(randf_range(-1, 1), randf_range(-0.6, 0.6))
	_blick = _blick.lerp(_blick_ziel, clampf(dt * 14.0, 0.0, 1.0))      # Blicksprünge sind schnell
	_mat.set_shader_parameter("blick", Vector2(_blick.x * 2.5, _blick.y * 1.2) / 1024.0)
	var atem := sin(_zeit * 1.35)
	var nicken := kief * 0.05
	_drehe(_i_hals, Basis.from_euler(Vector3(deg_to_rad(0.5 * atem), deg_to_rad(_blick.x * 1.0), 0.0)))
	_drehe(_i_kopf, Basis.from_euler(Vector3(deg_to_rad(-0.6 + 0.4 * atem - _blick.y * 1.2) + nicken, deg_to_rad(_blick.x * 2.0 + sin(_zeit * 0.4) * 1.5), deg_to_rad(sin(_zeit * 0.31) * 0.8))))
	_drehe(_i_kief, Basis(Vector3.RIGHT, kief * kiefer_max))
