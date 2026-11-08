// Kaminrauch aus Ballen (Kaminrauch.cs): Jeder Schornstein sendet in einem Kreislauf rund ein Dutzend weicher, runder Rauchballen aus, jeder ein
// eigenes Viereck, das der Kamera folgt. Alle Schornsteine der Stadt sind EIN MultiMesh mit EINEM Material (ein Zeichenaufruf); die Instanz ist der
// Schornstein, die Ballen sind Vierecke in seinem Netz (Vertexfarbe = Nummer des Ballens und drei Zufallszahlen). Der Ort jedes Ballens folgt in der
// Welt allein aus seinem Alter (kein Zustand, nichts wird gespeichert):
//  * Aufsteigen: Auftrieb der warmen Luft, klingt ab (Höhe = v0 · τ · (1 − e^(−t/τ)) + ein kleiner Rest) – erst ein Stück senkrecht,
//  * Abknicken: der Wind nimmt den Ballen seitwärts mit, mit Trägheit (Weg = U · (t − τ · (1 − e^(−t/τ)))): je stärker der Wind, desto kürzer τ,
//    desto früher und flacher knickt die Fahne ab; bei Windstille steht eine hohe, leicht wabernde Säule,
//  * Wachsen: der Ballen wird beim Aufsteigen größer (Mitreißen von Luft), dreht sich, franst am Rand aus und wird dünn und hell, bis er verschwindet.
// Licht: oben hell (die Sonne steht hoch), unten und im Inneren dunkler (Dicke), jung dichter und dunkler, alt luftig und hell; nachts ein mattes Grau
// im Mondlicht, am Fuß leicht rötlich von der Glut. Je Schornstein (Instanzdaten): Stärke (Feuer an/aus, weich), Menge (dünn bis dick), Ruß, Saat.
// Wetter: bei Nässe sinkt der Auftrieb (der Rauch drückt), im Winter ist er etwas sichtbarer.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

global uniform vec2 wind_richtung;
global uniform float wetter_wind;
global uniform float wetter_naesse;
global uniform vec4 jahreszeit_anteile;   // Frühling, Sommer, Herbst, Winter

uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
uniform sampler2D tiefe : hint_depth_texture, repeat_disable, filter_nearest;
uniform float zeit = 0.0;
// 0 Nacht … 1 Tag
uniform float umgebung = 1.0;
// 0 Niedrig (eine Rauschprobe, einfache Tönung), 1 Mittel (zwei Proben, Selbstschatten), 2 Hoch (drei Proben, weiche Kante zur Szene)
uniform int stufe = 2;
// Sekunden, bis ein Ballen vom Schornstein bis zum Verschwinden gestiegen ist
uniform float lebensdauer = 11.0;
uniform vec3 mond_farbe : source_color = vec3(0.30, 0.33, 0.40);

varying float alter;
varying float zufall;
varying float dichte;
varying float russ;
varying float radius;
varying float glut;
varying vec2 ecke;

float h11(float x) {
	return fract(sin(x * 127.1) * 43758.5453);
}

