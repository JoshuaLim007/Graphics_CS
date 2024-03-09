#version 410

layout(location = 0) out vec4 color;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D HighResTex;
uniform vec2 HighResTex_TexelSize;
uniform float intensity;
uniform int iterations;
uniform int doNormalize;

//bicubic filtering:
//https://github.com/SableRaf/Processing-Experiments/blob/master/2013/Shaders/filtersAndBlendModes/Filters/Bicubic/data/shader.glsl

vec3 reinhard(vec3 v)
{
	return v / (1.0f + v);
}
float lum(vec3 color) {
	return (0.299 * color.x + 0.587 * color.y + 0.114 * color.z);
}

float w0(float a)
{
	return (1.0 / 6.0) * (a * (a * (-a + 3.0) - 3.0) + 1.0);
}

float w1(float a)
{
	return (1.0 / 6.0) * (a * a * (3.0 * a - 6.0) + 4.0);
}

float w2(float a)
{
	return (1.0 / 6.0) * (a * (a * (-3.0 * a + 3.0) + 3.0) + 1.0);
}

float w3(float a)
{
	return (1.0 / 6.0) * (a * a * a);
}

// g0 and g1 are the two amplitude functions
float g0(float a)
{
	return w0(a) + w1(a);
}

float g1(float a)
{
	return w2(a) + w3(a);
}

// h0 and h1 are the two offset functions
float h0(float a)
{
	return -1.0 + w1(a) / (w0(a) + w1(a));
}

float h1(float a)
{
	return 1.0 + w3(a) / (w2(a) + w3(a));
}

vec4 texture2D_bicubic(sampler2D tex, vec2 uv, vec2 res)
{
	uv = uv * res + 0.5;
	vec2 iuv = floor(uv);
	vec2 fuv = fract(uv);

	float g0x = g0(fuv.x);
	float g1x = g1(fuv.x);
	float h0x = h0(fuv.x);
	float h1x = h1(fuv.x);
	float h0y = h0(fuv.y);
	float h1y = h1(fuv.y);

	vec2 p0 = (vec2(iuv.x + h0x, iuv.y + h0y) - 0.5) / res;
	vec2 p1 = (vec2(iuv.x + h1x, iuv.y + h0y) - 0.5) / res;
	vec2 p2 = (vec2(iuv.x + h0x, iuv.y + h1y) - 0.5) / res;
	vec2 p3 = (vec2(iuv.x + h1x, iuv.y + h1y) - 0.5) / res;

	return g0(fuv.y) * (g0x * texture2D(tex, p0) +
		g1x * texture2D(tex, p1)) +
		g1(fuv.y) * (g0x * texture2D(tex, p2) +
			g1x * texture2D(tex, p3));
}

void main()
{
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	
	//vec3 low = texture(MainTex, pos).xyz;
	vec3 low = texture2D_bicubic(MainTex, pos, vec2(1.0f / texelSize.x, 1.0f / texelSize.y)).xyz;
	vec3 high = texture(HighResTex, pos).xyz;

	if (doNormalize == 0) {
		color = vec4(high + low * intensity, 1.0);
	}
	else {
		high = max(high, vec3(0));
		low = low * intensity;
		color = vec4(high + low, 1.0);

		//high = max(high, vec3(0));
		//low = low * intensity / iterations;
		//vec3 highToned = reinhard(high);
		//vec3 lowToned = reinhard(low);
		//float backgroundBrightness = lum(highToned);
		//float foregroundBrightness = lum(lowToned);
		//float diff = max(foregroundBrightness - backgroundBrightness, 0);
		//vec3 final = mix(high, low, diff);
		//color = vec4(final, 1.0);
	}
}