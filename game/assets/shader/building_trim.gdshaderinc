// Rumpf von building_trim.gdshader und building_trim_kante.gdshader (Neubau der Gebäude): Uniforms, vertex() und fragment().
// Beschreibung im Kopf von building_trim.gdshader. Mit STROHKANTE (building_trim_kante.gdshader) schneidet der Shader die
// Alpha-Cards der Strohkanten aus (Alpha Scissor, kante_schwelle).

#include "res://assets/shader/wetter.gdshaderinc"

uniform sampler2DArray albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2DArray normal_tex : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2DArray orm_tex : filter_linear_mipmap_anisotropic;
uniform sampler2DArray ao_uv2_tex : filter_linear; // Gebackene Haus-AO auf UV2, eine Ebene je Gruppe

uniform float normal_strength : hint_range(0.0, 2.0) = 1.0;
uniform float ao_uv2_strength : hint_range(0.0, 1.0) = 0.75;
uniform float pixel_look = 0.0;                 // 1 im Pixel-Look
uniform float pixel_mip = 2.5;                  // um so viele Mip-Stufen gröber im Pixel-Look
uniform float ao_grund : hint_range(0.0, 1.0) = 0.68;   // Grundmaß der Verdeckung: so dunkel wie die übrigen Modelle im Schatten und bei Nacht (Umgebungslicht)
uniform float ao_stroh : hint_range(0.0, 1.0) = 0.6;
uniform float haus_baender = 1.0;              // 1: Trim-Sheet der Häuser (Bänder: Stroh, Holz, Putz); 0: andere Blätter (Klappbrücke), ohne Strohdach, Holz- und Putzfarbe
uniform float wetter_an = 1.0;                  // 0 für Innenräume (kein Regen im Haus)

// Stein-Mip (Nutzer, 15.11.2026): Mit pixel_mip 2,5 blieb von Bruchstein und Quadern im Pixel-Look nur der Grundton (Zoom nah: Mip 7,65,
// ein Texel ≈ 0,39 m). Auf den Steinbändern ist der Zuschlag kleiner (stein_mip, Standard 1 → Mip 6,15, ein Texel ≈ 0,14 m ≈ 2 Bildpunkte):
// Die Lagenfugen (12 Texel) dunkeln dann ihre Texelzeile um etwa ein Sechstel statt ein Zwanzigstel – sichtbar, aber noch über der
// Abtastgrenze (kein Flimmern beim Schwenken). Steinbänder je Blatt (v-Bereiche, -1 = keiner) setzt das Spiel (SteinMip.cs);
// Standard: Band „bruchstein“ der Häuser (0,85–0,95). Putz, Holz, Stroh und Schiefer behalten pixel_mip.
uniform float stein_mip = 1.0;
uniform vec4 stein_baender = vec4(0.85, 0.95, -1.0, -1.0);
// Häuser-Array: Steinbänder je Ebene aus STEIN_EBENE (Reihenfolge wie Steinebenen.cs, dort derselbe Inhalt, SteinebenenTests vergleicht).
// Eigene Arrays (Innenräume, Rathaus, Brücke) schalten das ab und setzen stein_baender.
uniform bool stein_je_ebene = true;
const vec4 STEIN_EBENE[8] = vec4[8](
	vec4(0.85, 0.95, -1.0, -1.0),   // 0 Mensch Stufe 1: Sockel (K4)
	vec4(0.85, 0.95, -1.0, -1.0),   // 1 Mensch Stufe 2: Mauer (K1)
	vec4(0.50, 0.70, 0.85, 0.95),   // 2 Mensch Stufe 3: Werkstein im Putzband, Mauer (K1)
	vec4(0.50, 0.70, 0.85, 0.95),   // 3 Zwerg Stufe 1: dunkler Quader im Putzband, Bruchstein (K3)
	vec4(0.50, 0.70, 0.85, 0.95),   // 4 Zwerg Stufe 2: dunkler Quader, Bruchstein (K3)
	vec4(0.50, 0.70, 0.85, 0.95),   // 5 Zwerg Stufe 3: dunkler Quader, heller Haustein
	vec4(0.50, 0.70, 0.85, 0.95),   // 6 Elb: Akzent (K5), Flächen (K2)
	vec4(0.50, 0.70, 0.85, 0.95)    // 7 Halbling: wie Elb
);

// Pixel-Look in Innenräumen (wetter_an = 0; aus dem Rathaus-Branch, Commit 9421283b, hier auf Arrays umgestellt): Aus der Nähe
// verwischte die um pixel_mip gröbere Mip-Stufe die Wände (kein Bildpunkt-Raster, nur Brei), und der Putz der Trim-Sheets (Rauheit im
// Mittel 0,53, stellenweise 0,3) glänzte mit der flachen Normale unter den Lampen. Darum innen: jeder Bildpunkt genau ein Texel der
// Mip-Stufe, die die grobe Pixel-Auflösung ohnehin wählt (texelFetch statt Filtern – harte Texel wie die harten Konturen), matt
// (Rauheit mindestens 0,85, Metall 0,45) und die Normale stark gedämpft. Ohne Pixel-Look und außen bleibt alles wie bisher.
vec4 texel_scharf(sampler2DArray t, vec3 uvw, float lod) {
	int stufe = int(floor(max(lod, 0.0) + 0.5));
	ivec2 g = textureSize(t, stufe).xy;
	return texelFetch(t, ivec3(clamp(ivec2(floor(fract(uvw.xy) * vec2(g))), ivec2(0), g - ivec2(1)), int(uvw.z)), stufe);
}

