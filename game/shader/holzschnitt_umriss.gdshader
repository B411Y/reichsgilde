// Schwarzer Umriss des Holzschnitt-Looks: ein Quad direkt vor der Kamera (QuadMesh 2 × 2, extra_cull_margin groß),
// das an Sprüngen in der Tiefe zeichnet – Umriss jedes Hauses, jeder Figur, jedes Baums vor dem Hintergrund.
// In Forward+ kommen Kanten aus dem Normalenpuffer dazu (First, Hausecken); der Compatibility-Renderer hat keinen.
// Für die orthografische Kamera des Spiels ist die Tiefe linear zwischen near und far.
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, blend_mix, fog_disabled;

uniform sampler2D tiefe : hint_depth_texture, filter_nearest;
#if CURRENT_RENDERER == RENDERER_FORWARD_PLUS
uniform sampler2D normalen : hint_normal_roughness_texture, filter_nearest;
#endif
uniform float staerke = 1.4;          // Linienbreite in Pixeln – das Spiel passt sie dem Zoom an
uniform float schwelle = 0.0025;      // Tiefensprung (Anteil von near…far), ab dem eine Linie entsteht
uniform float knick = 0.35;           // Forward+: Unterschied der Normalen, ab dem eine Kante entsteht
uniform vec3 tusche : source_color = vec3(0.04, 0.035, 0.03);

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 px = staerke / VIEWPORT_SIZE;
	float d = texture(tiefe, SCREEN_UV).r;
	float k = 0.0;
	float n_k = 0.0;
#if CURRENT_RENDERER == RENDERER_FORWARD_PLUS
	vec3 n0 = texture(normalen, SCREEN_UV).xyz * 2.0 - 1.0;
#endif
	for (int i = 0; i < 8; i++) {
		float w = float(i) * 0.785398;
		vec2 uv = SCREEN_UV + vec2(cos(w), sin(w)) * px;
		k = max(k, abs(texture(tiefe, uv).r - d));
#if CURRENT_RENDERER == RENDERER_FORWARD_PLUS
		n_k = max(n_k, 1.0 - dot(n0, texture(normalen, uv).xyz * 2.0 - 1.0));
#endif
	}
	ALBEDO = tusche;
	ALPHA = max(smoothstep(schwelle, schwelle * 2.5, k), smoothstep(knick, knick * 1.6, n_k));
}
