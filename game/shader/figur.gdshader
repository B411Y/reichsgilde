// Figuren (figur_<volk>.glb, PR #149): ein Material „Figur“ für alle Teile, damit eine Figur ein Zeichenaufruf ist statt sechs.
// Jede Fläche trägt ihre Zone in UV.x und ihre Grundfarbe in COLOR (als sRGB-Wert): 0 = feste Farbe (Auge, Leder, Holz, Eisen …),
// 1 = Gewand, 2 = Beinkleid, 3 = Haut, 4 = Haar, 5 = Kopfbedeckung. Die Farben der Zonen 1–5 setzt das Spiel je Figur als
// Instanz-Uniform (Stadtansicht.Figuren.cs) – alle Figuren teilen sich dieses eine Material.
shader_type spatial;
render_mode cull_disabled;

instance uniform vec4 gewand : source_color = vec4(0.5, 0.4, 0.3, 1.0);
instance uniform vec4 beinkleid : source_color = vec4(0.35, 0.3, 0.25, 1.0);
instance uniform vec4 haut : source_color = vec4(0.85, 0.66, 0.52, 1.0);
instance uniform vec4 haar : source_color = vec4(0.3, 0.2, 0.12, 1.0);
instance uniform vec4 kopfbedeckung : source_color = vec4(0.6, 0.5, 0.3, 1.0);

varying flat int zone;
varying vec3 grundfarbe;

void vertex() {
	zone = int(round(UV.x));
	// sRGB → linear wie bei StandardMaterial3D mit vertex_color_is_srgb (der Compatibility-Renderer rechnet in sRGB)
	grundfarbe = COLOR.rgb;
	if (!OUTPUT_IS_SRGB) {
		grundfarbe = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
}

void fragment() {
	vec3 farbe = grundfarbe;
	if (zone == 1) farbe = gewand.rgb;
	else if (zone == 2) farbe = beinkleid.rgb;
	else if (zone == 3) farbe = haut.rgb;
	else if (zone == 4) farbe = haar.rgb;
	else if (zone == 5) farbe = kopfbedeckung.rgb;
	ALBEDO = farbe;
	ROUGHNESS = 0.9;
}
