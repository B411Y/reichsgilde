# Versuch Charakter Phase 2: Basiskopf (Büste) aus einer .glb zur Laufzeit laden und über Gene, Alter, Geschlecht
# und Mimik steuern. Die .glb und ihre Beschreibung (<name>.json) entstehen mit tools/charakter/kopf2.sh aus ICT- und
# MakeHuman-Daten und liegen außerhalb des Repositorys (~/werkzeuge/charakter/glb2); fehlt die Datei, steht der
# Grund in `fehler`.
#
# Formen der .glb: makro_<g>_<alter> (Alter × Geschlecht), makro_<g>_<alter>_<kraft|gewicht>_<minus|plus>,
# gen_<kennung>_<minus|plus> (Gestalt-Gene) und die ARKit-Mimik. Die Gewichte rechnet _anwenden() wie
# MakeHuman: Alter und Geschlecht bilinear zwischen den Stützen, Kraft und Gewicht je Alter dazu.
# Mimik und Gestalt-Gene werden mit der Gesichtsgröße skaliert (Kind kleiner als Erwachsener, „mimikmass“).
extends Node3D

const HAUT := preload("res://versuch/charakter/haut.gdshader")
const AUGEN := preload("res://versuch/charakter/augen.gdshader")
const MUND := preload("res://versuch/charakter/mund.gdshader")
const ZAEHNE := preload("res://versuch/charakter/zaehne.gdshader")
const HAAR := preload("res://versuch/charakter/haar.gdshader")
const ERBGUT := "res://daten/erbgut.json"

var pfad := ""
var netze: Array[MeshInstance3D] = []
var formen := {}            # Formname -> Array von [MeshInstance3D, Index]
var info := {}              # Beschreibung der .glb (<name>.json)
var ladezeit_ms := 0.0
var komprimier_ms := 0.0
var fehler := ""
var augen_mitte := Vector3(0, 0.82, 0.12)
var mund_mitte := Vector3(0, 0.75, 0.14)
var oben := 0.93
var haut_stoffe: Array[ShaderMaterial] = []
var haar_stoffe: Array[ShaderMaterial] = []
var augen_stoffe: Array[ShaderMaterial] = []

# Zustand
var gene := {}              # Kennung -> sichtbarer Wert 0..255
var alter := 35.0
var geschlecht := 1.0       # 1 = Mann, 0 = Frau
var mimik := {}             # ARKit-Name -> Gewicht
var mimik_mit_alter := true
var mimikfaktor := 1.0
var gewichte := {}          # zuletzt gesetzte Formgewichte (zum Prüfen)
static var _poren: NoiseTexture2D
static var _erbgut := {}

func laden(datei: String) -> bool:
	pfad = datei
	if not FileAccess.file_exists(datei):
		fehler = "Kopf fehlt: %s\nDie Köpfe entstehen mit tools/charakter/kopf2.sh (Fremddaten, nicht im Repository)." % datei
		push_error(fehler)
		return false
	var beschr := datei.get_basename() + ".json"
	if FileAccess.file_exists(beschr):
		info = JSON.parse_string(FileAccess.get_file_as_string(beschr))
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
	gene = mittelwerte("Mensch")
	_anwenden()
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

# ---------------------------------------------------------------- Erbgut
static func erbgut() -> Dictionary:
	if _erbgut.is_empty():
		var d: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ERBGUT))
		for g in d["gene"]:
			_erbgut[g["kennung"]] = g
	return _erbgut

static func mittelwerte(volk: String) -> Dictionary:
	var aus := {}
	for k in erbgut():
		aus[k] = float(erbgut()[k]["voelker"][volk]["mittel"])
	return aus

