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

            float4 clay_color;
        CBUFFER_END

        StructuredBuffer<float3> particle_pos;
        Texture2D clay_main_tex, clay_normal_tex;
        ENDHLSL

        Pass
        {
            Name "Particle Depth"

            Cull Off
            ZTest LEqual
            ZWrite On

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
                float depth = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord).r;
                if (depth >= 1.0 || depth <= 0.0) return depth;

                int filter_size = min(max_filter_size, ceil(projected_particle_constant / depth));

                float sigma = filter_size / 3.0;
                float two_sigma = 2.0 * sigma * sigma;
                float sigma_depth = depth_threshold / 3.0;
                float two_sigma_depth = 2.0 * sigma_depth * sigma_depth;

                float sum = 0.0;
                float wsum = 0.0;

                for (int x = -filter_size; x <= filter_size; ++x)
                {
                    float sampled_depth = SAMPLE_TEXTURE2D_LOD(
                        _BlitTexture,
                        sampler_LinearClamp,
                        input.texcoord + x * blur_dir.xy * _BlitTexture_TexelSize.xy,
                        0
                    ).r;
                    sampled_depth = abs(sampled_depth);

                    float rr = 2.0 * x * x;
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

                float4 eye_pos = mul(UNITY_MATRIX_I_P, ndc);
                return eye_pos.xyz / eye_pos.w;
            }

            float3 get_view_pos_from_texcoord(float2 uv)
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).r;
                return compute_view_pos_from_depth(uv, depth);
            }

            float2 dir_to_oct_uv(float3 dir)
            {
                dir /= (abs(dir.x) + abs(dir.y) + abs(dir.z));
                if (dir.y < 0.0)
                {
                    float2 s = float2(dir.x >= 0 ? 1.0 : -1.0,
                                      dir.z >= 0 ? 1.0 : -1.0);
                    dir.xz = (1.0 - abs(dir.zx)) * s;
                }
                return dir.xz * 0.5 + 0.5;
            }

            float2 dir_to_equirect_uv(float3 dir)
            {
                float2 uv;
                uv.x = atan2(dir.z, dir.x) / (2.0 * PI) + 0.5;
                uv.y = asin(clamp(dir.y, -1.0, 1.1)) / PI + 0.5;
                return uv;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_PointClamp, IN.texcoord, 0).r;
                #if defined(UNITY_REVERSED_Z)
                if (depth <= 0.0) discard;
                #else
                if (depth >= 1.0) discard;
                #endif

                float3 pos_vs = ComputeViewSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_P);

                float3 n = cross(ddx(pos_vs), ddy(pos_vs));
                n = normalize(mul(transpose(UNITY_MATRIX_I_V), float4(n, 0)).rgb);
                float2 oct_uv = dir_to_oct_uv(n);
                float3 albedo = clay_color;
                float3 normal_detail = SAMPLE_TEXTURE2D(
                    clay_normal_tex, sampler_LinearRepeat, oct_uv * 2
                ).rgb * 2.0 - 1.0;
                normal_detail = mul((float3x3)UNITY_MATRIX_V, normal_detail);

                // n = normalize(n + normal_detail * .2); // 法線にディテールを加算

                // ライト・視線方向をビュー空間に変換
                float3 l = normalize(_MainLightPosition.xyz);
                float3 v = float3(0, 0, 1); // ビュー空間では視線はZ+
                float3 h = normalize(l + v);

                float n_dot_l = dot(n, l);
                float3 diffuse = (n_dot_l * 0.5 + 0.5) * _MainLightColor.rgb;
                float3 specular = pow(max(0.0, dot(n, h)), 80.0) * _MainLightColor.rgb * .1;
                return float4(albedo * diffuse + specular, 1.0);

                // // Oren-Nayar Diffuse
                // float diffuse = oren_nayar(n, l, v, 0.9);
                // float3 clay_tex_col = SAMPLE_TEXTURE2D(clay_main_tex, sampler_LinearClamp, IN.texcoord).rgb;
                // float3 color = clay_tex_col * light_color.rgb * diffuse;
                //
                // // SSS近似（法線とライトが逆向きの部分を少し明るく）
                // float sss = exp(-max(0.0, dot(n, l)) * sss_strength) * 0.3;
                // color += clay_color.rgb * light_color.rgb * sss;
                //
                // // 微量Specular（しっとり感）
                // float3 h = normalize(l + v);
                // float spec = pow(saturate(dot(n, h)), 4.0) * 0.05;
                // color += light_color.rgb * spec;
                //
                // return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}