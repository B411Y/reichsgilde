// Nachschärfen des fertigen 3D-Bildes (Einstellung „Nachschärfen“ ohne FSR 2.2), nach dem Verfahren von AMD FidelityFX CAS:
// Jeder Bildpunkt wird gegen seine vier Nachbarn geschärft, und zwar umso weniger, je größer der Kontrast dort schon ist –
// so entstehen an harten Kanten keine hellen Säume, und flache Flächen rauschen nicht auf.
// Läuft auf einer CanvasLayer unter allen Tafeln (Bildfilter.cs): Die Schrift der Oberfläche bleibt unberührt.
shader_type canvas_item;
render_mode unshaded, blend_disabled;

uniform sampler2D bild : hint_screen_texture, filter_nearest;
/// 0 = nicht schärfen, 1 = am stärksten
uniform float staerke : hint_range(0.0, 1.0) = 0.4;

void fragment() {
	vec2 p = SCREEN_PIXEL_SIZE;
	vec3 c = texture(bild, SCREEN_UV).rgb;
	vec3 n = texture(bild, SCREEN_UV + vec2(0.0, -p.y)).rgb;
	vec3 s = texture(bild, SCREEN_UV + vec2(0.0, p.y)).rgb;
	vec3 w = texture(bild, SCREEN_UV + vec2(-p.x, 0.0)).rgb;
	vec3 o = texture(bild, SCREEN_UV + vec2(p.x, 0.0)).rgb;
	vec3 klein = min(c, min(min(n, s), min(w, o)));
	vec3 gross = max(c, max(max(n, s), max(w, o)));
	// Spielraum bis Schwarz bzw. Weiß im Verhältnis zum hellsten Wert: klein bei hohem Kontrast
	vec3 spielraum = sqrt(clamp(min(klein, 1.0 - gross) / max(gross, vec3(1e-4)), 0.0, 1.0));
	vec3 gewicht = spielraum * (-1.0 / mix(8.0, 5.0, staerke));
	vec3 ergebnis = (c + (n + s + w + o) * gewicht) / (1.0 + 4.0 * gewicht);
	COLOR = vec4(clamp(ergebnis, 0.0, 1.0), 1.0);
}
