// Versuch Charakter Phase 0: Auge (eigener Code, MIT). Feucht glänzend (scharfes Spitzlicht), Lederhaut nicht
// reinweiß. Ohne Textur (ICT) zeichnet der Shader die Iris selbst: Pupille, Fasern, dunkler Limbusring;
// Bei ICT trägt die Eckfarbe R den Abstand von der Blickachse (innere Kugel: 2·R = 0 Pupillenmitte, 1 Limbus;
// äußere Kugel: R = 1 Hornhaut, dort durchsichtig), G/B den Winkel um die Achse (cos, sin). MPFB-Augen haben eine Hornhautschale mit Alpha 0 in der Textur.
shader_type spatial;
render_mode cull_back;

uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic;
uniform bool hat_farbe = true;
uniform bool iris_teil = false;
uniform bool lederhaut_teil = false;
uniform vec3 sklera : source_color = vec3(0.86, 0.82, 0.77);
uniform vec3 iris_farbe : source_color = vec3(0.36, 0.22, 0.11);
uniform float pupille = 0.36;
uniform float iris_saettigung = 0.85;

float rausch(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec3 c;
	float nv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	if (hat_farbe) {
		vec4 t = texture(farbe, UV);
		c = t.rgb;
		ALPHA = t.a;
		ALPHA_SCISSOR_THRESHOLD = 0.5;
		float l = dot(c, vec3(0.299, 0.587, 0.114));
		c = mix(vec3(l), c, iris_saettigung);
	} else if (iris_teil) {
		float r = COLOR.r * 2.0;
		float w = atan(COLOR.b - 0.5, COLOR.g - 0.5);
		float faser = 0.75 + 0.25 * sin(w * 48.0 + rausch(vec2(floor(w * 24.0), 1.0)) * 6.0) * smoothstep(pupille, 0.9, r);
		c = iris_farbe * faser * mix(1.25, 0.8, smoothstep(pupille, 1.0, r));
		c = mix(c, c * 0.35, smoothstep(0.82, 1.0, r));      // Limbusring
		c = mix(vec3(0.01), c, smoothstep(pupille - 0.02, pupille + 0.02, r));
		c = mix(c, sklera * 0.92, smoothstep(0.98, 1.06, r));     // außerhalb des Limbus: Lederhaut der inneren Kugel
	} else {
		c = sklera;
		ALPHA = 1.0 - smoothstep(0.4, 0.6, COLOR.r);
		ALPHA_SCISSOR_THRESHOLD = 0.5;
	}
	if (lederhaut_teil || !hat_farbe && !iris_teil) {
		// zum Rand hin etwas rosiger und dunkler (Lidschatten, Verdeckung)
		c = mix(c * vec3(0.95, 0.78, 0.74), c, smoothstep(0.15, 0.7, nv));
	}
	ALBEDO = c;
	ROUGHNESS = 0.06;
	SPECULAR = 0.6;
}
