#version 430
#define MAX_POINT_LIGHTS 128
#define MAX_POINT_SHADOWS 8

struct PointLight {
	vec4 Position;
	vec4 Color;
	float Constant;
	float Linear;
	float Exp;
	float Range;
	int HasShadows;
	float ShadowFarPlane;
	int ShadowIndex;
};
layout(std140, binding = 3) uniform PointLightBuffer
{
	PointLight[MAX_POINT_LIGHTS] PointLightData;
} PL;
uniform samplerCubeShadow PointLightShadowMap[MAX_POINT_SHADOWS];
uniform int PointLightCount;

in VS_OUT{
	vec3 Color;
	vec3 Normal;
	vec2 TexCoord;
	vec3 Position;
	vec3 Tangent;
	vec4 PositionLightSpace;
} fs_in;

uniform struct DIRECT_LIGHT {
	vec3 Direction;
	vec3 Color;
} DirectionalLight;
uniform sampler2D DirectionalShadowDepthMap;				//used to sample occluder depth
uniform sampler2DShadow DirectionalShadowDepthMap_Smooth;	//used to sample actual shadow
uniform vec2 DirectionalShadowDepthMapTexelSize;			//shadow map inv resolution
uniform int DirectionalShadowFilterMode;					//0 = hard, 1 = pcf, 2 = pcss
uniform int DirectionalFilterRadius;

//out to render texture
layout(location = 0) out vec4 frag;
layout(location = 1) out vec4 norm;
layout(location = 2) out vec4 specmet;

//textures
uniform sampler2D AlbedoTex;	//rgba texture
uniform sampler2D NormalTex;	//normal map in tangent space
uniform sampler2D MAOSTex;		//r = metallicness, g = ambient occlusion, b = smoothness
uniform sampler2D EmissionTex;	//r = metallicness, g = ambient occlusion, b = smoothness
uniform int textureMask;		//texture masks

//environment
uniform vec3 SkyColor;
uniform vec3 HorizonColor;
uniform vec3 GroundColor;
uniform samplerCube EnvironmentMap;

//scalars
uniform float Smoothness;
uniform float Metalness;
uniform float NormalStrength;
uniform float AoStrength;

//colors
uniform vec3 AlbedoColor;
uniform vec3 EmissiveColor;

//misc
uniform vec3 CameraWorldSpacePos;
uniform vec3 CameraDirection;
uniform int _Frame;
uniform float DirectionalShadowRange;
uniform bool HasDirectionalShadow;
const uint k = 1103515245U;
vec3 hash(uvec3 x)
{
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;

	return vec3(x) * (1.0 / float(0xffffffffU));
}

float SampleDirectionalShadow(vec3 position, float range) {
	float x, y;
	float percentCovered = 0;
	int sampleCount = 0;
	for (y = -range; y <= range; y += 1.0) {
		for (x = -range; x <= range; x += 1.0) {
			vec2 offset = vec2(x, y) * DirectionalShadowDepthMapTexelSize;
			percentCovered += 1 - texture(DirectionalShadowDepthMap_Smooth, vec3(position.xy + offset, position.z)).r;
			sampleCount++;
		}
	}
	return percentCovered / sampleCount;
}
float GetDirectionalShadow(vec4 lightSpacePos, vec3 normal, vec3 worldPosition) {
	if(!HasDirectionalShadow){
		return 0;
	}
	vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
	projCoords.xyz = projCoords.xyz * 0.5 + 0.5;

	float percentCovered = 0.0f;
	float currentDepth = projCoords.z;

	//apply shadow fading
	float distToCam = length(worldPosition - CameraWorldSpacePos);
	float halfShadowRange = DirectionalShadowRange;
	float fade = smoothstep(halfShadowRange, max(DirectionalShadowRange - 5, 0), distToCam);
	if (fade == 0) {
		return 0;
	}

	//pcss
	if (DirectionalShadowFilterMode == 2) {
		return 0;
	}
	//pcf
	else if(DirectionalShadowFilterMode == 1) {
		percentCovered = SampleDirectionalShadow(projCoords, DirectionalFilterRadius);
	}
	//hard
	else {
		percentCovered += 1 - texture(DirectionalShadowDepthMap_Smooth, vec3(projCoords.xy, currentDepth)).r;
	}

	return smoothstep(0, 1, percentCovered) * fade;
}
float GetPointLightShadow(vec3 viewPos, vec3 fragPos, vec3 lightPos, samplerCubeShadow depthMap, float farPlane, vec3 normal) {
	
	// get vector between fragment position and light position
	vec3 fragToLight = fragPos - lightPos;
	
	float d = 1 - abs(dot(normal, normalize(fragToLight)));
	float bias = mix(0.001, 0.05, d);

	// now test for shadows
	float shadow = 0.0;
	float dist = length(fragToLight);

	//fix at 64 samples
	float samples = 4.0;
	
	float offset = 0.005 * dist;

	for (float x = -offset; x < offset; x += offset / (samples * 0.5))
	{
		for (float y = -offset; y < offset; y += offset / (samples * 0.5))
		{
			for (float z = -offset; z < offset; z += offset / (samples * 0.5))
			{
				//orient to direction to light
				vec3 offset = vec3(x, y, z);
				offset *= -sign(dot(offset, fragToLight));

				vec3 dir = fragToLight + offset;

				float currentDepth = length(dir);
				currentDepth /= farPlane;

				vec4 t = vec4(normalize(dir), currentDepth - bias);
				shadow += 1 - texture(depthMap, t).r;
			}
		}
	}

	return smoothstep(0, 1, shadow / float(samples * samples * samples));
}

