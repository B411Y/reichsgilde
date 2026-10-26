// Die Person im Porträt (PortraetAnzeige): das freigestellte Brustbild, dazu
//  - Haar und Augen umgefärbt über Graustufen-Masken (weiß = Haar bzw. Iris): Zielfarbe mal Helligkeitsverlauf des Originals (Exponent 0,65),
//  - Blinzeln: im Augenband wird das Bild mit geschlossenen Augen eingeblendet,
//  - Atmen: die Person wächst um unten mittig herum um etwa 0,6 % (der Hintergrund bleibt stehen),
//  - Nicken: die Person sinkt kurz nach unten.
shader_type canvas_item;

uniform sampler2D geschlossen : filter_linear_mipmap, repeat_disable;
uniform sampler2D haarmaske : filter_linear_mipmap, repeat_disable;
uniform sampler2D augenmaske : filter_linear_mipmap, repeat_disable;
uniform bool hat_geschlossen = false;
uniform bool hat_haarmaske = false;
uniform bool hat_augenmaske = false;

uniform vec3 haarfarbe : source_color = vec3(0.4, 0.28, 0.15);
uniform vec3 augenfarbe : source_color = vec3(0.35, 0.22, 0.1);

// Blinzeln: 0 = offen, 1 = zu; das Augenband (von oben, Anteil der Bildhöhe), in dem das Bild mit geschlossenen Augen gilt
uniform float blinzeln : hint_range(0.0, 1.0) = 0.0;
uniform float augen_oben = 0.30;
uniform float augen_unten = 0.52;
// Atmen: Skalierung um den Punkt unten mittig (1,0 = ruhig), Nicken: Verschiebung in Bildhöhen (positiv = nach unten)
uniform float atmen = 1.0;
uniform float nicken = 0.0;

// Mittlere Helligkeit des Bildes im Bereich der Maske (aus PortraetAnzeige)
uniform float haar_mittel = 0.5;
uniform float augen_mittel = 0.5;

// Zielfarbe mal Helligkeitsverlauf des Originals (bezogen auf den Mittelwert im Bereich): Linien bleiben dunkel, Lichter hellen auf.
vec3 umfaerben(vec3 bild, vec3 ziel, float mittel) {
	float h = dot(bild, vec3(0.299, 0.587, 0.114));
	return clamp(ziel * pow(h / mittel, 0.65), 0.0, 1.0); // Exponent < 1: sonst bleiben helle Farben (Blond) zu dunkel, weil Linien und Schatten im Haar stark ins Gewicht fallen
}

void fragment() {
	// Atmen und Nicken: die Abtast-Koordinate verschieben (bei Vergrößerung >1 wird der Rand nach außen geschoben)
	vec2 anker = vec2(0.5, 1.0);
	vec2 uv = (UV - anker) / atmen + anker - vec2(0.0, nicken);
	vec4 p = texture(TEXTURE, uv);

	vec3 farbe = p.rgb;
	if (hat_haarmaske) {
		float m = smoothstep(0.05, 0.5, texture(haarmaske, uv).r);
		farbe = mix(farbe, umfaerben(farbe, haarfarbe, haar_mittel), m);
	}
	if (hat_augenmaske && blinzeln < 0.5) {
		float m = smoothstep(0.05, 0.5, texture(augenmaske, uv).r);
		farbe = mix(farbe, umfaerben(farbe, augenfarbe, augen_mittel), m);
	}
	COLOR = vec4(farbe, p.a) * COLOR;

	// Blinzeln zuletzt: das Bild mit geschlossenen Augen im Augenband einblenden. (Eine frühere Fassung tastete es vor dem Umfärben ab und
	// zeigte auf der T400 trotzdem das offene Bild; so, nach den Masken, kommt das richtige.)
	if (hat_geschlossen && blinzeln > 0.0) {
		float band = smoothstep(augen_oben - 0.03, augen_oben + 0.03, uv.y) * (1.0 - smoothstep(augen_unten - 0.03, augen_unten + 0.03, uv.y));
		vec4 z = texture(geschlossen, uv);
		COLOR = mix(COLOR, vec4(z.rgb, z.a) * vec4(1.0), band * blinzeln);
	}
}
