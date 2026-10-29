// Ringe und Spritzer der Regentropfen am Boden (Stadtansicht.Wetter.cs, GpuParticles3D): Jedes Partikel ist ein kurzlebiger Ring auf dem
// Boden (waagerechtes Quad), der sich vom Aufschlag nach außen weitet und ausblasst, dazu ein kleiner heller Spritzer im ersten Augenblick.
// Alter 0–1 kommt aus INSTANCE_CUSTOM.y des Partikels.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

global uniform float wetter_regen;

uniform vec4 farbe : source_color = vec4(0.8, 0.85, 0.95, 1.0);

varying float v_alter;
varying float v_id;

void vertex() {
	v_alter = INSTANCE_CUSTOM.y;
	v_id = float(INSTANCE_ID);
}

void fragment() {
	float r = length(UV - 0.5) * 2.0;
	float t = v_alter;
	float radius = 0.15 + 0.85 * t;
	float dicke = 0.2 - 0.09 * t;
	float ring = 1.0 - smoothstep(0.0, dicke, abs(r - radius));
	float spritzer = (1.0 - smoothstep(0.0, 0.35, r)) * (1.0 - smoothstep(0.0, 0.25, t));
	float a = (ring * (1.0 - t) * (1.0 - 0.6 * t) * 0.65 + spritzer * 0.65) * (0.6 + 0.4 * fract(sin(v_id * 12.9898) * 43758.5453));
	// Ausgabe nie NaN oder negativ (siehe regen.gdshader)
	float al = clamp(a, 0.0, 1.0) * farbe.a;
	if (!(al > 0.004)) discard;
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = al;
}
