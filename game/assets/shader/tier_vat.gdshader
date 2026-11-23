// Zugtiere (Pferd, Ochse) als gebackene Vertex-Animation (VAT) im MultiMesh (Stadtansicht.Passantenprobe.cs, Arten ab ProbeErsteTierart):
// dasselbe Verfahren wie figur_vat.gdshader (Lage und Normale jeder Ecke je Bild der Clips in zwei Texturen, Spalte = Eckennummer in UV2.x,
// Zeile = Bild), aber ohne Zonen, Palette und Stoffschichten: das Fell, Leder und Holz des Ochsen sind reine Eckenfarben (wie im Skelett-Material
// „Farben“ der Modelle, sRGB). Das Pferd (Modell „Horse“ von henrysteve973, CC BY 4.0) trägt stattdessen eine Albedo-Textur (hat_textur, UV im Netz,
// ein Atlas: Fell, Mähne als Alpha-Maske, Hufeisen, Augen, Geschirr); die Mähne wird bei Alpha < 0,5 weggeschnitten (wie MASK im Skelett-Material).
// Dazu Normal- und ORM-Karte des Pferds (hat_normal, hat_orm; Atlas in halber Auflösung, gleiche UV): Haarstrich im Fell, Narbung, Maserung, Eisen.
// INSTANCE_CUSTOM.x = Stand in der Schleife (Bilder), y = Clip, z = Abzeichen-Code (tier_abzeichen.gdshaderinc; 0 = keine).
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D lage : filter_nearest, repeat_disable;
uniform sampler2D normale : filter_nearest, repeat_disable;
uniform vec2 klips[16];          // erste Zeile und Bildzahl je Clip
uniform bool hat_textur = false;
uniform sampler2D albedo_textur : source_color, filter_linear_mipmap, repeat_disable;
// Normalkarte: Grün = oben im Bild (OpenGL, wie glTF). Die VAT kennt keine Tangenten: Tangentenraum je Pixel aus den Ableitungen von Lage und UV.
uniform bool hat_normal = false;
uniform sampler2D normal_textur : hint_normal, filter_linear_mipmap, repeat_disable;
uniform float normal_staerke = 1.0;
// ORM: R Verdeckung, G Rauheit, B Metall (wie glTF occlusion/metallicRoughness).
uniform bool hat_orm = false;
uniform sampler2D orm_textur : hint_default_white, filter_linear_mipmap, repeat_disable;
#include "res://assets/shader/tier_abzeichen.gdshaderinc"

varying vec3 grundfarbe;
varying flat float abzeichen_code;

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
	abzeichen_code = INSTANCE_CUSTOM.z;
	if (!OUTPUT_IS_SRGB) {
		grundfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
}

void fragment() {
	if (hat_textur) {
		vec4 t = texture(albedo_textur, UV);
		float huf_maske = hat_orm ? texture(orm_textur, UV).a : 0.0;
		ALBEDO = abzeichen(t.rgb, UV, abzeichen_code, huf_maske);
		ALPHA = t.a;
		ALPHA_SCISSOR_THRESHOLD = 0.5;
	} else {
		ALBEDO = min(grundfarbe, vec3(1.0));
	}
	ROUGHNESS = 0.9;
	if (hat_textur && hat_normal) {
		vec3 dp1 = dFdx(VERTEX);
		vec3 dp2 = dFdy(VERTEX);
		vec2 duv1 = dFdx(UV);
		vec2 duv2 = dFdy(UV);
		vec3 n = normalize(NORMAL);
		vec3 dp2s = cross(dp2, n);
		vec3 dp1s = cross(n, dp1);
		vec3 tu = dp2s * duv1.x + dp1s * duv2.x;   // Richtung wachsendes u
		vec3 tv = dp2s * duv1.y + dp1s * duv2.y;   // Richtung wachsendes v (im Bild nach unten)
		float s = inversesqrt(max(max(dot(tu, tu), dot(tv, tv)), 1e-20));
		vec3 tn;
		tn.xy = texture(normal_textur, UV).xy * 2.0 - 1.0;          // nur Rot und Grün (Godot speichert Normalkarten zweikanalig), Blau neu wie in Godots NORMAL_MAP
		tn.z = sqrt(max(0.0, 1.0 - dot(tn.xy, tn.xy)));
		tn.xy *= normal_staerke;
		NORMAL = normalize(tu * s * tn.x - tv * s * tn.y + n * tn.z);
	}
	if (hat_textur && hat_orm) {
		vec3 orm = texture(orm_textur, UV).rgb;
		AO = orm.r;
		ROUGHNESS = orm.g;
		METALLIC = orm.b;
	}
}
