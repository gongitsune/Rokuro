Shader "Hidden/TexturePacking"
{
    Properties {}
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_Roughness);
            TEXTURE2D(_Metallic);
            TEXTURE2D(_Ao);

            float4 frag(Varyings i) : SV_Target
            {
                float roughness = _Roughness.Sample(sampler_LinearClamp, i.texcoord).r;
                float metallic = _Metallic.Sample(sampler_LinearClamp, i.texcoord).r;
                float ao = _Ao.Sample(sampler_LinearClamp, i.texcoord).r;

                float smoothness = 1.0 - roughness;

                return float4(metallic, ao, 0.0, smoothness);
            }
            ENDHLSL
        }
    }
}