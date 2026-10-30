# Versuch Charakter Phase 2/3: Basiskopf mit Reglern für alle Gene, Alter, Geschlecht und Mimik (Testszene, kein
# Spielcode). Die .glb liegen in --glb=<ordner> (Standard ~/werkzeuge/charakter/glb3, nicht im Repository).
# Bogenzellen kennen zusätzlich (Phase 3): "stoff": {"haut": {param: wert}}, "umgebung": {…}, "schluessel": {…},
# "vorher": true (Stoffe der Phase 2 am Phase-3-Kopf), "ultra": true (Haut mit Bildschirm-SSS).
#   tools/charakter/schau.sh <bilder> 1600x1000                          Regler (zum Ausprobieren mit Bildschirm)
#   tools/charakter/schau.sh <bilder> <BxH> --bogen=<a.json>,<b.json> [--regler_bild] [--messen] --ende
# bogen:  Zeilen × Spalten aus einer JSON-Beschreibung; jede Zelle = Kopf + Alter + Geschlecht + Gene + Mimik +
#         Ansicht. Werte kommen aus dem Bogen, dann Zeile, dann Spalte, dann Zelle (später gewinnt).
#         Ergebnis: <name>.png und je Zelle ein Bild.
# regler_bild: die Reglerseite mit einem Beispiel als Bild (regler.png)
# messen: in diesem Prozess nacheinander leere Bühne, Kopf still, Mimik bewegt, alle Formen bewegt, mit/ohne Normalen
#         der Formen, 8 Köpfe; je Lauf GPU-Zeit, Dreiecke, Zeichenaufrufe, Speicher, Ladezeit (messung.jsonl)
extends Control

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")
const Buehne := preload("res://versuch/charakter/charakter_buehne.gd")

var glb_ordner := OS.get_environment("HOME").path_join("werkzeuge/charakter/glb3")
var bilder := OS.get_environment("HOME").path_join("werkzeuge/charakter/bilder/phase3")
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
	DirAccess.make_dir_recursive_absolute(bilder)
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var hg := ColorRect.new()
	hg.color = Color(0.11, 0.1, 0.09)
	hg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(hg)
	if not DirAccess.dir_exists_absolute(glb_ordner):
		_meldung("Ordner mit den Köpfen fehlt: %s\nDie .glb entstehen lokal mit tools/charakter/kopf3.sh aus ICT- und MakeHuman-Daten,\ndie nicht im Repository liegen (siehe docs/charakter/phase3.md)." % glb_ordner)
		return
	var etwas := false
	if args.has("bogen"):
		for datei in String(args["bogen"]).split(","):
			await _bogen(datei)
		etwas = true
	if args.has("regler_bild"):
		await _regler(true)
		etwas = true
	if args.has("messen"):
		await _messen()
		etwas = true
	if not etwas:
		await _regler(false)
		return
	get_tree().quit()

static func _label() -> Label:
	return Label.new()

func _meldung(text: String) -> void:
	push_error(text)
	print("MELDUNG ", text)
	var l := _label()
	l.text = text
	l.position = Vector2(30, 30)
	l.add_theme_font_size_override("font_size", 22)
	add_child(l)
	if args.has("ende"):
		await get_tree().create_timer(2.0).timeout
		get_tree().quit(1)

func _datei(name: String) -> String:
	return name if name.begins_with("/") else glb_ordner.path_join(name)

func _buehne(kopf: String, groesse: Vector2i) -> Buehne:
	var schl := "%s|%s" % [kopf, groesse]
	if _buehnen.has(schl):
		return _buehnen[schl]
	var b := Buehne.new(groesse)
	add_child(b)
	b.kopf_laden(_datei(kopf))
	_buehnen[schl] = b
	return b

func _warte(n: int) -> void:
	for i in n:
		# ohne Bildschirm (Syntax- und Ablaufprobe) gibt es kein frame_post_draw
		if DisplayServer.get_name() == "headless":
			await get_tree().process_frame
		else:
			await RenderingServer.frame_post_draw

