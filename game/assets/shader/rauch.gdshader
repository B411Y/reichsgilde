// Rauch (Feuer.cs; seit Phase 2 der einzige Rauch, für alle Feuer und Grafikstufen): eine senkrechte Rauchsäule aus einer einzigen Fläche, die
// der Kamera folgt. Das wandernde Rauschen (texturen/feuer/rauschen.png) steigt nach oben, die Säule wird mit der Höhe breiter, franst aus und
// löst sich auf; der Wind (globale Uniforms wetter_wind, wind_richtung) biegt sie. Am Fuß leuchtet sie vom Feuer orange, weiter oben ist sie grau
// und folgt dem Umgebungslicht („umgebung“: 0 Nacht … 1 Tag; Feuer.cs setzt es). Nachts wird sie nicht schwarz, sondern bleibt ein mattes,
// kühles Grau wie Rauch im Mondlicht (sonst verschwände der Kaminrauch nachts ganz). Ein Material je Feuerart, Startwert je Säule aus ihrer
// Lage, wie stark sie qualmt (`staerke`, Kamin an oder aus) in der Tiefe der flachen Fläche – keine Instanz-Uniforms. Mehrere Flächen mit verschiedenem Startwert ergeben eine
// dichtere Säule.
// An einer Wand (`an_wand`, Feuer.Wand): Die X-Achse der Fläche in der Welt zeigt von der Wand weg (Feuer.cs dreht sie so). Die Fläche kippt
// oben nicht in die Wand (sonst neigt sich die zur Kamera gedrehte Säule in die Mauer hinein) – so steigt der Rauch an der Wand hoch –, und der
// Wind biegt die Säule nur von der Wand weg oder an ihr entlang. Wo der Rauch eine Fläche der Szene berührt, blendet er weich aus (Tiefe der Szene).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

global uniform vec2 wind_richtung;
global uniform float wetter_wind;

uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
uniform float zeit = 0.0;
// 2 = ganz zur Kamera gedreht, 1 = senkrecht zur Kamera gedreht, 0 = fest (gekreuzte Flächen)
uniform int billboard = 2;
// 0 Nacht … 1 voller Tag: so hell ist das Grau des Rauchs
uniform float umgebung = 1.0;
// Wie dicht (0–1) und wie schnell der Rauch steigt
uniform float dichte = 0.6;
uniform float tempo = 1.0;
// Anfangsbreite der Säule unten, in Anteilen der Quadbreite (die Säule wird nach oben bis 1 breiter)
uniform float fuss_breite = 0.2;
// Wie dunkel der Rauch ist (0 = hell wie Dampf, 1 = schwarz wie Brandrauch)
uniform float russ = 0.3;
// Wie stark das Feuer den Fuß der Säule anleuchtet (0 = gar nicht, z. B. beim Kamin im Dach)
uniform float glut = 0.5;
uniform vec3 glut_farbe : source_color = vec3(1.0, 0.45, 0.1);
// Wie hoch das Feuer den Fuß der Säule anleuchtet, in Anteilen ihrer Höhe (Feuer.cs: etwa 1,5 m)
uniform float glut_zone = 0.3;
// Nachts: so hell bleibt der Rauch im Mondlicht (Grau ohne Ruß; Ruß macht ihn auch nachts dunkler)
uniform vec3 mond_farbe : source_color = vec3(0.30, 0.33, 0.40);
// Deckkraft bei Tag und bei Nacht (Faktor; Kamin: nur ein Hauch), dazwischen nach „umgebung“
uniform float deckung_tag = 1.0;
uniform float deckung_nacht = 1.0;
// Rauch an einer Wand (siehe oben)
uniform bool an_wand = false;
// Über so viele Meter blendet der Rauch vor einer Fläche der Szene aus (weiche Kante statt Schnittlinie)
uniform float weich = 0.6;
uniform sampler2D tiefe : hint_depth_texture, repeat_disable, filter_nearest;

varying float neigung;
varying float staerke;
varying flat uint saat;

// Wind am Kopf der Säule (Anteil der Säulenhöhe): bei Windstärke 0,25 (ruhiges Wetter) wie bisher gut ein Zehntel, bei Regen (0,5) ein Viertel,
// bei Gewitter (0,75) die Hälfte, bei Sturm (1) über vier Fünftel – dann liegt die Säule 40° geneigt. Dieselbe Quelle wie die Fahnen: Richtung
// wind_richtung, Stärke aus dem Wetter (wetter_wind, Stadtansicht.Wetter.cs setzt auch wind_staerke der Fahnen daraus).
float lehne_nach_wind(float w) {
	return 0.05 + 0.8 * w * w;
}

