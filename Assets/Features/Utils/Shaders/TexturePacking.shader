Shader "Hidden/TexturePacking"
{
    Properties {}
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct attributes
        {
            uint vertex_id : SV_VertexID;
        };

        struct varyings
        {
            float4 position_cs : SV_POSITION;
            float2 texcoord : TEXCOORD0;
        };

        varyings vert(attributes i)
        {
            varyings v;

            float2 uv = float2(i.vertex_id << 1 & 2, i.vertex_id & 2);
            v.position_cs = float4(uv * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);

            #if UNITY_UV_STARTS_AT_TOP
            v.texcoord = float2((i.vertex_id << 1) & 2, 1.0 - (i.vertex_id & 2));
            #else
            v.texcoord = float2((i.vertex_id << 1) & 2, i.vertex_id & 2);
            #endif

            return v;
        }
        ENDHLSL

        // We pack roughness, metallic, ao, and smoothness into a single RGBA texture.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_Roughness);
            TEXTURE2D(_Metallic);
            TEXTURE2D(_Ao);

            SAMPLER(linear_clamp_sampler);

            float4 frag(varyings i) : SV_Target
            {
                float roughness = _Roughness.Sample(linear_clamp_sampler, i.texcoord).r;
                float metallic = _Metallic.Sample(linear_clamp_sampler, i.texcoord).r;
                float ao = _Ao.Sample(linear_clamp_sampler, i.texcoord).r;

                float smoothness = 1.0 - roughness;

                return float4(metallic, ao, 0.0, smoothness);
            }
            ENDHLSL
        }

        // We convert a height map to a normal map using a 3x3 Sobel filter.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_Displacement);

            SAMPLER(linear_clamp_sampler);

            float normal_strength;
            float2 texel_size;

            float sample_height(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_Displacement, linear_clamp_sampler, uv).r;
            }

            float4 frag(varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 d = texel_size;

                // 3x3 Sobel
                float h00 = sample_height(uv + float2(-d.x, -d.y));
                float h10 = sample_height(uv + float2(0.0, -d.y));
                float h20 = sample_height(uv + float2(d.x, -d.y));
                float h01 = sample_height(uv + float2(-d.x, 0.0));
                float h21 = sample_height(uv + float2(d.x, 0.0));
                float h02 = sample_height(uv + float2(-d.x, d.y));
                float h12 = sample_height(uv + float2(0.0, d.y));
                float h22 = sample_height(uv + float2(d.x, d.y));

                float dx = (h20 + 2.0 * h21 + h22) - (h00 + 2.0 * h01 + h02);
                float dy = (h02 + 2.0 * h12 + h22) - (h00 + 2.0 * h10 + h20);
                float dz = 1.0 / max(0.001, normal_strength);

                float3 n = normalize(float3(-dx * normal_strength, -dy * normal_strength, dz));

                return float4(n * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}