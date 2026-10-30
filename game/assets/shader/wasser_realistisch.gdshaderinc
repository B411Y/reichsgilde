// Realistisches Wasser (docs/wasser/phase2.md, phase3.md): der gemeinsame Code aller Grafikstufen. Die Stufen sind eigene Shader, die nur
// MIT_…-Schalter setzen und diese Datei einbinden: wasser_realistisch.gdshader (Hoch und Ultra, alles), wasser_mittel.gdshader (ohne
// Brechung und Regenringe), wasser_niedrig.gdshader (ohne Brechung, Tiefentextur, Kaustik, Regenringe und mit nur einer Normalkarte).
// Wasserarten.cs wählt den Shader.
//
// Abgeleitet vom „Realistic Water Shader“ von UnionBytes / Achim Menzel (AiYori), Godot-4-Fassung realistisches_wasser/Water_Original.gdshader
// (MIT, Lizenz dort in LICENSE.md):
//   Copyright (c) 2019 UnionBytes, Achim Menzel (alias AiYori); Copyright (c) 2019 Realistic Water Shader Contributors.
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
//   "Software"), to deal in the Software without restriction, ... subject to the following conditions: The above copyright notice and this
//   permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT
//   WARRANTY OF ANY KIND (voller Text: realistisches_wasser/LICENSE.md).
// Neu gegenüber dem Original: Strömung in Laufrichtung (auch entlang gekrümmter Flüsse), Schaum als weicher zerrissener Saum (nur wo die Art
// ihn hat), Wellen und Normalen folgen dem Weltwind (Wellen in wasser_wellen.gdshaderinc, gemeinsam mit den Eisschollen), Regenringe, Eis
// (Winterfortschritt, vom Ufer her wachsend, als Mosaik gebrochener Platten mit feinen Rissen, klarem und weißem Eis, Kante mit Dicke, Grieseis
// davor, zum Ufer weich auslaufend; Löcher an Mündungen und schnellen Stellen; offenes Wasser im Winter kälter und dunkler; die treibenden
// Schollen sind eigene Körper, eis_scholle.gdshaderinc; die Oberseite des Eises ist für beide dieselbe, eis_material.gdshaderinc; festes Eis
// ist starr, die Wellen setzen erst vor seiner Kante ein), Bachwasser, das an einer Mündung als trüber Streifen in den Fluss zieht, Kaustik und Tiefe
// aus der Welt statt aus einer Projektionsmatrix, alle Werte je Gewässerart über Uniforms (Wasserarten.cs). Die fünf Bilder (Normalkarten,
// Strömungskarte, Schaum, Kaustik) sind austauschbar: nur die Sampler-Uniforms belegen.
//
// Netzwerte (ufer_im_netz = 1, so baut sie das Spiel in Stadtansicht.Wasser.cs):
//   COLOR.r = Abstand zum Ufer in Metern / 32 (0–32 m; 8 Bit)
//   COLOR.g = Eiskanal (Wasserarten.EisKanal), 0,5 = wie die Art. Darunter „offen“ = 1 − 2g: Anteil, um den das Eis zurückbleibt (Eis reicht
//             eis · (1 − offen) · eis_reichweite weit; 0 = friert nie: Mündung, schnelle Stelle eines Baches, Fährrinne; an einer Furt so viel,
//             dass nur Randeis bleibt). Darüber „still“ = 2g − 1: stilles Wasser vor einer Kaimauer, das Eis folgt dort eis_still und
//             still_reichweite (wie im Hafenbecken). Weich übergehend
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

// Wellen, Rauschen und die globalen Windwerte (wetter_wind, wellen_seegang, wind_richtung): gemeinsam mit den Eisschollen
#include "res://assets/shader/wasser_wellen.gdshaderinc"
#ifdef MIT_EIS
// Die Oberseite des Eises: dasselbe Material wie die Schollen (Uniforms eis_hell, eis_dunkel, eis_stirn, eis_schlamm, eis_klar, eis_relief)
#include "res://assets/shader/eis_material.gdshaderinc"
#endif

// Globale Werte des Spiels (project.godot, [shader_globals]); die Stadtansicht setzt sie je Bild
global uniform float wetter_regen;     // Regen gerade jetzt 0–1 (nur in den Wettereffekten Hoch > 0)
global uniform float wetter_schnee;    // Schneedecke 0–1
global uniform vec3 wetter_himmel;     // Himmelsfarbe (linear): das Wasser spiegelt sie

// ── Wellen (die übrigen Werte in wasser_wellen.gdshaderinc) ──
uniform vec2 netz_halb = vec2(0.0);    // halbe Netzgröße in m (Testfeld): Wellen klingen zum Rand hin ab; (0, 0) = aus
uniform float rand_breite = 6.0;       // Breite des Abklingens am Netzrand in m

