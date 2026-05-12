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

            float4x4 matrix_v;
            float4x4 matrix_p;
        CBUFFER_END

        StructuredBuffer<float3> particle_pos;

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
                float3 view_center = mul(matrix_v, float4(world_center, 1.0)).xyz;

                // ビュー空間でオフセット（ビルボード展開）
                float2 offset = quad[corner] * radius;
                float3 view_pos = view_center + float3(offset, 0.0);

                varyings OUT;
                OUT.clip_pos = mul(matrix_p, float4(view_pos, 1.0));
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
                float4 clip_pos = mul(matrix_p, float4(sphere_view_pos, 1.0));
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

            Cull Off
            ZWrite On
            ColorMask 0
            ZTest Always

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

            ZWrite Off
            ZTest Always
            Cull Off
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
    }
}