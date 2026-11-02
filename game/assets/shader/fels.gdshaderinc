// Fels-Material (Felspinsel F2): ein Shader für alle Felsstücke und Gesteinsarten. Die Gesteinsart kommt als Werte aus
// game/daten/gestein.json (Gesteinsarten.cs setzt sie), die Struktur aus einer kachelbaren Textur (tools/oberflaeche/felstexturen.py).
// Gemalt-realistisch: Godots eigenes Licht und eigene Schatten, kein light(), kein Umriss.
//
// Was er zeichnet, von unten nach oben:
//   1. Struktur (Bruchflächen, Risse, Körnung) – dreiseitig projiziert (DREISEITIG, ab Mittel: 3 Abtastungen) oder aus einer Richtung, der stärksten
//      der Fläche (Niedrig: 1)
//   2. Schichtbänder: Bänke nach Winkel und Dicke, hell und dunkel im Wechsel, dunkle Fugen, Absätze an der Oberkante jeder Bank
//   3. Klüfte: senkrechte Trennfugen (Pfeiler beim Trachyt, Blöcke bei Sandstein und Kalk), je Bank versetzt
//   4. Moos: auf Flächen, die nach oben zeigen, auf Absätzen, auf der Nordseite und in Höhlungen (statt echtem Schatten, siehe unten)
//   5. Flechten: Flecken auf freien Flächen, die nicht nach Norden zeigen (bunt und weißgrau)
//   6. Efeu in den Klüften steiler Wände (Trachyt)
//   7. „frisch gebrochen“ (Steinbruch): unter der Höhe frisch_bis kein Bewuchs, heller, glatter, mit Spaltrillen
// Alle Muster liegen in Weltkoordinaten (Felsstücke stehen still, das Muster schwimmt nicht) und kennen das einzelne Stück nicht (R1): Bänder,
// Klüfte und Farbe laufen über Stückgrenzen durch, eine Wand aus vielen Stücken liest sich als eine Masse. Neigung und Richtung der Bänke kommen
// aus gestein.json (band_winkel, band_richtung), ihre Dicke wellt sich über große Strecken.
// Makrovariation (R1): großflächige Helligkeit und Farbton (Wellenlängen etwa 15 und 60 m), einzelne Bänke mit Eisenfarbe (gestein.json: eisen),
// Auswaschung unter jeder Fuge und dunkle Wasserstreifen auf steilen Flächen.
// Fuß (R1): Die Höhe über dem Gelände kommt aus dem Höhenbild der Felsen (fels_gelaende, Stadtansicht.Felsgelaende.cs) an der Weltlage jedes
// Bildpunkts – auch bei großen, gekippten Stücken am Hang stimmt sie. In den untersten 0,35–1 m (20 % der Höhe des Stücks, INSTANCE_CUSTOM.y)
// nimmt der Fels die Farbe des Bodens an (Erde, Schutt der Gesteinsart, Moos), mit ausgefranster Grenze, und am Boden liegt eine dunkle Fuge.
// „Im Schatten“: Der Fragment-Shader weiß nicht, ob die Sonne ein Pixel erreicht (das rechnet Godot erst im Licht). Moos wächst
// deshalb dort, wo im Mittel des Tages Schatten ist: Nordseite (−z), Höhlungen der Struktur, Klüfte und unter Absätzen.
//
// Die echten Felsformen (F1, natur_felsen.glb, f1_netz = true; Felspinsel F5) bringen ihre gebackene Form mit: einen Atlas 4 × 2 über UV0 mit der
// Normalen (Tangentenraum, OpenGL-Richtung, felsen_normal.webp) und der Höhlung (felsen_hoehlung.webp: R Verdeckung, G Kante, B Rinne), dazu
// Vertexfarben (R Höhlung grob, G Kante, B Höhe im Stück 0 unten … 1 oben, A Tiefe 0 vorn … 1 zum Hang). Die Normale des Atlas ist die Grundform,
// die Struktur oben kommt als feine Körnung darauf. Ab Mittel (HOEHLUNG_BILD) kommt die Höhlung aus dem Bild, auf Niedrig aus den Ecken (eine
// Abtastung weniger). Verdeckung dunkelt und trägt Moos, Kanten bekommen Flechten und helle Abnutzung, Rinnen sind dunkel und tragen Moos im
// Schatten; am Fuß (Boden, INSTANCE_CUSTOM.x = Einsinktiefe in Metern) ein Moossaum, die Hangseite etwas dunkler und bemooster.
// Wetter wie die übrigen Oberflächen (wetter.gdshaderinc, wetter_flaeche): nass dunkler und glatter, Rinnsale auf steilen Flächen, Tropfen auf
// flachen (ab Mittel), Schnee auf allem, was nach oben zeigt. Die Platzhalter der Testszene (f1_netz = false) haben weder Atlas noch Vertexfarben.

