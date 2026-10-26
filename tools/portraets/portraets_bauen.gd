# Bereitet die Porträts (Gemini-Rohbilder) für das Spiel auf und setzt Vorschauen zusammen.
#
# Aufruf (aus dem Repository-Ordner, ohne Fenster):
#   godot --headless --path game --script ../tools/portraets/portraets_bauen.gd -- quellen <Rohbild-Ordner> [dateiname ...]
#   godot --headless --path game --script ../tools/portraets/portraets_bauen.gd -- spiel [volk]
#   godot --headless --path game --script ../tools/portraets/portraets_bauen.gd -- raster <ziel.png> <Spalten> <Zelle> <eintrag ...>
#   godot --headless --path game --script ../tools/portraets/portraets_bauen.gd -- stapeln <ziel.png> <bild.png ...>
#
# quellen: Liest die Rohbilder (1024 px, JPG/PNG) und legt unter tools/portraets/quellen/ ab:
#          personen/<name>.webp    Person freigestellt (Weiß wird durchsichtig, ohne hellen Saum), 512 px mit Alpha.
#          hintergruende/<name>.webp  Dateien mit "hg_" am Anfang: Rand abgeschnitten, 512 px, absichtlich weich (leicht unscharf).
#          masken/<name>.webp      Dateien mit "_maske" am Ende (Gemini-Fassung mit Haar in Magenta und Iris in Cyan):
#                                  Rot = Haar-Anteil, Grün = Augen-Anteil (0-255), siehe _maske_aufbereiten.
#          Namen der Personen: <kürzel>_<alter>_<kleidung>, z. B. m1_jung_schlicht, f3_alt_vornehm, m2_mittel_zu (Augen zu).
# spiel:   Schreibt die Dateien für das Spiel nach game/assets/oberflaeche/portraets/ (WebP, 512 px; Personen und Hintergründe verlustbehaftet mit Alpha, Masken verlustfrei):
#          <volk>/<geschlecht>_<gesicht 0-4>_<alter>_<kleidung|zu>.webp, dazu ..._haar.webp und ..._augen.webp (Graustufenmasken,
#          weiß = Haar bzw. Iris) und hintergrund_<stand>.webp. Kürzel m1..m5 / f1..f5 werden zu mann_0..4 / frau_0..4.
#          volk ist "mensch" (Vorgabe); die Quellen der anderen Völker kommen mit eigenem Kürzel-Präfix (siehe _spielname).
# raster:  Legt Einträge in ein Raster (zur Abnahme). Ein Eintrag ist "person@hintergrund", z. B. m1_jung_schlicht@hg_buerger;
#          ohne "@" nur die Person auf Grau, "-" lässt eine Zelle leer. Mit Maske (<person>_maske in masken/) lassen sich
#          Haar und Augen umfärben: "person@hintergrund@haar-hex@augen-hex", z. B. m1_jung_schlicht@hg_buerger@d8b45a@3a7bd5
#          (ein Feld leer lassen = unverändert, z. B. "person@hg@@3a7bd5").
extends SceneTree

const GROESSE := 512
const SPIEL_QUALITAET := 0.85  # Dateien für das Spiel (Vorgabe des Teamleiters: etwa 85)
const WEBP_QUALITAET := 0.92  # verlustbehaftetes WebP mit Alpha: ~30 KB je Porträt statt ~400 KB als PNG
const HG_RAND := 0.045      # Anteil, der vom Hintergrundbild rundum abgeschnitten wird (Gemini malt dort einen Rand)
const RAND_ABSCHNITT := 0.075  # bei Papierrahmen um die Person (Rahmenlinie liegt bei etwa 5 %)
const HG_WEICH := 160       # Hintergrund wird auf diese Kantenlänge verkleinert und wieder hochgezogen (weiche Schärfe)

var _wurzel: String


