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
# Mit --rahmen=0.07 wird der Rahmen auf 7 % der Kantenlänge gestaucht (wie 9-Slice).
# Mit --ohne-effekte (an beliebiger Stelle) ohne Lichthof, Saum und Schatten (Vorher-Bild).
# einrichtung: Rohbilder der Einrichtung (<Ordner>/<Kennung>.png, Motiv auf Weiß) freistellen und als
#          quellen/motiv/einrichtung_<kennung>.png ablegen. "bauen"/"tafel" setzen sie dann zusammen: Rahmen nach Reihe
#          (1 Stein, 2 Silber, 3 Gold), Hintergrund nach Spalte (siehe EINR_HINTERGRUND); Spalte und Reihe je Kennung
#          stehen in game/daten/wirtschaft.json (werkstatt.einrichtung, wohnen.einrichtung). Ausgabe einrichtung_<kennung>.png.
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
# Hintergrund der Einrichtung nach Spalte des Talentbaums
const EINR_HINTERGRUND := {
	"Werkstatt": "ockerbraun", "Lager & Handel": "bernstein", "Schutz & Personal": "schiefergrau",
	"Haushalt": "bernstein", "Ansehen": "purpur", "Schutz": "schiefergrau",
}
const EINR_RAHMEN := ["", "stein", "silber", "gold"]  # Index = Reihe (Gebäudestufe)
const ROHSTOFF_HINTERGRUND := "lehmbraun"
const RAHMEN := ["holz", "stein", "silber", "gold"]  # Index = Stufe (0 = Rohstoff)

# Hebt das Motiv vom Hintergrund ab (Lichthof, heller Saum, Schlagschatten). "--ohne-effekte" schaltet es ab.
var _effekte := true
# Rahmenbreite als Anteil der Kantenlänge (Standard 7 %; 0 = so wie gemalt). "--rahmen=0.07" staucht die Randstreifen.
var _rahmenanteil := 0.07
var _wurzel: String


func _init() -> void:
	# --script läuft im Projektordner (game/); das Repository liegt eine Ebene höher.
	_wurzel = ProjectSettings.globalize_path("res://").path_join("..").simplify_path()
	var arg := Array(OS.get_cmdline_user_args())
	for a in arg.duplicate():
		if str(a).begins_with("--rahmen="):
			_rahmenanteil = str(a).trim_prefix("--rahmen=").to_float()
			arg.erase(a)
	if arg.has("--ohne-effekte"):
		_effekte = false
		arg.erase("--ohne-effekte")
	if arg.is_empty():
		print("Aufruf: quellen <Ordner> | bauen [namen] | tafel <ziel.png> [namen]")
		quit(1)
		return
	match arg[0]:
		"quellen": _quellen(arg[1])
		"einrichtung": _einrichtung_quellen(arg[1])
		"bauen": _bauen(PackedStringArray(arg.slice(1)))
		"tafel": _tafel(arg[1], PackedStringArray(arg.slice(2)))
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


func _einrichtung_quellen(roh: String) -> void:
	DirAccess.make_dir_recursive_absolute(_quellordner("motiv"))
	for name in _dateien(roh):
		var bild := _laden(_finde(roh, name))
		if bild == null:
			continue
		bild = _motiv_freistellen(bild)
		var ziel := "einrichtung_" + dateiname(name)
		bild.save_png(_quellordner("motiv").path_join(ziel + ".png"))
		print("quelle: motiv/", ziel)


