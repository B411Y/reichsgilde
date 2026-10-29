// Flamme, Technik A (Phase 1 „Feuer und Flammen“, Feuer.cs): eine einzige senkrechte Fläche, die der Kamera folgt (Billboard um die Hochachse)
// oder – zwei Flächen im rechten Winkel, „gekreuzt“ – fest steht. Alles Übrige rechnet der Shader: Eine Tropfenform, die das wandernde Rauschen
// (texturen/feuer/rauschen.png) von oben her ausfranst und seitlich wiegt; Farbe nach „Temperatur“ (weißgelber Kern, Orange, rote Ränder);
// Höhe und Helligkeit flackern im Takt des Lichts (dieselben Rauschfunktionen wie Fensterlicht und Nachtlichter, dieselbe Uhr wie Feuer.cs,
// darum flackert das OmniLight in genau dem Takt, den man an der Flamme sieht). Dazu auf Wunsch Funken: kleine glühende Punkte, die steigen,
// seitlich abtreiben und verlöschen. Ein Material je Feuerart, der Startwert je Flamme (`phase`) ist ein Instanz-Uniform: so teilen sich
// alle Fackeln ein Material. Sehr billig, weil nur eine Fläche und kein Partikel: kostet je Flamme einen Zeichenaufruf.
//
// Die Fläche ist größer als die Flamme („rahmen“ in Flammenbreiten und -höhen), damit oben Platz für die Funken bleibt; Feuer.cs skaliert sie
// auf (Breite · rahmen.x, Höhe · rahmen.y), der Ursprung der Fläche ist der Fuß der Flamme.
//
// Große Feuer (Phase 1b, Gebäudebrand auf Niedrig): „bewegung“ teilt die Flamme oben in mehrere Zungen, die einzeln lecken, und lässt sie stärker
// pumpen; „stichflammen“ schickt ab und zu einen kurzen, hohen Ausbruch nach oben (die Flamme schießt hoch, wird oben schmal und heller und
// fällt in einer halben Sekunde zurück). Beides rechnet nur aus Zeit und Startwert (stich_wert, in Feuer.cs als Stichwert nachgebaut, damit das
// Licht mit aufflackert) und kostet zwei Texturzugriffe mehr.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

global uniform vec2 wind_richtung;
global uniform float wetter_wind;

uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
// Die Uhr der Feuer (Feuer.cs setzt sie jedes Bild), damit Licht und Flamme im selben Takt sind
uniform float zeit = 0.0;
// 2 = immer ganz zur Kamera gedreht, 1 = zur Kamera gedreht, aber senkrecht (Hochachse), 0 = fest (für gekreuzte Flächen)
uniform int billboard = 2;
// Größe der Fläche in Flammenbreiten (x) und Flammenhöhen (y); die Flamme füllt davon den unteren Mittelteil (1 Einheit in x = halbe Breite der Zunge)
uniform vec2 rahmen = vec2(2.2, 1.9);
// Wie groß die Flamme in Metern ist (nur für die Größe der Funken)
uniform vec2 groesse_m = vec2(0.2, 0.45);
uniform float tempo = 1.0;
// 0 = ruhig (Kerze), 1 = wild (Feuer im Wind)
uniform float flackern = 0.35;
uniform float wiegen = 0.6;
uniform float ausfransen = 0.8;
// HDR-Helligkeit des Kerns (über 1 erreicht der Bloom die Flamme)
uniform float helligkeit = 1.6;
uniform vec3 farbe_kern : source_color = vec3(1.0, 0.96, 0.7);
uniform vec3 farbe_mitte : source_color = vec3(1.0, 0.55, 0.08);
uniform vec3 farbe_rand : source_color = vec3(0.75, 0.10, 0.02);
// Funken: wie viele (0 = keine, höchstens 12), wie hoch sie steigen (in Flammenhöhen) und wie groß (in Metern)
uniform int funken = 0;
uniform float funken_hoehe = 2.5;
uniform float funken_groesse = 0.012;
// Wie stark der Wind die Flamme neigt (0 = gar nicht)
uniform float wind_neigung = 0.4;
// Große Feuer: mehr Bewegung (0–1) und Stichflammen (0 = keine … 1 = häufig, etwa jede zweite bis vierte Sekunde je Zunge)
uniform float bewegung = 0.0;
uniform float stichflammen = 0.0;
instance uniform float phase = 0.0;

varying flat uint saat;
varying float neigung;

