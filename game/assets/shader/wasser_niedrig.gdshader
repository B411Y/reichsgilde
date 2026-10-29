// Realistisches Wasser, Grafikstufe Niedrig: ohne Brechung, ohne Tiefentextur, ohne Kaustik und Regenringe. Farbe, Durchsicht und Uferschaum
// kommen aus dem Uferabstand im Netz (COLOR.r); Eis und Wind wie in Hoch. Code: wasser_realistisch.gdshaderinc.
shader_type spatial;

#define MIT_SCHAUM
#define MIT_EIS
#define MIT_WIND
#include "res://assets/shader/wasser_realistisch.gdshaderinc"
