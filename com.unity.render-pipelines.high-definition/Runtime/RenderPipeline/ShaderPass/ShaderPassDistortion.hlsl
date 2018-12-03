#if SHADERPASS != SHADERPASS_DISTORTION
#error SHADERPASS_is_not_correctly_define
#endif

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/VertMesh.hlsl"

PackedVaryingsType Vert(AttributesMesh inputMesh)
{
    UNITY_SETUP_INSTANCE_ID(inputMesh);
    VaryingsType varyingsType;
    varyingsType.vmesh = VertMesh(inputMesh);
    PackedVaryingsType packedVaryingsType = PackVaryingsType(varyingsType);
    UNITY_TRANSFER_INSTANCE_ID(inputMesh, packedVaryingsType);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(packedVaryingsType);
    return packedVaryingsType;
}

#ifdef TESSELLATION_ON

PackedVaryingsToPS VertTesselation(VaryingsToDS input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    VaryingsToPS varyingsType;
    varyingsType.vmesh = VertMeshTesselation(input.vmesh);
    PackedVaryingsToPS packedVaryingsType = PackVaryingsToPS(varyingsType);
    UNITY_TRANSFER_INSTANCE_ID(inputMesh, packedVaryingsType);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(packedVaryingsType);
    return packedVaryingsType;
}

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/TessellationShare.hlsl"

#endif // TESSELLATION_ON

float4 Frag(PackedVaryingsToPS packedInput) : SV_Target
{
    FragInputs input = UnpackVaryingsMeshToFragInputs(packedInput.vmesh);

    // input.positionSS is SV_Position
    PositionInputs posInput = GetPositionInput(input.positionSS.xy, _ScreenSize.zw, input.positionSS.z, input.positionSS.w, input.positionRWS);

#ifdef VARYINGS_NEED_POSITION_WS
    float3 V = GetWorldSpaceNormalizeViewDir(input.positionRWS);
#else
    // Unused
    float3 V = float3(1.0, 1.0, 1.0); // Avoid the division by 0
#endif

    // Perform alpha testing + get distortion
    SurfaceData surfaceData;
    BuiltinData builtinData;
    GetSurfaceAndBuiltinData(input, V, posInput, surfaceData, builtinData);

    float4 outBuffer;
    // Mark this pixel as eligible as source for distortion
    EncodeDistortion(builtinData.distortion, builtinData.distortionBlur, true, outBuffer);
    return outBuffer;
}
