// Holzschnitt-Look für alle Modelle (Grafikoption, Einbau siehe docs/bereiche/darstellung.md, „Holzschnitt“).
// Kolorierter Holzschnitt: Farbe mit Papierkorn und leicht versetzter Deckung, Materialstruktur als geschnittene Linien,
// Schatten in zwei Tönen mit Schraffur. Alle Muster werden dreiseitig in Weltkoordinaten projiziert – die Modelle
// brauchen keine UV-Abwicklung. Der schwarze Umriss kommt aus holzschnitt_umriss.gdshader (eigenes Bild-Quad).
shader_type spatial;
render_mode ambient_light_disabled, specular_disabled;

// Grundfarbe: aus dem alten Material (farbe) oder aus der Eckenfarbe der GLB (Material „Farben“ und „Figur“).
uniform vec4 farbe : source_color = vec4(1.0);
uniform bool eckenfarbe = false;
// Materialart (materialarten.json): 0 keine, 1 Holz, 2 Bretter, 3 Mauer, 4 Fels, 5 Ziegel, 6 Stroh, 7 Schiefer, 8 Laub, 9 Boden.
// art_aus_uv: je Fläche aus UV.y (Material „Farben“ und „Figur“), sonst der feste Wert art.
uniform int art = 0;
uniform bool art_aus_uv = false;
// Figuren: Zonen aus UV.x (1 Gewand, 2 Beinkleid, 3 Haut, 4 Haar, 5 Kopfbedeckung) je Figur einfärben.
uniform bool zonen = false;
instance uniform vec4 gewand : source_color = vec4(0.30, 0.40, 0.55, 1.0);
instance uniform vec4 beinkleid : source_color = vec4(0.30, 0.26, 0.22, 1.0);
instance uniform vec4 haut : source_color = vec4(0.86, 0.68, 0.55, 1.0);
instance uniform vec4 haar : source_color = vec4(0.32, 0.22, 0.14, 1.0);
instance uniform vec4 kopfbedeckung : source_color = vec4(0.78, 0.74, 0.64, 1.0);

uniform sampler2D schraffur : repeat_enable, filter_linear_mipmap;
uniform sampler2D papier : repeat_enable, filter_linear_mipmap;
uniform sampler2D struktur_a : repeat_enable, filter_linear_mipmap;
uniform sampler2D struktur_b : repeat_enable, filter_linear_mipmap;

uniform vec3 papierfarbe : source_color = vec3(0.93, 0.89, 0.79);   // scheint durch, wo die Farbe dünn sitzt
// Grundhelligkeit ohne Sonne (Farbe × Stärke des Umgebungslichts), nachts wichtig. Für alle Materialien gleich: globaler
// Shader-Parameter, der in den Projekteinstellungen (Shader Globals, Typ vec3) angelegt sein muss und den das Spiel je Bild setzt.
global uniform vec3 holzschnitt_umgebung;
// Innenräume (Einbau ins Spiel): 0 in der Stadt, 1 im Raum. Dort steht die Kamera nah, das Umgebungslicht trägt das Bild und die Lichter
// sind schwach: Die Schraffur wird feiner, Schatten beginnen erst bei dunkleren Flächen, die Grundhelligkeit ist weniger schraffiert.
global uniform float holzschnitt_raum;

const float SCHRAFFUR_DICHTE = 0.22;        // Kacheln je Meter (16 Linien je Kachel → etwa 28 cm Abstand)
const float RAUM_DICHTE = 3.0;              // Im Raum so viel mal feiner schraffiert
const float RAUM_LICHT = 6.0;               // Im Raum zählen die Lichter so viel mal stärker für die Schattenstufen
const float RAUM_LINIE = 0.5;               // Im Raum lassen die Schraffurlinien so viel Licht durch (Stadt: 0,12)

varying vec3 welt;
varying vec3 welt_n;
varying float v_art;
varying float v_zone;
varying vec3 eckfarbe;

void vertex() {
	// Eckenfarben stehen als sRGB-Werte in der GLB: Der Compatibility-Renderer rechnet in sRGB, Forward+ linear (wie figur.gdshader)
	eckfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		eckfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	welt_n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_art = art_aus_uv ? UV.y : float(art);
	v_zone = UV.x;
}

