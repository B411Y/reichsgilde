// Charakter Phase 6: Haut für Stand 6 (eigener Code, MIT). Kopie von haut3.gdshaderinc (Phase 3–5; bleibt unverändert
// für Stand 5) mit diesen Änderungen:
//   S2 Glanz wie Godots GGX: kein zweites / PI (π steckt schon in D), F0 fest 0,028 (Haut, IOR 1,4), Fresnel bis 1,
//      Sichtbarkeit V_SmithGGXCorrelated (Godots V_GGX), SPECULAR_AMOUNT nur als Lichtfaktor. Die Glanzmaske (Höhlen ×
//      Falten × Haar) erreicht als varying spec_v auch das direkte Licht; SPECULAR = 0,42 · spec_v ergibt F0 0,028 für
//      das Umgebungslicht. „glanz“ ist jetzt ein Faktor auf diesen Glanz (1 = Haut), „keule2“ der Anteil der schmalen
//      Keule (0 = eine Keule, Prüfkugel). Untergrenze der Rauheit 0,10 (genutzt erst über die Feuchtemaske, S8).
//   S2 Ultra (haut6_ultra.gdshader, #define ULTRA): render_mode sss_mode_skin (Hautkern statt des grauen
//      Allzweck-Kerns), der Wrap × 0,35 (die Streuung übernimmt dort Godots SSS). Zweig für Flächenlichter
//      (LIGHT_IS_AREA, Godot 4.7) nach dem Beispiel der Doku.
//   S3 Wrap energieerhaltend (n·l + w)/(1 + w)², Flaum 0,16 -> 0,05 (kommt in S8 als Glanz zurück), Streulicht
//      jenseits des Schattenrands nur noch zu 30 % ohne Schatten der eigenen Rundung (streu_schattenfrei 1 -> 0,3).
// Farben kommen als Color (sRGB, Godot rechnet nach linear um); Faktoren wie „toenung“ bleiben vec3.
// Phase 3 (Herkunft): gebaut auf dem Vortest A2 der Phase 0 (haut.gdshader).
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
#ifdef ULTRA
render_mode cull_back, depth_draw_opaque, sss_mode_skin;
#else
render_mode cull_back, depth_draw_opaque;
#endif

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
uniform float glanz = 1.0;                   // Faktor auf den Hautglanz (F0 0,028); 1 = Haut
uniform float keule2 = 0.18;                 // Anteil der schmalen Glanzkeule (Rauheit × 0,55); 0 = eine Keule
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
uniform float wrap_neutral = 1.0;            // Etappe-A-Nachtrag: 1 = im vollen Licht farbneutral (siehe light())
uniform float streu_farbe = 0.3;             // Etappe-A-Nachtrag: Anteil der Farbtrennung des Wraps (0 = grauer Wrap, 1 = volle)
uniform float streu_schattenfrei = 0.3;      // 1 = Streulicht jenseits des Schattenrands ohne Schatten der eigenen Rundung
uniform float flaum = 0.05;                  // Randlicht (S8: als Glanz)
uniform float durchlicht = 0.35;             // Ohren und Nase im Gegenlicht
uniform float sss_staerke = 0.25;            // nur ULTRA
uniform float unruhe = 0.0;                  // Phase 5: ungleichmäßiger Hautton der Erwachsenen (gegen „Porzellan“)
// Phase 4: Haut unter Haar und Bart (tools/charakter/kopf4_haar.py, Spiel-UV 1024²). Die Kopfhaut unter dichtem Haar
// nimmt die Haarfarbe an (Schatten und Haaransatz: kein heller Schimmer durch das Haar, kein Helmrand); wo eine
// Glatze ist (Rang < kahl), bleibt Haut.
uniform sampler2D kopfhaut_tex : hint_default_black, filter_linear_mipmap, repeat_disable;   // Dichte je Frisur (Kanal)
uniform sampler2D rang_tex : hint_default_white, filter_linear_mipmap, repeat_disable;       // A = Rang beim Ausfallen
uniform vec4 kopfhaut_kanal = vec4(0.0);
uniform float kahl = 0.0;
uniform float duenn_haut = 0.0;
uniform sampler2D bart_tex : hint_default_black, filter_linear_mipmap, repeat_disable;       // Dichte je Bartform
uniform vec4 bart_kanal = vec4(0.0);
uniform vec3 haar_unter : source_color = vec3(0.1, 0.07, 0.05);
uniform float kopfhaut_dunkel = 0.8;

