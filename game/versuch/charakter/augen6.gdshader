// Charakter Phase 6: Lederhaut und Iris für Stand 6 (eigener Code, MIT), Kopie von augen3.gdshader (bleibt für Stand 5).
// S2: Farben kommen als Color (sRGB, Godot rechnet nach linear um); die Lederhaut setzt charakter_kopf.gd (warm, etwas
// dunkler als in Phase 5), der Limbusring ist breiter und einstellbar (limbus, limbus_breite).
// Phase 3 (Herkunft): Lederhaut und Iris. Bilder: RoboPoets „digital_human“ (CC0, über
// tools/charakter/kopf3_backen.py umgewandelt): iris_muster (R = Fasern, G = innerer Kranz, B = Tiefe), lederhaut (Äderchen).
// UV der .glb (Phase 3): ebene Abbildung entlang der Blickachse; Iris: Limbus = Bildrand, Lederhaut: Augapfel = Bildrand.
// Eckfarbe R (Phase 2): Iris 2·R = Abstand von der Achse (1 = Limbus); äußere Kugel R = 1 im Bereich der Hornhaut.
// Den Glanz macht die Hornhaut darüber (hornhaut.gdshader); die Iris selbst ist matt.
shader_type spatial;
render_mode cull_disabled;   // Teile der ICT-Iris (Pupillenmulde) zeigen nach innen

uniform sampler2D iris_bild : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D lederhaut_bild : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform bool iris_teil = false;
// Etappe B (S7): sechs Irismuster als Bildarray (tools/charakter/kopf6_iris.py), Drehung je Person; ebene < 0 = iris_bild
uniform sampler2DArray iris_bilder6 : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform float iris_ebene = -1.0;
uniform float iris_drehung = 0.0;
uniform vec3 sklera : source_color = vec3(0.78, 0.73, 0.68);      // nicht reinweiß, warm (setzt charakter_kopf.gd)
uniform float adern = 0.35;
uniform vec3 iris_farbe : source_color = vec3(0.36, 0.22, 0.11);  // außen (aus den Genen)
uniform vec3 kranz_farbe : source_color = vec3(0.55, 0.36, 0.12); // innerer Kranz (bernstein bei grünen, braunen Augen)
uniform float kranz = 0.5;
uniform float limbus = 0.75;                 // Abdunkeln am Irisrand (dunkler Limbusring)
uniform float limbus_breite = 0.25;          // Breite des Rings (Anteil des Irishalbmessers)
uniform float pupille = 0.34;                // gemalte Pupille / Limbus (deckt die Öffnung im Netz)
const float PUPILLE_BILD = 0.31;             // Pupille im RoboPoets-Bild
uniform int pruefen = 0;                     // 1: Teil (rot Iris, grün Lederhaut), 2: Rückseite blau

void fragment() {
	float nv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec2 p = UV * 2.0 - 1.0;
	// Abstand von der Blickachse: Iris wie in Phase 2 aus der Eckfarbe (Pupille, Limbus mittig zur Öffnung im Netz),
	// Richtung für das Irisbild aus der UV; Lederhaut aus der UV
	float r = iris_teil ? COLOR.r * 2.0 : length(p);
	vec3 c;
	if (iris_teil) {
		// Fasern an der Pupille festhalten: Bildhalbmesser so umrechnen, dass die Pupillenkante passt
		float rb = mix(PUPILLE_BILD, 1.0, clamp((r - pupille) / (1.0 - pupille), 0.0, 1.0));
		float lp = length(p);
		vec2 q = lp > 1e-4 ? p / lp * rb : vec2(0.0);
		q = vec2(cos(iris_drehung) * q.x - sin(iris_drehung) * q.y, sin(iris_drehung) * q.x + cos(iris_drehung) * q.y);
		vec2 uvb = 0.5 + 0.5 * q;
		vec4 t = iris_ebene >= 0.0 ? texture(iris_bilder6, vec3(uvb, iris_ebene)) : texture(iris_bild, uvb);
		c = iris_farbe * (0.3 + 1.0 * t.r);
		float innen = 1.0 - smoothstep(0.35, 0.75, (r - pupille) / (1.0 - pupille));
		c = mix(c, kranz_farbe * (0.45 + 0.9 * t.r), clamp(t.g * kranz * innen * 1.4, 0.0, 1.0));
		c *= mix(1.0, 1.0 - limbus, smoothstep(1.0 - limbus_breite, 1.0, r));   // dunkler Limbusring
		c = mix(vec3(0.008), c, smoothstep(pupille - 0.015, pupille + 0.015, r));
		c = mix(c, sklera * 0.85, smoothstep(1.0, 1.08, r));               // außerhalb des Limbus (verdeckt)
		// Ecken hinter der Augapfelmitte (Eckfarbe R = 1): Das ICT-Auge hat die Pupille als Öffnung, durch die man
		// auf die Innenseite sieht – die ist schwarz (sonst erscheint dort eine helle Sichel)
		c = mix(c, vec3(0.004), smoothstep(0.97, 0.995, COLOR.r));
		ROUGHNESS = 0.45;
		SPECULAR = 0.2;
	} else {
		vec3 t = texture(lederhaut_bild, UV).rgb;
		float l = dot(t, vec3(0.333));
		vec3 ader = mix(vec3(1.0), t / max(l, 0.05) * l, adern);
		c = sklera * ader;
		// zum Rand hin rosiger und dunkler (Lidschatten, Verdeckung)
		c = mix(c * vec3(0.92, 0.74, 0.7), c, smoothstep(0.15, 0.7, nv));
		// Hornhautbereich der äußeren Kugel (Übergangsring): durchsichtig
		ALPHA = 1.0 - smoothstep(0.4, 0.6, COLOR.r);
		ALPHA_SCISSOR_THRESHOLD = 0.5;
		ROUGHNESS = 0.18;
		SPECULAR = 0.5;
	}
	if (pruefen == 1) {
		c = iris_teil ? vec3(1.0, 0.0, 0.0) * (0.3 + r * 0.7) : vec3(0.0, 1.0, 0.0);
	} else if (pruefen == 2) {
		c = FRONT_FACING ? vec3(1.0) : vec3(0.0, 0.0, 1.0);
	} else if (pruefen == 3) {
		c = vec3(length(p) * 0.5, COLOR.r, 0.0);
	}
	ALBEDO = c;
}