## Spalte und kleinste Reihe je Kennung: { "einrichtung_<kennung>": [spalte, reihe] } (die erste Fundstelle gilt).
func _einrichtung_aus_daten() -> Dictionary:
	var text := FileAccess.get_file_as_string(_wurzel.path_join("game/daten/wirtschaft.json"))
	var json: Variant = JSON.parse_string(text)
	var stuecke: Array = []
	for liste in (json["werkstatt"]["einrichtung"] as Dictionary).values():
		stuecke.append_array(liste)
	stuecke.append_array(json["wohnen"]["einrichtung"])
	var ergebnis := {}
	for e in stuecke:
		var key := "einrichtung_" + dateiname(e["kennung"])
		if not ergebnis.has(key):
			ergebnis[key] = [e.get("spalte", "Werkstatt"), int(e.get("abStufe", 1))]
	return ergebnis


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
		rechts.append(_erster_dunkler(bild, b - 1, p, -1, 0))
		oben.append(_erster_dunkler(bild, p, 0, 0, 1))
		unten.append(_erster_dunkler(bild, p, b - 1, 0, -1))
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


## Mittlere Helligkeit (kleinster Farbkanal) der Randpixel, höchstens 1.
static func _randhelligkeit(hellwert: PackedFloat32Array, b: int, h: int) -> float:
	var werte: Array = []
	for x in range(0, b, 8):
		werte.append(hellwert[x])
		werte.append(hellwert[(h - 1) * b + x])
	for y in range(0, h, 8):
		werte.append(hellwert[y * b])
		werte.append(hellwert[y * b + b - 1])
	werte.sort()
	return minf(werte[werte.size() * 3 / 4], 1.0)  # oberes Viertel: Papier, nicht die Ränder von Rahmenteilen


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
	# Papierhelligkeit: Gemini malt oft cremefarbenes statt reinem Weiß – Mittelwert der Randpixel als Maß.
	var papier := _randhelligkeit(hellwert, b, h)
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
		if aussen[i] != 0 or hellwert[i] < papier - 0.12:
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
			alpha[i] = clampf((papier - 0.03 - m) / 0.08, 0.0, 1.0)
		else:
			alpha[i] = clampf((papier - 0.01 - m) / 0.10, 0.0, 1.0)
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
	var rahmenname := ""
	var einrichtung := _einrichtung_aus_daten() if key.begins_with("einrichtung_") else {}
	if einrichtung.has(key):
		stufe = clampi(einrichtung[key][1], 1, 3)
		hintergrund = EINR_HINTERGRUND.get(einrichtung[key][0], "schiefergrau")
		rahmenname = EINR_RAHMEN[stufe]
	elif waren.has(key):
		beruf = waren[key][0]
		stufe = waren[key][1]
		hintergrund = HINTERGRUND[beruf]
	var bg: Image = _teil(teile, "hintergrund", hintergrund)
	var motiv: Image = _teil(teile, "motiv", key)
	var rahmen: Image = _teil(teile, "rahmen", rahmenname if rahmenname != "" else RAHMEN[stufe])
	if bg == null or motiv == null or rahmen == null:
		return null
	var bild := bg.duplicate() as Image
	var loch := _loch_rechteck(rahmen)
	if _rahmenanteil > 0.0:
		var breite := roundi(GROESSE * _rahmenanteil)
		rahmen = _rahmen_schmal(rahmen, loch, breite)
		loch = Rect2i(breite, breite, GROESSE - 2 * breite, GROESSE - 2 * breite)
	var m := motiv.duplicate() as Image
	m.resize(loch.size.x, loch.size.y, Image.INTERPOLATE_LANCZOS)
	if _effekte:
		_lichthof(bild, 1.3 if stufe == 0 and not waren.has(key) else 1.0)
		# Motiv auf eine volle Fläche setzen, damit Saum und Schatten nicht am Lochrand abgeschnitten werden.
		var flaeche := Image.create(GROESSE, GROESSE, false, Image.FORMAT_RGBA8)
		flaeche.blend_rect(m, Rect2i(0, 0, m.get_width(), m.get_height()), loch.position)
		var maske := _alpha_maske(flaeche)
		# Schlagschatten: nach unten rechts versetzt (3 px bei 128 px), weich, 35 %.
		_ebene(bild, _weich(_versetzt(maske, 6, 6), 4), Color(0.0, 0.0, 0.0), 0.35)
		# Heller Saum: Maske 5 px (= 2,5 px bei 128) erweitert und weich, warmweiß, 60 %.
		# Je heller der Motivrand, desto schwächer der Saum (helle Motive heben sich ohnehin ab).
		var rand := _randhelligkeit_motiv(maske, flaeche)
		var saum := lerpf(0.6, 0.25, smoothstep(0.35, 0.65, rand))
		_ebene(bild, _weich(_erweitert(maske, 5), 2), Color(1.0, 0.95, 0.82), saum)
		bild.blend_rect(flaeche, Rect2i(0, 0, GROESSE, GROESSE), Vector2i.ZERO)
	else:
		bild.blend_rect(m, Rect2i(0, 0, m.get_width(), m.get_height()), loch.position)
	bild.blend_rect(rahmen, Rect2i(0, 0, GROESSE, GROESSE), Vector2i.ZERO)
	bild.resize(ENDGROESSE, ENDGROESSE, Image.INTERPOLATE_LANCZOS)
	return bild


