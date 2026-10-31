# Versuch Charakter Phase 2/3: Basiskopf mit Reglern für alle Gene, Alter, Geschlecht und Mimik (Testszene, kein
# Spielcode). Die .glb liegen in --glb=<ordner> (Standard ~/werkzeuge/charakter/glb3, nicht im Repository).
# Bogenzellen kennen zusätzlich (Phase 3): "stoff": {"haut": {param: wert}}, "umgebung": {…}, "schluessel": {…},
# "vorher": true (Stoffe der Phase 2 am Phase-3-Kopf), "ultra": true (Haut mit Bildschirm-SSS).
# Phase 4: "haar": {"modus": "karten"|"tressfx"|"aus", "vorgabe": {"frisur": Stil, "bart": Stil}, "ruhe": true},
# "warten": Bilder bis zum Foto (TressFX braucht etwa 40, bis die Strähnen liegen).
#   tools/charakter/schau.sh <bilder> 1600x1000                          Regler (zum Ausprobieren mit Bildschirm)
#   tools/charakter/schau.sh <bilder> <BxH> --bogen=<a.json>,<b.json> [--regler_bild] [--messen] --ende
# bogen:  Zeilen × Spalten aus einer JSON-Beschreibung; jede Zelle = Kopf + Alter + Geschlecht + Gene + Mimik +
#         Ansicht. Werte kommen aus dem Bogen, dann Zeile, dann Spalte, dann Zelle (später gewinnt).
#         Ergebnis: <name>.png und je Zelle ein Bild.
# regler_bild: die Reglerseite mit einem Beispiel als Bild (regler.png)
# messen: in diesem Prozess nacheinander leere Bühne, Kopf still, Mimik bewegt, alle Formen bewegt, mit/ohne Normalen
#         der Formen, 8 Köpfe; je Lauf GPU-Zeit, Dreiecke, Zeichenaufrufe, Speicher, Ladezeit (messung.jsonl)
#         Phase 6: --wiederholungen=3 (je Lauf mehrmals im selben Prozess), --stand=5|6, --commit=<hash> (ins Protokoll)
#         Halt B2: --varianten=name:glb-ordner:weg_a:malfilter,… (gepaart wie --staende, Stand 6), --stufe=ultra
# Phase 6 (S0) – Bogenzellen kennen zusätzlich: "stand": 5|6, "saat": Startwert (Erbgut über den Kern gewürfelt,
#   ErbgutBruecke.cs), "familie": {"<saat>": [vater_saat, mutter_saat]} (vererbt), "saat_arten": ["Merkmal", …] (nur
#   diese Gene aus der Saat), "vorlage": Erbgut-Vorschlagsdatei, "licht": "spiel"|"vorlage"|"innen", "anzeige":
#   "albedo", "ziel_x", "kamera": "ortho", "schluessel_richtung": [azimut, hoehe], "pruefkoerper": "graukarte"|
#   "hautfeld"|"farbtest"|"kugel"|"alle". Jede Zelle schreibt neben ihr Bild eine Begleitdatei <zz_ss>.json (Kamera,
#   Kopf, Gene, Formgewichte aus der Szene) für tools/charakter/messen.
# Phase 6 (S2/S3): "kopf": "standard" wählt je Stand – Stand 6 kopf6.glb (im --glb-Ordner, gewöhnlich glb6), Stand 5
#   kopf5.glb (im --glb-Ordner, sonst in --glb5=<ordner>, Standard ~/werkzeuge/charakter/glb5); ein Kopfname, der im
#   --glb-Ordner fehlt, wird ebenso in glb5 gesucht. Prüfkörper zusätzlich "kugel6_glanz"|"kugel6_streu", Licht
#   "spiel5"|"spiel6"|"spiel6_weich"|"kugeltest" ("spiel" = nach dem Stand des Kopfs).
#   --messen --staende=5,6: jeden Lauf je Wiederholung für beide Stände nacheinander im selben Prozess (gepaart);
#   --licht=<art> für die Messläufe (Standard "spiel"). Zelle "bild": <png> zeigt ein fertiges Bild (Vergleichsblatt);
#   "fuell": {…} wie "schluessel" für das Fülllicht.
#   … --gewichte=<personen.json> --aus=<gewichte.json> --ende   (auch --headless) Formgewichte je Person ausschreiben
#   … --bruecke_pruefen [--aus=<datei.json>] --ende             Abnahme der Erbgut-Brücke (S0)
# Halt B2 (Probekopf): Zellen kennen "weg_a": true (Aussehen Weg A, charakter_kopf.gd), "licht_breit": false (altes
#   Short-Lighting bei ¾-Ansichten; Standard ist das Schlüssellicht auf der kameranahen Seite), "malfilter": true oder
#   {"radius": 3, "mischung": 0.85, "tusche": 0.3} (Kuwahara und leichte Tusche, nur Porträt). Ein Kopf mit eigener
#   regeln6.json im .glb-Ordner rechnet seine Gene nach dieser (Probeköpfe nebeneinander).
extends Control

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")
const Buehne := preload("res://versuch/charakter/charakter_buehne.gd")

