// Charakter Phase 3: Hornhaut und Tränenfilm (eigener Code, MIT): nur Glanz und Spiegelung, additiv über Iris und
// Lederhaut (Feuchtfilm, Glanzpunkt des Schlüssellichts). Ohne eigene Farbe, schreibt keine Tiefe.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_back, specular_schlick_ggx;

uniform float rauheit = 0.035;
uniform float staerke = 1.0;

void fragment() {
	ALBEDO = vec3(0.0);
	ROUGHNESS = rauheit;
	SPECULAR = 0.5 * staerke;
	METALLIC = 0.0;
}
