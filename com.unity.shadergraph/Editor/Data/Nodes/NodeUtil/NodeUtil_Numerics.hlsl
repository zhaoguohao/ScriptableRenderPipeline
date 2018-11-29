#ifndef UNITY_SHADERGRAPH_NODE_UTIL_NUMERICS
#define UNITY_SHADERGRAPH_NODE_UTIL_NUMERICS

/****************************************************************

    Unity_Random

****************************************************************/

real UnityUtil_Random( real Seed )
{
    return frac( sin( dot( Seed, float2( 12.9898, 78.233 ) ) ) * 43758.5453 );
}

/****************************************************************

    Unity_Random

****************************************************************/

real UnityUtil_Remap( real In, real2 InMinMax, real2 OutMinMax )
{
    return OutMinMax.x + ( OutMinMax.y - OutMinMax.x ) * ( In - InMinMax.x ) / ( InMinMax.y - InMinMax.x );
}

real2 UnityUtil_Remap( real2 In, real2 InMinMax, real2 OutMinMax )
{
    return real2( UnityUtil_Remap( In.x, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.y, InMinMax, OutMinMax ) );
}

real3 UnityUtil_Remap( real3 In, real2 InMinMax, real2 OutMinMax )
{
    return real3( UnityUtil_Remap( In.x, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.y, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.z, InMinMax, OutMinMax ) );
}

real4 UnityUtil_Remap( real4 In, real2 InMinMax, real2 OutMinMax )
{
    return real4( UnityUtil_Remap( In.x, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.y, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.z, InMinMax, OutMinMax ),
                  UnityUtil_Remap( In.w, InMinMax, OutMinMax ) );
}

#endif // UNITY_SHADERGRAPH_NODE_UTIL_NUMERICS