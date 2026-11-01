# Versuch Charakter Phase 2/3: Basiskopf (Büste) aus einer .glb zur Laufzeit laden und über Gene, Alter, Geschlecht
# und Mimik steuern. Die .glb und ihre Beschreibung (<name>.json) entstehen mit tools/charakter/kopf3.sh aus ICT- und
# MakeHuman-Daten und liegen außerhalb des Repositorys (~/werkzeuge/charakter/glb3); fehlt die Datei, steht der
# Grund in `fehler`.
#
# Phase 3: Liegt neben der .glb der Ordner kopf3_tex (gebackene Haut, Haar- und Augenbilder), bekommt der Kopf die
# Stoffe der Phase 3 (haut3, augen3, hornhaut, lidschatten, haar3); sonst die der Phase 2. `vorher_setzen(true)`
# schaltet einen Phase-3-Kopf zum Vergleich auf die Stoffe der Phase 2 (ohne Bilder).
#
# Phase 4: Liegt neben der .glb der Ordner haar4 (tools/charakter/kopf4_haar.py), bekommt der Kopf Haupthaar und Bart
# (charakter_haar.gd): Frisur und Bart aus den Genen (oder `haar_vorgabe`), als Haarkarten oder TressFX
# (`haar_modus_setzen`), die Haut darunter über die Kopfhautkarten.
#
# Formen der .glb: makro_<g>_<alter> (Alter × Geschlecht), makro_<g>_<alter>_<kraft|gewicht>_<minus|plus>,
# gen_<kennung>_<minus|plus> (Gestalt-Gene) und die ARKit-Mimik. Die Gewichte rechnet _anwenden() wie
# MakeHuman: Alter und Geschlecht bilinear zwischen den Stützen, Kraft und Gewicht je Alter dazu.
# Mimik und Gestalt-Gene werden mit der Gesichtsgröße skaliert (Kind kleiner als Erwachsener, „mimikmass“).
extends Node3D

const HAUT := preload("res://versuch/charakter/haut.gdshader")
const AUGEN := preload("res://versuch/charakter/augen.gdshader")
const MUND := preload("res://versuch/charakter/mund.gdshader")
const ZAEHNE := preload("res://versuch/charakter/zaehne.gdshader")
const HAAR := preload("res://versuch/charakter/haar.gdshader")
const HAUT3 := preload("res://versuch/charakter/haut3.gdshader")
const HAUT3_ULTRA := preload("res://versuch/charakter/haut3_ultra.gdshader")
const AUGEN3 := preload("res://versuch/charakter/augen3.gdshader")
const HORNHAUT := preload("res://versuch/charakter/hornhaut.gdshader")
const LIDSCHATTEN := preload("res://versuch/charakter/lidschatten.gdshader")
const LIDSCHATTEN6 := preload("res://versuch/charakter/lidschatten6.gdshader")   # Etappe D
const BRAUE6 := preload("res://versuch/charakter/braue6.gdshader")               # Etappe D: Brauen, Wimpern gemischt
const HAAR3 := preload("res://versuch/charakter/haar3.gdshader")
const HAAR4 := preload("res://versuch/charakter/charakter_haar.gd")
# Phase 6 (Stand 6): Haut und Augen mit behobenen Grundfehlern (S2) und Porträtlicht (S3); haut3/augen3 bleiben für Stand 5
const HAUT6 := preload("res://versuch/charakter/haut6.gdshader")
const HAUT6_ULTRA := preload("res://versuch/charakter/haut6_ultra.gdshader")
const AUGEN6 := preload("res://versuch/charakter/augen6.gdshader")
const HAUT6A := preload("res://versuch/charakter/haut6a.gdshader")
const HAUT6A_ULTRA := preload("res://versuch/charakter/haut6a_ultra.gdshader")
# Etappe C (Haut): eigene Zonen-, Glanz- und Reliefkarten (tools/charakter/kopf6_hautc.py) und Detailkacheln
# (kopf6_kacheln.py) im Bildordner des Kopfs; Bogenschlüssel „weg_c“ (zusammen mit „weg_a“), „wetter“ 0–1
const HAUT6C := preload("res://versuch/charakter/haut6c.gdshader")
const HAUT6C_ULTRA := preload("res://versuch/charakter/haut6c_ultra.gdshader")
const HAUT6C_EINFACH := preload("res://versuch/charakter/haut6c_einfach.gdshader")
const BILDER := ["haut_farbe", "haut_normal", "haut_masken", "haut_falten", "brauen", "wimpern", "iris_muster", "lederhaut"]
const BILDER_WAHL := ["brauen_w", "haut_zonen", "haut_c_zonen", "haut_c_glanz", "haut_c_relief", "haut_c_poren",
	"haut_c_flecken"]   # Phase 5 (sonst „brauen“); Halt B2: Zonenkarte für Weg A; Etappe C: Haut
# Etappe C: Reliefgewicht je Kanal über das Alter (Stirn, Augen, Mittelform, Mund/Hals); Mittelform (Nasolabial-,
# Tränen-, Lidrinne, Lippenrillen) gibt es schon in der Jugend schwach
const RELIEF_STAERKE := 3.2     # Etappe C: Reliefkarte × 3,2 (bei 1 lasen sich Falten im Porträtlicht kaum; Runde 2: mehr Relief,
                                # weniger aufgemalter Furchenschatten)
const RELIEF_ALTER := {
	"stirn": [[25, 0.0], [35, 0.15], [45, 0.4], [60, 0.8], [75, 1.1]],
	"augen": [[20, 0.0], [30, 0.3], [40, 0.5], [55, 0.8], [70, 1.1]],
	"mitte": [[10, 0.1], [25, 0.3], [35, 0.42], [50, 0.65], [70, 1.0]],
	"mund": [[35, 0.0], [48, 0.3], [62, 0.7], [75, 1.05]],
}
# Falten je Gegend über das Alter (Menschenjahre -> Gewicht), wie die Alterskurven in erbgut.json
const FALTEN_ALTER := {
	"stirn": [[25, 0.0], [38, 0.15], [50, 0.5], [62, 0.95], [75, 1.3]],
	"augen": [[28, 0.0], [38, 0.2], [50, 0.55], [62, 0.95], [75, 1.25]],
	"mund": [[20, 0.0], [32, 0.18], [45, 0.45], [60, 0.8], [75, 1.0]],
	"hals": [[35, 0.0], [50, 0.3], [62, 0.65], [75, 1.0]],
}
const ERBGUT := "res://daten/erbgut.json"
# Phase 5: Altersformen (tools/charakter/kopf2_regler.json „alter_formen“). Das ICT-Durchschnittsgesicht wirkte mit den
# MakeHuman-Makros allein wie ein Kind; „alter_reife_<g>“ macht es erwachsen, „alter_greis“ lässt es im Alter erschlaffen.
const REIFE_ALTER := {"m": [[11, 0.0], [15, 0.3], [19, 0.75], [24, 1.0]], "w": [[10, 0.0], [14, 0.35], [18, 0.8], [21, 1.0]]}
const GREIS_ALTER := [[40, 0.0], [55, 0.3], [68, 0.7], [78, 1.0], [90, 1.15]]

var pfad := ""
var netze: Array[MeshInstance3D] = []
var formen := {}            # Formname -> Array von [MeshInstance3D, Index]
var info := {}              # Beschreibung der .glb (<name>.json)
var ladezeit_ms := 0.0
var komprimier_ms := 0.0
var fehler := ""
var augen_mitte := Vector3(0, 0.82, 0.12)
var mund_mitte := Vector3(0, 0.75, 0.14)
var oben := 0.93
var haut_stoffe: Array[ShaderMaterial] = []
var haar_stoffe: Array[ShaderMaterial] = []
var augen_stoffe: Array[ShaderMaterial] = []
var wimpern_stoffe: Array[ShaderMaterial] = []
var lidschatten_stoffe: Array[ShaderMaterial] = []
var hornhaut_stoffe: Array[ShaderMaterial] = []   # Etappe D: Hornhaut (Glanz) …
var traene_stoffe: Array[ShaderMaterial] = []     # … und Tränenfilm getrennt einstellbar
var haut2_stoffe: Array[ShaderMaterial] = []     # Phase-2-Haut eines Phase-3-Kopfs (Vergleich)
var bilder := {}            # Phase 3: Name -> Texture2D (aus <glb-Ordner>/kopf3_tex)
var phase3 := false
var ultra := false          # Haut mit Bildschirm-SSS (nur Ultra)
var vorher := false         # Phase-3-Kopf mit den Stoffen der Phase 2
var bilder_ms := 0.0
var _stoffe3 := {}          # "<id>_<fläche>" -> [Stoff Phase 3, Stoff Phase 2]
# Phase 4: Haar und Bart
var haar_knoten: Node3D = null  # charakter_haar.gd, wenn <glb-Ordner>/haar4 da ist
var haar_modus := "karten"  # karten | tressfx | aus
var haar_vorgabe := {}      # {"frisur": Stil, "bart": Stil} statt der Gene ("" = keins)
var haar_werte := {}        # zuletzt gesetzte Werte (Frisur, Bart, Glatze, …)
var haar_ruhe := false      # TressFX nach kurzer Simulation einfrieren (Galerie)
var haar_sparsam := false   # TressFX mit weniger Folgehaaren und Detailstufe (kleine Porträts); in Ruhe immer
var haar_schatten := true   # Haarkarten werfen Schatten
static var _bild_speicher := {}
# Zustand
var gene := {}              # Kennung -> sichtbarer Wert 0..255
var alter := 35.0
var geschlecht := 1.0       # 1 = Mann, 0 = Frau
var mimik := {}             # ARKit-Name -> Gewicht
var mimik_mit_alter := true
var mimikfaktor := 1.0
var gewichte := {}          # zuletzt gesetzte Formgewichte (zum Prüfen)
var zusatz_formen := {}     # Formname -> Gewicht obendrauf (Bögen, Probeformen „probe_*“)
# Phase 6: Stand-Schalter. 5 = genau wie Phase 5 (glb5, haut3, augen3, Farbübergabe und Genrechnung der Phase 5),
# 6 = der neue Weg: S2 Farben als Color (Godot rechnet sRGB nach linear um), vorläufige Paletten (Haut linear nach
# PhysicallyBased, Iris, Lederhaut, Zahnfleisch), haut6/augen6; die .glb wählt die Schau (Stand 6: glb6/kopf6.glb).
# stand_setzen() schaltet einen geladenen Kopf um (Stoffe tauschen den Shader, die Parameter bleiben), damit
# Vorher-Bilder und gepaarte Messungen im selben Prozess laufen.
var stand := 5
var zahnfleisch_stoffe: Array[ShaderMaterial] = []

# Phase 6, Etappe B (S4–S7): neue Formen und Genrechnung, wenn die .glb sie hat (kopf6 nach formen_mischen.py) und der
# Kopf auf Stand 6 steht. Regeldaten in regeln6.json (tools/charakter/kopf6_eichen.py): Reife oben/unten, Mitte
# (25 → 45 Jahre), Greis ab 45; Gestalt-Gene mit Nullpunkt Mensch-Mittel (Gewicht = (Wert − Mittel) / Streuung ×
# Stärke je Geschlecht und Richtung × Reife), Karikaturbremse (nur Darstellung, das Erbgut bleibt), Budget je
# MakeHuman-Ziel, Gesichtszüge zug_01–12 ((Wert − 128) / 32 × 0,7) und Asymmetrie aus dem Personen-Samen.
const REGELN6 := "res://versuch/charakter/regeln6.json"
const VORSCHLAG := "res://versuch/charakter/erbgut_vorschlag.json"
static var _regeln6 := {}          # je Pfad (Halt B2: ein Probekopf bringt seine regeln6.json im .glb-Ordner mit)
var regeln_pfad := REGELN6
# Halt B2, Weg A (Probekopf, Bogenschlüssel „weg_a“): Haut mit Zonen und Stoppeln (haut6a), weichere und weniger deckende
# Brauen, Iris entsättigt mit hohler Normale und schmalerem Limbus, Lederhaut zu den Winkeln warm abgedunkelt, Lidschatten
# etwas stärker. Nur Stand 6; ohne Zonenkarte (kopf6_tex/haut_zonen.png) bleibt die Haut wie Stand 6.
var weg_a := false
var weg_c := false              # Etappe C: Haut (nur mit weg_a, Stand 6 und den Bildern haut_c_*)
# Etappe D (Bogenschlüssel „weg_d“, nur mit weg_a, Stand 6; Prüferbefund „Augen glasig, Brauen wie aufgeklebt“): Lederhaut
# matter und getönt mit grauem Saum am Limbus, Iris mit Parallaxe, Hornhaut-Glanz klein und weich (F0 0,02), Schatten des
# Oberlids und Kontaktschatten auf dem Augapfel (Lidschatten-Netz, Eckfarbe G/B aus kopf2_bauen.py), Tränensee am Unterlid,
# feuchter Lidrand, Pupille nach dem Licht; Brauen heller, je Haar verschieden hell, weicher Rand (Brauenbilder
# kopf3_haartexturen.py weich=1, Karten mit brauen_d=1 abstehend).
var weg_d := false
var pupille_licht := 0.8        # Etappe D: Helligkeit am Ort (0 Nacht … 1 Sonne) -> Pupille 0,26 … 0,46 des Irishalbmessers
var wetter := 0.0               # Etappe C: Wetterhaut 0–1 (Außenarbeit; später aus Beruf und Jahren im Kern)
var haut_einfach := false       # Etappe C: Stufen Niedrig/Mittel ohne Detailkacheln
var c_info := {}                # haut_c.json (Reliefmaßstab, Bildpunktgröße)
static var zuege_aus := false   # Prüfung „nur Gene“ (Schau: --ohne_zuege)
static var _erbgut6 := {}
var volk := "Mensch"
var aussehen_samen := 0         # 0 = keiner (Mittelgesicht: keine Asymmetrie)
var iris_vorgabe := -1          # Bogenschlüssel „iris_muster“ (0–5), sonst Wahl nach Augenfarbe und Samen
var gene_z := {}                # zuletzt gerechnete σ-Werte nach der Karikaturbremse (zum Prüfen)
var karikatur := {}             # K vorher/nachher, Kappungen (zum Prüfen)
static var _poren: NoiseTexture2D
static var _erbgut := {}

