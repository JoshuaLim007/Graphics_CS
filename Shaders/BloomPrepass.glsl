#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;
uniform vec4 _BloomThreshold;
uniform float ClampValue;

vec3 ApplyBloomThreshold(vec3 color) {
	float brightness = max(max(color.r, color.g), color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

void main()
{
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	vec3 col = vec3(0);
	col = texture(MainTex, pos).xyz;
	col = max(col, vec3(0));
	col = min(col, vec3(ClampValue));
	col = ApplyBloomThreshold(col);

	color = vec4(col, 1.0);
}