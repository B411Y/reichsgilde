// Wasserspritzer (Wasserspritzer.cs, Löschkette): ein Schwall Tropfen aus einem Eimer. Die Flugbahn rechnet dieser Shader aus der gemeinsamen Uhr und der Nummer
// des Tropfens (COLOR.r): Bogen nach vorn (+Z des Knotens, die Länge ist seine Skalierung in Z), Aufschlag, Spritzen nach allen Seiten, dazu etwas Dampf.
// Alle Flächen des Netzes liegen im Ursprung; hier werden sie zur Kamera gedreht und verschoben. POSITION wird immer gesetzt.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

uniform float zeit = 0.0;
uniform float umgebung = 1.0;
uniform float takt = 2.6;

varying float helle;
varying float dampf;
varying vec2 kante;

void vertex() {
	float id = COLOR.r;
	uint s = uint(id * 997.0) + 31u;
	float h1 = f_hash(uvec4(s, 1u, 0u, 0u));
	float h2 = f_hash(uvec4(s, 2u, 0u, 0u));
	float h3 = f_hash(uvec4(s, 3u, 0u, 0u));
	float h4 = f_hash(uvec4(s, 4u, 0u, 0u));
	float h5 = f_hash(uvec4(s, 5u, 0u, 0u));
	float h6 = f_hash(uvec4(s, 6u, 0u, 0u));
	float h7 = f_hash(uvec4(s, 7u, 0u, 0u));
	kante = UV;
	dampf = step(0.8, id);
	float tau = mod(zeit, takt) - id * 0.24;          // Zeit seit dem Abwurf dieses Tropfens
	float flug = 0.42 + 0.14 * h1;
	vec3 ziel = vec3((h2 - 0.5) * 0.5, 0.8 + (h3 - 0.5) * 0.9, 1.0);
	vec3 pos = vec3(0.0);
	float groesse = 0.0;
	helle = 0.0;
	if (dampf < 0.5) {
		if (tau >= 0.0 && tau < flug) {
			float u = tau / flug;
			float v0 = (ziel.y + 4.9 * flug * flug) / flug;
			pos = vec3(ziel.x * u, v0 * tau - 4.9 * tau * tau, ziel.z * u);
			groesse = 0.13 + 0.07 * h4;
			helle = 1.0;
		} else if (tau >= flug && tau < flug + 0.55) {
			float tt = tau - flug;
			vec3 v = vec3((h5 - 0.5) * 2.4, 1.0 + 1.8 * h6, -0.3 - 0.9 * h7);
			pos = ziel + v * tt + vec3(0.0, -4.9 * tt * tt, 0.0);
			float l = tt / 0.55;
			groesse = (0.1 + 0.06 * h4) * (1.0 - 0.6 * l);
			helle = 1.0 - l;
		}
	} else {
		// Dampf: steigt von der Stelle, an der das Wasser trifft, wird größer und verfliegt
		float tt = tau - flug;
		if (tt >= 0.0 && tt < 1.2) {
			float l = tt / 1.2;
			pos = ziel + vec3((h5 - 0.5) * 0.35, 0.15 + tt * 0.8, -0.1 - 0.1 * tt);
			groesse = 0.5 + 0.9 * l;
			helle = 0.55 * (1.0 - l) * smoothstep(0.0, 0.15, l);
		}
	}
	vec4 vp = VIEW_MATRIX * (MODEL_MATRIX * vec4(pos, 1.0));
	vp.xy += (UV - 0.5) * 2.0 * groesse;
	POSITION = PROJECTION_MATRIX * vp;
}

void fragment() {
	float d = length(kante - 0.5) * 2.0;
	float rund = 1.0 - smoothstep(0.35, 1.0, d);
	float tag = 0.4 + 0.6 * umgebung;
	vec3 wasser = vec3(0.78, 0.9, 1.0) * tag;
	vec3 nebel = vec3(0.92, 0.93, 0.95) * tag;
	ALBEDO = mix(wasser, nebel, dampf);
	ALPHA = rund * helle * mix(0.9, 0.5, dampf);
}