varying vec3 n_diffus;
varying float duenn;
varying float hoehle_v;
varying float spec_v;                        // Glanzmaske (Höhlen, Falten, Haar) × glanz, für das direkte Licht
const float F0_HAUT = 0.028;                 // Haut, IOR 1,4

// GGX wie Godot (scene_forward_lights_inc.glsl): D_GGX und V_GGX (Smith, korreliert, Näherung nach Hammon)
float d_ggx(float nh, float a) {
	float x = nh * a;
	float k = a / (1.0 - nh * nh + x * x);
	return k * k / PI;
}
float v_ggx(float nl, float nv, float a) {
	return 0.5 / mix(2.0 * nl * nv, nl + nv, a);
}

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
	// Phase 5: Unruhe im Hautton (grob und fein, aus der Porenkachel; nach Rot und Gelb verschoben, nicht nur heller)
	float fl = (texture(poren, UV * 3.1).x - 0.5) * 1.4 + (texture(poren, UV * 11.7 + 0.37).y - 0.5) * 0.8;
	c *= vec3(1.0) + unruhe * fl * vec3(0.8, 1.05, 1.3);
	c = mix(c, c * bart_farbe, clamp(m.a * bart, 0.0, 1.0));
	c = mix(c, haar_farbe * 0.8, clamp(f.a * brauen_dichte * 0.55, 0.0, 0.7));
	c *= 1.0 - falten_dunkel * clamp(-h0, 0.0, 1.0);
	c *= 1.0 - hoehlen * 0.5 * m.b;
	// Phase 4: unter Haar und Bart
	float unter = dot(texture(kopfhaut_tex, UV), kopfhaut_kanal);
	unter *= smoothstep(kahl - 0.05, kahl + 0.05, texture(rang_tex, UV).a) * (1.0 - 0.5 * duenn_haut);
	float unter_bart = clamp(dot(texture(bart_tex, UV), bart_kanal), 0.0, 1.0);
	float haarig = clamp(max(unter, unter_bart), 0.0, 1.0);
	c = mix(c, haar_unter * mix(0.55, 0.85, haarig), haarig * kopfhaut_dunkel);
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
	ROUGHNESS = clamp(mix(rauheit, rauheit_glanz, m.g) + 0.1 * pore + 0.12 * rand + 0.15 * clamp(-h0, 0.0, 1.0) + 0.3 * haarig, 0.10, 1.0);
	spec_v = glanz * (1.0 - 0.7 * hoehlen * m.b) * (1.0 - 0.5 * clamp(-h0, 0.0, 1.0)) * (1.0 - 0.7 * haarig);
	SPECULAR = 0.42 * spec_v;                // Godot: F0 = 0,16 · SPECULAR² = 0,028 (Umgebungslicht)
	AO = 1.0 - hoehlen * m.b * 0.8;
	AO_LIGHT_AFFECT = 0.3;
#ifdef ULTRA
	SSS_STRENGTH = sss_staerke;
#endif
}

