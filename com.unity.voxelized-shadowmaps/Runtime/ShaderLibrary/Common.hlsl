#ifndef UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
#define UNITY_VX_SHADOWMAPS_COMMON_INCLUDED


StructuredBuffer<uint> _VxShadowMapsBuffer;


uint emulateCLZ(uint x)
{
    // emulate it similar to count leading zero.
    // count leading 1bit.

    uint n = 32;
    uint y;

    y = x >> 16; if (y != 0) { n = n - 16; x = y; }
    y = x >>  8; if (y != 0) { n = n -  8; x = y; }
    y = x >>  4; if (y != 0) { n = n -  4; x = y; }
    y = x >>  2; if (y != 0) { n = n -  2; x = y; }
    y = x >>  1; if (y != 0) return n - 2;

    return n - x;
}


// todo : calculate uint2 and more?
uint CalculateRescale(uint srcPosbit, uint dstPosbit)
{
    return 32 - emulateCLZ(srcPosbit ^ dstPosbit);
}


uint4 TraverseVxShadowMapPosQ(uint maxScale, uint3 posQ)
{
    uint nodeIndex = 0;
    uint scale = maxScale;

    bool lit = false;
    bool shadowed = false;
    bool intersected = true;

    for (; scale > 3 && intersected; --scale)
    {
        // calculate where to go to child
        uint3 childDet = (posQ >> (scale - 1)) & 0x00000001;
        uint cellShift = (childDet.x << 1) + (childDet.y << 2) + (childDet.z << 3);
        uint cellbit   = 0x00000003 << cellShift;

        // calculate bit
        uint header = _VxShadowMapsBuffer[nodeIndex];
        uint childmask = header >> 16;
        uint shadowbit = (childmask & cellbit) >> cellShift;

        // determine whether it is lit or shadowed.
        lit      = shadowbit & 0x00000001;
        shadowed = shadowbit & 0x00000002;

        // if it has lit and shadowed, it is not decided yet(need to traverse more)
        intersected = lit && shadowed;

        // find next child node
        uint mask = ~(0xFFFFFFFF << cellShift);
        uint childrenbit = childmask & ((childmask & 0x0000AAAA) >> 1);
        uint childIndex = countbits(childrenbit & mask);

        // go down to the next node
        nodeIndex = _VxShadowMapsBuffer[nodeIndex + 1 + childIndex];
    }

    return uint4(nodeIndex, lit, shadowed, intersected);
}


uint2 TraverseVxShadowMapLeaf(uint posQ_z, uint4 innerResult)
{
    uint nodeIndex   = innerResult.x;

    bool lit         = innerResult.y;
    bool intersected = innerResult.w;

    uint bitmask0 = lit ? 0x00000000 : 0xFFFFFFFF;
    uint bitmask1 = lit ? 0x00000000 : 0xFFFFFFFF;

    if (intersected)
    {
        int childIndex = posQ_z % 8;
        int leafIndex = _VxShadowMapsBuffer[nodeIndex + childIndex];

        bitmask0 = _VxShadowMapsBuffer[leafIndex];
        bitmask1 = _VxShadowMapsBuffer[leafIndex + 1];
    }

    return uint2(bitmask0, bitmask1);
}


float PointSampleShadowBitmask(uint2 bitmask2, uint3 posQ)
{
    uint2 posLeaf = posQ.xy % uint2(8, 8);
    uint bitmask = posLeaf.y < 4 ? bitmask2.x : bitmask2.y;

    uint shift = posLeaf.x + 8 * (posLeaf.y % 4);
    uint mask = 0x00000001 << shift;

    float litRate = (bitmask & mask) == 0 ? 1.0 : 0.0;

    return litRate;
}


float PointSampleShadowBitmask(uint2 bitmask2, uint3 posQ, uint2 offset)
{
    uint2 posLeaf = (posQ.xy + offset) % uint2(8, 8);
    uint bitmask = posLeaf.y < 4 ? bitmask2.x : bitmask2.y;

    uint shift = posLeaf.x + 8 * (posLeaf.y % 4);
    uint mask = 0x00000001 << shift;

    float litRate = (bitmask & mask) == 0 ? 1.0 : 0.0;

    return litRate;
}


#endif // UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
