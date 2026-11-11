// Probefiguren „Weg A“ als gebackene Vertex-Animation (VAT) im MultiMesh (Stadtansicht.Passantenprobe.cs): ab Zoom 50 statt Skelett.
// Die Lage jeder Ecke (Modellraum, Halbfloat) und ihre Normale stehen je Bild der Clips in zwei Texturen: Spalte = Eckennummer (UV2.x),
// Zeile = Bild. Das Spiel schreibt je Figur in INSTANCE_CUSTOM: x = Stand in der Schleife (Bilder, mit Bruchteil), y = Clip + 16 × Kopffarbe,
// z = Gewand + 128 × Beinkleid + 16384 × Haut, w = Haar + 128 × Bart (je ein Platz in „farben“, 128 Plätze; 09.11.: mit 64 Plätzen lief die
// Palette über und Hauben bekamen Platz 0). Zonen und feste Farben wie figur.gdshader.
// Stoffe: UV.y = Art + 16 × (Schicht der Material-Bibliothek + 1) wie bei den Modellen; „tuch“ und „leder“ bekommen die Farbe ihrer Schicht
// aus modelle_farbe_hoehe (dreiseitig im Modellraum, geteilt durch die mittlere Farbe der Schicht) – eine einfache Fassung von oberflaeche.gdshader.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D lage : filter_nearest, repeat_disable;
uniform sampler2D normale : filter_nearest, repeat_disable;
uniform vec2 klips[16];          // erste Zeile und Bildzahl je Clip
uniform bool bartnetz = false;      // Bartnetz der Zwerge: Alpha der Eckenfarbe = Abstand von der Oberlippe (Stadtansicht.Passantenprobe.cs)
uniform vec4 farben[128];        // Palette (linear), vom Spiel gefüllt
uniform sampler2DArray bib_farbe : source_color, repeat_enable, filter_linear_mipmap;
uniform int stoff_schicht[2];   // Schicht von „tuch“ und „leder“ (−1 = keine)
uniform float stoff_kachel[2];  // Meter je Kachel
uniform vec3 stoff_mittel[2];   // mittlere Farbe der Schicht (linear)

varying flat int zone;
varying flat int stoff_nr;
varying vec3 grundfarbe;
varying vec3 ort;
varying vec3 ort_n;
varying flat vec3 f_gewand;
varying flat vec3 f_beinkleid;
varying flat vec3 f_haut;
varying flat vec3 f_haar;
varying flat vec3 f_kopf;
varying flat float f_bart;
varying float bart_abstand;

// Bartnetz (Zwerge): Alpha der Eckenfarbe = Abstand von der Oberlippe in Vierteln eines Meters. Daraus: weicher Übergang zur Haut oben (kein
// harter Rand), Mundfuge zwischen Schnurrbart und Vollbart, Strähnen, im Ansatz dunkler und zur Spitze heller.
vec3 bart_tonen(vec3 farbe, vec3 hautfarbe, float abstand, vec3 ort_m) {
	float m = abstand * 0.25; // Meter unter der Oberkante
	float strahne = 0.5 + 0.5 * sin(ort_m.x * 260.0 + ort_m.y * 40.0 + sin(ort_m.x * 90.0) * 2.0);
	vec3 c = farbe * mix(0.78, 1.12, smoothstep(0.0, 0.18, m)) * (0.86 + 0.28 * strahne);
	// Oberkante und Wangen: der Bart geht über einen Stoppelton in die Gesichtshaut über (kein harter Rand)
	float rand = max(1.0 - smoothstep(0.0, 0.050, m), smoothstep(0.040, 0.085, abs(ort_m.x)) * (1.0 - smoothstep(0.02, 0.10, m)));
	c = mix(c, mix(hautfarbe, farbe * 0.8, 0.45), rand * 0.85);
	c *= 1.0 - 0.55 * (smoothstep(0.022, 0.030, m) * (1.0 - smoothstep(0.036, 0.046, m))); // Mundfuge: Schnurrbart darüber, Vollbart darunter
	return c;
}

