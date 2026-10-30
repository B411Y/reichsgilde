// Treibende Eisschollen (docs/wasser/phase3.md, Phase 3c): ein flacher Körper je Scholle, viele Kopien über einen MultiMesh (Schollenfeld.cs).
// Das Netz ist eine runde Scheibe in Ringen (Schollenfeld.ScheibenNetz: UV.x = Halbmesser 0–1, UV.y = Höhe in Dicken, 0 oben bis −1 unten,
// UV2.x = Rolle: 0 Oberseite, 0–1 gerundete Kante, 1 Seite); den Umriss jeder Scholle rechnet dieser Shader aus ihrem Startwert: ein
// unregelmäßiges Vieleck aus Bruchkanten, leicht zittrig, mit gerundeter Oberkante. Die Scholle schaukelt und kippt mit denselben Wellen wie
// die Wasserfläche (wasser_wellen.gdshaderinc, gleiche Uniforms). Aussehen: Oberseite matt weiß mit Reif- und Schneeflecken und klaren,
// dunklen Stellen; der Rand nass und glänzend; die Stirn durchscheinend blaugrün (unten satter: Dicke), Fresnel-Spiegelung des Himmels, Licht
// von hinten scheint durch (BACKLIGHT). Mit MIT_BLASEN (Hoch, Ultra) Luftblasen in zwei Tiefen, die sich mit dem Blick gegeneinander
// verschieben, und Schlieren in der Stirn.
//
// Je Kopie (Schollenfeld): COLOR = (Startwert der Form, Dicke in m, Schnee 0–1, frei), INSTANCE_CUSTOM = (quer, Stelle, Fließrichtung x, z) –
// der Ort in den Laufachsen des Flusses für die Wellen. Die Lage (MODEL_MATRIX) ist gleichmäßig skaliert: Skalierung = Halbmesser in m.
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shader/wasser_wellen.gdshaderinc"

global uniform float wetter_schnee;    // Schneedecke 0–1
global uniform vec3 wetter_himmel;     // Himmelsfarbe (linear)

uniform float freibord = 0.12;         // Anteil der Dicke über Wasser (Eis schwimmt zu knapp neun Zehnteln unter Wasser)
uniform vec3 eis_hell : source_color = vec3(0.76, 0.82, 0.85);
uniform vec3 eis_dunkel : source_color = vec3(0.36, 0.44, 0.48);
uniform vec3 eis_stirn : source_color = vec3(0.22, 0.42, 0.44); // durchscheinende Seite (blaugrün, grau)
uniform vec3 schlamm : source_color = vec3(0.47, 0.44, 0.37);    // eingefrorener Schwebstoff (Flusseis ist selten rein)

varying vec3 lokal;        // Ort auf der Scholle in m (x, Höhe, z): dreht und treibt mit
varying vec2 muster;       // Versatz der Muster je Scholle
varying float rolle;       // 0 Oberseite … 1 Seite
varying float tiefe_rel;   // Seite: 0 oben … 1 unten
varying float randnah;     // Oberseite: 0 Mitte … 1 Rand
varying float schnee_s;
#ifdef MIT_BLASEN
varying vec3 blick_lokal;  // Richtung zur Kamera in den Achsen der Scholle
#endif

// Umriss der Scholle: das Innere von fünf bis neun Halbebenen (Bruchkanten) in unregelmäßigen Winkeln und Abständen, dazu je Scholle eine
// Streckung (länglich) und bei gut der Hälfte ein ausgebrochenes Stück (eine Bucht im Umriss). Liefert den Halbmesser in Richtung th (in
// Halbmessern der Scholle) und die Außennormale der Kante dort.
float umriss(float th, float saat, out vec2 n_aussen) {
	float r = 1.4;
	n_aussen = vec2(cos(th), sin(th));
	float kanten = 5.0 + floor(w_hash(vec2(saat, 91.0)) * 5.0);
	for (int i = 0; i < 9; i++) {
		float fi = float(i);
		if (fi >= kanten) {
			break;
		}
		float phi = (fi + 0.95 * (w_hash(vec2(saat, fi * 7.0 + 3.0)) - 0.5)) * 6.2831853 / kanten;
		float d = 0.52 + 0.48 * w_hash(vec2(saat, fi * 7.0 + 11.0));
		float c = cos(th - phi);
		if (c > 0.1) {
			float ri = d / c;
			if (ri < r) {
				r = ri;
				n_aussen = vec2(cos(phi), sin(phi));
			}
		}
	}
	// länglich: bis 40 % gestreckt entlang einer zufälligen Achse
	float streck = 0.4 * w_hash(vec2(saat, 57.0));
	float achse = w_hash(vec2(saat, 63.0)) * 6.2831853;
	r *= (1.0 + streck * cos(2.0 * (th - achse))) / (1.0 + streck * 0.6); // bleibt etwa im Halbmesser (die Verteilung rechnet damit)
	// ausgebrochenes Stück: eine Bucht im Umriss (nicht mehr konvex)
	float bucht = w_hash(vec2(saat, 71.0));
	if (bucht > 0.5) {
		float wo = w_hash(vec2(saat, 77.0)) * 6.2831853;
		float dw = atan(sin(th - wo), cos(th - wo));
		dw *= dw < 0.0 ? 1.8 : 0.9; // schief ausgebrochen, kein Herz
		r *= 1.0 - 0.28 * (bucht - 0.5) / 0.5 * exp(-dw * dw * 14.0);
	}
	return r;
}

