Shader "ColorPyramidPS"
{
    HLSLINCLUDE

        #pragma target 4.5
        #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

        TEXTURE2D_HALF(_Source);
        SamplerState sampler_PointClamp;
        SamplerState sampler_LinearClamp;
        uniform half4 _SrcScaleBias;
        uniform half4 _SrcUvLimits; // {xy: max uv, zw: offset of blur for 1 texel }
        uniform half _SourceMip;

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
            output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID) * _SrcScaleBias.xy + _SrcScaleBias.zw;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            // Gaussian weights for 9 texel kernel from center textel to furthest texel. Keep in sync with ColorPyramid.compute
            const half gaussWeights[] = { 0.27343750, 0.21875000, 0.10937500, 0.03125000, 0.00390625 };

            half2 offset = _SrcUvLimits.zw;
            half2 offset1 = offset * (1.0 + (gaussWeights[2] / (gaussWeights[1] + gaussWeights[2])));
            half2 offset2 = offset * (3.0 + (gaussWeights[4] / (gaussWeights[3] + gaussWeights[4])));

            half2 uv_m2 = input.texcoord.xy - offset2;
            half2 uv_m1 = input.texcoord.xy - offset1;
            half2 uv_p0 = input.texcoord.xy;
            half2 uv_p1 = min(_SrcUvLimits.xy, input.texcoord.xy + offset1);
            half2 uv_p2 = min(_SrcUvLimits.xy, input.texcoord.xy + offset2);

            return
              + SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv_m2, _SourceMip) * (gaussWeights[3] + gaussWeights[4])
              + SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv_m1, _SourceMip) * (gaussWeights[1] + gaussWeights[2])
              + SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv_p0, _SourceMip) *  gaussWeights[0]
              + SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv_p1, _SourceMip) * (gaussWeights[1] + gaussWeights[2])
              + SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv_p2, _SourceMip) * (gaussWeights[3] + gaussWeights[4]);
        }

        TEXTURE2D_FLOAT(_BlitTexture);
        TEXTURE2D_FLOAT(_BlitTextureDepth);
        TEXTURE2D_FLOAT(_BlitTextureDepthLowResMin);
        TEXTURE2D_FLOAT(_BlitTextureDepthLowResMax);

#if 1
        // Fetches 4 depth samples from th efull-resolution depth buffer and keeps the MAX value (i.e. farthest Z)
        #if UNITY_REVERSED_Z
            #define MIN_DEPTH(l, r) max(l, r)
            #define MAX_DEPTH(l, r) min(l, r)
        #else
            #define MIN_DEPTH(l, r) min(l, r)
            #define MAX_DEPTH(l, r) max(l, r)
        #endif

        float FragNearestDepth(Varyings input) : SV_Depth
        {
#if 1
            uint2   pixelCoord = floor( input.positionCS.xy - 0.5 );
                    pixelCoord <<= 2;
//            float   Z = MAX_DEPTH( 1, 1 );
            float   Z = MIN_DEPTH( 0, 0 );
            for ( uint y=0; y < 4; y++ ) {
                for ( uint x=0; x < 4; x++ ) {
//                    Z = MAX_DEPTH( Z, _BlitTextureDepth[pixelCoord].x );
                    Z = MIN_DEPTH( Z, _BlitTextureDepth[pixelCoord].x );
                    pixelCoord.x++;
                }
                pixelCoord.x -= 4;
                pixelCoord.y++;
            }

            return Z;
#elif 1
            float2  UV = input.positionCS.xy * _SrcScaleBias.xy;
            float4  Zs = GATHER_RED_TEXTURE2D( _BlitTextureDepth, sampler_PointClamp, UV );
            float   Z0 = MAX_DEPTH( MAX_DEPTH( Zs.x, Zs.z ), MAX_DEPTH( Zs.y, Zs.w ) );

            UV.x += _SrcScaleBias.x;

                    Zs = GATHER_RED_TEXTURE2D( _BlitTextureDepth, sampler_PointClamp, UV );
            float   Z1 = MAX_DEPTH( MAX_DEPTH( Zs.x, Zs.z ), MAX_DEPTH( Zs.y, Zs.w ) );

            UV.y += _SrcScaleBias.y;

                    Zs = GATHER_RED_TEXTURE2D( _BlitTextureDepth, sampler_PointClamp, UV );
            float   Z2 = MAX_DEPTH( MAX_DEPTH( Zs.x, Zs.z ), MAX_DEPTH( Zs.y, Zs.w ) );

            UV.x -= _SrcScaleBias.x;

                    Zs = GATHER_RED_TEXTURE2D( _BlitTextureDepth, sampler_PointClamp, UV );
            float   Z3 = MAX_DEPTH( MAX_DEPTH( Zs.x, Zs.z ), MAX_DEPTH( Zs.y, Zs.w ) );

            return MAX_DEPTH( MAX_DEPTH( Z0, Z1 ), MAX_DEPTH( Z2, Z3 ) );
#else
            float2  UV = input.positionCS.xy * _SrcScaleBias.xy;
            float4  Zs = GATHER_RED_TEXTURE2D( _BlitTextureDepth, sampler_PointClamp, UV );
            return MAX_DEPTH( MAX_DEPTH( Zs.x, Zs.z ), MAX_DEPTH( Zs.y, Zs.w ) );
#endif
        }