func laden(datei: String) -> bool:
	pfad = datei
	if not FileAccess.file_exists(datei):
		fehler = "Kopf fehlt: %s\nDie Köpfe entstehen mit tools/charakter/kopf2.sh (Fremddaten, nicht im Repository)." % datei
		push_error(fehler)
		return false
	var beschr := datei.get_basename() + ".json"
	if FileAccess.file_exists(beschr):
		info = JSON.parse_string(FileAccess.get_file_as_string(beschr))
	# Halt B2: eigene Regeldaten neben der .glb (Probekopf mit eigener Eichung), sonst die des Repositorys
	var eigene := datei.get_base_dir().path_join("regeln6.json")
	regeln_pfad = eigene if FileAccess.file_exists(eigene) else REGELN6
	var t := Time.get_ticks_usec()
	var doc := GLTFDocument.new()
	var st := GLTFState.new()
	st.handle_binary_image_mode = GLTFState.HANDLE_BINARY_IMAGE_MODE_EMBED_AS_UNCOMPRESSED
	var err := doc.append_from_file(datei, st)
	if err != OK:
		fehler = "glTF-Fehler %d beim Laden von %s" % [err, datei]
		push_error(fehler)
		return false
	var szene := doc.generate_scene(st)
	ladezeit_ms = (Time.get_ticks_usec() - t) / 1000.0
	add_child(szene)
	_sammeln(szene)
	_bilder_laden(datei)
	var tk := Time.get_ticks_usec()
	_stoffe_setzen()
	komprimier_ms = (Time.get_ticks_usec() - tk) / 1000.0
	_haar_einrichten()
	gene = mittelwerte("Mensch")
	_anwenden()
	return true

# Kopf aus einem schon geladenen Kopf kopieren: Netze (mit den Formen) werden geteilt, Stoffe und Formgewichte sind
# je Kopf eigen. Für die Galerie (viele Köpfe, ein Netz im Speicher).
func kopieren_von(quelle) -> void:
	pfad = quelle.pfad
	info = quelle.info
	regeln_pfad = quelle.regeln_pfad
	stand = quelle.stand
	bilder = quelle.bilder
	c_info = quelle.c_info
	phase3 = quelle.phase3
	ultra = quelle.ultra
	for k in quelle.get_children():
		if k == quelle.haar_knoten:
			continue
		var neu: Node = k.duplicate()
		add_child(neu)
		_sammeln(neu)
	_stoffe_setzen()
	haar_modus = quelle.haar_modus
	_haar_einrichten()
	gene = mittelwerte("Mensch")
	_anwenden()

# Phase 6: Stand umschalten (5 = Phase 5, 6 = neuer Weg); die Farben setzt das nächste _anwenden()/_farben()
func stand_setzen(n: int) -> void:
	if n == stand:
		return
	stand = n
	_shader_waehlen()
	if haar_knoten:
		haar_knoten.stand = stand
	_farben()

func _haut_shader() -> Shader:
	if stand >= 6 and weg_c and bilder.has("haut_c_relief"):
		return HAUT6C_ULTRA if ultra else (HAUT6C_EINFACH if haut_einfach else HAUT6C)
	if stand >= 6 and weg_a and bilder.has("haut_zonen"):
		return HAUT6A_ULTRA if ultra else HAUT6A
	return (HAUT6_ULTRA if ultra else HAUT6) if stand >= 6 else (HAUT3_ULTRA if ultra else HAUT3)

func weg_a_setzen(an: bool) -> void:
	if an == weg_a:
		return
	weg_a = an
	_shader_waehlen()

func weg_c_setzen(an: bool) -> void:
	if an == weg_c:
		return
	weg_c = an
	_shader_waehlen()

# Etappe D: Augen und Brauen (die Werte setzt das nächste _farben())
func weg_d_setzen(an: bool) -> void:
	weg_d = an

func pupille_licht_setzen(h: float) -> void:
	pupille_licht = clampf(h, 0.0, 1.0)

func _shader_waehlen() -> void:
	for m in haut_stoffe:
		if m.shader in [HAUT3, HAUT3_ULTRA, HAUT6, HAUT6_ULTRA, HAUT6A, HAUT6A_ULTRA, HAUT6C, HAUT6C_ULTRA, HAUT6C_EINFACH]:
			m.shader = _haut_shader()
	for m in augen_stoffe:
		if m.shader in [AUGEN3, AUGEN6]:
			m.shader = AUGEN6 if stand >= 6 else AUGEN3

# Phase 6 (S2): eine Farbe an ein Farb-Uniform (source_color) übergeben. Stand 6 übergibt Color (sRGB, Godot rechnet
# nach linear um, wie bei StandardMaterial3D); Stand 5 wie Phase 5 als Vector3 (ohne Umrechnung – der Fehler der
# Phase 5, für den Vergleich erhalten). Faktoren wie „toenung“ bleiben Vector3.
# Etappe B (S7): Brauenformen (brauen6_m_0–5, brauen6_w_0–5 -> Ebenen 0–11) und Irismuster (iris6_0–5) als Bildarray
func _bildarrays_laden(ordner: String) -> void:
	for art in [["brauen6", ["brauen6_m_0", "brauen6_m_1", "brauen6_m_2", "brauen6_m_3", "brauen6_m_4", "brauen6_m_5",
			"brauen6_w_0", "brauen6_w_1", "brauen6_w_2", "brauen6_w_3", "brauen6_w_4", "brauen6_w_5"]],
			["iris6", ["iris6_0", "iris6_1", "iris6_2", "iris6_3", "iris6_4", "iris6_5"]]]:
		var schl: String = ordner.path_join(art[0])
		if _bild_speicher.has(schl):
			bilder[art[0]] = _bild_speicher[schl]
			continue
		var liste: Array[Image] = []
		for n in art[1]:
			var f := ordner.path_join(n + ".png")
			if not FileAccess.file_exists(f):
				liste.clear()
				break
			var img := Image.load_from_file(f)
			img.generate_mipmaps()
			liste.append(img)
		if liste.is_empty():
			continue
		var arr := Texture2DArray.new()
		arr.create_from_images(liste)
		_bild_speicher[schl] = arr
		bilder[art[0]] = arr

# Brauenform aus „brauen_form“: Quantil der Glockenkurve des Gens -> 6 gleich häufige Formen
static func brauen_ebene(wert: float) -> int:
	var z := (wert - 128.0) / 70.0
	var phi := 0.5 * (1.0 + _erf(z / sqrt(2.0)))
	return clampi(int(floor(phi * 6.0)), 0, 5)

static func _erf(x: float) -> float:
	# Abramowitz–Stegun 7.1.26 (Fehler < 1,5e-7)
	var t := 1.0 / (1.0 + 0.3275911 * absf(x))
	var y := 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * exp(-x * x)
	return y if x >= 0.0 else -y

static func farbe_setzen(m: ShaderMaterial, name: String, c: Color, st: int) -> void:
	if st >= 6:
		m.set_shader_parameter(name, c)
	else:
		m.set_shader_parameter(name, Vector3(c.r, c.g, c.b))   # stand5: Weg der Phase 5

# Lineare Farbe (Albedo) so als Color übergeben, dass sie nach Godots Umrechnung genau linear ankommt
static func linear(c: Color) -> Color:
	return c.linear_to_srgb()

func _bilder_laden(datei: String) -> void:
	var name := datei.get_file().get_basename().trim_suffix("_mn").trim_suffix("_ohne")
	var ordner := datei.get_base_dir().path_join(name + "_tex")
	if not DirAccess.dir_exists_absolute(ordner):
		return
	var t := Time.get_ticks_usec()
	_bildarrays_laden(ordner)
	for b in BILDER + BILDER_WAHL:
		var f := ordner.path_join(b + ".png")
		if b in BILDER_WAHL and not FileAccess.file_exists(f):
			continue
		if _bild_speicher.has(f):
			bilder[b] = _bild_speicher[f]
			continue
		var img := Image.load_from_file(f)
		if img == null:
			push_error("Bild fehlt: " + f)
			return
		img.generate_mipmaps()
		var tex := ImageTexture.create_from_image(img)
		_bild_speicher[f] = tex
		bilder[b] = tex
	var cj := ordner.path_join("haut_c.json")
	if FileAccess.file_exists(cj):
		c_info = JSON.parse_string(FileAccess.get_file_as_string(cj))
	phase3 = true
	bilder_ms = (Time.get_ticks_usec() - t) / 1000.0

func _sammeln(n: Node) -> void:
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		netze.append(mi)
		for i in mi.mesh.get_blend_shape_count():
			var name := String(mi.mesh.get_blend_shape_name(i))
			if not formen.has(name):
				formen[name] = []
			formen[name].append([mi, i])
	for k in n.get_children():
		_sammeln(k)

func form(name: String, wert: float) -> void:
	for e in formen.get(name, []):
		(e[0] as MeshInstance3D).set_blend_shape_value(e[1], wert)

# Phase 6: die Formgewichte, wie sie wirklich in der Szene stehen (aus den Netzen zurückgelesen, je Form das erste Netz,
# das sie hat) – für den Gewichteausdruck und die Begleitdateien der Bögen; Python misst damit genau, was Godot zeigt.
func szene_gewichte() -> Dictionary:
	var aus := {}
	for name in formen:
		var e: Array = formen[name][0]
		aus[name] = (e[0] as MeshInstance3D).get_blend_shape_value(e[1])
	return aus

# ---------------------------------------------------------------- Erbgut
static func erbgut() -> Dictionary:
	if _erbgut.is_empty():
		var d: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(ERBGUT))
		for g in d["gene"]:
			_erbgut[g["kennung"]] = g
	return _erbgut

static func mittelwerte(volk: String) -> Dictionary:
	var aus := {}
	for k in erbgut():
		aus[k] = float(erbgut()[k]["voelker"][volk]["mittel"])
	return aus

# Phase 6: Würfeln und Vererben über den echten Kern (ErbgutBruecke.cs, Erbgut.Wuerfeln/Vererben). Die Darstellung
# bekommt die sichtbaren Allele so, wie der Kern sie hält („bartwuchs“ und „bartwuchs_frau“ getrennt – die
# Frauenregel wendet _farben() selbst an, wie bisher). familie: {"<saat>": [vater_saat, mutter_saat]} – wer dort
# steht, wird vererbt (Eltern rekursiv), alle anderen gewürfelt. arten: nur Gene dieser Arten (leer = alle).
const BRUECKE := "res://versuch/charakter/ErbgutBruecke.cs"
static var _bruecke: Object = null
static var _bruecke_vorlage := ""
static func bruecke(vorlage := "") -> Object:
	if _bruecke == null and ResourceLoader.exists(BRUECKE):
		var s: Script = load(BRUECKE)
		if s and s.can_instantiate():
			_bruecke = s.new()
	if _bruecke and vorlage != _bruecke_vorlage:
		var f: String = _bruecke.call("VorlageLaden", vorlage)
		if f != "":
			push_error("Erbgut-Vorlage %s: %s" % [vorlage, f])
		_bruecke_vorlage = vorlage
	return _bruecke

