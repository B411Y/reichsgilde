// Rumpf von building_trim.gdshader und building_trim_kante.gdshader (Neubau der Gebäude): Uniforms, vertex() und fragment().
// Beschreibung im Kopf von building_trim.gdshader. Mit STROHKANTE (building_trim_kante.gdshader) schneidet der Shader die
// Alpha-Cards der Strohkanten (Rathaus: Schieferkanten) aus (Alpha Scissor, kante_schwelle).

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
uniform float stroh_dach = 1.0;                 // 0: das Dachband (UV.y unter 0,25) ist Schiefer (Rathaus) – keine Halmstriche im Pixel-Look
uniform float haus_baender = 1.0;              // 1: Trim-Sheet der Häuser (Bänder: Stroh, Holz, Putz); 0: andere Blätter (Klappbrücke), ohne Strohdach, Holz- und Putzfarbe
uniform float wetter_an = 1.0;                  // 0 für Innenräume (kein Regen im Haus)

// Stein-Mip (Nutzer, 15.11.2026): Mit pixel_mip 2,5 blieb von Bruchstein und Quadern im Pixel-Look nur der Grundton (Zoom nah: Mip 7,65,
// ein Texel ≈ 0,39 m). Auf den Steinbändern ist der Zuschlag kleiner (stein_mip, Standard 1 → Mip 6,15, ein Texel ≈ 0,14 m ≈ 2 Bildpunkte):
// Die Lagenfugen (12 Texel) dunkeln dann ihre Texelzeile um etwa ein Sechstel statt ein Zwanzigstel – sichtbar, aber noch über der
// Abtastgrenze (kein Flimmern beim Schwenken). Steinbänder je Blatt (v-Bereiche, -1 = keiner) setzt das Spiel (SteinMip.cs);
// Standard: Band „bruchstein“ der Häuser (0,85–0,95). Putz, Holz, Stroh und Schiefer behalten pixel_mip.
uniform float stein_mip = 1.0;
uniform float stein_ruhe = 0.5;   // Pixel-Look: Anteil der groben Mip-Stufe (pixel_mip) in der Farbe der Steinbänder
uniform vec4 stein_baender = vec4(0.85, 0.95, -1.0, -1.0);
// Häuser-Array: Steinbänder je Ebene aus STEIN_EBENE (Reihenfolge wie Steinebenen.cs, dort derselbe Inhalt, SteinebenenTests vergleicht).
// Eigene Arrays (Innenräume, Rathaus, Brücke) schalten das ab und setzen stein_baender.
uniform bool stein_je_ebene = true;
const vec4 STEIN_EBENE[12] = vec4[12](
	vec4(0.85, 0.95, -1.0, -1.0),   // 0 Mensch Stufe 1: Sockel (K4)
	vec4(0.85, 0.95, -1.0, -1.0),   // 1 Mensch Stufe 2: Mauer (K1)
	vec4(0.50, 0.70, 0.85, 0.95),   // 2 Mensch Stufe 3: Werkstein im Putzband, Mauer (K1)
	vec4(0.50, 0.70, 0.85, 0.95),   // 3 Zwerg Stufe 1: dunkler Quader im Putzband, Bruchstein (K3)
	vec4(0.50, 0.70, 0.85, 0.95),   // 4 Zwerg Stufe 2: dunkler Quader, Bruchstein (K3)
	vec4(0.50, 0.70, 0.85, 0.95),   // 5 Zwerg Stufe 3: dunkler Quader, heller Haustein
	vec4(0.85, 0.95, -1.0, -1.0),   // 6 Elb Stufe 1: Haustein (K2) im Bruchsteinband, im Putzband Kalkputz
	vec4(0.85, 0.95, -1.0, -1.0),   // 7 Halbling Stufe 1: K2 im Bruchsteinband (Sockel, Schlot), im Putzband Lehmputz
	vec4(0.85, 0.95, -1.0, -1.0),   // 8 Elb Stufe 2: Haustein (K2) im Bruchsteinband, im Putzband Kalkputz (Putzsockel, Gefache)
	vec4(0.50, 0.70, 0.85, 0.95),   // 9 Elb Stufe 3: Werkstein (K5) im Putzband, Mauer (K2)
	vec4(0.85, 0.95, -1.0, -1.0),   // 10 Halbling Stufe 2: Backstein K6 im Bruchsteinband (Erdgeschoss), im Putzband Lehmputz
	vec4(0.50, 0.70, 0.85, 0.95)    // 11 Halbling Stufe 3: Werkstein im Putzband, Mauer Backstein K6
);

