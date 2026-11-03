// Pixel-Look (Grafikoption, Nutzer 03.11.2026): ein Durchgang über das FERTIGE UNDURCHSICHTIGE Bild der Welt im Low-Res-SubViewport (Stadtansicht.Pixel.cs).
// Ein Vollbild-Viereck vor der Kamera mit der kleinsten Zeichenreihenfolge der durchsichtigen Teile: Wasser, Feuer, Rauch, Regen und Nebel werden danach
// darüber gezeichnet und behalten ihre Original-Behandlung. Der Himmel (Tiefe 0) wird nicht angefasst. Farben bleiben Original (kein Farbstich), nur die
// Helligkeit wird skaliert. Die Bildpunkte sind Weltpixel (1/N der Fensterauflösung); das Raster der Texel ergibt sich aus dem Low-Res-Bild selbst.
//   1. Stufenlicht: Die Stufen kommen aus dem LICHT, nicht aus der Endhelligkeit: ein zweiter Durchgang (SubViewport, Debug-Zeichnung „Beleuchtung“,
//      weiße Oberflächen) liefert nur die Beleuchtung L. Sie wird in Stufen gesetzt; das Bild wird mit Lstufe / L skaliert (gleicher Farbton). Damit bleibt
//      ein dunkles Dach (dunkle Farbe, volles Licht) dunkel und wird nicht zur Schattenstufe.
//   2. Weiche Schattenkante: an Kanten des Beleuchtungsbildes wird über gleichtiefe Nachbarn gemittelt und feiner gestuft.
//   3. Umrisse (wie t3ssel8r): dunkle Außenkontur auf der NÄHEREN Seite eines Tiefensprungs (1 Weltpixel, Eigenfarbe abgedunkelt), dünne Teile (Zaun,
//      Gerüst) bekommen nur einen halben Strich; helle Innenkanten an KONVEXEN Normalenknicken (Dachfirst, Hausecke).
//   4. Tiefendunst: Ferne hinter der Brennebene der Kamera wird exponentiell heller und blasser (Luftperspektive).
//   5. Kontrast um die Mitte (nach Helligkeit, ohne Farbstich) und Düsternis (gleichmäßiger Faktor auf alle Farben).
// Alle Werte außer den Kameradaten und der Lichtskala stehen fest (Voreinstellung „Pixel 2 fein“). Eigenbau (MIT).
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, skip_vertex_transform, blend_mix;

uniform sampler2D tiefe : hint_depth_texture, filter_nearest;
uniform sampler2D normalen : hint_normal_roughness_texture, filter_nearest;
uniform sampler2D bild : hint_screen_texture, filter_nearest, repeat_disable;
uniform sampler2D licht : filter_nearest, repeat_disable;   // Beleuchtung allein (SubViewport), vom Skript gesetzt

uniform float ortho_groesse = 0.0;     // vom Skript: sichtbare Höhe in Metern bei orthografischer Kamera, sonst 0
uniform float fov_faktor = 1.0;        // vom Skript: 2 * tan(Sichtfeld / 2) bei perspektivischer Kamera
uniform float lichtskala = 1.0;        // vom Skript: Dämmerung und Nacht, alle Lichtstufen so viel dunkler
uniform float tag = 1.0;               // vom Skript: 1 = Tag, 0 = Nacht; Kontrast und Düsternis wirken nachts nicht (sonst säuft das Bild ab)
uniform float dunst_bezug = 150.0;     // vom Skript: Entfernung der Brennebene (Kamera zum Blickpunkt)
uniform vec3 dunst_farbe = vec3(0.55, 0.6, 0.68);   // vom Skript: bei Nacht dunkel (linear)

