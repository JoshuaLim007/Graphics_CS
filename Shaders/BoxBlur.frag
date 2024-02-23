#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

void main()
{ 
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 color = vec4(0);
    for(int i = 0; i <= 4; i++){
        for(int j = 0; j <= 4; j++){
            vec2 offset0 = uv + vec2(i - 2, j - 2) * MainTex_TexelSize;
            color += texture(MainTex, offset0);
        }
    }
    color /= 25;

    FragColor = color;
}