## Mittlere Helligkeit der Motivkante (deckende Pixel, die bei Erosion um 6 px wegfallen).
static func _randhelligkeit_motiv(maske: PackedFloat32Array, flaeche: Image) -> float:
	var umkehr := PackedFloat32Array()
	umkehr.resize(maske.size())
	for i in maske.size():
		umkehr[i] = 1.0 - maske[i]
	var innen := _erweitert(umkehr, 6)  # > 0, wo in 6 px Umkreis etwas Durchsichtiges liegt
	var summe := 0.0
	var n := 0
	var b := flaeche.get_width()
	for y in b:
		for x in b:
			var i := y * b + x
			if maske[i] > 0.8 and innen[i] > 0.5:
				summe += flaeche.get_pixel(x, y).get_luminance()
				n += 1
	return summe / n if n > 0 else 0.5


## Staucht die gemalten Randstreifen auf die Breite t (9-Slice): Ecken auf t x t, Seiten auf t Breite.
func _rahmen_schmal(rahmen: Image, loch: Rect2i, t: int) -> Image:
	var b := rahmen.get_width()
	var l := loch.position.x
	var o := loch.position.y
	var r := b - loch.end.x
	var u := b - loch.end.y
	var mb := b - 2 * t  # neue Mittenbreite
	var aus := Image.create(b, b, false, Image.FORMAT_RGBA8)
	# [Quelle (x, y, w, h), Ziel (x, y, w, h)]
	var teile := [
		[Rect2i(0, 0, l, o), Rect2i(0, 0, t, t)],
		[Rect2i(l, 0, loch.size.x, o), Rect2i(t, 0, mb, t)],
		[Rect2i(b - r, 0, r, o), Rect2i(b - t, 0, t, t)],
		[Rect2i(0, o, l, loch.size.y), Rect2i(0, t, t, mb)],
		[Rect2i(b - r, o, r, loch.size.y), Rect2i(b - t, t, t, mb)],
		[Rect2i(0, b - u, l, u), Rect2i(0, b - t, t, t)],
		[Rect2i(l, b - u, loch.size.x, u), Rect2i(t, b - t, mb, t)],
		[Rect2i(b - r, b - u, r, u), Rect2i(b - t, b - t, t, t)],
	]
	for teil in teile:
		var quelle: Rect2i = teil[0]
		var ziel: Rect2i = teil[1]
		var stueck := rahmen.get_region(quelle)
		stueck.resize(ziel.size.x, ziel.size.y, Image.INTERPOLATE_LANCZOS)
		aus.blit_rect(stueck, Rect2i(Vector2i.ZERO, stueck.get_size()), ziel.position)
	return aus


