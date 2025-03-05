#version 410

layout(location = 0) in vec3 aPos;

uniform mat4 ProjectionViewMatrix;
uniform mat4 ModelMatrix;
uniform mat4 prevProjectionViewModelMatrix;

smooth out vec4 vPosition;
smooth out vec4 vPrevPosition;
uniform int SkyBox;

void main()
{
    vec4 temp = ProjectionViewMatrix * ModelMatrix * vec4(aPos, 1.0);
    vPrevPosition = prevProjectionViewModelMatrix * vec4(aPos, 1.0);
    vPosition = temp;
    gl_Position = temp;
}