vec4 GetAmbientColor(vec3 normal) {
	float skyMix = dot(normal, vec3(0, 1, 0));
	float horizonMix = 1 - abs(skyMix);
	float floorMix = clamp(-skyMix, 0, 1);
	skyMix = clamp(skyMix, 0, 1);

	vec3 mixValues = vec3(skyMix, horizonMix, floorMix);
	mixValues = normalize(mixValues);
	vec3 finalCol = SkyColor * mixValues.x + HorizonColor * mixValues.y + GroundColor * mixValues.z;
	return vec4(finalCol, 0);
}

uniform sampler2D _CameraDepthTexture;
uniform sampler2D _SSGIColor;
uniform sampler2D _MotionTexture;
uniform vec3 FogColor;
uniform float FogDensity;
uniform vec4 CameraParams;
uniform vec2 RenderSize;
float get_depth(vec2 pos)
{
	float d = texture(_CameraDepthTexture, pos).r * 2 - 1;
	return d;
}
float linearDepth(float depthSample)
{
	depthSample = depthSample * 0.5 + 0.5;
	float zLinear = CameraParams.z * CameraParams.w / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
	return zLinear;
}
vec3 reinhard(vec3 v)
{
	return v / (1.0f + v);
}
uniform samplerCube SkyBox;
const float PI = 3.14159265359;
float DistributionGGX(vec3 N, vec3 H, float a)
{
	float a1 = a * a;
	float a2 = a1 * a1;
	float NdotH = max(dot(N, H), 0.0);
	float NdotH2 = NdotH * NdotH;

	float nom = a2;
	float denom = (NdotH2 * (a2 - 1.0) + 1.0);
	denom = PI * denom * denom;

	return nom / denom;
}
const float denomMin = 0.0001;
float GeometrySchlickGGX(float NdotV, float k)
{
	float nom = NdotV;
	float denom = NdotV * (1.0 - k) + k;

	return nom / max(denom, denomMin);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float k)
{
	k = k + 1;
	k = k * k;
	k /= 8.0;
	float NdotV = max(dot(N, V), denomMin);
	float NdotL = max(dot(N, L), denomMin);
	float ggx1 = GeometrySchlickGGX(NdotV, k);
	float ggx2 = GeometrySchlickGGX(NdotL, k);

	return ggx1 * ggx2;
}
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
	return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}
