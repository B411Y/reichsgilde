// Fensterlicht (PR #203, Stadtansicht.Modelle.cs): Bei Tag dunkles Glas wie das Material „Fenster“ aus Blender, abends Kerzen- und
// Herdlicht dahinter. Jedes Fenster entscheidet für sich, ob es leuchtet: Seine Kennung steht als Eckenfarbe im Netz (das Spiel
// nummeriert beim Laden die zusammenhängenden Scheiben, FensterNetz), dazu kommen die Lage des Hauses und die Nummer der Nacht.
// Ein Fenster leuchtet, wenn sein Zufallswert unter dem „anteil“ liegt; der Anteil sinkt über den Abend, so gehen die Lichter nach
// und nach aus – jedes weich über einige Sekunden, keines blinkt. Das Flackern rechnet der Shader selbst (TIME), die Uniforms setzt
// das Spiel nur, wenn sich etwas merklich ändert. Ein Material je Gruppe (Wohnen, Werkstatt, Wirtshaus, Wache, Kirche, Messe):
// keine zusätzlichen Zeichenaufrufe und keine Lichtquellen.
// Silhouetten (Stadtansicht.Fenstersilhouetten.cs): Hinter einem leuchtenden Fenster zeichnet der Shader hin und wieder den dunklen
// Schattenriss eines Bewohners – stehend, langsam vorbeigehend oder sitzend. Wer zu Hause ist, setzt das Spiel je Haus als Instanz-
// Uniform (Größe, Schulterbreite, Kopf, Frau); ohne Bewohner oder fern der Kamera ist „person_a“ null und der Shader tut nichts.
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
// Silhouetten: 1 = Zoom nah genug (das Spiel schaltet; sonst sind die Figuren zu klein, um sie zu zeigen)
uniform float figuren_an = 0.0;
// Zwei Bewohner des Hauses, die gerade zu Hause sind: x = Körpergröße (m), y = Schulterbreite (m), z = Kopfradius (m), w = 1 Frau, 0 Mann;
// x = 0: niemand. Je Haus gesetzt (Instanz-Uniform), das Material bleibt für alle Häuser einer Gruppe dasselbe.
instance uniform vec4 person_a = vec4(0.0);
instance uniform vec4 person_b = vec4(0.0);

const float WEICH = 0.025;        // Breite des Übergangs im Anteil: ein Fenster blendet über einige Spielminuten ein oder aus
const float FLACKERN = 0.32;      // ruhiges Flackern: meist um ±10 % …
const float UNRUHIG = 0.5;        // … und in unruhigen Augenblicken um ±15 % (die Summe der Rauschwerte streut um ±0,35)

#include "licht_flackern.gdshaderinc"

varying vec2 haus;
varying vec2 kennung;
varying vec2 scheibe;      // Ort in der Scheibe (0–1)
varying vec2 scheibemass;  // Breite und Höhe der Scheibe in Metern (0: unbekannt)

// Brüstungshöhe: so hoch über dem Fußboden sitzt die Unterkante der Scheibe
const float BRUESTUNG = 0.9;
const float SITZ = 0.72;           // Sitzend reicht der Kopf auf diesen Anteil der Körpergröße
const float DAUER_STEHEN = 9.0;    // Sekunden, die jemand am Fenster steht oder sitzt
const float DAUER_GEHEN = 4.0;     // Sekunden, die er braucht, um am Fenster vorbeizugehen

// Abstand zu einem Ellipsenrand (nur als Weichzeichner brauchbar, nicht metrisch)
float ellipse(vec2 p, vec2 mitte, vec2 halb) {
	vec2 q = (p - mitte) / halb;
	return (length(q) - 1.0) * min(halb.x, halb.y);
}

