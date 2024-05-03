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

// from http://www.java-gaming.org/index.php?topic=35123.0
vec4 cubic(float v) {
	vec4 n = vec4(1.0, 2.0, 3.0, 4.0) - v;
	vec4 s = n * n * n;
	float x = s.x;
	float y = s.y - 4.0 * s.x;
	float z = s.z - 4.0 * s.y + 6.0 * s.x;
	float w = 6.0 - x - y - z;
	return vec4(x, y, z, w) * (1.0 / 6.0);
}

vec4 textureBicubic(sampler2D sampler, vec2 texCoords) {

	vec2 texSize = textureSize(sampler, 0);
	vec2 invTexSize = 1.0 / texSize;

	texCoords = texCoords * texSize - 0.5;


	vec2 fxy = fract(texCoords);
	texCoords -= fxy;

	vec4 xcubic = cubic(fxy.x);
	vec4 ycubic = cubic(fxy.y);

	vec4 c = texCoords.xxyy + vec2(-0.5, +1.5).xyxy;

	vec4 s = vec4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
	vec4 offset = c + vec4(xcubic.yw, ycubic.yw) / s;

	offset *= invTexSize.xxyy;

	vec4 sample0 = texture(sampler, offset.xz);
	vec4 sample1 = texture(sampler, offset.yz);
	vec4 sample2 = texture(sampler, offset.xw);
	vec4 sample3 = texture(sampler, offset.yw);

	float sx = s.x / (s.x + s.y);
	float sy = s.z / (s.z + s.w);

	return mix(
		mix(sample3, sample2, sx), mix(sample1, sample0, sx)
		, sy);
}

void main()
{
	vec2 texelSize = MainTex_TexelSize.xy;
	vec2 pos = gl_FragCoord.xy * texelSize;
	
	vec3 low = textureBicubic(MainTex, pos).xyz;
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