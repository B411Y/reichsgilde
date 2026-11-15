// Master-Shader der neu gebauten Gebäude (Style-Guide „3D-Asset-Pipeline“, Abschnitt 5.2; Probe: Stadtansicht.Neubau.cs).
// Alle Häuser einer Gruppe teilen ein Trim-Sheet (waagerechte Bänder, UV1) und dieses eine Material; Putz-, Holz- und
// Akzentfarbe (Fensterläden, Eckenfarbe Rot) stellt das Spiel je Haus als Instanz-Uniform ein – ohne neues Material.
// UV2 trägt die gebackene Verdeckung des einzelnen Hauses (Kontaktschatten unter Dachüberstand und Balken) aus dem AO-Atlas
// der Gruppe. Die Namen der Uniforms stehen so im Style-Guide und bleiben deshalb englisch.
//
// Über den Guide hinaus (Nutzer, 14.11.2026), damit sich die neuen Häuser wie die übrigen Modelle verhalten:
// - Wetter wie in oberflaeche.gdshader (wetter.gdshaderinc): nasses Dach und nasse Wände bei Regen, Schutz unter der Traufe,
//   Schnee auf dem Dach. Welche Fläche Dach ist, sagt das Band (UV.y unter 0,25 = Stroh).
// - Pixel-Look (pixel_look = 1, setzt das Spiel): gröbere Mip-Stufe der Farbe und flachere Normale – ruhige Flächen wie die
//   Pixel-Fassung der Material-Bibliothek statt flimmernder Halme. Das Stroh bekommt dafür gezeichnete Halmstriche: Bahnen
//   von 14 cm Breite und etwa einem halben Meter Länge in drei Helligkeiten, die auch in groben Bildpunkten lesbar bleiben.
//
// Steinebenen je Volk (Nutzer, 15.11.2026): Die Trim-Sheets liegen als Texture2DArray vor, eine Ebene je Blatt mit gleichem
// Bandlayout (tools/modelle/neubau/trimsheet_ebenen.py). Welche Ebene ein Haus zeigt, sagt die Instanz-Uniform „ebene“ (Volk x Stufe,
// Steinebenen.cs), welche Zelle des AO-Atlas „ao_ebene“ (Gruppe). So teilen sich alle Häuser aller Völker EIN Material.
//
// Der Rumpf steht in building_trim.gdshaderinc; building_trim_kante.gdshader ist dieselbe Datei für die Alpha-Cards der Strohkanten
// (beidseitig, Alpha Scissor). Brandschaden ist eine Variante desselben Shaders (russ_tint), kein eigenes Material mit Texturen.
shader_type spatial;

#include "res://assets/shader/building_trim.gdshaderinc"
