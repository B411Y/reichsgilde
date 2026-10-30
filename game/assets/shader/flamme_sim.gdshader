// Flamme aus der Blender-Feuersimulation (Feuer.cs; seit Phase 2 die einzige Flamme, für alle Feuer und Grafikstufen): ein Flipbook
// (texturen/feuer/flamme_ruhig.png oder flamme_wild.png, 16 × 8 Bilder einer gebackenen Mantaflow-Simulation, erzeugt von
// tools/feuer/flammen_sim.py). Die Farbe ist vormultipliziert (RGB = Leuchten, A = Deckung): darum blend_premul_alpha – wo die Flamme nur
// leuchtet, addiert sie Licht, wo sie dicht ist, deckt sie.
//
// Je Zunge eine Fläche, die der Kamera folgt. Das Bild wählt der Shader aus der gemeinsamen Uhr (zeit · bildrate) und dem Startwert der Zunge;
// zwei aufeinanderfolgende Bilder werden überblendet, ein leichtes Wabern aus dem Rauschen verdeckt das Überblenden. Dazu auf Wunsch Funken:
// kleine glühende Punkte, die steigen, seitlich abtreiben und verlöschen. Eine Fläche, ein Zeichenaufruf je Zunge, kein Partikelsystem.
// Der Startwert je Zunge steckt in der Tiefe (Skalierung in Z) der flachen Fläche, nicht in einem Instanz-Uniform.
//
// Die Fläche ist so groß wie ein Bild des Flipbooks; Feuer.cs rechnet die Größe aus den Kennwerten der Simulation (flamme_<variante>.json:
// Höhe und Breite der Flamme im Bild, Lage des Fußes).
shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never, cull_disabled, fog_disabled;

#include "licht_flackern.gdshaderinc"

global uniform vec2 wind_richtung;
global uniform float wetter_wind;

uniform sampler2D atlas : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D rausch : repeat_enable, filter_linear_mipmap;
uniform float zeit = 0.0;
// Spalten und Zeilen des Atlas, Zahl der Bilder, Bilder je Sekunde
uniform vec2 raster = vec2(16.0, 8.0);
uniform float bilder = 128.0;
uniform float bildrate = 30.0;
// Bildpunkte eines Bildes (für den Rand, damit kein Nachbarbild durchscheint)
uniform vec2 bild_pixel = vec2(128.0, 256.0);
// 2 = ganz zur Kamera, 1 = senkrecht zur Kamera, 0 = fest
uniform int billboard = 2;
// HDR-Helligkeit (über 1 erfasst der Bloom den Kern) und wie stark die Flamme deckt
uniform float helligkeit = 1.8;
uniform float deckung = 1.0;
// Farbe, mit der das Leuchten gefärbt wird (1 = wie gebacken; Esse etwas röter)
uniform vec3 toenung : source_color = vec3(1.0);
// Wabern: wie weit (in Bildbreiten) das Rauschen die Flamme verschiebt
uniform float wabern = 0.012;
// Wie stark der Wind die Flamme oben zur Seite schiebt (in Bildbreiten bei voller Windstärke)
uniform float wind_neigung = 0.12;
// Wo die Flamme im Bild steht (Kennwerte der Simulation): Höhe und Breite in Anteilen des Bildes, Fuß über dem unteren Rand
uniform float flamme_hoehe = 0.6;
uniform float flamme_breite = 0.5;
uniform float fuss = 0.06;
// Funken: Zahl (höchstens 12), Steighöhe in Flammenhöhen, Größe in Metern; groesse_m = Breite und Höhe der Flamme in Metern
uniform int funken = 0;
uniform float funken_hoehe = 2.5;
uniform float funken_groesse = 0.012;
uniform vec2 groesse_m = vec2(0.2, 0.42);

varying float versatz;
varying float spiegeln;
varying float neigung;
varying flat uint saat;

