# Versuch Charakter Phase 4: Haupthaar und Bart (Testszene, kein Spielcode).
# Daten aus tools/charakter/kopf4_haar.py in <glb-Ordner>/haar4 (nicht im Repository): Stützpunkte der Haut mit ihren
# Formversätzen, je Stil Leitsträhnen und Haarkarten (aus derselben Quelle), Kartenbild, Kopfhautkarten.
#
# Zwei Arten, dasselbe Haar zu zeigen:
#   „karten“   Haarkarten (Rückfall: kleine Porträts, Grafikstufe Niedrig, TressFX fehlt). Die Karten folgen der
#              Kopfform im Shader (haar4_karte.gdshader): Jede Ecke hängt an vier Stützpunkten, deren Versatz hier aus
#              den Formgewichten des Kopfs (Alter, Geschlecht, Gene, Mimik) gerechnet und als Bild übergeben wird.
#   „tressfx“  Strähnen mit dem Addon TressFX (großes Porträt, Editor): Die Leitsträhnen werden mit demselben Versatz in
#              eine .tfx geschrieben (user://charakter4/), das Addon simuliert und zeichnet sie (haar4_straehne.gdshader).
#              Ohne Addon (Klasse TressFXHair fehlt) bleibt es bei den Karten.
# Gene -> Haar (aus_genen): Frisur nach „frisur“ (je Geschlecht drei), Bart nach „bartform“ (rasiert, Schnurrbart,
# Kinnbart, Vollbart, langer Bart) und „bartwuchs“ (Länge, Dichte), Glatze nach „glatze“ und Alter, Ergrauen wie bei den
# Brauen, lichter im Alter.
extends Node3D

const KARTE := preload("res://versuch/charakter/haar4_karte.gdshader")
const STRAEHNE := preload("res://versuch/charakter/haar4_straehne.gdshader")
const RECHNER := "res://versuch/charakter/HaarRechner.cs"
const STUETZ_BREITE := 64

static var _daten := {}          # Pfad (ohne Endung) -> Blöcke
static var _netze := {}          # Pfad -> ArrayMesh der Karten
static var _bilder := {}         # Pfad -> Texture2D
static var _uebersichten := {}   # Ordner -> haar4.json
static var _rechner: Object = null
static var _rechner_geprueft := false
static var _zaehler := 0

var ordner := ""
var stile: Array = []            # [Frisur, Bart] (leerer Name = keins)
var modus := "karten"            # karten | tressfx | aus
var werte := {}
var tfx_ruhe := false            # TressFX nach kurzer Simulation einfrieren (viele Köpfe)
var tfx_sparsam := false         # weniger Folgehaare, Detailstufe nach Abstand (viele Köpfe, kleine Porträts)
var karten_schatten := true      # Karten werfen Schatten (die Schattenkarte zeichnet alle Karten noch einmal)
var stand := 5                   # Phase 6: 5 = Farbe als Vector3 wie Phase 5, 6 = als Color (Godot rechnet sRGB nach linear um)
var verschiebung := PackedFloat32Array()
var zeiten := {}                 # ms: mischen, tfx_schreiben
var _stuetz_bild: Image
var _stuetz_tex: ImageTexture
var _teile: Array = []           # je Teil: {"datei", "art", "karte": MeshInstance3D, "stoff_karte", "tfx": Node, "stoff_tfx"}
var _gewichte := {}
var _tfx_stand := ""
var _id := 0

static func rechner() -> Object:
	if not _rechner_geprueft:
		_rechner_geprueft = true
		if ResourceLoader.exists(RECHNER):
			var s: Script = load(RECHNER)
			if s and s.can_instantiate():
				_rechner = s.new()
	return _rechner

static func tressfx_da() -> bool:
	return ClassDB.class_exists("TressFXHair")

static func uebersicht(o: String) -> Dictionary:
	if not _uebersichten.has(o):
		var p := o.path_join("haar4.json")
		_uebersichten[o] = JSON.parse_string(FileAccess.get_file_as_string(p)) if FileAccess.file_exists(p) else {}
	return _uebersichten[o]

static func bild(pfad: String) -> Texture2D:
	if not _bilder.has(pfad):
		var img := Image.load_from_file(pfad)
		if img == null:
			push_error("Bild fehlt: " + pfad)
			_bilder[pfad] = null
		else:
			img.generate_mipmaps()
			_bilder[pfad] = ImageTexture.create_from_image(img)
	return _bilder[pfad]

