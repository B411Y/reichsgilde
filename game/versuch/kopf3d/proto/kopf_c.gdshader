// Plan C: gebackene TRELLIS-Textur (Augen offen) und zweite Textur (Augen geschlossen), weich überblendet (blink 0..1).
// Weiches Licht (Wrap) mit kräftiger Umgebungsaufhellung, damit die gemalte Beleuchtung der Textur erhalten bleibt.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D tex_offen : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D tex_zu : source_color, filter_linear_mipmap, repeat_disable;
uniform float blink : hint_range(0.0, 1.0) = 0.0;
uniform float wrap : hint_range(0.0, 1.0) = 0.5;
uniform float licht_anteil : hint_range(0.0, 1.0) = 0.55;
uniform float emission_anteil : hint_range(0.0, 1.0) = 0.45;

void fragment() {
	vec3 c = mix(texture(tex_offen, UV).rgb, texture(tex_zu, UV).rgb, blink);
	ALBEDO = c;
	EMISSION = c * emission_anteil;
	ROUGHNESS = 0.75;
	SPECULAR = 0.2;
	RIM = 0.2;
	RIM_TINT = 0.5;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float weich = clamp((ndl + wrap) / (1.0 + wrap), 0.0, 1.0);
	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * weich * licht_anteil / PI;
}
