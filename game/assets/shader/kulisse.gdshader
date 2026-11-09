// Kulissenberge am Kartenrand (Felspinsel F7, Stadtansicht.Kulisse.cs): drei Staffeln eines Rings hinter der Außenkante (kulisse/kulisse_koeln.glb).
// Schlicht gemalt-realistisch: Godots Licht, keine Schatten (weder werfen noch empfangen), Farbe aus den Vertexfarben, dazu Luftperspektive.
//
// Vertexfarbe (linear): R Wald (0–1), G Fels (helle Bänder an steilen Stellen), B Tiefe im Ring (0 an der Außenkante, 1 bei 300 m dahinter),
// A Höhe / 250 m.
// Luftperspektive: Je weiter hinten (B) und je höher (A), desto mehr nimmt der Berg die Farbe des Himmels an – die ferne Staffel ist fast nur noch
// Silhouette. Der Himmel ist hier die Farbe des Dunstrings am Kartenrand (Stadtansicht setzt „himmel“ jedes Bild wie beim Ring): Die Kamera sieht hinter
// den Bergen den Ring, nicht den Himmel. Der Fuß taucht ganz in diese Farbe (wie der Boden dahinter), die Kämme laufen weich aus (an der Silhouette, wo
// die Fläche zur Seite zeigt, mehr Dunst): keine harte Kante. Mehr Dunst bei Nebel und Regen („dunst“); nachts weniger, damit die Berge dunkel vor dem
// Nachthimmel stehen („nacht“). Wald und Wiese folgen der Jahreszeit (globale Uniform wie der Boden), im Winter liegt Schnee auf den Höhen.
shader_type spatial;
render_mode cull_back, shadows_disabled, fog_disabled;

#include "res://assets/shader/himmel_verlauf.gdshaderinc"   // eigener Dunst statt Godots Nebel (wie der Dunstring, sonst heller als er)

global uniform vec4 jahreszeit_anteile;
global uniform float wetter_schnee;

uniform vec3 himmel : source_color = vec3(0.47, 0.60, 0.72);   // Horizont (Farbe des Dunstrings)
uniform vec3 zenit : source_color = vec3(0.25, 0.42, 0.61);   // Himmelsfarbe der Tageszeit (linear), nur für den Verlauf im Blick der Ego-Ansicht
uniform float dunst = 0.0;      // zusätzlicher Dunst durch das Wetter, 0–1
uniform float nacht = 0.0;      // 0 Tag … 1 tiefe Nacht
uniform vec4 wald_farbe : source_color = vec4(0.20, 0.27, 0.15, 1.0);
uniform vec4 wiese_farbe : source_color = vec4(0.42, 0.45, 0.27, 1.0);
uniform vec4 fels_farbe : source_color = vec4(0.66, 0.63, 0.57, 1.0);
uniform vec4 schnee_farbe : source_color = vec4(0.86, 0.88, 0.91, 1.0);
uniform float dunst_nah = 0.06;   // Dunst für nahe Berge (450 m von der Kamera)
uniform float dunst_fern = 0.80; // Dunst für ferne (2,2 km von der Kamera)
// Stufe Niedrig zeigt nur die nahe Staffel (F8): Ohne die Staffeln dahinter stünde ihr Kamm hart vor dem Ring – darum dort mehr Dunst nach oben
// und eine breitere, weichere Silhouette.
uniform float allein = 0.0;

varying vec4 v_farbe;
varying vec3 v_welt;

float k_hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

