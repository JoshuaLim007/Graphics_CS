#version 410
#include "common.frag"

layout(location = 0) out vec4 color;

uniform int Horizontal;
uniform int UseAntiFlicker;

// Downsample with a 4x4 box filter + anti-flicker filter
vec3 DownsampleAntiFlickerFilter(sampler2D tex, vec2 uv)
{
	vec4 d = MainTex_TexelSize.xyxy * vec4(-1.0, -1.0, 1.0, 1.0);

	vec3 s1 = texture(tex, uv + d.xy).rgb;
	vec3 s2 = texture(tex, uv + d.zy).rgb;
	vec3 s3 = texture(tex, uv + d.xw).rgb;
	vec3 s4 = texture(tex, uv + d.zw).rgb;

	// Karis's luma weighted average
	float s1w = 1.0 / (lum(s1) + 1.0);
	float s2w = 1.0 / (lum(s2) + 1.0);
	float s3w = 1.0 / (lum(s3) + 1.0);
	float s4w = 1.0 / (lum(s4) + 1.0);
	float one_div_wsum = 1.0 / (s1w + s2w + s3w + s4w);

	return vec3((s1 * s1w + s2 * s2w + s3 * s3w + s4 * s4w) * one_div_wsum);
}

void main()
{
	float offsets[9] = float[9](
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
		);
	float weights[9] = float[9](
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
		);
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	vec3 col = vec3(0);

	if (Horizontal == 1) {
		for (int i = 0; i < 9; i++) {
			float offset = offsets[i] * texelSize.x;

			if (UseAntiFlicker == 0) {
				col += texture(MainTex, pos + vec2(offset, 0.0)).rgb * weights[i];
			}
			else {
				col += DownsampleAntiFlickerFilter(MainTex, pos + vec2(offset, 0.0)).rgb * weights[i];
			}
		}
	}
	else {
		for (int i = 0; i < 9; i++) {
			float offset = offsets[i] * texelSize.y;

			if (UseAntiFlicker == 0) {
				col += texture(MainTex, pos + vec2(0.0, offset)).rgb * weights[i];
			}
			else {
				col += DownsampleAntiFlickerFilter(MainTex, pos + vec2(0.0, offset)).rgb * weights[i];
			}
		}
	}

	color = vec4(col, 0);
}