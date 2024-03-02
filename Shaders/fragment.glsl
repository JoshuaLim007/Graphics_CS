#version 430
#define MAX_POINT_LIGHTS 128

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
uniform samplerCube PointLightShadowMap[MAX_POINT_LIGHTS];
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
uniform sampler2DShadow DirectionalShadowDepthMap;
uniform vec2 DirectionalShadowDepthMapTexelSize;

//out to render texture
layout(location = 0) out vec4 frag;

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
const uint k = 1103515245U;
vec3 hash(uvec3 x)
{
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;

	return vec3(x) * (1.0 / float(0xffffffffU));
}

float GetDirectionalShadow(vec4 lightSpacePos, vec3 normal) {
	float bias = mix(0.0001f, 0.0, abs(dot(normal, DirectionalLight.Direction)));

	vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
	projCoords.xyz = projCoords.xyz * 0.5 + 0.5;

	if (projCoords.z > 1.0) {
		return 0;
	}

	float percentCovered = 0.0f;
	const int kernalHalfSize = 1;
	const float stepSize = 0.5f;
	int size = kernalHalfSize * 2 + 1;
	float currentDepth = projCoords.z;
	const int samples = 16;
	const int scale = 1000;
	const float spread = 1;
	uvec3 scaledPos = uvec3(abs(gl_FragCoord.x) * scale, abs(gl_FragCoord.y) * scale, 0);

	for (int i = 0; i < samples; i++)
	{
		int iscale = i * scale + _Frame;
		vec2 Offsets = hash(uvec3(scaledPos.x, scaledPos.y, iscale)).xy;
		Offsets.x *= DirectionalShadowDepthMapTexelSize.x * spread;
		Offsets.y *= DirectionalShadowDepthMapTexelSize.y * spread;
		vec3 UVC = vec3(projCoords.xy + Offsets, currentDepth - bias);
		percentCovered += 1 - texture(DirectionalShadowDepthMap, UVC);
	}

	//int samples = 0;
	//for (float i = -kernalHalfSize; i <= kernalHalfSize; i += stepSize)
	//{
	//	for (float j = -kernalHalfSize; j <= kernalHalfSize; j += stepSize)
	//	{
	//		samples++;
	//		vec2 Offsets = vec2(i * DirectionalShadowDepthMapTexelSize.x, j * DirectionalShadowDepthMapTexelSize.y);
	//		vec3 UVC = vec3(projCoords.xy + Offsets, currentDepth + bias);
	//		percentCovered += 1 - texture(DirectionalShadowDepthMap, UVC);
	//	}
	//}

	return percentCovered / samples;
}
float GetPointLightShadow(vec3 viewPos, vec3 fragPos, vec3 lightPos, samplerCube depthMap, float far_plane, vec3 normal) {
	// get vector between fragment position and light position
	vec3 fragToLight = fragPos - lightPos;
	// now get current linear depth as the length between the fragment and light position
	float currentDepth = length(fragToLight);
	// now test for shadows
	float dot = abs(dot(normal, normalize(fragToLight)));
	float bias = mix(.75, 0.025f, dot);

	float shadow = 0.0;
	int samples = 16;
	float viewDistance = length(lightPos - fragPos);
	float diskRadius = 0.1f;
	const int scale = 1000;
	uvec3 scaledPos = uvec3(abs(gl_FragCoord.x) * scale, abs(gl_FragCoord.y) * scale, 0);
	for (int i = 0; i < samples; ++i)
	{
		int iscale = i * scale + _Frame;
		vec3 randDir = hash(uvec3(scaledPos.x, scaledPos.y, iscale));
		randDir = normalize(randDir);
		float closestDepth = texture(depthMap, fragToLight + randDir * diskRadius).r;
		closestDepth *= far_plane;   // undo mapping [0;1]
		if (currentDepth - bias > closestDepth)
			shadow += 1.0;
	}
	shadow /= float(samples);


	return shadow;
}

