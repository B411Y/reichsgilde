// Charakter Phase 3: Hornhaut und Tränenfilm (eigener Code, MIT): nur Glanz und Spiegelung, additiv über Iris und
// Lederhaut (Feuchtfilm, Glanzpunkt des Schlüssellichts). Ohne eigene Farbe, schreibt keine Tiefe.
// Etappe D: Tränenfilm (traene_d) nach der Eckfarbe (kopf2_bauen.py: R = Nähe zum inneren Lidwinkel, G = Oberlid-Seite,
// B = Abstand zum Lidrand / 8 mm): der Tränensee am Unterlid glänzt, oben und an der Karunkel kaum. Hornhaut (fenster > 0):
// weiches Fenster-Glanzlicht – Spiegelung einer großen Lichtfläche schräg über der Kamera, unabhängig von den Lampen der
// Szene (der Glanzpunkt des Schlüssellichts lag unter dem Oberlid; Prüfer: „kein Glanzpunkt wie im Foto, leerer Blick“).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_back, specular_schlick_ggx;

uniform float rauheit = 0.035;
uniform float staerke = 1.0;
uniform bool traene_d = false;
uniform float traene_oben = 0.15;    // Anteil des Glanzes am Oberlid
uniform float traene_innen = 0.2;    // Anteil an der Karunkel
uniform float fenster = 0.0;         // Stärke des Fenster-Glanzlichts (0 = aus)
uniform vec3 fenster_richtung = vec3(-0.2, 0.17, 0.96);    // im Blickraum, zur Kamera hin
uniform float fenster_groesse = 0.13;  // Winkelhalbmesser (Bogenmaß)
// Grundkopf 3 (Prüfer: „Blick glasig, kein klarer Glanzpunkt“; 0 = wie bisher): ein kleiner, scharfer Kern im Fenster-Glanzlicht
// (der Glanzpunkt wie im Foto) und eine schwache Spiegelung der hellen Umgebung oben (Himmel, Decke) – nach unten dunkel,
// zum Rand stärker (Fresnel). Beides hängt nur an der Blickrichtung, nicht an den Lampen.
uniform float glanz_kern = 0.0;
uniform float glanz_kern_groesse = 0.035;
uniform float umgebung = 0.0;
uniform vec3 umgebung_farbe : source_color = vec3(0.85, 0.88, 0.92);

void fragment() {
	ALBEDO = vec3(0.0);
	ROUGHNESS = rauheit;
	float s = staerke;
	if (traene_d) {
		s *= mix(1.0, traene_oben, smoothstep(0.3, 0.7, COLOR.g)) * mix(1.0, traene_innen, COLOR.r);
	}
	SPECULAR = 0.5 * s;
	METALLIC = 0.0;
	if (fenster > 0.0) {
		float c = dot(normalize(reflect(-VIEW, NORMAL)), normalize(fenster_richtung));
		EMISSION = vec3(fenster * smoothstep(cos(fenster_groesse * 1.8), cos(fenster_groesse * 0.15), c));   // weicher Rand
		if (glanz_kern > 0.0) {
			EMISSION += vec3(glanz_kern * smoothstep(cos(glanz_kern_groesse * 1.5), cos(glanz_kern_groesse * 0.6), c)) * s;
		}
	}
	if (umgebung > 0.0) {
		vec3 rv = normalize(reflect(-VIEW, NORMAL));
		float fr = 0.04 + 0.96 * pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 5.0);
		EMISSION += umgebung_farbe * umgebung * smoothstep(-0.15, 0.6, rv.y) * (0.35 + 8.0 * fr) * s;
	}
}
