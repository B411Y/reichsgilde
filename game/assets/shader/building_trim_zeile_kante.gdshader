// Häuserzeilen: Alpha-Cards der Strohkanten (wie building_trim_kante.gdshader), Werte je Ecke wie building_trim_zeile.gdshader.
shader_type spatial;
render_mode cull_disabled;

#define ZEILE
#define STROHKANTE
#include "res://assets/shader/building_trim.gdshaderinc"
