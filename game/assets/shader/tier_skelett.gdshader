// Zugpferd nah (Skelett, Stadtansicht.Zugtiere.cs): dasselbe wie das eingelesene Material „Pferd“ des GLB (Albedo-Atlas mit Mähne als Alpha-Maske,
// Normal- und ORM-Karte, beidseitig), dazu die Abzeichen (tier_abzeichen.gdshaderinc). Der Code kommt je Pferd als Instanz-Parameter, so teilen sich
// alle Pferde eines Fells ein Material.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D albedo_textur : source_color, filter_linear_mipmap, repeat_disable;
uniform bool hat_normal = false;
uniform sampler2D normal_textur : hint_normal, filter_linear_mipmap, repeat_disable;
uniform float normal_staerke = 1.0;
// ORM: R Verdeckung, G Rauheit, B Metall (wie glTF occlusion/metallicRoughness).
uniform bool hat_orm = false;
uniform sampler2D orm_textur : hint_default_white, filter_linear_mipmap, repeat_disable;
#include "res://assets/shader/tier_abzeichen.gdshaderinc"
instance uniform float abzeichen_code = 0.0;

void fragment() {
	vec4 t = texture(albedo_textur, UV);
	float huf_maske = hat_orm ? texture(orm_textur, UV).a : 0.0;
	ALBEDO = abzeichen(t.rgb, UV, abzeichen_code, huf_maske);
	ALPHA = t.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	ROUGHNESS = 0.9;
	if (hat_normal) {
		NORMAL_MAP = texture(normal_textur, UV).rgb;
		NORMAL_MAP_DEPTH = normal_staerke;
	}
	if (hat_orm) {
		vec3 orm = texture(orm_textur, UV).rgb;
		AO = orm.r;
		ROUGHNESS = orm.g;
		METALLIC = orm.b;
	}
}
