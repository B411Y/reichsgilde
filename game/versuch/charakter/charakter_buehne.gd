# Versuch Charakter (Phase 0, Phase 3: Belichtung 1,0 statt 1,15, Schlüssellicht 2,3 statt 2,6): Bühne für einen Kopf – eigenes SubViewport mit eigener Welt, Licht wie ein gemaltes
# Porträt (warmes Schlüssellicht links oben, kühles schwaches Fülllicht rechts, warmes Gegenlicht hinten rechts),
# AgX, gleiche Kamera für alle Köpfe. Licht und Kamera stammen aus dem eigenen Porträt-Versuch (hg_fenster.gd).
#
# Phase 6 (S0, Prüfstand): Lichtarten (licht_art_setzen): „spiel“ (L-S, bis S3 genau das Licht der Phase 5),
# „vorlage“ (L-V, wie die Fotovorlagen, für alle Messwerte) und „innen“ (L-I); Anzeige „albedo“ (unbeleuchtet,
# linear); ansicht() mit ziel_x (Nahaufnahmen) und orthografischer Kamera; Schlüssellicht drehbar (Streiflicht);
# Prüfkörper (Graukarte, Hautfeld, Farbtest, Prüfkugel); Kamera- und Kopfdaten für die Begleitdateien der Bögen.
#
# Phase 6 (S3, Porträtlicht): „spiel“ richtet sich nach dem Stand des Kopfs – Stand 5 das Licht der Phase 5 („spiel5“),
# Stand 6 das kalibrierte Porträtlicht („spiel6“): Belichtung 0,8, Fülllicht 0,25, Gegenlicht 0,8, Schlüssellicht
# ≈ 49° aus der Achse, weiche Schatten über die Schattenweichzeichnung, Umgebungslicht und Spiegelung aus dem Studio-HDRI
# (Poly Haven „Studio Small 09“, CC0, ~/werkzeuge/charakter/hdri, nicht im Repository; fehlt es, wie Phase 5); der
# sichtbare Hintergrund bleibt flach. „spiel6_weich“: dasselbe mit Winkelgröße 3° statt Schattenweichzeichnung (Messung).
# „kugeltest“: nur das Schlüssellicht, kein Umgebungslicht, linear abgebildet (Prüfkugeln, S2).
#
# Phase 6 (Halt A, Entscheidungen 31.10.2026): „spiel6“ hebt die Sättigung auf 1,25 (nur diese Porträtbühne, die
# Spielwelt bleibt unverändert; E5 des Nutzers). Das Studio-HDRI gilt nur auf den Stufen Hoch und Ultra (Teamleiter;
# es kostete +0,5 ms ohne Haar, +1,5 ms mit TressFX); Niedrig und Mittel bekommen eine feste Umgebungsfarbe, aus dem
# HDRI gemittelt. L-V „vorlage“: die Aufhellung 1 : 3 ist jetzt ein Lichtverhältnis (Teamleiter): auf der Schattenwange
# kommt ⅓ der Beleuchtung der Lichtwange an (Lambert, Wangen ±40° zur Blickachse); Belichtung neu an der Graukarte.
extends SubViewport

const Kopf := preload("res://versuch/charakter/charakter_kopf.gd")
const FARBTEST := preload("res://versuch/charakter/farbtest.gdshader")
const HAUT3 := preload("res://versuch/charakter/haut3.gdshader")
const HAUT6 := preload("res://versuch/charakter/haut6.gdshader")
const HDRI := "~/werkzeuge/charakter/hdri/studio_small_09_1k.hdr"
# S3: Porträtlicht Stand 6 (L-S)
const SPIEL6_BELICHTUNG := 0.8
const SPIEL6_SCHLUESSEL := 2.3
const SPIEL6_FUELL := 0.25
const SPIEL6_GEGEN := 0.8
# Stärke des HDRI (PanoramaSkyMaterial.energy_multiplier; mittlere Strahldichte des HDRI ≈ 0,87). Über die Stärke des
# Himmels, nicht über ambient_light_energy: Godot backt den Himmel einmal, spätere Änderungen der Umgebungsenergie
# wirkten im Versuch nicht (s3_probe_hdri: 0 und 3 ergaben dasselbe Bild).
const SPIEL6_UMGEBUNG := 0.2
const SPIEL6_WINKEL := 3.0               # Winkelgröße des Schlüssellichts („spiel6_weich“), Grad
const SPIEL6_SAETTIGUNG := 1.25          # Halt A (E5): nur die Porträtbühne Stand 6
const HDRI_STUFEN := ["hoch", "ultra"]   # Halt A (Teamleiter): Studio-HDRI nur auf Hoch und Ultra

var kopf: Kopf
var kamera: Camera3D
var umgebung: Environment
var schluessel: DirectionalLight3D
var fuell: DirectionalLight3D
var gegen: DirectionalLight3D
var licht_art := "spiel"
var anzeige := "normal"
var pruefkoerper := ""
var _pruef: Node3D = null
var stufe := "hoch"         # Grafikstufe der Bühne (niedrig | mittel | hoch | ultra); Bogenschlüssel „stufe“

