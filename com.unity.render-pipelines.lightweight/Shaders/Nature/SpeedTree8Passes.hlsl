#ifndef LIGHTWEIGHT_SPEEDTREE8_PASSES_INCLUDED
#define LIGHTWEIGHT_SPEEDTREE8_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Lighting.hlsl"

struct SpeedTreeVertexInput
{
    float4 vertex       : POSITION;
    float3 normal       : NORMAL;
	float4 tangent      : TANGENT;
    float4 texcoord     : TEXCOORD0;
	float4 texcoord1    : TEXCOORD1;
	float4 texcoord2    : TEXCOORD2;
	float4 texcoord3    : TEXCOORD3;
    float4 color        : COLOR;
	
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SpeedTreeVertexInterpolated
{
#ifdef DEPTH_ONLY
    float2 uv                       : TEXCOORD0;
    half4 color                     : TEXCOORD1;
    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
#else
    float2 uv                       : TEXCOORD0;
    // Lightnaps/GI?                : TEXCOORD1;
    float4 posWS                    : TEXCOORD2;

    half4 normalWS                  : TEXCOORD3;    // xyz: tangent, w: viewDir.x
    half4 tangentWS                 : TEXCOORD4;    // xyz: tangent, w: viewDir.y
    half4 bitangentWS               : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
    #ifdef _MAIN_LIGHT_SHADOWS
        float4 shadowCoord          : TEXCOORD6;
    #endif

    half4 color                     : TEXCOORD7;
    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
#endif
};

struct SpeedTreeVertexOutput
{
    SpeedTreeVertexInterpolated interpolated;
};

struct SpeedTreeFragmentInput
{
    SpeedTreeVertexInterpolated interpolated;
#ifdef EFFECT_BACKSIDE_NORMALS
    half facing : VFACE;
#endif
};

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

void InitializeVertData(inout SpeedTreeVertexInput input, inout SpeedTreeVertexOutput output, float lodValue)
{
    // smooth LOD
    #if defined(LOD_FADE_PERCENTAGE) && !defined(EFFECT_BILLBOARD)
        input.vertex.xyz = lerp(input.vertex.xyz, input.texcoord2.xyz, lodValue);
    #endif

    // wind
    #if defined(ENABLE_WIND) && !defined(_WINDQUALITY_NONE)
        if (_WindEnabled > 0)
        {
            float3 rotatedWindVector = mul(_ST_WindVector.xyz, (float3x3)unity_ObjectToWorld);
            float windLength = length(rotatedWindVector);
            if (windLength < 1e-5)
            {
                // sanity check that wind data is available
                return;
            }
            rotatedWindVector /= windLength;

            float3 treePos = float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
            float3 windyPosition = input.vertex.xyz;

            #ifndef EFFECT_BILLBOARD
                // geometry type
                float geometryType = (int)(input.texcoord3.w + 0.25);
                bool leafTwo = false;
                if (geometryType > GEOM_TYPE_FACINGLEAF)
                {
                    geometryType -= 2;
                    leafTwo = true;
                }

                // leaves
                if (geometryType > GEOM_TYPE_FROND)
                {
                    // remove anchor position
                    float3 anchor = float3(input.texcoord1.zw, input.texcoord2.w);
                    windyPosition -= anchor;

                    if (geometryType == GEOM_TYPE_FACINGLEAF)
                    {
                        // face camera-facing leaf to camera
                        float offsetLen = length(windyPosition);
                        windyPosition = mul(windyPosition.xyz, (float3x3)UNITY_MATRIX_IT_MV); // inv(MV) * windyPosition
                        windyPosition = normalize(windyPosition) * offsetLen; // make sure the offset vector is still scaled
                    }

                    // leaf wind
                    #if defined(_WINDQUALITY_FAST) || defined(_WINDQUALITY_BETTER) || defined(_WINDQUALITY_BEST)
                        #ifdef _WINDQUALITY_BEST
                            bool bBestWind = true;
                        #else
                            bool bBestWind = false;
                        #endif
                        float leafWindTrigOffset = anchor.x + anchor.y;
                        windyPosition = LeafWind(bBestWind, leafTwo, windyPosition, input.normal, input.texcoord3.x, float3(0,0,0), input.texcoord3.y, input.texcoord3.z, leafWindTrigOffset, rotatedWindVector);
                    #endif

                    // move back out to anchor
                    windyPosition += anchor;
                }

                // frond wind
                bool bPalmWind = false;
                #ifdef _WINDQUALITY_PALM
                    bPalmWind = true;
                    if (geometryType == GEOM_TYPE_FROND)
                    {
                        windyPosition = RippleFrond(windyPosition, input.normal, input.texcoord.x, input.texcoord.y, input.texcoord3.x, input.texcoord3.y, input.texcoord3.z);
                    }
                #endif

                // branch wind (applies to all 3D geometry)
                #if defined(_WINDQUALITY_BETTER) || defined(_WINDQUALITY_BEST) || defined(_WINDQUALITY_PALM)
                    float3 rotatedBranchAnchor = normalize(mul(_ST_WindBranchAnchor.xyz, (float3x3)unity_ObjectToWorld)) * _ST_WindBranchAnchor.w;
                    windyPosition = BranchWind(bPalmWind, windyPosition, treePos, float4(input.texcoord.zw, 0, 0), rotatedWindVector, rotatedBranchAnchor);
                #endif

            #endif // !EFFECT_BILLBOARD

            // global wind
            float globalWindTime = _ST_WindGlobal.x;
            #if defined(EFFECT_BILLBOARD) && defined(UNITY_INSTANCING_ENABLED)
                globalWindTime += UNITY_ACCESS_INSTANCED_PROP(STWind, _GlobalWindTime);
            #endif
            windyPosition = GlobalWind(windyPosition, treePos, true, rotatedWindVector, globalWindTime);
            input.vertex.xyz = windyPosition;
        }
    #endif
}

///////////////////////////////////////////////////////////////////////
//  vertex program

SpeedTreeVertexOutput SpeedTree8Vert(SpeedTreeVertexInput input)
{
    SpeedTreeVertexOutput output = (SpeedTreeVertexOutput)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output.interpolated);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output.interpolated);

	// handle speedtree wind and lod
	InitializeVertData(input, output, unity_LODFade.x);
    output.interpolated.color = input.color;

