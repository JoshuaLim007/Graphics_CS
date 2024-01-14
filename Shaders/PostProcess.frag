#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform vec2 MainTex_Size;

void main()
{ 

    //tonemap
    vec4 col = texture(MainTex, gl_FragCoord.xy / MainTex_Size);
    col = col / (col + 1);

    //gamma correction
    float gamma = 2.2;
    col.rgb = pow(col.rgb, vec3(1.0/gamma));

    FragColor = col;
}