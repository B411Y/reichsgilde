// Pflanzen in den Reihen der Äcker (Stadtansicht.Feldpflanzen.cs): ein Büschel aus sieben Halmen (je ein Dreieck) steht auf jedem Furchenkamm, als MultiMesh
// je Zelle und Frucht. Wie hoch und in welcher Farbe, bestimmt die Jahreszeit (jahreszeit_anteile, wetter.gdshaderinc) – dieselben Anteile wie im Boden von
// acker.gdshader: Frühling kleine Keimlinge, Sommer hohe Frucht mit Ähren, Herbst Stoppeln (oder noch stehende Frucht), Winter kurze Stümpfe unter dem Schnee.
// Instanzfarbe: RGB leichter Farbstich des Stücks, A = 1 die Frucht steht im Herbst noch, 0 sie ist schon geschnitten.
// Der Zoom (orthografische Kamera, wie gras.gdshader): weit weg stehen weniger Büschel, dafür breitere; die Kämme sinken wie in acker.gdshader in die Fläche.
// Netz: Fuß der Halme am Ring (Höhe 0), Spitzen darüber, UV.x = Anteil der Höhe (0 Fuß, 1 Spitze), NORMAL = Richtung nach außen.
shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled;

#include "res://assets/shader/wetter.gdshaderinc"

// Farbe des Halms im Sommer und der Ähre bzw. Blüte; die junge Saat im Frühling
uniform vec3 farbe : source_color = vec3(0.70, 0.62, 0.30);
uniform vec3 spitze : source_color = vec3(0.82, 0.70, 0.36);
uniform vec3 jung : source_color = vec3(0.30, 0.48, 0.16);
// Höhe der Frucht im Sommer in Metern; Anteil der Büschel, die überhaupt stehen (Brache: lückenhaft)
uniform float hoehe = 1.0;
uniform float anteil = 1.0;
// Höhe der Kämme (acker.gdshader, kammhoehe): Die Büschel stehen auf ihnen
uniform float kammhoehe = 0.14;

varying vec3 farbton;
varying float schnee_v;

float h21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void vertex() {
	vec3 o = MODEL_MATRIX[3].xyz;
	float s = length(MODEL_MATRIX[0].xyz);
	float groesse = 2.0 / abs(PROJECTION_MATRIX[1][1]); // sichtbare Höhe in Metern (unter Vulkan gespiegelt: abs)
	float pixel = groesse / VIEWPORT_SIZE.y;
	float kammf = 1.0 - smoothstep(0.16, 0.30, pixel);
	float z1 = h21(o.xz * 7.31);
	float z2 = h21(o.xz * 3.17 + vec2(5.0, 9.0));

	// weit weg weniger Büschel, dafür breitere (etwa gleich viele Bildpunkte)
	float zoom = clamp(pow(60.0 / groesse, 1.2), 0.3, 1.0);
	float breite = inversesqrt(zoom);

	// Höhe nach Jahreszeit (Anteile Frühling, Sommer, Herbst, Winter); im Herbst steht die Frucht noch oder ist schon geschnitten
	vec4 j = jahreszeit_anteile;
	float stehend = COLOR.a;
	float hs = j.x * (0.12 + 0.05 * z2) + j.y + j.z * mix(0.09, 1.0, stehend) + j.w * 0.05;
	float schnee = schnee_decke(o.xz, 1.0);
	hs *= 1.0 - 0.85 * schnee * (1.0 - j.y);
	float h = hoehe * hs * mix(0.8, 1.15, z2);

	// Farbe: Fuß dunkler und grüner, oben Ähre bzw. Blüte
	float t = UV.x;
	vec3 stoppel = vec3(0.62, 0.54, 0.34);
	vec3 sommer = mix(farbe * vec3(0.78, 0.86, 0.6), spitze, smoothstep(0.55, 0.85, t));
	vec3 herbst = mix(stoppel * 0.9, mix(farbe * 0.8, spitze * 0.82, smoothstep(0.55, 0.85, t)), stehend);
	vec3 saat = mix(jung * 0.8, jung * 1.15, t);
	vec3 winter = vec3(0.38, 0.32, 0.22);
	vec3 c = saat * j.x + sommer * j.y + herbst * j.z + winter * j.w;
	farbton = mix(c, vec3(0.66, 0.68, 0.73), clamp(schnee * 0.8, 0.0, 1.0)) * COLOR.rgb;
	schnee_v = schnee;

	if (z1 >= zoom * anteil || h < 0.02) {
		VERTEX = vec3(0.0);
		return;
	}
	// Wind: die Spitzen neigen sich in Windrichtung, in Wellen über das Feld
	float welle = 0.6 + 0.4 * sin(TIME * 1.7 + o.x * 0.35 + o.z * 0.27 + z2 * 6.0);
	vec3 weh = vec3(wind_richtung.x, 0.0, wind_richtung.y) * (0.03 + 0.14 * wetter_wind) * welle * h * t * t;
	vec3 lokal = transpose(mat3(MODEL_MATRIX)) * weh / (s * s);
	VERTEX = vec3(VERTEX.x * breite, VERTEX.y * h + kammhoehe * kammf / s, VERTEX.z * breite) + lokal;
	NORMAL = normalize(vec3(NORMAL.x * 0.3, 1.0, NORMAL.z * 0.3));
}

void fragment() {
	ALBEDO = farbton;
	ROUGHNESS = 0.95;
}