static func erbgut_griff(saat: int, volk: String, familie: Dictionary, vorlage := "", _tiefe := 0) -> int:
	var b := bruecke(vorlage)
	if b == null:
		return 0
	var eltern: Array = familie.get(str(saat), [])
	if eltern.size() == 2 and _tiefe < 8:
		var v := erbgut_griff(int(eltern[0]), volk, familie, vorlage, _tiefe + 1)
		var m := erbgut_griff(int(eltern[1]), volk, familie, vorlage, _tiefe + 1)
		return b.call("Vererben", saat, v, m, volk)
	return b.call("Wuerfeln", saat, volk)

static func gene_aus_saat(saat: int, volk := "Mensch", familie := {}, arten: Array = [], vorlage := "") -> Dictionary:
	var g := erbgut_griff(saat, volk, familie, vorlage)
	if g == 0:
		push_error("ErbgutBruecke.cs fehlt oder lässt sich nicht laden")
		return {}
	var werte: Dictionary = bruecke(vorlage).call("SichtbareWerte", g, "m")
	var aus := {}
	for k in werte:
		# Etappe B: Art aus der Vorschlagsdatei (enthält die neuen Gene; erbgut() kennt nur die 40 Gene der Version 1)
		if arten.is_empty() or (erbgut6().has(k) and String(erbgut6()[k]["art"]) in arten):
			aus[k] = float(werte[k])
	return aus

# Etappe B (S7, Familienbogen): Gestalt-Gene (Art „Merkmal“, auch Züge) als Mittel beider Allele
static func gene_allelmittel_aus_saat(saat: int, volk := "Mensch", familie := {}, vorlage := "") -> Dictionary:
	var g := erbgut_griff(saat, volk, familie, vorlage)
	if g == 0:
		return {}
	var sw: Dictionary = bruecke(vorlage).call("SichtbareWerte", g, "m")
	var vw: Dictionary = bruecke(vorlage).call("VerdeckteWerte", g)
	var aus := {}
	for k in sw:
		var e: Dictionary = erbgut6().get(k, {})
		if String(e.get("art", "")) == "Merkmal":
			aus[k] = 0.5 * (float(sw[k]) + float(vw.get(k, sw[k])))
	return aus

static func erbgut_text(saat: int, volk := "Mensch", familie := {}, vorlage := "") -> String:
	var g := erbgut_griff(saat, volk, familie, vorlage)
	return String(bruecke(vorlage).call("AlsText", g)) if g != 0 else ""

# Sichtbarer Wert nach dem Alter verschoben (erbgut.json „alter“: Stützstellen in Menschenjahren)
static func wert_im_alter(kennung: String, wert: float, jahre: float) -> float:
	var g: Dictionary = erbgut().get(kennung, {})
	var stuetzen: Array = g.get("alter", [])
	if stuetzen.is_empty():
		return wert
	var v := float(stuetzen[0]["verschiebung"])
	for i in stuetzen.size():
		var s: Dictionary = stuetzen[i]
		if jahre >= float(s["jahre"]):
			v = float(s["verschiebung"])
			if i + 1 < stuetzen.size():
				var n: Dictionary = stuetzen[i + 1]
				var t := clampf((jahre - float(s["jahre"])) / (float(n["jahre"]) - float(s["jahre"])), 0.0, 1.0)
				v = lerpf(float(s["verschiebung"]), float(n["verschiebung"]), t)
	return clampf(wert + v, 0.0, 255.0)

# MakeHuman-Altersregler (0..1) aus Lebensjahren: 1 J. = 0, 11 J. = 0,1875, 25 J. = 0,5, 90 J. = 1
static func alter_zu_makro(jahre: float) -> float:
	if jahre <= 11.0:
		return maxf(0.0, (jahre - 1.0) / 10.0 * 0.1875)
	if jahre <= 25.0:
		return 0.1875 + (jahre - 11.0) / 14.0 * 0.3125
	return minf(1.0, 0.5 + (jahre - 25.0) / 65.0 * 0.5)

# Gewichte der Stützen [[name, wert], …] für m (stückweise linear, außen festgehalten)
static func stuetzen_gewichte(m: float, stuetzen: Array) -> Dictionary:
	if m <= float(stuetzen[0][1]):
		return {stuetzen[0][0]: 1.0}
	for i in stuetzen.size() - 1:
		var a := float(stuetzen[i][1])
		var b := float(stuetzen[i + 1][1])
		if m <= b:
			var t := (m - a) / (b - a)
			return {stuetzen[i][0]: 1.0 - t, stuetzen[i + 1][0]: t}
	return {stuetzen[-1][0]: 1.0}

func aussehen_setzen(neue_gene: Dictionary, jahre: float, mann: float) -> void:
	for k in neue_gene:
		gene[k] = float(neue_gene[k])
	alter = jahre
	geschlecht = mann
	_anwenden()

func mimik_setzen(werte: Dictionary) -> void:
	mimik = werte.duplicate()
	_anwenden()

func _anwenden() -> void:
	if formen.is_empty():
		return
	var w := {}
	var m := alter_zu_makro(alter)
	var stuetzen := []
	for s in info.get("alter_stuetzen", [{"name": "saeugling", "makro": 0.0}, {"name": "kind", "makro": 0.1875},
			{"name": "jung", "makro": 0.5}, {"name": "alt", "makro": 1.0}]):
		stuetzen.append([s["name"], float(s["makro"])])
	var w_alter := stuetzen_gewichte(m, stuetzen)
	var w_alter_mk := stuetzen_gewichte(maxf(m, 0.1875), stuetzen.slice(1))
	var w_g := {"w": 1.0 - geschlecht, "m": geschlecht}
	var mass: Dictionary = info.get("mimikmass", {})
	mimikfaktor = 0.0
	var kraft := float(gene.get("kraft", 128.0)) / 255.0
	var gewicht := float(gene.get("gewicht", 128.0)) / 255.0
	for g in w_g:
		for a in w_alter:
			var name := "makro_%s_%s" % [g, a]
			w[name] = float(w.get(name, 0.0)) + w_g[g] * w_alter[a]
			mimikfaktor += w_g[g] * w_alter[a] * float(mass.get(name, 1.0))
		for a in w_alter_mk:
			var f: float = w_g[g] * w_alter_mk[a]
			for art_wert in [["kraft", kraft], ["gewicht", gewicht]]:
				var v: float = art_wert[1]
				var richt := "minus" if v < 0.5 else "plus"
				var name := "makro_%s_%s_%s_%s" % [g, a, art_wert[0], richt]
				w[name] = float(w.get(name, 0.0)) + f * absf(v - 0.5) / 0.5
	if not mimik_mit_alter:
		mimikfaktor = 1.0
	if stand >= 6 and formen.has("alter_reife_m_oben"):
		_anwenden6(w)
		return
	# Gestalt-Gene: 128 = keine Form; nur „plus“ (spitze Ohren): Wert / 255
	var gene_info: Dictionary = info.get("gene", {})
	for k in gene_info:
		var fo: Dictionary = gene_info[k].get("formen", {})
		if fo.is_empty():
			continue
		var v := wert_im_alter(k, float(gene.get(k, 128.0)), alter)
		if fo.has("minus"):
			var x := (v - 128.0) / 127.0
			w[fo["minus"]] = maxf(-x, 0.0) * mimikfaktor
			w[fo["plus"]] = maxf(x, 0.0) * mimikfaktor
		else:
			w[fo["plus"]] = v / 255.0 * mimikfaktor
	# Phase 5: erwachsen und greis (nur, wenn die .glb die Formen hat)
	if formen.has("alter_reife_m"):
		w["alter_reife_m"] = kurve_im_alter(REIFE_ALTER["m"], alter) * geschlecht
		w["alter_reife_w"] = kurve_im_alter(REIFE_ALTER["w"], alter) * (1.0 - geschlecht)
		w["alter_greis"] = kurve_im_alter(GREIS_ALTER, alter)
		# Ruhelage der Oberlider: Erwachsene decken den oberen Irisrand 1–2 mm ab (kein „Puppenblick“), im Alter mehr
		var lid: float = 0.1 * kurve_im_alter(REIFE_ALTER["m" if geschlecht >= 0.5 else "w"], alter) + 0.06 * w["alter_greis"]
		for n in ["eyeBlinkLeft", "eyeBlinkRight"]:
			w[n] = float(w.get(n, 0.0)) + lid
	for k in mimik:
		w[k] = float(w.get(k, 0.0)) + float(mimik[k]) * mimikfaktor
	for n in ["eyeBlinkLeft", "eyeBlinkRight"]:
		if w.has(n):
			w[n] = minf(float(w[n]), 1.0)
	for k in zusatz_formen:
		w[k] = float(w.get(k, 0.0)) + float(zusatz_formen[k])
	gewichte = w
	for name in formen:
		form(name, float(w.get(name, 0.0)))
	_merkpunkte(w)
	_farben()

# ---------------------------------------------------------------- Stand 6 (Etappe B)
static func regeln6(pfad := REGELN6) -> Dictionary:
	if not _regeln6.has(pfad) and FileAccess.file_exists(pfad):
		_regeln6[pfad] = JSON.parse_string(FileAccess.get_file_as_string(pfad))
	return _regeln6.get(pfad, {})

# Genvorlage mit den Vorschlagsgenen (Mittel und Streuung der neuen Gene); die bestehenden Gene sind darin unverändert
static func erbgut6() -> Dictionary:
	if _erbgut6.is_empty():
		var pfad := VORSCHLAG if FileAccess.file_exists(VORSCHLAG) else ERBGUT
		var d: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(pfad))
		for g in d["gene"]:
			_erbgut6[g["kennung"]] = g
	return _erbgut6

static func _sigma(kennung: String, wert: float, v: String) -> float:
	var g: Dictionary = erbgut6().get(kennung, {})
	if g.is_empty():
		return 0.0
	var e: Dictionary = g["voelker"].get(v, g["voelker"]["Mensch"])
	var s := float(e["streuung"])
	return (wert - float(e["mittel"])) / s if s > 0.0 else 0.0