#else
        // Blits source texture into depth map
        float FragNearestDepth(Varyings input) : SV_Depth
        {
            float2 uv = input.texcoord.xy;
            #if UNITY_SINGLE_PASS_STEREO
                uv.x = uv.x / 2.0 + unity_StereoEyeIndex * 0.5;
                uv.y = 1.0 - uv.y; // Always flip Y when rendering stereo since HDRP doesn't support OpenGL
            #endif

            return SAMPLE_TEXTURE2D_LOD(_BlitTextureDepth, sampler_PointClamp, input.texcoord, _BlitMipLevel).x;
        }
#endif

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        // 0: Bilinear tri
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }

        // 1: Depth reduction with MAX operator
        Pass
        {
            ZWrite On ZTest Always Blend Off Cull Off
            ColorMask 0 // Don't write to color target

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragNearestDepth
            ENDHLSL
        }

        // 2: Bilateral Upscaling
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha      // Pre-multiplied alpha
//Blend Off

            HLSLPROGRAM
                #pragma vertex VertQuad
                #pragma fragment FragUpscale4

                uniform float4  _SourceSize;
                uniform float4  _TargetSize;
                uniform float4  _DepthAtlasScaleBias;

                uniform float   sigma_range;
                uniform float   sigma_depth;

                float   LinearDepth( float Zproj )
                {
                    return LinearEyeDepth( clamp( Zproj, 0.00001, 0.99 ), _ZBufferParams );
                }

                float   SampleDepth(float2 UV)
                {
                    return LinearDepth( SAMPLE_TEXTURE2D_LOD(_BlitTextureDepth, sampler_PointClamp, UV, 0).x );
                }

                float   SampleDepthLowResMin(float2 UV)
                {
                    UV = _DepthAtlasScaleBias.xy + _DepthAtlasScaleBias.zw * UV;
                    return LinearDepth( SAMPLE_TEXTURE2D_LOD(_BlitTextureDepthLowResMin, sampler_PointClamp, UV, 0).x );
                }
                float   SampleDepthLowResMaxPoint(float2 UV)
                {
                    return LinearDepth( SAMPLE_TEXTURE2D_LOD(_BlitTextureDepthLowResMax, sampler_PointClamp, UV, 0).x );
                }
                float   SampleDepthLowResMaxLinear(float2 UV)
                {
                    return LinearDepth( SAMPLE_TEXTURE2D_LOD(_BlitTextureDepthLowResMax, sampler_LinearClamp, UV, 0).x );
                }

                float   SampleDepthLowResPixels(uint2 pixelPosition)
                {
                    return LinearDepth( _BlitTextureDepthLowResMax[pixelPosition].x );
                }

                Varyings VertQuad(Attributes input)
                {
                    Varyings output;
                    output.positionCS = GetQuadVertexPosition(input.vertexID);
                    output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
                    output.texcoord = GetQuadTexCoord(input.vertexID);
                    return output;
                }

                float4 FragUpscale(Varyings input) : SV_Target
                {
                    float2  targetUV = input.positionCS.xy * _TargetSize.zw;
                    float2  sourcedUV = _SourceSize.zw;

                    float   centerDepth = SampleDepth( targetUV );
                    float   centerDepthLowResMin = SampleDepthLowResMin( targetUV );
                    float   centerDepthLowResMax = SampleDepthLowResMaxPoint( targetUV );
//                    float   centerDepthLowResMax = SampleDepthLowResPixels( uint2( floor( input.positionCS.xy - 0.5 ) ) >> 2 );

const float MIP_PIXEL_SIZE = 4.0;
                    float   depthSlope = centerDepthLowResMax - centerDepthLowResMin;
                            depthSlope = min( MIP_PIXEL_SIZE * tan( 5.0 * PI / 180.0 ), depthSlope );
                    float   maxDepth = centerDepthLowResMax - depthSlope;

                    float4  sumColor = 0.0;
                    float   sumWeights = 0.0;

//float3  pipo = step( centerDepth, centerDepthLowRes );  // centerDepthLowRes >= centerDepth = centerDepth < centerDepthLowRes
////float3  pipo = 1 - step( centerDepthLowRes, centerDepth );  // centerDepthLowRes > centerDepth = centerDepthLowRes <= centerDepth
////float3  pipo = centerDepthLowRes >= centerDepth ? 1 : 0;
//return float4( pipo, 1 );
////return float4( 100.0 * abs(centerDepth - centerDepthLowRes).xxx, 1 );
//return float4( 1, 0, 0, 1 );
//return float4( 0.1 * centerDepth.xxx, 1 );
//return float4( 0.1 * centerDepthLowRes.xxx, 1 );
////return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, targetUV, 0);

//                    const float sigma_range = 0.5;
//                    const float sigma_depth = 1;
                    const float k_range = -0.5 / Sq( sigma_range );
                    const float k_depth = -0.5 / Sq( sigma_depth );

#if 1
                    float2  sourceUV = targetUV;
//                    float4  color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, targetUV, 0);
                    float4  color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, targetUV, 0);
