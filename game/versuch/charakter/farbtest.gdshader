// Charakter Phase 6 (S0): Farbtest – kommt eine Farbe über set_shader_parameter richtig im Shader an?
// Godot rechnet einen übergebenen Color von sRGB nach linear um, einen Vector3 nicht – auch bei `source_color`.
// Bogen P10 (Prüfkörper „farbtest“): Vector3(0,5) -> linear 0,5, Color(0,5) -> linear 0,214 (wie StandardMaterial3D).
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 farbe : source_color = vec3(1.0);

void fragment() {
	ALBEDO = farbe;
}
