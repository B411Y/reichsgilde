// Brand am Haus (Gebaeudebrand.cs, Auftrag „Feuer überall“): die Flächen, die zu den Flammen gehören – nicht die Flammen selbst (flamme_sim.gdshader).
//   modus 0  Brandfleck auf dem Dach: schwarze Kohle mit glühenden Rissen und verkohltem Rand (liegt flach auf der Dachfläche)
//   modus 1  Öffnung eines Fensters: Gluthitze hinter der Scheibe – unten dunkelrot, in der Mitte gelb bis weiß, oben ruhiger; flackert
// Die Stärke der Stelle (Öffnung) steckt in der Tiefe der flachen Fläche (Skalierung Z / X − 1), wie bei den Flammen: kein Instanz-Uniform.
// „staerke“ ist die Flammenstärke des ganzen Brands (0–1), „glut“ die Glut nach dem Feuer (0–1); „umgebung“ 0 = Nacht … 1 = Tag.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
uniform int modus = 0;
uniform float zeit = 0.0;
uniform float staerke = 1.0;
uniform float glut = 0.0;
uniform float umgebung = 1.0;

varying float eigen;
varying flat uint saat;

void vertex() {
	float tiefe = length(MODEL_MATRIX[2].xyz) / max(length(MODEL_MATRIX[0].xyz), 1e-6) - 1.0;
	eigen = clamp(tiefe, 0.0, 1.0);
	vec3 o = MODEL_MATRIX[3].xyz;
	saat = f_hash_u(uvec4(uint(int(o.x * 10.0) & 0xFFFF), uint(int(o.y * 10.0) & 0xFFFF), uint(int(o.z * 10.0) & 0xFFFF), 3u)) & 0xFFFFu;
}

void fragment() {
	vec2 uv = UV;
	float s = float(saat) / 65535.0;
	float flackern = 0.72 + 0.28 * f_rausch(zeit * 5.0, saat) + 0.12 * (f_rausch(zeit * 13.0, saat ^ 0x55u) - 0.5);
	vec3 rgb = vec3(0.0);
	float a = 0.0;
	if (modus == 0) {
		// Brandfleck: weicher Rußschleier auf dem Dach (Verlauf statt Ausschnitt, das Dach scheint durch), unregelmäßig, mit schwachem Glutschimmer in der Mitte
		vec2 p = (uv - 0.5) * 2.0;
		float n1 = texture(rausch, uv * 0.8 + vec2(s, s * 0.7)).r;
		float n2 = texture(rausch, uv * 1.9 + vec2(s * 1.3, 3.1)).g;
		float d = length(p * vec2(0.9, 1.0)) + 0.8 * (n1 - 0.5) + 0.3 * (n2 - 0.5);
		float russ = pow(1.0 - smoothstep(0.05, 1.0, d), 1.6);
		float hitze = clamp(staerke * 1.1 + glut, 0.0, 1.3);
		float atmen = 0.6 + 0.4 * f_rausch(zeit * 1.7 + n2 * 3.0, saat ^ 0x2du);
		float ring = smoothstep(0.25, 0.55, d) * (1.0 - smoothstep(0.55, 0.85, d));
		float leucht = ring * smoothstep(0.4, 0.7, n2) * hitze * atmen * flackern * 0.8;
		rgb = vec3(0.04, 0.032, 0.028) + vec3(0.9, 0.22, 0.05) * leucht;
		a = clamp(russ * (0.35 + 0.35 * n2) + leucht * 0.5, 0.0, 0.7);
	} else {
		// Öffnung (modus 1): glühendes Innere des Fensters
		float k = clamp(eigen, 0.0, 1.0);
		vec2 p = uv;
		float n = texture(rausch, vec2(p.x * 1.6 + s, p.y * 0.9 - zeit * 0.25)).r;
		float mitte = 1.0 - abs(p.x - 0.5) * 2.0;
		float hoehe = 1.0 - p.y;                                  // 0 oben … 1 unten (UV.y läuft nach unten)
		float hell = clamp(hoehe * 0.8 + 0.25 + (n - 0.5) * 0.7, 0.0, 1.0) * (0.55 + 0.45 * mitte);
		vec3 heiss = mix(vec3(0.9, 0.16, 0.03), vec3(1.0, 0.7, 0.28), hell);
		heiss = mix(heiss, vec3(1.0, 0.92, 0.7), pow(hell, 3.0) * 0.6 * staerke);
		rgb = heiss * (1.25 + 1.2 * k) * flackern;
		// Kante: Fensterrahmen bleibt dunkel
		float rahmen = smoothstep(0.0, 0.08, p.x) * smoothstep(1.0, 0.92, p.x) * smoothstep(0.0, 0.06, p.y) * smoothstep(1.0, 0.94, p.y);
		a = clamp(0.35 + 0.65 * k, 0.0, 1.0) * rahmen;
	}
	// Bei Tag nichts zu dämpfen; die Glut selbst leuchtet nachts wie tags (unshaded, HDR für den Bloom)
	ALBEDO = rgb;
	ALPHA = clamp(a, 0.0, 1.0);
}
