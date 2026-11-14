// Butzenscheiben (Fensterglas um 1400): runde, in der Glashütte geschleuderte Scheiben von etwa 10 cm, dicht an dicht in Bleiruten
// gefasst; die Zwickel dazwischen sind mit Glasresten gefüllt. Eine Funktion für das Glas der Innenansicht (butzenglas.gdshader)
// und das Bleinetz außen (bleinetz.gdshader): beide teilen die Scheibe nach derselben Koordinate (Meter entlang der Wand und über
// der Fensterbank, aus dem Modell), also sitzen die Ruten innen und außen an derselben Stelle.
const float BUTZEN_TEILUNG = 0.108;   // Abstand der Scheibenmitten (Meter)
const float BUTZEN_BLEI = 0.008;      // Breite der Bleirute

// p: Lage auf der Scheibe in Metern. Ergebnis: x = Abstand von der Mitte der Butze (Meter), y = Abstand zur Mitte der Bleirute
// (Meter, 0 = mitten auf dem Blei), zw = Richtung von der Mitte der Butze nach außen. `kennung` unterscheidet die Butzen.
vec4 butzen(vec2 p, out float kennung) {
	float reihenhoehe = BUTZEN_TEILUNG * 0.8660254;
	float reihe = floor(p.y / reihenhoehe);
	float bester = 1e9;
	vec2 mitte = vec2(0.0);
	for (int i = 0; i < 2; i++) {
		float r = reihe + float(i);
		float versatz = mod(r, 2.0) * 0.5 * BUTZEN_TEILUNG;
		vec2 m = vec2((floor((p.x - versatz) / BUTZEN_TEILUNG + 0.5)) * BUTZEN_TEILUNG + versatz, r * reihenhoehe);
		float d = distance(p, m);
		if (d < bester) { bester = d; mitte = m; }
	}
	kennung = fract(sin(dot(mitte, vec2(127.1, 311.7))) * 43758.5453);
	float radius = 0.5 * BUTZEN_TEILUNG;                 // die Ruten liegen auf den Kreisen, die sich gerade berühren
	vec2 richtung = bester > 1e-5 ? (p - mitte) / bester : vec2(0.0, 1.0);
	return vec4(bester, abs(bester - radius), richtung);
}

// Deckung des Bleis (0–1), geglättet über die Breite eines Bildpunkts (`fein` = Meter je Bildpunkt): nah scharfe Ruten, in der
// Ferne ein gleichmäßiger Schleier statt Flimmern.
float butzen_blei(float zur_rute, float fein) {
	float halb = 0.5 * BUTZEN_BLEI;
	float scharf = 1.0 - smoothstep(halb - 0.5 * fein, halb + 0.5 * fein, zur_rute);
	float schleier = clamp(BUTZEN_BLEI * 3.6276 / BUTZEN_TEILUNG, 0.0, 1.0);   // Flächenanteil des Bleis (Umfang × Breite je Sechseck)
	return mix(scharf, schleier, smoothstep(0.5 * BUTZEN_BLEI, 2.5 * BUTZEN_BLEI, fein));
}
