Shader "Hidden/Clay"
{
    Properties
    {
        [Header(Particle Depth)]
        radius("Radius", Float) = 0.05

        [Header(Depth Blur)]
        sigma_space ("Sigma Space", Float) = 5.0
        sigma_depth ("Sigma Depth", Float) = 0.01
        kernel_radius ("Kernel Radius", Int) = 7

        [Header(Shading)]
        light_dir("Light Direction", Vector) = (1, 1, -1, 0)
        light_color("Light Color", Color) = (1, 1, 1, 1)
        clay_color("Clay Color", Color) = (0.8, 0.7, 0.6, 1)
        sss_strength("SSS Strength", Float) = 2.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float radius;

            float sigma_space;
            float sigma_depth;
            int kernel_radius;

            float4 light_dir;
            float4 light_color;
            float4 clay_color;
            float sss_strength;

            // float4x4 matrix_v;
            // float4x4 matrix_p;
            // float4x4 matrix_inv_p;
        CBUFFER_END

        StructuredBuffer<float3> particle_pos;
        Texture2D _NormalRT;

        float gaussian_space(float dist, float sigma)
        {
            return exp(-dist * dist / (2.0 * sigma * sigma));
        }

        float gaussian_depth(float diff, float sigma)
        {
            return exp(-diff * diff / (2.0 * sigma * sigma));
        }

        float bilateral_sample(float2 uv, float2 dir, float centerDepth)
        {
            float weight_sum = 0.0;
            float depth_sum = 0.0;

            for (int i = -kernel_radius; i <= kernel_radius; i++)
            {
                float2 offset = dir * i;
                float2 sample_uv = uv + offset * _BlitTexture_TexelSize.xy;

                float sample_depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, sample_uv, 0).r;

                if (sample_depth >= 1.0) continue;

                float w_space = gaussian_space((float)i, sigma_space);
                float w_depth = gaussian_depth(sample_depth - centerDepth, sigma_depth);
                float w = w_space * w_depth;

                depth_sum += sample_depth * w;
                weight_sum += w;
            }

            return weight_sum > 0.0 ? depth_sum / weight_sum : centerDepth;
        }
        ENDHLSL

        Pass
        {
            Tags
            {
                "LightMode"="DepthOnly"
            }

            Name "Particle Depth"

            Cull Off
            ZTest LEqual
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct attributes
            {
                uint vert_id : SV_VertexID;
            };

            struct varyings
            {
                float4 clip_pos : SV_POSITION;
                float2 local_uv : TEXCOORD0;
                float3 view_center: TEXCOORD1;
            };

            static const float2 quad[6] = {
                float2(-1, -1), float2(-1, 1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(1, -1)
            };

            varyings vert(attributes IN)
            {
                uint pid = IN.vert_id / 6;
                uint corner = IN.vert_id % 6;

                float3 world_center = particle_pos[pid];

                // ビュー空間に変換
                float3 view_center = mul(UNITY_MATRIX_V, float4(world_center, 1.0)).xyz;

                // ビュー空間でオフセット（ビルボード展開）
                float2 offset = quad[corner] * radius;
                float3 view_pos = view_center + float3(offset, 0.0);

                varyings OUT;
                OUT.clip_pos = mul(UNITY_MATRIX_P, float4(view_pos, 1.0));
                OUT.local_uv = quad[corner];
                OUT.view_center = view_center;
                return OUT;
            }

            float frag(varyings IN) : SV_Depth
            {
                float r2 = dot(IN.local_uv, IN.local_uv);
                if (r2 > 1.0) discard;

                // 球面上のビュー空間Z（UnityはZ負方向がカメラ前）
                float z_offset = sqrt(1.0 - r2) * radius;
                float3 sphere_view_pos = IN.view_center + float3(0.0, 0.0, -z_offset);

                // クリップ空間に変換してデプスを書き込む
                float4 clip_pos = mul(UNITY_MATRIX_P, float4(sphere_view_pos, 1.0));
                return clip_pos.z / clip_pos.w;
            }
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode"="DepthOnly"
            }

            Name "BilateralH"

            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            float frag(Varyings i) : SV_Depth
            {
                float center_depth = SAMPLE_DEPTH_TEXTURE_LOD(_BlitTexture, sampler_PointClamp, i.texcoord, 0);
                if (center_depth >= 1.0) return 1.0;

                return bilateral_sample(i.texcoord, float2(1, 0), center_depth);
            }
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode"="DepthOnly"
            }

            Name "BilateralV"

            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag_v

            float frag_v(Varyings i) : SV_Depth
            {
                float center_depth = SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, i.texcoord).r;
                if (center_depth >= 1.0) return 1.0;

                return bilateral_sample(i.texcoord, float2(0, 1), center_depth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Reconstruct Normal From Depth"

            ZTest Always

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            // デプスからビュー空間位置を復元
            float3 reconstruct_view_pos(float2 uv, float depth, float4x4 inv_p)
            {
                float4 clip_pos = float4(uv * 2.0 - 1.0, depth, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                clip_pos.y = -clip_pos.y;
                #endif
                float4 view_pos = mul(inv_p, clip_pos);
                return view_pos.xyz / view_pos.w;
            }

            float3 reconstruct_normal(TEXTURE2D(depthTex), SamplerState smp,
                                      float2 uv, float4 texel_size, float4x4 inv_p)
            {
                float2 duv = texel_size.xy;

                float dc = SAMPLE_TEXTURE2D_LOD(depthTex, smp, uv, 0).r;
                float dr = SAMPLE_TEXTURE2D_LOD(depthTex, smp, uv + float2(duv.x, 0), 0).r;
                float du = SAMPLE_TEXTURE2D_LOD(depthTex, smp, uv + float2(0, duv.y), 0).r;

                // パーティクルがない領域はスキップ
                if (dc >= 1.0) return float3(0, 0, 0);

                float3 pos_c = reconstruct_view_pos(uv, dc, inv_p);
                float3 pos_r = reconstruct_view_pos(uv + float2(duv.x, 0), dr, inv_p);
                float3 pos_u = reconstruct_view_pos(uv + float2(0, duv.y), du, inv_p);

                float3 dpdx = pos_r - pos_c;
                float3 dpdy = pos_u - pos_c;

                return normalize(cross(dpdx, dpdy));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normal = reconstruct_normal(
                    _BlitTexture, sampler_PointClamp,
                    input.texcoord,
                    _BlitTexture_TexelSize,
                    UNITY_MATRIX_I_P
                );
                // normal.z *= -1.0; // ビュー空間のZはカメラからの距離なので反転

                if (length(normal) < 0.5) return float4(0, 0, 0, 0);

                return float4(normal, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            // Oren-Nayar近似
            float oren_nayar(float3 n, float3 l, float3 v, float roughness)
            {
                float n_dot_l = saturate(dot(n, l));
                float n_dot_v = saturate(dot(n, v));

                float sigma2 = roughness * roughness;
                float a = 1.0 - 0.5 * sigma2 / (sigma2 + 0.33);
                float b = 0.45 * sigma2 / (sigma2 + 0.09);

                float theta_i = acos(n_dot_l);
                float theta_r = acos(n_dot_v);
                float alpha = max(theta_i, theta_r);
                float beta = min(theta_i, theta_r);

                return n_dot_l * (a + b * max(0.0, cos(theta_i - theta_r)) * sin(alpha) * tan(beta));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // デプスで背景判定
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                // Reversed-Zの場合は条件を逆にする
                #if defined(UNITY_REVERSED_Z)
                if (depth <= 0.0) discard;
                #else
                if (depth >= 1.0) discard;
                #endif

                // 法線復元
                // float2 nxyz = SAMPLE_TEXTURE2D_LOD(_NormalRT, sampler_PointClamp, uv, 0).rgz;
                float3 n = normalize(SAMPLE_TEXTURE2D_LOD(_NormalRT, sampler_PointClamp, uv, 0).rgb);

                // return float4(n, 1.0);

                // ライト・視線方向をビュー空間に変換
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_V, light_dir.xyz));
                float3 v = float3(0, 0, 1); // ビュー空間では視線はZ+

                // Oren-Nayar Diffuse
                float diffuse = oren_nayar(n, l, v, 0.9);
                float3 color = clay_color.rgb * light_color.rgb * diffuse;

                // SSS近似（法線とライトが逆向きの部分を少し明るく）
                float sss = exp(-max(0.0, dot(n, l)) * sss_strength) * 0.3;
                color += clay_color.rgb * light_color.rgb * sss;

                // 微量Specular（しっとり感）
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), 4.0) * 0.05;
                color += light_color.rgb * spec;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}