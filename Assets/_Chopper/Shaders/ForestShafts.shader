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
                float reach = min(length(delta), 0.55);
                float2 stepUv = reach > 0.0001 ? delta / max(length(delta), 0.0001) * (reach / 12.0) : float2(0, 0);
                float accum = 0;
                float2 p = uv;
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    p += stepUv;
                float3 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(p)).rgb;
                float lum = dot(sample, float3(0.25, 0.55, 0.20));
                float gap = saturate((lum - 0.22) * 2.4);
                accum += gap * (1.0 - i / 12.0);
                }
                accum /= 12.0;
                float here = SampleSceneDepth(uv);
                float onSurface = saturate((here - 0.002) * 600.0);
                color.rgb += float3(1.05, 0.68, 0.32) * accum * strength * 1.8 * onSurface;
                float low = saturate((0.58 - uv.y) * 1.35) * strength;
                color.rgb = lerp(color.rgb, color.rgb * float3(0.45, 0.58, 0.72), low * 0.35);
                return color;
            }
            ENDHLSL
        }
    }
}