# L-V „vorlage“ (Prüfplan 5.2): weiches Schlüssellicht 45° links oben (Schattenweichzeichnung 2,0; bis Etappe A
# Winkelgröße 3°, die ein Punktraster in die Schatten streute), Aufhellung 1 : 3 von rechts,
# kein Gegenlicht, Grund 18 % grau, neutrale Umgebung, AgX. Die Belichtung ist so geeicht, dass die Graukarte (18 %,
# zur Kamera) sRGB 118 ± 4 ergibt (Prüfkörper „graukarte“, Bogen P10).
const VORLAGE_SCHLUESSEL := 3.0          # Energie (Lux-Einheit von Godot)
const VORLAGE_UMGEBUNG := 0.12
const VORLAGE_GRUND := 0.18              # Grund: 18 % Reflexion unter demselben Licht (linear, vor der Belichtung)
const VORLAGE_SCHL_RICHTUNG := [-45.0, 45.0]   # Azimut, Höhe (Grad)
const VORLAGE_FUELL_RICHTUNG := [50.0, 10.0]
const VORLAGE_VERHAELTNIS := 3.0         # Lichtverhältnis Lichtwange : Schattenwange (Beleuchtung, Lambert)
const VORLAGE_WANGE_GRAD := 40.0         # Wangennormalen ±40° zur Blickachse (waagerecht)
# Etappe A: Fülllicht 1,0 als Energieverhältnis 1 : 3 (Licht : Schatten der Wange nur 1,15), Belichtung 0,435 (S0).
# Ab Etappe B (Halt A): Fülllicht aus dem Lichtverhältnis (vorlage_fuell(), ≈ 0,45), Belichtung so, dass die
# Graukarte wieder sRGB 118 zeigt: AgX sieht nur Belichtung × Beleuchtung, also 0,435 × E_alt / E_neu der Graukarte.
const VORLAGE_FUELL_ALT := 1.0
const VORLAGE_BELICHTUNG_ALT := 0.435

static func _richtung(azimut: float, hoehe: float) -> Vector3:
	var a := deg_to_rad(azimut)
	var h := deg_to_rad(hoehe)
	return Vector3(sin(a) * cos(h), sin(h), cos(a) * cos(h))

# Fülllicht für das Lichtverhältnis: Lichtwange (−x, zum Schlüssellicht) = K·(n·l) + F·(n·f) + U, Schattenwange (+x)
# entsprechend; F so, dass Licht : Schatten = VORLAGE_VERHAELTNIS
static func vorlage_fuell() -> float:
	var l := _richtung(VORLAGE_SCHL_RICHTUNG[0], VORLAGE_SCHL_RICHTUNG[1])
	var f := _richtung(VORLAGE_FUELL_RICHTUNG[0], VORLAGE_FUELL_RICHTUNG[1])
	var w := deg_to_rad(VORLAGE_WANGE_GRAD)
	var n_licht := Vector3(-sin(w), 0, cos(w))
	var n_schatten := Vector3(sin(w), 0, cos(w))
	var k_l := VORLAGE_SCHLUESSEL * maxf(n_licht.dot(l), 0.0)
	var k_s := VORLAGE_SCHLUESSEL * maxf(n_schatten.dot(l), 0.0)
	var f_l := maxf(n_licht.dot(f), 0.0)
	var f_s := maxf(n_schatten.dot(f), 0.0)
	# (k_l + F f_l + U) = V (k_s + F f_s + U)
	return maxf((k_l + VORLAGE_UMGEBUNG - VORLAGE_VERHAELTNIS * (k_s + VORLAGE_UMGEBUNG)) / (VORLAGE_VERHAELTNIS * f_s - f_l), 0.0)

# Beleuchtung der Graukarte (zur Kamera, Normale +z) durch Schlüssel-, Füll- und Umgebungslicht
static func _vorlage_e(fuell_energie: float) -> float:
	var l := _richtung(VORLAGE_SCHL_RICHTUNG[0], VORLAGE_SCHL_RICHTUNG[1])
	var f := _richtung(VORLAGE_FUELL_RICHTUNG[0], VORLAGE_FUELL_RICHTUNG[1])
	return VORLAGE_SCHLUESSEL * l.z + fuell_energie * f.z + VORLAGE_UMGEBUNG

static func vorlage_belichtung() -> float:
	return VORLAGE_BELICHTUNG_ALT * _vorlage_e(VORLAGE_FUELL_ALT) / _vorlage_e(vorlage_fuell())

