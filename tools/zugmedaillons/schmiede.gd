extends SceneTree
# Zug-Medaillons mit geschmiedetem Metallring: gewölbtes Profil, Hammerschlag-Dellen, Schleifspuren, Kerben von Schwert-/Axthieben.
# Gerechnet in 256 px, gespeichert in 128 px. Kerben je Zug an anderer Stelle (aus dem Namen gewürfelt), Licht immer oben links.
# Aufruf: godot --headless -s tools/zugmedaillons/schmiede.gd -- <quellordner> <zielordner>
#   <quellordner>/<kennung>.jpg oder .png: die gemalten Bilder (Gemini/ChatGPT, quadratisch, Motiv in der Mitte)
#   <zielordner>: dorthin kommen <kennung>.png (128 px) und uebersicht.png – im Spiel game/assets/oberflaeche/zuege/
const N := 256
const W := 22.0   # Ringbreite in 256 px (≈ 5,5 px bei 64)
var quelle := ""
var ziel := ""

func _farbe(art: String) -> Color:
	match art:
		"kupfer": return Color(0.74, 0.40, 0.25)
		"gold": return Color(0.98, 0.76, 0.30)
		_: return Color(0.58, 0.60, 0.63)

func _init():
	var args := OS.get_cmdline_user_args()
	if args.size() < 2:
		printerr("Aufruf: -- <quellordner> <zielordner>"); quit(1); return
	quelle = args[0].trim_suffix("/") + "/"; ziel = args[1].trim_suffix("/") + "/"
	var art := {}
	for k in ["gierig","grosszuegig","fleissig","traege","fromm","zynisch","mutig","feige","ehrlich","verschlagen","geduldig","jaehzornig","gesellig","scheu","bescheiden","hochmuetig"]: art[k] = "kupfer"
	for k in ["klug","stark","schoen","kraenklich"]: art[k] = "gold"
	for k in ["trinker","narbe","pilger","gelehrt"]: art[k] = "eisen"
	var licht := Vector3(-1, -1, 1.3).normalized()
	var halb := (licht + Vector3(0, 0, 1)).normalized()
	var dellen := FastNoiseLite.new(); dellen.noise_type = FastNoiseLite.TYPE_CELLULAR; dellen.frequency = 0.06
	var schliff := FastNoiseLite.new(); schliff.noise_type = FastNoiseLite.TYPE_SIMPLEX; schliff.frequency = 0.9
	var tafel := Image.create(8 * 136, 3 * 136, false, Image.FORMAT_RGBA8); tafel.fill(Color(0.16, 0.14, 0.12))
	var i := 0
	for k in art:
		var basis: Color = _farbe(art[k])
		var rng := RandomNumberGenerator.new(); rng.seed = hash(k)
		dellen.seed = rng.randi(); schliff.seed = rng.randi()
		# Kerben: Winkel, halbe Breite (Bogenmaß), Tiefe (Anteil der Ringbreite)
		var kerben := []
		var anzahl := rng.randi_range(2, 3)
		var start := rng.randf() * TAU
		for n in anzahl:
			kerben.append([start + n * TAU / anzahl + rng.randf_range(-0.5, 0.5), rng.randf_range(0.06, 0.11), rng.randf_range(0.65, 1.05)])
		var pfad: String = quelle + k + ".jpg"
		if not FileAccess.file_exists(pfad): pfad = quelle + k + ".png"
		var foto := Image.load_from_file(pfad); foto.convert(Image.FORMAT_RGBA8)
		var m := N / 2.0; var R := m - 1.0; var ri := R - W
		var rb := ri + W * 0.35   # Bild reicht unter den Ring
		var s := int(rb * 2.0 * 1.08); foto.resize(s, s, Image.INTERPOLATE_LANCZOS)
		var o := int(m - s / 2.0)
		var bild := Image.create(N, N, false, Image.FORMAT_RGBA8); bild.fill(Color(0, 0, 0, 0))
		for y in N:
			for x in N:
				var v := Vector2(x + 0.5 - m, y + 0.5 - m); var d := v.length()
				var c := Color(0, 0, 0, 0)
				if d < rb + 1.0 and x - o >= 0 and y - o >= 0 and x - o < s and y - o < s:
					var f: Color = foto.get_pixel(x - o, y - o)
					var dunkel := 1.0 - 0.3 * pow(clampf(d / rb, 0.0, 1.0), 4.0)
					c = Color(f.r * dunkel, f.g * dunkel, f.b * dunkel, clampf(rb + 1.0 - d, 0.0, 1.0))
				if d >= ri - 1.0 and d <= R + 1.0:
					var winkel := atan2(v.y, v.x)
					# Kerbe: Abstand zur Kerbenkante (in Pixeln), > 0 = Metall weggeschlagen
					var weg := -999.0
					for kb in kerben:
						var dw: float = abs(wrapf(winkel - kb[0], -PI, PI))
						var tiefe: float = kb[2] * W * (1.0 - dw / kb[1])
						weg = maxf(weg, tiefe - (R - d))
					var t := clampf((d - ri) / W, 0.0, 1.0)
					var radial := v / maxf(d, 0.001)
					var tang := Vector2(-radial.y, radial.x)
					# gewölbtes Profil, dazu Dellen (zelliges Rauschen) und Schleifspuren entlang des Rings
					var neig := (t - 0.5) * 1.7
					var delle := dellen.get_noise_2d(x, y)
					var spur := schliff.get_noise_2d(winkel * 40.0, d * 0.15)
					var n2 := radial * neig + radial * delle * 0.75 + tang * dellen.get_noise_2d(x + 37.0, y - 11.0) * 0.6
					var n := Vector3(n2.x, n2.y, 1.0).normalized()
					var diffus := maxf(n.dot(licht), 0.0)
					var glanz := pow(maxf(n.dot(halb), 0.0), 40.0)
					# Spiegelung: oben hell, unten dunkel, mit scharfem Horizont (wirkt metallisch)
					var refl := n.y
					var umgebung := 0.55 + 0.45 * smoothstep(0.08, -0.08, refl) - 0.12 * smoothstep(-0.1, 0.4, refl)
					var hell := (0.30 + 0.85 * diffus) * umgebung + spur * 0.10
					var mc := Color(basis.r * hell + glanz * 0.9, basis.g * hell + glanz * 0.85, basis.b * hell + glanz * 0.75)
					# Kanten des Rings dunkel
					var kante := minf(d - ri, R - d)
					if kante < 2.0: mc = mc.lerp(Color(0.05, 0.04, 0.03), (2.0 - kante) / 2.0 * 0.8)
					var a := clampf(minf(d - ri + 1.0, R + 1.0 - d), 0.0, 1.0)
					if weg > -2.5:
						if weg > 0.0: a = 0.0   # weggeschlagen
						else:
							# blank geschlagene Schnittkante: hell, kaum Farbe
							var blank := Color(0.92, 0.90, 0.86).lerp(basis, 0.25)
							mc = mc.lerp(blank, (2.5 + weg) / 2.5 * 0.85)
					if weg > 0.0 and weg < 1.5:
						mc = Color(0.08, 0.07, 0.06); a = (1.5 - weg) / 1.5 * 0.7   # dunkler Grund der Kerbe
					if a > 0.0:
						c = Color(mc.r, mc.g, mc.b, 1.0).lerp(c, 0.0) if c.a <= 0.0 else c.lerp(Color(mc.r, mc.g, mc.b, 1.0), a)
						c.a = maxf(c.a, a)
				bild.set_pixel(x, y, c)
		var klein := bild.duplicate(); klein.resize(128, 128, Image.INTERPOLATE_LANCZOS)
		klein.save_png(ziel + k + ".png")
		var b64 := bild.duplicate(); b64.resize(64, 64, Image.INTERPOLATE_LANCZOS)
		tafel.blend_rect(klein, Rect2i(0, 0, 128, 128), Vector2i((i % 8) * 136 + 4, (i / 8) * 136 + 4))
		i += 1
	tafel.save_png(ziel + "uebersicht.png"); quit()
