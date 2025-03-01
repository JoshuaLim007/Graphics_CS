uniform sampler2D _CameraDepthTexture;
uniform sampler2D _CameraNormalTexture;
uniform sampler2D MainTex;
uniform vec2 MainTex_TexelSize;

uniform vec4 CameraParams;
uniform mat4 InvProjectionViewMatrix;
uniform mat4 InvProjectionMatrix;
uniform mat4 InvViewMatrix;
uniform mat4 ViewMatrix;
uniform mat4 ProjectionMatrix;

vec4 calcViewPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 viewSpacePosition = InvProjectionMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition;
}

float lum(vec3 color) {
	return (0.299 * color.x + 0.587 * color.y + 0.114 * color.z);
}
//returns raw depth value at uv
float get_depth(vec2 pos)
{
    float d = texture(_CameraDepthTexture, pos).r;
    return d;
}
//converts depth to linear depth ranging from near clip to far clip
float linearEyeDepth(float depthSample)
{
    float div = CameraParams.w / CameraParams.z;
    float x = 1 - div;
    float y = div;
    float z = x / CameraParams.w;
    float w = y / CameraParams.w;

    float zLinear = 1.0 / (z * depthSample + w);

    return zLinear;
}
//converts depth to linear depth ranging form 0 to 1
float linear01Depth(float depth){
    float led = linearEyeDepth(depth);
    
    float min1 = CameraParams.z;
    float max1 = CameraParams.w;

    float min2 = 0;
    float max2 = 1;

    float ld = min2 + (led - min1) * (max2 - min2) / (max1 - min1);

    return ld;
}

//returns world position at texCoord with Depth
vec3 calcPositionFromDepth(vec2 texCoords, float depth) {
    vec4 clipSpacePosition = vec4(texCoords * 2.0 - 1.0, depth, 1.0);
    vec4 viewSpacePosition = InvProjectionViewMatrix * clipSpacePosition;
    viewSpacePosition.xyz = viewSpacePosition.xyz / viewSpacePosition.w;
    return viewSpacePosition.xyz;
}

//calculates world normals
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

    if (abs(dot(pos1, pos0)) < abs(dot(pos3, pos0))) {
        dy = pos1 - pos0;
    }
    else {
        dy = pos0 - pos3;
    }
    if (abs(dot(pos2, pos0)) < abs(dot(pos4, pos0))) {
        dx = pos2 - pos0;
    }
    else {
        dx = pos0 - pos4;
    }
    dy *= 0.5f;
    dx *= 0.5f;
    return normalize(cross(dx, dy));
}

float interpolateDepth(float zStart, float zEnd, float t) {
    float invZStart = 1.0 / zStart;
    float invZEnd = 1.0 / zEnd;
    float invZInterp = mix(invZStart, invZEnd, t);
    return 1.0 / invZInterp;
}

vec4 DDARayTrace(vec4 viewPos, vec3 view_rayDir, int steps, float maxThickness, float scale){
    viewPos.w = 1;
    vec4 startFrag = viewPos;
    startFrag = ProjectionMatrix * startFrag;
    startFrag.xyz /= startFrag.w;
    startFrag.xy = startFrag.xy * 0.5 + 0.5;

    vec4 endFrag = viewPos + vec4(view_rayDir, 0);
    endFrag.z = min(endFrag.z, CameraParams.z);
    endFrag = ProjectionMatrix * endFrag;
    endFrag.xyz /= endFrag.w;
    if(endFrag.z < 0){
        return vec4(0);
    }
    endFrag.xy = endFrag.xy * 0.5 + 0.5;

    vec2 px = (startFrag.xy / MainTex_TexelSize);
    vec2 endpx = (endFrag.xy / MainTex_TexelSize);

    int hit = 0;
    int i = 0;

    float pxDist = length(endpx - px);
    float pxStep = scale / max(pxDist, 1);
    float pxStepUnscaled = 1 / max(pxDist, 1);
    float t = 0.;
    vec2 curPx = px;
    vec2 uv = px * MainTex_TexelSize;
    while(i < steps){
        t += pxStep;
        curPx = mix(px, endpx, t);
        uv = curPx * MainTex_TexelSize;
        if(uv.x > 1. || uv.y > 1. || uv.x < 0. || uv.y < 0.){
            break;
        }

        float curD = interpolateDepth(startFrag.z, endFrag.z, t);
        float sampled_depth = get_depth(uv.xy);
        if(sampled_depth == 1 || curD < 0){
            break;
        }
        float td = curD - sampled_depth;
        if(td > 0.0 && td < maxThickness){
            hit = 1;
            break;
        }
        i++;
    }

    if(hit == 1 && pxStepUnscaled != pxStep){
        //linear refinement
        int i2 = 0;
        while(i2 < ceil(scale)){
            i2++;
            t -= pxStepUnscaled;
            curPx = mix(px, endpx, t);
            vec2 tuv = curPx * MainTex_TexelSize;
            float curD = interpolateDepth(startFrag.z, endFrag.z, t);
            float sampled_depth = get_depth(tuv.xy);
            float td = curD - sampled_depth;
            if(td <= 0.0 || td >= maxThickness){
                break;
            }
            uv = tuv;
        }
    }

    float visibility = hit 
        * (uv.x < 0 || uv.x > 1 ? 0 : 1)
        * (uv.y < 0 || uv.y > 1 ? 0 : 1)
        * ((i > 0) ? 1 : 0);
    return vec4(uv, i, visibility);
}

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

