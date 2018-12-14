// SH lighting environment
float4 unity_SHAr_RT;
float4 unity_SHAg_RT;
float4 unity_SHAb_RT;
float4 unity_SHBr_RT;
float4 unity_SHBg_RT;
float4 unity_SHBb_RT;
float4 unity_SHC_RT;

TEXTURE2D(unity_Lightmap_RT);
SAMPLER(samplerunity_Lightmap_RT);

float4 unity_LightmapST_RT;
TEXTURE2D(unity_LightmapInd_RT);

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/NormalSurfaceGradient.hlsl"

// Ref: "Efficient Evaluation of Irradiance Environment Maps" from ShaderX 2
real3 SHEvalLinearL0L1(real3 N, real4 shAr, real4 shAg, real4 shAb)
{
    real4 vA = real4(N, 1.0);

    real3 x1;
    // Linear (L1) + constant (L0) polynomial terms
    x1.r = dot(shAr, vA);
    x1.g = dot(shAg, vA);
    x1.b = dot(shAb, vA);

    return x1;
}

real3 SHEvalLinearL2(real3 N, real4 shBr, real4 shBg, real4 shBb, real4 shC)
{
    real3 x2;
    // 4 of the quadratic (L2) polynomials
    real4 vB = N.xyzz * N.yzzx;
    x2.r = dot(shBr, vB);
    x2.g = dot(shBg, vB);
    x2.b = dot(shBb, vB);

    // Final (5th) quadratic (L2) polynomial
    real vC = N.x * N.x - N.y * N.y;
    real3 x3 = shC.rgb * vC;

    return x2 + x3;
}

float3 SampleSH9(float4 SHCoefficients[7], float3 N)
{
    float4 shAr = SHCoefficients[0];
    float4 shAg = SHCoefficients[1];
    float4 shAb = SHCoefficients[2];
    float4 shBr = SHCoefficients[3];
    float4 shBg = SHCoefficients[4];
    float4 shBb = SHCoefficients[5];
    float4 shCr = SHCoefficients[6];

    // Linear + constant polynomial terms
    float3 res = SHEvalLinearL0L1(N, shAr, shAg, shAb);

    // Quadratic polynomials
    res += SHEvalLinearL2(N, shBr, shBg, shBb, shCr);

    return res;
}

real3 UnpackLightmapRGBM(real4 rgbmInput, real4 decodeInstructions)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return rgbmInput.rgb * (rgbmInput.a * decodeInstructions.x);
#else
    return rgbmInput.rgb * (PositivePow(rgbmInput.a, decodeInstructions.y) * decodeInstructions.x);
#endif
}

real3 UnpackLightmapDoubleLDR(real4 encodedColor, real4 decodeInstructions)
{
    return encodedColor.rgb * decodeInstructions.x;
}

real3 DecodeLightmap(real4 encodedIlluminance, real4 decodeInstructions)
{
#if defined(UNITY_LIGHTMAP_RGBM_ENCODING)
    return UnpackLightmapRGBM(encodedIlluminance, decodeInstructions);
#elif defined(UNITY_LIGHTMAP_DLDR_ENCODING)
    return UnpackLightmapDoubleLDR(encodedIlluminance, decodeInstructions);
#else // (UNITY_LIGHTMAP_FULL_HDR)
    return encodedIlluminance.rgb;
#endif
}

real3 DecodeHDREnvironment(real4 encodedIrradiance, real4 decodeInstructions)
{
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    real alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);

    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * encodedIrradiance.rgb;
}

real3 SampleSingleLightmap(TEXTURE2D_ARGS(lightmapTex, lightmapSampler), float2 uv, float4 transform, bool encodedLightmap, real4 decodeInstructions)
{
    // transform is scale and bias
    uv = uv * transform.xy + transform.zw;
    real3 illuminance = real3(0.0, 0.0, 0.0);
    // Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    if (encodedLightmap)
    {
        real4 encodedIlluminance = SAMPLE_TEXTURE2D_LOD(lightmapTex, lightmapSampler, uv, 0).rgba;
        illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
    }
    else
    {
        illuminance = SAMPLE_TEXTURE2D_LOD(lightmapTex, lightmapSampler, uv, 0).rgb;
    }
    return illuminance;
}