vec4 GetDirectionalLight(vec3 normal, vec3 geoNormal, vec3 reflectedVector) {

	//diffuse color
	float shadow = GetDirectionalShadow(fs_in.PositionLightSpace, geoNormal);
	float shade = min(max(dot(normal, DirectionalLight.Direction), 0), 1);
	shade = min(shade, 1 - shadow);
	vec3 sunColor = DirectionalLight.Color * shade;

	//specular color
	float specular = max(dot(reflectedVector, DirectionalLight.Direction), 0);
	specular = pow(specular, pow(Smoothness, 3) * 32);
	specular = smoothstep(0.3, 1., specular) * pow(Smoothness, 3) * 24;
	vec4 specColor = vec4(DirectionalLight.Color, 0) * specular * shade;

	//combined color
	vec4 c = vec4(sunColor, 0) + specColor;
	return c;
}
vec4 GetPointLight(vec3 cameraPosition, vec3 worldPosition, vec3 normal, vec3 reflectedVector) {
	float lightFactor = 1.0f;
	vec4 col = vec4(0, 0, 0, 0);
	for (int i = 0; i < PointLightCount; i++)
	{
		float constant = PL.PointLightData[i].Constant;

		vec3 dirFromLight = PL.PointLightData[i].Position.xyz - worldPosition;
		float dist = length(dirFromLight);
		dirFromLight = normalize(dirFromLight);

		//diffuse color
		float atten = (constant 
			+ PL.PointLightData[i].Exp * dist * dist
			+ PL.PointLightData[i].Linear * dist
		);
		float shade = min(max(dot(normal, dirFromLight), 0), 1) / atten;

		if (PL.PointLightData[i].HasShadows == 1) {
			int sIndex = PL.PointLightData[i].ShadowIndex;
			shade *= (1 - GetPointLightShadow(cameraPosition, worldPosition, PL.PointLightData[i].Position.xyz, PointLightShadowMap[sIndex], PL.PointLightData[i].ShadowFarPlane, fs_in.Normal));
		}

		shade *= 1 - smoothstep(PL.PointLightData[i].Range * 0.75f, PL.PointLightData[i].Range, dist);

		vec3 lCol = PL.PointLightData[i].Color.xyz * lightFactor * shade;
		//specular color
		float specular = min(max(dot(reflectedVector, dirFromLight), 0), 1);
		specular = pow(specular, pow(Smoothness, 3) * 32);
		specular = smoothstep(0.3, 1., specular) * pow(Smoothness, 3) * 24;
		vec4 specColor = vec4(PL.PointLightData[i].Color.xyz * lightFactor, 0) * specular * shade;

		//combined color
		col += vec4(lCol, 0) + specColor;
	}
	return col;
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
	float zLinear = 2.0 * CameraParams.z * CameraParams.w / (CameraParams.w + CameraParams.z - depthSample * (CameraParams.w - CameraParams.z));
	return zLinear;
}
vec3 reinhard(vec3 v)
{
	return v / (1.0f + v);
}
uniform samplerCube SkyBox;
const float PI = 3.1415;
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
const float denomMin = 0.001;
float GeometrySchlickGGX(float NdotV, float k)
{
	float nom = NdotV;
	float denom = NdotV * (1.0 - k) + k;

	return nom / max(denom, denomMin);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float k)
{
	k = k * k;
	k /= 2;
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
		bump.xy = bump.xy * 2 - 1;
	}
	bump.xyz = TBN * bump.xyz;
	vec3 normal = mix(normalize(fs_in.Normal), normalize(bump.xyz), NormalStrength);
	normal = normalize(normal);
	vec3 reflectedVector = reflect(-viewVector, normal);

	//Lambertian BRDF
	//directional light
	float sunShadow = GetDirectionalShadow(fs_in.PositionLightSpace, normalize(fs_in.Normal));
	vec3 sunColor = max(dot(normal, DirectionalLight.Direction), 0) * (1 - sunShadow) * DirectionalLight.Color;
	color.xyz *= AlbedoColor;

	vec3 incomingLightDiffuse = sunColor;
	vec3 diffuse = color.xyz / PI;

	float roughness = 1 - Smoothness;
	vec3 baseRef = mix(vec3(0.01), color.xyz, Metalness);

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

	vec3 diffuseAmbientColor = GetAmbientColor(normal).xyz;
	brdf += (1 - Metalness) * diffuse * diffuseAmbientColor;

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
	//vec4 envColor = vec4(0, 0, 0, 0);
	//vec4 diffuseAmbientColor = GetAmbientColor(normal);
	//vec4 reflectionColor = mix(vec4(0), envColor, Smoothness);
	//color = mix(color * vec4(AlbedoColor, 0), reflectionColor, 0.1f);
	//vec4 sunColor = GetDirectionalLight(normal, fs_in.Normal, reflectedVector);
	//vec4 pointLightColor = GetPointLight(CameraWorldSpacePos, fs_in.Position.xyz, normal, reflectedVector);
	//vec4 c = color * (sunColor + pointLightColor + diffuseAmbientColor) + vec4(EmissiveColor, 0);

	float depth = linearDepth(get_depth(gl_FragCoord.xy / RenderSize));
	float density = 1.0 / exp(pow(depth * FogDensity, 2));
	c = mix(vec4(c), vec4(FogColor, 1), 1 - density);
	
	frag = c;
}