//Traces ray in screen space with given view space position and reflection vector
vec4 traceScreenSpaceRay(
    vec4 viewPos, vec3 viewNormal, vec3 viewReflection, float maxDistance,
    float resolution, int maxTraceSteps, int maxBinarySteps, float thickness) {
    float reflection_steepness = max(1 - dot(viewNormal, viewReflection), 0);

    thickness /= pow(1 - reflection_steepness, 2);
    maxDistance /= pow(1 - reflection_steepness, 1);

    vec4 startView = vec4(viewPos.xyz + (viewReflection * 0), 1);
    vec4 endView = vec4(viewPos.xyz + (viewReflection * maxDistance), 1);

    vec2 texSize = 1.0 / MainTex_TexelSize;
    vec2 inv_texSize = MainTex_TexelSize;

    vec4 startFrag = startView;
    startFrag = ProjectionMatrix * startFrag;
    startFrag.xyz /= startFrag.w;
    startFrag.xy = startFrag.xy * 0.5 + 0.5;
    startFrag.xy *= texSize;
    startFrag.xy += (float((int(startFrag.x) + int(startFrag.y)) & 1) * 2 - 1) * 0.5;

    endView.z = -max(-endView.z, CameraParams.z);

    vec4 endFrag = endView;
    endFrag = ProjectionMatrix * endFrag;
    endFrag.xyz /= endFrag.w;
    endFrag.xy = endFrag.xy * 0.5 + 0.5;
    endFrag.xy *= texSize;

    //flip z
    startView.z *= -1;
    endView.z *= -1;

    vec3 unitPositionFrom = normalize(startView.xyz);

    vec2 frag = startFrag.xy;
    vec2 uv = frag * inv_texSize;

    float deltaX = endFrag.x - startFrag.x;
    float deltaY = endFrag.y - startFrag.y;

    float useX = abs(deltaX) >= abs(deltaY) ? 1 : 0;
    float delta = mix(abs(deltaY), abs(deltaX), useX) * clamp(resolution, 0, 1);
    vec2 increment = vec2(deltaX, deltaY) / max(delta, 0.001);

    float search0 = 0;
    float search1 = 0;
    int hit0 = 0;
    int hit1 = 0;

    float viewDistance = startView.z;
    float depth = thickness;
    vec4 positionTo = viewPos;

    int i = 0;
    
    for (i = 0; i < int(delta); ++i) {
        if(i > maxTraceSteps){
            break;        
        }

        frag += increment;
        uv.xy = frag * inv_texSize;

        vec4 worldPosition = vec4(calcPositionFromDepth(uv.xy, get_depth(uv.xy)), 1);
        vec4 viewPosition = ViewMatrix * worldPosition;

        //flip z
        viewPosition.z *= -1;
        positionTo = viewPosition;

        search1 = mix((frag.y - startFrag.y) / deltaY, (frag.x - startFrag.x) / deltaX, useX);
        search1 = clamp(search1, 0.0, 1.0);

        viewDistance = (startView.z * endView.z) / mix(endView.z, startView.z, search1);
        depth = viewDistance - positionTo.z;

        if (depth > 0 && depth < thickness) {
            hit0 = 1;
            break;
        }
        else {
            search0 = search1;
        }
    }

    search1 = search0 + ((search1 - search0) / 2.0);
    maxBinarySteps *= hit0;
    
    for (i = 0; i < maxBinarySteps; ++i) {
        frag = mix(startFrag.xy, endFrag.xy, search1);
        uv.xy = frag / texSize;
    
        vec4 worldPosition = vec4(calcPositionFromDepth(uv.xy, get_depth(uv.xy)), 1);
        vec4 viewPosition = ViewMatrix * worldPosition;

        //flip z
        viewPosition.z *= -1;
        positionTo = viewPosition;
    
        viewDistance = (startView.z * endView.z) / mix(endView.z, startView.z, search1);
        depth = viewDistance - positionTo.z;
    
        if (depth > 0 && depth < thickness) {
            hit1 = 1;
            search1 = search0 + ((search1 - search0) / 2);
        }
        else {
            float temp = search1;
            search1 = search1 + ((search1 - search0) / 2);
            search0 = temp;
        }
    }

    float visibility = hit1 
            * (uv.x < 0 || uv.x > 1 ? 0 : 1)
            * (uv.y < 0 || uv.y > 1 ? 0 : 1);

    visibility = clamp(visibility, 0, 1);
    return vec4(uv, 0, visibility);
}
