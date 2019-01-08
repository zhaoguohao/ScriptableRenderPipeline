Shader "Lightweight Render Pipeline/Nature/SpeedTree8"
{
    Properties
    {
        _MainTex ("Base (RGB) Transparency (A)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        [Toggle(EFFECT_HUE_VARIATION)] _HueVariationKwToggle("Hue Variation", Float) = 0
        _HueVariationColor ("Hue Variation Color", Color) = (1.0,0.5,0.0,0.1)

        [Toggle(EFFECT_BUMP)] _NormalMapKwToggle("Normal Mapping", Float) = 0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _ExtraTex ("Smoothness (R), Metallic (G), AO (B)", 2D) = "(0.5, 0.0, 1.0)" {}
        _Glossiness ("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0

        [Toggle(EFFECT_SUBSURFACE)] _SubsurfaceKwToggle("Subsurface", Float) = 0
        _SubsurfaceTex ("Subsurface (RGB)", 2D) = "white" {}
        _SubsurfaceColor ("Subsurface Color", Color) = (1,1,1,1)
        _SubsurfaceIndirect ("Subsurface Indirect", Range(0.0, 1.0)) = 0.25

        [Toggle(EFFECT_BILLBOARD)] _BillboardKwToggle("Billboard", Float) = 0
        _BillboardShadowFade ("Billboard Shadow Fade", Range(0.0, 1.0)) = 0.5

        [Enum(No,2,Yes,0)] _TwoSided ("Two Sided", Int) = 2 // enum matches cull mode
        [KeywordEnum(None,Fastest,Fast,Better,Best,Palm)] _WindQuality ("Wind Quality", Range(0,5)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "DisableBatching"="LODFading"
			"RenderPipeline" = "LightweightPipeline"
        }
        LOD 400
        Cull [_TwoSided]

		Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "LightweightForward" }

            HLSLPROGRAM
            #pragma target 3.0

            // -------------------------------------
            // Lightweight Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling maxcount:50
			
            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local _WINDQUALITY_NONE _WINDQUALITY_FASTEST _WINDQUALITY_FAST _WINDQUALITY_BETTER _WINDQUALITY_BEST _WINDQUALITY_PALM
            #pragma shader_feature_local EFFECT_BILLBOARD
            #pragma shader_feature_local EFFECT_HUE_VARIATION
            #pragma shader_feature_local EFFECT_SUBSURFACE
            #pragma shader_feature_local EFFECT_BUMP
            #pragma shader_feature_local EFFECT_EXTRA_TEX

            #define ENABLE_WIND
            #define EFFECT_BACKSIDE_NORMALS
            #define _ALPHATEST_ON
            #define _NORMALMAP

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"
            
            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags{"LightMode" = "SceneSelectionPass"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local _WINDQUALITY_NONE _WINDQUALITY_FASTEST _WINDQUALITY_FAST _WINDQUALITY_BETTER _WINDQUALITY_BEST _WINDQUALITY_PALM
            #pragma shader_feature_local EFFECT_BILLBOARD

            #define ENABLE_WIND
            #define DEPTH_ONLY
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0

            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local _WINDQUALITY_NONE _WINDQUALITY_FASTEST _WINDQUALITY_FAST _WINDQUALITY_BETTER _WINDQUALITY_BEST _WINDQUALITY_PALM
            #pragma shader_feature_local EFFECT_BILLBOARD

            #define ENABLE_WIND
            #define DEPTH_ONLY
            #define SHADOW_CASTER
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0

            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local _WINDQUALITY_NONE _WINDQUALITY_FASTEST _WINDQUALITY_FAST _WINDQUALITY_BETTER _WINDQUALITY_BEST _WINDQUALITY_PALM
            #pragma shader_feature_local EFFECT_BILLBOARD

            #define ENABLE_WIND
            #define DEPTH_ONLY
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"

            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "RenderType" = "TransparentCutout"
            "DisableBatching" = "LODFading"
            "RenderPipeline" = "LightweightPipeline"
        }
        LOD 400
        Cull[_TwoSided]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "LightweightForward" }

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            // -------------------------------------
            // Lightweight Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local EFFECT_BILLBOARD
            #pragma shader_feature_local EFFECT_EXTRA_TEX

            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "SceneSelectionPass"
            Tags{"LightMode" = "SceneSelectionPass"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0


            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local EFFECT_BILLBOARD

            #define DEPTH_ONLY
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0


            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local EFFECT_BILLBOARD

            #define DEPTH_ONLY
            #define SHADOW_CASTER
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma multi_compile_vertex LOD_FADE_PERCENTAGE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling maxcount:50

            #pragma vertex SpeedTree8Vert
            #pragma fragment SpeedTree8Frag

            #pragma shader_feature_local EFFECT_BILLBOARD

            #define DEPTH_ONLY
            #define _ALPHATEST_ON

            #include "SpeedTree8Input.hlsl"
            #include "SpeedTree8Passes.hlsl"

            ENDHLSL
        }
    }

    FallBack "Lightweight Render Pipeline/Simple Lit"
    CustomEditor "SpeedTree8ShaderGUI"
}
