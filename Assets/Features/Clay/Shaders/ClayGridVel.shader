Shader "Custom/ClayGridVel"
{
    Properties
    {
        scale("Scale", Float) = 10
        vel_scale("Vel Scale", Float) = 1
        size("Size", Float) = 0.1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Features/Utils/Shaders/utils.hlsl"
            #include "Assets/Features/Utils/Shaders/color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float scale;
                float vel_scale;
                float size;
                int n_grid;
            CBUFFER_END

            StructuredBuffer<int> grid_v;

            struct attributes
            {
                uint instance_id : SV_InstanceID;
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 velocity : COLOR;
            };

            float3x3 rotation_from_up_to_dir(float3 dir)
            {
                dir = normalize(dir);
                float3 up = float3(0, 1, 0);

                float cos_a = dot(up, dir);

                // ほぼ同方向 → 単位行列
                if (cos_a > 0.9999)
                    return float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);

                // ほぼ逆方向 → X軸周り180度回転
                if (cos_a < -0.9999)
                    return float3x3(1, 0, 0, 0, -1, 0, 0, 0, -1);

                float3 axis = normalize(cross(up, dir)); // 回転軸
                float sin_a = length(cross(up, dir)); // normalize前でもよい（up,dirが単位ベクトルなら）
                float t = 1.0 - cos_a; // 1 - cos

                // ロドリゲス公式
                return float3x3(
                    t * axis.x * axis.x + cos_a, t * axis.x * axis.y - sin_a * axis.z,
                    t * axis.x * axis.z + sin_a * axis.y,
                    t * axis.x * axis.y + sin_a * axis.z, t * axis.y * axis.y + cos_a,
                    t * axis.y * axis.z - sin_a * axis.x,
                    t * axis.x * axis.z - sin_a * axis.y, t * axis.y * axis.z + sin_a * axis.x,
                    t * axis.z * axis.z + cos_a
                );
            }

            varyings vert(attributes IN)
            {
                uint base_idx = IN.instance_id * 3;
                float3 vel = int3(
                    grid_v[base_idx + 0],
                    grid_v[base_idx + 1],
                    grid_v[base_idx + 2]
                ) * VEL_FP_SCALE_INV;

                uint3 grid_idx = uint3(
                    IN.instance_id % n_grid,
                    IN.instance_id / n_grid % n_grid,
                    IN.instance_id / (n_grid * n_grid)
                );
                float3 grid_pos = (grid_idx + 0.5) / n_grid * scale;

                float3 dir = normalize(vel);
                float3x3 rot = rotation_from_up_to_dir(dir);
                float vel_len = length(vel);
                float3 local_pos = IN.vertex.xyz * float3(size, vel_len * vel_scale, size);
                float3 pos = grid_pos + mul(rot, local_pos);

                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(pos);
                OUT.uv = IN.uv;
                OUT.velocity = float4(vel_len, 0, 0, 0);
                return OUT;
            }

            float4 frag(varyings IN) : SV_Target
            {
                if (IN.velocity.x < 0.01)
                    discard;
                float3 col = hsv2rgb(IN.velocity.x, 1, 1);
                return float4(col, 1);
            }
            ENDHLSL
        }

    }
}