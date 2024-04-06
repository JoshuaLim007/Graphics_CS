#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D PrevMainTex;
uniform vec2 MainTex_TexelSize;
uniform int AccumCount;
uniform sampler2D _MotionTexture;
uniform sampler2D _CameraDepthTexture;
uniform vec4 CameraParams;
uniform vec4 ClearColor;
uniform bool ClearOnInvalidate;
float linear01Depth(float depthSample)
{
    depthSample = depthSample * 0.5 + 0.5;
    float zLinear = CameraParams.z / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
void main()
{ 
    float mixVal = AccumCount;
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;

    float currentDepth = texture(_CameraDepthTexture, uv).r * 2 - 1;
    if(currentDepth == 1){
        FragColor = ClearColor;
        return;
    }
    vec2 velocity = texture(_MotionTexture, uv).rg;
    vec2 offset = uv - velocity;
    float prevDepth = texture(_CameraDepthTexture, offset).r * 2 - 1;
    const float bias = 0.005;

    if(offset.x < 0 || offset.y < 0 || offset.x > 1 || offset.y > 1){
        if(ClearOnInvalidate){
            FragColor = ClearColor;
            return;
        }
        mixVal = 1;
    }

    currentDepth = linear01Depth(currentDepth);
    prevDepth = linear01Depth(prevDepth);
    if(abs(currentDepth - prevDepth) >= bias){
        if(ClearOnInvalidate){
            FragColor = ClearColor;
            return;
        }
        mixVal = 1;
    }

    vec3 prev = texture(PrevMainTex, offset).xyz;
    vec3 new = texture(MainTex, uv).xyz;
    FragColor = vec4(max(prev * (1.0 - 1.0 / mixVal) + new * (1.0 / mixVal), 0), 0);
}