#include "res://assets/shader/wetter.gdshaderinc"

uniform sampler2D struktur : hint_default_white, repeat_enable, filter_linear_mipmap_anisotropic;
// Die gebackene Form der F1-Felsen (Atlas über UV0, ohne sRGB) und ob das Netz sie hat
uniform bool f1_netz = false;
uniform sampler2D atlas_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D atlas_hoehlung : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform float atlas_staerke = 1.0;          // wie stark die Normale des Atlas wirkt
uniform float kachel = 2.5;                  // Meter je Kachel der Struktur

uniform vec4 farbe_hell : source_color = vec4(0.55, 0.57, 0.59, 1.0);
uniform vec4 farbe_dunkel : source_color = vec4(0.30, 0.32, 0.35, 1.0);
uniform vec3 band_normale = vec3(0.0, 1.0, 0.0);   // senkrecht auf den Schichten (Welt), aus Winkel und Richtung
uniform float band_dicke = 0.4;
uniform float band_kontrast = 0.25;
uniform float fuge = 0.5;
uniform float kluft_abstand = 0.0;
uniform float kantenrundung = 0.4;
uniform float rauheit = 0.88;
uniform float moos = 0.5;
uniform float flechten = 0.5;
uniform float efeu = 0.0;
uniform vec4 moos_farbe : source_color = vec4(0.31, 0.36, 0.13, 1.0);
uniform vec4 moos_trocken : source_color = vec4(0.44, 0.42, 0.17, 1.0);
uniform vec4 flechte_farbe : source_color = vec4(0.79, 0.54, 0.23, 1.0);
uniform vec4 flechte_hell : source_color = vec4(0.79, 0.78, 0.74, 1.0);
uniform vec4 efeu_farbe : source_color = vec4(0.18, 0.29, 0.11, 1.0);

uniform bool frisch = false;
uniform float frisch_bis = 1000.0;           // Welt-y: darunter frisch gebrochen, darüber die alte Oberkante mit Bewuchs
uniform float frisch_aufhellen = 0.22;
uniform float frisch_rauheit = 0.72;
uniform float spaltrillen = 0.5;
uniform float rillen_abstand = 0.35;
uniform float eisen = 0.0;                   // Anteil der Bänke mit Eisenfarbe und Stärke der rostigen Auswaschung (0 … 1)
uniform vec4 eisen_farbe : source_color = vec4(0.62, 0.36, 0.2, 1.0);
uniform float fuss_hoehe = 0.8;              // so hoch (m) nimmt der Fels am Fuß die Farbe des Bodens an (ohne Höhe des Stücks)