void vertex() {
	saat = uint(phase * 65535.0) + 7u;
	if (billboard == 2) {
		// Ganz zur Kamera gedreht (wie die Partikel der Technik B): rechts und oben der Kamera, die Größe vom Knoten
		float sx = length(MODEL_MATRIX[0].xyz);
		float sy = length(MODEL_MATRIX[1].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, INV_VIEW_MATRIX[1] * sy, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz) * wetter_wind * wind_neigung;
	} else if (billboard == 1) {
		// Der Kamera zugewandt, aber senkrecht: rechts der Kamera, oben die Hochachse des Modells
		float sx = length(MODEL_MATRIX[0].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, MODEL_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz) * wetter_wind * wind_neigung;
	} else {
		vec3 rechts = normalize(MODEL_MATRIX[0].xyz);
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), rechts.xz) * wetter_wind * wind_neigung;
	}
}

// Das Flackern des Lichts (0–1), wie in nachtlicht.gdshader; Feuer.cs rechnet dieselbe Formel für das OmniLight
float flacker_wert(float t, uint s) {
	return 0.55 * f_rausch(t * 1.7, s) + 0.3 * f_rausch(t * 4.3, s ^ 0x5bd1e995u) + 0.15 * f_rausch(t * 11.0, s ^ 0x27d4eb2du);
}

// Stichflamme (0 = keine … 1 = auf der Spitze): Die Zeit ist in Abschnitte von 1,6 bis 3,2 s geteilt; in jedem kommt mit der Wahrscheinlichkeit
// stichflammen · 0,75 ein Stoß zu einer zufälligen Stelle, der in 0,12 s ansteigt und danach abklingt (Feuer.cs: Stichwert, gleiche Rechnung)
float stich_wert(float t, uint s) {
	if (stichflammen <= 0.0) return 0.0;
	float dauer = 1.6 + 1.6 * f_hash(uvec4(s, 41u, 0u, 0u));
	float lauf = t / dauer + f_hash(uvec4(s, 43u, 0u, 0u));
	uint abschnitt = uint(int(floor(lauf)) & 0xFFFFFF);
	float im_abschnitt = fract(lauf) * dauer;
	float start = f_hash(uvec4(s, abschnitt, 47u, 0u)) * max(dauer - 0.9, 0.1);
	if (f_hash(uvec4(s, abschnitt, 53u, 0u)) >= stichflammen * 0.75) return 0.0;
	float x = im_abschnitt - start;
	if (x < 0.0) return 0.0;
	return smoothstep(0.0, 0.12, x) * exp(-max(x - 0.12, 0.0) * 4.0);
}

