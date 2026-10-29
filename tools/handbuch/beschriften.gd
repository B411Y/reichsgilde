# Beschriftet Bildschirmfotos für das Spielbuch: nummerierte Kreise (Gold auf Dunkelbraun) und auf Wunsch ein Rahmen um den Bereich.
# Aufruf über beschriften.sh (braucht ein Fenster, also Xvfb; die Ziffern setzt die Schrift des Spiels).
# Eingabe: eine JSON-Datei mit { "bilder": [ { "roh": "name", "ziel": "name", "breite": 1280,
#   "markierungen": [ { "n": 1, "x": 120, "y": 40, "rahmen": [x, y, breite, hoehe] } ] } ] }
# Koordinaten gelten für das Rohbild (1600 x 900), der Kreis sitzt mit seiner Mitte auf (x, y); „rahmen“ ist optional.
# „ausschnitt“: [x, y, breite, hoehe] schneidet vorher einen Teil des Rohbilds aus (die Markierungen behalten die Koordinaten des Rohbilds).
extends SceneTree

const GOLD := Color(0.95, 0.82, 0.45)
const BRAUN := Color(0.16, 0.09, 0.05)
const SCHRIFT := "res://assets/oberflaeche/schriften/GermaniaOne-Regular.ttf"

var _auftraege: Array = []
var _index := 0
var _roh_ordner := ""
var _ziel_ordner := ""
var _viewport: SubViewport
var _frames := 0

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 3:
		push_error("Aufruf: -- <json> <rohordner> <zielordner>")
		quit(1)
		return
	var json = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	if json == null:
		push_error("JSON nicht lesbar: " + args[0])
		quit(1)
		return
	_auftraege = json["bilder"]
	_roh_ordner = args[1]
	_ziel_ordner = args[2]
	DirAccess.make_dir_recursive_absolute(_ziel_ordner)
	_naechstes()

func _naechstes() -> void:
	if _viewport != null:
		_viewport.queue_free()
		_viewport = null
	if _index >= _auftraege.size():
		quit(0)
		return
	var a: Dictionary = _auftraege[_index]
	var bild := Image.load_from_file(_roh_ordner.path_join(a["roh"] + ".png"))
	if bild == null:
		push_error("Rohbild fehlt: " + a["roh"])
		quit(1)
		return
	var versatz := Vector2.ZERO
	if a.has("ausschnitt"):
		var t: Array = a["ausschnitt"]
		bild = bild.get_region(Rect2i(t[0], t[1], t[2], t[3]))
		versatz = Vector2(t[0], t[1])
	var faktor := float(a.get("breite", 1280)) / bild.get_width()
	var groesse := Vector2i(round(bild.get_width() * faktor), round(bild.get_height() * faktor))
	_viewport = SubViewport.new()
	_viewport.size = groesse
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_viewport.transparent_bg = false
	root.add_child(_viewport)
	var tex := TextureRect.new()
	tex.texture = ImageTexture.create_from_image(bild)
	tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tex.stretch_mode = TextureRect.STRETCH_SCALE
	tex.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	tex.size = Vector2(groesse)
	_viewport.add_child(tex)
	var schrift: Font = load(SCHRIFT)
	for m in a["markierungen"]:
		var mitte := (Vector2(m["x"], m["y"]) - versatz) * faktor
		if m.has("rahmen"):
			var r: Array = m["rahmen"]
			var rahmen := Panel.new()
			var rs := StyleBoxFlat.new()
			rs.bg_color = Color(GOLD, 0.0)
			rs.set_border_width_all(3)
			rs.border_color = GOLD
			rs.set_corner_radius_all(4)
			rahmen.add_theme_stylebox_override("panel", rs)
			rahmen.position = (Vector2(r[0], r[1]) - versatz) * faktor
			rahmen.size = Vector2(r[2], r[3]) * faktor
			rahmen.mouse_filter = Control.MOUSE_FILTER_IGNORE
			_viewport.add_child(rahmen)
			var aussen := Panel.new()
			var os := StyleBoxFlat.new()
			os.bg_color = Color(BRAUN, 0.0)
			os.set_border_width_all(1)
			os.border_color = BRAUN
			os.set_corner_radius_all(5)
			aussen.add_theme_stylebox_override("panel", os)
			aussen.position = rahmen.position - Vector2(2, 2)
			aussen.size = rahmen.size + Vector2(4, 4)
			_viewport.add_child(aussen)
		var d := 34.0
		var kreis := Panel.new()
		var sb := StyleBoxFlat.new()
		sb.bg_color = BRAUN
		sb.set_border_width_all(3)
		sb.border_color = GOLD
		sb.set_corner_radius_all(int(d))
		sb.shadow_color = Color(0, 0, 0, 0.55)
		sb.shadow_size = 4
		kreis.add_theme_stylebox_override("panel", sb)
		kreis.size = Vector2(d, d)
		kreis.position = mitte - Vector2(d, d) / 2.0
		_viewport.add_child(kreis)
		var zahl := Label.new()
		zahl.text = str(int(m["n"]))
		zahl.add_theme_font_override("font", schrift)
		zahl.add_theme_font_size_override("font_size", 22)
		zahl.add_theme_color_override("font_color", GOLD)
		zahl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		zahl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		zahl.size = Vector2(d, d)
		zahl.position = kreis.position + Vector2(0, -1)
		_viewport.add_child(zahl)
	_frames = 0

func _process(_delta: float) -> bool:
	if _viewport == null:
		return false
	_frames += 1
	if _frames < 5:
		return false
	var a: Dictionary = _auftraege[_index]
	var ergebnis := _viewport.get_texture().get_image()
	var pfad: String = _ziel_ordner.path_join(a["ziel"] + ".webp")
	var fehler := ergebnis.save_webp(pfad, true, float(a.get("qualitaet", 0.85)))
	print("%s: %s (%d x %d, %d KB)" % [pfad, "ok" if fehler == OK else "FEHLER %d" % fehler, ergebnis.get_width(), ergebnis.get_height(), FileAccess.get_file_as_bytes(pfad).size() / 1024])
	_index += 1
	_naechstes()
	return false
