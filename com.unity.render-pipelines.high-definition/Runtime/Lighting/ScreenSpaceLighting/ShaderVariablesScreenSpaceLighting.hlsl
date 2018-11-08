#ifdef SHADER_VARIABLES_INCLUDE_CB
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/ScreenSpaceLighting/ShaderVariablesScreenSpaceLighting.cs.hlsl"
#else
    // Rough refraction texture
    // Color pyramid (width, height, lodcount, Unused)
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_ColorPyramidTexture);
    // Depth pyramid (width, height, lodcount, Unused)
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_DepthPyramidTexture);
    // Ambient occlusion texture
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_AmbientOcclusionTexture);
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraMotionVectorsTexture);
    UNITY_DECLARE_SCREENSPACE_TEXTURE(_SsrLightingTexture);
#endif
