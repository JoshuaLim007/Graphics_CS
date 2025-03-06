#version 460 core

//in from program
layout(location=0) in vec3 aPosition;
layout(location=1) in vec3 aColor;
layout(location=2) in vec2 aTexCoord;
layout(location=3) in vec3 aNormal;
layout(location=4) in vec3 atangent;

//out to fragment
out VS_OUT{
	vec3 Color;
	vec3 Normal;
	vec2 TexCoord;
	vec3 Position;
	vec3 Tangent;
	vec4 PositionLightSpace;
} vs_out;
out flat int DrawID;

uniform int IsBatched;
layout(std430, binding = 4) readonly buffer BatchVertexMeshUniformBuffer
{
	mat4 modelMatrices[];
} BatchData;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;
uniform mat4 DirectionalLightMatrix;
uniform vec2 UvScale;

void main(){
	mat4 modelMatrix = ModelMatrix;
	if (IsBatched == 1) {
		modelMatrix = BatchData.modelMatrices[gl_DrawID];
		DrawID = gl_DrawID;
	}
	vs_out.Color = aColor;
	vs_out.TexCoord = aTexCoord * UvScale;
	vs_out.Normal = normalize((modelMatrix * vec4(aNormal, 0.0)).xyz);
	vs_out.Position = (modelMatrix * vec4(aPosition,1)).xyz;
	vs_out.PositionLightSpace = DirectionalLightMatrix * vec4(vs_out.Position, 1);
	vs_out.Tangent = normalize((modelMatrix * vec4(atangent, 0.0)).xyz);
	gl_Position = ProjectionViewMatrix * vec4(vs_out.Position, 1);
}