shader_type spatial;
// Verkohltes Holz (Fachwerk, Balken, Bretter nach einem Brand): schwarze Kohle mit grober, rissiger Krokodilhaut. Große Zellen mit
// tiefen schwarzen Rissen, die meisten Schollen stumpf, einzelne glänzend. Eigenes Muster im Raum (Weltlage), keine Textur; was feiner
// ist als ein Bildpunkt, blendet nach fwidth zur mittleren Farbe aus (kein Flimmern, kein Moiré im Pixel-Look).
render_mode diffuse_burley, specular_schlick_ggx;

#include "brand_gemein.gdshaderinc"

uniform float zellgroesse = 13.0; // Zellen je Meter
varying vec3 welt;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hoehe(vec3 z) {
	return smoothstep(0.0, 0.16, z.y);
}

void fragment() {
	vec3 p = welt * zellgroesse;
	float fw = length(fwidth(p));
	float scharf = 1.0 - smoothstep(0.30, 0.85, fw); // Zellen größer als ein Bildpunkt: Muster zeigen
	vec3 z = scharf > 0.0 ? b_zellen(p) : vec3(0.4, 0.2, 0.5); // Zellen unter einem Bildpunkt: gar nicht erst rechnen
	float h = hoehe(z);
	float riss = mix(0.72, h, scharf);
	vec3 kohle = mix(vec3(0.016, 0.014, 0.013), vec3(0.052, 0.047, 0.043), z.z);
	float scholle = step(0.80, z.z);
	ALBEDO = kohle * mix(0.1, 1.0, riss);
	ROUGHNESS = mix(0.96, 0.36, scholle * riss * scharf);
	SPECULAR = mix(0.18, 0.55, scholle * scharf);
	// Wölbung der Schollen: Normale aus dem Zellmuster (Bildschirmableitung), nur solange das Muster zu sehen ist
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dFdx(h) * r1 + dFdy(h) * r2);
	NORMAL = normalize(abs(det) * NORMAL - 0.0016 * zellgroesse * scharf * grad);
}
