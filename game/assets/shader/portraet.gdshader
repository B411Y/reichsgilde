// Die Person im Porträt (PortraetAnzeige): das freigestellte Brustbild, dazu
//  - Haar und Augen umgefärbt über Graustufen-Masken (weiß = Haar bzw. Iris); die Helligkeit des Bildes bleibt, nur der Farbton wechselt,
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
uniform vec2 augenband = vec2(0.20, 0.44);
// Atmen: Skalierung um den Punkt unten mittig (1,0 = ruhig), Nicken: Verschiebung in Bildhöhen (positiv = nach unten)
uniform float atmen = 1.0;
uniform float nicken = 0.0;

// Die Farbe des Ziels mit der Helligkeit des Bildes: dunkle Stellen (Strähnen, Schraffur) bleiben dunkel, Lichter werden heller.
vec3 umfaerben(vec3 bild, vec3 ziel) {
	float h = dot(bild, vec3(0.299, 0.587, 0.114));
	vec3 dunkel = ziel * (h * 2.0);
	vec3 hell = mix(ziel, vec3(1.0), (h - 0.5) * 2.0 * 0.55);
	return h < 0.5 ? dunkel : hell;
}

void fragment() {
	// Atmen und Nicken: die Abtast-Koordinate verschieben (bei Vergrößerung >1 wird der Rand nach außen geschoben)
	vec2 anker = vec2(0.5, 1.0);
	vec2 uv = (UV - anker) / atmen + anker - vec2(0.0, nicken);
	vec4 p = texture(TEXTURE, uv);

	if (hat_geschlossen && blinzeln > 0.0) {
		float band = smoothstep(augenband.x - 0.03, augenband.x + 0.03, uv.y) * (1.0 - smoothstep(augenband.y - 0.03, augenband.y + 0.03, uv.y));
		vec4 z = texture(geschlossen, uv);
		p = mix(p, z, band * blinzeln);
	}
	vec3 farbe = p.rgb;
	if (hat_haarmaske) {
		float m = texture(haarmaske, uv).r;
		farbe = mix(farbe, umfaerben(farbe, haarfarbe), m);
	}
	if (hat_augenmaske && blinzeln < 0.5) {
		float m = texture(augenmaske, uv).r;
		farbe = mix(farbe, umfaerben(farbe, augenfarbe), m);
	}
	COLOR = vec4(farbe, p.a) * COLOR;
}
