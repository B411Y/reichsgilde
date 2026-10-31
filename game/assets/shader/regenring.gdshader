// Spritzer der Tropfen von der Dachkante am Boden (Stadtansicht.Traufen.cs, GpuParticles3D): Jedes Partikel ist ein Kranz aus vier kleinen Spritzern auf
// dem Boden (waagerechtes Quad), der auseinanderfliegt und vergeht, im ersten Augenblick mit hellem Kern. Alter 0–1 kommt aus INSTANCE_CUSTOM.y.
// Die feinen Ringe in den kleinen Pfützen der Tropfkante und die Ringe der Regentropfen überall zeichnet der Boden selbst (wetter.gdshaderinc,
// tropfringe und regentropfen); die Ringe des Regens als Partikel gibt es seit Regen 4 nicht mehr.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec4 farbe : source_color = vec4(0.8, 0.85, 0.95, 1.0);
// 0–1: Beim Herauszoomen blendet die Stadtansicht die Spritzer aus (sie behalten ihre Größe in der Welt und würden zu Bildpunkten)
uniform float weit : hint_range(0.0, 1.0) = 1.0;

varying float v_alter;
varying float v_id;

void vertex() {
	v_alter = INSTANCE_CUSTOM.y;
	v_id = float(INSTANCE_ID);
}

void fragment() {
	vec2 q = (UV - 0.5) * 2.0;
	float r = length(q);
	float t = v_alter;
	// Kranz aus vier Spritzern: Winkel und Weite je Partikel aus der Nummer, schnell nach außen, dann langsamer; jeder mindestens ~1,2 Bildpunkte groß
	float h0 = fract(sin(v_id * 12.9898) * 43758.5453);
	float weg = 0.12 + 0.7 * sqrt(clamp(t, 0.0, 1.0));
	float klein = max(0.1 * (1.0 - 0.5 * t), 1.2 * fwidth(r));
	float kranz = 0.0;
	for (int i = 0; i < 4; i++) {
		float hi = fract(sin((v_id + float(i) * 1.37) * 7.13) * 917.3);
		float w = (float(i) + h0 * 4.0 + 0.6 * hi) * 1.5708;
		vec2 c = vec2(cos(w), sin(w)) * weg * (0.7 + 0.45 * hi);
		kranz = max(kranz, 1.0 - smoothstep(klein * 0.45, klein, length(q - c)));
	}
	float kern = (1.0 - smoothstep(0.0, 0.16, r)) * (1.0 - smoothstep(0.0, 0.15, t));
	float a = (kranz * (1.0 - t) * (1.0 - t) + kern * 0.7) * (0.65 + 0.35 * h0);
	// Ausgabe nie NaN oder negativ (siehe regen.gdshader)
	float al = clamp(a, 0.0, 1.0) * farbe.a * clamp(weit, 0.0, 1.0);
	if (!(al > 0.004)) discard;
	ALBEDO = clamp(farbe.rgb, vec3(0.0), vec3(1.0));
	ALPHA = al;
}