static func _mann(wert) -> float:
	if wert is String:
		return 0.0 if wert == "w" else 1.0
	return float(wert)

# Eine Zelle auf einen Kopf anwenden (Volk-Mittel, dann Gene, Alter, Geschlecht, Mimik)
static func zelle_setzen(k: Kopf, z: Dictionary) -> void:
	var gene := Kopf.mittelwerte(z.get("volk", "Mensch"))
	gene.merge(z.get("gene", {}), true)
	k.mimik_mit_alter = bool(z.get("mimik_mit_alter", true))
	k.mimik = z.get("mimik", {})
	k.aussehen_setzen(gene, float(z.get("alter", 35.0)), _mann(z.get("geschlecht", "m")))

# ---------------------------------------------------------------- Bogen
func _bogen(datei: String) -> void:
	var text := FileAccess.get_file_as_string(datei)
	var plan: Dictionary = JSON.parse_string(text) if text != "" else {}
	if plan.is_empty():
		_meldung("Bogen fehlt oder ist kein JSON: " + datei)
		return
	var zelle := Vector2i(plan.get("zelle", [400, 440])[0], plan.get("zelle", [400, 440])[1])
	var spalten: Array = plan.get("spalten", [{}])
	var zeilen: Array = plan.get("zeilen", [{}])
	var name: String = plan.get("name", "bogen")
	var ueberall: Dictionary = plan.get("alle", {})
	DirAccess.make_dir_recursive_absolute(bilder.path_join(name))
	Kopf.poren()
	await get_tree().create_timer(1.5).timeout
	var fotos := []
	for zi in zeilen.size():
		var reihe := []
		for si in spalten.size():
			var z: Dictionary = ueberall.duplicate(true)
			z.merge(zeilen[zi], true)
			z.merge(spalten[si], true)
			var einzeln: Array = zeilen[zi].get("zellen", [])
			if si < einzeln.size():
				z.merge(einzeln[si], true)
			# Mimik von Zeile und Spalte zusammenführen (nicht ersetzen)
			var mim: Dictionary = {}
			for quelle in [ueberall, zeilen[zi], spalten[si]]:
				mim.merge(quelle.get("mimik", {}), true)
			z["mimik"] = mim
			if z.get("kopf") == null:
				reihe.append([null, z.get("titel", "")])
				continue
			var b := _buehne(z["kopf"], zelle)
			if b.kopf.fehler != "":
				reihe.append([null, b.kopf.fehler])
				continue
			zelle_setzen(b.kopf, z)
			if b.kopf.phase3:
				b.kopf.vorher_setzen(bool(z.get("vorher", false)))
				b.kopf.ultra_setzen(bool(z.get("ultra", false)))
			b.kopf.teile_ausblenden(z.get("ausblenden", []))
			b.kopf.stoff_parameter(z.get("stoff", {}))
			b.umgebung_setzen(z.get("umgebung", {}))
			b.licht_setzen(z.get("schluessel", {}))
			b.ansicht(z.get("ansicht", "portraet"), float(z.get("winkel", 0.0)), float(z.get("hoehe", 3.0)),
				float(z.get("spanne_m", 0.0)), float(z.get("ziel_m", 0.0)))
			await _warte(4)
			var bild := b.foto()
			if bild == null or bild.is_empty():
				bild = Image.create(zelle.x, zelle.y, false, Image.FORMAT_RGB8)
			bild.save_png(bilder.path_join(name).path_join("%02d_%02d.png" % [zi, si]))
			reihe.append([bild, z.get("titel", "")])
		fotos.append(reihe)
	for b in _buehnen.values():
		b.queue_free()
	_buehnen.clear()
	await _warte(2)
	# Bogen zusammensetzen: Überschrift, Spaltenköpfe, Zeilennamen links
	var tafel := Control.new()
	add_child(tafel)
	var hg := ColorRect.new()
	hg.color = Color(0.11, 0.1, 0.09)
	hg.set_anchors_preset(Control.PRESET_FULL_RECT)
	tafel.set_anchors_preset(Control.PRESET_FULL_RECT)
	tafel.add_child(hg)
	var rand := 150
	var kopfzeile := 34
	var titel := _label()
	titel.text = plan.get("titel", name)
	titel.position = Vector2(12, 6)
	titel.add_theme_font_size_override("font_size", 20)
	tafel.add_child(titel)
	for si in spalten.size():
		var l := _label()
		l.text = spalten[si].get("titel", "")
		l.position = Vector2(rand + si * (zelle.x + 6) + 6, kopfzeile)
		l.add_theme_font_size_override("font_size", 18)
		tafel.add_child(l)
	for zi in fotos.size():
		var y: int = kopfzeile + 30 + zi * (zelle.y + 26)
		var ln := _label()
		ln.text = zeilen[zi].get("titel", "")
		ln.position = Vector2(8, y + zelle.y * 0.45)
		ln.size = Vector2(rand - 12, 60)
		ln.autowrap_mode = TextServer.AUTOWRAP_WORD
		ln.add_theme_font_size_override("font_size", 16)
		tafel.add_child(ln)
		for si in fotos[zi].size():
			var eintrag: Array = fotos[zi][si]
			var x: int = rand + si * (zelle.x + 6)
			if eintrag[0] != null:
				var tr := TextureRect.new()
				tr.texture = ImageTexture.create_from_image(eintrag[0])
				tr.position = Vector2(x, y)
				tafel.add_child(tr)
			var lz := _label()
			lz.text = eintrag[1]
			lz.position = Vector2(x + 4, y + zelle.y + (0 if eintrag[0] != null else -zelle.y * 0.5))
			lz.size = Vector2(zelle.x - 8, 24)
			lz.autowrap_mode = TextServer.AUTOWRAP_WORD if eintrag[0] == null else TextServer.AUTOWRAP_OFF
			lz.add_theme_font_size_override("font_size", 14)
			tafel.add_child(lz)
	await _warte(3)
	var ges := Vector2i(rand + spalten.size() * (zelle.x + 6), kopfzeile + 30 + fotos.size() * (zelle.y + 26))
	var bild := get_viewport().get_texture().get_image()
	bild = bild.get_region(Rect2i(Vector2i.ZERO, ges.min(bild.get_size())))
	bild.save_png(bilder.path_join(name + ".png"))
	print("BOGEN ", bilder.path_join(name + ".png"), " ", ges)
	var fenster := Vector2i(get_viewport().get_visible_rect().size)
	if ges.x > fenster.x or ges.y > fenster.y:
		print("WARNUNG Fenster kleiner als der Bogen ", fenster, " < ", ges)
	tafel.queue_free()
	await _warte(2)

