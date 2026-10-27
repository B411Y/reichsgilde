// Haarkarten und Bart: Strähnen-Textur mit Alpha, Farbe aus "haarfarbe", Wurzel dunkler, Glanzband.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque, specular_schlick_ggx;

uniform sampler2D strahnen : source_color, filter_linear_mipmap, repeat_enable;
uniform vec3 haarfarbe : source_color = vec3(0.18, 0.1, 0.05);
uniform float schwelle : hint_range(0.0, 1.0) = 0.42;
uniform vec3 mitte = vec3(0.0, 1.66, 0.056);
uniform float aufhellen = 1.7;
uniform float glanz : hint_range(0.0, 1.0) = 0.5;

void vertex() {
	// Normalen zur Kugel um den Kopf hin biegen: weiche, zusammenhängende Beleuchtung statt Kartenkanten
	vec3 nach_aussen = normalize(VERTEX - mitte);
	NORMAL = normalize(mix(NORMAL, nach_aussen, 0.65));
}

void fragment() {
	vec4 t = texture(strahnen, UV);
	ALPHA = t.a;
	ALPHA_SCISSOR_THRESHOLD = schwelle;
	float wurzel = mix(0.35, 1.0, smoothstep(0.0, 0.4, UV.y));
	float strah = 0.25 + 1.2 * t.r;
	ALBEDO = haarfarbe * strah * wurzel * aufhellen;
	ROUGHNESS = 0.55;
	SPECULAR = 0.3 * glanz;
	if (!FRONT_FACING) { NORMAL = -NORMAL; }
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float weich = clamp(ndl * 0.5 + 0.5, 0.0, 1.0);
	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * ATTENUATION * weich * weich / PI;
	// Glanzband quer zur Strähne
	vec3 h = normalize(LIGHT + VIEW);
	float sp = pow(max(dot(NORMAL, h), 0.0), 24.0);
	SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * sp * 0.18 * ALBEDO.r * 3.0;
}