func _init(groesse := Vector2i(640, 640)) -> void:
	size = groesse
	own_world_3d = true
	msaa_3d = Viewport.MSAA_4X
	render_target_update_mode = SubViewport.UPDATE_ALWAYS
	positional_shadow_atlas_size = 0
	umgebung = Environment.new()
	umgebung.background_mode = Environment.BG_COLOR
	umgebung.background_color = Color(0.2, 0.18, 0.16)
	umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	umgebung.ambient_light_color = Color(0.62, 0.6, 0.58)
	umgebung.ambient_light_energy = 0.35
	umgebung.ssao_enabled = true
	umgebung.ssao_radius = 0.05
	umgebung.ssao_intensity = 1.4
	umgebung.tonemap_mode = Environment.TONE_MAPPER_AGX
	umgebung.tonemap_exposure = 1.0          # Phase 3: 1,15 -> 1,0 (Haut war überbelichtet)
	var we := WorldEnvironment.new()
	we.environment = umgebung
	add_child(we)
	schluessel = DirectionalLight3D.new()
	schluessel.light_color = Color(1.0, 0.9, 0.78)
	schluessel.light_energy = 2.3
	schluessel.shadow_enabled = true
	schluessel.shadow_blur = 1.2
	schluessel.shadow_normal_bias = 1.0
	schluessel.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	schluessel.directional_shadow_max_distance = 2.0
	add_child(schluessel)
	_richten(schluessel, Vector3(-1.6, 2.6, 1.6))
	fuell = DirectionalLight3D.new()
	fuell.light_color = Color(0.85, 0.9, 1.0)
	fuell.light_energy = 0.45
	add_child(fuell)
	_richten(fuell, Vector3(2.0, 1.7, 1.4))
	gegen = DirectionalLight3D.new()
	gegen.light_color = Color(1.0, 0.82, 0.6)
	gegen.light_energy = 1.6
	add_child(gegen)
	_richten(gegen, Vector3(1.3, 2.2, -2.0))
	kamera = Camera3D.new()
	kamera.fov = 20.0
	kamera.near = 0.02
	add_child(kamera)
	_grund_merken()

# ---------------------------------------------------------------- Lichtarten (Phase 6)
# Grundwerte (Phase 5) einmal merken, damit jede Lichtart von demselben Stand ausgeht
var _grund := {}
func _grund_merken() -> void:
	for n in [schluessel, fuell, gegen]:
		_grund[n] = [n.light_color, n.light_energy, n.transform, n.visible, n.light_angular_distance, n.shadow_blur,
			n.shadow_enabled]
	_grund[umgebung] = [umgebung.background_color, umgebung.ambient_light_color, umgebung.ambient_light_energy,
		umgebung.tonemap_exposure, umgebung.ssao_enabled, umgebung.tonemap_mode, umgebung.ambient_light_source,
		umgebung.reflected_light_source, umgebung.sky, umgebung.adjustment_enabled, umgebung.adjustment_saturation]

func _grund_setzen() -> void:
	for n in [schluessel, fuell, gegen]:
		var g: Array = _grund[n]
		n.light_color = g[0]
		n.light_energy = g[1]
		n.transform = g[2]
		n.visible = g[3]
		n.light_angular_distance = g[4]
		n.shadow_blur = g[5]
		n.shadow_enabled = g[6]
	var u: Array = _grund[umgebung]
	umgebung.background_color = u[0]
	umgebung.ambient_light_color = u[1]
	umgebung.ambient_light_energy = u[2]
	umgebung.tonemap_exposure = u[3]
	umgebung.ssao_enabled = u[4]
	umgebung.tonemap_mode = u[5]
	umgebung.ambient_light_source = u[6]
	umgebung.reflected_light_source = u[7]
	umgebung.sky = u[8]
	umgebung.adjustment_enabled = u[9]
	umgebung.adjustment_saturation = u[10]
	debug_draw = Viewport.DEBUG_DRAW_DISABLED
	if _innen:
		_innen.visible = false

# Licht aus Richtung (Grad): azimut 0 = von vorn, −90 = von links im Bild (−x), +90 = von rechts; hoehe über dem Horizont
static func licht_richten(n: DirectionalLight3D, azimut: float, hoehe: float) -> void:
	var a := deg_to_rad(azimut)
	var h := deg_to_rad(hoehe)
	var von := Vector3(sin(a) * cos(h), sin(h), cos(a) * cos(h))
	n.transform = Transform3D(Basis.looking_at(-von, Vector3.UP if absf(von.y) < 0.99 else Vector3.FORWARD), Vector3.ZERO)

