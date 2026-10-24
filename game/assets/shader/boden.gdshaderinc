// Boden der Geländekacheln (Kartenwerkstatt, Schritte 5e, 5g und 5h). Eingebunden von boden.gdshader (Hoch), boden_mittel.gdshader und
// boden_niedrig.gdshader, die nur BODEN_STUFE setzen (die Grafik-Einstellung „Boden“, Stadtansicht.Boden.cs wählt den Shader):
//   0 Niedrig: einfache Mischung (Gewicht mal Texturhöhe, weiche Ränder), zwei Maßstäbe je Textur übereinander.
//   1 Mittel:  Höhenmischung mit Rangfolge (scharfe, unregelmäßige Grenzen), Kachel-Brechung gegen sichtbare Wiederholung.
//   2 Hoch:    dazu Normalen und Rauheit der Texturen im Licht und am Hang dreiseitig abgetastet (keine Verzerrung).
// In allen Stufen: große Farb- und Helligkeitsflecken über die Welt (Makro-Variation) und Fels am Hang ab etwa 35° Gefälle.
// Schritt 5i: ab Mittel ein breiterer, ausgefranster Überhang der Wiese über Erde und Wege mit einer dunklen Schattenkante davor; in Hoch kräftigere
// Normalen und Makro-Flecken (makro_farbe in boden_gemein.gdshaderinc, die auch der Grasteppich nimmt).
//
// Gewichte je Punkt des Bodenrasters (1 m): Gruppe 0 = Wiese, Erde, Fels, Kies; Gruppe 1 = Waldboden, Sand, Schlamm, Moos; Gruppe 2 = Lehm,
// Pflaster, Mist (RGBA8, zusammen 255). Das Bild einer Kachel beginnt einen Punkt vor ihr (130 × 130 Punkte), damit die Übergänge über
// Kachelkanten glatt bleiben. Die Texturen (boden/boden_farbe_hoehe.webp, Texture2DArray) tragen Farbe und in A die Höhe,
// boden_normal_rauheit.webp Normale und Rauheit.
// Dazu die Wegeschicht (Schritt 5g, Stadtansicht.Wegeschicht.cs): je Kachel ein Bild zu 0,25 m mit Abstand zur Wegmitte (R, 0–10 m), Abstand zum
// Wegrand (G, −6 … +2 m, innen negativ), Belagstufe (B, 0 Pfad … 3 Pflaster, 4 Baustelle) und Spurrillen (A), dazu grob (1 m) Austritt, Zustand
// und Pflaster als Grund (B, in der Mauer). Spurrillen, Rinnen und Randsteine wirken als Unebenheit aufs Licht.
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://assets/shader/boden_gemein.gdshaderinc"

uniform sampler2D splat0 : filter_linear, repeat_disable;
uniform sampler2D splat1 : filter_linear, repeat_disable;
uniform sampler2D splat2 : filter_linear, repeat_disable;
uniform sampler2DArray texturen : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
#if BODEN_STUFE >= 2
// Normalen und Rauheit je Textur (gleiche Schichten wie texturen; Aufbau in nr_normale und nr_rauheit). Fehlen sie (nr_da = false),
// wird die Höhe der Texturen als Unebenheit genommen (Bump über Ableitungen, wie bei den Wegen).
uniform sampler2DArray texturen_nr : filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool nr_da = false;
#endif
// Welt-X/Z des ersten Punktes im Bild (eine Kachel minus 1 m) und Bildgröße in Punkten
uniform vec2 ursprung = vec2(0.0);
uniform float bildgroesse = 130.0;
// Länge einer Textur in Metern
uniform float massstab = 6.0;
// false: die Kachel hat keinen gemalten Punkt (nur Wiese, keine Gewichtsbilder)
uniform bool gemalt = true;
// Wegeschicht der Kachel
uniform bool wege_an = false;
uniform sampler2D wege : filter_linear_mipmap, repeat_disable;
uniform sampler2D wege_info : filter_linear, repeat_disable;
// Welt-X/Z der Kachelecke
uniform vec2 wege_ursprung = vec2(0.0);
// Grasteppich an (Schritt 5i, Stadtansicht.Gras.cs): Die Wiese darunter wird in seinen Horsten dunkler (Schatten der Halme)
uniform float teppich = 0.0;

const float WEG_TEXEL = 0.25;
const float WEG_BILD = 514.0;
const float WEG_INFO = 130.0;
const int T_WIESE = 0;
const int T_ERDE = 1;
const int T_FELS = 2;
const int T_KIES = 3;
const int T_SCHLAMM = 6;
const int T_LEHM = 8;
const int T_PFLASTER = 9;

