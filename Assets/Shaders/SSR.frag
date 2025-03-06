#version 420
#include "common.frag"

layout(location = 0) out vec4 FragColor;
uniform int SamplesPerPixel;             
uniform int _Frame;
uniform int Steps;
uniform sampler2D _CameraSpecularTexture;

void main(){
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float d = get_depth(uv);
    vec4 s = texture(_CameraSpecularTexture, uv);

    vec4 viewPos = calcViewPositionFromDepth(uv, d);
    vec3 worldNormal = texture(_CameraNormalTexture, uv).xyz;
    vec3 normal = (ViewMatrix * vec4(worldNormal, 0)).xyz;
    vec3 viewDir = normalize(viewPos.xyz);
    vec3 view_reflect = reflect(viewDir, normal);
    int SamplesPerPixel = max(SamplesPerPixel, 1);
    int sampleCount = 0;
    float aoa = abs(dot(viewDir, -normal));
    vec4 normmainCol = vec4(0);
    while(sampleCount < SamplesPerPixel){
        vec3 random = RandomUnitVector(uv, _Frame + sampleCount * 64);
        vec3 reflection = normalize((ViewMatrix * vec4(random, 0)).xyz);
        reflection = mix(view_reflect, reflection, 0 * min(s.r * s.r, 0.5));
        reflection = normalize(reflection);
        vec4 hitPoint = DDARayTrace(viewPos, reflection, Steps, 0.0001 / pow(aoa, 2), 4.0);
        if(hitPoint.w == 1){
            normmainCol += textureLod(MainTex, hitPoint.xy, round(11 * s.r * 1.2));
        }
        sampleCount++;
    }
    normmainCol /= SamplesPerPixel;
    normmainCol = mix(vec4(0), normmainCol, min(pow((1 - s.r) * 1.33, 16), 1));
    normmainCol = max(normmainCol, vec4(0));
    normmainCol = min(normmainCol, vec4(65536));
    FragColor = normmainCol;
}