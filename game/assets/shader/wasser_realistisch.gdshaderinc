// Realistisches Wasser (docs/wasser/phase2.md, phase3.md): der gemeinsame Code aller Grafikstufen. Die Stufen sind eigene Shader, die nur
// MIT_…-Schalter setzen und diese Datei einbinden: wasser_realistisch.gdshader (Hoch und Ultra, alles), wasser_mittel.gdshader (ohne
// Brechung und Regenringe), wasser_niedrig.gdshader (ohne Brechung, Tiefentextur, Kaustik und Regenringe). Wasserarten.cs wählt den Shader.
//
// Abgeleitet vom „Realistic Water Shader“ von UnionBytes / Achim Menzel (AiYori), Godot-4-Fassung realistisches_wasser/Water_Original.gdshader
// (MIT, Lizenz dort in LICENSE.md):
//   Copyright (c) 2019 UnionBytes, Achim Menzel (alias AiYori); Copyright (c) 2019 Realistic Water Shader Contributors.
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
//   "Software"), to deal in the Software without restriction, ... subject to the following conditions: The above copyright notice and this
//   permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT
//   WARRANTY OF ANY KIND (voller Text: realistisches_wasser/LICENSE.md).
// Neu gegenüber dem Original: Strömung in Laufrichtung (auch entlang gekrümmter Flüsse), Schaum als weicher zerrissener Saum (nur wo die Art
// ihn hat), Wellen und Normalen folgen schwach dem Weltwind, Regenringe, Eis (Winterfortschritt, vom Ufer her wachsend, mit Rissen und
// Schnee), Kaustik und Tiefe aus der Welt statt aus einer Projektionsmatrix, alle Werte je Gewässerart über Uniforms (Wasserarten.cs). Die
// fünf Bilder (Normalkarten, Strömungskarte, Schaum, Kaustik) sind austauschbar: nur die Sampler-Uniforms belegen.
//
// Netzwerte (ufer_im_netz = 1, so baut sie das Spiel in Stadtansicht.Wasser.cs):
//   COLOR.r = Abstand zum Ufer in Metern / 32 (0–32 m; 8 Bit)
//   COLOR.g = offen: 1 = friert hier nie (Furt, Fähre, Hafen, Mündung in einen Fluss), weich übergehend
//   COLOR.b = Schaumanteil: Faktor auf Stärke und Breite des Saums (1 = wie die Art, 0 = keiner, ~0,35 = schmaler Saum an einer Kaimauer)
//   COLOR.a = Deckkraft: 0 = durchsichtig (dort übernimmt ein anderes Gewässer, z. B. der Fluss vor einem Hafenbecken)
// Flüsse und Bäche (lauf_netz = 1): UV = (quer, Stelle) in Metern, UV2 = Strömung in m/s (Weltachsen x, z). Die Normalkarten laufen dann
// entlang des Laufs (auch durch Kurven, ohne dass sie sich mit der Zeit verzerren), `stroemung` ist (0, Tempo) in diesen Laufachsen.
render_mode cull_back, diffuse_burley, specular_schlick_ggx, blend_mix;

#ifdef MIT_TIEFE
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
#endif
#ifdef MIT_BRECHUNG
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
#endif

// Globale Werte des Spiels (project.godot, [shader_globals]); die Stadtansicht setzt sie je Bild
global uniform float wetter_wind;      // Windstärke 0–1 (0,25 leichter Wind, 1 Sturm)
global uniform vec2 wind_richtung;     // Weltrichtung des Windes (x, z)
global uniform float wetter_regen;     // Regen gerade jetzt 0–1 (nur in den Wettereffekten Hoch > 0)
global uniform float wetter_schnee;    // Schneedecke 0–1
global uniform vec3 wetter_himmel;     // Himmelsfarbe (linear): das Wasser spiegelt sie

