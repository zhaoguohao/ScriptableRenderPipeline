real3 Unity_Checkerboard(
    real2 UV,
    real3 ColorA,
    real3 ColorB,
    real2 Frequency)
{
    UV = (UV.xy + 0.5) * Frequency;
    real4 derivatives = real4(ddx(UV), ddy(UV));
    real2 duv_length = sqrt(real2(dot(derivatives.xz, derivatives.xz), dot(derivatives.yw, derivatives.yw)));
    real width = 1.0;
    real2 distance3 = 4.0 * abs(frac(UV + 0.25) - 0.5) - width;
    real2 scale = 0.35 / duv_length.xy;
    real freqLimiter = sqrt(clamp(1.1f - max(duv_length.x, duv_length.y), 0.0, 1.0));
    real2 vector_alpha = clamp(distance3 * scale.xy, -1.0, 1.0);
    real alpha = saturate(0.5f + 0.5f * vector_alpha.x * vector_alpha.y * freqLimiter);
    return lerp(ColorA, ColorB, alpha.xxx);
}
