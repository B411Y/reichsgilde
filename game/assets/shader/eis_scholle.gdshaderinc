// Treibende Eisschollen (docs/wasser/phase3.md, Phase 3c): ein flacher Körper je Scholle, viele Kopien über einen MultiMesh (Schollenfeld.cs).
// Das Netz ist eine runde Scheibe in Ringen (Schollenfeld.ScheibenNetz: UV.x = Halbmesser 0–1, UV.y = Höhe in Dicken, 0 oben bis −1 unten,
// UV2.x = Rolle: 0 Oberseite, 0–1 gerundete Kante, 1 Seite, 2 Mitte des Bodens; UV2.y = Ecken je Ring); den Umriss jeder Scholle rechnet dieser
// Shader aus ihrem Startwert: ein unregelmäßiges Vieleck aus Bruchkanten, leicht zittrig, mit gerundeter Oberkante. Die Scholle schaukelt und
// kippt mit denselben Wellen wie die Wasserfläche (wasser_wellen.gdshaderinc, gleiche Uniforms), als starrer Körper. Aussehen: Oberseite aus dem
// gemeinsamen Eismaterial; der Rand nass und glänzend; die Stirn durchscheinend blaugrün (unten satter: Dicke), Fresnel-Spiegelung des Himmels,
// Licht von hinten scheint durch (BACKLIGHT). Mit MIT_BLASEN (Hoch, Ultra) zusätzlich Luftblasen in der Tiefe des klaren Eises, die sich mit dem
// Blick gegen die obere Lage verschieben, und Schlieren in der Stirn.
//
// Phase 3d: Die Oberseite kommt aus dem gemeinsamen Eismaterial (eis_material.gdshaderinc) – dasselbe wie beim festen Eis auf dem Wasser,
// mit Relief aus der Normalkarte. Dieselben Körper liegen auch festgefroren im Randeis der Flüsse (Schollenfeld: feste Schollen): Sie schaukeln
// nicht, und sie wachsen auf, sobald das Eis der Art (eis_fest) ihre Schwelle erreicht.
//
// Schollen 2: Bruchstücke sind Ausschnitte [α, β] derselben Scholle um den Ursprung ihres Netzes – Ecken außerhalb des Ausschnitts legt der
// Shader auf den nächsten Bruchstrahl (alle Ringe einer Ecke auf denselben Punkt, von der Kante bis zum Ursprung gestaucht); so zeichnet die
// Seitenwand genau die Bruchkante, glasig wie die Stirn, ohne Schnee daneben, und zwei Stücke liegen genau aneinander. Schmelzende Schollen
// (Schmelzgrad m) haben runde Ecken (weiches Minimum über die Bruchkanten), weniger Zacken, liegen tiefer im Wasser und sind grauer und nasser.
// Der Umriss muss genau so rechnen wie Schollensimulation.Umriss (C#), sonst passen Stoßkreise und Bruchkanten nicht zur Zeichnung.
//
// Je Kopie (Schollenfeld): COLOR = (Startwert der Form, Dicke in m, treibend: Ende des Ausschnitts β + 8 oder 0 = ganz / fest: Schnee 0–1,
// treibend: −Schmelzgrad / fest: Schwelle > 0 = fest im Randeis ab diesem Eisfortschritt), INSTANCE_CUSTOM = (quer, Stelle, Winkel der
// Fließrichtung, Anfang des Ausschnitts α + 8 oder 0) – der Ort in den Laufachsen des Flusses für die Wellen. Die Lage (MODEL_MATRIX) ist
// gleichmäßig skaliert: Skalierung = Halbmesser in m.
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shader/wasser_wellen.gdshaderinc"
#include "res://assets/shader/eis_material.gdshaderinc"

global uniform float wetter_schnee;    // Schneedecke 0–1
global uniform vec3 wetter_himmel;     // Himmelsfarbe (linear)

uniform float freibord = 0.12;         // Anteil der Dicke über Wasser (Eis schwimmt zu knapp neun Zehnteln unter Wasser)
uniform float eis_fest = 1.0;          // Eisfortschritt der Art: feste Schollen erscheinen, wenn er ihre Schwelle (COLOR.a) übersteigt

const float SEKTOR_VERSATZ = 8.0;      // Schollenfeld.SektorVersatz