// ── Wellen ──
uniform float wellen_staerke = 1.0;    // Faktor auf die Steilheit der drei Wellen (0 = glatt)
uniform float wellen_laenge = 1.0;     // Faktor auf die Wellenlänge (Meer lang, Teich kurz)
uniform float wave_speed = 0.5;
uniform vec4 wave_a = vec4(1.0, 0.4, 0.35, 3.0);     // xy = Richtung, z = Steilheit, w = Länge
uniform vec4 wave_b = vec4(-0.1, 0.6, 0.30, 1.55);
uniform vec4 wave_c = vec4(-1.0, -0.8, 0.25, 0.9);
// Zwei weitere, längere und kürzere Wellenzüge (Meer, See, Hafen): xy = Richtung, z = Steilheit, w = Länge; wirken nur mit extra_wellen > 0
uniform vec4 wave_d = vec4(0.5, -1.0, 0.22, 6.3);
uniform vec4 wave_e = vec4(-0.7, 0.3, 0.16, 2.3);
uniform float extra_wellen = 0.0;      // Stärke der zwei zusätzlichen Wellenzüge (0 = aus)
uniform float wellen_vielfalt = 0.0;   // 0 = überall gleiche Wellen; 1 = Gruppen großer Wellen und ruhige Stellen (Stärke je Wellenzug wandert als Rauschen)
uniform vec2 netz_halb = vec2(0.0);    // halbe Netzgröße in m (Testfeld): Wellen klingen zum Rand hin ab; (0, 0) = aus
uniform float rand_breite = 6.0;       // Breite des Abklingens am Netzrand in m
uniform float netz_abstand = 0.0;      // Abstand der Netzecken in m: Wellen kürzer als das 1,5- bis 3-Fache klingen ab (sonst zittern die Ecken); 0 = aus
uniform float wind_anteil = 0.5;       // wie stark der Weltwind Wellen, Richtung und Kräuselung beeinflusst (0 = gar nicht)

// ── Oberfläche und Strömung ──
uniform vec2 stroemung = vec2(0.0);    // Strömung in m/s (Weltachsen x, z; bei lauf_netz = 1 in Laufachsen: (0, Tempo)), (0, 0) = stehend
uniform float lauf_netz = 0.0;         // 1 = Netz eines Flusses: UV = (quer, Stelle) in m, UV2 = Strömung (Welt)
uniform vec2 ruhe_drift = vec2(0.05, 0.04); // langsames Kräuseln auch ohne Strömung
uniform vec2 sampler_scale = vec2(0.25, 0.25);       // Bildwiederholung je Meter
uniform sampler2D uv_sampler : hint_anisotropy;      // Strömungskarte (verschiebt die Normalkarten)
uniform vec2 uv_sampler_scale = vec2(0.25, 0.25);
uniform float uv_sampler_strength = 0.04;
uniform sampler2D normalmap_a_sampler : hint_normal;
uniform sampler2D normalmap_b_sampler : hint_normal;
uniform float normal_tiefe = 1.25;

// ── Farbe, Klarheit ──
uniform vec4 color_deep : source_color = vec4(0.322, 0.4, 0.502, 1.0);
uniform vec4 color_shallow : source_color = vec4(0.659, 0.749, 0.761, 1.0);
uniform float beers_law = 2.0;         // je größer, desto schneller wird das Wasser tief gefärbt (kleiner = klarer)
uniform float depth_offset = -0.75;
uniform float refraction = 0.075;
uniform float tiefe_fest = 0.6;        // Tiefenanteil ohne Tiefentextur und ohne Uferwerte im Netz
uniform float rauheit = 0.2;
uniform float himmel_staerke = 0.5;    // Himmelsspiegelung (Fresnel) aus wetter_himmel
uniform float himmel_grund = 0.0;      // Grundspiegelung des Himmels, wenn die Szene keinen Himmel zum Spiegeln hat (Spiel: nur eine Hintergrundfarbe); die Kräusel modulieren sie