static func laden(pfad: String) -> Dictionary:
	if _daten.has(pfad):
		return _daten[pfad]
	var kopf: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(pfad + ".json"))
	var roh := FileAccess.get_file_as_bytes(pfad + ".bin")
	var aus := {"kopf": kopf}
	for name in kopf["bloecke"]:
		var b: Dictionary = kopf["bloecke"][name]
		var teil := roh.slice(int(b["start"]), int(b["start"]) + int(b["bytes"]))
		aus[name] = teil.to_float32_array() if b["art"] == "f32" else teil.to_int32_array()
	_daten[pfad] = aus
	return aus

# ---------------------------------------------------------------- Gene -> Haar
static func _glatt(x: float, a: float, b: float) -> float:
	var t := clampf((x - a) / (b - a), 0.0, 1.0)
	return t * t * (3.0 - 2.0 * t)

static func aus_genen(o: String, gene: Dictionary, jahre: float, mann: bool) -> Dictionary:
	var ue := uebersicht(o)
	if ue.is_empty():
		return {}
	var fr: Array = ue["frisuren"]["m" if mann else "w"]
	var fi := clampi(int(float(gene.get("frisur", 128.0)) * fr.size() / 256.0), 0, fr.size() - 1)
	var baerte: Array = [""] + ue["baerte"]
	var bi := clampi(int(float(gene.get("bartform", 128.0)) * baerte.size() / 256.0), 0, baerte.size() - 1)
	var bw := float(gene.get("bartwuchs" if mann else "bartwuchs_frau", 0.0))
	var bart := ""
	if bw >= 60.0 and jahre >= 15.0:
		bart = baerte[bi]
	var reife := _glatt(jahre, 15.0, 26.0)
	var g := float(gene.get("glatze", 90.0)) / 255.0
	var kahl := 0.0
	if mann:
		kahl = clampf((jahre - (22.0 + (1.0 - g) * 40.0)) / 25.0, 0.0, 1.0) * (0.25 + 0.72 * g)
	return {"frisur": fr[fi], "bart": bart,
		"struktur": float(gene.get("haarstruktur", 60.0)) / 255.0,
		"kahl": kahl,
		"duenn": clampf((jahre - 45.0) / 30.0, 0.0, 1.0) * (0.3 + (0.15 * g if mann else 0.0)),
		"bart_laenge": lerpf(0.35, 1.0, clampf((bw - 60.0) / 100.0, 0.0, 1.0)) * lerpf(0.3, 1.0, reife),
		"bart_dichte": lerpf(0.55, 1.0, clampf((bw - 60.0) / 120.0, 0.0, 1.0)) * lerpf(0.45, 1.0, reife)}

# ---------------------------------------------------------------- Aufbau
func einrichten(haar_ordner: String) -> bool:
	ordner = haar_ordner
	if not FileAccess.file_exists(ordner.path_join("stuetzen.json")):
		return false
	_id = _zaehler
	_zaehler += 1
	var st := laden(ordner.path_join("stuetzen"))
	var n := int(st["kopf"]["anzahl"])
	var hoehe := (n + STUETZ_BREITE - 1) / STUETZ_BREITE
	verschiebung = PackedFloat32Array()
	verschiebung.resize(n * 3)
	_stuetz_bild = Image.create_empty(STUETZ_BREITE, hoehe, false, Image.FORMAT_RGBF)
	_stuetz_tex = ImageTexture.create_from_image(_stuetz_bild)
	return true

func stile_setzen(neue: Array) -> void:
	if neue == stile:
		return
	stile = neue.duplicate()
	for t in _teile:
		if t.get("karte"):
			t["karte"].queue_free()
		if t.get("tfx"):
			t["tfx"].queue_free()
	_teile.clear()
	_tfx_stand = ""
	var ue := uebersicht(ordner)
	for name in stile:
		if name == "" or not ue.get("stile", {}).has(name):
			continue
		for teil in ue["stile"][name]["teile"]:
			# Teile: haupt (Frisur bzw. Bart), zusatz (Zöpfe, Knoten, Polster), Phase 5: netz und perlen (Crespine)
			var tn: String = teil["name"]
			var datei := ordner.path_join(name + ("" if tn == "haupt" else "_" + tn))
			var art: String = ue["stile"][name]["art"] if tn == "haupt" else ("zusatz" if tn == "zusatz" else tn)
			_teile.append({"datei": datei, "art": art, "name": name})
	_aufbauen()

