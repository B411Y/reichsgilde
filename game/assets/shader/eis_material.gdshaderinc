// Das gemeinsame Eismaterial (docs/wasser/phase3.md, Phase 3d): eine Oberseite für alles Eis – festes Randeis, zugefrorene Bäche, Teiche und
// Hafenbecken (wasser_realistisch.gdshaderinc) und die Schollen (eis_scholle.gdshaderinc, treibend und am Ufer festgefroren). Unterschiede
// nur über Werte: wie viel klares Eis (eis_klar je Gewässer, zur Kante hin mehr), wie viel Schnee, was durch klares Eis scheint.
//
// Vorbild Fotos von Flusseis (Rhein, Elbe, Mosel; Quellen in phase3.md): Flusseis ist fast immer weiß bis grau und matt – gefrorener Eisbrei,
// buckelig, mit Schnee in Fahnen; klares, dunkles Eis gibt es nur, wo das Wasser still war (Teich, Hafen, junge Kante), mit weißen Rissen und
// eingefrorenen Luftblasen; Flusseis führt Schwebstoff (bräunliche Schlieren). Das Relief (Buckel aus gefrorenem Brei und Schnee) kommt aus einer
// Normalkarte (ambientCG „Snow 014“, CC0, game/assets/texturen/eis/), zwei Lagen gegeneinander gedreht, damit sie sich nicht sichtbar wiederholt.
//
// Braucht w_hash, w_rausch und w_fbm aus wasser_wellen.gdshaderinc (vorher einbinden).

uniform vec3 eis_hell : source_color = vec3(0.84, 0.88, 0.90);   // weißes Eis (gefrorener Brei, Reif)
uniform vec3 eis_dunkel : source_color = vec3(0.42, 0.50, 0.54); // graues, nasses Eis
uniform vec3 eis_stirn : source_color = vec3(0.22, 0.42, 0.44);  // durchscheinende Seite (blaugrün, grau): Bruchkante, Stirn der Scholle
uniform vec3 eis_schlamm : source_color = vec3(0.47, 0.44, 0.37); // eingefrorener Schwebstoff
uniform float eis_klar = 0.25;         // Anteil klaren (schwarzen) Eises: Fluss wenig (gefrorener Brei), stilles Wasser mehr
uniform sampler2D eis_relief : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable; // Normalkarte des Reliefs
uniform float eis_relief_skala = 0.55; // Wiederholungen der Normalkarte je Meter
uniform float eis_relief_staerke = 0.8;
uniform float eis_tauen = 0.0;        // Eisgang am Winterende 0–1 (Wasserarten.Tauwetter): das Eis wird morsch, grau und nass, Platten und Schollen vergehen

struct Eisoberseite {
	vec3 farbe;
	float rau;
	float spec;
	vec2 neigung;   // Neigung der Oberfläche in den Achsen von p (x, z): Relief und Buckel, für NORMAL_MAP bzw. NORMAL
	float schnee;
	float klar;
	vec3 durch;     // Licht, das von hinten durchscheint (BACKLIGHT)
};

