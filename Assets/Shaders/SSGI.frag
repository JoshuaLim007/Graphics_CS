#version 420

layout(location = 0) out vec4 FragColor;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform vec3 CameraWorldSpacePos;
uniform vec4 CameraParams;
invariant uniform mat4 InvProjectionViewMatrix;
invariant uniform mat4 ProjectionViewMatrix;

float get_depth(vec2 pos)
{
    float d = texture(_CameraDepthTexture, pos).r * 2 - 1;
    return d;
}
float linear01Depth(float depthSample)
{
    depthSample = depthSample * 0.5 + 0.5;
    float zLinear = CameraParams.z / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
float linearEyeDepth(float depthSample)
{
    depthSample = depthSample * 0.5 + 0.5;
    float zLinear = CameraParams.z * CameraParams.w / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
    return zLinear;
}
vec3 calcPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 worldSpacePosition = InvProjectionViewMatrix * clipSpacePosition;
    worldSpacePosition.xyz = worldSpacePosition.xyz / worldSpacePosition.w;
    return worldSpacePosition.xyz;
}

float GetDepthAtPosition(vec3 worldPosition, out vec2 uv){
    vec4 p = vec4(worldPosition, 1);
    p = ProjectionViewMatrix * p;
    p /= p.w;
    p.xy = p.xy * 0.5 + 0.5;
    uv = p.xy;
    return p.z;
}

uniform int samples;
uniform int _Frame;

uvec3 pcg3d(uvec3 v) {

    v = v * 1664525u + 1013904223u;

    v.x += v.y*v.z;
    v.y += v.z*v.x;
    v.z += v.x*v.y;

    v ^= v >> 16u;

    v.x += v.y*v.z;
    v.y += v.z*v.x;
    v.z += v.x*v.y;

    return v;
}
float rand(vec2 co){ return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453); }

vec3 TraceRay(vec2 startUv, vec3 startWorldPosition, vec3 worldRayDirection, float maxRayLength, int samples, out int samplesTook, out float hit){
    vec3 retUv = vec3(startUv,0);
    float scale = maxRayLength / float(samples);
    worldRayDirection *= scale;
    vec3 currentPosition = startWorldPosition;
    int count = 0;
    hit = 0.0;

    //initial 3d ray march
    while(count < samples){
        count++;
        currentPosition += worldRayDirection;
        vec3 newUv;
        float currentDepth = GetDepthAtPosition(currentPosition, newUv.xy);
        float actualDepth = get_depth(newUv.xy);
        newUv.z = currentDepth;

        if(currentDepth < 0 || actualDepth == 1){
            break;
        }
        float depthDiff = linear01Depth(currentDepth) - linear01Depth(actualDepth);
        if(abs(depthDiff) >= 0.01 * scale){
            continue;
        }
        if(depthDiff > 0){
            hit = 1.0;
            retUv = newUv;
            break;
        }
    }

    vec3 sampleNormal = texture(_CameraNormalTexture, retUv.xy).xyz;
    float backFace = dot(sampleNormal, normalize(worldRayDirection));
    if(backFace > 0.5){
        hit = 0.0;
    }
    samplesTook = count;
    return retUv;
}

float CalculateMask(vec3 uv){
    if(uv.x < 0 || uv.x > 1 || uv.y > 1 || uv.y < 0 || uv.z < 0){
        return 0;
    }
    return 1;
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
    vec3 WorldPosition = calcPositionFromDepth(uv, d);
    vec3 normal = texture(_CameraNormalTexture, uv).xyz;
    vec3 viewDir = normalize(WorldPosition - CameraWorldSpacePos);
//    int totalSamples = 0;
//    float hit = 0;
//    float rdot;
//    vec3 newUv;
//    vec4 col;
//    int samples = 16;
//    while(samples > 0){
//        vec3 randomDir = normalize(pcg3d(uvec3(gl_FragCoord.x, gl_FragCoord.y, samples + _Frame * 1000)));
//        randomDir = randomDir * 2 - 1;
//        randomDir = randomDir * sign(dot(normalize(randomDir), normal));
//
//        vec3 reflection = normalize(reflect(viewDir, normalize(mix(randomDir, normal, 0.5))));
//        rdot = 1 - max(dot(reflection, viewDir), 0);
//        newUv = TraceRay(uv, WorldPosition, reflection, 32 / rdot, 128, totalSamples, hit);
//        float mask = CalculateMask(newUv);
//        vec4 temp = texture(MainTex, newUv.xy);
//        col += mix(vec4(0), temp, mask * hit);
//        samples--;
//    }
//    col /= 32;

    WorldPosition.x = mod(WorldPosition.x, 1);
    WorldPosition.y = mod(WorldPosition.y, 1);
    WorldPosition.z = mod(WorldPosition.z, 1);

    FragColor = vec4(WorldPosition, 0);
}