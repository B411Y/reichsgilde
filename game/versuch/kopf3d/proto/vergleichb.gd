# Plan-B-Tafel: 2D-Porträt | realistische Zwischenstufe | TRELLIS roh | fertiger Kopf (Ruhe, Sprechen, Blinzeln, Regler, Seite).
# godot --path game res://versuch/kopf3d/proto/vergleichb.tscn -- --bilder=<ordner> --portraet=<png> --real=<png> --roh=<png> --kopf=res://...glb [--name=m2]
extends Control

var bilder := ""
var pfade := {}
var kopf := ""
var name_ := "tafel"
var _dy := 0.0
var _t := 0.0
var _gemacht := false

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="): bilder = a.substr(9)
		elif a.begins_with("--portraet="): pfade["portraet"] = a.substr(11)
		elif a.begins_with("--real="): pfade["real"] = a.substr(7)
		elif a.begins_with("--roh="): pfade["roh"] = a.substr(6)
		elif a.begins_with("--kopf="): kopf = a.substr(7)
		elif a.begins_with("--name="): name_ = a.substr(7)
		elif a.begins_with("--dy="): _dy = float(a.substr(5))
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var raster := GridContainer.new()
	raster.columns = 4
	raster.add_theme_constant_override("h_separation", 14)
	raster.add_theme_constant_override("v_separation", 10)
	raster.position = Vector2(10, 8)
	add_child(raster)
	_bild(raster, "portraet", "1. 2D-Porträt (Bild-KI, Holzschnitt)")
	_bild(raster, "real", "2. Realistische Zwischenstufe (Bild-KI)")
	_bild(raster, "roh", "3. TRELLIS roh (Form + Textur, 300 000 Flächen)")
	var faelle := [
		["4. Fertig: Ruhe", {}, {}, ""],
		["5. Fertig: Sprechen (a)", {}, {"jawOpen": 0.9, "mouthLowerDownLeft": 0.35, "mouthLowerDownRight": 0.35}, ""],
		["6. Fertig: Blinzeln", {"blink": 1.0}, {}, ""],
		["7. Fertig: Lächeln", {}, {"jawOpen": 0.2, "mouthSmileLeft": 1.0, "mouthSmileRight": 1.0}, ""],
		["8. Dreiviertel von der Seite", {}, {}, "0.9,1.66,0.8"],
	]
	for f in faelle:
		var fw := KopfFenster.new(Vector2i(392, 420), Color(0.72, 0.74, 0.78), kopf, true)
		fw.dy = _dy
		var k := fw.kopf
		k.animiert = false
		k.blick_wechsel = false
		for key in f[1]:
			if key == "blink":
				k.set_meta("blink", f[1][key])
			else:
				k.set(key, f[1][key])
		if not f[2].is_empty():
			k.mund_haltung(f[2])
		if f[3] != "":
			var t: PackedStringArray = f[3].split(",")
			fw.kamera_ziel = Vector3(float(t[0]), float(t[1]), float(t[2]))
		_zelle(raster, fw, f[0])
		_blinker.append(k)

var _blinker: Array = []

func _bild(raster: Control, schluessel: String, titel: String) -> void:
	var tr := TextureRect.new()
	if pfade.has(schluessel) and FileAccess.file_exists(pfade[schluessel]):
		tr.texture = ImageTexture.create_from_image(Image.load_from_file(pfade[schluessel]))
	tr.custom_minimum_size = Vector2(406, 434)
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	var rahmen := PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.16, 0.15, 0.13); sb.set_border_width_all(3); sb.border_color = Color(0.04, 0.03, 0.02); sb.set_content_margin_all(7)
	rahmen.add_theme_stylebox_override("panel", sb)
	rahmen.add_child(tr)
	_zelle(raster, rahmen, titel)

func _zelle(raster: Control, inhalt: Control, titel: String) -> void:
	var v := VBoxContainer.new()
	v.add_child(inhalt)
	var l := Label.new()
	l.text = titel
	l.add_theme_font_size_override("font_size", 15)
	l.add_theme_color_override("font_color", Color(0.95, 0.82, 0.45))
	v.add_child(l)
	raster.add_child(v)

func _process(dt: float) -> void:
	_t += dt
	for k in _blinker:
		if k.has_meta("blink"):
			k._form("eyeBlinkLeft", k.get_meta("blink")); k._form("eyeBlinkRight", k.get_meta("blink"))
	if bilder != "" and _t > 3.0 and not _gemacht:
		_gemacht = true
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(bilder.path_join("tafel_" + name_ + ".png"))
		print("FOTO tafel FPS ", Engine.get_frames_per_second())
		get_tree().quit()
