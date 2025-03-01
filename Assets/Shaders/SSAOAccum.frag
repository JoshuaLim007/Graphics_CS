#version 410
#include "common.frag"

out vec4 FragColor;

uniform sampler2D PrevMainTex;
uniform sampler2D FrameCountBuffer;
uniform sampler2D SceneColor;
uniform int AccumCount;
uniform sampler2D _MotionTexture;
uniform vec4 ClearColor;
uniform bool ClearOnInvalidate;

void main()
{ 
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;

    float currentDepth = texture(_CameraDepthTexture, uv).r;
    float mixVal = texture(PrevMainTex, uv).a;
    mixVal = min(mixVal, 128);
    if(currentDepth == 1){
        if(ClearOnInvalidate){
            FragColor = ClearColor;
            FragColor.a = 0;
            return;
        }
        mixVal = 1;
    }
    vec2 velocity = texture(_MotionTexture, uv).rg;
    vec2 offset = uv - velocity;
    if(offset.x < 0 || offset.y < 0 || offset.x > 1 || offset.y > 1){
        if(ClearOnInvalidate){
            FragColor = ClearColor;
            FragColor.a = 0;
            return;
        }
        mixVal = 1;
    }
    float prevDepth = texture(_CameraDepthTexture, offset).r;
    if(abs(currentDepth - prevDepth) > 0.0001){
        if(ClearOnInvalidate){
            FragColor = ClearColor;
            FragColor.a = 0;
            return;
        }
        mixVal = 8;
    }
    vec3 prev = texture(PrevMainTex, offset).rgb;
    vec3 new = texture(MainTex, uv).xyz;

    FragColor = vec4(max(prev * (1.0 - 1.0 / mixVal) + new * (1.0 / mixVal), 0), mixVal + 1);
}

