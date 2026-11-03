// Pixel-Look (Testszene Pixeltest): Wasser im Pixelstil. Die Oberfläche liegt auf einem Weltraster aus Texeln (feste Größe in Metern); je Texel
// gibt es genau einen Wert aus zwei laufenden Wellenmustern, der in drei Farbstufen (tief, mittel, hell) gesetzt wird; die höchsten Spitzen
// werden zu hellen Glanzpunkten (gestufte Glanzlichter). Wird ein Texel kleiner als ~1,5 Bildpunkte (Ferne), blendet das Muster zur mittleren Stufe aus
// (kein Flimmern). Durchsichtig; ohne Bildschirmtextur. Eigenbau (MIT).
shader_type spatial;
render_mode unshaded, cull_back, blend_mix, depth_draw_never;

uniform vec3 tief : source_color = vec3(0.10, 0.22, 0.30);
uniform vec3 mittel : source_color = vec3(0.16, 0.34, 0.42);
uniform vec3 hell : source_color = vec3(0.34, 0.52, 0.58);
uniform vec3 glanz : source_color = vec3(0.92, 0.96, 1.0);
uniform float texel = 0.16;             // Meter
uniform float meter_je_pixel = 0.07;    // vom Skript gesetzt (Zoom)
uniform float deckung = 0.84;
uniform float tempo = 0.5;

varying vec3 welt;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = (floor(welt.xz / texel) + 0.5) * texel;
	float t = TIME * tempo;
	float w = 0.5 + 0.25 * sin(p.x * 1.7 + t * 1.3 + sin(p.y * 0.9 + t * 0.4) * 1.6)
				  + 0.25 * sin(p.y * 2.3 - t * 1.1 + sin(p.x * 1.2 - t * 0.5) * 1.3);
	float blende = smoothstep(1.4, 2.6, texel / meter_je_pixel);
	w = mix(0.5, w, blende);
	vec3 f = mittel;
	if (w < 0.36) f = tief;
	else if (w > 0.66) f = hell;
	if (w > 0.9) f = glanz;
	ALBEDO = f;
	ALPHA = mix(deckung, 1.0, step(0.9, w));
}
