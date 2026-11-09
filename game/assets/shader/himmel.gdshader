// Himmel der Ego-Ansicht und der freien Kamera (Stadtansicht.Himmel.cs): ein Verlauf vom dunstigen Horizont zum Zenit, Wolken im Wind, bei Nacht Sterne,
// ein Hof um Sonne und Mond. Gemalt-realistisch: kein Holzschnitt, nur Farbe und Rauschen, keine Textur. Die Spielkamera blickt von oben schräg auf den Boden
// und sieht den Himmel nie – dort bleibt der Hintergrund einfarbig wie bisher (der Himmel wird nur zugeschaltet, wenn eine perspektivische Kamera aktiv ist).
//
// Die Farben kommen aus der Lichtstimmung (Tageszeit, Jahreszeit, Wetter, Stadtansicht.Licht.cs): zenit ist die Himmelsfarbe, horizont die des Dunstrings
// (am Horizont gleich: der Himmel geht ohne Kante in den Dunst über). Beide sind Farben der Anzeige (source_color); der Zenit ist die Himmelsfarbe der Tageszeit
// so dunkel wie der frühere einfarbige Hintergrund, der Horizont die helle Dunstfarbe. Wolken: Deckung aus dem Wetter, Drift mit dem Wind (vom Spiel aufsummiert statt TIME,
// damit die Zeit der Partie gilt und das Bild bei Pause stillsteht). Die Rückstrahlung (Spiegelungen) bleibt wie früher die einfarbige Himmelsfarbe.
shader_type sky;

#include "res://assets/shader/himmel_verlauf.gdshaderinc"

uniform vec3 zenit : source_color = vec3(0.25, 0.42, 0.61);
uniform vec3 horizont : source_color = vec3(0.47, 0.60, 0.72);
uniform vec3 sonne_richtung = vec3(0.0, 1.0, 0.0);   // Richtung zur Lichtquelle (Sonne oder Mond)
uniform vec3 sonne_farbe : source_color = vec3(1.0, 0.95, 0.85);
uniform float sonne_staerke = 0.0;   // 0–1: wie hell die Sonne scheint (nachts 0)
uniform float mond_staerke = 0.0;    // 0–1: Mondlicht
uniform float nacht = 0.0;           // 0 Tag … 1 tiefe Nacht
uniform float deckung = 0.3;         // Bewölkung 0–1
uniform float truebe = 0.0;          // 0 weiße Wolken … 1 graue Regenwolken
uniform float nebel = 0.0;           // 0–1: Dunst bis in den Himmel
global uniform vec2 himmel_drift;    // Verschiebung der Wolken (Wind, aufsummiert; global, damit sich der Stoff nicht ändert)

float h_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float h_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h_hash(i), h_hash(i + vec2(1.0, 0.0)), f.x), mix(h_hash(i + vec2(0.0, 1.0)), h_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Wolkendichte: vier Lagen Rauschen, grob (große Ballen) bis fein (Fransen)
float h_wolken(vec2 p) {
	float s = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		s += a * h_rausch(p);
		p = p * 2.07 + vec2(17.3, 9.1);
		a *= 0.5;
	}
	return s;
}

void sky() {
	if (AT_CUBEMAP_PASS) {
		COLOR = zenit;   // Spiegelungen wie früher: die einfarbige Himmelsfarbe
	} else {
		vec3 d = normalize(EYEDIR);
		float h = d.y;
		float grau_l = dot(horizont, vec3(0.3, 0.55, 0.15));

		// Verlauf: bei Nebel flacher (der Himmel verschwimmt mit dem Dunst)
		vec3 himmel = himmel_verlauf(h, horizont, zenit);
		himmel = mix(himmel, horizont, nebel * 0.85);

		// Sterne in klarer Nacht
		float sterne = 0.0;
		if (nacht > 0.35 && h > 0.04) {
			vec3 p = d * 190.0;
			vec3 ip = floor(p);
			vec3 fp = fract(p) - 0.5;
			float r = h_hash(ip.xy + ip.z * 7.13);
			float punkt = step(0.985, r) * smoothstep(0.30, 0.0, length(fp));
			sterne = punkt * (0.5 + 0.5 * h_hash(ip.zx)) * smoothstep(0.35, 0.8, nacht) * smoothstep(0.04, 0.3, h) * (1.0 - nebel);
		}

		// Sonne und Mond: weicher Hof, dazu eine Scheibe; am Horizont ausgeblendet (dort gilt allein die Farbe des Dunstes)
		float s = max(dot(d, normalize(sonne_richtung)), 0.0);
		float ueber = smoothstep(0.0, 0.14, h);
		float hof = (pow(s, 6.0) * 0.18 + pow(s, 48.0) * 0.35) * ueber;
		float scheibe = smoothstep(0.9990, 0.9997, s) * ueber;
		vec3 licht_f = sonne_farbe;

		// Wolken auf einer Ebene über uns: nahe am Horizont rücken sie zusammen
		float sicht = smoothstep(0.02, 0.2, h);
		float dichte_w = 0.0;
		float helle = 0.5;
		if (sicht > 0.0 && deckung > 0.001) {
			vec2 uv = d.xz / (max(h, 0.0) + 0.17) * 0.9 + himmel_drift;
			float dens = h_wolken(uv);
			float schwelle = mix(0.80, 0.12, clamp(deckung, 0.0, 1.0));
			dichte_w = smoothstep(schwelle, schwelle + 0.20, dens);
			// Licht: was zur Sonne hin dünner wird, leuchtet (Kante zur Sonne hell, abgewandte Seite grau)
			vec2 zur_sonne = normalize(sonne_richtung.xz + vec2(0.0001));
			float dens2 = h_wolken(uv + zur_sonne * 0.07);
			helle = clamp(0.55 + (dens - dens2) * 5.0, 0.0, 1.0);
			dichte_w *= sicht * (1.0 - nebel * 0.7);
		}
		// Farben der Wolken aus dem Dunst (hell, kaum bunt), nachts dunkel; Regenwolken grau und schwerer
		vec3 wolke_hell = vec3(grau_l * 1.9) * mix(vec3(1.0), sonne_farbe * 1.05, 0.6 * sonne_staerke);
		wolke_hell = mix(wolke_hell, horizont * 1.3, 0.25);
		vec3 wolke_dunkel = mix(vec3(grau_l * 0.8) * vec3(0.95, 1.0, 1.1), zenit * 0.7, 0.35);
		wolke_dunkel *= (1.0 - 0.35 * truebe);
		wolke_hell = mix(wolke_hell, wolke_dunkel * 1.7, 0.55 * truebe);
		vec3 wolke = mix(wolke_dunkel, wolke_hell, helle);
		wolke += licht_f * (0.12 * mond_staerke * (1.0 - sonne_staerke));
		// Silberrand an dünnen Stellen im Sonnenlicht
		wolke += licht_f * sonne_staerke * 0.30 * pow(s, 10.0) * (1.0 - dichte_w * 0.5);

		vec3 farbe = himmel;
		farbe += licht_f * hof * (sonne_staerke + mond_staerke * 0.35) * (1.0 - 0.7 * dichte_w) * (1.0 - nebel * 0.5);
		farbe += licht_f * scheibe * (sonne_staerke * 3.0 + mond_staerke * 1.2) * (1.0 - dichte_w);
		farbe += vec3(0.8, 0.85, 1.0) * sterne * 0.9;
		farbe = mix(farbe, wolke, dichte_w);
		COLOR = farbe;
	}
}
