// Wasser der Karte (Kartenwerkstatt, Schritt 5c): Flüsse und Bäche als Band, Seen und Teiche als Fläche, gemalt-realistisch.
// Die Darstellung baut das Netz mit diesen Eckenwerten (siehe Stadtansicht.Wasser.cs):
//   UV     = (quer, Stelle): bei einem Fluss quer zur Strömung von -1 (linkes Ufer) bis +1 (rechtes Ufer) und die Stelle in Metern entlang
//            des Laufs; bei einem See die Weltkoordinaten x, z.
//   UV2    = Strömung in Metern je Sekunde nach x und z (See: 0).
//   COLOR  = (Tiefe in der Mitte, halbe Breite, 0, Fluss = 1 / See = 0). Beim See steht in r die Tiefe an dieser Ecke.
// Die Tiefe färbt das Wasser (flach: grünlich-braun, man sieht den Grund; tief: dunkles Blaugrün), am Ufer läuft es durchsichtig und mit
// Schaum aus. Die Oberfläche kräuselt sich mit der Strömung (Wellen wandern flussab, streifig in Fließrichtung); ein See liegt still.
// Licht und Tageszeit kommen von der Szene: Sonne und Mond spiegeln sich im Wasser, den Himmel liefert die Farbe "himmel" (das Spiel setzt
// sie jedes Bild auf die Himmelsfarbe der Tageszeit, weil die Szene keinen Himmel zum Spiegeln hat).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, specular_schlick_ggx;

uniform vec3 flach : source_color = vec3(0.30, 0.38, 0.27);
uniform vec3 tief : source_color = vec3(0.03, 0.15, 0.22);
uniform vec3 himmel : source_color = vec3(0.47, 0.60, 0.72);
uniform vec3 schaum : source_color = vec3(0.86, 0.88, 0.84);
uniform float spiegelung = 0.8;
uniform float welle = 0.22;

varying vec3 welt;
varying vec2 strom;
varying vec4 werte;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Wertrauschen mit Ableitung: x = Wert, yz = Steigung
vec3 rauschen(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	vec2 du = 30.0 * f * f * (f - 1.0) * (f - 1.0);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	float k = a - b - c + d;
	float v = a + (b - a) * u.x + (c - a) * u.y + k * u.x * u.y;
	vec2 g = du * vec2((b - a) + k * u.y, (c - a) + k * u.x);
	return vec3(v, g);
}

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	strom = UV2;
	werte = COLOR;
}

void fragment() {
	bool fluss = werte.a > 0.5;
	float quer = UV.x;
	// Tiefe an dieser Stelle: im Fluss ein Parabelbett (am Ufer 0, in der Mitte die Tiefe), im See der Wert der Ecke
	float tiefe = fluss ? werte.r * (1.0 - quer * quer) : werte.r;
	float rand = fluss ? (1.0 - abs(quer)) * werte.g : tiefe * 3.0; // Abstand zum Ufer in Metern (im See grob aus der Tiefe)
	float tempo = length(strom);

	// Wellen: zwei Lagen Rauschen, gestreckt in Fließrichtung und mit der Strömung flussab getragen
	vec2 richtung = tempo > 0.01 ? strom / tempo : vec2(1.0, 0.0);
	vec2 seite = vec2(-richtung.y, richtung.x);
	vec2 p = welt.xz;
	float s = dot(p, richtung);
	float q = dot(p, seite);
	float t = TIME;
	vec2 w1 = vec2(s - t * tempo * 0.9, q) * vec2(0.55, 1.5);
	vec2 w2 = vec2(s - t * tempo * 1.3 + 7.3, q + 3.1) * vec2(1.1, 2.6);
	vec3 r1 = rauschen(tempo > 0.01 ? w1 : p * 0.8 + vec2(t * 0.03, t * 0.02));
	vec3 r2 = rauschen(tempo > 0.01 ? w2 : p * 1.7 - vec2(t * 0.04, t * 0.03));
	// Steigung zurück in Weltachsen (in Fließrichtung gestreckte Wellen haben dort die kleinere Steigung)
	vec2 g1 = tempo > 0.01 ? (richtung * r1.y * 0.55 + seite * r1.z * 1.5) : r1.yz * 0.8;
	vec2 g2 = tempo > 0.01 ? (richtung * r2.y * 1.1 + seite * r2.z * 2.6) : r2.yz * 1.7;
	float ruhig = tempo > 0.01 ? clamp(0.5 + tempo * 0.6, 0.5, 1.5) : 0.9; // See: sanfte Kräuselung, schneller Fluss: unruhiger
	vec2 g = (g1 * 0.7 + g2 * 0.3) * welle * ruhig;
	vec3 n_welt = normalize(vec3(-g.x, 1.0, -g.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(n_welt, 0.0)).xyz);

	// Farbe nach Tiefe
	float k = 1.0 - exp(-max(tiefe, 0.0) * 1.3);
	vec3 farbe = mix(flach, tief, k);
	farbe *= 0.92 + 0.16 * (r1.x * 0.6 + r2.x * 0.4);

	// Schaum am Ufer und an schnellen Stellen
	float saum = 1.0 - smoothstep(0.0, 0.3, rand);
	float fleck = smoothstep(0.62, 1.0, r2.x * 0.5 + r1.x * 0.5);
	float schaum_menge = saum * fleck * 0.45;
	farbe = mix(farbe, schaum, schaum_menge);

	ALBEDO = farbe;
	ROUGHNESS = mix(0.04, 0.7, schaum_menge);
	SPECULAR = 0.55;
	METALLIC = 0.0;

	// Himmel im Wasser: stärker bei flachem Blick (Fresnel) und im tiefen Wasser
	float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 3.0);
	vec3 himmel_blau = mix(himmel, vec3(dot(himmel, vec3(0.333))) * vec3(0.8, 1.0, 1.25), 0.45); // Abendrot spiegelt sich im Wasser nur gedämpft
	EMISSION = himmel_blau * (0.12 + 0.55 * fresnel) * spiegelung * (0.65 + 0.35 * k) * (1.0 - schaum_menge);

	// durchsichtig am Ufer und im Flachen, fast deckend in der Tiefe
	float ufer = smoothstep(0.0, 0.14, tiefe);
	ALPHA = clamp((mix(0.7, 0.95, k) + schaum_menge * 0.5) * ufer, 0.0, 1.0);
}
