// Zugtiere (Pferd, Ochse) als gebackene Vertex-Animation (VAT) im MultiMesh (Stadtansicht.Passantenprobe.cs, Arten ab ProbeErsteTierart):
// dasselbe Verfahren wie figur_vat.gdshader (Lage und Normale jeder Ecke je Bild der Clips in zwei Texturen, Spalte = Eckennummer in UV2.x,
// Zeile = Bild), aber ohne Zonen, Palette und Stoffschichten: das Fell, Leder und Holz des Ochsen sind reine Eckenfarben (wie im Skelett-Material
// „Farben“ der Modelle, sRGB). Das Pferd (Modell „Horse“ von henrysteve973, CC BY 4.0) trägt stattdessen eine Albedo-Textur (hat_textur, UV im Netz,
// ein Atlas: Fell, Mähne als Alpha-Maske, Hufeisen, Augen, Geschirr in Farbfeldern); die Mähne wird bei Alpha < 0,5 weggeschnitten (wie MASK im Skelett-Material).
// INSTANCE_CUSTOM.x = Stand in der Schleife (Bilder), y = Clip.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D lage : filter_nearest, repeat_disable;
uniform sampler2D normale : filter_nearest, repeat_disable;
uniform vec2 klips[16];          // erste Zeile und Bildzahl je Clip
uniform bool hat_textur = false;
uniform sampler2D albedo_textur : source_color, filter_linear_mipmap, repeat_disable;

varying vec3 grundfarbe;

void vertex() {
	int ecke = int(UV2.x);
	int klip = int(round(INSTANCE_CUSTOM.y)) % 16;
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
	grundfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		grundfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
}

void fragment() {
	if (hat_textur) {
		vec4 t = texture(albedo_textur, UV);
		ALBEDO = t.rgb;
		ALPHA = t.a;
		ALPHA_SCISSOR_THRESHOLD = 0.5;
	} else {
		ALBEDO = min(grundfarbe, vec3(1.0));
	}
	ROUGHNESS = 0.9;
}
