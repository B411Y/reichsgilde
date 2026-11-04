// Bäume im Wind (Stadtansicht.Natur.cs, BaumWindStoff): Das Material der Kit-Bäume (natur_kit_baeume: Rinde, Laub, Kiefernnadeln, Totholz) mit
// Textur wie vorher, dazu ein Vertex-Shader: Der Stamm bleibt unten steif, die Krone neigt sich mit dem Wind und wiegt sich (je Baum eigene Phase aus
// seinem Ort, auch die kahlen Winterbäume), die Blätter und Nadeln flattern fein. Die Stärke folgt wetter_wind (dieselbe Größe wie Gras, Laub und
// Fahnen): bei Windstille kaum etwas, im Sturm deutlich. Alles rechnet in der Welt und wird dann in den Modellraum zurückgerechnet, darum gilt es für
// jede Größe und Drehung der Kopie im MultiMesh. Kostet nur ein paar Rechenschritte je Ecke und kein zusätzliches Zeichnen.
shader_type spatial;
render_mode cull_disabled;   // doppelseitig wie die Materialien der GLB

global uniform float wetter_wind;
global uniform vec2 wind_richtung;

uniform vec4 farbe : source_color = vec4(1.0);
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool normale = false;
uniform float normal_staerke = 1.0;
uniform float rauheit = 0.85;
uniform bool maske = false;           // Alpha-Schwelle (Blätter, Nadeln)
uniform float schwelle = 0.45;
uniform bool eckenfarbe = false;
uniform bool eckenfarbe_srgb = true;
// Höhe des Baums im Modell (Meter); 0 = steif. flattern: 1 für Blätter und Nadeln, 0 für Rinde.
uniform float hoehe = 8.0;
uniform float flattern = 0.0;

varying vec3 v_eck;

void vertex() {
	v_eck = COLOR.rgb;
	vec3 m0 = MODEL_MATRIX[0].xyz;
	float maszstab = max(length(m0), 0.0001);
	vec3 mittelpunkt = MODEL_MATRIX[3].xyz;
	// Biegung: Fuß steif, Krone frei (quadratisch mit der Höhe über dem Boden)
	float hf = clamp(VERTEX.y / max(hoehe, 0.1), 0.0, 1.0);
	float bieg = hf * hf;
	float w = clamp(wetter_wind, 0.0, 1.0);
	float ph = dot(mittelpunkt.xz, vec2(0.23, 0.17));    // je Baum eigene Phase (auch kahle Winterbäume)
	float t = TIME;
	float s = sin(t * (0.9 + 1.5 * w) + ph) * 0.65 + sin(t * 2.3 + ph * 1.7) * 0.35;
	float hohe_welt = hoehe * maszstab;
	float amp = hohe_welt * (0.006 + 0.085 * w * w) * bieg;
	vec2 r = normalize(wind_richtung + vec2(0.0001));
	// Neigung mit dem Wind (stärker bei Wind), dazu Wiegen vor und zurück und ein wenig quer
	vec3 aus = vec3(r.x, 0.0, r.y) * (s * 0.7 + w * 1.1) * amp + vec3(-r.y, 0.0, r.x) * sin(t * 1.3 + ph * 2.1) * 0.25 * amp;
	// feines Flattern: Blätter und Nadeln, je Ecke etwas anders, nur in der Krone
	float fp = dot(VERTEX, vec3(3.1, 2.3, 2.7)) + ph;
	vec3 flatter = vec3(sin(t * (7.0 + 5.0 * w) + fp), sin(t * (6.0 + 4.0 * w) + fp * 1.3) * 0.6, cos(t * (8.0 + 5.0 * w) + fp * 0.7))
		* hohe_welt * (0.002 + 0.016 * w) * flattern * smoothstep(0.25, 0.6, hf);
	vec3 ges = aus + flatter;
	VERTEX += transpose(mat3(MODEL_MATRIX)) * ges / (maszstab * maszstab);
}

void fragment() {
	vec4 t = texture(albedo_tex, UV);
	vec3 c = farbe.rgb * t.rgb;
	if (eckenfarbe) {
		vec3 e = v_eck;
		if (eckenfarbe_srgb && !OUTPUT_IS_SRGB) {
			e = mix(pow((e + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), e * (1.0 / 12.92), lessThan(e, vec3(0.04045)));
		}
		c *= e;
	}
	ALBEDO = c;
	ROUGHNESS = rauheit;
	if (normale) {
		NORMAL_MAP = texture(normal_tex, UV).rgb;
		NORMAL_MAP_DEPTH = normal_staerke;
	}
	if (maske) {
		ALPHA = t.a * farbe.a;
		ALPHA_SCISSOR_THRESHOLD = schwelle;
	}
}
