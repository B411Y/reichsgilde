// Bodennebel-Schwaden (Stadtansicht.Bodennebel.cs): flache, weiche Nebelbänke über Wiesen, in Senken und am Wasser. Ein Netz knapp über dem
// Boden; sein Eckenwert COLOR.r sagt, wie gern hier Nebel liegt (Senke, Wassernähe, in der Stadt weniger). Die Dichte folgt einem langsam
// ziehenden Rauschen in Weltkoordinaten, „staerke“ (0–1) kommt von der Uhrzeit: morgens dicht, bis zum Vormittag aufgerissen. Wo das Netz
// Mauern, Häuser oder den Boden schneidet, blendet es über die Tiefe weich aus (keine harten Linien).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

#include "res://assets/shader/wetter.gdshaderinc"

uniform sampler2D tiefe : hint_depth_texture, repeat_disable, filter_nearest;
uniform vec4 farbe : source_color = vec4(0.8, 0.84, 0.9, 1.0);
uniform float staerke = 0.0;
uniform float versatz = 0.0;   // je Schicht ein anderes Muster

varying float v_senke;
varying vec3 v_welt;

void vertex() {
	v_senke = COLOR.r;
	v_welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 drift = normalize(wind_richtung + vec2(0.0001)) * TIME * (0.25 + 0.5 * wetter_wind);
	vec2 p = v_welt.xz + drift + vec2(versatz * 37.0, versatz * 11.0);
	float n = w_rausch(p * 0.021) * 0.55 + w_rausch(p * 0.06 + 3.7) * 0.3 + w_rausch(p * 0.17 + 9.1) * 0.15;
	float dichte = smoothstep(0.3, 0.9, n + (v_senke - 0.4) * 0.7) * v_senke;   // breitere Schwellen als früher: auch wo die Neigung steil wechselt, bleibt der Rand weich
	// Tiefe der Szene hinter dem Netz: wo Wand oder Boden nahe liegen, weich ausblenden
	float tief = texture(tiefe, SCREEN_UV).x;
	vec4 sicht = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, tief, 1.0);
	float dahinter = (-sicht.z / sicht.w) - (-VERTEX.z);
	dichte *= smoothstep(0.0, 1.4, dahinter);
	ALBEDO = farbe.rgb;
	ALPHA = clamp(dichte * staerke * 0.8, 0.0, 0.85);
}