var glb_ordner := OS.get_environment("HOME").path_join("werkzeuge/charakter/glb3")
var glb5_ordner := OS.get_environment("HOME").path_join("werkzeuge/charakter/glb5")
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
	Kopf.zuege_aus = args.has("ohne_zuege")
	glb5_ordner = args.get("glb5", glb5_ordner)
	bilder = args.get("bilder", bilder)
	DirAccess.make_dir_recursive_absolute(bilder)
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	var hg := ColorRect.new()
	hg.color = Color(0.11, 0.1, 0.09)
	hg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(hg)
	if not DirAccess.dir_exists_absolute(glb_ordner) and not args.has("bruecke_pruefen"):
		_meldung("Ordner mit den Köpfen fehlt: %s\nDie .glb entstehen lokal mit tools/charakter/kopf3.sh aus ICT- und MakeHuman-Daten,\ndie nicht im Repository liegen (siehe docs/charakter/phase3.md)." % glb_ordner)
		return
	var etwas := false
	if args.has("gewichte"):
		await _gewichte_ausschreiben()
		etwas = true
	if args.has("bruecke_pruefen"):
		_bruecke_pruefen()
		etwas = true
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

# Prüferbefund Etappe A: get_file_as_string() liefert für /proc-Dateien (Länge 0) einen leeren Text, die Last stand
# deshalb immer auf 0,0. Zeilenweise lesen geht.
static func _last() -> float:
	var f := FileAccess.open("/proc/loadavg", FileAccess.READ)
	if f == null:
		return -1.0
	var z := f.get_line()
	f.close()
	return snappedf(float(z.split(" ")[0]), 0.01) if z != "" else -1.0

# Andere Prozesse auf der Grafikkarte (laut nvidia-smi, wie tools/charakter/warte_last.py), ohne diesen; −1 = unbekannt
static func _gpu_fremd() -> int:
	var aus := []
	if OS.execute("nvidia-smi", [], aus) != 0 or aus.is_empty():
		return -1
	var eigen := str(OS.get_process_id())
	var pids := {}
	for z in String(aus[0]).split("\n"):
		var t := z.split(" ", false)
		if t.size() > 6 and t[0] == "|" and t[t.size() - 1] == "|" and t[t.size() - 2].ends_with("MiB") and t[4].is_valid_int() and t[4] != eigen:
			pids[t[4]] = true
	return pids.size()

func _datei(name: String, st := 5) -> String:
	if name == "standard":
		name = "kopf3.glb"
		var wahl := ["kopf6.glb"] if st >= 6 else ["kopf5.glb", "kopf4.glb"]
		for n in wahl:
			if FileAccess.file_exists(glb_ordner.path_join(n)) or FileAccess.file_exists(glb5_ordner.path_join(n)):
				name = n
				break
	if name.begins_with("~/"):
		return OS.get_environment("HOME").path_join(name.substr(2))
	if name.begins_with("/"):
		return name
	# Phase 6: was im --glb-Ordner fehlt (glb6 hat nur kopf6), kommt aus glb5
	if not FileAccess.file_exists(glb_ordner.path_join(name)) and FileAccess.file_exists(glb5_ordner.path_join(name)):
		return glb5_ordner.path_join(name)
	return glb_ordner.path_join(name)

func _buehne(kopf: String, groesse: Vector2i, st := 5) -> Buehne:
	var datei := _datei(kopf, st)
	var schl := "%s|%s" % [datei, groesse]
	if _buehnen.has(schl):
		return _buehnen[schl]
	var b := Buehne.new(groesse)
	add_child(b)
	b.kopf_laden(datei, st)
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

