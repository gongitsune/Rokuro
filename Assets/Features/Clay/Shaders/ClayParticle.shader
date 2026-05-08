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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float scale;
                float radius;
            CBUFFER_END

            StructuredBuffer<float3> x;

            struct attributes
            {
                uint instance_id : SV_InstanceID;
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct varyings
            {
                float4 position_hcs : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            varyings vert(attributes IN)
            {
                float3 p_pos = x[IN.instance_id] * scale;

                float3 cam_dir = normalize(_WorldSpaceCameraPos - p_pos);
                float3 right = normalize(cross(float3(0, 1, 0), cam_dir));
                float3 up = cross(cam_dir, right);
                float3 vertex = IN.vertex.xyz * radius;
                float3 pos = p_pos + right * vertex.x + up * vertex.y;

                varyings OUT;
                OUT.position_hcs = TransformObjectToHClip(pos);
                OUT.color = float4((float3)0.3, 1);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(varyings IN) : SV_Target
            {
                float dist = length(IN.uv - float2(0.5, 0.5));
                IN.color.rgb *= 1 - smoothstep(0.45, 0.5, dist);
                IN.color.a = saturate(dist);
                clip(IN.color.a - smoothstep(0.45, 0.5, dist));
                return IN.color;
            }
            ENDHLSL
        }

    }
}