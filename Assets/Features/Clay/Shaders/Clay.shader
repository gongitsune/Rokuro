Shader "Hidden/Clay"
{
    Properties {}
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

            float projected_particle_constant;
            float depth_threshold;
            int max_filter_size;
            float4 blur_dir;

            float4 light_dir;
            float4 light_color;
            float4 clay_color;
            float sss_strength;
        CBUFFER_END

        StructuredBuffer<float3> particle_pos;
        Texture2D _NormalRT;
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
                float3 view_pos: TEXCOORD1;
            };

            static const float2 quad[6] = {
                float2(0.5, 0.5), float2(0.5, -0.5), float2(-0.5, -0.5),
                float2(0.5, 0.5), float2(-0.5, -0.5), float2(-0.5, 0.5)
            };

            varyings vert(attributes IN)
            {
                uint pid = IN.vert_id / 6;
                uint q_id = IN.vert_id % 6;

                float3 corner = float3(quad[q_id] * radius, 0.0);
                float2 uv = quad[q_id] + 0.5;

                float3 p_pos = particle_pos[pid];
                float3 view_pos = mul(UNITY_MATRIX_V, float4(p_pos, 1.0)).xyz;

                float4 out_pos = mul(UNITY_MATRIX_P, float4(view_pos + corner, 1.0));

                varyings OUT;
                OUT.clip_pos = out_pos;
                OUT.local_uv = uv;
                OUT.view_pos = view_pos;
                return OUT;
            }

            float frag(varyings IN) : SV_Depth
            {
                float2 normal_xy = IN.local_uv * 2.0 - 1.0;
                float r2 = dot(normal_xy, normal_xy);
                if (r2 > 1.0) discard;

                float normal_z = sqrt(1.0 - r2);
                float3 normal = float3(normal_xy, normal_z);

                float r = radius * 0.5;
                float4 view_pos = float4(IN.view_pos + normal * r, 1.0);
                float4 clip_pos = mul(UNITY_MATRIX_P, view_pos);
                return clip_pos.z / clip_pos.w;
            }
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode"="DepthNormal"
            }

            Name "Bilateral"

            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            float frag(Varyings input) : SV_Depth
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0).r;
                if (depth >= 1.0) return 1.0;

                int filter_size = min(max_filter_size, ceil(projected_particle_constant / depth));

                float sigma = filter_size / 3.0;
                float two_sigma = 2.0 * sigma * sigma;
                float sigma_depth = depth_threshold / 3.0;
                float two_sigma_depth = 2.0 * sigma_depth * sigma_depth;

                float sum = 0.0;
                float wsum = 0.0;

                for (int x = -filter_size; x <= filter_size; ++x)
                {
                    float2 coords = float2(x, x);
                    float sampled_depth = SAMPLE_TEXTURE2D_LOD(
                        _BlitTexture,
                        sampler_PointRepeat,
                        input.texcoord + coords * blur_dir,
                        0
                    ).r;
                    sampled_depth = abs(sampled_depth);

                    float rr = dot(coords, coords);
                    float w = exp(-rr / two_sigma);

                    float r_depth = sampled_depth - depth;
                    float wd = exp(-r_depth * r_depth / two_sigma_depth);
                    sum += sampled_depth * w * wd;
                    wsum += w * wd;
                }

                return sum / wsum;
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

            float3 compute_view_pos_from_depth(float2 uv, float depth)
            {
                float4 ndc = float4(uv * 2.0 - 1.0, depth, 1.0);
                // ndc.z = -UNITY_MATRIX_P._22 + UNITY_MATRIX_P._32 / depth; // Reversed-Z対応

                float4 eye_pos = mul(UNITY_MATRIX_I_P, ndc);
                return eye_pos.xyz / eye_pos.w;
            }

            float3 get_view_pos_from_texcoord(float2 uv)
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                return compute_view_pos_from_depth(uv, depth);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, IN.texcoord, 0).r;
                #if defined(UNITY_REVERSED_Z)
                if (depth <= 0.0) discard;
                #else
                if (depth >= 1.0) discard;
                #endif

                float3 view_pos = compute_view_pos_from_depth(IN.texcoord, depth);

                float3 ddx = get_view_pos_from_texcoord(IN.texcoord + float2(_BlitTexture_TexelSize.x, 0)) - view_pos;
                float3 ddy = get_view_pos_from_texcoord(IN.texcoord + float2(0, _BlitTexture_TexelSize.y)) - view_pos;
                float3 ddx2 = view_pos - get_view_pos_from_texcoord(IN.texcoord + float2(-_BlitTexture_TexelSize.x, 0));
                float3 ddy2 = view_pos - get_view_pos_from_texcoord(IN.texcoord + float2(0, -_BlitTexture_TexelSize.y));

                ddx = abs(ddx.z) < abs(ddx2.z) ? ddx : ddx2;
                ddy = abs(ddy.z) < abs(ddy2.z) ? ddy : ddy2;

                float3 normal = -normalize(cross(ddx, ddy));

                // ライト・視線方向をビュー空間に変換
                float3 l = normalize(mul((float3x3)UNITY_MATRIX_V, light_dir.xyz));
                float3 v = float3(0, 0, 1); // ビュー空間では視線はZ+

                // Oren-Nayar Diffuse
                float diffuse = oren_nayar(normal, l, v, 0.9);
                float3 color = clay_color.rgb * light_color.rgb * diffuse;

                // SSS近似（法線とライトが逆向きの部分を少し明るく）
                float sss = exp(-max(0.0, dot(normal, l)) * sss_strength) * 0.3;
                color += clay_color.rgb * light_color.rgb * sss;

                // 微量Specular（しっとり感）
                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(normal, h)), 4.0) * 0.05;
                color += light_color.rgb * spec;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}