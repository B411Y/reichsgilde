# Versuch 3D-Köpfe: Kopf laden, Kiefer-Formregler im Spiel verändern, Blinzeln per Shape Key,
# Bildschirmfotos und Grafikspeicher. Start: godot --path game res://versuch/kopf3d/kopf3d.tscn -- --bilder=<ordner>
extends Node3D

var netze: Array[MeshInstance3D] = []
var zeit := 0.0
var bilder := ""
var fotos := [[1.5, "kiefer_mitte"], [3.2, "kiefer_breit"], [5.0, "kiefer_schmal"], [6.05, "blinzeln"]]
var ohne_kopf := {}

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="):
			bilder = a.substr(9)
	# Grafikspeicher vor dem Kopf messen
	await get_tree().process_frame
	await get_tree().process_frame
	ohne_kopf = _speicher()
	var szene: PackedScene = load("res://versuch/kopf3d/kopf.glb")
	var kopf := szene.instantiate()
	add_child(kopf)
	_sammeln(kopf)
	for m in netze:
		print("NETZ ", m.name, " Formregler: ", _formregler(m))
	await get_tree().process_frame
	await get_tree().process_frame
	var mit := _speicher()
	print("SPEICHER ohne Kopf ", ohne_kopf)
	print("SPEICHER mit Kopf  ", mit)
	print("SPEICHER Kopf: Texturen %.1f MB, Puffer %.1f MB, Dreiecke im Bild %d" % [
		(mit.textur - ohne_kopf.textur) / 1048576.0, (mit.puffer - ohne_kopf.puffer) / 1048576.0,
		mit.dreiecke - ohne_kopf.dreiecke])

func _sammeln(n: Node) -> void:
	if n is MeshInstance3D:
		netze.append(n)
	for k in n.get_children():
		_sammeln(k)

func _formregler(m: MeshInstance3D) -> Array:
	var namen := []
	if m.mesh is ArrayMesh:
		for i in m.mesh.get_blend_shape_count():
			namen.append(m.mesh.get_blend_shape_name(i))
	return namen

func _speicher() -> Dictionary:
	return {
		"textur": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED),
		"puffer": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED),
		"dreiecke": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
	}

# Formregler auf allen Teilnetzen setzen (Kopf, Brauen, Wimpern tragen dieselben Namen)
func _setzen(name: String, wert: float) -> void:
	for m in netze:
		var i := m.find_blend_shape_by_name(name)
		if i >= 0:
			m.set_blend_shape_value(i, wert)

func _process(delta: float) -> void:
	if netze.is_empty():
		return
	zeit += delta
	# Kiefer: 0 -> breit -> schmal, wie ein Regler in einer Spielfigur-Erstellung
	var k := 0.0
	if zeit > 2.0 and zeit < 3.8:
		k = clampf((zeit - 2.0) / 1.0, 0.0, 1.0)
	elif zeit >= 3.8:
		k = -clampf((zeit - 3.8) / 1.0, 0.0, 1.0)
	_setzen("kiefer_breit", maxf(k, 0.0))
	_setzen("kiefer_schmal", maxf(-k, 0.0))
	# Blinzeln: 150 ms zu, wieder auf (bei 6,0 s)
	var b := 0.0
	var t := fmod(zeit, 3.0)
	if zeit > 5.5:
		b = clampf(1.0 - absf(t - 0.05) / 0.075, 0.0, 1.0)
	_setzen("eyeBlinkLeft", b)
	_setzen("eyeBlinkRight", b)
	if bilder != "" and not fotos.is_empty() and zeit >= fotos[0][0]:
		var f = fotos.pop_front()
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(bilder.path_join(f[1] + ".png"))
		print("FOTO ", f[1], " bei ", zeit, " s")
	if bilder != "" and fotos.is_empty() and zeit > 6.5:
		print("FPS ", Engine.get_frames_per_second())
		get_tree().quit()
