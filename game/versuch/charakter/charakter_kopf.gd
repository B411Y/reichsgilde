# Versuch Charakter Phase 0: Kopf (Büste) aus einer .glb zur Laufzeit laden.
# Die .glb liegen außerhalb des Repositorys (~/werkzeuge/charakter/glb, Fremddaten von MPFB und ICT);
# fehlt die Datei, steht der Grund in `fehler` und im Fenster.
# Stoffe: „roh“ = wie aus der .glb (Ausgangslage A1), „haut“ = eigener Hautshader mit Rötung, Glanz, Poren, Rand.
extends Node3D

const HAUT := preload("res://versuch/charakter/haut.gdshader")
const AUGEN := preload("res://versuch/charakter/augen.gdshader")

var pfad := ""
var stoff := "roh"
var komprimieren := true
var netze: Array[MeshInstance3D] = []
var formen := {}            # Formname -> Array von [MeshInstance3D, Index]
var ladezeit_ms := 0.0
var komprimier_ms := 0.0
var fehler := ""
var augen_mitte := Vector3(0, 1.6, 0)
var mund_mitte := Vector3(0, 1.55, 0)
var oben := 1.75
var unten := 1.3
var haut_stoffe: Array[ShaderMaterial] = []
var _texturen := {}
static var _poren: NoiseTexture2D

func laden(datei: String, art := "roh") -> bool:
	pfad = datei
	stoff = art
	if not FileAccess.file_exists(datei):
		fehler = "Kopf fehlt: %s\nDie Köpfe entstehen mit tools/charakter/alle_koepfe.sh (Fremddaten, nicht im Repository)." % datei
		push_error(fehler)
		return false
	var t := Time.get_ticks_usec()
	var doc := GLTFDocument.new()
	var st := GLTFState.new()
	st.handle_binary_image_mode = GLTFState.HANDLE_BINARY_IMAGE_MODE_EMBED_AS_UNCOMPRESSED
	var err := doc.append_from_file(datei, st)
	if err != OK:
		fehler = "glTF-Fehler %d beim Laden von %s" % [err, datei]
		push_error(fehler)
		return false
	var szene := doc.generate_scene(st)
	ladezeit_ms = (Time.get_ticks_usec() - t) / 1000.0
	add_child(szene)
	_sammeln(szene)
	var tk := Time.get_ticks_usec()
	_stoffe_setzen()
	komprimier_ms = (Time.get_ticks_usec() - tk) / 1000.0
	_masse()
	return true

func _sammeln(n: Node) -> void:
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		netze.append(mi)
		for i in mi.mesh.get_blend_shape_count():
			var name := String(mi.mesh.get_blend_shape_name(i))
			if not formen.has(name):
				formen[name] = []
			formen[name].append([mi, i])
	for k in n.get_children():
		_sammeln(k)

func form(name: String, wert: float) -> void:
	for e in formen.get(name, []):
		(e[0] as MeshInstance3D).set_blend_shape_value(e[1], wert)

# Teile zum Prüfen unsichtbar machen (z. B. ["haar", "traene"]); leere Liste zeigt alles wieder
var _versteckt := {}
func teile_ausblenden(arten: Array) -> void:
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.mesh.surface_get_material(s)
			var art := teil_art(String(alt.resource_name) if alt else "")
			var schl := "%d_%d" % [mi.get_instance_id(), s]
			if art in arten and not _versteckt.has(schl):
				_versteckt[schl] = mi.get_surface_override_material(s)
				var leer := StandardMaterial3D.new()
				leer.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
				leer.albedo_color = Color(0, 0, 0, 0)
				mi.set_surface_override_material(s, leer)
			elif not art in arten and _versteckt.has(schl):
				mi.set_surface_override_material(s, _versteckt[schl])
				_versteckt.erase(schl)

func formen_zuruecksetzen() -> void:
	for name in formen:
		form(name, 0.0)

func formen_setzen(werte: Dictionary) -> void:
	formen_zuruecksetzen()
	for name in werte:
		if not formen.has(name):
			push_warning("Form fehlt: %s in %s" % [name, pfad.get_file()])
		form(name, float(werte[name]))