// ── Oberfläche und Strömung ──
uniform vec2 stroemung = vec2(0.0);    // Strömung in m/s (Weltachsen x, z; bei lauf_netz = 1 in Laufachsen: (0, Tempo)), (0, 0) = stehend
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
uniform float eis = 0.0;               // Eisfortschritt der Art 0–1 (Wasserarten.EisFortschritt: stilles Wasser früher als ein Bach)
uniform float eis_reichweite = 0.0;    // Meter vom Ufer, die das Eis bei eis = 1 erreicht (0 = friert nie)
uniform float ufer_im_netz = 0.0;      // 1 = COLOR trägt die Netzwerte (siehe oben); 0 = aus der Tiefe geschätzt
uniform float winter_ton = 0.0;        // Winterfortschritt 0–1 (für alle Arten): offenes Wasser kälter, dunkler, schwächere Lichtnetze
uniform float eis_still = 0.0;         // Eisfortschritt stillen Wassers (Hafenbecken) für Ecken mit „still“ (vor einer Kaimauer)
uniform float still_reichweite = 4.5;  // seine Reichweite in m
uniform float rand_unruhe = 1.0;       // wie unregelmäßig die Eiskante ist (Randeis 1; ganz zufrierendes Wasser 0,5, damit es in der Mitte sicher schließt)
uniform float ufer_saum = 1.3;         // so weit (m) hört das Eis vor dem Netzrand unregelmäßig auf, dort liegt der Grund (Bach schmal, an Kaimauern 0)
// Löcher im Eis (Mündung, schnelle Stelle eines Baches; Stadtansicht.Wasser.cs): eine Kapsel von Ort bis Ort + Richtung · Länge mit Halbmesser,
// weich über den Rand (m), unregelmäßig. Am Fluss liegt die Mündung eines Baches als Rinne stromab am Ufer.
uniform int loch_zahl = 0;
uniform vec4 loch_ort[8];              // x, z, Halbmesser, weicher Rand (m)
uniform vec4 loch_form[8];             // Richtung x, z (Einheitsvektor), Länge (m), –

// ── Mündungen: Bachwasser zieht als trüber, hellerer Streifen am Ufer stromab und mischt sich ein (nur Flüsse, Laufachsen) ──
uniform int muendung_zahl = 0;
uniform vec4 muendung[4];              // quer (m), Stelle (m), Seite (+1/−1: an welchem Ufer), Breite des Baches (m)
uniform vec3 muendung_farbe : source_color = vec3(0.52, 0.53, 0.42);

varying vec2 welt_xz;
varying float welt_y;      // Höhe der Fläche (mit Welle)
varying vec2 lauf_uv;      // Fluss: (quer, Stelle) in m; sonst Welt x, z
varying vec2 lauf_dir;     // Fluss: Fließrichtung (Welt x, z, Einheitsvektor); sonst (0, 1)
varying vec2 strom_welt;   // Strömung in m/s (Welt)
varying float ufer;        // Abstand zum Ufer in m (aus dem Netz)
varying float offen;       // 1 = friert nie
varying float still;       // 1 = stilles Wasser vor einer Kaimauer (friert wie das Hafenbecken)
varying float schaum_anteil;
varying float deckkraft;
varying float wellen_y;    // Höhe der Welle an dieser Ecke
varying vec3 vertex_normal;
varying vec3 vertex_binormal;
varying vec3 vertex_tangent;

#ifdef MIT_EIS
// Wie weit das Eis hier wegen eines Loches zurückbleibt (1 = offen): Kapseln aus loch_ort/loch_form, der Rand weich und unregelmäßig
float loch_offen(vec2 p) {
	float o = 0.0;
	for (int i = 0; i < loch_zahl; i++) {
		vec4 a = loch_ort[i];
		vec4 f = loch_form[i];
		vec2 r = p - a.xy;
		float t = clamp(dot(r, f.xy), 0.0, f.z);
		float d = length(r - f.xy * t) - a.z;
		if (d > a.w * 1.6) {
			continue;
		}
		d += (w_fbm(p * 0.55 + float(i) * 7.3) - 0.5) * a.w * 1.1;
		o = max(o, 1.0 - smoothstep(0.0, a.w, d));
	}
	return o;
}

// Eisplatten: Voronoi-Zellen (Größe z m) um verschobene Gitterpunkte. Liefert die nächste und die zweitnächste Zellmitte (Welt) und den Abstand
// (m) zur Grenze zwischen beiden (Mittelsenkrechte): Die Kante des Randeises folgt diesen Grenzen (gebrochen, eckig), innen sind sie Risse.
void eis_zellen(vec2 p, float z, out vec2 c1, out vec2 c2, out float grenze) {
	vec2 q = p / z;
	vec2 zelle = floor(q);
	float f1 = 1e9;
	float f2 = 1e9;
	vec2 m1 = q;
	vec2 m2 = q;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = zelle + vec2(float(i), float(j));
			vec2 m = g + 0.12 + 0.76 * w_hash2(g + vec2(41.0, 7.0));
			vec2 r = m - q;
			float d = dot(r, r);
			if (d < f1) {
				f2 = f1;
				m2 = m1;
				f1 = d;
				m1 = m;
			} else if (d < f2) {
				f2 = d;
				m2 = m;
			}
		}
	}
	c1 = m1 * z;
	c2 = m2 * z;
	grenze = (f2 - f1) / (2.0 * max(length(m2 - m1), 1e-4)) * z;
}
#endif

