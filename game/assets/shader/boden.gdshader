// Boden der Geländekacheln, Stufe Hoch (auch Ultra): Höhenmischung, Kachel-Brechung, Normalen und Rauheit, dreiseitig am Hang.
// Der Shader steht in boden.gdshaderinc; boden_mittel.gdshader und boden_niedrig.gdshader sind die einfacheren Stufen.
shader_type spatial;
#define BODEN_STUFE 2
#include "res://assets/shader/boden.gdshaderinc"