# Eine Zelle auf einen Kopf anwenden (Volk-Mittel, dann Saat/Familie über den Kern, dann Gene, Alter, Geschlecht, Mimik)
static func zelle_setzen(k: Kopf, z: Dictionary) -> void:
	var volk: String = z.get("volk", "Mensch")
	var gene := Kopf.mittelwerte(volk)
	if int(z.get("stand", 5)) >= 6:
		gene.merge(Kopf.mittelwerte6(volk), false)    # Etappe B: auch die Vorschlagsgene (sonst bleiben Werte der Vorzelle)
	var ev := erbgut_vorlage(z)
	if z.has("saat"):
		gene.merge(Kopf.gene_aus_saat(int(z["saat"]), volk, z.get("familie", {}), z.get("saat_arten", []), ev), true)
		# Etappe B (S7, Familienbogen P5): „allelregel“: „mittel“ = Gestalt-Gene und Züge als Mittel beider Allele
		if String(z.get("allelregel", "sichtbar")) == "mittel":
			var mi := Kopf.gene_allelmittel_aus_saat(int(z["saat"]), volk, z.get("familie", {}), ev)
			var arten: Array = z.get("saat_arten", [])
			for kk in mi:
				if arten.is_empty() or "Merkmal" in arten:
					gene[kk] = mi[kk]
		# Personen-Samen des Aussehens: im Versuch aus dem Bogen-Startwert mit eigenem Zweck (Plan S6.6)
		k.aussehen_samen = hash("aussehen:%d" % int(z["saat"]))
	else:
		k.aussehen_samen = int(z.get("samen", 0))
	gene.merge(z.get("gene", {}), true)
	k.volk = volk
	k.iris_vorgabe = int(z.get("iris_muster", -1))
	k.gene = {}                                    # jede Zelle setzt alle Gene neu (nichts von der vorigen Person)
	k.stand_setzen(int(z.get("stand", 5)))
	k.weg_a_setzen(bool(z.get("weg_a", false)))       # Halt B2: Aussehen Weg A
	k.mimik_mit_alter = bool(z.get("mimik_mit_alter", true))
	k.mimik = z.get("mimik", {})
	k.zusatz_formen = z.get("formen", {})
	var hv: Dictionary = z.get("haar", {})
	k.haar_vorgabe = hv.get("vorgabe", {})
	k.haar_ruhe = bool(hv.get("ruhe", false))
	k.haar_sparsam = bool(hv.get("sparsam", false))
	k.haar_schatten = bool(hv.get("schatten", true))
	if k.haar_knoten and String(hv.get("modus", "karten")) != k.haar_modus:
		k.haar_modus_setzen(String(hv.get("modus", "karten")))
	k.aussehen_setzen(gene, float(z.get("alter", 35.0)), _mann(z.get("geschlecht", "m")))