func modus_setzen(m: String) -> void:
	if m == "tressfx" and not tressfx_da():
		push_warning("TressFX fehlt (addons/tressfx): Haarkarten bleiben")
		m = "karten"
	modus = m
	_aufbauen()

func _aufbauen() -> void:
	for t in _teile:
		if t.get("karte") == null:
			var mi := MeshInstance3D.new()
			mi.mesh = _netz(t["datei"])
			var s := ShaderMaterial.new()
			s.shader = KARTE
			s.set_shader_parameter("bild", bild(ordner.path_join("haar_karten.png")))
			s.set_shader_parameter("stuetz_tex", _stuetz_tex)
			if t["art"] == "bart":
				s.set_shader_parameter("innen_dunkel", 0.75)
				s.set_shader_parameter("wurzel_dunkel", 0.7)
			mi.material_override = s
			# Die Lage ändert der Shader: großzügiger Rahmen gegen Wegschneiden
			mi.custom_aabb = AABB(Vector3(-0.4, 0.2, -0.4), Vector3(0.8, 1.0, 0.8))
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if karten_schatten else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(mi)
			t["karte"] = mi
			t["stoff_karte"] = s
		t["karte"].visible = modus == "karten"
		if modus == "tressfx" and t.get("tfx") == null:
			_tfx_anlegen(t)
		if t.get("tfx"):
			t["tfx"].visible = modus == "tressfx"
	_parameter()
	if modus == "tressfx" and not _gewichte.is_empty():
		_tfx_schreiben()

static func _netz(datei: String) -> ArrayMesh:
	if _netze.has(datei):
		return _netze[datei]
	var d := laden(datei)
	var m := ArrayMesh.new()
	if not d.has("karten_pos"):
		push_warning("Keine Haarkarten in " + datei)
		_netze[datei] = m
		return m
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = _vec3(d["karten_pos"])
	arr[Mesh.ARRAY_NORMAL] = _vec3(d["karten_nrm"])
	arr[Mesh.ARRAY_TANGENT] = d["karten_tan"]
	arr[Mesh.ARRAY_TEX_UV] = _vec2(d["karten_uv"])
	arr[Mesh.ARRAY_TEX_UV2] = _vec2(d["karten_uv2"])
	var f: PackedFloat32Array = d["karten_farbe"]
	var farben := PackedColorArray()
	farben.resize(f.size() / 4)
	for i in farben.size():
		farben[i] = Color(f[4 * i], f[4 * i + 1], f[4 * i + 2], f[4 * i + 3])
	arr[Mesh.ARRAY_COLOR] = farben
	arr[Mesh.ARRAY_CUSTOM0] = d["karten_idx"]
	arr[Mesh.ARRAY_CUSTOM1] = d["karten_gew"]
	arr[Mesh.ARRAY_INDEX] = d["karten_index"]
	var flags := (Mesh.ARRAY_CUSTOM_RGBA_FLOAT << Mesh.ARRAY_FORMAT_CUSTOM0_SHIFT) | (Mesh.ARRAY_CUSTOM_RGBA_FLOAT << Mesh.ARRAY_FORMAT_CUSTOM1_SHIFT)
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr, [], {}, flags)
	_netze[datei] = m
	return m

static func _vec3(f: PackedFloat32Array) -> PackedVector3Array:
	var v := PackedVector3Array()
	v.resize(f.size() / 3)
	for i in v.size():
		v[i] = Vector3(f[3 * i], f[3 * i + 1], f[3 * i + 2])
	return v

static func _vec2(f: PackedFloat32Array) -> PackedVector2Array:
	var v := PackedVector2Array()
	v.resize(f.size() / 2)
	for i in v.size():
		v[i] = Vector2(f[2 * i], f[2 * i + 1])
	return v