// Das Gelände unter den Felsen (Stadtansicht.Felsgelaende.cs): Höhe in Metern je Rasterpunkt (2 m); fels_gelaende_feld = Welt-X/Z der Ecke
// (eine halbe Zelle vor dem ersten Punkt) und 1 / Ausdehnung in Metern. z = 0: kein Bild (Testszene), dann gilt der Boden unter der Mitte des Stücks.
global uniform sampler2D fels_gelaende : filter_linear, repeat_disable;
global uniform vec4 fels_gelaende_feld;
// Farbe des Schutts der Gesteinsart der Karte (linear, Stadtansicht.Felsboden.cs); boden_gestein_an = 0: Karte ohne Felsen
global uniform vec3 boden_gestein_schutt;
global uniform float boden_gestein_an;

varying vec3 v_welt;
varying vec3 v_normale;
varying vec4 v_ecke;                         // Vertexfarbe (siehe oben)
varying float v_fuss;                        // Höhe des Übergangs am Fuß (m): 0,3 … 1,0 nach der Höhe des Stücks (INSTANCE_CUSTOM.y)
varying vec2 v_makro;                        // Makrovariation (15 m, 60 m) – im Vertex-Shader, die Netze sind dicht genug
varying float v_ueber;                       // Höhe über dem Boden unter der Mitte des Stücks (Ersatz ohne Höhenbild)

float hash1(vec3 p) {
	p = fract(p * vec3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yzx + 33.33);
	return fract((p.x + p.y) * p.z);
}