func _anwenden6(w: Dictionary) -> void:
	var R := regeln6(regeln_pfad)
	var ku: Dictionary = R["alterskurven"]
	var mann := geschlecht
	var ro := kurve_im_alter(ku["reife_oben_m"], alter) * mann + kurve_im_alter(ku["reife_oben_w"], alter) * (1.0 - mann)
	var ru := kurve_im_alter(ku["reife_unten_m"], alter) * mann + kurve_im_alter(ku["reife_unten_w"], alter) * (1.0 - mann)
	w["alter_reife_m_oben"] = kurve_im_alter(ku["reife_oben_m"], alter) * mann
	w["alter_reife_m_unten"] = kurve_im_alter(ku["reife_unten_m"], alter) * mann
	w["alter_reife_w_oben"] = kurve_im_alter(ku["reife_oben_w"], alter) * (1.0 - mann)
	w["alter_reife_w_unten"] = kurve_im_alter(ku["reife_unten_w"], alter) * (1.0 - mann)
	var mitte := kurve_im_alter(ku["mitte"], alter)
	w["alter_mitte_m"] = mitte * mann
	w["alter_mitte_w"] = mitte * (1.0 - mann)
	w["alter_greis"] = kurve_im_alter(ku["greis"], alter)
	var neu: bool = volk in R["schalter"]["neue_rechnung_voelker"]
	var reg_gene: Dictionary = R["gene"]
	# σ-Werte der Gestalt-Gene (und der Züge) – Grundlage für Karikaturbremse und Gewichte
	var z := {}
	for k in reg_gene:
		var e: Dictionary = reg_gene[k]
		if e.get("absolut", false) or e.has("aus_gen"):
			continue
		var v := wert_im_alter6(k, float(gene.get(k, _mittel6(k))), alter)
		z[k] = _sigma(k, v, volk) if neu else (v - 128.0) / 127.0
	var zuege: Dictionary = R.get("zuege", {}).get("gene", {})
	if neu and R["schalter"].get("zuege", false) and not zuege_aus:
		for k in zuege:
			z[k] = (float(gene.get(k, 128.0)) - float(R["zuege"]["mitte"])) / float(R["zuege"]["streuung"])
	karikatur = {}
	if neu and R["schalter"].get("karikaturbremse", false):
		_karikatur_bremse(z, R["karikatur"])
	# Verschiedenheit E1 (nur Probeköpfe mit „verschieden“ in ihrer regeln6.json): Nasenbudget – Länge, Spitze und Höcker
	# zusammen höchstens norm_max (√Σz²), sonst alle drei gemeinsam kleiner (Ausreißer aus drei gleichsinnigen Nasengenen)
	var vs: Dictionary = R.get("verschieden", {}) if neu else {}
	if vs.has("nasenbudget"):
		var nb: Dictionary = vs["nasenbudget"]
		var s2 := 0.0
		for k in nb["gene"]:
			s2 += pow(float(z.get(k, 0.0)), 2.0)
		if sqrt(s2) > float(nb["norm_max"]):
			karikatur["nasenbudget"] = sqrt(s2)
			for k in nb["gene"]:
				if z.has(k):
					z[k] = float(z[k]) * float(nb["norm_max"]) / sqrt(s2)
		# Runde 3: Stupsnase (Spitze hoch und Nase kurz zugleich) zeigt die Nasenlöcher von vorn wie eine Schweinsnase –
		# Spitze⁺ + Kürze⁻ zusammen höchstens stupsnase_max (in σ), sonst beide kleiner
		if nb.has("stupsnase_max"):
			var hoch := maxf(float(z.get("nasenspitze", 0.0)), 0.0)
			var kurz := maxf(-float(z.get("nase_laenge", 0.0)), 0.0)
			if hoch + kurz > float(nb["stupsnase_max"]):
				var f := float(nb["stupsnase_max"]) / (hoch + kurz)
				karikatur["stupsnase"] = hoch + kurz
				if hoch > 0.0:
					z["nasenspitze"] = hoch * f
				if kurz > 0.0:
					z["nase_laenge"] = -kurz * f
	gene_z = z
	# Gewichte der Gestalt-Gene
	var gene_w := {}
	for k in reg_gene:
		var e: Dictionary = reg_gene[k]
		var fo: Dictionary = e["formen"]
		if e.get("absolut", false):
			gene_w[fo["plus"]] = float(gene.get(k, _mittel6(k))) / 255.0 * mimikfaktor
			continue
		var zk := 0.0
		if e.has("aus_gen"):
			zk = _sigma(e["aus_gen"], float(gene.get(e["aus_gen"], _mittel6(e["aus_gen"]))), volk) if neu else 0.0
		else:
			zk = float(z.get(k, 0.0))
		if not neu:
			# andere Völker: Rechnung der Phase 5 bis Phase 7 (128 = keine Form, ± 127)
			gene_w[fo["minus"]] = maxf(-zk, 0.0) * mimikfaktor
			gene_w[fo["plus"]] = maxf(zk, 0.0) * mimikfaktor
			continue
		var st: Dictionary = e["staerke"]
		var richt := "plus" if zk >= 0.0 else "minus"
		var s := float(st["m"][richt]) * mann + float(st["w"][richt]) * (1.0 - mann)
		var reife := 1.0
		if e.get("reife", "") == "oben":
			reife = ro
		elif e.get("reife", "") == "unten":
			reife = ru
		gene_w[fo[richt]] = absf(zk) * s * reife * mimikfaktor
		gene_w[fo["minus" if richt == "plus" else "plus"]] = 0.0
	if neu and R["schalter"].get("budget", false):
		_budget(w, gene_w, R)
	for k in gene_w:
		w[k] = gene_w[k]
	# Verschiedenheit E1: feste Zugaben je Geschlecht (× Reife oben), z. B. Nasenspitze der Frau etwas tiefer (Nasenlöcher
	# von vorn weniger sichtbar); stehen außerhalb des Budgets
	for gs in vs.get("zusatz_w", {}):
		var anteil := mann if gs == "m" else 1.0 - mann
		for k in vs["zusatz_w"][gs]:
			w[k] = float(w.get(k, 0.0)) + float(vs["zusatz_w"][gs][k]) * anteil * ro * mimikfaktor
	# Gesichtszüge (nur Menschen; bei Kindern × Reife)
	if neu and R["schalter"].get("zuege", false) and not zuege_aus:
		for k in zuege:
			w[zuege[k]] = float(z.get(k, 0.0)) * float(R["zuege"]["faktor"]) * ru * mimikfaktor
	# Asymmetrie aus dem Personen-Samen (nicht erblich): Gewicht ~ N(0, Streuung); Grenzen je Merkmal, sonst kleiner
	if neu and R["schalter"].get("asymmetrie", false) and aussehen_samen != 0:
		var a: Dictionary = R["asymmetrie"]
		var rng := RandomNumberGenerator.new()
		rng.seed = hash("asymmetrie:%d" % aussehen_samen)
		var gw := {}
		var d := []
		d.resize(a["merkmale"].size())
		d.fill(0.0)
		for n in a["formen"]:
			var f: Dictionary = a["formen"][n]
			gw[n] = rng.randfn(0.0, 1.0) * float(f["streuung"])
			for j in d.size():
				d[j] = float(d[j]) + gw[n] * float(f["merkmale_mm_je_1"][j])
		var faktor := 1.0
		for j in d.size():
			if absf(d[j]) > 1e-9:
				faktor = minf(faktor, float(a["grenzen_mm"][j]) / absf(d[j]))
		karikatur["asymmetrie_faktor"] = faktor
		for n in gw:
			w[n] = gw[n] * faktor * mimikfaktor
	# Ruhelage der Oberlider wie Phase 5, dazu die Lidspalte
	var lr: Dictionary = R["lid_ruhe"]
	var lid: float = float(lr["reife"]) * ro + float(lr["greis"]) * w["alter_greis"] + float(lr["lidspalte_je_sigma"]) * float(z.get("lidspalte", 0.0))
	for n in ["eyeBlinkLeft", "eyeBlinkRight"]:
		w[n] = float(w.get(n, 0.0)) + maxf(lid, 0.0)
	for k in mimik:
		w[k] = float(w.get(k, 0.0)) + float(mimik[k]) * mimikfaktor
	for n in ["eyeBlinkLeft", "eyeBlinkRight"]:
		if w.has(n):
			w[n] = minf(float(w[n]), 1.0)
	# Lidfalte (eigene Form): voll mit der Reife oben, beim Lidschluss ausgeblendet (die Rinne läge sonst auf dem Lid)
	if formen.has("lidfalte_m"):
		var b := 0.5 * (float(w.get("eyeBlinkLeft", 0.0)) + float(w.get("eyeBlinkRight", 0.0)))
		var ruhe := maxf(lid, 0.0)
		var auf := 1.0 - clampf((b - ruhe) / maxf(1.0 - ruhe, 0.01), 0.0, 1.0)
		w["lidfalte_m"] = kurve_im_alter(ku["reife_oben_m"], alter) * mann * auf * mimikfaktor
		w["lidfalte_w"] = kurve_im_alter(ku["reife_oben_w"], alter) * (1.0 - mann) * auf * mimikfaktor
	for k in zusatz_formen:
		w[k] = float(w.get(k, 0.0)) + float(zusatz_formen[k])
	gewichte = w
	for name in formen:
		form(name, float(w.get(name, 0.0)))
	_merkpunkte(w)
	_farben()

# Mittelwerte aller Gene der Vorschlagsdatei (Etappe B), für Zellen ohne Saat
static func mittelwerte6(v: String) -> Dictionary:
	var aus := {}
	for k in erbgut6():
		var g: Dictionary = erbgut6()[k]
		aus[k] = float(g["voelker"].get(v, g["voelker"]["Mensch"])["mittel"])
	return aus

static func _mittel6(k: String) -> float:
	var g: Dictionary = erbgut6().get(k, {})
	return float(g["voelker"]["Mensch"]["mittel"]) if not g.is_empty() else 128.0

# Altersverschiebung (erbgut_vorschlag.json „alter“), wie wert_im_alter, aber auch für neue Gene
static func wert_im_alter6(kennung: String, wert: float, jahre: float) -> float:
	var g: Dictionary = erbgut6().get(kennung, {})
	var stuetzen: Array = g.get("alter", [])
	if stuetzen.is_empty():
		return wert
	var v := float(stuetzen[0]["verschiebung"])
	for i in stuetzen.size():
		var st: Dictionary = stuetzen[i]
		if jahre >= float(st["jahre"]):
			v = float(st["verschiebung"])
			if i + 1 < stuetzen.size():
				var n: Dictionary = stuetzen[i + 1]
				v = lerpf(float(st["verschiebung"]), float(n["verschiebung"]), clampf((jahre - float(st["jahre"])) / (float(n["jahre"]) - float(st["jahre"])), 0.0, 1.0))
	return clampf(wert + v, 0.0, 255.0)

# Karikaturbremse (Plan S5.5): K = √(Mittel z²); K > K_max -> alle z gemeinsam kleiner; höchstens zwei Merkmale über
# |z| 2 (die übrigen auf 2); 0,05 < K < K_min -> die stärksten Abweichungen auf |z| 1,2 (nur mit Personen-Samen).
func _karikatur_bremse(z: Dictionary, k: Dictionary) -> void:
	if z.is_empty():
		return
	var summe := 0.0
	for n in z:
		summe += float(z[n]) * float(z[n])
	var K := sqrt(summe / z.size())
	karikatur["K_vorher"] = K
	if K > float(k["K_max"]):
		for n in z:
			z[n] = float(z[n]) * float(k["K_max"]) / K
	var namen := z.keys()
	namen.sort_custom(func(a, b): return absf(float(z[a])) > absf(float(z[b])))
	var grenze := float(k["z_grenze"])
	for i in namen.size():
		if i >= int(k["hoechstens_ueber"]) and absf(float(z[namen[i]])) > grenze:
			z[namen[i]] = signf(float(z[namen[i]])) * grenze
	# Anheben nur bei Personen mit Samen (gewürfelt); das Mittelgesicht bleibt Mittel, auch mit den Altersverschiebungen
	if aussehen_samen != 0 and K > 0.05 and K < float(k["K_min"]):
		for i in mini(int(k["anheben_anzahl"]), namen.size()):
			var v := float(z[namen[i]])
			if absf(v) < float(k["z_anheben"]):
				z[namen[i]] = signf(v) * float(k["z_anheben"])
	summe = 0.0
	for n in z:
		summe += float(z[n]) * float(z[n])
	karikatur["K_nachher"] = sqrt(summe / z.size())

# Budget je MakeHuman-Ziel (Plan S5.5): Summe aus Alters- und Genformen in [unten; oben]; gekürzt werden nur Gen-Anteile
func _budget(w: Dictionary, gene_w: Dictionary, R: Dictionary) -> void:
	var zm: Dictionary = R["zielmatrix"]
	var alt_teil := {}
	var gen_teil := {}
	for f in w:
		if zm.has(f) and String(f).begins_with("alter_"):
			for t in zm[f]:
				alt_teil[t] = float(alt_teil.get(t, 0.0)) + float(w[f]) * float(zm[f][t])
	for f in gene_w:
		if zm.has(f) and float(gene_w[f]) != 0.0:
			for t in zm[f]:
				gen_teil[t] = float(gen_teil.get(t, 0.0)) + float(gene_w[f]) * float(zm[f][t])
	var faktor_t := {}
	var B: Dictionary = R["budget"]
	var unten := float(B["unten"])
	var oben := float(B["oben"])
	# Verschiedenheit E1 (Modus „gen“): begrenzt wird der Gen-Anteil allein – die Reifeformen verbrauchen den Spielraum
	# nicht mehr (vorher starben so ganze Richtungen, z. B. Wangenknochen schmaler bei Frauen) –, dazu eine weitere Grenze
	# für die Summe. An den Zielen in „ziele_gesamt“ (Nase) zählt die Summe wie bisher.
	var nur_gen := String(B.get("modus", "")) == "gen"
	var ziele_ges: Array = B.get("ziele_gesamt", [])
	for t in gen_teil:
		var a := float(alt_teil.get(t, 0.0))
		var g := float(gen_teil[t])
		if nur_gen and not ziele_ges.has(t):
			var f := 1.0
			if g > float(B["gen_oben"]):
				f = float(B["gen_oben"]) / g
			elif g < float(B["gen_unten"]):
				f = float(B["gen_unten"]) / g
			if g > 0.0 and a + g * f > float(B["gesamt_oben"]):
				f = minf(f, maxf((float(B["gesamt_oben"]) - a) / g, 0.0))
			elif g < 0.0 and a + g * f < float(B["gesamt_unten"]):
				f = minf(f, maxf((float(B["gesamt_unten"]) - a) / g, 0.0))
			if f < 1.0:
				faktor_t[t] = clampf(f, 0.0, 1.0)
			continue
		var ges := a + g
		if ges > oben and g > 0.0:
			faktor_t[t] = clampf((oben - a) / g, 0.0, 1.0)
		elif ges < unten and g < 0.0:
			faktor_t[t] = clampf((unten - a) / g, 0.0, 1.0)
	if faktor_t.is_empty():
		return
	var gekuerzt := {}
	for f in gene_w:
		if not zm.has(f):
			continue
		var fa := 1.0
		for t in zm[f]:
			if faktor_t.has(t) and float(zm[f][t]) * float(gene_w[f]) * float(gen_teil[t]) > 0.0:
				fa = minf(fa, float(faktor_t[t]))
		if fa < 1.0:
			gene_w[f] = float(gene_w[f]) * fa
			gekuerzt[f] = fa
	karikatur["budget_gekuerzt"] = gekuerzt

