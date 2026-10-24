// Gemeinsame Rechnungen von Boden (boden.gdshaderinc) und Grasteppich (gras.gdshader, Schritt 5i): Rauschen und die Makro-Variation der Farbe.
// Der Grasteppich muss genau dieselben Flecken zeigen wie die Wiese unter ihm, darum steht beides nur hier.

// Eine feste Zahl 0 … 1 je ganzzahligem Punkt. Mit ganzen Zahlen gerechnet: Ein Hash aus fract() reagiert so stark auf Rundung, dass
// derselbe Gitterpunkt in zwei Nachbarzellen verschieden ausfallen kann – dann sieht man im Rauschen gerade Nähte.
// Dieselbe Rechnung steht in Stadtansicht.Bueschel.cs (Hash).
float hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p))) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	return float(n >> 8u) * (1.0 / 16777216.0);
}

float rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Makro-Variation: große Flecken heller und dunkler (90 und 27 m), auf der Wiese (Anteil wiese) trockene, gelbliche Stellen – von weit oben
// keine Kacheln. stark (Boden Hoch, Schritt 5i): kräftiger und auf der Wiese dazu ein mittlerer Maßstab (etwa 9 und 3,5 m) mit satten,
// dunklen und mageren, hellen Stellen, damit die Wiese auch aus Spielhöhe lebendig wirkt.
vec3 makro_farbe(vec3 c, vec2 welt, float wiese, bool stark) {
	float makro = rausch(welt * 0.011) * 0.62 + rausch(welt * 0.037 + 7.3) * 0.38;
	float trocken = smoothstep(0.45, 0.8, rausch(welt * 0.021 + 11.7));
	if (!stark) {
		c *= mix(0.86, 1.1, makro);
		return mix(c, c * vec3(1.1, 1.03, 0.8), trocken * wiese * 0.7);
	}
	c *= mix(0.78, 1.15, makro);
	if (wiese > 0.0) {
		float mitte = rausch(welt * 0.11 + 3.1) * 0.65 + rausch(welt * 0.29 + 5.7) * 0.35;
		vec3 satt = vec3(0.78, 0.86, 0.74);
		vec3 mager = vec3(1.13, 1.1, 0.9);
		c = mix(c, c * mix(satt, mager, smoothstep(0.22, 0.78, mitte)), wiese);
	}
	return mix(c, c * vec3(1.14, 1.04, 0.74), trocken * wiese * 0.85);
}

// Horste des Grasteppichs (gras.gdshader, Schritt 5i): 0 kurzes, lichtes Gras … 1 dichtes, hohes (Flecken von etwa 3 m). Der Boden darunter wird
// dort dunkler, wo der Teppich dicht steht (die Halme werfen Schatten aufeinander und auf den Grund).
float gras_horst(vec2 welt) {
	return smoothstep(0.25, 0.75, rausch(welt * 0.33 + 4.7));
}
