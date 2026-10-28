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
	float radius = 0.12 + 0.88 * t;
	float dicke = 0.16 - 0.08 * t;
	float ring = smoothstep(dicke, 0.0, abs(r - radius));
	float spritzer = smoothstep(0.35, 0.0, r) * smoothstep(0.25, 0.0, t);
	float a = (ring * (1.0 - t) * (1.0 - t) * 0.55 + spritzer * 0.6) * (0.6 + 0.4 * fract(sin(v_id * 12.9898) * 43758.5453));
	ALBEDO = farbe.rgb;
	ALPHA = clamp(a, 0.0, 1.0) * farbe.a;
}
