#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;
uniform float _BloomThreshold;
uniform float ClampValue;
float lum(vec3 color) {
	return (0.299 * color.x + 0.587 * color.y + 0.114 * color.z);
}
vec3 ApplyBloomThreshold(vec3 col) {
	float b = lum(col);
	float w = max(0, b - _BloomThreshold) / max(b, 0.00001);
	return col * w;
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