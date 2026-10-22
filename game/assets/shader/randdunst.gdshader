// Dunst am Rand der Karte (Kartenwerkstatt, Schritt 5a): ein flacher Ring über dem Boden, außen in der Farbe des Himmels. Innen ist er
// durchsichtig und wird nach außen dichter, bis er den Boden ganz zudeckt – so läuft die Randlandschaft in den Dunst aus, und die Kamera
// sieht nie eine harte Kante oder den Himmel hinter dem Boden. Das Spiel setzt die Farbe jedes Bild auf die Himmelsfarbe der Tageszeit.
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled, fog_disabled;

// Das Rechteck, in dem der Dunst noch nicht beginnt (x0, z0, x1, z1 in Metern) und die Breite, in der er bis zum vollen Dunst wächst
uniform vec4 innen = vec4(-100.0, -100.0, 100.0, 100.0);
uniform float breite = 100.0;
uniform vec3 farbe : source_color = vec3(0.47, 0.60, 0.72);

varying vec3 welt;

void vertex() {
	welt = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 aus = max(innen.xy - welt.xz, welt.xz - innen.zw);
	float t = clamp(max(aus.x, aus.y) / breite, 0.0, 1.0);
	ALBEDO = farbe;
	ALPHA = t * t * (3.0 - 2.0 * t);
}
