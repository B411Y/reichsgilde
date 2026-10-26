// Wetter und Jahreszeiten (PR #195, Stadtansicht.Wetter.cs): gemeinsame Werte und Rechnungen für Boden (boden.gdshaderinc), Grasteppich
// (gras.gdshader), Äcker (acker.gdshader) und die Modelle (oberflaeche.gdshader). Alle Werte sind globale Shader-Uniforms (project.godot,
// [shader_globals]): Die Stadtansicht setzt sie einmal je Bild, und jeder Shader liest dieselben – kein Material muss angefasst werden.
// Ohne Partie (Hauptmenü, Kartenwerkstatt) gelten die Grundwerte aus project.godot: Sommer, trocken, leichter Wind.

// Nässe des Bodens 0–1 (Regen der letzten Stunden) und Regen gerade jetzt 0–1 (Ringe in den Pfützen); in der Stufe Niedrig 0
global uniform float wetter_naesse;
global uniform float wetter_regen;
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
// Anteile der Jahreszeiten (Frühling, Sommer, Herbst, Winter), zusammen 1: beim Jahreswechsel gleitet die Farbe in Echtzeit über
global uniform vec4 jahreszeit_anteile;

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

// Pfützen (0–1): erst ab halber Nässe, dann in den Mulden (Rauschen und tiefe Stellen der Textur, tiefe 0–1), mit mehr Nässe größer
float pfuetze(vec2 wp, float tiefe) {
	if (wetter_naesse < 0.35) return 0.0;
	float m = w_rausch(wp * 0.21 + 3.0) * 0.7 + w_rausch(wp * 0.9 + 1.7) * 0.3 + tiefe * 0.25;
	float schwelle = 1.12 - 0.36 * smoothstep(0.35, 1.0, wetter_naesse);
	return smoothstep(schwelle, schwelle + 0.04, m);
}

// Ringe der Regentropfen in einer Pfütze: Neigung der Wasserfläche (x, z) – zwei Gitter zu 0,7 m, je Zelle ein Tropfen mit eigenem Takt
vec2 regenringe(vec2 p, float zeit) {
	vec2 neig = vec2(0.0);
	for (int k = 0; k < 2; k++) {
		vec2 q = p / 0.7 + float(k) * vec2(0.37, 0.61);
		vec2 zelle = floor(q);
		float z = w_hash(zelle + float(k) * 17.0);
		vec2 mitte = zelle + 0.2 + 0.6 * vec2(z, w_hash(zelle + 31.0));
		float t = fract(zeit * 0.9 + z);
		vec2 d = q - mitte;
		float r = length(d);
		float welle = sin((r - t * 0.6) * 38.0) * (1.0 - t) * smoothstep(0.0, 0.05, r) * (1.0 - smoothstep(t * 0.6, t * 0.6 + 0.12, r));
		neig += d / max(r, 0.001) * welle;
	}
	return neig * 0.35 * wetter_regen;
}

// Wetter auf dem Boden (boden.gdshaderinc): Nässe macht ihn dunkler und glänzend, in Mulden stehen Pfützen, die den Himmel spiegeln (als Leuchten,
// denn die Stadt hat keine Spiegelungen); Schnee deckt ihn zu, auf Wegen (aufweg 0–1) ist er ausgetreten und zu Matsch geworden, in der Stadt
// (stadt 0–1) zertreten und grauer. tiefe = Höhe der Textur umgekehrt (0–1). nw ist die Normale in der Welt.
void wetter_boden(inout vec3 bf, inout float rau, inout vec3 nw, inout vec3 leuchten, vec2 wp, vec3 n, float aufweg, float stadt, float tiefe, float wiese, bool ringe, float zeit) {
	if (wetter_naesse > 0.001) {
		float saugt = mix(1.0, 0.7, stadt);
		bf *= 1.0 - 0.38 * wetter_naesse * saugt;
		// Glanz nass: auf Wiese nur ein Drittel (sonst glitzert das Gras), im Pflaster schmal – nur einzelne Steine glänzen, der Rest bleibt matt
		float stein = smoothstep(0.62, 0.8, w_rausch(wp * 2.3 + 9.1));
		float glanz = 0.38 + 0.2 * wiese + stadt * (0.24 - 0.2 * stein);
		rau = mix(rau, min(rau, glanz), wetter_naesse);
		// Wege und Gassen bleiben dauerhaft etwas dunkler (Matsch, Abdrücke), unabhängig vom Licht
		bf *= 1.0 - 0.12 * wetter_naesse * max(aufweg, stadt * 0.6);
		float p = pfuetze(wp, tiefe) * (0.3 + 0.7 * max(aufweg, stadt * 0.3)) * smoothstep(0.9, 0.97, n.y);
		if (p > 0.0) {
			// Wasserfarbe: dunkel, aber zum Himmel hin getönt – so bleibt die Pfütze aus jeder Richtung ein Wasserfleck und kein dunkler Boden
			bf = mix(bf, bf * 0.3 + wetter_himmel * mix(0.22, 0.16, stadt), p);
			rau = mix(rau, 0.04, p);
			// Himmelsschein in der Farbe: aus jeder Richtung als Wasser lesbar; Tropfenringe als Hell-dunkel-Zeichnung
			vec3 schein = wetter_himmel * 0.2;
			if (ringe) {
				vec2 r0 = regenringe(wp, zeit);
				float zeichn = clamp(length(r0) * 2.2, 0.0, 1.0);
				bf *= 1.0 - 0.4 * zeichn;
				schein += wetter_himmel * 0.3 * zeichn;
			}
			vec3 glatt = n;
			if (ringe) {
				vec2 r = regenringe(wp, zeit);
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

// Wetter auf den Modellen (oberflaeche.gdshader): Schnee auf allem, was nach oben zeigt (Dächer, Mauerkronen, Brunnen …), Nässe dunkler und
// glänzender. wn = Normale in der Welt, wp = Ort in der Welt. dach = der Ort in der Ebene der Fläche (Meter: x waagerecht, y die Fläche hinauf,
// wie die Projektion „reihen“ der Material-Bibliothek), reihe = Höhe einer Ziegelreihe (Meter).
// Schnee liegt in der Dachebene und nicht in der Welt-x/z-Ebene (dort würde er an Schrägen gestreckt): Aus der Nähe in Reihen gegliedert – die
// Flecken laufen mit den Reihen, in jeder Reihenkante liegt er dichter –, mit wachsendem Abstand (fwidth der Reihen) wird das Muster weich zu einer
// ruhigen Decke gemittelt. Auf flachen Flächen (Mauerkronen) gibt es keine Reihen, dort gilt dasselbe Fleckenmuster ohne Kanten.
void wetter_flaeche(inout vec3 c, inout float rau, vec3 wn, vec3 wp, vec2 dach, float reihe) {
	if (wetter_flaechen < 0.5) return;
	if (wetter_naesse > 0.001) {
		float oben = clamp(wn.y, 0.0, 1.0);
		c *= 1.0 - 0.3 * wetter_naesse * (0.4 + 0.6 * oben);
		rau = mix(rau, rau * 0.45, wetter_naesse * (0.4 + 0.6 * oben));
	}
	if (wetter_dach > 0.001) {
		float pv = dach.y / reihe;
		float fein = 1.0 - smoothstep(0.05, 0.28, fwidth(pv));   // 1 nah genug für Reihen, 0 zu weit
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
