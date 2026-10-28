# Versuch HG-Köpfe vorführen.
#   godot --path game res://versuch/hg/hg_schau.tscn -- --kopf=res://versuch/hg/koepfe/mann.glb[,res://…elb.glb] --modus=<m> --bilder=<ordner>
# Modi: nah    Nahaufnahmen Mund (Viseme), Augen, Seiten, Regler-Extreme als PNG
#       film   Sprechen, Blinzeln, Atmen, Blick (mit --write-movie <x.avi> --fixed-fps 30), endet selbst
#       tafel  alle angegebenen Köpfe nebeneinander (vorn, schräg, Seite) als ein PNG
#       regler Regler live bewegen (Schieber für alle Regler und Gesichtsformen)
#       mess   --anzahl=N Fenster, mittlere Bilder je Sekunde über 6 s
extends Control

var bilder := ""
var koepfe: PackedStringArray = []
var modus := "nah"
var anzahl := 1
var feste := {}          # --fest=elb:1,alter:0.5
var saetze := ["Gott zum Gruße, Meister! Was darf es heute sein?", "Das Tuch kommt aus Brügge, feinste Ware.", "Im Winter wird das Brot teuer, merkt Euch das."]
var _t := 0.0
var _fenster: Array[HgFenster] = []
var _schritt := 0
var _warte := 0.0
var _bilder_n := 0
var _stats: Control
var tressfx := ""          # --tressfx=res://versuch/hg/tfx : Frisur und Bart als TressFX-Strähnen
var tfx_einzeln := false   # --tfx-einzeln: nur Köpfe mit @tfx:1 bekommen TressFX (Vergleichstafel)