# Lage von Augen, Mund, Scheitel aus der Beschreibung (Grundlage + Versatz je Form) für die Kamera
func _merkpunkte(w: Dictionary) -> void:
	var mp: Dictionary = info.get("merkpunkte", {})
	if mp.is_empty():
		return
	var aus := {}
	for k in mp:
		aus[k] = Vector3(mp[k][0], mp[k][1], mp[k][2])
	var mf: Dictionary = info.get("merkpunkt_formen", {})
	for name in mf:
		var g := float(w.get(name, 0.0))
		if g == 0.0:
			continue
		for k in mf[name]:
			var d: Array = mf[name][k]
			aus[k] += Vector3(d[0], d[1], d[2]) * g
	augen_mitte = aus.get("augen", augen_mitte)
	mund_mitte = aus.get("mund", mund_mitte)
	oben = (aus.get("scheitel", Vector3(0, oben, 0)) as Vector3).y

# ---------------------------------------------------------------- Farben aus den Farb-Genen
# Hautfarbe (sRGB) aus „Hautfarbe“: dunkel – mittel – hell (die gebackene Haut trägt nur die Abweichung davon)
static func hautfarbe(h: float) -> Color:
	if h < 0.5:
		return Color(0.2, 0.125, 0.09).lerp(Color(0.55, 0.37, 0.27), h / 0.5)
	return Color(0.55, 0.37, 0.27).lerp(Color(0.84, 0.66, 0.56), (h - 0.5) / 0.5)

# Phase 6 (S2, vorläufig bis S9): Hautfarbe linear (Albedo) aus „Hautfarbe“, Anker nach PhysicallyBased (CC0, Haut
# I–VI linear): Gen 235 ≈ Typ I–II, 185 ≈ II–III, 135 ≈ III–IV; darunter IV, V, VI, darüber I. Linear zwischen den
# Ankern (im linearen Raum).
const HAUT6_ANKER := [
	[0.0, Color(0.09, 0.05, 0.02)],         # VI
	[60.0, Color(0.283, 0.148, 0.079)],     # V
	[100.0, Color(0.436, 0.227, 0.131)],    # IV
	[135.0, Color(0.5295, 0.33, 0.237)],    # III–IV
	[185.0, Color(0.711, 0.459, 0.345)],    # II–III
	[235.0, Color(0.823, 0.5615, 0.4495)],  # I–II
	[255.0, Color(0.847, 0.638, 0.552)],    # I
]
static func hautfarbe6(gen: float) -> Color:
	if gen <= HAUT6_ANKER[0][0]:
		return HAUT6_ANKER[0][1]
	for i in HAUT6_ANKER.size() - 1:
		var a: Array = HAUT6_ANKER[i]
		var b: Array = HAUT6_ANKER[i + 1]
		if gen <= float(b[0]):
			return (a[1] as Color).lerp(b[1], (gen - float(a[0])) / (float(b[0]) - float(a[0])))
	return HAUT6_ANKER[-1][1]

# Phase 6 (S2, vorläufig bis S7): Augen linear. Dunkelbraun – (Bernstein – Hasel/Grün – Blau) nach „Augenhelligkeit“
# und „Augenton“; echte Iris ist dunkel (Bildleuchtdichte der Vorlagen 0,02–0,06, Phase 5 0,30–0,37).
const IRIS6_DUNKEL := Color(0.030, 0.014, 0.006)
const IRIS6_BERNSTEIN := Color(0.17, 0.085, 0.022)
const IRIS6_GRUEN := Color(0.075, 0.095, 0.035)
const IRIS6_BLAU := Color(0.075, 0.12, 0.20)
const KRANZ6 := [Color(0.13, 0.065, 0.016), Color(0.22, 0.12, 0.03)]
const SKLERA6 := Color(0.50, 0.44, 0.38)        # warm, dunkler als die beleuchtete Haut daneben (Vorlagen)
const ZAHNFLEISCH6 := Color(0.46, 0.13, 0.12)

# Gewicht einer Alterskurve [[jahre, wert], …] (stückweise linear, außen festgehalten)
static func kurve_im_alter(kurve: Array, jahre: float) -> float:
	if jahre <= float(kurve[0][0]):
		return float(kurve[0][1])
	for i in kurve.size() - 1:
		var a: Array = kurve[i]
		var b: Array = kurve[i + 1]
		if jahre <= float(b[0]):
			return lerpf(float(a[1]), float(b[1]), (jahre - float(a[0])) / (float(b[0]) - float(a[0])))
	return float(kurve[-1][1])

var werte3 := {}            # zuletzt gesetzte Phase-3-Werte (zum Prüfen und für die Anzeige)