real3 SampleDirectionalLightmap(TEXTURE2D_ARGS(lightmapTex, lightmapSampler), TEXTURE2D_ARGS(lightmapDirTex, lightmapDirSampler), float2 uv, float4 transform, float3 normalWS, bool encodedLightmap, real4 decodeInstructions)
{
    // In directional mode Enlighten bakes dominant light direction
    // in a way, that using it for half Lambert and then dividing by a "rebalancing coefficient"
    // gives a result close to plain diffuse response lightmaps, but normalmapped.

    // Note that dir is not unit length on purpose. Its length is "directionality", like
    // for the directional specular lightmaps.

    // transform is scale and bias
    uv = uv * transform.xy + transform.zw;

    real4 direction = SAMPLE_TEXTURE2D_LOD(lightmapDirTex, lightmapDirSampler, uv, 0);
    // Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    real3 illuminance = real3(0.0, 0.0, 0.0);
    if (encodedLightmap)
    {
        real4 encodedIlluminance = SAMPLE_TEXTURE2D_LOD(lightmapTex, lightmapSampler, uv, 0).rgba;
        illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
    }
    else
    {
        illuminance = SAMPLE_TEXTURE2D_LOD(lightmapTex, lightmapSampler, uv, 0).rgb;
    }
    real halfLambert = dot(normalWS, direction.xyz - 0.5) + 0.5;
    return illuminance * halfLambert / max(1e-4, direction.w);
}

float3 SampleBakedGI(float3 positionRWS, float3 normalWS, float2 uvStaticLightmap)
{
    // If there is no lightmap, it assume lightprobe
    #if defined(LIGHTMAP_ON)
        bool useRGBMLightmap = true;
        float4 decodeInstructions = float4(34.493242, 2.2, 0.0, 0.0); // Never used but needed for the interface since it supports gamma lightmaps

        #if defined(DIRLIGHTMAP_COMBINED)
            return SampleDirectionalLightmap(TEXTURE2D_PARAM(unity_Lightmap_RT, samplerunity_Lightmap_RT), TEXTURE2D_PARAM(unity_LightmapInd_RT, samplerunity_Lightmap_RT),
                                                uvStaticLightmap, unity_LightmapST_RT, normalWS, useRGBMLightmap, decodeInstructions);
        #else
            return SampleSingleLightmap(TEXTURE2D_PARAM(unity_Lightmap_RT, samplerunity_Lightmap_RT), uvStaticLightmap, unity_LightmapST_RT, useRGBMLightmap, decodeInstructions);
        #endif
    #else
        // TODO: pass a tab of coefficient instead!
        real4 SHCoefficients[7];
        SHCoefficients[0] = unity_SHAr_RT;
        SHCoefficients[1] = unity_SHAg_RT;
        SHCoefficients[2] = unity_SHAb_RT;
        SHCoefficients[3] = unity_SHBr_RT;
        SHCoefficients[4] = unity_SHBg_RT;
        SHCoefficients[5] = unity_SHBb_RT;
        SHCoefficients[6] = unity_SHC_RT;

        return SampleSH9(SHCoefficients, normalWS);
    #endif
}


void InitBuiltinData(   float alpha, float3 normalWS, float3 backNormalWS, float3 positionRWS, float4 texCoord1, out BuiltinData builtinData)
{
    ZERO_INITIALIZE(BuiltinData, builtinData);

    builtinData.opacity = alpha;

    // Sample lightmap/lightprobe/volume proxy
    builtinData.bakeDiffuseLighting = SampleBakedGI(positionRWS, normalWS, texCoord1.xy);
    // We also sample the back lighting in case we have transmission. If not use this will be optimize out by the compiler
    // For now simply recall the function with inverted normal, the compiler should be able to optimize the lightmap case to not resample the directional lightmap
    // however it may not optimize the lightprobe case due to the proxy volume relying on dynamic if (to verify), not a problem for SH9, but a problem for proxy volume.
    // TODO: optimize more this code.    
    builtinData.backBakeDiffuseLighting = SampleBakedGI(positionRWS, backNormalWS, texCoord1.xy);

    // Use uniform directly - The float need to be cast to uint (as unity don't support to set a uint as uniform)
    builtinData.renderingLayers = 0;
}

#define USE_RAY_CONE_LOD

float computeTextureLOD(Texture2D targetTexture, float4 uvMask, float3 viewWS, float3 normalWS, RayCone rayCone, IntersectionVertice intersectionVertice)
{
    // First of all we need to grab the dimensions of the target texture
    uint texWidth, texHeight, numMips;
    targetTexture.GetDimensions(0, texWidth, texHeight, numMips);

    // Fetch the target area based on the mask
    float targetTexcoordArea = uvMask.x * intersectionVertice.texCoord0Area 
                        + uvMask.y * intersectionVertice.texCoord1Area
                        + uvMask.z * intersectionVertice.texCoord2Area
                        + uvMask.w * intersectionVertice.texCoord3Area;

    // Compute dot product between view and surface normal
    float lambda = 0.0; //0.5f * log2(targetTexcoordArea / intersectionVertice.triangleArea);
    lambda += log2(abs(rayCone.width));
    lambda += 0.5 * log2(texWidth * texHeight);
    lambda -= log2(abs(dot(viewWS, normalWS)));
    return lambda;
}
