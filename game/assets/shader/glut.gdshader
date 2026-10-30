// Glut, Technik A und B (Phase 1 „Feuer und Flammen“, Feuer.cs): ein flaches, rundes Glutbett für Esse, Herd und Feuerstelle. Schwarze Kohle mit
// glühenden Stellen, die atmen und flackern (wanderndes Rauschen aus texturen/feuer/rauschen.png, derselbe Takt wie Licht und Flamme); die
// hellen Stellen sind HDR (über 1), damit der Bloom sie umgibt. Liegt flach auf dem Boden der Feuerstelle (Fläche nach oben, UV 0–1).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
uniform float zeit = 0.0;
uniform float helligkeit = 1.8;
uniform float flackern = 0.3;
// Anteil der Fläche, der glüht (0–1)
uniform float glimmen = 0.75;

varying flat uint saat;

void vertex() {
	saat = f_hash_u(uvec4(uvec3(ivec3(round(MODEL_MATRIX[3].xyz * 100.0))), 9u));   // Startwert aus der Lage (kein Instanz-Uniform)
}

void fragment() {
	float ph = f_hash(uvec4(saat, 5u, 0u, 0u));
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float n1 = texture(rausch, UV * 1.7 + ph).r;
	float n2 = texture(rausch, UV * 4.0 + vec2(ph * 3.1, zeit * 0.015)).g;
	float n3 = texture(rausch, UV * 7.0 + vec2(zeit * 0.01, ph * 5.0)).b;

	// Jede glühende Stelle atmet für sich: das Rauschen wird mit der Zeit verschoben
	float atem = f_rausch(zeit * 1.1 + n1 * 6.0, saat) * 0.5 + f_rausch(zeit * 3.7 + n2 * 9.0, saat ^ 0x5bd1e995u) * 0.5;
	float masse = 0.62 * n1 + 0.38 * n2;
	float gluh = smoothstep(1.0 - glimmen, 1.0 - glimmen + 0.18, masse + (atem - 0.5) * 0.45 * flackern * 2.0 + (n3 - 0.5) * 0.2);

	vec3 kohle = vec3(0.10, 0.025, 0.012) * (0.6 + 0.8 * n3) * helligkeit;
	vec3 rot = vec3(0.95, 0.16, 0.02);
	vec3 gelb = vec3(1.0, 0.72, 0.22);
	vec3 gluehend = mix(rot, gelb, clamp(gluh * (0.5 + 0.7 * atem), 0.0, 1.0)) * helligkeit * (0.9 + 0.6 * atem);
	vec3 farbe = mix(kohle, gluehend, gluh);

	// Rand: unregelmäßig und weich
	float rand = 1.0 - smoothstep(0.74, 0.98, r + (n2 - 0.5) * 0.25);
	ALBEDO = farbe;
	ALPHA = rand;
}
