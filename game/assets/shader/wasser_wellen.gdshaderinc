// Wellen des Wassers (docs/wasser/phase3.md, Phase 3c): gemeinsam für die Wasserfläche (wasser_realistisch.gdshaderinc) und die Eisschollen
// (eis_scholle.gdshaderinc). Beide rechnen mit denselben Uniforms (Wasserarten.WellenSetzen belegt sie gleich), so schaukeln die Schollen genau
// mit der Fläche, auf der sie treiben.
//
// Gerstner-Wellen wie im „Realistic Water Shader“ von UnionBytes / Achim Menzel (AiYori), MIT – Herkunft und Lizenz im Kopf von
// wasser_realistisch.gdshaderinc. Neu: Flüsse rechnen in ihren Laufachsen (quer, Stelle), die Wellen folgen den Kurven; ein Startwert je
// Gewässer (aus der Karte) verteilt Abschnitte mit größeren, kleineren, schnelleren und langsameren Wellen; der Wind hebt die Wellen
// (wind_hoehe: Fluss ruhig 5–10 cm, im Sturm bis etwa 30 cm).

// Globale Werte des Spiels (project.godot, [shader_globals]); die Stadtansicht setzt sie je Bild
global uniform float wetter_wind;      // Windstärke 0–1 (0,25 leichter Wind, 1 Sturm)
global uniform vec2 wind_richtung;     // Weltrichtung des Windes (x, z)

uniform float wellen_staerke = 1.0;    // Faktor auf die Steilheit der drei Wellen (0 = glatt)
uniform float wellen_laenge = 1.0;     // Faktor auf die Wellenlänge (Meer lang, Teich kurz)
uniform float wave_speed = 0.5;
uniform vec4 wave_a = vec4(1.0, 0.4, 0.35, 3.0);     // xy = Richtung, z = Steilheit, w = Länge
uniform vec4 wave_b = vec4(-0.1, 0.6, 0.30, 1.55);
uniform vec4 wave_c = vec4(-1.0, -0.8, 0.25, 0.9);
// Zwei weitere, längere und kürzere Wellenzüge (Meer, See, Hafen, Fluss): xy = Richtung, z = Steilheit, w = Länge; wirken nur mit extra_wellen > 0
uniform vec4 wave_d = vec4(0.5, -1.0, 0.22, 6.3);
uniform vec4 wave_e = vec4(-0.7, 0.3, 0.16, 2.3);
uniform float extra_wellen = 0.0;      // Stärke der zwei zusätzlichen Wellenzüge (0 = aus)
uniform float wellen_vielfalt = 0.0;   // 0 = überall gleiche Wellen; 1 = Gruppen großer Wellen und ruhige Stellen (Stärke je Wellenzug wandert als Rauschen)
uniform float vielfalt_skala = 1.0;    // Faktor auf die Feinheit dieser Felder: kleiner = längere Abschnitte mit gleichen Wellen (Rhein 0,45)
uniform float wellen_saat = 0.0;       // Startwert je Gewässer (aus der Karte): Wo welche Abschnitte liegen
uniform float wind_hoehe = 0.0;        // Fluss: so viel höher (Faktor − 1) werden die Wellen von leichtem Wind bis Sturm; 0 = nur der Grundanteil
uniform float netz_abstand = 0.0;      // Abstand der Netzecken in m: Wellen kürzer als das 1,5- bis 3-Fache klingen ab (sonst zittern die Ecken); 0 = aus
uniform float wind_anteil = 0.5;       // wie stark der Weltwind Wellen, Richtung und Kräuselung beeinflusst (0 = gar nicht)
uniform float lauf_netz = 0.0;         // 1 = Netz eines Flusses: UV = (quer, Stelle) in m, UV2 = Strömung (Welt); die Wellen laufen in diesen Achsen

// Ganzzahl-Hash (wie wetter.gdshaderinc): die Sinus-Fassung zerfällt bei großen Koordinaten in sichtbare Blöcke
float w_hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p))) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	return float(n >> 8u) * (1.0 / 16777216.0);
}

