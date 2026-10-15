Shader "Hidden/Chopper/ForestShafts"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _ForestSunUV;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float strength = _ForestSunUV.w;
                if (strength < 0.001 || _ForestSunUV.z < 0.5)
                    return color;

                float2 sun = _ForestSunUV.xy;
                float2 delta = sun - uv;
                float dist = length(delta);
                float2 stepUv = delta / 24.0;
                float accum = 0;
                float decay = 1.0;
                float2 p = uv;
                [unroll]
                for (int i = 0; i < 24; i++)
                {
                    p += stepUv;
                    float3 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(p)).rgb;
                    float lum = dot(sample, float3(0.25, 0.55, 0.20));
                    float bright = saturate((lum - 0.16) * 2.6);
                    accum += bright * decay;
                    decay *= 0.97;
                }
                accum /= 24.0;
                float radial = saturate(1.0 - dist * 0.9);
                radial = radial * radial * (3.0 - 2.0 * radial);
                color.rgb += float3(1.15, 0.78, 0.40) * accum * radial * strength * 3.6;
                float low = saturate((0.58 - uv.y) * 1.35) * strength;
                color.rgb = lerp(color.rgb, color.rgb * float3(0.45, 0.58, 0.72), low * 0.30);
                return color;
            }
            ENDHLSL
        }
    }
}