void vertex() {
	float saat = COLOR.r;
	float dicke = COLOR.g;
	schnee_s = COLOR.b;
	mat3 m = mat3(MODEL_MATRIX);
	float halbmesser = max(length(m[0]), 0.01);
	mat3 dreh = m / halbmesser;          // Drehung um die Hochachse
	mat3 zurueck = transpose(dreh);
	muster = vec2(w_hash(vec2(saat, 1.0)), w_hash(vec2(saat, 2.0))) * 97.0;

	// Form: Richtung des Netzpunktes, Umriss, Höhe
	float rho = UV.x;
	float h = UV.y;
	rolle = UV2.x;
	float th = atan(VERTEX.z, VERTEX.x);
	vec2 n_o;
	float r = umriss(th, saat, n_o);
	// zackiger Rand: zwei Lagen Rauschen über den Winkel (abgebrochene Ecken)
	r *= 1.0 + 0.12 * (w_rausch(vec2(th * 2.6 + saat * 0.13, saat * 0.71)) - 0.5) + 0.08 * (w_rausch(vec2(th * 7.0 + saat * 0.29, saat * 0.37)) - 0.5);
	vec2 xz = vec2(cos(th), sin(th)) * rho * r;
	float oben = freibord * dicke + 0.006;
	// Oberseite buckelig (Schnee, angefrorener Brei); bei einem Teil der Schollen ein aufgeworfener Rand (Schollen stoßen aneinander: Pfannkucheneis)
	float wulst = step(0.55, w_hash(vec2(saat, 83.0))) * 0.035 * smoothstep(0.55, 0.85, rho) * (1.0 - smoothstep(0.88, 1.0, rho));
	float buckel = ((w_rausch(xz * halbmesser * 0.8 + muster) - 0.5) * 0.035 + (1.0 - rho * rho) * 0.015) * (1.0 - smoothstep(0.75, 1.0, rho)) + wulst;
	float y = oben + h * dicke + (rolle < 0.01 ? buckel : 0.0);
	lokal = vec3(xz.x * halbmesser, y, xz.y * halbmesser);
	tiefe_rel = clamp(-h, 0.0, 1.0);
	randnah = rho * r;
	vec3 n_aussen = vec3(n_o.x, 0.0, n_o.y);
	vec3 n = rolle < 0.99 ? normalize(mix(vec3(0.0, 1.0, 0.0), n_aussen, rolle)) : normalize(n_aussen + vec3(0.0, 0.1, 0.0));
	vec3 v = vec3(xz.x, y / halbmesser, xz.y);

	// Wellen am Ort der Scholle: Höhe, Versatz und Neigung wie die Wasserfläche dort (große Schollen schaukeln träger)
	vec3 mitte = MODEL_MATRIX[3].xyz;
	float sturm;
	vec2 wind;
	wellen_wind(sturm, wind);
	float staerke = wellen_staerke * wellen_windfaktor(sturm);
	vec3 t_w;
	vec3 b_w;
	vec3 n_w;
	vec3 welle = wellen_welt(mitte.xz, INSTANCE_CUSTOM.xy, INSTANCE_CUSTOM.zw, staerke, wind, t_w, b_w, n_w);
	float traeg = 1.0 / (1.0 + halbmesser / 3.0);
	vec3 n_k = normalize(mix(vec3(0.0, 1.0, 0.0), n_w, traeg));
	vec3 achse = cross(vec3(0.0, 1.0, 0.0), n_k);
	float sn = length(achse);
	mat3 kipp = mat3(1.0);
	if (sn > 1e-5) {
		vec3 k = achse / sn;
		mat3 kk = mat3(vec3(0.0, k.z, -k.y), vec3(-k.z, 0.0, k.x), vec3(k.y, -k.x, 0.0));
		kipp = mat3(1.0) + sn * kk + (1.0 - n_k.y) * kk * kk;
	}
	v = zurueck * (kipp * (dreh * v));
	n = zurueck * (kipp * (dreh * n));
	v += zurueck * (vec3(welle.x * traeg, welle.y * mix(0.5, 1.0, traeg), welle.z * traeg) / halbmesser);
	VERTEX = v;
	NORMAL = n;
#ifdef MIT_BLASEN
	vec3 welt = (MODEL_MATRIX * vec4(v, 1.0)).xyz;
	blick_lokal = zurueck * normalize(CAMERA_POSITION_WORLD - welt);
#endif
}

