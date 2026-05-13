#define UNROLLED_CUBIC_FOR(n0, n1, n2) \
    [unroll] for (int i = 0; i < n0; ++i) \
    [unroll] for (int j = 0; j < n1; ++j) \
    [unroll] for (int k = 0; k < n2; ++k)
#define POW2(x) ((x) * (x))
#define PI 3.14159265358979323846

// ----------------------
// define
// ----------------------
#define VEL_FP_SCALE 1e4
#define VEL_FP_SCALE_INV 1e-4
#define WEIGHT_FP_SCALE 1e6
#define WEIGHT_FP_SCALE_INV 1e-6

// IbukiHash by Andante (https://twitter.com/andanteyk)
// This work is marked with CC0 1.0. To view a copy of this license, visit https://creativecommons.org/publicdomain/zero/1.0/
float rand(float4 v)
{
    const uint4 mult =
        uint4(0xae3cc725, 0x9fe72885, 0xae36bfb5, 0x82c1fcad);

    uint4 u = uint4(v);
    u = u * mult;
    u ^= u.wxyz ^ u >> 13;

    uint r = dot(u, mult);

    r ^= r >> 11;
    r = (r * r) ^ r;

    return r * 2.3283064365386962890625e-10;
}
