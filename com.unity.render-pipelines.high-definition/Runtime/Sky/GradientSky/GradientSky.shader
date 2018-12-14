Shader "Hidden/HDRenderPipeline/Sky/GradientSky"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma fragment Frag

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    float4x4 _PixelCoordToViewDirWS; // Actually just 3x3, but Unity can only set 4x4
    
	float4 _GradientBottom;
    float4 _GradientMiddle;
    float4 _GradientTop;
    float _GradientDiffusion;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        UNITY_SETUP_INSTANCE_ID(input);
        Varyings output;
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(USING_STEREO_MATRICES)
        // The computed PixelCoordToViewDir matrix doesn't seem to capture stereo eye offset. 
        // So for VR, we compute WSPosition using the stereo matrices instead.
        PositionInputs posInput = GetPositionInput_Stereo(input.positionCS.xy, _ScreenSize.zw, input.positionCS.z, UNITY_MATRIX_I_VP, UNITY_MATRIX_V, unity_StereoEyeIndex);
        float3 viewDirWS = -normalize(posInput.positionWS);
#else
		float3 viewDirWS = normalize(mul(float3(input.positionCS.xy, 1.0), (float3x3)_PixelCoordToViewDirWS));
#endif
        float verticalGradient = viewDirWS.y * _GradientDiffusion;
		float topLerpFactor = saturate(-verticalGradient);
		float bottomLerpFactor = saturate(verticalGradient);
		float3 color = lerp(_GradientMiddle.xyz, _GradientBottom.xyz, bottomLerpFactor);
		color = lerp(color, _GradientTop.xyz, topLerpFactor);
		return float4 (color, 1.0);
    }


    ENDHLSL

    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            ENDHLSL

        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
            ENDHLSL
        }

    }
    Fallback Off
}