## Hellt den Hintergrund in der Mitte auf (etwa +35 %) und dunkelt zu den Ecken leicht ab (etwa -15 %).
func _lichthof(bild: Image, extra: float) -> void:
	var b := bild.get_width()
	var mitte := (b - 1) / 2.0
	var maxabstand := mitte * 1.4142
	for y in b:
		for x in b:
			var d := Vector2(x - mitte, y - mitte).length() / maxabstand
			var f := (1.35 - 0.5 * smoothstep(0.0, 1.0, d)) * extra
			var c := bild.get_pixel(x, y)
			bild.set_pixel(x, y, Color(minf(c.r * f, 1.0), minf(c.g * f, 1.0), minf(c.b * f, 1.0), c.a))


static func _alpha_maske(bild: Image) -> PackedFloat32Array:
	var daten := bild.get_data()
	var maske := PackedFloat32Array()
	maske.resize(bild.get_width() * bild.get_height())
	for i in maske.size():
		maske[i] = daten[i * 4 + 3] / 255.0
	return maske


## Verschiebt die Maske um (dx, dy) Pixel.
static func _versetzt(maske: PackedFloat32Array, dx: int, dy: int) -> PackedFloat32Array:
	var b := int(sqrt(maske.size()))
	var aus := PackedFloat32Array()
	aus.resize(maske.size())
	for y in b:
		for x in b:
			var qx := x - dx
			var qy := y - dy
			if qx >= 0 and qy >= 0 and qx < b and qy < b:
				aus[y * b + x] = maske[qy * b + qx]
	return aus


## Erweitert die Maske um r Pixel (Maximum im Quadrat, zwei Durchgänge).
static func _erweitert(maske: PackedFloat32Array, r: int) -> PackedFloat32Array:
	var b := int(sqrt(maske.size()))
	var zwischen := PackedFloat32Array()
	zwischen.resize(maske.size())
	for y in b:
		for x in b:
			var m := 0.0
			for k in range(maxi(0, x - r), mini(b, x + r + 1)):
				m = maxf(m, maske[y * b + k])
			zwischen[y * b + x] = m
	var aus := PackedFloat32Array()
	aus.resize(maske.size())
	for y in b:
		for x in b:
			var m := 0.0
			for k in range(maxi(0, y - r), mini(b, y + r + 1)):
				m = maxf(m, zwischen[k * b + x])
			aus[y * b + x] = m
	return aus


## Weichzeichnen (Mittelwert in 2 Durchgängen je Richtung).
static func _weich(maske: PackedFloat32Array, r: int) -> PackedFloat32Array:
	var b := int(sqrt(maske.size()))
	var quelle := maske
	for _durchgang in 2:
		var zwischen := PackedFloat32Array()
		zwischen.resize(quelle.size())
		for y in b:
			for x in b:
				var summe := 0.0
				var n := 0
				for k in range(maxi(0, x - r), mini(b, x + r + 1)):
					summe += quelle[y * b + k]
					n += 1
				zwischen[y * b + x] = summe / n
		var aus := PackedFloat32Array()
		aus.resize(quelle.size())
		for y in b:
			for x in b:
				var summe := 0.0
				var n := 0
				for k in range(maxi(0, y - r), mini(b, y + r + 1)):
					summe += zwischen[k * b + x]
					n += 1
				aus[y * b + x] = summe / n
		quelle = aus
	return quelle


## Legt eine einfarbige Ebene mit der Maske als Durchsicht (mal Deckkraft) auf das Bild.
static func _ebene(bild: Image, maske: PackedFloat32Array, farbe: Color, deckkraft: float) -> void:
	var b := bild.get_width()
	for y in b:
		for x in b:
			var a := clampf(maske[y * b + x] * deckkraft, 0.0, 1.0)
			if a <= 0.0:
				continue
			var c := bild.get_pixel(x, y)
			bild.set_pixel(x, y, Color(c.r + (farbe.r - c.r) * a, c.g + (farbe.g - c.g) * a, c.b + (farbe.b - c.b) * a, c.a))


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