// Wertrauschen in 3D, 0..1 (nur Rechnung, keine Textur): Flechtenflecken und die Lage des Efeus
float wertrauschen(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash1(i), hash1(i + vec3(1, 0, 0)), f.x), mix(hash1(i + vec3(0, 1, 0)), hash1(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash1(i + vec3(0, 0, 1)), hash1(i + vec3(1, 0, 1)), f.x), mix(hash1(i + vec3(0, 1, 1)), hash1(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	v_welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_normale = normalize(MODEL_NORMAL_MATRIX * NORMAL);   // richtig auch bei ungleich skalierten Stücken
	v_ecke = COLOR;
	v_ueber = v_welt.y - (MODEL_MATRIX[3].y + INSTANCE_CUSTOM.x);
	// kleine Steine (Schutt) höchstens 30 % ihrer Höhe, sonst wären sie ganz bodenfarben
	v_fuss = INSTANCE_CUSTOM.y > 0.0 ? min(clamp(0.2 * INSTANCE_CUSTOM.y, 0.35, 1.0), 0.3 * INSTANCE_CUSTOM.y) : fuss_hoehe;
	v_makro = vec2(wertrauschen(v_welt * 0.067 + vec3(3.1, 1.7, 5.3)), wertrauschen(v_welt * 0.017 + vec3(7.7, 2.9, 0.4)));
}

// Neigung aus der Textur: kodiert wie tools/oberflaeche/texturen.py (128 ± 127)
vec2 neigung(vec2 c) {
	return (c * 255.0 - 128.0) / 127.0;
}

// Abstand (m) zur nächsten Linie einer Schar paralleler Linien im Abstand a entlang der Koordinate x
float linienabstand(float x, float a) {
	float f = fract(x / a);
	return min(f, 1.0 - f) * a;
}

void fragment() {
	vec3 p = v_welt;
	vec3 n = normalize(v_normale);
	float relief = mix(1.5, 0.55, kantenrundung);

	// Höhe über dem Gelände an genau diesem Punkt (siehe Kopf); ohne Höhenbild wie bisher
	float ueber = f1_netz ? v_ueber : 1.0;
	float fussAn = f1_netz ? 1.0 : 0.0;          // Platzhalter der Testszene ohne Höhenbild: kein Fuß
	if (fels_gelaende_feld.z > 0.0) {
		vec2 guv = (p.xz - fels_gelaende_feld.xy) * fels_gelaende_feld.zw;
		if (guv.x > 0.0 && guv.y > 0.0 && guv.x < 1.0 && guv.y < 1.0) {
			ueber = p.y - textureLod(fels_gelaende, guv, 0.0).r;
			fussAn = 1.0;
		}
	}
	// Makrovariation: zwei große Wellen in der Welt (etwa 15 und 60 m)
	float m15 = v_makro.x;
	float m60 = v_makro.y;

	// ── 0. Gebackene Form (F1) ──
	vec3 nf = n;                            // Grundform: Normale des Atlas in der Welt
	float aoForm = 1.0, kante = 0.0, rinne = 0.0, hoeheStueck = 0.5, tiefeStueck = 0.0;
	if (f1_netz) {
		vec2 t2 = texture(atlas_normal, UV).rg * 2.0 - 1.0;
		vec3 tn = vec3(t2 * atlas_staerke, sqrt(max(1.0 - dot(t2, t2), 0.0)));
		vec3 vn = normalize(TANGENT * tn.x + BINORMAL * tn.y + NORMAL * tn.z);
		nf = normalize((INV_VIEW_MATRIX * vec4(vn, 0.0)).xyz);
#ifdef HOEHLUNG_BILD
		vec3 hb = texture(atlas_hoehlung, UV).rgb;
		aoForm = hb.r;
		kante = hb.g;
		rinne = hb.b;
#else
		aoForm = v_ecke.r;                  // Verdeckung und Rinne zusammen (grob, je Ecke)
		kante = v_ecke.g;
		rinne = clamp((1.0 - v_ecke.r) * 0.8, 0.0, 1.0);
#endif
		hoeheStueck = v_ecke.b;
		tiefeStueck = v_ecke.a;
		relief *= 0.6;                      // die Form trägt der Atlas, die Struktur nur noch die Körnung
	}

	// ── 1. Struktur ──
	vec4 s;
	vec3 nw;
#ifdef DREISEITIG
	vec3 w = pow(abs(n), vec3(4.0));
	w /= (w.x + w.y + w.z);
	vec4 sx = texture(struktur, p.zy / kachel);
	vec4 sy = texture(struktur, p.xz / kachel);
	vec4 sz = texture(struktur, p.xy / kachel);
	s = sx * w.x + sy * w.y + sz * w.z;
	vec2 dx = neigung(sx.rg), dy = neigung(sy.rg), dz = neigung(sz.rg);
	// die Neigung jeder Projektion in ihre beiden Weltachsen (u, v der Abtastung = diese Achsen)
	nw = normalize(nf + relief * (w.x * vec3(0.0, dx.y, dx.x) + w.y * vec3(dy.x, 0.0, dy.y) + w.z * vec3(dz.x, dz.y, 0.0)));
#else
	// eine Abtastung aus der Richtung, in die die Fläche am stärksten zeigt (oben, Seite x oder Seite z): keine Streifen auf Wänden.
	// An den Nähten springt die Projektion; die Mip-Stufe kommt deshalb aus den Ableitungen der Weltlage (stetig über die Naht),
	// nicht aus denen der gewählten uv – sonst entstünden dort Linien aus der kleinsten Mip-Stufe.
	vec3 a = abs(n);
	vec3 px = dFdx(p) / kachel, py = dFdy(p) / kachel;
	bool oben_ = a.y >= a.x && a.y >= a.z;
	bool seiteX = !oben_ && a.x >= a.z;
	vec2 uv = oben_ ? p.xz : (seiteX ? p.zy : p.xy);
	vec2 gx = oben_ ? px.xz : (seiteX ? px.zy : px.xy);
	vec2 gy = oben_ ? py.xz : (seiteX ? py.zy : py.xy);
	s = textureGrad(struktur, uv / kachel, gx, gy);
	vec2 d = neigung(s.rg);
	vec3 kipp1 = oben_ ? vec3(d.x, 0.0, d.y) : (seiteX ? vec3(0.0, d.y, d.x) : vec3(d.x, d.y, 0.0));
	nw = normalize(nf + relief * kipp1);
#endif
	float h = s.b;                         // Höhe 0 tief … 1 hoch
	float fleck = s.a;

	// ── 2. Schichtbänder ──
	// in Weltlage über alle Stücke hinweg; die Grenze wellt sich mit der Struktur und zwei kurzen Wellen, die Bänke werden über große Strecken
	// dicker und dünner (m60) – so laufen sie durch eine ganze Wand, ohne mit dem Lineal gezogen zu wirken
	vec3 bn = normalize(band_normale);
	float welle = 0.25 * sin(dot(p.xz, vec2(0.61, 0.37))) + 0.15 * sin(dot(p, vec3(1.7, 0.3, -1.3)));
	float lage = dot(p, bn) / band_dicke + (h - 0.5) * 0.45 + welle + (m60 - 0.5) * 8.0;
	float bank = floor(lage);
	float inBank = fract(lage);
	float bankHell = hash1(vec3(bank, 13.0, 2.0)) - 0.5;
	float bankEisen = step(1.0 - 0.35 * eisen, hash1(vec3(bank, 4.0, 9.0)));   // einzelne Bänke rostfarben
	// Fuge mindestens 3 cm breit (auch bei dünnen Bänken sichtbar), bei gerundeten Kanten breiter
	float fugeBreite = max(mix(0.06, 0.14, kantenrundung), 0.03 / band_dicke);
	float unten = 1.0 - smoothstep(0.0, fugeBreite, inBank);         // Unterkante der Bank: Fläche neigt sich in die Fuge hinab
	float obenKante = 1.0 - smoothstep(0.0, fugeBreite, 1.0 - inBank); // Oberkante: Fläche neigt sich zum Absatz hinauf
	float fugeMaske = max(unten, obenKante) * fuge;
	nw = normalize(nw + bn * (obenKante - unten) * 0.8 * fuge);
	float absatz = smoothstep(0.7, 0.97, inBank);   // Oberkante der Bank (darüber liegt die Fuge)

	// ── 3. Klüfte ──
	float kluft = 0.0, kluftBreit = 0.0;
	if (kluft_abstand > 0.0) {
		vec2 r1 = vec2(0.955, 0.296);                    // fest in der Welt (etwa 17°): Klüfte laufen über Stückgrenzen durch
		vec2 r2 = vec2(-r1.y, r1.x);
		float warp = (h - 0.5) * 0.25 * kluft_abstand;
		float a1 = linienabstand(dot(p.xz, r1) + warp, kluft_abstand);
		// quer dazu je Bank versetzt (Blöcke statt Gitter)
		float a2 = linienabstand(dot(p.xz, r2) + warp + hash1(vec3(bank, 5.0, 1.0)) * kluft_abstand, kluft_abstand * 1.6);
		float a = min(a1, a2);
		kluft = 1.0 - smoothstep(0.015, 0.06, a);
		kluftBreit = 1.0 - smoothstep(0.05, 0.35, a);
		kluft *= 1.0 - smoothstep(0.6, 0.95, abs(nw.y));   // auf Oberseiten kaum zu sehen
	}

	// ── Grundfarbe ──
	float t = clamp(0.5 + (h - 0.5) * 0.45 + bankHell * 3.0 * band_kontrast + (fleck - 0.5) * 0.2, 0.0, 1.0);
	vec3 farbe = mix(farbe_dunkel.rgb, farbe_hell.rgb, t);
	// Makrovariation: Helligkeit und Farbton über große Flächen, rostige Bänke
	farbe *= mix(0.74, 1.2, m60 * 0.55 + m15 * 0.45);
	farbe = mix(farbe * vec3(0.94, 0.98, 1.05), farbe * vec3(1.07, 1.0, 0.88), smoothstep(0.3, 0.7, m15 * 0.6 + (1.0 - m60) * 0.4));
	farbe = mix(farbe, eisen_farbe.rgb * (0.65 + 0.55 * h), bankEisen * (0.3 + 0.25 * m15) * eisen);
	float ao = 1.0 - 0.55 * fugeMaske - 0.6 * kluft - 0.35 * (1.0 - h) * (1.0 - h);
	float rau = rauheit;
	if (f1_netz) {
		farbe = mix(farbe, farbe_hell.rgb * 1.08, 0.3 * kante);   // abgenutzte, helle Kanten
		farbe *= (1.0 - 0.3 * rinne) * (0.94 + 0.12 * hoeheStueck);   // Rinnen dunkel, oben heller
		ao *= mix(1.0, aoForm, 0.85) * (1.0 - 0.2 * tiefeStueck);       // Hangseite dunkler
	}

	// ── Auswaschung und Wasserstreifen (R1) ── auf steilen Flächen der Grundform: Unter jeder Fuge läuft Wasser über die Bank darunter
	// (dunkler, bei Eisen rostig), dazu lange, dunkle Rinnen aus senkrecht gestrecktem Rauschen, stärker in den Rinnen der Form
	float steilForm = 1.0 - smoothstep(0.35, 0.8, abs(nf.y));
	float streifen = wertrauschen(vec3(p.x * 1.6, p.y * 0.11, p.z * 1.6) + 11.0);
	float unterFuge = (1.0 - smoothstep(0.0, band_dicke * (0.5 + 1.5 * streifen) + 0.15, (1.0 - inBank) * band_dicke)) * fuge;
	float wasch = steilForm * smoothstep(0.35, 0.7, streifen) * unterFuge;
	float rinnsal = steilForm * smoothstep(0.62, 0.8, streifen + 0.25 * rinne + (m15 - 0.5) * 0.3);
	farbe *= 1.0 - 0.18 * wasch - 0.28 * rinnsal;
	farbe = mix(farbe, farbe * vec3(1.12, 0.92, 0.78), wasch * eisen * 0.8);

	// ── 7. frisch gebrochen (vor dem Bewuchs, weil es ihn wegnimmt) ──
	float fr = frisch ? 1.0 - smoothstep(frisch_bis - 0.25, frisch_bis + 0.25, p.y) : 0.0;
	if (fr > 0.0) {
		vec3 hell = mix(farbe_hell.rgb, farbe, 0.4) * (1.0 + frisch_aufhellen);
		farbe = mix(farbe, hell, fr);
		// Spaltrillen: senkrechte Rillen der Keile auf steilen Flächen, nur stellenweise
		vec3 q = vec3(n.z, 0.0, -n.x);                 // waagerecht in der Fläche (Kreuzprodukt mit oben)
		vec3 quer = q / max(length(q), 1e-4);
		float rille = 1.0 - smoothstep(0.0, 0.035, linienabstand(dot(p, quer) + 0.05 * sin(p.y * 3.0), rillen_abstand));
		rille *= (1.0 - abs(n.y)) * smoothstep(0.35, 0.6, fleck) * spaltrillen;
		farbe *= 1.0 - 0.35 * rille * fr;
		ao = mix(ao, 1.0 - 0.25 * kluft - 0.15 * (1.0 - h), fr);
		rau = mix(rau, frisch_rauheit, fr);
	}
	float bewuchs = 1.0 - fr;

	// ── 4. Moos ── (R1) als Polster nach der Weltlage statt eines gleichmäßigen Teppichs: auf Oberseiten und Absätzen, auf der Nordseite, in
	// Rinnen und Klüften; die Richtung kommt überwiegend aus der Grundform (nicht aus der feinen Körnung, die sonst Tarnflecken streut). Wo
	// es wächst, entscheiden zusätzlich große Zonen (m15) und Klumpen von etwa einem halben Meter.
	vec3 nm = normalize(mix(nf, nw, 0.35));
	float oben = smoothstep(0.35, 0.85, nm.y);
	float nord = clamp(-nm.z, 0.0, 1.0) * (1.0 - abs(nm.y));
	float zone = smoothstep(0.2, 0.75, m15 * 0.75 + m60 * 0.25);
	float klumpen = wertrauschen(p * 1.9 + 5.0);
	float mooswert = moos * (0.95 * oben * (0.55 + 0.9 * zone) + 0.5 * nord * (0.4 + 0.8 * zone) + 0.15 * (1.0 - h)
		+ 0.45 * absatz * clamp(nm.y + 0.4, 0.0, 1.0) + 0.35 * kluftBreit + 0.25 * rinnsal);
	if (f1_netz) {
		mooswert += moos * (0.35 * (1.0 - aoForm) * max(oben, nord) + 0.4 * rinne * (1.0 - oben) * (0.5 + nord) + 0.35 * kante * oben + 0.15 * tiefeStueck);
	}
	float saum = (1.0 - smoothstep(0.0, 0.5 + v_fuss, ueber)) * fussAn;   // Moossaum am Boden
	mooswert += 0.45 * moos * saum * (0.5 + nord + 0.5 * tiefeStueck);
	float moosRoh = mooswert + (klumpen - 0.5) * 0.55 + (fleck - 0.5) * 0.35;
	float moosMaske = smoothstep(0.4, 0.56, moosRoh) * bewuchs;
	vec3 moosFarbe = mix(moos_farbe.rgb, moos_trocken.rgb, clamp(fleck * 0.9 - 0.35 + oben * 0.15 - nord * 0.4 + (zone - 0.5) * -0.3, 0.0, 0.7));
	moosFarbe *= (0.8 + 0.4 * h) * (0.78 + 0.22 * smoothstep(0.45, 0.8, moosRoh));   // Polster: am Rand dunkler, in der Mitte voll

	// ── 5. Flechten ──
	float frei = clamp(0.75 + 0.35 * nw.z + 0.15 * nw.x, 0.0, 1.0) * (1.0 - 0.6 * oben) * (1.0 - kluftBreit);
	frei = clamp(frei * mix(1.0, aoForm, 0.6) * (1.0 - 0.7 * rinne) + 0.35 * kante, 0.0, 1.0);   // Flechten auf freien Kanten
	float flRausch = wertrauschen(p * 2.8 + 17.0) * 0.6 + wertrauschen(p * 9.0) * 0.4;
	float flSchwelle = 0.82 - 0.22 * flechten * frei;
	float flechteMaske = smoothstep(flSchwelle, flSchwelle + 0.04, flRausch * (0.6 + 0.4 * h)) * step(0.01, flechten) * (1.0 - moosMaske) * bewuchs;
	vec3 flechteFarbe = mix(flechte_hell.rgb, flechte_farbe.rgb, step(0.55, wertrauschen(p * 1.3 + 3.7)));

	// ── 6. Efeu in den Klüften ──
	float efeuMaske = 0.0;
	if (efeu > 0.0) {
		float steil = 1.0 - smoothstep(0.3, 0.7, abs(nw.y));
		float lage2 = wertrauschen(vec3(p.x * 0.7, p.y * 0.25, p.z * 0.7) + 9.0);
		efeuMaske = smoothstep(0.55, 0.7, kluftBreit * 0.7 + lage2 * efeu * 0.9 - 0.15 + (fleck - 0.5) * 0.4) * steil * bewuchs;
	}

	vec3 albedo = farbe;
	albedo = mix(albedo, flechteFarbe, flechteMaske);
	albedo = mix(albedo, moosFarbe, moosMaske);
	albedo = mix(albedo, efeu_farbe.rgb * (0.7 + 0.5 * fleck), efeuMaske);
	// Moos und Efeu glätten das Relief (Polster), Flechten liegen flach auf
	nw = normalize(mix(nw, nf, max(moosMaske, efeuMaske) * 0.6));   // zur Grundform, nicht zur groben Geometrie

	// ── Fuß (R1): Bodenfarbe in den untersten fuss_hoehe Metern ── Erde und Schutt der Gesteinsart wechseln fleckig, auf der Nord- und Hangseite
	// Moos; die Grenze franst mit dem Rauschen aus (statt einer Linie dort, wo das Netz den Boden schneidet). Darunter eine dunkle Fuge.
	float fuss = 0.0;
	if (fussAn > 0.0 && ueber < 1.5 * v_fuss + 0.6) {   // nur am Fuß rechnen (einheitlicher Zweig je Stück)
		float fussRand = ueber + (klumpen - 0.5) * 0.5 * v_fuss + (fleck - 0.5) * 0.2 * v_fuss;
		fuss = (1.0 - smoothstep(0.0, v_fuss, fussRand)) * fussAn;
		vec3 schutt = boden_gestein_an > 0.0 ? boden_gestein_schutt : farbe_dunkel.rgb * 0.8;
		vec3 erde = vec3(0.085, 0.066, 0.046);
		vec3 fussFarbe = mix(erde, schutt * (0.7 + 0.5 * h), smoothstep(0.35, 0.65, wertrauschen(p * 0.9 + 2.0) * 0.7 + h * 0.3));
		fussFarbe = mix(fussFarbe, moosFarbe * 0.85, clamp((0.35 + nord + 0.6 * tiefeStueck) * moos, 0.0, 0.75) * smoothstep(0.4, 0.6, klumpen + 0.15));
		// ganz unten (erstes Drittel) dunkles Grün wie Gras und Moos am Boden, darüber Erde und Schutt
		fussFarbe = mix(fussFarbe, jahreszeit_wiese(vec3(0.06, 0.085, 0.03)) * (0.8 + 0.4 * h), (1.0 - smoothstep(0.0, 0.35 * v_fuss, fussRand)) * 0.7 * bewuchs);
		albedo = mix(albedo, fussFarbe, fuss * 0.92 * bewuchs);
		ao *= mix(1.0, mix(0.55, 1.0, smoothstep(0.0, 0.35, ueber + 0.05)), fussAn);
		nw = normalize(mix(nw, nf, fuss * 0.5));
	}

	float rough = clamp(mix(rau, 1.0, max(max(moosMaske, efeuMaske), max(fugeMaske, fuss))), 0.0, 1.0);

	// ── 8. Wetter: Nässe, Rinnsale, Tropfen und Schnee wie die übrigen Oberflächen ──
	vec3 gl = vec3(0.0);
	vec3 su = cross(vec3(0.0, 1.0, 0.0), n);
	bool flach = length(su) < 0.05;
	su = flach ? vec3(1.0, 0.0, 0.0) : normalize(su);
	vec3 sv = flach ? vec3(0.0, 0.0, -1.0) : cross(n, su);
	float tiefeNass = clamp(0.5 + 0.5 * rinne + 0.3 * max(fugeMaske, kluft), 0.0, 1.0);   // Fugen und Rinnen bleiben länger nass
#ifdef DREISEITIG
	bool tropfen = true;
#else
	bool tropfen = false;
#endif
	wetter_flaeche(albedo, rough, nw, gl, nw, n, p, vec2(dot(p, su), dot(p, sv)), 0.3, 0.0, tiefeNass, ueber, TIME, tropfen);
	// Fels saugt mehr Wasser als Putz und Ziegel: nass noch etwas dunkler (Flechten und Moos weniger, sie halten das Wasser an der Oberfläche)
	albedo *= 1.0 - 0.22 * wetter_naesse * wetter_flaechen * (1.0 - 0.5 * max(moosMaske, flechteMaske));

	ALBEDO = albedo;
	ROUGHNESS = clamp(rough, 0.0, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.35;
	AO = clamp(ao, 0.0, 1.0);
	AO_LIGHT_AFFECT = 0.35;
	EMISSION = gl;
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
}
