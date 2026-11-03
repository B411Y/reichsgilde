// Pixel-Look (Grafikoption, Stadtansicht.Pixel.cs): minimale Vignette in voller Auflösung (mal, ohne Farbstich): Ränder und Ecken der Welt werden
// etwas dunkler. Liegt auf der Welt-Ebene unter der Oberfläche, Tafeln und Schrift bleiben unberührt. Eigenbau (MIT).
shader_type canvas_item;
render_mode blend_mul;

const float STAERKE = 0.18;

void fragment() {
	vec2 p = (UV - 0.5) * vec2(1.0, 0.9);
	float v = smoothstep(0.35, 0.85, length(p));
	float f = 1.0 - STAERKE * v;
	COLOR = vec4(vec3(f), 1.0);
}
