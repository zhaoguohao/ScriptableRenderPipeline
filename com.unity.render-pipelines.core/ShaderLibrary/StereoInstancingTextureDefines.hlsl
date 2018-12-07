//////////////////////////////////////////////////////////////////////
// This file defines macros for texture declarations and accessors
// that are compatible with single-pass instanced rendering.
//
// We expect UNITY_STEREO_INSTANCING_ENABLED and UNITY_STEREO_MULTIVIEW_ENABLED
// to be set up prior to including this file.
//

#ifndef STEREO_INSTANCING_TEXTURE_DEFINES
#define STEREO_INSTANCING_TEXTURE_DEFINES
///////////////////////////
// Compute-specific defines
//

#ifndef UNITY_SHADER_VARIABLES_INCLUDED
#if SHADER_STAGE_COMPUTE
CBUFFER_START(UnityPerPassStereoForCompute)
float _ComputeEyeIndex;
CBUFFER_END
#define unity_StereoEyeIndex _ComputeEyeIndex
#endif
#endif

//////////////////////////////////////////////////////
// Texture declarations and samplers for instancing
//
////////////////////////////////////////////
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) // Macros for Tex2DArrays
////////////////////////////////////////////
// Reimplementing macros from HLSLSupport.cginc
#define UNITY_DECLARE_DEPTH_TEXTURE_MS(tex) Texture2DMSArray<float> tex; SamplerState sampler##tex
#define UNITY_DECLARE_DEPTH_TEXTURE(tex) Texture2DArray tex; SamplerState sampler##tex
#define SAMPLE_RAW_DEPTH_TEXTURE(tex, uv2) tex.Sample(sampler##tex, float3(uv2.xy, (float)unity_StereoEyeIndex))
#define SAMPLE_RAW_DEPTH_TEXTURE_PROJ(tex, uv4) tex.Sample(sampler##tex, float3(uv4.x/uv4.w, uv4.y/uv4.w, (float)unity_StereoEyeIndex))
#define SAMPLE_RAW_DEPTH_TEXTURE_LOD(tex, uv4) tex.SampleLevel(sampler##tex).r, float3(uv4.xy, (float)unity_StereoEyeIndex), uv4.w)
#undef SAMPLE_DEPTH_TEXTURE // Conflict with API.hlsl files
#define SAMPLE_DEPTH_TEXTURE(tex, uv2) SAMPLE_RAW_DEPTH_TEXTURE(tex, uv2).r
#define SAMPLE_DEPTH_TEXTURE_PROJ(tex, uv4) SAMPLE_RAW_DEPTH_TEXTURE_PROJ(tex, uv4).r
#undef SAMPLE_DEPTH_TEXTURE_LOD // Conflict with API.hlsl files
#define SAMPLE_DEPTH_TEXTURE_LOD(tex, uv4) SAMPLE_RAW_DEPTH_TEXTURE_LOD(tex, uv4).r
#define UNITY_DECLARE_SCREENSPACE_SHADOWMAP Texture2DArray tex; SamplerState sampler##tex
#define UNITY_SAMPLE_SCREEN_SHADOW(tex, uv2) tex.Sample(sampler##tex, float3(uv2.xy, (float)unity_StereoEyeIndex))
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(tex) Texture2DArray tex; SamplerState sampler##tex
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(tex, uv2) tex.Sample(sampler##tex, float3(uv2.xy, (float)unity_StereoEyeIndex))

// Additional macros for HDRP
#define TEXTUREMS(tex) TEXTURE2DMS_ARRAY(tex)
#define DEPTH_TEXTURE_MS(tex, samples) Texture2DMSArray<float, samples> tex // For consistency with LWRP
#define TEXTUREMS_TYPE(type, tex) TEXTURE2DMS_ARRAY_TYPE(type, tex)
#define RW_TEXTURE(type, tex) RW_TEXTURE2D_ARRAY(type, tex)
#define TEXTURE_FLOAT(tex) TEXTURE2D_ARRAY_FLOAT(tex)
#define TEXTURE_TYPE(type, tex) TEXTURE2D_ARRAY_TYPE(type, tex)
#define LOAD_TEXTURE(tex, pixelCoord) LOAD_TEXTURE2D_ARRAY(tex, pixelCoord, unity_StereoEyeIndex)
#define LOAD_TEXTURE_MSAA(tex, pixelCoord, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(tex, pixelCoord, unity_StereoEyeIndex, sampleIndex)
#define LOAD_TEXTURE_LOD(tex, pixelCoord, lod) LOAD_TEXTURE2D_ARRAY_LOD(tex, pixelCoord, unity_StereoEyeIndex, lod)
#define SAMPLE_TEXTURE(tex, samplerName, pixelCoord) SAMPLE_TEXTURE2D_ARRAY(tex, samplerName, pixelCoord, unity_StereoEyeIndex)
#define SAMPLE_TEXTURE_LOD(tex, samplerName, pixelCoord, lod) SAMPLE_TEXTURE2D_ARRAY_LOD(tex, samplerName, pixelCoord, unity_StereoEyeIndex, lod)
#define INDEX_TEXTURE(pixelCoord) uint3(pixelCoord, unity_StereoEyeIndex)
////////////////////////////////////
#else // Macros for Tex2Ds
////////////////////////////////////
// Reimplementing macros from HLSLSupport.cginc
#define UNITY_DECLARE_DEPTH_TEXTURE_MS(tex) Texture2DMS<float> tex; SamplerState sampler##tex
#define UNITY_DECLARE_DEPTH_TEXTURE(tex) Texture2D tex; SamplerState sampler##tex
#define SAMPLE_RAW_DEPTH_TEXTURE(tex, uv2) tex.Sample(sampler##tex, uv2.xy)
#define SAMPLE_RAW_DEPTH_TEXTURE_PROJ(tex, uv4) tex.Sample(sampler##tex, float2(uv4.x/uv4.w, uv4.y/uv4.w))
#define SAMPLE_RAW_DEPTH_TEXTURE_LOD(tex, uv4) tex.SampleLevel(sampler##tex).r, uv4.xy, uv4.w)
#define SAMPLE_DEPTH_TEXTURE_PROJ(tex, uv4) SAMPLE_RAW_DEPTH_TEXTURE_PROJ(tex, uv4).r
#define UNITY_DECLARE_SCREENSPACE_SHADOWMAP Texture2D tex; SamplerState sampler##tex
#define UNITY_SAMPLE_SCREEN_SHADOW(tex, uv2) tex.Sample(sampler##tex, uv2.xy)
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(tex) Texture2D tex; SamplerState sampler##tex
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(tex, uv2) tex.Sample(sampler##tex, uv2.xy)

// Additional macros for HDRP
#define TEXTUREMS(tex) TEXTURE2DMS(tex)
#define DEPTH_TEXTURE_MS(tex, samples) Texture2DMS<float, samples> tex
#define TEXTUREMS_TYPE(type, tex) TEXTURE2DMS_TYPE(type, tex)
#define RW_TEXTURE(type, tex) RW_TEXTURE2D(type, tex)
#define TEXTURE_FLOAT(tex) TEXTURE2D_FLOAT(tex)
#define TEXTURE_TYPE(type, tex) TEXTURE2D_TYPE(type, tex)
#define LOAD_TEXTURE(tex, pixelCoord) LOAD_TEXTURE2D(tex, pixelCoord)
#define LOAD_TEXTURE_MSAA(tex, pixelCoord, sampleIndex) LOAD_TEXTURE2D_MSAA(tex, pixelCoord, sampleIndex)
#define LOAD_TEXTURE_LOD(tex, pixelCoord, lod) LOAD_TEXTURE2D_LOD(tex, pixelCoord, lod) 
#define SAMPLE_TEXTURE(tex, samplerName, pixelCoord) SAMPLE_TEXTURE2D(tex, samplerName, pixelCoord)
#define SAMPLE_TEXTURE_LOD(tex, samplerName, pixelCoord, lod) SAMPLE_TEXTURE2D_LOD(tex, samplerName, pixelCoord, lod)
#define INDEX_TEXTURE(pixelCoord) uint2(pixelCoord)
#endif // if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#endif // ifndef STEREO_INSTANCING_TEXTURE_DEFINES