void fragment() {
	vec2 p = lokal.xz + muster;
	vec3 farbe;
	float rau;
	float spec = 0.5;
	vec3 durch;
	float schnee = 0.0;
	if (rolle < 0.99) {
		// Oberseite: weißes, mattes Eis mit Wolken, klare (dunkle, glänzende) Stellen, Reif und Schnee; nasser Rand
		float wolke = w_fbm(p * 0.9);
		vec3 weiss = mix(eis_dunkel * 1.15, eis_hell, smoothstep(0.2, 0.8, wolke));
		// Schwebstoff im Eis: bräunlich-graue Schlieren, nicht auf jeder Scholle
		float dreck = smoothstep(0.55, 0.85, w_fbm(p * vec2(0.4, 1.3) + 17.0)) * w_hash(vec2(muster.x, 5.0));
		weiss = mix(weiss, schlamm * 0.85, dreck * 0.5);
		float klar = smoothstep(0.5, 0.7, w_fbm(p * 0.55 + 5.0));
		vec3 klarfarbe = eis_stirn * 0.38 + vec3(0.015);
#ifdef MIT_BLASEN
		// Luftblasen in zwei Tiefen: mit dem Blick gegeneinander verschoben (wirkt wie Tiefe im Eis)
		vec2 vb = blick_lokal.xz / max(blick_lokal.y, 0.25);
		float b1 = smoothstep(0.78, 0.94, w_rausch((p - vb * 0.03) * 7.0));
		float b2 = smoothstep(0.8, 0.95, w_rausch((p - vb * 0.08) * 4.3 + 3.0));
		klarfarbe = mix(klarfarbe, eis_hell * 0.9, b1 * 0.45 + b2 * 0.3);
#endif
		farbe = mix(weiss, klarfarbe, klar * 0.75);
		float reif = smoothstep(0.55, 0.8, w_rausch(p * 3.1 + 2.0)) * (1.0 - klar);
		farbe = mix(farbe, eis_hell * 1.08, reif * 0.35);
		schnee = clamp((wetter_schnee * 0.85 + schnee_s) * smoothstep(0.3, 0.62, w_fbm(p * 0.6 + 9.0) + 0.05 - 0.3 * klar), 0.0, 1.0);
		schnee *= 1.0 - smoothstep(0.8, 0.95, randnah); // am Rand spült Wasser den Schnee weg
		farbe = mix(farbe, vec3(0.93, 0.95, 0.97), schnee);
		// Nasser Rand: Wasser schwappt über die Kante – dunkler, mit Glanz
		float nass = max(smoothstep(0.88, 0.99, randnah), rolle);
		farbe = mix(farbe, farbe * 0.55 + eis_stirn * 0.12, nass * 0.85);
		rau = mix(mix(0.55, 0.18, klar), 0.85, schnee);
		rau = mix(rau, 0.08, nass);
		spec = mix(0.45, 0.6, nass);
		durch = eis_stirn * mix(0.3, 0.6, nass) * (1.0 - schnee);
	} else {
		// Stirn: durchscheinend blaugrün, oben heller (Luft, Bruch), nach unten satter (die Dicke färbt), mit senkrechten Schlieren
		farbe = mix(eis_hell * 0.55 + eis_stirn * 0.2, eis_stirn * 0.6, smoothstep(0.0, 0.3, tiefe_rel));
#ifdef MIT_BLASEN
		float schliere = w_rausch(vec2(atan(lokal.z, lokal.x) * 9.0 + muster.x, tiefe_rel * 1.8));
		farbe *= 0.85 + 0.3 * schliere;
#endif
		rau = 0.07;
		spec = 0.6;
		durch = eis_stirn * 0.9;
	}
	// Spiegelung des Himmels (Fresnel), wie beim Wasser: die Stadt hat keinen Himmel zum Spiegeln
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALBEDO = farbe;
	ROUGHNESS = rau;
	SPECULAR = spec;
	BACKLIGHT = durch;
	// Die Stirn leuchtet blaugrün aus dem Eis heraus (Licht, das im Eis gestreut wird): auch bei bedecktem Himmel, wie Glas an der Kante
	float leuchten = rolle < 0.99 ? rolle * 0.6 : 1.0;
	EMISSION = wetter_himmel * fresnel * 0.45 * (1.0 - rau) + eis_stirn * wetter_himmel * (0.06 + 0.32 * leuchten);
}
