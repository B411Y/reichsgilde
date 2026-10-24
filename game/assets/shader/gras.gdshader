// Grasteppich (Kartenwerkstatt, Schritt 5i; Stadtansicht.Gras.cs): kleine 3D-Halme auf der Wiese, nur im Bild.
// Gezeichnet werden Felder zu 4 × 4 m (MultiMesh je Geländekachel, nur Felder mit Wiese im Bild); jedes Feld ist dasselbe Netz aus Büscheln,
// je Feld gespiegelt und gedreht. Ob ein Büschel steht und wie hoch, rechnet dieser Shader aus der Welt:
//   - grasbild (je Kachel, 1 m wie die Gewichte des Bodens): R Dichte der Wiese (ohne Gebäude, Grundstücke, Bauzonen, Äcker, Wasser, Fels am
//     Hang und Pflaster der Stadt), G Höhe des Bodens, B und A die Normale (x, z); grasfarbe: die Makro-Flecken und die Horste;
//   - die Wegeschicht (Schritt 5g) für die Ränder der Wege mit demselben Rauschen wie im Boden-Shader: am Weg kurz und niedergetreten;
//   - der Zoom (orthografische Kamera): Weit weg sind die Büschel breiter (ein Halm mindestens etwa ein Bildpunkt) und höher, und es stehen umso
//     weniger, je weiter die Kamera weg ist (etwa gleich viele im Bild), höchstens dichte_max je m²; zum oberen und unteren Bildrand lichter;
//   - außen um den Blickpunkt werden sie weich niedriger (jenseits des Bildrands).
// Was der Teppich kostet, hängt fast nur an der Zahl der Ecken und Dreiecke – darum die kleinen Büschel, die geteilten Fußpunkte und die
// vorgerechneten Bilder. depth_draw_never: Die Halme gehen nicht in den Tiefen-Vorlauf (sie liegen ohnehin auf dem Boden, der Tiefe schreibt).
//
// Aufbau des Netzes (Stadtansicht.Gras.cs, GrasNetze): Ein Büschel ist ein Kranz aus drei Halmen; die Fußpunkte liegen auf einem Kreis und
// gehören je zwei Nachbarhalmen (ein Dreieck je Halm: zwei Fußpunkte und die Spitze darüber, mit Index – so rechnet die Grafikkarte nur zwei
// Ecken je Halm). UV2 = Wurzel des Büschels im Feld; VERTEX.xz = Wurzel plus Richtung auf dem Kreis (Halbmesser etwa 1, wird mit dem Zoom
// skaliert), VERTEX.y = 0 am Fuß bzw. der Anteil der Höhe an der Spitze; NORMAL = Richtung nach außen; UV.x = Neigung der Spitze (am Fuß 0);
// UV.y = ab welcher Dichte (Büschel je m²) das Büschel erscheint.
shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled, depth_draw_never;

#include "res://assets/shader/boden_gemein.gdshaderinc"

uniform sampler2D grasbild : filter_linear, repeat_disable;
// Welt-X/Z des ersten Punktes im Bild (eine Kachel minus 1 m) und Bildgröße in Punkten
uniform vec2 ursprung = vec2(0.0);
uniform float bildgroesse = 130.0;
// Wegeschicht der Kachel (wie im Boden-Shader)
uniform bool wege_an = false;
uniform sampler2D wege : filter_linear, repeat_disable;
uniform sampler2D wege_info : filter_linear, repeat_disable;
uniform vec2 wege_ursprung = vec2(0.0);
// je Kachel wie grasbild (RGBA8): RGB die Makro-Flecken der Wiese als Faktor auf ihre Farbe (halbiert), A die Horste (gras_horst) – einmal je Punkt
// in Stadtansicht.Gras.cs gerechnet statt in jeder Ecke
uniform sampler2D grasfarbe : filter_linear, repeat_disable;
// Farbe der Wiese (Mittel der Textur)
uniform vec3 wiese_farbe : source_color = vec3(0.32, 0.4, 0.18);
// Büschel je m² beim Standard-Zoom (95) und höchstens; Höhe der Halme in Metern
uniform float dichte = 6.0;
uniform float dichte_max = 32.0;
uniform float hoehe = 0.3;