var _innen: Node3D = null
func licht_art_setzen(art: String) -> void:
	licht_art = art
	_grund_setzen()
	_fuell_grund.clear()          # die Lichtart setzt das Fülllicht ganz neu
	if art in ["spiel", ""]:
		art = "spiel6" if kopf and kopf.stand >= 6 else "spiel5"
	if art == "innen" and kopf and kopf.stand >= 6:
		art = "innen6"
	match art:
		"spiel5":
			pass                                   # L-S der Phase 5 (Stand 5)
		"spiel6", "spiel6_weich", "spiel6_b12", "spiel6_ohne_himmel":
			# S3: Porträtlicht Stand 6. Weiche Schatten über die Schattenweichzeichnung (2,0); die Winkelgröße 3°
			# („spiel6_weich“) ließ im Versuch den Schlagschatten von Kopf und Kinn auf dem Kragen ganz verschwinden
			umgebung.tonemap_exposure = SPIEL6_BELICHTUNG
			schluessel.light_energy = SPIEL6_SCHLUESSEL
			fuell.light_energy = SPIEL6_FUELL
			gegen.light_energy = SPIEL6_GEGEN
			if art == "spiel6_weich":
				schluessel.light_angular_distance = SPIEL6_WINKEL
				schluessel.shadow_blur = 1.0
			elif art == "spiel6_b12":
				schluessel.shadow_blur = 1.2          # Messung: Weichzeichnung wie Phase 5 (Kosten von 2,0 mit TressFX)
			else:
				schluessel.shadow_blur = 2.0
			# Halt A (E5): Sättigung 1,25 nur auf dieser Porträtbühne
			umgebung.adjustment_enabled = true
			umgebung.adjustment_saturation = SPIEL6_SAETTIGUNG
			var himmel := _himmel() if art != "spiel6_ohne_himmel" else null   # Messung: Kosten des HDRI
			if himmel and stufe in HDRI_STUFEN:
				umgebung.sky = himmel
				umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
				umgebung.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
				umgebung.ambient_light_energy = 1.0
			elif himmel:
				# Niedrig und Mittel: feste Umgebungsfarbe = Mittel des HDRI × Stärke (keine Spiegelung des Himmels)
				umgebung.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
				umgebung.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
				umgebung.ambient_light_color = _himmel_mittel.linear_to_srgb()
				umgebung.ambient_light_energy = SPIEL6_UMGEBUNG
		"kugeltest":
			# S2: Prüfkugeln – nur das Schlüssellicht (weiß, hart), kein Umgebungslicht, linear, Grund schwarz
			schluessel.light_color = Color(1, 1, 1)
			schluessel.light_energy = 1.0
			schluessel.light_angular_distance = 0.0
			schluessel.shadow_enabled = false
			fuell.visible = false
			gegen.visible = false
			umgebung.ambient_light_energy = 0.0
			umgebung.ambient_light_color = Color(0, 0, 0)
			umgebung.background_color = Color(0, 0, 0)
			umgebung.ssao_enabled = false
			umgebung.tonemap_mode = Environment.TONE_MAPPER_LINEAR
			umgebung.tonemap_exposure = 1.0
		"vorlage", "vorlage_a":
			# „vorlage_a“: L-V der Etappe A (Fülllicht 1,0, Belichtung 0,435) zum Vergleich
			schluessel.light_color = Color(1, 1, 1)
			schluessel.light_energy = VORLAGE_SCHLUESSEL
			# Prüferbefund Etappe A: die Winkelgröße 3° (weiche Schatten über PCSS) streute ein Punktraster in alle
			# Schattenflächen (Schattenwange, Augenhöhle, Ohr, Kiefer, Kragen) und verfälschte Schatten-, Augen- und
			# Feinstrukturwerte. Weich wie L-S über die Schattenweichzeichnung, ohne Winkelgröße.
			schluessel.light_angular_distance = 0.0
			schluessel.shadow_blur = 2.0
			licht_richten(schluessel, VORLAGE_SCHL_RICHTUNG[0], VORLAGE_SCHL_RICHTUNG[1])
			fuell.light_color = Color(1, 1, 1)
			var fe := vorlage_fuell() if art == "vorlage" else VORLAGE_FUELL_ALT
			fuell.light_energy = fe
			licht_richten(fuell, VORLAGE_FUELL_RICHTUNG[0], VORLAGE_FUELL_RICHTUNG[1])
			gegen.visible = false
			umgebung.ambient_light_color = Color(1, 1, 1)
			umgebung.ambient_light_energy = VORLAGE_UMGEBUNG
			umgebung.tonemap_exposure = vorlage_belichtung() if art == "vorlage" else VORLAGE_BELICHTUNG_ALT
			# Grund: wie eine 18-%-Fläche unter Schlüssel- und Fülllicht (Lambert, zur Kamera) – im Bild so hell wie die Graukarte
			var e := _vorlage_e(fe)
			umgebung.background_color = Color(VORLAGE_GRUND * e, VORLAGE_GRUND * e, VORLAGE_GRUND * e).linear_to_srgb()
		"innen", "innen6":
			# L-I: Herdfeuer links vorn, Kerze rechts, kaum Umgebungslicht (Stand 6: Belichtung 1,0 statt 1,25)
			schluessel.visible = false
			fuell.visible = false
			gegen.visible = false
			if _innen == null:
				_innen = Node3D.new()
				add_child(_innen)
				for l in [[Vector3(-0.75, 0.95, 0.75), Color(1.0, 0.58, 0.28), 2.4, 4.0], [Vector3(0.8, 1.15, 0.35), Color(1.0, 0.7, 0.42), 0.7, 3.0]]:
					var o := OmniLight3D.new()
					o.position = l[0]
					o.light_color = l[1]
					o.light_energy = l[2]
					o.omni_range = l[3]
					o.shadow_enabled = true
					o.shadow_normal_bias = 1.5
					_innen.add_child(o)
				positional_shadow_atlas_size = 2048
			_innen.visible = true
			umgebung.background_color = Color(0.03, 0.02, 0.015)
			umgebung.ambient_light_color = Color(0.5, 0.38, 0.28)
			umgebung.ambient_light_energy = 0.12
			umgebung.tonemap_exposure = 1.25 if art == "innen" else 1.0
		_:
			print("WARNUNG Lichtart ", art, " unbekannt")
	if anzeige == "albedo":
		anzeige_setzen("albedo")

