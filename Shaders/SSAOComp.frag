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

    const float top = 2.0f;
    const float threshold = 1.0f;
    float brightness = max(max(color.r, color.g), color.b);
    float amountHigher = max(brightness - threshold, 0);
    amountHigher /= top;
    amountHigher = min(amountHigher, 1.0f);
    amountHigher = 1 - amountHigher;

    AO = mix(1, AO, str * amountHigher);
    color *= AO;
    FragColor = color;
}