// Rangfolge der Höhenmischung (ab Mittel): An einer Grenze gewinnt, was höher steht – Anteil (0–1) plus Texturhöhe mal HOEHE_GEWICHT plus
// dieser Versatz. Wer weiter oben steht, wächst über die anderen: Wiese über Erde, Erde über Fels, Moos über Fels.
const float RANG[11] = float[](
	0.22,   // Wiese: Halme wachsen über Erde, Sand, Lehm und an den Rand von Kies und Pflaster (5i: von 0,16 auf 0,22, breiterer Überhang)
	0.02,   // Erde
	-0.06,  // Fels: alles legt sich darüber, nur die Kuppen ragen heraus
	0.07,   // Kies: über Erde und Sand, Steine ragen aus dem Gras
	0.12,   // Waldboden: Laub und Nadeln decken Erde und Fels
	0.0,    // Sand
	0.03,   // Schlamm
	0.20,   // Moos: kriecht über Fels, Waldboden und Erde, in die Ritzen
	-0.02,  // Lehm
	0.05,   // Pflaster: über Erde und Lehm, Gras wächst an seinen Rand
	0.09    // Mist
);
// So viel zählt die Texturhöhe (0–1) gegen den Anteil
const float HOEHE_GEWICHT = 0.4;
// Breite des Übergangs an einer Grenze (in Einheiten von Anteil und Höhe): klein = scharf
const float UEBERGANG = 0.05;
// Fels am Hang: Anteil des Fels zwischen diesen Gefällen (Kosinus von 28° und 40°); mit der Höhenmischung (Fels steht in der Rangfolge
// unten) zeigt er sich ab etwa 35°
const float HANG_AB = 0.883;
const float HANG_VOLL = 0.766;
// Schritt 5i (ab Mittel): ausgefranster Rand der Wiese (Stärke des feinen Rauschens in Einheiten von Anteil und Höhe), Breite der dunklen
// Schattenkante vor dem Überhang (ebenso) und wie dunkel sie höchstens wird; an Wegen die Breite des Überhangs in Metern
const float WIESE_FRANSEN = 0.09;
const float SCHATTENKANTE = 0.11;
const float SCHATTEN_DUNKEL = 0.36;
const float WEG_UEBERHANG = 0.4;
// Schritt 5i (Hoch): so viel kräftiger wirken die Normalen der Texturen
const float NORMAL_STAERKE = 1.7;

varying vec2 welt;
varying vec3 welt_n;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	welt_n = (MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz;
}

// Eine abgetastete Textur: Farbe (rgb) und Höhe (a), Normale in Weltrichtung (ohne Normalen: die des Geländes) und Rauheit
struct Schicht {
	vec4 fh;
	vec3 n;
	float rau;
};

#if BODEN_STUFE >= 2
// Aufbau von boden_normal_rauheit.webp: RG = Normale im Tangentenraum (0,5 = flach; OpenGL, G+ zeigt zum oberen Bildrand), B = Rauheit.
// Zurück kommt die Normale in Richtung der Bildachsen u (nach rechts) und v (nach unten, wie die UV-Koordinate) – G wird dafür umgedreht.
vec3 nr_normale(vec4 t) {
	vec2 xy = t.rg * 2.0 - 1.0;
	return vec3(xy.x, -xy.y, sqrt(max(1.0 - dot(xy, xy), 0.0)));
}

float nr_rauheit(vec4 t) {
	return t.b;
}
#endif

// Eine Textur von oben abtasten (uv = Welt-X/Z). n = Normale des Geländes; dx, dy = Ableitungen von Welt-X/Z im Bild.
// Niedrig: zwei Maßstäbe übereinander. Ab Mittel: Kachel-Brechung – jede „virtuelle Kachel“ (aus einem langsamen Rauschen) liegt anders
// verschoben und gedreht; zwei Nachbarn gehen nach ihrer Höhe ineinander über (nach Quilez, „texture repetition“).
Schicht eben(int nr, vec2 p, vec3 n, vec2 dx, vec2 dy) {
	Schicht s;
	vec2 uv = p / massstab;
	vec2 gx = dx / massstab;
	vec2 gy = dy / massstab;
	s.n = n;
	s.rau = 0.95;
#if BODEN_STUFE == 0
	vec4 a = textureGrad(texturen, vec3(uv, float(nr)), gx, gy);
	vec4 b = textureGrad(texturen, vec3(uv / 2.63 + vec2(0.37, 0.71), float(nr)), gx / 2.63, gy / 2.63);
	s.fh = mix(a, b, 0.35);
#else
	float k = rausch(p * (0.45 / massstab) + float(nr) * 13.7) * 6.0;
	float i = floor(k);
	float f = fract(k);
	// Drehung um den goldenen Winkel je Variante, Versatz aus einer gleichmäßigen Folge. Nur nahe der Grenze zweier Varianten (f zwischen
	// 0,28 und 0,72) werden beide gelesen, sonst nur eine – das spart die Hälfte der Abtastungen.
	float ia = f > 0.72 ? i + 1.0 : i;
	float wa = ia * 2.39996;
	mat2 ra = mat2(vec2(cos(wa), sin(wa)), vec2(-sin(wa), cos(wa)));
	vec3 ua = vec3(ra * uv + fract(vec2(0.754877, 0.569840) * ia), float(nr));
	vec4 a = textureGrad(texturen, ua, ra * gx, ra * gy);
	vec4 b = a;
	mat2 rb = ra;
	vec3 ub = ua;
	float m = 0.0;
	if (f >= 0.28 && f <= 0.72) {
		float wb = wa + 2.39996;
		rb = mat2(vec2(cos(wb), sin(wb)), vec2(-sin(wb), cos(wb)));
		ub = vec3(rb * uv + fract(vec2(0.754877, 0.569840) * (i + 1.0)), float(nr));
		b = textureGrad(texturen, ub, rb * gx, rb * gy);
		// höchstens 0,12 verschoben: außerhalb des Bandes bleibt eine Variante ganz – sonst sähe man dort eine Naht
		m = smoothstep(0.4, 0.6, f + (b.a - a.a) * 0.12);
	}
	s.fh = mix(a, b, m);
#if BODEN_STUFE >= 2
	if (nr_da) {
		// zurück in Weltrichtung: die gedrehte Normale um denselben Winkel zurückdrehen
		vec4 na = textureGrad(texturen_nr, ua, ra * gx, ra * gy);
		vec3 t = nr_normale(na);
		t.xy = transpose(ra) * t.xy;
		s.rau = nr_rauheit(na);
		if (m > 0.0) {
			vec4 nb = textureGrad(texturen_nr, ub, rb * gx, rb * gy);
			vec3 tb = nr_normale(nb);
			tb.xy = transpose(rb) * tb.xy;
			t = normalize(mix(t, tb, m));
			s.rau = mix(s.rau, nr_rauheit(nb), m);
		}
		// auf die Fläche des Geländes (UDN-Mischung): u nach Welt-X, v nach Welt-Z
		s.n = normalize(vec3(t.x * NORMAL_STAERKE + n.x, n.y, t.y * NORMAL_STAERKE + n.z));
	}
#endif
#endif
	return s;
}

