// Häuserzeilen (modulares Wohnhaussystem, Stadtansicht.Zeilen.cs): derselbe Rumpf wie building_trim.gdshader, aber Ebene,
// AO-Ebene, Putz- und Akzentfarbe kommen je Ecke aus CUSTOM0/CUSTOM1 statt als Instanz-Uniform – so ist eine ganze Zeile aus
// Häusern verschiedener Völker und Stufen ein Netz mit einem Material.
shader_type spatial;

#define ZEILE
#include "res://assets/shader/building_trim.gdshaderinc"
