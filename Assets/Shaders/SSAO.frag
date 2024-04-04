#version 420

layout(location = 0) out vec4 FragColor;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform mat4 InvProjectionViewMatrix;
uniform vec4 CameraParams;
uniform mat4 ProjectionViewMatrix;
uniform float Radius;

float get_depth(vec2 pos)
{
    float d = texture(_CameraDepthTexture, pos).r * 2 - 1;
    return d;
}
float linearDepth(float depthSample)
{
    depthSample = depthSample * 0.5 + 0.5;
    float zLinear = CameraParams.z * CameraParams.w / (CameraParams.w + depthSample * (CameraParams.z - CameraParams.w));
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

    float v0 = abs(dot(pos1, pos0)) <= abs(dot(pos3, pos0)) ? 1 : 0;
    dy = mix(pos0 - pos3, pos1 - pos0, v0);
    float v1 = abs(dot(pos2, pos0)) <= abs(dot(pos4, pos0)) ? 1 : 0;
    dx = mix(pos0 - pos4, pos2 - pos0, v0);

    dy *= 0.5f;
    dx *= 0.5f;
    return normalize(cross(dx, dy));
}

uniform int samples;
uniform int _Frame;
uniform float DepthRange;
const uint k = 1103515245U;

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

uniform vec3 noiseSize;
uniform sampler2D noiseTexture;
vec3 GetNoise(float x, float y, float index){
    int xPos = int(x + mod(index, noiseSize.z) * noiseSize.x);
    int yPos = int(y);
    vec2 uv = vec2(xPos, yPos) / vec2(noiseSize.x * noiseSize.z, noiseSize.y);
    vec3 noiseTex = texture(noiseTexture, uv).xyz;
    return noiseTex;
}

void main()
{
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float depth = get_depth(uv);
    if(depth == 1){
        FragColor = vec4(1,1,1,1);
        return;
    }

    vec3 normal = texture(_CameraNormalTexture, uv).xyz;
    vec3 position = calcPositionFromDepth(uv, depth);

    float occlusion = 0;
    const float minBias = 0.01;
    const float maxBias = 1;

    //    vec3 noiseTex = GetNoise(
    //                        mod(gl_FragCoord.x, noiseSize.x), 
    //                        mod(gl_FragCoord.y, noiseSize.y), 
    //                        _Frame);
    //    
    //    FragColor = vec4(noiseTex, 0);
    //    return;

    for(int i = 1; i <= samples; i++){
        vec3 noiseTex = GetNoise(
                        mod(gl_FragCoord.x, noiseSize.x), 
                        mod(gl_FragCoord.y, noiseSize.y), 
                        i - 1);

//        vec3 randomDir = normalize(pcg3d(uvec3(gl_FragCoord.x, gl_FragCoord.y, i + _Frame * 1000)));
//        randomDir = randomDir * 2 - 1;
//        randomDir = randomDir * sign(dot(normalize(randomDir), normal));
//        float len = min(length(randomDir), 1);
//        randomDir = normalize(randomDir) * len;
//        float scale = float(i) / float(samples);
//        scale = mix(0.1f, Radius, scale * scale);
//        vec3 samplKernal = position + randomDir * scale;

        vec3 randomDir = noiseTex;
        randomDir = randomDir * sign(dot(normalize(randomDir), normal));
        float scale = (i - 1) / samples;
        scale = mix(0.1f, 1.0f, scale * scale);
        vec3 samplKernal = position + randomDir * Radius;

        vec4 cl = ProjectionViewMatrix * vec4(samplKernal, 1);
        cl.xyz /= cl.w;
        cl.xy = cl.xy * 0.5 + 0.5;
        float curDepth = cl.z;
        float sampledDepth = get_depth(cl.xy);
        if(sampledDepth == 1){
            continue;
        }
        
        sampledDepth = linearDepth(sampledDepth);
        curDepth = linearDepth(curDepth);

        float bias = mix(minBias, maxBias, sampledDepth / CameraParams.w);
        float dist = curDepth - (sampledDepth + bias);
        if(dist > 0 && abs(dist) < DepthRange){
            occlusion++;
        }
    }
    occlusion /= samples;
    FragColor = vec4(vec3(1 - occlusion), 1);
}