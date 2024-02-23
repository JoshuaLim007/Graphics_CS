#version 410

layout(location = 0) out vec4 FragColor;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D _CameraDepthTexture;
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
vec3 hash(uvec3 x)
{
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;
	x = ((x >> 8U) ^ x.yzx) * k;

	return vec3(x) * (1.0 / float(0xffffffffU));
}
vec3 hash3( uvec3 p ) 
{
    uint n = p.x + 2048 * p.y + (2048 * 2048) * uint(p.z);

    // integer hash copied from Hugo Elias
	n = (n << 13U) ^ n;
    n = n * (n * n * 15731U + 789221U) + 1376312589U;
    uvec3 k = n * uvec3(n,n*16807U,n*48271U);
    return vec3( k & uvec3(0x7fffffffU))/float(0x7fffffff);
}

float rand(vec2 co){ return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453); }

void main()
{
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float depth = get_depth(uv);
    if(depth == 1){
        FragColor = vec4(1,1,1,1);
        return;
    }

    vec3 normal = calcNormalFromPosition(uv);
    vec3 position = calcPositionFromDepth(uv, depth);

    float occlusion = 0;
    const float minBias = 0.01;
    const float maxBias = 1;

    for(int i = 0; i < samples; i++){
        vec3 randomDir = normalize(hash3(uvec3(gl_FragCoord.x, gl_FragCoord.y, i)));
        randomDir = randomDir * 2 - 1;
        randomDir = randomDir * sign(dot(randomDir, normal));
        
        float scale = float(i) / float(samples);
        scale = mix(0.1f, Radius, scale * scale);
        float len = rand(uv * vec2(i + 1) * 0.001) * scale;

        vec3 randomPos = position + randomDir * len;
        vec4 cl = ProjectionViewMatrix * vec4(randomPos, 1);
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

    FragColor = vec4(1 - occlusion);
}