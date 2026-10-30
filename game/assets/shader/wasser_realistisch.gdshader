// Realistisches Wasser, Grafikstufen Hoch und Ultra: alles (Brechung, Tiefe, Kaustik, Schaum, Eis, Wind, Regenringe). Der Code steht in
// wasser_realistisch.gdshaderinc (Herkunft und MIT-Lizenz des Originals dort); wasser_mittel.gdshader und wasser_niedrig.gdshader sind die
// einfacheren Stufen. Die Testszene (--wassertest=1, --wasserteile=-…) schaltet einzelne Teile ab, indem sie die #define-Zeilen auskommentiert.
shader_type spatial;

#define MIT_BRECHUNG
#define MIT_TIEFE
#define MIT_KAUSTIK
#define MIT_SCHAUM
#define MIT_EIS
#define MIT_WIND
#define MIT_RINGE
#define MIT_FEINKARTEN
#include "res://assets/shader/wasser_realistisch.gdshaderinc"
