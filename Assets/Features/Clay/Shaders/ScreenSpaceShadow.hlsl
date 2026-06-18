#ifndef SCREEN_SPACE_SHADOW
#define SCREEN_SPACE_SHADOW

#include "Assets/Features/Clay/Shaders/Parameters.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float screen_space_shadow(float3 position_ws, float3 light_dir_ws)
{
    float step_size = shadow_step_size;
    int step_count = shadow_step_count;
    float prev_diff = -1.0;

    for (int i = 1; i <= step_count; i++)
    {
        float3 sample_ws = position_ws + light_dir_ws * (step_size * i);

        // ワールド→クリップ→UV
        float4 sample_cs = TransformWorldToHClip(sample_ws);
        float2 sample_uv = (sample_cs.xy / sample_cs.w) * 0.5 + 0.5;
        #if UNITY_UV_STARTS_AT_TOP
        sample_uv.y = 1.0 - sample_uv.y;
        #endif

        if (any(sample_uv < 0) || any(sample_uv > 1))
            continue;

        float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sample_uv, 0).r;

        // sceneDepthをワールド座標に再構成して、サンプル点との前後関係を比較
        float3 pos_ws = ComputeWorldSpacePosition(sample_uv, depth, UNITY_MATRIX_I_VP);
        float sample_dist = length(sample_ws - _WorldSpaceCameraPos);
        float scene_dist = length(pos_ws - _WorldSpaceCameraPos);
        float diff = sample_dist - (scene_dist + shadow_bias);

        if (diff > 0 && prev_diff <= 0 && prev_diff != -1)
        {
            // 前ステップとの間で線形補間して、遮蔽開始の「正確な深さ」を求める
            float t = prev_diff / (prev_diff - diff); // 0〜1
            float hit_step = i - 1 + t;

            // 遮蔽の「深さ」に応じてソフトシャドウの強さを変える
            float penetration = diff; // 遮蔽物にどれだけ食い込んでいるか
            float softness = saturate(penetration / (shadow_bias * 2.0));

            float fade = 1.0 - saturate(hit_step / step_count);
            float shadow_amount = lerp(1.0, shadow_intensity, softness * fade);
            return shadow_amount;
        }
        prev_diff = diff;
    }

    return 1.0; // shadow無し
}

#endif
