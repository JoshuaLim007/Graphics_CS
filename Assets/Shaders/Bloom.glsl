#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;
uniform int Horizontal;

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
			col += texture(MainTex, pos + vec2(offset, 0.0)).rgb * weights[i];
		}
	}
	else {
		for (int i = 0; i < 9; i++) {
			float offset = offsets[i] * texelSize.y;
			col += texture(MainTex, pos + vec2(0.0, offset)).rgb * weights[i];
		}
	}

	color = vec4(col, 0);
}