// Nasse Tropfkante unter der Traufe (Stadtansicht.Traufen.cs, GpuParticles3D): Wo das Dachwasser aufkommt, liegt ein weicher, dunkler Fleck auf dem
// Boden, der langsam ausblasst; viele Flecken dicht an dicht ergeben eine nasse Rinne entlang der Hauswand. Alter 0–1 aus INSTANCE_CUSTOM.y.
// Ausgabe nie NaN oder negativ (siehe regen.gdshader).
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec4 farbe : source_color = vec4(0.05, 0.05, 0.06, 1.0);
// 0–1: Beim Herauszoomen blendet die Stadtansicht die Flecken aus (sie behalten ihre Größe in der Welt)
uniform float weit : hint_range(0.0, 1.0) = 1.0;

varying float v_alter;

void vertex() {
	v_alter = INSTANCE_CUSTOM.y;
}

void fragment() {
	float r = length(UV - 0.5) * 2.0;
	float a = (1.0 - smoothstep(0.35, 1.0, r)) * (1.0 - smoothstep(0.5, 1.0, v_alter)) * 0.34;
	float al = clamp(a, 0.0, 1.0) * farbe.a * clamp(weit, 0.0, 1.0);
	if (!(al > 0.004)) discard;
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = al;
}
