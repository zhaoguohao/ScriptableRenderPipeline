#ifndef UNITY_SHADERGRAPH_MATH_ADVANCED
#define UNITY_SHADERGRAPH_MATH_ADVANCED

/****************************************************************
    
    Absolute

****************************************************************/
real Unity_Absolute(real In)
{
    return abs(In);
}

real2 Unity_Absolute(real2 In)
{
    return abs(In);
}

real3 Unity_Absolute(real3 In)
{
    return abs(In);
}

real4 Unity_Absolute(real4 In)
{
    return abs(In);
    // return real4(abs(In.r), abs(In.g), abs(In.b), abs(In.a));
    // return real4(1.0, 0.0, 1.0, 0.0);
}

/****************************************************************
    
    Exponential

****************************************************************/
real Unity_Exponential(real In)
{
    return exp(In);
}

real2 Unity_Exponential(real2 In)
{
    return exp(In);
}

real3 Unity_Exponential(real3 In)
{
    return exp(In);
}

real4 Unity_Exponential(real4 In)
{
    return exp(In);
}

real Unity_Exponential2(real In)
{
    return exp(In);
}

real2 Unity_Exponential2(real2 In)
{
    return exp(In);
}

real3 Unity_Exponential2(real3 In)
{
    return exp(In);
}

real4 Unity_Exponential2(real4 In)
{
    return exp(In);
}
#endif // UNITY_SHADERGRAPH_MATH_ADVANCED