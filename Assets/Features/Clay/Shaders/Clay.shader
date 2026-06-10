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
        #include "Assets/Features/Clay/Shaders/Parameters.hlsl"
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

                float3 p_pos = mul(object_to_world, float4(particle_pos[pid], 1.0)).xyz;
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
            Name "Filter"

            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment narrow_range_filter_frag

            #include "Assets/Features/Clay/Shaders/NarrowRangeFilter.hlsl"
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

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
                float3 albedo = clay_color;

                // ライト・視線方向をビュー空間に変換
                float3 l = normalize(_MainLightPosition.xyz);
                float3 v = float3(0, 0, 1); // ビュー空間では視線はZ+
                float3 h = normalize(l + v);

                float n_dot_l = dot(n, l);
                float3 diffuse = (n_dot_l * 0.5 + 0.5) * _MainLightColor.rgb;
                float3 specular = pow(max(0.0, dot(n, h)), 80.0) * _MainLightColor.rgb * .1;
                return float4(albedo * diffuse + specular, 1.0);
            }
            ENDHLSL
        }
    }
}