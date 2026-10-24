// Boden der Geländekacheln (Kartenwerkstatt, Schritt 5e): mischt bis zu elf gemalte Texturen nach drei Gewichtsbildern.
// Gemalt-realistisch wie die übrige 3D-Welt. Gewichte je Punkt des Bodenrasters (1 m): Gruppe 0 = Wiese, Erde, Fels, Kies;
// Gruppe 1 = Waldboden, Sand, Schlamm, Moos; Gruppe 2 = Lehm, Pflaster, Mist (RGBA8, zusammen 255). Das Bild einer Kachel
// beginnt einen Punkt vor ihr (66 × 66 Punkte), damit die Übergänge über Kachelkanten glatt bleiben.
// Die Texturen (boden.png, Texture2DArray) tragen Farbe und in A die Höhe: Wo zwei Texturen ineinander übergehen, wächst das
// Erhabene (Steine, Halme) über das Tiefe (Fugen, Mulden), und ein Rauschen macht die Grenzen unregelmäßig.
// Dazu die Wegeschicht (Schritt 5g, Stadtansicht.Wegeschicht.cs): je Kachel ein Bild zu 0,25 m mit Abstand zur Wegmitte (R, 0–10 m), Abstand zum
// Wegrand (G, −6 … +2 m, innen negativ), Belagstufe (B, 0 Pfad … 3 Pflaster, 4 Baustelle) und Spurrillen (A), dazu grob (1 m) Austritt, Zustand
// und Pflaster als Grund (B, in der Mauer). Spurrillen, Rinnen und Randsteine wirken als Unebenheit aufs Licht.
// Die Wege sind so in den Boden eingebacken: Kreuzungen verschmelzen im Abstandsfeld, die Ränder je Belag kommen aus Rauschen im Abstand.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
// Rauheit und Glanz wie der StandardMaterial3D der Wiese (0,95 / 0,5), damit Kacheln mit und ohne Boden gleich hell sind

uniform sampler2D splat0 : filter_linear, repeat_disable;
uniform sampler2D splat1 : filter_linear, repeat_disable;
uniform sampler2D splat2 : filter_linear, repeat_disable;
uniform sampler2DArray texturen : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
// Welt-X/Z des ersten Punktes im Bild (eine Kachel minus 1 m) und Bildgröße in Punkten
uniform vec2 ursprung = vec2(0.0);
uniform float bildgroesse = 66.0;
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

const float WEG_TEXEL = 0.25;
const float WEG_BILD = 514.0;
const float WEG_INFO = 130.0;
const int T_WIESE = 0;
const int T_ERDE = 1;
const int T_KIES = 3;
const int T_SCHLAMM = 6;
const int T_LEHM = 8;
const int T_PFLASTER = 9;

varying vec2 welt;

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

// Farbe (rgb) und Höhe (a) einer Textur: zwei Maßstäbe übereinander, damit sich nichts sichtbar wiederholt
vec4 probe(int nr, vec2 p) {
	vec4 a = texture(texturen, vec3(p / massstab, float(nr)));
	vec4 b = texture(texturen, vec3(p / (massstab * 2.63) + vec2(0.37, 0.71), float(nr)));
	return vec4(mix(a.rgb, b.rgb, 0.35), mix(a.a, b.a, 0.35));
}

