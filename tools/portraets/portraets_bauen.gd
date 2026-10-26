# Bereitet die Porträts (Rohbilder der Bild-KI) für das Spiel auf und setzt Vorschauen zusammen.
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
#          masken/<name>.webp      Aus Farbvarianten <name>_haargruen (nur Haar, Brauen, Bart grün umgefärbt) und
#                                  <name>_augenblau (nur Iris blau): Der Farbabstand zum Original ergibt die Maske
#                                  (Rot = Haar-Anteil, Grün = Augen-Anteil, 0-255), siehe _maske_aus_unterschied.
#          Namen der Personen: <kürzel>_<alter>_<kleidung>, z. B. m1_jung_schlicht, f3_alt_vornehm, m2_mittel_zu (Augen zu).
# spiel:   Schreibt die Dateien für das Spiel nach game/assets/oberflaeche/portraets/ (WebP, 512 px; Personen und Hintergründe verlustbehaftet mit Alpha, Masken verlustfrei):
#          <volk>/<geschlecht>_<gesicht 0-4>_<alter>_<kleidung|zu>.webp, dazu ..._haar.webp und ..._augen.webp (Graustufenmasken,
#          weiß = Haar bzw. Iris) und hintergrund_<stand>.webp. Kürzel m1..m5 / f1..f5 werden zu mann_0..4 / frau_0..4.
#          volk ist "mensch" (Vorgabe); die Quellen der anderen Völker kommen mit eigenem Kürzel-Präfix (siehe _spielname).
# raster:  Legt Einträge in ein Raster (zur Abnahme). Ein Eintrag ist "person@hintergrund", z. B. m1_jung_schlicht@hg_buerger;
#          ohne "@" nur die Person auf Grau, "-" lässt eine Zelle leer. Mit Maske (<person> in masken/) lassen sich
#          Haar und Augen umfärben: "person@hintergrund@haar-hex@augen-hex", z. B. m1_jung_schlicht@hg_buerger@d8b45a@3a7bd5
#          (ein Feld leer lassen = unverändert, z. B. "person@hg@@3a7bd5").
extends SceneTree