#if BODEN_STUFE >= 2
// Am Hang dreiseitig abtasten: von oben (mit Kachel-Brechung) und von den Seiten X und Z, gewichtet nach der Richtung des Geländes –
// so wird ein steiler Hang nicht verzerrt. Auf fast ebenem Boden nur von oben.
Schicht dreiseitig(int nr, vec3 p, vec3 n, vec3 dpx, vec3 dpy) {
	vec3 b = pow(abs(n), vec3(4.0));
	b /= b.x + b.y + b.z;
	if (b.y > 0.98) return eben(nr, p.xz, n, dpx.xz, dpy.xz);
	Schicht s = eben(nr, p.xz, n, dpx.xz, dpy.xz);
	s.fh *= b.y;
	s.n *= b.y;
	s.rau *= b.y;
	float rest = 0.0;
	if (b.x > 0.02) {
		vec3 u = vec3(p.zy / massstab, float(nr));
		vec4 t = textureGrad(texturen, u, dpx.zy / massstab, dpy.zy / massstab);
		s.fh += t * b.x;
		vec3 nw = n;
		float r = 0.95;
		if (nr_da) {
			vec4 q = textureGrad(texturen_nr, u, dpx.zy / massstab, dpy.zy / massstab);
			vec3 tn = nr_normale(q);
			nw = normalize(vec3(n.x, tn.y * NORMAL_STAERKE + n.y, tn.x * NORMAL_STAERKE + n.z));
			r = nr_rauheit(q);
		}
		s.n += nw * b.x;
		s.rau += r * b.x;
	} else rest += b.x;
	if (b.z > 0.02) {
		vec3 u = vec3(p.xy / massstab, float(nr));
		vec4 t = textureGrad(texturen, u, dpx.xy / massstab, dpy.xy / massstab);
		s.fh += t * b.z;
		vec3 nw = n;
		float r = 0.95;
		if (nr_da) {
			vec4 q = textureGrad(texturen_nr, u, dpx.xy / massstab, dpy.xy / massstab);
			vec3 tn = nr_normale(q);
			nw = normalize(vec3(tn.x * NORMAL_STAERKE + n.x, tn.y * NORMAL_STAERKE + n.y, n.z));
			r = nr_rauheit(q);
		}
		s.n += nw * b.z;
		s.rau += r * b.z;
	} else rest += b.z;
	// was eine ausgelassene Seite getragen hätte, geht an die übrigen
	float w = 1.0 - rest;
	s.fh /= w;
	s.n = normalize(s.n);
	s.rau /= w;
	return s;
}
#endif

// Der Fels am Hang: in Hoch dreiseitig, sonst von oben
Schicht hangfels(vec3 p, vec3 n, vec3 dpx, vec3 dpy) {
#if BODEN_STUFE >= 2
	return dreiseitig(T_FELS, p, n, dpx, dpy);
#else
	return eben(T_FELS, p.xz, n, dpx.xz, dpy.xz);
#endif
}