func _farben() -> void:
	var h := float(gene.get("haut_helligkeit", 185.0)) / 255.0
	var ton := float(gene.get("haut_ton", 105.0)) / 255.0
	var st := stand if phase3 else 5
	var grund := hautfarbe(h) if phase3 else Color(0.27, 0.16, 0.10).lerp(Color(0.84, 0.64, 0.53), h)
	if st >= 6:
		grund = linear(hautfarbe6(h * 255.0))
	var toenung := Vector3.ONE
	if ton < 0.5:
		toenung = Vector3(1.07, 0.96, 0.95).lerp(Vector3.ONE, ton / 0.5)
	else:
		toenung = Vector3.ONE.lerp(Vector3(0.78, 0.95, 0.74), (ton - 0.5) / 0.5)
	for m in haut_stoffe + haut2_stoffe:
		farbe_setzen(m, "grundfarbe", grund, st if m in haut_stoffe else 5)
		m.set_shader_parameter("toenung", toenung)
	var hh := float(gene.get("haar_helligkeit", 100.0)) / 255.0
	var rot := float(gene.get("haar_rot", 70.0)) / 255.0
	var haar := Color(0.035, 0.028, 0.022).lerp(Color(0.78, 0.66, 0.45), hh)
	haar = haar.lerp(Color(0.5, 0.2, 0.07).lerp(Color(0.85, 0.45, 0.2), hh), rot * 0.8)
	# Ergrauen: ab 20 + Gen/255·50 Jahren, über 25 Jahre fast ganz grau
	var grau_ab := 20.0 + float(gene.get("ergrauen", 128.0)) / 255.0 * 50.0
	var grau := clampf((alter - grau_ab) / 25.0, 0.0, 0.85)
	var mann := geschlecht >= 0.5
	if phase3:
		# Brauen: Dichte aus dem Bartwuchs (Männer „bartwuchs“, Frauen dazu „bartwuchs_frau“), Kinder lichter,
		# alte Männer buschiger; Frauen schmaler. Graue Haare einzeln (Salz und Pfeffer).
		# Phase 5: Frauen haben ein eigenes Brauenbild (schmaler, Bogen, dünnes Ende) und volle Dichte – vorher schnitten
		# Dichte 0,65 und Breite 0,72 die Brauen fast weg. Brauen sind dunkler als das Haupthaar, bei hellem Haar mehr.
		var bw := float(gene.get("bartwuchs", 130.0)) / 255.0
		var bwf := float(gene.get("bartwuchs_frau", 0.0)) / 255.0
		var dichte := lerpf(0.7 + 0.12 * bw + 0.08 * bwf, 0.7 + 0.3 * bw, geschlecht)
		dichte *= lerpf(0.65, 1.0, clampf((alter - 3.0) / 12.0, 0.0, 1.0))
		if mann:
			dichte += 0.12 * clampf((alter - 55.0) / 15.0, 0.0, 1.0)
		var breite := lerpf(0.85, 1.0, clampf((alter - 3.0) / 12.0, 0.0, 1.0))
		var braue := haar.lerp(haar * 0.42, 0.35 + 0.35 * hh)
		var brauen_bild: Texture2D = bilder.get("brauen_w", bilder["brauen"]) if not mann else bilder["brauen"]
		if not bilder.has("brauen_w"):
			breite *= lerpf(0.72, 1.0, geschlecht)
		var ebene := -1.0
		var bd := 0.5
		if st >= 6 and bilder.has("brauen6") and formen.has("alter_reife_m_oben"):
			# Etappe B (S7): Form aus „brauen_form“, Dichte und Breite aus „brauen_dichte“ (Vorschlagsgene)
			ebene = float(brauen_ebene(float(gene.get("brauen_form", 128.0))) + (0 if mann else 6))
			bd = clampf((float(gene.get("brauen_dichte", 128.0)) - 40.0) / 180.0, 0.0, 1.0)
			dichte *= lerpf(0.72, 1.15, bd)
			breite *= lerpf(0.88, 1.08, bd)
			if not bilder.has("brauen_w"):
				breite /= lerpf(0.72, 1.0, geschlecht)
		var a_an := st >= 6 and weg_a
		var d_an := a_an and weg_d
		if a_an:
			# Weg A: Brauen nur wenig dunkler als das Haar (weniger Kontrast), etwas lichter und weicher an den Rändern
			braue = haar.lerp(haar * 0.8, 0.2 + 0.2 * hh)
			if mann:
				dichte *= 0.85        # Frauenbrauen sind schon schmal und licht (sonst fast unsichtbar, Zufallsbogen)
		if d_an:
			# Etappe D: Brauen wie das Haar, kaum dunkler; die weichen Bilder haben weniger und feinere Randhaare, daher
			# volle Dichte
			braue = haar.lerp(haar * 0.85, 0.15 + 0.15 * hh)
			dichte = minf(dichte / 0.85, 1.0) if mann else dichte
		# Verschiedenheit E1 (Probekopf mit „verschieden.brauen“): Brauen je Person stark verschieden – Dunkelheit gegen das
		# Haar (im Mittel wie Etappe D, + dunkler, − heller als das Haar), Dichte breiter gestreut; Winkel und Dunkelheit
		# vorläufig aus den freien Vorschlagsgenen (zug_11, zug_12; Vorschlag an kern: brauen_winkel, brauen_dunkel)
		var vb: Dictionary = regeln6(regeln_pfad).get("verschieden", {}).get("brauen", {}) if d_an and volk == "Mensch" else {}
		if not vb.is_empty():
			var dz := clampf(_sigma(vb["dunkel_gen"], float(gene.get(vb["dunkel_gen"], _mittel6(vb["dunkel_gen"]))), volk), -2.5, 2.5)
			if dz >= 0.0:
				braue = braue * (1.0 - float(vb["dunkel_je_sigma"]) * dz)
			else:
				var hell := float(vb["hell_je_sigma"]) * (1.0 if mann else float(vb.get("frau_hell", 1.0)))
				braue = braue.lerp(haar.lerp(Color(0.62, 0.5, 0.38), 0.5), hell * -dz)
			braue.a = 1.0
			dichte = dichte / lerpf(0.72, 1.15, bd) * lerpf(float(vb["dichte_min"]), float(vb["dichte_max"]), bd)
			if not mann:
				dichte *= float(vb.get("frau_dichte", 1.0))
		for m in haar_stoffe:
			if m.shader in [HAAR3, BRAUE6]:
				m.shader = BRAUE6 if d_an else HAAR3
			m.set_shader_parameter("bild", brauen_bild)
			m.set_shader_parameter("bilder6", bilder.get("brauen6"))
			m.set_shader_parameter("ebene", ebene)
			m.set_shader_parameter("deckung", (1.05 if mann else 1.05) if d_an else ((0.68 if mann else 0.88) if a_an else 1.0))
			m.set_shader_parameter("mip_hub", 0.3)
			m.set_shader_parameter("schwelle", 0.12 if d_an else (0.2 if a_an else 0.3))
			m.set_shader_parameter("dunkel", 0.9 if d_an else (0.78 if a_an else 0.6))
			m.set_shader_parameter("zufall_hell", 0.7 if d_an else 0.4)
			if m.shader == BRAUE6:
				# Brauen je Person (Verschiedenheitsplan, Hebel 1): Stand 1,5 mm je σ zusätzlich zur Hautform (die bringt
				# 1,0 mm/σ, zusammen 2,5), Dicke aus „brauen_dichte“ (σ 35: 0,6–1,4 über ±2σ), Winkel aus einem Gen
				# „brauen_winkel“, wenn es eins gibt (Vorschlag an kern, heute 0). Mittelgesicht = unverändert.
				m.set_shader_parameter("braue", true)
				var z_dichte := _sigma("brauen_dichte", float(gene.get("brauen_dichte", 128.0)), volk)
				if vb.is_empty():
					m.set_shader_parameter("v_nach_unten", false)
					m.set_shader_parameter("stand_oben_max", 0.0)
					m.set_shader_parameter("mitte_v", 0.62)
					m.set_shader_parameter("stand_mm", 1.5 * float(gene_z.get("brauen_stand", 0.0)))
					m.set_shader_parameter("dicke", clampf(1.0 + 0.2 * z_dichte, 0.7, 1.4))
					m.set_shader_parameter("winkel_mm", 2.0 * float(gene_z.get("brauen_winkel", 0.0)))
					m.set_shader_parameter("rand_aus", 0.0)
					m.set_shader_parameter("stand_unten_max", 0.0)
					m.set_shader_parameter("rand_aus_kante", 0.38)
				else:
					# E1: v richtig herum (siehe braue6), Stand zusätzlich zur Hautform, oben weich begrenzt; Dicke wächst
					# um eine Linie im oberen Drittel, also vor allem nach unten (oben ist in der Karte wenig Platz)
					var wz := clampf(_sigma(vb["winkel_gen"], float(gene.get(vb["winkel_gen"], _mittel6(vb["winkel_gen"]))), volk), -2.5, 2.5)
					m.set_shader_parameter("v_nach_unten", bool(vb.get("v_nach_unten", true)))
					m.set_shader_parameter("stand_oben_max", float(vb["stand_oben_max_mm"]))
					m.set_shader_parameter("mitte_v", float(vb["mitte_v"]))
					m.set_shader_parameter("stand_mm", float(vb["stand_mm_je_sigma"]) * float(gene_z.get("brauen_stand", 0.0)))
					var fd := 1.0 if mann else float(vb.get("frau_dicke", 1.0))
					# Runde 3: dünner nur wenig (gestaucht zerfiel die Braue zum gestrichelten Strich); dünne Brauen kommen
					# aus der Form („dünn auslaufend“) und der Dichte
					var kd := float(vb["dicke_je_sigma"]) if z_dichte >= 0.0 else float(vb.get("dicke_je_sigma_minus", vb["dicke_je_sigma"]))
					m.set_shader_parameter("dicke", clampf((1.0 + kd * z_dichte) * fd, float(vb["dicke_min"]), float(vb["dicke_max"])))
					m.set_shader_parameter("winkel_mm", float(vb["winkel_mm_je_sigma"]) * wz)
					m.set_shader_parameter("rand_aus", float(vb.get("rand_aus", 0.0)))
					# E3: nach unten weich begrenzt, breiter ausgeblendet am Umriss (fehlen die Werte: wie E1)
					m.set_shader_parameter("stand_unten_max", float(vb.get("stand_unten_max_mm", 0.0)))
					m.set_shader_parameter("rand_aus_kante", float(vb.get("rand_aus_kante", 0.38)))
			farbe_setzen(m, "farbe", braue, st)
			# Etappe D: Brauen ergrauen später als das Haupthaar und bleiben grau-braun statt weiß (Prüfer: „weiße Watte“)
			m.set_shader_parameter("grau", grau * 0.6 if d_an else grau)
			if d_an:
				farbe_setzen(m, "farbe_grau", Color(0.55, 0.52, 0.49), st)
			else:
				m.set_shader_parameter("farbe_grau", null)      # Vorgabe des Shaders (wie bisher)
			m.set_shader_parameter("dichte", clampf(dichte, 0.0, 1.0))
			m.set_shader_parameter("breite", breite)
		var wimper := haar.darkened(0.7)
		for m in wimpern_stoffe:
			m.set_shader_parameter("schwelle", 0.35)
			farbe_setzen(m, "farbe", wimper, st)
			m.set_shader_parameter("grau", grau * 0.5)
			m.set_shader_parameter("dichte", lerpf(1.0, 0.8, geschlecht))
		# Falten aus dem Alter; helle Haut faltet etwas früher, Leibesfülle glättet die Stirn
		var fett := float(gene.get("gewicht", 128.0)) / 255.0
		var faktor := 0.9 + 0.25 * (h - 0.5) * 2.0 * 0.5
		# Mimik vertieft die Falten (Ausdrucksfalten): Brauen heben -> Stirn, Lächeln/Zusammenkneifen -> Augen und
		# Mund; bei Jungen schwächer
		var mf := clampf((alter - 12.0) / 20.0, 0.25, 1.0) * mimikfaktor
		var m_stirn := maxf(float(mimik.get("browInnerUp", 0.0)), maxf(float(mimik.get("browOuterUpLeft", 0.0)), float(mimik.get("browOuterUpRight", 0.0))))
		m_stirn = maxf(m_stirn, 0.6 * maxf(float(mimik.get("browDownLeft", 0.0)), float(mimik.get("browDownRight", 0.0))))
		var m_auge := maxf(maxf(float(mimik.get("cheekSquintLeft", 0.0)), float(mimik.get("cheekSquintRight", 0.0))),
			maxf(float(mimik.get("eyeSquintLeft", 0.0)), float(mimik.get("eyeSquintRight", 0.0))))
		var m_mund := maxf(float(mimik.get("mouthSmileLeft", 0.0)), float(mimik.get("mouthSmileRight", 0.0)))
		var f := Vector4(kurve_im_alter(FALTEN_ALTER["stirn"], alter) * (1.15 - 0.3 * fett),
			kurve_im_alter(FALTEN_ALTER["augen"], alter), kurve_im_alter(FALTEN_ALTER["mund"], alter) * (0.85 + 0.3 * fett),
			kurve_im_alter(FALTEN_ALTER["hals"], alter)) * faktor * (1.0 if mann else 0.85)
		f += Vector4(0.7 * m_stirn, 0.6 * m_auge, 0.5 * m_mund, 0.0) * mf
		# Bartschatten: Männer ab 15 nach „bartwuchs“, Frauen nach „bartwuchs_frau“; heller Bart zeigt sich kaum
		var bart_gen := bw if mann else bwf
		var bart := clampf((alter - 15.0) / 8.0, 0.0, 1.0) * pow(bart_gen, 0.7) * 0.8 * (1.0 - 0.75 * hh)
		# Phase 5: Haut der Erwachsenen weniger „Porzellan“: Rötung der Wangen (Kindchenschema) zurück, Poren deutlicher,
		# Männer mit kräftigerem Bartschatten; im Alter wieder etwas Rötung (Äderchen)
		var reif := kurve_im_alter(REIFE_ALTER["m" if mann else "w"], alter)
		var greis := kurve_im_alter(GREIS_ALTER, alter)
		bart = clampf(bart * lerpf(1.0, 1.45, reif), 0.0, 0.85) if mann else bart
		for m in haut_stoffe:
			m.set_shader_parameter("roetung", lerpf(0.34, 0.2 if mann else 0.25, reif) + 0.06 * greis)
			m.set_shader_parameter("poren_staerke", lerpf(0.32, 0.62 if mann else 0.5, reif) + 0.08 * greis)
			# Stand 6: „glanz“ ist ein Faktor auf F0 0,028 (haut6), Stand 5 der Spiegelanteil der Phase 5
			m.set_shader_parameter("glanz", 1.0 if st >= 6 else lerpf(0.34, 0.3, reif))
			m.set_shader_parameter("unruhe", lerpf(0.03, 0.11 if mann else 0.075, reif) + 0.05 * greis)
			m.set_shader_parameter("falten", f)
			m.set_shader_parameter("bart", bart)
			m.set_shader_parameter("brauen_dichte", clampf(dichte, 0.0, 1.0))
			farbe_setzen(m, "haar_farbe", haar, st)
		werte3 = {"falten": f, "bart": bart, "brauen_dichte": dichte, "brauen_breite": breite, "grau": grau,
			"grundfarbe": grund, "brauen_ebene": ebene}
		# Etappe C: Haut aus Zonen, Relief und Kacheln (nur Weg A + C, Stand 6)
		var c_an := st >= 6 and weg_a and weg_c and bilder.has("haut_c_relief")
		if c_an:
			_haut_c_setzen(mann, h, hh, bart, reif, greis, fett, faktor,
				Vector4(0.7 * m_stirn, 0.6 * m_auge, 0.3 * m_mund, 0.3 * m_mund) * mf, grund)
			# Etappe D: Lidrand und Karunkel nasser, Haut unter der Braue etwas dunkler (Haarwurzeln, Schatten der Haare)
			for m in haut_stoffe:
				m.set_shader_parameter("feucht", 1.7 if weg_d else 1.0)
				# E1: der Hautschatten liegt fest an der Grundbraue und wandert nicht mit – schwächer, sonst doppelte Braue
				m.set_shader_parameter("brauen_weich", float(vb["haut_schatten"]) if not vb.is_empty() else (0.42 if weg_d else 0.3))
				m.set_shader_parameter("brauen_glanz", 0.85 if weg_d else 0.0)
				if weg_d:
					m.set_shader_parameter("c_loch", 0.38)   # Nasenloch innen nicht als schwarzes Loch (Runde 2) …
					m.set_shader_parameter("c_loch_braun", 0.8)   # … und nicht rot ausgeleuchtet (Runde 3)
				else:
					m.set_shader_parameter("c_loch_braun", 0.0)
		for m in wimpern_stoffe:
			if m.shader in [HAAR3, BRAUE6]:
				m.shader = BRAUE6 if d_an else HAAR3
			# Etappe C: Unterlid-Wimpern licht, dünn, Wurzel weich; alle Wimpern etwas weniger schwarz und deckend
			m.set_shader_parameter("unten_deckung", 0.3 if c_an else 1.0)
			m.set_shader_parameter("unten_wurzel", 1.0 if c_an else 0.0)
			m.set_shader_parameter("unten_hell", 0.45 if c_an else 0.0)
			farbe_setzen(m, "haut_ton", grund, st)
			m.set_shader_parameter("dunkel", 0.85 if c_an else 0.6)
			m.set_shader_parameter("deckung", 0.65 if d_an else (0.7 if c_an else 1.0))
			m.set_shader_parameter("mip_hub", 0.35)
			if c_an:
				# Runde 2/3 (Prüfer: Wimpern schwarz, hart, zu dicht): lichter, durchscheinender, ein Viertel weniger Haare;
				# graue Wimpern ergrauen fast wie die Brauen
				farbe_setzen(m, "farbe", haar.darkened(0.4).lerp(grund, 0.15), st)
				m.set_shader_parameter("dichte", 0.72 if mann else 0.78)
				m.set_shader_parameter("grau", grau * 0.8)
			if d_an:
				# Etappe D, Runde 2 (Prüfer: „steifer schwarzer Stachelkranz, außen abstehend“): weniger, braunere Wimpern
				farbe_setzen(m, "farbe", haar.darkened(0.25).lerp(grund, 0.2), st)
				m.set_shader_parameter("dichte", 0.5 if mann else 0.58)
		_haar_aktualisieren(haar, grau)
	else:
		haar = haar.lerp(Color(0.72, 0.71, 0.69), grau)
		for m in haar_stoffe:
			farbe_setzen(m, "farbe", haar, st)
	# Augen: außen aus Helligkeit und Ton (dunkelbraun -> bernstein/grün/blau), innerer Kranz bernstein bei braunen
	# und grünen Augen, schwach bei blauen
	var ah := float(gene.get("augen_helligkeit", 130.0)) / 255.0
	var at := float(gene.get("augen_ton", 135.0)) / 255.0
	var hell := Color(0.6, 0.42, 0.14).lerp(Color(0.36, 0.46, 0.25), clampf(at / 0.5, 0.0, 1.0))
	if at > 0.5:
		hell = Color(0.36, 0.46, 0.25).lerp(Color(0.38, 0.52, 0.72), (at - 0.5) / 0.5)
	var iris := Color(0.16, 0.08, 0.035).lerp(hell, ah)
	var kranz := Color(0.5, 0.32, 0.1).lerp(Color(0.62, 0.45, 0.16), ah)
	if st >= 6:
		# lineare Palette (S2), als Color übergeben
		var hell6 := IRIS6_BERNSTEIN.lerp(IRIS6_GRUEN, clampf(at / 0.5, 0.0, 1.0))
		if at > 0.5:
			hell6 = IRIS6_GRUEN.lerp(IRIS6_BLAU, (at - 0.5) / 0.5)
		iris = linear(IRIS6_DUNKEL.lerp(hell6, ah))
		kranz = linear((KRANZ6[0] as Color).lerp(KRANZ6[1], ah))
	# Etappe B (S7): Irismuster nach Augenfarbe und Personen-Samen, Drehung je Person (ohne Samen: Grundmuster)
	var iris_ebene := -1.0
	var iris_dreh := 0.0
	if st >= 6 and bilder.has("iris6") and formen.has("alter_reife_m_oben"):
		var wahl: Array = [0, 1, 3, 5] if ah < 0.35 else ([0, 2, 3, 4] if at > 0.6 else [1, 2, 4, 5])
		iris_ebene = 0.0
		if iris_vorgabe >= 0:
			iris_ebene = float(clampi(iris_vorgabe, 0, 5))
		elif aussehen_samen != 0:
			var rng := RandomNumberGenerator.new()
			rng.seed = hash("iris:%d" % aussehen_samen)
			iris_ebene = float(wahl[rng.randi_range(0, wahl.size() - 1)])
			iris_dreh = rng.randf_range(0.0, TAU)
	werte3["iris_ebene"] = iris_ebene
	var a_auge := st >= 6 and weg_a
	if a_auge:
		# Weg A: Iris entsättigt (Diagnose: zu grün und gesättigt), Kranz ebenso; Limbus schmaler, damit die Iris nicht kleiner wirkt
		iris = _entsaettigt(iris, 0.5)
		kranz = _entsaettigt(kranz, 0.35)
	var d_auge := a_auge and weg_d
	for m in lidschatten_stoffe:
		m.shader = LIDSCHATTEN6 if d_auge else LIDSCHATTEN
		m.set_shader_parameter("staerke", 0.9 if a_auge else 0.75)
	for m in hornhaut_stoffe:
		# Runde 2 (Prüfer: „kein Glanzpunkt wie im Foto, leerer Blick“): Glanzpunkt klein und weich, aber deutlich; die
		# Hornhaut zeichnet nach dem Lidschatten, damit er den Glanzpunkt des Schlüssellichts nicht schluckt
		m.set_shader_parameter("rauheit", 0.045 if d_auge else 0.035)
		m.set_shader_parameter("staerke", 0.85 if d_auge else 1.0)
		m.render_priority = 1 if d_auge else 0
		# weiches Fenster-Glanzlicht, heller bei hellem Licht am Ort
		m.set_shader_parameter("fenster", (0.15 + 0.5 * pupille_licht) if d_auge else 0.0)
	for m in traene_stoffe:
		m.set_shader_parameter("traene_d", d_auge)
		m.set_shader_parameter("rauheit", 0.05 if d_auge else 0.08)
		m.set_shader_parameter("staerke", 0.65 if d_auge else 0.7)
	var loch := float(info.get("pupillenoeffnung", 0.31)) + 0.03
	for m in augen_stoffe:
		m.set_shader_parameter("iris_hohl", 0.55 if a_auge else 0.0)
		m.set_shader_parameter("rand_schatten", (1.0 if d_auge else 0.8) if a_auge else 0.0)
		m.set_shader_parameter("limbus", (0.72 if d_auge else 0.6) if a_auge else 0.75)
		m.set_shader_parameter("limbus_breite", (0.22 if d_auge else 0.16) if a_auge else 0.25)
		m.set_shader_parameter("iris_tiefe", 0.09 if d_auge else 0.0)
		m.set_shader_parameter("lederhaut_glanz", 0.25 if d_auge else 0.5)
		m.set_shader_parameter("lederhaut_rauheit", 0.32 if d_auge else 0.18)
		m.set_shader_parameter("limbus_saum", 0.6 if d_auge else 0.0)
		# Pupille nach dem Licht (Etappe D): hell 3 mm, dämmrig bis 5 mm; nie kleiner als die Öffnung im Netz
		m.set_shader_parameter("pupille", loch + (0.2 * (1.0 - pupille_licht) if d_auge else 0.0))
		m.set_shader_parameter("iris_bilder6", bilder.get("iris6"))
		m.set_shader_parameter("iris_ebene", iris_ebene)
		m.set_shader_parameter("iris_drehung", iris_dreh)
		farbe_setzen(m, "iris_farbe", iris, st)
		farbe_setzen(m, "kranz_farbe", kranz, st)
		if st >= 6:
			# Etappe D, Runde 3 (Prüfer: „Augenweiß zu hell“): Lederhaut etwas dunkler
			farbe_setzen(m, "sklera", linear(SKLERA6.darkened(0.12) if d_auge else SKLERA6), st)
		m.set_shader_parameter("kranz", clampf(1.0 - at * 0.9, 0.1, 1.0) * lerpf(0.4, 1.0, ah))
	for m in zahnfleisch_stoffe:
		farbe_setzen(m, "farbe", linear(ZAHNFLEISCH6) if st >= 6 else Color(0.62, 0.27, 0.26), st)

