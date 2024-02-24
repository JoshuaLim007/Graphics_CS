#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D AccumAO;
uniform vec2 MainTex_TexelSize;
uniform int AccumCount;
uniform sampler2D _MotionTexture;
uniform sampler2D _CameraDepthTexture;
uniform vec4 CameraParams;
float linearDepth(float depthSample)
{
    float zLinear = 2.0 * CameraParams.z * CameraParams.w / (CameraParams.w + CameraParams.z - depthSample * (CameraParams.w - CameraParams.z));
    return zLinear;
}
void main()
{ 
    float mixVal = AccumCount;
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;

    float currentDepth = texture(_CameraDepthTexture, uv).r;
    if(currentDepth == 1){
        FragColor = vec4(1);
        return;
    }
    vec2 velocity = texture(_MotionTexture, uv).rg;
    vec2 offset = uv - velocity;
    float prevDepth = texture(_CameraDepthTexture, offset).r;
    const float bias = 0.25;

    if(offset.x < 0 || offset.y < 0 || offset.x > 1 || offset.y > 1){
        mixVal = 1;
    }

    currentDepth = linearDepth(currentDepth);
    prevDepth = linearDepth(prevDepth);
    float percentDifferent = abs(currentDepth - prevDepth) / ((currentDepth + prevDepth) * 0.5);
    if(percentDifferent > bias){
        mixVal = 1;
    }

    float prev = texture(AccumAO, offset).r;
    float new = texture(MainTex, uv).r;
    FragColor = vec4(prev * (1.0 - 1.0 / mixVal) + new * (1.0 / mixVal));
}