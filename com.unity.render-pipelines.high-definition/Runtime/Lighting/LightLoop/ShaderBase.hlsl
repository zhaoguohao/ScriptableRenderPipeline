#ifndef __SHADERBASE_H__
#define __SHADERBASE_H__

#ifdef SHADER_API_PSSL

#ifndef Texture2DMS
    #define Texture2DMS     MS_Texture2D
#endif

#ifndef SampleCmpLevelZero
    #define SampleCmpLevelZero              SampleCmpLOD0
#endif

#ifndef firstbithigh
    #define firstbithigh        FirstSetBit_Hi
#endif

#endif

float FetchDepth(Texture2DArray depthTexture, uint2 pixCoord)
{
    float zdpth = LOAD_TEXTURE2D_ARRAY(depthTexture, pixCoord.xy, unity_StereoEyeIndex).x;
#if UNITY_REVERSED_Z
    zdpth = 1.0 - zdpth;
#endif
    return zdpth;
}

float FetchDepthMSAA(Texture2DMSArray<float> depthTexture, uint2 pixCoord, uint sampleIdx)
{
    float zdpth = LOAD_TEXTURE2D_ARRAY_MSAA(depthTexture, pixCoord.xy, unity_StereoEyeIndex, sampleIdx).x;
#if UNITY_REVERSED_Z
    zdpth = 1.0 - zdpth;
#endif
    return zdpth;
}

#endif
