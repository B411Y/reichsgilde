// Versuch HG-Köpfe: Haarkarten. Alpha-Test mit Kantenglättung (MSAA, Alpha-to-Coverage), beidseitig,
// Normalen kommen aus dem Modell (vom Kopf weg) und werden auf der Rückseite NICHT umgedreht – sonst wirkt jede
// von hinten gesehene Karte schwarz.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D normalen : hint_normal, filter_linear_mipmap_anisotropic;
uniform float schwelle = 0.3;
uniform float rauheit = 0.7;
uniform float glanz = 0.15;
uniform vec3 toenung = vec3(1.0);

varying vec3 n_sicht;

void vertex() {
	n_sicht = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec4 f = texture(farbe, UV);
	ALBEDO = f.rgb * toenung;
	ALPHA = f.a;
	ALPHA_SCISSOR_THRESHOLD = schwelle;
	ALPHA_ANTIALIASING_EDGE = 0.25;
	ALPHA_TEXTURE_COORDINATE = UV * vec2(textureSize(farbe, 0));
	NORMAL = normalize(n_sicht);
	ROUGHNESS = rauheit;
	SPECULAR = glanz;
	BACKLIGHT = vec3(0.25, 0.18, 0.12);
}
