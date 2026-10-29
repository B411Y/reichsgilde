// Tropfen an den Dachkanten (Stadtansicht.Traufen.cs, GpuParticles3D): Jeder Tropfen ist ein kurzer, dicker, am Kopf runder Tropfen, der IMMER
// senkrecht fällt – auch bei Wind (der Regen vom Himmel fällt schräg, was von der Traufe fällt, hat keinen Schwung). Anders als die Striche des
// Regens (regen.gdshader) sind es einzelne, deckende Tropfen. Länge und Dicke wachsen mit dem Maßstab des Partikels (Zoom), nie dünner als ~2,2
// Bildpunkte. Ausgabe nie NaN oder negativ (siehe regen.gdshader).
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec4 farbe : source_color = vec4(0.82, 0.88, 0.96, 1.0);

varying float v_alpha;

float h(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	float id = float(INSTANCE_ID);
	float h1 = h(id), h2 = h(id + 17.0), h3 = h(id + 41.0);
	float s = length(MODEL_MATRIX[0].xyz);
	float px = 2.0 / PROJECTION_MATRIX[1][1] / VIEWPORT_SIZE.y;   // Meter je Bildpunkt (Kamera ohne Perspektive)
	float breite = max((0.10 + 0.04 * h2) * s, px * (2.2 + 0.6 * h2));
	float laenge = max((0.30 + 0.16 * h1) * s, px * 5.0);
	vec3 mitte = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz;
	// senkrecht in der Welt = auf dem Bildschirm entlang der Projektion der Lotrechten (Kamera ohne Rollen: gerade nach unten)
	vec3 d = (VIEW_MATRIX * vec4(0.0, -1.0, 0.0, 0.0)).xyz;
	vec2 a = normalize(d.xy + vec2(0.0001, 0.0));
	vec2 q = vec2(-a.y, a.x);
	// UV.y: 0 oben (Spitze), 1 unten (runder Kopf)
	vec2 p = q * VERTEX.x * breite + a * (-VERTEX.y) * laenge * max(length(d.xy), 0.35);
	POSITION = PROJECTION_MATRIX * vec4(mitte + vec3(p, 0.0), 1.0);
	v_alpha = 0.7 + 0.3 * h3;
}

void fragment() {
	float y = clamp(UV.y, 0.0, 1.0);
	float d = abs(UV.x - 0.5) * 2.0;
	// Umriss eines Tropfens: oben schmal, nach unten breiter, unten rund
	float t = clamp((y - 0.65) / 0.35, 0.0, 1.0);
	float hw = y < 0.65 ? mix(0.15, 1.0, y / 0.65) : sqrt(max(1.0 - t * t, 0.0));
	float a = (1.0 - smoothstep(hw * 0.6, max(hw, 0.05), d)) * clamp(farbe.a * v_alpha, 0.0, 1.0);
	if (!(a > 0.004)) discard;
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = a;
}
