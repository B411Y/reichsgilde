// Äcker der Randlandschaft (Kartenwerkstatt, Schritt 5a): ein flaches Viereck je Ackerstück, die Furchen und der Bewuchs kommen aus diesem
// Shader. Gemalt-realistisch wie die übrige 3D-Welt: Erde und Frucht mit leichter Körnung und Flecken, Furchen als Unebenheit im Licht.
// Die Furchen laufen entlang der lokalen z-Achse des Stücks (wie die Länge des Stücks in FeldVerteilung); je Stück ein anderer Versatz.
// An den langen Kanten jedes Stücks liegt eine Furche (dort stoßen die Streifen aneinander): So erscheint jeder Streifen mit seinem Rain.
// Vertexdaten (aus Stadtansicht.Rand.cs, Felder): UV = Ort im Stück in Metern (x quer, z längs), UV2.x = Drehung des Stücks (Bogenmaß, wie
// Rotation.Y), COLOR.rgb = leichter Farbstich der Variante um 1, COLOR.a = halbe Breite des Stücks geteilt durch 16.
shader_type spatial;
render_mode cull_disabled;

// Farbe der Frucht im Sommer, Farbe der Erde dazwischen und wie dicht der Bewuchs steht (1: Reihen ganz geschlossen, 0: nackte Erde)
uniform vec3 farbe : source_color = vec3(0.70, 0.62, 0.30);
uniform vec3 erde : source_color = vec3(0.34, 0.25, 0.16);
uniform float bewuchs : hint_range(0.0, 1.0) = 0.8;
// Abstand der Furchen in Metern und wie stark sie in Licht und Farbe zählen
uniform float abstand = 1.5;
uniform float furchen : hint_range(0.0, 1.0) = 1.0;
// Die Jahreszeit kommt aus jahreszeit_anteile (wetter.gdshaderinc): Frühling junge grüne Saat, Sommer reife Frucht, Herbst Stoppeln,
// Winter brach und dunkel, mit Schnee; dazu Nässe.
#include "res://assets/shader/wetter.gdshaderinc"

varying vec3 welt;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

// Wertrauschen: weich zwischen Zufallswerten an den Gitterpunkten
float rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	// Furchen quer zum Stück: ganze Zahl an Furchen je Stück, die erste und letzte genau an den Kanten
	float halb = COLOR.a * 16.0;
	float anzahl = max(1.0, round(2.0 * halb / abstand));
	float phase = (UV.x + halb) / (2.0 * halb / anzahl);
	// Aus der Ferne, wo eine Furche nur noch ein, zwei Bildpunkte breit ist, verschwinden die Streifen (sonst flimmern sie als Moiré)
	float fern = 1.0 - smoothstep(0.18, 0.45, fwidth(phase));
	float welle = -cos(phase * TAU) * fern;         // −1 Furche (auch an den Kanten), +1 Kamm
	// Flecken (groß), Schollen (mittel) und Körnung (fein) in Weltkoordinaten: über Stückgrenzen hinweg ohne Naht
	float flecken = rausch(welt.xz * 0.11);
	float schollen = rausch(welt.xz * 0.8);
	float koernung = rausch(welt.xz * 5.5);
	float unruhe = 0.5 * flecken + 0.3 * schollen + 0.2 * koernung;

	// Der Bewuchs sitzt auf den Kämmen, in den Furchen schaut die Erde heraus; Flecken lassen ihn dichter oder lichter stehen
	vec4 j = jahreszeit_anteile;
	vec3 reif = farbe * COLOR.rgb;
	vec3 saat = mix(reif, vec3(0.34, 0.5, 0.2) * COLOR.rgb, 0.85);                          // Frühling: junge Saat
	vec3 stoppel = vec3(0.66, 0.58, 0.38) * COLOR.rgb * (0.88 + 0.24 * rausch(welt.xz * 9.0)); // Herbst: Stoppeln, fein gestreut
	vec3 frucht = (saat * j.x + reif * j.y + stoppel * j.z + reif * j.w) / max(j.x + j.y + j.z + j.w, 0.0001);
	float bew = bewuchs * (0.72 * j.x + j.y + 0.6 * j.z + j.w);
	float dicht = clamp(bew + furchen * 0.26 * welle + 0.55 * (unruhe - 0.5), 0.0, 1.0);
	vec3 boden = mix(erde * 0.85, erde * 1.15, schollen);
	vec3 grund = mix(boden, frucht, dicht);
	// Furchen im Licht: Kämme etwas heller, Furchen dunkler
	grund *= 1.0 + furchen * 0.12 * welle;
	grund *= 0.90 + 0.20 * unruhe;
	grund = mix(grund, boden * 0.85, j.w);
	float rau = 1.0;
	if (wetter_naesse > 0.001) {
		grund *= 1.0 - 0.32 * wetter_naesse;
		rau = mix(1.0, 0.55, wetter_naesse);
	}
	// Schnee: in den Furchen bleibt er länger liegen als auf den Kämmen
	float schnee = clamp(schnee_decke(welt.xz, 1.0) * (1.0 - 0.35 * max(welle, 0.0)), 0.0, 1.0);
	grund = mix(grund, vec3(0.86, 0.89, 0.94), schnee);

	// Unebenheit im Licht: Der Kamm neigt die Fläche quer zu den Furchen (Richtung quer über das Stück in Weltkoordinaten)
	vec3 quer = vec3(cos(UV2.x), 0.0, -sin(UV2.x));
	float neigung = -sin(phase * TAU) * fern * furchen * 0.38 + (koernung - 0.5) * 0.12;
	NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(quer, 0.0)).xyz * neigung);

	ALBEDO = grund;
	ROUGHNESS = mix(rau, 0.7, schnee);
	SPECULAR = 0.2;
}
