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
// Aus dem alten Material übernommen
uniform float rauheit = 1.0;
uniform float metall = 0.0;
uniform float glanz = 0.5;

// Material-Bibliothek (Modell-Texturen, tools/oberflaeche/materialien_blender.py, game/daten/materialbibliothek.json): gebackene
// 1024-px-Materialien (Bruchstein, Quader, Putz, Holz, Ziegel, Schiefer, Stroh …). bib ≥ 0 ist die Schicht, sonst gilt die Struktur
// darunter. bib_stufe (Einstellung „Boden“): 0 Niedrig – die Struktur wie bisher, 1 Mittel – Farbe der Bibliothek, 2 Hoch – dazu
// Normalen und Rauheit. Die Textur wird durch ihre mittlere Farbe (bib_mittel) geteilt und mit der Modellfarbe gefärbt: Im Mittel
// bleibt jedes Material so hell und bunt wie die Modellfarbe, Häuser bleiben an ihren Farben zu erkennen.
uniform int bib = -1;
uniform int bib_stufe = 2;
uniform float bib_kachel = 2.0;                  // Meter je Kachel
uniform vec3 bib_mittel = vec3(0.5);             // mittlere Farbe der Schicht (linear)
uniform bool bib_dach = false;                   // Dachdeckung: eine Projektion entlang der Dachneigung (oben = firstwärts)
uniform bool bib_reihen = false;                 // Ziegel und Schiefer: Spalten je Modellreihe versetzt (siehe fragment)
uniform sampler2DArray bib_farbe : source_color, repeat_enable, filter_linear_mipmap_anisotropic;   // RGB Farbe, A Höhe
uniform sampler2DArray bib_nr : repeat_enable, filter_linear_mipmap_anisotropic;                    // RG Normale, B Rauheit
const float BIB_RELIEF = 1.0;                    // Stärke der Normalen
const float BIB_REIHE_HELL = 0.04;               // Helligkeitsunterschied der Dachreihen
const float BIB_LAUF = 0.22;                     // Laufspuren auf Dächern: so viel dunkler
const vec3 BIB_MOOS = vec3(0.30, 0.34, 0.12);    // Moos auf Dächern: Farbton (die Helligkeit kommt vom Dach)
const float BIB_MOOS_ANTEIL = 0.45;

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
varying flat int v_zone;
varying vec3 eckfarbe;

void vertex() {
	// Eckenfarben stehen als sRGB-Werte in der GLB: Der Compatibility-Renderer rechnet in sRGB, Forward+ linear (wie figur.gdshader)
	eckfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		eckfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	ort = VERTEX;
	ort_n = NORMAL;
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_art = art_aus_uv ? int(round(UV.y)) : art;
	v_zone = int(round(UV.x));
	if (v_art == 8 && !zonen) VERTEX += wetter_laub(VERTEX, welt, MODEL_MATRIX, TIME); // Wetter: Laub im Wind
}

// Ein Wert der Schicht: (Wert − 128) / 127, also −1 … 1 mit 0 bei 128. Mit den Ableitungen von außen (textureGrad), denn gelesen
// wird nur in Zweigen, und dort sind die Ableitungen der Grafikkarte nicht verlässlich.
vec4 lesen(vec2 uv, vec2 dx, vec2 dy, float schicht) {
	return textureGrad(strukturen, vec3(uv, schicht), dx, dy) * (255.0 / 127.0) - (128.0 / 127.0);
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
	}
	float rau = rauheit;   // Rauheit nach Bibliothek bzw. Modell; das Wetter (Nässe) ändert sie danach
	if (bib >= 0 && bib_stufe > 0) {
		bool normalen = bib_stufe >= 2;
		float s = 1.0 / bib_kachel;
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
		if (bib_reihen && !liegend) {
			// Die Reihen hat das Modell: jede Ziegel- oder Schieferreihe eine eigene, leicht gekippte Fläche (Sägezahn,
			// tools/modelle/bausteine.py, zaehne). Ihr Abstand vom Ursprung, dot(ort, n), ist auf der ganzen Fläche gleich und springt
			// von Reihe zu Reihe um einige Zentimeter. Daraus je Reihe ein Versatz der Spalten (Fugen versetzt wie beim Decken, kein
			// Karomuster) und ein Helligkeitsunterschied. Stetig in d statt eines Würfels: Rechenrauschen verschiebt nichts sichtbar.
			float d = dot(ort, n);
			uv.x += d * 7.31 + 0.37 * sin(d * 113.0) + 0.23 * sin(d * 271.0);
			reihe = sin(d * 157.0 + 1.3 * sin(d * 59.0));
		}
		vec4 fh = textureGrad(bib_farbe, vec3(uv, float(bib)), uv_dx, uv_dy);
		vec4 nr = normalen ? textureGrad(bib_nr, vec3(uv, float(bib)), uv_dx, uv_dy) : vec4(0.5, 0.5, 0.0, 1.0);
		vec2 t = nr.rg * 2.0 - 1.0;
		vec3 neigung = u * t.x + v * t.y;     // Neigung der Fläche (Objektkoordinaten) aus der Normalen der Textur
		vec2 uf = vec2(welt.x + welt.y * 0.6, welt.z - welt.y * 0.4) / FLECKEN_GROESSE;
		vec4 f = lesen(uf, dFdx(uf), dFdy(uf), 10.0);
		grund *= fh.rgb / bib_mittel * (1.0 + FLECKEN_HELL * f.r) * (1.0 + FLECKEN_TON * f.g * WARM) * (1.0 + BIB_REIHE_HELL * reihe);
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
		grund = clamp(grund, 0.0, 1.0);
		rau = normalen ? clamp(nr.b + rauheit - 0.9, 0.04, 1.0) : rauheit;
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
	if (!zonen) wetter_flaeche(grund, rau, mat3(INV_VIEW_MATRIX) * NORMAL, welt); // Wetter: Schnee und Nässe
	ALBEDO = grund;
	ROUGHNESS = rau;
	METALLIC = metall;
	SPECULAR = glanz;
}
