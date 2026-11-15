// Neubau der Gebäude: Alpha-Cards der Strohkanten an Traufe, First und Giebel (Style-Guide 5.1) – derselbe Shader wie
// building_trim.gdshader (Rumpf building_trim.gdshaderinc), beidseitig sichtbar und mit Alpha Scissor aus der Albedo des Arrays.
shader_type spatial;
render_mode cull_disabled;

#define STROHKANTE
#include "res://assets/shader/building_trim.gdshaderinc"
