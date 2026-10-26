// Fensterlicht (PR #203, Stadtansicht.Modelle.cs): Bei Tag dunkles Glas wie das Material „Fenster“ aus Blender, abends Kerzen- und
// Herdlicht dahinter. Jedes Fenster entscheidet für sich, ob es leuchtet: Seine Kennung steht als Eckenfarbe im Netz (das Spiel
// nummeriert beim Laden die zusammenhängenden Scheiben, FensterNetz), dazu kommen die Lage des Hauses und die Nummer der Nacht.
// Ein Fenster leuchtet, wenn sein Zufallswert unter dem „anteil“ liegt; der Anteil sinkt über den Abend, so gehen die Lichter nach
// und nach aus – jedes weich über einige Sekunden, keines blinkt. Das Flackern rechnet der Shader selbst (TIME), die Uniforms setzt
// das Spiel nur, wenn sich etwas merklich ändert. Ein Material je Gruppe (Wohnen, Werkstatt, Wirtshaus, Wache, Kirche, Messe):
// keine zusätzlichen Zeichenaufrufe und keine Lichtquellen.
shader_type spatial;
render_mode cull_disabled;   // die Modelle aus Blender sind doppelseitig

// Das Glas bei Tag (aus dem Material „Fenster“ der Datei)
uniform vec4 farbe : source_color = vec4(0.09, 0.11, 0.14, 1.0);
uniform float rauheit = 0.2;
// Kerzen- und Herdlicht (sRGB) und die Emission eines voll leuchtenden Fensters
uniform vec4 licht_farbe : source_color = vec4(1.0, 0.5, 0.15, 1.0);
uniform float staerke = 2.6;
// Helligkeit der Gruppe: 0 bei Tag, 1 in der Nacht (Dämmerung dazwischen); Kirchen nur ein schwacher Schein, zur Messe mehr
uniform float helligkeit = 0.0;
// So viele Fenster leuchten gerade (0–1; über 1: alle)
uniform float anteil = 0.0;
// Nummer der Nacht: jede Nacht sind andere Fenster hell
uniform float nacht = 0.0;

const float WEICH = 0.025;        // Breite des Übergangs im Anteil: ein Fenster blendet über einige Spielminuten ein oder aus
const float FLACKERN = 0.32;      // ruhiges Flackern: meist um ±10 % …
const float UNRUHIG = 0.5;        // … und in unruhigen Augenblicken um ±15 % (die Summe der Rauschwerte streut um ±0,35)

#include "licht_flackern.gdshaderinc"

varying vec2 haus;
varying vec2 kennung;

void vertex() {
	haus = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xz;   // Lage des Hauses: gleiche Modelle leuchten verschieden
	kennung = COLOR.rg;                                     // ohne Eckenfarbe (1, 1): das ganze Modell ist ein Fenster
}

void fragment() {
	ALBEDO = farbe.rgb;
	ROUGHNESS = rauheit;
	METALLIC = 0.0;
	if (helligkeit > 0.0) {
		uvec2 k = uvec2(round(kennung * 255.0));
		uvec2 h = uvec2(ivec2(round(haus * 4.0)));
		uint fenster = f_hash_u(uvec4(k, h));
		float an = f_hash(uvec4(fenster, uint(nacht), 1u, 0u));
		float hell = smoothstep(an - WEICH, an + WEICH, anteil);
		if (hell > 0.0) {
			// Mal hellere, mal schwächere Lichter (eine Kerze oder das Herdfeuer)
			float kraft = 0.7 + 0.5 * f_hash(uvec4(fenster, 2u, 0u, 0u));
			// Flackern: drei Frequenzen übereinander, je Fenster eigener Takt; ab und zu wird es unruhig (Luftzug)
			float t = TIME * (0.8 + 0.4 * f_hash(uvec4(fenster, 3u, 0u, 0u)));
			float f = 0.55 * f_rausch(t * 1.7, fenster) + 0.3 * f_rausch(t * 4.3, fenster ^ 0x5bd1e995u) + 0.15 * f_rausch(t * 11.0, fenster ^ 0x27d4eb2du);
			float unruhe = smoothstep(0.6, 0.9, f_rausch(t * 0.15, fenster ^ 0x165667b1u));
			float flackern = 1.0 + (f - 0.5) * 2.0 * mix(FLACKERN, UNRUHIG, unruhe);
			EMISSION = licht_farbe.rgb * (staerke * helligkeit * hell * kraft * flackern);
		}
	}
}
