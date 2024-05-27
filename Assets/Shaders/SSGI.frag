#version 420
#include "common.frag"

layout(location = 0) out vec4 FragColor;

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

vec4 calcViewPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 viewSpacePosition = InvProjectionMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition;
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
    vec4 viewPos = calcViewPositionFromDepth(uv, d);
    vec3 worldNormal = texture(_CameraNormalTexture, uv).xyz;
    vec3 normal = (ViewMatrix * vec4(worldNormal, 0)).xyz;

    normmainCol = vec4(0);
    int SamplesPerPixel = max(SamplesPerPixel, 1);
    int sampleCount = 0;
    
    while(sampleCount < SamplesPerPixel){
        vec3 random = RandomUnitVector(uv, _Frame + sampleCount * 1024);
        if(dot(random, worldNormal) < 0){
            sampleCount++;
            continue;
        }

        vec3 reflection = normalize((ViewMatrix * vec4(random, 0)).xyz);
        vec4 hitPoint = traceScreenSpaceRay(viewPos, normal.xyz, reflection, 64.0f, 0.1, 128, 16, 2.5);

        normmainCol += texture(MainTex, hitPoint.xy) * hitPoint.w;
        sampleCount++;
    }
    
    normmainCol /= SamplesPerPixel;
    FragColor = vec4(normmainCol);
}

