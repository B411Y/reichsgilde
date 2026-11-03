// Pixel-Look (Testszene Pixeltest): feine Umrisslinien, 1 Bildschirmpixel breit, aus Tiefe und Normalen.
// Ein Vollbild-Viereck vor der Kamera (Eigenbau, MIT). Voller Auflösung: die Nachbarn liegen genau einen Bildpunkt auseinander,
// die Bildauflösung wird nicht gesenkt. Kante = Tiefensprung (relativ zur Entfernung, damit er mit dem Abstand nicht kippt)
// oder Knick der Normalen (Hauskanten innerhalb einer Fläche gleicher Tiefe).
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, skip_vertex_transform;

uniform sampler2D tiefe : hint_depth_texture, filter_nearest;
uniform sampler2D normalen : hint_normal_roughness_texture, filter_nearest;
uniform vec4 linienfarbe : source_color = vec4(0.07, 0.05, 0.04, 1.0);
uniform float tiefenschwelle = 0.08;   // Anteil der Entfernung, ab dem ein Sprung als Kante gilt
uniform float normalenschwelle = 0.9;  // 1 - Skalarprodukt zweier Normalen
uniform float staerke = 1.0;           // 1 = ein Bildpunkt; 2 = Nachbarn zwei Punkte entfernt (dicker)
uniform float faerbung = 0.0;          // 0 = Linie in linienfarbe; 1 = Linie nur abgedunkelt (Farbe des Bildes bleibt)

float linear_tief(vec2 uv, mat4 inv_proj) {
	float d = texture(tiefe, uv).x;
	vec3 ndc = vec3(uv * 2.0 - 1.0, d);
	vec4 v = inv_proj * vec4(ndc, 1.0);
	return -v.z / v.w;
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 px = staerke / VIEWPORT_SIZE;
	vec2 uv = SCREEN_UV;
	float z0 = linear_tief(uv, INV_PROJECTION_MATRIX);
	vec3 n0 = texture(normalen, uv).xyz * 2.0 - 1.0;
	float kante = 0.0;
	vec2 rich[4] = vec2[](vec2(1, 0), vec2(-1, 0), vec2(0, 1), vec2(0, -1));
	for (int i = 0; i < 4; i++) {
		vec2 u = uv + rich[i] * px;
		float z = linear_tief(u, INV_PROJECTION_MATRIX);
		vec3 n = texture(normalen, u).xyz * 2.0 - 1.0;
		// Tiefe: nur die nähere Seite zeichnet die Linie (sonst wäre sie zwei Punkte dick)
		if (z0 - z > tiefenschwelle * z0 && z0 < 900.0) kante = 1.0;
		if (1.0 - dot(n0, n) > normalenschwelle && abs(z0 - z) < tiefenschwelle * z0 * 3.0) kante = max(kante, 1.0);
	}
	ALBEDO = linienfarbe.rgb;
	ALPHA = kante * linienfarbe.a;
}