vec4 dreiseitig(sampler2D t, vec3 p, vec3 n, float s) {
	vec3 w = pow(abs(n), vec3(8.0));
	w /= (w.x + w.y + w.z);
	return texture(t, p.zy * s) * w.x + texture(t, p.xz * s) * w.y + texture(t, p.xy * s) * w.z;
}

// Linien der Materialstruktur (1 = keine Linie, 0 = schwarz) und wie kräftig sie gedruckt werden.
float struktur(int a, vec3 p, vec3 n) {
	if (a == 1) return mix(0.50, 1.0, dreiseitig(struktur_a, p, n, 0.45).r);
	if (a == 2) return mix(0.35, 1.0, dreiseitig(struktur_a, p, n, 0.5).g);
	if (a == 3) return mix(0.30, 1.0, dreiseitig(struktur_a, p, n, 0.45).b);
	if (a == 4) return mix(0.40, 1.0, dreiseitig(struktur_a, p, n, 0.3).a);
	if (a == 5) return mix(0.35, 1.0, dreiseitig(struktur_b, p, n, 0.9).r);
	if (a == 6) return mix(0.45, 1.0, dreiseitig(struktur_b, p, n, 0.5).g);
	if (a == 7) return mix(0.35, 1.0, dreiseitig(struktur_b, p, n, 0.7).b);
	if (a == 8) return mix(0.40, 1.0, dreiseitig(struktur_b, p, n, 0.6).a);
	if (a == 9) return mix(0.55, 1.0, dreiseitig(papier, p, n, 0.35).b);
	return 1.0;
}

void fragment() {
	vec3 grund = eckenfarbe ? eckfarbe : farbe.rgb;
	if (zonen) {
		int z = int(round(v_zone));
		if (z == 1) grund = gewand.rgb;
		else if (z == 2) grund = beinkleid.rgb;
		else if (z == 3) grund = haut.rgb;
		else if (z == 4) grund = haar.rgb;
		else if (z == 5) grund = kopfbedeckung.rgb;
	}
	// Von Hand aufgetragene Farbe: Flecken, an denen sie dünner sitzt, gegen die Linien leicht verschoben
	vec4 pap = dreiseitig(papier, welt + vec3(0.21, 0.0, 0.13), welt_n, 0.2);
	float deckung = smoothstep(0.30, 0.62, pap.g);
	vec3 c = mix(mix(grund, papierfarbe, 0.09), grund, deckung) * (0.95 + 0.05 * pap.r);
	c *= struktur(int(round(v_art)), welt, welt_n);
	ALBEDO = c;
	// Grundhelligkeit ohne Sonne (statt des abgeschalteten Umgebungslichts), mit leichter Schraffur
	vec4 h = dreiseitig(schraffur, welt, welt_n, SCHRAFFUR_DICHTE * mix(1.0, RAUM_DICHTE, holzschnitt_raum));
	EMISSION = c * holzschnitt_umgebung * mix(mix(0.55, 0.8, holzschnitt_raum), 1.0, h.r);
}

void light() {
	float anteil = clamp(dot(NORMAL, LIGHT), 0.0, 1.0) * ATTENUATION;
	float hell = anteil * mix(1.0, RAUM_LICHT, holzschnitt_raum);
	vec4 h = dreiseitig(schraffur, welt, welt_n, SCHRAFFUR_DICHTE * mix(1.0, RAUM_DICHTE, holzschnitt_raum));
	float linie = mix(0.12, RAUM_LINIE, holzschnitt_raum);   // wie schwarz die Schraffurlinien sind (im Raum zarter)
	float tusche = 1.0;
	if (hell < 0.50) tusche *= mix(linie, 1.0, h.r);   // Halbschatten: einfache Schraffur
	if (hell < 0.22) tusche *= mix(linie, 1.0, h.g);   // Schatten: Kreuzschraffur
	if (hell < 0.06) tusche *= mix(0.25, 1.0, h.b);    // tiefer Schatten: dichter
	float ton = mix(0.62, 1.0, step(0.50, hell));    // nur zwei Farbtöne wie beim Druck
	// Die Sonne der Stadt gibt überall gleich viel; die Lichter im Raum (Glut, Fenster) sind stark und nehmen mit Abstand und Winkel ab
	DIFFUSE_LIGHT += LIGHT_COLOR * ton * tusche * 0.42 * mix(1.0, clamp(anteil * 2.0, 0.0, 1.0), holzschnitt_raum);
}
