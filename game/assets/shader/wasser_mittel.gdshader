// Realistisches Wasser, Grafikstufe Mittel: ohne Brechung (keine Bildschirmtextur, der teuerste Teil) und ohne Regenringe; das Flache wird
// stattdessen durchsichtig. Tiefe, Kaustik, Schaum, Eis und Wind wie in Hoch. Code: wasser_realistisch.gdshaderinc.
shader_type spatial;

#define MIT_TIEFE
#define MIT_KAUSTIK
#define MIT_SCHAUM
#define MIT_EIS
#define MIT_WIND
#include "res://assets/shader/wasser_realistisch.gdshaderinc"
