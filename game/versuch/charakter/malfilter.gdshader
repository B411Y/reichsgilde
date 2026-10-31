// Charakter Halt B2: Malfilter nur für das Porträt (eigener Code, MIT). Kuwahara-Filter mit vier Sektoren (je Bildpunkt
// der Sektor mit der kleinsten Helligkeitsstreuung, Mittelwert seiner Farben – flächige, pinselartige Farbfelder, Kanten
// bleiben) und eine leichte Tusche-Kante (Sobel auf der Helligkeit, dunkelt nur kräftige Kanten). Liegt als ColorRect
// in einer CanvasLayer der Porträtbühne (charakter_buehne.gd, malfilter_setzen) über dem 3D-Bild.
// Kosten: 4 · (radius + 1)² + 9 Bildzugriffe je Bildpunkt (radius 3: 73), nur im Porträtfenster.
shader_type canvas_item;

uniform sampler2D bild : hint_screen_texture, filter_nearest;
uniform int radius = 3;              // Halbmesser der Sektoren in Bildpunkten
uniform float mischung = 0.85;       // Anteil des Filters (1 = ganz gemalt, 0 = Original)
uniform float tusche = 0.3;          // Abdunkeln an kräftigen Kanten
uniform vec3 tusche_farbe : source_color = vec3(0.16, 0.11, 0.08);

float hell(vec3 c) {
	return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

void fragment() {
	vec2 px = SCREEN_PIXEL_SIZE;
	vec3 original = texture(bild, SCREEN_UV).rgb;
	vec3 mittel[4];
	float streu[4];
	vec2 richtung[4] = vec2[](vec2(-1.0, -1.0), vec2(1.0, -1.0), vec2(-1.0, 1.0), vec2(1.0, 1.0));
	float n = float((radius + 1) * (radius + 1));
	for (int k = 0; k < 4; k++) {
		vec3 summe = vec3(0.0);
		float s1 = 0.0;
		float s2 = 0.0;
		for (int i = 0; i <= radius; i++) {
			for (int j = 0; j <= radius; j++) {
				vec3 c = texture(bild, SCREEN_UV + vec2(float(i), float(j)) * richtung[k] * px).rgb;
				float h = hell(c);
				summe += c;
				s1 += h;
				s2 += h * h;
			}
		}
		mittel[k] = summe / n;
		float m = s1 / n;
		streu[k] = s2 / n - m * m;
	}
	vec3 gemalt = mittel[0];
	float best = streu[0];
	for (int k = 1; k < 4; k++) {
		if (streu[k] < best) {
			best = streu[k];
			gemalt = mittel[k];
		}
	}
	vec3 c = mix(original, gemalt, mischung);
	// Tusche: Sobel auf der Helligkeit (3 × 3)
	float a = hell(texture(bild, SCREEN_UV + vec2(-1.0, -1.0) * px).rgb);
	float b = hell(texture(bild, SCREEN_UV + vec2(0.0, -1.0) * px).rgb);
	float d = hell(texture(bild, SCREEN_UV + vec2(1.0, -1.0) * px).rgb);
	float e = hell(texture(bild, SCREEN_UV + vec2(-1.0, 0.0) * px).rgb);
	float f = hell(texture(bild, SCREEN_UV + vec2(1.0, 0.0) * px).rgb);
	float g = hell(texture(bild, SCREEN_UV + vec2(-1.0, 1.0) * px).rgb);
	float h = hell(texture(bild, SCREEN_UV + vec2(0.0, 1.0) * px).rgb);
	float i2 = hell(texture(bild, SCREEN_UV + vec2(1.0, 1.0) * px).rgb);
	float gx = (d + 2.0 * f + i2) - (a + 2.0 * e + g);
	float gy = (g + 2.0 * h + i2) - (a + 2.0 * b + d);
	float kante = smoothstep(0.12, 0.45, length(vec2(gx, gy)));
	c = mix(c, c * tusche_farbe * 2.0, kante * tusche);
	COLOR = vec4(c, 1.0);
}
