#version 410

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 norm;
layout(location = 2) out vec4 spec;

in vec3 TexCoords;

uniform samplerCube SkyBox;
uniform float skyBoxIntensity;

void main()
{
    norm = vec4(0, 0, 0, 0);
    spec = vec4(0, 0, 0, 0);
    FragColor = texture(SkyBox, TexCoords) * skyBoxIntensity;
}