Shader "Custom/ClayMPM"
{
    Properties
    {
        scale("Scale", Float) = 10
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
                float p_size : PSIZE;
            };

            varyings vert(attributes IN)
            {
                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(x[IN.vert_id] * scale);
                OUT.color = float4(1, 0, 0, 1);
                OUT.p_size = 10;
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