float k_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(k_hash(i), k_hash(i + vec2(1.0, 0.0)), f.x), mix(k_hash(i + vec2(0.0, 1.0)), k_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	v_farbe = COLOR;
	v_welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float wald = v_farbe.r, fels = v_farbe.g, tiefe = v_farbe.b, hoehe = v_farbe.a;
	vec4 j = jahreszeit_anteile;
	// Wald je Jahreszeit: Frühling heller, Herbst rostbraun, Winter kahl graubraun; die Wiese wie der Boden (frisch, satt, gelblich, fahl)
	vec3 waldF = wald_farbe.rgb * (j.x * vec3(1.08, 1.12, 0.9) + j.y + j.z * vec3(1.55, 1.05, 0.62) + j.w * vec3(1.05, 0.92, 0.88));
	vec3 wieseF = wiese_farbe.rgb * (j.x * vec3(0.95, 1.07, 0.84) + j.y + j.z * vec3(1.3, 1.05, 0.7) + j.w * vec3(1.05, 1.0, 0.95));
	// grobe Flecken, damit der Wald nicht wie eine Farbfläche wirkt
	float fleck = k_rausch(v_welt.xz * 0.045) * 0.6 + k_rausch(v_welt.xz * 0.17) * 0.4;
	vec3 grund = mix(wieseF, waldF * (0.6 + 0.8 * fleck), smoothstep(0.2, 0.7, wald + (fleck - 0.5) * 0.3));
	grund = mix(grund, fels_farbe.rgb * (0.8 + 0.2 * fleck), 0.6 * smoothstep(0.3, 0.8, fels));
	// Schnee im Winter: oben zuerst, auf flachen Stellen
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float schnee = smoothstep(0.0, 0.3, wetter_schnee * 1.4 + hoehe * 0.8 - 0.55 + (fleck - 0.5) * 0.3) * smoothstep(0.4, 0.75, wn.y) * step(0.01, wetter_schnee);
	grund = mix(grund, schnee_farbe.rgb, schnee);

	// Luftperspektive (Ego-Ansicht, 09.11.2026): Je weiter hinten (tiefe), desto mehr nimmt der Berg die Farbe der Luft an – und die Luft vor dem Horizont ist
	// ein heller Dunst, darüber blaut sie zum Himmel. Die nahe Staffel ist ein dunkles, blaugrünes Band, die mittlere blasser, die ferne nur noch ein
	// Hauch, der mit dem Himmel dahinter verschwimmt. Das Ziel des Dunstes ist die Himmelsfarbe in der Blickrichtung zum Berg (himmel_verlauf, wie der Himmel
	// selbst): Der Kamm läuft ohne Kante in den Himmel aus, eine Ebene davor steht dunkler und bläulicher.
	vec3 blick = normalize(v_welt - CAMERA_POSITION_WORLD);
	vec3 himmel_hier = himmel_verlauf(blick.y, himmel, zenit);
	// Tiefe nach dem Abstand zur Kamera (nicht nach der Tiefe im Ring: Wer mitten in der Stadt steht, sieht alle Berge weiter weg als vom Rand)
	float abstand = length(v_welt - CAMERA_POSITION_WORLD);
	float tiefe_a = clamp((abstand - 450.0) / 1750.0, 0.0, 1.0);
	// unten (Täler, Vorberge) dunstiger, die Kämme klarer: So zeichnen sich Rücken und Täler ab
	float d = mix(dunst_nah, dunst_fern, tiefe_a) + 0.18 * (1.0 - smoothstep(0.0, 0.5, hoehe)) - 0.06;
	float seitlich = 1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
	d += mix(pow(seitlich, 4.0) * 0.25, pow(seitlich, 1.6) * 0.6, allein);  // Silhouette weich (allein: breiter)
	d += allein * 0.3 * smoothstep(0.15, 0.7, hoehe);                       // allein: die Kämme tiefer im Dunst
	d = mix(d, 1.0, dunst * 0.75);
	d = clamp(d, 0.0, 1.0) * (1.0 - 0.6 * nacht);
	d = max(d, 1.0 - smoothstep(0.0, 0.04, hoehe));                        // Fuß im Dunst (unter 10 m), auch nachts genau wie der Ring
	vec3 ziel = mix(himmel_hier, zenit, 0.75 * (1.0 - tiefe_a));             // vorn bläulicher als der Himmel dahinter, hinten gleich
	ziel = mix(ziel, himmel_hier, clamp(pow(seitlich, 3.0) + dunst * 0.8, 0.0, 1.0));  // am Rand und bei Nebel: genau der Himmel
	ziel = mix(ziel, himmel, 1.0 - smoothstep(0.0, 0.04, hoehe));           // der Fuß: genau der Ring
	ALBEDO = grund * 1.5 * (1.0 - d) * (1.0 - 0.75 * nacht);             // nachts dunkel vor dem Himmel (der Fuß bleibt im Ring)
	// nachts (F8): ein schwacher, kühler Schein auf Hängen und Kämmen, die zum Himmel zeigen – die Berge heben sich leicht vom Nachthimmel ab,
	// statt mit ihm zu verschwimmen; der Fuß bleibt wie der Ring
	float oben_n = clamp(wn.y, 0.0, 1.0);
	vec3 nachtschein = vec3(0.020, 0.026, 0.040) * (0.35 + 0.65 * oben_n) * (0.5 + 0.5 * pow(seitlich, 1.5)) * smoothstep(0.08, 0.3, hoehe);
	EMISSION = ziel * d + nachtschein * nacht * (1.0 - d);
	ROUGHNESS = 0.95;
	SPECULAR = 0.15 * (1.0 - d);                                          // im vollen Dunst kein Glanz: genau die Farbe des Rings
}
