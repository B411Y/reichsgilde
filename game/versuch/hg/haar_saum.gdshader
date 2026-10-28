// Versuch HG-Köpfe: Haarkarten, zweiter Durchgang (weiche Säume): nur Alpha < 0,5, gemischt, ohne Tiefe zu schreiben.
shader_type spatial;
render_mode cull_disabled, blend_mix, depth_draw_never;

#include "res://versuch/hg/haar_licht.gdshaderinc"

void fragment() {
	vec4 f = texture(farbe, UV);
	if (f.a >= 0.5 || f.a < 0.05) {
		discard;
	}
	ALBEDO = haar_farbe(f, UV2.x);
	NORMAL = n_sicht;
	ROUGHNESS = 0.6;
	SPECULAR = 0.0;
	BACKLIGHT = vec3(0.22, 0.15, 0.1);
	ALPHA = f.a * 1.6 * kanten_blende(VERTEX, VIEW);
}
