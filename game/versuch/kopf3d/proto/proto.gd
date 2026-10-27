# Testszene Live-3D-Kopf: Kopf im Rahmen, rechts Regler. Start:
#   godot --path game res://versuch/kopf3d/proto/proto.tscn -- [--bilder=<ordner>]
# Mit --bilder macht die Szene nach einigen Sekunden Fotos und beendet sich.
extends Control

var fenster: KopfFenster
var bilder := ""
var _t := 0.0
var _fotos := []
var _ohnebart := false
var _ereignis1 := false
var _ereignis2 := false

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="):
			bilder = a.substr(9)
		if a == "--ohnebart":
			_ohnebart = true
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var box := HBoxContainer.new()
	box.set_anchors_preset(Control.PRESET_FULL_RECT)
	box.add_theme_constant_override("separation", 18)
	add_child(box)
	fenster = KopfFenster.new(Vector2i(480, 560))
	box.add_child(fenster)
	var rechts := VBoxContainer.new()
	rechts.custom_minimum_size = Vector2(330, 0)
	box.add_child(rechts)
	_regler(rechts, "Kiefer (schmal - breit)", -1, 1, 0, func(v): fenster.kopf.kiefer = v)
	_regler(rechts, "Nase (klein - gross)", -1, 1, 0, func(v): fenster.kopf.nase = v)
	_regler(rechts, "Alter (jung - alt)", -1, 1, 0, func(v): fenster.kopf.alter = v)
	_regler(rechts, "Hautton (hell - dunkel)", 0, 1, 0.0, func(v): fenster.kopf.hautton = Color(1, 1, 1).lerp(Color(0.62, 0.45, 0.34), v))
	_regler(rechts, "Haarfarbe (dunkel - hell)", 0, 1, 0.3, func(v): fenster.kopf.haarfarbe = Color(0.10, 0.06, 0.03).lerp(Color(0.62, 0.42, 0.2), v).lerp(Color(0.85, 0.82, 0.78), clampf((v - 0.8) * 5.0, 0, 1)))
	var fr := OptionButton.new()
	for n in ["keine Frisur", "Frisur kurz", "Frisur lang"]:
		fr.add_item(n)
	fr.select(1)
	fr.item_selected.connect(func(i): fenster.kopf.frisur = i)
	rechts.add_child(fr)
	var bt := CheckButton.new()
	bt.text = "Bart"
	bt.button_pressed = true
	bt.toggled.connect(func(v): fenster.kopf.bart = v)
	rechts.add_child(bt)
	var tx := LineEdit.new()
	tx.text = "Willkommen in unserer Stadt, werter Herr."
	rechts.add_child(tx)
	var sp := Button.new()
	sp.text = "Sprechen (Text)"
	sp.pressed.connect(func(): fenster.kopf.sprechen_text(tx.text))
	rechts.add_child(sp)
	var sa := Button.new()
	sa.text = "Sprechen (Audio-Probe)"
	sa.pressed.connect(func(): fenster.kopf.sprechen_audio(load("res://versuch/kopf3d/proto/probe.wav")))
	rechts.add_child(sa)
	if _ohnebart:
		bt.button_pressed = false
	_fotos = [[1.5, "ruhe"], [2.2, "sprechen1"], [2.45, "sprechen2"], [2.7, "sprechen3"], [4.5, "blinzeln"]] if bilder != "" else []

func _regler(eltern: Control, titel: String, mn: float, mx: float, start: float, f: Callable) -> void:
	var l := Label.new()
	l.text = titel
	eltern.add_child(l)
	var s := HSlider.new()
	s.min_value = mn; s.max_value = mx; s.step = 0.01; s.value = start
	s.custom_minimum_size = Vector2(300, 0)
	print('REGLER ', titel, ' ', s.value)
	s.value_changed.connect(f)
	eltern.add_child(s)
	f.call(start)

func _process(dt: float) -> void:
	_t += dt
	if bilder != "" and not _ereignis1 and _t > 1.8:
		_ereignis1 = true
		fenster.kopf.sprechen_text("Willkommen in unserer Stadt")
	if bilder != "" and not _ereignis2 and _t > 4.2:
		_ereignis2 = true
		fenster.kopf.animiert = false
		fenster.kopf._form("eyeBlinkLeft", 1.0)
		fenster.kopf._form("eyeBlinkRight", 1.0)
	if bilder != "" and not _fotos.is_empty() and _t >= _fotos[0][0]:
		var f = _fotos.pop_front()
		await RenderingServer.frame_post_draw
		DirAccess.make_dir_recursive_absolute(bilder)
		get_viewport().get_texture().get_image().save_png(bilder.path_join(f[1] + ".png"))
		print("FOTO ", f[1], " FPS ", Engine.get_frames_per_second())
		if _fotos.is_empty():
			get_tree().quit()