void vertex() {
	// Die Daten des Schornsteins stecken in der Matrix der Instanz (INSTANCE_CUSTOM zeichnete in Godot 4.7.2 mit diesem Netz nichts): Stärke in
	// der x-Achse (x), Saat (y), Menge in der y-Achse (y), Ruß in der z-Achse (z); der Ort ist die Verschiebung
	float st = MODEL_MATRIX[0].x;
	float menge = MODEL_MATRIX[1].y;
	russ = MODEL_MATRIX[2].z;
	float saat = MODEL_MATRIX[0].y;
	float idx = COLOR.r;

	float w = clamp(wetter_wind, 0.0, 1.0);
	float nass = clamp(wetter_naesse, 0.0, 1.0);

	// Wind in m/s; jeder Schornstein ein wenig anders gerichtet und mit eigenem Takt der Böen
	float dreh = (saat - 0.5) * 0.5 * w;
	vec2 wd = normalize(wind_richtung + vec2(0.0001));
	wd = vec2(cos(dreh) * wd.x - sin(dreh) * wd.y, sin(dreh) * wd.x + cos(dreh) * wd.y);
	float boe = 1.0 + 0.35 * w * sin(zeit * (0.4 + 0.3 * saat) + saat * 40.0);
	float u = (0.10 + 3.4 * w * w) * boe;

	// Alter dieses Ballens: alle Ballen eines Schornsteins sind gleichmäßig über den Kreislauf verteilt. Im Wind ist der Kreislauf kürzer (der Wind
	// trägt die Ballen schnell fort und mischt sie mit Luft): so bleiben sie dicht hintereinander, die Fahne reißt nicht in einzelne Bälle auf
	float leben_s = lebensdauer / (1.0 + 0.55 * u);
	float z = zeit / leben_s + idx + saat * 7.0;
	float zyklus = floor(z);
	alter = fract(z);
	float t = alter * leben_s;
	// Zufall je Ballen und Durchlauf (Größe, Versatz, Drehung): jeder Ballen jedes Mal ein wenig anders
	float r1 = h11(zyklus * 13.7 + COLOR.g * 91.3 + saat * 57.0 + 1.0);
	float r2 = h11(zyklus * 7.3 + COLOR.b * 53.1 + saat * 31.0 + 2.0);
	float r3 = h11(zyklus * 3.1 + COLOR.a * 17.7 + saat * 83.0 + 3.0);
	zufall = r1;

	// Aufsteigen: warme Luft; dicker, heißer Rauch steigt schneller; Nässe und Wind drücken ihn nieder
	float v0 = (0.95 + 0.7 * menge) * (1.0 - 0.35 * nass) / (1.0 + 0.45 * u);
	float ty = 3.2 * (1.0 - 0.3 * nass);
	float hoehe = v0 * ty * (1.0 - exp(-t / ty)) + 0.12 * t;
	// Abknicken: der Wind nimmt den Ballen mit Trägheit mit
	float tx = 2.4 / (1.0 + 1.2 * u);
	float drift = u * (t - tx * (1.0 - exp(-t / tx)));

	// Waberndes Schlängeln: langsam wechselnd mit der Entstehungszeit (benachbarte Ballen folgen einander, die Fahne schlängelt sich),
	// dazu ein kleiner Zufallsversatz je Ballen
	float amp = (0.04 + 0.34 * alter) * (0.7 + 0.5 * menge) * (1.0 + 1.1 * w);
	float geb = zeit - t;
	vec2 schlaengel = vec2(sin(geb * 0.55 + saat * 6.28), sin(geb * 0.43 + saat * 9.1 + 1.3)) * amp * 1.3;
	vec2 versatz = (vec2(r1, r2) - 0.5) * amp * 1.2;

	float groesse = mix(0.8, 1.4, menge) * (0.6 + 0.4 * st) * (0.82 + 0.36 * r3);
	float r0 = 0.26 * groesse;
	radius = r0 * (1.0 + 3.4 * pow(alter, 0.6));

	vec3 p = MODEL_MATRIX[3].xyz + vec3(0.0, 0.18 + hoehe, 0.0)
		+ vec3(wd.x, 0.0, wd.y) * drift + vec3(schlaengel.x + versatz.x, 0.0, schlaengel.y + versatz.y);

	// Fläche zur Kamera gedreht, Ballen am Ort p; Viereck 1,4 Radien breit (Platz für den ausgefransten Rand)
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(p, 1.0));
	ecke = VERTEX.xy;
	VERTEX = vec3(VERTEX.xy * radius * 1.4, 0.0);

	dichte = st * mix(0.40, 0.68, menge);
	// Am Fuß leuchtet die Glut durch den Rauch (nur nachts sichtbar): mehr bei dickem Rauch
	glut = (1.0 - smoothstep(0.0, 0.22, alter)) * (0.35 + 0.65 * menge);
	// POSITION immer setzen (wer es nur manchmal setzt, bekommt sonst gar keine Position); Feuer aus: außerhalb des Bildes, nichts zeichnen
	POSITION = st < 0.004 ? vec4(2.0, 2.0, 2.0, 1.0) : PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
}