void vertex() {
	vec4 vertex = vec4(VERTEX, 1.0);
	vec3 wp = (MODEL_MATRIX * vertex).xyz;
	welt_xz = wp.xz;
	if (ufer_im_netz > 0.5) {
		ufer = COLOR.r * 32.0;
		offen = clamp(1.0 - 2.0 * COLOR.g, 0.0, 1.0);
		still = clamp(2.0 * COLOR.g - 1.0, 0.0, 1.0);
		schaum_anteil = COLOR.b;
		deckkraft = COLOR.a;
	} else {
		ufer = 1000.0;
		offen = 0.0;
		still = 0.0;
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

	float sturm;
	vec2 wind;
	wellen_wind(sturm, wind);
	float staerke = wellen_staerke * wellen_windfaktor(sturm);
#ifdef MIT_EIS
	// Festes Eis ist starr und angefroren (Nutzer 30.10.2026: es bewegte sich „wie ein Schwamm“): unter dem Eis keine Wellen, auch nicht unter den
	// Platten, die über die glatte Kante hinausragen (bis knapp die halbe Plattengröße); erst davor setzen sie über 2,5 m ein. Dieselbe Kante
	// wie im Bildpunkt-Teil (Reichweite, großflächige Buchten, Löcher), nur ohne die Platten.
	float eis_v = mix(eis, eis_still, still) * (1.0 - offen);
	float reich_v = mix(eis_reichweite, still_reichweite, still);
	if (eis_v > 0.001 && reich_v > 0.01) {
		if (loch_zahl > 0) {
			eis_v *= 1.0 - loch_offen(wp.xz);
		}
		float unruhe_v = min(reich_v, 12.0) * 0.42 * rand_unruhe;
		float zelle_v = clamp(reich_v * 0.75, 1.3, 3.6);
		float vor_kante = ufer - (eis_v * reich_v * (rand_unruhe < 0.99 ? 1.3 : 1.0) + (w_fbm(wp.xz * 0.045 + 17.0) - 0.5) * 2.0 * unruhe_v * smoothstep(0.0, 0.3, eis_v));
		float ab = (zelle_v * 0.45 + 0.2) * smoothstep(0.0, 0.15, eis_v);
		staerke *= smoothstep(ab, ab + 2.5, vor_kante);
	}
#endif

	if (netz_halb.x > 0.0) { // am Rand des Testfeldes abklingen
		vec2 innen = netz_halb - abs(VERTEX.xz);
		staerke *= smoothstep(0.0, rand_breite, min(innen.x, innen.y));
	}

	// Flüsse rechnen in Laufachsen (die Wellen folgen den Kurven), alles andere in Weltachsen
	vec3 tang;
	vec3 bin;
	vec3 normale;
	vec3 versatz = wellen_welt(wp.xz, lauf_uv, lauf_dir, staerke, wind, tang, bin, normale);
	vertex.xyz += versatz;
	wellen_y = versatz.y;
	welt_y = wp.y + versatz.y;

	vertex_tangent = tang;
	vertex_binormal = bin;
	TANGENT = tang;
	BINORMAL = bin;
	vertex_normal = normale;
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


// Mündungen in diesen Fluss (Laufachsen): x = wie viel Bachwasser hier ist (trüber Streifen), y = dünne Schaumstreifen an der Mischkante,
// z = Nähe zur Mündung (dort kein Uferschaum: das Ufer ist offen, der Grund flach)
vec3 muendung_fahne(vec2 lauf, float ufer_m, float zeit) {
	vec3 r = vec3(0.0);
	for (int i = 0; i < muendung_zahl; i++) {
		vec4 m = muendung[i];
		float ds = lauf.y - m.y;
		if (ds < -m.w - 8.0 || ds > 110.0 || m.z * lauf.x < 0.0) {
			continue;
		}
		float t = ds + m.w * 0.5;                                  // ab dem oberen Rand der Einmündung
		float breite = m.w * (1.3 + 0.65 * sqrt(max(t, 0.0)));     // der Streifen wird stromab breiter (Duffesbach: 20 m stromab gut 12 m)
		vec2 sp = vec2(lauf.x, lauf.y - stroemung.y * zeit * 0.9);  // mit dem Wasser treibend
		float wirbel = w_fbm(sp * vec2(0.3, 0.09) + float(i) * 13.1);
		float kante = ufer_m - breite * (0.7 + 0.75 * (wirbel - 0.5));
		float laengs = smoothstep(0.0, m.w * 0.9, t) * exp(-max(t, 0.0) / 60.0);
		float drin = (1.0 - smoothstep(-breite * 0.45, breite * 0.12, kante)) * laengs;
		float streifen = w_rausch(sp * vec2(1.3, 0.16) + float(i) * 7.7);
		r.x = max(r.x, drin * (0.35 + 0.65 * streifen));
		// Schaum nur als dünne, abreißende Streifen an der Mischkante
		r.y = max(r.y, (1.0 - smoothstep(0.0, 0.16 + breite * 0.025, abs(kante))) * smoothstep(0.55, 0.85, streifen) * laengs * 0.8);
		r.z = max(r.z, smoothstep(-m.w - 7.0, -m.w - 1.5, ds) * (1.0 - smoothstep(18.0, 40.0, ds)) * (1.0 - smoothstep(breite + 1.0, breite + 6.0, ufer_m)));
	}
	return r;
}

void fragment() {
	// ── Ableitungen hier oben (gleichmäßiger Ablauf): Meter je Bildpunkt und wie sich der Uferabstand über die Welt ändert ──
	vec2 d_ort_x = dFdx(welt_xz);
	vec2 d_ort_y = dFdy(welt_xz);
	float bildpunkt_m = max(length(d_ort_x), length(d_ort_y));
	float d_ufer_x = dFdx(ufer);
	float d_ufer_y = dFdy(ufer);
	float det = d_ort_x.x * d_ort_y.y - d_ort_x.y * d_ort_y.x;
	vec2 ufer_grad = abs(det) > 1e-9 ? vec2(d_ort_y.y * d_ufer_x - d_ort_x.y * d_ufer_y, d_ort_x.x * d_ufer_y - d_ort_y.x * d_ufer_x) / det : vec2(0.0);
	ufer_grad *= min(1.0, 1.5 / max(length(ufer_grad), 1e-4)); // Kanten des Netzes: nicht mehr als 1,5 m je m

	// ── Oberfläche: zwei Normalkarten, die mit der Strömung wandern (und gegeneinander kräuseln) ──
	vec2 drift = ruhe_drift * TIME;
	vec2 fluss = stroemung * sampler_scale * TIME;
#ifdef MIT_FEINKARTEN
	vec2 uv_sampler_uv = UV * uv_sampler_scale + drift - fluss * 0.5;
	vec2 uv_versatz = uv_sampler_strength * texture(uv_sampler, uv_sampler_uv).rg * 2.0 - 1.0;
	vec2 uv = UV + uv_versatz;
	vec3 normalmap = texture(normalmap_a_sampler, uv - drift * 2.0 - fluss).rgb * 0.75;
	normalmap += texture(normalmap_b_sampler, uv + drift - fluss * 1.35).rgb * 0.25;
#else
	// Stufe Niedrig: nur eine Normalkarte, ohne Verwirbelung durch die Strömungskarte (zwei Bildzugriffe weniger), etwas flacher gewichtet
	vec3 normalmap = mix(vec3(0.5, 0.5, 1.0), texture(normalmap_a_sampler, UV - drift * 2.0 - fluss).rgb, 0.8);
#endif
	if (lauf_netz > 0.5) {
		// Die Karten liegen in Laufachsen (quer, längs): ihre Neigung zurück in die Weltachsen drehen
		vec2 n = normalmap.xy * 2.0 - 1.0;
		n = vec2(-lauf_dir.y, lauf_dir.x) * n.x + lauf_dir * n.y;
		normalmap.xy = n * 0.5 + 0.5;
	}

	float sturm = 0.0;
#ifdef MIT_WIND
	sturm = wind_anteil * wellen_seegang; // Kräusel so träge wie der Seegang (sonst sprang ihre Stärke beim Wetterwechsel)
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
	float lot_tiefe = ufer_im_netz > 0.5 ? ufer * 0.18 : 2.0; // senkrechte Wassertiefe (ohne Tiefentextur aus dem Uferabstand, Ufer etwa 1 : 6)
#ifdef MIT_TIEFE
	float depth_raw = texture(DEPTH_TEXTURE, SCREEN_UV).r;
	vec4 depth_view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth_raw, 1.0);
	vec3 grund_view = depth_view.xyz / depth_view.w;
	dist = distance(VERTEX, grund_view);
	grund_welt = (INV_VIEW_MATRIX * vec4(grund_view, 1.0)).xyz;
	depth_blend = clamp(1.0 - exp((dist + depth_offset) * -beers_law), 0.0, 1.0);
	lot_tiefe = welt_y - grund_welt.y;
#endif
	float depth_blend_pow = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	// ── Farbe ──
	vec3 dye_color = mix(color_shallow.rgb, color_deep.rgb, depth_blend_pow);
#ifdef MIT_EIS
	// Im Winter ist offenes Wasser kalt: dunkler und grauer, ohne den grünen Sommerton (sonst wirkt eine offene Furt im Eis sommerlich)
	vec3 kalt = vec3(dot(dye_color, vec3(0.3, 0.5, 0.2))) * vec3(0.72, 0.86, 1.0) * 0.75;
	dye_color = mix(dye_color, kalt, winter_ton * 0.65);
#endif
	vec3 grund_farbe = dye_color * 0.45; // was durch flaches Wasser vom Grund zu sehen ist (ohne Bildschirmtextur: ein mittleres Grau)
#ifdef MIT_BRECHUNG
	vec3 ref_normalmap = normalmap * 2.0 - 1.0;
	ref_normalmap = normalize(vertex_tangent * ref_normalmap.x + vertex_binormal * ref_normalmap.y + vertex_normal * ref_normalmap.z);
	vec2 ref_uv = SCREEN_UV + (ref_normalmap.xy * refraction) / max(-VERTEX.z, 0.5);
	vec3 screen_color = textureLod(SCREEN_TEXTURE, ref_uv, depth_blend_pow * 2.5).rgb;
	vec3 color = mix(screen_color * dye_color, dye_color * 0.25, depth_blend_pow * 0.5);
	grund_farbe = screen_color; // der Grund (für Eis, das am Ufer auf ihm aufliegt; dort ist das Wasser flach und kaum gebrochen)
#else
	// Ohne Bildschirmtextur steht statt des Grundes ein mittleres Grau dahinter (so hell wie der Grund unter Wasser in Hoch); das Flache wird durchsichtig (ALPHA)
	vec3 color = mix(dye_color * 0.45, dye_color * 0.25, depth_blend_pow * 0.5);
#endif

#if defined(MIT_KAUSTIK) && defined(MIT_TIEFE)
	if (kaustik_staerke > 0.01 && depth_blend < 0.95) {
		vec2 kuv = grund_welt.xz * kaustik_skala + strom_welt * TIME * 0.02;
		float kaustik = texture(caustic_sampler, vec3(kuv, mod(TIME * 14.0, 16.0))).r;
		color *= 1.0 + pow(kaustik, 1.5) * (1.0 - depth_blend) * 6.0 * kaustik_staerke * (1.0 - 0.85 * winter_ton); // Wintersonne steht tief
	}
#endif

	// ── Mündung: das Bachwasser zieht als hellerer, trüber Streifen am Ufer stromab und mischt sich in Schlieren ein ──
	vec3 fahne = vec3(0.0);
	if (muendung_zahl > 0 && lauf_netz > 0.5) {
		fahne = muendung_fahne(lauf_uv, ufer, TIME);
		fahne.x *= 1.0 - 0.75 * winter_ton; // im Winter führt der Bach wenig und kaltes, klares Wasser: der Streifen ist kaum zu sehen
		vec3 trueb = mix(muendung_farbe, vec3(dot(muendung_farbe, vec3(0.3, 0.5, 0.2))) * vec3(0.75, 0.85, 0.95) * 0.7, winter_ton * 0.7);
		color = mix(color, trueb * mix(0.7, 1.0, depth_blend), fahne.x * 0.6);
	}

	float specular = 0.2 + depth_blend_pow * 0.4;
	float roughness = rauheit + fahne.x * 0.12; // das einströmende Wasser wirbelt: die Fläche ist unruhiger, spiegelt weniger
	normalmap.xy = mix(normalmap.xy, normalmap.xy * 1.35 - 0.175, fahne.x * 0.6);

	// ── Schaum: weicher, zerrissener Saum dort, wo hinter der Fläche etwas dicht darunter liegt (Ufer, Fels, Pfahl), mit der Strömung getragen ──
	float schaum = 0.0;
#ifdef MIT_SCHAUM
	float s_staerke = schaum_staerke * schaum_anteil * (1.0 - 0.95 * fahne.z); // an der Mündung kein Uferschaum (das Ufer ist offen, der Grund flach)
	float s_breite = max(schaum_breite * schaum_anteil, 0.02);
	// Rauschen nur dort rechnen, wo Schaum sein kann (Saum nahe am Hindernis, Kämme): spart im offenen Wasser fast alles
	if ((s_staerke > 0.001 && dist < s_breite * 2.2) || schaum_kamm > 0.001) {
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
	schaum = max(schaum, fahne.y * 0.6);
	// Im Winter ist Schaum grauer Matsch (Eisbrei), kein weißer Sommerschaum
	vec3 schaum_farbe = mix(vec3(0.9, 0.93, 0.92), vec3(0.62, 0.68, 0.71), winter_ton * 0.7);
#else
	vec3 schaum_farbe = vec3(0.9, 0.93, 0.92);
#endif

	// ── Eis: vom Ufer her wachsend, die Kante gebrochen entlang eines Mosaiks von Platten (beim Randeis bis innen als feine Fugen, bei einer
	//    Decke nur an der Kante); Oberseite aus dem gemeinsamen Eismaterial; die Kante hat Dicke, davor Grieseis; zum Ufer weich auf den Grund ──
	float eismaske = 0.0;
	float eis_alpha = 0.0;   // Deckkraft des Eises (ohne Bildschirmtextur): am Ufer dünner
	vec3 eis_leuchten = vec3(0.0); // Himmel im Eis und Licht aus seinem Inneren (eis_himmel)
	vec3 eis_durch = vec3(0.0);    // Licht, das durch das Eis scheint
#ifdef MIT_EIS
	float eis_art = mix(eis, eis_still, still);
	float reichweite = mix(eis_reichweite, still_reichweite, still);
	if (eis_art > 0.001 && reichweite > 0.01) {
		float abstand = ufer;
		vec2 grad = ufer_grad;
		if (ufer_im_netz < 0.5) {
			abstand = dist * 2.0; // ohne Uferwerte grob aus der Tiefe geschätzt
			grad = vec2(0.0);
		}
		float gross = min(reichweite, 12.0);
		float unruhe = gross * 0.42 * rand_unruhe;  // großflächige Buchten und Zungen (m)
		// Weiter draußen als Eis, Platten und Grieseis reichen können: nichts rechnen (spart im offenen Fluss alles)
		if (abstand < eis_art * reichweite * 1.3 + unruhe + 3.5) {
			float offen_hier = offen;
			if (loch_zahl > 0) {
				offen_hier = max(offen_hier, loch_offen(welt_xz));
			}
			float eis_hier = eis_art * (1.0 - offen_hier);
			float zelle_m = clamp(reichweite * 0.75, 1.3, 3.6);
			// Die Kante zittert etwas (Ort leicht verzogen): keine geraden Linien
			vec2 p = welt_xz + (vec2(w_rausch(welt_xz * 1.4), w_rausch(welt_xz * 1.4 + 9.2)) - 0.5) * min(zelle_m, 1.5) * 0.45;
			vec2 c1;
			vec2 c2;
			float grenze;
			eis_zellen(p, zelle_m, c1, c2, grenze);
			// Wie weit das Eis über die Mitte einer Platte hinausreicht (> 0: Platte gehört zum Eis); je Platte etwas anders
			float anlauf = smoothstep(0.0, 0.3, eis_hier);
			// Löcher wirken je Platte (an ihrer Mitte): Der Rand eines Loches bricht wie die übrige Eiskante
			float eis_c1 = eis_hier;
			float eis_c2 = eis_hier;
			if (loch_zahl > 0) {
				eis_c1 = eis_art * (1.0 - max(offen, loch_offen(c1)));
				eis_c2 = eis_art * (1.0 - max(offen, loch_offen(c2)));
			}
			// Eine Decke (Bach, Teich, Hafenbecken) schließt sicher: etwas mehr Reichweite, sonst blieben im schmalen Bach einzelne Platten offen
			bool decke = rand_unruhe < 0.99;
			float reich_w = reichweite * (decke ? 1.3 : 1.0);
			float w1 = eis_c1 * reich_w + ((w_fbm(c1 * 0.045 + 17.0) - 0.5) * 2.0 * unruhe + (w_hash(c1 * 3.1 + 7.0) - 0.5) * zelle_m * 0.9 * rand_unruhe) * anlauf
				- (abstand + dot(c1 - p, grad));
			float w2 = eis_c2 * reich_w + ((w_fbm(c2 * 0.045 + 17.0) - 0.5) * 2.0 * unruhe + (w_hash(c2 * 3.1 + 7.0) - 0.5) * zelle_m * 0.9 * rand_unruhe) * anlauf
				- (abstand + dot(c2 - p, grad));
			float aa = bildpunkt_m * 1.2 + 0.01;
			float kante_aussen = 99.0;  // Abstand (m) vor der nächsten Platte (offenes Wasser)
			float kante_innen = 99.0;   // Abstand (m) hinter der Bruchkante (auf dem Eis): nasser Rand, Lippe
			float riss_platte = 0.0;    // Fuge zwischen zwei Platten
			if (w1 > 0.0 && w2 > 0.0) {
				eismaske = 1.0;
				// Randeis: Nahe der Kante gehen manche Fugen auf (bis 9 cm, offenes Wasser darin), weiter innen sind sie feine Risse, nur nah.
				// Eine Decke hat keine Fugen (aus der Höhe sah man sonst das Mosaik der Platten)
				if (!decke) {
					float spalt = mix(0.004, 0.09, (1.0 - smoothstep(0.0, zelle_m * 0.35, min(w1, w2))) * step(0.55, w_hash(c1 + c2 + vec2(9.0, 4.0))));
					float fuge = 1.0 - smoothstep(spalt, spalt + aa, grenze);
					float offen_fuge = smoothstep(0.03, 0.09, spalt);
					eismaske = 1.0 - fuge * offen_fuge;
					riss_platte = fuge * (1.0 - offen_fuge) * step(0.6, w_hash(c1 + c2 + vec2(3.0, 1.0))) * 0.6;
					kante_aussen = fuge * offen_fuge > 0.5 ? max(spalt - grenze, 0.0) : 99.0;
					kante_innen = fuge * offen_fuge > 0.5 ? 99.0 : max(grenze - spalt, 0.0) + (1.0 - offen_fuge) * 99.0;
				}
			} else if (w1 > 0.0) {
				eismaske = smoothstep(-aa * 0.5, aa * 0.5, grenze);
				kante_innen = grenze;
			} else if (w2 > 0.0) {
				eismaske = 1.0 - smoothstep(-aa * 0.5, aa * 0.5, grenze);
				kante_aussen = grenze;
			}

			// Grieseis: vor der Kante ein grauer, matter, löchriger Saum (gut 1,5 m), der mit dem Wasser treibt und kaum Kräusel hat
			float w_hier = eis_hier * reich_w + (w_fbm(p * 0.045 + 17.0) - 0.5) * 2.0 * unruhe * anlauf - abstand;
			float matsch = (1.0 - smoothstep(0.0, 1.8, -w_hier)) * smoothstep(-0.2, 0.5, 1.5 - max(w_hier, 0.0)) * smoothstep(0.0, 0.15, eis_hier);
			vec2 tp = lauf_uv - stroemung * TIME * 0.6;
			matsch *= smoothstep(0.25, 0.6, w_fbm(tp * 0.9 + 4.0)) * (1.0 - eismaske);
			matsch = max(matsch, (1.0 - smoothstep(0.0, 0.6, kante_aussen)) * 0.7 * (1.0 - eismaske));
			if (matsch > 0.001) {
				// Treibende Flocken (wie auf den Fotos: weiße Punkte auf dunklem Wasser), dazwischen grauer Brei
				float flocke = smoothstep(0.62, 0.86, w_rausch(tp * 3.1)) + smoothstep(0.8, 0.95, w_rausch(tp * 7.3 + 2.0)) * 0.6;
				color = mix(color, mix(eis_dunkel * 0.75, eis_hell * 0.85, clamp(flocke, 0.0, 1.0)), matsch * 0.5);
				roughness = mix(roughness, 0.6, matsch * 0.6);
				normalmap = mix(normalmap, vec3(0.5, 0.5, 1.0), matsch * 0.55);
				schaum *= 1.0 - matsch;
			}
			// Unter Wasser vor der Kante: die Stirn der Platte (blaugrün, sie leuchtet wie die Stirn der Schollen), direkt an der Kante ein dunkler
			// Schattenstrich
			if (kante_aussen < 0.3) {
				float stirn = 1.0 - smoothstep(0.0, 0.22, kante_aussen);
				color = mix(color, eis_stirn * 0.75 * mix(0.6, 1.0, depth_blend), stirn * 0.4);
				color *= 1.0 - 0.3 * (1.0 - smoothstep(0.0, 0.03 + aa, kante_aussen));
			}

			if (eismaske > 0.001) {
				// Die Oberseite: dasselbe Material wie die Schollen (eis_material.gdshaderinc). Zur Kante hin mehr klares (junges) Eis, am Ufer mehr
				// Schnee; der Anteil wechselt stetig über die Fläche (nicht je Platte)
				float aussen = clamp(abstand / max(eis_hier * reichweite, 0.5), 0.0, 1.0);
				Eisoberseite e = eis_oberseite(welt_xz, (aussen - 0.55) * (decke ? 0.4 : 0.8), wetter_schnee * mix(1.15, 0.9, aussen), color, bildpunkt_m);
				vec3 eisfarbe = e.farbe;
				// Fugen des Randeises: nur nah, dünn
				eisfarbe = mix(eisfarbe, mix(eis_dunkel * 0.75, eis_hell * 1.05, e.klar), riss_platte * 0.5 * (1.0 - smoothstep(0.03, 0.1, bildpunkt_m)));
				// Die Bruchkante hat Dicke wie die Schollen: an der Kante die gerundete, nasse, blaugrün glänzende Stirn, dahinter ein nasser,
				// dunkler Rand (Wasser schwappt über), dann trockenes Eis
				float lippe = 1.0 - smoothstep(0.0, 0.06 + aa, kante_innen);
				float nass = 1.0 - smoothstep(0.04, 0.3 + aa, kante_innen);
				eisfarbe = mix(eisfarbe, eisfarbe * 0.55 + eis_stirn * 0.12, nass * 0.8 * (1.0 - e.schnee * 0.5));
				eisfarbe = mix(eisfarbe, eis_stirn * 0.85 + eis_hell * 0.2, lippe * 0.7);
				float rau_eis = mix(e.rau, 0.08, max(nass, lippe));
				// Zum Ufer weich auf den Grund auslaufen: Wo das Wasser nur noch wenige Zentimeter tief ist, liegt dünnes, klares Eis auf dem Grund
				// (er scheint durch, mit Reif), und die Grenze zum Ufer folgt dem Rauschen statt einer geraden Linie
				float n_ufer = w_fbm(welt_xz * 0.45 + 31.0);
				float ufer_aus = smoothstep(0.02, 0.3, lot_tiefe + (n_ufer - 0.5) * 0.36);
				// nur nahe am Ufer: weiter draußen liegt das Eis auf dem Wasser, auch wo es flach ist (im Bach sonst dunkle Flecken mitten im Eis)
				float saum_m = max(ufer_saum, 0.3);
				ufer_aus = max(ufer_aus, smoothstep(saum_m * 1.5, saum_m * 3.0, abstand));
				// Das Netz endet am Ufer (Uferabstand 0) mit einer geraden Linie: Das Eis hört davor unregelmäßig auf (0–1,8 m), dahinter liegt der Grund
				if (ufer_saum > 0.01) {
					float saum_zitter = (w_rausch(welt_xz * 2.1) - 0.5) * min(ufer_saum, 0.6) * 0.6;
					ufer_aus = min(ufer_aus, smoothstep(ufer_saum * 0.14, ufer_saum * 0.5, abstand - ufer_saum * smoothstep(0.3, 0.75, n_ufer) - saum_zitter));
				}
				float reif = smoothstep(0.55, 0.8, w_fbm(welt_xz * 4.0 + 6.0));
				// Der Grund im Flachen ist Schlamm und Kies (der Boden zeichnet dort noch die Wiese): entsättigt, bräunlich, mit Reif
				vec3 matschgrund = mix(grund_farbe, vec3(dot(grund_farbe, vec3(0.3, 0.5, 0.2))) * vec3(1.0, 0.93, 0.82), 0.7) * 0.9;
				vec3 auf_grund = mix(matschgrund, eis_hell * 0.9, clamp(reif * 0.25 + wetter_schnee * 0.6, 0.0, 1.0)); // bei Schnee kaum ein Sandstreifen
				eisfarbe = mix(auf_grund, eisfarbe, ufer_aus);
				color = mix(color, eisfarbe, eismaske * 0.97);
				schaum *= 1.0 - eismaske;
				roughness = mix(roughness, mix(0.3, rau_eis, ufer_aus), eismaske);
				specular = mix(specular, mix(e.spec, 0.6, max(nass, lippe)), eismaske);
				// Relief des Eises statt der Kräusel (Neigung in Weltachsen x, z wie die Normalkarten des Flusses nach dem Drehen)
				vec2 neig = clamp(e.neigung * ufer_aus, vec2(-0.85), vec2(0.85));
				normalmap = mix(normalmap, vec3(neig * 0.5 + 0.5, 1.0), eismaske);
				eis_alpha = eismaske * mix(0.35, 1.0, ufer_aus);
				float fr = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 4.0);
				eis_leuchten = eis_himmel(e, fr, wetter_himmel) + eis_stirn * wetter_himmel * 0.3 * lippe;
				eis_durch = e.durch * ufer_aus;
			}
		}
	}
#endif

	color = mix(color, schaum_farbe, schaum);
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
	alpha = max(alpha, max(schaum, eis_alpha) * deckend);
#endif

	ALBEDO = color;
	ALPHA = clamp(alpha, 0.0, 1.0);
	METALLIC = 0.1 * (1.0 - schaum) * (1.0 - eismaske);
	ROUGHNESS = roughness;
	SPECULAR = specular;
	EMISSION = wetter_himmel * (fresnel * himmel_staerke + himmel_spiegel) * (1.0 - schaum) * (1.0 - fahne.x * 0.45);
	// Trübes Wasser streut das Himmelslicht zurück (Eigenfarbe), auch dort, wo die Spiegelung überwiegt
	EMISSION += muendung_farbe * wetter_himmel * fahne.x * 0.22 * (1.0 - 0.6 * winter_ton);
	// Eis spiegelt und leuchtet wie die Schollen (eis_material.gdshaderinc)
	EMISSION = mix(EMISSION, eis_leuchten, eismaske);
	NORMAL_MAP = normalmap;
	// Tangente und Binormale sind so lang wie die Summe der Wellenzüge (3 bzw. 5): Für das Relief des Eises gleicht die Tiefe das aus, damit es so
	// stark geneigt ist wie auf den Schollen (sonst fünffach: das Eis wirkte fleckig und dunkler als die Schollen daneben)
	NORMAL_MAP_DEPTH = mix(normal_tiefe * (1.0 + sturm * 0.6), 0.8 / max(length(vertex_tangent), 1.0), eismaske);
#ifdef MIT_EIS
	BACKLIGHT = eis_durch * eismaske;
#endif
}