const GROESSE := 512
const TASCHE_MAX := 6000      # größte Papiertasche (in Pixeln des 1024er Rohbilds), die entfernt wird
const TASCHE_DUNKEL := 0.55   # Anteil dunkler Randpixel, ab dem eine helle Fläche als Papiertasche gilt
const SPIEL_QUALITAET := 0.85  # Dateien für das Spiel (Vorgabe des Teamleiters: etwa 85)
const WEBP_QUALITAET := 0.92  # verlustbehaftetes WebP mit Alpha: ~30 KB je Porträt statt ~400 KB als PNG
const HG_RAND := 0.045      # Anteil, der vom Hintergrundbild rundum abgeschnitten wird (Gemini malt dort einen Rand)
const RAND_ABSCHNITT := 0.075  # bei Papierrahmen um die Person (Rahmenlinie liegt bei etwa 5 %)
const HG_WEICH := 160       # Hintergrund wird auf diese Kantenlänge verkleinert und wieder hochgezogen (weiche Schärfe)
const GRUEN_VON := 0.15      # Farbabstand zum Schlüsselgrün (0–1): darunter ganz durchsichtig …
const GRUEN_BIS := 0.40      # … darüber ganz deckend; dazwischen weich (Haarsträhnen, Kanten)
const RAND_TIEFE := 14       # so viele Pixel (1024er Rohbild) reicht die weiche Randzone von außen ins Bild – nur durch helle/graue Pixel
const RAND_DUNKEL := 0.42    # dunkler als das (Hellwert) gilt als Linie: Dort endet die Randzone, dahinter (Haut) bleibt alles deckend
const RAND_WEICH := 0.38     # in der Randzone: so viel dunkler als das Papier ist ein Pixel voll deckend
const INSEL_NAH := 48        # Papierinseln (zwischen Locken) höchstens so weit (Pixel, 1024er Rohbild) von der Außenfläche gelten als Hintergrund
const PAPIER_NAH := 30       # so nah (Pixel, 1024er Rohbild) an der Außenfläche gilt jedes papierfarbene Pixel als Hintergrund
const PAPIER_SATT := 0.035   # … wenn es so ungesättigt ist wie Papier (max − min der Farbkanäle); Haut ist farbiger
const PAPIER_ABSTAND := 0.035 # … und höchstens so viel dunkler als das Papier (Glanz auf der Haut liegt bei etwa −0,04 und darunter)
const HAUT_HELL := 0.35      # Randpixel einer hellen Fläche gilt als Haut, wenn es heller als das ist …
const HAUT_SATT := 0.05      # … und farbiger (max − min) – Haarstriche sind dunkel, Papier ist ungesättigt
const HAUT_ANTEIL := 0.30    # ab so viel Haut am Rand ist eine helle Fläche Glanz auf der Haut und bleibt
const PAPIER_FLECK_MAX := 2500  # papierweiße Stelle am Rand bis zu dieser Größe (Pixel, 1024er Rohbild) gilt immer als Hintergrund
const STRICH_KLEIN := 0.15   # so viel Strich braucht ein kleiner Fleck am Rand mindestens
const STRICH_DUNKEL := 0.55  # Randpixel gilt als Strich (Haar, Linie), wenn dunkler als das
const STRICH_ANTEIL := 0.40  # so viel Strich muss am Rand einer weißen Stelle sein, damit sie als Papier zwischen Strähnen gilt
const INSEL_RAND := 0.75     # … wenn so viel ihres Randes kein Papier ist (Haarstriche, auch die hellen Kanten der Striche)

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
	DirAccess.make_dir_recursive_absolute(_quellordner("masken"))
	var namen := PackedStringArray()
	for datei in DirAccess.get_files_at(roh):
		if datei.get_extension().to_lower() in ["png", "jpg", "jpeg", "webp"] and not datei.begins_with("_"):
			var n := datei.get_basename()
			if nur.is_empty() or n in nur:
				namen.append(datei)
	namen.sort()
	for datei in namen:
		var name := datei.get_basename()
		if name.ends_with("_haargruen") or name.ends_with("_augenblau"):
			continue  # Farbvarianten: werden unten zu Masken
		var bild := _laden(roh.path_join(datei))
		if bild == null:
			continue
		if name.begins_with("hg_"):
			bild = _hintergrund_aufbereiten(bild)
			if name == "hg_adel":
				_abdunkeln(bild)
			bild.save_webp(_quellordner("hintergruende").path_join(name + ".webp"), true, WEBP_QUALITAET)
		else:
			var beschneiden := false
			var haar := _finde(roh, name + "_haargruen")
			var augen := _finde(roh, name + "_augenblau")
			if _hat_papierrand(bild):
				bild = _papierrand_abschneiden(bild)
				beschneiden = true
			if haar != "" or augen != "":
				# Varianten wie das Original behandeln (gleicher Beschnitt), sonst liegt die Maske daneben.
				var vh := _laden(haar) if haar != "" else null
				var va := _laden(augen) if augen != "" else null
				if beschneiden:
					vh = _papierrand_abschneiden(vh) if vh != null else null
					va = _papierrand_abschneiden(va) if va != null else null
				var maske := _maske_aus_unterschied(bild, vh, va)
				maske.save_webp(_quellordner("masken").path_join(name + ".webp"), false)
			# Gibt es eine Fassung mit Chroma-Grün statt Papierweiß (roh/gruen/<name>), wird die freigestellt: Weiß vor Weiß
			# (weißes Haar, Pelz, Schleier) lässt sich vom Papier nicht sicher trennen, vom Grün schon.
			var gruen_pfad := _finde(roh.path_join("gruen"), name)
			if gruen_pfad != "":
				var gruen := _laden(gruen_pfad)
				if gruen.get_size() != Vector2i(1024, 1024):
					gruen.resize(1024, 1024, Image.INTERPOLATE_LANCZOS)
				if beschneiden:
					gruen = _papierrand_abschneiden(gruen)
				bild = _freistellen_gruen(gruen, bild)
			else:
				bild = _freistellen(bild)
			bild.save_webp(_quellordner("personen").path_join(name + ".webp"), true, WEBP_QUALITAET)
		print("quelle: ", name)


