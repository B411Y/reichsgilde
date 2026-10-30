// Laternenscheiben aus Horn (Phase 2 „Feuer und Flammen“, Stadtansicht.Nachtlichter.cs): wie nachtlicht.gdshader (bei Tag die Farbe der Datei,
// nachts glüht die Scheibe mit derselben Flacker-Technik wie das Fensterlicht), aber durchscheinend, damit man die echte Flamme dahinter sieht
// (Feuer.cs, Feuerart Laterne). Nachts lässt die Scheibe mehr durch als bei Tag: Das Horn leuchtet von innen, die Flamme ist ein heller Kern darin.
// Ein Material für alle Hand- und Wandlaternen der Stadt.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;   // die Modelle aus Blender sind doppelseitig

// Das Material bei Tag (aus der Datei)
uniform vec4 farbe : source_color = vec4(0.3, 0.22, 0.12, 1.0);
uniform float rauheit = 0.6;
uniform float kalt = 1.0;
// Kerzenlicht (sRGB) und Emission eines voll brennenden Lichts
uniform vec4 licht_farbe : source_color = vec4(1.0, 0.52, 0.16, 1.0);
uniform float staerke = 3.0;
// 0 bei Tag, 1 in der Nacht (Dämmerung dazwischen)
uniform float helligkeit = 0.0;
uniform float flackern = 0.32;
uniform float tempo = 1.0;
// Deckung der Scheibe bei Tag und nachts (dann sieht man die Flamme dahinter)
uniform float deckung_tag = 0.85;
uniform float deckung_nacht = 0.55;
instance uniform float phase = -1.0;

const float UNRUHE_FAKTOR = 1.55;

#include "licht_flackern.gdshaderinc"

varying flat uint licht;

void vertex() {
	vec3 o = MODEL_MATRIX[3].xyz;
	licht = phase >= 0.0 ? uint(phase * 65535.0) : f_hash_u(uvec4(uvec3(ivec3(round(o * 4.0))), 11u));
}

void fragment() {
	ALBEDO = farbe.rgb * kalt;
	ROUGHNESS = rauheit;
	METALLIC = 0.0;
	ALPHA = mix(deckung_tag, deckung_nacht, helligkeit);
	if (helligkeit > 0.0) {
		float t = TIME * tempo * (0.8 + 0.4 * f_hash(uvec4(licht, 3u, 0u, 0u)));
		float f = 0.55 * f_rausch(t * 1.7, licht) + 0.3 * f_rausch(t * 4.3, licht ^ 0x5bd1e995u) + 0.15 * f_rausch(t * 11.0, licht ^ 0x27d4eb2du);
		float unruhe = smoothstep(0.6, 0.9, f_rausch(t * 0.15, licht ^ 0x165667b1u));
		float flacker = 1.0 + (f - 0.5) * 2.0 * flackern * mix(1.0, UNRUHE_FAKTOR, unruhe);
		float kraft = 0.85 + 0.3 * f_hash(uvec4(licht, 2u, 0u, 0u));
		ALBEDO = mix(farbe.rgb * kalt, licht_farbe.rgb * 0.25, helligkeit);
		// Durchscheinend: das Leuchten gilt für die ganze Scheibe, auch wo sie nur teilweise deckt
		EMISSION = licht_farbe.rgb * (staerke * helligkeit * kraft * flacker) / max(ALPHA, 0.3);
	}
}
