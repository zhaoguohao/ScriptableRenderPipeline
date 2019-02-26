#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"

#if ((defined(SHADER_API_PSSL) && (UNITY_VERSION < 20192)) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL)) 
#define SCATTERING 0
#else
#define SCATTERING 1
#endif 

#if SCATTERING
#define TILE_SIZE                   16u
#else
#define TILE_SIZE                   32u
#endif
#define WAVE_SIZE                   64u

#ifdef MOTION_VEC_PREPPING 
RW_TEXTURE2D_X(float3, _MotionVecAndDepth);
#else
TEXTURE2D_X(_MotionVecAndDepth);
#endif

#ifdef GEN_PASS
RW_TEXTURE2D_X(uint, _TileToScatterMax);
RW_TEXTURE2D_X(uint, _TileToScatterMin);
RW_TEXTURE2D_X(float3, _TileMinMaxMotionVec);
#else
TEXTURE2D_X(_TileMinMaxMotionVec);
#endif


#if NEIGHBOURHOOD_PASS
RW_TEXTURE2D_X(uint, _TileToScatterMax);
RW_TEXTURE2D_X(uint, _TileToScatterMin);
#endif


CBUFFER_START(MotionBlurUniformBuffer)
float4x4 _PrevVPMatrixNoTranslation;
float4 _TileTargetSize;     // .xy size, .zw 1/size
float4 _MotionBlurParams0;  // Unpacked below.
float4 _MotionBlurParams1;  // Upacked below.
int    _SampleCount;
CBUFFER_END

#define _ScreenMagnitude                    _MotionBlurParams0.x
#define _ScreenMagnitudeSq                  _MotionBlurParams0.y
#define _MinMotionVecThreshold              _MotionBlurParams0.z
#define _MinMotionVecThresholdSq            _MotionBlurParams0.w
#define _MotionBlurIntensity                _MotionBlurParams1.x
#define _MotionBlurMaxMotionVec             _MotionBlurParams1.y
#define _MinMaxMotionVecRatioForSlowPath    _MotionBlurParams1.z
#define _CameraRotationClampNDC             _MotionBlurParams1.w


// --------------------------------------
// Functions that work on encoded representation
// --------------------------------------

float MotionVecLengthInPixelsFromEncoded(float2 motionVec)
{
    return  motionVec.x * _ScreenMagnitude;
}

float2 DecodeMotionVectorFromPacked(float2 motionVec)
{
    float theta = motionVec.y * (2.0 * PI) - PI;
    return  (float2(sin(theta), cos(theta)) * motionVec.x).yx;
}

float MotionVecLengthFromEncoded(float2 motionVec)
{
    return  motionVec.x;
}

float2 MaxMotionVec(float2 v, float2 w)
{
    return (MotionVecLengthFromEncoded(v) < MotionVecLengthFromEncoded(w)) ? w : v;
}
