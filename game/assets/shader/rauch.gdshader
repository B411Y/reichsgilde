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

void vertex() {
	// Ohne Instanz-Uniform (sie belegten je Fläche Platz im knappen Puffer der Grafikkarte): die Stärke steckt in der Tiefe der flachen Fläche
	// (Skalierung Z / X − 1, Feuer.RauchStaerke), der Startwert kommt aus der Lage in der Welt (Rauch steht still)
	staerke = clamp(length(MODEL_MATRIX[2].xyz) / max(length(MODEL_MATRIX[0].xyz), 1e-6) - 1.0, 0.0, 1.0);
	saat = f_hash_u(uvec4(uvec3(ivec3(round(MODEL_MATRIX[3].xyz * 100.0))), 3u));
	float lage = 0.0;
	if (billboard == 2) {
		float sx = length(MODEL_MATRIX[0].xyz);
		float sy = length(MODEL_MATRIX[1].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, INV_VIEW_MATRIX[1] * sy, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		lage = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz);
	} else if (billboard == 1) {
		float sx = length(MODEL_MATRIX[0].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, MODEL_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		lage = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz);
	} else {
		lage = dot(normalize(wind_richtung + vec2(0.0001)), normalize(MODEL_MATRIX[0].xyz).xz);
	}
	// Auch bei Windstille steigt der Rauch nicht ganz gerade
	neigung = lage * (0.15 + 1.4 * wetter_wind);
	if (an_wand && billboard != 0) {
		vec2 weg = normalize(MODEL_MATRIX[0].xz + vec2(1e-6));
		vec3 rechts = INV_VIEW_MATRIX[0].xyz * length(MODEL_MATRIX[0].xyz);
		vec3 oben = billboard == 2 ? INV_VIEW_MATRIX[1].xyz * length(MODEL_MATRIX[1].xyz) : vec3(0.0, length(MODEL_MATRIX[1].xyz), 0.0);
		// Die zur Kamera gedrehte Fläche kippt oben nach hinten; zeigt das in die Wand, steigt sie stattdessen senkrecht hoch (nur auf die Wand
		// projiziert neigte sie sich seitlich). Die Seiten bleiben, wo sie sind: Was seitlich in einer Wand steckt, verdeckt die Wand selbst,
		// die Kante blendet weich aus.
		float kipp = -dot(normalize(oben.xz + vec2(1e-6)), weg);
		oben.xz *= 1.0 - smoothstep(0.0, 0.35, kipp);
		VERTEX = MODEL_MATRIX[3].xyz + rechts * VERTEX.x + oben * VERTEX.y;
		MODELVIEW_MATRIX = VIEW_MATRIX;
		// Wind: den Anteil auf die Wand zu weglassen
		vec2 w = normalize(wind_richtung + vec2(0.0001));
		w -= weg * min(dot(w, weg), 0.0);
		neigung = dot(w, INV_VIEW_MATRIX[0].xz) * (0.15 + 1.4 * wetter_wind);
	}
}

void fragment() {
	float ph = f_hash(uvec4(saat, 5u, 0u, 0u));
	float t = zeit * tempo;
	float h = 1.0 - UV.y;                      // 0 unten … 1 oben
	float x = (UV.x - 0.5) * 2.0;

	// Die Säule wird schnell breiter (Rauch quillt auf), schlängelt sich und franst am Rand aus; innen ist sie fleckig
	float breite = mix(fuss_breite, 1.0, 1.0 - pow(1.0 - h, 2.2));
	float xl = x - neigung * h * h * 1.2;
	float n1 = texture(rausch, vec2(xl * 0.30 + ph, h * 0.55 - t * 0.10)).r;
	float n2 = texture(rausch, vec2(xl * 0.80 + ph * 1.9 + 0.31, h * 1.30 - t * 0.17)).g;
	float n3 = texture(rausch, vec2(xl * 1.60 + ph * 2.7 + 0.62, h * 2.40 - t * 0.28)).b;

	float versatz = (n1 - 0.5) * 1.4 * h;
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
