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

float lum(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

float linear_tief(float d, mat4 inv_proj, vec2 uv) {
	vec4 v = inv_proj * vec4(uv * 2.0 - 1.0, d, 1.0);
	return -v.z / v.w;
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
	if (stufen_an) {
		vec3 lr = texture(licht, uvs).rgb;
		float l = lum(pow(max(lr, vec3(0.0)), vec3(2.2)));   // Beleuchtungsbild ist sRGB-kodiert
		float n = float(max(stufen, 2));
		float x = clamp((l - licht_dunkel * 0.5) / max(licht_hell - licht_dunkel * 0.5, 0.01), 0.0, 1.0);
		float k = min(floor(x * n), n - 1.0);
		float lq = mix(licht_dunkel, licht_hell, k / (n - 1.0));
		lq = mix(l, lq, stufen_staerke);
		c *= lq / max(l, 0.04);
	}

	// 3. Kantenlicht (Faktor, kein Farbstich)
	if (kante_an) {
		float z0 = linear_tief(d, INV_PROJECTION_MATRIX, uv);
		float bezug = ortho ? ortho_groesse : z0;
		float schwelle = (ortho ? tiefenschwelle_ortho : tiefenschwelle) * bezug;
		vec2 px = 1.0 / VIEWPORT_SIZE;
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

	// 4. Düsternis
	c *= 1.0 - duester;
	ALBEDO = max(c, vec3(0.0));
	ALPHA = 1.0;
	}
}