func _init() -> void:
	# --script läuft im Projektordner (game/); das Repository liegt eine Ebene höher.
	_wurzel = ProjectSettings.globalize_path("res://").path_join("..").simplify_path()
	var arg := Array(OS.get_cmdline_user_args())
	if arg.is_empty():
		print("Aufruf: quellen <Ordner> [namen] | raster <ziel.png> <Spalten> <Zelle> <einträge>")
		quit(1)
		return
	match arg[0]:
		"quellen": _quellen(arg[1], PackedStringArray(arg.slice(2)))
		"spiel": _spiel(arg[1] if arg.size() > 1 else "mensch")
		"stapeln": _stapeln(arg[1], PackedStringArray(arg.slice(2)))
		"raster": _raster(arg[1], int(arg[2]), int(arg[3]), PackedStringArray(arg.slice(4)))
		_: print("Unbekannter Befehl: ", arg[0])
	quit()


# ---------- Hilfen ----------

func _quellordner(art: String) -> String:
	return _wurzel.path_join("tools/portraets/quellen").path_join(art)


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


func _person(name: String) -> Image:
	return _laden(_quellordner("personen").path_join(name + ".webp"))


func _hintergrund(name: String) -> Image:
	return _laden(_quellordner("hintergruende").path_join(name + ".webp"))


# ---------- quellen ----------

func _quellen(roh: String, nur: PackedStringArray) -> void:
	DirAccess.make_dir_recursive_absolute(_quellordner("personen"))
	DirAccess.make_dir_recursive_absolute(_quellordner("hintergruende"))
	var namen := PackedStringArray()
	for datei in DirAccess.get_files_at(roh):
		if datei.get_extension().to_lower() in ["png", "jpg", "jpeg", "webp"] and not datei.begins_with("_"):
			var n := datei.get_basename()
			if nur.is_empty() or n in nur:
				namen.append(datei)
	namen.sort()
	for datei in namen:
		var name := datei.get_basename()
		var bild := _laden(roh.path_join(datei))
		if bild == null:
			continue
		if name.ends_with("_maske"):
			DirAccess.make_dir_recursive_absolute(_quellordner("masken"))
			var vorlage := _laden(_finde(roh, name.trim_suffix("_maske")))
			bild = _maske_aufbereiten(bild, vorlage, name.contains("_alt_"))
			bild.save_webp(_quellordner("masken").path_join(name.trim_suffix("_maske") + ".webp"), false)
		elif name.begins_with("hg_"):
			bild = _hintergrund_aufbereiten(bild)
			if name == "hg_adel":
				_abdunkeln(bild)
			bild.save_webp(_quellordner("hintergruende").path_join(name + ".webp"), true, WEBP_QUALITAET)
		else:
			bild = _freistellen(_papierrand_abschneiden(bild))
			bild.save_webp(_quellordner("personen").path_join(name + ".webp"), true, WEBP_QUALITAET)
		print("quelle: ", name)


## Aus der Magenta/Cyan-Fassung zwei weiche Masken: Rot = Haar (Magenta: Rot und Blau hoch, Grün tief),
## Grün = Iris (Cyan: Grün und Blau hoch, Rot tief). Auf 512 px verkleinert.
## Gemini ändert in der Maskenfassung gelegentlich Stirnfransen oder Haarsaum leicht. Deshalb wird die Haar-Maske mit dem
## Original abgeglichen: Helle, hautfarbene Pixel (Helligkeit über 0,62, mit weichem Übergang bis 0,78) zählen nicht
## zum Haar. Bei "alt" (weißes Haar) gibt es keine Haar-Maske.
func _maske_aufbereiten(bild: Image, vorlage: Image, ohne_haar: bool) -> Image:
	var b := bild.get_width()
	var daten := bild.get_data()
	var daten_v := vorlage.get_data() if vorlage != null else PackedByteArray()
	var ziel := PackedByteArray()
	ziel.resize(b * b * 4)
	for i in b * b:
		var r: float = daten[i * 4] / 255.0
		var g: float = daten[i * 4 + 1] / 255.0
		var bl: float = daten[i * 4 + 2] / 255.0
		var haar := clampf((minf(r, bl) - g - 0.40) / 0.30, 0.0, 1.0)
		if ohne_haar:
			haar = 0.0
		elif vorlage != null and haar > 0.0:
			var licht := (0.2126 * daten_v[i * 4] + 0.7152 * daten_v[i * 4 + 1] + 0.0722 * daten_v[i * 4 + 2]) / 255.0
			haar *= 1.0 - smoothstep(0.62, 0.78, licht)
		var auge := clampf((minf(g, bl) - r - 0.40) / 0.30, 0.0, 1.0)
		ziel[i * 4] = int(haar * 255.0)
		ziel[i * 4 + 1] = int(auge * 255.0)
		ziel[i * 4 + 3] = 255
	var m := Image.create_from_data(b, b, false, Image.FORMAT_RGBA8, ziel)
	m.resize(GROESSE, GROESSE, Image.INTERPOLATE_BILINEAR)
	return m


