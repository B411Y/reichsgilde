# Live-3D-Kopf (Prototyp): lädt kopf_proto.glb, hängt Haut-/Haarshader an, steuert Regler und Animation.
# Regler: kiefer, nase (-1..1), alter (-1 jung .. +1 alt), hautton, haarfarbe, frisur (0 keine, 1 kurz, 2 lang), bart.
# Animation: zufälliges Blinzeln, Atmen, Blickwechsel, Sprechen (Text mit Visemen oder Audio-Lautstärke).
class_name KopfAnsicht
extends Node3D

const HAUT := preload("res://versuch/kopf3d/proto/haut.gdshader")
const HAAR := preload("res://versuch/kopf3d/proto/haar.gdshader")
const KOPFMITTE := Vector3(0.0, 1.62, 0.0)

var kiefer := 0.0: set = _set_kiefer
var nase := 0.0: set = _set_nase
var alter := 0.0: set = _set_alter
var hautton := Color(1, 1, 1): set = _set_hautton
var haarfarbe := Color(0.16, 0.09, 0.045): set = _set_haarfarbe
var frisur := 1: set = _set_frisur
var bart := true: set = _set_bart
var quelle := "res://versuch/kopf3d/proto/kopf_proto.glb"
var gemalt := false          # Plan B: Haut und Haar sind aus der Form gebackene Texturen, keine Shader nötig
var animiert := true
var blick_wechsel := true

var _wurzel: Node3D
var _pivot: Node3D
const HALS := Vector3(0.0, 1.45, 0.0)
var _netze: Array[MeshInstance3D] = []
var _haut: ShaderMaterial
var _haar_mat: ShaderMaterial
var _teile := {}
var _formen := {}            # Name -> [MeshInstance3D, Index]
var _mund := {}              # Zielwerte der Sprechformen
var _mund_ist := {}
var _blink_t := 2.0
var _blink_phase := -1.0
var _blick_t := 3.0
var _blick := Vector2.ZERO
var _blick_ziel := Vector2.ZERO
var _zeit := randf() * 10.0
var _sprech: Array = []       # [[dauer, {form: wert}], ...]
var _pose_fix := {}
var _sprech_t := 0.0
var _sprech_i := -1
var _audio: AudioStreamPlayer
var _huell: PackedFloat32Array = PackedFloat32Array()
var _huell_dt := 0.02

func _ready() -> void:
	var szene: PackedScene = load(quelle)
	_pivot = Node3D.new()
	_pivot.position = HALS
	add_child(_pivot)
	_wurzel = szene.instantiate()
	_wurzel.position = -HALS
	_pivot.add_child(_wurzel)
	_sammeln(_wurzel)
	if not gemalt:
		_haut = ShaderMaterial.new()
		_haut.shader = HAUT
		_haut.set_shader_parameter("tex_jung", load("res://versuch/kopf3d/proto/haut_jung.png"))
		_haut.set_shader_parameter("tex_alt", load("res://versuch/kopf3d/proto/haut_alt.png"))
		_haar_mat = ShaderMaterial.new()
		_haar_mat.shader = HAAR
		_haar_mat.set_shader_parameter("strahnen", load("res://versuch/kopf3d/proto/haarkarte.png"))
	for m in _netze:
		var n := String(m.name)
		_teile[n] = m
		if gemalt and (n == "Human" or n == "haar_schale"):
			var gm := (m.get_active_material(0) as StandardMaterial3D).duplicate() as StandardMaterial3D
			gm.roughness = 0.72 if n == "Human" else 0.85
			gm.metallic_specular = 0.25
			gm.rim_enabled = true
			gm.rim = 0.25
			gm.rim_tint = 0.5
			m.material_override = gm
		elif n == "Human":
			m.material_override = _haut
		elif n.begins_with("haar_") or n == "bart":
			m.material_override = _haar_mat
			m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		elif n.contains("low-poly"):
			var au := (m.get_active_material(0) as StandardMaterial3D).duplicate() as StandardMaterial3D
			au.roughness = 0.05
			au.metallic_specular = 1.0
			au.clearcoat_enabled = true
			au.clearcoat = 1.0
			au.clearcoat_roughness = 0.02
			m.material_override = au
		elif n.contains("eyelash"):
			var lm := (m.get_active_material(0) as StandardMaterial3D).duplicate() as StandardMaterial3D
			lm.albedo_color = Color(0.12, 0.09, 0.07)
			m.material_override = lm
		elif n.contains("casualsuit"):
			var kl := StandardMaterial3D.new()
			kl.albedo_color = Color(0.30, 0.24, 0.17)
			kl.roughness = 0.95
			m.material_override = kl
		elif n.contains("eyebrow"):
			var mat := (m.get_active_material(0) as StandardMaterial3D).duplicate() as StandardMaterial3D
			m.material_override = mat
		for i in m.mesh.get_blend_shape_count():
			var f := String(m.mesh.get_blend_shape_name(i))
			if not _formen.has(f):
				_formen[f] = []
			_formen[f].append([m, i])
	_audio = AudioStreamPlayer.new()
	add_child(_audio)
	_alles_anwenden()

