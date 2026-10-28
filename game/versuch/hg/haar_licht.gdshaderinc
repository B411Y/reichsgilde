// Gemeinsame Teile der Haar-Shader (Kajiya-Kay-Licht, dunkler Ansatz).
uniform sampler2D farbe : source_color, filter_linear_mipmap_anisotropic;
uniform vec3 toenung = vec3(1.0);
uniform float ansatz_dunkel = 0.5;      // so dunkel wird das Haar am Ansatz (UV2.x = 0)
uniform float glanz1 = 0.16;
uniform float glanz2 = 0.18;
uniform float exp1 = 90.0;
uniform float exp2 = 22.0;
uniform float versatz1 = 0.08;
uniform float versatz2 = -0.12;

varying vec3 strang;     // Strähnenrichtung (Sicht)
varying vec3 n_sicht;    // Normale vom Kopf weg (Sicht), nicht umgedreht auf der Rückseite

void vertex() {
	strang = normalize((MODELVIEW_MATRIX * vec4(BINORMAL, 0.0)).xyz);
	n_sicht = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

vec3 haar_farbe(vec4 f, float wurzel) {
	float ansatz = mix(ansatz_dunkel, 1.0, smoothstep(0.0, 0.5, wurzel));
	return f.rgb * toenung * ansatz;
}

float kk(vec3 t, vec3 n, vec3 h, float versatz, float e) {
	vec3 ts = normalize(t + n * versatz);
	float th = dot(ts, h);
	float s = sqrt(max(1.0 - th * th, 0.0));
	return pow(s, e) * smoothstep(-1.0, 0.0, th);
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float diff = max((ndl + 0.5) / 1.5, 0.0);
	DIFFUSE_LIGHT += diff * ATTENUATION * LIGHT_COLOR / PI;
	vec3 h = normalize(LIGHT + VIEW);
	float sicht = clamp(ndl * 2.0 + 0.3, 0.0, 1.0);
	vec3 sp = vec3(kk(strang, NORMAL, h, versatz1, exp1) * glanz1)
		+ ALBEDO * 2.0 * kk(strang, NORMAL, h, versatz2, exp2) * glanz2;
	SPECULAR_LIGHT += sp * sicht * ATTENUATION * LIGHT_COLOR / PI;
}
