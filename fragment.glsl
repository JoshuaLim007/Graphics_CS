#version 410
#define MAX_POINT_LIGHTS 8

in VS_OUT{
	vec3 Color;
	vec3 Normal;
	vec2 TexCoord;
	vec3 Position;
	vec3 Tangent;
} fs_in;

//out to screen
out vec4 frag;

uniform sampler2D albedoTex;	//rgba texture
uniform sampler2D bumpTex;		//normal map in tangent space
uniform sampler2D MAOSETex;		//r = metallicness, g = ambient occlusion, b = smoothness, a = emission
uniform int textureMask;		//texture masks

//environment
uniform vec3 SkyColor;
uniform vec3 HorizonColor;
uniform vec3 GroundColor;
uniform vec3[MAX_POINT_LIGHTS] PointLightPositions;
uniform vec3[MAX_POINT_LIGHTS] PointLightColors;
uniform int PointLightCount;
uniform vec3 DirectionalLightDirection;
uniform vec3 DirectionalLightColor;

//scalars
uniform float Smoothness;
uniform float Metalness;
uniform float NormalStrength;
uniform float AoStrength;

//colors
uniform vec3 AlbedoColor;
uniform vec3 EmissiveColor;

//matrices
uniform mat4 _viewMatrix;
uniform mat4 _projectionMatrix;
uniform mat4 _modelMatrix;

//misc
uniform vec3 _CameraWorldSpacePos;
uniform vec3 _CameraDirection;

void main(){

	vec3 viewVector = normalize(fs_in.Position.xyz - _CameraWorldSpacePos.xyz);
	vec3 sunDirection = normalize(vec3(1,1,1));

	vec4 color = vec4(1,1,1,1);
	vec4 bump = vec4(0,0,1,0);

	mat3 TBN = mat3(fs_in.Tangent, cross(fs_in.Normal, fs_in.Tangent), fs_in.Normal);

	if(textureMask > 0){
		color = texture(albedoTex, fs_in.TexCoord);
	}
	if(textureMask > 1){
		bump = texture(bumpTex, fs_in.TexCoord) * 2 - 1;
	}

	bump.xyz = normalize(mix(TBN * vec3(0,0,1), TBN * bump.xyz, .5));


	vec3 normal = bump.xyz;
	vec3 shade = vec3(max(dot(normal, sunDirection), 0)) * vec3(20,17,12);
	vec3 reflectedVector = reflect(viewVector, normal);
	float specular = max(dot(reflectedVector, sunDirection), 0);
	specular = pow(specular, 8) * 4;

	vec4 c = vec4((color.xyz) * (shade + vec3(1.5,2,4)),0) + specular;

	c.r *= AlbedoColor.r;
	c.g *= AlbedoColor.g;
	c.b *= AlbedoColor.b;

	frag = c / (1 + c);
}