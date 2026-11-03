// Pixel-Look (Testszene Pixeltest), Runde 4: Glühen in voller Auflösung über dem Pixelbild. Liegt als Rechteck über dem hochgezogenen Weltbild
// (additiv) und liest dessen niedrig aufgelöstes Bild weich (linear): Leuchtendes (Fenster, Feuer, Laternen) wird mit drei Ringen aus je 12 Abtastwerten
// verwischt und dazugerechnet. Was leuchtet, entscheidet das Verhältnis von Bild zu Beleuchtungsbild, nicht die Helligkeit allein (helle Wände glühen nicht). Das Pixelbild darunter bleibt hart, nur der Schein ist weich. Eigenbau (MIT).
shader_type canvas_item;
render_mode blend_add;

uniform sampler2D welt : filter_linear, repeat_disable;   // das Low-Res-Bild der Welt (vom Skript gesetzt)
uniform vec2 welt_groesse = vec2(800.0, 450.0);            // Größe des Low-Res-Bildes in Bildpunkten
uniform float staerke = 1.0;
uniform sampler2D licht : filter_linear, repeat_disable;   // Beleuchtungsbild (vom Skript gesetzt), gleiche Größe wie das Weltbild
uniform float schwelle = 0.3;      // so hell muss ein Bildpunkt mindestens sein, damit er glüht
uniform float verhaeltnis = 1.3;   // Bild / Beleuchtung: ab hier gilt der Bildpunkt als leuchtend
uniform float radius = 1.0;        // Vielfaches des Standardradius
uniform vec3 tint = vec3(1.0, 0.82, 0.6);   // wärmt nur den Schein (Farbe des Lichts), nicht das Bild

float lum(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

vec3 hell(vec2 uv) {
	vec3 c = texture(welt, uv).rgb;
	float l = lum(c);
	// Leuchtendes erkennt man daran, dass das Bild heller ist, als die Beleuchtung allein es erlaubt (Beleuchtungsbild: weiße Flächen, gleiche Lichter):
	// Ein beleuchteter Körper ist höchstens so hell wie das Licht, ein Fenster oder Feuer deutlich heller.
	float lb = max(lum(texture(licht, uv).rgb), 0.03);
	float w = smoothstep(verhaeltnis, verhaeltnis + 0.7, l / lb) * smoothstep(schwelle, schwelle + 0.15, l);
	return c * w;
}

void fragment() {
	vec2 px = 1.0 / welt_groesse;
	vec3 s = vec3(0.0);
	float g = 0.0;
	for (int i = 0; i < 12; i++) {
		float a = float(i) * 2.399963;               // Goldener Winkel
		float r1 = sqrt((float(i) + 0.5) / 12.0);
		vec2 d = vec2(cos(a), sin(a)) * r1;
		s += hell(UV + d * px * 1.8 * radius) * 0.45;
		s += hell(UV + d * px * 4.5 * radius) * 0.35;
		s += hell(UV + d * px * 10.0 * radius) * 0.20;
		g += 1.0;
	}
	s /= g;
	COLOR = vec4(s * tint * staerke * 2.2, 1.0);
}
