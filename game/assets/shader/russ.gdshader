// Ruß nach einem Brand (Blitzschlag, Anschlag): eine zweite Schicht über dem Dachmaterial (next_pass, Stadtansicht.Feuer.cs RussStoff).
// Wie echter Ruß (Recherche 12.11.2026, docs/bereiche/darstellung.md): tiefschwarz und stumpf (kein Glanz), färbt schon dünn das ganze
// Dach dunkler; dazu große, weiche Wolken, wo mehr Rauch abzog.
// Keine Ascheschlieren und kein Muster aus x + z: x + z ist bei der Spielkamera (Gier −45°) die waagrechte Bildachse, daraus wurden
// senkrechte helle Streifen. Das Muster hängt an der Weltlage, ist rund (keine Vorzugsrichtung) und grob (Merkmale ab etwa 1 m), damit es
// im Pixel-Look nicht flimmert; die feinste Stufe blendet aus, sobald sie im Bild zu klein wird.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley, specular_disabled;

uniform float staerke = 1.0;

// Rauschen im Raum (nicht auf der Grundfläche): auf steilen Dächern sonst entlang der Falllinie gestreckt
float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float rauschen(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	float a = mix(mix(hash(i), hash(i + vec3(1, 0, 0)), u.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), u.x), u.y);
	float b = mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), u.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), u.x), u.y);
	return mix(a, b, u.z);
}

varying vec3 welt;
void vertex() { welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	vec3 p = welt * 0.22;
	// feinste Stufe (etwa 1 m) nur, solange sie im Bild groß genug ist (sonst Moiré im Pixel-Look)
	float fein = 1.0 - smoothstep(0.15, 0.3, length(fwidth(p * 4.7)));
	float n = 0.6 * rauschen(p) + 0.4 * rauschen(p * 2.1 + 5.3) + 0.25 * fein * (rauschen(p * 4.7 + 1.9) - 0.5);
	// Schleier überall, in den Wolken dichter (weich, ohne harten Rand)
	float wolke = smoothstep(0.35, 0.8, n);
	ALBEDO = vec3(0.022, 0.02, 0.018);
	ROUGHNESS = 1.0;
	ALPHA = clamp((0.34 + 0.4 * wolke) * staerke, 0.0, 0.85);
}