// ── Schaum ──
uniform sampler2D foam_sampler : hint_default_black;
uniform float schaum_staerke = 0.0;    // 0 = kein Schaum (Teich, See, Bach, Hafenbecken, Furt)
uniform float schaum_breite = 0.8;     // Breite des Saums in m (Abstand zum Hindernis hinter der Fläche)
uniform float schaum_kamm = 0.0;       // Schaumkronen auf den Wellen bei Wind (Meer)

// ── Kaustik ──
uniform sampler2DArray caustic_sampler : hint_default_black;  // 16 Lagen (Animation)
uniform float kaustik_staerke = 1.0;
uniform float kaustik_skala = 0.3;     // je Meter

// ── Eis ──
uniform float eis = 0.0;               // Winterfortschritt 0–1 mal Eisregel der Art
uniform float eis_reichweite = 0.0;    // Meter vom Ufer, die das Eis bei eis = 1 erreicht (0 = friert nie)
uniform float ufer_im_netz = 0.0;      // 1 = COLOR trägt die Netzwerte (siehe oben); 0 = aus der Tiefe geschätzt
uniform vec3 eis_hell : source_color = vec3(0.82, 0.90, 0.93);
uniform vec3 eis_dunkel : source_color = vec3(0.45, 0.60, 0.68);

varying vec2 welt_xz;
varying vec2 lauf_uv;      // Fluss: (quer, Stelle) in m; sonst Welt x, z
varying vec2 lauf_dir;     // Fluss: Fließrichtung (Welt x, z, Einheitsvektor); sonst (0, 1)
varying vec2 strom_welt;   // Strömung in m/s (Welt)
varying float ufer;        // Abstand zum Ufer in m (aus dem Netz)
varying float offen;       // 1 = friert nie
varying float schaum_anteil;
varying float deckkraft;
varying float wellen_y;    // Höhe der Welle an dieser Ecke
varying vec3 vertex_normal;
varying vec3 vertex_binormal;
varying vec3 vertex_tangent;

// Ganzzahl-Hash (wie wetter.gdshaderinc): die Sinus-Fassung zerfällt bei großen Koordinaten in sichtbare Blöcke
float w_hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p))) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	return float(n >> 8u) * (1.0 / 16777216.0);
}

vec2 w_hash2(vec2 p) {
	return vec2(w_hash(p), w_hash(p + vec2(57.0, 113.0)));
}