void vertex() {
	// Startwert der Zunge: steckt in der Tiefe der flachen Fläche (Skalierung Z / X − 1, 256 Stufen; Feuer.FlaechenSetzen). Kein Instanz-Uniform:
	// das belegte je Fläche Platz im knappen Puffer der Grafikkarte
	float tiefe = length(MODEL_MATRIX[2].xyz) / max(length(MODEL_MATRIX[0].xyz), 1e-6) - 1.0;
	float phase = (floor(clamp(tiefe, 0.0, 0.9999) * 256.0) + 0.5) / 256.0;
	saat = uint(phase * 65535.0) + 7u;
	versatz = f_hash(uvec4(saat, 23u, 0u, 0u));
	spiegeln = step(0.5, f_hash(uvec4(saat, 29u, 0u, 0u)));
	if (billboard == 2) {
		float sx = length(MODEL_MATRIX[0].xyz);
		float sy = length(MODEL_MATRIX[1].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, INV_VIEW_MATRIX[1] * sy, INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz) * wetter_wind * wind_neigung;
	} else if (billboard == 1) {
		float sx = length(MODEL_MATRIX[0].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * sx, MODEL_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), INV_VIEW_MATRIX[0].xz) * wetter_wind * wind_neigung;
	} else {
		neigung = dot(normalize(wind_richtung + vec2(0.0001)), normalize(MODEL_MATRIX[0].xyz).xz) * wetter_wind * wind_neigung;
	}
}

// Ein Bild des Atlas an der Stelle uv (0–1 im Bild)
vec4 bild_lesen(float nr, vec2 uv) {
	vec2 rand = 0.5 / bild_pixel;
	uv = clamp(uv, rand, 1.0 - rand);
	vec2 zelle = vec2(mod(nr, raster.x), floor(nr / raster.x));
	return texture(atlas, (zelle + uv) / raster);
}

void fragment() {
	float h = 1.0 - UV.y;                        // 0 unten … 1 oben im Bild
	vec2 uv = UV;
	if (spiegeln > 0.5) uv.x = 1.0 - uv.x;
	// Wabern: die Luft über der Flamme flimmert; unten fast nichts, nach oben mehr. Dazu der Wind
	float t = zeit;
	vec2 n = texture(rausch, vec2(uv.x * 1.3 + versatz, h * 1.1 - t * 0.9)).rg - 0.5;
	uv.x += n.x * wabern * (0.2 + h) - neigung * h * h;
	uv.y += n.y * wabern * 0.6 * h;

	float lauf = t * bildrate + versatz * bilder;
	float b0 = mod(floor(lauf), bilder);
	float b1 = mod(b0 + 1.0, bilder);
	float w = fract(lauf);
	vec4 c = mix(bild_lesen(b0, uv), bild_lesen(b1, uv), w);

	vec3 rgb = c.rgb * toenung * helligkeit;
	float a = clamp(c.a * deckung, 0.0, 1.0);

	{
		// Funken: in Flammeneinheiten – x in halben Flammenbreiten, y in Flammenhöhen über dem Fuß
		float fx = (UV.x - 0.5) * 2.0 / flamme_breite;
		float fh = (h - fuss) / flamme_hoehe;
		for (int i = 0; i < funken; i++) {
			float dauer = 0.9 + 0.9 * f_hash(uvec4(saat, uint(i), 11u, 0u));
			float lauf_f = zeit / dauer + f_hash(uvec4(saat, uint(i), 13u, 0u));
			uint umlauf = uint(int(floor(lauf_f)) & 0xFFFFFF);
			float leben = fract(lauf_f);
			float zx = f_hash(uvec4(saat, uint(i), umlauf, 17u));
			float zy = f_hash(uvec4(saat, uint(i), umlauf, 19u));
			float px = (zx - 0.5) * 0.9 + (zy - 0.5) * 0.9 * leben + neigung * 8.0 * leben * leben + 0.08 * sin(leben * 9.0 + zx * 6.28);
			float py = 0.35 + leben * funken_hoehe * (0.5 + 0.5 * zy);
			vec2 abstand = vec2((fx - px) * groesse_m.x * 0.5, (fh - py) * groesse_m.y) / funken_groesse;
			float r2 = dot(abstand, abstand * vec2(1.0, 0.45));
			float glimmen = (1.0 - leben) * (1.0 - leben) * (0.6 + 0.4 * sin(zeit * 31.0 + zx * 40.0));
			// Funken oben im Bild verlöschen, bevor sie den Rand erreichen
			rgb += vec3(1.0, 0.65, 0.2) * exp(-r2 * 1.2) * glimmen * 2.2 * step(0.0, py) * (1.0 - smoothstep(0.85, 0.98, h));
		}
	}
	ALBEDO = rgb;
	ALPHA = a;
}
