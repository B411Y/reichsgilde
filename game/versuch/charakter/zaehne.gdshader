// Versuch Charakter Phase 2: Zähne und Zahnfleisch (eigener Code, MIT). Die Tiefe hinter den Lippen steht im Rot der
// Eckfarbe (0 vorn, 1 nach 3 cm): Backenzähne und Zunge hinten werden dunkel, die Schneidezähne bleiben hell.
shader_type spatial;
render_mode cull_back;

uniform vec3 farbe : source_color = vec3(0.86, 0.83, 0.76);
uniform float rauheit = 0.32;
uniform float glanz = 0.45;
uniform float dunkel = 0.9;
uniform float anfang = 0.22;
uniform float ende = 0.75;

varying float tiefe;

void vertex() {
	tiefe = COLOR.r;
}

void fragment() {
	float d = smoothstep(anfang, ende, tiefe);
	ALBEDO = farbe * mix(1.0, 1.0 - dunkel, d);
	ROUGHNESS = rauheit;
	SPECULAR = glanz * (1.0 - 0.9 * d);
	AO = 1.0 - 0.85 * d;
	AO_LIGHT_AFFECT = 0.5 * d;
}
