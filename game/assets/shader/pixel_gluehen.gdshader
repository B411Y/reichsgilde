// Pixel-Look (Grafikoption, Stadtansicht.Pixel.cs): Glühen, gerechnet im Low-Res-Maßstab der Welt (nicht in voller Fensterauflösung). Das Ergebnis liegt in
// einem eigenen SubViewport von der Größe des Weltbildes; die Stadtansicht zieht es weich (linear) auf Fenstergröße und legt es additiv über das harte
// Pixelbild. Das spart gegenüber dem Versuch (36 Abtastungen je Fensterpixel) etwa drei Viertel (Pixel 2) bis fünf Sechstel (Pixel 3) der Arbeit.
// Leuchtendes (Fenster, Feuer, Laternen) wird mit drei Ringen aus je 8 Abtastwerten verwischt. Was leuchtet, entscheidet das Verhältnis von Bild zu
// Eigenfarbenbild (Zeichnung „Unbeleuchtet“) im Vergleich zum Licht der Umgebung, nicht die Helligkeit allein (helle Wände glühen nicht). Eigenbau (MIT).
shader_type canvas_item;
render_mode blend_disabled;

uniform sampler2D welt : filter_linear, repeat_disable;    // das Low-Res-Bild der Welt (vom Skript gesetzt)
uniform sampler2D albedo : filter_linear, repeat_disable;  // Eigenfarbenbild (vom Skript gesetzt), gleiche Größe
uniform vec2 welt_groesse = vec2(800.0, 450.0);            // Größe des Low-Res-Bildes in Bildpunkten (vom Skript)
uniform float licht_max = 1.5;                             // stärkstes Licht der Tageszeit (vom Skript)

const float STAERKE = 0.85;
const float SCHWELLE = 0.3;      // so hell muss ein Bildpunkt mindestens sein, damit er glüht
const float VERHAELTNIS_LIN = 1.5;  // Verhältnis Bild zu Eigenfarbe, geteilt durch das Licht der Umgebung: ab hier gilt der Bildpunkt als leuchtend
const float RING = 10.0;         // Abstand (Bildpunkte) der Messpunkte für das Licht der Umgebung
const vec3 TINT = vec3(1.0, 0.82, 0.6);   // wärmt nur den Schein (Farbe des Lichts), nicht das Bild
const int TAPS = 8;

float lum(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

// Wie viel mal heller das Bild über der Eigenfarbe liegt, als die Eigenfarbe allein es hätte (= Licht an dieser Stelle, bei Leuchtendem darüber); Magenta (Hilfsfläche): 0
float verhaeltnis(vec2 uv) {
	vec3 a = texture(albedo, uv).rgb;
	if ((a.r > 0.9 && a.g < 0.1 && a.b > 0.9) || a.r + a.g + a.b < 0.02) return 0.0;   // Hilfsfläche (Magenta) und Himmel (Schwarz im Eigenfarbenbild)
	vec3 w = texture(welt, uv).rgb;
	return lum(pow(w, vec3(2.2))) / max(lum(pow(a, vec3(2.2))), 0.03);
}

vec3 hell(vec2 uv, float bezug) {
	vec3 c = texture(welt, uv).rgb;
	float l = lum(c);
	// Leuchtendes erkennt man daran, dass das Bild heller ist, als die Eigenfarbe bei dem Licht der Umgebung erlaubt (Verhältnis im Linearen)
	float w = smoothstep(VERHAELTNIS_LIN, VERHAELTNIS_LIN * 2.6, verhaeltnis(uv) / bezug);
	return c * w * smoothstep(SCHWELLE, SCHWELLE + 0.15, l);
}

void fragment() {
	vec2 px = 1.0 / welt_groesse;
	// Bezug: das Licht der Tageszeit, in der Nähe höchstens das stärkere Licht der Umgebung (Schein einer Laterne auf dem Boden ist kein Glühen)
	float bezug = licht_max;
	for (int i = 0; i < 8; i++) {
		float a = float(i) * 0.7853982;
		bezug = max(bezug, verhaeltnis(UV + vec2(cos(a), sin(a)) * px * RING));
	}
	vec3 s = vec3(0.0);
	for (int i = 0; i < TAPS; i++) {
		float a = float(i) * 2.399963;               // Goldener Winkel
		float r1 = sqrt((float(i) + 0.5) / float(TAPS));
		vec2 d = vec2(cos(a), sin(a)) * r1;
		s += hell(UV + d * px * 1.8, bezug) * 0.45;
		s += hell(UV + d * px * 4.5, bezug) * 0.35;
		s += hell(UV + d * px * 10.0, bezug) * 0.20;
	}
	s /= float(TAPS);
	COLOR = vec4(s * TINT * STAERKE * 2.2, 1.0);
}