// Pro Gebäude-Instanz in Godot variierbar (ohne neues Material!):
instance uniform vec3 plaster_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 wood_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 accent_tint : source_color = vec3(1.0, 1.0, 1.0); // z.B. Fensterläden
instance uniform int ebene = 0;      // Ebene im Array der Trim-Sheets (Volk x Stufe, Steinebenen.Ebene)
instance uniform int ao_ebene = 0;   // Ebene im Array der AO-Atlanten (Gruppe, Steinebenen.AoEbene)

// Brandschaden als Variante (Kopie des Materials, Stadtansicht.Brandschaden.cs): Ruß tönt die Farbe wie früher die Albedofarbe des
// Grundmaterials (Weiß nach Ruß, Anteil aus NeubauBrand.RussAnteil); 1 = ohne Brand.
uniform vec3 russ_tint : source_color = vec3(1.0);
#ifdef STROHKANTE
uniform float kante_schwelle = 0.5;   // Alpha Scissor der Strohkanten; im Pixel-Look 1,01 (ganz aus), NeubauBrand.KantenSchwelle
#endif

varying vec3 welt_ort;
varying vec3 welt_normale;
varying float fuss_hoehe;

void vertex() {
	welt_ort = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	welt_normale = normalize(mat3(MODEL_MATRIX) * NORMAL);
	fuss_hoehe = VERTEX.y;
}

void fragment() {
	vec3 uvw = vec3(UV, float(ebene));
	vec4 sb = stein_je_ebene ? STEIN_EBENE[clamp(ebene, 0, 7)] : stein_baender;
	bool stein = (UV.y >= sb.x && UV.y < sb.y) || (UV.y >= sb.z && UV.y < sb.w);
	bool scharf = pixel_look > 0.5 && wetter_an < 0.5;   // Pixel-Look im Haus
	float grob = scharf ? 0.0 : pixel_look * (stein ? stein_mip : pixel_mip);
	float lod = textureQueryLod(albedo_tex, UV).y;     // Albedo und ORM sind gleich groß (2048²)
	vec4 tex = scharf ? texel_scharf(albedo_tex, uvw, lod) : texture(albedo_tex, uvw, grob);
	vec3 orm = scharf ? texel_scharf(orm_tex, uvw, lod).rgb : texture(orm_tex, uvw, grob).rgb;
	float house_ao = texture(ao_uv2_tex, vec3(UV2, float(ao_ebene))).r;

	vec3 final_color = tex.rgb;

	// Pixel-Look: Halmstriche im Stroh (nur die Dachfläche, nicht der Fransenstreifen)
	if (haus_baender > 0.5 && pixel_look > 0.5 && UV.y < 0.1875) {
		float bahn = floor(UV.x * 4.0 / 0.14);
		float versatz = fract(sin(bahn * 12.9898) * 43758.5453);
		float stueck = floor(UV.y / 0.1875 * 1.55 / 0.55 + versatz);
		float zufall = fract(sin(dot(vec2(bahn, stueck), vec2(127.1, 311.7))) * 43758.5453);
		final_color *= 0.82 + 0.18 * floor(zufall * 3.0);   // drei Stufen: dunkel, mittel, hell
	}

	// 1. Variation über Trim-Sheet-Bänder (UV.y)
	if (haus_baender < 0.5) {
	} else if (UV.y >= 0.25 && UV.y < 0.50) {
		final_color *= wood_tint;
	} else if (UV.y >= 0.50 && UV.y < 0.70) {
		final_color *= plaster_tint;
	}

	// 2. Variation über Vertex-Color-Maske (z.B. Fensterläden auf Rot-Kanal)
	final_color = mix(final_color, final_color * accent_tint, COLOR.r);
	final_color *= russ_tint;

	float rauheit = orm.g;
	vec3 schimmer = vec3(0.0);
	float schnee = 0.0;   // wie weit Schnee die Fläche deckt: dort keine Halme (Normale) und keine Verdeckung der Textur
	if (wetter_an > 0.5) {
		// Wetter wie bei den übrigen Modellen: dach = Ort in der Dachebene (Meter; das Strohband ist 4 m breit, eine Stufe 1,55 m hoch)
		bool stroh = haus_baender > 0.5 && UV.y < 0.25;
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
	NORMAL_MAP = texture(normal_tex, uvw, grob).rgb;
	NORMAL_MAP_DEPTH = normal_strength * (scharf ? 0.25 : 1.0 - 0.6 * pixel_look) * (1.0 - schnee);
	EMISSION = schimmer;
	if (scharf) {
		rauheit = max(rauheit, mix(0.85, 0.45, orm.b));   // matt; Metall (Beschläge, Messing) behält etwas Glanz
		SPECULAR = 0.25;
	}

	// Kombiniert Trim-Sheet-AO mit der individuellen Haus-AO (UV2)
	// Stroh nimmt weniger Umgebungslicht an (ao_stroh): sonst sind die Dächer nachts heller als die übrigen Strohdächer; Schnee hebt das auf
	float grund = mix(ao_grund * (haus_baender > 0.5 && UV.y < 0.25 ? ao_stroh : 1.0), 1.0, schnee);
	AO = grund * mix(orm.r, 1.0, schnee) * mix(1.0, house_ao, ao_uv2_strength * (1.0 - schnee));
	ROUGHNESS = rauheit;
	METALLIC = orm.b;
#ifdef STROHKANTE
	ALPHA = tex.a;
	ALPHA_SCISSOR_THRESHOLD = kante_schwelle;
#endif
}
