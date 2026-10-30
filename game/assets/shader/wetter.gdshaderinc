// Wetter und Jahreszeiten (PR #195, Stadtansicht.Wetter.cs): gemeinsame Werte und Rechnungen für Boden (boden.gdshaderinc), Grasteppich
// (gras.gdshader), Äcker (acker.gdshader) und die Modelle (oberflaeche.gdshader). Alle Werte sind globale Shader-Uniforms (project.godot,
// [shader_globals]): Die Stadtansicht setzt sie einmal je Bild, und jeder Shader liest dieselben – kein Material muss angefasst werden.
// Ohne Partie (Hauptmenü, Kartenwerkstatt) gelten die Grundwerte aus project.godot: Sommer, trocken, leichter Wind.

// Nässe des Bodens 0–1 (Regen der letzten Stunden) und Regen gerade jetzt 0–1 (Ringe in den Pfützen); in der Stufe Niedrig 0
global uniform float wetter_naesse;
global uniform float wetter_regen;
// Rinnsale auf Dächern und Wänden 0–1 (Regen der letzten Stunden, bei Regen gerade jetzt stärker; Niedrig 0, Mittel schwächer)
global uniform float wetter_rinn;
// Schneedecke am Boden 0–1 (im Winter, mehr bei Schneefall) und auf Dächern und allem, was nach oben zeigt
global uniform float wetter_schnee;
global uniform float wetter_dach;
// 1 = Nässe und Schnee auch auf den Modellen; 0 in der Innenansicht (sonst läge Schnee im aufgeschnittenen Haus)
global uniform float wetter_flaechen;
// Windstärke 0–1 (0,25 leichter Wind): Gras, Laub
global uniform float wetter_wind;
global uniform vec2 wind_richtung;
// Farbe des Himmels (linear): Pfützen spiegeln ihn
global uniform vec3 wetter_himmel;
// Regentropfen auf nassem Boden und auf flachen Flächen der Modelle (regentropfen), 0–1: Dichte; Hoch = Regen, Mittel weniger, Niedrig 0 (Regen 4: ersetzt
// die Ringe als Partikel)
global uniform float wetter_ringe;
// Bild um den Blickpunkt (Stadtansicht.Traufen.cs), solange es von den Dächern tropft: R Abstand zur nächsten Traufkante (0–1 = 0–TRAUFE_WEIT m, für die
// nasse Tropfkante am Boden), G Höhe dieser Traufe, B und A die Unterkanten der Fenster davor (unterste und nächste Reihe) – für den Schutz der Wände unter
// Vorsprüngen (wand_schutz). Höhen in Stufen zu TRAUFE_STUFE Metern über traufe_basis, 0 = keine. traufe_feld = (Ecke x, Ecke z, 1 / Seitenlänge in m,
// Stärke 0–1; 0 = kein Bild).
global uniform vec4 traufe_feld;
global uniform sampler2D traufe_maske : filter_linear, repeat_disable;
global uniform float traufe_basis;
const float TRAUFE_STUFE = 0.15;
const float TRAUFE_BILD = 512.0;   // Seitenlänge des Bilds in Bildpunkten (TropfBild in Stadtansicht.Traufen.cs)
// Anteile der Jahreszeiten (Frühling, Sommer, Herbst, Winter), zusammen 1: beim Jahreswechsel gleitet die Farbe in Echtzeit über
global uniform vec4 jahreszeit_anteile;

// Wie glatt die Kuppen eines nassen Dachs höchstens werden (Rauheit, Regen 3): darunter spiegelt eine einzelne Facette der flach geschatteten Dächer
// (Kegel, Turmhelme) den Himmel oder das Licht von Sonne und Mond. Glatter werden nur Rinnen, Fugen und die Schlieren des laufenden Wassers.
const float DACH_GLATT = 0.55;

// Eigene Namen (w_…): acker.gdshader und boden_gemein.gdshaderinc haben ihr eigenes hash/rausch.
float w_hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p))) * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	return float(n >> 8u) * (1.0 / 16777216.0);
}

