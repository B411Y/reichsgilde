# Versuch Charakter Phase 0: Köpfe vergleichen und messen (Testszene, kein Spielcode).
#   tools/charakter/schau.sh <bildordner> <BxH> --modus=bogen --bogen=<datei.json>
#   tools/charakter/schau.sh <bildordner> 1280x720 --modus=messen --kopf=<datei.glb> [--stoff=haut] [--animiert=-1]
#       [--anzahl=1] [--sekunden=8] [--name=<kennung>] [--unkomprimiert]
# Die .glb liegen in --glb=<ordner> (Standard ~/werkzeuge/charakter/glb, nicht im Repository).
# bogen:  Zeilen × Spalten aus einer JSON-Beschreibung; jede Zelle = Kopf + Stoff + Formen + Ansicht.
#         Werte kommen aus Zeile, dann Spalte, dann Zelle (später gewinnt). Ergebnis: <name>.png und je Zelle ein Bild.
# messen: Bilder je Sekunde, GPU-Zeit, Dreiecke, Zeichenaufrufe, Grafikspeicher, Ladezeit; alle (oder n) Formen bewegt.
extends Control

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")
const Buehne := preload("res://versuch/charakter/charakter_buehne.gd")

var glb_ordner := OS.get_environment("HOME").path_join("werkzeuge/charakter/glb")
var bilder := OS.get_environment("HOME").path_join("werkzeuge/charakter/bilder")
var modus := "bogen"
var args := {}
var _buehnen := {}

func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--") and a.contains("="):
			var kv := a.substr(2).split("=", true, 1)
			args[kv[0]] = kv[1]
		elif a.begins_with("--"):
			args[a.substr(2)] = "1"
	glb_ordner = args.get("glb", glb_ordner)
	bilder = args.get("bilder", bilder)
	modus = args.get("modus", modus)
	DirAccess.make_dir_recursive_absolute(bilder)
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var hg := ColorRect.new()
	hg.color = Color(0.11, 0.1, 0.09)
	hg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(hg)
	if not DirAccess.dir_exists_absolute(glb_ordner):
		_meldung("Ordner mit den Köpfen fehlt: %s\nDie .glb entstehen lokal mit tools/charakter/alle_koepfe.sh aus MPFB- und ICT-Daten,\ndie nicht im Repository liegen (siehe docs/charakter/phase0.md)." % glb_ordner)
		return
	match modus:
		"bogen": _bogen()
		"messen": _messen()
		_: _meldung("Unbekannter Modus: " + modus)

func _meldung(text: String) -> void:
	push_error(text)
	print("MELDUNG ", text)
	var l := Label.new()
	l.text = text
	l.position = Vector2(30, 30)
	l.add_theme_font_size_override("font_size", 22)
	add_child(l)
	if args.has("ende"):
		await get_tree().create_timer(2.0).timeout
		get_tree().quit(1)

func _datei(name: String) -> String:
	return name if name.begins_with("/") else glb_ordner.path_join(name)

func _buehne(kopf: String, stoff: String, groesse: Vector2i) -> Buehne:
	var schl := "%s|%s|%s" % [kopf, stoff, groesse]
	if _buehnen.has(schl):
		return _buehnen[schl]
	var b := Buehne.new(groesse)
	add_child(b)
	b.kopf_laden(_datei(kopf), stoff)
	_buehnen[schl] = b
	return b

func _warte(n: int) -> void:
	for i in n:
		await RenderingServer.frame_post_draw