# Etappe C: Hautwerte je Person. Alter der Haut: Außenarbeit (wetter) altert die Haut um bis zu 10 Jahre; Sonne bräunt
# und rötet Stirn, Nase, Jochbein, Ohren und Nacken; Gene „hautroetung“ und „sommersprossen“ (Vorschlag S7).
func _haut_c_setzen(mann: bool, h: float, hh: float, bart: float, reif: float, greis: float, fett: float, faktor: float,
		mimik4: Vector4, grund: Color) -> void:
	var w := clampf(wetter, 0.0, 1.0)
	var a_h := alter + 10.0 * w
	var rel := Vector4(kurve_im_alter(RELIEF_ALTER["stirn"], a_h) * (1.15 - 0.3 * fett),
		kurve_im_alter(RELIEF_ALTER["augen"], a_h), kurve_im_alter(RELIEF_ALTER["mitte"], a_h),
		kurve_im_alter(RELIEF_ALTER["mund"], a_h) * (0.85 + 0.3 * fett))
	rel = rel * faktor * (1.0 if mann else 0.95) * (1.0 + 0.25 * w) + mimik4
	var roet := pow(clampf(float(gene.get("hautroetung", 128.0)) / 128.0, 0.2, 2.0), 0.7)
	# Sommersprossen: steile Kurve (Gen 110 ≈ 3 %, 150 ≈ 11 %, 255 ≈ 60 % der Flecken auf Nase und Wangen)
	var spross := clampf((float(gene.get("sommersprossen", 60.0)) - 60.0) / 195.0, 0.0, 1.0)
	spross = pow(spross, 2.2) * 0.6 * lerpf(1.0, 0.85, reif)
	var ring_a := clampf((a_h - 22.0) / 40.0, 0.0, 1.0)
	var rng := RandomNumberGenerator.new()
	rng.seed = hash("haut_c:%d" % aussehen_samen)
	var versatz := Vector2(rng.randf(), rng.randf()) if aussehen_samen != 0 else Vector2(0.137, 0.731)
	# Bartschatten (rasiert): Albedo im Bartgebiet bei mittlerem Bartwuchs und dunklem Haar ≈ 25 % dunkler, kühl (Plan S9:
	# ΔL* −6 … −12); helles oder graues Haar zeigt sich kaum
	var bart_c := clampf(bart * 1.35, 0.0, 0.95) if mann else clampf(bart * 0.6, 0.0, 0.3)
	var bart_farbe := Vector3(0.6, 0.64, 0.72).lerp(Vector3(0.9, 0.9, 0.9), clampf(hh * 1.2, 0.0, 1.0))
	for m in haut_stoffe:
		m.set_shader_parameter("relief_gewicht", rel)
		m.set_shader_parameter("c_versatz", versatz)
		m.set_shader_parameter("c_hb", 0.2 * roet * (1.12 if mann else 1.0) + 0.05 * greis + 0.08 * w)
		m.set_shader_parameter("c_sonne", 0.06 + 0.04 * reif + 0.2 * w)
		m.set_shader_parameter("c_braeune", 0.15 * w)
		# Etappe D, Runde 3 (Prüfer: „dunkelviolette Linie unter dem Unterlid“): Augenringe schwächer, die Furche ist Form
		m.set_shader_parameter("c_ringe", (0.35 + 0.15 * ring_a) * (1.0 if mann else 1.35) * (0.65 if weg_d else 1.0))
		m.set_shader_parameter("c_bart", bart_c)
		m.set_shader_parameter("c_bart_farbe", bart_farbe)
		m.set_shader_parameter("c_stoppeln", (0.9 * clampf(bart / 0.5, 0.0, 1.0) * (1.0 - 0.6 * hh)) if mann else 0.0)
		m.set_shader_parameter("c_unruhe_eu", (0.08 + 0.04 * reif + 0.04 * greis + 0.04 * w) * (1.0 if mann else 0.9))
		m.set_shader_parameter("c_unruhe_hb", (0.08 + 0.03 * reif + 0.05 * greis) * (1.0 if mann else 0.9))
		m.set_shader_parameter("c_sommersprossen", spross)
		m.set_shader_parameter("c_altersflecken", clampf((a_h - 45.0) / 30.0, 0.0, 1.0) * 0.18)
		m.set_shader_parameter("c_rau", 0.03 * greis + 0.05 * w + (0.02 if mann else 0.0))
		m.set_shader_parameter("c_poren_staerke", (lerpf(0.35, 0.75 if mann else 0.62, reif) + 0.1 * greis + 0.1 * w) * (1.0 if mann else 0.9))
		# feste Abstimmung (jede Zelle setzt sie neu; „haut_werte“ eines Bogens bleibt sonst am Stoff hängen)
		m.set_shader_parameter("relief_staerke", RELIEF_STAERKE)
		m.set_shader_parameter("c_kontrast", 1.15)
		m.set_shader_parameter("c_furche_dunkel", 0.08 + 0.12 * clampf((a_h - 30.0) / 35.0, 0.0, 1.0))
		m.set_shader_parameter("c_hoehle_albedo", 0.35)
		m.set_shader_parameter("c_uneben", 0.5)
		m.set_shader_parameter("c_loch", 0.45)
		m.set_shader_parameter("c_gelb_alle", 0.18 + 0.1 * w)
		m.set_shader_parameter("c_lippen_weich", 0.6)
		m.set_shader_parameter("c_zeige", 0)
		m.set_shader_parameter("c_spross_staerke", 0.6)
		m.set_shader_parameter("c_gelb", 0.35 + 0.25 * greis)
	werte3["haut_c"] = {"relief": rel, "wetter": w, "sommersprossen": spross, "bart": bart_c}

# Halt B2: Farbe zur Helligkeit hin entsättigen (lineare Farbe als Color wie in _farben)
static func _entsaettigt(c: Color, anteil: float) -> Color:
	var l := 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b
	return Color(lerpf(c.r, l, anteil), lerpf(c.g, l, anteil), lerpf(c.b, l, anteil), c.a)

# ---------------------------------------------------------------- Haar und Bart (Phase 4)
func _haar_einrichten() -> void:
	var o := pfad.get_base_dir().path_join("haar4")
	if not phase3 or not FileAccess.file_exists(o.path_join("stuetzen.json")):
		return
	haar_knoten = HAAR4.new()
	haar_knoten.name = "Haar"
	add_child(haar_knoten)
	if not haar_knoten.einrichten(o):
		haar_knoten.queue_free()
		haar_knoten = null
		return
	haar_knoten.tfx_ruhe = haar_ruhe
	haar_knoten.tfx_sparsam = haar_ruhe or haar_sparsam
	haar_knoten.karten_schatten = haar_schatten
	haar_knoten.stand = stand
	haar_knoten.modus = haar_modus if haar_modus != "tressfx" or HAAR4.tressfx_da() else "karten"

func haar_modus_setzen(m: String) -> void:
	haar_modus = m
	if haar_knoten:
		haar_knoten.tfx_ruhe = haar_ruhe
		haar_knoten.tfx_sparsam = haar_ruhe or haar_sparsam
		haar_knoten.karten_schatten = haar_schatten
		haar_knoten.modus_setzen(m if m != "aus" else "karten")
		haar_knoten.visible = m != "aus"
		_farben()

func _haar_aktualisieren(farbe: Color, grau: float) -> void:
	if haar_knoten == null:
		return
	var o: String = haar_knoten.ordner
	var mann := geschlecht >= 0.5
	var hw := HAAR4.aus_genen(o, gene, alter, mann)
	if hw.is_empty():
		return
	for k in haar_vorgabe:
		hw[k] = haar_vorgabe[k]
	if haar_modus == "aus":
		hw["frisur"] = ""
		hw["bart"] = ""
	hw["farbe"] = farbe
	hw["grau"] = grau
	hw["kopfmitte"] = Vector3(0.0, augen_mitte.y + 0.01, augen_mitte.z - 0.08)
	haar_werte = hw
	haar_knoten.karten_schatten = haar_schatten
	haar_knoten.stile_setzen([hw["frisur"], hw["bart"]])
	haar_knoten.aktualisieren(gewichte, hw)
	# Haut unter Haar und Bart
	var ue := HAAR4.uebersicht(o)
	var karten: Dictionary = ue.get("kopfhaut", {})
	# Phase 5: je Bild vier Stile; Bärte in bart*.png, der Rang (Glatze) im Kanal A des Bildes mit „rang“
	var kopfhaut_tex: Texture2D = null
	var bart_tex: Texture2D = null
	var rang_tex: Texture2D = null
	var kanal := Vector4.ZERO
	var bart_kanal := Vector4.ZERO
	for datei in karten:
		var liste: Array = karten[datei]
		var ist_bart := String(datei).begins_with("bart")
		if liste.has("rang"):
			rang_tex = HAAR4.bild(o.path_join(datei))
		var i := liste.find(hw["frisur"])
		if i >= 0 and not ist_bart and hw["frisur"] != "":
			kopfhaut_tex = HAAR4.bild(o.path_join(datei))
			kanal[i] = 1.0
		i = liste.find(hw["bart"])
		if i >= 0 and ist_bart and hw["bart"] != "":
			bart_tex = HAAR4.bild(o.path_join(datei))
			bart_kanal[i] = clampf(float(hw["bart_dichte"]) * (0.5 + 0.5 * float(hw["bart_laenge"])), 0.0, 1.0)
	var unter := farbe.lerp(Color(0.78, 0.77, 0.75), grau)
	for m in haut_stoffe:
		m.set_shader_parameter("kopfhaut_tex", kopfhaut_tex)
		m.set_shader_parameter("kopfhaut_kanal", kanal)
		m.set_shader_parameter("rang_tex", rang_tex)
		m.set_shader_parameter("kahl", float(hw["kahl"]))
		m.set_shader_parameter("duenn_haut", float(hw["duenn"]))
		m.set_shader_parameter("bart_tex", bart_tex)
		m.set_shader_parameter("bart_kanal", bart_kanal)
		farbe_setzen(m, "haar_unter", unter, stand)