// Höhenmischung zweier Schichten (ab Mittel): a = Anteil der oberen (0–1) mit Höhe ho und Rang ro, die untere mit Höhe hu und Rang ru;
// gibt den sichtbaren Anteil der oberen zurück – scharf und dem Relief der Texturen folgend statt eines weichen Verlaufs.
float hmisch(float a, float ho, float ro, float hu, float ru, float breite) {
	float so = a + HOEHE_GEWICHT * ho + ro;
	float su = (1.0 - a) + HOEHE_GEWICHT * hu + ru;
	float m = max(so, su) - breite;
	float bo = max(so - m, 0.0);
	float bu = max(su - m, 0.0);
	return bo / max(bo + bu, 0.0001);
}

// Wie hmisch, dazu (y) die Schattenkante auf der oberen Schicht (Schritt 5i): je knapper die untere – die Wiese, die in der Rangfolge darüber
// wächst – dort verloren hat, desto dunkler; so liegt vor jedem Überhang ein schmaler Schatten.
vec2 hmisch_schatten(float a, float ho, float ro, float hu, float ru, float breite) {
	float so = a + HOEHE_GEWICHT * ho + ro;
	float su = (1.0 - a) + HOEHE_GEWICHT * hu + ru;
	float m = hmisch(a, ho, ro, hu, ru, breite);
	return vec2(m, (1.0 - smoothstep(0.0, SCHATTENKANTE, so - su)) * m);
}