## Masken aus Farbvarianten: Die Bild-KI färbt im Bearbeiten-Modus nur das Haar (grün) bzw. die Iris (blau) um und lässt
## alles andere unverändert. Je größer der Farbabstand zum Original, desto größer der Maskenanteil.
## Rot = Haar (mit Brauen und Bart), Grün = Iris, jeweils 0-255, auf 512 px verkleinert. Nur Pixel, die in der Variante
## wirklich grün (Haar) bzw. blau (Iris) sind, zählen: So fallen kleine Änderungen der Bild-KI an Haut und Schraffur heraus.
func _maske_aus_unterschied(orig: Image, haar: Image, augen: Image) -> Image:
	var b := orig.get_width()
	var ziel := PackedByteArray()
	ziel.resize(b * b * 4)
	var d := orig.get_data()
	var dh := haar.get_data() if haar != null else PackedByteArray()
	var da := augen.get_data() if augen != null else PackedByteArray()
	for i in b * b:
		if haar != null:
			ziel[i * 4] = int(_abstand(d, dh, i) * smoothstep(0.04, 0.20, (dh[i * 4 + 1] - maxi(dh[i * 4], dh[i * 4 + 2])) / 255.0) * 255.0)
		if augen != null:
			ziel[i * 4 + 1] = int(_abstand(d, da, i) * smoothstep(0.04, 0.20, (da[i * 4 + 2] - da[i * 4]) / 255.0) * 255.0)
		ziel[i * 4 + 3] = 255
	var m := Image.create_from_data(b, b, false, Image.FORMAT_RGBA8, ziel)
	m.resize(GROESSE, GROESSE, Image.INTERPOLATE_BILINEAR)
	return m


static func _abstand(a: PackedByteArray, c: PackedByteArray, i: int) -> float:
	var dr := (a[i * 4] - c[i * 4]) / 255.0
	var dg := (a[i * 4 + 1] - c[i * 4 + 1]) / 255.0
	var db := (a[i * 4 + 2] - c[i * 4 + 2]) / 255.0
	return smoothstep(0.16, 0.36, sqrt(dr * dr + dg * dg + db * db))


## Färbt die Bereiche der Maske um, ohne Linien und Schattierung zu verlieren: Der Zielton wird mit dem Helligkeitsverlauf
## des Originals (bezogen auf den Mittelwert im Bereich) multipliziert.
func _umfaerben(person: Image, maske: Image, kanal: int, ziel: Color) -> void:
	var b := person.get_width()
	# Bezugshelligkeit: Mittel der Flächenfarbe im Bereich, ohne schwarze Schraffurlinien und ohne helle Haut.
	var summe := 0.0
	var gewicht := 0.0
	for y in b:
		for x in b:
			var w: float = maske.get_pixel(x, y)[kanal]
			if w > 0.05:
				var l := person.get_pixel(x, y).get_luminance()
				if l > 0.12 and l < 0.62:
					summe += l * w
					gewicht += w
	if gewicht < 1.0:
		return
	var mittel := maxf(summe / gewicht, 0.05)
	for y in b:
		for x in b:
			var w: float = maske.get_pixel(x, y)[kanal]
			if w <= 0.0:
				continue
			var p := person.get_pixel(x, y)
			# Iris-Maske deckt oft das ganze Auge: Das Weiße des Auges (kaum Farbsättigung) bleibt unverändert.
			if kanal == 1:
				w *= smoothstep(0.12, 0.32, p.s)
			var faktor := p.get_luminance() / mittel
			# Dunkle Linien bleiben dunkel (Faktor < 1), Lichter hellen auf, aber höchstens bis Weiß.
			var neu := Color(clampf(ziel.r * faktor, 0.0, 1.0), clampf(ziel.g * faktor, 0.0, 1.0), clampf(ziel.b * faktor, 0.0, 1.0), p.a)
			person.set_pixel(x, y, p.lerp(neu, clampf(w, 0.0, 1.0)))


