// Pixel-Look (Testszene Pixeltest), Runde 2: ein Durchgang über das FERTIGE UNDURCHSICHTIGE Bild (Himmel, Boden, Häuser, Figuren) in voller Auflösung.
// Ein Vollbild-Viereck vor der Kamera mit der kleinsten Zeichenreihenfolge der durchsichtigen Teile: Wasser, Feuer und Nebel werden danach darüber
// gezeichnet und behalten ihre eigene Behandlung. Der Himmel (Tiefe 0) wird nicht angefasst: keine Bänder im Himmel.
// Bausteine (einzeln schaltbar), alle in Oklab gerechnet, damit der Farbton erhalten bleibt (Vorbild: Referenzbild des Nutzers, 03.11.2026):
//   1. Stufenlicht: Helligkeit in 3 Stufen, harte Schattengrenze; der Schatten wird violett-kühl verschoben, nicht grau.
//   2. Palette: warm und entsättigt (Farbanteil mal Faktor, Verschiebung zu Gelb), Farbanteil und Farbton in wenigen Stufen.
//   3. Dithering (Standard aus): Bayer 8x8 am Bildschirm oder an den Weltrasterzellen.
//   4. Texelflecken: Raster aus Texeln der Weltposition (Größe in Bildpunkten wählbar); wenige Texel je Fläche etwas heller oder dunkler.
//      Texelmuster blenden aus, wenn ein Texel kleiner als ~1,5 Bildpunkte wird (Moiré).
//   5. Kanten statt schwarzem Umriss: aus Tiefe und Normalen; Kanten zur Sonne hin und nach oben heller, auf der Schattenseite dunkler.
//      Gras ist ausgenommen (Normalen nach oben, kleiner Tiefensprung).
// Eigenbau (MIT); das Bayer-Muster ist die bekannte Rekursion (gemeinfrei), Oklab nach Björn Ottosson (gemeinfrei).
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, skip_vertex_transform, blend_mix;

uniform sampler2D bild : hint_screen_texture, filter_nearest, repeat_disable;
uniform sampler2D tiefe : hint_depth_texture, filter_nearest;
uniform sampler2D normalen : hint_normal_roughness_texture, filter_nearest;

uniform float ortho_groesse = 0.0;     // vom Skript: sichtbare Höhe in Metern bei orthografischer Kamera, sonst 0
uniform float fov_faktor = 1.0;        // vom Skript: 2 * tan(Sichtfeld / 2) bei perspektivischer Kamera
uniform bool stufen_an = false;
uniform int stufen = 4;                // Helligkeitsstufen
uniform float l_schatten = 0.42;      // Helligkeit der Schattenstufe
uniform float l_max = 0.8;             // oberhalb dieser Helligkeit bleibt der Wert unverändert (Glanzlichter)
uniform int palette = 0;               // 0 aus, 1 RGB-Stufen, 2 Oklab
uniform int pal_l = 12;                // Stufen für L (Oklab) bzw. je Kanal (RGB)
uniform int pal_c = 4;                 // Stufen für den Farbanteil (Oklab)
uniform int pal_h = 24;                // Stufen für den Farbton (Oklab)
uniform float sattel = 0.8;            // Farbanteil-Faktor (entsättigen)
uniform vec2 waerme = vec2(0.01, 0.03);// Verschiebung in Oklab a/b (Richtung Gelb/Orange)
uniform vec2 schatten_ton = vec2(0.035, -0.06); // Schatten: Verschiebung zu Violett
uniform vec3 sonne_richtung = vec3(0.4, -0.7, 0.5); // Richtung, in die das Licht fällt (Welt)
uniform int dither = 0;                // 0 aus, 1 Bildschirm, 2 Welt
uniform int dither_punkt = 1;
uniform float dither_staerke = 1.0;
uniform float blende_von = 1.4;     // Muster blenden aus, wenn ein Texel weniger Bildpunkte hat
uniform bool koernung_an = false;
uniform float koernung = 0.07;
uniform float texel_je_meter = 16.0;
uniform bool texel_auto = false;       // orthografisch: Texel wächst in Zweierstufen mit dem Zoom, bleibt ~3 Bildpunkte groß
uniform float texel_ziel_px = 3.0;      // gewünschte Texelgröße in Bildpunkten (orthografisch)
uniform bool umriss_an = false;
uniform float kante_hell = 0.13;       // Aufhellung einer Kante zur Sonne
uniform float kante_dunkel = 0.12;     // Abdunkelung auf der Schattenseite
uniform float tiefenschwelle = 0.08;   // perspektivisch: Anteil der Entfernung
uniform float tiefenschwelle_ortho = 0.02; // orthografisch: Anteil der sichtbaren Höhe
uniform float normalenschwelle = 0.9;

