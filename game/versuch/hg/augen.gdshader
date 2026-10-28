// Versuch HG-Köpfe: Augapfel und Iris. Iris etwas satter, Lederhaut nicht reinweiß (leicht warm, zum Rand hin rosiger).
shader_type spatial;

uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic;
uniform float iris_saettigung = 1.35;
uniform vec3 sklera = vec3(0.9, 0.85, 0.8);

void fragment() {
	vec3 c = texture(farbe, UV).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	float sat = max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
	float weiss = smoothstep(0.45, 0.7, l) * (1.0 - smoothstep(0.15, 0.35, sat));
	vec3 iris = mix(vec3(l), c, iris_saettigung);
	ALBEDO = mix(iris, c * sklera, weiss);
	ROUGHNESS = 0.3;
	SPECULAR = 0.4;
}
