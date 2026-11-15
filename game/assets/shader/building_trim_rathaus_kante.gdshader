// Rathaus: Alpha-Cards der Schieferkanten – derselbe Shader wie building_trim_rathaus.gdshader, beidseitig sichtbar und mit Alpha Scissor.
shader_type spatial;
render_mode cull_disabled;

#define SCHIEFERKANTE
#include "res://assets/shader/building_trim_rathaus.gdshaderinc"
