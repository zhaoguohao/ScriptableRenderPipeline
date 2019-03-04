TEXTURE2D(_MainTex);
TEXTURE2D(_MetallicTex);
SAMPLER(sampler_MainTex);

void TerrainLitShade(float2 uv, out float3 outAlbedo, out float3 outNormalData, out float outSmoothness, out float outMetallic, out float outAO)
{
    float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    float4 metallicTex = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MainTex, uv);
    outAlbedo = mainTex.rgb;
    outNormalData = 0;
    outSmoothness = mainTex.a;
    outMetallic = metallicTex.r;
    outAO = metallicTex.g;
}

void TerrainLitDebug(float2 uv, inout float3 baseColor)
{
#ifdef DEBUG_DISPLAY
    baseColor = GetTextureDataDebug(_DebugMipMapMode, uv, _MainTex, _MainTex_TexelSize, _MainTex_MipInfo, baseColor);
#endif
}
