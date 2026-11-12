shader_type spatial;
// Brandschaden an Wänden (Stein, Putz, Lehm): zweite Schicht über dem Wandstoff. Rußfahnen über allen Öffnungen (V-Muster, am Sturz dunkel,
// nach oben breiter und blasser, Saum um die Öffnung) und ein leichter, fleckiger Schleier über der ganzen Wand. Weltlage statt Fläche, feines
// Rauschen blendet nach fwidth aus (Pixel-Look ohne Moiré).
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

#include "brand_gemein.gdshaderinc"

uniform float schleier = 0.10;

varying vec3 welt;
varying vec3 normale_welt;

void vertex() {
	VERTEX += NORMAL * 0.012; // 1 cm vor der Fläche: sonst streiten Schicht und Dach um die Tiefe (Punktraster im Pixel-Look)
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	normale_welt = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
	float fw = length(fwidth(welt));
	float fein = 1.0 - smoothstep(0.03, 0.12, fw);
	float n = b_rauschen(welt * 1.7) * 0.65 + mix(0.5, b_rauschen(welt * 5.5), fein) * 0.35;
	float fahne = b_fahnen(welt, normalize(normale_welt), n);
	float kante = smoothstep(0.0, 1.0, fahne);
	ALBEDO = vec3(0.016, 0.014, 0.013);
	ROUGHNESS = 1.0;
	ALPHA = clamp(schleier * (0.4 + 1.2 * n) + kante * 0.9, 0.0, 0.94);
}
