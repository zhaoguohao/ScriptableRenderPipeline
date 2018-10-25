Shader "Hidden/HDRenderPipeline/OpaqueAtmosphericScattering"
{
    HLSLINCLUDE
        #pragma target 4.5
        #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

        #pragma multi_compile _ DEBUG_DISPLAY

        // #pragma enable_d3d11_debug_symbols

        Texture2DMS<float> _DepthTextureMS;
        
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"

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
            output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            return output;
        }

        inline float4 AtmosphericScatteringCompute(Varyings input, float depth)
        {
            PositionInputs posInput = GetPositionInput_Stereo(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V, unity_StereoEyeIndex);

            if (depth == UNITY_RAW_FAR_CLIP_VALUE)
            {
                // When a pixel is at far plane, the world space coordinate reconstruction is not reliable.
                // So in order to have a valid position (for example for height fog) we just consider that the sky is a sphere centered on camera with a radius of 5km (arbitrarily chosen value!)
                // And recompute the position on the sphere with the current camera direction.
                float3 viewDirection = -GetWorldSpaceNormalizeViewDir(posInput.positionWS) * 5000.0f;
                posInput.positionWS = GetCurrentViewPosition() + viewDirection;
            }

            return EvaluateAtmosphericScattering(posInput);
        }

        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float depth = LOAD_TEXTURE2D(_CameraDepthTexture, input.positionCS.xy).x;
            return AtmosphericScatteringCompute(input, depth);
        }

        float4 FragMSAA(Varyings input, uint sampleIndex: SV_SampleIndex) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            int2 msTex = int2(TexCoordStereoOffset(input.texcoord.xy * _ScreenSize.xy));
            float depth = _DepthTextureMS.Load(msTex, sampleIndex).x;
            return AtmosphericScatteringCompute(input, depth);
        }
    ENDHLSL

    SubShader
    {
        // 0: NOMSAA
        Pass
        {
            Cull Off ZTest  Always ZWrite Off Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }

        // 1: MSAA
        Pass
        {
            Cull Off ZTest  Always ZWrite Off Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragMSAA
            ENDHLSL
        }
    }
}
