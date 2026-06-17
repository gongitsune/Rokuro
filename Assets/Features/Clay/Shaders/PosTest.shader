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
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.normal = v.normal;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float depth = i.vertex.z;
                float3 pos_vs = ComputeViewSpacePosition(i.vertex.xy / _ScreenParams.xy, i.vertex.z, UNITY_MATRIX_I_P);
                float3 n = cross(ddx(pos_vs), ddy(pos_vs));
                n = normalize(mul(transpose(UNITY_MATRIX_V), float4(n, 0)).rgb);
                return float4(n, 1.0);
            }
            ENDHLSL
        }
    }
}