# ---------------------------------------------------------------- Bogen
func _bogen() -> void:
	var text := FileAccess.get_file_as_string(args.get("bogen", ""))
	var plan: Dictionary = JSON.parse_string(text) if text != "" else {}
	if plan.is_empty():
		_meldung("Bogen fehlt oder ist kein JSON: " + args.get("bogen", "(kein --bogen=)"))
		return
	var zelle := Vector2i(plan.get("zelle", [480, 480])[0], plan.get("zelle", [480, 480])[1])
	var spalten: Array = plan.get("spalten", [{}])
	var zeilen: Array = plan.get("zeilen", [{}])
	var name: String = plan.get("name", "bogen")
	DirAccess.make_dir_recursive_absolute(bilder.path_join(name))
	# Poren-Rauschen wird im Hintergrund erzeugt: kurz warten
	Kopf.poren()
	await get_tree().create_timer(1.5).timeout
	var fotos := []
	for zi in zeilen.size():
		var reihe := []
		for si in spalten.size():
			var z: Dictionary = {}
			z.merge(zeilen[zi], true)
			z.merge(spalten[si], true)
			var einzeln: Array = zeilen[zi].get("zellen", [])
			if si < einzeln.size():
				z.merge(einzeln[si], true)
			if z.get("kopf") == null:
				reihe.append([null, z.get("titel", "")])
				continue
			var b := _buehne(z["kopf"], z.get("stoff", "haut"), zelle)
			if b.kopf.fehler != "":
				reihe.append([null, b.kopf.fehler])
				continue
			if z.has("toenung"):
				for m in b.kopf.haut_stoffe:
					m.set_shader_parameter("toenung", Vector3(z["toenung"][0], z["toenung"][1], z["toenung"][2]))
			b.kopf.formen_setzen(z.get("formen", {}))
			b.kopf.teile_ausblenden(z.get("ausblenden", []))
			b.ansicht(z.get("ansicht", "buste"), float(z.get("winkel", 0.0)), float(z.get("hoehe", 3.0)),
				float(z.get("spanne_m", 0.0)), float(z.get("ziel_m", 0.0)))
			await _warte(4)
			var bild := b.foto()
			bild.save_png(bilder.path_join(name).path_join("%02d_%02d.png" % [zi, si]))
			reihe.append([bild, z.get("titel", "")])
		fotos.append(reihe)
	for b in _buehnen.values():
		b.queue_free()
	_buehnen.clear()
	# Bogen zusammensetzen: Überschrift, Spaltenköpfe, Zeilennamen links
	var rand := 150
	var kopfzeile := 34
	var titel := Label.new()
	titel.text = plan.get("titel", name)
	titel.position = Vector2(12, 6)
	titel.add_theme_font_size_override("font_size", 20)
	add_child(titel)
	for si in spalten.size():
		var l := Label.new()
		l.text = spalten[si].get("titel", "")
		l.position = Vector2(rand + si * (zelle.x + 6) + 6, kopfzeile)
		l.add_theme_font_size_override("font_size", 18)
		add_child(l)
	for zi in fotos.size():
		var y: int = kopfzeile + 30 + zi * (zelle.y + 26)
		var ln := Label.new()
		ln.text = zeilen[zi].get("titel", "")
		ln.position = Vector2(8, y + zelle.y * 0.45)
		ln.size = Vector2(rand - 12, 60)
		ln.autowrap_mode = TextServer.AUTOWRAP_WORD
		ln.add_theme_font_size_override("font_size", 16)
		add_child(ln)
		for si in fotos[zi].size():
			var eintrag: Array = fotos[zi][si]
			var x: int = rand + si * (zelle.x + 6)
			if eintrag[0] != null:
				var tr := TextureRect.new()
				tr.texture = ImageTexture.create_from_image(eintrag[0])
				tr.position = Vector2(x, y)
				add_child(tr)
			var lz := Label.new()
			lz.text = eintrag[1]
			lz.position = Vector2(x + 4, y + zelle.y + (0 if eintrag[0] != null else -zelle.y * 0.5))
			lz.size = Vector2(zelle.x - 8, 24)
			lz.autowrap_mode = TextServer.AUTOWRAP_WORD if eintrag[0] == null else TextServer.AUTOWRAP_OFF
			lz.add_theme_font_size_override("font_size", 14)
			add_child(lz)
	await _warte(3)
	var ges := Vector2i(rand + spalten.size() * (zelle.x + 6), kopfzeile + 30 + fotos.size() * (zelle.y + 26))
	var bild := get_viewport().get_texture().get_image()
	bild = bild.get_region(Rect2i(Vector2i.ZERO, ges.min(bild.get_size())))
	bild.save_png(bilder.path_join(name + ".png"))
	print("BOGEN ", bilder.path_join(name + ".png"), " ", ges)
	var fenster := Vector2i(get_viewport().get_visible_rect().size)
	if ges.x > fenster.x or ges.y > fenster.y:
		print("WARNUNG Fenster kleiner als der Bogen ", fenster, " < ", ges)
	get_tree().quit()

