// Belag der Wege (Kartenwerkstatt, Schritt 5d): ein flaches Band je Weg, darauf Trampelpfad, Fahrweg mit Spurrillen, Kies oder Pflaster.
// Gemalt-realistisch wie die übrige 3D-Welt: Erde, Gras, Kiesel und Pflastersteine mit Körnung und Flecken, ohne Holzschnitt-Look.
// Alle vier Stufen stecken in einem Shader und gehen über Gewichte ineinander über (weiche Übergänge an den Grenzen der Abschnitte).
// Vertexdaten (aus Stadtansicht.Wegbelag.cs): UV.x = Stelle entlang des Weges in Metern, UV.y = Abstand von der Mitte in Metern (links +),
// UV2.x = Austritt (0 zugewachsen … 1 tief ausgefahren), UV2.y = Zustand (1 neu … 0 verfallen),
// COLOR.rgb = Gewicht von Trampelpfad, Fahrweg und Kies (Pflaster = Rest), COLOR.a = halbe Breite des Weges geteilt durch 8.
shader_type spatial;
render_mode cull_disabled, blend_mix, depth_draw_opaque;

uniform vec3 gras : source_color = vec3(0.30, 0.36, 0.17);
uniform vec3 erde : source_color = vec3(0.55, 0.44, 0.31);
uniform vec3 kies : source_color = vec3(0.74, 0.70, 0.62);
uniform vec3 stein : source_color = vec3(0.62, 0.58, 0.53);
// 1: Baustelle (aufgewühlte Erde mit Kies- und Steinhaufen) statt des Belags
uniform float baustelle : hint_range(0.0, 1.0) = 0.0;
// Helligkeit, die die Jahreszeit dem Gras gibt (1 Sommer)
uniform float gruen : hint_range(0.0, 1.0) = 1.0;

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

