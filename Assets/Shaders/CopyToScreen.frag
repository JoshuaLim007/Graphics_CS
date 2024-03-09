#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D DepthTex;
uniform vec2 MainTex_TexelSize;
uniform float RenderScale;

void main()
{ 
    FragColor = texture(MainTex, gl_FragCoord.xy * MainTex_TexelSize);
}