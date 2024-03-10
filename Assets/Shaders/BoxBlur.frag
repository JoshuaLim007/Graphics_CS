#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;
uniform bool DoDepthCheck;
uniform float MaxDepthDiff;
uniform sampler2D _CameraDepthTexture;
uniform vec4 CameraParams;
float linearDepth(float depthSample)
{
    float zLinear = 2.0 * CameraParams.z * CameraParams.w / (CameraParams.w + CameraParams.z - depthSample * (CameraParams.w - CameraParams.z));
    return zLinear;
}
void main()
{ 
    int samples = 1;
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 color = texture(MainTex, uv);
    float cR = texture(_CameraDepthTexture, uv).r;
    float bias = MaxDepthDiff;
    for(int i = 0; i <8; i++){
        for(int j = 0; j < 8; j++){
            vec2 offset0 = uv + vec2(i - 3.5f, j - 3.5f) * MainTex_TexelSize;
            if(DoDepthCheck){
                float d = texture(_CameraDepthTexture, offset0).r;

                float d0 = linearDepth(d);
                float d1 = linearDepth(cR);

                float percentDiff = abs(d1 - d0) / ((d1 + d0) / 2);
                if(percentDiff > bias){
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