float bayer8(ivec2 p) {
	int x = p.x & 7;
	int y = p.y & 7;
	int v = 0;
	for (int i = 0; i < 3; i++) {
		int bx = (x >> i) & 1;
		int by = ((x ^ y) >> i) & 1;
		v = v * 4 + by * 2 + bx;
	}
	return (float(v) + 0.5) / 64.0;
}

float hash(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

vec3 cbrt3(vec3 x) { return sign(x) * pow(abs(x), vec3(1.0 / 3.0)); }

vec3 zu_oklab(vec3 c) {
	vec3 lms = vec3(
		0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b,
		0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b,
		0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b);
	lms = cbrt3(lms);
	return vec3(
		0.2104542553 * lms.x + 0.7936177850 * lms.y - 0.0040720468 * lms.z,
		1.9779984951 * lms.x - 2.4285922050 * lms.y + 0.4505937099 * lms.z,
		0.0259040371 * lms.x + 0.7827717662 * lms.y - 0.8086757660 * lms.z);
}

vec3 aus_oklab(vec3 o) {
	vec3 lms = vec3(
		o.x + 0.3963377774 * o.y + 0.2158037573 * o.z,
		o.x - 0.1055613458 * o.y - 0.0638541728 * o.z,
		o.x - 0.0894841775 * o.y - 1.2914855480 * o.z);
	lms = lms * lms * lms;
	return vec3(
		4.0767416621 * lms.x - 3.3077115913 * lms.y + 0.2309699292 * lms.z,
		-1.2684380046 * lms.x + 2.6097574011 * lms.y - 0.3413193965 * lms.z,
		-0.0041960863 * lms.x - 0.7034186147 * lms.y + 1.7076147010 * lms.z);
}

float linear_tief(float d, mat4 inv_proj, vec2 uv) {
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return -v.z / v.w;
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float d = texture(tiefe, uv).x;
	if (d <= 0.000001) { discard; }   // Himmel: nichts anfassen
	bool ortho = ortho_groesse > 0.0;
	vec3 c = textureLod(bild, uv, 0.0).rgb;
	vec3 n0 = texture(normalen, uv).xyz * 2.0 - 1.0;

	// Weltposition und Texelgröße
	vec4 vp = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, d, 1.0);
	vp.xyz /= vp.w;
	vec3 wp = (INV_VIEW_MATRIX * vec4(vp.xyz, 1.0)).xyz;
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(n0, 0.0)).xyz);
	float mpp = ortho ? ortho_groesse / VIEWPORT_SIZE.y : -vp.z * fov_faktor / VIEWPORT_SIZE.y;   // Meter je Bildpunkt
	float texel = 1.0 / (texel_je_meter);
	if (texel_auto) texel = exp2(round(log2(texel_ziel_px * mpp)));   // Zweierstufen: ~0,7 bis 1,4 mal Zielgröße
		float px_je_texel = texel / mpp;
	float blende = smoothstep(blende_von, blende_von + 1.2, px_je_texel);   // unter ~1,5 Bildpunkten: Muster aus (Moiré)

	vec3 an = abs(wn);
	vec2 e; float lage;
	if (an.y >= an.x && an.y >= an.z) { e = wp.xz; lage = 0.0; }
	else if (an.x >= an.z)            { e = wp.zy; lage = 1.0; }
	else                              { e = wp.xy; lage = 2.0; }
	vec2 t = floor(e / texel + 0.0001);

	float b = 0.0;   // Schwelle -0,5..0,5
	if (dither == 1) b = (bayer8(ivec2(FRAGCOORD.xy) / max(dither_punkt, 1)) - 0.5) * dither_staerke;
	else if (dither == 2) b = (bayer8(ivec2(t)) - 0.5) * dither_staerke * blende;

	vec3 o = zu_oklab(c);
	o.yz = o.yz * sattel + waerme * smoothstep(0.0, 0.15, length(o.yz));
	float schatten = 0.0;
	if (stufen_an) {
		float n = float(max(stufen, 2));
		if (o.x < l_max) {
			float k = floor(o.x / l_max * n + 0.5 + b * 0.9);
			float lq = k / n * l_max;
			schatten = step(k, 1.01);   // die zwei untersten Stufen = Schatten
			lq = max(lq, l_schatten);   // Schatten bleibt hell genug, um Farbe zu zeigen
			float f = lq / max(o.x, 0.03);
			o = vec3(lq, o.yz * f);
			o.yz += schatten_ton * schatten;
		}
	}
	if (palette == 2) {
		float ln = float(max(pal_l, 2));
		float lq = floor(o.x * ln + 0.5 + b) / ln;
		float ch = length(o.yz);
		float cn = float(max(pal_c, 2));
		float cq = floor(ch / 0.25 * cn + 0.5 + b * 0.7) / cn * 0.25;
		float h = atan(o.z, o.y);
		float hn = float(max(pal_h, 4));
		float hq = floor(h / 6.2831853 * hn + 0.5) / hn * 6.2831853;
		if (cq < 0.012) cq = 0.0;
		o = vec3(max(lq, 0.0), cq * cos(hq), cq * sin(hq));
	}
	c = aus_oklab(o);
	if (palette == 1) {
		float k = float(max(pal_l, 2) - 1);
		vec3 g = pow(clamp(c, 0.0, 1.0), vec3(1.0 / 2.2));
		g = floor(g * k + 0.5 + b) / k;
		c = pow(g, vec3(2.2));
	}
	if (koernung_an) {
		float h = hash(vec3(t, lage));
		float fleck = h > 0.93 ? 1.0 : (h < 0.07 ? -1.0 : 0.0);   // wenige Texelflecken, Rest ruhig
		c *= 1.0 + fleck * koernung * blende;
	}

	// Umriss
	float kante = 0.0;
	if (umriss_an) {
		float z0 = linear_tief(d, INV_PROJECTION_MATRIX, uv);
		float bezug = ortho ? ortho_groesse : z0;
		float schwelle = (ortho ? tiefenschwelle_ortho : tiefenschwelle) * bezug;
		vec2 px = 1.0 / VIEWPORT_SIZE;
		vec2 rich[4] = vec2[](vec2(1, 0), vec2(-1, 0), vec2(0, 1), vec2(0, -1));
		for (int i = 0; i < 4; i++) {
			vec2 u = uv + rich[i] * px;
			float dn = texture(tiefe, u).x;
			if (dn <= 0.000001) { kante = 1.0; continue; }   // Silhouette gegen den Himmel
			float z = linear_tief(dn, INV_PROJECTION_MATRIX, u);
			vec3 n = texture(normalen, u).xyz * 2.0 - 1.0;
			if (z0 - z > schwelle) kante = 1.0;
			if (1.0 - dot(n0, n) > normalenschwelle && abs(z0 - z) < schwelle * 3.0) kante = 1.0;
		}
	}
	if (kante > 0.0) {
		float zur_sonne = dot(wn, -normalize(sonne_richtung));
		vec3 ok = zu_oklab(max(c, vec3(0.0)));
		ok.x += (zur_sonne > 0.15 || wn.y > 0.6) ? kante_hell : -kante_dunkel;
		c = aus_oklab(vec3(max(ok.x, 0.0), ok.yz));
	}
	ALBEDO = max(c, vec3(0.0));
	ALPHA = 1.0;
}