# ---------------------------------------------------------------- Stoffe
# Teilart nach dem Stoffnamen der .glb
static func teil_art(stoffname: String) -> String:
	match stoffname:
		"Haut": return "haut"
		"Mund": return "mund"
		"Kleidung": return "kleid"
		"Kragen": return "kragen"
		"Augen_Lederhaut": return "lederhaut"
		"Augen_Iris": return "iris"
		"Traene": return "traene"
		"Hornhaut": return "hornhaut"
		"Lidschatten": return "lidschatten"
		"Zaehne": return "zaehne"
		"Zahnfleisch": return "zahnfleisch"
		"Brauen", "Wimpern": return "haar"
	return "sonst"

# Teile zum Prüfen unsichtbar machen (z. B. ["haar", "kragen"]); leere Liste zeigt alles wieder
var _versteckt := {}
func teile_ausblenden(arten: Array) -> void:
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.mesh.surface_get_material(s)
			var art := teil_art(String(alt.resource_name) if alt else "")
			var schl := "%d_%d" % [mi.get_instance_id(), s]
			if art in arten and not _versteckt.has(schl):
				_versteckt[schl] = mi.get_surface_override_material(s)
				mi.set_surface_override_material(s, _leer())
			elif not art in arten and _versteckt.has(schl):
				mi.set_surface_override_material(s, _versteckt[schl])
				_versteckt.erase(schl)

# Stoffparameter aus einem Bogen: {"haut": {"masken": false}, "augen": {…}}; vor jeder Zelle gelten wieder die
# Grundwerte (gemerkt beim ersten Setzen)
var _stoff_grund := {}
func stoff_parameter(werte: Dictionary) -> void:
	var gruppen := {"haut": haut_stoffe, "haar": haar_stoffe, "augen": augen_stoffe}
	for schl in _stoff_grund:
		var teile: PackedStringArray = schl.split("|")
		for m in gruppen[teile[0]]:
			m.set_shader_parameter(teile[1], _stoff_grund[schl])
	for g in werte:
		if not gruppen.has(g):
			print("WARNUNG Stoffgruppe ", g, " unbekannt")
			continue
		for p in werte[g]:
			var schl := "%s|%s" % [g, p]
			for m in gruppen[g]:
				if not _stoff_grund.has(schl):
					_stoff_grund[schl] = m.get_shader_parameter(p)
				var v = werte[g][p]
				if v is Array and v.size() == 3:            # JSON-Liste -> vec3 (Faktoren wie streu_anteil)
					v = Vector3(v[0], v[1], v[2])
				m.set_shader_parameter(p, v)

static func _leer() -> StandardMaterial3D:
	var leer := StandardMaterial3D.new()
	leer.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	leer.albedo_color = Color(0, 0, 0, 0)
	return leer

static func poren() -> NoiseTexture2D:
	# Poren und feine Fältchen als nahtlose Normalenkarte, im Spiel erzeugt (keine Datei, keine Fremddaten)
	if _poren == null:
		var rauschen := FastNoiseLite.new()
		rauschen.noise_type = FastNoiseLite.TYPE_CELLULAR
		rauschen.frequency = 0.09
		rauschen.cellular_return_type = FastNoiseLite.RETURN_DISTANCE
		rauschen.fractal_type = FastNoiseLite.FRACTAL_FBM
		rauschen.fractal_octaves = 2
		rauschen.fractal_gain = 0.35
		_poren = NoiseTexture2D.new()
		_poren.width = 512
		_poren.height = 512
		_poren.seamless = true
		_poren.as_normal_map = true
		_poren.bump_strength = 5.0
		_poren.noise = rauschen
	return _poren

func _stoffe_setzen() -> void:
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var alt := mi.mesh.surface_get_material(s)
			var name := String(alt.resource_name) if alt else ""
			var art := teil_art(name)
			var neu2 := _stoff(alt as BaseMaterial3D, art, mi, s)
			var neu: Material = neu2
			if phase3:
				var neu3 := _stoff3(art, mi, s)
				if neu3:
					neu = neu3
				_stoffe3["%d_%d" % [mi.get_instance_id(), s]] = [neu, neu2]
			if neu:
				mi.set_surface_override_material(s, neu)

# Phase-3-Kopf zum Vergleich mit den Stoffen der Phase 2 (ohne Bilder) zeigen; Brauen und Wimpern bleiben
func vorher_setzen(an: bool) -> void:
	vorher = an
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			var paar: Array = _stoffe3.get("%d_%d" % [mi.get_instance_id(), s], [])
			if paar.is_empty():
				continue
			var art := teil_art(String(mi.mesh.surface_get_material(s).resource_name))
			var m: Material = paar[0]
			if an and art in ["hornhaut", "lidschatten"]:
				m = _leer()
			elif an and art != "haar" and paar[1] != null:
				m = paar[1]
			mi.set_surface_override_material(s, m)

# Haut mit oder ohne Bildschirm-SSS (Ultra); Parameter bleiben
func ultra_setzen(an: bool) -> void:
	ultra = an
	_shader_waehlen()

func _stoff3(art: String, mi: MeshInstance3D, s: int) -> Material:
	match art:
		"haut":
			var m := ShaderMaterial.new()
			m.shader = _haut_shader()
			m.set_shader_parameter("farbe_tex", bilder["haut_farbe"])
			if bilder.has("haut_zonen"):
				m.set_shader_parameter("zonen_tex", bilder["haut_zonen"])
			if bilder.has("haut_c_relief"):
				for b in ["zonen", "glanz", "relief", "poren", "flecken"]:
					if bilder.has("haut_c_" + b):
						m.set_shader_parameter("c_" + b, bilder["haut_c_" + b])
				var bp: Array = c_info.get("relief_bp", [1.0, 1.0, 1.0, 1.0])
				m.set_shader_parameter("relief_bp", Vector4(bp[0], bp[1], bp[2], bp[3]))
				m.set_shader_parameter("c_bildpunkt_mm", float(c_info.get("mm_je_bildpunkt_median", 0.24)))
				var muv := meter_je_uv(mi.mesh, s)
				# Kachel 25 mm, hier auf 40 mm gestreckt: echte Poren lägen im Porträt unter einem Bildpunkt (gemalt-realistisch:
				# etwas gröber, damit die Haut im Licht Struktur zeigt)
				m.set_shader_parameter("c_poren_dichte", muv / 0.04)
				m.set_shader_parameter("c_flecken_dichte", muv / 0.06)
			m.set_shader_parameter("normal_tex", bilder["haut_normal"])
			m.set_shader_parameter("masken_tex", bilder["haut_masken"])
			m.set_shader_parameter("falten_tex", bilder["haut_falten"])
			m.set_shader_parameter("poren", poren())
			m.set_shader_parameter("poren_dichte", meter_je_uv(mi.mesh, s) / 0.045)
			haut_stoffe.append(m)
			return m
		"lederhaut", "iris":
			var m := ShaderMaterial.new()
			m.shader = AUGEN6 if stand >= 6 else AUGEN3
			m.set_shader_parameter("iris_teil", art == "iris")
			# gemalte Pupille knapp größer als die Öffnung im Netz (Beschreibung der .glb)
			m.set_shader_parameter("pupille", float(info.get("pupillenoeffnung", 0.31)) + 0.03)
			m.set_shader_parameter("iris_bild", bilder["iris_muster"])
			m.set_shader_parameter("lederhaut_bild", bilder["lederhaut"])
			augen_stoffe.append(m)
			return m
		"hornhaut", "traene":
			var m := ShaderMaterial.new()
			m.shader = HORNHAUT
			if art == "traene":
				m.set_shader_parameter("rauheit", 0.08)
				m.set_shader_parameter("staerke", 0.7)
				traene_stoffe.append(m)
			else:
				hornhaut_stoffe.append(m)
			return m
		"lidschatten":
			var m := ShaderMaterial.new()
			m.shader = LIDSCHATTEN
			lidschatten_stoffe.append(m)
			return m
		"haar":
			var m := ShaderMaterial.new()
			m.shader = HAAR3
			var wimper := String(mi.mesh.surface_get_material(s).resource_name) == "Wimpern"
			m.set_shader_parameter("bild", bilder["wimpern" if wimper else "brauen"])
			if wimper:
				wimpern_stoffe.append(m)
			else:
				haar_stoffe.append(m)
			return m
	return null

func _stoff(std: BaseMaterial3D, art: String, mi: MeshInstance3D, s: int) -> Material:
	match art:
		"haut":
			var m := ShaderMaterial.new()
			m.shader = HAUT
			m.set_shader_parameter("hat_farbe", false)
			m.set_shader_parameter("poren", poren())
			m.set_shader_parameter("poren_dichte", meter_je_uv(mi.mesh, s) / 0.045)
			if phase3:
				haut2_stoffe.append(m)
			else:
				haut_stoffe.append(m)
			return m
		"mund":
			var m := ShaderMaterial.new()
			m.shader = MUND
			return m
		"zaehne", "zahnfleisch":
			var m := ShaderMaterial.new()
			m.shader = ZAEHNE
			if art == "zahnfleisch":
				farbe_setzen(m, "farbe", Color(0.62, 0.27, 0.26), 5)   # Farbe setzt _farben() je Stand
				zahnfleisch_stoffe.append(m)
				m.set_shader_parameter("rauheit", 0.35)
				m.set_shader_parameter("glanz", 0.4)
				m.set_shader_parameter("anfang", 0.15)
			return m
		"lederhaut", "iris":
			var m := ShaderMaterial.new()
			m.shader = AUGEN
			m.set_shader_parameter("hat_farbe", false)
			m.set_shader_parameter("iris_teil", art == "iris")
			m.set_shader_parameter("lederhaut_teil", art == "lederhaut")
			augen_stoffe.append(m)
			return m
		"traene":
			var m := StandardMaterial3D.new()
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			m.albedo_color = Color(0.9, 0.85, 0.85, 0.12)
			m.roughness = 0.05
			m.metallic_specular = 0.8
			return m
		"haar":
			var m := ShaderMaterial.new()
			m.shader = HAAR
			if std and std.albedo_texture:
				m.set_shader_parameter("bild", std.albedo_texture)
			elif phase3:
				return null           # Phase-3-Karten haben kein MakeHuman-Bild
			haar_stoffe.append(m)
			return m
		"kleid", "kragen":
			# Platzhalter: schlichtes Leinen (später zeigen Hemd und Kragen Stand und Beruf)
			var m := StandardMaterial3D.new()
			m.albedo_color = Color(0.56, 0.5, 0.4) if art == "kleid" else Color(0.5, 0.44, 0.35)
			m.roughness = 0.92
			return m
		"aus", "lidschatten", "hornhaut":
			return _leer()
	return null

static func meter_je_uv(mesh: Mesh, s: int) -> float:
	var a := mesh.surface_get_arrays(s)
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var uv: PackedVector2Array = a[Mesh.ARRAY_TEX_UV]
	var ind: PackedInt32Array = a[Mesh.ARRAY_INDEX]
	if uv.is_empty() or ind.is_empty():
		return 0.25
	var welt := 0.0
	var flach := 0.0
	for i in range(0, ind.size(), 3):
		var p0 := v[ind[i]]
		var u0 := uv[ind[i]]
		welt += (v[ind[i + 1]] - p0).cross(v[ind[i + 2]] - p0).length()
		var d1 := uv[ind[i + 1]] - u0
		var d2 := uv[ind[i + 2]] - u0
		flach += absf(d1.x * d2.y - d1.y * d2.x)
	return sqrt(welt / maxf(flach, 1e-9))

func dreiecke() -> int:
	var n := 0
	for mi in netze:
		for s in mi.mesh.get_surface_count():
			n += mi.mesh.surface_get_array_index_len(s) / 3
	return n
