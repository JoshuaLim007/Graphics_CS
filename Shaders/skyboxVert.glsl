#version 410

layout(location = 0) in vec3 aPos;

out vec3 TexCoords;

uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;
uniform mat4 ModelMatrix;

void main()
{
    TexCoords = aPos;
    vec4 pos = ProjectionMatrix * ViewMatrix * ModelMatrix * vec4(aPos, 1.0);
    gl_Position = pos.xyww;
}