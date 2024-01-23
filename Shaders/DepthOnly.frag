#version 410
out float FragColor;

void main()
{ 
    FragColor = gl_FragCoord.z;
}