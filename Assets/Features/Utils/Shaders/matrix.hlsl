#ifndef SVD3X3_INCLUDED
#define SVD3X3_INCLUDED

#define FLOAT33_IDENTITY float3x3(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1))

static const float svd_eps = 1e-6;


// ============================================================
// Utility
// ============================================================
float3x3 outer_product(float3 a, float3 b)
{
    return float3x3(a.x * b, a.y * b, a.z * b);
}

float3 get_column(float3x3 m, int i)
{
    return float3(m[0][i], m[1][i], m[2][i]);
}

void set_column(inout float3x3 m, int i, float3 v)
{
    m[0][i] = v.x;
    m[1][i] = v.y;
    m[2][i] = v.z;
}

void jacobi_rotate(
    inout float3x3 A,
    inout float3x3 V,
    int p,
    int q)
{
    float apq = A[p][q];

    if (abs(apq) < 1e-7)
        return;

    float app = A[p][p];
    float aqq = A[q][q];

    float tau = (aqq - app) / (2.0 * apq);

    float t =
        sign(tau) /
        (abs(tau) + sqrt(1.0 + tau * tau));

    float c = rsqrt(1.0 + t * t);
    float s = t * c;

    // Rotate A
    int k;
    [unroll] for (k = 0; k < 3; ++k)
    {
        float aik = A[p][k];
        float aqk = A[q][k];

        A[p][k] = c * aik - s * aqk;
        A[q][k] = s * aik + c * aqk;
    }

    [unroll] for (k = 0; k < 3; ++k)
    {
        float akp = A[k][p];
        float akq = A[k][q];

        A[k][p] = c * akp - s * akq;
        A[k][q] = s * akp + c * akq;
    }

    // Rotate V
    [unroll] for (k = 0; k < 3; ++k)
    {
        float vip = V[k][p];
        float viq = V[k][q];

        V[k][p] = c * vip - s * viq;
        V[k][q] = s * vip + c * viq;
    }
}

void eigen_symmetric(
    float3x3 a,
    out float3 eigen_values,
    out float3x3 eigen_vectors)
{
    eigen_vectors = FLOAT33_IDENTITY;

    // Jacobi iterations
    [unroll]
    for (int i = 0; i < 10; ++i)
    {
        jacobi_rotate(a, eigen_vectors, 0, 1);
        jacobi_rotate(a, eigen_vectors, 0, 2);
        jacobi_rotate(a, eigen_vectors, 1, 2);
    }

    eigen_values = float3(
        a[0][0],
        a[1][1],
        a[2][2]
    );
}

// ============================================================
// Sort singular values descending
// ============================================================

void swap_f(inout float a, inout float b)
{
    float t = a;
    a = b;
    b = t;
}

void swap_v(inout float3 a, inout float3 b)
{
    float3 t = a;
    a = b;
    b = t;
}

void sort_singular_values(
    inout float3 sigma,
    inout float3x3 v)
{
    float3 c0 = get_column(v, 0);
    float3 c1 = get_column(v, 1);
    float3 c2 = get_column(v, 2);

    if (sigma.x < sigma.y)
    {
        swap_f(sigma.x, sigma.y);
        swap_v(c0, c1);
    }

    if (sigma.x < sigma.z)
    {
        swap_f(sigma.x, sigma.z);
        swap_v(c0, c2);
    }

    if (sigma.y < sigma.z)
    {
        swap_f(sigma.y, sigma.z);
        swap_v(c1, c2);
    }

    set_column(v, 0, c0);
    set_column(v, 1, c1);
    set_column(v, 2, c2);
}

// ============================================================
// Orthonormalize matrix columns
// ============================================================

float3 safe_normalize(float3 v)
{
    return normalize(v + 1e-20);
}

void orthonormalize(inout float3x3 M)
{
    float3 c0 = get_column(M, 0);
    float3 c1 = get_column(M, 1);

    c0 = safe_normalize(c0);

    c1 = c1 - dot(c0, c1) * c0;
    c1 = safe_normalize(c1);

    float3 c2 = cross(c0, c1);

    set_column(M, 0, c0);
    set_column(M, 1, c1);
    set_column(M, 2, c2);
}

// ============================================================
// Main SVD
// ============================================================

void svd_3x3(
    float3x3 f,
    out float3x3 u,
    out float3 sigma,
    out float3x3 v)
{
    // --------------------------------------------------------
    // Compute A = F^T F
    // --------------------------------------------------------

    float3x3 A = mul(transpose(f), f);

    // --------------------------------------------------------
    // Eigen decomposition
    // --------------------------------------------------------

    float3 eigenvalues;

    eigen_symmetric(A, eigenvalues, v);

    // --------------------------------------------------------
    // Singular values
    // --------------------------------------------------------

    sigma = sqrt(max(eigenvalues, 0.0));

    // Sort descending
    sort_singular_values(sigma, v);

    // --------------------------------------------------------
    // Recover U
    // --------------------------------------------------------

    float inv0 = 1.0 / max(sigma.x, svd_eps);
    float inv1 = 1.0 / max(sigma.y, svd_eps);
    float inv2 = 1.0 / max(sigma.z, svd_eps);

    float3 v0 = get_column(v, 0);
    float3 v1 = get_column(v, 1);
    float3 v2 = get_column(v, 2);

    float3 u0 = mul(f, v0) * inv0;
    float3 u1 = mul(f, v1) * inv1;
    float3 u2 = mul(f, v2) * inv2;

    u = float3x3(
        u0.x, u1.x, u2.x,
        u0.y, u1.y, u2.y,
        u0.z, u1.z, u2.z
    );

    orthonormalize(u);

    // --------------------------------------------------------
    // Reflection fix
    // Ensure det(U) > 0
    // --------------------------------------------------------

    if (determinant(u) < 0.0)
    {
        sigma.z *= -1.0;

        float3 c = get_column(u, 2);
        set_column(u, 2, -c);
    }
}

#endif
