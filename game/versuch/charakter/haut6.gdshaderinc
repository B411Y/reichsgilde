// Charakter Phase 6: Haut für Stand 6 (eigener Code, MIT). Kopie von haut3.gdshaderinc (Phase 3–5; bleibt unverändert
// für Stand 5) mit diesen Änderungen:
//   S2 Glanz wie Godots GGX: kein zweites / PI (π steckt schon in D), F0 fest 0,028 (Haut, IOR 1,4), Fresnel bis 1,
//      Sichtbarkeit V_SmithGGXCorrelated (Godots V_GGX), SPECULAR_AMOUNT nur als Lichtfaktor. Die Glanzmaske (Höhlen ×
//      Falten × Haar) erreicht als varying spec_v auch das direkte Licht; SPECULAR = 0,42 · spec_v ergibt F0 0,028 für
//      das Umgebungslicht. „glanz“ ist jetzt ein Faktor auf diesen Glanz (1 = Haut), „keule2“ der Anteil der schmalen
//      Keule (0 = eine Keule, Prüfkugel). Untergrenze der Rauheit 0,10 (genutzt erst über die Feuchtemaske, S8).
//   S2 Ultra (haut6_ultra.gdshader, #define ULTRA): render_mode sss_mode_skin (Hautkern statt des grauen
//      Allzweck-Kerns), der Wrap × 0,35 (die Streuung übernimmt dort Godots SSS). Zweig für Flächenlichter
//      (LIGHT_IS_AREA, Godot 4.7) nach dem Beispiel der Doku.
//   S3 Wrap energieerhaltend (n·l + w)/(1 + w)², Flaum 0,16 -> 0,05 (kommt in S8 als Glanz zurück), Streulicht
//      jenseits des Schattenrands nur noch zu 30 % ohne Schatten der eigenen Rundung (streu_schattenfrei 1 -> 0,3).
// Halt B2, Weg A (haut6a.gdshader, #define WEG_A; Diagnose „Puppe“: gleichförmiger Albedo, kein Bartschatten, Brauen
//   wie Aufkleber, Glanz auf dem Nasenrücken): Zonenkarte zonen_tex (tools/charakter/kopf6_zonen.py; R Rötung von Wangen,
//   Nase, Ohren, Kinn, G Schatten unter den Augen, B Feuchte am Lidrand und Karunkel, A Nasenrücken), Bartstoppeln als
//   feine Punkte aus der Porenkachel, Brauenschatten auf der Haut weich (Mip-Stufe) und schwächer, Poren-AO auf dem
//   Albedo, Streulicht je Kanal (Rot geglättet, Grün und Blau mit Poren: nach MatMADNESS „HumanShaders“, MIT,
//   github.com/matmadness/HumanShaders, skin_shader.gdshader), Feuchte und Nasenrücken steuern Rauheit und Glanz.
// Etappe C, Haut (haut6c.gdshader, #define WEG_A und WEG_C; Befund des blinden Prüfers zu B2: glatte, gleichförmig rosa
//   Haut, Glanzstreifen auf Nase und unter dem Auge, orange Nasenlöcher): eigene Karten aus tools/charakter/kopf6_hautc.py
//   (c_zonen: Hämoglobin, Sonne/Gelb, Augenringe, Bart; c_glanz: Rauheit je Zone, Poren, Höhle, Feuchte; c_relief: Falten
//   in vier Alterskanälen) und Detailkacheln aus kopf6_kacheln.py (c_poren 25 mm: Poren, Hautfelderung, Stoppeln;
//   c_flecken 60 mm: Melanin- und Blutunruhe, Sommersprossen, Altersflecken). Farbe als Absorption (Pigmentmodell nach
//   Plan S9, vereinfacht): albedo = Grundton · Hautbild^Kontrast · exp(−Σ Pigment · k). Rauheit je Zone statt der
//   T-Zonen-Maske, Doppelglanz breiter (× 1,3 und × 0,75, Mischung 0,85), Nasenlöcher dunkel ohne Streulicht.
//   Die alten Falten-, Rötungs- und Unruhe-Wege bleiben für Stand 6 und Weg A unverändert. EINFACH (haut6c_einfach,
//   Stufen Niedrig/Mittel): ohne Detailkacheln.
// Farben kommen als Color (sRGB, Godot rechnet nach linear um); Faktoren wie „toenung“ bleiben vec3.
// Phase 3 (Herkunft): gebaut auf dem Vortest A2 der Phase 0 (haut.gdshader).
// Bilder (tools/charakter/kopf3_backen.py, 2048², linear):
//   farbe_tex   RGB = Hautbild / mittlere Hautfarbe × 0,5 (der Ton kommt aus den Genen: grundfarbe), A = Brauenschatten
//   normal_tex  RG = Normale (Tangentenraum), B = Porenstärke für die feine Kachel (poren)
//   masken_tex  R = Rötung, G = Glanz, B = Verdeckung, A = Bartschatten (Bereich)
//   falten_tex  Höhen der Falten (0,5 = keine): R Stirn, G Augen, B Mund, A Hals und Lippen; je Kanal ein Gewicht aus dem Alter
// Licht: Wrap je Farbkanal (Rot streut am weitesten) mit geglätteter Normale für das Streulicht und voller Normale für den
// Glanz; Randlicht (Flaum); Durchlicht an Ohren und Nase; zwei Glanzkeulen. Echtes Subsurface Scattering (Bildschirm,
// teuer) nur in der Ultra-Fassung (haut3_ultra.gdshader, #define ULTRA).
// Der Schatten des Schlüssellichts gilt im Streulichtbereich jenseits des Schattenrands nicht: Dort verdeckt die eigene
// Rundung (Schattenkarte) – das ergab in Phase 2 den „dunklen Fleck vor dem Ohr“ (Schatten × Wrap).
#ifdef ULTRA
render_mode cull_back, depth_draw_opaque, sss_mode_skin;
#else
render_mode cull_back, depth_draw_opaque;
#endif

