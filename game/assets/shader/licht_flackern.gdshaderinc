// Gemeinsame Rauschfunktionen der Lichter (Fensterlicht #203, Nachtlichter #207): ein Hash und glattes Rauschen in der Zeit,
// damit jedes Licht seinen eigenen Takt hat und die Flammen nach derselben Technik flackern wie die Fenster.

uint f_hash_u(uvec4 v) {
	uint n = v.x * 1597334673u ^ v.y * 3812015801u ^ v.z * 2798796415u ^ v.w * 1979697957u;
	n = (n ^ (n >> 15u)) * 2246822519u;
	n = (n ^ (n >> 13u)) * 3266489917u;
	return n ^ (n >> 16u);
}

float f_hash(uvec4 v) {
	return float(f_hash_u(v) >> 8u) * (1.0 / 16777216.0);
}

// Glattes Rauschen in der Zeit (0–1) mit eigenem Startwert je Fenster
float f_rausch(float t, uint s) {
	float i = floor(t);
	float f = fract(t);
	f = f * f * (3.0 - 2.0 * f);
	uint k = uint(int(i) & 0xFFFFFF);
	return mix(f_hash(uvec4(k, s, 7u, 0u)), f_hash(uvec4(k + 1u, s, 7u, 0u)), f);
}
