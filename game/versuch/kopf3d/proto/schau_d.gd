# Plan D vorführen: Nahaufnahmen (Mund-Zustände, Augen) oder kurzer Film (Ruhe, Blinzeln, ein Satz, Lächeln).
#   godot --path game res://versuch/kopf3d/proto/schau_d.tscn -- --kopf=res://...glb --bilder=<ordner> --modus=nah|film|tafel
#   Film: zusätzlich --write-movie <datei.avi> --fixed-fps 30 an Godot geben.
extends Control

var bilder := ""
var kopfpfad := ""
var modus := "nah"
var satz := "Gott zum Gruße, Meister! Was darf es heute sein?"
var _t := 0.0
var _fw: KopfFenster
var _k: KopfD
var _schritt := 0
var _warte := 0.0

# Mund ungefähr bei (0; 1,578; 0,16), Augen bei (±0,033; 1,64; 0,15) (Godot-Raum)
const NAH_MUND := [Vector3(0.07, 1.585, 0.43), Vector3(0.0, 1.578, 0.15)]
const NAH_AUGEN := [Vector3(0.05, 1.645, 0.43), Vector3(0.0, 1.64, 0.14)]

func _ready() -> void:
	seed(7)
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="): bilder = a.substr(9)
		elif a.begins_with("--kopf="): kopfpfad = a.substr(7)
		elif a.begins_with("--modus="): modus = a.substr(8)
		elif a.begins_with("--satz="): satz = a.substr(7)
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var fs := get_tree().root.size
	var gr := Vector2i(560, 560) if modus == "nah" else Vector2i(mini(fs.x - 40, 780), fs.y - 40)
	_fw = KopfFenster.new(gr, Color(0.72, 0.74, 0.78), kopfpfad, true)
	_fw.position = Vector2(10, 10) if modus == "nah" else Vector2((fs.x - gr.x) / 2 - 10, 10)
	add_child(_fw)
	_k = _fw.kopf as KopfD
	if modus == "film":
		_fw.kamera_ziel = Vector3(0.3, 1.63, 0.68)
	if modus == "nah":
		_k.animiert = false
		_k.blick_wechsel = false

func _kamera(k: Array) -> void:
	_fw.kamera.look_at_from_position(k[0], k[1])

func _foto(n: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(bilder.path_join(modus + "_" + n + ".png"))
	print("FOTO ", n)

func _process(dt: float) -> void:
	_t += dt
	if modus == "nah":
		_nah(dt)
	else:
		_film()

func _nah(dt: float) -> void:
	var plan := [
		["mund_ruhe", NAH_MUND, {}, 0.0],
		["mund_a", NAH_MUND, {"kiefer": 0.55, "lippen_auf": 0.9}, 0.0],
		["mund_e", NAH_MUND, {"kiefer": 0.3, "lippen_auf": 0.55, "mund_breit": 0.55}, 0.0],
		["mund_o", NAH_MUND, {"kiefer": 0.4, "lippen_auf": 0.5, "mund_rund": 0.7}, 0.0],
		["mund_m", NAH_MUND, {"mund_press": 1.0}, 0.0],
		["mund_f", NAH_MUND, {"kiefer": 0.08, "lippe_f": 1.0}, 0.0],
		["mund_laecheln", NAH_MUND, {"laecheln_l": 1.0, "laecheln_r": 1.0}, 0.0],
		["augen_offen", NAH_AUGEN, {}, 0.0],
		["augen_halb", NAH_AUGEN, {}, 0.5],
		["augen_zu", NAH_AUGEN, {}, 1.0],
		["augen_laecheln", NAH_AUGEN, {"laecheln_l": 1.0, "laecheln_r": 1.0}, 0.0],
		["ganz_laecheln", [Vector3(0.4, 1.64, 1.0), Vector3(0, 1.595, 0)], {"laecheln_l": 1.0, "laecheln_r": 1.0}, 0.0],
		["ganz_ruhe", [Vector3(0.4, 1.64, 1.0), Vector3(0, 1.595, 0)], {}, 0.0],
	]
	_warte -= dt
	if _warte > 0.0 or _t < 1.0:
		return
	if _schritt >= plan.size() * 2:
		get_tree().quit()
		return
	var p: Array = plan[_schritt / 2]
	if _schritt % 2 == 0:
		_kamera(p[1])
		_k.mund_haltung(p[2])
		_k._form("eyeBlinkLeft", p[3])
		_warte = 0.6
	else:
		_foto(p[0])
		_warte = 0.1
	_schritt += 1

func _film() -> void:
	# 0-1,5 s Ruhe, 1,6 s Blinzeln, 2,3 s Satz, danach Lächeln, 8,6 s Blinzeln, 10 s Ende
	match _schritt:
		0:
			if _t > 1.6: _k.blinzeln(); _schritt += 1
		1:
			if _t > 2.3: _k.sprechen_text(satz); _schritt += 1
		2:
			if not _k.spricht() and _t > 3.0: _schritt += 1; _warte = _t
		3:
			if _t > _warte + 0.6: _k.ausdruck = {"laecheln": 0.85}; _schritt += 1
		4:
			if _t > _warte + 2.6: _k.blinzeln(); _schritt += 1
		5:
			if _t > _warte + 3.6: get_tree().quit()