uniform sampler2D farbe_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D masken_tex : hint_default_black, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D falten_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D poren : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 grundfarbe : source_color = vec3(0.78, 0.6, 0.5);
uniform vec3 toenung = vec3(1.0);            // je Volk und Gen „Hautton“
uniform float poren_dichte = 60.0;           // Wiederholungen der feinen Kachel je UV-Einheit
uniform float poren_staerke = 0.45;
uniform float normal_staerke = 1.0;          // gebackene Normale (Poren der Nase, Lippenlinien)
uniform float rauheit = 0.56;
uniform float rauheit_glanz = 0.4;           // T-Zone, Lippen
uniform float glanz = 1.0;                   // Faktor auf den Hautglanz (F0 0,028); 1 = Haut
uniform float keule2 = 0.18;                 // Anteil der schmalen Glanzkeule (Rauheit × 0,55); 0 = eine Keule
uniform float roetung = 0.32;
uniform vec3 roetung_farbe = vec3(1.0, 0.76, 0.72);
uniform float hoehlen = 0.6;                 // Verdeckung in Furchen, Nasenlöchern, Ohren
uniform vec4 falten = vec4(0.0);             // Gewicht je Kanal (Alter): Stirn, Augen, Mund, Hals/Lippen
uniform float falten_tiefe = 2.0;            // Neigung der Normale je Höheneinheit und Bildpunkt
uniform float falten_dunkel = 0.12;          // Furchen etwas dunkler
uniform float bart = 0.0;                    // Bartschatten (Stoppeln unter der Haut), 0..1
uniform vec3 bart_farbe = vec3(0.72, 0.74, 0.8);
uniform float brauen_dichte = 0.0;           // Brauenschatten auf der Haut
uniform vec3 haar_farbe : source_color = vec3(0.12, 0.08, 0.05);
uniform float streu = 0.4;                   // Wrap-Breite für Rot (Grün, Blau schmaler)
uniform vec3 streu_anteil = vec3(1.0, 0.5, 0.36);
uniform float wrap_neutral = 1.0;            // Etappe-A-Nachtrag: 1 = im vollen Licht farbneutral (siehe light())
uniform float streu_farbe = 0.3;             // Etappe-A-Nachtrag: Anteil der Farbtrennung des Wraps (0 = grauer Wrap, 1 = volle)
uniform float streu_schattenfrei = 0.3;      // 1 = Streulicht jenseits des Schattenrands ohne Schatten der eigenen Rundung
uniform float flaum = 0.05;                  // Randlicht (S8: als Glanz)
uniform float durchlicht = 0.35;             // Ohren und Nase im Gegenlicht
uniform float sss_staerke = 0.25;            // nur ULTRA
uniform float unruhe = 0.0;                  // Phase 5: ungleichmäßiger Hautton der Erwachsenen (gegen „Porzellan“)
// Phase 4: Haut unter Haar und Bart (tools/charakter/kopf4_haar.py, Spiel-UV 1024²). Die Kopfhaut unter dichtem Haar
// nimmt die Haarfarbe an (Schatten und Haaransatz: kein heller Schimmer durch das Haar, kein Helmrand); wo eine
// Glatze ist (Rang < kahl), bleibt Haut.
uniform sampler2D kopfhaut_tex : hint_default_black, filter_linear_mipmap, repeat_disable;   // Dichte je Frisur (Kanal)
uniform sampler2D rang_tex : hint_default_white, filter_linear_mipmap, repeat_disable;       // A = Rang beim Ausfallen
uniform vec4 kopfhaut_kanal = vec4(0.0);
uniform float kahl = 0.0;
uniform float duenn_haut = 0.0;
uniform sampler2D bart_tex : hint_default_black, filter_linear_mipmap, repeat_disable;       // Dichte je Bartform
uniform vec4 bart_kanal = vec4(0.0);
uniform vec3 haar_unter : source_color = vec3(0.1, 0.07, 0.05);
uniform float kopfhaut_dunkel = 0.8;