# Erbgut-Vorlage einer Zelle: „erbgut_vorlage“, sonst „vorlage“, wenn es eine .json ist (S0; „vorlage“ ohne .json ist eine
# Fotovorlage in P3), sonst ab Stand 6 der Vorschlag mit den neuen Genen (S7), sonst Erbgutvorlage.Standard
static func erbgut_vorlage(z: Dictionary) -> String:
	if z.has("erbgut_vorlage"):
		return String(z["erbgut_vorlage"])
	var v = z.get("vorlage", "")
	if v is String and String(v).ends_with(".json"):
		return String(v)
	return Kopf.VORSCHLAG if int(z.get("stand", 5)) >= 6 else ""

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
			# Zusatzformen ebenso (Zeile: Grundmischung, Spalte: eine Probe dazu)
			var fo: Dictionary = {}
			for quelle in [ueberall, zeilen[zi], spalten[si]] + ([einzeln[si]] if si < einzeln.size() else []):
				for n in quelle.get("formen", {}):
					fo[n] = float(fo.get(n, 0.0)) + float(quelle["formen"][n])
			z["formen"] = fo
			# Phase 6: --stand=<n> gilt für alle Zellen ohne eigenen Stand
			if args.has("stand") and not z.has("stand"):
				z["stand"] = int(args["stand"])
			# Phase 6: fertiges Bild statt Render (Vergleichsblatt A0 | A1 | Fotovorlage, tools/charakter/messen/vergleichsblatt.py)
			if z.has("bild"):
				var fertig := Image.load_from_file(String(z["bild"]))
				if fertig and fertig.get_size() != zelle:
					fertig.resize(zelle.x, zelle.y, Image.INTERPOLATE_LANCZOS)
				reihe.append([fertig, z.get("titel", "")])
				continue
			if z.get("kopf") == null:
				reihe.append([null, z.get("titel", "")])
				continue
			var b := _buehne(z["kopf"], zelle, int(z.get("stand", 5)))
			if b.kopf.fehler != "":
				reihe.append([null, b.kopf.fehler])
				continue
			zelle_setzen(b.kopf, z)
			if b.kopf.phase3:
				b.kopf.vorher_setzen(bool(z.get("vorher", false)))
				b.kopf.ultra_setzen(bool(z.get("ultra", false)))
			b.kopf.teile_ausblenden(z.get("ausblenden", []))
			b.kopf.stoff_parameter(z.get("stoff", {}))
			# Phase 6: erst alte Einzelwerte zurück, dann Lichtart und Anzeige, dann die Einzelwerte der Zelle
			b.umgebung_setzen({})
			b.licht_setzen({})
			# Halt A: Grafikstufe der Bühne (HDRI nur Hoch/Ultra)
			b.stufe = String(z.get("stufe", "ultra" if bool(z.get("ultra", false)) else "hoch"))
			b.licht_art_setzen(String(z.get("licht", "spiel")))
			b.anzeige_setzen(String(z.get("anzeige", "normal")))
			var sr: Array = z.get("schluessel_richtung", [])
			if sr.size() == 2:
				b.schluessel_richtung(float(sr[0]), float(sr[1]))
			b.pruefkoerper_setzen(String(z.get("pruefkoerper", "")))
			b.umgebung_setzen(z.get("umgebung", {}))
			b.licht_setzen(z.get("schluessel", {}))
			b.fuell_setzen(z.get("fuell", {}))
			# Halt B2: Schlüssellicht auf der breiten Seite (Standard an), Malfilter nur auf Wunsch
			b.schluessel_breit = bool(z.get("licht_breit", true))
			var mf = z.get("malfilter", false)
			b.malfilter_setzen(mf is Dictionary or bool(mf), mf if mf is Dictionary else {})
			b.ansicht(z.get("ansicht", "portraet"), float(z.get("winkel", 0.0)), float(z.get("hoehe", 3.0)),
				float(z.get("spanne_m", 0.0)), float(z.get("ziel_m", 0.0)), float(z.get("ziel_x", 0.0)),
				String(z.get("kamera", "")) == "ortho")
			await _warte(int(z.get("warten", 4)))
			var bild := b.foto()
			if z.has("vorlage") and not (z.get("vorlage") is String and String(z.get("vorlage")).ends_with(".json")):
				bild = _vorlage_bild(b, z["vorlage"], zelle, bool(z.get("geschlecht", "m") == "w"))
			if bild == null or bild.is_empty():
				bild = Image.create(zelle.x, zelle.y, false, Image.FORMAT_RGB8)
			bild.save_png(bilder.path_join(name).path_join("%02d_%02d.png" % [zi, si]))
			_begleitdatei(b, z, bilder.path_join(name).path_join("%02d_%02d.json" % [zi, si]))
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
	# Etappe B: „schrift“ vergrößert alle Beschriftungen (Halt-Mappen für Laien), „rand“ die Breite der Zeilennamen
	var sf := float(plan.get("schrift", 1.0))
	var rand := int(plan.get("rand", 150))
	var kopfzeile := int(34 * sf)
	var spaltenkopf := int(30 * sf)
	var unter := int(26 * sf)
	var titel := _label()
	titel.text = plan.get("titel", name)
	titel.position = Vector2(12, 6)
	titel.add_theme_font_size_override("font_size", int(20 * sf))
	tafel.add_child(titel)
	for si in spalten.size():
		var l := _label()
		l.text = spalten[si].get("titel", "")
		l.position = Vector2(rand + si * (zelle.x + 6) + 6, kopfzeile)
		l.add_theme_font_size_override("font_size", int(18 * sf))
		tafel.add_child(l)
	for zi in fotos.size():
		var y: int = kopfzeile + spaltenkopf + zi * (zelle.y + unter)
		var ln := _label()
		ln.text = zeilen[zi].get("titel", "")
		ln.position = Vector2(8, y + zelle.y * 0.4)
		ln.size = Vector2(rand - 12, 60 * sf)
		ln.autowrap_mode = TextServer.AUTOWRAP_WORD
		ln.add_theme_font_size_override("font_size", int(16 * sf))
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
			lz.size = Vector2(zelle.x - 8, 24 * sf)
			lz.autowrap_mode = TextServer.AUTOWRAP_WORD if eintrag[0] == null else TextServer.AUTOWRAP_OFF
			lz.add_theme_font_size_override("font_size", int(14 * sf))
			tafel.add_child(lz)
	await _warte(3)
	var ges := Vector2i(rand + spalten.size() * (zelle.x + 6), kopfzeile + spaltenkopf + fotos.size() * (zelle.y + unter))
	var bild := get_viewport().get_texture().get_image()
	bild = bild.get_region(Rect2i(Vector2i.ZERO, ges.min(bild.get_size())))
	bild.save_png(bilder.path_join(name + ".png"))
	print("BOGEN ", bilder.path_join(name + ".png"), " ", ges)
	var fenster := Vector2i(get_viewport().get_visible_rect().size)
	if ges.x > fenster.x or ges.y > fenster.y:
		print("WARNUNG Fenster kleiner als der Bogen ", fenster, " < ", ges)
	tafel.queue_free()
	await _warte(2)

