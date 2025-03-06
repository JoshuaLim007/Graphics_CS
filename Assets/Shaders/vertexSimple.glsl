#version 460 core

//in from program
layout(location = 0) in vec3 aPosition;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;

uniform int IsBatched;
layout(std430, binding = 4) readonly buffer BatchVertexMeshUniformBuffer
{
	mat4 modelMatrices[];
} BatchData;

void main() {
	mat4 modelMatrix = ModelMatrix;
	if (IsBatched == 1) {
		modelMatrix = BatchData.modelMatrices[gl_DrawID];
	}
	gl_Position = ProjectionViewMatrix * modelMatrix * vec4(aPosition, 1.0);
}