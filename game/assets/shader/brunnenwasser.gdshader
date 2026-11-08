// Wasser im Marktbrunnen (Becken und Viehtrog): dunkles, glattes Wasser wie die Pfützen (pfuetze.gdshader), das den Himmel spiegelt, dazu laufende Wellenringe
// um die vier Stellen, an denen die Strahlen auftreffen. Das Modell trägt das Material „Brunnenwasser“ (tools/modelle/brunnen.py, Netz „Brunnen_Wasser“); die
// Stadtansicht belegt es damit (Stadtansicht.Brunnen.cs). Die Auftreffpunkte liegen im Modell bei (±1,06 | ±1,06) m (Abstand 1,5 m, Diagonalen).
//
// stufe wie der Boden: 0 Niedrig = dunkle, glatte Fläche, 1 Mittel = dazu Himmelsspiegelung, 2 Hoch = dazu Wellenringe und bei Regen die Tropfen der Welt.
// ringe: 1 = Ringe an, 0 = aus (die Stadtansicht schaltet sie ab etwa 60 m Zoom ab, dort sieht man sie nicht).
shader_type spatial;

#include "res://assets/shader/wetter.gdshaderinc"

uniform vec3 farbe : source_color = vec3(0.095, 0.135, 0.155);
uniform int stufe : hint_range(0, 2) = 2;
uniform float rauheit : hint_range(0.0, 1.0) = 0.03;
uniform float ringe : hint_range(0.0, 1.0) = 1.0;

varying vec3 welt;
varying vec2 lok;
varying vec3 achse_x;
varying vec3 achse_z;

const vec2 AUF[4] = vec2[4](vec2(1.0607, 1.0607), vec2(-1.0607, 1.0607), vec2(1.0607, -1.0607), vec2(-1.0607, -1.0607));

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	lok = VERTEX.xz;
	achse_x = MODEL_MATRIX[0].xyz;   // Richtung der Modell-Achsen in der Welt (der Marktplatz kann gedreht stehen)
	achse_z = MODEL_MATRIX[2].xyz;
}

void fragment() {
	float px = max(fwidth(welt.x), fwidth(welt.z));
	float regen = wetter_regen;
	ALBEDO = farbe * (1.0 - 0.25 * regen);
	ROUGHNESS = stufe == 0 ? 0.1 : mix(rauheit, 0.015, regen);
	if (stufe >= 1) {
		vec3 n = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
		vec3 nw = n;
		float fresnel = pow(1.0 - clamp(dot(NORMAL, normalize(VIEW)), 0.0, 1.0), 4.0);
		vec3 leuchten = wetter_himmel * (0.05 + 0.5 * fresnel) * (1.0 + 0.6 * regen);
		if (stufe >= 2) {
			if (ringe > 0.5) {
				vec2 kipp = vec2(0.0);
				float hell = 0.0;
				for (int i = 0; i < 4; i++) {
					vec2 d = lok - AUF[i];
					float r = length(d);
					float blass = clamp(1.0 - r / 0.8, 0.0, 1.0);
					blass *= blass;
					float ph = r * 14.0 - TIME * 3.5 + float(i) * 1.7;
					kipp += d / max(r, 0.001) * cos(ph) * blass * 0.5;
					hell += max(sin(ph), 0.0) * blass;
				}
				vec3 kippwelt = normalize(achse_x) * kipp.x + normalize(achse_z) * kipp.y;
				nw = normalize(n + kippwelt);
				leuchten += wetter_himmel * 0.2 * hell;
			}
			if (wetter_ringe > 0.001) {
				vec3 tr = regentropfen(welt.xz, TIME, px, wetter_ringe);
				nw = normalize(nw + vec3(tr.x, 0.0, tr.y) * 0.9);
				ALBEDO *= 1.0 - 0.35 * max(-tr.z, 0.0);
				leuchten += wetter_himmel * 0.25 * max(tr.z, 0.0);
			}
		}
		NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
		EMISSION = leuchten;
	}
}
