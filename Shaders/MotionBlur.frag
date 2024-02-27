#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D _CameraDepthTexture;
uniform sampler2D _MotionTexture;
uniform vec2 MainTex_TexelSize;
uniform vec4 CameraParams;
uniform int samples;
uniform int _Frame;
uniform float strength;
uniform float scale;

float get_depth(vec2 pos)
{
    float d = texture(_CameraDepthTexture, pos).r * 2 - 1;
    return d;
}
float linearDepth(float depthSample)
{
    float zLinear = 2.0 * CameraParams.z * CameraParams.w / (CameraParams.w + CameraParams.z - depthSample * (CameraParams.w - CameraParams.z));
    return zLinear;
}
float rand(vec2 co){ return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453); }
void main()
{ 
    vec2 pos = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 col = max(texture(MainTex, pos), vec4(0));

    vec2 mv = texture(_MotionTexture, pos).rg;

    vec2 halfMv = mv * 0.5f;
    vec2 startPos = pos - halfMv;

    vec2 stride = mv / samples;
    pos = startPos;
    for(int i = 1; i <= samples; i++){
        float r = rand(pos + (i + _Frame) * 0.001) * 2 - 1;
        vec2 offset = stride * r;
        pos += stride;
        col += max(texture(MainTex, pos + offset), vec4(0));
    }
    col /= samples;
    FragColor = vec4(col.xyz, 1.0);
}