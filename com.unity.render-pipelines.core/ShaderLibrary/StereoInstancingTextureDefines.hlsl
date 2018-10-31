//////////////////////////////////////////////////////
// Texture declarations and samplers for instancing
//

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
#if SHADER_STAGE_COMPUTE
#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    float _ComputeEyeIndex;
    #define unity_StereoEyeIndex _ComputeEyeIndex
#endif
#endif
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(textureName) TEXTURE2D_ARRAY(textureName)
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(textureName, uv) SAMPLE_TEXTURE2D_ARRAY(textureName, sampler##textureName, uv, unity_StereoEyeIndex)
#define RW_TEXTURE(type, textureName) RW_TEXTURE2D_ARRAY(type, textureName)
#define TEXTURE(type, textureName) Texture2DArray<type> textureName
#define LOAD_TEXTURE(textureName, pixelCoord) LOAD_TEXTURE2D_ARRAY(textureName, pixelCoord, unity_StereoEyeIndex)
#define LOAD_TEXTURE_LOD(textureName, pixelCoord, lod) LOAD_TEXTURE2D_ARRAY_LOD(textureName, pixelCoord, unity_StereoEyeIndex, lod)
#define INDEX_TEXTURE(pixelCoord) uint3(pixelCoord, unity_StereoEyeIndex)
#else
#define UNITY_DECLARE_SCREENSPACE_TEXTURE(textureName) TEXTURE2D(textureName)
#define UNITY_SAMPLE_SCREENSPACE_TEXTURE(textureName, uv) SAMPLE_TEXTURE2D(textureName, sampler##textureName, uv)
#define RW_TEXTURE(type, textureName) RW_TEXTURE2D(type, textureName)
#define TEXTURE(type, textureName) Texture2D<type> textureName
#define LOAD_TEXTURE(textureName, pixelCoord) LOAD_TEXTURE2D(textureName, pixelCoord)
#define LOAD_TEXTURE_LOD(textureName, pixelCoord, lod) LOAD_TEXTURE2D_LOD(textureName, pixelCoord, lod) 
#define INDEX_TEXTURE(pixelCoord) uint2(pixelCoord)
#endif
