# Baut die Waren-Knöpfe (128 × 128) aus Rahmen, Hintergrund und Motiv zusammen.
#
# Aufruf (aus dem Repository-Ordner, ohne Fenster):
#   godot --headless --path game --script ../tools/icons/knoepfe_bauen.gd -- quellen <Rohbild-Ordner>
#   godot --headless --path game --script ../tools/icons/knoepfe_bauen.gd -- bauen [dateiname ...]
#   godot --headless --path game --script ../tools/icons/knoepfe_bauen.gd -- tafel <ziel.png> [dateiname ...]
#
# quellen: Liest die Rohbilder von Gemini (<Ordner>/rahmen|hintergrund|motiv/<name>.jpg|png, 1024 px)
#          und legt die Zwischenteile als 256-px-PNG unter tools/icons/quellen/ ab:
#          Rahmen zugeschnitten mit durchsichtiger Mitte, Motive freigestellt (Weiß wird durchsichtig).
# bauen:   Setzt je Ware Hintergrund (Beruf) + Motiv + Rahmen (Stufe) zusammen und schreibt
#          game/assets/oberflaeche/icons/<ware>.png. Ohne Namen alle Motive unter quellen/motiv/.
#          Beruf und Stufe je Ware kommen aus game/daten/wirtschaft.json (niedrigste Stufe, in der die Ware
#          vorkommt); Waren ohne Betrieb sind Rohstoffe (Holzrahmen, Lehmbraun).
# tafel:   Übersichtstafel (128 px oben, 64 px darunter) zur Abnahme.
#
# Die Dateinamen folgen WarenIcons.Dateiname: "Kräuterschnaps" -> "kraeuterschnaps".
extends SceneTree

const GROESSE := 256   # Größe der Zwischenteile
const ENDGROESSE := 128

const HINTERGRUND := {
	"Schmied": "glutrot", "Alchimist": "smaragdgruen", "Parfumeur": "purpur", "Prediger": "nachtblau",
	"Wirt": "bernstein", "Tischler": "ockerbraun", "Steinmetz": "schiefergrau",
}
const ROHSTOFF_HINTERGRUND := "lehmbraun"
const RAHMEN := ["holz", "stein", "silber", "gold"]  # Index = Stufe (0 = Rohstoff)

var _wurzel: String


func _init() -> void:
	# --script läuft im Projektordner (game/); das Repository liegt eine Ebene höher.
	_wurzel = ProjectSettings.globalize_path("res://").path_join("..").simplify_path()
	var arg := OS.get_cmdline_user_args()
	if arg.is_empty():
		print("Aufruf: quellen <Ordner> | bauen [namen] | tafel <ziel.png> [namen]")
		quit(1)
		return
	match arg[0]:
		"quellen": _quellen(arg[1])
		"bauen": _bauen(arg.slice(1))
		"tafel": _tafel(arg[1], arg.slice(2))
		_: print("Unbekannter Befehl: ", arg[0])
	quit()


# ---------- Hilfen ----------

static func dateiname(ware: String) -> String:
	return ware.to_lower().replace("ä", "ae").replace("ö", "oe").replace("ü", "ue").replace("ß", "ss").replace(" ", "_")


func _quellordner(art: String) -> String:
	return _wurzel.path_join("tools/icons/quellen").path_join(art)


func _laden(pfad: String) -> Image:
	var bild := Image.load_from_file(pfad)
	if bild == null or bild.is_empty():
		push_error("Bild nicht lesbar: " + pfad)
		return null
	bild.convert(Image.FORMAT_RGBA8)
	return bild


func _finde(ordner: String, name: String) -> String:
	for endung in ["png", "jpg", "jpeg", "webp"]:
		var pfad := ordner.path_join(name + "." + endung)
		if FileAccess.file_exists(pfad):
			return pfad
	return ""


func _dateien(ordner: String) -> PackedStringArray:
	var namen := PackedStringArray()
	for datei in DirAccess.get_files_at(ordner):
		if datei.get_extension().to_lower() in ["png", "jpg", "jpeg", "webp"]:
			namen.append(datei.get_basename())
	namen.sort()
	return namen


# ---------- quellen ----------

func _quellen(roh: String) -> void:
	for art in ["rahmen", "hintergrund", "motiv"]:
		DirAccess.make_dir_recursive_absolute(_quellordner(art))
		for name in _dateien(roh.path_join(art)):
			var bild := _laden(_finde(roh.path_join(art), name))
			if bild == null:
				continue
			match art:
				"rahmen": bild = _rahmen_aufbereiten(bild)
				"hintergrund": bild.resize(GROESSE, GROESSE, Image.INTERPOLATE_LANCZOS)
				"motiv": bild = _motiv_freistellen(bild)
			bild.save_png(_quellordner(art).path_join(name + ".png"))
			print("quelle: ", art, "/", name)


