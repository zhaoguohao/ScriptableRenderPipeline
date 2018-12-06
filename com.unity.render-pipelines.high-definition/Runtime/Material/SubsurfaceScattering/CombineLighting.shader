Shader "Hidden/HDRenderPipeline/CombineLighting"
{
    Properties
    {
        [HideInInspector] _StencilMask("_StencilMask", Int) = 7
    }

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Stencil
            {
                ReadMask [_StencilMask]
                Ref  1 // StencilLightingUsage.SplitLighting
                Comp Equal
                Pass Keep
            }

            Cull   Off
            ZTest  Always
            ZWrite Off
            Blend  One One // Additive

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
            // #pragma enable_d3d11_debug_symbols

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D_ARRAY(_IrradianceSource);

            struct Attributes
            {
                uint vertexID : SV_VertexID;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_Position;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return LOAD_TEXTURE2D_ARRAY(_IrradianceSource, input.positionCS.xy, unity_StereoEyeIndex);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
