#version 410
out vec4 FragColor;
  
uniform sampler2D screenTexture;
uniform vec2 MainTex_Size;

void main()
{ 
    FragColor = texture(screenTexture, gl_FragCoord.xy / MainTex_Size);
    //FragColor = vec4(gl_FragCoord.xy / MainTex_Size, 0, 0);
}