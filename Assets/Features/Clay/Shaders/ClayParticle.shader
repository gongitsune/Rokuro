Shader "Custom/ClayParticle"
{
    Properties
    {
        scale("Scale", Float) = 10
        radius("Radius", Float) = 0.1
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float scale;
                float radius;
            CBUFFER_END

            StructuredBuffer<float3> x;

            struct attributes
            {
                uint vert_id : SV_VertexID;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float4 color : COLOR;
            };

            varyings vert(attributes IN)
            {
                float3 p_pos = x[IN.vert_id / 6];
                const float3 offsets[] = {
                    float3(-radius, -radius, 0),
                    float3(radius, -radius, 0),
                    float3(radius, radius, 0),
                    float3(-radius, -radius, 0),
                    float3(radius, radius, 0),
                    float3(-radius, radius, 0)
                };
                float3 offset = offsets[IN.vert_id % 6];
                float3 pos = mul(UNITY_MATRIX_I_V, TransformObjectToWorld(offset));
                // pos += p_pos;

                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(pos);
                OUT.color = float4(1, 0, 0, 1);
                return OUT;
            }

            float4 frag(varyings IN) : COLOR
            {
                return IN.color;
            }
            ENDHLSL
        }

    }
}