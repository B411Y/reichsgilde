// Pfützen und stehendes Wasser der Restflächen-Füllung (Wasserstelle, Paket 4): dunkles Wasser mit glatter Oberfläche, das den Himmel spiegelt, bei Regen
// dunkler und glänzender wird und Tropfenringe bekommt. Das Modell trägt das Material „Pfuetze“ (tools/modelle/fuellung_p3.py, Rauheit 0,03); die
// Stadtansicht tauscht es beim Backen der nahen Füllnetze gegen dieses Material (Stadtansicht.Fuellbacken.cs, PfuetzenTauschen). Ferne Netze (Zoom 50 und
// 100 m) behalten das einfache Material und legen es mit dem „Wasser“ zusammen (fern_stoffe), das spart Zeichenaufrufe.
//
// stufe wie der Boden (Einstellung „Boden“): 0 Niedrig = dunkle, glatte Fläche wie bisher, 1 Mittel = dazu Himmelsspiegelung (Fresnel), 2 Hoch = dazu die
// Regentropfen der Welt (regentropfen in wetter.gdshaderinc, Dichte wetter_ringe, also nur bei Regen und nur in der Wetterstufe Hoch oder Mittel).
// Keine Bildschirmspiegelung (Screen-Space-Reflection): Sie wäre für alle glatten Flächen der Welt zugleich an; die Pfütze spiegelt nur den Himmel.
shader_type spatial;

#include "res://assets/shader/wetter.gdshaderinc"

uniform vec3 farbe : source_color = vec3(0.095, 0.135, 0.155);
uniform int stufe : hint_range(0, 2) = 2;
uniform float rauheit : hint_range(0.0, 1.0) = 0.03;

varying vec3 welt;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float px = max(fwidth(welt.x), fwidth(welt.z));   // Meter je Bildpunkt, vor jeder Verzweigung (Ableitungen brauchen alle Nachbarpunkte)
	float regen = wetter_regen;
	ALBEDO = farbe * (1.0 - 0.25 * regen);
	ROUGHNESS = stufe == 0 ? 0.1 : mix(rauheit, 0.015, regen);
	if (stufe >= 1) {
		vec3 n = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
		vec3 nw = n;
		float fresnel = pow(1.0 - clamp(dot(NORMAL, normalize(VIEW)), 0.0, 1.0), 4.0);
		// Himmelsschein: flach von oben leicht, flach gesehen deutlich; bei Regen stärker (nasse, glänzende Fläche)
		vec3 leuchten = wetter_himmel * (0.05 + 0.5 * fresnel) * (1.0 + 0.6 * regen);
		if (stufe >= 2 && wetter_ringe > 0.001) {
			vec3 tr = regentropfen(welt.xz, TIME, px, wetter_ringe);
			nw = normalize(n + vec3(tr.x, 0.0, tr.y) * 0.9);
			ALBEDO *= 1.0 - 0.35 * max(-tr.z, 0.0);
			leuchten += wetter_himmel * 0.25 * max(tr.z, 0.0);
		}
		NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
		EMISSION = leuchten;
	}
}
