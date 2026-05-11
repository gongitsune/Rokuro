Shader "Hidden/Clay"
{
    Properties
    {
        radius("Radius", Float) = 0.05
        scale("Scale", Float) = 1.0
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
            //            Tags
            //            {
            //                "LightMode"="DepthOnly"
            //            }

            Cull Off
            ZTest LEqual
            ZWrite On
            //            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float radius;
                float scale;
                float4x4 matrix_v;
                float4x4 matrix_p;
            CBUFFER_END

            StructuredBuffer<float3> particle_pos;

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

    }
}