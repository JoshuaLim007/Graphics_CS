﻿#version 410
#include "common.frag"

layout(location = 0) out vec3 OutColor;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Copyright (c) 2018-2019 Michele Morrone
//  All rights reserved.
//
//  https://michelemorrone.eu - https://BrutPitt.com
//
//  me@michelemorrone.eu - brutpitt@gmail.com
//  twitter: @BrutPitt - github: BrutPitt
//  
//  https://github.com/BrutPitt/glslSmartDeNoise/
//
//  This software is distributed under the terms of the BSD 2-Clause license
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

#define INV_SQRT_OF_2PI 0.39894228040143267793994605993439  // 1.0/SQRT_OF_2PI
#define INV_PI 0.31830988618379067153776752674503

//  smartDeNoise - parameters
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//
//  sampler2D tex     - sampler image / texture
//  vec2 texelSize    - 1 / texture size
//  vec2 uv           - actual fragment coord
//  float sigma  >  0 - sigma Standard Deviation
//  float kSigma >= 0 - sigma coefficient 
//      kSigma * sigma  -->  radius of the circular kernel
//  float threshold   - edge sharpening threshold 

vec4 smartDeNoise(sampler2D tex, vec2 texelSize, vec2 uv, float sigma, float kSigma, float threshold)
{
    float radius = round(kSigma*sigma);
    float radQ = radius * radius;
    
    float invSigmaQx2 = .5 / (sigma * sigma);      // 1.0 / (sigma^2 * 2.0)
    float invSigmaQx2PI = INV_PI * invSigmaQx2;    // 1.0 / (sqrt(PI) * sigma)
    
//    float invThresholdSqx2 = .5 / (threshold * threshold);     // 1.0 / (sigma^2 * 2.0)
//    float invThresholdSqrt2PI = INV_SQRT_OF_2PI / threshold;   // 1.0 / (sqrt(2*PI) * sigma)
    
    vec4 centrPx = texture(tex,uv).rgba;
    float centrPx_depth = linearEyeDepth(get_depth(uv));

    float zBuff = 0.0;
    vec4 aBuff = vec4(0.0);
    
    for(float x=-radius; x <= radius; x++) {
        float pt = sqrt(radQ-x*x);  // pt = yRadius: have circular trend
        for(float y=-pt; y <= pt; y++) {
            vec2 d = vec2(x,y);

            float blurFactor = exp( -dot(d , d) * invSigmaQx2 ) * invSigmaQx2PI; 
            vec4 walkPx =  texture(tex,uv+d * texelSize).rgba;
            float walkPx_depth = linearEyeDepth(get_depth(uv+d * texelSize));

            float dC = abs(walkPx_depth - centrPx_depth);

            float percentDiff = abs(walkPx_depth - centrPx_depth) / ((centrPx_depth + walkPx_depth) / 2);
            if(percentDiff > threshold){
                continue;
            }

//            float deltaFactor = exp( -dot(dC, dC) * invThresholdSqx2) * invThresholdSqrt2PI * blurFactor;
            float deltaFactor = blurFactor;
                                 
            zBuff += deltaFactor;
            aBuff += deltaFactor*walkPx;
        }
    }
    if(zBuff == 0){
        return centrPx;
    }
    return aBuff/zBuff;
}

uniform float color_intensity;
void main()
{
	vec2 uv = gl_FragCoord.xy * MainTex_TexelSize;
    vec4 color;
    float r = get_depth(uv);
    if(r == 1){
        OutColor = vec3(0);
        return;
    }
    color = smartDeNoise(MainTex, MainTex_TexelSize, uv, 6.0, 2.0, 0.01);
	OutColor = color.xyz * color_intensity;
}

