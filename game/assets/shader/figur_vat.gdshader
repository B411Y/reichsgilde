// Probefiguren „Weg A“ als gebackene Vertex-Animation (VAT) im MultiMesh (Stadtansicht.Passantenprobe.cs): ab Zoom 50 statt Skelett.
// Die Lage jeder Ecke (Modellraum, Halbfloat) und ihre Normale stehen je Bild der vier Clips in zwei Texturen: Spalte = Eckennummer (UV2.x),
// Zeile = Bild. Das Spiel schreibt je Figur in INSTANCE_CUSTOM: x = Stand in der Schleife (Bilder, mit Bruchteil), y = Clip + 8 × Kopffarbe,
// z = Farben (Gewand + 64 × Beinkleid + 4096 × Haut + 262144 × Haar, je ein Platz in „farben“). Zonen und feste Farben wie figur.gdshader.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D lage : filter_nearest, repeat_disable;
uniform sampler2D normale : filter_nearest, repeat_disable;
uniform vec2 klips[4];          // erste Zeile und Bildzahl je Clip
uniform vec4 farben[64];        // Palette (linear), vom Spiel gefüllt

varying flat int zone;
varying vec3 grundfarbe;
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
	int klip = code % 8;
	vec2 k = klips[clamp(klip, 0, 3)];
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

	zone = int(round(UV.x));
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
	f_kopf = farbe_aus(code / 8, srgb);
}

void fragment() {
	vec3 farbe = grundfarbe;
	if (zone == 1) farbe = f_gewand;
	else if (zone == 2) farbe = f_beinkleid;
	else if (zone == 3) farbe = f_haut;
	else if (zone == 4) farbe = f_haar;
	else if (zone == 5) farbe = f_kopf;
	ALBEDO = farbe;
	ROUGHNESS = 0.9;
}