const float FELD = 4.0;
const float WEG_TEXEL = 0.25;
const float WEG_BILD = 514.0;
const float WEG_INFO = 130.0;
const float STANDARD_ZOOM = 95.0;
// Halbmesser des Kranzes, in dem die Halme eines Büschels wurzeln (Meter), mindestens (wächst mit dem Zoom); so breit ist etwa ein Halm am Fuß
const float HALM_KRANZ = 0.018;

varying vec3 farbe;
varying float spitze;
varying vec3 gras_n;

void vertex() {
	// Ecke des Feldes in der Welt (die Kopie des MultiMesh trägt nur eine Verschiebung)
	vec3 o = MODEL_MATRIX[3].xyz;
	// je Feld eine von acht Spiegelungen und Drehungen: kein Feld gleicht dem Nachbarn
	int sym = int(hash(floor(o.xz / FELD) + vec2(17.0, 29.0)) * 8.0);
	vec2 wl = UV2 - FELD * 0.5;
	vec2 vl = VERTEX.xz - UV2;
	vec2 nl = NORMAL.xz;
	if ((sym & 1) != 0) { wl.x = -wl.x; vl.x = -vl.x; nl.x = -nl.x; }
	if ((sym & 2) != 0) { wl.y = -wl.y; vl.y = -vl.y; nl.y = -nl.y; }
	if ((sym & 4) != 0) { wl = wl.yx; vl = vl.yx; nl = nl.yx; }
	vec2 w = o.xz + FELD * 0.5 + wl; // Wurzel des Büschels (Welt-X/Z)

	// Zoom: Höhe des Bildes in Metern (orthografisch) und ein Bildpunkt in Metern
	float groesse = 2.0 / abs(PROJECTION_MATRIX[1][1]); // abs: unter Vulkan ist die y-Achse gespiegelt
	float pixel = groesse / VIEWPORT_SIZE.y;
	float ziel = min(dichte * (STANDARD_ZOOM / groesse) * (STANDARD_ZOOM / groesse), dichte_max);
	vec4 g = textureLod(grasbild, (w - ursprung + vec2(0.5)) / bildgroesse, 0.0);
	// zum oberen und unteren Bildrand hin lichter (dort wird das Bild mit der Miniatur-Unschärfe ohnehin weich): spart die Hälfte dort
	float bild_y = (VIEW_MATRIX * vec4(w.x, g.g, w.y, 1.0)).y / (groesse * 0.5); // −1 unten … 1 oben (weiter weg)
	ziel *= mix(1.0, 0.35, smoothstep(0.2, 0.8, bild_y)) * mix(1.0, 0.5, smoothstep(-0.65, -0.95, bild_y));
	float zeigen = 1.0 - smoothstep(ziel * 0.75, ziel, UV.y);

	float da = smoothstep(0.22, 0.45, g.r) * zeigen;
	float tritt = 1.0; // am Weg niedergetreten: kürzer und gelblicher

	// ── Ränder der Wege: dieselbe Rechnung wie weg() in boden.gdshaderinc ──
	if (wege_an && da > 0.0) {
		// in der Stadt (Pflaster als Grund) kein Gras
		vec4 info = textureLod(wege_info, (w - wege_ursprung + vec2(1.0)) / WEG_INFO, 0.0);
		da *= 1.0 - smoothstep(0.3, 0.6, info.b);
		vec4 ws = textureLod(wege, (w - wege_ursprung + vec2(WEG_TEXEL)) / (WEG_BILD * WEG_TEXEL), 0.0);
		float e = ws.g * 8.0 - 6.0;
		if (e < 1.9 && da > 0.0) {
			float st = ws.b * 4.0;
			float halb = max(ws.r * 10.0 - e, 0.3);
			float n1 = rausch(w * 0.8) - 0.5;
			float n2 = rausch(w * 3.3) - 0.5;
			float n3 = rausch(w * 11.0) - 0.5;
			float w_pd = max(0.0, 1.0 - abs(st));
			float w_fa = max(0.0, 1.0 - abs(st - 1.0));
			float w_ki = max(0.0, 1.0 - abs(st - 2.0));
			float w_pf = max(0.0, 1.0 - abs(st - 3.0));
			float w_bs = max(0.0, 1.0 - abs(st - 4.0));
			// Trampelpfad: Gras bis an den ausgetretenen Streifen, daneben niedergetreten
			float en = e + n1 * 0.55 + n2 * 0.25 + n3 * 0.08;
			float streifen = halb * (1.0 - mix(0.55, 0.95, info.r));
			float steht = smoothstep(-streifen - 0.05, -streifen + 0.2, en) * w_pd;
			float kurz = mix(0.3, 1.0, smoothstep(-0.2, 0.8, en)) * w_pd;
			// Fahrweg
			en = e + n1 * 0.5 + n2 * 0.22 + n3 * 0.08;
			steht += smoothstep(-0.12, 0.12, en) * w_fa;
			kurz += mix(0.35, 1.0, smoothstep(-0.1, 0.9, en)) * w_fa;
			// Kies: knapp außerhalb, zwischen den verstreuten Steinchen niedrig
			en = e + n1 * 0.25 + n2 * 0.15 + n3 * 0.12;
			steht += smoothstep(0.0, 0.25, en) * w_ki;
			kurz += mix(0.5, 1.0, smoothstep(0.0, 0.7, en)) * w_ki;
			// Pflaster: hinter den Randsteinen und dem dunklen Saum
			steht += smoothstep(0.08, 0.25, e) * w_pf;
			kurz += mix(0.6, 1.0, smoothstep(0.1, 0.6, e)) * w_pf;
			// Baustelle: aufgewühlt
			en = e + n1 * 0.4 + n2 * 0.2;
			steht += smoothstep(0.2, 0.45, en) * w_bs;
			kurz += mix(0.5, 1.0, smoothstep(0.25, 0.8, en)) * w_bs;
			da *= steht;
			tritt = kurz;
		}
	}

	// Zufall je Büschel (aus seinem Ort in der Welt)
	float z1 = hash(w * 53.0);
	float z2 = hash(w * 53.0 + vec2(19.0, 7.0));
	float z3 = hash(w * 53.0 + vec2(5.0, 41.0));

	// außen um den Blickpunkt (jenseits des Bildrands) weich niedriger: der Blickpunkt ist, wo die Mitte des Bildes den Boden trifft
	vec3 kamera = INV_VIEW_MATRIX[3].xyz;
	vec3 vor = -INV_VIEW_MATRIX[2].xyz;
	vec2 blick = kamera.xz + vor.xz * ((g.g - kamera.y) / min(vor.y, -0.05));
	float weit = groesse * 0.5 * length(vec2(VIEWPORT_SIZE.x / VIEWPORT_SIZE.y, 1.0 / max(-vor.y, 0.2))) + 8.0;
	float aussen = 1.0 - smoothstep(weit, weit * 1.2, distance(w, blick));

	// Höhe: Horste dichteren, höheren Grases (etwa 3 m) und dazwischen kurzes, lichtes; je Büschel verschieden; am Rand der Wiese und am Weg niedriger
	vec4 gf = textureLod(grasfarbe, (w - ursprung + vec2(0.5)) / bildgroesse, 0.0);
	float horst = gf.a;
	da *= mix(0.6, 1.0, horst);
	float h = hoehe * mix(0.55, 1.2, horst) * mix(0.55, 1.3, z2) * tritt * mix(0.55, 1.0, smoothstep(0.35, 0.75, g.r)) * aussen;
	if (z1 >= da || h < 0.02) {
		// nicht da: alle drei Ecken auf einen Punkt (keine Fläche, nichts zu zeichnen)
		VERTEX = vec3(w.x, g.g - 1.0, w.y) - o;
		farbe = vec3(0.0);
		spitze = 0.0;
		gras_n = vec3(0.0, 1.0, 0.0);
	} else {
		vec3 bn = vec3(g.b, sqrt(max(1.0 - g.b * g.b - g.a * g.a, 0.0)), g.a);
		// Weit weg wird das Büschel breiter (ein Halm ist am Fuß etwa so breit wie der Kranz weit: mindestens anderthalb Bildpunkte, sonst fielen
		// die Halme zwischen die Bildpunkte) und etwas höher: Aus Spielhöhe sieht man nicht einzelne Halme, sondern Büschel mit hellen Spitzen
		// und dunklem Grund.
		float kranz = max(HALM_KRANZ, pixel);
		h *= mix(1.0, 1.5, smoothstep(0.03, 0.1, pixel));
		vec2 fuss = w + vl * kranz;
		bool unten = VERTEX.y < 0.001;
		vec3 p;
		if (unten) {
			vec2 d = fuss - w;
			// auf der Fläche des Bodens (am Hang), etwas eingesunken
			p = vec3(fuss.x, g.g - (bn.x * d.x + bn.z * d.y) / bn.y - 0.03, fuss.y);
			spitze = 0.0;
		} else {
			// Wind: Böen ziehen über die Wiese, jeder Halm schwingt etwas anders
			vec2 wind = vec2(0.928, 0.371);
			float boe = 0.5 + 0.5 * sin(dot(w, wind) * 0.25 - TIME * 0.9) * sin(dot(w, vec2(-wind.y, wind.x)) * 0.11 + TIME * 0.3);
			float schwing = sin(TIME * 1.9 + dot(w, vec2(0.7, 0.45)) + z2 * 6.283);
			vec2 wv = wind * (0.25 + 0.75 * boe) * (0.6 + 0.4 * schwing) * 0.22;
			vec2 neigung = nl * UV.x + wv;
			vec2 q = fuss + neigung * h;
			vec2 d = fuss - w;
			p = vec3(q.x, g.g - (bn.x * d.x + bn.z * d.y) / bn.y + h * VERTEX.y * sqrt(max(1.0 - dot(neigung, neigung), 0.3)), q.y);
			spitze = 1.0;
		}
		VERTEX = p - o;
		// Licht: halb wie der Boden, halb nach außen, wohin der Halm zeigt – so fängt ein Teil der Halme die Sonne und ein Teil liegt im Schatten;
		// beide Seiten gleich (siehe fragment)
		vec3 n = normalize(bn + vec3(nl.x, 0.0, nl.y) * 0.5);
		NORMAL = n;
		gras_n = (VIEW_MATRIX * vec4(n, 0.0)).xyz;
		// Farbe: die Wiese mit den Flecken des Bodens, je Büschel etwas heller, dunkler oder gelblicher; niedergetreten gelblich wie am Weg
		vec3 c = wiese_farbe * gf.rgb * 2.0;
		c *= mix(0.84, 1.12, z3) * vec3(mix(1.0, 1.08, z1 / max(da, 0.001)), 1.0, mix(1.0, 0.85, z2 * z2));
		c = mix(c * vec3(0.9, 0.86, 0.68), c, smoothstep(0.3, 0.9, tritt));
		farbe = c;
	}
}

void fragment() {
	// am Grund dunkel (die Halme verdecken einander), an den Spitzen hell und etwas gelblich (von der Sonne gebleicht)
	ALBEDO = mix(farbe * 0.42, farbe * vec3(1.34, 1.32, 1.0), smoothstep(0.0, 0.7, spitze));
	// dieselbe Normale für Vorder- und Rückseite (sonst wäre eine Seite jedes Halms dunkel)
	NORMAL = normalize(gras_n);
	ROUGHNESS = 0.85;
	SPECULAR = 0.3;
}