#ifdef WEG_A
uniform sampler2D zonen_tex : hint_default_black, filter_linear_mipmap, repeat_disable;
uniform float zonen_roetung = 0.5;
uniform vec3 zonen_rot = vec3(1.0, 0.7, 0.66);         // Faktor auf den Hautton in den Rötungszonen
uniform float augenschatten = 0.4;
uniform vec3 augenschatten_farbe = vec3(0.8, 0.68, 0.7);
uniform float feucht = 1.0;
uniform float ruecken_matt = 0.7;
uniform float stoppeln = 0.7;                          // Bartstoppeln (nur mit Bartschatten „bart“)
uniform float stoppel_dichte = 2.4;                    // × poren_dichte
uniform float poren_ao = 0.1;
uniform float brauen_weich = 0.3;                      // Brauenschatten auf der Haut (Phase 5: 0,55, hart)
uniform float brauen_glanz = 0.0;                      // Etappe D: Hautglanz unter der Braue weg (funkelte durch die Haare)
uniform float streu_poren = 0.5;                       // Grün und Blau: Anteil der Porennormale im Streulicht
varying vec3 n_diffus_gb;
#endif
#ifdef WEG_C
uniform sampler2D c_zonen : hint_default_black, filter_linear_mipmap, repeat_disable;
uniform sampler2D c_glanz : hint_default_black, filter_linear_mipmap, repeat_disable;
uniform sampler2D c_relief : hint_default_black, filter_linear_mipmap, repeat_disable;
uniform sampler2D c_poren : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D c_flecken : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 relief_bp = vec4(1.0);          // Maßstab je Kanal (haut_c.json): Höhe in Bildpunkt-Einheiten bei Wert 1
uniform vec4 relief_gewicht = vec4(0.0);     // Alter: Stirn, Augen, Mittelform, Mund/Hals
uniform float relief_staerke = 1.0;
uniform float c_bildpunkt_mm = 0.24;         // Bildpunktgröße der Reliefkarte im Gesicht (Furchentiefe in mm)
uniform float c_poren_dichte = 20.0;         // Wiederholungen der Porenkachel (25 mm) je UV-Einheit
uniform float c_flecken_dichte = 8.0;        // … der Fleckenkachel (60 mm)
uniform vec2 c_versatz = vec2(0.0);          // je Person (Samen)
uniform float c_poren_staerke = 0.6;         // Porenstärke (× Zone)
uniform float c_hoehle_albedo = 0.1;         // Porenboden dunkler
uniform float c_kontrast = 1.4;              // Mittelfrequenz des Hautbilds verstärken
uniform float c_hb = 0.35;                   // Hämoglobin in den Zonen
uniform float c_sonne = 0.2;                 // Melanin in den Sonnenzonen
uniform float c_braeune = 0.0;               // Melanin überall (Wetterhaut)
uniform float c_gelb = 0.4;                  // Gelbstich Stirn und Schläfe
uniform float c_gelb_alle = 0.0;             // Gelbstich überall (Runde 2: Haut zu rosa-pfirsich gegen die Fotos)
uniform float c_lippen_weich = 0.0;          // Farbkanten des Hautbilds (Lippenrand) weicher: Farbton aus gröberer Mip-Stufe
uniform float c_ringe = 0.5;                 // Augenringe, Venen
uniform float c_bart = 0.0;                  // Bartschatten (Fläche)
uniform vec3 c_bart_farbe = vec3(0.72, 0.76, 0.82);
uniform float c_stoppeln = 0.0;
uniform float c_unruhe_eu = 0.18;
uniform float c_unruhe_hb = 0.18;
uniform float c_sommersprossen = 0.0;        // Dichte (Rang-Schwelle) 0..1
uniform float c_spross_staerke = 0.9;
uniform float c_altersflecken = 0.0;
uniform float c_rau = 0.0;                   // Rauheit dazu (Alter, Wetter)
uniform float c_loch = 0.8;                  // Nasenloch dunkel
uniform float c_furche_dunkel = 0.25;        // Furchen dunkler (Falten lesen sich über die Verschattung)
uniform float c_uneben = 0.35;               // grobe Lage der Porenkachel (× 2,7 größer): Unebenheit 1–2 mm
uniform int c_zeige = 0;                     // Prüfanzeige: 1 Höhle, 2 Glanzkarte, 3 Zonen, 4 Furche, 5 Bart/Sprossen
const vec3 K_EU = vec3(0.405, 0.572, 1.0);   // Eumelanin (Plan S9)
const vec3 K_HB = vec3(0.037, 1.0, 0.795);   // Hämoglobin
const vec3 K_RING = vec3(0.2, 0.26, 0.34);   // Augenringe: dunkler, bräunlich (Runde 2: nicht violett)
const vec3 K_GELB = vec3(0.0, 0.06, 0.3);    // Gelbstich
const vec3 K_SPROSS = vec3(0.45, 0.7, 1.0);  // Sommersprossen (hellbraun)
const vec3 K_ALT = vec3(0.8, 0.9, 1.05);     // Altersflecken (graubraun, kaum orange)
#endif
varying vec3 n_diffus;
varying float duenn;
varying float hoehle_v;
varying float spec_v;                        // Glanzmaske (Höhlen, Falten, Haar) × glanz, für das direkte Licht
const float F0_HAUT = 0.028;                 // Haut, IOR 1,4

