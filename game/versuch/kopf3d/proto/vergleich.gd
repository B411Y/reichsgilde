# Vergleichstafel 2D-Porträt gegen 3D-Kopf (Ruhe, Sprechen, alt, jung, Frisuren, Bart) plus Messung.
# Start: godot --path game res://versuch/kopf3d/proto/vergleich.tscn -- --bilder=<ordner> [--portraet=<png>] [--messen]
extends Control

var bilder := ""
var portraet := ""
var messen := false
var _t := 0.0
var _fenster: Array[KopfFenster] = []
var _foto_gemacht := false

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="): bilder = a.substr(9)
		if a.begins_with("--portraet="): portraet = a.substr(11)
		if a == "--messen": messen = true
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	if messen:
		_messung_starten()
		return
	var raster := GridContainer.new()
	raster.columns = 4
	raster.add_theme_constant_override("h_separation", 14)
	raster.add_theme_constant_override("v_separation", 10)
	raster.position = Vector2(10, 8)
	add_child(raster)
	# 1: 2D-Porträt
	var tr := TextureRect.new()
	if portraet != "" and FileAccess.file_exists(portraet):
		tr.texture = ImageTexture.create_from_image(Image.load_from_file(portraet))
	tr.custom_minimum_size = Vector2(406, 434)
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	var rahmen := PanelContainer.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.16, 0.15, 0.13); sb.set_border_width_all(3); sb.border_color = Color(0.04, 0.03, 0.02); sb.set_content_margin_all(7)
	rahmen.add_theme_stylebox_override("panel", sb)
	rahmen.add_child(tr)
	_zelle(raster, rahmen, "2D-Porträt (Bild-KI, Holzschnitt)")
	var braun := Color(0.20, 0.11, 0.05)
	var grau := Color(0.50, 0.48, 0.46)
	var faelle := [
		["3D: Ruhe", {"frisur": 1, "bart": true}, {}],
		["3D: Sprechen (a)", {"frisur": 1, "bart": true}, {"jawOpen": 0.62, "mouthLowerDownLeft": 0.3, "mouthLowerDownRight": 0.3}],
		["3D: alt (Alter +1)", {"frisur": 1, "bart": true, "alter": 1.0, "haarfarbe": grau}, {}],
		["3D: jung (Alter -1), kurz", {"frisur": 1, "bart": false, "alter": -1.0, "haarfarbe": Color(0.35, 0.21, 0.09)}, {}],
		["3D: Frisur lang + Bart", {"frisur": 2, "bart": true}, {}],
		["3D: Frisur lang, ohne Bart", {"frisur": 2, "bart": false, "haarfarbe": Color(0.5, 0.3, 0.12), "hautton": Color(0.92, 0.82, 0.74)}, {}],
		["3D: Kiefer breit, Nase gross", {"frisur": 1, "bart": false, "kiefer": 1.0, "nase": 1.0}, {}],
	]
	for f in faelle:
		var fw := KopfFenster.new(Vector2i(392, 420))
		_fenster.append(fw)
		_zelle(raster, fw, f[0])
		var kopf := fw.kopf
		kopf.animiert = false
		kopf.blick_wechsel = false
		for k in f[1]:
			if k == "haarfarbe":
				kopf.haarfarbe = f[1][k]
			else:
				kopf.set(k, f[1][k])
		if not f[2].is_empty():
			kopf.mund_haltung(f[2])
		if not f[1].has("haarfarbe"):
			kopf.haarfarbe = braun

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
	if messen or bilder == "":
		return
	if _t > 3.0 and not _foto_gemacht:
		_foto_gemacht = true
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(bilder.path_join("vergleich.png"))
		print("FOTO vergleich FPS ", Engine.get_frames_per_second())
		get_tree().quit()

# ---- Messung: Speicher und Bildrate eines Kopfes ---------------------------------------------
func _info(n: int) -> float:
	return float(RenderingServer.get_rendering_info(n))

func _stand() -> Dictionary:
	return {"textur": _info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED), "puffer": _info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED),
		"video": _info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED), "dreiecke": _info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
		"zeichnungen": _info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)}

func _fps_messen(sek: float) -> float:
	var n := 0
	var t0 := Time.get_ticks_msec()
	while (Time.get_ticks_msec() - t0) < sek * 1000.0:
		await get_tree().process_frame
		n += 1
	return n / ((Time.get_ticks_msec() - t0) / 1000.0)

func _messung_starten() -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	var leer := _stand()
	var fw := KopfFenster.new(Vector2i(384, 448))
	add_child(fw)
	fw.kopf.frisur = 2
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().create_timer(1.0).timeout
	var mit := _stand()
	var fps1 := await _fps_messen(3.0)
	print("MESS Kopf: Texturen %.1f MB, Puffer %.1f MB, Video gesamt %.1f MB" % [(mit.textur - leer.textur) / 1048576.0, (mit.puffer - leer.puffer) / 1048576.0, (mit.video - leer.video) / 1048576.0])
	print("MESS Dreiecke im Bild (alle Durchgänge, mit Schatten) %d, Zeichenaufrufe %d" % [mit.dreiecke - leer.dreiecke, mit.zeichnungen - leer.zeichnungen])
	print("MESS FPS mit 1 Kopf (Frisur lang, Bart, SubViewport 384x448, MSAA 4x): %.0f" % fps1)
	# mehrere Köpfe: 4 weitere
	var weitere: Array = []
	for i in 4:
		var w := KopfFenster.new(Vector2i(384, 448))
		w.position = Vector2(400 + 50 * i, 20 * i)
		add_child(w)
		weitere.append(w)
	await get_tree().create_timer(1.0).timeout
	var fps5 := await _fps_messen(3.0)
	var mit5 := _stand()
	print("MESS FPS mit 5 Köpfen: %.0f, Texturen gesamt +%.1f MB" % [fps5, (mit5.textur - leer.textur) / 1048576.0])
	# Sprechen läuft
	fw.kopf.sprechen_audio(load("res://versuch/kopf3d/proto/probe.wav"))
	var fps_s := await _fps_messen(3.0)
	print("MESS FPS mit 5 Köpfen, einer spricht (Audio): %.0f" % fps_s)
	get_tree().quit()