## Manchmal malt Gemini trotz Auftrag einen Papierrahmen mit Pergamentrand um das Bild (m5_*). Ist der Rand
## erkennbar nicht weiß, wird er samt Rahmenlinie abgeschnitten und das Bild wieder auf Ausgangsgröße gezogen.
func _hat_papierrand(bild: Image) -> bool:
	var b := bild.get_width()
	var summe := 0.0
	var n := 0
	var ring := int(b * 0.012)
	for i in range(0, b, 16):
		for p in [Vector2i(i, ring), Vector2i(ring, i), Vector2i(i, b - 1 - ring), Vector2i(b - 1 - ring, i)]:
			var c := bild.get_pixelv(p)
			summe += minf(c.r, minf(c.g, c.b))
			n += 1
	return summe / n < 0.6


func _papierrand_abschneiden(bild: Image) -> Image:
	var b := bild.get_width()
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
## Helles (helle Haut, Auge, Hemd) bleibt deckend, weil die schwarze Umrisslinie die Figur abschließt. Die Farbe wird vom Weiß befreit
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
	# Papierinseln zwischen Locken am Rand der Figur zählen zur Außenfläche (siehe _papiertaschen_entfernen).
	var nah := _abstand_zu(aussen, INSEL_NAH, b, h)
	var weg := _papiertaschen_entfernen(aussen, hellwert, daten, papier, nah, b, h)
	for i in weg:
		aussen[i] = 1
	# Nah an der Außenfläche ist papierfarbenes Weiß Hintergrund – auch wenn es über helle Lichter im Haar mit dem Gesicht
	# zusammenhängt und darum keine eigene Insel ist (weißer Schleier zwischen den Locken). Aber nur Flächen, die von Haar
	# oder Papier umgeben sind: Ein heller Glanz auf der Wange ist von Haut umgeben und bleibt (sonst Loch im Gesicht).
	var kandidat := PackedByteArray()
	kandidat.resize(b * h)
	for i in b * h:
		if aussen[i] == 0 and nah[i] >= 0 and nah[i] <= PAPIER_NAH and hellwert[i] >= papier - PAPIER_ABSTAND:
			var hoechst := maxf(daten[i * 4], maxf(daten[i * 4 + 1], daten[i * 4 + 2])) / 255.0
			if hoechst - hellwert[i] <= PAPIER_SATT:
				kandidat[i] = 1
	for start in b * h:
		if kandidat[start] != 1:
			continue
		var helle := PackedInt32Array()
		var weg_stapel := PackedInt32Array([start])
		kandidat[start] = 2
		var rand_haut := 0
		var rand_dunkel := 0
		var rand_gesamt := 0
		while not weg_stapel.is_empty():
			var i: int = weg_stapel[weg_stapel.size() - 1]
			weg_stapel.resize(weg_stapel.size() - 1)
			helle.append(i)
			var x := i % b
			for j in [i - 1 if x > 0 else -1, i + 1 if x < b - 1 else -1, i - b if i >= b else -1, i + b if i < (h - 1) * b else -1]:
				if j < 0:
					continue
				if kandidat[j] == 1:
					kandidat[j] = 2
					weg_stapel.append(j)
				elif kandidat[j] == 0 and aussen[j] == 0:
					rand_gesamt += 1
					var hoch := maxf(daten[j * 4], maxf(daten[j * 4 + 1], daten[j * 4 + 2])) / 255.0
					if hellwert[j] > HAUT_HELL and hoch - hellwert[j] > HAUT_SATT:
						rand_haut += 1
					if hellwert[j] < STRICH_DUNKEL:
						rand_dunkel += 1
		# Papier zwischen Strähnen grenzt an dunkle Striche; Glanz auf Haut oder weißem Pelz grenzt an Haut bzw. helles Grau.
		# Kleine Flecken (Papier zwischen Strähnen) oder Flächen mit vielen dunklen Strichen am Rand – große helle Flächen
		# ohne Striche ringsum sind weißer Pelz oder Stoff und bleiben.
		# Klein und wenigstens ein paar dunkle Striche am Rand (Papier zwischen braunen Strähnen); weißes Haar grenzt nur an
		# helles Grau und bleibt.
		var klein := helle.size() < PAPIER_FLECK_MAX and rand_gesamt > 0 and float(rand_dunkel) / rand_gesamt >= STRICH_KLEIN
		var strichig := rand_gesamt > 0 and float(rand_dunkel) / rand_gesamt >= STRICH_ANTEIL
		if rand_gesamt == 0 or (float(rand_haut) / rand_gesamt < HAUT_ANTEIL and (klein or strichig)):
			for i in helle:
				aussen[i] = 1
	# Randzone: von der Außenfläche aus höchstens RAND_TIEFE Pixel weit, nur durch Pixel heller als eine Linie. So werden die
	# grauen Lücken zwischen feinen Haarsträhnen erreicht (bisher deckend hellgrau = weißer Schleier), die Haut hinter der
	# schwarzen Umrisslinie aber nicht.
	var tiefe := PackedInt32Array()
	tiefe.resize(b * h)
	tiefe.fill(-1)
	var welle := PackedInt32Array()
	for i in b * h:
		if aussen[i] != 0:
			tiefe[i] = 0
			welle.append(i)
	for schritt in RAND_TIEFE:
		var naechste := PackedInt32Array()
		for i in welle:
			var x := i % b
			var y := i / b
			for j in [i - 1 if x > 0 else -1, i + 1 if x < b - 1 else -1, i - b if y > 0 else -1, i + b if y < h - 1 else -1]:
				if j < 0 or tiefe[j] >= 0 or hellwert[j] < RAND_DUNKEL:
					continue
				tiefe[j] = schritt + 1
				naechste.append(j)
		welle = naechste
	var alpha := PackedFloat32Array()
	alpha.resize(b * h)
	for i in b * h:
		var m := hellwert[i]
		if aussen[i] != 0:
			alpha[i] = clampf((papier - 0.20 - m) / 0.10, 0.0, 1.0)
		elif tiefe[i] > 0:
			# Randzone: Deckkraft nach dem Abstand zur Papierhelligkeit – helles Grau zwischen Strähnen wird durchsichtig.
			alpha[i] = clampf((papier - 0.04 - m) / RAND_WEICH, 0.0, 1.0)
		else:
			# Alles, was nicht mit dem Außenpapier verbunden ist, bleibt deckend (helle Haut hat fast Papierweiß).
			alpha[i] = 1.0
	var papierfarbe := _papierfarbe(daten, aussen, hellwert, papier, b, h)
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
					# Entmischen gegen die echte (leicht cremefarbene) Papierfarbe: p = a * c + (1 - a) * papier
					var cr := clampf((daten[i * 4] / 255.0 - (1.0 - a) * papierfarbe.r) / a, 0.0, 1.0)
					var cg := clampf((daten[i * 4 + 1] / 255.0 - (1.0 - a) * papierfarbe.g) / a, 0.0, 1.0)
					var cb := clampf((daten[i * 4 + 2] / 255.0 - (1.0 - a) * papierfarbe.b) / a, 0.0, 1.0)
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


