// Oberflächendetails für die Modelle (Grafikoption, Einbau: game/scripts/Oberflaechendetails.cs, Beschreibung in docs/ARCHITEKTUR.md).
// Die Grundfarbe des Modells bleibt bestimmend; je Materialart kommt eine dezente Struktur dazu: Steine mit leichten Farbunterschieden
// und weichen Fugen, Maserung mit Ästen, Ziegel mit leichter Abnutzung, Stroh mit Halmen, Laub mit Blättern, Boden mit Körnung –
// und eine leichte Unebenheit im Licht (Normale). Licht und Schatten sind Godots eigene (kein light()).
// Die Strukturen werden dreiseitig in Objektkoordinaten projiziert: Die Modelle brauchen keine UV-Abwicklung, und an laufenden
// Figuren, fahrenden Karren und gedrehten Häusern bleibt das Muster auf dem Modell, statt durch die Welt zu „schwimmen“.
shader_type spatial;
render_mode cull_disabled;   // Die Modelle aus Blender sind doppelseitig (wie figur.gdshader)

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
	ALBEDO = grund;
	ROUGHNESS = rauheit;
	METALLIC = metall;
	SPECULAR = glanz;
}
