// Pixel-Look (Testszene Pixeltest): Texel-Raster in der Welt. Als „Material-Overlay“ auf jedes Netz gelegt (blend_mul): multipliziert ein
// grobes Körnungsmuster, das an die WELTPOSITION gekoppelt ist (feste Texel je Meter), nicht an UV oder Bildschirm. So haben Haus, Boden,
// Fels und Figur gleich große Pixel, egal wie ihre UV liegen. Dreiseitig: Die dominante Achse der Normalen wählt die Ebene.
// Die Körnung ist ein Hash je Texel (kein Textur-Lesen, keine Filterung = „nearest“). Eigenbau (MIT).
shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_never, cull_back;

uniform float texel_je_meter = 16.0;  // 16 → ein Texel ist 6,25 cm
uniform float koernung = 0.18;        // Stärke des Musters (± Anteil der Helligkeit)
uniform float stufen = 4.0;           // Muster in so vielen Helligkeitsstufen (Farbtiefe des Musters)
uniform bool geordnet = false;        // true: Bayer-Muster je Texel (Welt-Dithering: bleibt bei Bewegung und Drehung am Objekt), false: Hash-Körnung

varying vec3 welt_pos;
varying vec3 welt_norm;

float hash(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return fract((p.x + p.y) * p.z);
}

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

void vertex() {
	welt_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	welt_norm = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
	vec3 a = abs(normalize(welt_norm));
	vec2 e;
	float lage;
	if (a.y >= a.x && a.y >= a.z) { e = welt_pos.xz; lage = 0.0; }
	else if (a.x >= a.z)          { e = welt_pos.zy; lage = 1.0; }
	else                          { e = welt_pos.xy; lage = 2.0; }
	vec2 t = floor(e * texel_je_meter);
	float h = hash(vec3(t, lage));
	h = floor(h * stufen) / (stufen - 1.0);   // Farbtiefe des Musters
	if (geordnet) h = bayer8(ivec2(t)); // Welt-Dithering: Schwelle je Texel der Weltgitter-Ebene
	ALBEDO = vec3(1.0 + (h - 0.5) * 2.0 * koernung);
	ALPHA = 1.0;
}
