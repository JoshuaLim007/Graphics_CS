#version 410
#include "common.frag"

out vec4 FragColor;


uniform vec3 FogColor;
uniform float FogDensity;
uniform int Tonemapping;
uniform int GammaCorrection;
uniform float Gamma;
uniform float Exposure;
uniform int Srgb;
uniform int Vignette;
uniform float VignetteStrength;
uniform sampler2D DirectionalShadowDepthMap;

uniform int AmbientOcclusion;
uniform vec3 CameraWorldSpacePos;

vec3 aces_tonemap(vec3 color){	
	mat3 m1 = mat3(
        0.59719, 0.07600, 0.02840,
        0.35458, 0.90834, 0.13383,
        0.04823, 0.01566, 0.83777
	);
	mat3 m2 = mat3(
        1.60475, -0.10208, -0.00327,
        -0.53108,  1.10813, -0.07276,
        -0.07367, -0.00605,  1.07602
	);
	vec3 v = m1 * color;    
	vec3 a = v * (v + 0.0245786) - 0.000090537;
	vec3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
	return m2 * (a / b);
}

vec3 linear_srgb(vec3 x) {
    return mix(1.055*pow(x, vec3(1./Gamma)) - 0.055, 12.92*x, step(x,vec3(0.0031308)));
}

void main()
{ 
    vec2 pos = gl_FragCoord.xy * MainTex_TexelSize;
    vec2 resolution = vec2(1. / MainTex_TexelSize.x, 1. / MainTex_TexelSize.y);
    vec2 normCoord = pos * 2.0 - 1.0;
    
    // Apply Panini projection
//    float PaniniStrength = 1.0f;
//    float d = length(normCoord);
//    float PaniniFactor = tan(PaniniStrength * d) / tan(PaniniStrength);
//    vec2 distortedCoord = normalize(normCoord) * PaniniFactor;
//    pos = (distortedCoord + 1.0) / 2.0;

    if(any(greaterThan(pos, vec2(1,1))) || any(lessThan(pos, vec2(0,0)))){
        FragColor = vec4(0);
        return;
    }

    vec4 col = texture(MainTex, pos);
//    float depth = get_depth(pos);
//    vec3 position = calcPositionFromDepth(pos, depth);
//    vec3 normal = calcNormalFromPosition(pos);
    

    vec4 worldPos = vec4(calcPositionFromDepth(pos, get_depth(pos)).xyz, 1);
    vec3 worldDir = (worldPos.xyz - CameraWorldSpacePos);

    vec4 viewPos = ViewMatrix * worldPos;
    vec3 viewDir = (ViewMatrix * vec4(worldDir, 0)).xyz;

    vec4 viewNormal = ViewMatrix * vec4(texture(_CameraNormalTexture, pos).xyz, 0);
    vec3 viewReflection = normalize(reflect(viewDir, viewNormal.xyz));

//    if(pos.x > 0.0){
//        vec4 hitPoint = traceScreenSpaceRay(viewPos, viewNormal.xyz, viewReflection, 64.0f, 0.1, 128, 16, 2.5);
//        col.rgb = vec3(hitPoint.xyz) * hitPoint.w;
//        FragColor = vec4(col.xyz, 1.0);
//        return;
//    }

    col.rgb *= Exposure;

    //aces tonemapping
    if(Tonemapping == 1){
        col.xyz = aces_tonemap(col.xyz);
    }

    //Linear to Gamma
    if(GammaCorrection == 1){
        col.rgb = pow(col.rgb, vec3(1.0/Gamma));
    }

    //Linear to Srgb 
    if(Srgb == 1){
        col.xyz = linear_srgb(col.xyz);
    }

    if(Vignette == 1){
        float dist = length(pos * 2 - 1) * 0.7071;
        dist = min(max(dist, 0), 1);
        dist = smoothstep(0.5f, 1.25f, dist);
        dist *= VignetteStrength;
        col.xyz *= vec3(1 - dist);
    }

//    vec2 pos2 = gl_FragCoord.xy / (vec2(2048, 2048)) * 5;
//    if(pos2.x < 1 && pos2.y < 1){
//        vec4 shadow = texture(DirectionalShadowDepthMap, pos2);
//        FragColor = vec4( smoothstep(0.25f, 0.75f, pow(shadow.r, 1.0f)) );
//        return;
//    }

//    float d = get_depth(pos);
//    float led = linearEyeDepth(d);
//    float ld = linear01Depth(d);

//    ld = ld / (ld + 1);
//    FragColor = vec4(ld);


    FragColor = vec4(col.xyz, 1.0);

    //FragColor = vec4(viewReflection.xyz, 1.0);
//    FragColor = vec4(normal, 0);
    //FragColor = vec4(pos, 0, 0);
//    if(gl_FragCoord.x >= 512 || gl_FragCoord.y >= 512){
//        FragColor = vec4(col.xyz, 1.0);
//    }
//    else{
//        FragColor = vec4(sr,sr,sr, 1.0);
//    }
}