#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D _CameraDepthTexture;
uniform sampler2D _MotionTexture;
uniform vec2 MainTex_TexelSize;
uniform vec4 CameraParams;
uniform int samples;
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

void main()
{ 
    vec2 pos = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 col = texture(MainTex, pos);

    vec2 mv = texture(_MotionTexture, pos).rg;
    vec2 stride = (mv / samples) * strength * scale;
    for(int i = 1; i <= samples; i++){
        col += texture(MainTex, pos - stride * i);
    }
    col /= float(samples);

    FragColor = vec4(col.xyz, 1.0);
}