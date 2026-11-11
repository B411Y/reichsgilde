// Oberflächendetails für die Modelle (Grafikoption, Einbau: game/scripts/Oberflaechendetails.cs, Beschreibung in docs/ARCHITEKTUR.md).
// Die Grundfarbe des Modells bleibt bestimmend; je Materialart kommt eine dezente Struktur dazu: Steine mit leichten Farbunterschieden
// und weichen Fugen, Maserung mit Ästen, Ziegel mit leichter Abnutzung, Stroh mit Halmen, Laub mit Blättern, Boden mit Körnung –
// und eine leichte Unebenheit im Licht (Normale). Licht und Schatten sind Godots eigene (kein light()).
// Die Strukturen werden dreiseitig in Objektkoordinaten projiziert: Die Modelle brauchen keine UV-Abwicklung, und an laufenden
// Figuren, fahrenden Karren und gedrehten Häusern bleibt das Muster auf dem Modell, statt durch die Welt zu „schwimmen“.
shader_type spatial;
render_mode cull_disabled;   // Die Modelle aus Blender sind doppelseitig (wie figur.gdshader)

// Wetter (PR #195): Laub im Wind, Schnee auf Dächern, Nässe – nur die drei markierten Stellen unten
#include "res://assets/shader/wetter.gdshaderinc"

// Grundfarbe: aus dem alten Material (farbe), mal der Eckenfarbe der GLB (Material „Farben“ und „Figur“).
uniform vec4 farbe : source_color = vec4(1.0);
uniform bool eckenfarbe = false;
// Materialart (materialarten.json): 0 keine, 1 Holz, 2 Bretter, 3 Mauer, 4 Fels, 5 Ziegel, 6 Stroh, 7 Schiefer, 8 Laub, 9 Boden.
// art_aus_uv: je Fläche aus UV.y (Material „Farben“ und „Figur“), sonst der feste Wert art.
uniform int art = 0;
uniform bool art_aus_uv = false;
// Figuren: Zonen aus UV.x (1 Gewand, 2 Beinkleid, 3 Haut, 4 Haar, 5 Kopfbedeckung) je Figur einfärben. Gewöhnliche Uniforms, kein
// „instance uniform“: Das Spiel gibt jeder Tracht ein eigenes Material. Jede Instanz mit Instanz-Uniform belegt 16 Plätze im Puffer
// der Grafikkarte (nur 4096 in Forward+), und dieser Shader gilt für alle Knoten der Stadt, nicht nur für Figuren.
uniform bool zonen = false;
uniform vec4 gewand : source_color = vec4(0.30, 0.40, 0.55, 1.0);
uniform vec4 beinkleid : source_color = vec4(0.30, 0.26, 0.22, 1.0);
uniform vec4 haut : source_color = vec4(0.86, 0.68, 0.55, 1.0);
uniform vec4 haar : source_color = vec4(0.32, 0.22, 0.14, 1.0);
uniform vec4 kopfbedeckung : source_color = vec4(0.78, 0.74, 0.64, 1.0);
uniform bool bart = false; // Zone 6 (Bart): Haarfarbe statt Haut
// Aus dem alten Material übernommen
uniform float rauheit = 1.0;
uniform float metall = 0.0;
uniform float glanz = 0.5;

// Material-Bibliothek (Modell-Texturen, tools/oberflaeche/materialien_blender.py, game/daten/materialbibliothek.json): gebackene
// 1024-px-Materialien (Bruchstein, Quader, Putz, Holz, Ziegel, Schiefer, Stroh …). bib ≥ 0 ist die Schicht, sonst gilt die Struktur
// darunter. bib_stufe (Einstellung „Boden“): 0 Niedrig – die Struktur wie bisher, 1 Mittel – Farbe der Bibliothek, 2 Hoch – dazu
// Normalen und Rauheit. Die Textur wird durch ihre mittlere Farbe (bib_mittel) geteilt und mit der Modellfarbe gefärbt: Im Mittel
// bleibt jedes Material so hell und bunt wie die Modellfarbe, Häuser bleiben an ihren Farben zu erkennen.
// Modelle mit „Farben“ (art_aus_uv) tragen die Schicht je Fläche im Netz (UV.y = Art + 16 · (Schicht + 1), tools/modelle/bausteine.py,
// arten_setzen); dafür stehen die Werte aller Schichten in den Feldern unten (Oberflaechendetails.cs setzt sie in jedes Material).
uniform int bib = -1;
uniform int bib_stufe = 2;
const int BIB_MAX = 32;
uniform float bib_kacheln[BIB_MAX];              // Meter je Kachel
uniform vec3 bib_mittel[BIB_MAX];                // mittlere Farbe der Schicht (linear)
uniform int bib_projektion[BIB_MAX];             // 0 wand, 1 dach (entlang der Dachneigung, oben = firstwärts), 2 reihen (siehe fragment)
uniform float bib_reihe[BIB_MAX];                // Reihenhöhe (Meter) der Deckungen mit Projektion „reihen“ für Dächer ohne Modellreihen
uniform sampler2DArray bib_farbe : source_color, repeat_enable, filter_linear_mipmap_anisotropic;   // RGB Farbe, A Höhe
uniform sampler2DArray bib_nr : repeat_enable, filter_linear_mipmap_anisotropic;                    // RG Normale, B Rauheit
const float BIB_RELIEF = 1.0;                    // Stärke der Normalen
const float BIB_REIHE_HELL = 0.04;               // Helligkeitsunterschied der Dachreihen
const float BIB_LAUF = 0.22;                     // Laufspuren auf Dächern: so viel dunkler
const vec3 BIB_MOOS = vec3(0.30, 0.34, 0.12);    // Moos auf Dächern: Farbton (die Helligkeit kommt vom Dach)
const float BIB_MOOS_ANTEIL = 0.45;
const float BIB_REIHE_KANTE = 0.45;              // Dächer ohne Modellreihen: so viel dunkler an der Unterkante jeder Reihe
const float BIB_REIHE_NEIGUNG = 0.25;            // … und so weit kippt jede Reihe (Normale), wie die Sägezähne der Modelle

