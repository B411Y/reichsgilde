// Bleinetz der Butzenscheiben von außen (Neubau der Gebäude): liegt als dünne Fläche vor der Scheibe mit dem Fensterlicht
// (fenster.gdshader) und zeichnet nur die Bleiruten – bei Tag dunkel auf dunklem Glas, nachts als Gitter vor dem Kerzenlicht.
// In der Ferne wird daraus ein gleichmäßiger Schleier (kein Flimmern, auch im Pixel-Look). Teilung wie innen: butzen.gdshaderinc.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, shadows_disabled;

#include "butzen.gdshaderinc"

uniform vec3 bleifarbe : source_color = vec3(0.10, 0.10, 0.11);
uniform float deckung : hint_range(0.0, 1.0) = 0.9;

void fragment() {
	float kennung;
	vec4 b = butzen(UV, kennung);
	float fein = max(fwidth(UV.x), fwidth(UV.y));
	ALBEDO = bleifarbe;
	ROUGHNESS = 0.55;
	METALLIC = 0.5;
	ALPHA = butzen_blei(b.y, fein) * deckung;
}
