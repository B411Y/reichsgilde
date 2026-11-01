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
// Alle Muster liegen in Weltkoordinaten (Felsstücke stehen still, das Muster schwimmt nicht). Jedes Stück hat seine eigene Lage der
// Bänder: Sie kippen bis etwa 6° und verschieben sich (Ort des Stücks als Zufall), damit nicht jeder Stein gleich aussieht; an der Grenze
// zweier Stücke springen sie deshalb wie zwischen zwei verrutschten Blöcken. Werden Stücke später zu einem Netz zusammengefasst, teilen
// sie sich diesen Zufall (bei MultiMesh enthält MODEL_MATRIX die Instanz, dort bleibt er je Stück).
// „Im Schatten“: Der Fragment-Shader weiß nicht, ob die Sonne ein Pixel erreicht (das rechnet Godot erst im Licht). Moos wächst
// deshalb dort, wo im Mittel des Tages Schatten ist: Nordseite (−z), Höhlungen der Struktur, Klüfte und unter Absätzen.

uniform sampler2D struktur : hint_default_white, repeat_enable, filter_linear_mipmap_anisotropic;
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

varying vec3 v_welt;
varying vec3 v_normale;
varying vec3 v_stueck;

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
	v_stueck = MODEL_MATRIX[3].xyz;
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
	float zufall = hash1(floor(v_stueck * 3.1) + 0.37);
	float relief = mix(1.5, 0.55, kantenrundung);

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
	nw = normalize(n + relief * (w.x * vec3(0.0, dx.y, dx.x) + w.y * vec3(dy.x, 0.0, dy.y) + w.z * vec3(dz.x, dz.y, 0.0)));
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
	vec3 t = oben_ ? vec3(d.x, 0.0, d.y) : (seiteX ? vec3(0.0, d.y, d.x) : vec3(d.x, d.y, 0.0));
	nw = normalize(n + relief * t);
#endif
	float h = s.b;                         // Höhe 0 tief … 1 hoch
	float fleck = s.a;

	// ── 2. Schichtbänder ──
	// je Stück kippen die Bänke bis etwa 6° und verschieben sich, die Grenze wellt sich mit der Struktur und einer langen Welle
	vec3 kipp = vec3(zufall - 0.5, 0.0, fract(zufall * 7.13) - 0.5) * 0.2;
	vec3 bn = normalize(band_normale + kipp);
	float welle = 0.25 * sin(dot(p.xz, vec2(0.61, 0.37)) + zufall * 6.28) + 0.15 * sin(dot(p, vec3(1.7, 0.3, -1.3)));
	float lage = dot(p, bn) / band_dicke + (h - 0.5) * 0.45 + welle + zufall * 3.0;
	float bank = floor(lage);
	float inBank = fract(lage);
	float bankHell = hash1(vec3(bank, zufall * 13.0, 2.0)) - 0.5;
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
		vec2 r1 = vec2(cos(zufall * 0.6 + 0.3), sin(zufall * 0.6 + 0.3));
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
	farbe *= 1.0 + (zufall - 0.5) * 0.12;
	float ao = 1.0 - 0.55 * fugeMaske - 0.6 * kluft - 0.35 * (1.0 - h) * (1.0 - h);
	float rau = rauheit;

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

	// ── 4. Moos ──
	float oben = smoothstep(0.25, 0.85, nw.y);
	float nord = clamp(-nw.z, 0.0, 1.0) * (1.0 - abs(nw.y));
	float mooswert = moos * (0.85 * oben + 0.45 * nord + 0.3 * (1.0 - h) + 0.35 * absatz * clamp(nw.y + 0.4, 0.0, 1.0) + 0.35 * kluftBreit);
	float moosMaske = smoothstep(0.42, 0.62, mooswert + (fleck - 0.5) * 1.2) * bewuchs;
	vec3 moosFarbe = mix(moos_farbe.rgb, moos_trocken.rgb, clamp(fleck * 0.9 - 0.35 + oben * 0.15 - nord * 0.4, 0.0, 0.7));
	moosFarbe *= 0.8 + 0.4 * h;

	// ── 5. Flechten ──
	float frei = clamp(0.75 + 0.35 * nw.z + 0.15 * nw.x, 0.0, 1.0) * (1.0 - 0.6 * oben) * (1.0 - kluftBreit);
	float flRausch = wertrauschen(p * 2.8 + zufall * 17.0) * 0.6 + wertrauschen(p * 9.0) * 0.4;
	float flSchwelle = 0.82 - 0.22 * flechten * frei;
	float flechteMaske = smoothstep(flSchwelle, flSchwelle + 0.04, flRausch * (0.6 + 0.4 * h)) * step(0.01, flechten) * (1.0 - moosMaske) * bewuchs;
	vec3 flechteFarbe = mix(flechte_hell.rgb, flechte_farbe.rgb, step(0.55, wertrauschen(p * 1.3 + 3.7)));

	// ── 6. Efeu in den Klüften ──
	float efeuMaske = 0.0;
	if (efeu > 0.0) {
		float steil = 1.0 - smoothstep(0.3, 0.7, abs(nw.y));
		float lage2 = wertrauschen(vec3(p.x * 0.7, p.y * 0.25, p.z * 0.7) + zufall * 9.0);
		efeuMaske = smoothstep(0.55, 0.7, kluftBreit * 0.7 + lage2 * efeu * 0.9 - 0.15 + (fleck - 0.5) * 0.4) * steil * bewuchs;
	}

	vec3 albedo = farbe;
	albedo = mix(albedo, flechteFarbe, flechteMaske);
	albedo = mix(albedo, moosFarbe, moosMaske);
	albedo = mix(albedo, efeu_farbe.rgb * (0.7 + 0.5 * fleck), efeuMaske);
	// Moos und Efeu glätten das Relief (Polster), Flechten liegen flach auf
	nw = normalize(mix(nw, n, max(moosMaske, efeuMaske) * 0.6));

	ALBEDO = albedo;
	ROUGHNESS = clamp(mix(rau, 1.0, max(max(moosMaske, efeuMaske), fugeMaske)), 0.0, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.35;
	AO = clamp(ao, 0.0, 1.0);
	AO_LIGHT_AFFECT = 0.35;
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);
}