# ---------------------------------------------------------------- Regler
var _kopf: Kopf
var _buehne_r: Buehne
var _gene_regler := {}
var _alter_r: HSlider
var _mann_r: OptionButton
var _mimik_wahl: OptionButton
var _mimik_wert: HSlider
var _mimik_alter: CheckBox
var _ansicht_wahl: OptionButton
var _winkel_r: HSlider
var _wert_texte := {}

func _regler(nur_bild: bool) -> void:
	var fs := Vector2i(get_viewport().get_visible_rect().size)
	var breite_rechts := 470
	var c := SubViewportContainer.new()
	c.stretch = true
	c.position = Vector2.ZERO
	c.size = Vector2(fs.x - breite_rechts, fs.y)
	add_child(c)
	_buehne_r = Buehne.new(Vector2i(fs.x - breite_rechts, fs.y))
	c.add_child(_buehne_r)
	var datei := _datei(args.get("kopf", "kopf3.glb"))
	if not _buehne_r.kopf_laden(datei):
		_meldung(_buehne_r.kopf.fehler)
		return
	_kopf = _buehne_r.kopf
	var rolle := ScrollContainer.new()
	rolle.position = Vector2(fs.x - breite_rechts, 0)
	rolle.size = Vector2(breite_rechts, fs.y)
	add_child(rolle)
	var liste := VBoxContainer.new()
	liste.custom_minimum_size = Vector2(breite_rechts - 24, 0)
	rolle.add_child(liste)
	_ueberschrift(liste, "Basiskopf Phase 3 – %s" % datei.get_file())
	var zeile := HBoxContainer.new()
	liste.add_child(zeile)
	_mann_r = OptionButton.new()
	_mann_r.add_item("Mann")
	_mann_r.add_item("Frau")
	_mann_r.item_selected.connect(func(_i): _aendern())
	zeile.add_child(_mann_r)
	var zurueck := Button.new()
	zurueck.text = "Mensch-Mittel"
	zurueck.pressed.connect(_mittel)
	zeile.add_child(zurueck)
	_alter_r = _regler_zeile(liste, "Alter (Jahre)", 5, 70, 35, 1)
	_ueberschrift(liste, "Mimik (ARKit)")
	var mz := HBoxContainer.new()
	liste.add_child(mz)
	_mimik_wahl = OptionButton.new()
	_mimik_wahl.add_item("keine")
	for n in _kopf.info.get("arkit", []):
		_mimik_wahl.add_item(n)
	_mimik_wahl.item_selected.connect(func(_i): _aendern())
	mz.add_child(_mimik_wahl)
	var weiter := Button.new()
	weiter.text = "Nächste"
	weiter.pressed.connect(func():
		_mimik_wahl.select((_mimik_wahl.selected + 1) % _mimik_wahl.item_count)
		_aendern())
	mz.add_child(weiter)
	_mimik_wert = _regler_zeile(liste, "Stärke", 0.0, 1.0, 1.0, 0.05)
	_mimik_alter = CheckBox.new()
	_mimik_alter.text = "Mimik und Gene mit der Gesichtsgröße skalieren"
	_mimik_alter.button_pressed = true
	_mimik_alter.toggled.connect(func(_b): _aendern())
	liste.add_child(_mimik_alter)
	_ueberschrift(liste, "Ansicht")
	_ansicht_wahl = OptionButton.new()
	for a in ["portraet", "kopf", "augen", "mund", "buste"]:
		_ansicht_wahl.add_item(a)
	_ansicht_wahl.item_selected.connect(func(_i): _aendern())
	liste.add_child(_ansicht_wahl)
	_winkel_r = _regler_zeile(liste, "Drehung (Grad)", -90, 90, 20, 1)
	var arten := {"Makro": "Körper (Makros)", "Merkmal": "Gestalt", "Farbe": "Farben", "Zubehoer": "Zubehör (kommt mit Haar und Bart)"}
	var info_gene: Dictionary = _kopf.info.get("gene", {})
	for art in arten:
		_ueberschrift(liste, arten[art])
		for k in Kopf.erbgut():
			var g: Dictionary = Kopf.erbgut()[k]
			if g["art"] != art:
				continue
			var hinweis := ""
			var ie: Dictionary = info_gene.get(k, {})
			if ie.get("formen", {}).is_empty() and ie.get("art", "") not in ["makro", "farbe"]:
				hinweis = " –"
			_gene_regler[k] = _regler_zeile(liste, "%s%s" % [g["name"], hinweis], 0, 255, float(_kopf.gene.get(k, 128)), 1)
	_aendern()
	if nur_bild:
		_mann_r.select(0)
		_alter_r.value = 50
		_gene_regler["nase_hoecker"].value = 200
		_gene_regler["brauenwulst"].value = 170
		_gene_regler["haar_helligkeit"].value = 40
		_mimik_wahl.select(_mimik_index("mouthSmileLeft"))
		_mimik_wert.value = 0.8
		_aendern()
		await _warte(6)
		get_viewport().get_texture().get_image().save_png(bilder.path_join("regler.png"))
		print("REGLER ", bilder.path_join("regler.png"))
		c.queue_free()
		rolle.queue_free()
		await _warte(2)

