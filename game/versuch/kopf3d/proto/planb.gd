# Plan B: Kopf aus TRELLIS-Form + MPFB-Gesichtseinheiten. Start:
#   godot --path game res://versuch/kopf3d/proto/planb.tscn -- --bilder=<ordner> [--kopf=res://...glb]
extends Control

var bilder := ""
var kopfpfad := "res://versuch/kopf3d/proto/b/m2_kopf.glb"
var _t := 0.0
var _fw: KopfFenster
var _stufe := 0
var _kam := ""

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="): bilder = a.substr(9)
		if a.begins_with("--kopf="): kopfpfad = a.substr(7)
		if a.begins_with("--kamera="): _kam = a.substr(9)
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	_fw = KopfFenster.new(Vector2i(720, 800), Color(0.72, 0.74, 0.78), kopfpfad, true)
	_fw.position = Vector2(10, 10)
	add_child(_fw)
	if _kam != "":
		var t := _kam.split(",")
		_fw.kamera.look_at_from_position(Vector3(float(t[0]), float(t[1]), float(t[2])), Vector3(0, 1.62, 0))
	_fw.kopf.animiert = false
	_fw.kopf.blick_wechsel = false

func _process(dt: float) -> void:
	_t += dt
	if bilder == "":
		return
	var plan := [[1.5, "ruhe"], [2.0, "sprechen"], [2.6, "o"], [3.2, "laecheln"], [3.8, "m"], [4.4, "blinzeln"]]
	if _stufe < plan.size() and _t > plan[_stufe][0]:
		var n: String = plan[_stufe][1]
		match n:
			"sprechen": _fw.kopf.mund_haltung({"jawOpen": 0.9, "mouthLowerDownLeft": 0.35, "mouthLowerDownRight": 0.35})
			"o": _fw.kopf.mund_haltung({"jawOpen": 0.5, "mouthFunnel": 0.9})
			"laecheln": _fw.kopf.mund_haltung({"jawOpen": 0.2, "mouthSmileLeft": 1.0, "mouthSmileRight": 1.0})
			"m": _fw.kopf.mund_haltung({"mouthClose": 1.0, "mouthPressLeft": 0.6, "mouthPressRight": 0.6})
			"blinzeln":
				_fw.kopf.mund_haltung({})
				_fw.kopf._form("eyeBlinkLeft", 1.0); _fw.kopf._form("eyeBlinkRight", 1.0)
		_stufe += 1
		await get_tree().create_timer(0.5).timeout
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(bilder.path_join("planb_" + n + ".png"))
		print("FOTO ", n)
		if _stufe >= plan.size():
			get_tree().quit()