// p: Ort auf dem Eis in m (Welt beim festen Eis, mitdrehend bei der Scholle). klar_zusatz: −1…1 verschiebt den Anteil klaren Eises (zur Kante
// hin junges, klares Eis). schnee_menge: 0–1 (Schneedecke und eigene Menge). darunter: was durch klares Eis zu sehen ist (dunkles Wasser).
// bildpunkt_m: Meter je Bildpunkt – feine Muster (Blasen, Risse, Reif) aus der Ferne ausblenden, sonst flimmern sie.
Eisoberseite eis_oberseite(vec2 p, float klar_zusatz, float schnee_menge, vec3 darunter, float bildpunkt_m) {
	Eisoberseite e;
	float nah = 1.0 - smoothstep(0.01, 0.04, bildpunkt_m);
	// Großflächig wechselnd (3 und 15 m): weißer, trockener Brei neben grauem, nassem Eis – keine Platten, keine Kacheln
	float wolke = w_fbm(p * 0.32 + 11.0);
	float gross = w_fbm(p * 0.065 + 3.0);
	vec3 weiss = mix(eis_dunkel * 1.3, eis_hell, smoothstep(0.15, 0.7, wolke * 0.55 + gross * 0.45));
	// Schwebstoff: bräunlich-graue Schlieren (Flusseis ist selten rein)
	float dreck = smoothstep(0.58, 0.86, w_fbm(p * vec2(0.33, 1.05) + 17.0)) * smoothstep(0.35, 0.65, gross);
	weiss = mix(weiss, eis_schlamm * 0.85, dreck * 0.4);
	// Klares Eis: zeigt das dunkle Wasser darunter, leicht milchig, mit eingefrorenen Luftblasen (nur nah)
	e.klar = smoothstep(0.52, 0.72, w_fbm(p * 0.11 + 21.0) + (eis_klar - 0.3) * 0.9 + klar_zusatz * 0.3);
	vec3 klarfarbe = darunter * 0.5 + eis_dunkel * 0.28 + vec3(0.0, 0.012, 0.018);
	float blasen = smoothstep(0.86, 0.96, w_rausch(p * 14.0 + 3.0)) * (1.0 - smoothstep(0.004, 0.012, bildpunkt_m));
	klarfarbe = mix(klarfarbe, eis_hell * 0.85, blasen * 0.4);
	e.farbe = mix(weiss, klarfarbe, e.klar * 0.82);
	// Reif auf dem weißen Eis
	float reif = smoothstep(0.55, 0.8, w_fbm(p * 3.1 + 2.0)) * (1.0 - e.klar) * nah;
	e.farbe = mix(e.farbe, eis_hell * 1.08, reif * 0.3);
	// Feine Risse (Null-Linien von Rauschen, etwa einen Bildpunkt breit, stückweise): im klaren Eis weiß und deutlich, im weißen kaum
	float n1 = w_fbm(p * 0.8 + 2.0) - 0.5;
	float r1 = (1.0 - smoothstep(0.0, bildpunkt_m * 1.6 + 0.004, abs(n1))) * smoothstep(0.5, 0.68, w_rausch(p * 0.35 + 8.0));
	float n2 = w_rausch(p * 2.7 + 5.0) - 0.5;
	float r2 = (1.0 - smoothstep(0.0, bildpunkt_m * 4.0 + 0.006, abs(n2))) * smoothstep(0.6, 0.78, w_rausch(p * 0.9 + 1.0));
	float riss = max(r1 * (1.0 - smoothstep(0.03, 0.09, bildpunkt_m)), r2 * 0.6 * (1.0 - smoothstep(0.012, 0.035, bildpunkt_m)));
	riss *= mix(0.3, 1.0, e.klar);
	e.farbe = mix(e.farbe, mix(eis_dunkel * 0.75, eis_hell * 1.05, e.klar), riss * 0.5);
	// Relief: zwei Lagen der Normalkarte, gegeneinander gedreht und verschoben; klares Eis ist glatt
	vec2 q1 = p * eis_relief_skala;
	vec2 q2 = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * eis_relief_skala * 0.41 + vec2(0.37, 0.71);
	vec2 rel = (texture(eis_relief, q1).xy * 2.0 - 1.0) + (texture(eis_relief, q2).xy * 2.0 - 1.0) * 0.8;
	e.neigung = rel * eis_relief_staerke * mix(1.0, 0.2, e.klar);
	// Schnee: in Fahnen, mehr auf weißem, kaum auf klarem Eis
	e.schnee = clamp(schnee_menge * (1.0 - smoothstep(0.1, 0.7, eis_tauen)) * smoothstep(0.3, 0.6, w_fbm(p * 0.5 + 9.0) * 0.7 + gross * 0.3 + 0.1 - 0.35 * e.klar), 0.0, 1.0);
	e.farbe = mix(e.farbe, vec3(0.93, 0.95, 0.97), e.schnee);
	e.neigung *= 1.0 - 0.35 * e.schnee;
	e.rau = mix(mix(0.62, 0.1, e.klar), 0.85, e.schnee);
	e.spec = mix(0.4, 0.55, e.klar) * (1.0 - 0.5 * e.schnee);
	// Tauwetter: morsches Eis, grau und nass (Wasser steht darauf), der Schnee darauf geht weg
	float morsch = smoothstep(0.0, 0.6, eis_tauen);
	e.farbe = mix(e.farbe, e.farbe * 0.68 + eis_stirn * 0.07, morsch * 0.75);
	e.rau = mix(e.rau, 0.18, morsch * 0.7);
	e.spec = mix(e.spec, 0.55, morsch * 0.6);
	e.durch = eis_stirn * 0.3 * (1.0 - e.schnee);
	return e;
}

// Wie das Eis den Himmel spiegelt und Licht aus seinem Inneren streut (EMISSION): Fresnel nach Glätte, dazu ein schwacher blaugrüner Schein.
// fresnel = pow(1 − Normale·Blick, 4)
vec3 eis_himmel(Eisoberseite e, float fresnel, vec3 himmel) {
	return himmel * fresnel * 0.45 * (1.0 - e.rau) + eis_stirn * himmel * 0.06;
}