# S3: Studio-HDRI als Himmel (nur für Umgebungslicht und Spiegelung; der Hintergrund bleibt die flache Farbe)
static var _himmel_speicher: Sky = null
static var _himmel_geprueft := false
static var _himmel_mittel := Color(0.87, 0.87, 0.87)
static func _himmel() -> Sky:
	if _himmel_geprueft:
		return _himmel_speicher
	_himmel_geprueft = true
	var p := OS.get_environment("HOME").path_join(HDRI.substr(2))
	if not FileAccess.file_exists(p):
		print("WARNUNG Studio-HDRI fehlt: ", p, " (Umgebung wie Phase 5)")
		return null
	var img := Image.load_from_file(p)
	if img == null or img.is_empty():
		print("WARNUNG Studio-HDRI lässt sich nicht laden: ", p)
		return null
	var mat := PanoramaSkyMaterial.new()
	mat.panorama = ImageTexture.create_from_image(img)
	mat.energy_multiplier = SPIEL6_UMGEBUNG
	# Mittel des HDRI (linear) für Niedrig/Mittel: auf 16 × 8 verkleinern und mitteln
	var klein := img.duplicate() as Image
	klein.resize(16, 8, Image.INTERPOLATE_BILINEAR)
	var summe := Color(0, 0, 0)
	for y in 8:
		for x in 16:
			var c := klein.get_pixel(x, y)
			summe = Color(summe.r + c.r, summe.g + c.g, summe.b + c.b)
	_himmel_mittel = Color(summe.r / 128.0, summe.g / 128.0, summe.b / 128.0)
	_himmel_speicher = Sky.new()
	_himmel_speicher.sky_material = mat
	_himmel_speicher.radiance_size = Sky.RADIANCE_SIZE_128
	return _himmel_speicher

# Anzeige „albedo“: unbeleuchtet (Godot: DEBUG_DRAW_UNSHADED), linear abgebildet (kein AgX, Belichtung 1, keine
# Verdeckung), Grund 18 % grau – das Bild zeigt sRGB(ALBEDO).
func anzeige_setzen(art: String) -> void:
	anzeige = art
	if art == "albedo":
		debug_draw = Viewport.DEBUG_DRAW_UNSHADED
		umgebung.tonemap_mode = Environment.TONE_MAPPER_LINEAR
		umgebung.tonemap_exposure = 1.0
		umgebung.ssao_enabled = false
		umgebung.background_color = Color(0.18, 0.18, 0.18).linear_to_srgb()
	else:
		anzeige = "normal"
		licht_art_setzen(licht_art)

# Schlüssellicht aus einer Richtung (Bogen „schluessel_richtung“: [azimut, hoehe], Streiflicht N5)
func schluessel_richtung(azimut: float, hoehe: float) -> void:
	licht_richten(schluessel, azimut, hoehe)

# Phase 3: freie Kamera (game/scripts/FreieKamera.cs) statt der festen, für die Regler-Ansicht (jede Testszene startet
# mit freier Kamera); ansicht() stellt sie dann nur neu hin. Gibt false zurück, wenn es die Kamera nicht gibt.
const FREIE_KAMERA := "res://scripts/FreieKamera.cs"
var freie := false
func freie_kamera_einsetzen() -> bool:
	if not ResourceLoader.exists(FREIE_KAMERA):
		return false
	var skript: Script = load(FREIE_KAMERA)
	var k: Node = skript.new() if skript and skript.can_instantiate() else null
	if not k is Camera3D:
		return false
	var alt := kamera
	k.fov = alt.fov
	k.near = alt.near
	add_child(k)
	k.make_current()
	k.set("Tempo", 0.5)
	kamera = k
	alt.queue_free()
	freie = true
	return true

# Licht von pos auf die Kopfmitte richten (geht auch, bevor die Bühne im Baum hängt)
static func _richten(n: Node3D, pos: Vector3, ziel := Vector3(0, 1.6, 0)) -> void:
	n.transform = Transform3D(Basis.looking_at(ziel - pos), pos)

func kopf_laden(datei: String, st := 5) -> bool:
	kopf = Kopf.new()
	kopf.stand = st
	add_child(kopf)
	return kopf.laden(datei)