# Teilart nach dem Stoffnamen aus MPFB („Koerper.body“, „Kopfhaut“, „Koerper.high-poly“ …) bzw. ICT („M_Face“ …)
static func teil_art(stoffname: String) -> String:
	var s := stoffname.to_lower()
	if s.ends_with(".body") or s == "kopfhaut" or s == "m_face" or s == "m_backhead":
		return "haut"
	if s.contains("high-poly") or s.contains("low-poly"):
		return "auge"
	if s.contains("sclera"):
		return "lederhaut"
	if s.contains("iris"):
		return "iris"
	if s.contains("teeth"):
		return "zaehne"
	if s.contains("tongue") or s.contains("gums"):
		return "mund"
	if s.contains("eyebrow") or s.contains("eyelash"):
		return "haar"
	if s.contains("lacrimal"):
		return "traene"
	if s.contains("eyeblend") or s.contains("eyeocclusion"):
		return "aus"
	if s.contains("hauer"):
		return "hauer"
	return "sonst"

func _textur(t: Texture2D, normal := false) -> Texture2D:
	if t == null or not komprimieren:
		return t
	if _texturen.has(t):
		return _texturen[t]
	var bild := t.get_image()
	if bild == null:
		return t
	bild = bild.duplicate()
	if bild.is_compressed():
		return t
	bild.generate_mipmaps()
	# wie der Import im Spiel: Farbe BPTC (sRGB), Normalen RGTC
	bild.compress(Image.COMPRESS_S3TC if normal else Image.COMPRESS_BPTC,
		Image.COMPRESS_SOURCE_NORMAL if normal else Image.COMPRESS_SOURCE_SRGB)
	var neu := ImageTexture.create_from_image(bild)
	_texturen[t] = neu
	return neu

static func poren() -> NoiseTexture2D:
	# Poren und feine Fältchen als nahtlose Normalenkarte, im Spiel erzeugt (keine Datei, keine Fremddaten)
	if _poren == null:
		var rauschen := FastNoiseLite.new()
		rauschen.noise_type = FastNoiseLite.TYPE_CELLULAR
		rauschen.frequency = 0.09
		rauschen.cellular_return_type = FastNoiseLite.RETURN_DISTANCE
		rauschen.fractal_type = FastNoiseLite.FRACTAL_FBM
		rauschen.fractal_octaves = 2
		rauschen.fractal_gain = 0.35
		_poren = NoiseTexture2D.new()
		_poren.width = 512
		_poren.height = 512
		_poren.seamless = true
		_poren.as_normal_map = true
		_poren.bump_strength = 5.0
		_poren.noise = rauschen
	return _poren

func _stoffe_setzen() -> void:
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.get_active_material(s)
			var name := String(alt.resource_name) if alt else ""
			var art := teil_art(name)
			var neu: Material = null
			var std := alt as BaseMaterial3D
			if art == "aus":
				var leer := StandardMaterial3D.new()
				leer.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
				leer.albedo_color = Color(0, 0, 0, 0)
				neu = leer
			elif stoff == "roh":
				neu = _roh(std, art)
			else:
				neu = _eigen(std, art, mi, s)
			if neu:
				mi.set_surface_override_material(s, neu)

# A1: Stoffe wie geliefert. Nur die Durchsichtigkeit, die der MPFB-Export überall setzt, wird für feste Teile
# abgeschaltet (sonst sortiert Godot Haut und Zähne falsch); Brauen und Wimpern bekommen Alphatest.
func _roh(std: BaseMaterial3D, art: String) -> Material:
	if std == null:
		return null
	var m := std.duplicate() as BaseMaterial3D
	m.albedo_texture = _textur(m.albedo_texture)
	# Die Eckfarbe ist die Hautmaske (bzw. Augendaten), keine Farbe: Godots glTF-Import würde sie als Albedo nehmen
	m.vertex_color_use_as_albedo = false
	match art:
		"haar":
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
			m.alpha_scissor_threshold = 0.35
			m.alpha_antialiasing_mode = BaseMaterial3D.ALPHA_ANTIALIASING_ALPHA_TO_COVERAGE
			m.cull_mode = BaseMaterial3D.CULL_DISABLED
		"auge":
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_DEPTH_PRE_PASS
		_:
			m.transparency = BaseMaterial3D.TRANSPARENCY_DISABLED
	return m

