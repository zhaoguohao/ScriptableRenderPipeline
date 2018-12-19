#if (SHADERPASS != SHADERPASS_DEPTH_ONLY && SHADERPASS != SHADERPASS_SHADOWS)
#error SHADERPASS_is_not_correctly_define
#endif

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/VertMesh.hlsl"

InstancedPackedVaryingsType Vert(AttributesMesh inputMesh)
{
    UNITY_SETUP_INSTANCE_ID(inputMesh);
    VaryingsType varyingsType;
    varyingsType.vmesh = VertMesh(inputMesh);
    InstancedPackedVaryingsType outVaryings;
    outVaryings.packedVaryingsType = PackVaryingsType(varyingsType);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(outVaryings);
    return outVaryings;
}

#ifdef TESSELLATION_ON

InstancedPackedVaryingsToPS VertTesselation(VaryingsToDS input)
{
    UNITY_SETUP_INSTANCE_ID(inputMesh);
    VaryingsToPS varyingsToPS;
    varyingsToPS.vmesh = VertMeshTesselation(input.vmesh);
    InstancedPackedVaryingsToPS outVaryings;
    outVaryings.packedVaryingsType = PackVaryingsToPS(varyingsToPS);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(outVaryings);
    return outVaryings;
}

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/TessellationShare.hlsl"

#endif // TESSELLATION_ON

void Frag(  InstancedPackedVaryingsToPS packedInput
            #ifdef WRITE_NORMAL_BUFFER
            , out float4 outNormalBuffer : SV_Target0
                #ifdef WRITE_MSAA_DEPTH
            , out float1 depthColor : SV_Target1
                #endif
            #elif defined(SCENESELECTIONPASS)
            , out float4 outColor : SV_Target0
            #endif

            #ifdef _DEPTHOFFSET_ON
            , out float outputDepth : SV_Depth
            #endif
        )
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(packedInput);
    FragInputs input = UnpackVaryingsMeshToFragInputs(packedInput.packedVaryingsType.vmesh);

    // input.positionSS is SV_Position
    PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

#ifdef VARYINGS_NEED_POSITION_WS
    float3 V = GetWorldSpaceNormalizeViewDir(input.positionRWS);
#else
    // Unused
    float3 V = float3(1.0, 1.0, 1.0); // Avoid the division by 0
#endif

    SurfaceData surfaceData;
    BuiltinData builtinData;
    GetSurfaceAndBuiltinData(input, V, posInput, surfaceData, builtinData);

#ifdef _DEPTHOFFSET_ON
    outputDepth = posInput.deviceDepth;
#endif

#ifdef WRITE_NORMAL_BUFFER
    EncodeIntoNormalBuffer(ConvertSurfaceDataToNormalData(surfaceData), posInput.positionSS, outNormalBuffer);
    #ifdef WRITE_MSAA_DEPTH
    // In case we are rendering in MSAA, reading the an MSAA depth buffer is way too expensive. To avoid that, we export the depth to a color buffer
    depthColor = packedInput.vmesh.positionCS.z;
    #endif
#elif defined(SCENESELECTIONPASS)
    // We use depth prepass for scene selection in the editor, this code allow to output the outline correctly
    outColor = float4(_ObjectId, _PassValue, 1.0, 1.0);
#endif
}
