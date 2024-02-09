﻿#version 410
#define MAX_POINT_LIGHTS 16

in VS_OUT{
	vec3 Color;
	vec3 Normal;
	vec2 TexCoord;
	vec3 Position;
	vec3 Tangent;
} fs_in;

uniform struct POINT_LIGHT {
	vec3 Position;
	vec3 Color;
	float Constant;
	float Linear;
	float Exp;
} PointLights[MAX_POINT_LIGHTS];
uniform int PointLightCount;

uniform struct DIRECT_LIGHT {
	vec3 Direction;
	vec3 Color;
} DirectionalLight;

//out to render texture
layout(location = 0) out vec4 frag;

//textures
uniform sampler2D AlbedoTex;	//rgba texture
uniform sampler2D NormalTex;		//normal map in tangent space
uniform sampler2D MAOSTex;		//r = metallicness, g = ambient occlusion, b = smoothness
uniform sampler2D EmissionTex;		//r = metallicness, g = ambient occlusion, b = smoothness
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

//matrices
uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;
uniform mat4 ModelMatrix;

//misc
uniform vec3 CameraWorldSpacePos;
uniform vec3 CameraDirection;

vec4 GetDirectionalLight(vec3 normal, vec3 reflectedVector) {

	//diffuse color
	float shade = min(max(dot(normal, DirectionalLight.Direction), 0), 1);
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

vec4 GetPointLight(vec3 worldPosition, vec3 normal, vec3 reflectedVector) {
	float lightFactor = 1.0f;
	vec4 col = vec4(0, 0, 0, 0);
	for (int i = 0; i < PointLightCount; i++)
	{
		float constant = PointLights[i].Constant;

		vec3 dirFromLight = PointLights[i].Position - worldPosition;
		float dist = length(dirFromLight);
		dirFromLight = normalize(dirFromLight);

		//diffuse color
		float atten = (constant 
			+ PointLights[i].Exp * dist * dist
			+ PointLights[i].Linear * dist
		);
		float shade = min(max(dot(normal, dirFromLight), 0), 1) / atten;
		vec3 lCol = PointLights[i].Color * lightFactor * shade;

		//specular color
		float specular = min(max(dot(reflectedVector, dirFromLight), 0), 1);
		specular = pow(specular, pow(Smoothness, 3) * 32);
		specular = smoothstep(0.3, 1., specular) * pow(Smoothness, 3) * 24;
		vec4 specColor = vec4(PointLights[i].Color * lightFactor, 0) * specular * shade;

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
void main(){

	vec3 viewVector = normalize(fs_in.Position.xyz - CameraWorldSpacePos.xyz);

	vec4 color = vec4(1,1,1,1);
	vec4 bump = vec4(0,0,1,0);

	mat3 TBN = mat3(fs_in.Tangent, cross(fs_in.Normal, fs_in.Tangent), fs_in.Normal);

	color = texture(AlbedoTex, fs_in.TexCoord);
	bump = texture(NormalTex, fs_in.TexCoord);
	if (bump.x == 0 && bump.y == 0 && bump.z == 0) {
		bump.xyz = vec3(0, 0, 1);
	}
	else {
		bump = bump * 2 - 1;
	}
	bump.xyz = normalize(mix(TBN * vec3(0,0,1), TBN * bump.xyz, .5));
	vec3 normal = mix(bump.xyz, fs_in.Normal, 0.0f);
	vec3 reflectedVector = reflect(viewVector, normal);

	vec4 sunColor = GetDirectionalLight(normal, reflectedVector);
	vec4 pointLightColor = GetPointLight(fs_in.Position.xyz, normal, reflectedVector);

	vec4 envColor = vec4(texture(SkyBox, reflectedVector).rgb, 1.0);
	envColor.xyz = reinhard(envColor.xyz);

	vec4 reflectionColor = mix(vec4(0), envColor, Smoothness);
	color = mix(color * vec4(AlbedoColor, 0), reflectionColor, 0.1f);
	vec4 c = color * (sunColor + pointLightColor + GetAmbientColor(normal)) + vec4(EmissiveColor, 0);

	float depth = linearDepth(get_depth(gl_FragCoord.xy / RenderSize));
	float density = 1.0 / exp(pow(depth * FogDensity, 2));
	c = mix(vec4(c), vec4(FogColor, 1), 1 - density);

	frag = c;
}