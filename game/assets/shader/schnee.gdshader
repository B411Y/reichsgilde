// Schneeflocken (Stadtansicht.Wetter.cs, MultiMeshInstance3D): Jede Flocke ist ein Bildschirm-Quad, dessen Ort hier aus ihrer Nummer
// (INSTANCE_ID) und wenigen Uniforms der Stadtansicht berechnet wird – es gibt keine Teilchen mit Lebensdauer. Die Flocken liegen in einem Kasten
// um den Blickpunkt, ihre Lage ist ein Bruchteil des Kastens (0–1, umlaufend): Wer oben herausfällt, kommt unten wieder herein, und wo die Kamera
// auch hinwandert oder hinzoomt, ist der Kasten sofort gleichmäßig gefüllt (vorher: Teilchen blieben in der Welt liegen und bildeten nach schnellem
// Zoomen fleckige Inseln). Die Flocke fällt klar nach unten, schwingt dabei aber wie eine Feder hin und her: Seitenschwung mit Neigung, und sie fällt
// in der Mitte der Bewegung am schnellsten, an den Umkehrpunkten am langsamsten – nie Stillstand, nie Aufstieg. Jede Flocke hat eigene Phase, Weite,
// Schwungrichtung und Fallgeschwindigkeit. Das Pendeln rechnet hier, damit es in jeder Grafikstufe gilt.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

global uniform float wetter_wind;

uniform vec4 farbe : source_color = vec4(0.95, 0.96, 1.0, 0.9);
// Maßstab der Ansicht (Zoom / 95): Weite des Schwungs und Größe wachsen mit, damit die Bewegung bei jedem Zoom gleich aussieht
uniform float massstab = 1.0;
// Mitte des Kastens (Welt), Drehung um die Hochachse, ganze Kantenlängen (Meter)
uniform vec3 mitte = vec3(0.0);
uniform float drehung = 0.0;
uniform vec3 ganz = vec3(100.0, 12.0, 100.0);
// Verschiebung als Bruchteil des Kastens (Kamerabewegung und Wind), umlaufend 0–1
uniform vec3 versatz = vec3(0.0);
// Fallweg in Kastenhöhen, umlaufend 0–20 (je Flocke mit n/20 malgenommen, darum geht der Umlauf ohne Sprung)
uniform float fall = 0.0;
// Flockengröße in Metern (vor dem Faktor je Flocke) und kleinste Größe in Bildpunkten
uniform float groesse = 0.4;

varying float v_winkel;
varying float v_flach;
varying float v_neigung;
varying float v_alpha;

float h(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	float id = float(INSTANCE_ID);
	float h1 = h(id), h2 = h(id + 17.0), h3 = h(id + 41.0), h4 = h(id + 73.0);
	// gleichmäßig über den Kasten verteilt (Zahlenfolge R3 mit etwas Streuung), nicht als Haufen
	vec3 u = fract(vec3(0.5) + id * vec3(0.8191725134, 0.6710436067, 0.5497004779) + 0.3 * (vec3(h1, h2, h3) - 0.5));
	float f = (16.0 + floor(h4 * 9.0)) / 20.0;   // Fallgeschwindigkeit 0,8–1,2
	u.y = fract(u.y - fall * f + versatz.y);
	u.x = fract(u.x + versatz.x);
	u.z = fract(u.z + versatz.z);
	vec3 lok = (u - 0.5) * ganz;
	float c = cos(drehung), s = sin(drehung);
	vec3 ort = mitte + vec3(c * lok.x + s * lok.z, lok.y, -s * lok.x + c * lok.z);
	// Feder: Seitenschwung mit eigener Phase, Weite, Richtung; der Fall wird an den Umkehrpunkten gebremst (Geschwindigkeit 1 + b·cos 2θ, b < 1)
	float t = TIME;
	float w = 1.6 + 1.3 * h2;                     // rad/s: Schwungdauer 2–4 s
	float th = w * t + h1 * 6.2832;
	float psi = h3 * 6.2832;
	float weite = (0.8 + 1.0 * h4) * massstab;
	float seit = sin(th) * weite;
	vec3 weg = vec3(cos(psi), 0.0, sin(psi)) * seit;
	weg += vec3(-sin(psi), 0.0, cos(psi)) * sin(th * 0.5 + h2 * 3.0) * 0.25 * weite;   // kleiner Quergang: Flocken laufen nicht auf einer Geraden
	weg.y = -2.5 * massstab * 0.6 * sin(2.0 * th) / (2.0 * w);
	// Böen tragen alle Flocken zugleich, jede etwas anders
	float boe = sin(t * 0.45 + ort.x * 0.04 + h1 * 2.0) * 0.5 + 0.5;
	weg.x += wetter_wind * boe * 0.5 * massstab * sin(t * 1.3 + h1 * 6.0);
	ort += weg;
	vec3 vp = (VIEW_MATRIX * vec4(ort, 1.0)).xyz;
	float px = 2.0 / PROJECTION_MATRIX[1][1] / VIEWPORT_SIZE.y;   // Meter je Bildpunkt (Kamera ohne Perspektive)
	float gr = max(groesse * (0.6 + 0.9 * h2), px * 2.4);
	POSITION = PROJECTION_MATRIX * vec4(vp + vec3(VERTEX.xy * gr, 0.0), 1.0);
	// Neigung: Die Flocke kippt mit dem Schwung, flach gesehen wird sie schmal
	v_neigung = 0.9 * cos(th) * sign(cos(psi) + 0.0001);
	v_flach = 0.62 + 0.38 * abs(sin(th * 0.5 + h3 * 6.0));
	v_winkel = t * (h3 - 0.5) * 1.6 + h1 * 6.2832;
	// oben und unten im Kasten weich ein- und ausblenden (dort wird umgelaufen)
	v_alpha = smoothstep(0.0, 0.05, u.y) * (1.0 - smoothstep(0.93, 1.0, u.y));
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	// gekippt: gedreht und auf einer Achse gestaucht
	float cn = cos(v_neigung), sn = sin(v_neigung);
	p = vec2(cn * p.x + sn * p.y, -sn * p.x + cn * p.y);
	p *= vec2(sqrt(v_flach), 1.0 / sqrt(v_flach));   // gestaucht, die Fläche bleibt gleich
	float r = length(p);
	float a = atan(p.y, p.x + 0.00001) - v_winkel;   // atan(0, 0) ist nicht definiert (Flockenmitte)
	// weiche Scheibe mit leicht sechsstrahligem Rand (dreht sich mit der Flocke)
	float form = r * (1.0 - 0.18 * pow(abs(cos(3.0 * a)), 3.0));
	float rand = 1.0 - smoothstep(0.25, 1.0, form);
	// Ausgabe nie NaN oder negativ (siehe regen.gdshader): Bloom und Unschärfe würden sie verschmieren
	float al = clamp(farbe.a * rand * rand * clamp(v_alpha, 0.0, 1.0), 0.0, 1.0);
	if (!(al > 0.004)) discard;
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = al;
}