// GGX wie Godot (scene_forward_lights_inc.glsl): D_GGX und V_GGX (Smith, korreliert, Näherung nach Hammon)
float d_ggx(float nh, float a) {
	float x = nh * a;
	float k = a / (1.0 - nh * nh + x * x);
	return k * k / PI;
}
float v_ggx(float nl, float nv, float a) {
	return 0.5 / mix(2.0 * nl * nv, nl + nv, a);
}

void fragment() {
	vec4 f = texture(farbe_tex, UV);
	vec4 m = texture(masken_tex, UV);
	vec3 nt = texture(normal_tex, UV).rgb;
#ifdef WEG_C
	vec4 zc = texture(c_zonen, UV);
	vec4 gl = texture(c_glanz, UV);
	// Relief: Höhe und Gefälle in Bildpunkt-Einheiten (drei Abtastungen), Gewicht je Alterskanal
	vec2 er = 1.0 / vec2(textureSize(c_relief, 0));
	vec4 rw = relief_bp * relief_gewicht * relief_staerke;
	vec4 rw_f = rw * vec4(1.0, 1.0, 0.35, 1.0);     // Furchenschatten vor allem aus den Alterskanälen
	float h0 = dot(texture(c_relief, UV) * 2.0 - 1.0, rw);
	float hu = dot(texture(c_relief, UV + vec2(er.x, 0.0)) * 2.0 - 1.0, rw);
	float hv = dot(texture(c_relief, UV + vec2(0.0, er.y)) * 2.0 - 1.0, rw);
	vec2 fal = vec2(-(hu - h0), hv - h0);
	float furche = clamp(-dot(texture(c_relief, UV) * 2.0 - 1.0, rw_f) * c_bildpunkt_mm / 0.3, 0.0, 1.0);
#ifdef EINFACH
	vec4 pt = vec4(0.5, 0.5, 0.0, 0.0);
	vec4 pt2 = pt;
	vec4 fl1 = vec4(0.5, 0.5, 1.0, 0.0);
	vec4 fl2 = fl1;
	vec4 fl3 = fl1;
#else
	vec4 pt = texture(c_poren, UV * c_poren_dichte + c_versatz);
	vec4 pt2 = texture(c_poren, mat2(vec2(0.6, -0.8), vec2(0.8, 0.6)) * UV * (c_poren_dichte * 0.37) + c_versatz.yx * 3.1);
	vec4 fl1 = texture(c_flecken, UV * c_flecken_dichte + c_versatz.yx);
	vec4 fl2 = texture(c_flecken, mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * UV * (c_flecken_dichte * 0.41) + c_versatz * 1.7 + 0.37);
	vec4 fl3 = texture(c_flecken, UV * (c_flecken_dichte * 0.33) + c_versatz * 2.3 + 0.61);
#endif
	// Grundton × Hautbild mit verstärkter Mittelfrequenz
	vec3 fb = max(f.rgb * 2.0, vec3(0.0));
	// Farbkanten weicher (Lippenrand): Helligkeit scharf, Farbton aus Mip 2
	vec3 fw = max(textureLod(farbe_tex, UV, 2.0).rgb * 2.0, vec3(1e-3));
	float l_s = dot(fb, vec3(0.2126, 0.7152, 0.0722));
	fb = mix(fb, fw * (l_s / max(dot(fw, vec3(0.2126, 0.7152, 0.0722)), 1e-3)), c_lippen_weich);
	vec3 c = grundfarbe * toenung * pow(fb, vec3(c_kontrast));
	// Pigmente als Absorption: Melanin (Sonne, Unruhe in zwei Weiten, Flecken), Hämoglobin, Augenringe, Gelbstich
	float eu = c_braeune + zc.g * c_sonne + (fl1.r - 0.5) * 2.0 * c_unruhe_eu + (fl2.g - 0.5) * 1.6 * c_unruhe_eu;
	float hb = zc.r * c_hb + (fl1.g - 0.5) * 2.0 * c_unruhe_hb + (fl2.r - 0.5) * 1.2 * c_unruhe_hb;
	float spross = fl1.a * step(fl1.b, c_sommersprossen * smoothstep(0.05, 0.6, zc.g + 0.7 * zc.r));
	// Altersflecken in zwei Größen, gehäuft wo die grobe Melanin-Unruhe hoch ist (nicht als gleichmäßiges Muster)
	float haeufung = smoothstep(0.35, 0.7, fl2.g);
	float altfl = fl3.a * step(fract(fl3.b + 0.43), c_altersflecken * zc.g * haeufung * 1.6)
		+ 0.7 * fl2.a * step(fract(fl2.b + 0.71), c_altersflecken * 0.6 * zc.g * haeufung);
	c *= exp(-(eu * K_EU + hb * K_HB + zc.b * c_ringe * K_RING + (zc.g * c_gelb + c_gelb_alle) * K_GELB
		+ spross * c_spross_staerke * K_SPROSS + altfl * 0.45 * K_ALT));
	// Bart: kühler Schatten der Haare unter der Haut, dazu Stoppeln (Striche der Porenkachel)
	c *= mix(vec3(1.0), c_bart_farbe, clamp(zc.a * c_bart, 0.0, 1.0));
	// Stoppeln: feine Striche und eine gröbere Lage; Runde 3 (Prüfer: „Dreitagebart nur ein Hauch“): zusätzlich mit
	// Mip-Versatz 1,5 abgetastet und geschwellt – die Striche werden im Porträt zu sichtbaren Punkten
#ifdef EINFACH
	float stoppel = 0.0;
#else
	float stoppel = max(smoothstep(0.03, 0.2, texture(c_poren, UV * c_poren_dichte + c_versatz, 1.5).a),
		0.7 * smoothstep(0.03, 0.2, texture(c_poren, mat2(vec2(0.6, -0.8), vec2(0.8, 0.6)) * UV * (c_poren_dichte * 0.37) + c_versatz.yx * 3.1, 1.0).a));
	stoppel = max(stoppel, pt.a + 0.6 * pt2.a);
#endif
	c = mix(c, haar_farbe * 0.55, clamp(stoppel * zc.a * c_stoppeln, 0.0, 0.85));
	// Brauenschatten weich (wie Weg A)
	float braue_a = textureLod(farbe_tex, UV, 2.5).a;
	c = mix(c, haar_farbe * 0.8, clamp(braue_a * brauen_dichte * brauen_weich, 0.0, 0.5));
	// Furchen, Porenböden, Höhlen (Nasenloch innen dunkel)
	float hl = gl.b;                         // neue Höhlenkarte (Nasenloch innen, Ohr); die alte Verdeckung nur im AO
	c *= 1.0 - c_furche_dunkel * furche;
	c *= 1.0 - c_hoehle_albedo * (pt.b + 0.5 * c_uneben * pt2.b) * gl.g;
	c *= 1.0 - hoehlen * 0.5 * m.b;
	c *= 1.0 - c_loch * gl.b;
	// Lippenrot (Glanzkarte A ≈ 0,3): weniger lachsfarben (Runde 3)
	float lippe = smoothstep(0.12, 0.26, gl.a) * (1.0 - smoothstep(0.36, 0.44, gl.a));
	c = mix(c, vec3(dot(c, vec3(0.2126, 0.7152, 0.0722))) * vec3(1.12, 0.94, 0.9), 0.3 * lippe);
	float unter = dot(texture(kopfhaut_tex, UV), kopfhaut_kanal);
	unter *= smoothstep(kahl - 0.05, kahl + 0.05, texture(rang_tex, UV).a) * (1.0 - 0.5 * duenn_haut);
	float unter_bart = clamp(dot(texture(bart_tex, UV), bart_kanal), 0.0, 1.0);
	float haarig = clamp(max(unter, unter_bart), 0.0, 1.0);
	c = mix(c, haar_unter * mix(0.55, 0.85, haarig), haarig * kopfhaut_dunkel);
	ALBEDO = c;
	if (c_zeige == 1) { ALBEDO = vec3(hl); }
	if (c_zeige == 2) { ALBEDO = gl.rgb; }
	if (c_zeige == 3) { ALBEDO = zc.rgb; }
	if (c_zeige == 4) { ALBEDO = vec3(furche); }
	if (c_zeige == 5) { ALBEDO = vec3(zc.a * c_bart, spross, altfl); }
	vec2 meso = (nt.xy * 2.0 - 1.0) * normal_staerke;
	vec2 mikro = ((pt.xy * 2.0 - 1.0) + (pt2.xy * 2.0 - 1.0) * c_uneben) * 1.2 * c_poren_staerke * gl.g;
	vec3 n = normalize(vec3(meso + mikro + fal, 1.0));
	NORMAL_MAP = n * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;
	vec3 n_glatt = normalize(vec3(meso * 0.5 + fal * 0.6, 1.0));
	n_diffus = normalize(TANGENT * n_glatt.x + BINORMAL * n_glatt.y + NORMAL * n_glatt.z);
	vec3 n_gb = normalize(vec3(meso * 0.5 + fal * 0.8 + mikro * streu_poren, 1.0));
	n_diffus_gb = normalize(TANGENT * n_gb.x + BINORMAL * n_gb.y + NORMAL * n_gb.z);
	duenn = clamp(m.r * 1.2 - 0.25, 0.0, 1.0) * (1.0 - hl);
	hoehle_v = hl;
	float rand = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	float nass = smoothstep(0.45, 0.95, gl.a) * feucht;
	float r = 0.25 + 0.4 * gl.r + c_rau + 0.05 * pt.b * gl.g + 0.08 * furche + 0.1 * rand + 0.3 * haarig;
	ROUGHNESS = clamp(mix(r, 0.15, nass), 0.10, 1.0);
	spec_v = glanz * (1.0 - 0.85 * hl) * (1.0 - 0.4 * furche) * (1.0 - 0.4 * pt.b * gl.g) * (1.0 - 0.7 * haarig) * (1.0 + 0.8 * nass)
		* (1.0 - brauen_glanz * clamp(braue_a * 2.0, 0.0, 1.0));
	SPECULAR = 0.42 * spec_v;
	AO = 1.0 - max(0.8 * hoehlen * m.b, 0.9 * hl);
	AO_LIGHT_AFFECT = mix(0.3, 1.0, hl);
#ifdef ULTRA
	SSS_STRENGTH = sss_staerke;
#endif
#else
	// Falten: Höhe und Gefälle (drei Abtastungen)
	vec2 e = 1.0 / vec2(textureSize(falten_tex, 0));
	vec4 g = falten;
	float h0 = dot(texture(falten_tex, UV).rgba * 2.0 - 1.0, g);
	float hu = dot(texture(falten_tex, UV + vec2(e.x, 0.0)).rgba * 2.0 - 1.0, g);
	float hv = dot(texture(falten_tex, UV + vec2(0.0, e.y)).rgba * 2.0 - 1.0, g);
	// Farbe: Ton aus den Genen × Hautbild, Rötung, Bart, Braue, Furchen
	vec3 c = grundfarbe * toenung * f.rgb * 2.0;
	c *= mix(vec3(1.0), roetung_farbe, clamp(m.r * roetung, 0.0, 1.0));
	// Phase 5: Unruhe im Hautton (grob und fein, aus der Porenkachel; nach Rot und Gelb verschoben, nicht nur heller)
	float fl = (texture(poren, UV * 3.1).x - 0.5) * 1.4 + (texture(poren, UV * 11.7 + 0.37).y - 0.5) * 0.8;
	c *= vec3(1.0) + unruhe * fl * vec3(0.8, 1.05, 1.3);
	c = mix(c, c * bart_farbe, clamp(m.a * bart, 0.0, 1.0));
#ifdef WEG_A
	vec4 zo = texture(zonen_tex, UV);
	c *= mix(vec3(1.0), zonen_rot, clamp(zo.r * zonen_roetung, 0.0, 1.0));
	c *= mix(vec3(1.0), augenschatten_farbe, clamp(zo.g * augenschatten, 0.0, 1.0));
	// Bartstoppeln: einzelne dunkle Punkte (Haarschäfte unter der Haut) aus einer feinen Lage der Porenkachel
	float stoppel = smoothstep(0.58, 0.78, texture(poren, UV * poren_dichte * stoppel_dichte + vec2(0.31, 0.77)).x);
	c = mix(c, haar_farbe * 0.55, clamp(stoppel * m.a * bart * stoppeln, 0.0, 0.75));
	// Brauenschatten weich (gröbere Mip-Stufe) und schwächer: keine Kante unter den Haarkarten
	float braue_a = textureLod(farbe_tex, UV, 2.5).a;
	c = mix(c, haar_farbe * 0.8, clamp(braue_a * brauen_dichte * brauen_weich, 0.0, 0.5));
#else
	c = mix(c, haar_farbe * 0.8, clamp(f.a * brauen_dichte * 0.55, 0.0, 0.7));
#endif
	c *= 1.0 - falten_dunkel * clamp(-h0, 0.0, 1.0);
	c *= 1.0 - hoehlen * 0.5 * m.b;
	// Phase 4: unter Haar und Bart
	float unter = dot(texture(kopfhaut_tex, UV), kopfhaut_kanal);
	unter *= smoothstep(kahl - 0.05, kahl + 0.05, texture(rang_tex, UV).a) * (1.0 - 0.5 * duenn_haut);
	float unter_bart = clamp(dot(texture(bart_tex, UV), bart_kanal), 0.0, 1.0);
	float haarig = clamp(max(unter, unter_bart), 0.0, 1.0);
	c = mix(c, haar_unter * mix(0.55, 0.85, haarig), haarig * kopfhaut_dunkel);
	// Normale: gebacken (Mittel) + Falten + feine Kachel (Poren), im Tangentenraum zusammen
	vec2 meso = (nt.xy * 2.0 - 1.0) * normal_staerke;
	vec2 mikro = (texture(poren, UV * poren_dichte).xy * 2.0 - 1.0) * poren_staerke * (0.35 + nt.b);
#ifdef WEG_A
	c *= 1.0 - poren_ao * clamp(length(mikro) * 2.5 - 0.25, 0.0, 1.0);    // Porenböden etwas dunkler (gegen Porzellan)
#endif
	ALBEDO = c;
	// Tangentenraum: +x = +u, +y = Bild oben = -v (Godot-UV)
	vec2 fal = vec2(-(hu - h0), (hv - h0)) * falten_tiefe;
	vec3 n = normalize(vec3(meso + mikro + fal, 1.0));
	NORMAL_MAP = n * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 1.0;
	// Streulicht mit geglätteter Normale (halb Geometrie, halb Falten, ohne Poren)
	vec3 n_glatt = normalize(vec3(meso * 0.5 + fal * 0.6, 1.0));
	n_diffus = normalize(TANGENT * n_glatt.x + BINORMAL * n_glatt.y + NORMAL * n_glatt.z);
#ifdef WEG_A
	// gleiche Mittelform wie Rot (sonst färbt eine Kante der gebackenen Normale an der Kachelgrenze), nur die Poren dazu
	vec3 n_gb = normalize(vec3(meso * 0.5 + fal * 0.6 + mikro * streu_poren, 1.0));
	n_diffus_gb = normalize(TANGENT * n_gb.x + BINORMAL * n_gb.y + NORMAL * n_gb.z);
#endif
	duenn = clamp(m.r * 1.2 - 0.25, 0.0, 1.0) * (1.0 - m.b);
	hoehle_v = m.b;
	float pore = clamp(length(mikro) * 2.0, 0.0, 1.0);
	float rand = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ROUGHNESS = clamp(mix(rauheit, rauheit_glanz, m.g) + 0.1 * pore + 0.12 * rand + 0.15 * clamp(-h0, 0.0, 1.0) + 0.3 * haarig, 0.10, 1.0);
	spec_v = glanz * (1.0 - 0.7 * hoehlen * m.b) * (1.0 - 0.5 * clamp(-h0, 0.0, 1.0)) * (1.0 - 0.7 * haarig);
#ifdef WEG_A
	// feuchter Lidrand und Karunkel: glatt und glänzend; Nasenrücken: matter
	float nass = smoothstep(0.45, 0.95, zo.b) * feucht;    // nur der schmale Saum, nicht die Umgebung
	ROUGHNESS = clamp(mix(ROUGHNESS, 0.16, nass) + 0.2 * zo.a * ruecken_matt, 0.10, 1.0);
	spec_v *= (1.0 + 0.8 * nass) * (1.0 - 0.5 * zo.a * ruecken_matt);
#endif
	SPECULAR = 0.42 * spec_v;                // Godot: F0 = 0,16 · SPECULAR² = 0,028 (Umgebungslicht)
	AO = 1.0 - hoehlen * m.b * 0.8;
	AO_LIGHT_AFFECT = 0.3;
#ifdef ULTRA
	SSS_STRENGTH = sss_staerke;
#endif
#endif
}

