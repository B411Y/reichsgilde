// Verlauf des Himmels vom Horizont zum Zenit (Ego-Ansicht, Stadtansicht.Himmel.cs): gemeinsame Rechnung für den Himmel (himmel.gdshader), die Kulissenberge
// (kulisse.gdshader) und das Fernland (fernland.gdshader). Alle drei färben ihr Ende in die Farbe, die der Himmel an dieser Stelle hat, so gibt es nirgends eine
// Kante zwischen Berg, Land und Himmel.
//
// horizont: die Farbe des Dunstrings am Kartenrand (so geht der Boden ohne Kante in den Himmel über); zenit: die Himmelsfarbe der Tageszeit (linear, wie der
// frühere einfarbige Hintergrund). h: Höhe der Blickrichtung über dem Horizont (Sinus, 0 = Horizont, 1 = Zenit).
vec3 himmel_verlauf(float h, vec3 horizont, vec3 zenit) {
	float t = 1.0 - exp(-3.0 * max(h, 0.0));
	return mix(horizont, zenit, t);
}
