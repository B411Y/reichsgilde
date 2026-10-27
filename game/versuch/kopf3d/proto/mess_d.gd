# Plan D messen: 8 Köpfe gleichzeitig (wie Stammbaum/Ratsbaum), jeder im eigenen Fenster, animiert und sprechend.
#   godot --path game res://versuch/kopf3d/proto/mess_d.tscn -- --koepfe=res://a_d.glb,res://b_d.glb [--anzahl=8] [--bilder=<ordner>]
extends Control

var _t := 0.0
var _bilder := 0
var _fps := []
var _koepfe: Array = []
var _fertig := false
var bilder := ""

func _ready() -> void:
	var pfade := PackedStringArray()
	var anzahl := 8
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--koepfe="): pfade = a.substr(9).split(",")
		elif a.begins_with("--anzahl="): anzahl = int(a.substr(9))
		elif a.begins_with("--bilder="): bilder = a.substr(9)
		elif a == "--sparsam": KopfFenster.sparsam = true
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var raster := GridContainer.new()
	raster.columns = 4
	add_child(raster)
	var vor := Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)
	for i in anzahl:
		var fw := KopfFenster.new(Vector2i(370, 410), Color(0.72, 0.74, 0.78), pfade[i % pfade.size()], true)
		raster.add_child(fw)
		_koepfe.append(fw.kopf)
	print("MESS Köpfe ", anzahl, " Grafikspeicher vorher ", vor / 1048576.0, " MB")

func _process(dt: float) -> void:
	_t += dt
	if _t > 2.0 and _t < 2.1:
		for k in _koepfe:
			k.sprechen_text("Gott zum Gruße, Meister! Was darf es heute sein? Wir haben Tuch, Wein und Salz aus Lüneburg.")
	if _t > 3.0 and not _fertig:
		_fps.append(1.0 / maxf(dt, 1e-6))
	if _t > 9.0 and not _fertig:
		_fertig = true
		_fps.sort()
		var mittel := 0.0
		for f in _fps: mittel += f
		mittel /= _fps.size()
		print("MESS Bildrate Mittel %.1f, Median %.1f, 5%%-Tief %.1f" % [mittel, _fps[_fps.size() / 2], _fps[int(_fps.size() * 0.05)]])
		print("MESS Grafikspeicher %.1f MB, Texturen %.1f MB, Puffer %.1f MB, Hauptspeicher (statisch) %.1f MB" % [
			Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / 1048576.0, Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED) / 1048576.0,
			Performance.get_monitor(Performance.RENDER_BUFFER_MEM_USED) / 1048576.0, Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0])
		print("MESS Draw Calls ", Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME), " Primitive ", Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
		if bilder != "":
			await RenderingServer.frame_post_draw
			get_viewport().get_texture().get_image().save_png(bilder.path_join("mess_8.png"))
		get_tree().quit()
