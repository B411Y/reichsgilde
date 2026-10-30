// Charakter Phase 3: Lidschatten (ICT „EyeOcclusion“-Netz, eigener Code, MIT): dunkelt den Augapfel unter den Lidern ab
// (Multiplikation), damit das Auge nicht aufgeklebt wirkt. Eckfarbe R = Dunkelheit (aus der Nähe zur Lidnaht, oben stärker).
shader_type spatial;
render_mode blend_mul, unshaded, depth_draw_never, cull_back, shadows_disabled;

uniform float staerke = 0.75;
uniform vec3 farbe : source_color = vec3(0.32, 0.24, 0.22);

void fragment() {
	ALBEDO = mix(vec3(1.0), farbe, clamp(COLOR.r * staerke, 0.0, 1.0));
}
