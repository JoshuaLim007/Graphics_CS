#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D AOTex;
uniform vec2 MainTex_TexelSize;
uniform float Intensity;

void main()
{ 
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 color = texture(MainTex, uv);
    float AO = texture(AOTex, uv).r;
    const float scale = 0.5f;
    AO = max(AO - scale, 0) * (1.0f / (1 - scale));
    AO = mix(1, AO, Intensity);
    color *= AO;
    FragColor = color;
}