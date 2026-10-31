// Realistisches Wasser, Grafikstufe Mittel: ohne Brechung (keine Bildschirmtextur, der teuerste Teil); das Flache wird stattdessen durchsichtig.
// Tiefe, Kaustik, Schaum, Eis, Wind und Regenringe (nur Normale, billig; Regen 4) wie in Hoch. Code: wasser_realistisch.gdshaderinc.
shader_type spatial;

#define MIT_TIEFE
#define MIT_KAUSTIK
#define MIT_SCHAUM
#define MIT_EIS
#define MIT_WIND
#define MIT_RINGE
#define MIT_FEINKARTEN
#include "res://assets/shader/wasser_realistisch.gdshaderinc"