# ---------------------------------------------------------------- Messen
func _messen() -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var datei := _datei(args.get("kopf", "mann35_a2.glb"))
	var anzahl := int(args.get("anzahl", "1"))
	var fs := Vector2i(get_viewport().get_visible_rect().size)
	var spalten := 1 if anzahl == 1 else 4
	var zeilen := int(ceil(anzahl / float(spalten)))
	var gr := Vector2i(fs.x / spalten, fs.y / zeilen)
	var leer := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED)
	var leer_tex := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED)
	var leer_puf := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED)
	var liste: Array[Buehne] = []
	var laden_ms := 0.0
	var komp_ms := 0.0
	for i in anzahl:
		var c := SubViewportContainer.new()
		c.stretch = true
		c.position = Vector2((i % spalten) * gr.x, (i / spalten) * gr.y)
		c.size = Vector2(gr)
		add_child(c)
		var b := Buehne.new(gr)
		c.add_child(b)
		var k := Kopf.new()
		k.komprimieren = not args.has("unkomprimiert")
		b.kopf = k
		b.add_child(k)
		if args.get("kopf", "") == "keiner":
			# Grundlinie: gleiche Bühne (Licht, Schatten, Ansicht) ohne Kopf
			liste.append(b)
			RenderingServer.viewport_set_measure_render_time(b.get_viewport_rid(), true)
			continue
		if not k.laden(datei, args.get("stoff", "haut")):
			_meldung(k.fehler)
			return
		laden_ms += k.ladezeit_ms
		komp_ms += k.komprimier_ms
		b.ansicht("buste", 20.0)
		RenderingServer.viewport_set_measure_render_time(b.get_viewport_rid(), true)
		liste.append(b)
	var namen: Array = liste[0].kopf.formen.keys()
	namen.sort()
	var bewegt := int(args.get("animiert", "-1"))
	if bewegt < 0 or bewegt > namen.size():
		bewegt = namen.size()
	var anlauf := 2.5
	var dauer := float(args.get("sekunden", "8"))
	var t := 0.0
	var bilder_n := 0
	var gpu := 0.0
	var cpu := 0.0
	var t_mess := 0.0
	var schlimmste := 0.0
	while t < anlauf + dauer:
		var dt := get_process_delta_time()
		await get_tree().process_frame
		t += dt
		for b in liste:
			for i in bewegt:
				b.kopf.form(namen[i], 0.5 + 0.5 * sin(t * 3.0 + i * 0.7))
		if t > anlauf:
			bilder_n += 1
			t_mess += dt
			schlimmste = maxf(schlimmste, dt)
			for b in liste:
				gpu += RenderingServer.viewport_get_measured_render_time_gpu(b.get_viewport_rid())
				cpu += RenderingServer.viewport_get_measured_render_time_cpu(b.get_viewport_rid())
	var erg := {
		"name": args.get("name", datei.get_file()),
		"kopf": datei.get_file(),
		"glb_mb": snappedf(FileAccess.get_file_as_bytes(datei).size() / 1e6, 0.01) if FileAccess.file_exists(datei) else 0.0,
		"anzahl": anzahl,
		"formen": namen.size(),
		"bewegt": bewegt,
		"fps": snappedf(bilder_n / maxf(t_mess, 1e-6), 0.1),
		"bild_ms": snappedf(t_mess * 1000.0 / maxf(bilder_n, 1), 0.01),
		"schlimmstes_bild_ms": snappedf(schlimmste * 1000.0, 0.1),
		"gpu_ms": snappedf(gpu / maxf(bilder_n, 1), 0.01),
		"cpu_render_ms": snappedf(cpu / maxf(bilder_n, 1), 0.01),
		"dreiecke_szene": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
		"dreiecke_kopf": liste[0].kopf.dreiecke(),
		"zeichenaufrufe": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
		"vram_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED) - leer) / 1048576.0, 0.1),
		"vram_texturen_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED) - leer_tex) / 1048576.0, 0.1),
		"vram_puffer_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED) - leer_puf) / 1048576.0, 0.1),
		"vram_gesamt_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED) / 1048576.0, 0.1),
		"laden_ms": snappedf(laden_ms / anzahl, 0.1),
		"komprimieren_ms": snappedf(komp_ms / anzahl, 0.1),
		"aufloesung": "%dx%d" % [fs.x, fs.y],
		"komprimiert": not args.has("unkomprimiert"),
	}
	print("MESSUNG ", JSON.stringify(erg))
	var f := FileAccess.open(bilder.path_join("messung_%s.json" % erg["name"]), FileAccess.WRITE)
	f.store_string(JSON.stringify(erg, " "))
	f.close()
	get_viewport().get_texture().get_image().save_png(bilder.path_join("messung_%s.png" % erg["name"]))
	get_tree().quit()