float w_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(w_hash(i), w_hash(i + vec2(1.0, 0.0)), f.x), mix(w_hash(i + vec2(0.0, 1.0)), w_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float w_fbm(vec2 p) {
	return w_rausch(p) * 0.55 + w_rausch(p * 2.13 + 7.1) * 0.3 + w_rausch(p * 4.7 + 3.3) * 0.15;
}

// Stärke eines Wellenzuges an diesem Ort: großflächiges, langsam wanderndes Rauschen mit festem Startwert (kein Uhrzeit-Zufall), je Zug anders
float zug_staerke(vec2 position, float saat, float skala) {
	if (wellen_vielfalt <= 0.0) {
		return 1.0;
	}
	float n = w_rausch(position * skala + vec2(saat * 17.3, saat * 5.1) + vec2(TIME * 0.02 * (0.6 + saat * 0.3), TIME * 0.013));
	return clamp(1.0 + wellen_vielfalt * (n - 0.5) * 3.2, 0.12, 1.0 + wellen_vielfalt * 0.5);
}

vec4 wave(vec4 parameter, vec2 position, float time, float staerke, float laenge, vec2 wind, inout vec3 tangent, inout vec3 binormal) {
	float wave_length = parameter.w * laenge;
	float wave_steepness = parameter.z * staerke;
	if (netz_abstand > 0.0) {
		// Ein grobes Netz kann kurze Wellen nicht zeigen: Die Ecken sprängen zufällig auf und ab
		wave_steepness *= smoothstep(1.5 * netz_abstand, 3.0 * netz_abstand, wave_length);
	}
	float k = 2.0 * 3.14159265359 / wave_length;
	float c = sqrt(9.8 / k);
	vec2 d = normalize(parameter.xy + wind);
	float f = k * (dot(d, position) - c * time);
	float a = wave_steepness / k;
	tangent += normalize(vec3(1.0 - d.x * d.x * (wave_steepness * sin(f)), d.x * (wave_steepness * cos(f)), -d.x * d.y * (wave_steepness * sin(f))));
	binormal += normalize(vec3(-d.x * d.y * (wave_steepness * sin(f)), d.y * (wave_steepness * cos(f)), 1.0 - d.y * d.y * (wave_steepness * sin(f))));
	return vec4(d.x * (a * cos(f)), a * sin(f) * 0.25, d.y * (a * cos(f)), 0.0);
}

void vertex() {
	float time = TIME * wave_speed;
	vec4 vertex = vec4(VERTEX, 1.0);
	vec3 wp = (MODEL_MATRIX * vertex).xyz;
	welt_xz = wp.xz;
	if (ufer_im_netz > 0.5) {
		ufer = COLOR.r * 32.0;
		offen = COLOR.g;
		schaum_anteil = COLOR.b;
		deckkraft = COLOR.a;
	} else {
		ufer = 1000.0;
		offen = 0.0;
		schaum_anteil = 1.0;
		deckkraft = 1.0;
	}
	if (lauf_netz > 0.5) {
		lauf_uv = UV;
		float tempo = length(UV2);
		lauf_dir = tempo > 0.0001 ? UV2 / tempo : vec2(0.0, 1.0);
		strom_welt = UV2;
	} else {
		lauf_uv = wp.xz;
		lauf_dir = vec2(0.0, 1.0);
		strom_welt = stroemung;
	}

	float sturm = 0.0;
	vec2 wind = vec2(0.0);
#ifdef MIT_WIND
	sturm = wind_anteil * wetter_wind;
	wind = normalize(wind_richtung + vec2(0.0001)) * sturm * 0.7;
#endif
	float staerke = wellen_staerke * (1.0 + sturm * 0.6);
#ifdef MIT_EIS
	// Unter dem Eis liegt das Wasser still
	staerke *= 1.0 - clamp((eis * (1.0 - offen) * eis_reichweite - ufer) / 0.8, 0.0, 1.0);
#endif

	if (netz_halb.x > 0.0) { // am Rand des Testfeldes abklingen
		vec2 innen = netz_halb - abs(VERTEX.xz);
		staerke *= smoothstep(0.0, rand_breite, min(innen.x, innen.y));
	}

	// Jeder Zug hat eigene Länge, Richtung, eigenes Tempo (über die Länge) und eine eigene, wandernde Stärke
	vec3 tang = vec3(0.0);
	vec3 bin = vec3(0.0);
	vec4 versatz = wave(wave_a, wp.xz, time * 1.0, staerke * zug_staerke(wp.xz, 0.0, 0.031), wellen_laenge, wind, tang, bin);
	versatz += wave(wave_b, wp.xz, time * 1.15, staerke * zug_staerke(wp.xz, 1.0, 0.043), wellen_laenge, wind, tang, bin);
	versatz += wave(wave_c, wp.xz, time * 0.85, staerke * zug_staerke(wp.xz, 2.0, 0.057), wellen_laenge, wind, tang, bin);
	if (extra_wellen > 0.0) {
		versatz += wave(wave_d, wp.xz, time * 0.9, staerke * extra_wellen * zug_staerke(wp.xz, 3.0, 0.023), wellen_laenge, wind, tang, bin);
		versatz += wave(wave_e, wp.xz, time * 1.25, staerke * extra_wellen * zug_staerke(wp.xz, 4.0, 0.067), wellen_laenge, wind, tang, bin);
	}
	vertex += versatz;
	wellen_y = versatz.y;

	vertex_tangent = tang;
	vertex_binormal = bin;
	TANGENT = tang;
	BINORMAL = bin;
	vertex_normal = normalize(cross(bin, tang));
	NORMAL = vertex_normal;
	UV = (lauf_netz > 0.5 ? lauf_uv : vertex.xz) * sampler_scale;
	VERTEX = vertex.xyz;
}

#ifdef MIT_RINGE
// Regenringe: zwei Lagen Zellen, in jeder wächst je Zelle ein Ring und verklingt; Wirkung nur auf die Normale
vec2 ringe(vec2 p, float t) {
	vec2 summe = vec2(0.0);
	for (int l = 0; l < 2; l++) {
		vec2 q = p * (1.3 + float(l) * 0.9) + float(l) * 17.3;
		vec2 zelle = floor(q);
		vec2 lokal = fract(q) - 0.5;
		vec2 h = w_hash2(zelle);
		float alter = fract(t * (0.8 + 0.3 * h.x) + h.y);
		vec2 v = lokal - (h - 0.5) * 0.4;
		float d = length(v);
		float ring = exp(-pow((d - alter * 0.5) * 22.0, 2.0)) * (1.0 - alter);
		summe += normalize(v + vec2(0.0001)) * ring;
	}
	return summe;
}
#endif

void fragment() {
	// ── Oberfläche: zwei Normalkarten, die mit der Strömung wandern (und gegeneinander kräuseln) ──
	vec2 drift = ruhe_drift * TIME;
	vec2 fluss = stroemung * sampler_scale * TIME;
	vec2 uv_sampler_uv = UV * uv_sampler_scale + drift - fluss * 0.5;
	vec2 uv_versatz = uv_sampler_strength * texture(uv_sampler, uv_sampler_uv).rg * 2.0 - 1.0;
	vec2 uv = UV + uv_versatz;
	vec3 normalmap = texture(normalmap_a_sampler, uv - drift * 2.0 - fluss).rgb * 0.75;
	normalmap += texture(normalmap_b_sampler, uv + drift - fluss * 1.35).rgb * 0.25;
	if (lauf_netz > 0.5) {
		// Die Karten liegen in Laufachsen (quer, längs): ihre Neigung zurück in die Weltachsen drehen
		vec2 n = normalmap.xy * 2.0 - 1.0;
		n = vec2(-lauf_dir.y, lauf_dir.x) * n.x + lauf_dir * n.y;
		normalmap.xy = n * 0.5 + 0.5;
	}

	float sturm = 0.0;
#ifdef MIT_WIND
	sturm = wind_anteil * wetter_wind;
#endif
#ifdef MIT_RINGE
	if (wetter_regen > 0.02) {
		normalmap.xy += ringe(welt_xz, TIME) * 0.38 * wetter_regen;
	}
#endif

	// ── Tiefe: Abstand von der Fläche bis zu dem, was dahinter liegt (Grund, Fels, Pfahl) ──
	float dist = ufer_im_netz > 0.5 ? clamp(ufer * 0.5, 0.0, 4.0) : 4.0; // ohne Tiefentextur grob aus dem Uferabstand
	vec3 grund_welt = vec3(0.0);
	float depth_blend = ufer_im_netz > 0.5 ? clamp(1.0 - exp((dist + depth_offset) * -beers_law), 0.0, 1.0) : tiefe_fest;
#ifdef MIT_TIEFE
	float depth_raw = texture(DEPTH_TEXTURE, SCREEN_UV).r;
	vec4 depth_view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth_raw, 1.0);
	vec3 grund_view = depth_view.xyz / depth_view.w;
	dist = distance(VERTEX, grund_view);
	grund_welt = (INV_VIEW_MATRIX * vec4(grund_view, 1.0)).xyz;
	depth_blend = clamp(1.0 - exp((dist + depth_offset) * -beers_law), 0.0, 1.0);
#endif
	float depth_blend_pow = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	// ── Farbe ──
	vec3 dye_color = mix(color_shallow.rgb, color_deep.rgb, depth_blend_pow);
#ifdef MIT_BRECHUNG
	vec3 ref_normalmap = normalmap * 2.0 - 1.0;
	ref_normalmap = normalize(vertex_tangent * ref_normalmap.x + vertex_binormal * ref_normalmap.y + vertex_normal * ref_normalmap.z);
	vec2 ref_uv = SCREEN_UV + (ref_normalmap.xy * refraction) / max(-VERTEX.z, 0.5);
	vec3 screen_color = textureLod(SCREEN_TEXTURE, ref_uv, depth_blend_pow * 2.5).rgb;
	vec3 color = mix(screen_color * dye_color, dye_color * 0.25, depth_blend_pow * 0.5);
#else
	vec3 color = mix(dye_color, dye_color * 0.25, depth_blend_pow * 0.5);
#endif

#if defined(MIT_KAUSTIK) && defined(MIT_TIEFE)
	if (kaustik_staerke > 0.01 && depth_blend < 0.95) {
		vec2 kuv = grund_welt.xz * kaustik_skala + strom_welt * TIME * 0.02;
		float kaustik = texture(caustic_sampler, vec3(kuv, mod(TIME * 14.0, 16.0))).r;
		color *= 1.0 + pow(kaustik, 1.5) * (1.0 - depth_blend) * 6.0 * kaustik_staerke;
	}
#endif

	float specular = 0.2 + depth_blend_pow * 0.4;
	float roughness = rauheit;

	// ── Schaum: weicher, zerrissener Saum dort, wo hinter der Fläche etwas dicht darunter liegt (Ufer, Fels, Pfahl), mit der Strömung getragen ──
	float schaum = 0.0;
#ifdef MIT_SCHAUM
	float s_staerke = schaum_staerke * schaum_anteil;
	if (s_staerke > 0.001 || schaum_kamm > 0.001) {
		float s_breite = max(schaum_breite * schaum_anteil, 0.02);
		vec2 sp = lauf_uv * 0.9 - stroemung * TIME * 0.9;
		float reisser = w_fbm(sp) * 0.65 + (1.0 - textureLod(foam_sampler, sp * 0.5 + 0.3, 1.5).r) * 0.35;
		if (s_staerke > 0.001) {
			float saum = 1.0 - smoothstep(0.0, s_breite, dist - (reisser - 0.45) * s_breite * 1.3);
			float flocken = smoothstep(0.30, 0.85, reisser);
			schaum = saum * mix(0.3, 1.0, flocken) * s_staerke;
		}
		if (schaum_kamm > 0.001) {
			schaum += smoothstep(0.75, 1.0, wellen_y * 4.0 + reisser * 0.4) * smoothstep(0.1, 0.5, wetter_wind) * schaum_kamm * reisser;
		}
		schaum = clamp(schaum, 0.0, 1.0);
	}
#endif

	// ── Eis: wächst vom Ufer her, mit Rissen; Schnee darauf ──
	float eismaske = 0.0;
#ifdef MIT_EIS
	float eis_hier = eis * (1.0 - offen);
	if (eis_hier > 0.001 && eis_reichweite > 0.01) {
		float abstand = ufer;
		if (ufer_im_netz < 0.5) {
			abstand = dist * 2.0; // ohne Uferwerte grob aus der Tiefe geschätzt
		}
		float rand_n = w_fbm(welt_xz * 0.8);
		eismaske = clamp((eis_hier * eis_reichweite - abstand) / 0.8 + (rand_n - 0.5) * 1.2, 0.0, 1.0);
		if (eismaske > 0.001) {
			// Risse: Abstand zur zweitnächsten minus zur nächsten Zellmitte (Voronoi)
			vec2 q = welt_xz * 0.45 + (vec2(w_rausch(welt_xz * 0.6), w_rausch(welt_xz * 0.6 + 9.0)) - 0.5) * 1.4; // verzogen: Risse laufen krumm
			vec2 zelle = floor(q);
			float f1 = 8.0;
			float f2 = 8.0;
			for (int j = -1; j <= 1; j++) {
				for (int i = -1; i <= 1; i++) {
					vec2 g = vec2(float(i), float(j));
					vec2 r = g + w_hash2(zelle + g) - fract(q);
					float d = dot(r, r);
					if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) { f2 = d; }
				}
			}
			float riss = 1.0 - smoothstep(0.0, 0.05, sqrt(f2) - sqrt(f1));
			float wolke = w_fbm(welt_xz * 0.35 + 11.0);
			vec3 eisfarbe = mix(eis_dunkel, eis_hell, wolke);
			eisfarbe = mix(eisfarbe, eis_dunkel * 0.7, riss * 0.5);
			float schnee_auf = clamp(wetter_schnee * 1.1 * smoothstep(0.25, 0.7, w_fbm(welt_xz * 0.5 + 3.0)), 0.0, 1.0);
			eisfarbe = mix(eisfarbe, vec3(0.93, 0.95, 0.97), schnee_auf);
			color = mix(color, eisfarbe, eismaske * 0.95);
			schaum *= 1.0 - eismaske;
			roughness = mix(roughness, mix(0.12, 0.85, schnee_auf), eismaske);
			specular = mix(specular, mix(0.5, 0.1, schnee_auf), eismaske);
			normalmap = mix(normalmap, vec3(0.5, 0.5, 1.0), eismaske * 0.9);
		}
	}