# Ansichten: portraet (Kopf, Kragen, Schulteransatz), buste, kopf, augen, mund; winkel = Grad nach rechts gedreht (¾-Ansicht etwa 35)
# spanne_m > 0: feste Bildhöhe in Metern (gleiche Kamera für Köpfe verschiedener Größe), ziel_m = Bildmitte über den Augen
# Phase 6: ziel_x = Bildmitte seitlich (Meter, +x = linke Gesichtshälfte der Figur, im Bild von vorn rechts);
# ortho = orthografische Kamera mit derselben Bildhöhe (Vergleich Bild gegen Netz)
func ansicht(art: String, winkel := 0.0, hoehe := 3.0, spanne_m := 0.0, ziel_m := 0.0, ziel_x := 0.0, ortho := false) -> void:
	var e := kopf.augen_mitte if kopf else Vector3(0, 0.82, 0.13)
	var ob: float = kopf.oben if kopf else 0.93
	var s := maxf(ob - e.y, 0.05)          # Augen bis Scheitel, Erwachsener etwa 0,12 m
	var ziel := Vector3(0.0, e.y, e.z)
	var spanne := 1.0
	match art:
		"portraet":
			ziel.y = e.y - 0.52 * s
			spanne = 3.2 * s
		"buste":
			# Kopf und Schulteransatz (der Zuschnitt unten bleibt außerhalb des Bildes)
			ziel.y = e.y - 0.95 * s
			spanne = 4.3 * s
		"kopf":
			ziel.y = e.y - 0.45 * s
			spanne = 2.75 * s
		"augen":
			spanne = 0.85 * s
		"mund":
			if kopf:
				ziel = Vector3(0.0, kopf.mund_mitte.y + 0.01 * s, kopf.mund_mitte.z)
			spanne = 0.85 * s
	if spanne_m > 0.0:
		spanne = spanne_m
		ziel.y = e.y + ziel_m
	ziel.x = ziel_x
	# Drehpunkt in Kopfmitte (hinter den Augen), damit das Gesicht in der ¾-Ansicht in der Mitte bleibt
	var dreh := Vector3(ziel.x, ziel.y, e.z - 0.75 * s)
	if art in ["augen", "mund"]:
		dreh = Vector3(ziel.x, ziel.y, ziel.z - 0.35 * s)   # nah: um den Mund bzw. die Augen drehen
	var abstand := (ziel.z - dreh.z) + spanne * 0.5 / tan(deg_to_rad(kamera.fov * 0.5))
	var w := deg_to_rad(winkel)
	var h := deg_to_rad(hoehe)
	var pos := dreh + Vector3(sin(w) * cos(h), sin(h), cos(w) * cos(h)) * abstand
	var blick := dreh + (ziel - dreh).rotated(Vector3.UP, w)
	if ziel_x != 0.0:
		# Phase 6, Nahaufnahme eines festen Punkts (Wange, N1/N5): die Kamera blickt aus der Richtung (winkel, hoehe)
		# genau auf ziel; der Punkt dreht nicht wie das Gesicht um die Kopfmitte mit
		pos = ziel + Vector3(sin(w) * cos(h), sin(h), cos(w) * cos(h)) * (spanne * 0.5 / tan(deg_to_rad(kamera.fov * 0.5)))
		blick = ziel
	if ortho:
		kamera.projection = Camera3D.PROJECTION_ORTHOGONAL
		kamera.size = spanne
		kamera.far = 10.0
	else:
		kamera.projection = Camera3D.PROJECTION_PERSPECTIVE
	if freie:
		kamera.call("Blicken", pos, blick)
	else:
		kamera.transform = Transform3D(Basis.looking_at(blick - pos), pos)

# Einstellungen der Umgebung aus einem Bogen ({"ssao_radius": 0.02, "ssao_enabled": false, …}); Unbekanntes wird
# gemeldet. Vor jeder Zelle gelten wieder die Grundwerte der Bühne.
var _umgebung_grund := {}
func umgebung_setzen(werte: Dictionary) -> void:
	for k in _umgebung_grund:
		umgebung.set(k, _umgebung_grund[k])
	for k in werte:
		if not k in umgebung:
			print("WARNUNG Umgebung kennt ", k, " nicht")
			continue
		if not _umgebung_grund.has(k):
			_umgebung_grund[k] = umgebung.get(k)
		umgebung.set(k, werte[k])

# Schlüssellicht aus einem Bogen ({"shadow_enabled": false, "light_energy": 2.0}), wie umgebung_setzen
var _licht_grund := {}
func licht_setzen(werte: Dictionary) -> void:
	for k in _licht_grund:
		schluessel.set(k, _licht_grund[k])
	for k in werte:
		if not k in schluessel:
			print("WARNUNG Schlüssellicht kennt ", k, " nicht")
			continue
		if not _licht_grund.has(k):
			_licht_grund[k] = schluessel.get(k)
		schluessel.set(k, werte[k])

# Phase 6 (S3): Fülllicht aus einem Bogen ({"light_energy": 0.0}), wie licht_setzen
var _fuell_grund := {}
func fuell_setzen(werte: Dictionary) -> void:
	for k in _fuell_grund:
		fuell.set(k, _fuell_grund[k])
	for k in werte:
		if not k in fuell:
			print("WARNUNG Fülllicht kennt ", k, " nicht")
			continue
		if not _fuell_grund.has(k):
			_fuell_grund[k] = fuell.get(k)
		fuell.set(k, werte[k])

func foto() -> Image:
	return get_texture().get_image()

