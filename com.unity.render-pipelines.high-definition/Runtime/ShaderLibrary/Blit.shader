Shader "Hidden/HDRenderPipeline/Blit"
{
    HLSLINCLUDE

        #pragma target 4.5
        #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

        TEXTURE2D(_BlitTexture);
        TEXTURE2D_FLOAT(_BlitTextureDepth);
        SamplerState sampler_PointClamp;
        SamplerState sampler_LinearClamp;
        uniform float4 _BlitScaleBias;
        uniform float4 _BlitScaleBiasRt;
        uniform float _BlitMipLevel;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord   : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;	
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
            return output;
        }

        Varyings VertQuad(Attributes input)
        {
            Varyings output;
            output.positionCS = GetQuadVertexPosition(input.vertexID) * float4(_BlitScaleBiasRt.x, _BlitScaleBiasRt.y, 1, 1) + float4(_BlitScaleBiasRt.z, _BlitScaleBiasRt.w, 0, 0);
            output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
            output.texcoord = GetQuadTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
            return output;
        }

        float4 FragNearest(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
#if UNITY_SINGLE_PASS_STEREO
            uv.x = uv.x / 2.0 + unity_StereoEyeIndex * 0.5;
            uv.y = 1.0 - uv.y; // Always flip Y when rendering stereo since HDRP doesn't support OpenGL
#endif
            return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, _BlitMipLevel);
        }

        float4 FragBilinear(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
#if UNITY_SINGLE_PASS_STEREO
            uv.x = uv.x / 2.0 + unity_StereoEyeIndex * 0.5;
            uv.y = 1.0 - uv.y; // Always flip Y when rendering stereo since HDRP doesn't support OpenGL
#endif
            return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
        }

        // Blits source texture into depth map
        float FragNearestDepth(Varyings input) : SV_Depth
        {
            float2 uv = input.texcoord.xy;
#if UNITY_SINGLE_PASS_STEREO
            uv.x = uv.x / 2.0 + unity_StereoEyeIndex * 0.5;
            uv.y = 1.0 - uv.y; // Always flip Y when rendering stereo since HDRP doesn't support OpenGL
#endif

return 10.0 * SAMPLE_TEXTURE2D_LOD(_BlitTextureDepth, sampler_PointClamp, input.texcoord, _BlitMipLevel).x;
            return SAMPLE_TEXTURE2D_LOD(_BlitTextureDepth, sampler_PointClamp, input.texcoord, _BlitMipLevel).x;
        }
            
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        // 0: Nearest
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragNearest
            ENDHLSL
        }

        // 1: Bilinear
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBilinear
            ENDHLSL
        }

        // 2: Nearest quad
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragNearest
            ENDHLSL
        }

        // 3: Bilinear quad
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragBilinear
            ENDHLSL
        }

        // 4: Depth quad
        Pass
        {
            ZWrite On ZTest Always Blend Off Cull Off
            ColorMask 0 // Don't write to color target

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragNearestDepth
            ENDHLSL
        }
    }

    Fallback Off
}
