// Charakter Etappe D: Brauen und Wimpern weich (eigener Code, MIT) – wie haar3.gdshader, aber mit Alpha-Mischung statt
// Alpha-Schere: Haare schmaler als ein Bildpunkt decken anteilig statt als ganzer, dunkler Bildpunkt (Prüferbefund „Brauen
// wie aufgeklebt“, „Wimpern hart“). Schreibt keine Tiefe (zwei Kartenlagen mischen sich ohne Lücken). Werte wie haar3.
// Brauen je Person (nur Brauen, braue = true): Stand, Dicke und Winkel verschieben das Bild in der Karte (mm), damit sich
// die Braue sichtbar je Person unterscheidet, ohne neue Bilder oder Formen. Karte: u innen -> außen (64 mm), v unten ->
// oben, Höhe 16,5 mm innen bis 11,5 mm außen, zwei Lagen übereinander im Bild (v 0..0,5 und 0,5..1).
// Verschiedenheit E1: In Godot läuft UV.y in der Karte von oben nach unten (glTF: Bildzeile 0 = oben); Etappe D rechnete
// mit v von unten nach oben, Stand und Winkel wirkten damit verkehrt herum und die Dicke wuchs um eine Linie am unteren
// Brauenrand. v_nach_unten = true rechnet richtig herum (Probekopf glb6e); stand_oben_max begrenzt die Verschiebung nach
// oben weich, weil über der Braue in der Karte nur 1,5–3 mm Platz sind (sonst wäre die Braue oben abgeschnitten).
shader_type spatial;
render_mode cull_disabled, depth_draw_never;

uniform sampler2D bild : hint_default_transparent, filter_linear_mipmap_anisotropic, repeat_disable;
uniform vec3 farbe : source_color = vec3(0.12, 0.08, 0.05);
uniform vec3 farbe_grau : source_color = vec3(0.78, 0.77, 0.75);
uniform float grau = 0.0;           // Anteil grauer Haare
uniform float dichte = 1.0;         // Haare mit Zufallswert < dichte bleiben
uniform float breite = 1.0;         // nur Brauen: Haare mit Wurzelabstand < breite bleiben
uniform float schwelle = 0.3;       // Phase 4: tiefer (feinere, lichtere Haare; Kanten über Alpha-to-Coverage)
uniform float dunkel = 0.6;         // Haare verschatten sich gegenseitig
// Phase 6 (S7): Brauenformen als Bildarray; ebene < 0 = „bild“ wie Phase 3–5
uniform sampler2DArray bilder6 : hint_default_transparent, filter_linear_mipmap_anisotropic, repeat_disable;
uniform float ebene = -1.0;
uniform float glanz = 0.15;
uniform bool braue = false;
uniform float stand_mm = 0.0;        // + = Braue höher
uniform float dicke = 1.0;           // Höhe der Braue um ihre Mittellinie (0,6 … 1,4)
uniform float winkel_mm = 0.0;       // + = Ende außen höher (je Länge der Braue)
uniform float mitte_v = 0.62;        // Mittellinie der Braue in der Lage (Anteil der Kartenhöhe)         // Glanz der Haare (haar3: 0,35 – funkelte als weiße Punkte)
uniform bool v_nach_unten = false;   // E1: v der Karte läuft von oben nach unten (richtig für die Karten aus kopf3_haare.py)
uniform float stand_oben_max = 0.0;  // E1: > 0 = Verschiebung nach oben weich auf diesen Wert begrenzt (mm)
// E1 Runde 2 (Prüfer E0/E1: „die hintere Braue steht schräg über den Stirnumriss hinaus und schwebt“): die Karte steht
// von der Haut ab; wo man sie fast von der Kante sieht (am Umriss), blendet sie aus. 0 = aus (wie Etappe D)
uniform float rand_aus = 0.0;
// E3 (Grundkopf; Prüfer E1: tief gesetzte Braue macht den Brauenwulst schräg zum „Sims“): Verschiebung nach unten weich
// auf diesen Wert begrenzt (mm, 0 = aus), und die Kante des Ausblendens am Umriss (E1: 0,38)
uniform float stand_unten_max = 0.0;
uniform float rand_aus_kante = 0.38;
uniform float mip_hub = 0.25;       // Deckung in der Ferne anheben (Mip-Stufe)
uniform float deckung = 1.0;        // Halt B2 (Weg A): Brauen weniger deckend (weiche Haargruppen statt Aufkleber)
// Etappe C (nur Wimpern; Prüferbefund „Unterlid wie ein Strich“): Band 0 des Wimpernbilds (Unterlid, UV.y > 0,75)
// lichter, dünner und heller, die Wurzeln laufen weich aus (keine dunkle Naht am Lidrand). Standard = Phase 5.
uniform float unten_deckung = 1.0;
uniform float unten_wurzel = 0.0;   // 0 = Wurzel voll, 1 = Wurzel ausgeblendet
uniform float unten_hell = 0.0;     // Anteil Hautton
uniform vec3 haut_ton : source_color = vec3(0.6, 0.45, 0.38);
uniform float zufall_hell = 0.4;    // Etappe D (Brauen 0,7): Helligkeit je Haar stärker verschieden (kein einheitlicher Farbblock)
// Grundkopf 2 (Prüfer: „Brauen aufgemalt“, bei dünner Dichte „gepunktete Striche“): haerchen > 0 – Haare über der Dichte
// fallen nicht weg, sondern bleiben als feine, lichte Haare (haerchen_rest); die Spitzen laufen aus, jedes Haar deckt etwas
// verschieden, und die Haarfarbe hat einen schmalen, längs laufenden Glanz. 0 = wie bisher.
uniform float haerchen = 0.0;
uniform float haerchen_rest = 0.4;