# ---------------------------------------------------------------- TressFX
func _tfx_anlegen(t: Dictionary) -> void:
	var haar: Node3D = ClassDB.instantiate("TressFXHair")
	var s := ShaderMaterial.new()
	s.shader = STRAEHNE
	var bart: bool = t["art"] == "bart"
	if bart:
		# Phase 5: Bart kräftiger und deckender (war fleckig und durchscheinend)
		s.set_shader_parameter("fiber_radius", 0.00034)
		s.set_shader_parameter("fiber_ratio", 0.4)
		s.set_shader_parameter("tip_fade", 0.3)
		s.set_shader_parameter("root_shadow", 0.35)
		s.set_shader_parameter("innen_dunkel", 0.45)
		if String(t["datei"]).get_file() == "stoppeln":
			# Stoppeln: feine, kurze Haare – sonst wirken sie wie ein dichter Pelz
			s.set_shader_parameter("fiber_radius", 0.00011)
			s.set_shader_parameter("fiber_ratio", 0.6)
			s.set_shader_parameter("tip_fade", 0.2)
			s.set_shader_parameter("innen_dunkel", 0.7)
	add_child(haar)
	haar.set("material", s)
	# Formbindung: die gekämmte Ruheform soll halten; Bart und Zöpfe fast starr
	var steif: bool = bart or t["art"] in ["zusatz", "netz", "perlen"]
	if t["art"] == "netz":
		# Goldschnur der Crespine: metallisch glänzend, nicht ergrauend
		s.set_shader_parameter("fiber_radius", 0.00028)
		s.set_shader_parameter("fiber_ratio", 1.0)
		s.set_shader_parameter("tip_fade", 0.0)
		s.set_shader_parameter("root_shadow", 0.0)
		s.set_shader_parameter("color_variation", 0.15)
		s.set_shader_parameter("k_diffuse", 0.3)
		s.set_shader_parameter("k_spec1", 0.5)
		s.set_shader_parameter("spec_exp1", 30.0)
		s.set_shader_parameter("innen_dunkel", 0.9)
		s.set_shader_parameter("spitze_hell", 1.0)
	elif t["art"] == "perlen":
		s.set_shader_parameter("fiber_radius", 0.0011)
		s.set_shader_parameter("fiber_ratio", 1.0)
		s.set_shader_parameter("tip_fade", 0.0)
		s.set_shader_parameter("root_shadow", 0.0)
		s.set_shader_parameter("color_variation", 0.1)
		s.set_shader_parameter("k_diffuse", 0.35)
		s.set_shader_parameter("k_spec1", 0.35)
		s.set_shader_parameter("innen_dunkel", 1.0)
		s.set_shader_parameter("spitze_hell", 1.0)
	var stoppeln := String(t["datei"]).get_file() == "stoppeln"
	var schmuck: bool = t["art"] in ["netz", "perlen"]
	haar.set("num_follow_hairs", 1 if schmuck or stoppeln else ((2 if tfx_sparsam else 3) if not bart else (2 if tfx_sparsam else 3)))
	haar.set("follow_radius", 0.0002 if schmuck else (0.004 if not bart else 0.003))
	haar.set("tip_separation", 0.3 if not bart else 0.15)
	haar.set("global_stiffness", 1.0 if steif else 0.85)
	haar.set("global_range", 1.0)
	haar.set("local_stiffness", 1.0 if steif else 0.92)
	haar.set("vsp_coeff", 0.9)
	haar.set("gravity", 0.0 if steif else 0.15)
	haar.set("damping", 0.35)
	haar.set("collision_skip_root_vertices", 3)
	haar.set("cast_hair_shadows", false)
	if tfx_sparsam:
		# viele Köpfe: Detailstufe nach Abstand (das Addon blendet Strähnen aus und zeichnet die übrigen breiter)
		haar.set("lod_enabled", true)
		haar.set("lod_start_distance", 0.8)
		haar.set("lod_end_distance", 4.0)
		haar.set("lod_percent", 0.25)
	t["tfx"] = haar
	t["stoff_tfx"] = s