// Der Weg über dem Boden <grund>: Farbe (rgb) und Rauheit (a). e = Abstand zum Rand (innen negativ), dm = Abstand zur Mitte, st = Stufe,
// rf = Stärke der Spurrillen, pb = Pflaster als Grund (gemalt oder in der Mauer): Dort wird jeder Weg zur Gasse im Pflaster – dunkler,
// ausgefahren, mit einer Rinne am Rand –, ohne Erde, Gras und Randsteine.
vec4 weg(vec2 p, float kante, vec3 grund, float e, float dm, float st, float rf, float austritt, float zustand, float pb, inout float hoehe) {
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
	vec3 erde = vec3(0.0);
	if (w_pd + w_fa + w_bs > 0.0) erde = probe(T_ERDE, p).rgb;
	vec3 getreten = grund * vec3(0.9, 0.86, 0.68);
	vec3 farbe = vec3(0.0);
	float rau = 0.95;

	// ── Trampelpfad: ausgetretener Streifen in der Mitte, daneben getretenes Gras, weicher, unregelmäßiger Rand ──
	if (w_pd > 0.0) {
		float en = e + n1 * 0.55 + n2 * 0.25 + n3 * 0.08;
		float streifen = halb * (1.0 - mix(0.55, 0.95, austritt));
		float kern = 1.0 - smoothstep(-streifen - 0.15, -streifen + 0.2, en);
		float tritt = 1.0 - smoothstep(-0.2, 0.8, en);
		vec3 boden = mix(grund, mix(getreten, erde * 0.95, step(0.62, flecken + n3 * 0.6) * 0.6), tritt);
		farbe += mix(boden, erde * mix(0.88, 1.06, n2 + 0.5), kern) * w_pd;
	}

	// ── Fahrweg: festgefahrene Erde, Spurrillen entlang der Fahrtrichtung (Linien gleichen Abstands zur Mitte), Gras in der Mitte ──
	if (w_fa > 0.0) {
		float en = e + n1 * 0.5 + n2 * 0.22 + n3 * 0.08;
		float kern = 1.0 - smoothstep(-0.35, 0.05, en);
		float tritt = 1.0 - smoothstep(-0.1, 0.9, en);
		float spur = clamp(halb * 0.45, 0.45, 0.85);
		float rb = mix(0.08, 0.17, austritt);
		float rille = (1.0 - smoothstep(rb * 0.4, rb, abs(dm - spur + n2 * 0.06))) * rf * mix(0.35, 1.0, austritt);
		float streifen = rausch(vec2(dm * 9.0, (p.x + p.y) * 0.04));
		vec3 f = erde * mix(0.9, 1.08, streifen) * (0.94 + 0.12 * n3);
		f *= 1.0 - 0.32 * rille;
		float mitte = (1.0 - smoothstep(0.0, spur * 0.65, dm)) * (1.0 - austritt) * smoothstep(0.35, 0.65, flecken + 0.25 * n3) * rf;
		f = mix(f, getreten, mitte * 0.8);
		vec3 boden = mix(grund, mix(getreten, erde, step(0.66, flecken + n3 * 0.5) * 0.5), tritt);
		farbe += mix(boden, f, kern) * w_fa;
		rau -= 0.12 * rille * w_fa; // in den Rillen steht Feuchte
		hoehe -= 0.07 * rille * kern * w_fa;
	}

	// ── Kies: scharfer Rand, daneben verstreute Steinchen; verfallen kommen Erde und Gras durch ──
	if (w_ki > 0.0) {
		float en = e + n1 * 0.25 + n2 * 0.15 + n3 * 0.12;
		float kern = 1.0 - smoothstep(-0.12, 0.08, en);
		vec2 zelle = p * 9.0;
		vec2 id = floor(zelle);
		float korn = step(length(fract(zelle) - 0.5), 0.32);
		float dichte = 1.0 - smoothstep(0.0, 0.7, e + n2 * 0.2);
		float streu = step(1.0 - dichte * 0.6, hash(id)) * korn * (1.0 - kern);
		vec3 k = probe(T_KIES, p).rgb;
		float verfall = (1.0 - zustand) * smoothstep(0.30, 0.65, flecken + 0.4 * n1);
		vec3 kz = mix(k, mix(probe(T_ERDE, p).rgb, grund, step(0.5, n3 + 0.5) * (1.0 - zustand)), clamp(verfall * 1.3, 0.0, 1.0));
		vec3 boden = mix(grund, getreten, (1.0 - smoothstep(0.0, 0.6, en)) * 0.5);
		farbe += mix(boden, kz, max(kern, streu)) * w_ki;
	}

	// ── Pflaster: klare Kante, eine Reihe Randsteine; verfallen fehlen Steine ──
	if (w_pf > 0.0) {
		float b = kante;
		float kern = 1.0 - smoothstep(-b, b, e);
		vec3 pf = probe(T_PFLASTER, p).rgb;
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
		pf = mix(pf, mix(probe(T_ERDE, p).rgb * 0.85, grund, step(0.45, hash(floor(p * 3.0) + 31.0))), fehlt);
		vec3 boden = mix(grund, grund * 0.8, 1.0 - smoothstep(0.0, 0.15, e)); // ein dunkler Saum vor der Kante
		farbe += mix(boden, pf, kern) * w_pf;
		rau -= 0.07 * kern * w_pf;
	}

	// ── Gasse im Pflaster: dunkler und schmutziger, ausgefahrene Spuren, eine Rinne am Rand ──
	if (w_ga > 0.0) {
		float kern = 1.0 - smoothstep(-kante, kante, e);
		vec3 pf = probe(T_PFLASTER, p).rgb;
		float schmutz = clamp(0.45 + 0.7 * (flecken - 0.5) + 0.3 * n2, 0.0, 1.0);
		vec3 gasse = mix(pf, pf * vec3(0.74, 0.69, 0.62), schmutz);
		float spur = clamp(halb * 0.45, 0.45, 0.85);
		float rille = (1.0 - smoothstep(0.06, 0.2, abs(dm - spur + n2 * 0.05))) * rf;
		gasse *= 1.0 - 0.22 * rille;
		hoehe -= 0.025 * rille * kern * w_ga;
		vec3 f = mix(grund, gasse, kern);
		float rinne = 1.0 - smoothstep(0.04 + kante, 0.14 + kante, abs(e + 0.05));
		f = mix(f, f * 0.55, rinne * 0.8);
		hoehe -= 0.03 * rinne * w_ga;
		farbe += f * w_ga;
		rau -= 0.06 * kern * w_ga;
	}

	// ── Baustelle: aufgewühlter Lehm und Schlamm, Haufen von Kies ──
	if (w_bs > 0.0) {
		float en = e + n1 * 0.4 + n2 * 0.2;
		float kern = 1.0 - smoothstep(-0.2, 0.25, en);
		vec3 f = mix(probe(T_LEHM, p).rgb, probe(T_SCHLAMM, p).rgb, smoothstep(0.45, 0.75, rausch(p * 1.3)) * 0.45);
		f *= 0.85 + 0.25 * (n3 + 0.5);
		float haufen = smoothstep(0.62, 0.78, rausch(p * 0.9 + 4.0));
		f = mix(f, probe(T_KIES, p).rgb, haufen * 0.85);
		farbe += mix(grund, f, kern) * w_bs;
	}
	return vec4(farbe, rau);
}