// Der Weg über dem Boden <grund> (Farbe und Höhe gh, Rang gr, Normale gn, Rauheit grau): Farbe (rgb) und Rauheit (a), die Normale in nw.
// e = Abstand zum Rand (innen negativ), dm = Abstand zur Mitte, st = Stufe, rf = Stärke der Spurrillen, pb = Pflaster als Grund (gemalt oder
// in der Mauer): Dort wird jeder Weg zur Gasse im Pflaster – dunkler, ausgefahren, mit einer Rinne am Rand –, ohne Erde, Gras und Randsteine.
// Ab Mittel mischen die Ränder von Pfad, Fahrweg und Kies wie der Boden nach Höhe und Rang: Gras wächst über den Rand, Steine ragen heraus.
// Schritt 5i: Der Rand wird auf WEG_UEBERHANG Metern nach der Höhe entschieden (breiter, ausgefranster Überhang), davor liegt auf dem Weg eine
// Schattenkante – beides nur, soweit der Boden daneben Wiese ist (gw).
vec4 weg(vec3 p3, vec3 n, vec3 dpx, vec3 dpy, float kante, vec4 grund, float gr, float gw, float grau, float e, float dm, float st, float rf,
		float austritt, float zustand, float pb, Schicht pflaster, bool pflaster_da, inout float hoehe, inout vec3 nw) {
	vec2 p = p3.xz;
	vec2 dx = dpx.xz;
	vec2 dy = dpy.xz;
	float w_pf = max(0.0, 1.0 - abs(st - 3.0));
	float w_pd = max(0.0, 1.0 - abs(st));
	float w_fa = max(0.0, 1.0 - abs(st - 1.0));
	float w_ki = max(0.0, 1.0 - abs(st - 2.0));
	float w_bs = max(0.0, 1.0 - abs(st - 4.0));
	float stadt = smoothstep(0.3, 0.7, pb);
	float w_ga = (w_pd + w_fa + w_ki + w_pf) * stadt;
	w_pd *= 1.0 - stadt; w_fa *= 1.0 - stadt; w_ki *= 1.0 - stadt; w_pf *= 1.0 - stadt;
	float halb = max(dm - e, 0.3);
	float n1 = rausch(p * 0.8) - 0.5;
	float n2 = rausch(p * 3.3) - 0.5;
	float n3 = rausch(p * 11.0) - 0.5;
	float flecken = rausch(p * 0.35);
	// niedergetretenes Gras: dunkler, gelblicher, mit Flecken nackter Erde
	Schicht erde;
	erde.fh = vec4(0.0);
	erde.n = n;
	erde.rau = 0.95;
	if (w_pd + w_fa + w_bs > 0.0) erde = eben(T_ERDE, p, n, dx, dy);
	vec3 getreten = grund.rgb * vec3(0.9, 0.86, 0.68);
	vec3 farbe = vec3(0.0);
	float rau = 0.0;
	vec3 normale = vec3(0.0);

	// ── Trampelpfad: ausgetretener Streifen in der Mitte, daneben getretenes Gras, weicher, unregelmäßiger Rand ──
	if (w_pd > 0.0) {
		float en = e + n1 * 0.55 + n2 * 0.25 + n3 * 0.08;
		float streifen = halb * (1.0 - mix(0.55, 0.95, austritt));
		float kern = 1.0 - smoothstep(-streifen - 0.15, -streifen + 0.2, en);
		float schatten = 0.0;
#if BODEN_STUFE >= 1
		vec2 hs = hmisch_schatten(1.0 - smoothstep(-streifen - WEG_UEBERHANG * 0.5, -streifen + WEG_UEBERHANG * 0.5, en),
				erde.fh.a, RANG[T_ERDE], grund.a, gr, UEBERGANG);
		kern = hs.x;
		schatten = hs.y * gw;
#endif
		float tritt = 1.0 - smoothstep(-0.2, 0.8, en);
		vec3 boden = mix(grund.rgb, mix(getreten, erde.fh.rgb * 0.95, step(0.62, flecken + n3 * 0.6) * 0.6), tritt);
		farbe += mix(boden, erde.fh.rgb * mix(0.88, 1.06, n2 + 0.5), kern) * (1.0 - SCHATTEN_DUNKEL * schatten) * w_pd;
		rau += mix(grau, erde.rau, kern) * w_pd;
		normale += mix(nw, erde.n, kern) * w_pd;
	}

	// ── Fahrweg: festgefahrene Erde, Spurrillen entlang der Fahrtrichtung (Linien gleichen Abstands zur Mitte), Gras in der Mitte ──
	if (w_fa > 0.0) {
		float en = e + n1 * 0.5 + n2 * 0.22 + n3 * 0.08;
		float kern = 1.0 - smoothstep(-0.35, 0.05, en);
		float schatten = 0.0;
#if BODEN_STUFE >= 1
		vec2 hs = hmisch_schatten(1.0 - smoothstep(-0.15 - WEG_UEBERHANG * 0.5, -0.15 + WEG_UEBERHANG * 0.5, en),
				erde.fh.a, RANG[T_ERDE], grund.a, gr, UEBERGANG);
		kern = hs.x;
		schatten = hs.y * gw;
#endif
		float tritt = 1.0 - smoothstep(-0.1, 0.9, en);
		float spur = clamp(halb * 0.45, 0.45, 0.85);
		float rb = mix(0.08, 0.17, austritt);
		float rille = (1.0 - smoothstep(rb * 0.4, rb, abs(dm - spur + n2 * 0.06))) * rf * mix(0.35, 1.0, austritt);
		float streifen = rausch(vec2(dm * 9.0, (p.x + p.y) * 0.04));
		vec3 f = erde.fh.rgb * mix(0.9, 1.08, streifen) * (0.94 + 0.12 * n3);
		f *= 1.0 - 0.32 * rille;
		float mitte = (1.0 - smoothstep(0.0, spur * 0.65, dm)) * (1.0 - austritt) * smoothstep(0.35, 0.65, flecken + 0.25 * n3) * rf;
		f = mix(f, getreten, mitte * 0.8);
		vec3 boden = mix(grund.rgb, mix(getreten, erde.fh.rgb, step(0.66, flecken + n3 * 0.5) * 0.5), tritt);
		farbe += mix(boden, f, kern) * (1.0 - SCHATTEN_DUNKEL * schatten) * w_fa;
		rau += (mix(grau, erde.rau, kern) - 0.12 * rille) * w_fa; // in den Rillen steht Feuchte
		normale += mix(nw, erde.n, kern) * w_fa;
		hoehe -= 0.07 * rille * kern * w_fa;
	}

	// ── Kies: scharfer Rand, daneben verstreute Steinchen; verfallen kommen Erde und Gras durch ──
	if (w_ki > 0.0) {
		float en = e + n1 * 0.25 + n2 * 0.15 + n3 * 0.12;
		Schicht k = eben(T_KIES, p, n, dx, dy);
		float kern = 1.0 - smoothstep(-0.12, 0.08, en);
		float schatten = 0.0;
#if BODEN_STUFE >= 1
		vec2 hs = hmisch_schatten(1.0 - smoothstep(-0.3, 0.2, en), k.fh.a, RANG[T_KIES], grund.a, gr, UEBERGANG);
		kern = hs.x;
		schatten = hs.y * gw;
#endif
		vec2 zelle = p * 9.0;
		vec2 id = floor(zelle);
		float korn = step(length(fract(zelle) - 0.5), 0.32);
		float dichte = 1.0 - smoothstep(0.0, 0.7, e + n2 * 0.2);
		float streu = step(1.0 - dichte * 0.6, hash(id)) * korn * (1.0 - kern);
		float verfall = (1.0 - zustand) * smoothstep(0.30, 0.65, flecken + 0.4 * n1);
		vec3 kz = k.fh.rgb;
		if (verfall > 0.0) kz = mix(kz, mix(eben(T_ERDE, p, n, dx, dy).fh.rgb, grund.rgb, step(0.5, n3 + 0.5) * (1.0 - zustand)), clamp(verfall * 1.3, 0.0, 1.0));
		vec3 boden = mix(grund.rgb, getreten, (1.0 - smoothstep(0.0, 0.6, en)) * 0.5);
		float deckt = max(kern, streu);
		farbe += mix(boden, kz, deckt) * (1.0 - SCHATTEN_DUNKEL * schatten) * w_ki;
		rau += mix(grau, k.rau, deckt) * w_ki;
		normale += mix(nw, k.n, deckt) * w_ki;
	}

	// ── Pflaster: klare Kante, eine Reihe Randsteine; verfallen fehlen Steine ──
	if (w_pf > 0.0) {
		float b = kante;
		float kern = 1.0 - smoothstep(-b, b, e);
		Schicht ps = pflaster;
		if (!pflaster_da) ps = eben(T_PFLASTER, p, n, dx, dy);
		vec3 pf = ps.fh.rgb;
		// Randsteine: ein Streifen von 0,3 m an der Kante, Steine von etwa 0,5 m, grauer und gröber als das Pflaster
		float randstein = (1.0 - smoothstep(-0.32 - b, -0.32 + b, e)) * kern;
		vec2 rz = p * 2.2;
		float ton = hash(floor(rz));
		vec3 rs = vec3(dot(pf, vec3(0.33))) * mix(0.72, 0.95, ton) * vec3(1.0, 0.99, 0.96);
		float fuge = 1.0 - smoothstep(0.0, 0.05, min(min(fract(rz.x), 1.0 - fract(rz.x)), min(fract(rz.y), 1.0 - fract(rz.y))));
		rs = mix(rs, rs * 0.55, fuge * 0.7);
		float fugenrinne = 1.0 - smoothstep(0.0, 0.05, abs(e + 0.32));
		rs = mix(rs, rs * 0.6, fugenrinne); // die Rinne zwischen Randstein und Pflaster
		hoehe += (0.03 * randstein - 0.02 * fugenrinne * kern) * w_pf;
		pf = mix(pf, rs, randstein);
		float fehlt = step(zustand + 0.35, hash(floor(p * 3.0) + 23.0)) * step(zustand, 0.99);
		if (fehlt > 0.0) pf = mix(eben(T_ERDE, p, n, dx, dy).fh.rgb * 0.85, grund.rgb, step(0.45, hash(floor(p * 3.0) + 31.0)));
		vec3 boden = mix(grund.rgb, grund.rgb * 0.8, 1.0 - smoothstep(0.0, 0.15, e)); // ein dunkler Saum vor der Kante
		farbe += mix(boden, pf, kern) * w_pf;
		rau += mix(grau, ps.rau - 0.07, kern) * w_pf;
		normale += mix(nw, mix(ps.n, n, randstein), kern) * w_pf;
	}

	// ── Gasse im Pflaster: dunkler und schmutziger, ausgefahrene Spuren, eine Rinne am Rand ──
	if (w_ga > 0.0) {
		float kern = 1.0 - smoothstep(-kante, kante, e);
		Schicht ps = pflaster;
		if (!pflaster_da) ps = eben(T_PFLASTER, p, n, dx, dy);
		vec3 pf = ps.fh.rgb;
		float schmutz = clamp(0.45 + 0.7 * (flecken - 0.5) + 0.3 * n2, 0.0, 1.0);
		vec3 gasse = mix(pf, pf * vec3(0.74, 0.69, 0.62), schmutz);
		float spur = clamp(halb * 0.45, 0.45, 0.85);
		float rille = (1.0 - smoothstep(0.06, 0.2, abs(dm - spur + n2 * 0.05))) * rf;
		gasse *= 1.0 - 0.22 * rille;
		hoehe -= 0.025 * rille * kern * w_ga;
		vec3 f = mix(grund.rgb, gasse, kern);
		float rinne = 1.0 - smoothstep(0.04 + kante, 0.14 + kante, abs(e + 0.05));
		f = mix(f, f * 0.55, rinne * 0.8);
		hoehe -= 0.03 * rinne * w_ga;
		farbe += f * w_ga;
		rau += mix(grau, ps.rau - 0.06, kern) * w_ga;
		normale += mix(nw, ps.n, kern) * w_ga;
	}

	// ── Baustelle: aufgewühlter Lehm und Schlamm, Haufen von Kies ──
	if (w_bs > 0.0) {
		float en = e + n1 * 0.4 + n2 * 0.2;
		float kern = 1.0 - smoothstep(-0.2, 0.25, en);
		vec3 f = mix(eben(T_LEHM, p, n, dx, dy).fh.rgb, eben(T_SCHLAMM, p, n, dx, dy).fh.rgb, smoothstep(0.45, 0.75, rausch(p * 1.3)) * 0.45);
		f *= 0.85 + 0.25 * (n3 + 0.5);
		float haufen = smoothstep(0.62, 0.78, rausch(p * 0.9 + 4.0));
		f = mix(f, eben(T_KIES, p, n, dx, dy).fh.rgb, haufen * 0.85);
		farbe += mix(grund.rgb, f, kern) * w_bs;
		rau += grau * w_bs;
		normale += mix(nw, erde.n, kern) * w_bs;
	}
	nw = normalize(normale);
	return vec4(farbe, rau);
}

