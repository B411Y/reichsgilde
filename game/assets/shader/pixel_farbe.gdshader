// Pixel-Look (Testszene Pixeltest), Runde 3: ein Durchgang über das FERTIGE UNDURCHSICHTIGE Bild (Himmel, Boden, Häuser, Figuren) in voller Auflösung.
// Ein Vollbild-Viereck vor der Kamera mit der kleinsten Zeichenreihenfolge der durchsichtigen Teile: Wasser, Feuer und Nebel werden danach darüber
// gezeichnet und behalten ihre Original-Behandlung. Der Himmel (Tiefe 0) wird nicht angefasst. Farben bleiben Original (kein Farbstich), nur Helligkeit wird skaliert.
// Bausteine (einzeln schaltbar):
//   1. Texel: Die Oberfläche wird an einem Weltraster gleich großer Texel abgetastet (je Fläche in ihrer Ebene, Mitte des Texels), Nearest, ohne
//      Farbveränderung. Am Rand von Flächen (Tiefe/Normale passen am Texelmittelpunkt nicht) bleibt der eigene Bildpunkt.
//   2. Stufenlicht: Die Stufen kommen aus dem LICHT, nicht aus der Endhelligkeit: ein zweiter Durchgang (SubViewport, Debug-Zeichnung „Beleuchtung“,
//      weiße Oberflächen) liefert nur die Beleuchtung L. Die Beleuchtung wird in Stufen gesetzt; das Bild wird mit Lstufe / L skaliert (gleicher Farbton).
//      Damit bleibt ein dunkles Dach (dunkle Farbe, volles Licht) dunkel und wird nicht zur Schattenstufe.
//   3. Kantenlicht: schwache Aufhellung (Faktor) an Tiefen- und Normalensprüngen auf der Sonnenseite und nach oben.
//   4. Düsternis: gleichmäßiger Faktor auf alle Farben (keine Verschiebung des Farbtons).
// Runde 4 (Feinschliff auf Weg A, alles einzeln schaltbar; der Shader läuft im Low-Res-SubViewport, die Bildpunkte sind Weltpixel):
//   5. Umrisse (wie t3ssel8r): dunkle Außenkontur auf der NÄHEREN Seite eines Tiefensprungs (1 Weltpixel, Farbe = abgedunkelte Eigenfarbe), dünne Teile
//      (Zaun, Gerüst: auf der Gegenseite im Abstand 2 wieder tief) bekommen nur einen halben Strich; helle Innenkanten an KONVEXEN Normalenknicken
//      (Dachfirst, Hausecke; konvex = Nachbar liegt hinter der Fläche).
//   6. Tiefendunst: Ferne hinter der Brennebene der Kamera wird exponentiell heller und blasser (Luftperspektive).
//   7. Kontrast um die Mitte (nach Helligkeit, ohne Farbstich).
//   8. Schattenkante weich gestuft: an Kanten des Beleuchtungsbildes wird über gleichtiefe Nachbarn gemittelt und feiner gestuft (1–2 Zwischenstufen).
// Eigenbau (MIT).
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, skip_vertex_transform, blend_mix;

uniform sampler2D bild : hint_screen_texture, filter_nearest, repeat_disable;
uniform sampler2D tiefe : hint_depth_texture, filter_nearest;
uniform sampler2D normalen : hint_normal_roughness_texture, filter_nearest;
uniform sampler2D licht : filter_nearest, repeat_disable;   // Beleuchtung allein (SubViewport), vom Skript gesetzt