float w_rausch(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(w_hash(i), w_hash(i + vec2(1.0, 0.0)), f.x), mix(w_hash(i + vec2(0.0, 1.0)), w_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Die Wiese je Jahreszeit: Frühling frisch und hell, Sommer satt (wie die Textur), Herbst gelblich-braun, Winter fahl und grau
vec3 jahreszeit_wiese(vec3 c) {
	vec4 j = jahreszeit_anteile;
	if (j.y > 0.999) return c;
	float l = dot(c, vec3(0.3, 0.55, 0.15));
	vec3 fruehling = c * vec3(0.95, 1.07, 0.84);
	vec3 herbst = mix(c, vec3(l) * vec3(1.5, 1.18, 0.6), 0.72);
	vec3 winter = mix(c, vec3(l) * vec3(1.1, 1.05, 0.9), 0.78) * 0.92;
	return fruehling * j.x + c * j.y + herbst * j.z + winter * j.w;
}

// Schneedecke am Boden (0–1) an einem Ort der Welt; oben = y der Normale (an steilen Hängen bleibt er nicht liegen). Bei wenig Schnee
// fleckig, bei viel geschlossen.
float schnee_decke(vec2 wp, float oben) {
	if (wetter_schnee <= 0.001) return 0.0;
	float f = w_rausch(wp * 0.13) * 0.6 + w_rausch(wp * 0.71 + 5.3) * 0.3 + w_rausch(wp * 3.1) * 0.1;
	float d = wetter_schnee * 1.5 - 0.35 + (f - 0.5) * 1.2;
	return smoothstep(0.0, 0.35, d) * smoothstep(0.6, 0.85, oben);
}

// Pfützen (naesse = Nässe an diesem Ort, siehe wetter_boden): erst ab halber Nässe, dann in den Mulden (Rauschen und tiefe Stellen der Textur, tiefe 0–1), mit mehr Nässe größer. Gibt zurück:
// x = Wasser (0–1, weicher, vom Rauschen gefranster Rand), y = Feuchte (0–1, ein breiter Verlauf „trocken → feucht → nass“ um die Pfütze, rund einen
// Meter). Im Pflaster (stadt) zählt die Tiefe der Textur mehr: Am Rand der Pfütze steht das Wasser zuerst in den Fugen, erst weiter innen auch auf den Steinen.
vec2 pfuetze(vec2 wp, float tiefe, float stadt, float naesse) {
	if (naesse < 0.35) return vec2(0.0);
	float m = w_rausch(wp * 0.21 + 3.0) * 0.7 + w_rausch(wp * 0.9 + 1.7) * 0.3 + tiefe * (0.25 + 0.2 * stadt);
	m += (w_rausch(wp * 3.1 + 8.0) - 0.5) * 0.09 + (w_rausch(wp * 11.0 + 2.0) - 0.5) * 0.03;   // gefranster Rand statt glatter Kontur
	float schwelle = 1.12 - 0.36 * smoothstep(0.35, 1.0, naesse);
	return vec2(smoothstep(schwelle, schwelle + 0.07, m), smoothstep(schwelle - 0.3, schwelle, m));
}

// Regentropfen auf nassem Boden, in Pfützen und auf flachen Flächen der Modelle (Regen 4: ersetzt die Ringe als Partikel, die an einer Ebene in Höhe der
// Bildmitte lagen – wo das Gelände höher stieg, verschwanden sie hinter einer geraden Linie, die beim Verschieben mitsprang). Je Zelle (0,45 m, eine
// Lage: zwei kosteten auf der T400 rund 1 ms mehr) fällt im eigenen Takt (0,8–1,25 s) ein Tropfen, aber nur in einem Teil der Takte (dichte): ein
// kurzer Spritzer, dann ein flacher Ring, der sich auf 0,15 m Halbmesser weitet und verklingt – außen ein Wellenkamm, innen dicht dahinter eine Rinne;
// die Mitte liegt so weit innen in der Zelle, dass der Ring nie an ihrem Rand abgeschnitten wird. Feste Größe in der Welt; die Linie ist nie
// schmaler als gut ein Bildpunkt (breiter = etwas schwächer), und weiter weg (px ab 0,08, ganz weg bei 0,2; px aus fwidth, rund das Doppelte der
// Meter je Bildpunkt: Zoom 25 etwa 0,05, Zoom 60 etwa 0,12) werden die Tropfen weich schwächer – der Übergang folgt dem Abstand im Bild, ohne Kante.
// Gibt (Neigung x, Neigung z, Zeichnung) zurück: Zeichnung > 0 heller Kamm, < 0 dunkle Rinne. px vor jeder Verzweigung berechnet.
vec3 regentropfen(vec2 p, float zeit, float px, float dichte) {
	float sicht = 1.0 - smoothstep(0.08, 0.2, px);
	if (sicht <= 0.0 || dichte <= 0.001) return vec3(0.0);
	float breite = max(0.016, 0.8 * px);
	vec2 zelle = floor(p * (1.0 / 0.45));
	float z = w_hash(zelle);
	float lauf = zeit * (0.8 + 0.5 * fract(z * 7.31)) + z;
	float t = fract(lauf);
	float an = step(w_hash(zelle + vec2(floor(lauf) * 3.1, 17.0)), dichte) * sicht * clamp(sqrt(0.02 / breite), 0.65, 1.0);
	vec2 d = p - (zelle + 0.35 + 0.3 * vec2(z, fract(z * 13.7))) * 0.45;
	float r = length(d);
	float ka = (r - 0.015 - 0.135 * sqrt(t)) / breite;
	float ri = ka + 1.7;
	float kamm = exp(-ka * ka);
	float rinne = exp(-ri * ri);
	float alt = an * (1.0 - t) * (1.0 - t);
	float spritz = an * (1.0 - smoothstep(0.0, 0.025 + breite, r)) * (1.0 - smoothstep(0.0, 0.1, t));
	return vec3(d / max(r, 0.001) * (kamm - rinne) * alt, (kamm * 0.8 - rinne * 0.6) * alt + spritz * 0.7);
}

// Nasse Tropfkante unter den Traufen (Stadtansicht.Traufen.cs): Abstand am Boden (m), ab dem nichts mehr nass ist (Wert 1 im Bild traufe_maske, das
// boden.gdshaderinc liest); muss zu TropfWeit in Stadtansicht.Traufen.cs passen.
const float TRAUFE_WEIT = 1.2;

// Wie durchnässt der Boden an der Tropfkante ist (0–1). traufe = (Abstand zur nächsten Traufkante im Grundriss in m, Stärke 0–1). Ein unregelmäßiger
// Streifen: die Breite schwankt längs der Kante (rund 0,2–0,5 m zu jeder Seite der Linie, auf der die Tropfen aufschlagen), der Rand franst aus.
// Ein Abstand, keine Summe: Wo Kanten zusammenkommen (Hausecken, Giebel, Nachbarhäuser), wird der Boden nicht nässer als nass.
float traufe_nass(vec2 wp, vec2 traufe) {
	if (traufe.y <= 0.001 || traufe.x >= TRAUFE_WEIT - 0.02) return 0.0;
	float breite = 0.2 + 0.3 * w_rausch(wp * 0.7 + 11.0);
	float d = traufe.x + (w_rausch(wp * 3.3 + 2.0) - 0.5) * 0.24 + (w_rausch(wp * 9.0 + 6.0) - 0.5) * 0.08;
	return (1.0 - smoothstep(breite, breite + 0.28, d)) * clamp(traufe.y, 0.0, 1.0);
}

// Ringe der Tropfen von der Traufe in den kleinen Pfützen der Tropfkante: dichter und schneller als die des Regens (Gitter 0,4 m, rund 1,3 Tropfen
// je Sekunde und Zelle), feine, flache Wellen, die schnell vergehen. Gibt die Neigung der Wasserfläche (x, z) zurück; weit herausgezoomt aus (px = Meter
// je Bildpunkt).
vec2 tropfringe(vec2 p, float zeit, float px) {
	vec2 neig = vec2(0.0);
	for (int k = 0; k < 2; k++) {
		vec2 q = p / 0.4 + float(k) * vec2(0.41, 0.23);
		vec2 zelle = floor(q);
		float z = w_hash(zelle + float(k) * 23.0 + 5.0);
		vec2 mitte = zelle + 0.25 + 0.5 * vec2(z, w_hash(zelle + 47.0));
		float t = fract(zeit * 1.3 + z);
		vec2 d = q - mitte;
		float r = length(d);
		float welle = sin((r - t * 0.5) * 44.0) * (1.0 - t) * (1.0 - t) * smoothstep(0.0, 0.04, r) * (1.0 - smoothstep(t * 0.5, t * 0.5 + 0.08, r));
		neig += d / max(r, 0.001) * welle;
	}
	return neig * 0.3 * (1.0 - smoothstep(0.07, 0.16, px / 0.4));
}

// Wetter auf dem Boden (boden.gdshaderinc): Nässe macht ihn dunkler und glänzend, in Mulden stehen Pfützen, die den Himmel spiegeln (als Leuchten,
// denn die Stadt hat keine Spiegelungen); Schnee deckt ihn zu, auf Wegen (aufweg 0–1) ist er ausgetreten und zu Matsch geworden, in der Stadt
// (stadt 0–1) zertreten und grauer. tiefe = Höhe der Textur umgekehrt (0–1). nw ist die Normale in der Welt. traufe = (Abstand zur Traufkante in m,
// Stärke) für die nasse Tropfkante (traufe_nass), ohne Traufe (9, 0).
// Die Nässe an einem Ort ist die größere aus Wetter und Tropfkante (max, nicht Summe): An der Traufe ist der Boden so nass, wie er bei Regen
// höchstens wird, dazu durchnässt (satt): dunkler und gesättigter, matt glänzend, mit kleinen Pfützen in Fugen und Mulden – mit fester Obergrenze, nie schwarz.
void wetter_boden(inout vec3 bf, inout float rau, inout vec3 nw, inout vec3 leuchten, vec2 wp, vec3 n, float aufweg, float stadt, float tiefe, float wiese, int stufe, float zeit, vec2 traufe) {
	float px = max(fwidth(wp.x), fwidth(wp.y));   // Meter je Bildpunkt, vor jeder Verzweigung (Ableitungen brauchen alle Nachbarpunkte)
	bool ringe = stufe >= 2;
	float tk = traufe_nass(wp, traufe);
	float naesse = max(wetter_naesse, tk);
	if (naesse > 0.001) {
		float saugt = mix(1.0, 0.7, stadt);
		bf *= 1.0 - 0.38 * naesse * saugt;
		// Glanz nass: auf Wiese nur ein Drittel (sonst glitzert das Gras), im Pflaster schmal – nur einzelne Steine glänzen, der Rest bleibt matt
		float stein = smoothstep(0.62, 0.8, w_rausch(wp * 2.3 + 9.1));
		float glanz = 0.38 + 0.2 * wiese + stadt * (0.24 - 0.2 * stein);
		rau = mix(rau, min(rau, glanz), naesse);
		// Wege und Gassen bleiben dauerhaft etwas dunkler (Matsch, Abdrücke), unabhängig vom Licht
		bf *= 1.0 - 0.12 * naesse * max(aufweg, stadt * 0.6);
		// Durchnässt an der Tropfkante: Erde und Lehm satt dunkelbraun und matschig glänzend, Pflaster dunkelgrau und jeder Stein nass, Gras kaum
		// (eine Obergrenze: höchstens 45 % dunkler als der nasse Boden daneben, Pflaster 27 %). Ihr Glanz ist ein schwacher Schein in der Farbe des
		// Himmels, zum Boden hin getönt (die Stadt hat keine Spiegelungen, und Glanz allein zeigt sich nur im Sonnenlicht).
		float satt = tk * (1.0 - 0.8 * wiese);
		vec3 ton = vec3(1.0);   // Farbton des Bodens (Helligkeit 1): Spiegelungen in der Tropfkante nehmen ihn zu 70 % an
		if (satt > 0.0) {
			float l = dot(bf, vec3(0.3, 0.59, 0.11));
			ton = mix(vec3(1.0), clamp(bf / max(l, 0.02), vec3(0.0), vec3(2.0)), 0.7);
			bf = max(mix(vec3(l), bf, 1.0 + 0.35 * satt), vec3(0.0));
			bf *= (1.0 - 0.45 * satt * mix(1.0, 0.6, stadt)) * mix(vec3(1.0), vec3(1.0, 0.95, 0.88), satt * (1.0 - 0.6 * stadt));   // wärmer: nasse Erde wird braun, nicht oliv
			rau = mix(rau, min(rau, mix(0.2, 0.28, stadt)), satt);
			leuchten += wetter_himmel * ton * 0.025 * satt * (0.5 + w_rausch(wp * 5.0 + 3.0));
		}
		float flach = smoothstep(0.9, 0.97, n.y);
		float flaeche = (0.3 + 0.7 * max(aufweg, stadt * 0.3)) * flach;
		vec2 pf = pfuetze(wp, tiefe, stadt, max(wetter_naesse, satt)) * flaeche;   // im Gras macht die Tropfkante keine Pfützen
		// Kleine Pfützen und Rinnen in der Tropfkante: wo der Boden tiefer liegt (Fugen, Mulden der Textur), nicht im Gras, nicht am Hang
		float tw = 0.0;
		if (satt > 0.3) {
			float m = (satt - 0.5) * 0.6 + tiefe * (0.35 + 0.2 * stadt) + (w_rausch(wp * 2.1 + 5.0) - 0.5) * 0.5 + (w_rausch(wp * 7.3 + 1.0) - 0.5) * 0.12;
			tw = smoothstep(0.5, 0.57, m) * flach;
		}
		float p = max(pf.x, tw);
		// Feuchte um die Pfütze: nur leicht dunkler und glänzender, im breiten weichen Verlauf (keine Randlinie)
		float feucht = pf.y * (1.0 - p);
		bf *= 1.0 - 0.2 * feucht;
		rau = mix(rau, min(rau, 0.3), feucht * 0.55);
		// Regentropfen (Boden Mittel und Hoch): überall auf dem nassen Boden, nicht nur in Pfützen. Außerhalb der Pfützen liegt nur ein dünner Film – dort
		// sind die Ringe schwach, im Gras fast nichts; der Kamm hellt in der Farbe des Bodens auf (kein reines Weiß), die Rinne dunkelt ab.
		vec3 tr = vec3(0.0);
		if (stufe >= 1 && wetter_ringe > 0.001) {
			tr = regentropfen(wp, zeit, px, wetter_ringe) * smoothstep(0.85, 0.95, n.y);
			float film = (0.45 + 0.35 * stadt + 0.2 * aufweg) * (1.0 - 0.8 * wiese) * (1.0 - p) * min(naesse * 1.5, 1.0);
			float l = max(dot(bf, vec3(0.3, 0.59, 0.11)), 0.02);
			vec3 eigen = clamp(bf / l, vec3(0.0), vec3(2.0));
			bf *= 1.0 - 0.4 * max(-tr.z, 0.0) * film;
			leuchten += mix(wetter_himmel, wetter_himmel * eigen, 0.5) * 0.35 * max(tr.z, 0.0) * film;
			nw = normalize(nw + vec3(tr.x, 0.0, tr.y) * 0.5 * film);
		}
		if (p > 0.0) {
			// Wasserfarbe: die Farbe des Untergrunds scheint durch, etwas dunkler, mit leichter Spiegelung des Himmels; in der Tropfkante (seichtes
			// Wasser über nasser Erde) gedämpfter und in der Farbe des Bodens
			float ta = tw / max(p, 0.001);
			bf = mix(bf, bf * mix(0.78, 0.85, ta) + wetter_himmel * mix(0.07, 0.02, ta), p);
			rau = mix(rau, 0.04, p);
			// Himmelsschein: leicht; in der Tropfkante gedämpft und zum Boden getönt (seichtes Wasser über Erde); Tropfenringe als Hell-dunkel-Zeichnung
			vec3 schein = wetter_himmel * mix(vec3(0.08), ton * 0.1, ta);
			vec3 glatt = n;
			if (ringe) {
				// In der Pfütze dieselben Tropfen wie auf dem Boden daneben, nur deutlicher (stehendes Wasser), dazu die feinen Ringe der Traufe
				vec2 rt = tropfringe(wp, zeit, px) * clamp(traufe.y, 0.0, 1.0) * step(0.001, tw);
				float zt = clamp(length(rt) * 2.2, 0.0, 1.0);
				bf *= (1.0 - 0.3 * max(-tr.z, 0.0)) * (1.0 - 0.15 * zt);
				schein += wetter_himmel * (0.16 * max(tr.z, 0.0) + ton * 0.12 * zt);
				vec2 r = tr.xy * 0.6 + rt;
				glatt = normalize(n + vec3(r.x, 0.0, r.y));
			}
			nw = normalize(mix(nw, glatt, p));
			leuchten += schein * p;
		}
	}
	if (wetter_schnee > 0.001) {
		float d = schnee_decke(wp, n.y);
		// Auf Wegen ausgetreten, an ihren Rändern Matsch; in der Stadt zertreten
		float frei = aufweg * smoothstep(0.25, 0.75, w_rausch(wp * 1.3) * 0.5 + 0.5 * aufweg);
		float deck = d * (1.0 - 0.9 * frei) * mix(1.0, 0.65, stadt);
		float matsch = aufweg * d * (1.0 - deck);
		bf = mix(bf, bf * vec3(0.58, 0.55, 0.52), matsch * 0.75);
		rau = mix(rau, 0.45, matsch);
		vec3 weiss = vec3(0.66, 0.68, 0.73) * (0.94 + 0.06 * (1.0 - tiefe)) * mix(1.0, 0.86, stadt);
		bf = mix(bf, weiss, deck);
		rau = mix(rau, 0.7, deck);
		nw = normalize(mix(nw, n, deck * 0.7));
	}
}

// Dachanteil einer Fläche 0–1: wie sehr sie ein Dach ist (nass: Wasserfilm) und nicht eine Wand. wg = glatte Weltnormale, dachstoff aus
// oberflaeche.gdshader: 1 = Deckung (Ziegel, Schiefer, Schindeln), 0,5 = Stroh und Kupfer, 0 = alles andere. Eine Deckung ist immer Dach, auch am
// steilen Turmhelm (Neigung über 60°): nach der Neigung allein zählte er als Wand, und der Rathausturm blieb bei Regen ohne Nässe auf dem Dach.
float wetter_dachanteil(vec3 wg, float dachstoff) {
	float neig = sqrt(max(1.0 - wg.y * wg.y, 0.0));            // Sinus des Neigungswinkels: 0 flach, 1 senkrecht
	float schraeg = smoothstep(0.15, 0.4, neig);
	if (dachstoff > 0.75) return schraeg;
	return schraeg * (1.0 - smoothstep(dachstoff > 0.25 ? 0.93 : 0.86, dachstoff > 0.25 ? 0.99 : 0.97, neig));
}

// Schutz einer Wand unter Vorsprüngen, 0 (offen) bis 1 (geschützt), aus dem Bild der Tropfkante (nur nahe der Kamera, solange es tropft; sonst 0).
// wp = Ort, aus = waagerechte Richtung aus der Wand heraus. Gelesen wird der Bildpunkt 12 cm vor der Wand (ohne Filter: Höhen dürfen sich nicht
// mischen). Der Regen fällt schräg (waagerecht 0,6 · Wind² je Meter Fall, Stadtansicht.Wetter.cs; mindestens 0,12 für Böen und Spritzer): Unter einem
// Vorsprung mit Überstand ü bleibt die Wand bis etwa ü / Schräge darunter trocken, nach unten weich auslaufend.
//   Traufe (G): Überstand = Abstand zur Tropflinie (R) + 0,12 m, geschützt höchstens 1,8 m tief; bei Sturm nur ein schmaler Streifen.
//   Fensterbänke (B, A): die unterste Reihe und die nächste, darüber im selben Abstand weiter bis unter die Traufe; Überstand rund 12 cm, also
//   0,2–0,7 m trockener Streifen unter jedem Fenster, zu 80 %.
float wand_schutz(vec3 wp, vec2 aus) {
	if (traufe_feld.w <= 0.001) return 0.0;
	vec2 tu = (wp.xz + aus * 0.12 - traufe_feld.xy) * traufe_feld.z;
	if (tu.x < 0.0 || tu.y < 0.0 || tu.x >= 1.0 || tu.y >= 1.0) return 0.0;
	vec4 m = texelFetch(traufe_maske, ivec2(tu * TRAUFE_BILD), 0);
	float schraeg = max(0.6 * wetter_wind * wetter_wind, 0.12);
	float schutz = 0.0;
	float traufe = 1e6;
	if (m.g > 0.002) {
		traufe = traufe_basis + (m.g * 255.0 - 1.0) * TRAUFE_STUFE;
		float tief = clamp((m.r * TRAUFE_WEIT + 0.12) / schraeg, 0.4, 1.8);
		float unter = traufe - wp.y;
		schutz = step(-0.1, unter) * (1.0 - smoothstep(tief * 0.35, tief, unter));
	}
	if (m.b > 0.002) {
		float h0 = traufe_basis + (m.b * 255.0 - 1.0) * TRAUFE_STUFE;
		float h1 = m.a > 0.002 ? traufe_basis + (m.a * 255.0 - 1.0) * TRAUFE_STUFE : 1e6;
		float reihenabstand = max(h1 - h0, 1.5);
		float unter = 1e6;
		for (int i = 0; i < 4; i++) {
			float hb = i == 0 ? h0 : h1 + float(i - 1) * reihenabstand;
			if (i >= 2 && hb > traufe - 0.6) break;               // weiter oben nur bis unter die Traufe (darüber gibt es keine Fenster)
			float u = hb - wp.y;
			if (u > -0.03 && u < unter) unter = u;
		}
		float tief = clamp(0.12 / schraeg, 0.2, 0.7);
		schutz = max(schutz, 0.8 * (1.0 - smoothstep(tief * 0.3, tief, unter)));
	}
	return schutz * clamp(traufe_feld.w * 2.0, 0.0, 1.0);
}

// Nasse Wand (Regen 4, ersetzt die festen Laufspuren im Geschossraster): Mauer, Putz und Fachwerk werden dunkler und etwas satter, fleckig (große und
// kleine Flecken, unten in der Spritzzone über dem Boden nässer, die Fugen der Bibliothek länger nass); die Wetterseite (gegen den Wind) wird nässer als
// der Windschatten, unter Traufe und Fensterbänken bleibt es trockener (wand_schutz). Beim Abtrocknen bleiben die Flecken zuletzt. Bei Regen (nur Hoch)
// dazu kleine Wasserflecken der auftreffenden Tropfen, die erscheinen und einziehen, und einzelne Rinnsale: ein dunkler, schmaler, leicht schlängelnder
// Lauf (mindestens 4 cm breit), dessen Kopf (ein kleiner Glanz) mit 0,5–1 m/s hinabläuft und eine nasse Spur hinterlässt, die nach oben hin auslaufend
// einzieht; je Spalte (38 cm) alle 4–9 s ein Lauf, aber nur in einem Teil der Spalten (mit dem Regen und auf der Wetterseite mehr), jeder 1,3–2,6 m
// lang. Aus der Ferne (px aus fwidth 0,05–0,1, etwa Zoom 25–50) blenden Flecken und Rinnsale weich aus. gewicht = Wandanteil der Fläche, hoehe = Höhe
// über dem Fuß des Modells (m), tiefe = Tiefe der Textur (0–1, 0,5 ohne Bibliothek), px = Größe eines Bildpunkts (fwidth des Orts).
void wetter_wand(inout vec3 c, inout float rau, inout vec3 gl, vec3 wg, vec3 wp, float hoehe, float tiefe, float gewicht, float zeit, float px) {
	vec2 aus = normalize(wg.xz + vec2(1e-5, 0.0));
	vec3 quer = vec3(-aus.y, 0.0, aus.x);
	float schutz = wand_schutz(wp, aus);
	float luv = clamp(dot(aus, -normalize(wind_richtung + vec2(1e-4, 0.0))) * 0.5 + 0.5, 0.0, 1.0);
	float aussen = mix(1.0, 0.45 + 0.55 * luv, clamp(wetter_wind * 1.3, 0.0, 1.0)) * (1.0 - 0.85 * schutz);
	vec2 wand = vec2(dot(wp, quer), wp.y);
	float f = smoothstep(0.32, 0.68, w_rausch(wand * vec2(0.9, 0.6) + 3.1) * 0.6 + w_rausch(wand * 3.1 + 7.3) * 0.4);
	float sockel = 1.0 - smoothstep(0.25, 0.9, hoehe);
	float w = wetter_naesse * aussen * (0.45 + 0.6 * f) + 0.3 * sockel * wetter_naesse + 0.25 * (tiefe - 0.5) * wetter_naesse;
	w = clamp(w, 0.0, 1.0) * gewicht;
	float l = dot(c, vec3(0.3, 0.59, 0.11));
	c = max(mix(vec3(l), c, 1.0 + 0.1 * w), vec3(0.0));
	c *= 1.0 - 0.52 * w;
	rau = mix(rau, rau * 0.65, w);
	gl += wetter_himmel * 0.012 * w;
	if (wetter_regen < 0.01) return;
	float sicht = (1.0 - smoothstep(0.05, 0.1, px)) * gewicht * (1.0 - schutz) * wetter_regen;
	if (sicht <= 0.001) return;
	// Kleine Wasserflecken der Tropfen: Zellen zu 14 cm, je Zelle im eigenen Takt (1,8–4,2 s) ein Fleck von 2,5–4,5 cm, der schnell kommt und langsam einzieht
	vec2 sz = wand / 0.14;
	vec2 zi = floor(sz);
	float hz = w_hash(zi + 3.7);
	float takt = 1.8 + 2.4 * w_hash(zi + 9.1);
	float lauf = zeit / takt + hz;
	float an = step(w_hash(zi + vec2(floor(lauf) * 1.7, 4.4)), (0.2 + 0.4 * luv) * aussen);
	float r = length(sz - zi - 0.25 - 0.5 * vec2(hz, w_hash(zi + 5.5))) * 0.14;
	float gr = 0.025 + 0.02 * w_hash(zi + 8.8);
	float fleck = an * (1.0 - smoothstep(gr * 0.6, gr, r)) * smoothstep(0.0, 0.05, fract(lauf)) * (1.0 - fract(lauf));
	c *= 1.0 - 0.3 * fleck * sicht;
	// Rinnsale: Spalten zu 38 cm, Abschnitte zu 2,6 m (je Spalte versetzt); s = Meter unter dem oberen Ende des Abschnitts
	float spalte = wand.x / 0.38;
	float ci = floor(spalte);
	float h1 = w_hash(vec2(ci, 3.0));
	float h2 = w_hash(vec2(ci, 11.0));
	float h3 = w_hash(vec2(ci, 23.0));
	float yy = wp.y + h1 * 2.6;
	float si = floor(yy / 2.6);
	float s = (si + 1.0) * 2.6 - yy;
	float periode = 4.0 + 5.0 * h2;
	float rl = zeit / periode + h3 + si * 0.37;
	float rein = step(w_hash(vec2(ci + floor(rl) * 7.0, si + 2.0)), (0.2 + 0.4 * wetter_regen) * (0.4 + 0.6 * luv));
	float tempo = 0.5 + 0.5 * h1;
	float kopf = fract(rl) * periode * tempo;
	float laenge = 2.6 * (0.5 + 0.5 * h2);
	float x = (fract(spalte) - 0.5 + 0.3 * (h3 - 0.5)) * 0.38 + 0.02 * sin(wp.y * 3.7 + ci * 2.3) + 0.008 * sin(wp.y * 11.0 + ci);
	float breite = max(0.04, 0.8 * px);
	float linie = exp(-(x * x) / (breite * breite)) * sqrt(0.04 / breite);
	float hinter = min(kopf, laenge) - s;
	float spur = step(0.0, hinter) * step(s, laenge) * exp(-max(kopf - s, 0.0) / (tempo * 3.5));
	float perle = step(kopf, laenge) * exp(-(hinter * hinter) / 0.004);
	float rinn = rein * linie * sicht;
	c *= 1.0 - 0.6 * rinn * spur;
	rau = mix(rau, 0.3, rinn * spur * 0.6);
	gl += wetter_himmel * 0.15 * rinn * perle;
}

// Nasses Dach (Regen 4, nach Fotos nasser Ziegel- und Schieferdächer): Die Deckung wird deutlich dunkler und satter; das Wasser sammelt sich in den
// Rinnen und Fugen der Deckung (tiefe Stellen der Textur der Bibliothek, tiefe) und an der Unterkante jeder Reihe (reihe_film), dort ist es dunkler und
// glatt. Die Kuppen bleiben matt (ein dünner Film ist kein Spiegel; vorher war das ganze Dach gleichmäßig glänzend, in der Sonne „eine glänzende
// Schicht“). Bei Regen läuft ein dünner Film als schmale, lange Schlieren in der Falllinie bergab (gut 0,6 m/s): Nur dort und in den Rinnen glänzt es,
// darum ist die Spiegelung streifig statt flächig. Nach dem Regen trocknet das Dach fleckig ab: erst die Kuppen und einzelne Flächen, zuletzt Rinnen und
// Fugen. dach = Ort in der Dachebene (m, y die Fläche hinauf), gewicht = Dachanteil, stark = 0,75–1 nach Neigung.
void wetter_dach_nass(inout vec3 c, inout float rau, inout vec3 gl, vec2 dach, float tiefe, float reihe_film, float gewicht, float stark, float zeit, float px) {
	float kanal = smoothstep(0.5, 0.8, tiefe);
	float ft = w_rausch(dach * vec2(0.35, 0.5) + 2.0) * 0.65 + w_rausch(dach * 1.3 + 9.0) * 0.35;
	float schwelle = ft * 0.8 + 0.12 - 0.35 * kanal - 0.2 * reihe_film;
	float nl = smoothstep(schwelle - 0.12, schwelle + 0.12, wetter_naesse * 1.15) * gewicht;
	float l = dot(c, vec3(0.3, 0.59, 0.11));
	c = max(mix(vec3(l), c, 1.0 + 0.15 * nl), vec3(0.0));
	c *= 1.0 - (0.44 + 0.25 * kanal + 0.12 * reihe_film) * nl * stark;
	rau = mix(rau, min(rau, max(rau * 0.85, DACH_GLATT)), nl);
	float strom = 0.0;
	if (wetter_rinn > 0.01) {
		float sl = w_rausch(vec2(dach.x * 6.0, dach.y * 0.7 + zeit * 0.45)) * 0.7 + w_rausch(vec2(dach.x * 13.0 + 3.0, dach.y * 1.6 + zeit * 1.0)) * 0.3;
		strom = smoothstep(0.5, 0.78, sl) * clamp(wetter_rinn * 1.2, 0.0, 1.0) * (1.0 - smoothstep(0.06, 0.12, px));
	}
	float nasskanal = clamp(kanal * (0.45 + 0.55 * strom) + 0.4 * strom + 0.5 * reihe_film, 0.0, 1.0) * nl;
	rau = mix(rau, max(0.22, rau * 0.35), nasskanal);
	// Schlieren: ein Hauch Schimmer des Himmels, zur Farbe der Deckung getönt (kein helles Blau)
	vec3 ton = clamp(c / max(dot(c, vec3(0.3, 0.59, 0.11)), 0.02), vec3(0.0), vec3(2.0));
	c *= 1.0 - 0.06 * strom * nl;
	gl += mix(wetter_himmel, wetter_himmel * ton, 0.5) * (0.01 + 0.025 * nasskanal + 0.045 * strom) * nl;
}

// Wetter auf den Modellen (oberflaeche.gdshader): Schnee auf allem, was nach oben zeigt (Dächer, Mauerkronen, Brunnen …), Nässe auf Dächern
// (wetter_dach_nass), Wänden (wetter_wand) und dem Übrigen (Mauerkronen, Treppen, flache Teile: dunkler und glatter, mit Regentropfen auf flachen
// Flächen wie am Boden). wn = Normale in der Welt (mit Struktur), wg = glatte Weltnormale, wp = Ort in der Welt. dach = der Ort in der Ebene der Fläche
// (Meter: x waagerecht, y die Fläche hinauf, wie die Projektion „reihen“ der Material-Bibliothek), reihe = Höhe einer Ziegelreihe (Meter), tiefe = Tiefe
// der Textur der Bibliothek (0 oben … 1 tief, 0,5 ohne Bibliothek), hoehe = Höhe über dem Fuß des Modells (m). nw gibt die geneigte Normale zurück.
// Schnee liegt in der Dachebene und nicht in der Welt-x/z-Ebene (dort würde er an Schrägen gestreckt): Aus der Nähe in Reihen gegliedert – die
// Flecken laufen mit den Reihen, in jeder Reihenkante liegt er dichter –, mit wachsendem Abstand (fwidth der Reihen) wird das Muster weich zu einer
// ruhigen Decke gemittelt. Auf flachen Flächen (Mauerkronen) gibt es keine Reihen, dort gilt dasselbe Fleckenmuster ohne Kanten.
void wetter_flaeche(inout vec3 c, inout float rau, inout vec3 nw, inout vec3 gl, vec3 wn, vec3 wg, vec3 wp, vec2 dach, float reihe, float dachstoff, float tiefe, float hoehe, float zeit) {
	if (wetter_flaechen < 0.5) return;
	float px = max(max(fwidth(wp.x), fwidth(wp.y)), fwidth(wp.z));   // Meter je Bildpunkt, vor jeder Verzweigung
	float pv = dach.y / max(reihe, 0.05);
	float fein = 1.0 - smoothstep(0.05, 0.28, fwidth(pv));                // 1 nah genug für Reihen, 0 zu weit
	if (wetter_naesse > 0.001) {
		float oben = clamp(wn.y, 0.0, 1.0);
		float stark = 0.75 + 0.25 * oben;
		float neig = sqrt(max(1.0 - wg.y * wg.y, 0.0));
		float dachst = wetter_dachanteil(wg, dachstoff);
		float wandst = dachstoff > 0.75 ? 0.0 : smoothstep(0.86, 0.97, neig);
		float sonst = max(1.0 - dachst - wandst, 0.0);
		if (dachst > 0.001) {
			// Die Reihen der Deckung halten das Wasser unten in jeder Reihe
			float film = (1.0 - smoothstep(0.0, 0.5, fract(pv))) * fein * smoothstep(0.1, 0.8, oben) * (1.0 - smoothstep(0.9, 0.98, oben));
			wetter_dach_nass(c, rau, gl, dach, tiefe, film, dachst, stark, zeit, px);
		}
		if (wandst > 0.001) wetter_wand(c, rau, gl, wg, wp, hoehe, tiefe, wandst, zeit, px);
		if (sonst > 0.001) {
			c *= 1.0 - 0.36 * stark * wetter_naesse * sonst;
			rau = mix(rau, rau * 0.5, wetter_naesse * stark * sonst);
			gl += wetter_himmel * 0.02 * wetter_naesse * stark * sonst;
			if (wetter_ringe > 0.001 && wg.y > 0.9) {
				vec3 tr = regentropfen(wp.xz, zeit, px, wetter_ringe) * smoothstep(0.9, 0.97, wg.y) * sonst * min(wetter_naesse * 1.5, 1.0);
				vec3 eigen = clamp(c / max(dot(c, vec3(0.3, 0.59, 0.11)), 0.02), vec3(0.0), vec3(2.0));
				c *= 1.0 - 0.4 * max(-tr.z, 0.0);
				gl += mix(wetter_himmel, wetter_himmel * eigen, 0.5) * 0.35 * max(tr.z, 0.0);
				nw = normalize(nw + vec3(tr.x, 0.0, tr.y) * 0.4);
			}
		}
	}
	if (wetter_dach > 0.001) {
		float fr = fract(pv);
		float flach = smoothstep(0.9, 0.98, wn.y);               // Mauerkronen, Plätze: keine Reihen
		float kante = mix(1.0 - smoothstep(0.0, 0.4, fr), 0.35, flach);   // unten in jeder Reihe, wo sie unter der nächsten liegt
		float f = w_rausch(vec2(dach.x * 0.5, pv * 0.2)) * 0.65 + w_rausch(vec2(dach.x * 1.9 + 7.1, pv * 0.7)) * 0.35;
		float mittel = clamp(wetter_dach * 1.5 - 0.3, 0.0, 1.0);
		float dicke = clamp(mittel + ((f - 0.5) * 0.8 + (kante - 0.35) * 0.14) * fein, 0.0, 1.0);
		// nah: Reihenmuster, Schnee oder frei; fern: fast geschlossene Decke, die Reste grau-schattig statt in Ziegelfarbe
		float nah = smoothstep(0.25, 0.75, dicke);
		float fern = smoothstep(0.12, 0.4, mittel) * 0.97;
		float d = smoothstep(0.32, 0.55, wn.y + (f - 0.5) * 0.25 * fein) * mix(fern, nah, fein);
		c = mix(c, vec3(dot(c, vec3(0.3, 0.55, 0.15))) * vec3(0.9, 0.93, 1.0), (1.0 - fein) * 0.7 * smoothstep(0.32, 0.55, wn.y));
		c = mix(c, vec3(0.7, 0.72, 0.77), d);
		rau = mix(rau, 0.75, d);
	}
}

// Laub im Wind (oberflaeche.gdshader, nur Materialart Laub): Die Krone neigt sich mit dem Wind und wiegt sich, oben mehr als unten. v = Ecke
// im Modell, wp = Ort in der Welt (für den eigenen Takt jedes Baums), m = MODEL_MATRIX (Drehung und gleichmäßige Größe). Gibt die
// Verschiebung im Modell zurück.
vec3 wetter_laub(vec3 v, vec3 wp, mat4 m, float zeit) {
	float h = clamp(v.y / 6.0, 0.0, 1.5);
	float ph = dot(wp.xz, vec2(0.23, 0.17));
	float s = sin(zeit * (1.1 + 1.6 * wetter_wind) + ph) * 0.7 + sin(zeit * 2.7 + ph * 1.9) * 0.3;
	float a = (0.03 + 0.3 * wetter_wind * wetter_wind * wetter_flaechen) * h; // in der Innenansicht nur ein Hauch
	vec2 r = normalize(wind_richtung + vec2(0.0001));
	vec3 aus = vec3(r.x, 0.0, r.y) * (s * 0.6 + wetter_wind * 0.8) * a;
	return transpose(mat3(m)) * aus / max(dot(m[0].xyz, m[0].xyz), 0.0001);
}