void fragment() {
	float halb = max(0.2, COLOR.a * 8.0);
	float q = UV.y;
	float qn = q / halb;                       // −1 … 1 quer über den Weg
	float aq = abs(qn);
	vec2 p = vec2(UV.x, q);
	float austritt = clamp(UV2.x, 0.0, 1.0);
	float zustand = clamp(UV2.y, 0.0, 1.0);
	float w_tr = COLOR.r;
	float w_fa = COLOR.g;
	float w_ki = COLOR.b;
	float w_pf = clamp(1.0 - w_tr - w_fa - w_ki, 0.0, 1.0);

	float flecken = rausch(p * 0.35);
	float schollen = rausch(p * 1.7);
	float koernung = rausch(p * 9.0);
	float kante = rausch(vec2(UV.x * 0.9, q * 0.3));   // unregelmäßiger Rand
	float fern = 1.0 - smoothstep(0.5, 1.5, fwidth(UV.x) * 6.0);

	vec3 farbe = vec3(0.0);
	float deck = 0.0;     // Deckkraft: am Rand läuft jede Stufe weich ins Gelände aus
	float rau = 1.0;

	// ── Trampelpfad: ausgetretener Streifen, daneben Gras ──
	{
		float breite = mix(0.50, 0.95, austritt);
		float kern = 1.0 - smoothstep(breite * 0.75, breite + 0.05 + kante * 0.2, aq + (schollen - 0.5) * 0.25);
		vec3 g = gras * mix(0.8, 1.15, schollen) * mix(0.75, 1.0, gruen);
		g = mix(g, g * vec3(1.15, 1.1, 0.75), step(0.78, koernung) * 0.5);
		vec3 e = erde * mix(0.85, 1.12, schollen) * (0.92 + 0.16 * koernung);
		vec3 f = mix(g, e, kern);
		float a = 1.0 - smoothstep(0.80, 1.0, aq + (kante - 0.5) * 0.35);
		farbe += f * w_tr;
		deck += a * w_tr;
	}

	// ── Fahrweg: festgefahrene Erde mit zwei Spurrillen, in der Mitte und am Rand Gras ──
	{
		float spur = 0.40;
		float rbreit = mix(0.07, 0.14, austritt);
		float rille = 0.0;
		float wackel = (rausch(vec2(UV.x * 0.35, 3.0)) - 0.5) * 0.06;
		rille += 1.0 - smoothstep(rbreit * 0.4, rbreit, abs(aq - spur + wackel));
		float rilletief = rille * mix(0.3, 1.0, austritt);
		vec3 e = erde * mix(0.88, 1.12, schollen) * (0.9 + 0.2 * koernung);
		e *= 1.0 - 0.30 * rilletief;
		// zwischen den Rillen wächst Gras, wenn der Weg wenig befahren ist
		float mitte = (1.0 - smoothstep(0.0, spur * 0.7, aq)) * (1.0 - austritt) * smoothstep(0.35, 0.65, flecken + 0.25 * koernung);
		vec3 g = gras * mix(0.8, 1.15, schollen) * mix(0.75, 1.0, gruen);
		vec3 f = mix(e, g, mitte * 0.8);
		float rand = smoothstep(0.62, 0.92, aq + (kante - 0.5) * 0.3);
		f = mix(f, g, rand);
		float a = 1.0 - smoothstep(0.84, 1.0, aq + (kante - 0.5) * 0.3);
		farbe += f * w_fa;
		deck += a * w_fa;
	}

	// ── Kies: helle Körnung, bei schlechtem Zustand kommt die Erde durch ──
	{
		float k1 = rausch(p * 14.0);
		float k2 = rausch(p * 31.0 + 7.0);
		vec3 k = kies * mix(0.82, 1.14, k1) * mix(0.9, 1.1, k2);
		k = mix(k, k * 0.7, step(0.86, k2) * 0.8);
		float verfall = (1.0 - zustand) * smoothstep(0.30, 0.65, flecken + 0.4 * (schollen - 0.5));
		vec3 e = erde * mix(0.85, 1.1, schollen);
		vec3 g = gras * mix(0.8, 1.1, schollen) * mix(0.75, 1.0, gruen);
		vec3 f = mix(k, mix(e, g, step(0.5, koernung) * (1.0 - zustand)), clamp(verfall * 1.3, 0.0, 1.0));
		// am Rand liegt der Kies lockerer und dünner
		float rand = smoothstep(0.78, 1.0, aq + (kante - 0.5) * 0.3);
		f = mix(f, mix(k, e, 0.5), rand * 0.6);
		float a = 1.0 - smoothstep(0.88, 1.0, aq + (kante - 0.5) * 0.25);
		farbe += f * w_ki;
		deck += a * w_ki;
		rau = mix(rau, 0.95, w_ki);
	}

	// ── Pflaster: Reihen versetzter Steine mit Fugen ──
	{
		float zelle = 0.34;
		vec2 c = vec2(UV.x / zelle, q / zelle);
		float reihe = floor(c.y);
		c.x += mod(reihe, 2.0) * 0.5 + hash(vec2(reihe, 3.0)) * 0.3;
		vec2 id = vec2(floor(c.x), reihe);
		vec2 f = fract(c);
		float rund = max(abs(f.x - 0.5), abs(f.y - 0.5));
		float fuge = smoothstep(0.40, 0.47, rund + (hash(id + 11.0) - 0.5) * 0.04);
		float ton = hash(id);
		vec3 s = stein * mix(0.72, 1.18, ton) * mix(0.95, 1.05, rausch(p * 20.0));
		s = mix(s, s * vec3(1.08, 0.98, 0.88), step(0.7, hash(id + 5.0)) * 0.6);
		vec3 mortel = erde * 0.55;
		vec3 pfl = mix(s, mortel, fuge * fern);
		pfl = mix(pfl, stein * 0.9, (1.0 - fern) * 0.9);
		// verfallen: einzelne Steine fehlen, die Erde und Gras kommen durch, Fugen wachsen zu
		float fehlt = step(zustand + 0.35, hash(id + 23.0)) * step(zustand, 0.99);
		vec3 g = gras * mix(0.8, 1.1, schollen) * mix(0.75, 1.0, gruen);
		vec3 loch = mix(erde * 0.8, g, step(0.45, hash(id + 31.0)));
		pfl = mix(pfl, loch, fehlt);
		pfl = mix(pfl, g, fuge * (1.0 - zustand) * 0.55 * fern);
		float rand = smoothstep(0.90, 1.0, aq + (kante - 0.5) * 0.08);
		pfl = mix(pfl, stein * 0.7, rand);
		float a = 1.0 - smoothstep(0.96, 1.0, aq);
		farbe += pfl * w_pf;
		deck += a * w_pf;
		rau = mix(rau, 0.9, w_pf);
	}

	// ── Baustelle: aufgewühlte Erde, Kies- und Steinhaufen ──
	if (baustelle > 0.5) {
		float hu = rausch(p * 2.3);
		vec3 e = erde * vec3(0.78, 0.70, 0.62) * mix(0.7, 1.15, hu) * (0.9 + 0.2 * koernung);
		float haufen = smoothstep(0.62, 0.78, rausch(p * 0.9 + 4.0));
		e = mix(e, kies * mix(0.7, 1.0, rausch(p * 16.0)), haufen * 0.85);
		float stn = step(0.9, rausch(p * 6.0 + 9.0));
		e = mix(e, stein * 0.8, stn);
		float furche = smoothstep(0.35, 0.5, abs(fract(UV.x * 0.5 + rausch(vec2(0.0, q)) * 0.3) - 0.5));
		e *= 0.88 + 0.12 * furche;
		farbe = e;
		deck = 1.0 - smoothstep(0.9, 1.0, aq + (kante - 0.5) * 0.2);
		rau = 1.0;
	}

	ALBEDO = farbe;
	ROUGHNESS = rau;
	ALPHA = clamp(deck, 0.0, 1.0);
}
