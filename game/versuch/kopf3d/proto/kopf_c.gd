# Plan C: TRELLIS-Kopf mit Skelett (Hals, Kopf, Kiefer). Gleiche Schnittstelle wie KopfAnsicht, soweit sie die Tafeln brauchen.
# Blinzeln: Textur-Überblendung (blink); Sprechen: Kiefer aus Text-Visemen oder Audio-Lautstärke; Atmen/Blick: Hals- und Kopfknochen.
class_name KopfC
extends Node3D

const SHADER := preload("res://versuch/kopf3d/proto/kopf_c.gdshader")

var quelle := ""
var ordner := ""
var gemalt := true
var animiert := true
var blick_wechsel := true
var kiefer := 0.0      # Platzhalter (Regler gibt es in Plan C nicht)
var nase := 0.0
var alter := 0.0
var frisur := 0
var bart := true
var haarfarbe := Color.WHITE
var hautton := Color.WHITE
var kiefer_max := 0.22   # größte Öffnung in Bogenmaß (rund 12 Grad)

var _skel: Skeleton3D
var _mat: ShaderMaterial
var _i_hals := -1
var _i_kopf := -1
var _i_kief := -1
var _base := {}
var _zeit := randf() * 10.0
var _blink_t := 2.0
var _blink_phase := -1.0
var _blink_fix := -1.0
var _blick_t := 3.0
var _blick := Vector2.ZERO
var _blick_ziel := Vector2.ZERO
var _kiefer_ist := 0.0
var _kiefer_fix := -1.0
var _sprech: Array = []
var _sprech_t := 0.0
var _sprech_i := -1
var _audio: AudioStreamPlayer
var _huell := PackedFloat32Array()
var _huell_dt := 0.02

func _ready() -> void:
	var szene: PackedScene = load(quelle)
	var wurzel := szene.instantiate()
	add_child(wurzel)
	_skel = _finde_skelett(wurzel)
	_i_hals = _skel.find_bone("hals"); _i_kopf = _skel.find_bone("kopf"); _i_kief = _skel.find_bone("kiefer")
	_mat = ShaderMaterial.new()
	_mat.shader = SHADER
	var basis := quelle.get_base_dir()
	var name := quelle.get_file().get_basename()
	_mat.set_shader_parameter("tex_offen", load(basis.path_join("%s_haut_offen.jpg" % name)))
	var zu := basis.path_join("%s_haut_zu.jpg" % name)
	_mat.set_shader_parameter("tex_zu", load(zu) if ResourceLoader.exists(zu) else load(basis.path_join("%s_haut_offen.jpg" % name)))
	for m in _alle_netze(wurzel):
		match String(m.name):
			"gesicht": m.material_override = _mat
	_audio = AudioStreamPlayer.new()
	add_child(_audio)
	for i in [_i_hals, _i_kopf, _i_kief]:
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

# Knochen um seinen Ursprung (Gelenk) mit einer Weltdrehung R drehen, ausgehend von der importierten Grundhaltung.
func _drehe(i: int, r: Basis) -> void:
	if i < 0:
		return
	var par := _skel.get_bone_parent(i)
	var bp := Basis.IDENTITY if par < 0 else _skel.get_bone_global_pose(par).basis
	var rl := _skel.get_bone_rest(i).basis
	var lokal := bp.inverse() * r * bp * rl * Basis(_base[i])
	_skel.set_bone_pose_rotation(i, (rl.inverse() * lokal).get_rotation_quaternion())

func _form(name: String, wert: float) -> void:
	# Kompatibilität zur Tafel: Blinzel-Formen
	if name == "eyeBlinkLeft" or name == "eyeBlinkRight":
		_blink_fix = wert
		_setze_blink(wert)

func _setze_blink(v: float) -> void:
	if _mat:
		_mat.set_shader_parameter("blink", v)

func mund_haltung(ziel: Dictionary) -> void:
	_kiefer_fix = float(ziel.get("jawOpen", 0.0)) if not ziel.is_empty() else -1.0

const OEFFNUNG := {"a": 0.85, "e": 0.45, "i": 0.25, "o": 0.55, "u": 0.25, "m": 0.0, "f": 0.12, "t": 0.22, "-": 0.0}

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
		_sprech.append([tempo * (2.4 if ch in [",", "."] else 1.0), OEFFNUNG[k]])
	_sprech_t = 0.0
	_sprech_i = 0

func sprechen_audio(strom: AudioStreamWAV) -> void:
	var daten := strom.data
	var schritt := int(strom.mix_rate * _huell_dt)
	_huell = PackedFloat32Array()
	var i := 0
	var spitze := 0.0001
	while i + schritt * 2 <= daten.size():
		var s := 0.0
		for j in range(0, schritt):
			var v := float(daten.decode_s16(i + j * 2))
			s += v * v
		var r := sqrt(s / schritt) / 32768.0
		_huell.append(r); spitze = maxf(spitze, r)
		i += schritt * 2
	for k in _huell.size():
		_huell[k] /= spitze
	_audio.stream = strom
	_audio.play()

func _kiefer_ziel(dt: float) -> float:
	if _sprech_i >= 0:
		_sprech_t += dt
		if _sprech_t >= _sprech[_sprech_i][0]:
			_sprech_t -= _sprech[_sprech_i][0]
			_sprech_i += 1
			if _sprech_i >= _sprech.size():
				_sprech_i = -1
		if _sprech_i >= 0:
			return _sprech[_sprech_i][1]
	elif _audio.playing and not _huell.is_empty():
		var p := _audio.get_playback_position() + AudioServer.get_time_since_last_mix()
		return _huell[clampi(int(p / _huell_dt), 0, _huell.size() - 1)]
	if _kiefer_fix >= 0.0:
		return _kiefer_fix
	return 0.0

func _process(dt: float) -> void:
	if _skel == null:
		return
	_zeit += dt
	var ziel := _kiefer_ziel(dt)
	_kiefer_ist = lerpf(_kiefer_ist, ziel, clampf(dt * 20.0, 0.0, 1.0))
	_drehe(_i_hals, Basis.IDENTITY)
	_drehe(_i_kopf, Basis.IDENTITY)
	_drehe(_i_kief, Basis(Vector3.RIGHT, _kiefer_ist * kiefer_max))
	if not animiert:
		return
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
	_setze_blink(b)
	_blick_t -= dt
	if _blick_t <= 0.0 and blick_wechsel:
		_blick_t = randf_range(1.8, 4.0)
		_blick_ziel = Vector2(randf_range(-1, 1), randf_range(-0.5, 0.5))
	_blick = _blick.lerp(_blick_ziel, clampf(dt * 4.0, 0.0, 1.0))
	var atem := sin(_zeit * 1.35)
	var nicken := _kiefer_ist * 0.05      # beim Sprechen leicht mitnicken
	_drehe(_i_hals, Basis.from_euler(Vector3(deg_to_rad(0.5 * atem), deg_to_rad(_blick.x * 1.5), 0.0)))
	_drehe(_i_kopf, Basis.from_euler(Vector3(deg_to_rad(-0.6 + 0.4 * atem - _blick.y * 2.0) + nicken, deg_to_rad(_blick.x * 4.0 + sin(_zeit * 0.4) * 1.5), deg_to_rad(sin(_zeit * 0.31) * 0.8))))
	_drehe(_i_kief, Basis(Vector3.RIGHT, _kiefer_ist * kiefer_max))
