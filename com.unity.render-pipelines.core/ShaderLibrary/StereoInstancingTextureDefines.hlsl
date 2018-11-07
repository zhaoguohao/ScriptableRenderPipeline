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

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(textureName) TEXTURE2D_ARRAY(textureName)
#define TEXTUREMS(textureName) TEXTURE2DMS_ARRAY(textureName)
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(textureName, uv) SAMPLE_TEXTURE2D_ARRAY(textureName, sampler##textureName, uv, unity_StereoEyeIndex)
#define RW_TEXTURE(type, textureName) RW_TEXTURE2D_ARRAY(type, textureName)
#define TEXTURE_FLOAT(textureName) TEXTURE2D_ARRAY_FLOAT(textureName)
#define TEXTURE_TYPE(type, textureName) TEXTURE2D_ARRAY_TYPE(type, textureName)
#define LOAD_TEXTURE(textureName, pixelCoord) LOAD_TEXTURE2D_ARRAY(textureName, pixelCoord, unity_StereoEyeIndex)
#define LOAD_TEXTURE_MSAA(textureName, pixelCoord, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(textureName, pixelCoord, unity_StereoEyeIndex, sampleIndex)
#define LOAD_TEXTURE_LOD(textureName, pixelCoord, lod) LOAD_TEXTURE2D_ARRAY_LOD(textureName, pixelCoord, unity_StereoEyeIndex, lod)
#define SAMPLE_TEXTURE_LOD(textureName, samplerName, pixelCoord, lod) SAMPLE_TEXTURE2D_ARRAY_LOD(textureName, samplerName, pixelCoord, unity_StereoEyeIndex, lod)
#define INDEX_TEXTURE(pixelCoord) uint3(pixelCoord, unity_StereoEyeIndex)
#else
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(textureName) TEXTURE2D(textureName)
#define TEXTUREMS(textureName) TEXTURE2DMS(textureName)
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(textureName, uv) SAMPLE_TEXTURE2D(textureName, sampler##textureName, uv)
#define RW_TEXTURE(type, textureName) RW_TEXTURE2D(type, textureName)
#define TEXTURE_FLOAT(textureName) TEXTURE2D_FLOAT(textureName)
#define TEXTURE_TYPE(type, textureName) TEXTURE2D_TYPE(type, textureName)
#define LOAD_TEXTURE(textureName, pixelCoord) LOAD_TEXTURE2D(textureName, pixelCoord)
#define LOAD_TEXTURE_MSAA(textureName, pixelCoord, sampleIndex) LOAD_TEXTURE2D_MSAA(textureName, pixelCoord, sampleIndex)
#define LOAD_TEXTURE_LOD(textureName, pixelCoord, lod) LOAD_TEXTURE2D_LOD(textureName, pixelCoord, lod) 
#define SAMPLE_TEXTURE_LOD(textureName, samplerName, pixelCoord, lod) SAMPLE_TEXTURE2D_LOD(textureName, samplerName, pixelCoord, lod)
#define INDEX_TEXTURE(pixelCoord) uint2(pixelCoord)
#endif // if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#endif // ifndef STEREO_INSTANCING_TEXTURE_DEFINES
