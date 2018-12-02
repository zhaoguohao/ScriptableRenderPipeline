#ifndef UNITY_SHADERGRAPH_CHANNEL
#define UNITY_SHADERGRAPH_CHANNEL

real Unity_Flip(real In, real flipX)
{
    return In * flipX;
}

real2 Unity_Flip(real2 In, real flipX, real flipY)
{
    return In * real2(flipX, flipY);
}

real3 Unity_Flip(real3 In, real flipX, real flipY, real flipZ)
{
    return In * real3(flipX, flipY, flipZ);
}

real4 Unity_Flip(real4 In, real flipX, real flipY, real flipZ, real flipW)
{
    return In * real4(flipX, flipY, flipZ, flipW);
}

#endif