// Relief-Texturen (CC0, Poly Haven und ambientCG; tools/oberflaeche/relief_texturen.py, Block "relief" in materialbibliothek.json): echte Aufnahmen mit
// Normalen, Rauheit, Verdeckung (AO) und Hoehe, je Schicht der Bibliothek, wo es sie gibt. rel_schicht[b] ist die Schicht im Relief-Array plus 1 (0 = keine,
// dann gilt die bisherige Textur von oben; das Spiel setzt die Felder nur, wenn die beiden Dateien da sind). Kachel, mittlere Farbe, Projektion und
// Reihenhoehe stehen dann schon in den bib_-Feldern. Ab bib_stufe 1 (Mittel) mit Normalen, Rauheit und AO, ab 2 (Hoch, Ultra) dazu Parallax (bib_pom = Schritte).
uniform sampler2DArray rel_farbe : source_color, repeat_enable, filter_linear_mipmap_anisotropic;    // RGB Farbe, A Hoehe (1 oben)
uniform sampler2DArray rel_nr : repeat_enable, filter_linear_mipmap_anisotropic;                     // RG Normale, B Rauheit, A Verdeckung
uniform int rel_schicht[BIB_MAX];
uniform float rel_tiefe[BIB_MAX];                // Tiefe des Reliefs in Metern (Parallax)
uniform float rel_staerke[BIB_MAX];              // Staerke der Normalen dieser Schicht
uniform float rel_nass[BIB_MAX];                 // wie viel der Nässe-Abdunkelung gilt (1 = ganz; Pflaster weniger, sonst wird es bei Regen fast schwarz)
uniform float bib_pixel = 0.0;                  // 1 im Pixel-Look: Relief nur als ruhige Farbe (keine Normale, keine Fugen, kein Parallax), sonst Punktreihen und Schachbrett
uniform int bib_pom = 0;                         // 0 aus, sonst Schritte der Parallax-Okklusion (Hoch 4, Ultra 8)
const float REL_AO_UMGEBUNG = 0.25;              // so viel der Verdeckung dunkelt das Umgebungslicht ab (AO_LIGHT_AFFECT bleibt 0: das Direktlicht dunkelt schon die Farbe ab)
const float REL_AO_FARBE = 0.55;                 // so viel der Verdeckung dunkelt auch das Licht ab (Fugen bleiben lesbar, Godots AO wirkt nur auf Umgebungslicht)
const float REL_POM_NAH = 0.008;                 // Parallax: voll bis so viele Meter je Bildpunkt, ...
const float REL_POM_FERN = 0.02;                 // ... aus (Ferne flimmert und waere unsichtbar)

// Pixel-Fassung (tools/oberflaeche/pixel_fassung.py, pixel_farbe.webp): je Schicht eine eigene grobe, farbstufige Textur mit eingebackenen Schatten, nur fuer den Pixel-Look
// (bib_pixel = 1). pix_kacheln[b] = Meter je Kachel, 0 = keine Fassung fuer diese Schicht (dann gilt die normale Schicht wie vorher). Keine Normale, keine Rauheit, kein Parallax.
uniform sampler2DArray pix_farbe : source_color, repeat_enable, filter_linear_mipmap;
uniform float pix_kacheln[BIB_MAX];
uniform float pix_aktiv = 0.0;                   // 1, wenn das Spiel pixel_farbe.webp geladen hat