func _mimik_index(name: String) -> int:
	for i in _mimik_wahl.item_count:
		if _mimik_wahl.get_item_text(i) == name:
			return i
	return 0

func _ueberschrift(liste: VBoxContainer, text: String) -> void:
	var l := _label()
	l.text = text
	l.add_theme_font_size_override("font_size", 17)
	l.add_theme_color_override("font_color", Color(0.95, 0.85, 0.6))
	liste.add_child(l)

func _regler_zeile(liste: VBoxContainer, text: String, von: float, bis: float, wert: float, schritt: float) -> HSlider:
	var z := HBoxContainer.new()
	liste.add_child(z)
	var l := _label()
	l.text = text
	l.custom_minimum_size = Vector2(200, 0)
	l.clip_text = true
	l.add_theme_font_size_override("font_size", 13)
	z.add_child(l)
	var r := HSlider.new()
	r.min_value = von
	r.max_value = bis
	r.step = schritt
	r.value = wert
	r.custom_minimum_size = Vector2(170, 18)
	r.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	z.add_child(r)
	var w := _label()
	w.custom_minimum_size = Vector2(40, 0)
	w.add_theme_font_size_override("font_size", 13)
	z.add_child(w)
	_wert_texte[r] = w
	r.value_changed.connect(func(_v): _aendern())
	return r