//color = color.w;
//color = float4( 1, 0, 0, 1 );
//color.xyz *= color.w;

                    float   depth = SampleDepth( targetUV );
//                    float   weight = step( depth, centerDepth );
//                    float   weight = step( depth, centerDepthLowRes );
                    float   weight = 1 - step( maxDepth, depth );
weight = depth < maxDepth ? 0 : 1;
weight = 1;

                    sumColor += weight * color;
                    sumWeights += weight;

//weight = centerDepthLowResMax - depth;
//weight = centerDepthLowResMax - depth > 0.05 ? 1 : 0;
//weight = depth < maxDepth ? 1 : 0;

//weight = maxDeltaDepth;
//weight = maxDepth;
//weight = 0.25 * (centerDepthLowResMax - centerDepthLowResMin);
//weight = depthSlope;
//weight = depth < centerDepthLowResMax ? 1 : 0;
//weight = step( depth, centerDepthLowResMax );

//sumColor = float4( 0.5 * weight.xxx, 1 );
//sumColor = float4( 0.1 * depth.xxx, 1 );
//sumColor = float4( 0.1 * centerDepthLowResMin.xxx, 1 );
//sumColor = float4( 0.1 * centerDepthLowResMax.xxx, 1 );
//sumWeights = 1;
//sumColor.xyz *= sumColor.w;
//sumColor.w = sumWeights;
//sumColor = float4( 0, 0, 0, 0.5 );
//sumWeights *= color.w;
#else
                    float2  sourceUV;
                    sourceUV.y = targetUV.y - 2.0 * sourcedUV.y;
                    for ( uint y=0; y < 5; y++ )
                    {
                        sourceUV.x = targetUV.x - 2.0 * sourcedUV.x;
                        for ( uint x=0; x < 5; x++ )
                        {
                            float2  dUV = sourceUV - targetUV;
                            float2  dPixels = dUV * _SourceSize.xy;
//                            float   sqRange = dot( dUV, dUV );
                            float   sqRange = dot( dPixels, dPixels );

                            float4  color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, sourceUV, 0);
                            float   depth = SampleDepth( sourceUV );

                            float   weightRange = exp( k_range * sqRange );
//                            float   depthWeights = exp( k_depth * Sq( depth - centerDepth ) ); // Normal weight
float   depthWeights = exp( k_depth * Sq( depth - centerDepth ) ); // Weight when comparing to low-resolution depth
//float   depthWeights = 1.0 - exp( k_depth * Sq( max( 0.0, centerDepth - depth ) ) ); // Weight when comparing to low-resolution depth
//float   depthWeights = step( centerDepth - depth, 0.1 );
        depthWeights *= step( depth, centerDepth );
                            float   weight = weightRange * depthWeights;

                            sumColor += weight * color;
                            sumWeights += weight;

                            sourceUV.x += sourcedUV.x;
                        }

                        sourceUV.y += sourcedUV.y;
                    }