static func _hell(farbe: Color) -> float:
	return minf(farbe.r, minf(farbe.g, farbe.b))


static func _dunkel(farbe: Color) -> bool:
	return farbe.get_luminance() < 0.12


## Schneidet den Rahmen an seinem schwarzen Außenumriss zu (Gemini lässt oft einen Rand),
## skaliert auf 256 px und macht die weiße Mitte durchsichtig.
func _rahmen_aufbereiten(bild: Image) -> Image:
	var b := bild.get_width()
	var mitte := b / 2
	# Außenrand: erster dunkler Umriss von jeder Seite, gemessen an mehreren Linien (Median gegen Ausreißer).
	var links: Array = []
	var rechts: Array = []
	var oben: Array = []
	var unten: Array = []
	for t in [0.3, 0.4, 0.5, 0.6, 0.7]:
		var p := int(b * t)
		links.append(_erster_dunkler(bild, 0, p, 1, 0))
		rechts.append(b - 1 - _erster_dunkler(bild, b - 1, p, -1, 0))
		oben.append(_erster_dunkler(bild, p, 0, 0, 1))
		unten.append(b - 1 - _erster_dunkler(bild, p, b - 1, 0, -1))
	var x0: int = _median(links)
	var x1: int = b - 1 - _median(rechts)
	var y0: int = _median(oben)
	var y1: int = b - 1 - _median(unten)
	# Nur zuschneiden, wenn die Kanten sinnvoll liegen (Rand höchstens 12 % je Seite).
	var maxrand := int(b * 0.12)
	x0 = x0 if x0 <= maxrand else 0
	y0 = y0 if y0 <= maxrand else 0
	x1 = x1 if b - 1 - x1 <= maxrand else b - 1
	y1 = y1 if b - 1 - y1 <= maxrand else b - 1
	var ausschnitt := bild.get_region(Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1))
	ausschnitt.resize(GROESSE, GROESSE, Image.INTERPOLATE_LANCZOS)
	_mitte_durchsichtig(ausschnitt)
	return ausschnitt


## Vom Rand (x, y) in Richtung (dx, dy) bis zum ersten dunklen Pixel; liefert die Schrittzahl.
func _erster_dunkler(bild: Image, x: int, y: int, dx: int, dy: int) -> int:
	var n := 0
	while x >= 0 and y >= 0 and x < bild.get_width() and y < bild.get_height():
		if _dunkel(bild.get_pixel(x, y)):
			return n
		x += dx
		y += dy
		n += 1
	return 0


static func _median(werte: Array) -> int:
	var kopie := werte.duplicate()
	kopie.sort()
	return kopie[kopie.size() / 2]


## Füllt von der Bildmitte aus alles Weiße (die Mitte des Rahmens) als durchsichtig; ein Pixel mehr
## als nötig, damit kein heller Saum am Umriss bleibt.
func _mitte_durchsichtig(bild: Image) -> void:
	var b := bild.get_width()
	var h := bild.get_height()
	var loch := PackedByteArray()
	loch.resize(b * h)
	var stapel := PackedInt32Array([(h / 2) * b + b / 2])
	while not stapel.is_empty():
		var i: int = stapel[stapel.size() - 1]
		stapel.resize(stapel.size() - 1)
		if loch[i] != 0:
			continue
		var x := i % b
		var y := i / b
		if _hell(bild.get_pixel(x, y)) < 0.8:
			continue
		loch[i] = 1
		if x > 0: stapel.append(i - 1)
		if x < b - 1: stapel.append(i + 1)
		if y > 0: stapel.append(i - b)
		if y < h - 1: stapel.append(i + b)
	# Eine Pixelreihe nach außen erweitern; diese Reihe wird halb durchsichtig.
	var rand := PackedByteArray()
	rand.resize(b * h)
	for y in h:
		for x in b:
			var i := y * b + x
			if loch[i] != 0:
				continue
			if (x > 0 and loch[i - 1] != 0) or (x < b - 1 and loch[i + 1] != 0) \
					or (y > 0 and loch[i - b] != 0) or (y < h - 1 and loch[i + b] != 0):
				rand[i] = 1
	for y in h:
		for x in b:
			var i := y * b + x
			if loch[i] != 0:
				bild.set_pixel(x, y, Color(0, 0, 0, 0))
			elif rand[i] != 0:
				var f := bild.get_pixel(x, y)
				f.a = clampf(1.0 - (_hell(f) - 0.25) * 1.4, 0.0, 1.0) # hell = eher Saum
				bild.set_pixel(x, y, f)


