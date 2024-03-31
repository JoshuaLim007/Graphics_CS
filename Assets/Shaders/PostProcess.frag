#version 410
out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D _CameraDepthTexture;
uniform vec3 FogColor;
uniform float FogDensity;
uniform vec2 MainTex_TexelSize;
uniform vec4 CameraParams;
uniform int Tonemapping;
uniform int GammaCorrection;
uniform float Gamma;
uniform int Srgb;
uniform int Vignette;
uniform float VignetteStrength;
uniform sampler2D DirectionalShadowDepthMap;
uniform mat4 InvProjectionViewMatrix;
uniform int AmbientOcclusion;

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
vec3 calcPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 viewSpacePosition = InvProjectionViewMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition.xyz;
}

vec3 calcNormalFromPosition(vec2 texCoords) {
    vec2 offset1 = texCoords + vec2(0, 1) * MainTex_TexelSize;
    vec2 offset2 = texCoords + vec2(1, 0) * MainTex_TexelSize;
    vec2 offset3 = texCoords + vec2(0, -1) * MainTex_TexelSize;
    vec2 offset4 = texCoords + vec2(-1, 0) * MainTex_TexelSize;

    vec3 pos0 = calcPositionFromDepth(texCoords, get_depth(texCoords));

    //up
    vec3 pos1 = calcPositionFromDepth(offset1, get_depth(offset1));
    //right
    vec3 pos2 = calcPositionFromDepth(offset2, get_depth(offset2));
    //down
    vec3 pos3 = calcPositionFromDepth(offset3, get_depth(offset3));
    //left
    vec3 pos4 = calcPositionFromDepth(offset4, get_depth(offset4));

    vec3 dx;
    vec3 dy;

    if(abs(dot(pos1, pos0)) < abs(dot(pos3, pos0))){
        dy = pos1 - pos0;
    }
    else{
        dy = pos0 - pos3;
    }
    if(abs(dot(pos2, pos0)) < abs(dot(pos4, pos0))){
        dx = pos2 - pos0;
    }
    else{
        dx = pos0 - pos4;
    }
    dy *= 0.5f;
    dx *= 0.5f;
    return normalize(cross(dx, dy));
}

vec3 calcNormalFromPosition_fast(vec2 texCoords) {
    vec3 pos0 = calcPositionFromDepth(texCoords, get_depth(texCoords));
    vec3 dx = dFdx(pos0);
    vec3 dy = dFdy(pos0);
    return normalize(cross(dx, dy));
}
vec3 linear_srgb(vec3 x) {
    return mix(1.055*pow(x, vec3(1./Gamma)) - 0.055, 12.92*x, step(x,vec3(0.0031308)));
}
void main()
{ 
    vec2 pos = gl_FragCoord.xy * MainTex_TexelSize;
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
    float depth = get_depth(pos);
    vec3 position = calcPositionFromDepth(pos, depth);
    vec3 normal = calcNormalFromPosition(pos);

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

    FragColor = vec4(col.xyz, 1.0);
//    FragColor = vec4(normal, 0);
    //FragColor = vec4(pos, 0, 0);
//    if(gl_FragCoord.x >= 512 || gl_FragCoord.y >= 512){
//        FragColor = vec4(col.xyz, 1.0);
//    }
//    else{
//        FragColor = vec4(sr,sr,sr, 1.0);
//    }
}