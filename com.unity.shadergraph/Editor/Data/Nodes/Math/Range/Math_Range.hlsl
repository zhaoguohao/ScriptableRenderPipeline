#ifndef UNITY_SHADERGRAPH_MATH_RANGE
#define UNITY_SHADERGRAPH_MATH_RANGE

/****************************************************************
    
    Unity_Clamp

****************************************************************/

real Unity_Clamp(real In, real Min, real Max)
{
    return clamp(In, Min, Max);
}

real2 Unity_Clamp(real2 In, real2 Min, real2 Max)
{
    return clamp(In, Min, Max);
}

real3 Unity_Clamp(real3 In, real3 Min, real3 Max)
{
    return clamp(In, Min, Max);
}

real4 Unity_Clamp(real4 In, real4 Min, real4 Max)
{
    return clamp(In, Min, Max);
}

real2x2 Unity_Clamp(real2x2 In, real2x2 Min, real2x2 Max)
{
    return clamp(In, Min, Max);
}

real3x3 Unity_Clamp(real3x3 In, real3x3 Min, real3x3 Max)
{
    return clamp(In, Min, Max);
}

real4x4 Unity_Clamp(real4x4 In, real4x4 Min, real4x4 Max)
{
    return clamp(In, Min, Max);
}

/****************************************************************
    
    Unity_Fraction

****************************************************************/

real Unity_Fraction(real In)
{
    return frac(In);
}

real2 Unity_Fraction(real2 In)
{
    return frac(In);
}

real3 Unity_Fraction(real3 In)
{
    return frac(In);
}

real4 Unity_Fraction(real4 In)
{
    return frac(In);
}

real2x2 Unity_Fraction(real2x2 In)
{
    return frac(In);
}

real3x3 Unity_Fraction(real3x3 In)
{
    return frac(In);
}

real4x4 Unity_Fraction(real4x4 In)
{
    return frac(In);
}

/****************************************************************
    
    Unity_Maximum

****************************************************************/

real Unity_Maximum(real A, real B)
{
    return max(A, B);
}

real2 Unity_Maximum(real2 A, real2 B)
{
    return max(A, B);
}

real3 Unity_Maximum(real3 A, real3 B)
{
    return max(A, B);
}

real4 Unity_Maximum(real4 A, real4 B)
{
    return max(A, B);
}

real2x2 Unity_Maximum(real2x2 A, real2x2 B)
{
    return max(A, B);
}

real3x3 Unity_Maximum(real3x3 A, real3x3 B)
{
    return max(A, B);
}

real4x4 Unity_Maximum(real4x4 A, real4x4 B)
{
    return max(A, B);
}

/****************************************************************
    
    Unity_Minimum

****************************************************************/

real Unity_Minimum(real A, real B)
{
    return min(A, B);
}

real2 Unity_Minimum(real2 A, real2 B)
{
    return min(A, B);
}

real3 Unity_Minimum(real3 A, real3 B)
{
    return min(A, B);
}

real4 Unity_Minimum(real4 A, real4 B)
{
    return min(A, B);
}

real2x2 Unity_Minimum(real2x2 A, real2x2 B)
{
    return min(A, B);
}

real3x3 Unity_Minimum(real3x3 A, real3x3 B)
{
    return min(A, B);
}

real4x4 Unity_Minimum(real4x4 A, real4x4 B)
{
    return min(A, B);
}

/****************************************************************
    
    Unity_OneMinus

****************************************************************/

real Unity_OneMinus(real In)
{
    return 1 - In;
}

real2 Unity_OneMinus(real2 In)
{
    return 1 - In;
}

real3 Unity_OneMinus(real3 In)
{
    return 1 - In;
}

real4 Unity_OneMinus(real4 In)
{
    return 1 - In;
}

/****************************************************************
    
    Unity_RandomRange

****************************************************************/

real Unity_RandomRange(real A, real B)
{
    return min(A, B);
}

real2 Unity_RandomRange(real2 A, real2 B)
{
    return min(A, B);
}

real3 Unity_RandomRange(real3 A, real3 B)
{
    return min(A, B);
}

real4 Unity_RandomRange(real4 A, real4 B)
{
    return min(A, B);
}

real2x2 Unity_RandomRange(real2x2 A, real2x2 B)
{
    return min(A, B);
}

real3x3 Unity_RandomRange(real3x3 A, real3x3 B)
{
    return min(A, B);
}

real4x4 Unity_RandomRange(real4x4 A, real4x4 B)
{
    return min(A, B);
}

#endif