void fragment() {
	vec2 uv = UV;
	float aussen = 0.0;
	if (braue) {
		float lage = step(0.5, UV.y);
		float v = UV.y * 2.0 - lage;
		if (v_nach_unten) {
			v = 1.0 - v;
		}
		float h = mix(16.5, 11.5, UV.x);
		float s = stand_mm + winkel_mm * (UV.x - 0.15);
		if (stand_oben_max > 0.0 && s > 0.0) {
			s = stand_oben_max * tanh(s / stand_oben_max);
		}
		if (stand_unten_max > 0.0 && s < 0.0) {
			s = stand_unten_max * tanh(s / stand_unten_max);
		}
		float vq = (v - mitte_v - s / h) / max(dicke, 0.3) + mitte_v;
		aussen = step(vq, 0.004) + step(0.996, vq);
		if (v_nach_unten) {
			vq = 1.0 - vq;
		}
		uv = vec2(UV.x, 0.5 * lage + 0.5 * clamp(vq, 0.004, 0.996));
	}
	vec4 t = ebene >= 0.0 ? texture(bilder6, vec3(uv, ebene)) : texture(bild, uv);
	vec2 px = uv * (ebene >= 0.0 ? vec2(textureSize(bilder6, 0).xy) : vec2(textureSize(bild, 0)));
	float lod = max(0.0, 0.5 * log2(max(dot(dFdx(px), dFdx(px)), dot(dFdy(px), dFdy(px)))));
	float a = clamp(t.a * (1.0 + mip_hub * lod), 0.0, 1.0);
	float weg_d = smoothstep(dichte - 0.03, dichte, t.r);
	float weg_b = smoothstep(breite - 0.06, breite, t.g);
	if (haerchen > 0.0) {
		// lichte Haare statt Lücken; die Randhaare (breite) bleiben schwächer als die über der Dichte
		float rest = haerchen_rest * haerchen;
		a *= (1.0 - weg_d * (1.0 - rest)) * (1.0 - weg_b * (1.0 - 0.6 * rest));
		a *= mix(1.0, 0.55 + 0.6 * fract(t.r * 13.7 + 0.21), 0.6 * haerchen);   // jedes Haar deckt etwas anders
		a *= 1.0 - 0.45 * haerchen * smoothstep(0.55, 1.0, t.b);                 // Spitzen laufen aus
	} else {
		a *= 1.0 - weg_d;
		a *= 1.0 - weg_b;
	}
	// Zufall je Haar aus R (langsam veränderlich, damit gefilterte Ränder nicht flimmern)
	float zufall = fract(t.r * 5.71 + 0.3);
	vec3 c = farbe * (1.0 - 0.5 * zufall_hell + zufall_hell * zufall);
	c = mix(c, farbe_grau * (0.85 + 0.3 * zufall), smoothstep(-0.04, 0.04, grau - fract(t.r * 2.37 + 0.11)));
	c *= mix(0.85, 1.2, t.b);                    // Spitzen heller
	float unten = step(0.75, UV.y) * step(ebene, -0.5);
	a *= mix(1.0, unten_deckung * mix(1.0, smoothstep(0.0, 0.45, t.b), unten_wurzel), unten);
	c = mix(c, mix(c, haut_ton * 0.55, unten_hell), unten);
	ALBEDO = c * dunkel;
	ALPHA = clamp(a * deckung, 0.0, 1.0) * (1.0 - clamp(aussen, 0.0, 1.0));   // deckung > 1: dünne Haare decken mehr (Brauen)
	if (braue && rand_aus > 0.0) {
		ALPHA *= mix(1.0, smoothstep(0.1, max(rand_aus_kante, 0.11), abs(dot(normalize(NORMAL), VIEW))), rand_aus);
	}
	ROUGHNESS = mix(0.5, 0.38, haerchen);
	SPECULAR = glanz * (1.0 + 0.6 * haerchen);
	BACKLIGHT = c * 0.25;
}