void light() {
	if (LIGHT_IS_AREA) {
		// Flächenlicht (Godot 4.7): Godot rechnet die Fläche (LTC); hier nur Streulicht und Glanzmaske (Beispiel der Doku)
		DIFFUSE_LIGHT += LIGHT_AREA_DIFFUSE_MULTIPLIER * ATTENUATION * LIGHT_COLOR;
		SPECULAR_LIGHT += LIGHT_AREA_SPECULAR_MULTIPLIER * ATTENUATION * LIGHT_COLOR * SPECULAR_AMOUNT * spec_v;
	} else {
		float ndl = dot(NORMAL, LIGHT);
		float ndl_d = dot(n_diffus, LIGHT);
		// in Höhlen (Nasenlöcher, Ohrmuschel, Mundwinkel) kaum Streulicht
		vec3 w = streu * streu_anteil * (1.0 - 0.7 * hoehle_v);
#ifdef ULTRA
		w *= 0.35;                           // die Streuung macht unter Ultra Godots SSS (Hautkern)
#endif
		// S3: energieerhaltend (McAuley 2011, wie Godots DIFFUSE_LAMBERT_WRAP)
		vec3 wrap_e = clamp((vec3(ndl_d) + w) / ((1.0 + w) * (1.0 + w)), 0.0, 1.0);
		// Etappe-A-Nachtrag (Prüferbefund „Haut fahl“, „Oberlid orange“): je Kanal (n·l + w)/(1 + w)² nahm dem Rot im
		// vollen Licht bis zu 17 % (Wange R/G 1,22 -> 1,11) und gab es am Schattenrand im Übermaß zurück (Oberlid C 24).
		// Neutral: (n·l + w)/(1 + w) je Kanal, geteilt durch (1 + w) der Helligkeit – im vollen Licht alle Kanäle gleich,
		// die Energie bleibt in der Helligkeit erhalten
		float w_hell = dot(w, vec3(0.2126, 0.7152, 0.0722));
		vec3 wrap_n = clamp((vec3(ndl_d) + w) / ((1.0 + w) * (1.0 + w_hell)), 0.0, 1.0);
		vec3 wrap = mix(wrap_e, wrap_n, wrap_neutral);
		float wrap_grau = clamp((ndl_d + w_hell) / ((1.0 + w_hell) * (1.0 + w_hell)), 0.0, 1.0);
		wrap = vec3(wrap_grau) + streu_farbe * (wrap - vec3(dot(wrap, vec3(0.2126, 0.7152, 0.0722))));
		float schatten = ATTENUATION;
		if (LIGHT_IS_DIRECTIONAL) {
			float frei = streu_schattenfrei * (1.0 - hoehle_v);
			schatten = mix(mix(1.0, ATTENUATION, 1.0 - frei), ATTENUATION, smoothstep(-0.05, 0.3, ndl_d));
		}
		vec3 diff = wrap * schatten;
		// Flaum: Randlicht, nur wo Licht hinkommt
		float nv = max(dot(NORMAL, VIEW), 1e-4);
		float umriss = pow(1.0 - nv, 4.0) * smoothstep(-0.3, 0.4, ndl);
		diff += vec3(umriss * flaum * 3.0) * schatten;
		// Durchlicht: Licht von hinten durch Ohren und Nasenflügel (rötlich)
		float hinten = pow(clamp(dot(VIEW, -LIGHT), 0.0, 1.0), 3.0) * clamp(-ndl_d + 0.3, 0.0, 1.0);
		diff += vec3(1.0, 0.35, 0.22) * hinten * duenn * durchlicht;
		DIFFUSE_LIGHT += diff * LIGHT_COLOR / PI;   // Lambert: LIGHT_COLOR enthält π
		// Glanz: zwei GGX-Keulen (Rauheit r und 0,55 r) wie Godot, Schlick-Fresnel mit F0 0,028 bis 1, ohne / PI
		float hart = max(ndl, 0.0);
		vec3 h = normalize(VIEW + LIGHT);
		float nh = clamp(dot(NORMAL, h), 0.0, 1.0);
		float lh = clamp(dot(LIGHT, h), 0.0, 1.0);
		float f = F0_HAUT + (1.0 - F0_HAUT) * pow(1.0 - lh, 5.0);
		float a1 = ROUGHNESS * ROUGHNESS;
		float r2 = ROUGHNESS * 0.55;
		float a2 = r2 * r2;
		float spec = (1.0 - keule2) * d_ggx(nh, a1) * v_ggx(hart, nv, a1) + keule2 * d_ggx(nh, a2) * v_ggx(hart, nv, a2);
		SPECULAR_LIGHT += hart * spec * f * spec_v * ATTENUATION * LIGHT_COLOR * SPECULAR_AMOUNT;
	}
}
