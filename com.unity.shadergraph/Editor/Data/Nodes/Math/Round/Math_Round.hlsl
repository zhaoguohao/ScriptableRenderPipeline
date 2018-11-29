#ifndef UNITY_SHADERGRAPH_MATH_ROUND
#define UNITY_SHADERGRAPH_MATH_ROUND

/****************************************************************

    Ceiling

****************************************************************/
real Unity_Ceiling(real In)
{
    return ceil(In);
}

real2 Unity_Ceiling(real2 In)
{
    return ceil(In);
}

real3 Unity_Ceiling(real3 In)
{
    return ceil(In);
}

real4 Unity_Ceiling(real4 In)
{
    return ceil(In);
}

/****************************************************************

    Floor

****************************************************************/
real Unity_Floor(real In)
{
    return floor(In);
}

real2 Unity_Floor(real2 In)
{
    return floor(In);
}

real3 Unity_Floor(real3 In)
{
    return floor(In);
}

real4 Unity_Floor(real4 In)
{
    return floor(In);
}

/****************************************************************

    Round

****************************************************************/
real Unity_Round(real In)
{
    return round(In);
}

real2 Unity_Round(real2 In)
{
    return round(In);
}

real3 Unity_Round(real3 In)
{
    return round(In);
}

real4 Unity_Round(real4 In)
{
    return round(In);
}

/****************************************************************

    Sign

****************************************************************/
real Unity_Sign(real In)
{
    return sign(In);
}

real2 Unity_Sign(real2 In)
{
    return sign(In);
}

real3 Unity_Sign(real3 In)
{
    return sign(In);
}

real4 Unity_Sign(real4 In)
{
    return sign(In);
}

/****************************************************************

    Step

****************************************************************/
real Unity_Step(real a, real b)
{
    return step(a, b);
}

real2 Unity_Step(real2 a, real2 b)
{
    return step(a, b);
}

real3 Unity_Step(real3 a, real3 b)
{
    return step(a, b);
}

real4 Unity_Step(real4 a, real4 b)
{
    return step(a, b);
}

/****************************************************************

    Truncate

****************************************************************/
real Unity_Truncate(real In)
{
    return trunc(In);
}

real2 Unity_Truncate(real2 In)
{
    return trunc(In);
}

real3 Unity_Truncate(real3 In)
{
    return trunc(In);
}

real4 Unity_Truncate(real4 In)
{
    return trunc(In);
}

#endif // UNITY_SHADERGRAPH_MATH_ROUND
