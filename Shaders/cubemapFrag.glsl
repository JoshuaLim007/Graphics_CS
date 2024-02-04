#version 410

layout(location = 0) out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube SkyBox;
uniform float skyBoxIntensity;

void main()
{
    FragColor = texture(SkyBox, TexCoords) * skyBoxIntensity;
}