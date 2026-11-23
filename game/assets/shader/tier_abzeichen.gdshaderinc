// Abzeichen des Zugpferds (Stern, Blesse, Schnippe, weiße Fesseln/Socken, Maulpartie) als zweite Schicht über der Albedo; gemeinsam für tier_vat.gdshader (fern)
// und tier_skelett.gdshader (nah). Masken im UV des Fells (tools/modelle/pferd2_abzeichen.py):
//   abzeichen_kopf  RGBA: R Stern, G Blesse, B Schnippe, A Maulpartie (weich)
//   abzeichen_beine L:    (Beinnummer + Höhe/0,5 m) / 4, ohne Filter gelesen (0 LF, 1 RF, 2 LH, 3 RH)
// Code je Pferd (Stadtansicht.Zugtiere.cs, fester Hash aus Karren und Tier, nur Darstellung):
//   kopf + 6 * (lf + 3 * (rf + 3 * (lh + 3 * rh))) + 486 * maul
//   kopf 0 keins, 1 Stern, 2 Blesse, 3 Schnippe, 4 Stern+Schnippe, 5 Blesse+Schnippe; Bein 0 nichts, 1 Krone/Fessel, 2 Socke; maul 0 wie Fell, 1 dunkler, 2 heller
uniform bool hat_abzeichen = false;
uniform sampler2D abzeichen_kopf : hint_default_black, filter_linear_mipmap, repeat_disable;
uniform sampler2D abzeichen_beine : hint_default_black, filter_nearest, repeat_disable;

const vec3 ABZEICHEN_WEISS = vec3(0.84, 0.81, 0.75);   // sRGB 236/232/224, leicht warm
const vec3 ABZEICHEN_MEHL = vec3(0.58, 0.41, 0.22);    // sRGB 200/172/128, Mehlmaul

vec3 abzeichen(vec3 albedo, vec2 uv, float code_f) {
	int code = int(round(code_f));
	if (!hat_abzeichen || code <= 0) {
		return albedo;
	}
	int kopf = code % 6;
	int beine = (code / 6) % 81;
	int maul = (code / 486) % 3;
	vec4 k = texture(abzeichen_kopf, uv);
	vec3 farbe = albedo;
	if (maul == 1) {
		farbe = farbe * (1.0 - 0.90 * k.a);   // linear 0,10 = sRGB etwa 0,35 (wie die Vorschau in pferd2_abzeichen.py)
	} else if (maul == 2) {
		farbe = mix(farbe, ABZEICHEN_MEHL, 0.65 * k.a);
	}
	float weiss = 0.0;
	if (kopf == 1 || kopf == 4) weiss = max(weiss, k.r);
	if (kopf == 2 || kopf == 5) weiss = max(weiss, k.g);
	if (kopf >= 3) weiss = max(weiss, k.b);
	float b = texture(abzeichen_beine, uv).r;
	if (b > 0.0) {
		float x = b * 4.0;
		int bein = int(floor(x + 1e-4));
		float h = x - float(bein);
		int art = (beine / int(pow(3.0, float(bein)) + 0.5)) % 3;
		float grenze = art == 1 ? 0.36 : (art == 2 ? 0.70 : 0.0);
		if (h < grenze) weiss = 1.0;
	}
	return mix(farbe, ABZEICHEN_WEISS, weiss);
}