# Sichtbarer Wert nach dem Alter verschoben (erbgut.json „alter“: Stützstellen in Menschenjahren)
static func wert_im_alter(kennung: String, wert: float, jahre: float) -> float:
	var g: Dictionary = erbgut().get(kennung, {})
	var stuetzen: Array = g.get("alter", [])
	if stuetzen.is_empty():
		return wert
	var v := float(stuetzen[0]["verschiebung"])
	for i in stuetzen.size():
		var s: Dictionary = stuetzen[i]
		if jahre >= float(s["jahre"]):
			v = float(s["verschiebung"])
			if i + 1 < stuetzen.size():
				var n: Dictionary = stuetzen[i + 1]
				var t := clampf((jahre - float(s["jahre"])) / (float(n["jahre"]) - float(s["jahre"])), 0.0, 1.0)
				v = lerpf(float(s["verschiebung"]), float(n["verschiebung"]), t)
	return clampf(wert + v, 0.0, 255.0)

# MakeHuman-Altersregler (0..1) aus Lebensjahren: 1 J. = 0, 11 J. = 0,1875, 25 J. = 0,5, 90 J. = 1
static func alter_zu_makro(jahre: float) -> float:
	if jahre <= 11.0:
		return maxf(0.0, (jahre - 1.0) / 10.0 * 0.1875)
	if jahre <= 25.0:
		return 0.1875 + (jahre - 11.0) / 14.0 * 0.3125
	return minf(1.0, 0.5 + (jahre - 25.0) / 65.0 * 0.5)

# Gewichte der Stützen [[name, wert], …] für m (stückweise linear, außen festgehalten)
static func stuetzen_gewichte(m: float, stuetzen: Array) -> Dictionary:
	if m <= float(stuetzen[0][1]):
		return {stuetzen[0][0]: 1.0}
	for i in stuetzen.size() - 1:
		var a := float(stuetzen[i][1])
		var b := float(stuetzen[i + 1][1])
		if m <= b:
			var t := (m - a) / (b - a)
			return {stuetzen[i][0]: 1.0 - t, stuetzen[i + 1][0]: t}
	return {stuetzen[-1][0]: 1.0}

func aussehen_setzen(neue_gene: Dictionary, jahre: float, mann: float) -> void:
	for k in neue_gene:
		gene[k] = float(neue_gene[k])
	alter = jahre
	geschlecht = mann
	_anwenden()

func mimik_setzen(werte: Dictionary) -> void:
	mimik = werte.duplicate()
	_anwenden()

func _anwenden() -> void:
	if formen.is_empty():
		return
	var w := {}
	var m := alter_zu_makro(alter)
	var stuetzen := []
	for s in info.get("alter_stuetzen", [{"name": "saeugling", "makro": 0.0}, {"name": "kind", "makro": 0.1875},
			{"name": "jung", "makro": 0.5}, {"name": "alt", "makro": 1.0}]):
		stuetzen.append([s["name"], float(s["makro"])])
	var w_alter := stuetzen_gewichte(m, stuetzen)
	var w_alter_mk := stuetzen_gewichte(maxf(m, 0.1875), stuetzen.slice(1))
	var w_g := {"w": 1.0 - geschlecht, "m": geschlecht}
	var mass: Dictionary = info.get("mimikmass", {})
	mimikfaktor = 0.0
	var kraft := float(gene.get("kraft", 128.0)) / 255.0
	var gewicht := float(gene.get("gewicht", 128.0)) / 255.0
	for g in w_g:
		for a in w_alter:
			var name := "makro_%s_%s" % [g, a]
			w[name] = float(w.get(name, 0.0)) + w_g[g] * w_alter[a]
			mimikfaktor += w_g[g] * w_alter[a] * float(mass.get(name, 1.0))
		for a in w_alter_mk:
			var f: float = w_g[g] * w_alter_mk[a]
			for art_wert in [["kraft", kraft], ["gewicht", gewicht]]:
				var v: float = art_wert[1]
				var richt := "minus" if v < 0.5 else "plus"
				var name := "makro_%s_%s_%s_%s" % [g, a, art_wert[0], richt]
				w[name] = float(w.get(name, 0.0)) + f * absf(v - 0.5) / 0.5
	if not mimik_mit_alter:
		mimikfaktor = 1.0
	# Gestalt-Gene: 128 = keine Form; nur „plus“ (spitze Ohren): Wert / 255
	var gene_info: Dictionary = info.get("gene", {})
	for k in gene_info:
		var fo: Dictionary = gene_info[k].get("formen", {})
		if fo.is_empty():
			continue
		var v := wert_im_alter(k, float(gene.get(k, 128.0)), alter)
		if fo.has("minus"):
			var x := (v - 128.0) / 127.0
			w[fo["minus"]] = maxf(-x, 0.0) * mimikfaktor
			w[fo["plus"]] = maxf(x, 0.0) * mimikfaktor
		else:
			w[fo["plus"]] = v / 255.0 * mimikfaktor
	for k in mimik:
		w[k] = float(w.get(k, 0.0)) + float(mimik[k]) * mimikfaktor
	gewichte = w
	for name in formen:
		form(name, float(w.get(name, 0.0)))
	_merkpunkte(w)
	_farben()

