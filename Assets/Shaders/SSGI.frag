#version 420

layout(location = 0) out vec4 FragColor;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform vec4 CameraParams;
uniform mat4 InvProjectionViewMatrix;
uniform mat4 ProjectionViewMatrix;

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
    vec4 viewSpacePosition = InvProjectionViewMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition.xyz;
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

void main()
{
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float d = get_depth(uv);
    //d = mod(linear01Depth(d), 1.0);
    FragColor = vec4(linear01Depth(d));
}