uniform float ortho_groesse = 0.0;     // vom Skript: sichtbare Höhe in Metern bei orthografischer Kamera, sonst 0
uniform float fov_faktor = 1.0;        // vom Skript: 2 * tan(Sichtfeld / 2) bei perspektivischer Kamera
uniform bool texel_an = true;
uniform bool stufen_an = true;
uniform int stufen = 3;                // Lichtstufen
uniform float licht_dunkel = 0.25;     // Beleuchtung der untersten Stufe (Schatten); darunter wird sie angehoben
uniform float licht_hell = 0.95;       // Beleuchtung der obersten Stufe (volles Sonnenlicht)
uniform float stufen_staerke = 1.0;    // 0 = Beleuchtung unverändert, 1 = ganz in Stufen
uniform float duester = 0.12;          // Düsternis: Anteil, um den alles dunkler wird
uniform vec3 sonne_richtung = vec3(0.4, -0.7, 0.5); // Richtung, in die das Licht fällt (Welt)
uniform float blende_von = 1.25;       // Texel unter dieser Größe (Bildpunkte): keine Rasterung
uniform float texel_je_meter = 16.0;
uniform bool texel_auto = false;       // orthografisch: Texelgröße = texel_ziel_px Bildpunkte (sonst fest texel_je_meter)
uniform float texel_ziel_px = 3.0;
uniform bool zweierstufen = false;     // Texelgröße nur in Zweierpotenzen (kein Wandern beim Zoomen, Größe schwankt dafür um ~40 %)
uniform bool zeige_licht = false;     // Prüfbild: nur das Beleuchtungsbild zeigen
uniform bool kante_an = true;
uniform float kante_hell = 0.08;       // schwache Aufhellung an Kanten zur Sonne / nach oben
uniform float tiefenschwelle = 0.08;   // perspektivisch: Anteil der Entfernung
uniform float tiefenschwelle_ortho = 0.02; // orthografisch: Anteil der sichtbaren Höhe
uniform float normalenschwelle = 0.9;

// Runde 4
uniform bool umriss_an = false;
uniform float umriss_dunkel = 0.4;     // Außenkontur: Eigenfarbe mal diesem Faktor
uniform float umriss_hell = 0.2;      // Innenkante: Aufhellung an konvexen Knicken
uniform float umriss_tiefe = 0.012;    // Tiefensprung als Anteil der sichtbaren Höhe ...
uniform float umriss_tiefe_min = 0.55; // ... mindestens so viele Meter (Gras und Halme sollen keine Kontur bekommen)
uniform float umriss_knick = 0.6;      // Innenkante, wenn das Skalarprodukt der Normalen darunter liegt
uniform bool dunst_an = false;
uniform float dunst_dichte = 0.0045;   // je Meter hinter der Brennebene
uniform float dunst_max = 0.42;
uniform vec3 dunst_farbe = vec3(0.55, 0.6, 0.68);   // linear
uniform float dunst_bezug = 150.0;     // Entfernung der Brennebene (Kamera zum Blickpunkt)
uniform float kontrast = 1.0;
uniform int schatten_weich = 0;        // 0 hart, 1 weich (Radius 1), 2 weicher (Radius 2)
uniform float lichtskala = 1.0;        // Abend: alle Lichtstufen so viel dunkler

