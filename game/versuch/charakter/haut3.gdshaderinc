// Charakter Phase 3: Haut (eigener Code, MIT), gebaut auf dem Vortest A2 der Phase 0 (haut.gdshader).
// Bilder (tools/charakter/kopf3_backen.py, 2048², linear):
//   farbe_tex   RGB = Hautbild / mittlere Hautfarbe × 0,5 (der Ton kommt aus den Genen: grundfarbe), A = Brauenschatten
//   normal_tex  RG = Normale (Tangentenraum), B = Porenstärke für die feine Kachel (poren)
//   masken_tex  R = Rötung, G = Glanz, B = Verdeckung, A = Bartschatten (Bereich)
//   falten_tex  Höhen der Falten (0,5 = keine): R Stirn, G Augen, B Mund, A Hals und Lippen; je Kanal ein Gewicht aus dem Alter
// Licht: Wrap je Farbkanal (Rot streut am weitesten) mit geglätteter Normale für das Streulicht und voller Normale für den
// Glanz; Randlicht (Flaum); Durchlicht an Ohren und Nase; zwei Glanzkeulen. Echtes Subsurface Scattering (Bildschirm,
// teuer) nur in der Ultra-Fassung (haut3_ultra.gdshader, #define ULTRA).
// Der Schatten des Schlüssellichts gilt im Streulichtbereich jenseits des Schattenrands nicht: Dort verdeckt die eigene
// Rundung (Schattenkarte) – das ergab in Phase 2 den „dunklen Fleck vor dem Ohr“ (Schatten × Wrap).
render_mode cull_back, depth_draw_opaque;

uniform sampler2D farbe_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D masken_tex : hint_default_black, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D falten_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D poren : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 grundfarbe : source_color = vec3(0.78, 0.6, 0.5);
uniform vec3 toenung = vec3(1.0);            // je Volk und Gen „Hautton“
uniform float poren_dichte = 60.0;           // Wiederholungen der feinen Kachel je UV-Einheit
uniform float poren_staerke = 0.45;
uniform float normal_staerke = 1.0;          // gebackene Normale (Poren der Nase, Lippenlinien)
uniform float rauheit = 0.56;
uniform float rauheit_glanz = 0.4;           // T-Zone, Lippen
uniform float glanz = 0.34;                  // Spiegelanteil (0,5 = Standard)
uniform float roetung = 0.32;
uniform vec3 roetung_farbe = vec3(1.0, 0.76, 0.72);
uniform float hoehlen = 0.6;                 // Verdeckung in Furchen, Nasenlöchern, Ohren
uniform vec4 falten = vec4(0.0);             // Gewicht je Kanal (Alter): Stirn, Augen, Mund, Hals/Lippen
uniform float falten_tiefe = 2.0;            // Neigung der Normale je Höheneinheit und Bildpunkt
uniform float falten_dunkel = 0.12;          // Furchen etwas dunkler
uniform float bart = 0.0;                    // Bartschatten (Stoppeln unter der Haut), 0..1
uniform vec3 bart_farbe = vec3(0.72, 0.74, 0.8);
uniform float brauen_dichte = 0.0;           // Brauenschatten auf der Haut
uniform vec3 haar_farbe : source_color = vec3(0.12, 0.08, 0.05);
uniform float streu = 0.4;                   // Wrap-Breite für Rot (Grün, Blau schmaler)
uniform vec3 streu_anteil = vec3(1.0, 0.5, 0.36);
uniform float streu_schattenfrei = 1.0;      // 1 = Streulicht jenseits des Schattenrands ohne Schatten der eigenen Rundung
uniform float flaum = 0.16;                  // Randlicht
uniform float durchlicht = 0.35;             // Ohren und Nase im Gegenlicht
uniform float sss_staerke = 0.25;            // nur ULTRA

varying vec3 n_diffus;
varying float duenn;
varying float hoehle_v;