// Pixel-Look, reine Bänder (Klicktest NAS 19.11.2026, Halblinge Stufe 2/3): Die grobe Mip-Stufe (aussen etwa 7,6 bis 10,2) mischt die
// Nachbarbänder bei. Das Dachband „stroh_franse“ ist nur 128 Bildpunkte hoch: Der blaue Schiefer wurde mit dem Kupfer darüber (Stufe 3)
// dunkelgrün, mit dem Holz darunter (Stufe 2) grau, die Rauheit fiel von 0,85 auf 0,65 (Glanzfleck). Das Backsteinband bekam oben das
// helle Farbholz, unten das dunkle Eisen (Metall 0,2) bei: je Wandstreifen von 0,4 m oben hell, unten dunkel, wie Bohlen eines
// Blockhauses. Für die Ebenen mit BAND_REIN liest der Shader im Pixel-Look die Dachbänder und Steinbänder darum je Mip-Stufe nur aus
// Texeln, die ganz im eigenen Band liegen (v auf deren Mitten geklemmt), höchstens bis zur gröbsten Stufe, die noch einen solchen
// Texel hat (Dach 7 bzw. 8, Backstein 7; bei Zoom 100 mit Pixel 3, Grundstufe 7,7, bis 0,7 Stufen feiner als der Bildpunkt: im reinen
// Band ist die Stufe 7 schon so gemittelt, dass nichts flimmert). Nachgerechnet: tools/modelle/neubau/pixel_mip_pruefung.py.
// Seit 19.11.2026 (Nutzer) für alle Ebenen: nachgerechnet je Volk und Stufe mischte die grobe Stufe überall die Nachbarbänder bei
// (Stroh, Ziegel, Schiefer: Farbe bis 0,23, Rauheit bis −0,2; Steinbänder: Metall bis +0,25 aus dem Eisenband, Streifen), mit band_lesen
// höchstens 0,03. Ohne Pixel-Look greift der Zweig nicht (pixel_look > 0,5), dort bleibt alles bildpunktgleich.
const float BAND_REIN[12] = float[12](1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);   // 0 = Ebene ohne reine Bänder
// Geteiltes Band „stroh“ (Halbling Stufe 3, belebt 19.11.2026): oben Efeu (v bis 0,125), darunter Kupfer – zwei reine Bänder; 0 = ungeteilt
const float STROH_TEIL[12] = float[12](0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.125);

float band_y(float y, float a, float e, float m) {
	float t = exp2(m);
	float lo = (ceil(a / t) + 0.5) * t, hi = (floor(e / t) - 0.5) * t;
	return lo <= hi ? clamp(y, lo, hi) : 0.5 * (a + e);
}

vec4 band_lesen(sampler2DArray t, vec3 uvw, float v0, float v1, float stufe) {
	float h = float(textureSize(t, 0).y);
	float a = v0 * h, e = v1 * h;
	float mmax = 0.0;
	for (int k = 1; k <= 11; k++) {
		float s = exp2(float(k));
		if (floor(e / s) - 1.0 >= ceil(a / s)) mmax = float(k);
	}
	float st = clamp(stufe, 0.0, mmax);
	float m0 = floor(st);
	float y = fract(uvw.y) * h;
	vec4 c0 = textureLod(t, vec3(uvw.x, band_y(y, a, e, m0) / h, uvw.z), m0);
	vec4 c1 = textureLod(t, vec3(uvw.x, band_y(y, a, e, m0 + 1.0) / h, uvw.z), m0 + 1.0);
	return mix(c0, c1, st - m0);
}

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

#ifdef ZEILE
// Häuserzeilen (building_trim_zeile*.gdshader, Stadtansicht.Zeilen.cs): eine Zeile ist EIN verschmolzenes Netz aus Häusern
// verschiedener Völker, Stufen und Farben. Was sonst Instanz-Uniform ist, trägt jede Ecke: CUSTOM0 = (Putzfarbe, Ebene),
// CUSTOM1 = (Akzentfarbe, AO-Ebene im Array der Zeilen-Atlanten zeilen_ao_ebenen.png).
varying flat vec4 zeile_a;
varying flat vec4 zeile_b;
const vec3 efeu_zufall = vec3(0.0, 2.0, 0.0);   // Zeilen: kein Efeu-Zufall
#else
// Pro Gebäude-Instanz in Godot variierbar (ohne neues Material!):
instance uniform vec3 plaster_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 wood_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 accent_tint : source_color = vec3(1.0, 1.0, 1.0); // z.B. Fensterläden
instance uniform int ebene = 0;      // Ebene im Array der Trim-Sheets (Volk x Stufe, Steinebenen.Ebene)
instance uniform int ao_ebene = 0;   // Ebene im Array der AO-Atlanten (Gruppe, Steinebenen.AoEbene)
// Zufall des Efeus je Haus (EfeuZufall.cs, 21.11.2026): x = Versatz des Rauschfelds 0..1, y = Dichte (Anteil bewachsen, über 1 = nichts ausblenden),
// z = Farbton -1..1 (Grün gelblicher bzw. bläulicher). Wirkt nur auf Blattkarten (efeu_blatt).
instance uniform vec3 efeu_zufall = vec3(0.0, 2.0, 0.0);
#endif

