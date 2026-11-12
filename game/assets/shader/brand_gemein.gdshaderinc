// Gemeinsames der Brandspuren am Gebäude (Brandschaden): Rauschen, Zellmuster (Krokodilhaut) und die Rußfahnen über allen Öffnungen.
// Die Öffnungen eines Hauses liegen in einer kleinen Float-Textur: Spalte i, Zeile 0 = Mitte (xyz, Weltlage) und Breite (w),
// Zeile 1 = waagerechte Normale (xyz) und Höhe (w). So braucht kein Haus eine Reihe Einzelwerte, und die Zahl ist nicht begrenzt.

uniform sampler2D oeffnungen : filter_nearest, repeat_disable;
uniform int oeffnungen_anzahl = 0;

float b_hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Glattes Raumrauschen (0 bis 1)
float b_rauschen(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = mix(mix(b_hash(i), b_hash(i + vec3(1, 0, 0)), f.x), mix(b_hash(i + vec3(0, 1, 0)), b_hash(i + vec3(1, 1, 0)), f.x), f.y);
	float b = mix(mix(b_hash(i + vec3(0, 0, 1)), b_hash(i + vec3(1, 0, 1)), f.x), mix(b_hash(i + vec3(0, 1, 1)), b_hash(i + vec3(1, 1, 1)), f.x), f.y);
	return mix(a, b, f.z);
}

// Zellmuster im Raum: x = Abstand zum nächsten Zellkern, y = Abstand zur Zellkante (0 im Riss), z = Zahl der Zelle (0 bis 1).
// Sparsam: nur 2 x 2 x 2 Zellen (statt 27), die Kerne liegen in der Mitte ihrer Zelle (0,25 bis 0,75), dann liegt der nächste immer im Block.
vec3 b_zellen(vec3 p) {
	vec3 i0 = floor(p - 0.5);
	float d1 = 8.0, d2 = 8.0, id = 0.0;
	for (int x = 0; x < 2; x++)
		for (int y = 0; y < 2; y++)
			for (int z = 0; z < 2; z++) {
				vec3 c = i0 + vec3(float(x), float(y), float(z));
				vec3 k = vec3(b_hash(c + 11.0), b_hash(c + 23.0), b_hash(c + 37.0));
				float d = length(c + 0.25 + 0.5 * k - p);
				if (d < d1) { d2 = d1; d1 = d; id = b_hash(c + 5.0); }
				else if (d < d2) { d2 = d; }
			}
	return vec3(d1, d2 - d1, id);
}

// Die Rußfahnen: je Öffnung ein umgekehrter Kegel (V) über dem Sturz, dunkel am Sturz, nach oben breiter und blasser; dazu ein
// angekohlter Saum um die Öffnung. Nur auf der Wand, zu der die Öffnung gehört (Abstand zur Wandebene und Richtung der Fläche).
float b_fahnen(vec3 p, vec3 n, float rausch) {
	float best = 0.0;
	for (int i = 0; i < oeffnungen_anzahl; i++) {
		vec4 a = texelFetch(oeffnungen, ivec2(i, 0), 0);
		vec3 d = p - a.xyz;
		if (dot(d, d) > 40.0) continue; // weiter als etwa 6 m weg: keine Fahne mehr
		vec4 b = texelFetch(oeffnungen, ivec2(i, 1), 0);
		float dn = dot(d, b.xyz);
		if (abs(dn) > 0.9 || dot(n, b.xyz) < 0.25) continue;
		float dx = dot(d.xz, vec2(-b.z, b.x));
		float halbh = b.w * 0.5;
		float halbb = a.w * 0.5;
		// Saum: gleich um die Öffnung herum angekohlt
		vec2 rand = max(vec2(abs(dx) - halbb, abs(d.y) - halbh), vec2(0.0));
		float saum = (1.0 - smoothstep(0.0, 0.28 + 0.12 * rausch, length(rand))) * 0.9;
		// Fahne über dem Sturz
		float hoch = d.y - halbh * 0.8;
		float laenge = b.w * 2.6 + 0.9;
		float t = hoch / laenge;
		float fahne = 0.0;
		if (t > -0.12 && t < 1.0) {
			float halb = halbb * (0.95 + 1.5 * clamp(t, 0.0, 1.0)) + 0.08;
			float quer = abs(dx) / halb;
			float seite = 1.0 - smoothstep(0.45 + 0.25 * rausch, 1.0 + 0.15 * rausch, quer);
			float oben = 1.0 - smoothstep(0.25, 1.0 - 0.3 * rausch, max(t, 0.0));
			fahne = clamp(seite * oben * (1.0 + 0.7 * (rausch - 0.4)), 0.0, 1.0);
		}
		best = max(best, max(saum, fahne));
	}
	return clamp(best, 0.0, 1.0);
}
