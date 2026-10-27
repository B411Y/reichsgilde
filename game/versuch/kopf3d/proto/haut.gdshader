// Haut des Live-Kopfes (Version 2): gemalt statt Puppenhaut.
// Alles Gesichtsspezifische hängt an Koordinaten im Modellraum (Meter, Nasenspitze bei (0, 1.598, 0.177)):
// Rötung (Wangen, Nase, Ohren), Augenringe, Augenhöhlen-Schatten, Poren, Fleckigkeit und Falten (Stirn, Krähenfüße,
// Nasolabialfalte, Tränensäcke), die mit "alter" stärker werden. Falten und Poren wirken als Bump auf die Normale.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D tex_jung : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D tex_alt : source_color, filter_linear_mipmap, repeat_disable;
uniform float alter : hint_range(-1.0, 1.0) = 0.0;
uniform vec3 hautton : source_color = vec3(1.0);
uniform vec3 streulicht : source_color = vec3(0.8, 0.22, 0.14);
uniform float wrap : hint_range(0.0, 1.0) = 0.35;
uniform float rand_staerke : hint_range(0.0, 2.0) = 0.5;
uniform float roetung : hint_range(0.0, 2.0) = 0.75;
uniform float poren : hint_range(0.0, 2.0) = 1.0;
uniform float falten : hint_range(0.0, 2.0) = 1.0;

varying vec3 mp;

float hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float vnoise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i + vec3(0, 0, 0)), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 p) {
	float a = 0.5;
	float s = 0.0;
	for (int i = 0; i < 4; i++) {
		s += a * vnoise(p);
		p *= 2.03;
		a *= 0.5;
	}
	return s;
}
float blob(vec2 p, vec2 c, vec2 r) {
	vec2 d = (p - c) / r;
	return exp(-dot(d, d));
}

// Höhenfeld der Haut: Poren und Falten (positiv = Erhebung)
float hoehe(vec3 q, float alt01) {
	float h = 0.0;
	// Poren und feine Haut
	h += poren * (0.5 * vnoise(q * 1100.0) + 0.5 * vnoise(q * 380.0)) * 0.00035;
	h += poren * fbm(q * 90.0) * 0.0006;
	float ax = abs(q.x);
	vec2 p = vec2(ax, q.y);
	float stark = falten * (0.25 + 0.75 * alt01);
	// Stirnfalten: waagerechte Rillen zwischen Brauen und Haaransatz, in der Mitte stärker
	float stirn = smoothstep(1.668, 1.676, q.y) * (1.0 - smoothstep(1.700, 1.715, q.y)) * (1.0 - smoothstep(0.045, 0.075, ax)) * step(0.0, q.z - 0.08);
	float welle = sin(q.y * 740.0 + fbm(q * 40.0) * 5.0 + ax * 25.0);
	h -= stirn * stark * smoothstep(0.55, 1.0, welle) * 0.0014;
	// Zornesfalte zwischen den Brauen
	h -= stark * blob(p, vec2(0.009, 1.658), vec2(0.0035, 0.011)) * 0.0018;
	// Krähenfüße: Strahlen vom äußeren Augenwinkel
	vec2 ec = vec2(0.066, 1.642);
	vec2 dd = p - ec;
	float ang = atan(dd.y, dd.x);
	float r = length(dd);
	float kr = smoothstep(0.004, 0.012, r) * (1.0 - smoothstep(0.022, 0.034, r)) * step(0.0, dd.x + 0.004);
	h -= stark * kr * smoothstep(0.6, 1.0, sin(ang * 17.0 + fbm(q * 50.0) * 2.0)) * 0.0011;
	// Tränensack und Rille darunter
	float rb = length((p - vec2(0.031, 1.643)) * vec2(1.0, 1.15));
	h -= stark * exp(-pow((rb - 0.0215) / 0.0028, 2.0)) * smoothstep(1.642, 1.628, q.y) * 0.0013;
	// Nasolabialfalte: Bogen von Nasenflügel zum Mundwinkel
	float t = clamp((1.612 - q.y) / 0.050, 0.0, 1.0);
	vec2 nl = vec2(0.020 + 0.022 * t - 0.01 * t * t, 1.612 - 0.050 * t);
	h -= stark * exp(-pow(length(p - nl) / 0.0042, 2.0)) * step(q.y, 1.614) * step(1.560, q.y) * 0.0022;
	// Kinn-Lippen-Furche
	h -= stark * blob(vec2(q.x, q.y), vec2(0.0, 1.543), vec2(0.02, 0.0035)) * 0.0012;
	return h;
}

