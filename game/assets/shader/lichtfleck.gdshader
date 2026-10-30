// Lichtflecken der Fackeln, Laternen und Feuerkörbe (Stadtansicht.Lichtflecken.cs): Auch wo kein echtes Licht mehr steht (weit vom Blickpunkt,
// herausgezoomt, Grafik Niedrig), soll jede Flamme leuchten. Ein Vielfach-Zeichenaufruf je Art, beide zur Kamera gedreht, ohne Beleuchtung; Farbe
// und Stärke je Licht kommen aus der Instanzfarbe (Alpha = Stärke):
//  * Schein (boden = true, Phase 2 „Feuer und Flammen“): ein billiges Ersatzlicht. Die Fläche deckt die Kugel um die Flamme ab (Halbmesser aus der
//    Größe der Instanz); je Bildpunkt holt der Shader aus der Tiefe der Szene den Punkt der Welt, der dort zu sehen ist, und hellt ihn nach seinem
//    Abstand zur Flamme auf (weicher Abfall bis null am Rand der Kugel, zur Flamme gewandte Flächen stärker; die Richtung der Fläche aus den
//    Ableitungen des Weltpunkts). So fällt der Schein auf Boden, Mauern und Stufen wie ein Licht und hat keinen Rand: Früher lag hier eine flache
//    Scheibe knapp über dem Boden, die an Sockeln, Stufen und Wänden hart abgeschnitten war (Nutzer 29.10.2026: harter Kreis um die Torlaternen).
//  * Heiligenschein (boden = false): ein kleiner, weicher Hof um die Flamme.
// Bei Nässe glänzt der Schein stärker (nasser Boden gibt Licht zurück). Wo ein echtes Licht steht, blendet die Darstellung den Schein ganz aus.
shader_type spatial;
render_mode unshaded, blend_premul_alpha, cull_disabled, depth_draw_never, fog_disabled;

global uniform float wetter_naesse;

uniform sampler2D tiefe : hint_depth_texture, repeat_disable, filter_nearest;
// true: Ersatzlicht auf der Szene, false: Heiligenschein
uniform bool boden = false;

varying vec3 mitte;
varying float halbmesser;

void vertex() {
	// Zur Kamera gedreht, die Größe aus der Instanz
	mitte = MODEL_MATRIX[3].xyz;
	halbmesser = length(MODEL_MATRIX[0].xyz) * 0.5;
	// Der Schein steht an der Vorderseite der Kugel (zur Kamera hin): So liegt alles, was er beleuchtet, hinter der Fläche, und nur was wirklich
	// davor steht, verdeckt ihn (der Heiligenschein bleibt in der Mitte)
	vec3 ort = boden ? mitte + normalize(INV_VIEW_MATRIX[2].xyz) * halbmesser : mitte;
	mat4 m = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(ort, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * m * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
	if (boden) {
		// Der Punkt der Szene hinter diesem Bildpunkt, in Weltkoordinaten
		float tief = texture(tiefe, SCREEN_UV).x;
		vec4 sicht = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, tief, 1.0);
		sicht.xyz /= sicht.w;
		vec3 welt = (INV_VIEW_MATRIX * vec4(sicht.xyz, 1.0)).xyz;
		vec3 zur_flamme = mitte - welt;
		float r = length(zur_flamme) / halbmesser;
		if (r >= 1.0) discard;
		// Richtung der Fläche aus den Nachbarbildpunkten; zur Flamme gewandte Flächen hell, abgewandte schwach
		vec3 n = normalize(cross(dFdx(welt), dFdy(welt)));
		if (dot(n, INV_VIEW_MATRIX[2].xyz) < 0.0) n = -n;
		float zuwendung = 0.35 + 0.65 * clamp(dot(n, zur_flamme / max(length(zur_flamme), 0.001)), 0.0, 1.0);
		// Weicher Abfall wie ein Punktlicht, an der Kugelgrenze null (auch die Steigung), nass etwas heller
		float a = 1.0 - r * r;
		float s = a * a / (1.0 + 6.0 * r * r) * zuwendung * (1.0 + 0.45 * wetter_naesse);
		// Sättigende Mischung (vormultipliziert): Szene · (1 − deck) + Licht. Viele Scheine übereinander nähern sich einer Obergrenze in
		// Lichtfarbe (höchstens warm-hell, nie weiß), und die Oberfläche darunter bleibt erkennbar.
		ALBEDO = COLOR.rgb * vec3(1.0, 0.85, 0.7) * COLOR.a * s * 0.08;
		ALPHA = 0.12 * s * COLOR.a;
	} else {
		float r = length(UV - 0.5) * 2.0;
		if (r >= 1.0) discard;
		// kleiner heller Kern und weiter Schein, rein additiv (klein, überlappt kaum)
		float a = 1.0 - r;
		ALBEDO = COLOR.rgb * COLOR.a * (a * a * a * 0.9 + exp(-r * r * 22.0) * 0.8);
		ALPHA = 0.0;
	}
}