void vertex() {
	// Ohne Instanz-Uniform (sie belegten je Fläche Platz im knappen Puffer der Grafikkarte): die Stärke steckt in der Tiefe der flachen Fläche
	// (Skalierung Z / X − 1, Feuer.RauchStaerke), der Startwert kommt aus der Lage in der Welt (Rauch steht still)
	staerke = clamp(length(MODEL_MATRIX[2].xyz) / max(length(MODEL_MATRIX[0].xyz), 1e-6) - 1.0, 0.0, 1.0);
	saat = f_hash_u(uvec4(uvec3(ivec3(round(MODEL_MATRIX[3].xyz * 100.0))), 3u));
	vec3 v0 = VERTEX;
	float sx = length(MODEL_MATRIX[0].xyz);
	float sy = length(MODEL_MATRIX[1].xyz);
	float w = clamp(wetter_wind, 0.0, 1.0);

	// Jeder Schornstein ein wenig anders (Werte aus seiner Lage, kein Spielzufall): Windrichtung bis ±14° verdreht, Neigung 0,75- bis 1,25-fach,
	// eigener Takt der Böen. Bei Windstille fällt das kaum auf, im Sturm bläst es jede Säule etwas anders.
	float p1 = f_hash(uvec4(saat, 11u, 0u, 0u));
	float p2 = f_hash(uvec4(saat, 12u, 0u, 0u));
	float drehwinkel = (p1 - 0.5) * 0.5 * w;
	vec2 wind = normalize(wind_richtung + vec2(0.0001));
	wind = vec2(cos(drehwinkel) * wind.x - sin(drehwinkel) * wind.y, sin(drehwinkel) * wind.x + cos(drehwinkel) * wind.y);
	float boe = 1.0 + 0.3 * w * sin(TIME * (0.5 + 0.4 * p1) + p2 * 6.2832);
	float lehne = lehne_nach_wind(w) * (0.75 + 0.5 * p2) * boe;

	float lage = 0.0;
	if (billboard == 2) {
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, INV_VIEW_MATRIX[1] * sy, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		lage = dot(wind, INV_VIEW_MATRIX[0].xz);
		// Der Wind schiebt den Kopf der Säule in der Welt: Der Versatz (Meter) geht in die Fläche, mit der Höhe wachsend (auch auf die Kamera zu oder
		// von ihr weg: dann sinkt oder steigt der Kopf im Bild)
		vec3 d = vec3(wind.x, 0.0, wind.y) * lehne * sy * VERTEX.y;
		VERTEX.x += dot(d, INV_VIEW_MATRIX[0].xyz) / sx;
		VERTEX.y += dot(d, INV_VIEW_MATRIX[1].xyz) / sy;
	} else if (billboard == 1) {
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, MODEL_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		lage = dot(wind, INV_VIEW_MATRIX[0].xz);
		vec3 d = vec3(wind.x, 0.0, wind.y) * lehne * sy * VERTEX.y;
		VERTEX.x += dot(d, INV_VIEW_MATRIX[0].xyz) / sx;
	} else {
		vec3 achse = normalize(MODEL_MATRIX[0].xyz);
		lage = dot(wind, achse.xz);
		vec3 d = vec3(wind.x, 0.0, wind.y) * lehne * sy * VERTEX.y;
		VERTEX.x += dot(d, achse) / sx;
	}
	// Ein Rest der Biegung im Shader (der Fuß steigt gerade, der Kopf läuft aus): auch bei Windstille steigt der Rauch nicht ganz gerade
	neigung = lage * (0.1 + 0.5 * w);
	if (an_wand && billboard != 0) {
		vec2 weg = normalize(MODEL_MATRIX[0].xz + vec2(1e-6));
		vec3 rechts = INV_VIEW_MATRIX[0].xyz * sx;
		vec3 oben = billboard == 2 ? INV_VIEW_MATRIX[1].xyz * sy : vec3(0.0, sy, 0.0);
		// Die zur Kamera gedrehte Fläche kippt oben nach hinten; zeigt das in die Wand, steigt sie stattdessen senkrecht hoch (nur auf die Wand
		// projiziert neigte sie sich seitlich). Die Seiten bleiben, wo sie sind: Was seitlich in einer Wand steckt, verdeckt die Wand selbst,
		// die Kante blendet weich aus.
		float kipp = -dot(normalize(oben.xz + vec2(1e-6)), weg);
		oben.xz *= 1.0 - smoothstep(0.0, 0.35, kipp);
		// Wind: den Anteil auf die Wand zu weglassen (nur von der Wand weg oder an ihr entlang)
		vec2 wf = wind - weg * min(dot(wind, weg), 0.0);
		float hoehe = clamp(v0.y, 0.0, 1.0);
		VERTEX = MODEL_MATRIX[3].xyz + rechts * v0.x + oben * v0.y + vec3(wf.x, 0.0, wf.y) * lehne * sy * hoehe;
		MODELVIEW_MATRIX = VIEW_MATRIX;
		neigung = dot(wf, INV_VIEW_MATRIX[0].xz) * (0.1 + 0.5 * w);
	}
}