func _tfx_schreiben() -> void:
	var kennung := str(_gewichte.hash()) + "_" + str(stile) + "_" + str(werte.get("struktur", 0.0))
	if kennung == _tfx_stand:
		return
	_tfx_stand = kennung
	var t0 := Time.get_ticks_usec()
	DirAccess.make_dir_recursive_absolute("user://charakter4")
	for t in _teile:
		if t.get("tfx") == null:
			continue
		var d := laden(t["datei"])
		var S := int(d["kopf"]["straehnen"])
		var P := int(d["kopf"]["punkte"])
		var pos := _verformen(d["straehnen_pos"], d["straehnen_idx"], d["straehnen_gew"], P)
		var info: PackedFloat32Array = d["straehnen_info"]
		# Phase 5: Haarstruktur (glatt – wellig – lockig) auf die Strähnen legen; Locken brauchen mehr Punkte
		var r := rechner()
		var st_w := _struktur(t["art"] == "bart")
		if r and float(st_w["hoehe"]) > 0.0002 and not t["art"] in ["netz", "perlen"]:
			var phase := PackedFloat32Array()
			phase.resize(S)
			for i in S:
				phase[i] = info[4 * i + 2]
			var neuP := mini(64, P * int(st_w["mal"]))
			pos = r.call("Struktur", pos, S, P, neuP, float(st_w["hoehe"]), float(st_w["wl"]), float(st_w["locke"]), phase,
				werte.get("kopfmitte", Vector3(0, 0.82, 0.04)))
			P = neuP
		# Strähnen-UV: x = Zufall (64 Stufen) + Lage über der Haut (Nachkomma, Phase 5), y = Rang (Glatze)
		var uv := PackedFloat32Array()
		uv.resize(S * 2)
		for i in S:
			uv[2 * i] = (floorf(info[4 * i] * 63.0) + clampf(info[4 * i + 3], 0.0, 0.99)) / 64.0
			uv[2 * i + 1] = info[4 * i + 1]
		var pfad := "user://charakter4/k%d_%s.tfx" % [_id, t["datei"].get_file()]
		var f := FileAccess.open(pfad, FileAccess.WRITE)
		f.store_float(4.0)
		for z in [S, P, 160, 160 + S * P * 16, 0, 0, 0]:
			f.store_32(z)
		var leer := PackedByteArray()
		leer.resize(128)
		f.store_buffer(leer)
		f.store_buffer(pos.to_byte_array())
		f.store_buffer(uv.to_byte_array())
		f.close()
		var haar: Node = t["tfx"]
		haar.set("tfx_path", "")
		haar.set("tfx_path", ProjectSettings.globalize_path(pfad))
		if tfx_ruhe:
			_einfrieren(haar)
	zeiten["tfx_schreiben"] = (Time.get_ticks_usec() - t0) / 1000.0

# Haarstruktur aus werte["struktur"] (0 glatt, 0,5 wellig, 1 lockig; vorläufig aus dem Gen-Vorschlag „haarstruktur“):
# Ausschlag, Wellenlänge, Schraubenanteil, Faktor für die Punktzahl. Bärte kräuseln enger und flacher.
func _struktur(bart: bool) -> Dictionary:
	var g := clampf(float(werte.get("struktur", 0.0)), 0.0, 1.0)
	var hoehe := lerpf(0.0, 0.006, _glatt(g, 0.08, 0.5)) + lerpf(0.0, 0.0012, _glatt(g, 0.6, 1.0))
	var wl := lerpf(0.065, 0.02, _glatt(g, 0.35, 1.0))
	var locke := _glatt(g, 0.55, 0.95)
	if bart:
		hoehe *= 0.55
		wl *= 0.55
	return {"hoehe": hoehe, "wl": wl, "locke": locke, "mal": 1 if g < 0.3 else (2 if g < 0.7 else 4)}

func _einfrieren(haar: Node3D) -> void:
	# Ohne Simulation zeichnet das Addon nichts: kurz simulieren, dann über die Simulationsweite anhalten
	haar.set("simulation_distance", 100.0)
	var tm := get_tree().create_timer(0.8) if is_inside_tree() else null
	if tm:
		# Phase 6: gebundene Methode statt Lambda – ein Lambda, dessen erfasstes Haar vorher freigegeben wurde, meldete
		# „Lambda capture at index 0 was freed“; die Verbindung zu einer Methode löst Godot beim Freigeben selbst
		tm.timeout.connect(_anhalten.bind(weakref(haar)))

func _anhalten(ref: WeakRef) -> void:
	var haar: Node3D = ref.get_ref()
	if haar and is_instance_valid(haar):
		haar.set("simulation_distance", 0.01)

func _verformen(basis: PackedFloat32Array, idx: PackedInt32Array, gew: PackedFloat32Array, P: int) -> PackedFloat32Array:
	var r := rechner()
	if r:
		return r.call("Verformen", basis, idx, gew, verschiebung, P)
	var n := basis.size() / 3
	var aus := PackedFloat32Array()
	aus.resize(n * 4)
	for i in n:
		var x := basis[3 * i]
		var y := basis[3 * i + 1]
		var z := basis[3 * i + 2]
		for k in 4:
			var c := idx[4 * i + k]
			var w := gew[4 * i + k]
			x += w * verschiebung[3 * c]
			y += w * verschiebung[3 * c + 1]
			z += w * verschiebung[3 * c + 2]
		aus[4 * i] = x
		aus[4 * i + 1] = y
		aus[4 * i + 2] = z
		aus[4 * i + 3] = 0.0 if i % P < 2 else 1.0
	return aus