func _eigen(std: BaseMaterial3D, art: String, mi: MeshInstance3D, s: int) -> Material:
	match art:
		"haut":
			var m := ShaderMaterial.new()
			m.shader = HAUT
			var tex: Texture2D = std.albedo_texture if std else null
			m.set_shader_parameter("hat_farbe", tex != null)
			if tex:
				m.set_shader_parameter("farbe", _textur(tex))
			m.set_shader_parameter("poren", poren())
			# Poren gleich groß auf jeder UV: Meter je UV-Einheit aus Flächen der Oberfläche
			m.set_shader_parameter("poren_dichte", meter_je_uv(mi.mesh, s) / 0.045)
			haut_stoffe.append(m)
			return m
		"auge", "lederhaut", "iris":
			var m := ShaderMaterial.new()
			m.shader = AUGEN
			var tex: Texture2D = std.albedo_texture if std else null
			m.set_shader_parameter("hat_farbe", tex != null)
			if tex:
				m.set_shader_parameter("farbe", _textur(tex))
			m.set_shader_parameter("iris_teil", art == "iris")
			m.set_shader_parameter("lederhaut_teil", art == "lederhaut")
			return m
		"zaehne":
			var m := _roh(std, art) as BaseMaterial3D
			if m == null:
				m = StandardMaterial3D.new()
			m.albedo_color = Color(0.82, 0.78, 0.7) if m.albedo_texture == null else Color(0.9, 0.88, 0.84)
			m.roughness = 0.32
			m.metallic_specular = 0.45
			return m
		"mund":
			var m := _roh(std, art) as BaseMaterial3D
			if m == null:
				m = StandardMaterial3D.new()
			if m.albedo_texture == null:
				m.albedo_color = Color(0.55, 0.22, 0.2)
			m.roughness = 0.35
			return m
		"haar":
			var m := _roh(std, art) as BaseMaterial3D
			if m == null:
				m = StandardMaterial3D.new()
			if m.albedo_texture == null:
				# ICT-Wimpern sind Streifen für eine Haartextur, die fehlt: ohne Textur wirken sie wie Schatten
				# unter den Augen, darum vorerst unsichtbar (Phase 2: Wimpernkarte mit Alpha)
				m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
				m.albedo_color = Color(0.05, 0.035, 0.03, 0.0)
			m.roughness = 0.6
			return m
		"traene":
			var m := StandardMaterial3D.new()
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			m.albedo_color = Color(0.9, 0.85, 0.85, 0.12)
			m.roughness = 0.05
			m.metallic_specular = 0.8
			return m
		"hauer":
			var m := StandardMaterial3D.new()
			m.albedo_color = Color(0.8, 0.74, 0.6)
			m.roughness = 0.42
			return m
	return _roh(std, art)

static func meter_je_uv(mesh: Mesh, s: int) -> float:
	var a := mesh.surface_get_arrays(s)
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var uv: PackedVector2Array = a[Mesh.ARRAY_TEX_UV]
	var ind: PackedInt32Array = a[Mesh.ARRAY_INDEX]
	if uv.is_empty() or ind.is_empty():
		return 0.25
	var welt := 0.0
	var flach := 0.0
	for i in range(0, ind.size(), 3):
		var p0 := v[ind[i]]
		var u0 := uv[ind[i]]
		welt += (v[ind[i + 1]] - p0).cross(v[ind[i + 2]] - p0).length()
		var d1 := uv[ind[i + 1]] - u0
		var d2 := uv[ind[i + 2]] - u0
		flach += absf(d1.x * d2.y - d1.y * d2.x)
	return sqrt(welt / maxf(flach, 1e-9))

# Lage von Augen, Mund, Scheitel und Unterkante für die Kamera
func _masse() -> void:
	var augen := AABB()
	var zaehne := AABB()
	var alles := AABB()
	var hat_a := false
	var hat_z := false
	var hat := false
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.mesh.surface_get_material(s)
			var art := teil_art(String(alt.resource_name) if alt else "")
			var b := _surface_aabb(mi, s)
			if art in ["auge", "lederhaut", "iris"]:
				augen = b if not hat_a else augen.merge(b)
				hat_a = true
			elif art == "zaehne":
				zaehne = b if not hat_z else zaehne.merge(b)
				hat_z = true
			alles = b if not hat else alles.merge(b)
			hat = true
	if hat_a:
		augen_mitte = augen.get_center()
	if hat_z:
		mund_mitte = Vector3(zaehne.get_center().x, zaehne.get_center().y, zaehne.end.z)
	oben = alles.end.y
	unten = alles.position.y

static func _surface_aabb(mi: MeshInstance3D, s: int) -> AABB:
	var v: PackedVector3Array = mi.mesh.surface_get_arrays(s)[Mesh.ARRAY_VERTEX]
	var b := AABB(v[0], Vector3.ZERO)
	for p in v:
		b = b.expand(p)
	return mi.global_transform * b if mi.is_inside_tree() else b

func dreiecke() -> int:
	var n := 0
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			n += mi.mesh.surface_get_array_index_len(s) / 3
	return n
