#define FLOAT33_IDENTITY float3x3(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1))

static const float eps = 1e-7;

float3 get_col(in float3x3 m, int c) { return float3(m[0][c], m[1][c], m[2][c]); }

void set_col(inout float3x3 m, int c, float3 v)
{
    m[0][c] = v.x;
    m[1][c] = v.y;
    m[2][c] = v.z;
}

void jacobi_rotate(inout float3x3 a, inout float3x3 v, int p, int q)
{
    float app = a[p][p], aqq = a[q][q], apq = a[p][q];
    if (abs(apq) < 1e-12) return;

    float tau = (aqq - app) / (2.0 * apq);
    float t = sign(tau) / (abs(tau) + sqrt(1.0 + tau * tau));
    float c = rsqrt(1.0 + t * t);
    float s = t * c;

    int k;
    // A = J^T A J
    [unroll] for (k = 0; k < 3; ++k)
    {
        float aik = a[p][k], aqk = a[q][k];
        a[p][k] = c * aik - s * aqk;
        a[q][k] = s * aik + c * aqk;
    }
    [unroll] for (k = 0; k < 3; ++k)
    {
        float akp = a[k][p], akq = a[k][q];
        a[k][p] = c * akp - s * akq;
        a[k][q] = s * akp + c * akq;
    }

    // V = V J
    [unroll] for (k = 0; k < 3; ++k)
    {
        float vkp = v[k][p], vkq = v[k][q];
        v[k][p] = c * vkp - s * vkq;
        v[k][q] = s * vkp + c * vkq;
    }
}

// ReSharper disable once CppInconsistentNaming
void svd_3x3(float3x3 a, out float3x3 u, out float3 s, out float3x3 v)
{
    float3x3 ata = mul(transpose(a), a);
    v = float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);

    // 対称行列の固有分解
    [unroll] for (int i = 0; i < 5; ++i)
    {
        jacobi_rotate(ata, v, 0, 1);
        jacobi_rotate(ata, v, 0, 2);
        jacobi_rotate(ata, v, 1, 2);
    }

    float3 lambda = float3(ata[0][0], ata[1][1], ata[2][2]);
    s = sqrt(max(lambda, eps));

    // U = A V S^{-1}
    float3x3 av = mul(a, v);
    float3 c0 = get_col(av, 0) / s.x;
    float3 c1 = get_col(av, 1) / s.y;
    // float3 c2 = get_col(av, 2) / s.z;

    c0 = normalize(c0);
    c1 = normalize(c1 - c0 * dot(c0, c1));
    float3 c2 = normalize(cross(c0, c1)); // 右手系化
    u = float3x3(c0.x, c1.x, c2.x, c0.y, c1.y, c2.y, c0.z, c1.z, c2.z);

    // det(U)<0 の補正
    if (determinant(u) < 0.0)
    {
        c2 = -c2;
        set_col(u, 2, c2);
        set_col(v, 2, -get_col(v, 2));
    }
}

float3x3 outer_product(float3 a, float3 b)
{
    return float3x3(a.x * b, a.y * b, a.z * b);
}