## Freistellen vor Chroma-Grün: Die Deckkraft folgt dem Farbabstand zum Grün (Mischpixel an Haarsträhnen werden weich),
## die Farbe wird gegen das Grün entmischt, und grüner Schimmer an halbdurchsichtigen Kanten wird entfernt (Grün höchstens so
## stark wie Rot bzw. Blau). Grüne Kleidung bleibt deckend: Sie ist dunkler und matter als das leuchtende Schlüsselgrün.
func _freistellen_gruen(bild: Image, orig: Image) -> Image:
	var b := bild.get_width()
	var h := bild.get_height()
	var daten := bild.get_data()
	# Die Farbe kommt aus dem Original auf Papier (die Grün-Fassung verschiebt die Farben leicht), die Deckkraft aus der
	# Grün-Fassung. Entmischt wird gegen die Papierfarbe des Originals.
	if orig.get_size() != bild.get_size():
		orig.resize(b, h, Image.INTERPOLATE_LANCZOS)
	var odaten := orig.get_data()
	var ohell := PackedFloat32Array()
	ohell.resize(b * h)
	for i in b * h:
		ohell[i] = minf(odaten[i * 4], minf(odaten[i * 4 + 1], odaten[i * 4 + 2])) / 255.0
	var oaussen := PackedByteArray()
	oaussen.resize(b * h)
	var opapier := _randhelligkeit(ohell, b, h)
	for i in b * h:
		var gc := Vector3(daten[i * 4], daten[i * 4 + 1], daten[i * 4 + 2]) / 255.0
		if gc.y > 0.6 and gc.x < 0.5 and gc.z < 0.5:
			oaussen[i] = 1
	var papier := _papierfarbe(odaten, oaussen, ohell, opapier, b, h)
	var pv := Vector3(papier.r, papier.g, papier.b)
	# Schlüsselgrün: Mittel der deutlich grünen Pixel am ganzen Bildrand (Ecken, die die Person verdeckt, zählen nicht).
	var schluessel := Vector3.ZERO
	var n := 0
	var randpunkte: Array[Vector2i] = []
	for x in range(0, b, 8):
		randpunkte.append(Vector2i(x, 2))
		randpunkte.append(Vector2i(x, h - 3))
	for y in range(0, h, 8):
		randpunkte.append(Vector2i(2, y))
		randpunkte.append(Vector2i(b - 3, y))
	for p in randpunkte:
		var i: int = (p.y * b + p.x) * 4
		var c := Vector3(daten[i], daten[i + 1], daten[i + 2]) / 255.0
		if c.y > 0.6 and c.x < 0.5 and c.z < 0.5:
			schluessel += c
			n += 1
	schluessel = schluessel / n if n > 0 else Vector3(0.0, 1.0, 0.0)
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
					var i := ((y * f + dy) * b + (x * f + dx)) * 4
					var c := Vector3(daten[i], daten[i + 1], daten[i + 2]) / 255.0
					var a := smoothstep(GRUEN_VON, GRUEN_BIS, (c - schluessel).length() / sqrt(3.0))
					if a <= 0.0:
						continue
					var oc := Vector3(odaten[i], odaten[i + 1], odaten[i + 2]) / 255.0
					var fa := ((oc - pv * (1.0 - a)) / a).clamp(Vector3.ZERO, Vector3.ONE)
					summe_a += a
					r += fa.x * a
					g += fa.y * a
					bl += fa.z * a
			var j := (y * GROESSE + x) * 4
			if summe_a > 0.0:
				ziel[j] = int(clampf(r / summe_a, 0.0, 1.0) * 255.0)
				ziel[j + 1] = int(clampf(g / summe_a, 0.0, 1.0) * 255.0)
				ziel[j + 2] = int(clampf(bl / summe_a, 0.0, 1.0) * 255.0)
			ziel[j + 3] = int(clampf(summe_a / float(f * f), 0.0, 1.0) * 255.0)
	return Image.create_from_data(GROESSE, GROESSE, false, Image.FORMAT_RGBA8, ziel)


