// Plan D: Gesicht aus dem Porträt (UV2, scharf), Seiten/Hinterkopf aus dem gebackenen Atlas (UV), Gewicht in COLOR.a.
// COLOR.rgb dunkelt den Mundinnenraum ab. Augen: Maske im Porträtraum (R Öffnung, G Blick-Gewicht, B/A Lage im Auge)
// für gewölbte Hornhaut (Glanzpunkt), Blick (Iris wandert) und Blinzeln (zweites Porträt mit geschlossenen Lidern).
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_disabled;

uniform sampler2D tex_atlas : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D tex_portraet : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D tex_portraet_zu : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D tex_augen : filter_linear, repeat_disable;
uniform float blink : hint_range(0.0, 1.0) = 0.0;
uniform vec2 blick = vec2(0.0);            // Verschiebung der Iris im Porträtraum (UV-Einheiten)
uniform vec3 achse_u = vec3(1.0, 0.0, 0.0); // Bildachsen des Porträts im Modellraum
uniform vec3 achse_v = vec3(0.0, -1.0, 0.0);
uniform float woelbung : hint_range(0.0, 2.0) = 0.4;
uniform float glanz : hint_range(0.0, 4.0) = 0.35;
uniform float wrap : hint_range(0.0, 1.0) = 0.5;
uniform float licht_anteil : hint_range(0.0, 1.0) = 0.55;
uniform float emission_anteil : hint_range(0.0, 1.0) = 0.45;

varying float v_auge;

void fragment() {
	vec4 m = texture(tex_augen, UV2);
	vec2 uv_offen = UV2 - blick * m.g;
	vec3 p = mix(texture(tex_portraet, uv_offen).rgb, texture(tex_portraet_zu, UV2).rgb, blink);
	vec3 at = texture(tex_atlas, UV).rgb;
	vec3 c = mix(at, p, clamp(COLOR.a, 0.0, 1.0)) * COLOR.rgb;
	ALBEDO = c;
	EMISSION = c * emission_anteil;
	float auge = m.r * (1.0 - smoothstep(0.2, 0.6, blink)) * step(0.5, COLOR.a);
	float kern = m.g * (1.0 - smoothstep(0.2, 0.6, blink)) * step(0.5, COLOR.a);
	v_auge = kern;
	vec2 lage = m.ba * 2.0 - 1.0;
	vec3 aus = achse_u * lage.x + achse_v * lage.y;
	vec3 aus_v = (VIEW_MATRIX * MODEL_MATRIX * vec4(aus, 0.0)).xyz;
	NORMAL = normalize(NORMAL + aus_v * woelbung * auge);
	ROUGHNESS = mix(0.75, 0.4, auge);
	SPECULAR = mix(0.2, 0.25, auge);
	RIM = 0.2;
	RIM_TINT = 0.5;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float weich = clamp((ndl + wrap) / (1.0 + wrap), 0.0, 1.0);
	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * weich * licht_anteil / PI;
	if (v_auge > 0.01) {
		vec3 h = normalize(LIGHT + VIEW);
		float s = pow(max(dot(NORMAL, h), 0.0), 400.0);
		SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * s * v_auge * glanz;
	}
}
