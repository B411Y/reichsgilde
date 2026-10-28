// Die Person im Porträt (PortraetAnzeige): das freigestellte Brustbild, dazu
//  - Haar und Augen umgefärbt über Graustufen-Masken (weiß = Haar bzw. Iris): Zielfarbe mal Helligkeitsverlauf des Originals (Exponent 0,65),
//  - Blinzeln: um die Augen herum wird das Bild mit geschlossenen Augen eingeblendet (ebenfalls umgefärbt, im Bauwerkzeug farblich angeglichen),
//  - Atmen: die Person wächst um unten mittig herum um etwa 0,6 % (der Hintergrund bleibt stehen),
//  - Nicken: die Person sinkt kurz nach unten.
shader_type canvas_item;

// Beim Eintritt in fragment() ist COLOR schon Bild x Modulate (Godot 4); der Shader multipliziert sein Ergebnis damit, also ist das Ergebnis Umfärbung x Bild.
// Beim Blinzeln muss dafür das Bild mit geschlossenen Augen eingehen, nicht das offene: sonst scheinen Iris und Lider des offenen Bildes durch.
varying vec4 modulat;
void vertex() {
	modulat = COLOR;
}

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

// Farbe der Person an dieser Stelle: Haar und Iris umgefärbt (gilt für das offene wie das geschlossene Bild, sonst blitzt beim Blinzeln die alte Haarfarbe auf)
vec3 person_farbe(vec3 farbe, vec2 uv, bool iris) {
	if (hat_haarmaske) {
		float m = smoothstep(0.05, 0.5, texture(haarmaske, uv).r);
		farbe = mix(farbe, umfaerben(farbe, haarfarbe, haar_mittel), m);
	}
	if (iris && hat_augenmaske) {
		float m = smoothstep(0.05, 0.5, texture(augenmaske, uv).r);
		farbe = mix(farbe, umfaerben(farbe, augenfarbe, augen_mittel), m);
	}
	return farbe;
}

// Wo beim Blinzeln das Bild mit geschlossenen Augen gilt: eine weiche Ellipse um jedes Auge (aus der Augenmaske in PortraetAnzeige.Augenzonen: x, y, Halbachsen),
// nicht das ganze Augenband – sonst flackert die fein abweichende Haarschraffur der beiden Bilder. Ohne Augenmaske das Augenband von oben bis unten.
uniform vec4 auge_links = vec4(0.0);
uniform vec4 auge_rechts = vec4(0.0);

float ellipse(vec2 uv, vec4 a) {
	vec2 r = max(a.zw, vec2(0.0001));
	return (1.0 - smoothstep(0.75, 1.0, length((uv - a.xy) / r))) * step(0.0001, a.z);
}

float lidzone(vec2 uv) {
	float band = smoothstep(augen_oben - 0.03, augen_oben + 0.03, uv.y) * (1.0 - smoothstep(augen_unten - 0.03, augen_unten + 0.03, uv.y));
	float augen = max(ellipse(uv, auge_links), ellipse(uv, auge_rechts));
	return (hat_augenmaske && (auge_links.z > 0.0 || auge_rechts.z > 0.0)) ? augen : band;
}

void fragment() {
	// Atmen und Nicken: die Abtast-Koordinate verschieben (bei Vergrößerung >1 wird der Rand nach außen geschoben)
	vec2 anker = vec2(0.5, 1.0);
	vec2 uv = (UV - anker) / atmen + anker - vec2(0.0, nicken);
	vec4 p = texture(TEXTURE, uv);
	vec4 z = texture(geschlossen, uv); // immer abtasten, nicht erst im if: sonst lieferte der Treiber (T400) das offene Bild

	vec3 farbe = person_farbe(p.rgb, uv, true);
	// Blinzeln: das (farblich angeglichene, ebenso umgefärbte) Bild mit geschlossenen Augen im Augenbereich überblenden; der Lidschlag
	// ist kurz, deshalb eine etwas steilere Kurve statt eines trägen Überblendens.
	if (hat_geschlossen && blinzeln > 0.0) {
		vec3 zf = person_farbe(z.rgb, uv, false);
		float t = smoothstep(0.25, 0.75, blinzeln) * lidzone(uv);
		farbe = farbe * (1.0 - t) + zf * t;
		p = p * (1.0 - t) + z * t;
	}
	COLOR = vec4(farbe, p.a) * p * modulat;
}
