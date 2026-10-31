// Charakter Phase 3: Brauen und Wimpern als Haarkarten (eigener Code, MIT). Bild: tools/charakter/kopf3_haartexturen.py
// (R = Zufallswert des Haars, G = Abstand der Wurzel zur Brauenmitte, B = Lage entlang des Haars, A = Deckung).
// Dichte und Breite aus den Genen schneiden ganze Haare weg; graue Haare einzeln (Salz und Pfeffer). Kanten über
// Alpha-to-Coverage (braucht MSAA), sonst Alpha-Schere; in der Ferne (Mipmaps) wird die Deckung angehoben, damit die
// feinen Haare nicht verschwinden. Phase 4: Wimpern in vier Bändern (Unterlid, Oberlid Haupt-, lange und Fülllage),
// Brauen dünner und lichter; das Bild ist dasselbe Format.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D bild : hint_default_transparent, filter_linear_mipmap_anisotropic, repeat_disable;
uniform vec3 farbe : source_color = vec3(0.12, 0.08, 0.05);
uniform vec3 farbe_grau : source_color = vec3(0.78, 0.77, 0.75);
uniform float grau = 0.0;           // Anteil grauer Haare
uniform float dichte = 1.0;         // Haare mit Zufallswert < dichte bleiben
uniform float breite = 1.0;         // nur Brauen: Haare mit Wurzelabstand < breite bleiben
uniform float schwelle = 0.3;       // Phase 4: tiefer (feinere, lichtere Haare; Kanten über Alpha-to-Coverage)
uniform float dunkel = 0.6;         // Haare verschatten sich gegenseitig
// Phase 6 (S7): Brauenformen als Bildarray; ebene < 0 = „bild“ wie Phase 3–5
uniform sampler2DArray bilder6 : hint_default_transparent, filter_linear_mipmap_anisotropic, repeat_disable;
uniform float ebene = -1.0;
uniform float deckung = 1.0;        // Halt B2 (Weg A): Brauen weniger deckend (weiche Haargruppen statt Aufkleber)
// Etappe C (nur Wimpern; Prüferbefund „Unterlid wie ein Strich“): Band 0 des Wimpernbilds (Unterlid, UV.y > 0,75)
// lichter, dünner und heller, die Wurzeln laufen weich aus (keine dunkle Naht am Lidrand). Standard = Phase 5.
uniform float unten_deckung = 1.0;
uniform float unten_wurzel = 0.0;   // 0 = Wurzel voll, 1 = Wurzel ausgeblendet
uniform float unten_hell = 0.0;     // Anteil Hautton
uniform vec3 haut_ton : source_color = vec3(0.6, 0.45, 0.38);

void fragment() {
	vec4 t = ebene >= 0.0 ? texture(bilder6, vec3(UV, ebene)) : texture(bild, UV);
	vec2 px = UV * (ebene >= 0.0 ? vec2(textureSize(bilder6, 0).xy) : vec2(textureSize(bild, 0)));
	float lod = max(0.0, 0.5 * log2(max(dot(dFdx(px), dFdx(px)), dot(dFdy(px), dFdy(px)))));
	float a = clamp(t.a * (1.0 + 0.45 * lod), 0.0, 1.0);
	a *= 1.0 - smoothstep(dichte - 0.03, dichte, t.r);
	a *= 1.0 - smoothstep(breite - 0.06, breite, t.g);
	// Zufall je Haar aus R (langsam veränderlich, damit gefilterte Ränder nicht flimmern)
	float zufall = fract(t.r * 5.71 + 0.3);
	vec3 c = farbe * (0.8 + 0.4 * zufall);
	c = mix(c, farbe_grau * (0.85 + 0.3 * zufall), smoothstep(-0.04, 0.04, grau - fract(t.r * 2.37 + 0.11)));
	c *= mix(0.85, 1.2, t.b);                    // Spitzen heller
	float unten = step(0.75, UV.y) * step(ebene, -0.5);
	a *= mix(1.0, unten_deckung * mix(1.0, smoothstep(0.0, 0.45, t.b), unten_wurzel), unten);
	c = mix(c, mix(c, haut_ton * 0.55, unten_hell), unten);
	ALBEDO = c * dunkel;
	ALPHA = a * deckung;
	ALPHA_SCISSOR_THRESHOLD = schwelle;
	ALPHA_ANTIALIASING_EDGE = schwelle;
	ALPHA_TEXTURE_COORDINATE = px;
	ROUGHNESS = 0.5;
	SPECULAR = 0.35;
	BACKLIGHT = c * 0.25;
}
