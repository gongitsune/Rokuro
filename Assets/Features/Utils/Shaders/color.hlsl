float3 hsv2rgb(float h, float s, float v)
{
    return ((clamp(abs(frac(h + float3(0, 2, 1) / 3.) * 6. - 3.) - 1., 0., 1.) - 1.) * s + 1.) * v;
}