# Phase 6 (Bogen P3): Fotovorlage statt Render, auf den Pupillenabstand des Kopfs dieser Zelle skaliert und an
# dessen Pupillen gelegt (gleiche Kamera wie die Nachbarzelle). v = {"bild": Pfad, "pupillen": [[x, y], [x, y]]
# (links und rechts im Bild), "ipd_mm": Pupillenabstand des Kopfs (Mittelgesicht: Mann 63,6, Frau 62,6)}.
func _vorlage_bild(b: Buehne, v: Dictionary, zelle: Vector2i, frau: bool) -> Image:
	var pfad := String(v.get("bild", ""))
	if pfad.begins_with("~/"):
		pfad = OS.get_environment("HOME").path_join(pfad.substr(2))
	var img := Image.load_from_file(pfad)
	if img == null:
		print("WARNUNG Vorlage fehlt: ", pfad)
		return null
	img.convert(Image.FORMAT_RGB8)
	var pu: Array = v.get("pupillen", [])
	if pu.size() != 2:
		return null
	var q_l := Vector2(pu[0][0], pu[0][1])
	var q_r := Vector2(pu[1][0], pu[1][1])
	# Pupillen des Kopfs: Mitte der Pupillenöffnung (tools/charakter/messen/merkpunkte.py), am Mittelgesicht 10 mm vor
	# und 0,5 mm unter der Augenmitte, Abstand Mann 63,6 mm, Frau 62,6 mm
	var halb := float(v.get("ipd_mm", 62.6 if frau else 63.6)) / 2000.0
	var t := b.kopf.global_transform
	var z_l := b.kamera.unproject_position(t * (b.kopf.augen_mitte + Vector3(-halb, -0.0005, 0.010)))
	var z_r := b.kamera.unproject_position(t * (b.kopf.augen_mitte + Vector3(halb, -0.0005, 0.010)))
	if z_l.x > z_r.x:
		var tmp := z_l
		z_l = z_r
		z_r = tmp
	var s := z_l.distance_to(z_r) / maxf(q_l.distance_to(q_r), 1.0)
	var neu := img.duplicate()
	neu.resize(maxi(1, int(round(img.get_width() * s))), maxi(1, int(round(img.get_height() * s))), Image.INTERPOLATE_LANCZOS)
	var aus := Image.create(zelle.x, zelle.y, false, Image.FORMAT_RGB8)
	aus.fill(Color(0.45, 0.45, 0.45))
	var versatz := (z_l + z_r) * 0.5 - (q_l + q_r) * 0.5 * s
	var ziel := Vector2i(int(round(versatz.x)), int(round(versatz.y)))
	var quelle := Vector2i(maxi(0, -ziel.x), maxi(0, -ziel.y))
	var nach := Vector2i(maxi(0, ziel.x), maxi(0, ziel.y))
	var gr := Vector2i(mini(neu.get_width() - quelle.x, zelle.x - nach.x), mini(neu.get_height() - quelle.y, zelle.y - nach.y))
	if gr.x > 0 and gr.y > 0:
		aus.blit_rect(neu, Rect2i(quelle, gr), nach)
	return aus

# ---------------------------------------------------------------- Begleitdateien und Gewichte (Phase 6)
static func _json_wert(v):
	if v is Vector3:
		return [v.x, v.y, v.z]
	if v is Vector4:
		return [v.x, v.y, v.z, v.w]
	if v is Color:
		return [v.r, v.g, v.b, v.a]
	if v is Dictionary:
		var d := {}
		for k in v:
			d[str(k)] = _json_wert(v[k])
		return d
	if v is Array:
		return v.map(func(x): return _json_wert(x))
	return v

static func kopf_daten(k: Kopf) -> Dictionary:
	return {
		"glb": k.pfad, "stand": k.stand, "alter": k.alter, "geschlecht": k.geschlecht, "gene": k.gene.duplicate(),
		"mimik": k.mimik.duplicate(), "mimikfaktor": k.mimikfaktor, "gewichte": k.gewichte.duplicate(),
		"szene": k.szene_gewichte(), "augen_mitte": k.augen_mitte, "mund_mitte": k.mund_mitte, "oben": k.oben,
		"werte3": k.werte3.duplicate(), "haar_modus": k.haar_modus, "volk": k.volk, "aussehen_samen": k.aussehen_samen,
		"gene_z": k.gene_z.duplicate(), "karikatur": k.karikatur.duplicate(),
	}

func _begleitdatei(b: Buehne, z: Dictionary, datei: String) -> void:
	var d := b.kamera_daten()
	d["zelle"] = z
	if b.kopf:
		d["kopf"] = kopf_daten(b.kopf)
		if z.has("saat"):
			d["erbgut"] = Kopf.erbgut_text(int(z["saat"]), z.get("volk", "Mensch"), z.get("familie", {}), erbgut_vorlage(z))
	var f := FileAccess.open(datei, FileAccess.WRITE)
	f.store_string(JSON.stringify(_json_wert(d), " ", false))
	f.close()

# --gewichte=<personen.json> --aus=<gewichte.json>: je Person (Schlüssel wie eine Bogenzelle: gene, saat, familie,
# alter, geschlecht, volk, mimik, formen, stand) die Formgewichte, die _anwenden() rechnet, und die Werte aus der Szene.
# Läuft auch ohne Grafikkarte (--headless). Python misst genau diese Gewichte (kein Nachbau von _anwenden()).
func _gewichte_ausschreiben() -> void:
	var text := FileAccess.get_file_as_string(args["gewichte"])
	var eingabe = JSON.parse_string(text) if text != "" else null
	if eingabe == null:
		_meldung("Personendatei fehlt oder ist kein JSON: " + String(args["gewichte"]))
		return
	var liste: Array = eingabe if eingabe is Array else eingabe.get("personen", [])
	var kopfdatei := _datei(eingabe.get("kopf", "standard") if eingabe is Dictionary else "standard", int(args.get("stand", "5")))
	var k := Kopf.new()
	k.haar_modus = "aus"
	add_child(k)
	if not k.laden(kopfdatei):
		_meldung(k.fehler)
		return
	var t := Time.get_ticks_usec()
	var aus := []
	for p in liste:
		var z: Dictionary = p.duplicate(true)
		if not z.has("haar"):
			z["haar"] = {"modus": "aus"}
		if args.has("stand") and not z.has("stand"):
			z["stand"] = int(args["stand"])
		zelle_setzen(k, z)
		var e := kopf_daten(k)
		e["name"] = z.get("name", "")
		if z.has("saat"):
			e["saat"] = int(z["saat"])
			e["erbgut"] = Kopf.erbgut_text(int(z["saat"]), z.get("volk", "Mensch"), z.get("familie", {}), erbgut_vorlage(z))
		aus.append(e)
	var ziel := String(args.get("aus", bilder.path_join("gewichte.json")))
	var f := FileAccess.open(ziel, FileAccess.WRITE)
	f.store_string(JSON.stringify(_json_wert({"kopf": kopfdatei, "personen": aus}), " ", false))
	f.close()
	print("GEWICHTE %d Personen in %.0f ms -> %s" % [aus.size(), (Time.get_ticks_usec() - t) / 1000.0, ziel])
	k.queue_free()

