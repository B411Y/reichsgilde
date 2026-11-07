// Wasserverdränger für Schiffe und Boote: malt nichts, schreibt aber Tiefe.
// Der Körper (Knoten „Verdraenger“ im Modell, geschlossen, in Rumpfform von knapp über dem Boden bis knapp unter das Dollbord) wird vor allen Gewässern gezeichnet
// (render_priority −20 im Material, die Gewässer liegen bei −8 bis −6). Weil er Tiefe schreibt, besteht die Wasserfläche hinter ihm den Tiefentest nicht:
// Im Boot ist kein Wasser zu sehen (Nachen mit Schiffer, Boden unter der Wasserlinie, Fugen der Wegerung, Wellengang), außen ändert sich nichts.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always, shadows_disabled;

void fragment() {
	ALBEDO = vec3(0.0);
	ALPHA = 0.0;
}
