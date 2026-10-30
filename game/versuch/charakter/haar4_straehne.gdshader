// Charakter Phase 4: TressFX-Strähnen für Frisur und Bart.
// Grundlage: addons/tressfx/shaders/tressfx_strand.gdshader aus „TressFX for Godot“ (MIT, Copyright (c) 2026 Ismail
// Ivanov; Algorithmen aus AMD TressFX 4.1, MIT, Copyright (c) 2020 Advanced Micro Devices, Inc.; Lizenztext:
// addons/tressfx/LICENSE.md). Die Uniforms positions, strand_uvs, verts_per_strand, lod_* setzt TressFXHair.
// Eigene Ergänzungen (MIT): Farbe aus den Genen, Ergrauen je Haar (Salz und Pfeffer), Glatze nach Rang
// (strand_uvs.y, aus tools/charakter/kopf4_haar.py), Ausdünnen im Alter, Bartlänge (Strähne endet früher).
shader_type spatial;
render_mode world_vertex_coords, cull_disabled, depth_prepass_alpha;

uniform sampler2D positions : filter_nearest;
uniform sampler2D strand_uvs : filter_nearest;
uniform int verts_per_strand = 8;
instance uniform int lod_bucket = 0;
const int LOD_BUCKETS = 4;
uniform float lod_start = 1.0;
uniform float lod_end = 5.0;
uniform float lod_width_multiplier = 1.0;

uniform vec3 farbe : source_color = vec3(0.25, 0.16, 0.09);
uniform vec3 farbe_grau : source_color = vec3(0.8, 0.79, 0.77);
uniform float grau = 0.0;
uniform float kahl = 0.0;
uniform float duenn = 0.0;
uniform float laenge = 1.0;
uniform float dichte = 1.0;
uniform float fiber_radius = 0.00035;
uniform float fiber_ratio = 0.3;
uniform float expand_pixels = 0.71;
uniform float k_diffuse = 0.22;
uniform float k_spec1 = 0.035;
uniform float spec_exp1 = 60.0;
uniform float k_spec2 = 0.12;
uniform float spec_exp2 = 14.0;
uniform float cone_angle_deg = 10.0;
uniform float root_shadow : hint_range(0.0, 1.0) = 0.45;
uniform float color_variation : hint_range(0.0, 1.0) = 0.45;
uniform float tip_fade : hint_range(0.0, 1.0) = 0.35;
uniform float spitze_hell = 1.25;

varying vec3 v_tangent;
varying vec3 v_color;
varying float v_t;
varying float v_side;
varying float v_half_width;
varying float v_expanded;
varying float v_weg;
varying float v_ende;

vec3 unpack_tangent(float p) {
	float x = floor(p / 4096.0);
	vec2 e = vec2(x, p - x * 4096.0) / 4095.0 * 2.0 - 1.0;
	vec3 n = vec3(e, 1.0 - abs(e.x) - abs(e.y));
	float t = max(-n.z, 0.0);
	n.xy += vec2(n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t);
	return normalize(n);
}

float hash1(float n) {
	return fract(sin(n * 12.9898) * 43758.5453);
}

