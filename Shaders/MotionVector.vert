#version 410

layout(location = 0) in vec3 aPos;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;
uniform mat4 prevProjectionViewModelMatrix;

smooth out vec4 vPosition;
smooth out vec4 vPrevPosition;

void main()
{
    vPosition = ProjectionViewMatrix * ModelMatrix * vec4(aPos, 1.0);
    vPrevPosition = prevProjectionViewModelMatrix * vec4(aPos, 1.0);
    gl_Position = vPosition;
}