void fragment() {
	vec4 f = texture(farbe_tex, UV);
	vec4 m = texture(masken_tex, UV);
	vec3 nt = texture(normal_tex, UV).rgb;
	// Falten: Höhe und Gefälle (drei Abtastungen)
	vec2 e = 1.0 / vec2(textureSize(falten_tex, 0));
	vec4 g = falten;
	float h0 = dot(texture(falten_tex, UV).rgba * 2.0 - 1.0, g);
	float hu = dot(texture(falten_tex, UV + vec2(e.x, 0.0)).rgba * 2.0 - 1.0, g);
	float hv = dot(texture(falten_tex, UV + vec2(0.0, e.y)).rgba * 2.0 - 1.0, g);
	// Farbe: Ton aus den Genen × Hautbild, Rötung, Bart, Braue, Furchen
	vec3 c = grundfarbe * toenung * f.rgb * 2.0;
	c *= mix(vec3(1.0), roetung_farbe, clamp(m.r * roetung, 0.0, 1.0));
	c = mix(c, c * bart_farbe, clamp(m.a * bart, 0.0, 1.0));
	c = mix(c, haar_farbe * 0.8, clamp(f.a * brauen_dichte * 0.55, 0.0, 0.7));
	c *= 1.0 - falten_dunkel * clamp(-h0, 0.0, 1.0);
	c *= 1.0 - hoehlen * 0.5 * m.b;
	ALBEDO = c;
	// Normale: gebacken (Mittel) + Falten + feine Kachel (Poren), im Tangentenraum zusammen
	vec2 meso = (nt.xy * 2.0 - 1.0) * normal_staerke;
	vec2 mikro = (texture(poren, UV * poren_dichte).xy * 2.0 - 1.0) * poren_staerke * (0.35 + nt.b);
	// Tangentenraum: +x = +u, +y = Bild oben = -v (Godot-UV)
	vec2 fal = vec2(-(hu - h0), (hv - h0)) * falten_tiefe;
	vec3 n = normalize(vec3(meso + mikro + fal, 1.0));
	NORMAL_MAP = n * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;
	// Streulicht mit geglätteter Normale (halb Geometrie, halb Falten, ohne Poren)
	vec3 n_glatt = normalize(vec3(meso * 0.5 + fal * 0.6, 1.0));
	n_diffus = normalize(TANGENT * n_glatt.x + BINORMAL * n_glatt.y + NORMAL * n_glatt.z);
	duenn = clamp(m.r * 1.2 - 0.25, 0.0, 1.0) * (1.0 - m.b);
	hoehle_v = m.b;
	float pore = clamp(length(mikro) * 2.0, 0.0, 1.0);
	float rand = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ROUGHNESS = clamp(mix(rauheit, rauheit_glanz, m.g) + 0.1 * pore + 0.12 * rand + 0.15 * clamp(-h0, 0.0, 1.0), 0.18, 1.0);
	SPECULAR = glanz * (1.0 - 0.7 * hoehlen * m.b) * (1.0 - 0.5 * clamp(-h0, 0.0, 1.0));
	AO = 1.0 - hoehlen * m.b * 0.8;
	AO_LIGHT_AFFECT = 0.3;
#ifdef ULTRA
	SSS_STRENGTH = sss_staerke;
#endif
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float ndl_d = dot(n_diffus, LIGHT);
	// in Höhlen (Nasenlöcher, Ohrmuschel, Mundwinkel) kaum Streulicht
	vec3 w = streu * streu_anteil * (1.0 - 0.7 * hoehle_v);
	vec3 wrap = clamp((vec3(ndl_d) + w) / (1.0 + w), 0.0, 1.0);
	float schatten = ATTENUATION;
	if (LIGHT_IS_DIRECTIONAL) {
		float frei = streu_schattenfrei * (1.0 - hoehle_v);
		schatten = mix(mix(1.0, ATTENUATION, 1.0 - frei), ATTENUATION, smoothstep(-0.05, 0.3, ndl_d));
	}
	vec3 diff = wrap * schatten;
	// Flaum: Randlicht, nur wo Licht hinkommt
	float nv = max(dot(NORMAL, VIEW), 1e-3);
	float umriss = pow(1.0 - nv, 4.0) * smoothstep(-0.3, 0.4, ndl);
	diff += vec3(umriss * flaum * 3.0) * schatten;
	// Durchlicht: Licht von hinten durch Ohren und Nasenflügel (rötlich)
	float hinten = pow(clamp(dot(VIEW, -LIGHT), 0.0, 1.0), 3.0) * clamp(-ndl_d + 0.3, 0.0, 1.0);
	diff += vec3(1.0, 0.35, 0.22) * hinten * duenn * durchlicht;
	DIFFUSE_LIGHT += diff * LIGHT_COLOR / PI;
	// Glanz: zwei GGX-Keulen (breit und schmal) mit Schlick-Fresnel
	float hart = max(ndl, 0.0);
	vec3 h = normalize(VIEW + LIGHT);
	float nh = max(dot(NORMAL, h), 0.0);
	float f0 = 0.08 * SPECULAR_AMOUNT;
	float f = f0 + (1.0 - f0) * pow(1.0 - max(dot(VIEW, h), 0.0), 5.0);
	float spec = 0.0;
	for (int i = 0; i < 2; i++) {
		float r = i == 0 ? ROUGHNESS : ROUGHNESS * 0.55;
		float a = r * r;
		float a2 = a * a;
		float dnen = nh * nh * (a2 - 1.0) + 1.0;
		float dd = a2 / (PI * dnen * dnen);
		float k = a * 0.5;
		float vis = 0.25 / ((hart * (1.0 - k) + k) * (nv * (1.0 - k) + k) + 1e-4);
		spec += (i == 0 ? 0.82 : 0.18) * dd * vis;
	}
	// LIGHT_COLOR enthält in Godot den Faktor PI, daher hier wieder heraus
	SPECULAR_LIGHT += hart * spec * f * ATTENUATION * LIGHT_COLOR / PI;
}
