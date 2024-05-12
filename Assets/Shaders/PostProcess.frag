#version 410
#include "common.glsl"

out vec4 FragColor;

uniform sampler2D MainTex;
uniform sampler2D _CameraNormalTexture;
uniform vec3 FogColor;
uniform float FogDensity;
uniform vec2 MainTex_TexelSize;
uniform int Tonemapping;
uniform int GammaCorrection;
uniform float Gamma;
uniform float Exposure;
uniform int Srgb;
uniform int Vignette;
uniform float VignetteStrength;
uniform sampler2D DirectionalShadowDepthMap;
uniform mat4 InvProjectionViewMatrix;
uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;
uniform int AmbientOcclusion;
uniform vec3 CameraWorldSpacePos;
// By Morgan McGuire and Michael Mara at Williams College 2014
// Released as open source under the BSD 2-Clause License
// http://opensource.org/licenses/BSD-2-Clause
#define point2 vec2
#define point3 vec3

float distanceSquared(vec2 a, vec2 b) { a -= b; return dot(a, a); }

// Returns true if the ray hit something
bool traceScreenSpaceRay(
 // Camera-space ray origin, which must be within the view volume
 point3 csOrig, 

 // Unit length camera-space ray direction
 vec3 csDir,

 // A projection matrix that maps to pixel coordinates (not [-1, +1]
 // normalized device coordinates)
 // use standard projection matrix [-1, +1]
 mat4x4 proj, 

 // The camera-space Z buffer (all negative values)
 //raw camera depth converted into eye space depth (ranges from near to clip plane)
 sampler2D csZBuffer,

 // Dimensions of csZBuffer
 // use current blit resolution
 vec2 csZBufferSize,

 // Camera space thickness to ascribe to each pixel in the depth buffer
 float zThickness, 

 // (Negative number)
 // negative near clip plane
 float nearPlaneZ, 

 // Step in horizontal or vertical pixels between samples. This is a float
 // because integer math is slow on GPUs, but should be set to an integer >= 1
 float stride,

 // Number between 0 and 1 for how far to bump the ray in stride units
 // to conceal banding artifacts
 float jitter,

 // Maximum number of iterations. Higher gives better images but may be slow
 const float maxSteps, 

 // Maximum camera-space distance to trace before returning a miss
 float maxDistance, 

 // Pixel coordinates of the first intersection with the scene
 out point2 hitPixel, 

 // Camera space location of the ray hit
 out point3 hitPoint) {

    // Clip to the near plane    
    float rayLength = ((csOrig.z + csDir.z * maxDistance) > nearPlaneZ) ?
        (nearPlaneZ - csOrig.z) / csDir.z : maxDistance;
    point3 csEndPoint = csOrig + csDir * rayLength;

    // Project into homogeneous clip space
    vec4 H0 = proj * vec4(csOrig, 1.0);
    vec4 H1 = proj * vec4(csEndPoint, 1.0);
    float k0 = 1.0 / H0.w, k1 = 1.0 / H1.w;

    // The interpolated homogeneous version of the camera-space points  
    point3 Q0 = csOrig * k0, Q1 = csEndPoint * k1;

    // Screen-space endpoints
    point2 P0 = H0.xy * k0, P1 = H1.xy * k1;
    P0.xy = (P0.xy * 0.5 + 0.5) * csZBufferSize.xy;
    P1.xy = (P1.xy * 0.5 + 0.5) * csZBufferSize.xy;

    // If the line is degenerate, make it cover at least one pixel
    // to avoid handling zero-pixel extent as a special case later
    P1 += vec2((distanceSquared(P0, P1) < 0.0001) ? 0.01 : 0.0);
    vec2 delta = P1 - P0;

    // Permute so that the primary iteration is in x to collapse
    // all quadrant-specific DDA cases later
    bool permute = false;
    if (abs(delta.x) < abs(delta.y)) { 
        // This is a more-vertical line
        permute = true; delta = delta.yx; P0 = P0.yx; P1 = P1.yx; 
    }

    float stepDir = sign(delta.x);
    float invdx = stepDir / delta.x;

    // Track the derivatives of Q and k
    vec3  dQ = (Q1 - Q0) * invdx;
    float dk = (k1 - k0) * invdx;
    vec2  dP = vec2(stepDir, delta.y * invdx);

    // Scale derivatives by the desired pixel stride and then
    // offset the starting values by the jitter fraction
    dP *= stride; dQ *= stride; dk *= stride;
    P0 += dP * jitter; Q0 += dQ * jitter; k0 += dk * jitter;

    // Slide P from P0 to P1, (now-homogeneous) Q from Q0 to Q1, k from k0 to k1
    point3 Q = Q0; 

    // Adjust end condition for iteration direction
    float  end = P1.x * stepDir;

    float k = k0, stepCount = 0.0, prevZMaxEstimate = csOrig.z;
    float rayZMin = prevZMaxEstimate, rayZMax = prevZMaxEstimate;
    float sceneZMax = rayZMax + 100;
    for (point2 P = P0; 
         ((P.x * stepDir) <= end) && (stepCount < maxSteps) &&
         ((rayZMax < sceneZMax - zThickness) || (rayZMin > sceneZMax)) &&
          (sceneZMax != 0); 
         P += dP, Q.z += dQ.z, k += dk, ++stepCount) {
        
        rayZMin = prevZMaxEstimate;
        rayZMax = (dQ.z * 0.5 + Q.z) / (dk * 0.5 + k);
        prevZMaxEstimate = rayZMax;
        if (rayZMin > rayZMax) { 
           float t = rayZMin; rayZMin = rayZMax; rayZMax = t;
        }

        hitPixel = permute ? P.yx : P;

        // You may need hitPixel.y = csZBufferSize.y - hitPixel.y; here if your vertical axis
        // is different than ours in screen space
        float rawDepth = texture(csZBuffer, hitPixel.xy * MainTex_TexelSize).r;
//        float rawDepth = texelFetch(csZBuffer, ivec2(hitPixel.xy), 0).r;
        float eyeDepth = -linearEyeDepth(rawDepth);
        sceneZMax = eyeDepth;//texelFetch(csZBuffer, ivec2(hitPixel.xy), 0).r;
    }
    
    // Advance Q based on the number of steps
    Q.xy += dQ.xy * stepCount;
    hitPoint = Q * (1.0 / k);
    return (rayZMax >= sceneZMax - zThickness) && (rayZMin < sceneZMax);
}

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
    
    point2 outPixelPos;
    point3 outViewPos;
    bool hit = traceScreenSpaceRay(
        viewPos.xyz, 
        viewReflection, 
        ProjectionMatrix, 
        _CameraDepthTexture, 
        resolution,         
        -1.,                 //thickness
        -CameraParams.z,    //near clip
        1.,                 //stride
        0.,                 //jitter
        32,                //max steps
        1024,               //max distance
        outPixelPos, 
        outViewPos);
    vec2 hitUv = outPixelPos * MainTex_TexelSize;
    vec4 hitCol = texture(MainTex, hitUv);
    col.xyz = mix(col.xyz, hitCol.xyz, hit ? 1 : 0);




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