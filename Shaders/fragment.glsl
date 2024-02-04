#version 410
#define MAX_POINT_LIGHTS 8

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
	specular = pow(specular, 32);
	specular = smoothstep(0.3, 1., specular) * 24;
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
		specular = pow(specular, 32);
		specular = smoothstep(0.3, 1., specular) * 24;
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
uniform vec4 CameraParams;
float get_depth()
{
	float d = gl_FragCoord.z * 2 - 1;
	return d;
}
float linearDepth(float depthSample)
{
	float zLinear = 2.0 * CameraParams.z * CameraParams.w / (CameraParams.w + CameraParams.z - depthSample * (CameraParams.w - CameraParams.z));
	return zLinear;
}

void main(){

	vec3 viewVector = normalize(fs_in.Position.xyz - CameraWorldSpacePos.xyz);

	vec4 color = vec4(1,1,1,1);
	vec4 bump = vec4(0,0,1,0);

	mat3 TBN = mat3(fs_in.Tangent, cross(fs_in.Normal, fs_in.Tangent), fs_in.Normal);

	color = texture(AlbedoTex, fs_in.TexCoord);
	bump = texture(NormalTex, fs_in.TexCoord) * 2 - 1;
	bump.xyz = normalize(mix(TBN * vec3(0,0,1), TBN * bump.xyz, .5));
	vec3 normal = bump.xyz;
	vec3 reflectedVector = reflect(viewVector, normal);

	vec4 sunColor = GetDirectionalLight(normal, reflectedVector);
	vec4 pointLightColor = GetPointLight(fs_in.Position.xyz, normal, reflectedVector);

	vec4 c = (color * vec4(AlbedoColor, 0)) * (sunColor + pointLightColor + GetAmbientColor(normal)) + vec4(EmissiveColor,0);

	//float d = linearDepth(get_depth());
	//float density = 1.0 / pow(2.71828, pow(d * .1f, 2));
	//c = mix(c, vec4(1,1,1,1), 1 - density);

	frag = c;
}