# ---------------------------------------------------------------- Prüfkörper (Phase 6, Bogen P10)
# art: "graukarte" | "hautfeld" | "farbtest" | "kugel" | "alle" | "" (keine). Der Kopf wird dabei ausgeblendet.
# Graukarte: 18 % Reflexion (linear), matt. Hautfeld: Hauttyp III nach PhysicallyBased (CC0), linear 0,623/0,433/0,343.
# Farbtest: links Shader-Uniform `vec3 farbe : source_color` mit Vector3(0,5), Mitte derselbe mit Color(0,5),
# rechts StandardMaterial3D mit Color(0,5) – alle unbeleuchtet. Godot rechnet nur den Color von sRGB nach linear um
# (Phase-5-Fehler der Farbübergabe): erwartet linear 0,5 / 0,214 / 0,214.
# Kugel: links haut3 ohne Wrap (streu 0, kein Flaum), Mitte haut3 wie im Spiel, rechts StandardMaterial3D (gleiche
# Albedo, Rauheit 0,56, specular 0,42); neutrale Bilder (farbe 0,5, keine Masken).
func pruefkoerper_setzen(art: String) -> void:
	pruefkoerper = art
	if _pruef:
		_pruef.queue_free()
		_pruef = null
	if kopf:
		kopf.visible = art == ""
	if art == "":
		return
	_pruef = Node3D.new()
	add_child(_pruef)
	var e := kopf.augen_mitte if kopf else Vector3(0, 0.82, 0.13)
	var mitte := Vector3(0.0, e.y - 0.02, e.z)
	match art:
		"graukarte":
			_pruef.add_child(_flaeche(mitte, 0.14, _matt(Color(0.18, 0.18, 0.18))))
		"hautfeld":
			_pruef.add_child(_flaeche(mitte, 0.14, _matt(Color(0.623, 0.433, 0.343))))
		"farbtest":
			_farbtest(mitte, 0.07)
		"kugel":
			_kugeln(mitte, 0.036)
		"kugel6_glanz", "kugel6_streu":
			_kugeln6(mitte, 0.036, art == "kugel6_glanz")
		"alle":
			_pruef.add_child(_flaeche(mitte + Vector3(-0.07, 0.07, 0), 0.12, _matt(Color(0.18, 0.18, 0.18))))
			_pruef.add_child(_flaeche(mitte + Vector3(0.07, 0.07, 0), 0.12, _matt(Color(0.623, 0.433, 0.343))))
			_farbtest(mitte + Vector3(0, -0.035, 0), 0.045)
			_kugeln(mitte + Vector3(0, -0.11, 0), 0.03)
		_:
			print("WARNUNG Prüfkörper ", art, " unbekannt")