func _ready() -> void:
	seed(7)
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--bilder="): bilder = a.substr(9)
		elif a.begins_with("--kopf="): koepfe = a.substr(7).split(",")
		elif a.begins_with("--modus="): modus = a.substr(8)
		elif a.begins_with("--anzahl="): anzahl = int(a.substr(9))
		elif a.begins_with("--tressfx="): tressfx = a.substr(10)
		elif a == "--tfx-einzeln": tfx_einzeln = true
		elif a.begins_with("--fest="):
			for p in a.substr(7).split(","):
				var kv := p.split(":")
				feste[kv[0]] = float(kv[1])
	var bg := ColorRect.new()
	bg.color = Color(0.1, 0.09, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var fs := get_tree().root.size
	match modus:
		"nah":
			_neu(koepfe[0], Vector2i(640, 640), Vector2(10, 10))
		"film":
			var b := mini(fs.x - 30, 600)
			_neu(koepfe[0], Vector2i(b, fs.y - 30), Vector2((fs.x - b) / 2 - 8, 6))
		"tafel":
			var n := koepfe.size()
			var w := (fs.x - 20) / 3 - 16
			var h := (fs.y - 20) / n - 16
			for i in n:
				for j in 3:
					var f := _neu(koepfe[i], Vector2i(w, h), Vector2(10 + j * (w + 16), 10 + i * (h + 16)))
					f.winkel = [Vector2(0, 2), Vector2(32, 4), Vector2(88, 2)][j]
		"reihe":
			# je Kopf ein Fenster (schräg von vorn), drei Spalten – z. B. Hauttypen nebeneinander
			var n := koepfe.size()
			var sp := 3
			var w := (fs.x - 20) / sp - 14
			var h := (fs.y - 20) / int(ceil(n / float(sp))) - 14
			for i in n:
				var f := _neu(koepfe[i], Vector2i(w, h), Vector2(10 + (i % sp) * (w + 14), 10 + (i / sp) * (h + 14)))
				f.winkel = Vector2(22, 3)
		"regler":
			_neu(koepfe[0], Vector2i(560, fs.y - 30), Vector2(10, 6))
		"mess":
			HgFenster.sparsam = anzahl > 1
			var spalten := 4 if anzahl > 1 else 1
			var w := 520 if anzahl == 1 else (fs.x - 20) / spalten - 14
			var h := 600 if anzahl == 1 else (fs.y - 20) / int(ceil(anzahl / float(spalten))) - 14
			for i in anzahl:
				_neu(koepfe[i % koepfe.size()], Vector2i(w, h), Vector2(10 + (i % spalten) * (w + 14), 10 + (i / spalten) * (h + 14)))
	for f in _fenster:
		for k in feste:
			f.kopf.setze_fest(k, feste[k])
		# TressFX: alle Fenster (--tressfx=<ordner>) oder nur die mit „@tfx:1“ im Kopfpfad;
		# Strähnen je Kopf in <ordner>/<glb-name>/ (frisur.tfx, bart.tfx)
		if tressfx != "" and (not tfx_einzeln or f.get_meta("tfx", false)):
			f.ansicht.use_taa = true
			var kname := f.kopf.quelle.get_file().get_basename()
			HgTressFX.anbringen(f.kopf, tressfx.path_join(kname), Color(0.11, 0.07, 0.045))
	if tressfx != "" and modus == "mess":
		var cl := CanvasLayer.new()
		add_child(cl)
		if ClassDB.class_exists("TressFXStats"):
			var st: Control = ClassDB.instantiate("TressFXStats")
			st.position = Vector2(1200, 10)
			cl.add_child(st)
			_stats = st
	if modus == "regler":
		_regler_bauen()
	if modus in ["nah", "tafel", "reihe"]:
		for f in _fenster:
			f.kopf.animiert = false
			f.kopf.blick_wechsel = false

# Pfad mit eigenen Formwerten: res://…/mann.glb@elb:1;alter:0.5
func _neu(pfad: String, gr: Vector2i, pos: Vector2) -> HgFenster:
	var teile := pfad.split("@")
	var f := HgFenster.new(gr, teile[0])
	f.position = pos
	add_child(f)
	_fenster.append(f)
	if teile.size() > 1:
		for p in teile[1].split(";"):
			var kv := p.split(":")
			if kv[0] == "haut":
				f.kopf.setze_hauttyp(kv[1])
			elif kv[0] == "tfx":
				f.set_meta("tfx", kv[1] == "1")
			else:
				f.kopf.setze_fest(kv[0], float(kv[1]))
	return f

func _foto(n: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png(bilder.path_join(modus + "_" + n + ".png"))
	print("FOTO ", n)

func _process(dt: float) -> void:
	_t += dt
	match modus:
		"nah": _nah(dt)
		"film": _film()
		"tafel", "reihe":
			if _t > 2.0 and _schritt == 0:
				_schritt = 1
				for f in _fenster: f.kamera_setzen()
				await _foto("koepfe")
				get_tree().quit()
		"mess": _mess(dt)

# Nahaufnahmen: Kamera, Formen; je Schritt warten, bis die Formen angefahren sind
func _nah(dt: float) -> void:
	var f := _fenster[0]
	var k := f.kopf
	var plan := [
		["mund_ruhe", "mund", {}],
		["mund_a", "mund", HgKopf.VISEME["a"]],
		["mund_e", "mund", HgKopf.VISEME["e"]],
		["mund_i", "mund", HgKopf.VISEME["i"]],
		["mund_o", "mund", HgKopf.VISEME["o"]],
		["mund_u", "mund", HgKopf.VISEME["u"]],
		["mund_m", "mund", HgKopf.VISEME["m"]],
		["mund_f", "mund", HgKopf.VISEME["f"]],
		["mund_l", "mund", HgKopf.VISEME["l"]],
		["mund_weit", "mund", {"jawOpen": 1.0}],
		["mund_laecheln", "mund", {"mouthSmile_L": 1.0, "mouthSmile_R": 1.0}],
		["augen_offen", "augen", {}],
		["augen_zu", "augen", {"eyeBlink_L": 1.0, "eyeBlink_R": 1.0}],
		["augen_breit", "augen", {"eyeWide_L": 1.0, "eyeWide_R": 1.0, "browInnerUp": 1.0, "browOuterUp_L": 0.8, "browOuterUp_R": 0.8}],
		["augen_boese", "augen", {"browDown_L": 1.0, "browDown_R": 1.0, "eyeSquint_L": 0.6, "eyeSquint_R": 0.6, "noseSneer_L": 0.5, "noseSneer_R": 0.5}],
		["ganz_vorn", "vorn", {}],
		["ganz_laecheln", "schraeg", {"mouthSmile_L": 0.8, "mouthSmile_R": 0.8, "cheekSquint_L": 0.4, "cheekSquint_R": 0.4}],
		["seite_links", "links", {}],
		["seite_rechts", "rechts", {}],
		["hinten", "hinten", {}],
		["ohr", "ohr", {}],
		["haut_wange", "wange", {}],
		["oben", "oben", {}],
	]
	_warte -= dt
	if _warte > 0.0 or _t < 1.5:
		return
	if _schritt > 0:
		await _foto(plan[_schritt - 1][0])
	if _schritt >= plan.size():
		get_tree().quit()
		return
	var p: Array = plan[_schritt]
	match p[1]:
		"mund": f.abstand = 0.32; f.winkel = Vector2(14, -4); f.blick_ziel = _mitte(f) + Vector3(0, -0.075, 0.06)
		"augen": f.abstand = 0.3; f.winkel = Vector2(8, 3); f.blick_ziel = _mitte(f) + Vector3(0, 0.0, 0.06)
		"vorn": f.abstand = 0.95; f.winkel = Vector2(0, 2); f.blick_ziel = _mitte(f)
		"schraeg": f.abstand = 0.95; f.winkel = Vector2(30, 4); f.blick_ziel = _mitte(f)
		"links": f.abstand = 0.95; f.winkel = Vector2(90, 2); f.blick_ziel = _mitte(f)
		"rechts": f.abstand = 0.95; f.winkel = Vector2(-90, 2); f.blick_ziel = _mitte(f)
		"hinten": f.abstand = 0.95; f.winkel = Vector2(160, 8); f.blick_ziel = _mitte(f)
		"oben": f.abstand = 0.95; f.winkel = Vector2(20, 35); f.blick_ziel = _mitte(f)
		"ohr": f.abstand = 0.38; f.winkel = Vector2(70, 8); f.blick_ziel = _mitte(f) + Vector3(0.075, 0.0, -0.045)
		"wange": f.abstand = 0.22; f.winkel = Vector2(25, 2); f.blick_ziel = _mitte(f) + Vector3(0.035, -0.03, 0.05)
	f.kamera_setzen()
	k.setze_ausdruck(p[2])
	_schritt += 1
	_warte = 0.6

func _mitte(f: HgFenster) -> Vector3:
	var sk := f.kopf.skelett
	return sk.global_transform * sk.get_bone_global_rest(sk.find_bone("head")).origin + Vector3(0, 0.075, 0.03)

# Film: ruhig schauen, drei Sätze, dazwischen Lächeln, Kamera wandert langsam
func _film() -> void:
	var f := _fenster[0]
	var k := f.kopf
	f.winkel = Vector2(18.0 * sin(_t * 0.4), 3.0)
	f.abstand = 1.3 - 0.4 * smoothstep(5.5, 7.5, _t)
	f.kamera_setzen()
	var plan := [[0.8, "satz", 0], [4.6, "laecheln", 0], [5.6, "neutral", 0], [6.0, "satz", 1], [10.0, "laecheln", 0], [11.6, "ende", 0]]
	while _schritt < plan.size() and _t >= plan[_schritt][0]:
		var p: Array = plan[_schritt]
		match p[1]:
			"satz": k.sprich(saetze[p[2]])
			"laecheln": k.setze_ausdruck({"mouthSmile_L": 0.7, "mouthSmile_R": 0.7, "cheekSquint_L": 0.3, "cheekSquint_R": 0.3})
			"neutral": k.setze_ausdruck({})
			"ende": get_tree().quit()
		_schritt += 1

func _mess(dt: float) -> void:
	if _t < 3.0:
		return
	_bilder_n += 1
	if _t > 9.0:
		print("MESS ", anzahl, " Köpfe: ", snappedf(_bilder_n / (_t - 3.0), 0.1), " Bilder/s")
		if _stats and _stats.has_method("get"):
			print("TRESSFX ", _stats.get("text"))
		if bilder != "":
			await _foto("mess_%d" % anzahl)
		get_tree().quit()
	for f in _fenster:
		if _bilder_n == 1:
			f.kopf.sprich(saetze[0] + " " + saetze[1] + " " + saetze[2])

# Live-Regler: Schieber rechts, je Regler und je Gesichtsform
func _regler_bauen() -> void:
	var k := _fenster[0].kopf
	var sc := ScrollContainer.new()
	sc.position = Vector2(600, 10)
	sc.size = Vector2(get_tree().root.size.x - 620, get_tree().root.size.y - 20)
	add_child(sc)
	var box := VBoxContainer.new()
	box.custom_minimum_size.x = sc.size.x - 20
	sc.add_child(box)
	var namen: Array = k.formen()
	namen.sort()
	for n in namen:
		var zeile := HBoxContainer.new()
		var l := Label.new(); l.text = n; l.custom_minimum_size.x = 180
		var s := HSlider.new(); s.min_value = -1.0; s.max_value = 1.0; s.step = 0.01; s.custom_minimum_size.x = 260
		s.value = feste.get(n, 0.0)
		s.value_changed.connect(func(v): k.setze_fest(n, v))
		zeile.add_child(l); zeile.add_child(s)
		box.add_child(zeile)
	var knopf := Button.new(); knopf.text = "Sprechen"
	knopf.pressed.connect(func(): k.sprich(saetze[randi() % saetze.size()]))
	box.add_child(knopf)