## Färbt die Bereiche der Maske um, ohne Linien und Schattierung zu verlieren: Der Zielton wird mit dem Helligkeitsverlauf
## des Originals (bezogen auf den Mittelwert im Bereich) multipliziert.
func _umfaerben(person: Image, maske: Image, kanal: int, ziel: Color) -> void:
	var b := person.get_width()
	var summe := 0.0
	var gewicht := 0.0
	for y in b:
		for x in b:
			var w: float = maske.get_pixel(x, y)[kanal]
			if w > 0.05:
				var p := person.get_pixel(x, y)
				summe += p.get_luminance() * w
				gewicht += w
	if gewicht < 1.0:
		return
	var mittel := maxf(summe / gewicht, 0.05)
	if kanal == 0:
		# Zweiter Durchgang ohne die hellen Hautpixel in der Maske, sonst liegt der Mittelwert zu hoch.
		summe = 0.0
		gewicht = 0.0
		for y in b:
			for x in b:
				var w: float = maske.get_pixel(x, y)[kanal]
				var l := person.get_pixel(x, y).get_luminance()
				if w > 0.05 and l < mittel * 1.5:
					summe += l * w
					gewicht += w
		mittel = maxf(summe / maxf(gewicht, 0.001), 0.05)
	for y in b:
		for x in b:
			var w: float = maske.get_pixel(x, y)[kanal]
			if w <= 0.0:
				continue
			var p := person.get_pixel(x, y)
			# Haut zwischen Haarsträhnen (deutlich heller als das Haar) bleibt unverändert.
			if kanal == 0:
				w *= 1.0 - smoothstep(mittel * 1.7, mittel * 2.3, p.get_luminance())
			var faktor := p.get_luminance() / mittel
			# Dunkle Linien bleiben dunkel (Faktor < 1), Lichter hellen auf, aber höchstens bis Weiß.
			var neu := Color(clampf(ziel.r * faktor, 0.0, 1.0), clampf(ziel.g * faktor, 0.0, 1.0), clampf(ziel.b * faktor, 0.0, 1.0), p.a)
			person.set_pixel(x, y, p.lerp(neu, clampf(w, 0.0, 1.0)))


## Manchmal malt Gemini trotz Auftrag einen Papierrahmen mit Pergamentrand um das Bild (m5_*). Ist der Rand
## erkennbar nicht weiß, wird er samt Rahmenlinie abgeschnitten und das Bild wieder auf Ausgangsgröße gezogen.
func _papierrand_abschneiden(bild: Image) -> Image:
	var b := bild.get_width()
	var summe := 0.0
	var n := 0
	var ring := int(b * 0.012)
	for i in range(0, b, 16):
		for p in [Vector2i(i, ring), Vector2i(ring, i), Vector2i(i, b - 1 - ring), Vector2i(b - 1 - ring, i)]:
			var c := bild.get_pixelv(p)
			summe += minf(c.r, minf(c.g, c.b))
			n += 1
	if summe / n > 0.85:
		return bild
	var r := int(b * RAND_ABSCHNITT)
	var ausschnitt := bild.get_region(Rect2i(r, r, b - 2 * r, b - 2 * r))
	ausschnitt.resize(b, b, Image.INTERPOLATE_LANCZOS)
	print("  Papierrand abgeschnitten")
	return ausschnitt