float lum(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

float linear_tief(float d, mat4 inv_proj, vec2 uv) {
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return -v.z / v.w;
}

vec3 ansichtsort(float d, mat4 inv_proj, vec2 uv) {
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return v.xyz / v.w;
}

float licht_l(vec2 u) {
	return lum(pow(max(texture(licht, u).rgb, vec3(0.0)), vec3(2.2)));
}

float stufe_von(float l, float n, float dunkel, float hell) {
	float x = clamp((l - dunkel * 0.5) / max(hell - dunkel * 0.5, 0.01), 0.0, 1.0);
	float k = min(floor(x * n), n - 1.0);
	return mix(dunkel, hell, k / (n - 1.0));
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float d = texture(tiefe, uv).x;
	if (d <= 0.000001) { discard; }   // Himmel: nichts anfassen
	bool ortho = ortho_groesse > 0.0;
	vec3 n0 = texture(normalen, uv).xyz * 2.0 - 1.0;

	// Weltposition und Texelgröße
	vec4 vp = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, d, 1.0);
	vp.xyz /= vp.w;
	vec3 wp = (INV_VIEW_MATRIX * vec4(vp.xyz, 1.0)).xyz;
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(n0, 0.0)).xyz);
	float mpp = ortho ? ortho_groesse / VIEWPORT_SIZE.y : -vp.z * fov_faktor / VIEWPORT_SIZE.y;   // Meter je Bildpunkt
	float texel = 1.0 / texel_je_meter;
	if (texel_auto) {
		texel = texel_ziel_px * mpp;
		if (zweierstufen) texel = exp2(round(log2(texel)));
	}
	float px_je_texel = texel / mpp;

	// 1. Texel: Abtastpunkt = Mitte des Weltrasters in der Ebene der Fläche
	vec2 uvs = uv;
	if (texel_an && px_je_texel >= blende_von && abs(wn.y) + abs(wn.x) + abs(wn.z) > 0.0) {
		vec3 an = abs(wn);
		vec3 q = wp;
		if (an.y >= an.x && an.y >= an.z) {
			q.xz = (floor(wp.xz / texel) + 0.5) * texel;
			q.y = wp.y - (wn.x * (q.x - wp.x) + wn.z * (q.z - wp.z)) / wn.y;
		} else if (an.x >= an.z) {
			q.zy = (floor(wp.zy / texel) + 0.5) * texel;
			q.x = wp.x - (wn.z * (q.z - wp.z) + wn.y * (q.y - wp.y)) / wn.x;
		} else {
			q.xy = (floor(wp.xy / texel) + 0.5) * texel;
			q.z = wp.z - (wn.x * (q.x - wp.x) + wn.y * (q.y - wp.y)) / wn.z;
		}
		vec4 qv = VIEW_MATRIX * vec4(q, 1.0);
		vec4 qc = PROJECTION_MATRIX * qv;
		vec2 u2 = (qc.xy / qc.w) * 0.5 + 0.5;
		if (u2.x > 0.0 && u2.x < 1.0 && u2.y > 0.0 && u2.y < 1.0) {
			float d2 = texture(tiefe, u2).x;
			if (d2 > 0.000001) {
				float z2 = linear_tief(d2, INV_PROJECTION_MATRIX, u2);
				vec3 n2 = texture(normalen, u2).xyz * 2.0 - 1.0;
				if (abs(z2 + qv.z) < texel * 1.5 + 0.03 && dot(n0, n2) > 0.85) uvs = u2;
			}
		}
	}

	vec3 c = textureLod(bild, uvs, 0.0).rgb;
	if (zeige_licht) { ALBEDO = pow(texture(licht, uv).rgb, vec3(2.2)); ALPHA = 1.0; }
	else {

	// 2. Stufenlicht aus der Beleuchtung
	float z0 = linear_tief(d, INV_PROJECTION_MATRIX, uv);
	float bezug = ortho ? ortho_groesse : z0;
	vec2 px = 1.0 / VIEWPORT_SIZE;
	float uschwelle = max(umriss_tiefe * bezug, ortho ? umriss_tiefe_min : 0.0);
	if (stufen_an) {
		float l = licht_l(uvs);
		float n = float(max(stufen, 2));
		float dk = licht_dunkel * lichtskala, hl = licht_hell * lichtskala;
		float lq = stufe_von(l, n, dk, hl);
		if (schatten_weich > 0) {
			// Mittel über gleichtiefe Nachbarn (Radius 1 oder 2 Weltpixel); nur an Kanten des Beleuchtungsbildes feiner gestuft
			float r = float(schatten_weich);
			float sum = l, cnt = 1.0, lmin = l, lmax = l;
			for (int i = 0; i < 8; i++) {
				float a = float(i) * 0.7853982;
				vec2 u = uvs + vec2(cos(a), sin(a)) * px * r;
				float dn = texture(tiefe, u).x;
				if (dn <= 0.000001) continue;
				if (abs(linear_tief(dn, INV_PROJECTION_MATRIX, u) - z0) > uschwelle) continue;
				float ln = licht_l(u);
				sum += ln; cnt += 1.0; lmin = min(lmin, ln); lmax = max(lmax, ln);
			}
			if (lmax - lmin > 0.18 * hl) lq = stufe_von(sum / cnt, n * 2.0 - 1.0, dk, hl);
		}
		lq = mix(l, lq, stufen_staerke);
		float f = lq / max(l, 0.04);
		f = min(f, mix(1.8, 1.0, smoothstep(0.5, 0.9, lum(c))));   // leuchtende Flächen (Fenster, Feuer) nicht hochziehen
		c *= f;
	}

	// 3. Kantenlicht (Faktor, kein Farbstich); mit „Umrisse“ übernehmen diese die Kanten
	if (kante_an && !umriss_an) {
		float schwelle = (ortho ? tiefenschwelle_ortho : tiefenschwelle) * bezug;
		vec2 rich[4] = vec2[](vec2(1, 0), vec2(-1, 0), vec2(0, 1), vec2(0, -1));
		float kante = 0.0;
		for (int i = 0; i < 4; i++) {
			vec2 u = uv + rich[i] * px;
			float dn = texture(tiefe, u).x;
			if (dn <= 0.000001) { kante = 1.0; continue; }
			float z = linear_tief(dn, INV_PROJECTION_MATRIX, u);
			vec3 n = texture(normalen, u).xyz * 2.0 - 1.0;
			if (z0 - z > schwelle) kante = 1.0;
			if (1.0 - dot(n0, n) > normalenschwelle && abs(z0 - z) < schwelle * 3.0) kante = 1.0;
		}
		float zur_sonne = dot(wn, -normalize(sonne_richtung));
		if (kante > 0.0 && (zur_sonne > 0.15 || wn.y > 0.6)) c *= 1.0 + kante_hell;
	}

	// 3b. Umrisse im Low-Res-Bild
	if (umriss_an) {
		vec2 rich[4] = vec2[](vec2(1, 0), vec2(-1, 0), vec2(0, 1), vec2(0, -1));
		vec3 p0 = vp.xyz;
		float aussen = 0.0, innen = 0.0;
		for (int i = 0; i < 4; i++) {
			vec2 u = uv + rich[i] * px;
			float dn = texture(tiefe, u).x;
			bool fern;
			float z = 0.0;
			if (dn <= 0.000001) fern = true;
			else { z = linear_tief(dn, INV_PROJECTION_MATRIX, u); fern = z - z0 > uschwelle; }
			if (fern) {
				// dünnes Teil? Gegenseite im Abstand 2 ebenfalls tief oder Himmel: nur halber Strich
				vec2 u2 = uv - rich[i] * px * 2.0;
				float d2 = texture(tiefe, u2).x;
				bool dunn = d2 <= 0.000001 || linear_tief(d2, INV_PROJECTION_MATRIX, u2) - z0 > uschwelle;
				aussen = max(aussen, dunn ? 0.45 : 1.0);
			} else if (abs(z - z0) < uschwelle) {
				vec3 n = texture(normalen, u).xyz * 2.0 - 1.0;
				if (dot(n0, n) < umriss_knick) {
					vec3 dp = ansichtsort(dn, INV_PROJECTION_MATRIX, u) - p0;
					if (dot(dp, n0) < -0.004 * bezug * 0.05) innen = 1.0;   // Nachbar liegt hinter der Fläche: konvex
				}
			}
		}
		if (aussen > 0.0) c *= mix(1.0, umriss_dunkel, aussen);
		else if (innen > 0.0) c *= 1.0 + umriss_hell;
	}

	// 3c. Tiefendunst (Luftperspektive): hinter der Brennebene heller und blasser
	if (dunst_an) {
		float t = clamp(1.0 - exp(-dunst_dichte * max(z0 - dunst_bezug, 0.0)), 0.0, dunst_max);
		c = mix(c, dunst_farbe, t);
	}

	// 3d. Kontrast um die Mitte (nach Helligkeit)
	if (kontrast != 1.0) {
		float lc = max(lum(c), 0.0005);
		c *= clamp(pow(lc / 0.18, kontrast - 1.0), 0.6, 1.5);
	}

	// 4. Düsternis
	c *= 1.0 - duester;
	ALBEDO = max(c, vec3(0.0));
	ALPHA = 1.0;
	}
}