void fragment() {
	vec3 p3 = vec3(welt.x, 0.0, welt.y);
	p3.y = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).y;
	vec3 dpx = dFdx(p3);
	vec3 dpy = dFdy(p3);
	vec3 n = normalize(welt_n);
	float w[11] = float[](1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
	float pflaster = 0.0;
	if (gemalt) {
		vec2 uv = (welt - ursprung + vec2(0.5)) / bildgroesse;
		vec4 g0 = texture(splat0, uv);
		vec4 g1 = texture(splat1, uv);
		vec4 g2 = texture(splat2, uv);
		w[0] = g0.r; w[1] = g0.g; w[2] = g0.b; w[3] = g0.a;
		w[4] = g1.r; w[5] = g1.g; w[6] = g1.b; w[7] = g1.a;
		w[8] = g2.r; w[9] = g2.g; w[10] = g2.b;
		pflaster = g2.g;
	}
	// die Wegeschicht zuerst: Ganz im Pflaster der Stadt braucht es den Boden darunter nicht
	vec4 ws = vec4(1.0, 1.0, 0.25, 0.0);
	vec4 info = vec4(0.0, 1.0, 0.0, 1.0);
	if (wege_an) {
		ws = texture(wege, (welt - wege_ursprung + vec2(WEG_TEXEL)) / (WEG_BILD * WEG_TEXEL));
		info = texture(wege_info, (welt - wege_ursprung + vec2(1.0)) / WEG_INFO);
	}
	bool nur_stadt = info.b > 0.996;

	// Fels am Hang: ab etwa 35° Gefälle tritt er von selbst hervor (die Grenze leicht unregelmäßig). Er wird nach den übrigen Texturen
	// darübergelegt (in Hoch dreiseitig abgetastet); gemalter Fels am Hang gehört dazu.
	float hang = 0.0;
	if (n.y < HANG_AB + 0.03) hang = smoothstep(HANG_AB, HANG_VOLL, n.y + (rausch(welt * 0.35) - 0.5) * 0.06);
	float fels = 0.0;
	if (hang > 0.0) {
		fels = hang + (1.0 - hang) * w[T_FELS];
		float rest = 1.0 - w[T_FELS];
		w[T_FELS] = 0.0;
		if (rest > 0.01) for (int i = 0; i < 11; i++) w[i] /= rest;
		else w[T_WIESE] = 1.0;
	}

	// unregelmäßige Grenzen: ein langsames Rauschen verschiebt, wer wo gewinnt
	float rn = 0.5;
	if (gemalt) rn = rausch(welt * 0.45) * 0.6 + rausch(welt * 1.7) * 0.4;
	vec4 fh = vec4(0.0);
	vec3 nw = vec3(0.0);
	float rau = 0.0;
	float gr = 0.0; // Rang des Bodens (gewichtet), für die Ränder der Wege
	float wiese = w[T_WIESE];
	float schatten_kante = 0.0; // Schritt 5i: vor dem Überhang der Wiese (ab Mittel)
	if (nur_stadt) {
		wiese = 0.0;
		nw = n;
	} else {
#if BODEN_STUFE == 0
		float summe = 0.0;
		for (int i = 0; i < 11; i++) {
			if (w[i] < 0.012) continue;
			Schicht s = eben(i, welt, n, dpx.xz, dpy.xz);
			float gewinn = w[i] * (0.55 + s.fh.a * 0.9 + (hash(vec2(float(i), 3.0)) - 0.5) * (rn - 0.5) * 1.6);
			gewinn = max(gewinn, 0.0) * w[i];
			fh += s.fh * gewinn;
			rau += s.rau * gewinn;
			gr += RANG[i] * gewinn;
			summe += gewinn;
		}
		fh /= max(summe, 0.0001);
		rau /= max(summe, 0.0001);
		gr /= max(summe, 0.0001);
		nw = n;
#else
		// Höhenmischung mit Rangfolge über die (höchstens) drei stärksten Texturen
		int nr[3] = int[](-1, -1, -1);
		float an[3] = float[](0.0, 0.0, 0.0);
		for (int i = 0; i < 11; i++) {
			float a = w[i];
			if (a < 0.012) continue;
			int k = i;
			for (int j = 0; j < 3; j++) {
				if (a > an[j]) {
					float ta = an[j];
					int tk = nr[j];
					an[j] = a;
					nr[j] = k;
					a = ta;
					k = tk;
				}
			}
		}
		if (nr[1] < 0) {
			// nur eine Textur: nichts zu mischen
			Schicht s = eben(nr[0], welt, n, dpx.xz, dpy.xz);
			fh = s.fh;
			nw = s.n;
			rau = s.rau;
			gr = RANG[nr[0]];
		} else {
			vec4 sfh[3];
#if BODEN_STUFE >= 2
			vec3 sn[3];
			float sr[3];
#endif
			float sc[3] = float[](-9.0, -9.0, -9.0);
			float hoechst = -9.0;
			for (int j = 0; j < 3; j++) {
				if (nr[j] < 0) break;
				Schicht s = eben(nr[j], welt, n, dpx.xz, dpy.xz);
				sfh[j] = s.fh;
#if BODEN_STUFE >= 2
				sn[j] = s.n;
				sr[j] = s.rau;
#endif
				sc[j] = an[j] + HOEHE_GEWICHT * s.fh.a + RANG[nr[j]] + (hash(vec2(float(nr[j]), 3.0)) - 0.5) * (rn - 0.5) * 0.4;
				// der Rand der Wiese ausgefranst (feine Halmbüschel, etwa 15 cm)
				if (nr[j] == T_WIESE) sc[j] += (rausch(welt * 7.0) - 0.5) * 2.0 * WIESE_FRANSEN;
				hoechst = max(hoechst, sc[j]);
			}
			// der Übergang wird mit der Entfernung etwas breiter (weit weg flimmert eine scharfe Grenze)
			float breite = UEBERGANG + 0.25 * length(vec2(length(dpx), length(dpy)));
			float summe = 0.0;
			for (int j = 0; j < 3; j++) {
				if (nr[j] < 0) break;
				float b = max(sc[j] - hoechst + breite, 0.0);
				fh += sfh[j] * b;
#if BODEN_STUFE >= 2
				nw += sn[j] * b;
				rau += sr[j] * b;
#else
				rau += 0.95 * b;
#endif
				gr += RANG[nr[j]] * b;
				summe += b;
			}
			fh /= summe;
			// die dunkle Kante vor dem Überhang: auf dem, was die Wiese knapp nicht mehr deckt
			for (int j = 0; j < 3; j++) {
				if (nr[j] != T_WIESE) continue;
				float bw = max(sc[j] - hoechst + breite, 0.0) / summe;
				schatten_kante = (1.0 - smoothstep(0.0, SCHATTENKANTE, hoechst - sc[j])) * (1.0 - bw);
			}
			fh.rgb *= 1.0 - SCHATTEN_DUNKEL * schatten_kante;
#if BODEN_STUFE >= 2
			nw = normalize(nw);
#else
			nw = n;
#endif
			rau /= summe;
			gr /= summe;
		}
#endif

	}

	if (fels > 0.0) {
		Schicht f = hangfels(p3, n, dpx, dpy);
#if BODEN_STUFE == 0
		float m = fels;
#else
		float m = hmisch(fels, f.fh.a, RANG[T_FELS], fh.a, gr, UEBERGANG);
#endif
		fh = mix(fh, f.fh, m);
		nw = normalize(mix(nw, f.n, m));
		rau = mix(rau, f.rau, m);
		gr = mix(gr, RANG[T_FELS], m);
		wiese *= 1.0 - m;
	}

	// Makro-Variation: große Flecken heller und dunkler, auf der Wiese trockene (gelbliche) und satte Stellen – von weit oben keine Kacheln
	fh.rgb = makro_farbe(fh.rgb, welt, wiese, BODEN_STUFE >= 2);
	if (teppich > 0.0) fh.rgb *= 1.0 - teppich * wiese * mix(0.1, 0.24, gras_horst(welt));

	vec3 farbe = fh.rgb;
	float hoehe = 0.0; // Unebenheit in Metern, wirkt nur aufs Licht (Wege; in Hoch ohne Normalenbild auch die Texturhöhe)
	if (wege_an) {
		float e = ws.g * 8.0 - 6.0;
		float kante = max(fwidth(e), 0.02); // außerhalb der Verzweigung: Ableitungen brauchen alle Nachbarpunkte
		// Pflaster in der Mauer: Grund wie gemaltes Pflaster (einmal gelesen, auch für Gassen und Pflasterwege)
		Schicht ps;
		ps.fh = vec4(0.0);
		ps.n = n;
		ps.rau = 0.95;
		bool ps_da = info.b > 0.004;
		if (ps_da) {
			ps = eben(T_PFLASTER, welt, n, dpx.xz, dpy.xz);
			farbe = mix(farbe, ps.fh.rgb, info.b);
			fh.a = mix(fh.a, ps.fh.a, info.b);
			gr = mix(gr, RANG[T_PFLASTER], info.b);
			nw = normalize(mix(nw, ps.n, info.b));
			pflaster = max(pflaster, info.b);
			rau = mix(rau, min(ps.rau, 0.88), info.b);
		}
		if (e < 1.9) {
			vec4 wg = weg(p3, n, dpx, dpy, kante, vec4(farbe, fh.a), gr, wiese, rau, e, ws.r * 10.0, ws.b * 4.0, ws.a, info.r, info.g, pflaster, ps, ps_da, hoehe, nw);
			farbe = wg.rgb;
			rau = wg.a;
		}
	}
	ALBEDO = farbe;
	ROUGHNESS = clamp(rau, 0.0, 1.0);
#if BODEN_STUFE >= 2
	if (!nr_da) hoehe += fh.a * 0.025;
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
	{
#else
	if (wege_an) {
#endif
		// Höhe als Unebenheit im Licht (Bump-Mapping über Ableitungen, Mikkelsen 2010)
		vec3 vx = dFdx(VERTEX);
		vec3 vy = dFdy(VERTEX);
		vec3 r1 = cross(vy, NORMAL);
		vec3 r2 = cross(NORMAL, vx);
		float det = dot(vx, r1);
		vec3 grad = sign(det) * (dFdx(hoehe) * r1 + dFdy(hoehe) * r2);
		NORMAL = normalize(abs(det) * NORMAL - grad);
	}
}
