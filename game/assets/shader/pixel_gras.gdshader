// Pixel-Look (Testszene Pixeltest), Runde 4: ruhiges Gras. Die Halme zeigen mit beiden Seiten nach oben (kein umgedrehter Normalenvektor auf der Rückseite),
// sie werden also wie der Boden beleuchtet statt dunkel zu flackern – das war die Hauptquelle des Strichrauschens im Low-Res-Bild. Eigenbau (MIT).
shader_type spatial;
render_mode cull_disabled;

void fragment() {
	vec3 c = COLOR.rgb;   // Vertexfarbe in sRGB
	ALBEDO = mix(pow((c + 0.055) / 1.055, vec3(2.4)), c / 12.92, vec3(lessThan(c, vec3(0.04045))));
	ROUGHNESS = 1.0;
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
}
