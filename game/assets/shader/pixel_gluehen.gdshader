// Pixel-Look (Grafikoption, Stadtansicht.Pixel.cs): Glühen, gerechnet im Low-Res-Maßstab der Welt (nicht in voller Fensterauflösung). Das Ergebnis liegt in
// einem eigenen SubViewport von der Größe des Weltbildes; die Stadtansicht zieht es weich (linear) auf Fenstergröße und legt es additiv über das harte
// Pixelbild. Das spart gegenüber dem Versuch (36 Abtastungen je Fensterpixel) etwa drei Viertel (Pixel 2) bis fünf Sechstel (Pixel 3) der Arbeit.
// Leuchtendes (Fenster, Feuer, Laternen) wird mit drei Ringen aus je 8 Abtastwerten verwischt. Was leuchtet, entscheidet das Verhältnis von Bild zu
// Beleuchtungsbild, nicht die Helligkeit allein (helle Wände glühen nicht). Eigenbau (MIT).
shader_type canvas_item;
render_mode blend_disabled;

uniform sampler2D welt : filter_linear, repeat_disable;    // das Low-Res-Bild der Welt (vom Skript gesetzt)
uniform sampler2D licht : filter_linear, repeat_disable;   // Beleuchtungsbild (vom Skript gesetzt), gleiche Größe
uniform vec2 welt_groesse = vec2(800.0, 450.0);            // Größe des Low-Res-Bildes in Bildpunkten (vom Skript)

const float STAERKE = 0.85;
const float SCHWELLE = 0.3;      // so hell muss ein Bildpunkt mindestens sein, damit er glüht
const float VERHAELTNIS = 1.3;   // Bild / Beleuchtung: ab hier gilt der Bildpunkt als leuchtend
const vec3 TINT = vec3(1.0, 0.82, 0.6);   // wärmt nur den Schein (Farbe des Lichts), nicht das Bild
const int TAPS = 8;

float lum(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

vec3 hell(vec2 uv) {
	vec3 c = texture(welt, uv).rgb;
	float l = lum(c);
	// Leuchtendes erkennt man daran, dass das Bild heller ist, als die Beleuchtung allein es erlaubt (Beleuchtungsbild: weiße Flächen, gleiche Lichter)
	float lb = max(lum(texture(licht, uv).rgb), 0.03);
	float w = smoothstep(VERHAELTNIS, VERHAELTNIS + 0.7, l / lb) * smoothstep(SCHWELLE, SCHWELLE + 0.15, l);
	return c * w;
}

void fragment() {
	vec2 px = 1.0 / welt_groesse;
	vec3 s = vec3(0.0);
	for (int i = 0; i < TAPS; i++) {
		float a = float(i) * 2.399963;               // Goldener Winkel
		float r1 = sqrt((float(i) + 0.5) / float(TAPS));
		vec2 d = vec2(cos(a), sin(a)) * r1;
		s += hell(UV + d * px * 1.8) * 0.45;
		s += hell(UV + d * px * 4.5) * 0.35;
		s += hell(UV + d * px * 10.0) * 0.20;
	}
	s /= float(TAPS);
	COLOR = vec4(s * TINT * STAERKE * 2.2, 1.0);
}
