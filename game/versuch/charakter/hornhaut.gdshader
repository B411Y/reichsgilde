// Charakter Phase 3: Hornhaut und Tränenfilm (eigener Code, MIT): nur Glanz und Spiegelung, additiv über Iris und
// Lederhaut (Feuchtfilm, Glanzpunkt des Schlüssellichts). Ohne eigene Farbe, schreibt keine Tiefe.
// Etappe D: Tränenfilm (traene_d) nach der Eckfarbe (kopf2_bauen.py: R = Nähe zum inneren Lidwinkel, G = Oberlid-Seite,
// B = Abstand zur Lidnaht / 8 mm): der Tränensee am Unterlid glänzt, oben und an der Karunkel kaum.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_back, specular_schlick_ggx;

uniform float rauheit = 0.035;
uniform float staerke = 1.0;
uniform bool traene_d = false;
uniform float traene_oben = 0.15;    // Anteil des Glanzes am Oberlid
uniform float traene_innen = 0.2;    // Anteil an der Karunkel

void fragment() {
	ALBEDO = vec3(0.0);
	ROUGHNESS = rauheit;
	float s = staerke;
	if (traene_d) {
		s *= mix(1.0, traene_oben, smoothstep(0.3, 0.7, COLOR.g)) * mix(1.0, traene_innen, COLOR.r);
	}
	SPECULAR = 0.5 * s;
	METALLIC = 0.0;
}