#endif

//return float4( 0.1 * centerDepth.xxx, 1 );
//return float4( sumWeights.xxx / 25.0, 1 );
                    return sumWeights > 0.0 ? sumColor / sumWeights : 0.0;

//                    float4  V = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, targetUV, 0);
//                    float3  color = 10.0 * SAMPLE_TEXTURE2D_LOD(_BlitTextureDepth, sampler_LinearClamp, targetUV, 0);
//                    return float4( color, V.w );
                }

                float4 FragUpscale2(Varyings input) : SV_Target
                {
                    float2  targetUV = input.positionCS.xy * _TargetSize.zw;
                    float2  sourcedUV = _SourceSize.zw;

                    float   centerDepth = SampleDepth( targetUV );
                    float   centerDepthLowRes = SampleDepthLowResMaxLinear( targetUV );

                    const float k_range = -0.5 / Sq( sigma_range );
                    const float k_depth = -0.5 / Sq( sigma_depth );

                    float4  sumColor = 0.0;

                    float2  sourceUV;
                    sourceUV.y = targetUV.y - 2.0 * sourcedUV.y;
                    for ( uint y=0; y < 5; y++ )
                    {
                        sourceUV.x = targetUV.x - 2.0 * sourcedUV.x;
                        for ( uint x=0; x < 5; x++ )
                        {
                            float2  dUV = sourceUV - targetUV;
                            float2  dPixels = dUV * _SourceSize.xy;
                            float   sqRange = dot( dPixels, dPixels );

                            float4  color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                            float   depth = SampleDepth( sourceUV );

                            float   weightRange = exp( k_range * sqRange );
                            float   depthWeights = exp( k_depth * Sq( depth - centerDepth ) ); // Normal weight
depthWeights = 1;//exp( k_depth * Sq( max( 0.0, centerDepth - depth ) ) ); // Weight when comparing to low-resolution depth
//depthWeights *= step( depth, centerDepth );
//weightRange = 1;
//depthWeights = 1;
                            float   weight = color.w * weightRange * depthWeights;

                            sumColor += weight * float4( color.xyz, 1 );

                            sourceUV.x += sourcedUV.x;
                        }

                        sourceUV.y += sourcedUV.y;
                    }

                    return sumColor.w > 0.0 ? sumColor / sumColor.w : 0.0;
                }

                float   DepthWeight( float fullZ, float lowZ )
                {
//                    return exp( -sigma_depth * Sq( fullZ - lowZ ) );   // Works both ways
//                    return exp( -sigma_depth * Sq( max( 0.0, lowZ - fullZ ) ) );  // Blocks what's behind full-res depth (preferred)
                    return step( max( 0.0, lowZ - fullZ ), 10.0 );
                }

                float4 FragUpscale3(Varyings input) : SV_Target
                {
                    float2  targetPixelPosition = input.positionCS.xy;          // With 0.5 pixel offset
                    float2  sourcePixelPosition = 0.25 * targetPixelPosition;   // Quarter resolution
//                            sourcePixelPosition -= 0.5;                         // Half-pixel offset for bilinear interpolation
                            sourcePixelPosition += 0.5;                         // Half-pixel offset for bilinear interpolation

                    float2  sourcePixelIndex = floor( sourcePixelPosition );
                    float2  uv = sourcePixelPosition - sourcePixelIndex;
                    float2  sourceUV = sourcePixelIndex * _SourceSize.zw;
//return float4( uv, 0, 1 );

                    // Sample the 4 corner values for our bilinear interpolation
                    float4  C00 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                    float   Z00 = SampleDepthLowResMaxPoint( sourceUV );
                            sourceUV.x += _SourceSize.z;
                    float4  C10 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                    float   Z10 = SampleDepthLowResMaxPoint( sourceUV );
                            sourceUV.y += _SourceSize.w;
                    float4  C11 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                    float   Z11 = SampleDepthLowResMaxPoint( sourceUV );
                            sourceUV.x -= _SourceSize.z;
                    float4  C01 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
                    float   Z01 = SampleDepthLowResMaxPoint( sourceUV );
                            sourceUV.y -= _SourceSize.w;


                    // Sample center value
                    float   Z = SampleDepth( targetPixelPosition * _TargetSize.zw );


float4  depthWeights = float4(  DepthWeight( Z, Z00 ),
                                DepthWeight( Z, Z01 ),
                                DepthWeight( Z, Z11 ),
                                DepthWeight( Z, Z10 )
                            );
float   sumWeights = dot( depthWeights, 0.25 );
//C00.w *= depthWeights.x;
//C01.w *= depthWeights.y;
//C11.w *= depthWeights.z;
//C10.w *= depthWeights.w;
                    


                    #if 1
                        // Pre-multiply by alpha for correct blending
                        C00.xyz *= C00.w;
                        C01.xyz *= C01.w;
                        C11.xyz *= C11.w;
                        C10.xyz *= C10.w;
                    #endif

//// Replace invalid colors with default color
//float4  defaultColor = float4( 1, 0, 0, 1 );//C00;
//C01 = lerp( defaultColor, C01, depthWeights.y );
//C11 = lerp( defaultColor, C11, depthWeights.z );
//C10 = lerp( defaultColor, C10, depthWeights.w );

//                    #if 0
//                        // Pre-multiply by depth weight
//                        C00.xyz *= depthWeights.x;
//                        C01.xyz *= depthWeights.y;
//                        C11.xyz *= depthWeights.z;
//                        C10.xyz *= depthWeights.w;
//                    #endif

                    // Perform custom bilinear interpolation
//                    float4  C = 0.25 * (C00 + C01 + C10 + C11);
                    float4  C = 0;
                    C = lerp( lerp( C00, C10, uv.x ), lerp( C01, C11, uv.x ), uv.y );
//                    C = float4( uv, 0, 1 );

//                    return sumWeights > 0.0 ? float4( C.xyz / sumWeights, C.w ) : C;
                    return C;
                    return C.w > 0.0 ? float4( C.xyz / C.w, 0 ) : 0.0;
                }

                // Version with edge cases
                float4 FragUpscale4(Varyings input) : SV_Target
                {
                    float2  targetPixelPosition = input.positionCS.xy;          // With 0.5 pixel offset
                    float2  sourcePixelPosition = 0.25 * targetPixelPosition;   // Quarter resolution
                            sourcePixelPosition -= 0.5;                         // Half-pixel offset for bilinear interpolation
//                            sourcePixelPosition += 0.5;                         // Half-pixel offset for bilinear interpolation

                    float2  sourcePixelIndex = floor( sourcePixelPosition );
                    float2  uv = sourcePixelPosition - sourcePixelIndex;
                    float2  sourceUV = sourcePixelIndex * _SourceSize.zw;
//return float4( uv, 0, 1 );
//return float4( sourceUV, 0, 1 );

                    // Sample the 9 values for our bilinear interpolation
//sourceUV += 0.5 * _SourceSize.zw;
//                    float4  C00 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
//                    float   Z00 = SampleDepthLowResMaxPoint( sourceUV );
                    float4  C00 = _BlitTexture[sourcePixelPosition];
                    float   Z00 = LinearDepth( _BlitTextureDepthLowResMax[sourcePixelPosition].x );
                            sourceUV.x += _SourceSize.z;
                            sourcePixelPosition.x++;
//                    float4  C10 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
//                    float   Z10 = SampleDepthLowResMaxPoint( sourceUV );
                    float4  C10 = _BlitTexture[sourcePixelPosition];
                    float   Z10 = LinearDepth( _BlitTextureDepthLowResMax[sourcePixelPosition].x );
                            sourceUV.y += _SourceSize.w;
                            sourcePixelPosition.y++;
//                    float4  C11 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
//                    float   Z11 = SampleDepthLowResMaxPoint( sourceUV );
                    float4  C11 = _BlitTexture[sourcePixelPosition];
                    float   Z11 = LinearDepth( _BlitTextureDepthLowResMax[sourcePixelPosition].x );
                            sourceUV.x -= _SourceSize.z;
                            sourcePixelPosition.x--;
//                    float4  C01 = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sourceUV, 0);
//                    float   Z01 = SampleDepthLowResMaxPoint( sourceUV );
                    float4  C01 = _BlitTexture[sourcePixelPosition];
                    float   Z01 = LinearDepth( _BlitTextureDepthLowResMax[sourcePixelPosition].x );

                            sourceUV.y -= _SourceSize.w;
                            sourcePixelPosition.y--;


                    // Sample depth values
                    float   Zc = SampleDepth( targetPixelPosition * _TargetSize.zw );

                    float4  depthWeights = float4(  DepthWeight( Zc, Z00 ),
                                                    DepthWeight( Zc, Z01 ),
                                                    DepthWeight( Zc, Z11 ),
                                                    DepthWeight( Zc, Z10 )
                                                );

                    #if 1
                        // Pre-multiply by alpha for correct blending
                        C00.xyz *= C00.w;
                        C01.xyz *= C01.w;
                        C11.xyz *= C11.w;
                        C10.xyz *= C10.w;
                    #endif

#if 1
                    float4  sC00 = C00;
                    float4  sC01 = C01;
                    float4  sC11 = C11;
                    float4  sC10 = C10;
                    float   sZ00 = C00.w * Z00;
                    float   sZ01 = C01.w * Z01;
                    float   sZ11 = C11.w * Z11;
                    float   sZ10 = C10.w * Z10;

                    // Blend colors / depths as either fully themselves, or the sum of their valid neighbors
                    const float DIAGONAL_WEIGHT = 0;

                    C00 = lerp( sC01 + sC10 + DIAGONAL_WEIGHT * sC11, sC00, sC00.w );
                    C01 = lerp( sC00 + DIAGONAL_WEIGHT * sC10 + sC11, sC01, sC01.w );
                    C11 = lerp( DIAGONAL_WEIGHT * sC00 + sC01 + sC10, sC11, sC11.w );
                    C10 = lerp( sC00 + DIAGONAL_WEIGHT * sC01 + sC11, sC10, sC10.w );
                    Z00 = lerp( sZ01 + sZ10 + DIAGONAL_WEIGHT * sZ11, sZ00, sC00.w );
                    Z01 = lerp( sZ00 + DIAGONAL_WEIGHT * sZ10 + sZ11, sZ01, sC01.w );
                    Z11 = lerp( DIAGONAL_WEIGHT * sZ00 + sZ01 + sZ10, sZ11, sC11.w );
                    Z10 = lerp( sZ00 + DIAGONAL_WEIGHT * sZ01 + sZ11, sZ10, sC10.w );

                    // Normalize by the amount of summed pixels
                    float   f = C00.w > 0.0 ? 1.0 / C00.w : 1.0;
                    Z00 *= f;
                    C00 *= f;

                    f = C01.w > 0.0 ? 1.0 / C01.w : 1.0;
                    Z01 *= f;
                    C01 *= f;

                    f = C11.w > 0.0 ? 1.0 / C11.w : 1.0;
                    Z11 *= f;
                    C11 *= f;

                    f = C10.w > 0.0 ? 1.0 / C10.w : 1.0;
                    Z10 *= f;
                    C10 *= f;

#else
                    // Handle cases
                    const float ALPHA_TEST = 0.001;
                    uint    present = (C00.w > ALPHA_TEST ? 1 : 0)
                                    | (C10.w > ALPHA_TEST ? 2 : 0)
                                    | (C11.w > ALPHA_TEST ? 4 : 0)
                                    | (C01.w > ALPHA_TEST ? 8 : 0);

#define ALL(a) (C00 = C01 = C10 = C11 = a)

//return float4( present.xxx / 16.0, 1 );

                    switch ( present )
                    {
                        case 0: // No color
                            break;
                        case 1: // Top-left only
                            C10 = C11 = C01 = C00;
                                Z10 = Z11 = Z01 = Z00;
                            break;
                        case 2: // Top-right only
                            C00 = C11 = C01 = C10;
                                Z00 = Z11 = Z01 = Z10;
                            break;
                        case 3: // Top only
                            C01 = C00;
                            C11 = C10;
                                Z01 = Z00;
                                Z11 = Z10;
                            break;
                        case 4: // Bottom-right only
                            C00 = C10 = C01 = C11;
                                Z00 = Z10 = Z01 = Z11;
//ALL(float4(1,0,1,1));
                            break;
                        case 5: // Top-left and Bottom-right only
                            C01 = C10 = 0.5 * (C00 + C11);
                                Z01 = Z10 = 0.5 * (Z00 + Z11);
                            break;
                        case 6: // Right only
                            C00 = C10;
                            C01 = C11;
                                Z00 = Z10;
                                Z01 = Z11;
                            break;
                        case 7: // Complement bottom-left
                            C01 = 0.3333 * (C00 + C10 + C11);
                                Z01 = 0.3333 * (Z00 + Z10 + Z11);
                            break;
                        case 8: // Bottom-left only
                            C00 = C10 = C11 = C01;
                                Z00 = Z10 = Z11 = Z01;
//ALL(float4(1,0,1,1));
                            break;
                        case 9: // Left only
                            C10 = C00;
                            C11 = C01;
                                Z10 = Z00;
                                Z11 = Z01;
                            break;
                        case 10: // Top-right and Bottom-left only
                            C00 = C11 = 0.5 * (C10 + C01);
                                Z00 = Z11 = 0.5 * (Z10 + Z01);
                            break;
                        case 11: // Complement bottom-right
                            C11 = 0.3333 * (C00 + C10 + C01);
                                Z11 = 0.3333 * (Z00 + Z10 + Z01);
                            break;
                        case 12: // Bottom only
                            C00 = C01;
                            C10 = C11;
                                Z00 = Z01;
                                Z10 = Z11;
//ALL(float4(1,0,1,1));
                            break;
                        case 13: // Complement top-right
                            C10 = 0.3333 * (C00 + C11 + C01);
                                Z10 = 0.3333 * (Z00 + Z11 + Z01);
                            break;
                        case 14: // Complement top-left
                            C00 = 0.3333 * (C10 + C11 + C01);
                                Z00 = 0.3333 * (Z10 + Z11 + Z01);
                            break;
                        case 15:    // All colors
//ALL(float4(1,0,1,1));
//return float4( 0, 1, 1, 1 );
                            break;
                    }
#endif


                    // Perform custom bilinear interpolation
//                    float4  C = 0.25 * (C00 + C01 + C10 + C11);
                    float4  C = lerp( lerp( C00, C10, uv.x ), lerp( C01, C11, uv.x ), uv.y );
                    float   Z = lerp( lerp( Z00, Z10, uv.x ), lerp( Z01, Z11, uv.x ), uv.y );

//return float4( DepthWeight( Zc, Z ).xxx, 1 );
//return float4( 0.1 * Z.xxx, 1 );
//return float4( 0.1 * Zc.xxx, 1 );
//return float4( 1.0 * (Zc - Z).xxx, 1 );

                    C *= DepthWeight( Zc, Z );
//                    C *= step( Z, Zc );

//                    return sumWeights > 0.0 ? float4( C.xyz / sumWeights, C.w ) : C;
                    return C;
                    return C.w > 0.0 ? float4( C.xyz / C.w, 0 ) : 0.0;
               }
            ENDHLSL
        }
    }

    Fallback Off
}