# --bruecke_pruefen: Abnahme S0 der Erbgut-Brücke (gleiche Bytes, bestehende Gene mit/ohne Vorschlag gleich)
func _bruecke_pruefen() -> void:
	var b := Kopf.bruecke()
	if b == null:
		_meldung("ErbgutBruecke.cs lässt sich nicht laden (C#-Assembly gebaut?)")
		return
	var t := Time.get_ticks_usec()
	var erg: Dictionary = b.call("Pruefen", int(args.get("von", "10001")), int(args.get("anzahl", "1000")), String(args.get("vorlage", "")))
	erg["ms"] = (Time.get_ticks_usec() - t) / 1000.0
	# 30 Gensätze wie die Galerie mit Taste Z: alle verschieden?
	var texte := {}
	for i in 30:
		texte[Kopf.erbgut_text(20001 + i)] = true
	erg["galerie_30_verschieden"] = texte.size()
	# Etappe B (S6/S7): der Personen-Samen bleibt mit Probe-Gen und mit der Vorschlagsdatei gleich
	erg["samen"] = b.call("SamenPruefen", int(args.get("von", "10001")), int(args.get("anzahl", "1000")), Kopf.VORSCHLAG)
	erg["vorschlag_gegen_standard"] = b.call("Pruefen", int(args.get("von", "10001")), int(args.get("anzahl", "1000")), Kopf.VORSCHLAG)
	print("BRUECKE ", JSON.stringify(erg))
	if args.has("aus"):
		var f := FileAccess.open(String(args["aus"]), FileAccess.WRITE)
		f.store_string(JSON.stringify(erg, " ", false))
		f.close()

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
var _ansicht_schl := ""
var _haar_wahl: OptionButton
var _frisur_wahl: OptionButton
var _struktur_r: Range
var _bart_wahl: OptionButton

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
	if not nur_bild:
		print("REGLER Kamera: ", "frei" if _buehne_r.freie_kamera_einsetzen() else "fest (FreieKamera.cs fehlt)")
	var datei := _datei(args.get("kopf", "standard"), int(args.get("stand", "5")))
	if not _buehne_r.kopf_laden(datei, int(args.get("stand", "5"))):
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
	_ueberschrift(liste, "Basiskopf Phase 4 – %s" % datei.get_file())
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
	if _kopf.haar_knoten:
		_ueberschrift(liste, "Haar und Bart (Phase 4)")
		var hz := HBoxContainer.new()
		liste.add_child(hz)
		_haar_wahl = OptionButton.new()
		for t in ["Haarkarten", "TressFX", "ohne Haar"]:
			_haar_wahl.add_item(t)
		_haar_wahl.item_selected.connect(func(i):
			_kopf.haar_modus_setzen(["karten", "tressfx", "aus"][i]))
		hz.add_child(_haar_wahl)
		var ue: Dictionary = _kopf.haar_knoten.uebersicht(_kopf.haar_knoten.ordner)
		_frisur_wahl = OptionButton.new()
		_frisur_wahl.add_item("Frisur aus den Genen")
		for g in ["m", "w"]:
			for n in ue.get("frisuren", {}).get(g, []):
				_frisur_wahl.add_item(n)
		_frisur_wahl.item_selected.connect(func(_i): _aendern())
		hz.add_child(_frisur_wahl)
		_bart_wahl = OptionButton.new()
		_bart_wahl.add_item("Bart aus den Genen")
		_bart_wahl.add_item("rasiert")
		for n in ue.get("baerte", []):
			_bart_wahl.add_item(n)
		_bart_wahl.item_selected.connect(func(_i): _aendern())
		liste.add_child(_bart_wahl)
		# Phase 5: Haarstruktur (vorläufig, bis kern das Gen „haarstruktur“ in erbgut.json hat)
		_struktur_r = _regler_zeile(liste, "Haarstruktur (glatt – wellig – lockig, TressFX)", 0, 255, 60, 1)
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
	if _frisur_wahl:
		var v := {}
		if _frisur_wahl.selected > 0:
			v["frisur"] = _frisur_wahl.get_item_text(_frisur_wahl.selected)
		if _bart_wahl.selected == 1:
			v["bart"] = ""
		elif _bart_wahl.selected > 1:
			v["bart"] = _bart_wahl.get_item_text(_bart_wahl.selected)
		if _struktur_r:
			v["struktur"] = _struktur_r.value / 255.0
		_kopf.haar_vorgabe = v
	_kopf.aussehen_setzen(gene, _alter_r.value, 1.0 if _mann_r.selected == 0 else 0.0)
	# Mit freier Kamera nur neu hinstellen, wenn Ansicht oder Drehung geändert wurden
	var schl := "%s|%s" % [_ansicht_wahl.selected, _winkel_r.value]
	if not _buehne_r.freie or schl != _ansicht_schl:
		_ansicht_schl = schl
		_buehne_r.ansicht(_ansicht_wahl.get_item_text(_ansicht_wahl.selected), _winkel_r.value)

