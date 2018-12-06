Shader "Hidden/HDRenderPipeline/ColorResolve"
{
    HLSLINCLUDE
        #pragma target 4.5
        #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        #pragma enable_d3d11_debug_symbols

        TEXTURE2DMS_ARRAY_TYPE(float4, _ColorTextureMS);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID) * _ScreenSize.xy;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            return output;
        }

        float4 Frag1X(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            int2 pixelCoords = int2(TexCoordStereoOffset(input.texcoord));
            return LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 0);
        }

        float4 Frag2X(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            int2 pixelCoords = int2(TexCoordStereoOffset(input.texcoord));
            return FastTonemapInvert((FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 0)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 1))) * 0.5f);
        }

        float4 Frag4X(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            int2 pixelCoords = int2(TexCoordStereoOffset(input.texcoord));
            return FastTonemapInvert((FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 0)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 1))
                            + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 2)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 3))) * 0.25f);
        }

        float4 Frag8X(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            int2 pixelCoords = int2(TexCoordStereoOffset(input.texcoord));
            return FastTonemapInvert((FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 0)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 1))
                            + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 2)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 3))
                            + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 4)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 5))
                            + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 6)) + FastTonemap(LOAD_TEXTURE2D_ARRAY_MSAA(_ColorTextureMS, pixelCoords, unity_StereoEyeIndex, 7))) * 0.125f);
        }
    ENDHLSL
    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        // 0: MSAA 1x
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag1X
            ENDHLSL
        }

        // 1: MSAA 2x
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag2X
            ENDHLSL
        }

        // 2: MSAA 4X
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag4X
            ENDHLSL
        }

        // 3: MSAA 8X
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag8X
            ENDHLSL
        }
    }
    Fallback Off
}
