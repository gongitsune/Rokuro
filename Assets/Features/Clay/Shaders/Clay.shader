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
        #include "Assets/Features/Utils/Shaders/quaternion.hlsl"

        #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
        ENDHLSL

        Pass
        {
            Name "Particle Depth"

            Cull Off
            ZTest LEqual
            ZWrite On

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag

            CBUFFER_START(ClayDepthParams)
                float radius;

                float delta; // δ: 深度範囲（例: 粒子半径の10倍）
                float mu; // µ: エッジカーブ強度（例: 粒子半径）
                float sigma_world; // ワールド空間フィルタサイズ
                int max_filter_radius; // カーネルピクセル半径
                int direction; // 0=水平, 1=垂直, 2=2D(クリーンアップ)

                float yaw_rad; // Yaw方向の回転

                float4x4 object_to_world;
            CBUFFER_END

            StructuredBuffer<float3> particle_pos;

            struct attributes
            {
                uint vert_id : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct varyings
            {
                float4 clip_pos : SV_POSITION;
                float2 local_uv : TEXCOORD0;
                float3 view_pos: TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float2 quad[6] = {
                float2(0.5, 0.5), float2(0.5, -0.5), float2(-0.5, -0.5),
                float2(0.5, 0.5), float2(-0.5, -0.5), float2(-0.5, 0.5)
            };

            varyings vert(attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                uint pid = IN.vert_id / 6;
                uint q_id = IN.vert_id % 6;

                float3 corner = float3(quad[q_id] * radius, 0.0);
                float2 uv = quad[q_id] + 0.5;

                float3 p_pos = particle_pos[pid] - 0.5;
                float4 q = rotate_angle_axis(yaw_rad, float3(0, 1, 0));
                p_pos = rotate_vector(p_pos, q);
                p_pos += 0.5;

                p_pos = mul(object_to_world, float4(p_pos, 1.0)).xyz;
                float3 view_pos = mul(UNITY_MATRIX_V, float4(p_pos, 1.0)).xyz;
                float4 out_pos = mul(UNITY_MATRIX_P, float4(view_pos + corner, 1.0));

                OUT.clip_pos = out_pos;
                OUT.local_uv = uv;
                OUT.view_pos = view_pos;
                return OUT;
            }

            float frag(varyings IN) : SV_Depth
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

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
            Name "Filter"

            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment narrow_range_filter_frag

            CBUFFER_START(ClayFilterParams)
                float delta; // δ: 深度範囲（例: 粒子半径の10倍）
                float mu; // µ: エッジカーブ強度（例: 粒子半径）
                float sigma_world; // ワールド空間フィルタサイズ
                int max_filter_radius; // カーネルピクセル半径
                int direction; // 0=水平, 1=垂直, 2=2D(クリーンアップ)
            CBUFFER_END

            #include "Assets/Features/Clay/Shaders/NarrowRangeFilter.hlsl"
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            #include "Assets/Features/Clay/Shaders/gtao.hlsl"

            CBUFFER_START(ClayParams)
                float4 clay_color;
                float normal_strength;
                float normal_tiling;
                float yaw_rad;
            CBUFFER_END

            TEXTURE2D(normal_map);

            float3 sample_triplanar_normal(float3 pos_ws, float3 n_ws, float tiling, float strength)
            {
                float3 blend = abs(n_ws);
                blend = pow(blend, 1); // ブレンドの境界をシャープに（任意）
                blend /= blend.x + blend.y + blend.z + 1e-5;

                float2 uv_x = pos_ws.zy * tiling; // X軸投影面
                float2 uv_y = pos_ws.xz * tiling; // Y軸投影面
                float2 uv_z = pos_ws.xy * tiling; // Z軸投影面

                float3 tn_x = UnpackNormalScale(SAMPLE_TEXTURE2D_X(normal_map, sampler_LinearRepeat, uv_x), strength);
                float3 tn_y = UnpackNormalScale(SAMPLE_TEXTURE2D_X(normal_map, sampler_LinearRepeat, uv_y), strength);
                float3 tn_z = UnpackNormalScale(SAMPLE_TEXTURE2D_X(normal_map, sampler_LinearRepeat, uv_z), strength);

                // 各投影面のtangent-space法線をワールド空間の対応軸系に変換
                // X面: tangent=Z, binormal=Y, normal=X相当
                float3 n_x = float3(tn_x.z * sign(n_ws.x), tn_x.y, tn_x.x);
                float3 n_y = float3(tn_y.x, tn_y.z * sign(n_ws.y), tn_y.y);
                float3 n_z = float3(tn_z.x, tn_z.y, tn_z.z * sign(n_ws.z));

                // 元の幾何法線と合成してブレンド
                float3 n_blend = normalize(n_x * blend.x + n_y * blend.y + n_z * blend.z);
                return n_blend;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float depth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, IN.texcoord, 0).r;

                #if defined(UNITY_REVERSED_Z)
                if (depth <= 0.0) discard;
                #else
                if (depth >= 1.0) discard;
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif

                float3 pos_ws = ComputeWorldSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_VP);
                float3 pos_ws_rot = rotate_vector(pos_ws - 0.5,
                                                  rotate_angle_axis(-yaw_rad, float3(0, 1, 0))
                ) + 0.5;
                float3 n_ws = normalize(cross(ddy(pos_ws), ddx(pos_ws)));
                float3 n_final = sample_triplanar_normal(pos_ws_rot, n_ws, normal_tiling, normal_strength);

                float3 albedo = clay_color.rgb;

                float3 l = normalize(_MainLightPosition.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - pos_ws);
                float3 h = normalize(l + v);

                // float shadow = screen_space_shadow(pos_ws, l);

                float n_dot_l = dot(n_final, l);
                float3 diffuse = (n_dot_l * 0.5 + 0.5) * _MainLightColor.rgb;
                float3 specular = pow(max(0.0, dot(n_final, h)), 80.0) * _MainLightColor.rgb * .1;
                float3 col = albedo * diffuse + specular;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}