# ---------------------------------------------------------------- Aktualisieren
# gewichte: Formgewichte des Kopfs (Name -> Gewicht); neue_werte: farbe, grau, kahl, duenn, bart_laenge, bart_dichte
func aktualisieren(gewichte: Dictionary, neue_werte: Dictionary) -> void:
	werte = neue_werte
	_gewichte = gewichte
	var t0 := Time.get_ticks_usec()
	var st := laden(ordner.path_join("stuetzen"))
	var n := int(st["kopf"]["anzahl"])
	var namen: Array = st["kopf"]["formen"]
	if not st.has("index"):
		var ix := {}
		for i in namen.size():
			ix[namen[i]] = i
		st["index"] = ix
	var formen := PackedInt32Array()
	var gew := PackedFloat32Array()
	for k in gewichte:
		var w := float(gewichte[k])
		if absf(w) > 1e-4 and st["index"].has(k):
			formen.append(st["index"][k])
			gew.append(w)
	var r := rechner()
	if r:
		verschiebung = r.call("Mischen", st["versatz"], n, formen, gew)
	else:
		verschiebung.fill(0.0)
		var vs: PackedFloat32Array = st["versatz"]
		for f in formen.size():
			var w := gew[f]
			var start := formen[f] * n * 3
			for j in n * 3:
				verschiebung[j] += w * vs[start + j]
	var bytes := verschiebung.to_byte_array()
	bytes.resize(_stuetz_bild.get_width() * _stuetz_bild.get_height() * 12)
	_stuetz_bild.set_data(_stuetz_bild.get_width(), _stuetz_bild.get_height(), false, Image.FORMAT_RGBF, bytes)
	_stuetz_tex.update(_stuetz_bild)
	zeiten["mischen"] = (Time.get_ticks_usec() - t0) / 1000.0
	_parameter()
	if modus == "tressfx":
		_tfx_schreiben()

func _parameter() -> void:
	var farbe: Color = werte.get("farbe", Color(0.25, 0.16, 0.09))
	for t in _teile:
		if t.get("karte"):
			t["karte"].cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if karten_schatten else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var bart: bool = t["art"] == "bart"
		for s in [t.get("stoff_karte"), t.get("stoff_tfx")]:
			if s == null:
				continue
			var schmuck: bool = t["art"] in ["netz", "perlen"]
			var f := Color(0.8, 0.6, 0.26) if t["art"] == "netz" else (Color(0.9, 0.88, 0.82) if t["art"] == "perlen" else farbe)
			# Phase 6 (S2): Stand 6 übergibt Color (auch Goldnetz und Perlen), Stand 5 wie Phase 5 als Vector3
			if stand >= 6:
				s.set_shader_parameter("farbe", f)
			else:
				s.set_shader_parameter("farbe", Vector3(f.r, f.g, f.b))   # stand5: Weg der Phase 5
			s.set_shader_parameter("grau", 0.0 if schmuck else float(werte.get("grau", 0.0)))
			s.set_shader_parameter("kahl", 0.0 if bart or t["art"] in ["zusatz", "netz", "perlen"] else float(werte.get("kahl", 0.0)))
			s.set_shader_parameter("duenn", 0.0 if schmuck else float(werte.get("duenn", 0.0)) * (0.5 if bart else 1.0))
			s.set_shader_parameter("laenge", float(werte.get("bart_laenge", 1.0)) if bart else 1.0)
			s.set_shader_parameter("dichte", float(werte.get("bart_dichte", 1.0)) if bart else 1.0)
			if s == t.get("stoff_tfx"):
				var km: Vector3 = werte.get("kopfmitte", Vector3(0, 0.82, 0.04))
				s.set_shader_parameter("kopf_mitte", global_transform * km if is_inside_tree() else km)

func dreiecke() -> int:
	var n := 0
	for t in _teile:
		if modus == "karten" and t.get("karte") and t["karte"].mesh.get_surface_count() > 0:
			n += t["karte"].mesh.surface_get_array_index_len(0) / 3
	return n
