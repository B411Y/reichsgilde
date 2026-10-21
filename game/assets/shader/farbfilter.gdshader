// Farbfilter für Farbsehschwäche (Daltonisierung nach Fidaner, Lin und Ozguven, 2005): Das Bild wird so gerechnet, wie es ein
// Mensch mit der gewählten Schwäche sieht (Simulation im LMS-Raum der Zapfen). Was dabei verloren geht (der Fehler), wird auf
// die Farbkanäle verschoben, die er gut unterscheidet – rot-grüne Unterschiede werden so zu hell-dunkel und blau-gelb.
// Liegt auf der obersten CanvasLayer (Bildfilter.cs) und gilt für das ganze Bild: 3D-Ansicht und Oberfläche.
shader_type canvas_item;
render_mode unshaded, blend_disabled;

uniform sampler2D bild : hint_screen_texture, filter_nearest;
/// 1 = Protanopie (Rot), 2 = Deuteranopie (Grün), 3 = Tritanopie (Blau)
uniform int art = 2;

void fragment() {
	vec3 rgb = texture(bild, SCREEN_UV).rgb;
	// RGB → LMS
	mat3 zu_lms = mat3(vec3(17.8824, 3.45565, 0.0299566), vec3(43.5161, 27.1554, 0.184309), vec3(4.11935, 3.86714, 1.46709));
	mat3 zu_rgb = mat3(vec3(0.0809444479, -0.0102485335, -0.000365296938), vec3(-0.130504409, 0.0540193266, -0.00412161469),
	                   vec3(0.116721066, -0.113614708, 0.693511405));
	vec3 lms = zu_lms * rgb;
	vec3 sim = lms;
	if (art == 1) sim.x = 2.02344 * lms.y - 2.52581 * lms.z;
	else if (art == 2) sim.y = 0.494207 * lms.x + 1.24827 * lms.z;
	else sim.z = -0.395913 * lms.x + 0.801109 * lms.y;
	vec3 fehler = rgb - zu_rgb * sim;
	// Den Fehler auf die sichtbaren Kanäle verschieben
	vec3 korrektur = art == 3
		? vec3(fehler.r + 0.7 * fehler.b, fehler.g + 0.7 * fehler.b, 0.0)
		: vec3(0.0, 0.7 * fehler.r + fehler.g, 0.7 * fehler.r + fehler.b);
	COLOR = vec4(clamp(rgb + korrektur, 0.0, 1.0), 1.0);
}