# Lage von Augen, Mund, Scheitel aus der Beschreibung (Grundlage + Versatz je Form) für die Kamera
func _merkpunkte(w: Dictionary) -> void:
	var mp: Dictionary = info.get("merkpunkte", {})
	if mp.is_empty():
		return
	var aus := {}
	for k in mp:
		aus[k] = Vector3(mp[k][0], mp[k][1], mp[k][2])
	var mf: Dictionary = info.get("merkpunkt_formen", {})
	for name in mf:
		var g := float(w.get(name, 0.0))
		if g == 0.0:
			continue
		for k in mf[name]:
			var d: Array = mf[name][k]
			aus[k] += Vector3(d[0], d[1], d[2]) * g
	augen_mitte = aus.get("augen", augen_mitte)
	mund_mitte = aus.get("mund", mund_mitte)
	oben = (aus.get("scheitel", Vector3(0, oben, 0)) as Vector3).y

# ---------------------------------------------------------------- Farben aus den Farb-Genen
func _farben() -> void:
	var h := float(gene.get("haut_helligkeit", 185.0)) / 255.0
	var ton := float(gene.get("haut_ton", 105.0)) / 255.0
	var grund := Color(0.27, 0.16, 0.10).lerp(Color(0.84, 0.64, 0.53), h)
	var toenung := Vector3.ONE
	if ton < 0.5:
		toenung = Vector3(1.07, 0.96, 0.95).lerp(Vector3.ONE, ton / 0.5)
	else:
		toenung = Vector3.ONE.lerp(Vector3(0.78, 0.95, 0.74), (ton - 0.5) / 0.5)
	for m in haut_stoffe:
		m.set_shader_parameter("grundfarbe", Vector3(grund.r, grund.g, grund.b))
		m.set_shader_parameter("toenung", toenung)
	var hh := float(gene.get("haar_helligkeit", 100.0)) / 255.0
	var rot := float(gene.get("haar_rot", 70.0)) / 255.0
	var haar := Color(0.035, 0.028, 0.022).lerp(Color(0.78, 0.66, 0.45), hh)
	haar = haar.lerp(Color(0.5, 0.2, 0.07).lerp(Color(0.85, 0.45, 0.2), hh), rot * 0.8)
	# Ergrauen: ab 20 + Gen/255·50 Jahren, über 25 Jahre fast ganz grau
	var grau_ab := 20.0 + float(gene.get("ergrauen", 128.0)) / 255.0 * 50.0
	haar = haar.lerp(Color(0.72, 0.71, 0.69), clampf((alter - grau_ab) / 25.0, 0.0, 0.85))
	for m in haar_stoffe:
		m.set_shader_parameter("farbe", Vector3(haar.r, haar.g, haar.b))
	var ah := float(gene.get("augen_helligkeit", 130.0)) / 255.0
	var at := float(gene.get("augen_ton", 135.0)) / 255.0
	var hell := Color(0.6, 0.42, 0.14).lerp(Color(0.36, 0.46, 0.25), clampf(at / 0.5, 0.0, 1.0))
	if at > 0.5:
		hell = Color(0.36, 0.46, 0.25).lerp(Color(0.38, 0.52, 0.72), (at - 0.5) / 0.5)
	var iris := Color(0.16, 0.08, 0.035).lerp(hell, ah)
	for m in augen_stoffe:
		m.set_shader_parameter("iris_farbe", Vector3(iris.r, iris.g, iris.b))