## Mittlere Farbe des Außenpapiers (nur sicher papierhelle Pixel), zum Entmischen der Randfarben.
static func _papierfarbe(daten: PackedByteArray, aussen: PackedByteArray, hellwert: PackedFloat32Array, papier: float,
		b: int, h: int) -> Color:
	var r := 0.0
	var g := 0.0
	var bl := 0.0
	var n := 0
	for i in range(0, b * h, 7):
		if aussen[i] != 0 and hellwert[i] >= papier - 0.03:
			r += daten[i * 4]
			g += daten[i * 4 + 1]
			bl += daten[i * 4 + 2]
			n += 1
	if n == 0:
		return Color(1, 1, 1)
	return Color(r / n / 255.0, g / n / 255.0, bl / n / 255.0)


## Zwischen Haarsträhnen und Locken am Rand bleiben kleine Papierflächen eingeschlossen (sie hängen nicht mit dem Außenpapier
## zusammen) und würden als weiße Fransen stehen. Eine zusammenhängende fast weiße Fläche gilt als Hintergrund, wenn sie klein ist und
## entweder überwiegend an dunkle Pixel grenzt oder nah an der Außenfläche liegt (höchstens INSEL_NAH) und ihr Rand fast nur aus
## Nicht-Papier besteht (Haarstriche samt ihrer hellen Kanten). Weiter innen (das Weiße der Augen, helle Haut) bleibt alles deckend.
## Gibt die Pixel der Flächen zurück.
func _papiertaschen_entfernen(aussen: PackedByteArray, hellwert: PackedFloat32Array, daten: PackedByteArray, papier: float,
		nah: PackedInt32Array, b: int, h: int) -> PackedInt32Array:
	var weg := PackedInt32Array()
	var besucht := PackedByteArray()
	besucht.resize(b * h)
	for start in b * h:
		if besucht[start] != 0 or aussen[start] != 0 or hellwert[start] < papier - 0.05:
			continue
		var flaeche := PackedInt32Array()
		var stapel := PackedInt32Array([start])
		besucht[start] = 1
		var rand_dunkel := 0
		var rand_kein_papier := 0
		var rand_strich := 0
		var rand_haut := 0
		var rand_gesamt := 0
		var alle_nah := true
		while not stapel.is_empty():
			var i: int = stapel[stapel.size() - 1]
			stapel.resize(stapel.size() - 1)
			flaeche.append(i)
			if nah[i] < 0:
				alle_nah = false
			var x := i % b
			var y := i / b
			for nb in [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]]:
				if nb[0] < 0 or nb[1] < 0 or nb[0] >= b or nb[1] >= h:
					continue
				var j: int = nb[1] * b + nb[0]
				if hellwert[j] >= papier - 0.05 and aussen[j] == 0:
					if besucht[j] == 0:
						besucht[j] = 1
						stapel.append(j)
				elif aussen[j] == 0:
					rand_gesamt += 1
					var lum := (0.2126 * daten[j * 4] + 0.7152 * daten[j * 4 + 1] + 0.0722 * daten[j * 4 + 2]) / 255.0
					if lum < 0.40:
						rand_dunkel += 1
					if hellwert[j] < papier - 0.15:
						rand_kein_papier += 1
					if hellwert[j] < STRICH_DUNKEL:
						rand_strich += 1
					var hoch := maxf(daten[j * 4], maxf(daten[j * 4 + 1], daten[j * 4 + 2])) / 255.0
					if hellwert[j] > HAUT_HELL and hoch - hellwert[j] > HAUT_SATT:
						rand_haut += 1
		if rand_gesamt == 0 or float(rand_haut) / rand_gesamt >= HAUT_ANTEIL:
			continue   # Glanz auf der Haut (von Haut umgeben) bleibt deckend
		# Kleine Flächen mit dunklem Rand; am Rand der Figur auch große, langgezogene (Papier zwischen den Locken innerhalb
		# einer Außenlinie um das ganze Haar) – sie liegen ja vollständig in der schmalen Zone nah an der Außenfläche.
		var dunkel := flaeche.size() < TASCHE_MAX and float(rand_dunkel) / rand_gesamt > TASCHE_DUNKEL
		var am_rand := alle_nah and float(rand_kein_papier) / rand_gesamt > INSEL_RAND \
				and float(rand_strich) / rand_gesamt >= STRICH_ANTEIL
		if dunkel or am_rand:
			weg.append_array(flaeche)
	return weg


## Abstand (4er-Nachbarschaft, in Pixeln) jedes Pixels zur markierten Fläche, höchstens bis grenze; weiter weg: -1.
static func _abstand_zu(markiert: PackedByteArray, grenze: int, b: int, h: int) -> PackedInt32Array:
	var abstand := PackedInt32Array()
	abstand.resize(b * h)
	abstand.fill(-1)
	var welle := PackedInt32Array()
	for i in b * h:
		if markiert[i] != 0:
			abstand[i] = 0
			welle.append(i)
	for schritt in grenze:
		var naechste := PackedInt32Array()
		for i in welle:
			var x := i % b
			for j in [i - 1 if x > 0 else -1, i + 1 if x < b - 1 else -1, i - b if i >= b else -1, i + b if i < (h - 1) * b else -1]:
				if j >= 0 and abstand[j] < 0:
					abstand[j] = schritt + 1
					naechste.append(j)
		welle = naechste
	return abstand


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
