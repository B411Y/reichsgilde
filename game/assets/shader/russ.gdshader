// Ruß nach einem Brand (Blitzschlag, Anschlag): eine zweite Schicht über dem Dachmaterial (next_pass, Stadtansicht.Feuer.cs RussStoff).
// Fleckig verkohlt (fast schwarz, matt) mit hellgrauen Ascheschlieren, die der Falllinie des Dachs folgen – so ist der Ruß auch auf
// dunklem Schiefer zu erkennen (dort fällt die helle Asche auf, auf rotem Ziegel die schwarze Kohle). Das Muster hängt an der Weltlage.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley, specular_disabled;

uniform float staerke = 1.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float rauschen(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { return 0.55 * rauschen(p) + 0.3 * rauschen(p * 2.3 + 7.1) + 0.15 * rauschen(p * 5.1 + 3.7); }

varying vec3 welt;
void vertex() { welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	// Ruß nur stellenweise (etwa ein Drittel der Fläche), nicht über das ganze Dach: große Dächer (Rathaus, Schiefer) waren sonst eine einheitlich dunkle Fläche
	float kohle = smoothstep(0.52, 0.72, fbm(welt.xz * 0.45 + welt.y * 0.2));
	// Asche: in Streifen die Falllinie hinab (lang in y, schmal quer)
	// Flaches Dach (Rathaus): keine Falllinie, y ist überall gleich und das Muster würde zu senkrechten Streifen; dort fleckig in der Fläche
	float flach = smoothstep(0.85, 0.97, (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).y);
	vec2 ap = mix(vec2(welt.x * 1.6 + welt.z * 1.6, welt.y * 0.35), welt.xz * 0.9, flach);
	float asche = smoothstep(0.62, 0.8, fbm(ap)) * (1.0 - kohle) * mix(1.0, 0.6, flach);
	vec3 farbe = mix(vec3(0.03, 0.027, 0.025), vec3(0.30, 0.285, 0.27), asche / max(asche + kohle, 1e-3));
	ALBEDO = farbe;
	ROUGHNESS = 0.95;
	ALPHA = clamp((0.8 * kohle + 0.4 * asche) * staerke, 0.0, 0.85);
}