vec2 w_hash2(vec2 p) {
	return vec2(w_hash(p), w_hash(p + vec2(57.0, 113.0)));
}

float w_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(w_hash(i), w_hash(i + vec2(1.0, 0.0)), f.x), mix(w_hash(i + vec2(0.0, 1.0)), w_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float w_fbm(vec2 p) {
	return w_rausch(p) * 0.55 + w_rausch(p * 2.13 + 7.1) * 0.3 + w_rausch(p * 4.7 + 3.3) * 0.15;
}

// Sturmanteil und Windrichtung (Welt, Länge = Stärke) aus dem Weltwind; ohne MIT_WIND kein Wind
void wellen_wind(out float sturm, out vec2 wind) {
	sturm = 0.0;
	wind = vec2(0.0);
#ifdef MIT_WIND
	sturm = wind_anteil * wetter_wind;
	wind = normalize(wind_richtung + vec2(0.0001)) * sturm * 0.7;
#endif
}

// Faktor auf die Wellenstärke durch den Wind: wie bisher etwas steiler (bis 60 % beim Meer), dazu beim Fluss die Wellenhöhe (wind_hoehe)
float wellen_windfaktor(float sturm) {
	float f = 1.0 + sturm * 0.6;
#ifdef MIT_WIND
	f *= 1.0 + wind_hoehe * smoothstep(0.25, 1.0, wetter_wind);
#endif
	return f;
}

// Stärke eines Wellenzuges an diesem Ort: großflächiges, langsam wanderndes Rauschen mit festem Startwert (kein Uhrzeit-Zufall), je Zug und
// je Gewässer (wellen_saat) anders – so hat jeder Abschnitt eines Flusses seine eigene Mischung aus langen, kurzen, schnellen und langsamen Wellen
float zug_staerke(vec2 position, float saat, float skala) {
	if (wellen_vielfalt <= 0.0) {
		return 1.0;
	}
	vec2 versatz = vec2(saat * 17.3 + wellen_saat * 7.31, saat * 5.1 + wellen_saat * 3.17);
	float n = w_rausch(position * skala * vielfalt_skala + versatz + vec2(TIME * 0.02 * (0.6 + saat * 0.3), TIME * 0.013));
	return clamp(1.0 + wellen_vielfalt * (n - 0.5) * 3.2, 0.12, 1.0 + wellen_vielfalt * 0.5);
}

vec4 wave(vec4 parameter, vec2 position, float time, float staerke, float laenge, vec2 wind, inout vec3 tangent, inout vec3 binormal) {
	float wave_length = parameter.w * laenge;
	float wave_steepness = parameter.z * staerke;
	if (netz_abstand > 0.0) {
		// Ein grobes Netz kann kurze Wellen nicht zeigen: Die Ecken sprängen zufällig auf und ab
		wave_steepness *= smoothstep(1.5 * netz_abstand, 3.0 * netz_abstand, wave_length);
	}
	float k = 2.0 * 3.14159265359 / wave_length;
	float c = sqrt(9.8 / k);
	vec2 d = normalize(parameter.xy + wind);
	float f = k * (dot(d, position) - c * time);
	float a = wave_steepness / k;
	tangent += normalize(vec3(1.0 - d.x * d.x * (wave_steepness * sin(f)), d.x * (wave_steepness * cos(f)), -d.x * d.y * (wave_steepness * sin(f))));
	binormal += normalize(vec3(-d.x * d.y * (wave_steepness * sin(f)), d.y * (wave_steepness * cos(f)), 1.0 - d.y * d.y * (wave_steepness * sin(f))));
	return vec4(d.x * (a * cos(f)), a * sin(f) * 0.25, d.y * (a * cos(f)), 0.0);
}

// Die Summe aller Wellenzüge. p = Ort in Wellenachsen (Fluss mit lauf_netz: Laufachsen (quer, Stelle), sonst Welt x, z), wind = Wind in
// denselben Achsen, staerke = Faktor (Wind, Eis, Rand). Liefert den Versatz in Wellenachsen (x = quer bzw. Welt x, y = hoch, z) und Tangente und
// Binormale in denselben Achsen.
vec3 wellen_summe(vec2 p, vec2 wind, float staerke, out vec3 tang, out vec3 bin) {
	float time = TIME * wave_speed;
	tang = vec3(0.0);
	bin = vec3(0.0);
	// Jeder Zug hat eigene Länge, Richtung, eigenes Tempo (über die Länge) und eine eigene, wandernde Stärke
	vec4 versatz = wave(wave_a, p, time * 1.0, staerke * zug_staerke(p, 0.0, 0.031), wellen_laenge, wind, tang, bin);
	versatz += wave(wave_b, p, time * 1.15, staerke * zug_staerke(p, 1.0, 0.043), wellen_laenge, wind, tang, bin);
	versatz += wave(wave_c, p, time * 0.85, staerke * zug_staerke(p, 2.0, 0.057), wellen_laenge, wind, tang, bin);
	if (extra_wellen > 0.0) {
		versatz += wave(wave_d, p, time * 0.9, staerke * extra_wellen * zug_staerke(p, 3.0, 0.023), wellen_laenge, wind, tang, bin);
		versatz += wave(wave_e, p, time * 1.25, staerke * extra_wellen * zug_staerke(p, 4.0, 0.067), wellen_laenge, wind, tang, bin);
	}
	return versatz.xyz;
}

// Laufachsen → Welt: quer liegt bei (−Richtung.z, Richtung.x), längs ist die Fließrichtung (wie im Netz, Stadtansicht.Wasser.cs)
vec2 lauf_in_welt(vec2 v, vec2 richtung) {
	return vec2(-richtung.y, richtung.x) * v.x + richtung * v.y;
}

vec2 welt_in_lauf(vec2 v, vec2 richtung) {
	return vec2(dot(v, vec2(-richtung.y, richtung.x)), dot(v, richtung));
}

// Wellen an einem Weltort: rechnet bei einem Fluss (lauf_netz = 1) in Laufachsen (lauf = (quer, Stelle), richtung = Fließrichtung Welt) und gibt
// Versatz, Tangente, Binormale und Normale in Weltachsen zurück (Tangente ≈ Welt x, Binormale ≈ Welt z, wie die Normalkarten sie erwarten).
vec3 wellen_welt(vec2 welt, vec2 lauf, vec2 richtung, float staerke, vec2 wind_welt, out vec3 tang, out vec3 bin, out vec3 normale) {
	if (lauf_netz > 0.5) {
		vec3 t_l;
		vec3 b_l;
		vec3 v = wellen_summe(lauf, welt_in_lauf(wind_welt, richtung), staerke, t_l, b_l);
		vec3 n_l = normalize(cross(b_l, t_l));
		// Die Abbildung Laufachsen → Welt ist orthogonal (eine Spiegelung): Normalen gehen wie Richtungen über
		normale = normalize(vec3(lauf_in_welt(n_l.xz, richtung).x, n_l.y, lauf_in_welt(n_l.xz, richtung).y));
		// Tangente und Binormale in Weltachsen aus der Normalen; so lang wie die Summe der Züge (wie ohne Laufachsen: Brechung und Grundspiegelung
		// gewichten die Normalkarte mit dieser Länge)
		float zuege = extra_wellen > 0.0 ? 5.0 : 3.0;
		tang = normalize(vec3(1.0, -normale.x / max(normale.y, 0.05), 0.0)) * zuege;
		bin = normalize(vec3(0.0, -normale.z / max(normale.y, 0.05), 1.0)) * zuege;
		vec2 xz = lauf_in_welt(v.xz, richtung);
		return vec3(xz.x, v.y, xz.y);
	}
	vec3 v = wellen_summe(welt, wind_welt, staerke, tang, bin);
	normale = normalize(cross(bin, tang));
	return v;
}