vec3 halfVector(vec3 viewVector, vec3 lightDir){
	return normalize(vec3(viewVector + lightDir));
}
void main(){

	vec3 viewVector = normalize(CameraWorldSpacePos.xyz - fs_in.Position.xyz);
	vec4 color = vec4(1,1,1,1);
	vec4 bump = vec4(0,0,1,0);
	mat3 TBN = mat3(fs_in.Tangent, cross(fs_in.Normal, fs_in.Tangent), normalize(fs_in.Normal));
	color = texture(AlbedoTex, fs_in.TexCoord);
	bump = texture(NormalTex, fs_in.TexCoord);
	if (bump.x == 1 && bump.y == 1 && bump.z == 1) {
		bump.xyz = vec3(0, 0, 1);
	}
	else {
		bump.xyz = bump.xyz * 2 - 1;
	}
	bump.xyz = TBN * bump.xyz;
	vec3 normal = mix(normalize(fs_in.Normal), normalize(bump.xyz), NormalStrength);
	normal = normalize(normal);
	vec3 reflectedVector = reflect(-viewVector, normal);

	//Lambertian BRDF
	//directional light
	float sunShadow = GetDirectionalShadow(fs_in.PositionLightSpace, normalize(fs_in.Normal), fs_in.Position);
	vec3 sunColor = max(dot(normal, DirectionalLight.Direction), 0) * (1 - sunShadow) * DirectionalLight.Color;
	color.xyz *= AlbedoColor;

	vec3 incomingLightDiffuse = sunColor;
	vec3 diffuse = color.xyz / PI;
	vec3 maos = texture(MAOSTex, fs_in.TexCoord).xyz;

	float Smoothness = maos.b * Smoothness;
	float Metalness = maos.r * Metalness;
	vec3 baseRef = mix(vec3(0.05), diffuse.xyz, Metalness);
	float roughness = 1 - Smoothness;

	//BRDF
	vec3 h = halfVector(viewVector, DirectionalLight.Direction);
	float D = DistributionGGX(normal, h, roughness);
	float G = GeometrySmith(normal, viewVector, DirectionalLight.Direction, roughness);
	float denom = 4 * max(dot(normal, DirectionalLight.Direction), denomMin) * max(dot(normal, viewVector), denomMin);
	vec3 fresnal = fresnelSchlick(max(dot(viewVector, h), 0), baseRef);
	float reflectanceBRDF = D * G / denom; 
	vec3 kd = 1 - fresnal;
	kd *= (1 - Metalness);

	vec3 brdf = (kd * diffuse + fresnal * vec3(reflectanceBRDF)) * incomingLightDiffuse;
	vec2 screenUV = gl_FragCoord.xy / RenderSize.xy;
	vec2 mv = texture(_MotionTexture, screenUV).xy;
	vec2 projectUv = screenUV - mv;
	projectUv.x = projectUv.x < 0 ? 0 : projectUv.x;
	projectUv.y = projectUv.y < 0 ? 0 : projectUv.y;
	projectUv.x = projectUv.y > 1 ? 1 : projectUv.x;
	projectUv.y = projectUv.y > 1 ? 1 : projectUv.y;
	vec3 ssgi = texture(_SSGIColor, projectUv).xyz;
	vec3 diffuseAmbientColor = GetAmbientColor(normal).xyz + ssgi;
	brdf += (1 - Metalness) * (diffuse * diffuseAmbientColor);

	//point light
	for (int i = 0; i < PointLightCount; i++)
	{
		float constant = PL.PointLightData[i].Constant;
		vec3 dirFromLight = PL.PointLightData[i].Position.xyz - fs_in.Position;
		float dist = length(dirFromLight);
		dirFromLight = normalize(dirFromLight);
		float atten = (constant 
			+ PL.PointLightData[i].Exp * dist * dist
			+ PL.PointLightData[i].Linear * dist
		);
		float shade = min(max(dot(normal, dirFromLight), 0), 1) / atten;
		if (PL.PointLightData[i].HasShadows == 1) {
			int sIndex = PL.PointLightData[i].ShadowIndex;
			shade *= (1 - GetPointLightShadow(CameraWorldSpacePos, fs_in.Position, PL.PointLightData[i].Position.xyz, PointLightShadowMap[sIndex], PL.PointLightData[i].ShadowFarPlane, fs_in.Normal));
		}
		shade *= 1 - smoothstep(PL.PointLightData[i].Range * 0.75f, PL.PointLightData[i].Range, dist);
		vec3 lCol = PL.PointLightData[i].Color.xyz * shade;
		incomingLightDiffuse = lCol;

		//BRDF
		h = halfVector(viewVector, dirFromLight);
		D = DistributionGGX(normal, h, roughness);
		G = GeometrySmith(normal, viewVector, dirFromLight, roughness);
		denom = 4 * max(dot(normal, dirFromLight), denomMin) * max(dot(normal, viewVector), denomMin);
		fresnal = fresnelSchlick(max(dot(viewVector, h), 0), baseRef);
		reflectanceBRDF = D * G / denom; 
		kd = 1 - fresnal;
		kd *= (1 - Metalness);

		brdf += (kd * diffuse + fresnal * vec3(reflectanceBRDF)) * incomingLightDiffuse;
	}

	vec4 c = vec4(brdf + EmissiveColor, 0);

	float depth = linearDepth(get_depth(gl_FragCoord.xy / RenderSize));
	float density = 1.0 / exp(pow(depth * FogDensity, 2));
	c = mix(vec4(c), vec4(FogColor, 1), 1 - density);

	norm = vec4(normal, 0);
	specmet = vec4(roughness, baseRef.xyz);
	frag = c;
}