// Schattenriss einer Person von der Hüfte aufwärts: Kopf, Hals, Schultern, Körper. Rückgabe 0–1 (1 = dunkel).
float person_riss(vec2 p, vec4 person, float kopf_hoehe, float x) {
	float r = person.z;
	vec2 kopf = vec2(x, kopf_hoehe - r);
	float d = ellipse(p, kopf, vec2(r * 0.9, r));                                    // Kopf
	d = min(d, ellipse(p, kopf + vec2(0.0, -r * 1.25), vec2(r * 0.5, r * 0.7)));     // Hals
	float schulter = person.y * 0.5;
	d = min(d, ellipse(p, kopf + vec2(0.0, -r * 2.05), vec2(schulter, r * 0.95)));   // Schultern
	// Körper: unter den Schultern ein Block, der unten ausläuft
	float rumpf = p.y < kopf.y - r * 2.0 ? abs(p.x - x) - schulter * 0.85 : 1.0;
	d = min(d, rumpf);
	if (person.w > 0.5) {
		d = min(d, ellipse(p, kopf + vec2(0.0, r * 0.95), vec2(r * 0.55, r * 0.5)));  // Haarknoten oder Haube
		d = min(d, ellipse(p, kopf + vec2(0.0, -r * 0.3), vec2(r * 1.15, r * 1.25))); // Haar um die Wangen
	}
	return 1.0 - smoothstep(-0.008, 0.012, d);
}

// Ein Besuch am Fenster: wann, was (0 stehen, 1 gehen, 2 sitzen) und wie weit der Auftritt ist (0–1 über die Dauer)
float figur_auftritt(uint fenster, float zeit, out int art, out float fortschritt, out float deckkraft) {
	float takt = 18.0 + 20.0 * f_hash(uvec4(fenster, 11u, 0u, 0u));    // Sekunden zwischen zwei Besuchen
	float t = zeit / takt + f_hash(uvec4(fenster, 12u, 0u, 0u));
	uint runde = uint(floor(t));
	float im_takt = fract(t) * takt;
	float chance = f_hash(uvec4(fenster, runde, 13u, 0u));
	float w = f_hash(uvec4(fenster, runde, 14u, 0u));
	art = w < 0.4 ? 0 : (w < 0.75 ? 1 : 2);
	float dauer = art == 1 ? DAUER_GEHEN : DAUER_STEHEN;
	float start = f_hash(uvec4(fenster, runde, 15u, 0u)) * max(takt - dauer - 1.0, 0.0);
	fortschritt = clamp((im_takt - start) / dauer, 0.0, 1.0);
	float drin = step(start, im_takt) * step(im_takt, start + dauer);
	deckkraft = art == 1 ? 1.0 : smoothstep(0.0, 0.08, fortschritt) * (1.0 - smoothstep(0.9, 1.0, fortschritt));
	return drin * step(chance, 0.55);
}

void vertex() {
	haus = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xz;   // Lage des Hauses: gleiche Modelle leuchten verschieden
	kennung = COLOR.rg;                                     // ohne Eckenfarbe (1, 1): das ganze Modell ist ein Fenster
	scheibe = UV2;
	scheibemass = COLOR.b < 0.999 ? vec2(COLOR.b, COLOR.a) * 2.0 : vec2(0.0);   // Byte 255 = keine Maße (Netze ohne Scheibenmaße)
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
			float licht = hell * kraft * flackern;
			if (figuren_an > 0.5 && hell > 0.5 && scheibemass.x > 0.0) {
				// Je Fenster abwechselnd: einer der Bewohner, die gerade zu Hause sind, steht, geht vorbei oder sitzt
				vec4 person = f_hash(uvec4(fenster, 16u, 0u, 0u)) < 0.5 || person_b.x <= 0.0 ? person_a : person_b;
				int art = 0; float fortschritt = 0.0; float deckkraft = 0.0;
				float da = person.x > 0.0 ? figur_auftritt(fenster, TIME, art, fortschritt, deckkraft) : 0.0;
				if (da > 0.0) {
					vec2 p = scheibe * scheibemass;
					float breite = scheibemass.x;
					float x = breite * (0.3 + 0.4 * f_hash(uvec4(fenster, 17u, 0u, 0u)));
					float hoehe = person.x - BRUESTUNG;
					if (art == 1) {
						x = mix(-person.y * 0.6, breite + person.y * 0.6, fortschritt);
						hoehe += 0.012 * sin(fortschritt * 18.0);                       // leichtes Wippen beim Gehen
					} else if (art == 2) {
						hoehe = person.x * SITZ - BRUESTUNG;
					} else {
						x += 0.015 * sin(TIME * 0.7 + float(fenster & 255u));          // Stehen mit leichtem Schwanken
					}
					licht *= 1.0 - 0.94 * person_riss(p, person, hoehe, x) * deckkraft;
				}
			}
			EMISSION = licht_farbe.rgb * (staerke * helligkeit * licht);
		}
	}
}