static func _matt(linear: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = linear.linear_to_srgb()     # StandardMaterial3D erwartet sRGB
	m.roughness = 1.0
	m.metallic_specular = 0.0
	return m

static func _flaeche(pos: Vector3, groesse: float, stoff: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = Vector2(groesse, groesse)
	mi.mesh = q
	mi.material_override = stoff
	mi.position = pos
	return mi

func _farbtest(mitte: Vector3, b: float) -> void:
	var a := ShaderMaterial.new()
	a.shader = FARBTEST
	a.set_shader_parameter("farbe", Vector3(0.5, 0.5, 0.5))   # stand5: absichtlich (Farbtest: Weg der Phase 5)
	var c := ShaderMaterial.new()
	c.shader = FARBTEST
	Kopf.farbe_setzen(c, "farbe", Color(0.5, 0.5, 0.5), 6)    # S2: Weg über die Hilfsfunktion (Stand 6)
	var s := StandardMaterial3D.new()
	s.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	s.albedo_color = Color(0.5, 0.5, 0.5)
	var i := 0
	for m in [a, c, s]:
		_pruef.add_child(_flaeche(mitte + Vector3((i - 1) * b * 1.05, 0, 0), b, m))
		i += 1

static var _neutral := {}
static func _neutral_bild(farbe: Color) -> ImageTexture:
	var schl := farbe.to_html()
	if not _neutral.has(schl):
		var img := Image.create(4, 4, false, Image.FORMAT_RGBA8)
		img.fill(farbe)
		_neutral[schl] = ImageTexture.create_from_image(img)
	return _neutral[schl]

func _kugeln(mitte: Vector3, r: float) -> void:
	# Phase 6: mit einem Kopf in Stand 6 haut6 und die lineare Palette (Color), sonst wie Phase 5
	var st: int = kopf.stand if kopf else 5
	var grund: Color = Kopf.hautfarbe(185.0 / 255.0) if st < 6 else Kopf.linear(Kopf.hautfarbe6(185.0))
	var i := 0
	for wrap in [0.0, 0.4, -1.0]:
		var mi := MeshInstance3D.new()
		var sm := SphereMesh.new()
		sm.radius = r
		sm.height = 2.0 * r
		sm.radial_segments = 96
		sm.rings = 48
		mi.mesh = sm
		if wrap >= 0.0:
			var m := ShaderMaterial.new()
			m.shader = HAUT6 if st >= 6 else HAUT3
			m.set_shader_parameter("farbe_tex", _neutral_bild(Color(0.5, 0.5, 0.5, 0.0)))
			m.set_shader_parameter("normal_tex", _neutral_bild(Color(0.5, 0.5, 0.0, 1.0)))
			Kopf.farbe_setzen(m, "grundfarbe", grund, st)    # Stand 5: Weg der Phase 5 (Vector3)
			m.set_shader_parameter("streu", wrap)
			if st < 6 or wrap == 0.0:
				m.set_shader_parameter("flaum", 0.0 if wrap == 0.0 else 0.16)
			mi.material_override = m
		else:
			var s := StandardMaterial3D.new()
			s.albedo_color = grund
			s.roughness = 0.56
			s.metallic_specular = 0.42
			mi.material_override = s
		mi.position = mitte + Vector3((i - 1) * r * 2.3, 0, 0)
		_pruef.add_child(mi)
		i += 1

# S2: Prüfkugeln für den Glanz und das Streulicht (Prüfkörper „kugel6_glanz“/„kugel6_streu“, Lichtart „kugeltest“):
# links haut6 (Wrap 0, eine Keule, kein Flaum, Glanzmaske 1), Mitte StandardMaterial3D (Lambert, Rauheit 0,56,
# specular 0,42 = F0 0,028), rechts haut3 (Wrap 0, kein Flaum) als Phase 5. glanz = true: Albedo 0 (nur Glanz);
# false: Glanz 0 (nur Streulicht; haut3 behält seinen Glanz, er ist dort nicht abschaltbar).
func _kugeln6(mitte: Vector3, r: float, glanz: bool) -> void:
	var grund_lin := Kopf.hautfarbe6(185.0)          # linear (Albedo), für alle drei gleich
	var grund := Kopf.linear(grund_lin)
	var schwarz := Color(0, 0, 0)
	var i := 0
	for art in ["haut6", "standard", "haut3"]:
		var mi := MeshInstance3D.new()
		var sm := SphereMesh.new()
		sm.radius = r
		sm.height = 2.0 * r
		sm.radial_segments = 128
		sm.rings = 64
		mi.mesh = sm
		if art == "standard":
			var s := StandardMaterial3D.new()
			s.albedo_color = schwarz if glanz else grund
			s.roughness = 0.56
			s.metallic_specular = 0.42 if glanz else 0.0
			s.diffuse_mode = BaseMaterial3D.DIFFUSE_LAMBERT
			mi.material_override = s
		else:
			var m := ShaderMaterial.new()
			m.shader = HAUT6 if art == "haut6" else HAUT3
			m.set_shader_parameter("farbe_tex", _neutral_bild(Color(0.5, 0.5, 0.5, 0.0)))
			m.set_shader_parameter("normal_tex", _neutral_bild(Color(0.5, 0.5, 0.0, 1.0)))
			if art == "haut6":
				Kopf.farbe_setzen(m, "grundfarbe", schwarz if glanz else grund, 6)
			else:
				Kopf.farbe_setzen(m, "grundfarbe", schwarz if glanz else grund_lin, 5)   # Vector3 = linear
			m.set_shader_parameter("streu", 0.0)
			m.set_shader_parameter("flaum", 0.0)
			m.set_shader_parameter("unruhe", 0.0)
			if art == "haut6":
				m.set_shader_parameter("keule2", 0.0)
				m.set_shader_parameter("glanz", 1.0 if glanz else 0.0)
			else:
				m.set_shader_parameter("glanz", 0.34)
			mi.material_override = m
		mi.position = mitte + Vector3((i - 1) * r * 2.3, 0, 0)
		_pruef.add_child(mi)
		i += 1

# ---------------------------------------------------------------- Begleitdaten (Phase 6)
# Kamera und Kopf für eine Begleitdatei: Python (tools/charakter/messen) projiziert damit die Merkpunkte ins Bild.
static func transform_liste(t: Transform3D) -> Array:
	return [t.basis.x.x, t.basis.x.y, t.basis.x.z, t.basis.y.x, t.basis.y.y, t.basis.y.z,
		t.basis.z.x, t.basis.z.y, t.basis.z.z, t.origin.x, t.origin.y, t.origin.z]

func kamera_daten() -> Dictionary:
	return {
		"transform": transform_liste(kamera.global_transform),
		"projektion": "ortho" if kamera.projection == Camera3D.PROJECTION_ORTHOGONAL else "perspektive",
		"fov": kamera.fov, "size": kamera.size, "keep_aspect": kamera.keep_aspect,
		"near": kamera.near, "far": kamera.far,
		"bild": [size.x, size.y],
		"kopf_transform": transform_liste(kopf.global_transform) if kopf else [],
		"lichtart": licht_art, "anzeige": anzeige, "pruefkoerper": pruefkoerper,
		"belichtung": umgebung.tonemap_exposure,
		"schluessel_von": [schluessel.global_transform.basis.z.x, schluessel.global_transform.basis.z.y, schluessel.global_transform.basis.z.z],
	}
