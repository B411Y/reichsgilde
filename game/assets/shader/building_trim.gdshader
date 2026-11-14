// Master-Shader der neu gebauten Gebäude (Style-Guide „3D-Asset-Pipeline“, Abschnitt 5.2; Probe: Stadtansicht.Neubau.cs).
// Alle Häuser einer Gruppe teilen ein Trim-Sheet (waagerechte Bänder, UV1) und dieses eine Material; Putz-, Holz- und
// Akzentfarbe (Fensterläden, Eckenfarbe Rot) stellt das Spiel je Haus als Instanz-Uniform ein – ohne neues Material.
// UV2 trägt die gebackene Verdeckung des einzelnen Hauses (Kontaktschatten unter Dachüberstand und Balken) aus dem AO-Atlas
// der Gruppe. Die Namen der Uniforms stehen so im Style-Guide und bleiben deshalb englisch.
//
// Über den Guide hinaus (Nutzer, 14.11.2026), damit sich die neuen Häuser wie die übrigen Modelle verhalten:
// - Wetter wie in oberflaeche.gdshader (wetter.gdshaderinc): nasses Dach und nasse Wände bei Regen, Schutz unter der Traufe,
//   Schnee auf dem Dach. Welche Fläche Dach ist, sagt das Band (UV.y unter 0,25 = Stroh).
// - Pixel-Look (pixel_look = 1, setzt das Spiel): gröbere Mip-Stufe der Farbe und flachere Normale – ruhige Flächen wie die
//   Pixel-Fassung der Material-Bibliothek statt flimmernder Halme. Das Stroh bekommt dafür gezeichnete Halmstriche: Bahnen
//   von 14 cm Breite und etwa einem halben Meter Länge in drei Helligkeiten, die auch in groben Bildpunkten lesbar bleiben.
shader_type spatial;

#include "res://assets/shader/wetter.gdshaderinc"

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D orm_tex : filter_linear_mipmap_anisotropic;
uniform sampler2D ao_uv2_tex : filter_linear; // Gebackene Haus-AO auf UV2

uniform float normal_strength : hint_range(0.0, 2.0) = 1.0;
uniform float ao_uv2_strength : hint_range(0.0, 1.0) = 0.75;
uniform float pixel_look = 0.0;                 // 1 im Pixel-Look
uniform float pixel_mip = 2.5;                  // um so viele Mip-Stufen gröber im Pixel-Look
uniform float ao_grund : hint_range(0.0, 1.0) = 0.68;   // Grundmaß der Verdeckung: so dunkel wie die übrigen Modelle im Schatten und bei Nacht (Umgebungslicht)
uniform float ao_stroh : hint_range(0.0, 1.0) = 0.6;
uniform float wetter_an = 1.0;                  // 0 für Innenräume (kein Regen im Haus)

// Pro Gebäude-Instanz in Godot variierbar (ohne neues Material!):
instance uniform vec3 plaster_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 wood_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 accent_tint : source_color = vec3(1.0, 1.0, 1.0); // z.B. Fensterläden

varying vec3 welt_ort;
varying vec3 welt_normale;
varying float fuss_hoehe;

void vertex() {
	welt_ort = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	welt_normale = normalize(mat3(MODEL_MATRIX) * NORMAL);
	fuss_hoehe = VERTEX.y;
}

void fragment() {
	float grob = pixel_look * pixel_mip;
	vec4 tex = texture(albedo_tex, UV, grob);
	vec3 orm = texture(orm_tex, UV, grob).rgb;
	float house_ao = texture(ao_uv2_tex, UV2).r;

	vec3 final_color = tex.rgb;

	// Pixel-Look: Halmstriche im Stroh (nur die Dachfläche, nicht der Fransenstreifen)
	if (pixel_look > 0.5 && UV.y < 0.1875) {
		float bahn = floor(UV.x * 4.0 / 0.14);
		float versatz = fract(sin(bahn * 12.9898) * 43758.5453);
		float stueck = floor(UV.y / 0.1875 * 1.55 / 0.55 + versatz);
		float zufall = fract(sin(dot(vec2(bahn, stueck), vec2(127.1, 311.7))) * 43758.5453);
		final_color *= 0.82 + 0.18 * floor(zufall * 3.0);   // drei Stufen: dunkel, mittel, hell
	}

	// 1. Variation über Trim-Sheet-Bänder (UV.y)
	if (UV.y >= 0.25 && UV.y < 0.50) {
		final_color *= wood_tint;
	} else if (UV.y >= 0.50 && UV.y < 0.70) {
		final_color *= plaster_tint;
	}

	// 2. Variation über Vertex-Color-Maske (z.B. Fensterläden auf Rot-Kanal)
	final_color = mix(final_color, final_color * accent_tint, COLOR.r);

	float rauheit = orm.g;
	vec3 schimmer = vec3(0.0);
	float schnee = 0.0;   // wie weit Schnee die Fläche deckt: dort keine Halme (Normale) und keine Verdeckung der Textur
	if (wetter_an > 0.5) {
		// Wetter wie bei den übrigen Modellen: dach = Ort in der Dachebene (Meter; das Strohband ist 4 m breit, eine Stufe 1,55 m hoch)
		bool stroh = UV.y < 0.25;
		vec3 wn = normalize(welt_normale);
		// Das steile Strohdach (52°) hält den Schnee wie die flacheren Dächer der übrigen Häuser: Normale für das Wetter etwas aufgerichtet
		if (stroh && wn.y > 0.2) wn = normalize(wn + vec3(0.0, 0.45 + 0.9 * step(0.001, wetter_dach), 0.0));   // bei Schnee wie ein flaches Dach: geschlossene Decke
		vec3 wn_neu = wn;
		vec2 dach = vec2(UV.x * 4.0, (0.1875 - UV.y) / 0.1875 * 1.55);
		vec3 trocken = final_color;
		wetter_flaeche(final_color, rauheit, wn_neu, schimmer, wn, wn, welt_ort, dach, 1.55, stroh ? 1.0 : 0.0, 1.0 - orm.r, fuss_hoehe, TIME, false);
		if (wn_neu != wn) NORMAL = normalize(mat3(VIEW_MATRIX) * wn_neu);
		if (wetter_dach > 0.001) schnee = smoothstep(0.03, 0.3, dot(final_color - trocken, vec3(0.3, 0.59, 0.11)));
	}

	ALBEDO = final_color;
	NORMAL_MAP = texture(normal_tex, UV, grob).rgb;
	NORMAL_MAP_DEPTH = normal_strength * (1.0 - 0.6 * pixel_look) * (1.0 - schnee);
	EMISSION = schimmer;

	// Kombiniert Trim-Sheet-AO mit der individuellen Haus-AO (UV2)
	// Stroh nimmt weniger Umgebungslicht an (ao_stroh): sonst sind die Dächer nachts heller als die übrigen Strohdächer; Schnee hebt das auf
	float grund = mix(ao_grund * (UV.y < 0.25 ? ao_stroh : 1.0), 1.0, schnee);
	AO = grund * mix(orm.r, 1.0, schnee) * mix(1.0, house_ao, ao_uv2_strength * (1.0 - schnee));
	ROUGHNESS = rauheit;
	METALLIC = orm.b;
}
