// Charakter Phase 4: Haarkarten für Frisur und Bart (eigener Code, MIT). Netz und Bild: tools/charakter/kopf4_haar.py.
// Lage: Die Karten liegen in der Grundlage (Mann, 25 Jahre); jede Ecke folgt vier Stützpunkten der Haut
// (CUSTOM0 = Nummern, CUSTOM1 = Gewichte). Deren Versatz für Alter, Geschlecht, Gene und Mimik rechnet charakter_haar.gd
// und legt ihn in stuetz_tex (ein Bildpunkt je Stützpunkt, RGB = Versatz in Metern).
// Bild (haar_karten.png): R = Zufall des Haars, G = Helligkeit, B = Lage im Haar (0 Wurzel, 1 Spitze), A = Deckung.
// Ecke: UV2 = (Lage entlang der Karte, Zufall der Karte), COLOR = (Rang beim Ausfallen, Zufall, Lage 0 innen/1 außen, –).
// Licht wie die TressFX-Strähnen (haar4_straehne.gdshader: Kajiya-Diffus und zwei verschobene Marschner-Glanzbänder entlang
// der Haarrichtung = TANGENT), damit Karten und Strähnen gleich hell wirken; Kanten über Alpha-to-Coverage (MSAA),
// sonst Alpha-Schere.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform sampler2D bild : hint_default_transparent, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D stuetz_tex : filter_nearest, repeat_disable;
uniform vec3 farbe : source_color = vec3(0.25, 0.16, 0.09);
uniform vec3 farbe_grau : source_color = vec3(0.8, 0.79, 0.77);
uniform float grau = 0.0;          // Anteil grauer Haare
uniform float kahl = 0.0;          // Karten mit Rang < kahl fallen weg (Glatze)
uniform float duenn = 0.0;         // Anteil ausgedünnter Haare (Alter)
uniform float laenge = 1.0;        // Bart: Haare enden bei Lage < laenge (Bartwuchs)
uniform float dichte = 1.0;        // Bart: Anteil sichtbarer Haare
uniform float schwelle = 0.42;
uniform float k_diffuse = 0.32;
uniform float k_spec1 = 0.03;
uniform float spec_exp1 = 60.0;
uniform float k_spec2 = 0.1;
uniform float spec_exp2 = 14.0;
uniform float cone_angle_deg = 10.0;
uniform float innen_dunkel = 0.62; // innere Lage dunkler (Eigenschatten der Frisur)
uniform float wurzel_dunkel = 0.55;

varying vec3 strang;
varying vec4 info;
varying float kartenzufall;
varying float lage_karte;

void vertex() {
	int w = textureSize(stuetz_tex, 0).x;
	vec3 v = vec3(0.0);
	for (int k = 0; k < 4; k++) {
		int i = int(CUSTOM0[k] + 0.5);
		v += CUSTOM1[k] * texelFetch(stuetz_tex, ivec2(i % w, i / w), 0).xyz;
	}
	VERTEX += v;
	strang = normalize((MODELVIEW_MATRIX * vec4(TANGENT, 0.0)).xyz);
	info = COLOR;
	kartenzufall = UV2.y;
	lage_karte = UV2.x;
	// ganze Karte weg, wenn der Rang unter der Glatzenstufe liegt (weiches Ausdünnen über den Zufall)
	float bleibt = step(kahl, info.r + 0.04 * (info.g - 0.5));
	if (bleibt < 0.5) {
		VERTEX = vec3(0.0);
	}
}

void fragment() {
	vec4 t = texture(bild, UV);
	t.a *= smoothstep(0.0, 0.06, lage_karte);          // weiche Wurzel
	vec2 px = UV * vec2(textureSize(bild, 0));
	float lod = max(0.0, 0.5 * log2(max(dot(dFdx(px), dFdx(px)), dot(dFdy(px), dFdy(px)))));
	float a = clamp(t.a * (1.0 + 0.3 * lod), 0.0, 1.0);
	// je Haar: Zufall aus R und der Karte
	float z = fract(t.r * 7.13 + kartenzufall * 3.71);
	a *= step(duenn, fract(z * 3.3 + 0.17));
	a *= step(1.0 - dichte, fract(z * 5.1 + 0.41));
	// Bart kürzer: Haar endet nach seiner Länge (je Haar etwas anders)
	a *= 1.0 - smoothstep(laenge * (0.75 + 0.35 * z) - 0.04, laenge * (0.75 + 0.35 * z), lage_karte);
	vec3 c = farbe * (0.78 + 0.44 * t.g);
	c = mix(c, farbe_grau * (0.88 + 0.24 * t.g), step(fract(z * 2.37 + 0.11), grau));
	// Ansatz dunkler, Spitzen heller; innere Lage dunkler
	// graues Haar ist am Ansatz und innen kaum dunkler (sonst wirkt ein weißer Haarkranz wie ein dunkler Fleck)
	c *= mix(mix(wurzel_dunkel, 0.9, grau), 1.0, smoothstep(0.0, 0.35, lage_karte)) * mix(1.0, 1.12, smoothstep(0.6, 1.0, t.b));
	c *= mix(mix(innen_dunkel, 0.9, grau), 1.0, info.b);
	ALBEDO = c;
	ALPHA = a;
	ALPHA_SCISSOR_THRESHOLD = schwelle;
	ALPHA_ANTIALIASING_EDGE = schwelle;
	ALPHA_TEXTURE_COORDINATE = px;
	ROUGHNESS = 0.7;
	SPECULAR = 0.0;
	BACKLIGHT = c * 0.25;
	AO = mix(0.55, 1.0, smoothstep(0.0, 0.5, lage_karte)) * mix(0.75, 1.0, info.b);
	AO_LIGHT_AFFECT = 0.4;
}

void light() {
	vec3 T = normalize(strang);
	float cos_tl = dot(T, LIGHT);
	float sin_tl = sqrt(max(0.0, 1.0 - cos_tl * cos_tl));
	float cos_trl = -cos_tl;
	float sin_trl = sin_tl;
	float cos_te = dot(T, VIEW);
	float sin_te = sqrt(max(0.0, 1.0 - cos_te * cos_te));
	float a = radians(cone_angle_deg);
	float cos_root = cos_trl * cos(2.0 * a) - sin_trl * sin(2.0 * a);
	float sin_root = sqrt(max(0.0, 1.0 - cos_root * cos_root));
	float spec_root = max(0.0, cos_root * cos_te + sin_root * sin_te);
	float cos_tip = cos_trl * cos(-3.0 * a) - sin_trl * sin(-3.0 * a);
	float sin_tip = sqrt(max(0.0, 1.0 - cos_tip * cos_tip));
	float spec_tip = max(0.0, cos_tip * cos_te + sin_tip * sin_te);
	// Karten sind Flächen: die vom Licht abgewandte Seite der Frisur etwas dunkler
	float zum_licht = clamp(dot(NORMAL, LIGHT) * 0.5 + 0.5, 0.0, 1.0);
	vec3 light_col = LIGHT_COLOR * ATTENUATION / PI;
	DIFFUSE_LIGHT += (k_diffuse * sin_tl * mix(0.45, 1.1, zum_licht) + k_spec2 * pow(spec_tip, spec_exp2)) * light_col;
	SPECULAR_LIGHT += k_spec1 * pow(spec_root, spec_exp1) * light_col * zum_licht;
}