void fragment() {
	vec3 farbe = vec3(0.0);
	float pflaster = 0.0;
	if (gemalt) {
		vec2 uv = (welt - ursprung + vec2(0.5)) / bildgroesse;
		vec4 g0 = texture(splat0, uv);
		vec4 g1 = texture(splat1, uv);
		vec4 g2 = texture(splat2, uv);
		float w[11] = float[](g0.r, g0.g, g0.b, g0.a, g1.r, g1.g, g1.b, g1.a, g2.r, g2.g, g2.b);
		pflaster = g2.g;
		// unregelmäßige Grenzen: ein langsames Rauschen verschiebt, wer wo gewinnt
		float n = rausch(welt * 0.45) * 0.6 + rausch(welt * 1.7) * 0.4;
		float summe = 0.0;
		for (int i = 0; i < 11; i++) {
			if (w[i] < 0.012) continue;
			vec4 t = probe(i, welt);
			float gewinn = w[i] * (0.55 + t.a * 0.9 + (hash(vec2(float(i), 3.0)) - 0.5) * (n - 0.5) * 1.6);
			gewinn = max(gewinn, 0.0) * w[i];
			farbe += t.rgb * gewinn;
			summe += gewinn;
		}
		farbe /= max(summe, 0.0001);
	} else {
		farbe = probe(T_WIESE, welt).rgb;
	}
	float rau = 0.95;
	float hoehe = 0.0; // Unebenheit der Wege (Spurrillen, Rinnen, Randsteine) in Metern, wirkt nur aufs Licht
	if (wege_an) {
		vec4 s = texture(wege, (welt - wege_ursprung + vec2(WEG_TEXEL)) / (WEG_BILD * WEG_TEXEL));
		vec4 info = texture(wege_info, (welt - wege_ursprung + vec2(1.0)) / WEG_INFO);
		float e = s.g * 8.0 - 6.0;
		float kante = max(fwidth(e), 0.02); // außerhalb der Verzweigung: Ableitungen brauchen alle Nachbarpunkte
		// Pflaster in der Mauer: Grund wie gemaltes Pflaster
		if (info.b > 0.004) {
			farbe = mix(farbe, probe(T_PFLASTER, welt).rgb, info.b);
			pflaster = max(pflaster, info.b);
			rau = mix(rau, 0.88, info.b);
		}
		if (e < 1.9) {
			vec4 w = weg(welt, kante, farbe, e, s.r * 10.0, s.b * 4.0, s.a, info.r, info.g, pflaster, hoehe);
			farbe = w.rgb;
			rau = w.a;
		}
	}
	ALBEDO = farbe;
	ROUGHNESS = rau;
	if (wege_an) {
		// Höhe als Unebenheit im Licht (Bump-Mapping über Ableitungen, Mikkelsen 2010)
		vec3 dpdx = dFdx(VERTEX);
		vec3 dpdy = dFdy(VERTEX);
		vec3 r1 = cross(dpdy, NORMAL);
		vec3 r2 = cross(NORMAL, dpdx);
		float det = dot(dpdx, r1);
		vec3 grad = sign(det) * (dFdx(hoehe) * r1 + dFdy(hoehe) * r2);
		NORMAL = normalize(abs(det) * NORMAL - grad);
	}
}