#ifdef DEPTH_ONLY
    // Probably need to deal with the shadow bias for tree self-shadowing?
    output.interpolated.uv = input.texcoord.xy;
    half3 normalWS = TransformObjectToWorldNormal(input.normal);
    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
    output.interpolated.clipPos = vertexInput.positionCS;
    #ifdef SHADOW_CASTER
        float4 positionCS = TransformWorldToHClip(ApplyShadowBias(vertexInput.positionWS, normalWS, _LightDirection));
        output.interpolated.clipPos = positionCS;
    #endif
#else
    float3 treePos = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
    #if defined(EFFECT_BILLBOARD)
        // crossfade faces
        bool topDown = (input.texcoord.z > 0.5);
        float3 viewDir = UNITY_MATRIX_IT_MV[2].xyz;
        float3 cameraDir = normalize(mul((float3x3)unity_WorldToObject, _WorldSpaceCameraPos - treePos));
        float viewDot = max(dot(viewDir, input.normal), dot(cameraDir, input.normal));
        viewDot *= viewDot;
        viewDot *= viewDot;
        viewDot += topDown ? 0.38 : 0.18; // different scales for horz and vert billboards to fix transition zone

        // if invisible, avoid overdraw
        if (viewDot < 0.3333)
        {
            input.vertex.xyz = float3(0,0,0);
        }

        output.interpolated.color = float4(1, 1, 1, clamp(viewDot, 0, 1));

        // adjust lighting on billboards to prevent seams between the different faces
        if (topDown)
        {
            input.normal += cameraDir;
        }
        else
        {
            half3 binormal = cross(input.normal, input.tangent.xyz) * input.tangent.w;
            float3 right = cross(cameraDir, binormal);
            input.normal = cross(binormal, right);
        }
        input.normal = normalize(input.normal);
    #endif

    // color already contains (ao, ao, ao, blend)
    // put hue variation amount in there
    #ifdef EFFECT_HUE_VARIATION
        float hueVariationAmount = frac(treePos.x + treePos.y + treePos.z);
        output.interpolated.color.g = saturate(hueVariationAmount * _HueVariationColor.a);
    #endif
	
	VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
	
    output.interpolated.uv = input.texcoord.xy;
    output.interpolated.posWS.xyz = vertexInput.positionWS;

    #ifdef _MAIN_LIGHT_SHADOWS
		output.interpolated.shadowCoord = GetShadowCoord(vertexInput);
	#endif

    //output.interpolated.posWS.w = ComputeFogFactor(vertexInput.positionCS.z);
    output.interpolated.posWS.w = 1.0;
	
    real sign = input.tangent.w * GetOddNegativeScale();
	output.interpolated.normalWS.xyz = TransformObjectToWorldNormal(input.normal);
	output.interpolated.tangentWS.xyz = TransformObjectToWorldDir(input.tangent.xyz);
	output.interpolated.bitangentWS.xyz = cross(output.interpolated.normalWS.xyz, output.interpolated.tangentWS.xyz) * sign;

    half3 viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
    // View dir packed in w.
    output.interpolated.normalWS.w = viewDirWS.x;
    output.interpolated.tangentWS.w = viewDirWS.y;
    output.interpolated.bitangentWS.w = viewDirWS.z;

    // Probably need to deal with the shadow bias for tree self-shadowing?
    output.interpolated.clipPos = vertexInput.positionCS;
