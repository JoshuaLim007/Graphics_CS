uniform sampler2D _CameraDepthTexture;
uniform vec4 CameraParams;

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