void fragment() {
	float ph = f_hash(uvec4(saat, 5u, 0u, 0u));
	float t = zeit * tempo;
	float h = 1.0 - UV.y;                      // 0 unten … 1 oben
	float x = (UV.x - 0.5) * 2.0;

	// Die Säule wird schnell breiter (Rauch quillt auf), schlängelt sich und franst am Rand aus; innen ist sie fleckig
	float breite = mix(fuss_breite, 1.0, 1.0 - pow(1.0 - h, 2.2));
	// Böiger Wind verwirbelt die Säule: ein Schlängeln mit eigener Phase je Schornstein und ein stärkeres Ausfransen (bei Windstärke 0,25 kaum zu sehen)
	float wirbel = wetter_wind * wetter_wind;
	float xl = x - neigung * h * h * 1.2 - sin(h * (4.0 + 3.0 * ph) - t * (1.0 + 2.0 * wetter_wind) + ph * 6.2832) * 0.22 * wirbel * (0.25 + h);
	float n1 = texture(rausch, vec2(xl * 0.30 + ph, h * 0.55 - t * 0.10)).r;
	float n2 = texture(rausch, vec2(xl * 0.80 + ph * 1.9 + 0.31, h * 1.30 - t * 0.17)).g;
	float n3 = texture(rausch, vec2(xl * 1.60 + ph * 2.7 + 0.62, h * 2.40 - t * 0.28)).b;

	float versatz = (n1 - 0.5) * 1.4 * h * (1.0 + 1.2 * wirbel);
	float d = 1.0 - abs(xl - versatz) / breite;
	d -= (1.0 - (0.5 * n2 + 0.5 * n3)) * 0.8 * (0.15 + h);
	float deckung = smoothstep(0.0, 0.4, d) * smoothstep(0.0, 0.05, h) * (1.0 - smoothstep(0.5, 1.0, h)) * (0.55 + 0.9 * n2 * n3 * 1.6);

	vec3 hell = vec3(0.70, 0.70, 0.72);
	vec3 dunkel = vec3(0.02, 0.02, 0.022);
	vec3 grau = mix(hell, dunkel, russ) * (0.75 + 0.5 * n2);
	// Nachts dunkler und kühler, aber nie schwarz: matt im Mondlicht, Ruß bleibt auch nachts dunkler
	vec3 nacht = mond_farbe * mix(1.0, 0.25, russ) * (0.75 + 0.5 * n2);
	grau = mix(nacht, grau, umgebung);
	// Am Fuß vom Feuer angeleuchtet
	float anteil = glut * (1.0 - smoothstep(0.0, glut_zone, h)) * (1.0 - 0.6 * umgebung);
	vec3 farbe = mix(grau, glut_farbe, anteil * 0.8);

	ALBEDO = farbe;
	// Weiche Kante, wo der Rauch eine Fläche der Szene schneidet (Wand, Dach): Abstand zur Szene dahinter in Blickrichtung
	float tief = texture(tiefe, SCREEN_UV).x;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
	vec4 sicht = INV_PROJECTION_MATRIX * vec4(vec3(SCREEN_UV, tief) * 2.0 - 1.0, 1.0);
#else
	vec4 sicht = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, tief, 1.0);
#endif
	float vorn = VERTEX.z - sicht.z / sicht.w;
	float kante = clamp(vorn / weich, 0.0, 1.0);

	ALPHA = clamp(deckung * dichte * staerke * mix(deckung_nacht, deckung_tag, umgebung) * kante, 0.0, 0.95);
}
