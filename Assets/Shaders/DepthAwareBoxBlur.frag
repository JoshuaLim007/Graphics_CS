#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;
uniform bool DoDepthCheck;
uniform float MaxDepthDiff;
uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform vec4 CameraParams;
float linear01Depth(float depthSample)
{
    depthSample = depthSample * 0.5 + 0.5;
    float zLinear = CameraParams.z / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
void main()
{ 
    int samples = 1;
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 color = texture(MainTex, uv);
    float cR = linear01Depth(texture(_CameraDepthTexture, uv).r * 2 - 1);
    vec3 curNormal = texture(_CameraNormalTexture, uv).xyz;
    float bias = MaxDepthDiff;
    for(int i = 0; i <8; i++){
        for(int j = 0; j < 8; j++){
            vec2 offset0 = uv + vec2(i - 3.5f, j - 3.5f) * MainTex_TexelSize;
            if(DoDepthCheck){
                float d = texture(_CameraDepthTexture, offset0).r * 2 - 1;
                vec3 newNormal = texture(_CameraNormalTexture, offset0).xyz;
                float oDotn = dot(curNormal, newNormal); 

                float d0 = linear01Depth(d);
                float d1 = cR;
                float percentDiff = abs(d1 - d0) / ((d1 + d0) / 2);
                if((oDotn <= 0.5 && percentDiff > bias) || percentDiff > bias * 10){
                    continue;
                }
            }
            samples++;
            color += texture(MainTex, offset0);
        }
    }
    color /= samples;

    FragColor = color;
}