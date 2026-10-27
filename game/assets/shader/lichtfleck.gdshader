// Lichtflecken der Fackeln und Laternen (Stadtansicht.Lichtflecken.cs): Auch wo kein echtes Licht mehr steht (weit vom Blickpunkt, herausgezoomt,
// Grafik Niedrig), soll jede Flamme leuchten. Ein Vielfach-Zeichenaufruf je Art: ein Heiligenschein um die Flamme (zur Kamera gedreht) und
// ein Lichtfleck auf dem Boden (waagerecht, mit Rand, der der Fläche folgt). Beide additiv, ohne Beleuchtung; Farbe und Stärke je Licht kommen aus
// der Instanzfarbe (Alpha = Stärke). Bei Nässe glänzt der Fleck stärker (nasser Boden gibt Licht zurück).
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

global uniform float wetter_naesse;

// true: Fleck auf dem Boden (liegt waagerecht), false: Heiligenschein (zur Kamera gedreht)
uniform bool boden = false;

void vertex() {
	if (!boden) {
		mat4 m = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(MODEL_MATRIX[3].xyz, 1.0));
		MODELVIEW_MATRIX = VIEW_MATRIX * m * mat4(
			vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
			vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
			vec4(0.0, 0.0, 0.0, 1.0));
	}
}

void fragment() {
	float r = length(UV - 0.5) * 2.0;
	if (r >= 1.0) discard;
	float s;
	if (boden) {
		// weicher Abfall wie ein Punktlicht, nass etwas weiter und heller
		float a = 1.0 - r;
		s = a * a * (0.55 + 0.45 * a) * (1.0 + 0.45 * wetter_naesse);
	} else {
		// kleiner heller Kern und weiter Schein
		float a = 1.0 - r;
		s = a * a * a * 0.9 + exp(-r * r * 22.0) * 0.8;
	}
	ALBEDO = COLOR.rgb * COLOR.a * s;
	ALPHA = 1.0;
}