void vertex() {
	mp = VERTEX;
}

void fragment() {
	float alt01 = clamp(alter, 0.0, 1.0);
	vec3 jung = texture(tex_jung, UV).rgb;
	vec3 alt = texture(tex_alt, UV).rgb;
	vec3 c = mix(jung, alt, alt01);
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(l), c, 0.68) * hautton * vec3(1.0, 0.975, 0.95);
	vec2 p = vec2(abs(mp.x), mp.y);
	float vorn = smoothstep(0.02, 0.08, mp.z);

	// Fleckigkeit: großflächig wolkige Farbunterschiede wie aufgetragene Lasur
	float wolke = fbm(mp * 22.0 + 3.0);
	c *= 1.0 + (wolke - 0.5) * 0.22 * vec3(1.0, 0.8, 0.8);
	c *= 1.0 - 0.08 * (fbm(mp * 150.0) - 0.5);

	// Rötung: Wangen, Nasenspitze und -flügel, Ohren, Stirn leicht
	vec3 rot = vec3(0.34, -0.10, -0.14);
	float w = blob(p, vec2(0.052, 1.588), vec2(0.026, 0.020));
	float n = blob(p, vec2(0.004, 1.598), vec2(0.014, 0.013)) + 0.6 * blob(p, vec2(0.021, 1.594), vec2(0.008, 0.008));
	float ohr = smoothstep(0.070, 0.082, abs(mp.x)) * (1.0 - smoothstep(0.02, 0.07, mp.z));
	c *= 1.0 + roetung * (rot * (0.75 * w + 0.7 * n + 0.8 * ohr) + rot * 0.15 * blob(p, vec2(0.0, 1.69), vec2(0.04, 0.02)));
	// Augenringe und Schatten in den Augenhöhlen (Augen sitzen tiefer)
	float ring = blob(p, vec2(0.031, 1.628), vec2(0.020, 0.0085));
	c = mix(c, c * vec3(0.78, 0.68, 0.72), ring * (0.45 + 0.4 * alt01) * vorn);
	float hoehle = blob(p, vec2(0.031, 1.648), vec2(0.026, 0.018));
	c *= 1.0 - 0.32 * hoehle * vorn;
	float nasenseite = blob(p, vec2(0.018, 1.628), vec2(0.007, 0.018));
	c *= 1.0 - 0.14 * nasenseite * vorn;
	// Altersflecken
	c = mix(c, c * vec3(0.78, 0.66, 0.55), smoothstep(0.72, 0.85, vnoise(mp * 260.0)) * alt01 * 0.6);

	// Bump aus Poren und Falten: Normale aus der Ableitung des Höhenfelds
	float h = hoehe(mp, alt01);
	c *= 1.0 + clamp(h * 190.0, -0.35, 0.1) * vorn;
	vec3 dpdx = dFdx(VERTEX);
	vec3 dpdy = dFdy(VERTEX);
	float hx = dFdx(h);
	float hy = dFdy(h);
	vec3 r1 = cross(dpdy, NORMAL);
	vec3 r2 = cross(NORMAL, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (hx * r1 + hy * r2);
	NORMAL = normalize(abs(det) * NORMAL - grad);

	ALBEDO = c;
	// Schmale Glanzfläche in der T-Zone (Stirn, Nasenrücken), sonst matt
	float tz = blob(p, vec2(0.0, 1.64), vec2(0.03, 0.05)) + blob(p, vec2(0.003, 1.605), vec2(0.007, 0.03));
	ROUGHNESS = mix(0.66, 0.42, clamp(tz, 0.0, 1.0)) - 0.05 * wolke;
	SPECULAR = 0.3;
	RIM = rand_staerke * 0.3;
	RIM_TINT = 0.5;
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float weich = clamp((ndl + wrap) / (1.0 + wrap), 0.0, 1.0);
	float rand = smoothstep(0.0, 0.5, weich) * (1.0 - smoothstep(0.5, 1.0, weich));
	vec3 farbe = ALBEDO * (weich + streulicht * rand * 0.45);
	DIFFUSE_LIGHT += farbe * LIGHT_COLOR * ATTENUATION / PI;
}
