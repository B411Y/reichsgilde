// Schneeflocken (Stadtansicht.Wetter.cs, GpuParticles3D): weiche runde Flocken, die zur Kamera zeigen, im Fallen hin und her pendeln,
// sich langsam drehen und von Böen (wetter_wind) seitlich getragen werden. Jede Flocke hat über ihre Nummer (INSTANCE_ID) ihren eigenen
// Takt, ihre eigene Größe und Drehrichtung. Das Pendeln rechnet hier und nicht im Partikelmaterial, damit es in jeder Grafikstufe gilt.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

global uniform float wetter_wind;

uniform vec4 farbe : source_color = vec4(0.95, 0.96, 1.0, 0.9);
// Maßstab der Ansicht (Zoom / 95): Pendelweite und Böen wachsen mit, damit die Bewegung bei jedem Zoom gleich aussieht
uniform float massstab = 1.0;

varying float v_winkel;

float h(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	float id = float(INSTANCE_ID);
	float h1 = h(id), h2 = h(id + 17.0), h3 = h(id + 41.0);
	float t = TIME;
	float phase = h1 * 6.2832;
	// Pendeln: zwei Schwingungen quer, eine kleine auf und ab; je Flocke andere Weite und anderer Takt
	float weite = (0.3 + 0.5 * h2) * massstab;
	vec3 weg = vec3(
		sin(t * (0.8 + 0.7 * h2) + phase),
		sin(t * 1.6 + phase * 2.0) * 0.12,
		cos(t * (0.6 + 0.6 * h3) + phase * 1.7)) * weite;
	// Böen tragen alle Flocken zugleich, jede etwas anders
	float boe = sin(t * 0.45 + MODEL_MATRIX[3].x * 0.04 + h1 * 2.0) * 0.5 + 0.5;
	weg.x += wetter_wind * boe * 0.9 * massstab * sin(t * 1.3 + phase);
	vec3 ort = MODEL_MATRIX[3].xyz + weg;
	// zur Kamera gedreht, Größe aus dem Partikel (Maßstab der Flocke) behalten
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(ort, 1.0));
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	v_winkel = t * (h3 - 0.5) * 1.6 + phase;
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float a = atan(p.y, p.x) - v_winkel;
	// weiche Scheibe mit leicht sechsstrahligem Rand (dreht sich mit der Flocke)
	float form = r * (1.0 - 0.18 * pow(abs(cos(3.0 * a)), 3.0));
	float rand = 1.0 - smoothstep(0.25, 1.0, form);
	ALBEDO = farbe.rgb;
	ALPHA = farbe.a * rand * rand;
}
