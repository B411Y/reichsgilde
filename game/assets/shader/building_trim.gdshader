// Master-Shader der neu gebauten Gebäude (Style-Guide „3D-Asset-Pipeline“, Abschnitt 5.2; Probe: Stadtansicht.Neubau.cs).
// Alle Häuser einer Gruppe teilen ein Trim-Sheet (waagerechte Bänder, UV1) und dieses eine Material; Putz-, Holz- und
// Akzentfarbe (Fensterläden, Eckenfarbe Rot) stellt das Spiel je Haus als Instanz-Uniform ein – ohne neues Material.
// UV2 trägt die gebackene Verdeckung des einzelnen Hauses (Kontaktschatten unter Dachüberstand und Balken).
// Die Namen der Uniforms stehen so im Style-Guide und bleiben deshalb englisch.
shader_type spatial;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic;
uniform sampler2D orm_tex : filter_linear_mipmap_anisotropic;
uniform sampler2D ao_uv2_tex : filter_linear; // Gebackene Haus-AO auf UV2

uniform float normal_strength : hint_range(0.0, 2.0) = 1.0;
uniform float ao_uv2_strength : hint_range(0.0, 1.0) = 0.75;

// Pro Gebäude-Instanz in Godot variierbar (ohne neues Material!):
instance uniform vec3 plaster_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 wood_tint : source_color = vec3(1.0, 1.0, 1.0);
instance uniform vec3 accent_tint : source_color = vec3(1.0, 1.0, 1.0); // z.B. Fensterläden

void fragment() {
	vec4 tex = texture(albedo_tex, UV);
	vec3 orm = texture(orm_tex, UV).rgb;
	float house_ao = texture(ao_uv2_tex, UV2).r;

	vec3 final_color = tex.rgb;

	// 1. Variation über Trim-Sheet-Bänder (UV.y)
	if (UV.y >= 0.25 && UV.y < 0.50) {
		final_color *= wood_tint;
	} else if (UV.y >= 0.50 && UV.y < 0.70) {
		final_color *= plaster_tint;
	}

	// 2. Variation über Vertex-Color-Maske (z.B. Fensterläden auf Rot-Kanal)
	final_color = mix(final_color, final_color * accent_tint, COLOR.r);

	ALBEDO = final_color;
	NORMAL_MAP = texture(normal_tex, UV).rgb;
	NORMAL_MAP_DEPTH = normal_strength;

	// Kombiniert Trim-Sheet-AO mit der individuellen Haus-AO (UV2)
	AO = orm.r * mix(1.0, house_ao, ao_uv2_strength);
	ROUGHNESS = orm.g;
	METALLIC = orm.b;
}
