// Fahnen im Wind: Das Tuch ist ein feines Netz in der Ebene XY (u = UV.x läuft vom Mast zum freien Ende, v = UV.y über die Höhe,
// UV2 = Länge und Höhe in Metern). Der Vertex-Shader biegt es senkrecht zur Tuchfläche (lokal +Z) mit mehreren Wellen,
// die vom Mast nach außen laufen. Am Mast (u = 0) bleibt das Tuch fest, am freien Ende schlägt es am stärksten aus.
// Die Normalen kommen aus der Steigung der Welle: So werfen die Falten Licht und Schatten. Kein Skript, keine Physik.
shader_type spatial;
render_mode cull_disabled;

// Der Wind ist für alle Fahnen gleich (project.godot, [shader_globals]); ein Wetter kann ihn später ändern.
global uniform vec2 wind_richtung;
global uniform float wind_staerke;

uniform vec4 farbe : source_color = vec4(0.75, 0.15, 0.15, 1.0);
uniform float rauheit : hint_range(0.0, 1.0) = 0.9;
// Wandbehang: schlägt nur von der Wand weg (+Z) aus, nie in sie hinein
uniform bool wandbehang = false;
// Ausschlag am freien Ende in Teilen der Fahnenlänge (bei Windstärke 1)
uniform float ausschlag = 0.16;
// Ein Rand ringsum in dieser Farbe, Breite in Metern (0 = keiner; die Spielerfahne der Kartenwerkstatt: weiß mit goldenem Rand)
uniform vec4 rand_farbe : source_color = vec4(0.85, 0.62, 0.12, 1.0);
uniform float rand_breite = 0.0;

// Stärke des Flatterns: ein leichter Grundwind, dazu der Wind; bei Windstärke 0 hängt das Tuch still
float wirkung() {
	return clamp(wind_staerke * 20.0, 0.0, 1.0) * (0.3 + wind_staerke);
}

// Verschiebung des Tuchs senkrecht zur Fläche (in Metern) an der Stelle (u, v)
float verbiegung(float u, float v, float phase, float t, float laenge) {
	float g = pow(u, 1.35);
	float w1 = sin(u * 5.5 - t * 2.6 + phase);
	float w2 = sin(u * 10.5 - t * 4.1 + phase * 1.7) * 0.4;
	float w3 = sin(v * 4.0 + u * 3.0 - t * 1.9 + phase * 0.6) * 0.25;
	// Böen: zwei unregelmäßig überlagerte langsame Schwingungen verändern die Stärke
	float boe = 0.8 + 0.2 * sin(t * 0.37 + phase) + 0.15 * sin(t * 0.91 + phase * 2.3);
	float rohwert = (w1 + w2 + w3) * g * boe * ausschlag * laenge * wirkung();
	if (wandbehang) {
		// Verschoben und halbiert: immer >= 0 (größtmöglicher Ausschlag der Wellen: 1,65 · Böe 1,15 ≈ 1,9)
		float grenze = 1.9 * g * ausschlag * laenge * wirkung();
		return (rohwert + grenze) * 0.25;
	}
	return rohwert;
}

void vertex() {
	float laenge = UV2.x;
	float hoehe = UV2.y;
	float u = UV.x;
	float v = UV.y;
	// Jede Fahne bekommt aus ihrer Weltposition eine eigene Phase, damit nicht alle im Gleichtakt flattern.
	float phase = dot(NODE_POSITION_WORLD.xz, vec2(0.37, 0.71));
	float t = TIME * (1.0 + 1.5 * wind_staerke);

	float aus = verbiegung(u, v, phase, t, laenge);
	// Der Wind drückt das Tuch zusätzlich auf die Seite, von der er kommt weg (Windrichtung gegen die Tuchnormale in der Welt)
	vec3 normale_welt = normalize((MODEL_MATRIX * vec4(0.0, 0.0, 1.0, 0.0)).xyz);
	float drueck = dot(normalize(wind_richtung + vec2(0.0001)), normale_welt.xz) * wind_staerke * pow(u, 1.35) * laenge * 0.06;
	if (wandbehang) VERTEX.z += aus + drueck;
	else VERTEX.z += aus;

	// Steigung der Welle entlang u (Länge) und v (Höhe) ergibt die geneigte Normale
	float e = 0.01;
	float du = (verbiegung(min(u + e, 1.0), v, phase, t, laenge) - verbiegung(max(u - e, 0.0), v, phase, t, laenge)) / (2.0 * e * laenge);
	float dv = (verbiegung(u, min(v + e, 1.0), phase, t, laenge) - verbiegung(u, max(v - e, 0.0), phase, t, laenge)) / (2.0 * e * hoehe);
	NORMAL = normalize(vec3(-du, -dv, 1.0));

	if (!wandbehang) {
		// Alle Fahnen wehen in dieselbe Weltrichtung (die Windrichtung, der auch der Regen folgt), egal wie das Gebäude gedreht ist:
		// Das Tuch wird um seinen Mast (Ursprung) so gedreht, dass seine Längsachse in Windrichtung zeigt; die Höhe bleibt.
		mat4 modell = MODEL_MATRIX;
		float mass = length(modell[0].xyz);
		vec3 hoch = modell[1].xyz;
		vec3 hoch_n = normalize(hoch);
		vec2 w = normalize(wind_richtung + vec2(0.0001));
		vec3 laengs = normalize(vec3(w.x, 0.0, w.y) - hoch_n * dot(vec3(w.x, 0.0, w.y), hoch_n) + vec3(0.0001, 0.0, 0.0));
		vec3 quer = cross(laengs, hoch_n);
		vec3 welt = modell[3].xyz + laengs * VERTEX.x * mass + hoch * VERTEX.y + quer * VERTEX.z * mass;
		vec3 n_welt = laengs * NORMAL.x + hoch_n * NORMAL.y + quer * NORMAL.z;
		mat4 zurueck = inverse(modell);
		VERTEX = (zurueck * vec4(welt, 1.0)).xyz;
		NORMAL = normalize((zurueck * vec4(n_welt, 0.0)).xyz);
	}
}

void fragment() {
	ALBEDO = farbe.rgb;
	if (rand_breite > 0.0) {
		// Abstand zur nächsten Kante in Metern (am Schwalbenschwanz läuft u = 1 an der Kerbe entlang, dort wird der Rand etwas schmaler)
		float kante = min(min(UV.x, 1.0 - UV.x) * UV2.x, min(UV.y, 1.0 - UV.y) * UV2.y);
		if (kante < rand_breite) ALBEDO = rand_farbe.rgb;
	}
	ROUGHNESS = rauheit;
	SPECULAR = 0.25;
}
