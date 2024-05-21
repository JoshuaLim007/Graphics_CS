#version 420

layout(location = 0) out vec4 FragColor;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform vec3 CameraWorldSpacePos;
uniform vec4 CameraParams;
uniform mat4 InvProjectionMatrix;
uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;

float get_depth(vec2 pos)
{
    float d = texture(_CameraDepthTexture, pos).r;
    return d;
}
float linear01Depth(float depthSample)
{
    float zLinear = CameraParams.z / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
float linearEyeDepth(float depthSample)
{
    float zLinear = CameraParams.z * CameraParams.w / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
vec3 calcViewPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 viewSpacePosition = InvProjectionMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition.xyz;
}

void GetDepthAtViewPosition(vec3 worldPosition, out vec3 uv){
    vec4 p = vec4(worldPosition, 1);
    p = ProjectionMatrix * p;
    p /= p.w;
    p.xy = p.xy * 0.5 + 0.5;
    uv = p.xyz;
}

uniform int samples;
uniform int SamplesPerPixel;             
uniform int _Frame;

uvec3 murmurHash33(uvec3 src) {
    const uint M = 0x5bd1e995u;
    uvec3 h = uvec3(1190494759u, 2147483647u, 3559788179u);
    src *= M; src ^= src>>24u; src *= M;
    h *= M; h ^= src.x; h *= M; h ^= src.y; h *= M; h ^= src.z;
    h ^= h>>13u; h *= M; h ^= h>>15u;
    return h;
}
vec3 hash33(vec3 src) {
    uvec3 h = murmurHash33(floatBitsToUint(src));
    return uintBitsToFloat(h & 0x007fffffu | 0x3f800000u) - 1.0;
}
vec3 RandomUnitVector(vec2 uv, int index){
    vec3 randomNormal = normalize(hash33(vec3(uv, index)) * 2 - 1);
    return randomNormal;
}
vec3 OrientToNormal(vec3 vector, vec3 normal){
    float s = sign(dot(vector, normal));
    return vector * s;
}

float CalculateMask(vec3 uv){
    if(uv.x < 0 || uv.x > 1 || uv.y > 1 || uv.y < 0 || uv.z < 0){
        return 0;
    }

    float xDist = min((1 - abs(uv.x * 2 - 1)) * 8, 1);
    float yDist = min((1 - abs(uv.y * 2 - 1)) * 8, 1);

    return xDist * yDist;
}

vec3 TraceRay(
    vec3 startViewPosition, 
    vec3 viewRayDirection, 
    float maxRayLength, 
    int maxSamples, 
    float maxThickness,
    out int samplesTook, 
    out float hit
){
    vec3 retUv = vec3(0);
    float scale = maxRayLength / float(maxSamples);
    viewRayDirection *= scale;
    vec3 currentPosition = startViewPosition;
    int count = 0;
    hit = 0.0;

    //initial 3d ray march
    while(count < maxSamples){
        count++;
        currentPosition += viewRayDirection;
        vec3 newUv;
        GetDepthAtViewPosition(currentPosition, newUv.xyz);
        float currentDepth = newUv.z;
        float actualDepth = get_depth(newUv.xy);

        if(currentDepth < 0 || actualDepth == 1){
            break;
        }
        float depthDiff = linear01Depth(currentDepth) - linear01Depth(actualDepth);
        if(abs(depthDiff) >= maxThickness){
            continue;
        }
        if(depthDiff > 0){
            hit = 1.0;
            retUv = newUv;
            break;
        }
    }
    hit *= CalculateMask(retUv);
    samplesTook = count;
    return retUv;
}

uniform bool FarRangeSSGI;










// By Morgan McGuire and Michael Mara at Williams College 2014
// Released as open source under the BSD 2-Clause License
// http://opensource.org/licenses/BSD-2-Clause
#define point2 vec2
#define point3 vec3

float distanceSquared(vec2 a, vec2 b) { a -= b; return dot(a, a); }

// Returns true if the ray hit something
bool traceScreenSpaceRay1(
 // Camera-space ray origin, which must be within the view volume
 point3 csOrig, 

 // Unit length camera-space ray direction
 vec3 csDir,

 // A projection matrix that maps to pixel coordinates (not [-1, +1]
 // normalized device coordinates)
 mat4x4 proj, 

 // The camera-space Z buffer (all negative values)
 sampler2D csZBuffer,

 // Dimensions of csZBuffer
 vec2 csZBufferSize,

 // Camera space thickness to ascribe to each pixel in the depth buffer
 float zThickness, 

 // (Negative number)
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
        sceneZMax = texelFetch(csZBuffer, ivec2(hitPixel.xy), 0).r;
    }
    
    // Advance Q based on the number of steps
    Q.xy += dQ.xy * stepCount;
    hitPoint = Q * (1.0 / k);
    return (rayZMax >= sceneZMax - zThickness) && (rayZMin < sceneZMax);
}




void main()
{
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float d = get_depth(uv);
    vec4 normmainCol = texture(MainTex, uv);
    if(d == 1){
        FragColor = normmainCol;
        return;
    }
    vec3 ViewPos = calcViewPositionFromDepth(uv, d);
    vec3 worldNormal = texture(_CameraNormalTexture, uv).xyz;
    vec3 normal = (ViewMatrix * vec4(worldNormal, 0)).xyz;
    vec3 viewDir = normalize(ViewPos);

    normmainCol = vec4(0);
    int SamplesPerPixel = max(SamplesPerPixel, 1);
    int sampleCount = 0;
    
    while(sampleCount < SamplesPerPixel){
        vec3 random = OrientToNormal(RandomUnitVector(uv, _Frame + sampleCount * 1024), normal);
        vec3 reflection = random;
        float rdot = 1 - max(dot(reflection, viewDir), 0);
        float scaler = pow(rdot, 0.5);
        int totalSamples = 0;
        float hit = 0;
        vec3 newUv = TraceRay(
            ViewPos,                                        //View position
            reflection,                                     //view reflection
            FarRangeSSGI ? 32 / scaler : 8 / scaler,        //max ray length
            64,                                             //max samples
            FarRangeSSGI ? .0001 / scaler : 0.000025 / scaler,  //thickness
            totalSamples,                                   //samples taken
            hit);                                           //intersection hit

//        vec3 hitNormal = texture(_CameraNormalTexture, newUv.xy).xyz;
//        float backFaceReflect = dot(worldNormal, hitNormal);
//        if(backFaceReflect > 0){
//            hit = 0;
//        }
        normmainCol += texture(MainTex, newUv.xy) * hit;
        sampleCount++;
    }
    normmainCol /= SamplesPerPixel;

    FragColor = vec4(normmainCol);
}

