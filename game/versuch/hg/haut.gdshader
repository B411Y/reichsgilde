// Versuch HG-Köpfe: Haut. Gebackene HG-Hauttextur (aus 8K herunter), dazu je Volk ein Hauttyp (Tönung, Sättigung,
// Rauheit, Streulicht). Streulicht billig über „Wrap“-Beleuchtung: am Schattenrand rötlich weich statt hart
// (kein Bildschirm-Subsurface, das bei vielen Porträts zu teuer wäre).
shader_type spatial;
render_mode cull_back;

uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D normalen : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D rauheit : hint_roughness_g, filter_linear_mipmap;
uniform vec3 toenung = vec3(1.0);
uniform float saettigung = 1.0;
uniform float helligkeit = 1.0;
uniform float poren = 1.0;              // Stärke der Normalenkarte (Poren, Falten)
uniform float rauh_versatz = 0.12;
uniform float rauh_min = 0.42;
uniform float glanz = 0.26;             // Spiegelanteil (0,5 = Standard); Haut wirkt sonst ölig
uniform float streu = 0.38;             // Wrap-Breite
uniform vec3 streu_farbe = vec3(0.9, 0.42, 0.32);
uniform float gegerbt = 0.0;            // wettergegerbt: Falten tiefer, Haut matter und dunkler in den Furchen

void fragment() {
	vec3 c = texture(farbe, UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	// Kittelmaske in UV2.y (gleiche Textur wie die Haut): Blender schreibt 1 auf den Kittel, der glTF-Export dreht v um
	// (v -> 1 - v), im Spiel ist also UV2.y = 0 auf dem Kittel und 1 auf der Haut
	float haut = step(0.5, UV2.y);
	c = mix(c, mix(vec3(l), c, saettigung) * toenung * helligkeit, haut);
	vec3 n = texture(normalen, UV).rgb;
	// Furchen aus der Normalenkarte (Abweichung von flach) leicht abdunkeln
	float furche = clamp(length(n.xy - 0.5) * 2.0, 0.0, 1.0);
	c *= 1.0 - gegerbt * 0.35 * furche * haut;
	ALBEDO = c;
	NORMAL_MAP = n;
	NORMAL_MAP_DEPTH = poren * (1.0 + gegerbt);
	ROUGHNESS = clamp(texture(rauheit, UV).g + rauh_versatz + gegerbt * 0.12, rauh_min, 1.0);
	SPECULAR = glanz;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float hart = max(ndl, 0.0);
	float wrap = max((ndl + streu) / (1.0 + streu), 0.0);
	vec3 diff = vec3(hart) + (wrap - hart) * streu_farbe;
	DIFFUSE_LIGHT += diff * ATTENUATION * LIGHT_COLOR / PI;
	// Glanz: GGX mit Schlick-Fresnel (wie Godot, aber über SPECULAR_AMOUNT gedämpft)
	vec3 h = normalize(VIEW + LIGHT);
	float a = ROUGHNESS * ROUGHNESS;
	float a2 = a * a;
	float nh = max(dot(NORMAL, h), 0.0);
	float dnen = nh * nh * (a2 - 1.0) + 1.0;
	float d = a2 / (PI * dnen * dnen);
	float nv = max(dot(NORMAL, VIEW), 1e-3);
	float k = a * 0.5;
	float vis = 0.25 / ((hart * (1.0 - k) + k) * (nv * (1.0 - k) + k) + 1e-4);
	float f0 = 0.08 * SPECULAR_AMOUNT;
	float f = f0 + (1.0 - f0) * pow(1.0 - max(dot(VIEW, h), 0.0), 5.0);
	// LIGHT_COLOR enthält in Godot den Faktor PI (Energie * PI), daher hier wieder heraus
	SPECULAR_LIGHT += hart * d * vis * f * ATTENUATION * LIGHT_COLOR / PI;
}
