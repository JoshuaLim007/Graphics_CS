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
    const float scale = 0.25f;
    AO = max(AO - scale, 0) * (1.0f / (1 - scale));
    float str = Intensity;
    if(any(greaterThan(color, vec4(1)))){
        str = 0;
    }
    AO = mix(1, AO, str);
    color *= AO;
    FragColor = color;
}