## Gewölbe-Hintergrund (Adel): helle Flächen (das Fenster links) stark dämpfen, dazu eine Vignette, damit helle Gesichter
## vor dem Bild stehen.
func _abdunkeln(bild: Image) -> void:
	var b := bild.get_width()
	for y in b:
		for x in b:
			var c := bild.get_pixel(x, y)
			var licht := smoothstep(0.30, 0.75, c.get_luminance())
			var faktor := lerpf(0.85, 0.40, licht)
			var dx := (x - b * 0.5) / (b * 0.5)
			var dy := (y - b * 0.5) / (b * 0.5)
			faktor *= 1.0 - 0.55 * clampf((dx * dx + dy * dy) * 0.6, 0.0, 1.0)
			bild.set_pixel(x, y, Color(c.r * faktor, c.g * faktor, c.b * faktor, 1.0))


func _hintergrund_aufbereiten(bild: Image) -> Image:
	var b := bild.get_width()
	var r := int(b * HG_RAND)
	var ausschnitt := bild.get_region(Rect2i(r, r, b - 2 * r, b - 2 * r))
	ausschnitt.resize(HG_WEICH, HG_WEICH, Image.INTERPOLATE_LANCZOS)
	ausschnitt.resize(GROESSE, GROESSE, Image.INTERPOLATE_CUBIC)
	return ausschnitt


static func _randhelligkeit(hellwert: PackedFloat32Array, b: int, h: int) -> float:
	var werte: Array = []
	for x in range(0, b, 8):
		werte.append(hellwert[x])
		werte.append(hellwert[(h - 1) * b + x])
	for y in range(0, h, 8):
		werte.append(hellwert[y * b])
		werte.append(hellwert[y * b + b - 1])
	werte.sort()
	return minf(werte[werte.size() * 3 / 4], 1.0)


## Weiß -> durchsichtig. Von den Bildrändern aus füllen wir alles Helle (Außenfläche) weich weg; eingeschlossenes
## Weiß (z. B. Auge, Hemd) fällt nur über die Helligkeit allein und streng. Die Farbe wird vom Weiß befreit
## (kein heller Saum) und mit 2 × 2 Mittelung auf 512 px verkleinert.
func _freistellen(bild: Image) -> Image:
	var b := bild.get_width()
	var h := bild.get_height()
	var daten := bild.get_data()
	var hellwert := PackedFloat32Array()
	hellwert.resize(b * h)
	for i in b * h:
		hellwert[i] = minf(daten[i * 4], minf(daten[i * 4 + 1], daten[i * 4 + 2])) / 255.0
	var papier := _randhelligkeit(hellwert, b, h)
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
		# Innen nur dann durchsichtig, wenn es wirklich Papierweiß ist; Haut und Hemd bleiben deckend.
		if aussen[i] == 0 and m < papier - 0.10:
			alpha[i] = 1.0
		elif aussen[i] == 0:
			alpha[i] = maxf(alpha[i], 0.0)
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


# ---------- raster ----------