func _mittel() -> void:
	var m := Kopf.mittelwerte("Mensch")
	for k in _gene_regler:
		_gene_regler[k].set_value_no_signal(m[k])
	_aendern()

func _aendern() -> void:
	if _kopf == null:
		return
	for r in _wert_texte:
		_wert_texte[r].text = str(snappedf(r.value, 0.01))
	var gene := {}
	for k in _gene_regler:
		gene[k] = _gene_regler[k].value
	var mim := {}
	if _mimik_wahl.selected > 0:
		var n := _mimik_wahl.get_item_text(_mimik_wahl.selected)
		mim[n] = _mimik_wert.value
	_kopf.mimik_mit_alter = _mimik_alter.button_pressed
	_kopf.mimik = mim
	_kopf.aussehen_setzen(gene, _alter_r.value, 1.0 if _mann_r.selected == 0 else 0.0)
	_buehne_r.ansicht(_ansicht_wahl.get_item_text(_ansicht_wahl.selected), _winkel_r.value)

# ---------------------------------------------------------------- Messen
func _messen() -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var laeufe := [
		{"name": "leer", "kopf": "keiner"},
		{"name": "still", "kopf": "kopf3.glb", "bewegt": "keine"},
		{"name": "still_vorher", "kopf": "kopf3.glb", "bewegt": "keine", "vorher": true},
		{"name": "still_ultra", "kopf": "kopf3.glb", "bewegt": "keine", "ultra": true},
		{"name": "still_phase2", "kopf": OS.get_environment("HOME").path_join("werkzeuge/charakter/glb2/kopf2.glb"), "bewegt": "keine"},
		{"name": "mimik", "kopf": "kopf3.glb", "bewegt": "arkit"},
		{"name": "alle", "kopf": "kopf3.glb", "bewegt": "alle"},
		{"name": "alle_ohne_normalen", "kopf": "kopf3_ohne.glb", "bewegt": "alle"},
		{"name": "alle_mit_normalen", "kopf": "kopf3_mn.glb", "bewegt": "alle"},
		{"name": "x8_still", "kopf": "kopf3.glb", "bewegt": "keine", "anzahl": 8},
		{"name": "x8_mimik", "kopf": "kopf3.glb", "bewegt": "arkit", "anzahl": 8},
	]
	for l in laeufe:
		if args.has("nur") and not String(args["nur"]).split(",").has(l["name"]):
			continue
		await _ein_lauf(l)