# ---------------------------------------------------------------- Stoffe
# Teilart nach dem Stoffnamen der .glb
static func teil_art(stoffname: String) -> String:
	match stoffname:
		"Haut": return "haut"
		"Mund": return "mund"
		"Kleidung": return "kleid"
		"Kragen": return "kragen"
		"Augen_Lederhaut": return "lederhaut"
		"Augen_Iris": return "iris"
		"Traene": return "traene"
		"Lidschatten": return "aus"
		"Zaehne": return "zaehne"
		"Zahnfleisch": return "zahnfleisch"
		"Brauen", "Wimpern": return "haar"
	return "sonst"

# Teile zum Prüfen unsichtbar machen (z. B. ["haar", "kragen"]); leere Liste zeigt alles wieder
var _versteckt := {}
func teile_ausblenden(arten: Array) -> void:
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.mesh.surface_get_material(s)
			var art := teil_art(String(alt.resource_name) if alt else "")
			var schl := "%d_%d" % [mi.get_instance_id(), s]
			if art in arten and not _versteckt.has(schl):
				_versteckt[schl] = mi.get_surface_override_material(s)
				mi.set_surface_override_material(s, _leer())
			elif not art in arten and _versteckt.has(schl):
				mi.set_surface_override_material(s, _versteckt[schl])
				_versteckt.erase(schl)

static func _leer() -> StandardMaterial3D:
	var leer := StandardMaterial3D.new()
	leer.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	leer.albedo_color = Color(0, 0, 0, 0)
	return leer

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
			var neu := _stoff(alt as BaseMaterial3D, teil_art(name), mi, s)
			if neu:
				mi.set_surface_override_material(s, neu)

func _stoff(std: BaseMaterial3D, art: String, mi: MeshInstance3D, s: int) -> Material:
	match art:
		"haut":
			var m := ShaderMaterial.new()
			m.shader = HAUT
			m.set_shader_parameter("hat_farbe", false)
			m.set_shader_parameter("poren", poren())
			m.set_shader_parameter("poren_dichte", meter_je_uv(mi.mesh, s) / 0.045)
			haut_stoffe.append(m)
			return m
		"mund":
			var m := ShaderMaterial.new()
			m.shader = MUND
			return m
		"zaehne", "zahnfleisch":
			var m := ShaderMaterial.new()
			m.shader = ZAEHNE
			if art == "zahnfleisch":
				m.set_shader_parameter("farbe", Vector3(0.62, 0.27, 0.26))
				m.set_shader_parameter("rauheit", 0.35)
				m.set_shader_parameter("glanz", 0.4)
				m.set_shader_parameter("anfang", 0.15)
			return m
		"lederhaut", "iris":
			var m := ShaderMaterial.new()
			m.shader = AUGEN
			m.set_shader_parameter("hat_farbe", false)
			m.set_shader_parameter("iris_teil", art == "iris")
			m.set_shader_parameter("lederhaut_teil", art == "lederhaut")
			augen_stoffe.append(m)
			return m
		"traene":
			var m := StandardMaterial3D.new()
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			m.albedo_color = Color(0.9, 0.85, 0.85, 0.12)
			m.roughness = 0.05
			m.metallic_specular = 0.8
			return m
		"haar":
			var m := ShaderMaterial.new()
			m.shader = HAAR
			if std and std.albedo_texture:
				m.set_shader_parameter("bild", std.albedo_texture)
			haar_stoffe.append(m)
			return m
		"kleid", "kragen":
			# Platzhalter: schlichtes Leinen (später zeigen Hemd und Kragen Stand und Beruf)
			var m := StandardMaterial3D.new()
			m.albedo_color = Color(0.56, 0.5, 0.4) if art == "kleid" else Color(0.5, 0.44, 0.35)
			m.roughness = 0.92
			return m
		"aus":
			return _leer()
	return null

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

func dreiecke() -> int:
	var n := 0
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			n += mi.mesh.surface_get_array_index_len(s) / 3
	return n