#endif
	return output;
}

void InitializeInputData(SpeedTreeFragmentInput input, half3 normalTS, out InputData inputData)
{
#ifdef DEPTH_ONLY
    // Nothing
#else
    inputData.positionWS = input.interpolated.posWS.xyz;

    half3 viewDirWS = half3(input.interpolated.normalWS.w, input.interpolated.tangentWS.w, input.interpolated.bitangentWS.w);
#if SHADER_HINT_NICE_QUALITY
    viewDirWS = SafeNormalize(viewDirWS);
#endif

    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.interpolated.tangentWS.xyz, input.interpolated.bitangentWS.xyz, input.interpolated.normalWS.xyz));
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

    inputData.viewDirectionWS = viewDirWS;
#ifdef _MAIN_LIGHT_SHADOWS
    inputData.shadowCoord = input.interpolated.shadowCoord;
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = 0;
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = half3(0, 0, 0);

    //inputData.fogCoord = input.fogFactorAndVertexLight.x;
    //inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    //inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
#endif
}

half4 SpeedTree8Frag(SpeedTreeFragmentInput input) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(input.interpolated);

    float2 uv = input.interpolated.uv;
    half4 diffuseAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_PARAM(_MainTex, sampler_MainTex)) * _Color;

    half alpha = diffuseAlpha.a * input.interpolated.color.a;
    AlphaDiscard(alpha - 0.3333, 0.0);

    #ifdef DEPTH_ONLY
        return half4(1,1,1,1);
    #else
        half3 diffuse = diffuseAlpha.rgb;
        half3 emission = 0;
	    half metallic = 0;
	    half smoothness = 0;
	    half occlusion = 0;
        half3 specular = 0;
        half3 normalTs = half3(0, 0, 1);

        // hue variation
        #ifdef EFFECT_HUE_VARIATION
            half3 shiftedColor = lerp(diffuse, _HueVariationColor.rgb, input.interpolated.color.g);

            // preserve vibrance
            half maxBase = max(diffuse.r, max(diffuse.g, diffuse.b));
            half newMaxBase = max(shiftedColor.r, max(shiftedColor.g, shiftedColor.b));
            maxBase /= newMaxBase;
            maxBase = maxBase * 0.5f + 0.5f;
            shiftedColor.rgb *= maxBase;

            diffuse = saturate(shiftedColor);
        #endif

        // normal
        #ifdef EFFECT_BUMP
            normalTs = SampleNormal(uv, TEXTURE2D_PARAM(_BumpMap, sampler_BumpMap));
        #elif defined(EFFECT_BACKSIDE_NORMALS) || defined(EFFECT_BILLBOARD)
            normalTs = half3(0, 0, 1);
        #endif

        // flip normal on backsides
        #ifdef EFFECT_BACKSIDE_NORMALS
            if (input.facing < 0.5)
            {
                normalTs.z = -normalTs.z;
            }
        #endif

        // adjust billboard normals to improve GI and matching
        #ifdef EFFECT_BILLBOARD
            normalTs.z *= 0.5;
            normalTs = normalize(normalTs);
        #endif

        // extra
        #ifdef EFFECT_EXTRA_TEX
            half4 extra = tex2D(_ExtraTex, uv);
            smoothness = extra.r;
            metallic = extra.g;
            occlusion = extra.b * input.interpolated.color.r;
        #else
            smoothness = _Glossiness;
            metallic = _Metallic;
            occlusion = input.interpolated.color.r;
        #endif

        // subsurface (hijack emissive)
        #ifdef EFFECT_SUBSURFACE
            emission = tex2D(_SubsurfaceTex, uv).rgb * _SubsurfaceColor.rgb;
        #endif

        InputData inputData;
        InitializeInputData(input, normalTs, inputData);

        half4 color = LightweightFragmentPBR(inputData, diffuse, metallic, specular, smoothness, occlusion, emission, alpha);
        //color.rgb = MixFog(color.rgb, inputData.fogCoord);
    
        return color;
    #endif
}

#endif