void vertex() {
	int hair_vertex = VERTEX_ID >> 1;
	int strand = lod_bucket + (hair_vertex / verts_per_strand) * LOD_BUCKETS;
	int gi = strand * verts_per_strand + hair_vertex % verts_per_strand;
	float side = float(VERTEX_ID & 1) * 2.0 - 1.0;
	int w = textureSize(positions, 0).x;
	vec4 d = texelFetch(positions, ivec2(gi % w, gi / w), 0);
	vec3 p = d.xyz;
	vec3 t = unpack_tangent(d.w);

	v_t = float(gi % verts_per_strand) / float(verts_per_strand - 1);
	int wu = textureSize(strand_uvs, 0).x;
	vec2 suv = texelFetch(strand_uvs, ivec2(strand % wu, strand / wu), 0).xy;   // x = Zufall, y = Rang
	float h1 = hash1(float(strand));
	float h2 = hash1(float(strand) * 7.31 + 1.7);
	float h3 = hash1(float(strand) * 3.17 + 5.3);
	// Glatze (Rang je Leitsträhne), Ausdünnen und Bartdichte je Haar
	v_weg = step(suv.y, kahl) * step(0.001, kahl) + step(h2, duenn) * step(0.001, duenn) + step(dichte, h3) * step(dichte, 0.999);
	v_ende = laenge * (0.75 + 0.35 * h1);

	float ratio = mix(1.0, fiber_ratio, v_t);
	vec3 eye = INV_VIEW_MATRIX[3].xyz;
	float lod = smoothstep(lod_start, lod_end, distance(eye, p));
	float r = fiber_radius * ratio * mix(1.0, lod_width_multiplier, lod);
	vec3 right = normalize(cross(t, normalize(p - eye)));
	mat4 vp = PROJECTION_MATRIX * VIEW_MATRIX;
	vec4 c0 = vp * vec4(p - right * r, 1.0);
	vec4 c1 = vp * vec4(p + right * r, 1.0);
	vec2 s0 = c0.xy / max(c0.w, 1e-4) * VIEWPORT_SIZE * 0.5;
	vec2 s1 = c1.xy / max(c1.w, 1e-4) * VIEWPORT_SIZE * 0.5;
	v_half_width = max(0.5 * length(s1 - s0), 1e-4);
	float px = r / v_half_width;
	v_expanded = v_half_width + expand_pixels;
	v_side = side;
	VERTEX = p + right * side * v_expanded * px;
	NORMAL = normalize(cross(right, t));
	v_tangent = (VIEW_MATRIX * vec4(t, 0.0)).xyz;

	vec3 c = farbe * (1.0 + color_variation * (h1 - 0.5) * 1.2);
	c *= mix(vec3(1.0), vec3(1.0 + (h3 - 0.5) * 0.25, 1.0, 1.0 - (h3 - 0.5) * 0.25), color_variation);
	vec3 g = farbe_grau * (0.9 + 0.2 * h1);
	c = mix(c, g, step(fract(h1 * 2.37 + suv.x * 0.5), grau));
	v_color = c * mix(1.0, spitze_hell, smoothstep(0.5, 1.0, v_t));
}

void fragment() {
	if (v_weg > 0.5 || v_t > v_ende) {
		discard;
	}
	ALBEDO = v_color;
	float dist = abs(v_side) * v_expanded;
	ALPHA = clamp((v_expanded - dist) / max(expand_pixels, 1e-3), 0.0, 1.0);
	float ende = min(1.0, v_ende);
	ALPHA *= smoothstep(ende, ende - tip_fade * ende, v_t);
	AO = mix(1.0 - root_shadow * (1.0 - 0.6 * grau), 1.0, v_t);
	AO_LIGHT_AFFECT = 1.0;
}

void light() {
	vec3 T = normalize(v_tangent);
	float cos_tl = dot(T, LIGHT);
	float sin_tl = sqrt(max(0.0, 1.0 - cos_tl * cos_tl));
	float cos_trl = -cos_tl;
	float sin_trl = sin_tl;
	float cos_te = dot(T, VIEW);
	float sin_te = sqrt(max(0.0, 1.0 - cos_te * cos_te));
	float a = radians(cone_angle_deg);
	float cos_root = cos_trl * cos(2.0 * a) - sin_trl * sin(2.0 * a);
	float sin_root = sqrt(max(0.0, 1.0 - cos_root * cos_root));
	float spec_root = max(0.0, cos_root * cos_te + sin_root * sin_te);
	float cos_tip = cos_trl * cos(-3.0 * a) - sin_trl * sin(-3.0 * a);
	float sin_tip = sqrt(max(0.0, 1.0 - cos_tip * cos_tip));
	float spec_tip = max(0.0, cos_tip * cos_te + sin_tip * sin_te);
	vec3 light_col = LIGHT_COLOR * ATTENUATION / PI;
	DIFFUSE_LIGHT += (k_diffuse * sin_tl + k_spec2 * pow(spec_tip, spec_exp2)) * light_col;
	SPECULAR_LIGHT += k_spec1 * pow(spec_root, spec_exp1) * light_col;
}