void light() {
	if (LIGHT_IS_AREA) {
		// Flächenlicht (Godot 4.7): Godot rechnet die Fläche (LTC); hier nur Streulicht und Glanzmaske (Beispiel der Doku)
#ifdef WEG_C
		// Etappe C: auch das Flächenlicht erreicht das Nasenloch innen nicht (sonst glüht es orange)
		DIFFUSE_LIGHT += LIGHT_AREA_DIFFUSE_MULTIPLIER * ATTENUATION * LIGHT_COLOR * (1.0 - 0.75 * smoothstep(0.15, 0.85, hoehle_v));
#else
		DIFFUSE_LIGHT += LIGHT_AREA_DIFFUSE_MULTIPLIER * ATTENUATION * LIGHT_COLOR;
#endif
		SPECULAR_LIGHT += LIGHT_AREA_SPECULAR_MULTIPLIER * ATTENUATION * LIGHT_COLOR * SPECULAR_AMOUNT * spec_v;
	} else {
		float ndl = dot(NORMAL, LIGHT);
		float ndl_d = dot(n_diffus, LIGHT);
#ifdef WEG_A
		float ndl_gb = dot(n_diffus_gb, LIGHT);   // Grün und Blau sehen die Poren (Rot streut weiter)
#else
		float ndl_gb = ndl_d;
#endif
		// in Höhlen (Nasenlöcher, Ohrmuschel, Mundwinkel) kaum Streulicht
		vec3 w = streu * streu_anteil * (1.0 - 0.7 * hoehle_v);
#ifdef ULTRA
		w *= 0.35;                           // die Streuung macht unter Ultra Godots SSS (Hautkern)
#endif
		// S3: energieerhaltend (McAuley 2011, wie Godots DIFFUSE_LAMBERT_WRAP)
		vec3 ndl3 = vec3(ndl_d, ndl_gb, ndl_gb);
		vec3 wrap_e = clamp((ndl3 + w) / ((1.0 + w) * (1.0 + w)), 0.0, 1.0);
		// Etappe-A-Nachtrag (Prüferbefund „Haut fahl“, „Oberlid orange“): je Kanal (n·l + w)/(1 + w)² nahm dem Rot im
		// vollen Licht bis zu 17 % (Wange R/G 1,22 -> 1,11) und gab es am Schattenrand im Übermaß zurück (Oberlid C 24).
		// Neutral: (n·l + w)/(1 + w) je Kanal, geteilt durch (1 + w) der Helligkeit – im vollen Licht alle Kanäle gleich,
		// die Energie bleibt in der Helligkeit erhalten
		float w_hell = dot(w, vec3(0.2126, 0.7152, 0.0722));
		vec3 wrap_n = clamp((ndl3 + w) / ((1.0 + w) * (1.0 + w_hell)), 0.0, 1.0);
		vec3 wrap = mix(wrap_e, wrap_n, wrap_neutral);
		float ndl_hell = dot(ndl3, vec3(0.2126, 0.7152, 0.0722));
		float wrap_grau = clamp((ndl_hell + w_hell) / ((1.0 + w_hell) * (1.0 + w_hell)), 0.0, 1.0);
		wrap = vec3(wrap_grau) + streu_farbe * (wrap - vec3(dot(wrap, vec3(0.2126, 0.7152, 0.0722))));
		float schatten = ATTENUATION;
		if (LIGHT_IS_DIRECTIONAL) {
			float frei = streu_schattenfrei * (1.0 - hoehle_v);
			schatten = mix(mix(1.0, ATTENUATION, 1.0 - frei), ATTENUATION, smoothstep(-0.05, 0.3, ndl_d));
		}
		vec3 diff = wrap * schatten;
		// Flaum: Randlicht, nur wo Licht hinkommt
		float nv = max(dot(NORMAL, VIEW), 1e-4);
		float umriss = pow(1.0 - nv, 4.0) * smoothstep(-0.3, 0.4, ndl);
		diff += vec3(umriss * flaum * 3.0) * schatten;
		// Durchlicht: Licht von hinten durch Ohren und Nasenflügel (rötlich)
		float hinten = pow(clamp(dot(VIEW, -LIGHT), 0.0, 1.0), 3.0) * clamp(-ndl_d + 0.3, 0.0, 1.0);
		diff += vec3(1.0, 0.35, 0.22) * hinten * duenn * durchlicht;
#ifdef WEG_C
		// Nasenloch, Gehörgang: kein direktes Licht, auch kein Randlicht (die Innenwände sieht man streifend)
		diff *= 1.0 - 0.75 * smoothstep(0.15, 0.85, hoehle_v);
#endif
		DIFFUSE_LIGHT += diff * LIGHT_COLOR / PI;   // Lambert: LIGHT_COLOR enthält π
		// Glanz: zwei GGX-Keulen (Rauheit r und 0,55 r) wie Godot, Schlick-Fresnel mit F0 0,028 bis 1, ohne / PI
		float hart = max(ndl, 0.0);
		vec3 h = normalize(VIEW + LIGHT);
		float nh = clamp(dot(NORMAL, h), 0.0, 1.0);
		float lh = clamp(dot(LIGHT, h), 0.0, 1.0);
		float f = F0_HAUT + (1.0 - F0_HAUT) * pow(1.0 - lh, 5.0);
#ifdef WEG_C
		// Doppelglanz breiter (Plan S8): × 1,3 und × 0,75, Mischung 0,85 – kein harter Streifen
		float r1 = min(ROUGHNESS * 1.3, 1.0);
		float a1 = r1 * r1;
		float r2 = ROUGHNESS * 0.75;
		float a2 = r2 * r2;
		float spec = 0.85 * d_ggx(nh, a1) * v_ggx(hart, nv, a1) + 0.15 * d_ggx(nh, a2) * v_ggx(hart, nv, a2);
#else
		float a1 = ROUGHNESS * ROUGHNESS;
		float r2 = ROUGHNESS * 0.55;
		float a2 = r2 * r2;
		float spec = (1.0 - keule2) * d_ggx(nh, a1) * v_ggx(hart, nv, a1) + keule2 * d_ggx(nh, a2) * v_ggx(hart, nv, a2);
#endif
		SPECULAR_LIGHT += hart * spec * f * spec_v * ATTENUATION * LIGHT_COLOR * SPECULAR_AMOUNT;
	}
}
