#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

// 式(5): ワールドスペースからスクリーンスペースのσに変換
float compute_sigma_screen(float eye_depth)
{
    float h = _BlitTexture_TexelSize.w; // 縦解像度
    float tan_half_fov = tan(UNITY_MATRIX_P[1][1] > 0
                                 ? atan(1.0 / UNITY_MATRIX_P[1][1])
                                 : radians(30.0));
    return h * sigma_world / (2.0 * abs(eye_depth) * tan_half_fov);
}

// 式(2): クランプ関数
float clamp_func(float zi, float zj)
{
    return zj >= zi - delta ? zj : zi - mu;
}

// ガウスウェイト
float gauss_weight(float dist2, float sigma)
{
    return exp(-dist2 / (2.0 * sigma * sigma));
}

float sample_depth(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
}

// Eye-space深度（正値）に変換
float to_eye_depth(float raw_depth)
{
    // LinearEyeDepthは正値でカメラからの距離を返す
    return LinearEyeDepth(raw_depth, _ZBufferParams);
}

// 1Dフィルタ（水平または垂直）
float narrow_range_filter_1d(float2 uv, float2 dir)
{
    float raw_depth_i = sample_depth(uv);

    #if defined(UNITY_REVERSED_Z)
    if (raw_depth_i <= 0.0) return raw_depth_i;
    #else
    if (raw_depth_i >= 1.0) return raw_depth_i;
    #endif

    float zi = -to_eye_depth(raw_depth_i); // 論文規約: 負のeye-space depth
    float sigma = compute_sigma_screen(zi);
    int filter_radius = min(int(ceil(sigma * 3.0)), max_filter_radius);
    sigma = clamp(sigma, 1.0, float(filter_radius));

    // Dynamic Range Adjustment 初期化（式7,8,9）
    float delta_low = delta;
    float delta_high = delta;

    // 中心から近い順にサンプリングしてδを動的に拡張
    for (int r = 1; r <= filter_radius; r++)
    {
        float2 uv_j = uv + dir * (_BlitTexture_TexelSize.xy * r);
        float2 uv_k = uv - dir * (_BlitTexture_TexelSize.xy * r); // bias correction用の対称点

        float raw_j = sample_depth(uv_j);
        float raw_k = sample_depth(uv_k);

        #if defined(UNITY_REVERSED_Z)
        bool bg_j = raw_j <= 0.0;
        bool bg_k = raw_k <= 0.0;
        #else
        bool bg_j = raw_j >= 1.0;
        bool bg_k = raw_k >= 1.0;
        #endif

        float zj = bg_j ? zi - delta * 2.0 : -to_eye_depth(raw_j);
        float zk = bg_k ? zi - delta * 2.0 : -to_eye_depth(raw_k);

        // Dynamic Range Adjustment（式8,9）
        // 範囲内なら閾値を拡張
        if (zj >= zi - delta_low && zj <= zi + delta_high)
            delta_low = max(delta_low, zi - zj + delta);
        if (zk >= zi - delta_low && zk <= zi + delta_high)
            delta_high = max(delta_high, zk - zi + delta);
    }

    // 中心ピクセルの寄与
    float weight_sum = 1.0;
    float depth_sum = zi;

    for (int s = 1; s <= filter_radius; s++)
    {
        float2 uv_j = uv + dir * (_BlitTexture_TexelSize.xy * s);
        float2 uv_k = uv - dir * (_BlitTexture_TexelSize.xy * s);

        float raw_j = sample_depth(uv_j);
        float raw_k = sample_depth(uv_k);

        #if defined(UNITY_REVERSED_Z)
        bool bg_j = raw_j <= 0.0;
        bool bg_k = raw_k <= 0.0;
        #else
        bool bg_j = raw_j >= 1.0;
        bool bg_k = raw_k >= 1.0;
        #endif

        float zj = bg_j ? zi - delta * 2.0 : -to_eye_depth(raw_j);
        float zk = bg_k ? zi - delta * 2.0 : -to_eye_depth(raw_k);

        float dist2 = float(s * s);
        float w = gauss_weight(dist2, sigma);

        // Bias Correction（式6）: j,k どちらかが前景にあれば両方無視
        bool j_out_of_range = zj > zi + delta_high;
        bool k_out_of_range = zk > zi + delta_high;

        // +側（j）
        if (!j_out_of_range && !k_out_of_range)
        {
            depth_sum += w * clamp_func(zi, zj);
            weight_sum += w;
        }

        // -側（k）
        if (!k_out_of_range && !j_out_of_range)
        {
            depth_sum += w * clamp_func(zi, zk);
            weight_sum += w;
        }
    }

    float filtered_z = depth_sum / weight_sum;

    // eye-space深度をraw depthに戻す
    // zi = -LinearEyeDepth だったので逆変換
    float eye_depth = -filtered_z;
    // LinearEyeDepth(d) = 1/(C + D*d) → d = (1/eyeDepth - C) / D
    // _ZBufferParams: x=1-far/near, y=far/near, z=x/far, w=y/far
    float raw_out = (1.0 / eye_depth - _ZBufferParams.w) / _ZBufferParams.z;

    return raw_out;
}

// 2Dクリーンアップフィルタ（小さい固定カーネル）
float narrow_range_filter_2d(float2 uv)
{
    float raw_depth_i = sample_depth(uv);

    #if defined(UNITY_REVERSED_Z)
    if (raw_depth_i <= 0.0) return raw_depth_i;
    #else
    if (raw_depth_i >= 1.0) return raw_depth_i;
    #endif

    float zi = -to_eye_depth(raw_depth_i);

    float weight_sum = 1.0;
    float depth_sum = zi;

    int r = 2; // 5x5
    for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            float sigma = 2.0;
            if (dx == 0 && dy == 0) continue;

            float2 uv_j = uv + float2(dx, dy) * _BlitTexture_TexelSize.xy;
            float raw_j = sample_depth(uv_j);

            #if defined(UNITY_REVERSED_Z)
            if (raw_j <= 0.0) continue;
            #else
            if (raw_j >= 1.0) continue;
            #endif

            float zj = -to_eye_depth(raw_j);

            // 前景は無視（式3）
            if (zj > zi + delta) continue;

            float dist2 = float(dx * dx + dy * dy);
            float w = gauss_weight(dist2, sigma);

            depth_sum += w * clamp_func(zi, zj);
            weight_sum += w;
        }

    float filtered_z = depth_sum / weight_sum;
    float eye_depth = -filtered_z;
    float raw_out = (1.0 / eye_depth - _ZBufferParams.w) / _ZBufferParams.z;
    return raw_out;
}

float narrow_range_filter_frag(Varyings IN) : SV_Depth
{
    if (direction == 0)
        return narrow_range_filter_1d(IN.texcoord, float2(1, 0));
    if (direction == 1)
        return narrow_range_filter_1d(IN.texcoord, float2(0, 1));
    return narrow_range_filter_2d(IN.texcoord);
}
