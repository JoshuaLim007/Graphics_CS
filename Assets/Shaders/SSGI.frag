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

void main()
{
    vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    float d = get_depth(uv);
    if(d == 1){
        FragColor = vec4(0);
        return;
    }
    vec4 normmainCol = texture(MainTex, uv);
    if(d == 1){
        FragColor = normmainCol;
        return;
    }
    vec4 viewPos = calcViewPositionFromDepth(uv, d);
    vec3 worldNormal = texture(_CameraNormalTexture, uv).xyz;
    vec3 normal = (ViewMatrix * vec4(worldNormal, 0)).xyz;

    vec3 viewDir = normalize(viewPos.xyz);
    vec3 view_reflect = reflect(viewDir, normal);
    normmainCol = vec4(0);
    int SamplesPerPixel = max(SamplesPerPixel, 1);
    int sampleCount = 0;
    
    float rd = linearEyeDepth(d);
    float scaledH = 1024 / rd;   //scaled steps at rd meters
    scaledH *= 2;
    scaledH = min(256, scaledH);

    while(sampleCount < SamplesPerPixel){
        vec3 random = RandomUnitVector(uv, _Frame + sampleCount * 64);
        if(dot(random, worldNormal) < 0){
            sampleCount++;
            continue;
        }
        vec3 reflection = normalize((ViewMatrix * vec4(random, 0)).xyz);
        vec4 hitPoint = DDARayTrace(viewPos, reflection, int(scaledH), 0.004, 4.0);
        if(hitPoint.w == 1){
            normmainCol += texture(MainTex, hitPoint.xy);
        }
        sampleCount++;
    }
    normmainCol /= SamplesPerPixel;
    normmainCol = max(normmainCol, vec4(0));
    normmainCol = min(normmainCol, vec4(65536));
    FragColor = vec4(normmainCol);
}

