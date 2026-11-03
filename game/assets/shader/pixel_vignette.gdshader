// Pixel-Look (Testszene Pixeltest), Runde 4: minimale Vignette in voller Auflösung (mal, ohne Farbstich): Ränder und Ecken werden etwas dunkler.
// Eigenbau (MIT).
shader_type canvas_item;
render_mode blend_mul;

uniform float staerke = 0.18;

void fragment() {
	vec2 p = (UV - 0.5) * vec2(1.0, 0.9);
	float v = smoothstep(0.35, 0.85, length(p));
	float f = 1.0 - staerke * v;
	COLOR = vec4(vec3(f), 1.0);
}