// Kanten (Oberflaechendetails.cs legt je Polygon-Dreieck den Abstand zu den echten Kanten und die Normale der Nachbarflaeche in CUSTOM0 bis CUSTOM2): An Kanten zwischen zwei
// Flaechen (Dach/Giebel, Wandecken, Firste) setzt sich die Struktur ueber die Kante fort (in einem schmalen Streifen weich mit der Projektion der Nachbarflaeche gemischt, an
// der Kante je zur Haelfte: beide Seiten zeigen dort dasselbe Bild), die Normalen-Struktur und der Parallax laufen zur Kante hin aus, dazu ein leichter Kantenschatten.
uniform float kanten_staerke = 1.0;              // 0 aus
const float KANTE_BREITE = 0.07;                 // Streifen der Ueberblendung in Metern (mindestens 2 Bildpunkte)
const float KANTE_SCHATTEN = 0.28;               // so viel dunkler direkt an der Kante
const float KANTE_SCHATTEN_BREITE = 0.035;       // Breite des Kantenschattens in Metern (mindestens 1 Bildpunkt)

const float REL_FEIN_NAH = 0.08;                 // Feinheiten (Fugen, Ziegelkanten): voll bis 8 cm je Bildpunkt (Zoom 25/50/100 auf 1080p: 2,3/4,6/9,3 cm), ...
const float REL_FEIN_FERN = 0.20;                // ... darueber gröbere Mip-Stufe und schwächere Normale (Moiré: Punktreihen, Schachbrett, v. a. im Pixel-Look)

// Elf Schichten (tools/oberflaeche/texturen.py): 0–8 die Materialarten 1–9, 9 Pflaster (Mauer von oben), 10 Flecken.
// R Helligkeit, G Farbton (+ warm, − kühl), B/A Neigung der Fläche in U/V – alle um 128 als „nichts“.
uniform sampler2DArray strukturen : repeat_enable, filter_linear_mipmap;

// Je Materialart (Holz, Bretter, Mauer, Fels, Ziegel, Stroh, Schiefer, Laub, Boden):
const float GROESSE[9] = float[9](1.6, 2.0, 2.0, 4.0, 1.2, 1.5, 2.0, 1.0, 3.0);         // Meter je Kachel
const float HELL[9] = float[9](1.4, 1.2, 1.0, 1.0, 1.2, 1.0, 1.4, 1.0, 1.4);             // Anteil der Helligkeitsunterschiede
const float TON[9] = float[9](0.8, 0.8, 1.0, 1.0, 1.0, 0.8, 0.8, 1.0, 1.0);              // Anteil der Farbtonunterschiede
const float RELIEF[9] = float[9](0.6, 0.6, 0.9, 0.8, 0.9, 0.7, 0.9, 0.6, 1.0);           // Stärke der Unebenheit im Licht
const vec3 WARM = vec3(0.07, 0.0, -0.08);   // Farbton +1: etwas röter, weniger blau (−1 umgekehrt)
const float SCHAERFE = 12.0;                // Übergang zwischen den drei Projektionen: groß = schmal (Dachflächen mischen kaum)
const float FLECKEN_GROESSE = 14.0;         // Meter je Kachel der Flecken (in Weltkoordinaten: über Kachelgrenzen hinweg)
const float FLECKEN_HELL = 0.10;
const float FLECKEN_TON = 0.6;

varying vec3 ort;
varying vec3 ort_n;
varying vec3 welt;
varying flat int v_art;
varying flat int v_bib;
varying flat int v_zone;
varying flat int v_reihen;
varying vec3 eckfarbe;
varying vec3 kante_d;                  // Abstand (Objektkoordinaten) zu den drei Kanten des Dreiecks, 1e4 = keine echte Kante
varying flat vec3 kante_n0;            // Normale der Nachbarflaeche je Kante, (0,0,0) = keine
varying flat vec3 kante_n1;
varying flat vec3 kante_n2;

void vertex() {
	// Eckenfarben stehen als sRGB-Werte in der GLB: Der Compatibility-Renderer rechnet in sRGB, Forward+ linear (wie figur.gdshader)
	eckfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		eckfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	kante_d = CUSTOM0.xyz;
	kante_n0 = CUSTOM1.xyz;
	kante_n1 = vec3(CUSTOM1.w, CUSTOM2.xy);
	kante_n2 = vec3(CUSTOM2.zw, CUSTOM0.w);
	ort = VERTEX;
	ort_n = NORMAL;
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// UV.y bei art_aus_uv: Art + 16 · (Schicht der Bibliothek + 1); ältere Modelle haben nur die Art (dann keine Schicht)
	int code = art_aus_uv ? int(round(UV.y)) : art;
	v_art = art_aus_uv ? code % 16 : art;
	v_bib = art_aus_uv ? code / 16 - 1 : -1;
	v_zone = int(round(UV.x));
	v_reihen = zonen ? 0 : int(round(UV.x));   // 1: Dachfläche ohne Modellreihen (bausteine.py, Netz.objekt) – der Shader zeichnet sie
	if (v_art == 8 && !zonen) VERTEX += wetter_laub(VERTEX, welt, MODEL_MATRIX, TIME); // Wetter: Laub im Wind
}