func _sammeln(n: Node) -> void:
	if n is MeshInstance3D:
		_netze.append(n)
	for k in n.get_children():
		_sammeln(k)

func _form(name: String, wert: float) -> void:
	if not _formen.has(name):
		return
	for e in _formen[name]:
		(e[0] as MeshInstance3D).set_blend_shape_value(e[1], wert)

func _alles_anwenden() -> void:
	if _wurzel == null:
		return
	_set_kiefer(kiefer); _set_nase(nase); _set_alter(alter); _set_hautton(hautton)
	_set_haarfarbe(haarfarbe); _set_frisur(frisur); _set_bart(bart)

func _set_kiefer(v: float) -> void:
	kiefer = v
	_form("kiefer_breit", maxf(v, 0.0)); _form("kiefer_schmal", maxf(-v, 0.0))

func _set_nase(v: float) -> void:
	nase = v
	_form("nase_gross", maxf(v, 0.0)); _form("nase_klein", maxf(-v, 0.0))

func _set_alter(v: float) -> void:
	alter = v
	_form("alt", maxf(v, 0.0)); _form("jung", maxf(-v, 0.0))
	if _haut:
		_haut.set_shader_parameter("alter", v)

func _set_hautton(c: Color) -> void:
	hautton = c
	if _haut:
		_haut.set_shader_parameter("hautton", c)

func _set_haarfarbe(c: Color) -> void:
	haarfarbe = c
	if _haar_mat:
		_haar_mat.set_shader_parameter("haarfarbe", c)
	var br = _teile.get("Human.eyebrow009")
	if br and br.material_override:
		(br.material_override as StandardMaterial3D).albedo_color = c.lerp(Color(1, 1, 1), 0.0) * 1.6

func _set_frisur(v: int) -> void:
	frisur = v
	if _teile.has("haar_kurz"):
		(_teile["haar_kurz"] as Node3D).visible = v == 1
		(_teile["haar_lang"] as Node3D).visible = v == 2

func _set_bart(v: bool) -> void:
	bart = v
	if _teile.has("bart"):
		(_teile["bart"] as Node3D).visible = v

# ---- Sprechen -------------------------------------------------------------------------------

# Vokal/Konsonant -> Mundformen (ARKit-Einheiten); feste Taktung je Zeichen
const VISEME := {
	"a": {"jawOpen": 0.62, "mouthLowerDownLeft": 0.3, "mouthLowerDownRight": 0.3},
	"e": {"jawOpen": 0.3, "mouthStretchLeft": 0.5, "mouthStretchRight": 0.5},
	"i": {"jawOpen": 0.14, "mouthSmileLeft": 0.5, "mouthSmileRight": 0.5, "mouthStretchLeft": 0.25, "mouthStretchRight": 0.25},
	"o": {"jawOpen": 0.35, "mouthFunnel": 0.6},
	"u": {"jawOpen": 0.14, "mouthPucker": 0.75},
	"m": {"mouthClose": 0.8},
	"f": {"jawOpen": 0.08, "mouthUpperUpLeft": 0.3, "mouthUpperUpRight": 0.3},
	"t": {"jawOpen": 0.16},
	"-": {},
}

func sprechen_text(text: String, tempo := 0.075) -> void:
	_sprech.clear()
	for ch in text.to_lower():
		var k := ""
		match ch:
			"a", "ä": k = "a"
			"e": k = "e"
			"i", "y": k = "i"
			"o", "ö": k = "o"
			"u", "ü": k = "u"
			"m", "b", "p": k = "m"
			"f", "v", "w": k = "f"
			" ", ",", ".", "!", "?": k = "-"
			_: k = "t"
		var d := tempo * (2.4 if ch in [",", "."] else 1.0)
		_sprech.append([d, VISEME[k]])
	_sprech_t = 0.0
	_sprech_i = 0

func sprechen_audio(strom: AudioStreamWAV) -> void:
	# Lautstärke-Hüllkurve aus den Samples (16 Bit, mono) vorab berechnen
	var daten := strom.data
	var rate := strom.mix_rate
	var schritt := int(rate * _huell_dt)
	_huell = PackedFloat32Array()
	var i := 0
	var spitze := 0.0001
	while i + schritt * 2 <= daten.size():
		var s := 0.0
		for j in range(0, schritt):
			var v := float(daten.decode_s16((i + j * 2)))
			s += v * v
		var r := sqrt(s / schritt) / 32768.0
		_huell.append(r)
		spitze = maxf(spitze, r)
		i += schritt * 2
	for k in _huell.size():
		_huell[k] /= spitze
	_audio.stream = strom
	_audio.play()