vec3 farbe_aus(int i, bool srgb) {
	vec3 c = farben[clamp(i, 0, 127)].rgb;
	if (srgb) {
		c = mix(1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, c * 12.92, lessThan(c, vec3(0.0031308)));
	}
	return c;
}

void vertex() {
	int ecke = int(UV2.x);
	int code = int(round(INSTANCE_CUSTOM.y));
	int klip = code % 16;
	vec2 k = klips[clamp(klip, 0, 15)];
	int start = int(k.x);
	int bilder = max(int(k.y), 1);
	float bild = INSTANCE_CUSTOM.x;
	int b0 = int(floor(bild)) % bilder;
	int b1 = (b0 + 1) % bilder;
	float t = fract(bild);
	vec3 p0 = texelFetch(lage, ivec2(ecke, start + b0), 0).xyz;
	vec3 p1 = texelFetch(lage, ivec2(ecke, start + b1), 0).xyz;
	vec3 n0 = texelFetch(normale, ivec2(ecke, start + b0), 0).xyz * 2.0 - 1.0;
	vec3 n1 = texelFetch(normale, ivec2(ecke, start + b1), 0).xyz * 2.0 - 1.0;
	VERTEX = mix(p0, p1, t);
	NORMAL = normalize(mix(n0, n1, t));
	ort = VERTEX;
	ort_n = NORMAL;

	zone = int(round(UV.x));
	int schicht = int(round(UV.y)) / 16 - 1;
	stoff_nr = -1;
	if (schicht >= 0 && schicht == stoff_schicht[0]) stoff_nr = 0;
	else if (schicht >= 0 && schicht == stoff_schicht[1]) stoff_nr = 1;
	bart_abstand = COLOR.a;
	grundfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		grundfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	int f = int(round(INSTANCE_CUSTOM.z));
	bool srgb = OUTPUT_IS_SRGB;
	int h = int(round(INSTANCE_CUSTOM.w));
	f_gewand = farbe_aus(f % 128, srgb);
	f_beinkleid = farbe_aus((f / 128) % 128, srgb);
	f_haut = farbe_aus((f / 16384) % 128, srgb);
	f_haar = farbe_aus(h % 128, srgb);
	f_kopf = farbe_aus((code / 16) % 128, srgb);
	f_bart = float(h / 128); // Bart an (Zone 6 in Haarfarbe) – sonst Haut
}

void fragment() {
	vec3 farbe = grundfarbe;
	if (zone == 1) farbe = f_gewand;
	else if (zone == 2) farbe = f_beinkleid;
	else if (zone == 3) farbe = f_haut;
	else if (zone == 4) farbe = f_haar;
	else if (zone == 5) farbe = f_kopf;
	else if (zone == 6) farbe = f_bart > 0.5 ? mix(f_haut, f_haar, 0.7) : f_haut;
	if (bartnetz && zone == 4) farbe = bart_tonen(farbe, f_haut, bart_abstand, ort);
	if (stoff_nr >= 0) {
		// dreiseitig im Modellraum: Stoffbild je Achse, gewichtet nach der Normalen
		float s = 1.0 / stoff_kachel[stoff_nr];
		vec3 w = pow(abs(normalize(ort_n)), vec3(4.0));
		w /= (w.x + w.y + w.z + 1e-5);
		float lage_schicht = float(stoff_schicht[stoff_nr]);
		vec3 tx = texture(bib_farbe, vec3(ort.zy * s, lage_schicht)).rgb;
		vec3 ty = texture(bib_farbe, vec3(ort.xz * s, lage_schicht)).rgb;
		vec3 tz = texture(bib_farbe, vec3(ort.xy * s, lage_schicht)).rgb;
		vec3 muster = (tx * w.x + ty * w.y + tz * w.z) / max(stoff_mittel[stoff_nr], vec3(0.01));
		farbe *= clamp(muster, vec3(0.4), vec3(1.8));
	}
	// nie heller als weiß: das Glühen des Pixel-Looks hält Bildpunkte, die heller sind als ihr Licht, für Leuchtendes
	ALBEDO = min(farbe, vec3(1.0));
	ROUGHNESS = 0.9;
}