// Ein Wert der Schicht: (Wert − 128) / 127, also −1 … 1 mit 0 bei 128. Mit den Ableitungen von außen (textureGrad), denn gelesen
// wird nur in Zweigen, und dort sind die Ableitungen der Grafikkarte nicht verlässlich.
vec4 lesen(vec2 uv, vec2 dx, vec2 dy, float schicht) {
	return textureGrad(strukturen, vec3(uv, schicht), dx, dy) * (255.0 / 127.0) - (128.0 / 127.0);
}

// Parallax-Okklusion fuer eine Relief-Schicht: Der Blick dringt in Stufen in die Hoehe (A von rel_farbe, 1 = oben) ein, bis er unter der Oberflaeche
// liegt, dazwischen linear angenaehert. sicht = Blickrichtung zur Kamera in der Flaeche (x entlang uv.x, y entlang uv.y, z = Normale), gx/gy Ableitungen.
// Gibt den Versatz der Texturkoordinate zurueck (die Kamera ist orthogonal: eine Richtung gilt fuer die ganze Flaeche).
vec2 rel_versatz(vec2 uv, vec2 gx, vec2 gy, vec3 sicht, float schicht, float tiefe_uv, int schritte) {
	if (sicht.z < 0.15) return vec2(0.0);
	vec2 ganz = sicht.xy / sicht.z * tiefe_uv;            // Versatz bei voller Tiefe
	float lage = 1.0 / float(schritte);
	vec2 schritt = ganz * lage;
	vec2 off = vec2(0.0), off_alt = vec2(0.0);
	float tiefe = 0.0, tiefe_alt = 0.0;
	float ist = 1.0 - textureGrad(rel_farbe, vec3(uv, schicht), gx, gy).a, ist_alt = ist;
	for (int i = 0; i < 16; i++) {
		if (i >= schritte || tiefe >= ist) break;
		off_alt = off; tiefe_alt = tiefe; ist_alt = ist;
		off += schritt; tiefe += lage;
		ist = 1.0 - textureGrad(rel_farbe, vec3(uv - off, schicht), gx, gy).a;
	}
	float danach = ist - tiefe, davor = ist_alt - tiefe_alt;
	float w = clamp(danach / (danach - davor - 1e-5), 0.0, 1.0);
	return -mix(off, off_alt, w);
}

