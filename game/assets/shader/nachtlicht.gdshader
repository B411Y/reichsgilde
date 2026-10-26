// Nachtlichter (PR #207, Stadtansicht.Nachtlichter.cs): Flamme und Laternenlicht aus lichter.glb. Bei Tag das Material der Datei (kaltes
// Pech, trübes Horn), nachts glüht es mit derselben Flacker-Technik wie das Fensterlicht (fenster.gdshader): drei Rauschfrequenzen
// übereinander, je Licht eigener Takt, ab und zu unruhig (Luftzug). Offenes Feuer (Fackel, Feuerkorb) flackert stärker, schneller und rötlicher
// als das Licht hinter dem Horn einer Laterne. Ein Material je Art für die ganze Stadt: keine zusätzlichen Zeichenaufrufe, keine Lichtquellen
// (die wenigen echten Lichter nahe der Kamera setzt das Spiel selbst).
shader_type spatial;
render_mode cull_disabled;   // die Modelle aus Blender sind doppelseitig

// Das Material bei Tag (aus der Datei)
uniform vec4 farbe : source_color = vec4(0.3, 0.22, 0.12, 1.0);
uniform float rauheit = 0.6;
// So viel der Farbe bleibt bei Tag: ein Feuerkorb ohne Feuer ist kalte Glut, kein orangefarbener Topf
uniform float kalt = 1.0;
// Flammen- bzw. Kerzenlicht (sRGB) und Emission eines voll brennenden Lichts
uniform vec4 licht_farbe : source_color = vec4(1.0, 0.5, 0.15, 1.0);
uniform float staerke = 3.0;
// 0 bei Tag, 1 in der Nacht (Dämmerung dazwischen)
uniform float helligkeit = 0.0;
// Wie stark das Licht flackert (wie FLACKERN im Fensterlicht) und wie schnell
uniform float flackern = 0.32;
uniform float tempo = 1.0;
// Eigener Startwert eines einzelnen Lichts (0–1); ohne (−1) kommt er aus der Lage des Lichts in der Welt (feste Lichter, auch gebündelt)
instance uniform float phase = -1.0;

const float UNRUHE_FAKTOR = 1.55;   // in unruhigen Augenblicken so viel stärker

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
	if (helligkeit > 0.0) {
		float t = TIME * tempo * (0.8 + 0.4 * f_hash(uvec4(licht, 3u, 0u, 0u)));
		float f = 0.55 * f_rausch(t * 1.7, licht) + 0.3 * f_rausch(t * 4.3, licht ^ 0x5bd1e995u) + 0.15 * f_rausch(t * 11.0, licht ^ 0x27d4eb2du);
		float unruhe = smoothstep(0.6, 0.9, f_rausch(t * 0.15, licht ^ 0x165667b1u));
		float flacker = 1.0 + (f - 0.5) * 2.0 * flackern * mix(1.0, UNRUHE_FAKTOR, unruhe);
		float kraft = 0.85 + 0.3 * f_hash(uvec4(licht, 2u, 0u, 0u));
		ALBEDO = mix(farbe.rgb * kalt, licht_farbe.rgb * 0.25, helligkeit);
		EMISSION = licht_farbe.rgb * (staerke * helligkeit * kraft * flacker);
	}
}
