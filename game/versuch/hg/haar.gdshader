// Versuch HG-Köpfe: Haarkarten, erster Durchgang (deckender Kern).
// Alpha-Test bei 0,5 mit Alpha-to-Coverage (MSAA glättet die Kanten), Tiefe wird geschrieben -> keine Sortierfehler.
// Die weichen Säume (Alpha < 0,5) zeichnet haar_saum.gdshader als zweiter Durchgang darüber.
// Licht nach Kajiya-Kay: Glanz quer zur Strähne (Strähnenrichtung = UV-v der HG-Karten = BINORMAL), zwei Glanzbänder
// (weiß, leicht zur Spitze verschoben; farbig, zur Wurzel verschoben). Ideen aus dem MIT-Projekt
// github.com/Bauxitedev/godot-hair-shader und Scheuermann (ATI 2004); Code eigenständig geschrieben.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

#include "res://versuch/hg/haar_licht.gdshaderinc"

void fragment() {
	vec4 f = texture(farbe, UV);
	ALBEDO = haar_farbe(f, UV2.x);
	NORMAL = n_sicht;
	ROUGHNESS = 0.6;
	SPECULAR = 0.0;
	BACKLIGHT = vec3(0.22, 0.15, 0.1);
	ALPHA = f.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	ALPHA_ANTIALIASING_EDGE = 0.3;
	ALPHA_TEXTURE_COORDINATE = UV * vec2(textureSize(farbe, 0));
}
