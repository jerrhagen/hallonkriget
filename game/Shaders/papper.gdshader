// Pappret: hela skärmen multipliceras med en papperstextur och ett svagt pigmentbrus.
// Ligger på en ColorRect i ett CanvasLayer över allt annat. Fast mot skärmen, som en boksida.
shader_type canvas_item;
render_mode blend_mul, unshaded;

uniform sampler2D paper_texture : repeat_enable, filter_linear;
// Hur många skärmpixlar en texturpixel täcker
uniform float paper_scale = 1.0;
// 0 = inget papper, 1 = full textur
uniform float strength : hint_range(0.0, 1.0) = 0.9;
// Fläckigt pigment: små korn i ett grövre mönster
uniform float pigment_noise : hint_range(0.0, 0.2) = 0.05;
// Mörkare hörn, som en gammal bok
uniform float vignette : hint_range(0.0, 1.0) = 0.22;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void fragment() {
	vec2 px = FRAGCOORD.xy;
	vec2 tex_size = vec2(textureSize(paper_texture, 0)) * paper_scale;
	vec3 paper = texture(paper_texture, px / tex_size).rgb;
	float grain = hash(floor(px / 2.0)) * 0.6 + hash(floor(px / 7.0)) * 0.4;
	vec3 col = mix(vec3(1.0), paper, strength) * (1.0 - pigment_noise * grain);
	vec2 uv = SCREEN_UV - 0.5;
	col *= 1.0 - vignette * dot(uv, uv) * 1.6;
	COLOR = vec4(col, 1.0);
}
