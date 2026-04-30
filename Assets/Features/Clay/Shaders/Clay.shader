Shader "Custom/Clay"
{
    Properties
    {
        _sdf_tex("SDF Texture", 3D) = "" { }
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE3D(_sdf_tex);
            SAMPLER(sampler_sdf_tex);

            CBUFFER_START(UnityPerMaterial)
                float3 bounds_min;
                float3 bounds_max;
                int resolution;
            CBUFFER_END

            struct attributes
            {
                float4 position_os : POSITION;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float3 world_pos : TEXCOORD0;
            };

            varyings vert(attributes IN)
            {
                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(IN.position_os.xyz);
                OUT.world_pos = TransformObjectToWorld(IN.position_os.xyz);
                return OUT;
            }

            // SDF をテクスチャからサンプル
            float sample_sdf(float3 world_pos)
            {
                float3 uvw = (world_pos - bounds_min) / (bounds_max - bounds_min);
                // ボックス外はSDFを大きな正値に
                if (any(uvw < 0) || any(uvw > 1)) return 1e9;
                return SAMPLE_TEXTURE3D_LOD(_sdf_tex, sampler_sdf_tex, uvw, 0).r;
            }

            // 法線：有限差分でSDF勾配を計算
            float3 calc_normal(float3 p)
            {
                float e = 0.005;
                return normalize(float3(
                    sample_sdf(p + float3(e, 0, 0)) - sample_sdf(p - float3(e, 0, 0)),
                    sample_sdf(p + float3(0, e, 0)) - sample_sdf(p - float3(0, e, 0)),
                    sample_sdf(p + float3(0, 0, e)) - sample_sdf(p - float3(0, 0, e))
                ));
            }

            bool intersect_aabb(float3 ro, float3 rd, float3 bmin, float3 bmax,
                                out float t_near, out float t_far)
            {
                float3 inv_rd = 1.0 / rd;
                float3 t0 = (bmin - ro) * inv_rd;
                float3 t1 = (bmax - ro) * inv_rd;
                float3 t_min = min(t0, t1);
                float3 t_max = max(t0, t1);
                t_near = max(max(t_min.x, t_min.y), t_min.z);
                t_far = min(min(t_max.x, t_max.y), t_max.z);
                return t_near <= t_far && t_far > 0;
            }

            float4 frag(varyings IN) : SV_Target
            {
                // レイの設定
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(IN.world_pos - ro);

                // ボックスとの交差判定
                float t_near, t_far;
                if (!intersect_aabb(ro, rd, bounds_min, bounds_max, t_near, t_far))
                    discard;

                // ボックス内部からスタート（カメラがボックス内なら0から）
                float t = max(t_near, 0.0);
                float t_max = t_far;
                float3 bounds_size = bounds_max - bounds_min;
                float voxel_size = min(min(bounds_size.x, bounds_size.y), bounds_size.z) / resolution;
                const int max_steps = 128;
                float step_size = voxel_size * 0.5; // ボクセルの半分ずつ進む

                // Sphere Marching
                for (int i = 0; i < max_steps; i++)
                {
                    float surf_dist = max(0.002, step_size * 0.5);
                    float3 p = ro + rd * t;
                    float d = sample_sdf(p);

                    if (d < surf_dist)
                    {
                        // ヒット → ランバート拡散光
                        float3 normal = calc_normal(p);
                        Light light = GetMainLight();
                        float diffuse = saturate(dot(normal, light.direction));
                        float3 col = (0.3 + 0.7 * diffuse) * float3(0.8, 0.6, 0.4); // 粘土色
                        return float4(col, 1);
                    }

                    t += max(d, step_size);

                    // ボックスを抜けたら終了
                    if (t > t_max) break;
                }

                // ミスした場合は discard
                discard;
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }

    }
}