void fragment() {
	vec3 grund = farbe.rgb;
	if (eckenfarbe) grund *= eckfarbe;
	if (zonen) {
		if (v_zone == 1) grund = gewand.rgb;
		else if (v_zone == 2) grund = beinkleid.rgb;
		else if (v_zone == 3) grund = haut.rgb;
		else if (v_zone == 4) grund = haar.rgb;
		else if (v_zone == 5) grund = kopfbedeckung.rgb;
		else if (v_zone == 6) grund = bart ? mix(haut.rgb, haar.rgb, 0.7) : haut.rgb;
	}
	float rau = rauheit;   // Rauheit nach Bibliothek bzw. Modell; das Wetter (Nässe) ändert sie danach
	float tiefe = 0.5;     // Tiefe der Textur der Bibliothek (0 oben … 1 Fuge, Rinne); das Wetter hält dort das Wasser länger
	float ao_aus = 1.0;    // Verdeckung der Relief-Textur (AO)
	int b = (art_aus_uv && v_bib >= 0) ? v_bib : bib;
	if (b >= 0 && b < BIB_MAX && bib_stufe > 0) {
		int rs = rel_schicht[b] - 1;       // Schicht im Relief-Array, −1: die bisherige Textur
		bool rel = rs >= 0;
		// Pixel-Look mit Pixel-Fassung dieser Schicht: eigene grobe Textur, eigene Kachel, ohne Normale und Parallax
		bool pixfass = bib_pixel > 0.5 && pix_aktiv > 0.5 && pix_kacheln[b] > 0.0;
		bool weich = rel || pixfass;       // Relief-Stoffe und Pixel-Fassung: Dachreihen weicher gezeichnet
		bool normalen = !pixfass && (bib_stufe >= 2 || (rel && bib_stufe >= 1));
		float s = 1.0 / (pixfass ? pix_kacheln[b] : bib_kacheln[b]);
		bool bib_dach = bib_projektion[b] >= 1;
		bool bib_reihen = bib_projektion[b] == 2;
		vec3 n = normalize(ort_n);
		// Eine Projektion je Fläche, an ihrer Normalen ausgerichtet (die Modelle sind kantig schattiert, die Normale ist je Fläche fest):
		// u waagerecht in der Fläche, v die Fläche hinauf – an Wänden senkrecht (Lagen laufen um die Ecken weiter), auf Dächern
		// firstwärts (Ziegelspalten laufen die Dachfläche hinauf). Flächen, die nach oben schauen, und flache Dächer liegen in x/z.
		vec3 u = cross(vec3(0.0, 1.0, 0.0), n);
		bool liegend = bib_dach ? length(u) < 0.05 : abs(n.y) > 0.7;
		u = liegend ? vec3(1.0, 0.0, 0.0) : normalize(u);
		vec3 v = liegend ? vec3(0.0, 0.0, -sign(n.y + 1e-4)) : cross(n, u);
		vec2 uv = vec2(dot(ort, u), -dot(ort, v)) * s;
		vec2 uv_dx = dFdx(uv), uv_dy = dFdy(uv);
		float reihe = 0.0;
		float kante = 1.0;
		float kippen = 0.0;
		if (bib_reihen && !liegend) {
			// Die Reihen hat das Modell: jede Ziegel- oder Schieferreihe eine eigene, leicht gekippte Fläche (Sägezahn,
			// tools/modelle/bausteine.py, zaehne). Ihr Abstand vom Ursprung, dot(ort, n), ist auf der ganzen Fläche gleich und springt
			// von Reihe zu Reihe um einige Zentimeter. Daraus je Reihe ein Versatz der Spalten (Fugen versetzt wie beim Decken, kein
			// Karomuster) und ein Helligkeitsunterschied. Stetig in d statt eines Würfels: Rechenrauschen verschiebt nichts sichtbar.
			float d = dot(ort, n);
			if (v_reihen == 1) {
				// Dachfläche ohne Modellreihen (Turmhelm, Pultdach, Halle): die Reihen hier zeichnen – je Reihe ein Sprung in d wie
				// beim Sägezahn, an der Unterkante dunkler (Stoß und Schatten der Reihe darüber) und die Reihe leicht gekippt.
				float pv = dot(ort, v) / bib_reihe[b];
				float r = floor(pv);
				float fr = pv - r;
				d = r * 0.0537;
				float breite = fwidth(pv);            // weich, wo eine Reihe nur wenige Bildpunkte hoch ist (keine flimmernden Linien)
				kante = 1.0 - BIB_REIHE_KANTE * (weich ? 1.0 - 0.6 * bib_pixel : 1.0) * (1.0 - smoothstep(0.0, 0.12 + breite, fr)) * (1.0 - smoothstep(0.25, 0.6, breite));
				kippen = BIB_REIHE_NEIGUNG * (weich ? 1.0 - 0.6 * bib_pixel : 1.0) * (1.0 - smoothstep(0.25, 0.6, breite));
			}
			uv.x += d * 7.31 + 0.37 * sin(d * 113.0) + 0.23 * sin(d * 271.0);
			reihe = sin(d * 157.0 + 1.3 * sin(d * 59.0));
		}
		// Kanten: Abstand zur naechsten echten Kante (Meter) und die Projektion der Nachbarflaeche dort (gleiche Regeln wie oben, mit deren Normale). Ohne Kantendaten
		// im Netz (Nullen) bleibt alles wie bisher.
		float kante_nah = 0.0;             // 1 an der Kante ... 0 ausserhalb des Streifens
		float kante_dunkel = 0.0;          // dito fuer den Kantenschatten
		vec3 u2 = u, v2 = v;
		vec2 uv2 = uv;
		float mass = length(MODEL_MATRIX[0].xyz);
		float wpp = max(length(dFdx(welt)), length(dFdy(welt)));   // Meter je Bildpunkt (ausserhalb des Zweigs: Ableitungen)
		if (kanten_staerke > 0.0) {
			float kd = min(kante_d.x, min(kante_d.y, kante_d.z));
			vec3 kn = (kante_d.x <= kante_d.y && kante_d.x <= kante_d.z) ? kante_n0 : (kante_d.y <= kante_d.z ? kante_n1 : kante_n2);
			if (dot(kn, kn) > 0.5 && kd < 1000.0) {
				float km = kd * mass;
				kante_nah = (1.0 - smoothstep(0.0, max(KANTE_BREITE, 2.0 * wpp), km)) * kanten_staerke;
				kante_dunkel = (1.0 - smoothstep(0.0, max(KANTE_SCHATTEN_BREITE, wpp), km)) * kanten_staerke;
				vec3 n2 = normalize(kn);
				u2 = cross(vec3(0.0, 1.0, 0.0), n2);
				bool liegend2 = bib_dach ? length(u2) < 0.05 : abs(n2.y) > 0.7;
				u2 = liegend2 ? vec3(1.0, 0.0, 0.0) : normalize(u2);
				v2 = liegend2 ? vec3(0.0, 0.0, -sign(n2.y + 1e-4)) : cross(n2, u2);
				uv2 = vec2(dot(ort, u2), -dot(ort, v2)) * s;
			}
		}
		vec4 fh, nr;
		vec4 nr_nachbar = vec4(0.5, 0.5, 0.0, 1.0);   // Normale usw. der Nachbarprojektion, Gewicht nachbar_wert (0 = keine)
		float nachbar_wert = 0.0;
		float staerke = 1.0;
		float grob = 0.0;   // 0 nah genug fuer Fugen und Normale, 1 zu fern oder Pixel-Look
		if (pixfass) {
			fh = vec4(textureGrad(pix_farbe, vec3(uv, float(b)), uv_dx, uv_dy).rgb, 1.0);
			nr = vec4(0.5, 0.5, 0.0, 1.0);
			staerke = 0.0;
			float rl = dot(fh.rgb, vec3(0.3333)) / max(dot(bib_mittel[b], vec3(0.3333)), 1e-3);
			tiefe = clamp(1.1 - 0.8 * rl, 0.0, 1.0);   // dunkle Stellen (Fugen) halten das Wasser laenger
			if (kante_nah > 0.001) fh.rgb = mix(fh.rgb, textureGrad(pix_farbe, vec3(uv2, float(b)), uv_dx, uv_dy).rgb, 0.5 * kante_nah);
		} else if (rel) {
			float mpp = max(length(uv_dx), length(uv_dy)) / s * length(MODEL_MATRIX[0].xyz);   // Meter je Bildpunkt
			grob = smoothstep(REL_FEIN_NAH, REL_FEIN_FERN, mpp);
			float pix = bib_pixel;   // Pixel-Look: gröbere Mip-Stufe immer, Normale und Fugen gedämpft, nicht weg (sonst glatt), keine harten Reihenlinien   // 0 nah, 1 fern (im Pixel-Look wegen der grossen Bildpunkte schon früh)
			uv_dx *= 1.0 + 3.0 * max(grob, pix);   // gröbere Mip-Stufe: die Fugen werden Fläche statt Punktreihe
			uv_dy *= 1.0 + 3.0 * max(grob, pix);
			staerke = (1.0 - grob) * (1.0 - 0.55 * pix);
			grob = max(grob, 0.55 * pix);   // Fugenfarbe und AO im Pixel-Look auf 45 %
			// Parallax (nur Hoch und Ultra, nur ganz nah): die Textur um den Versatz verschieben, ehe Farbe, Normale und AO gelesen werden
			if (bib_pom > 0 && bib_pixel < 0.5 && bib_stufe >= 2 && rel_tiefe[b] > 0.0) {
				float weit = (1.0 - smoothstep(REL_POM_NAH, REL_POM_FERN, mpp)) * (1.0 - kante_nah);
				if (weit > 0.0) {
					vec3 vw = normalize(transpose(mat3(MODEL_MATRIX)) * (mat3(INV_VIEW_MATRIX) * VIEW));   // Blick zur Kamera in Objektkoordinaten
					uv += rel_versatz(uv, uv_dx, uv_dy, vec3(dot(vw, u), -dot(vw, v), dot(vw, n)), float(rs), rel_tiefe[b] * s * weit, bib_pom);
				}
			}
			fh = textureGrad(rel_farbe, vec3(uv, float(rs)), uv_dx, uv_dy);
			nr = textureGrad(rel_nr, vec3(uv, float(rs)), uv_dx, uv_dy);
			if (kante_nah > 0.001) {
				// an der Kante je zur Haelfte mit der Struktur in der Projektion der Nachbarflaeche (ohne Parallax, nur in diesem schmalen Streifen)
				float wk = 0.5 * kante_nah;
				fh = mix(fh, textureGrad(rel_farbe, vec3(uv2, float(rs)), uv_dx, uv_dy), wk);
				vec4 nr2 = textureGrad(rel_nr, vec3(uv2, float(rs)), uv_dx, uv_dy);
				nr_nachbar = nr2;
				nachbar_wert = wk;
			}
			ao_aus = nr.a;
			staerke *= rel_staerke[b];
			// Tiefe fuer die Naesse: Fugen und Senken (Hoehe und AO) halten das Wasser laenger, Rinnen laufen dort
			tiefe = clamp(0.75 * (1.0 - fh.a) + 0.6 * (1.0 - nr.a), 0.0, 1.0);
		} else {
			fh = textureGrad(bib_farbe, vec3(uv, float(b)), uv_dx, uv_dy);
			tiefe = 1.0 - fh.a;
			nr = normalen ? textureGrad(bib_nr, vec3(uv, float(b)), uv_dx, uv_dy) : vec4(0.5, 0.5, 0.0, 1.0);
			if (kante_nah > 0.001) {
				float wk = 0.5 * kante_nah;
				fh = mix(fh, textureGrad(bib_farbe, vec3(uv2, float(b)), uv_dx, uv_dy), wk);
				if (normalen) nr_nachbar = textureGrad(bib_nr, vec3(uv2, float(b)), uv_dx, uv_dy);
				nachbar_wert = wk;
			}
		}
		vec2 t = (nr.rg * 2.0 - 1.0) * staerke;
		vec3 neigung = u * t.x + v * (t.y + kippen);     // Neigung der Fläche (Objektkoordinaten) aus der Normalen der Textur
		if (nachbar_wert > 0.0) {
			vec2 t2 = (nr_nachbar.rg * 2.0 - 1.0) * staerke;
			neigung = mix(neigung, u2 * t2.x + v2 * t2.y, nachbar_wert);
			ao_aus = mix(ao_aus, nr_nachbar.a, nachbar_wert);
		}
		vec2 uf = vec2(welt.x + welt.y * 0.6, welt.z - welt.y * 0.4) / FLECKEN_GROESSE;
		vec4 f = lesen(uf, dFdx(uf), dFdy(uf), 10.0);
		vec3 g_vor = grund;
		vec3 fl = (1.0 + FLECKEN_HELL * f.r) * (1.0 + FLECKEN_TON * f.g * WARM) * (1.0 + BIB_REIHE_HELL * reihe) * kante;
		grund *= fh.rgb / bib_mittel[b] * fl;
		if (bib_reihen && !liegend) {
			// Verwitterung über die Reihen hinweg, in Dachkoordinaten ohne Versatz (je Haus verschoben): dunkle Laufspuren in
			// Fallrichtung (die Flecken schmal und lang gezogen) und Moosflecken, mehr auf der Nordseite (−z).
			vec2 dach = vec2(dot(ort, u), -dot(ort, v)) + vec2(dot(MODEL_MATRIX[3].xyz, vec3(0.37, 0.0, 0.61)), 0.0);
			vec2 ul = dach / vec2(1.6, 30.0);
			float lauf = smoothstep(0.15, 0.7, lesen(ul, dFdx(ul), dFdy(ul), 10.0).r);
			vec2 um = dach / 4.0 + 0.5;
			float moos = smoothstep(0.25, 0.8, lesen(um, dFdx(um), dFdy(um), 10.0).r);
			moos *= 0.5 + 0.5 * clamp(-normalize(mat3(MODEL_MATRIX) * n).z * 2.0, 0.0, 1.0);
			grund *= 1.0 - BIB_LAUF * lauf;
			grund = mix(grund, BIB_MOOS * dot(grund, vec3(0.3333)) / dot(BIB_MOOS, vec3(0.3333)), BIB_MOOS_ANTEIL * moos);
		}
		if (normalen) {
			vec3 sicht = mat3(VIEW_MATRIX) * (mat3(MODEL_MATRIX) * neigung) / length(MODEL_MATRIX[0].xyz);
			NORMAL = normalize(NORMAL + sicht * BIB_RELIEF);
		}
		if (rel) {
			grund *= mix(1.0, ao_aus, REL_AO_FARBE);
			grund = mix(grund, g_vor * fl, grob);   // fern: Fugen und Verdeckung weg, die mittlere Helligkeit ist die der Modellfarbe (das Mittel der Schicht enthaelt Verdeckung und Streiflicht)
			ao_aus = mix(ao_aus, 1.0, grob);
		}
		grund *= 1.0 - KANTE_SCHATTEN * kante_dunkel;   // Kantenschatten
		ao_aus *= 1.0 - 0.5 * kante_dunkel;
		grund = clamp(grund, 0.0, 1.0);
		rau = normalen ? clamp(nr.b + rauheit - (rel ? 1.0 : 0.9), 0.04, 1.0) : rauheit;
	} else {
		if (v_art >= 1 && v_art <= 9) {   // Art 0 („keine“): nur die Farbe des Modells, etwa wo das Modell Steine schon als Form hat
			int i = v_art - 1;
			vec3 n = normalize(ort_n);
			vec3 w = pow(abs(n), vec3(SCHAERFE));
			w /= (w.x + w.y + w.z);
			float s = 1.0 / GROESSE[i];
			// Seitenflächen mit V nach unten (die Bilder stehen aufrecht: Ziegelspitzen und Strohlagen zeigen nach unten).
			// Eine Mauer von oben gesehen (Mauerkrone, Pflaster) zeigt runde Steine statt Quader.
			float oben = v_art == 3 ? 9.0 : float(i);
			// Nur die Projektionen lesen, die zählen: Meist ist es eine, an Kanten und schrägen Flächen zwei oder drei.
			vec2 ux = vec2(ort.z, -ort.y) * s, uy = ort.xz * s, uz = vec2(ort.x, -ort.y) * s;
			vec2 dxx = dFdx(ux), dyx = dFdy(ux), dxy = dFdx(uy), dyy = dFdy(uy), dxz = dFdx(uz), dyz = dFdy(uz);
			vec4 tx = vec4(0.0), ty = vec4(0.0), tz = vec4(0.0);
			if (w.x > 0.01) tx = lesen(ux, dxx, dyx, float(i));
			if (w.y > 0.01) ty = lesen(uy, dxy, dyy, oben);
			if (w.z > 0.01) tz = lesen(uz, dxz, dyz, float(i));
			vec4 t = tx * w.x + ty * w.y + tz * w.z;
			vec2 uf = vec2(welt.x + welt.y * 0.6, welt.z - welt.y * 0.4) / FLECKEN_GROESSE;
			vec4 f = lesen(uf, dFdx(uf), dFdy(uf), 10.0);
			float hell = 0.5 * t.r * HELL[i] + FLECKEN_HELL * f.r;
			float ton = t.g * TON[i] + FLECKEN_TON * f.g;
			grund *= (1.0 + hell) * (1.0 + ton * WARM);
			// Neigung: je Projektion in Richtung ihrer U- und V-Achse (Objektkoordinaten), dann in die Kamerasicht wie NORMAL
			vec3 neigung = vec3(w.y * ty.b + w.z * tz.b, -(w.x * tx.a + w.z * tz.a), w.x * tx.b + w.y * ty.a);
			vec3 sicht = mat3(VIEW_MATRIX) * (mat3(MODEL_MATRIX) * neigung) / length(MODEL_MATRIX[0].xyz);
			NORMAL = normalize(NORMAL + sicht * RELIEF[i]);
		}
	}
	if (!zonen) {
		// Wetter: Schnee und Nässe. Der Schnee liegt in der Ebene der Fläche (wie die Reihen der Bibliothek): u waagerecht, v hinauf; Reihenhöhe
		// aus der Bibliothek (Dächer ohne Angabe: 0,3 m)
		vec3 sn = normalize(ort_n);
		vec3 su = cross(vec3(0.0, 1.0, 0.0), sn);
		bool sflach = length(su) < 0.05;
		su = sflach ? vec3(1.0, 0.0, 0.0) : normalize(su);
		vec3 sv = sflach ? vec3(0.0, 0.0, -1.0) : cross(sn, su);
		float smass = length(MODEL_MATRIX[0].xyz);
		int sb = (art_aus_uv && v_bib >= 0) ? v_bib : bib;
		float sreihe = (sb >= 0 && sb < BIB_MAX && bib_reihe[sb] > 0.01 ? bib_reihe[sb] : 0.3) * smass;
		vec3 wnorm = mat3(INV_VIEW_MATRIX) * NORMAL;
		vec3 wnorm_neu = wnorm;
		vec3 gl = vec3(0.0);
		// Dachdeckung (Projektion „reihen“: Ziegel, Schiefer, Schindeln) = 1, „dach“ (Stroh, Kupfer) = 0,5, sonst 0: Der Wetter-Shader zählt eine Deckung auch am
		// steilen Turmhelm als Dach (nach der Neigung allein wäre sie eine Wand)
		float dachstoff = (sb >= 0 && sb < BIB_MAX) ? (bib_projektion[sb] == 2 ? 1.0 : (bib_projektion[sb] == 1 ? 0.5 : 0.0)) : 0.0;
		vec3 trocken = grund;
		wetter_flaeche(grund, rau, wnorm_neu, gl, wnorm, normalize(mat3(MODEL_MATRIX) * ort_n), welt, vec2(dot(ort, su), dot(ort, sv)) * smass, sreihe, dachstoff,
				tiefe, ort.y * smass, TIME, bib_stufe >= 1);
		if (sb >= 0 && sb < BIB_MAX && bib_stufe >= 1 && rel_schicht[sb] > 0) grund = mix(trocken, grund, rel_nass[sb]);   // Relief-Stoffe: Nässe je Stoff weniger dunkel
		EMISSION += gl;
		if (wnorm_neu != wnorm) NORMAL = normalize(mat3(VIEW_MATRIX) * wnorm_neu);   // Regentropfen auf flachen Flächen
	}
	ALBEDO = grund;
	AO = mix(1.0, ao_aus, REL_AO_UMGEBUNG);
	AO_LIGHT_AFFECT = 0.0;
	ROUGHNESS = rau;
	METALLIC = metall;
	SPECULAR = glanz;
}