const int STUFEN = 3;
const float LICHT_DUNKEL = 0.25;       // Beleuchtung der untersten Stufe (Schatten)
const float LICHT_HELL = 0.95;         // Beleuchtung der obersten Stufe (volles Sonnenlicht)
const float DUESTER = 0.12;            // Düsternis: Anteil, um den alles dunkler wird
const float UMRISS_DUNKEL = 0.4;       // Außenkontur: Eigenfarbe mal diesem Faktor
const float UMRISS_HELL = 0.2;         // Innenkante: Aufhellung an konvexen Knicken
const float UMRISS_TIEFE = 0.012;      // Tiefensprung als Anteil der sichtbaren Höhe ...
const float UMRISS_TIEFE_MIN = 0.55;   // ... mindestens so viele Meter (Gras und Halme bekommen keine Kontur)
const float UMRISS_KNICK = 0.6;        // Innenkante, wenn das Skalarprodukt der Normalen darunter liegt
const float DUNST_DICHTE = 0.0045;     // je Meter hinter der Brennebene
const float DUNST_MAX = 0.42;
const float KONTRAST = 1.12;

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
	// weiche Stufen: die Stufe wächst in der oberen Hälfte jedes Abschnitts glatt an (harte Sprünge machten um Laternen Ringe)
	float v = x * n;
	float k = min(floor(v) + smoothstep(0.5, 1.0, fract(v)), n - 1.0);
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
	vec3 c = textureLod(bild, uv, 0.0).rgb;

	vec4 vp = INV_PROJECTION_MATRIX * vec4(uv * 2.0 - 1.0, d, 1.0);
	vp.xyz /= vp.w;
	float z0 = linear_tief(d, INV_PROJECTION_MATRIX, uv);
	float bezug = ortho ? ortho_groesse : z0;
	vec2 px = 1.0 / VIEWPORT_SIZE;
	float uschwelle = max(UMRISS_TIEFE * bezug, ortho ? UMRISS_TIEFE_MIN : 0.0);

	// 1. und 2. Stufenlicht aus der Beleuchtung, an Schattenkanten weich (Radius 1 Weltpixel)
	{
		float l = licht_l(uv);
		float n = float(STUFEN);
		float dk = LICHT_DUNKEL * lichtskala, hl = LICHT_HELL * lichtskala;
		float lq = stufe_von(l, n, dk, hl);
		float sum = l, cnt = 1.0, lmin = l, lmax = l;
		for (int i = 0; i < 8; i++) {
			float a = float(i) * 0.7853982;
			vec2 u = uv + vec2(cos(a), sin(a)) * px;
			float dn = texture(tiefe, u).x;
			if (dn <= 0.000001) continue;
			if (abs(linear_tief(dn, INV_PROJECTION_MATRIX, u) - z0) > uschwelle) continue;
			float ln = licht_l(u);
			sum += ln; cnt += 1.0; lmin = min(lmin, ln); lmax = max(lmax, ln);
		}
		if (lmax - lmin > 0.18 * hl) lq = stufe_von(sum / cnt, n * 2.0 - 1.0, dk, hl);
		float f = lq / max(l, 0.04 * lichtskala);
		f = min(f, mix(1.8, 1.0, smoothstep(0.5, 0.9, lum(c))));   // leuchtende Flächen (Fenster, Feuer) nicht hochziehen
		f = mix(f, 1.0, smoothstep(hl, hl * 1.6, l));   // heller als volles Sonnenlicht: Punktlichter (Laterne, Fackel) fallen ungestuft und weich ab
		c *= f;
	}

	// 3. Umrisse im Low-Res-Bild
	{
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
				if (dot(n0, n) < UMRISS_KNICK) {
					vec3 dp = ansichtsort(dn, INV_PROJECTION_MATRIX, u) - p0;
					if (dot(dp, n0) < -0.004 * bezug * 0.05) innen = 1.0;   // Nachbar liegt hinter der Fläche: konvex
				}
			}
		}
		if (aussen > 0.0) c *= mix(1.0, UMRISS_DUNKEL, aussen);
		else if (innen > 0.0) c *= 1.0 + UMRISS_HELL;
	}

	// 4. Tiefendunst (Luftperspektive): hinter der Brennebene heller und blasser
	{
		// Die Dichte ist auf Zoom 40 abgestimmt (Versuch); weiter herausgezoomt liegt mehr Tiefe im Bild, darum dünner
		float dichte = DUNST_DICHTE * (ortho ? clamp(40.0 / ortho_groesse, 0.3, 1.5) : 1.0);
		float t = clamp(1.0 - exp(-dichte * max(z0 - dunst_bezug, 0.0)), 0.0, DUNST_MAX);
		c = mix(c, dunst_farbe, t);
	}

	// 5. Kontrast um die Mitte (nach Helligkeit) und Düsternis
	float lc = max(lum(c), 0.0005);
	c *= clamp(pow(lc / 0.18, (KONTRAST - 1.0) * tag), 0.6, 1.5);
	c *= 1.0 - DUESTER * tag;
	ALBEDO = max(c, vec3(0.0));
	ALPHA = 1.0;
}
