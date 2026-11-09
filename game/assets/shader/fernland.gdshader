// Fernland: das Land hinter dem Kartenrand, wie es die Ego-Ansicht und die freie Kamera zeigen (Stadtansicht.Himmel.cs). Ein großes, flaches Netz mit sanften
// Hügeln um die Karte, Äckern, Weiden, Wald und Brache als Flickenmuster, nach Jahreszeit gefärbt (wie der Boden und die Kulissenberge). Je weiter weg, desto
// mehr nimmt es die Farbe der Luft an: Es beginnt dort, wo der Dunstring der Karte endet (mit demselben Dunstanteil), und läuft bis zum Horizont ganz in den
// Dunst aus. Gemalt-realistisch: Godots Licht, kein Schatten, keine Textur, nur Farbe und Rauschen. Die Spielkamera sieht es nie (sie blickt nur auf die
// Karte): Das Spiel blendet es dort aus.
shader_type spatial;
render_mode cull_back, shadows_disabled, fog_disabled;

global uniform vec4 jahreszeit_anteile;
global uniform float wetter_schnee;

uniform vec3 himmel : source_color = vec3(0.47, 0.60, 0.72);   // Horizont (Farbe des Dunstrings)
uniform vec4 innen = vec4(-100.0, -100.0, 100.0, 100.0);       // wie am Dunstring: dort beginnt der Dunst
uniform float rand_aus = 600.0;        // so weit (m) vom Rechteck „innen“ endet der Dunstring: hier beginnt das Fernland
uniform float anfang_dunst = 0.45;     // Dunstanteil des Rings an seinem äußeren Rand
uniform float reichweite = 1100.0;     // so weit (m) hinter dem Ring wächst der Dunst auf das Vierfache der Restdurchsicht
uniform float dunst = 0.0;             // zusätzlicher Dunst durch das Wetter, 0–1

varying vec3 v_welt;
varying float v_wald;

float f_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float f_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(f_hash(i), f_hash(i + vec2(1.0, 0.0)), f.x), mix(f_hash(i + vec2(0.0, 1.0)), f_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float wald_rauschen(vec2 p) {
	return f_rausch(p * 0.0042 + 11.0) * 0.65 + f_rausch(p * 0.012 + 3.0) * 0.25 + f_rausch(p * 0.05) * 0.10;
}

void vertex() {
	// Waldstücke stehen als niedrige Kuppen über dem Land (Baumhöhe ~16 m): vom Boden aus zeichnen sich Wälder am Horizont ab, flache Felder nicht
	vec2 p = VERTEX.xz;
	float aus = max(max(innen.x - p.x, p.x - innen.z), max(innen.y - p.y, p.y - innen.w));
	v_wald = smoothstep(0.55, 0.68, wald_rauschen(p));
	VERTEX.y += v_wald * 16.0 * smoothstep(rand_aus, rand_aus + 350.0, aus);
	v_welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec4 j = jahreszeit_anteile;
	vec2 p = v_welt.xz;

	// Flicken: gedrehtes Raster, je Zelle eine Nutzung (Acker, Weide, Stoppel, Brache), darüber Wälder aus grobem Rauschen
	vec2 q = mat2(vec2(0.94, -0.34), vec2(0.34, 0.94)) * p / 130.0;
	vec2 zelle = floor(q);
	float zufall = f_hash(zelle);
	float zufall2 = f_hash(zelle + 31.7);
	float feinheit = f_rausch(p * 0.05) * 0.6 + f_rausch(p * 0.21) * 0.4;
	float furchen = 0.5 + 0.5 * sin(dot(q, vec2(zufall - 0.5, zufall2 - 0.5) * 2.0 + vec2(0.3, 1.0)) * 90.0);

	vec3 wiese = vec3(0.30, 0.36, 0.17) * (j.x * vec3(0.95, 1.07, 0.84) + j.y + j.z * vec3(1.3, 1.05, 0.7) + j.w * vec3(1.15, 1.1, 1.05));
	vec3 acker = vec3(0.30, 0.22, 0.13) * (0.8 + 0.4 * furchen * 0.5 + 0.2 * zufall2);
	vec3 korn = vec3(0.45, 0.40, 0.18) * (j.x * vec3(0.55, 0.85, 0.5) + j.y * vec3(1.15, 1.05, 0.85) + j.z * vec3(0.8, 0.65, 0.5) + j.w * vec3(0.7, 0.6, 0.55));
	vec3 weide = vec3(0.26, 0.34, 0.15) * (0.85 + 0.3 * feinheit);
	vec3 grund = wiese * (0.85 + 0.3 * feinheit);
	grund = mix(grund, weide, step(0.30, zufall) * step(zufall, 0.60));
	grund = mix(grund, korn * (0.9 + 0.2 * furchen), step(0.60, zufall) * step(zufall, 0.80));
	grund = mix(grund, acker, step(0.80, zufall));

	float wald = v_wald;
	vec3 wald_f = vec3(0.10, 0.17, 0.07) * (j.x * vec3(1.1, 1.15, 0.9) + j.y + j.z * vec3(1.9, 1.0, 0.55) + j.w * vec3(1.2, 1.0, 0.95)) * (0.85 + 0.3 * f_rausch(p * 0.09));
	grund = mix(grund, wald_f, wald);

	// Winter: Schnee deckt Äcker und Weiden, im Wald bleibt es dunkler
	grund = mix(grund, vec3(0.78, 0.80, 0.84), clamp(wetter_schnee * (0.95 - 0.4 * wald), 0.0, 1.0));

	// Dunst: wie der Ring am Rand beginnen, dann mit dem Abstand zur Kamera dichter; bei Nebel mehr
	float aus = max(max(innen.x - p.x, p.x - innen.z), max(innen.y - p.y, p.y - innen.w));
	float ferne = max(aus - rand_aus, 0.0);
	float d = 1.0 - (1.0 - anfang_dunst) * exp(-ferne / reichweite);
	d = mix(d, 1.0, dunst * 0.8);
	d = clamp(d, 0.0, 1.0);
	d = max(d, smoothstep(0.55, 1.0, d));       // fern ganz im Dunst
	ALBEDO = grund * (1.0 - d);
	EMISSION = himmel * d;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
