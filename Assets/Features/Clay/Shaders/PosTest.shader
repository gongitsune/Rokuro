Shader "Unlit/PosTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 screen_uv = i.vertex.xy / _ScaledScreenParams.xy;
                float3 pos_ws = ComputeWorldSpacePosition(screen_uv, i.vertex.z, UNITY_MATRIX_I_VP);
                float3 y = i.vertex.z;
                float4 col = float4(y, 1);
                return col;
            }
            ENDHLSL
        }
    }
}