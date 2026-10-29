// Versuch Charakter Phase 2: Brauen und Wimpern als Karten (eigener Code, MIT). Bild: MakeHuman eyebrow001 bzw.
// eyelashes01 (CC0, steckt in der .glb). Aus dem Bild kommen nur Deckkraft und etwas Helligkeit, die Farbe aus den
// Haar-Genen; Kanten über Alpha-to-Coverage (braucht MSAA, die Bühne hat 4×).
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D bild : source_color, filter_linear_mipmap_anisotropic;
uniform vec3 farbe : source_color = vec3(0.12, 0.08, 0.05);
uniform float schwelle = 0.12;

void fragment() {
	vec4 t = texture(bild, UV);
	float l = dot(t.rgb, vec3(0.3333));
	ALBEDO = farbe * (0.75 + 0.5 * l);
	ALPHA = t.a;
	ALPHA_SCISSOR_THRESHOLD = schwelle;
	ALPHA_ANTIALIASING_EDGE = schwelle;
	ALPHA_TEXTURE_COORDINATE = UV * vec2(textureSize(bild, 0));
	ROUGHNESS = 0.55;
	SPECULAR = 0.3;
}
