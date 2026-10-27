// Haut des Live-Kopfes: gemalt-realistisch. Zwei Hauttexturen (jung/alt) werden nach "alter" gemischt,
// "hautton" färbt um, weiches Wrap-Licht mit rötlichem Streulicht (Subsurface-Ersatz) und warmer Randton.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D tex_jung : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D tex_alt : source_color, filter_linear_mipmap, repeat_disable;
uniform float alter : hint_range(-1.0, 1.0) = 0.0;
uniform vec3 hautton : source_color = vec3(1.0);
uniform vec3 streulicht : source_color = vec3(0.85, 0.28, 0.18);
uniform float wrap : hint_range(0.0, 1.0) = 0.45;
uniform float rand_staerke : hint_range(0.0, 2.0) = 0.6;

void fragment() {
	vec3 jung = texture(tex_jung, UV).rgb;
	vec3 alt = texture(tex_alt, UV).rgb;
	vec3 c = mix(jung, alt, clamp(alter, 0.0, 1.0));
	// leicht entsättigt und warm, wie aufgetragene Ölfarbe
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, 0.78) * hautton * vec3(0.97, 0.95, 0.93);
	ALBEDO = c;
	ROUGHNESS = 0.62;
	SPECULAR = 0.35;
	RIM = rand_staerke * 0.35;
	RIM_TINT = 0.5;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float weich = clamp((ndl + wrap) / (1.0 + wrap), 0.0, 1.0);
	// Streulicht: an der Hell-Dunkel-Grenze schimmert Blut durch die Haut
	float rand = smoothstep(0.0, 0.5, weich) * (1.0 - smoothstep(0.5, 1.0, weich));
	vec3 farbe = ALBEDO * (weich + streulicht * rand * 0.3);
	DIFFUSE_LIGHT += farbe * LIGHT_COLOR * ATTENUATION / PI;
}
