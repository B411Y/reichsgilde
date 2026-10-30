// Bodennebel (Stadtansicht.Bodennebel.cs): flache, weiche Nebelfetzen über Wasser, Wiesen und in Senken. Ein Netz knapp über dem Boden;
// seine Eckenwerte sagen, wie gern hier Nebel liegt: COLOR.r an Land (Senke, Wiese; in der Stadt weniger), COLOR.g über und neben dem
// Wasser (über breitem Wasser 1, an einem Bach nur etwa ein Drittel, zum Ufer hin über 20–30 m weich auslaufend).
// Die Form kommt aus einem gefalteten Rauschen in Weltkoordinaten (die Oktaven gegeneinander gedreht, damit keine Gitterrichtung durchscheint),
// das mit dem Wind zieht: Fetzen mit Lücken statt Bändern. „staerke_land“ und „staerke_wasser“ (0–1) sagen, wie viel davon liegt – beim
// Auflösen steigt die Schwelle, die Fetzen schrumpfen von den Rändern her und reißen auf, statt gleichmäßig zu verblassen.
// Auch bei voller Stärke höchstens 62 % (über Wasser) bzw. 40 % (an Land) deckend: Aus Spielhöhe sieht man den Fluss durch den Nebel.
// Farbe: Eigenleuchten in der Farbe der Stunde (vom Skript, nachts so dunkel wie eine unbeleuchtete Wand). Dazu Licht (light()): der Mond
// nur schwach und nur, wo er hinkommt (in den Schatten der Häuser bleibt der Nebel dunkel – kein heller Saum am Fuß der Mauern), Laternen
// und Feuer mit einem leichten Lichthof; die Sonne nicht (ihr Anteil steckt in der Farbe). Wo das Netz Mauern, Häuser oder den Boden
// schneidet, blendet es über die Tiefe weich aus.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, fog_disabled, ambient_light_disabled;

#include "res://assets/shader/wetter.gdshaderinc"

uniform sampler2D tiefe : hint_depth_texture, repeat_disable, filter_nearest;
uniform vec4 farbe : source_color = vec4(0.8, 0.84, 0.9, 1.0);
uniform float staerke_land = 0.0;
uniform float staerke_wasser = 0.0;
uniform float versatz = 0.0;     // je Schicht ein anderes Muster
uniform float deckkraft = 1.0;   // Anteil dieser Schicht (bei zwei Schichten zusammen etwa so dicht wie eine)
uniform float mond = 0.0;        // wie stark Mond (und Blitz) den Nebel aufhellen; tags 0
uniform float hof = 0.0;         // Lichthof um Laternen, Fackeln und Feuer (echte Lichter nahe der Kamera)

varying float v_land;
varying float v_wasser;
varying vec3 v_welt;

// Die Oktaven werden je um etwa 37° gedreht: Wertrauschen hat sonst eine Vorzugsrichtung entlang der Achsen.
const mat2 DREHUNG = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));

float schichten2(vec2 p) {
	return w_rausch(p) * 0.66 + w_rausch(DREHUNG * p * 2.03 + 11.7) * 0.34;
}

float schichten4(vec2 p) {
	float summe = 0.0, gewicht = 0.5;
	for (int i = 0; i < 4; i++) {
		summe += w_rausch(p) * gewicht;
		p = DREHUNG * p * 2.03 + 17.1;
		gewicht *= 0.5;
	}
	return summe / 0.9375;
}

void vertex() {
	v_land = COLOR.r;
	v_wasser = COLOR.g;
	v_welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// Zug mit dem Wind (0,3 bis 0,9 m/s) und ein langsames Umformen
	vec2 wind = normalize(wind_richtung + vec2(0.0001));
	float zeit = TIME;
	vec2 p = v_welt.xz - wind * zeit * (0.3 + 0.6 * wetter_wind) + vec2(versatz * 37.0, versatz * 11.0);
	// Falten: ein großes Rauschen verschiebt das Feld, so werden aus runden Flecken gewundene, ausgefranste Fetzen
	vec2 falte = vec2(schichten2(p * 0.007 + vec2(zeit * 0.006, 0.0)), schichten2(p * 0.007 + vec2(5.2, 1.3 - zeit * 0.005))) - 0.5;
	vec2 q = p + falte * 90.0;
	float n = schichten4(q * 0.022);                        // Fetzen von etwa 25–60 m
	n += (w_rausch(DREHUNG * q * 0.13 + 3.1) - 0.5) * 0.16; // gefranster Rand
	n = clamp((n - 0.5) * 2.3 + 0.5, 0.0, 1.0);             // das Rauschen liegt sonst fast nur zwischen 0,3 und 0,7

	float land = v_land * staerke_land;
	float wasser = v_wasser * staerke_wasser;
	float s = max(land, wasser);
	// Je stärker, desto tiefer die Schwelle und desto mehr Fläche bedeckt (bei 1 etwa drei Viertel, bei 0,3 einzelne Fetzen)
	float schwelle = mix(0.95, 0.3, s);
	float dichte = smoothstep(schwelle - 0.06, schwelle + 0.32, n);
	float anteil_wasser = wasser / max(land + wasser, 0.0001);
	float hoechst = mix(0.40, 0.62, anteil_wasser) * (0.45 + 0.55 * s);

	// Tiefe der Szene hinter dem Netz: wo Wand oder Boden nahe liegen, weich ausblenden
	float tief = texture(tiefe, SCREEN_UV).x;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
	vec3 ndc = vec3(SCREEN_UV, tief) * 2.0 - 1.0; // OpenGL: die Tiefe 0 … 1 steht für −1 … 1 (sonst blendete der Nebel überall aus)
#else
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, tief);
#endif
	vec4 sicht = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	float dahinter = (-sicht.z / sicht.w) - (-VERTEX.z);
	dichte *= smoothstep(0.0, 1.5, dahinter);

	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz); // das Netz hat keine Normalen; light() braucht keine, Godot rechnet aber mit ihr
	ALBEDO = vec3(1.0);
	EMISSION = farbe.rgb;
	ALPHA = clamp(dichte * hoechst * deckkraft, 0.0, 0.7);
}

void light() {
	// Nebel streut in alle Richtungen: kein Winkel zur Fläche, nur Stärke und (beim Mond) Schatten
	if (LIGHT_IS_DIRECTIONAL) {
		DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * (mond / PI);
	} else {
		DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * (hof / PI);
	}
}
