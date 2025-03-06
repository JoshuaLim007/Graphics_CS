#version 460 core

layout(location = 0) in vec3 aPos;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;
uniform mat4 prevProjectionViewModelMatrix;

smooth out vec4 vPosition;
smooth out vec4 vPrevPosition;
uniform int SkyBox;

uniform int IsBatched;
layout(std430, binding = 4) readonly buffer BatchVertexMeshUniformBuffer
{
	mat4 modelMatrices[];
} BatchData;

layout(std430, binding = 6) readonly buffer MotionVectorBatchVertexMeshUniformBuffer
{
	mat4 prevProjectionViewModelMatrices[];
} MotionVectorBatchData;


void main()
{
	mat4 modelMatrix = ModelMatrix;
	mat4 prevProjectionViewModelMatrix = prevProjectionViewModelMatrix;
	if (IsBatched == 1) {
		modelMatrix = BatchData.modelMatrices[gl_DrawID];
		prevProjectionViewModelMatrix = MotionVectorBatchData.prevProjectionViewModelMatrices[gl_DrawID];
	}

    vec4 temp = ProjectionViewMatrix * modelMatrix * vec4(aPos, 1.0);
    vPrevPosition = prevProjectionViewModelMatrix * vec4(aPos, 1.0);
    vPosition = temp;
    gl_Position = temp;
}