func _raster(ziel: String, spalten: int, zelle: int, eintraege: PackedStringArray) -> void:
	var zeilen := (eintraege.size() + spalten - 1) / spalten
	var blatt := Image.create(spalten * zelle, zeilen * zelle, false, Image.FORMAT_RGBA8)
	blatt.fill(Color(0.18, 0.17, 0.16))
	for n in eintraege.size():
		if eintraege[n] == "-":
			continue
		var teile := eintraege[n].split("@")
		var bild: Image
		if teile.size() > 1:
			bild = _hintergrund(teile[1])
		else:
			bild = Image.create(GROESSE, GROESSE, false, Image.FORMAT_RGBA8)
			bild.fill(Color(0.45, 0.45, 0.45))
		var person := _person(teile[0])
		if bild == null or person == null:
			continue
		if teile.size() > 2:
			var maske := _laden(_quellordner("masken").path_join(teile[0] + ".webp"))
			if maske != null and teile[2] != "":
				_umfaerben(person, maske, 0, Color.html(teile[2]))
			if maske != null and teile.size() > 3 and teile[3] != "":
				_umfaerben(person, maske, 1, Color.html(teile[3]))
		bild.blend_rect(person, Rect2i(0, 0, GROESSE, GROESSE), Vector2i.ZERO)
		bild.resize(zelle, zelle, Image.INTERPOLATE_LANCZOS)
		blatt.blit_rect(bild, Rect2i(0, 0, zelle, zelle), Vector2i((n % spalten) * zelle, (n / spalten) * zelle))
	blatt.save_png(ziel)
	print("Raster: ", ziel)


## Stapelt fertige Raster-Bilder untereinander (mittig, mit schmalem Abstand) zu einer Übersichtstafel.
func _stapeln(ziel: String, dateien: PackedStringArray) -> void:
	var bilder: Array[Image] = []
	var breite := 0
	var hoehe := 0
	const ABSTAND := 12
	for d in dateien:
		var bild := _laden(d)
		if bild == null:
			continue
		bilder.append(bild)
		breite = maxi(breite, bild.get_width())
		hoehe += bild.get_height() + ABSTAND
	var blatt := Image.create(breite, hoehe, false, Image.FORMAT_RGBA8)
	blatt.fill(Color(0.1, 0.09, 0.08))
	var y := 0
	for bild in bilder:
		blatt.blit_rect(bild, Rect2i(Vector2i.ZERO, bild.get_size()), Vector2i((breite - bild.get_width()) / 2, y))
		y += bild.get_height() + ABSTAND
	blatt.save_png(ziel)
	print("Tafel: ", ziel)


# ---------- spiel ----------

## m1_jung_schlicht -> mann_0_jung_schlicht (Kürzel: m/f + Nummer 1-5, danach Alter und Kleidung bzw. "zu").
static func _spielname(name: String) -> String:
	var teile := name.split("_")
	var geschlecht := "mann" if teile[0].begins_with("m") else "frau"
	teile[0] = "%s_%d" % [geschlecht, teile[0].substr(1).to_int() - 1]
	return "_".join(teile)


func _spiel(volk: String) -> void:
	var ziel := _wurzel.path_join("game/assets/oberflaeche/portraets")
	DirAccess.make_dir_recursive_absolute(ziel.path_join(volk))
	for datei in DirAccess.get_files_at(_quellordner("hintergruende")):
		var bild := _laden(_quellordner("hintergruende").path_join(datei))
		bild.save_webp(ziel.path_join("hintergrund_" + datei.get_basename().trim_prefix("hg_") + ".webp"), true, SPIEL_QUALITAET)
	for datei in DirAccess.get_files_at(_quellordner("personen")):
		var name := datei.get_basename()
		_laden(_quellordner("personen").path_join(datei)).save_webp(ziel.path_join(volk).path_join(_spielname(name) + ".webp"), true, SPIEL_QUALITAET)
	for datei in DirAccess.get_files_at(_quellordner("masken")):
		var name := _spielname(datei.get_basename())
		var maske := _laden(_quellordner("masken").path_join(datei))
		for kanal in [["haar", 0], ["augen", 1]]:
			if kanal[0] == "haar" and "_alt_" in name:
				continue  # weißes Haar wird nicht umgefärbt
			var grau := Image.create(GROESSE, GROESSE, false, Image.FORMAT_L8)
			for y in GROESSE:
				for x in GROESSE:
					var w: float = maske.get_pixel(x, y)[kanal[1]]
					grau.set_pixel(x, y, Color(w, w, w))
			grau.save_webp(ziel.path_join(volk).path_join(name + "_" + kanal[0] + ".webp"), false)
	print("Spiel-Dateien: ", ziel)
