// Probefiguren „Weg A“ als gebackene Vertex-Animation (VAT) im MultiMesh (Stadtansicht.Passantenprobe.cs): ab Zoom 50 statt Skelett.
// Die Lage jeder Ecke (Modellraum, Halbfloat) und ihre Normale stehen je Bild der Clips in zwei Texturen: Spalte = Eckennummer (UV2.x),
// Zeile = Bild. Das Spiel schreibt je Figur in INSTANCE_CUSTOM: x = Stand in der Schleife (Bilder, mit Bruchteil), y = Clip + 16 × Kopffarbe,
// z = Farben (Gewand + 64 × Beinkleid + 4096 × Haut + 262144 × Haar, je ein Platz in „farben“). Zonen und feste Farben wie figur.gdshader.
// Stoffe: UV.y = Art + 16 × (Schicht der Material-Bibliothek + 1) wie bei den Modellen; „tuch“ und „leder“ bekommen die Farbe ihrer Schicht
// aus modelle_farbe_hoehe (dreiseitig im Modellraum, geteilt durch die mittlere Farbe der Schicht) – eine einfache Fassung von oberflaeche.gdshader.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D lage : filter_nearest, repeat_disable;
uniform sampler2D normale : filter_nearest, repeat_disable;
uniform vec2 klips[16];          // erste Zeile und Bildzahl je Clip
uniform vec4 farben[64];        // Palette (linear), vom Spiel gefüllt
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

vec3 farbe_aus(int i, bool srgb) {
	vec3 c = farben[clamp(i, 0, 63)].rgb;
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
	grundfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		grundfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	int f = int(round(INSTANCE_CUSTOM.z));
	bool srgb = OUTPUT_IS_SRGB;
	f_gewand = farbe_aus(f % 64, srgb);
	f_beinkleid = farbe_aus((f / 64) % 64, srgb);
	f_haut = farbe_aus((f / 4096) % 64, srgb);
	f_haar = farbe_aus((f / 262144) % 64, srgb);
	f_kopf = farbe_aus(code / 16, srgb);
}

void fragment() {
	vec3 farbe = grundfarbe;
	if (zone == 1) farbe = f_gewand;
	else if (zone == 2) farbe = f_beinkleid;
	else if (zone == 3) farbe = f_haut;
	else if (zone == 4) farbe = f_haar;
	else if (zone == 5) farbe = f_kopf;
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