// Brandschaden als Variante (Kopie des Materials, Stadtansicht.Brandschaden.cs): Ruß tönt die Farbe wie früher die Albedofarbe des
// Grundmaterials (Weiß nach Ruß, Anteil aus NeubauBrand.RussAnteil); 1 = ohne Brand.
uniform vec3 russ_tint : source_color = vec3(1.0);
#ifdef STROHKANTE
uniform float kante_schwelle = 0.5;   // Alpha Scissor der Strohkanten; im Pixel-Look 1,01 (ganz aus), NeubauBrand.KantenSchwelle
// Efeublatt (Halbling Stufe 3, Nutzer 19.11.2026): Blattkarten mit Alpha im Efeu-Teil des Bands „stroh“ (STROH_TEIL). Anders als die
// Strohfransen bleiben sie im Pixel-Look sichtbar: Schwelle immer 0,5, das Alpha aus der feinen Mip-Stufe ohne Pixel-Zuschlag (und nur aus
// dem Efeu-Band, nicht aus dem Kupfer darunter); die Farbe kommt wie sonst aus dem reinen Band (band_lesen).
uniform bool efeu_blatt = false;
// Die Blattbilder liegen nur in einer Ebene (Halbling Stufe 3). Blattkarten anderer Häuser (Elben, 21.11.2026) lesen deshalb diese
// Ebene statt der ihres Hauses: efeu_ebene ab 0 (Stadtansicht.Neubau.cs, Steinebenen.EfeuEbene), -1 = Ebene des Hauses.
uniform int efeu_ebene = -1;
#endif

varying vec3 welt_ort;
varying vec3 modell_ort;   // Ort im Modell (Meter): bei Blattkarten Grundlage der Auswahl, welche Bereiche bewachsen sind
varying vec3 welt_normale;
varying float fuss_hoehe;

