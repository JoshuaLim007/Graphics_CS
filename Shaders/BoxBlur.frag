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
    for(int i = 0; i <4; i++){
        for(int j = 0; j < 4; j++){
            vec2 offset0 = uv + vec2(i - 1.5f, j - 1.5f) * MainTex_TexelSize;
            if(DoDepthCheck){
                float d = texture(_CameraDepthTexture, offset0).r;
                if(abs(linearDepth(cR) - linearDepth(d)) > bias){
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