// Pixel-Look (Testszene Pixeltest): Nachbearbeitung des fertigen Bildes in VOLLER Auflösung – keine kleinere Renderauflösung, kein Hochskalieren.
// Der Pixel-Eindruck kommt aus drei Bausteinen (einzeln schaltbar), die alle am fertig getonten Bild rechnen:
//   1. Stufenlicht: die Helligkeit wird auf wenige Stufen gesetzt (Toon), Farbton bleibt; Schattengrenzen werden dadurch hart.
//   2. Palette: frei (Farbtiefe je Kanal) oder feste Holzschnitt-Palette mit 21 Farben (Papier, Tinte, Krapp, Ocker, Grünspan, Blau …).
//   3. Dithering: geordnet (Bayer 8×8), Punktgröße 1 bis 3 Bildpunkte; verteilt die Rundungsfehler von 1. und 2. in Verläufen fein.
// Eigenbau (MIT); das Bayer-Muster ist die bekannte Rekursion (gemeinfrei).
shader_type canvas_item;

uniform sampler2D bild : hint_screen_texture, filter_nearest, repeat_disable;
uniform bool stufen_an = false;
uniform int stufen = 4;               // 3–5 Helligkeitsstufen
uniform int palette = 0;              // 0 aus, 1 frei, 2 Holzschnitt
uniform int frei_stufen = 6;          // Stufen je Farbkanal bei „frei“ (6 → 216 Farben)
uniform bool dither_an = false;
uniform int dither_punkt = 1;         // Kantenlänge einer Musterzelle in Bildpunkten
uniform float dither_staerke = 1.0;

const vec3 HOLZSCHNITT[21] = vec3[](
	vec3(0.10, 0.08, 0.07),  // Tinte
	vec3(0.27, 0.18, 0.12),  // dunkles Braun
	vec3(0.45, 0.30, 0.18),  // Braun
	vec3(0.72, 0.55, 0.25),  // Ocker
	vec3(0.88, 0.76, 0.38),  // Gelb
	vec3(0.93, 0.87, 0.70),  // Papier
	vec3(0.97, 0.94, 0.84),  // helles Papier
	vec3(0.62, 0.17, 0.15),  // Krapp
	vec3(0.78, 0.38, 0.25),  // Ziegelrot
	vec3(0.88, 0.65, 0.52),  // Fleisch
	vec3(0.17, 0.27, 0.18),  // dunkles Grün
	vec3(0.36, 0.48, 0.24),  // Grün
	vec3(0.58, 0.65, 0.38),  // helles Grün
	vec3(0.24, 0.36, 0.52),  // Blau
	vec3(0.55, 0.67, 0.74),  // helles Blau
	vec3(0.52, 0.50, 0.46),  // Grau
	vec3(0.44, 0.50, 0.26),  // Olivgrün
	vec3(0.28, 0.34, 0.17),  // dunkles Olivgrün
	vec3(0.76, 0.68, 0.50),  // Sand
	vec3(0.45, 0.47, 0.50),  // Steingrau
	vec3(0.40, 0.22, 0.16)); // Rotbraun

// Bayer-Matrix 8×8 aus der Rekursion; Ergebnis 0..1 (ohne Mittelwert)
float bayer8(ivec2 p) {
	int x = p.x & 7;
	int y = p.y & 7;
	int a = 0;
	for (int i = 0; i < 3; i++) {
		int bx = (x >> (2 - i)) & 1;
		int by = ((x ^ y) >> (2 - i)) & 1;
		a = a * 4 + (bx * 2 + by) * 1;
	}
	// a ist die bit-umgekehrte Verschränkung; Auswertung der bekannten Formel
	int v = 0;
	for (int i = 0; i < 3; i++) {
		int bx = (x >> i) & 1;
		int by = ((x ^ y) >> i) & 1;
		v = v * 4 + by * 2 + bx;
	}
	// v läuft über 0..63 in Bayer-Reihenfolge, wenn die Bits oben beginnend gesetzt werden
	return (float(v) + 0.5) / 64.0;
}

vec3 holzschnitt(vec3 c) {
	float best = 1e9;
	vec3 wahl = HOLZSCHNITT[0];
	for (int i = 0; i < 21; i++) {
		vec3 d = (c - HOLZSCHNITT[i]) * vec3(0.9, 1.0, 0.7);
		float e = dot(d, d);
		if (e < best) { best = e; wahl = HOLZSCHNITT[i]; }
	}
	return wahl;
}

void fragment() {
	vec3 c = textureLod(bild, SCREEN_UV, 0.0).rgb;
	float b = 0.0;
	if (dither_an) {
		b = (bayer8(ivec2(FRAGCOORD.xy) / max(dither_punkt, 1)) - 0.5) * dither_staerke;
	}
	if (stufen_an) {
		float n = float(max(stufen, 2));
		float l = dot(c, vec3(0.299, 0.587, 0.114));
		float lq = floor(l * n + 0.5 + b * 0.9) / n;
		// Stufe 0 bleibt dunkel, nicht schwarz: sonst fressen Schatten die Farbe
		lq = max(lq, 0.5 / n);
		c = clamp(c * (lq / max(l, 0.02)), 0.0, 1.0);
	}
	if (palette == 1) {
		float k = float(max(frei_stufen, 2) - 1);
		c = clamp(floor(c * k + 0.5 + b) / k, 0.0, 1.0);
	} else if (palette == 2) {
		c = holzschnitt(c + vec3(b * 0.16));
	}
	COLOR = vec4(c, 1.0);
}