func _ein_lauf(l: Dictionary) -> void:
	var datei := _datei(l.get("kopf", "kopf3.glb"))
	var anzahl: int = l.get("anzahl", 1)
	var gr0 := Vector2i(1280, 720)
	var spalten := 1 if anzahl == 1 else 4
	var zeilen := int(ceil(anzahl / float(spalten)))
	var gr := Vector2i(gr0.x / spalten, gr0.y / zeilen)
	await _warte(5)
	var leer := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED)
	var leer_puf := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED)
	var behaelter := Control.new()
	add_child(behaelter)
	var liste: Array[Buehne] = []
	var laden_ms := 0.0
	for i in anzahl:
		var c := SubViewportContainer.new()
		c.stretch = true
		c.position = Vector2((i % spalten) * gr.x, (i / spalten) * gr.y)
		c.size = Vector2(gr)
		behaelter.add_child(c)
		var b := Buehne.new(gr)
		c.add_child(b)
		RenderingServer.viewport_set_measure_render_time(b.get_viewport_rid(), true)
		liste.append(b)
		if l["kopf"] == "keiner":
			continue
		if not b.kopf_laden(datei):
			_meldung(b.kopf.fehler)
			behaelter.queue_free()
			return
		laden_ms += b.kopf.ladezeit_ms
		zelle_setzen(b.kopf, {"alter": 35, "geschlecht": "m"})
		if b.kopf.phase3:
			b.kopf.vorher_setzen(bool(l.get("vorher", false)))
			b.kopf.ultra_setzen(bool(l.get("ultra", false)))
		b.ansicht("portraet", 20.0)
	var namen: Array = []
	if liste[0].kopf != null:
		match l.get("bewegt", "keine"):
			"arkit": namen = liste[0].kopf.info.get("arkit", [])
			"alle": namen = liste[0].kopf.formen.keys()
	var anlauf := 2.5
	var dauer := float(args.get("sekunden", "6"))
	var t := 0.0
	var n_bilder := 0
	var gpu := 0.0
	var t_mess := 0.0
	while t < anlauf + dauer:
		var dt := get_process_delta_time()
		await get_tree().process_frame
		t += dt
		for b in liste:
			for i in namen.size():
				b.kopf.form(namen[i], 0.5 + 0.5 * sin(t * 3.0 + i * 0.7))
		if t > anlauf:
			n_bilder += 1
			t_mess += dt
			for b in liste:
				gpu += RenderingServer.viewport_get_measured_render_time_gpu(b.get_viewport_rid())
	var erg := {
		"name": l["name"], "kopf": datei.get_file() if l["kopf"] != "keiner" else "-",
		"glb_mb": snappedf(FileAccess.get_file_as_bytes(datei).size() / 1e6, 0.01) if l["kopf"] != "keiner" and FileAccess.file_exists(datei) else 0.0,
		"anzahl": anzahl, "formen": liste[0].kopf.formen.size() if liste[0].kopf else 0, "bewegt": namen.size(),
		"fps": snappedf(n_bilder / maxf(t_mess, 1e-6), 0.1),
		"gpu_ms": snappedf(gpu / maxf(n_bilder, 1), 0.01),
		"dreiecke_kopf": liste[0].kopf.dreiecke() if liste[0].kopf else 0,
		"dreiecke_bild": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
		"zeichenaufrufe": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
		"vram_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED) - leer) / 1048576.0, 0.1),
		"vram_puffer_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED) - leer_puf) / 1048576.0, 0.1),
		"laden_ms": snappedf(laden_ms / anzahl, 0.1),
		"bilder_ms": snappedf(liste[0].kopf.bilder_ms, 0.1) if liste[0].kopf else 0.0,
		"textur_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED) / 1048576.0, 0.1),
		"aufloesung": "%dx%d" % [gr0.x, gr0.y],
		"last": snappedf(float(FileAccess.get_file_as_string("/proc/loadavg").split(" ")[0]), 0.1),
	}
	print("MESSUNG ", JSON.stringify(erg))
	var f := FileAccess.open(bilder.path_join("messung.jsonl"), FileAccess.READ_WRITE if FileAccess.file_exists(bilder.path_join("messung.jsonl")) else FileAccess.WRITE)
	f.seek_end()
	f.store_line(JSON.stringify(erg))
	f.close()
	get_viewport().get_texture().get_image().save_png(bilder.path_join("messung_%s.png" % l["name"]))
	behaelter.queue_free()
	await _warte(4)