varying vec3 lokal;        // Ort auf der Scholle in m (x, Höhe, z): dreht und treibt mit
varying vec2 muster;       // Versatz der Muster je Scholle
varying float rolle;       // 0 Oberseite … 1 Seite
varying float tiefe_rel;   // Seite: 0 oben … 1 unten
varying float randnah;     // Oberseite: 0 Mitte … 1 Rand
varying float schnee_s;
varying float fest_v;      // 1 = feste Scholle im Randeis
varying float schmelz_v;   // Schmelzgrad 0–1 (treibende)
varying flat vec3 sektor_v; // Bruchstück: (α, β, 1); ganze Scholle: (0, 0, 0)
#ifdef MIT_BLASEN
varying vec3 blick_lokal;  // Richtung zur Kamera in den Achsen der Scholle
#endif

// Umriss der Scholle: das Innere von fünf bis neun Halbebenen (Bruchkanten) in unregelmäßigen Winkeln und Abständen, dazu je Scholle eine
// Streckung (länglich) und bei gut der Hälfte ein ausgebrochenes Stück (eine Bucht im Umriss). Beim Schmelzen (m > 0) ein weiches Minimum über
// die Halbebenen: die Ecken runden sich. Liefert den Halbmesser in Richtung th (in Halbmessern der Scholle) und die Außennormale der Kante dort.
float umriss(float th, float saat, float m, out vec2 n_aussen) {
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
	if (m > 0.0) {
		// weiches Minimum (log-sum-exp um das harte): Schärfe von etwa 400 (frisch) auf 8 (morsch), Normale gewichtet gemittelt
		float k = 8.0 + 400.0 * pow(1.0 - m, 8.0);
		float summe = exp(-k * (1.4 - r));
		vec2 n_w = vec2(cos(th), sin(th)) * summe;
		for (int i = 0; i < 9; i++) {
			float fi = float(i);
			if (fi >= kanten) {
				break;
			}
			float phi = (fi + 0.95 * (w_hash(vec2(saat, fi * 7.0 + 3.0)) - 0.5)) * 6.2831853 / kanten;
			float d = 0.52 + 0.48 * w_hash(vec2(saat, fi * 7.0 + 11.0));
			float c = cos(th - phi);
			if (c > 0.1) {
				float g = exp(-k * (d / c - r));
				summe += g;
				n_w += vec2(cos(phi), sin(phi)) * g;
			}
		}
		r -= log(summe) / k;
		n_aussen = normalize(n_w);
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

// Umriss mit zackigem Rand (zwei Lagen Rauschen über den Winkel: abgebrochene Ecken; beim Schmelzen glatter)
float umriss_zackig(float th, float saat, float m, out vec2 n_o) {
	float r = umriss(th, saat, m, n_o);
	return r * (1.0 + (1.0 - 0.7 * m) * (0.12 * (w_rausch(vec2(th * 2.6 + saat * 0.13, saat * 0.71)) - 0.5)
		+ 0.08 * (w_rausch(vec2(th * 7.0 + saat * 0.29, saat * 0.37)) - 0.5)));
}

// Punkt des Umrisses an der Ecke k des Netzes (Winkel wie die Ecke im Netz: atan der Lage)
vec2 umriss_ecke(float k, float schritt, float saat, float m) {
	float w = k * schritt;
	float th = atan(sin(w), cos(w));
	vec2 n_o;
	return vec2(cos(th), sin(th)) * umriss_zackig(th, saat, m, n_o);
}

// Wo der Bruchstrahl im Winkel a die Kante zwischen den Ecken davor und danach schneidet (Einheit Halbmesser)
vec2 strahl_ende(float a, float schritt, float saat, float m) {
	float k0 = floor(a / schritt);
	vec2 p0 = umriss_ecke(k0, schritt, saat, m);
	vec2 p1 = umriss_ecke(k0 + 1.0, schritt, saat, m);
	vec2 d = vec2(cos(a), sin(a));
	vec2 e = p1 - p0;
	float nenner = d.x * e.y - d.y * e.x;
	float t = abs(nenner) > 1e-6 ? -(d.x * p0.y - d.y * p0.x) / nenner : 0.5;
	return p0 + e * clamp(t, 0.0, 1.0);
}

void vertex() {
	float saat = COLOR.r;
	float dicke = COLOR.g;
	float fest = COLOR.a;
	bool treibt = fest <= 0.0;
	float m = treibt ? clamp(-fest, 0.0, 1.0) : 0.0;
	schmelz_v = m;
	// Schnee: feste Schollen aus der Kopie, treibende aus der Saat (dort trägt COLOR.b den Ausschnitt)
	schnee_s = treibt ? w_hash(vec2(saat, 5.0)) * 0.35 : COLOR.b;
	bool stueck = treibt && COLOR.b > 0.5;
	float sek_a = INSTANCE_CUSTOM.w - SEKTOR_VERSATZ;
	float sek_b = COLOR.b - SEKTOR_VERSATZ;
	sektor_v = stueck ? vec3(sek_a, sek_b, 1.0) : vec3(0.0);
	// Feste Scholle im Randeis: wächst auf, wenn das Eis sie erreicht (vorher ein Punkt ohne Fläche)
	float da = fest > 0.0 ? smoothstep(fest, fest + 0.05, eis_fest) : 1.0;
	// Eisgang: Das Randeis bricht auf, die festen Schollen vergehen eine nach der anderen (bei Tauwetter 0,9 alle)
	if (fest > 0.0) {
		float weg = smoothstep(0.1, 0.9, eis_tauen) * 1.06;
		float hf = w_hash(vec2(saat, 101.0));
		da *= 1.0 - smoothstep(hf * 0.94, hf * 0.94 + 0.06, weg);
	}
	fest_v = fest > 0.0 ? 1.0 : 0.0;
	mat3 mm = mat3(MODEL_MATRIX);
	float halbmesser = max(length(mm[0]), 0.01);
	mat3 dreh = mm / halbmesser;          // Drehung (um die Hochachse; beim Tauchen gekippt)
	mat3 zurueck = transpose(dreh);
	muster = vec2(w_hash(vec2(saat, 1.0)), w_hash(vec2(saat, 2.0))) * 97.0;

	// Form: Richtung des Netzpunktes, Umriss, Höhe
	float rho = UV.x;
	float h = UV.y;
	rolle = UV2.x;
	float teile = max(UV2.y, 3.0);
	float th = atan(VERTEX.z, VERTEX.x);
	vec2 n_o;
	float r = umriss_zackig(th, saat, m, n_o);
	vec2 xz = vec2(cos(th), sin(th)) * rho * r;
	bool versetzt = false;
	vec3 n_schnitt = vec3(0.0, 1.0, 0.0);
	if (rolle > 1.5) {
		// Mitte des Bodens
		xz = vec2(0.0);
	} else if (stueck) {
		// Bruchstück: Ecken außerhalb [α, β] auf den nächsten Bruchstrahl, alle Ringe einer Ecke auf denselben Punkt
		float schritt = TAU / teile;
		float k = mod(floor(mod(th, TAU) / schritt + 0.5), teile);
		float spanne = mod(sek_b - sek_a, TAU);
		float rel = mod(k * schritt - sek_a, TAU);
		if (rel > spanne + 1e-4 && rel < TAU - 1e-4) {
			float innen = floor((sek_a + spanne) / schritt + 1e-4) - ceil(sek_a / schritt - 1e-4) + 1.0;
			float aussen = teile - innen;
			float rel_erste = ceil((sek_a + spanne) / schritt + 1e-4) * schritt - sek_a;
			float j = floor((rel - rel_erste) / schritt + 0.5);
			float n_b = ceil(aussen * 0.5);
			float n_a = max(aussen - n_b, 1.0);
			float strahl;
			float t;
			if (j < n_b) {
				strahl = sek_b;
				t = j / n_b;
				n_schnitt = vec3(-sin(sek_b), 0.0, cos(sek_b));
			} else {
				strahl = sek_a;
				t = (aussen - 1.0 - j) / n_a;
				n_schnitt = vec3(sin(sek_a), 0.0, -cos(sek_a));
			}
			xz = strahl_ende(strahl, schritt, saat, m) * (1.0 - clamp(t, 0.0, 1.0));
			versetzt = true;
		}
	}
	float oben = freibord * (1.0 - 0.5 * m) * dicke + 0.006; // morsches Eis liegt tiefer
	// Oberseite buckelig (Schnee, angefrorener Brei); bei einem Teil der Schollen ein aufgeworfener Rand (Schollen stoßen aneinander: Pfannkucheneis)
	float wulst = step(0.55, w_hash(vec2(saat, 83.0))) * 0.035 * smoothstep(0.55, 0.85, rho) * (1.0 - smoothstep(0.88, 1.0, rho)) * (1.0 - m);
	float buckel = ((w_rausch(xz * halbmesser * 0.8 + muster) - 0.5) * 0.035 + (1.0 - rho * rho) * 0.015) * (1.0 - smoothstep(0.75, 1.0, rho)) + wulst;
	float y = oben + h * dicke + (rolle < 0.01 && !versetzt ? buckel : 0.0);
	lokal = vec3(xz.x * halbmesser, y, xz.y * halbmesser);
	tiefe_rel = clamp(-h, 0.0, 1.0);
	randnah = versetzt ? 1.0 : rho * r;
	vec3 n_aussen = vec3(n_o.x, 0.0, n_o.y);
	vec3 n = rolle < 0.99 ? normalize(mix(vec3(0.0, 1.0, 0.0), n_aussen, rolle)) : normalize(n_aussen + vec3(0.0, 0.1, 0.0));
	if (rolle > 1.5) {
		n = vec3(0.0, -1.0, 0.0);
		rolle = 1.0;
	} else if (versetzt) {
		// Bruchfläche: senkrecht, glasig wie die Stirn (die oberen Ringe bleiben Oberseite)
		n = rolle < 0.3 ? vec3(0.0, 1.0, 0.0) : n_schnitt;
		rolle = rolle < 0.3 ? 0.0 : 1.0;
	}
	vec3 v = vec3(xz.x, y / halbmesser, xz.y);

	// Wellen am Ort der Scholle: Höhe, Versatz und Neigung wie die Wasserfläche dort (große Schollen schaukeln träger). Die Scholle bleibt ein
	// starrer Körper (gedreht und verschoben, nicht verformt); feste Schollen im Randeis bewegen sich gar nicht. Ein Bruchstück kippt um seinen
	// Schwerpunkt (nicht um die Spitze des Ausschnitts)
	if (treibt) {
		vec3 dreh_um = vec3(0.0);
		if (stueck) {
			float spanne = max(mod(sek_b - sek_a, TAU), 0.2);
			float mitte = sek_a + spanne * 0.5;
			dreh_um = vec3(cos(mitte), 0.0, sin(mitte)) * (4.0 * sin(spanne * 0.5) / (3.0 * spanne)) * 0.8;
		}
		vec3 mitte_w = (MODEL_MATRIX * vec4(dreh_um, 1.0)).xyz;
		float sturm;
		vec2 wind;
		wellen_wind(sturm, wind);
		float staerke = wellen_staerke * wellen_windfaktor(sturm);
		vec3 t_w;
		vec3 b_w;
		vec3 n_w;
		vec2 richtung = vec2(cos(INSTANCE_CUSTOM.z), sin(INSTANCE_CUSTOM.z));
		vec3 welle = wellen_welt(mitte_w.xz, INSTANCE_CUSTOM.xy, richtung, staerke, wind, t_w, b_w, n_w);
		float traeg = 1.0 / (1.0 + halbmesser / 3.0);
		vec3 n_k = normalize(mix(vec3(0.0, 1.0, 0.0), n_w, traeg));
		vec3 achse = cross(vec3(0.0, 1.0, 0.0), n_k);
		float sn = length(achse);
		mat3 kipp = mat3(1.0);
		if (sn > 1e-5) {
			vec3 kk_a = achse / sn;
			mat3 kk = mat3(vec3(0.0, kk_a.z, -kk_a.y), vec3(-kk_a.z, 0.0, kk_a.x), vec3(kk_a.y, -kk_a.x, 0.0));
			kipp = mat3(1.0) + sn * kk + (1.0 - n_k.y) * kk * kk;
		}
		v = dreh_um + zurueck * (kipp * (dreh * (v - dreh_um)));
		n = zurueck * (kipp * (dreh * n));
		v += zurueck * (vec3(welle.x * traeg, welle.y * mix(0.5, 1.0, traeg), welle.z * traeg) / halbmesser);
	}
	VERTEX = v * da;
	NORMAL = n;
#ifdef MIT_BLASEN
	vec3 welt = (MODEL_MATRIX * vec4(v, 1.0)).xyz;
	blick_lokal = zurueck * normalize(CAMERA_POSITION_WORLD - welt);
#endif
}

// Abstand (m) eines Punktes der Oberseite (Achsen der Scholle) zum Bruchstrahl im Winkel a durch den Ursprung
float strahl_abstand(vec2 p, float a) {
	vec2 d = vec2(cos(a), sin(a));
	float laengs = dot(p, d);
	return laengs > 0.0 ? abs(p.x * d.y - p.y * d.x) : length(p);
}

void fragment() {
	vec2 p = lokal.xz + muster;
	vec3 farbe;
	float rau;
	float spec = 0.5;
	vec3 durch;
	float schnee = 0.0;
	vec3 leuchten;
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	// frische Bruchkante: ein schmaler nasser Streifen ohne Schnee daneben (bis gut 0,2 m)
	float bruch = 0.0;
	if (sektor_v.z > 0.5) {
		float d = min(strahl_abstand(lokal.xz, sektor_v.x), strahl_abstand(lokal.xz, sektor_v.y));
		bruch = 1.0 - smoothstep(0.04, 0.25, d);
	}
	if (rolle < 0.99) {
		// Oberseite: das gemeinsame Eismaterial (weiß, matt, buckelig, Schnee in Fahnen, wenig klares Eis), am Rand spült Wasser den Schnee weg
		float bp = max(length(dFdx(lokal.xz)), length(dFdy(lokal.xz)));
		float rand_weg = (1.0 - smoothstep(0.8 - 0.2 * schmelz_v, 0.95, randnah)) * (1.0 - bruch);
		// Schnee nur, wo Schnee liegt (sonst sähen die Schollen anders aus als das Eis auf dem Wasser); eigene Menge je Scholle obendrauf, morsch weniger
		Eisoberseite e = eis_oberseite(p, -0.15, clamp(wetter_schnee * (0.8 + schnee_s), 0.0, 1.0) * rand_weg * (1.0 - schmelz_v), eis_dunkel * 0.35, bp);
		farbe = e.farbe;
		schnee = e.schnee;
#ifdef MIT_BLASEN
		// Luftblasen tiefer im klaren Eis: mit dem Blick gegen die obere Lage (im Eismaterial) verschoben – wirkt wie Tiefe im Eis
		vec2 vb = blick_lokal.xz / max(blick_lokal.y, 0.25);
		float b2 = smoothstep(0.8, 0.95, w_rausch((p - vb * 0.08) * 4.3 + 3.0)) * (1.0 - smoothstep(0.006, 0.02, bp));
		farbe = mix(farbe, eis_hell * 0.9, b2 * 0.3 * e.klar * (1.0 - schnee));
#endif
		// Nasser Rand: Wasser schwappt über die Kante – dunkler, mit Glanz (bei festen Schollen im Randeis kaum: dort schwappt nichts); morsches Eis
		// ist breiter nass, an der frischen Bruchkante ebenso
		float nass = max(max(smoothstep(0.88 - 0.3 * schmelz_v, 0.99, randnah), rolle), bruch * 0.8) * mix(1.0, 0.35, fest_v);
		farbe = mix(farbe, farbe * 0.55 + eis_stirn * 0.12, nass * 0.85);
		// morsch: grau und nass, Wasser steht darauf
		farbe = mix(farbe, farbe * 0.62 + eis_dunkel * 0.25 + eis_stirn * 0.04, schmelz_v * 0.7);
		rau = mix(mix(e.rau, 0.08, nass), 0.14, schmelz_v * 0.6);
		spec = mix(mix(e.spec, 0.6, nass), 0.58, schmelz_v * 0.5);
		durch = eis_stirn * mix(0.3, 0.6, max(nass, schmelz_v * 0.6)) * (1.0 - schnee);
		// Relief der Oberseite (Achsen der Scholle → Welt → Blick), zur gerundeten Kante hin ausgeblendet, morsch flacher
		vec3 neig_w = normalize(MODEL_MATRIX[0].xyz) * e.neigung.x + normalize(MODEL_MATRIX[2].xyz) * e.neigung.y;
		NORMAL = normalize(NORMAL + mat3(VIEW_MATRIX) * neig_w * (1.0 - rolle) * 0.8 * (1.0 - 0.5 * schmelz_v));
		leuchten = eis_himmel(e, fresnel, wetter_himmel) + eis_stirn * wetter_himmel * 0.32 * rolle * 0.6;
	} else {
		// Stirn: durchscheinend blaugrün, oben heller (Luft, Bruch), nach unten satter (die Dicke färbt), mit senkrechten Schlieren; eine frische
		// Bruchfläche genauso (glasig, ohne Wulst)
		farbe = mix(eis_hell * 0.55 + eis_stirn * 0.2, eis_stirn * 0.6, smoothstep(0.0, 0.3, tiefe_rel));
#ifdef MIT_BLASEN
		float schliere = w_rausch(vec2(atan(lokal.z, lokal.x) * 9.0 + muster.x, tiefe_rel * 1.8));
		farbe *= 0.85 + 0.3 * schliere;
#endif
		farbe = mix(farbe, farbe * 0.75 + eis_dunkel * 0.15, schmelz_v * 0.5);
		rau = 0.07;
		spec = 0.6;
		durch = eis_stirn * 0.9;
		// Die Stirn leuchtet blaugrün aus dem Eis heraus (Licht, das im Eis gestreut wird): auch bei bedecktem Himmel, wie Glas an der Kante
		leuchten = wetter_himmel * fresnel * 0.45 * (1.0 - rau) + eis_stirn * wetter_himmel * 0.38;
	}
	ALBEDO = farbe;
	ROUGHNESS = rau;
	SPECULAR = spec;
	BACKLIGHT = durch;
	EMISSION = leuchten;
}