void fragment() {
	float ph = f_hash(uvec4(saat, 5u, 0u, 0u));
	float t = zeit * tempo * (0.85 + 0.3 * f_hash(uvec4(saat, 3u, 0u, 0u)));
	float f = flacker_wert(zeit * tempo, saat);   // derselbe Wert wie beim Licht (ohne den kleinen Tempo-Unterschied der Rauschmuster)
	float stich = stich_wert(zeit * tempo, saat);

	float fx = (UV.x - 0.5) * 2.0 * rahmen.x;     // Flammenbreite: die Zunge ist etwa −0,95 … 0,95 breit
	float fh = (1.0 - UV.y) * rahmen.y;           // Flammenhöhe: 0 … 1 ist die Flamme

	// Die Flamme wird mit dem Flackern höher und niedriger; große Feuer pumpen stärker (eigenes, langsameres Rauschen), eine Stichflamme schießt hoch
	float pumpen = bewegung * 0.45 * (f_rausch(zeit * tempo * 2.3, saat ^ 0x68e31da4u) - 0.5);
	float u = fh / (1.0 + (f - 0.5) * 0.55 * flackern + pumpen + 0.85 * stich);
	float u1 = clamp(u, 0.0, 1.0);
	// Während des Stoßes strömt das Rauschen schneller nach oben
	t += stich * 0.35;

	float grob = texture(rausch, vec2(fx * 0.12 + ph, u1 * 0.30 - t * 0.35)).r;
	float fein = texture(rausch, vec2(fx * 0.60 + ph * 1.7, u1 * 0.80 - t * 0.90)).g;
	float zack = texture(rausch, vec2(fx * 1.10 + ph * 2.3, u1 * 1.40 - t * 1.70)).b;

	float wieg = (grob - 0.5) * 0.9 * wiegen * (1.0 + 0.8 * bewegung) * pow(u1, 1.4)
		+ 0.10 * wiegen * pow(u1, 1.6) * sin(6.2832 * (t * 0.7 + ph))
		+ neigung * pow(u1, 1.5);
	float xc = fx - wieg;

	// Umriss: eine Tropfenform, unten gerundet, nach oben spitz; beim Stoß oben schmaler
	float breite = 0.95 * pow(1.0 - u1, 0.85 + 0.5 * stich) * (1.0 - exp(-u1 * 12.0)) + 0.004;
	// Große Feuer: oben teilen sich mehrere Zungen, die einzeln lecken (ein breites, schnell steigendes Rauschen schneidet Kerben in den Umriss)
	if (bewegung > 0.0) {
		float zunge = texture(rausch, vec2(fx * 0.42 + ph * 3.1, u1 * 0.45 - t * 0.75)).r;
		float kerbe = texture(rausch, vec2(fx * 0.9 + ph * 1.3 + 0.5, u1 * 0.7 - t * 1.2)).g;
		breite *= 1.0 + bewegung * 0.5 * u1 * (zunge - 0.5) * 2.0;
		breite *= 1.0 - bewegung * 0.55 * smoothstep(0.25, 0.9, u1) * smoothstep(0.45, 0.75, kerbe) * (1.0 - stich);
	}
	float innen = clamp(1.0 - pow(abs(xc) / breite, 2.0), 0.0, 1.0);
	// Ausfransen: je weiter oben, desto stärker frisst das Rauschen den Rand an
	float frass = (0.10 + 0.95 * u1) * (1.0 - (0.6 * fein + 0.4 * zack)) * 1.25 * ausfransen * (1.0 + 0.3 * bewegung);
	float d = clamp(innen * (1.25 + 0.35 * u1) - frass, 0.0, 1.0);

	// Heiß ist es unten und in der Mitte (beim Stoß auch weiter oben)
	float temp = clamp(pow(d, 1.7) * (1.25 - 0.75 * u1 + 0.35 * stich), 0.0, 1.0);
	vec3 farbe = temp < 0.5 ? mix(farbe_rand, farbe_mitte, temp * 2.0) : mix(farbe_mitte, farbe_kern, (temp - 0.5) * 2.0);
	float deckung = smoothstep(0.02, 0.35, d) * (0.92 - 0.12 * u1) * smoothstep(0.0, 0.07, u) * (1.0 - smoothstep(0.8, 1.05, u));
	float hell = helligkeit * (1.0 + (f - 0.5) * 0.7 * flackern) * (1.0 + 0.45 * stich);
	vec3 aus = farbe * deckung * hell;

	// Funken: je Funke eine Lebensdauer; bei jedem Neustart andere Startstelle und Abdrift (Zufall aus der Nummer des Umlaufs)
	for (int i = 0; i < funken; i++) {
		float dauer = 0.9 + 0.9 * f_hash(uvec4(saat, uint(i), 11u, 0u));
		float lauf = zeit / dauer + f_hash(uvec4(saat, uint(i), 13u, 0u));
		uint umlauf = uint(int(floor(lauf)) & 0xFFFFFF);
		float leben = fract(lauf);
		float zx = f_hash(uvec4(saat, uint(i), umlauf, 17u));
		float zy = f_hash(uvec4(saat, uint(i), umlauf, 19u));
		float px = (zx - 0.5) * 0.9 + (zy - 0.5) * 0.9 * leben + neigung * leben * leben + 0.08 * sin(leben * 9.0 + zx * 6.28);
		float py = 0.35 + leben * funken_hoehe * (0.5 + 0.5 * zy);
		// die Fläche ist in Flammeneinheiten gemessen, die Funkengröße in Metern: die Breite hat 1 Einheit = groesse_m.x / 2 (halbe Breite)
		vec2 abstand = vec2((fx - px) * groesse_m.x * 0.5, (fh - py) * groesse_m.y) / funken_groesse;
		float r2 = dot(abstand, abstand * vec2(1.0, 0.45));
		float glimmen = (1.0 - leben) * (1.0 - leben) * (0.6 + 0.4 * sin(zeit * 31.0 + zx * 40.0));
		aus += vec3(1.0, 0.65, 0.2) * exp(-r2 * 1.2) * glimmen * 2.2 * step(0.0, py);
	}
	ALBEDO = aus;
	ALPHA = 1.0;
}
