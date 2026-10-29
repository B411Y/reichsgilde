// Versuch Charakter Phase 0: Haut (eigener Code, MIT). Vorbild für die Bausteine war RoboPoets „digital_human“
// (Rauheit am Rand für Flaum, Glanz nur auf straffer Haut); hier ohne Bildschirm-Subsurface, damit viele
// Porträts bezahlbar bleiben:
// - Streulicht billig über Wrap-Beleuchtung: am Schattenrand rötlich weich statt hart
// - Rötung, Glanz (T-Zone, Lippen) und Vertiefungen aus der Eckfarbe „Hautmaske“ (R, G, B) der .glb
// - Poren als nahtlose Normalenkarte, gleich groß auf jeder UV (poren_dichte = Wiederholungen je UV-Einheit)
// - Randlicht (Flaum) und Glanzdämpfung in Furchen
shader_type spatial;
render_mode cull_back, depth_draw_opaque;

uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform bool hat_farbe = true;
uniform vec3 grundfarbe : source_color = vec3(0.78, 0.6, 0.5);
uniform vec3 toenung = vec3(1.0);          // je Volk (Ork grünlich, Oger grau)
uniform bool masken = true;                 // Eckfarbe nutzen (A1 hat keine)
uniform sampler2D poren : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float poren_dichte = 60.0;
uniform float poren_staerke = 0.35;
uniform float rauheit = 0.58;
uniform float rauheit_glanz = 0.36;         // in der T-Zone und auf den Lippen
uniform float glanz = 0.32;                 // Spiegelanteil (0,5 = Standard)
uniform float roetung = 0.55;
uniform vec3 roetung_farbe = vec3(1.0, 0.72, 0.68);
uniform float hoehlen = 0.55;               // Abdunkeln und weniger Glanz in Furchen
uniform float streu = 0.42;                 // Wrap-Breite
uniform vec3 streu_farbe = vec3(0.95, 0.45, 0.32);
uniform float flaum = 0.18;                 // Randlicht

varying vec3 maske;

void vertex() {
	maske = masken ? COLOR.rgb : vec3(0.0);
}

void fragment() {
	vec3 c = hat_farbe ? texture(farbe, UV).rgb : grundfarbe;
	c *= toenung;
	c *= mix(vec3(1.0), roetung_farbe, clamp(maske.r * roetung, 0.0, 1.0));
	c *= 1.0 - hoehlen * 0.45 * maske.b;
	ALBEDO = c;
	vec3 n = texture(poren, UV * poren_dichte).rgb;
	NORMAL_MAP = n;
	NORMAL_MAP_DEPTH = poren_staerke;
	// Rauheit: Grundwert, in der T-Zone glatter; Poren rauer; am Rand rauer (Flaum, weniger Spiegelung am Umriss)
	float pore = clamp(length(n.xy - 0.5) * 3.0, 0.0, 1.0);
	float rand = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ROUGHNESS = clamp(mix(rauheit, rauheit_glanz, maske.g) + 0.12 * pore + 0.15 * rand, 0.2, 1.0);
	SPECULAR = glanz * (1.0 - 0.7 * hoehlen * maske.b);
	BACKLIGHT = vec3(0.0);
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float hart = max(ndl, 0.0);
	float wrap = max((ndl + streu) / (1.0 + streu), 0.0);
	vec3 diff = vec3(hart) + (wrap - hart) * streu_farbe;
	// Flaum: Randlicht, nur wo Licht hinkommt (Gegenlicht zeichnet den Umriss)
	float nv = max(dot(NORMAL, VIEW), 1e-3);
	float umriss = pow(1.0 - nv, 4.0) * smoothstep(-0.3, 0.4, ndl);
	diff += umriss * flaum * 3.0;
	DIFFUSE_LIGHT += diff * ATTENUATION * LIGHT_COLOR / PI;
	// Glanz: GGX mit Schlick-Fresnel, über SPECULAR_AMOUNT gedämpft
	vec3 h = normalize(VIEW + LIGHT);
	float a = ROUGHNESS * ROUGHNESS;
	float a2 = a * a;
	float nh = max(dot(NORMAL, h), 0.0);
	float dnen = nh * nh * (a2 - 1.0) + 1.0;
	float d = a2 / (PI * dnen * dnen);
	float k = a * 0.5;
	float vis = 0.25 / ((hart * (1.0 - k) + k) * (nv * (1.0 - k) + k) + 1e-4);
	float f0 = 0.08 * SPECULAR_AMOUNT;
	float f = f0 + (1.0 - f0) * pow(1.0 - max(dot(VIEW, h), 0.0), 5.0);
	// LIGHT_COLOR enthält in Godot den Faktor PI (Energie · PI), daher hier wieder heraus
	SPECULAR_LIGHT += hart * d * vis * f * ATTENUATION * LIGHT_COLOR / PI;
}