#endif

	color = mix(color, vec3(0.9, 0.93, 0.92), schaum);
	roughness = mix(roughness, 0.8, schaum);
	specular = mix(specular, 0.08, schaum);

	// Himmel im Wasser: stärker bei flachem Blick (Fresnel)
	float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 3.0);
	// Ohne Himmel in der Szene (Spiel): eine Grundspiegelung, die mit der Neigung der Kräusel schwankt (Normale aus den Normalkarten, Weltachsen)
	float himmel_spiegel = 0.0;
	if (himmel_grund > 0.0) {
		vec3 n_t = normalmap * 2.0 - 1.0;
		n_t.xy *= normal_tiefe * (1.0 + sturm * 0.6);
		vec3 n_w = normalize(vertex_tangent * n_t.x + vertex_binormal * n_t.y + vertex_normal * n_t.z);
		vec3 v_w = normalize((INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xyz);
		himmel_spiegel = himmel_grund * (0.7 + 3.0 * pow(1.0 - clamp(dot(n_w, v_w), 0.0, 1.0), 3.0)) * mix(0.35, 1.0, depth_blend); // im Flachen sieht man den Grund
	}

	// Deckkraft: Mit Bildschirmtextur ist das Wasser deckend (der Grund steckt schon in der Farbe), nur die Netzwerte blenden über.
	// Ohne sie wird das Flache durchsichtig, und am Ufer läuft das Wasser weich aus.
	// Am Rand des Netzes (Uferabstand 0) immer weich: ein schmaler Bach liegt knapp über dem gezeichneten Boden und hätte sonst harte Kanten.
	float deckend = deckkraft * (ufer_im_netz > 0.5 ? smoothstep(0.0, 0.35, ufer) : 1.0);
	float alpha = deckend;
#ifndef MIT_BRECHUNG
	alpha *= mix(0.5, 1.0, depth_blend) * smoothstep(0.0, 0.2, dist);
	alpha = max(alpha, max(schaum, eismaske) * deckend);
#endif

	ALBEDO = color;
	ALPHA = clamp(alpha, 0.0, 1.0);
	METALLIC = 0.1 * (1.0 - schaum);
	ROUGHNESS = roughness;
	SPECULAR = specular;
	EMISSION = wetter_himmel * (fresnel * himmel_staerke + himmel_spiegel) * (1.0 - schaum) * (1.0 - eismaske * 0.6);
	NORMAL_MAP = normalmap;
	NORMAL_MAP_DEPTH = normal_tiefe * (1.0 + sturm * 0.6);
}
