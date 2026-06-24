#ifndef GTAO_SHADER
#define GTAO_SHADER
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

CBUFFER_START(UnityPerMaterial)
    float gtao_radius_ws; // ワールド空間でのAOサンプリング半径 (m)
    int gtao_slice_count; // スライス数 (方向数) 例: 2〜4
    int gtao_step_count; // 1スライスあたりのレイマーチステップ数 例: 4〜8
    float gtao_intensity; // AO強度 (0〜1で減衰量を調整)
    float gtao_thickness_ws; // 厚みの仮定値 (薄い形状での過剰遮蔽を緩和)
CBUFFER_END

// ----------------------------------------------------------
// interleaved gradient noise によるスライス角オフセット
// ----------------------------------------------------------
float ign_noise(float2 pixel_coord)
{
    return frac(52.9829189 * frac(dot(pixel_coord, float2(0.06711056, 0.00583715))));
}

float2 world_radius_to_screen_uv(float radius_ws, float3 position_ws)
{
    // カメラ空間でのZ距離を取得
    float view_z = -TransformWorldToView(position_ws).z;

    // 透視投影のスケール: 半径をview距離で割って、クリップ空間でのサイズに変換
    // unity_CameraProjection._m11 は縦方向のFOVスケール
    float2 uv_radius = (radius_ws / view_z) * float2(unity_CameraProjection._m00, unity_CameraProjection._m11) * 0.5;

    return uv_radius;
}

// ----------------------------------------------------------
// 1方向の地平線コサイン値を探索
// ----------------------------------------------------------
float search_horizon_cos(float2 uv, float2 dir_ss, float3 position_ws, float3 view_dir_ws,
                         int step_count, float max_radius_ws)
{
    float max_cos_h = -1.0;
    float2 max_uv_radius = world_radius_to_screen_uv(max_radius_ws, position_ws);

    for (int i = 1; i <= step_count; i++)
    {
        float t = (float)i / step_count;
        float2 step_uv_radius = max_uv_radius * (t * t);

        float2 sample_uv = uv + dir_ss * step_uv_radius;
        if (any(sample_uv < 0.0) || any(sample_uv > 1.0))
            continue;

        float depth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sample_uv, 0).r;
        float3 sample_ws = ComputeWorldSpacePosition(sample_uv, depth, UNITY_MATRIX_I_VP);

        float3 horizon_vec = sample_ws - position_ws;
        float horizon_dist = length(horizon_vec);
        if (horizon_dist < 0.0001)
            continue;

        if (horizon_dist > max_radius_ws + gtao_thickness_ws)
            continue;

        float cos_h = dot(horizon_vec, view_dir_ws) / horizon_dist;
        max_cos_h = max(max_cos_h, cos_h);
    }

    return max_cos_h;
}

// ----------------------------------------------------------
// GTAO本体: スライスごとに解析積分を計算して合算
// ----------------------------------------------------------
float compute_gtao(float2 uv, float3 position_ws, float3 normal_ws, float3 view_dir_ws, float2 pixel_coord)
{
    float noise_offset = ign_noise(pixel_coord) * PI / gtao_slice_count;
    float visibility_sum = 0.0;

    for (int slice = 0; slice < gtao_slice_count; slice++)
    {
        float slice_angle = (PI / gtao_slice_count) * slice + noise_offset;
        float2 slice_dir_ss = float2(cos(slice_angle), sin(slice_angle));

        float cos_h1 = search_horizon_cos(uv, slice_dir_ss, position_ws, view_dir_ws, gtao_step_count,
                                          gtao_radius_ws);
        float cos_h2 = search_horizon_cos(uv, -slice_dir_ss, position_ws, view_dir_ws, gtao_step_count,
                                          gtao_radius_ws);

        float h1 = -acos(clamp(cos_h1, -1.0, 1.0));
        float h2 = acos(clamp(cos_h2, -1.0, 1.0));

        // スライス平面の法線(スクリーン空間の方向ベクトルから求める平面の法線)
        float3 slice_plane_normal_ws = float3(-slice_dir_ss.y, 0.0, slice_dir_ss.x);
        float3 projected_normal_ws = normalize(
            normal_ws - slice_plane_normal_ws * dot(normal_ws, slice_plane_normal_ws));

        float n = acos(clamp(dot(projected_normal_ws, view_dir_ws), -1.0, 1.0)) - HALF_PI;

        h1 = n + max(h1 - n, -HALF_PI);
        h2 = n + min(h2 - n, HALF_PI);

        float cos_n = cos(n);
        float sin_n = sin(n);

        float integral =
            0.25 * (
                (h1 - n) * sin_n + cos_n - cos(2.0 * h1 - n) +
                (h2 - n) * sin_n + cos_n - cos(2.0 * h2 - n)
            ) * 0.5;

        visibility_sum += integral;
    }

    float ao_raw = saturate(visibility_sum / gtao_slice_count);
    return saturate(lerp(1.0, ao_raw, gtao_intensity));
}

#endif