void fragment() {
	float ph = zufall;
	float t = zeit;
	// Ballen drehen sich langsam, jeder anders herum
	float winkel = ph * 6.2832 + alter * (ph - 0.5) * 5.0;
	vec2 q = ecke * 1.4;
	float rr = length(q);
	vec2 qd = vec2(cos(winkel) * q.x - sin(winkel) * q.y, sin(winkel) * q.x + cos(winkel) * q.y);
	vec2 o = vec2(ph * 3.1, ph * 7.7 + 0.37) + vec2(t * 0.012, -t * 0.02);

	float n1 = texture(rausch, qd * 0.28 + o).r;
	float n2 = 0.5;
	float n3 = 0.5;
	if (stufe >= 1) n2 = texture(rausch, qd * 0.62 + o * 1.7 + 0.31).g;
	if (stufe >= 2) n3 = texture(rausch, qd * 1.25 + o * 2.3 + 0.62).b;
	float nz = stufe >= 2 ? 0.5 * n1 + 0.3 * n2 + 0.2 * n3 : (stufe == 1 ? 0.65 * n1 + 0.35 * n2 : n1);

	// Dichte: rund und in der Mitte am dichtesten; der Rand franst mit dem Alter stärker aus und zieht sich zusammen
	float kern = 1.0 - rr;
	float d = kern + (nz - 0.5) * (0.85 + 1.1 * alter) - 0.30 * alter * alter;
	float form = smoothstep(-0.02, 0.6, d);

	// Deckung über das Leben: weich einsetzen, nach oben ausdünnen
	float leben = smoothstep(0.0, 0.07, alter) * (1.0 - smoothstep(0.45, 1.0, alter));
	float jahr = jahreszeit_anteile.w;
	float nass = clamp(wetter_naesse, 0.0, 1.0);
	float tagnacht = mix(0.3, 1.0, umgebung);   // nachts kaum zu sehen
	float a = form * leben * dichte * tagnacht * (1.0 + 0.3 * jahr);

	// Licht: Kugel aus der Fläche, Sonne von oben und leicht von vorn; jung und dick = innen dunkler, alt und dünn = hell
	vec3 nrm = normalize(vec3(q.x, q.y, sqrt(max(0.05, 1.0 - rr * rr))) + vec3(0.0, 0.0, 0.0));
	float hell = 0.5 + 0.5 * dot(nrm, normalize(vec3(0.20, 0.85, 0.48)));
	if (stufe >= 1) hell = clamp(hell + (n2 - 0.5) * 0.5, 0.0, 1.0);
	float innen = 1.0 - 0.45 * form * (1.0 - alter);
	float schatten = mix(0.5, 1.1, hell) * innen * mix(0.9, 1.0, alter);

	vec3 grund = mix(vec3(0.60, 0.60, 0.62), vec3(0.07, 0.07, 0.08), russ);
	grund = mix(grund, grund * vec3(0.92, 0.96, 1.06), jahr);   // Frost: eine Spur kühler (und dichter, siehe oben)
	vec3 tag = grund * schatten * (1.0 - 0.2 * nass);
	vec3 nacht = mond_farbe * 0.7 * mix(1.0, 0.3, russ) * mix(0.7, 1.1, hell) * mix(0.9, 1.0, alter);
	vec3 farbe = mix(nacht, tag, umgebung);

	float nachts = 1.0 - umgebung;
	float gl = glut * nachts * (1.0 - clamp(rr, 0.0, 1.0)) * (1.0 - hell * 0.5);
	farbe = mix(farbe, vec3(1.0, 0.42, 0.12) * 0.9, clamp(gl * 0.6, 0.0, 0.7));
	a += gl * 0.2 * form * leben;

	ALBEDO = farbe;

	float kante = 1.0;
	if (stufe >= 2) {
		// Weiche Kante, wo der Ballen eine Fläche der Szene schneidet (Schornstein, Dach)
		float tief = texture(tiefe, SCREEN_UV).x;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
		vec4 sicht = INV_PROJECTION_MATRIX * vec4(vec3(SCREEN_UV, tief) * 2.0 - 1.0, 1.0);
#else
		vec4 sicht = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, tief, 1.0);
#endif
		kante = clamp((VERTEX.z - sicht.z / sicht.w) / max(radius * 0.7, 0.05), 0.0, 1.0);
	}
	ALPHA = clamp(a * kante, 0.0, 0.9);
}