# Mund fest in eine Haltung bringen (für Vergleichsbilder); leeres Wörterbuch löst die Haltung
func mund_haltung(ziel: Dictionary) -> void:
	_pose_fix = ziel

func spricht() -> bool:
	return _sprech_i >= 0 or _audio.playing

func _sprech_schritt(dt: float) -> void:
	var ziel := {}
	if _sprech_i >= 0:
		_sprech_t += dt
		var e = _sprech[_sprech_i]
		if _sprech_t >= e[0]:
			_sprech_t -= e[0]
			_sprech_i += 1
			if _sprech_i >= _sprech.size():
				_sprech_i = -1
		if _sprech_i >= 0:
			ziel = _sprech[_sprech_i][1]
	elif _audio.playing and not _huell.is_empty():
		var p := _audio.get_playback_position() + AudioServer.get_time_since_last_mix()
		var idx := clampi(int(p / _huell_dt), 0, _huell.size() - 1)
		var l := _huell[idx]
		var wechsel := 0.5 + 0.5 * sin(p * 11.0)        # Vokalwechsel grob nachgeahmt
		ziel = {"jawOpen": l * 0.65, "mouthFunnel": l * 0.5 * (1.0 - wechsel), "mouthStretchLeft": l * 0.4 * wechsel,
			"mouthStretchRight": l * 0.4 * wechsel}
	if ziel.is_empty():
		ziel = _pose_fix
	for f in ["jawOpen", "mouthFunnel", "mouthPucker", "mouthSmileLeft", "mouthSmileRight", "mouthClose", "mouthStretchLeft",
			"mouthStretchRight", "mouthLowerDownLeft", "mouthLowerDownRight", "mouthUpperUpLeft", "mouthUpperUpRight"]:
		var z: float = ziel.get(f, 0.0)
		var ist: float = _mund_ist.get(f, 0.0)
		ist = lerpf(ist, z, clampf(dt * 22.0, 0.0, 1.0))
		_mund_ist[f] = ist
		_form(f, ist)

# ---- Leben ----------------------------------------------------------------------------------

func _process(dt: float) -> void:
	if _wurzel == null:
		return
	_zeit += dt
	_sprech_schritt(dt)
	if not animiert:
		return
	# Blinzeln: alle 2-5 s, 140 ms
	_blink_t -= dt
	if _blink_t <= 0.0 and _blink_phase < 0.0:
		_blink_phase = 0.0
		_blink_t = randf_range(2.0, 5.0)
	var b := 0.0
	if _blink_phase >= 0.0:
		_blink_phase += dt
		b = clampf(1.0 - absf(_blink_phase - 0.07) / 0.07, 0.0, 1.0)
		if _blink_phase > 0.14:
			_blink_phase = -1.0
	_form("eyeBlinkLeft", b); _form("eyeBlinkRight", b)
	# Blick wechselt alle 2-4 s, Augen und Kopf folgen sanft
	_blick_t -= dt
	if _blick_t <= 0.0 and blick_wechsel:
		_blick_t = randf_range(1.8, 4.0)
		_blick_ziel = Vector2(randf_range(-1, 1), randf_range(-0.5, 0.5))
	_blick = _blick.lerp(_blick_ziel, clampf(dt * 5.0, 0.0, 1.0))
	_form("eyeLookOutLeft", maxf(-_blick.x, 0.0)); _form("eyeLookInRight", maxf(-_blick.x, 0.0))
	_form("eyeLookInLeft", maxf(_blick.x, 0.0)); _form("eyeLookOutRight", maxf(_blick.x, 0.0))
	_form("eyeLookUpLeft", maxf(_blick.y, 0.0)); _form("eyeLookUpRight", maxf(_blick.y, 0.0))
	_form("eyeLookDownLeft", maxf(-_blick.y, 0.0)); _form("eyeLookDownRight", maxf(-_blick.y, 0.0))
	# Atmen und Kopfbewegung: Büste kippt/dreht leicht um die Halsmitte
	var atem := sin(_zeit * 1.35)
	_pivot.position = HALS + Vector3(0, atem * 0.0025, 0)
	_pivot.rotation = Vector3(deg_to_rad(-0.8 + 0.6 * atem - _blick.y * 1.5), deg_to_rad(_blick.x * 4.0 + sin(_zeit * 0.4) * 1.5), deg_to_rad(sin(_zeit * 0.31) * 0.8))
