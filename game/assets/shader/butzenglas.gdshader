// Fensterglas der Innenansicht (Neubau der Gebäude, Stadtansicht.Neubau.cs): Butzenscheiben in Blei, wie man sie um 1400 kannte –
// grünlich-gelbes Waldglas, jede Butze mit Schleuderringen und dem dicken Nabel in der Mitte, dazwischen Zwickel aus Glasresten.
// Man sieht hindurch (die echte Stadt hinter dem Fenster aus dem Bildschirm-Bild), aber verzogen, leicht trüb und getönt.
// Eigener Shader des Projekts (MIT), nach eigener Recherche (docs/bereiche/darstellung.md); keine fremde Textur.
shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

#include "butzen.gdshaderinc"

uniform sampler2D schirm : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform vec3 glasfarbe : source_color = vec3(0.70, 0.80, 0.56);   // Waldglas: Eisen im Sand färbt grünlich
uniform vec3 bleifarbe : source_color = vec3(0.10, 0.10, 0.11);
uniform float brechung : hint_range(0.0, 0.1) = 0.004;            // wie stark die Ringe das Bild dahinter verziehen
uniform float truebung : hint_range(0.0, 4.0) = 0.4;              // Unschärfe (Mip-Stufe des Bilds dahinter)
uniform float helligkeit : hint_range(0.0, 2.0) = 1.0;

void fragment() {
	float kennung;
	vec4 b = butzen(UV, kennung);
	float fein = max(fwidth(UV.x), fwidth(UV.y));
	float blei = butzen_blei(b.y, fein);
	float radius = 0.5 * BUTZEN_TEILUNG;
	bool zwickel = b.x > radius;
	// Schleuderringe: Wellen um die Mitte, zum Nabel hin steiler; Zwickel sind flache Scherben mit eigener Neigung
	float ringe = sin(b.x * 420.0 + kennung * 6.283) * 0.3 + sin(b.x * 170.0 + kennung * 17.0) * 0.45;
	float nabel = 1.0 - smoothstep(0.0, 0.011, b.x);
	vec2 neigung = zwickel ? (vec2(kennung, fract(kennung * 7.31)) - 0.5) * 0.6 : b.zw * (ringe * 0.55 + nabel * 1.6);
	vec2 versatz = neigung * brechung * (1.0 - blei);
	vec3 dahinter = textureLod(schirm, clamp(SCREEN_UV + versatz, vec2(0.001), vec2(0.999)), truebung + nabel * 1.5).rgb;
	// Dicke des Glases: am Nabel und am Rand dunkler und grüner, jede Butze etwas anders
	float dicke = 0.4 + 0.7 * nabel + 0.2 * smoothstep(0.75 * radius, radius, b.x) + 0.22 * (kennung - 0.5) + 0.03 * ringe;
	vec3 ton = pow(glasfarbe, vec3(max(dicke, 0.2)));
	vec3 glas = dahinter * ton * helligkeit;
	ALBEDO = bleifarbe * blei;
	EMISSION = glas * (1.0 - blei);
	ROUGHNESS = mix(0.3, 0.55, blei);
	METALLIC = 0.6 * blei;
	SPECULAR = mix(0.15, 0.5, blei);   // das Glas selbst spiegelt kaum: Der Blick nach draußen soll bleiben
	NORMAL = normalize(NORMAL + (TANGENT * neigung.x + BINORMAL * neigung.y) * 0.03 * (1.0 - blei));
}
