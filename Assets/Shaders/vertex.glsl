#version 410

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

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;
uniform mat4 DirectionalLightMatrix;
uniform vec2 UvScale;

void main(){
	vs_out.Color = aColor;
	vs_out.TexCoord = aTexCoord * UvScale;
	vs_out.Normal = normalize((ModelMatrix * vec4(aNormal, 0.0)).xyz);
	vs_out.Position = (ModelMatrix * vec4(aPosition,1)).xyz;
	vs_out.PositionLightSpace = DirectionalLightMatrix * vec4(vs_out.Position, 1);
	vs_out.Tangent = normalize((ModelMatrix * vec4(atangent, 0.0)).xyz);
	gl_Position = ProjectionViewMatrix * vec4(vs_out.Position, 1);
}