## Rechteck der durchsichtigen Mitte (in Bildpunkten).
static func _loch_rechteck(rahmen: Image) -> Rect2i:
	var b := rahmen.get_width()
	var mx := b / 2
	var x0 := mx
	while x0 > 0 and rahmen.get_pixel(x0 - 1, mx).a < 0.5: x0 -= 1
	var x1 := mx
	while x1 < b - 1 and rahmen.get_pixel(x1 + 1, mx).a < 0.5: x1 += 1
	var y0 := mx
	while y0 > 0 and rahmen.get_pixel(mx, y0 - 1).a < 0.5: y0 -= 1
	var y1 := mx
	while y1 < b - 1 and rahmen.get_pixel(mx, y1 + 1).a < 0.5: y1 += 1
	return Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1)


## Weiß -> durchsichtig. Von den Bildrändern aus füllen wir alles Helle (Außenfläche) weich weg; eingeschlossenes
## Weiß (zwischen gekreuzten Werkzeugen) fällt über die Helligkeit allein. Die Farbe wird vom Weiß befreit
## (kein heller Saum) und mit 4 × 4 Mittelung auf 256 px verkleinert.
func _motiv_freistellen(bild: Image) -> Image:
	var b := bild.get_width()
	var h := bild.get_height()
	var daten := bild.get_data()
	var hellwert := PackedFloat32Array()
	hellwert.resize(b * h)
	for i in b * h:
		hellwert[i] = minf(daten[i * 4], minf(daten[i * 4 + 1], daten[i * 4 + 2])) / 255.0
	# Außenfläche von allen Randpixeln aus.
	var aussen := PackedByteArray()
	aussen.resize(b * h)
	var stapel := PackedInt32Array()
	for x in b:
		stapel.append(x)
		stapel.append((h - 1) * b + x)
	for y in h:
		stapel.append(y * b)
		stapel.append(y * b + b - 1)
	while not stapel.is_empty():
		var i: int = stapel[stapel.size() - 1]
		stapel.resize(stapel.size() - 1)
		if aussen[i] != 0 or hellwert[i] < 0.85:
			continue
		aussen[i] = 1
		var x := i % b
		var y := i / b
		if x > 0: stapel.append(i - 1)
		if x < b - 1: stapel.append(i + 1)
		if y > 0: stapel.append(i - b)
		if y < h - 1: stapel.append(i + b)
	var alpha := PackedFloat32Array()
	alpha.resize(b * h)
	for i in b * h:
		var m := hellwert[i]
		if aussen[i] != 0:
			alpha[i] = clampf((0.93 - m) / 0.08, 0.0, 1.0)
		else:
			alpha[i] = clampf((0.96 - m) / 0.10, 0.0, 1.0)
	# Verkleinern (vormultipliziert), dabei Weiß aus der Farbe herausrechnen.
	var f := b / GROESSE
	var ziel := PackedByteArray()
	ziel.resize(GROESSE * GROESSE * 4)
	for y in GROESSE:
		for x in GROESSE:
			var summe_a := 0.0
			var r := 0.0
			var g := 0.0
			var bl := 0.0
			for dy in f:
				for dx in f:
					var i := (y * f + dy) * b + (x * f + dx)
					var a := alpha[i]
					if a <= 0.0:
						continue
					# Entmischen: p = a * c + (1 - a) * weiß
					var cr := clampf((daten[i * 4] / 255.0 - (1.0 - a)) / a, 0.0, 1.0)
					var cg := clampf((daten[i * 4 + 1] / 255.0 - (1.0 - a)) / a, 0.0, 1.0)
					var cb := clampf((daten[i * 4 + 2] / 255.0 - (1.0 - a)) / a, 0.0, 1.0)
					summe_a += a
					r += cr * a
					g += cg * a
					bl += cb * a
			var j := (y * GROESSE + x) * 4
			if summe_a > 0.0:
				ziel[j] = int(clampf(r / summe_a, 0.0, 1.0) * 255.0)
				ziel[j + 1] = int(clampf(g / summe_a, 0.0, 1.0) * 255.0)
				ziel[j + 2] = int(clampf(bl / summe_a, 0.0, 1.0) * 255.0)
			ziel[j + 3] = int(clampf(summe_a / float(f * f), 0.0, 1.0) * 255.0)
	return Image.create_from_data(GROESSE, GROESSE, false, Image.FORMAT_RGBA8, ziel)


