// Boden der Geländekacheln (Kartenwerkstatt, Schritt 5e): mischt bis zu elf gemalte Texturen nach drei Gewichtsbildern.
// Gemalt-realistisch wie die übrige 3D-Welt. Gewichte je Punkt des Bodenrasters (1 m): Gruppe 0 = Wiese, Erde, Fels, Kies;
// Gruppe 1 = Waldboden, Sand, Schlamm, Moos; Gruppe 2 = Lehm, Pflaster, Mist (RGBA8, zusammen 255). Das Bild einer Kachel
// beginnt einen Punkt vor ihr (66 × 66 Punkte), damit die Übergänge über Kachelkanten glatt bleiben.
// Die Texturen (boden.png, Texture2DArray) tragen Farbe und in A die Höhe: Wo zwei Texturen ineinander übergehen, wächst das
// Erhabene (Steine, Halme) über das Tiefe (Fugen, Mulden), und ein Rauschen macht die Grenzen unregelmäßig.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
// Rauheit und Glanz wie der StandardMaterial3D der Wiese (0,95 / 0,5), damit Kacheln mit und ohne Boden gleich hell sind

uniform sampler2D splat0 : filter_linear, repeat_disable;
uniform sampler2D splat1 : filter_linear, repeat_disable;
uniform sampler2D splat2 : filter_linear, repeat_disable;
uniform sampler2DArray texturen : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
// Welt-X/Z des ersten Punktes im Bild (eine Kachel minus 1 m) und Bildgröße in Punkten
uniform vec2 ursprung = vec2(0.0);
uniform float bildgroesse = 66.0;
// Länge einer Textur in Metern
uniform float massstab = 6.0;

varying vec2 welt;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

// Farbe (rgb) und Höhe (a) einer Textur: zwei Maßstäbe übereinander, damit sich nichts sichtbar wiederholt
vec4 probe(int nr, vec2 p) {
	vec4 a = texture(texturen, vec3(p / massstab, float(nr)));
	vec4 b = texture(texturen, vec3(p / (massstab * 2.63) + vec2(0.37, 0.71), float(nr)));
	return vec4(mix(a.rgb, b.rgb, 0.35), mix(a.a, b.a, 0.35));
}

void fragment() {
	vec2 uv = (welt - ursprung + vec2(0.5)) / bildgroesse;
	vec4 g0 = texture(splat0, uv);
	vec4 g1 = texture(splat1, uv);
	vec4 g2 = texture(splat2, uv);
	float w[11] = float[](g0.r, g0.g, g0.b, g0.a, g1.r, g1.g, g1.b, g1.a, g2.r, g2.g, g2.b);
	// unregelmäßige Grenzen: ein langsames Rauschen verschiebt, wer wo gewinnt
	float n = rausch(welt * 0.45) * 0.6 + rausch(welt * 1.7) * 0.4;
	vec3 farbe = vec3(0.0);
	float summe = 0.0;
	for (int i = 0; i < 11; i++) {
		if (w[i] < 0.012) continue;
		vec4 t = probe(i, welt);
		float gewinn = w[i] * (0.55 + t.a * 0.9 + (hash(vec2(float(i), 3.0)) - 0.5) * (n - 0.5) * 1.6);
		gewinn = max(gewinn, 0.0) * w[i];
		farbe += t.rgb * gewinn;
		summe += gewinn;
	}
	farbe /= max(summe, 0.0001);
	ALBEDO = farbe;
	ROUGHNESS = 0.95;
}
