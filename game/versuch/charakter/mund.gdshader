// Versuch Charakter Phase 2: Mundhöhle (eigener Code, MIT). Feucht und nach innen dunkel: Die Tiefe hinter den
// Lippen (0 an der Lippe, 1 nach 3 cm) steht im Blau der Eckfarbe; sie dunkelt Farbe, Glanz und Umgebungslicht ab.
// So bleibt die Höhle auch bei offenem Mund und Licht von vorn dunkel (Verdeckung, die das Netz selbst nicht hat).
shader_type spatial;
render_mode cull_back;

uniform vec3 farbe : source_color = vec3(0.50, 0.20, 0.19);
uniform float dunkel = 0.93;          // wie dunkel es ganz hinten wird
uniform float anfang = 0.06;          // ab hier (Anteil von 3 cm) wird es dunkler
uniform float ende = 0.55;

varying float tiefe;

void vertex() {
	tiefe = COLOR.b;
}

void fragment() {
	float d = smoothstep(anfang, ende, tiefe);
	ALBEDO = farbe * mix(1.0, 1.0 - dunkel, d);
	ROUGHNESS = mix(0.28, 0.6, d);
	SPECULAR = mix(0.5, 0.02, d);
	AO = 1.0 - 0.9 * d;
	AO_LIGHT_AFFECT = 0.6 * d;
}