# ---------- bauen ----------

## Beruf und Stufe je Dateiname aus wirtschaft.json: { "werkzeug": ["Schmied", 2], ... }
func _waren_aus_daten() -> Dictionary:
	var text := FileAccess.get_file_as_string(_wurzel.path_join("game/daten/wirtschaft.json"))
	var json: Variant = JSON.parse_string(text)
	var ergebnis := {}
	for betrieb in (json["betriebsarten"] as Dictionary).values():
		if not betrieb.has("produkte"):
			continue
		var stufe: int = int(betrieb.get("stufe", 1))
		for ware in betrieb["produkte"]:
			var key := dateiname(ware)
			if not ergebnis.has(key) or ergebnis[key][1] > stufe:
				ergebnis[key] = [betrieb["beruf"], stufe]
	return ergebnis


func _knopf(key: String, waren: Dictionary, teile: Dictionary) -> Image:
	var beruf := ""
	var stufe := 0
	var hintergrund := ROHSTOFF_HINTERGRUND
	if waren.has(key):
		beruf = waren[key][0]
		stufe = waren[key][1]
		hintergrund = HINTERGRUND[beruf]
	var bg: Image = _teil(teile, "hintergrund", hintergrund)
	var motiv: Image = _teil(teile, "motiv", key)
	var rahmen: Image = _teil(teile, "rahmen", RAHMEN[stufe])
	if bg == null or motiv == null or rahmen == null:
		return null
	var bild := bg.duplicate() as Image
	var loch := _loch_rechteck(rahmen)
	var m := motiv.duplicate() as Image
	m.resize(loch.size.x, loch.size.y, Image.INTERPOLATE_LANCZOS)
	bild.blend_rect(m, Rect2i(0, 0, m.get_width(), m.get_height()), loch.position)
	bild.blend_rect(rahmen, Rect2i(0, 0, GROESSE, GROESSE), Vector2i.ZERO)
	bild.resize(ENDGROESSE, ENDGROESSE, Image.INTERPOLATE_LANCZOS)
	return bild


func _teil(teile: Dictionary, art: String, name: String) -> Image:
	var schluessel := art + "/" + name
	if not teile.has(schluessel):
		var pfad := _quellordner(art).path_join(name + ".png")
		teile[schluessel] = _laden(pfad) if FileAccess.file_exists(pfad) else null
		if teile[schluessel] == null:
			push_error("Teil fehlt: " + pfad)
	return teile[schluessel]


func _bauen(namen: PackedStringArray) -> void:
	var waren := _waren_aus_daten()
	var teile := {}
	var ziel := _wurzel.path_join("game/assets/oberflaeche/icons")
	if namen.is_empty():
		namen = _dateien(_quellordner("motiv"))
	for key in namen:
		var knopf := _knopf(key, waren, teile)
		if knopf != null:
			knopf.save_png(ziel.path_join(key + ".png"))
			print("knopf: ", key)


func _tafel(zieldatei: String, namen: PackedStringArray) -> void:
	var waren := _waren_aus_daten()
	var teile := {}
	if namen.is_empty():
		namen = _dateien(_quellordner("motiv"))
	var spalten := 4
	var zeilen := ceili(namen.size() / float(spalten))
	var zeilenhoehe := ENDGROESSE + 8 + 64 + 12
	var tafel := Image.create(spalten * (ENDGROESSE + 16) + 16, zeilen * zeilenhoehe + 16, false, Image.FORMAT_RGBA8)
	tafel.fill(Color(0.85, 0.8, 0.7))
	for i in namen.size():
		var knopf := _knopf(namen[i], waren, teile)
		if knopf == null:
			continue
		var x := 16 + (i % spalten) * (ENDGROESSE + 16)
		var y := 16 + (i / spalten) * zeilenhoehe
		tafel.blend_rect(knopf, Rect2i(0, 0, ENDGROESSE, ENDGROESSE), Vector2i(x, y))
		var klein := knopf.duplicate() as Image
		klein.resize(64, 64, Image.INTERPOLATE_LANCZOS)
		tafel.blend_rect(klein, Rect2i(0, 0, 64, 64), Vector2i(x, y + ENDGROESSE + 8))
	tafel.save_png(zieldatei)
	print("tafel: ", zieldatei)
