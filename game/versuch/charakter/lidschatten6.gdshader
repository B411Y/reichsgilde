// Charakter Etappe D: Lidschatten auf dem Augapfel (ICT „EyeOcclusion“-Netz, eigener Code, MIT; Prüferbefund „Augen
// glasig“): Schatten des Oberlids (oben breit und weich, etwa ein Drittel der Lidspalte) und Kontaktschatten an beiden
// Lidkanten, multipliziert. Eckfarbe (kopf2_bauen.py): G = Oberlid-Seite 0..1, B = Abstand zum sichtbaren Lidrand
// (ICT-Merkpunkte 36–47) / 8 mm. Beide Seiten sichtbar (lidschatten.gdshader zeichnet nur die Vorderseite – bei diesem
// Netz kam davon fast nichts an). Der Stoff zeichnet nach der Hornhaut (render_priority 1) und dunkelt deren Spiegelung
// mit ab: das Lid hält dort das Licht der Umgebung ab.
shader_type spatial;
render_mode blend_mul, unshaded, depth_draw_never, cull_disabled, shadows_disabled;

uniform float oben = 0.55;           // Schatten des Oberlids (Stärke)
uniform float oben_weite = 0.4;      // bis zu diesem Abstand zum Lidrand (Anteil von 8 mm, also 3,2 mm)
uniform float kontakt = 0.55;        // Kontaktschatten an der Lidkante
uniform float kontakt_weite = 0.1;   // (Anteil von 8 mm)
uniform vec3 d_farbe : source_color = vec3(0.40, 0.31, 0.28);
uniform int pruefen = 0;             // 1: Netz grün, 2: Eckfarbe G (grün) und B × 3 (blau)

void fragment() {
	float d = COLOR.b;
	float o = smoothstep(0.25, 0.75, COLOR.g);
	float s_oben = oben * o * pow(1.0 - smoothstep(0.0, oben_weite, d), 1.5);
	float s_k = kontakt * (1.0 - smoothstep(0.0, kontakt_weite, d)) * mix(0.6, 1.0, o);
	float s = 1.0 - (1.0 - s_oben) * (1.0 - s_k);
	ALBEDO = mix(vec3(1.0), d_farbe, clamp(s, 0.0, 1.0));
	if (pruefen == 1) { ALBEDO = vec3(0.3, 1.0, 0.3); }
	if (pruefen == 2) { ALBEDO = vec3(0.0, COLOR.g, COLOR.b * 3.0); }
}
