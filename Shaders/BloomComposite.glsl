#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D HighResTex;
uniform vec2 HighResTex_TexelSize;
uniform float intensity;

void main()
{
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	
	vec3 low = texture(MainTex, pos).xyz;
	vec3 high = texture(HighResTex, pos).xyz;

	color = vec4(high + low * intensity, 1.0);
}