// Regentropfen (Stadtansicht.Wetter.cs, GpuParticles3D): Jeder Tropfen ist ein Strich in Fallrichtung, der auf dem Bildschirm in Richtung der
// Fallbewegung liegt (also mit dem Wind schräg) und zur Kamera zeigt. Länge, Dicke und Durchsicht hat jeder Tropfen aus seiner Nummer
// (INSTANCE_ID); das untere Ende (Kopf) ist hell, der Schweif läuft weich aus – wie ein Tropfen, der im Belichtungsbild einen Strich zieht.
// Die Fallrichtung kommt als Uniform (dieselbe wie im Partikelmaterial).
//
// Wichtig (PR #237): Ein Strich darf nie NaN oder einen negativen Wert ausgeben. Solche Bildpunkte werden von Bloom und Miniatur-Unschärfe
// weitergereicht und verschmiert – Bloom malt dann eine weiße Fläche, die Unschärfe dicke schwarze Flecken, ohne beides bleiben schwarze Striche
// im Regen. Darum: kein sqrt/pow auf Werten, die durch Interpolation an den Rändern knapp unter 0 fallen können (max/clamp), Alpha auf 0–1 begrenzt,
// und Bildpunkte ohne Wirkung (auch NaN) werden verworfen. Die Striche sind außerdem mindestens 1,6 Bildpunkte breit mit weichem Rand: Schmalere
// Quads treffen nur jeden zweiten Bildpunkt und zerfallen in Striche mit Lücken.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec4 farbe : source_color = vec4(0.78, 0.82, 0.9, 1.0);
// Fallrichtung in der Welt (Länge 1)
uniform vec3 richtung = vec3(0.0, -1.0, 0.0);
// Stärke des Regens 0–1 (stärkerer Regen: längere, deutlichere Striche)
uniform float staerke = 0.5;

varying float v_alpha;

float h(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	float id = float(INSTANCE_ID);
	float h1 = h(id), h2 = h(id + 17.0), h3 = h(id + 41.0);
	float s = length(MODEL_MATRIX[0].xyz);   // Maßstab des Partikels (wächst mit dem Zoom)
	float laenge = (0.8 + 1.5 * h1) * (0.8 + 0.4 * staerke) * s;
	float px = 2.0 / PROJECTION_MATRIX[1][1] / VIEWPORT_SIZE.y;   // Meter je Bildpunkt (Kamera ohne Perspektive)
	float breite = max((0.03 + 0.03 * h2) * s, px * (1.6 + 0.5 * h2));   // nie dünner als ~1,6 Bildpunkte, sonst zerfällt der Strich in Punkte
	laenge = max(laenge, px * 8.0);
	vec3 mitte = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz;
	vec3 d = (VIEW_MATRIX * vec4(richtung, 0.0)).xyz;
	// auf dem Bildschirm in Fallrichtung (Länge verkürzt sich mit der Schrägsicht, aber nie unter ein Drittel)
	vec2 a = normalize(d.xy + vec2(0.0001, 0.0));
	float kurz = max(length(d.xy), 0.35);
	vec2 q = vec2(-a.y, a.x);
	// UV.y: 0 oben (Schweif), 1 unten (Kopf); das Quad ist 1 × 1 groß
	vec2 p = q * VERTEX.x * breite + a * (-VERTEX.y) * laenge * kurz;
	POSITION = PROJECTION_MATRIX * vec4(mitte + vec3(p, 0.0), 1.0);
	v_alpha = (0.4 + 0.6 * h3) * (0.7 + 0.5 * staerke);
}

void fragment() {
	float quer = clamp(1.0 - abs(UV.x - 0.5) * 2.0, 0.0, 1.0);
	quer = quer * quer * (3.0 - 2.0 * quer);   // weicher Rand quer zum Strich (keine harte Kante, die flimmert)
	float laengs = smoothstep(0.0, 0.7, UV.y) * (1.0 - smoothstep(0.93, 1.0, UV.y));
	float a = clamp(farbe.a * v_alpha * quer * laengs, 0.0, 1.0);
	if (!(a > 0.004)) discard;   // „nicht größer“ fängt auch NaN
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = a;
}