#ifdef STROHKANTE
// Wertrauschen im Raum für den Efeu-Zufall (kein Zeichenaufruf, nur Rechnung im Fragment der Blattkarten)
float efeu_hash(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float efeu_rauschen(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(efeu_hash(i), efeu_hash(i + vec3(1, 0, 0)), f.x), mix(efeu_hash(i + vec3(0, 1, 0)), efeu_hash(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(efeu_hash(i + vec3(0, 0, 1)), efeu_hash(i + vec3(1, 0, 1)), f.x), mix(efeu_hash(i + vec3(0, 1, 1)), efeu_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
#endif

void vertex() {
	welt_ort = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	welt_normale = normalize(mat3(MODEL_MATRIX) * NORMAL);
	fuss_hoehe = VERTEX.y;
	modell_ort = VERTEX;
#ifdef ZEILE
	zeile_a = CUSTOM0;
	zeile_b = CUSTOM1;
#endif
}

void fragment() {
#ifdef ZEILE
	int ebene = int(zeile_a.w + 0.5);
	int ao_ebene = int(zeile_b.w + 0.5);
	vec3 plaster_tint = zeile_a.rgb;
	vec3 accent_tint = zeile_b.rgb;
	vec3 wood_tint = vec3(1.0);
#endif
	int eb = ebene;   // Ebene des Hauses (bei ZEILE aus den Instanzdaten); Blattkarten lesen die Ebene der Blattbilder
#ifdef STROHKANTE
	if (efeu_blatt && efeu_ebene >= 0) eb = efeu_ebene;
#endif
	vec3 uvw = vec3(UV, float(eb));
	vec4 sb = stein_je_ebene ? STEIN_EBENE[clamp(eb, 0, 11)] : stein_baender;
	bool stein = (UV.y >= sb.x && UV.y < sb.y) || (UV.y >= sb.z && UV.y < sb.w);
	bool scharf = pixel_look > 0.5 && wetter_an < 0.5;   // Pixel-Look im Haus
	float grob = scharf ? 0.0 : pixel_look * (stein ? stein_mip : pixel_mip);
	float grob_licht = scharf ? 0.0 : pixel_look * pixel_mip;   // Rauheit, Verdeckung und Normale immer so grob wie der Putz: nur die FARBE der Steinbänder bleibt feiner
	float lod = textureQueryLod(albedo_tex, UV).y;     // Albedo und ORM sind gleich groß (2048²)

	vec4 tex = scharf ? texel_scharf(albedo_tex, uvw, lod) : texture(albedo_tex, uvw, grob);
	if (!scharf && stein && pixel_look > 0.5) tex = mix(tex, texture(albedo_tex, uvw, grob_licht), stein_ruhe);
	vec3 orm = scharf ? texel_scharf(orm_tex, uvw, lod).rgb : texture(orm_tex, uvw, grob_licht).rgb;
	vec3 normal_tx = texture(normal_tex, uvw, grob_licht).rgb;
	// Reine Bänder (BAND_REIN, siehe oben): Dachbänder „stroh“ und „stroh_franse“, die Steinbänder der Ebene
	float yb = fract(UV.y);
	if (!scharf && pixel_look > 0.5 && haus_baender > 0.5 && stein_je_ebene && BAND_REIN[clamp(eb, 0, 11)] > 0.5 && (yb < 0.25 || stein)) {
		vec2 bd = yb < 0.1875 ? vec2(0.0, 0.1875) : (yb < 0.25 ? vec2(0.1875, 0.25) : ((UV.y >= sb.x && UV.y < sb.y) ? sb.xy : sb.zw));
		float teil = STROH_TEIL[clamp(eb, 0, 11)];
		if (teil > 0.0 && yb < 0.1875) bd = yb < teil ? vec2(0.0, teil) : vec2(teil, 0.1875);
		tex = band_lesen(albedo_tex, uvw, bd.x, bd.y, lod + grob);
		if (stein) tex = mix(tex, band_lesen(albedo_tex, uvw, bd.x, bd.y, lod + grob_licht), stein_ruhe);
		orm = band_lesen(orm_tex, uvw, bd.x, bd.y, lod + grob_licht).rgb;
		normal_tx = band_lesen(normal_tex, uvw, bd.x, bd.y, lod + grob_licht).rgb;
	}
	float house_ao = texture(ao_uv2_tex, vec3(UV2, float(ao_ebene))).r;
#ifdef STROHKANTE
	if (efeu_blatt) house_ao = 1.0;   // Blattkarten: die gebackene Verdeckung (Karten hintereinander, deckend gebacken) gilt fuer sie nicht
#endif

	vec3 final_color = tex.rgb;

	// Pixel-Look: Halmstriche im Stroh (nur die Dachfläche, nicht der Fransenstreifen)
	bool efeu = false;
#ifdef STROHKANTE
	efeu = efeu_blatt;
#endif
	if (haus_baender > 0.5 && stroh_dach > 0.5 && pixel_look > 0.5 && UV.y < 0.1875 && !efeu) {
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
#ifdef STROHKANTE
	if (efeu_blatt) {
		// Grün je Haus leicht verschoben (gelblicher bzw. bläulicher), dazu fleckenweise etwas heller oder dunkler
		float fleck = efeu_rauschen(modell_ort * 0.9 + vec3(efeu_zufall.x * 23.0));
		final_color *= vec3(1.0 + 0.16 * efeu_zufall.z, 1.0, 1.0 - 0.20 * efeu_zufall.z) * (0.88 + 0.24 * fleck);
	}
#endif

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
	NORMAL_MAP = normal_tx;
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
	if (efeu_blatt) {
		float teil_e = STROH_TEIL[clamp(eb, 0, 11)];
		ALPHA = teil_e > 0.0 ? band_lesen(albedo_tex, uvw, 0.0, teil_e, lod).a : texture(albedo_tex, uvw).a;
		ALPHA_SCISSOR_THRESHOLD = 0.5;
		if (efeu_zufall.y <= 1.0) {
			// Je Haus andere Bereiche bewachsen: Rauschfeld im Modell (Fleck etwa 2 m, feiner dazu), verschoben nach efeu_zufall.x; wo es
			// über der Dichte liegt, schrumpfen die Blätter (Rand) bzw. fehlen ganz
			vec3 q = modell_ort * 0.55 + vec3(efeu_zufall.x * 37.0, efeu_zufall.x * 11.0, efeu_zufall.x * 53.0);
			float m = smoothstep(0.30, 0.70, 0.7 * efeu_rauschen(q) + 0.3 * efeu_rauschen(q * 2.7 + 5.0));
			ALPHA *= clamp((efeu_zufall.y - m) * 7.0 + 0.5, 0.0, 1.0);
		}
	}
#endif
}
