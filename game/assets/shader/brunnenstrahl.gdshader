// Wasserstrahlen des Marktbrunnens (vier Röhren und der Überlauf zum Trog): ein Netz aus dünnen Röhren („Brunnen_Strahl“, Material „Brunnenstrahl“, tools/modelle/brunnen.py),
// halb durchsichtig, mit hellen Streifen, die an den Strahlen entlang nach unten laufen. Die Höhe nimmt entlang jedes Strahls ab, darum genügt sie als Laufrichtung
// (keine UV nötig); ein Stoff für alle Strahlen. Die Stadtansicht belegt es (Stadtansicht.Brunnen.cs), schaltet das Netz im Winter und ab etwa 60 m Zoom aus und
// lässt es keinen Schatten werfen.
//
// stufe wie der Boden: 0 Niedrig = ruhiger, gleichmäßiger Strahl, 1 und 2 = laufende Streifen und leichtes Flimmern quer dazu.
shader_type spatial;
render_mode cull_disabled, depth_draw_never;

#include "res://assets/shader/wetter.gdshaderinc"

uniform vec3 farbe : source_color = vec3(0.62, 0.74, 0.78);
uniform int stufe : hint_range(0, 2) = 2;
uniform float tempo : hint_range(0.0, 6.0) = 2.4;

varying vec3 lok;

void vertex() {
	lok = VERTEX;
}

void fragment() {
	float streifen = 0.5;
	if (stufe >= 1) {
		float quer = sin((lok.x + lok.z) * 55.0 + lok.y * 9.0);
		streifen = 0.5 + 0.5 * sin(lok.y * 22.0 + TIME * tempo * 6.2831 * 0.5 + quer * 1.2);
	}
	float fresnel = pow(1.0 - clamp(dot(NORMAL, normalize(VIEW)), 0.0, 1.0), 2.0);
	ALBEDO = farbe;
	ROUGHNESS = 0.05;
	ALPHA = clamp(mix(0.35, 0.75, streifen) + 0.2 * fresnel, 0.0, 0.9);
	EMISSION = wetter_himmel * (0.12 + 0.35 * fresnel) + farbe * 0.12 * streifen;
}
