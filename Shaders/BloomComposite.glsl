#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D HighResTex;
uniform vec2 HighResTex_TexelSize;
uniform float intensity;
uniform int iterations;
uniform int doNormalize;

vec3 reinhard(vec3 v)
{
	return v / (1.0f + v);
}
float lum(vec3 color) {
	return (0.299 * color.x + 0.587 * color.y + 0.114 * color.z);
}
void main()
{
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	
	vec3 low = texture(MainTex, pos).xyz;
	vec3 high = texture(HighResTex, pos).xyz;

	if (doNormalize == 0) {
		color = vec4(high + low * intensity, 1.0);
	}
	else {
		low = low * intensity / iterations;

		vec3 highToned = reinhard(high);
		vec3 lowToned = reinhard(low);
		float backgroundBrightness = lum(highToned);
		float foregroundBrightness = lum(lowToned);

		float diff = max(foregroundBrightness - backgroundBrightness, 0);
		vec3 final = mix(high, low, diff);
		color = vec4(final, 1.0);
	}
}