# ---------------------------------------------------------------- Messen
func _messen() -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var kd := "standard"
	var vk := {"frisur": "mann_pagen", "bart": "vollbart"}
	var laeufe := [
		{"name": "leer", "kopf": "keiner"},
		{"name": "ohne_haar", "kopf": kd, "bewegt": "keine", "haar": {"modus": "aus"}},
		{"name": "haar_karten", "kopf": kd, "bewegt": "keine", "haar": {"modus": "karten", "vorgabe": vk}},
		{"name": "haar_tressfx", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": vk}},
		{"name": "haar_tressfx_ruhe", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": vk, "ruhe": true}},
		{"name": "haar_karten_frau_offen", "kopf": kd, "bewegt": "keine", "geschlecht": "w", "haar": {"modus": "karten", "vorgabe": {"frisur": "frau_offen"}}},
		{"name": "haar_tressfx_frau_offen", "kopf": kd, "bewegt": "keine", "geschlecht": "w", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "frau_offen"}}},
		{"name": "haar_karten_ohne_schatten", "kopf": kd, "bewegt": "keine", "haar": {"modus": "karten", "vorgabe": vk, "schatten": false}},
		# Phase 5: die teuersten der zehn Stile (Locken mit 32/64 Punkten, wilder Bart) und ein schlichter
		{"name": "haar_tressfx_krauskopf_wildbart", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "krauskopf", "bart": "wildbart"}}},
		{"name": "haar_tressfx_frau_locken", "kopf": kd, "bewegt": "keine", "geschlecht": "w", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "krause_locken"}}},
		{"name": "haar_tressfx_geschoren_stoppeln", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "mann_kurz", "bart": "stoppeln"}}},
		{"name": "haar_tressfx_crespine", "kopf": kd, "bewegt": "keine", "geschlecht": "w", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "haarnetz"}}},
		{"name": "haar_tressfx_lockig", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": {"frisur": "mann_pagen", "bart": "vollbart", "struktur": 1.0}}},
		{"name": "haar_tressfx_sparsam", "kopf": kd, "bewegt": "keine", "haar": {"modus": "tressfx", "vorgabe": vk, "sparsam": true}},
		{"name": "x8_karten_ohne_schatten", "kopf": kd, "bewegt": "keine", "anzahl": 8, "haar": {"modus": "karten", "vorgabe": vk, "schatten": false}},
		{"name": "x8_ohne_haar", "kopf": kd, "bewegt": "keine", "anzahl": 8, "haar": {"modus": "aus"}},
		# Etappe B: Zufallspersonen (alle Gen-, Zug- und Asymmetrieformen mit Gewicht ≠ 0)
		{"name": "ohne_haar_zufall", "kopf": kd, "bewegt": "keine", "saat": 6001, "haar": {"modus": "aus"}},
		{"name": "haar_tressfx_ruhe_zufall", "kopf": kd, "bewegt": "keine", "saat": 6001, "haar": {"modus": "tressfx", "vorgabe": vk, "ruhe": true}},
		{"name": "x8_ohne_haar_zufall", "kopf": kd, "bewegt": "keine", "anzahl": 8, "saat": 6001, "haar": {"modus": "aus"}},
		{"name": "x8_karten", "kopf": kd, "bewegt": "keine", "anzahl": 8, "haar": {"modus": "karten", "vorgabe": vk}},
		{"name": "x8_tressfx_ruhe", "kopf": kd, "bewegt": "keine", "anzahl": 8, "haar": {"modus": "tressfx", "vorgabe": vk, "ruhe": true}},
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
	# Phase 6: gepaart – je Wiederholung jeden Lauf für jeden Stand nacheinander (Reihenfolge je Wiederholung gewechselt)
	var staende: Array = []
	for t in String(args.get("staende", args.get("stand", "5"))).split(","):
		staende.append(int(t))
	# Halt B2: gepaarte Varianten statt Stände (--varianten=name:glb-ordner:weg_a:malfilter,…; Stand 6), z. B.
	# haltb:~/werkzeuge/charakter/glb6:0:0,b2a:~/werkzeuge/charakter/glb6b2:1:0
	if args.has("varianten"):
		staende.clear()
		for v in String(args["varianten"]).split(","):
			var t := v.split(":")
			var o := t[1] if t.size() > 1 else glb_ordner
			if o.begins_with("~/"):
				o = OS.get_environment("HOME").path_join(o.substr(2))
			staende.append({"variante": t[0], "glb": o, "weg_a": t.size() > 2 and t[2] == "1",
				"malfilter": t.size() > 3 and t[3] == "1"})
	for wdh in int(args.get("wiederholungen", "1")):
		for l in laeufe:
			if args.has("nur") and not String(args["nur"]).split(",").has(l["name"]):
				continue
			var folge := staende.duplicate()
			if wdh % 2 == 1:
				folge.reverse()
			for st in folge:
				var lauf: Dictionary = l.duplicate(true)
				lauf["wiederholung"] = wdh
				if st is Dictionary:
					lauf.merge(st, true)
					lauf["stand"] = 6
				else:
					lauf["stand"] = st
				await _ein_lauf(lauf)

func _ein_lauf(l: Dictionary) -> void:
	var st := int(l.get("stand", args.get("stand", "5")))
	var ordner_vorher := glb_ordner
	if l.has("glb"):
		glb_ordner = String(l["glb"])         # Halt B2: Variante mit eigenem Kopf-Ordner
	var datei := _datei(l.get("kopf", "kopf3.glb"), st)
	glb_ordner = ordner_vorher
	var ultra_stufe := String(args.get("stufe", "hoch")) == "ultra"
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
			# leere Bühne: Licht des Stands (ohne Kopf richtet sich „spiel“ nicht nach ihm)
			var la := String(args.get("licht", "spiel"))
			b.licht_art_setzen(("spiel6" if st >= 6 else "spiel5") if la == "spiel" else la)
			continue
		if not b.kopf_laden(datei, st):
			_meldung(b.kopf.fehler)
			behaelter.queue_free()
			return
		laden_ms += b.kopf.ladezeit_ms
		var mz := {"alter": 35, "geschlecht": l.get("geschlecht", "m"), "haar": l.get("haar", {"modus": "aus"}),
			"stand": int(l.get("stand", args.get("stand", "5")))}
		if l.has("saat"):
			mz["saat"] = int(l["saat"]) + i      # Etappe B: Zufallsperson (Gene, Züge, Asymmetrie gesetzt)
		mz["weg_a"] = bool(l.get("weg_a", false))
		zelle_setzen(b.kopf, mz)
		if b.kopf.phase3:
			b.kopf.vorher_setzen(bool(l.get("vorher", false)))
			b.kopf.ultra_setzen(bool(l.get("ultra", false)) or ultra_stufe)
		b.stufe = "ultra" if ultra_stufe else "hoch"
		b.licht_art_setzen(String(args.get("licht", "spiel")))
		b.malfilter_setzen(bool(l.get("malfilter", false)))
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
		"dreiecke_haar": liste[0].kopf.haar_knoten.dreiecke() if liste[0].kopf and liste[0].kopf.haar_knoten else 0,
		"haar_ms": liste[0].kopf.haar_knoten.zeiten if liste[0].kopf and liste[0].kopf.haar_knoten else {},
		"dreiecke_bild": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
		"zeichenaufrufe": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
		"vram_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_VIDEO_MEM_USED) - leer) / 1048576.0, 0.1),
		"vram_puffer_mb": snappedf((RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED) - leer_puf) / 1048576.0, 0.1),
		"laden_ms": snappedf(laden_ms / anzahl, 0.1),
		"bilder_ms": snappedf(liste[0].kopf.bilder_ms, 0.1) if liste[0].kopf else 0.0,
		"textur_mb": snappedf(RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED) / 1048576.0, 0.1),
		"aufloesung": "%dx%d" % [gr0.x, gr0.y],
		"last": _last(), "gpu_fremd": _gpu_fremd(),
		"stand": st, "wiederholung": int(l.get("wiederholung", 0)), "licht": String(args.get("licht", "spiel")),
		"variante": String(l.get("variante", "")), "stufe": "ultra" if ultra_stufe else "hoch",
		"commit": String(args.get("commit", "")), "zeit": Time.get_datetime_string_from_system(),
	}
	print("MESSUNG ", JSON.stringify(erg))
	var f := FileAccess.open(bilder.path_join("messung.jsonl"), FileAccess.READ_WRITE if FileAccess.file_exists(bilder.path_join("messung.jsonl")) else FileAccess.WRITE)
	f.seek_end()
	f.store_line(JSON.stringify(erg))
	f.close()
	get_viewport().get_texture().get_image().save_png(bilder.path_join("messung_%s.png" % l["name"]))
	behaelter.queue_free()
	await _warte(4)
