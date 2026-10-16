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
            float4 _ChopperHaze;

            float3 Haze(float2 uv, float3 color)
            {
                if (_ChopperHaze.z < 0.001) return color;
                float raw = SampleSceneDepth(uv);
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 toPoint = world - _WorldSpaceCameraPos;
                float d = length(toPoint);
                #if UNITY_REVERSED_Z
                bool sky = raw < 0.00001;
                #else
                bool sky = raw > 0.99999;
                #endif
                if (sky) d = 260.0;
                float height = sky ? 30.0 : max(world.y - _ChopperHaze.y, 0.0);
                float thick = (1.0 - exp(-max(d - 42.0, 0.0) * _ChopperHaze.x)) * exp(-height * 0.035);
                float2 sunDelta = _ForestSunUV.xy - uv;
                float toward = saturate(1.0 - length(sunDelta) * 0.8) * _ForestSunUV.z;
                float3 cool = float3(0.60, 0.68, 0.78);
                float3 warm = float3(1.02, 0.84, 0.62);
                float3 tint = lerp(cool, warm, toward * toward) * _ChopperHaze.w;
                return lerp(color, tint, saturate(thick * _ChopperHaze.z));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                color.rgb = Haze(uv, color.rgb);
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
                    float bright = saturate((lum - 0.32) * 2.2);
                    accum += bright * decay;
                    decay *= 0.97;
                }
                accum /= 24.0;
                float radial = saturate(1.0 - dist * 0.9);
                radial = radial * radial * (3.0 - 2.0 * radial);
                color.rgb += float3(1.15, 0.78, 0.40) * accum * radial * strength * 2.4;
                float low = saturate((0.58 - uv.y) * 1.35) * strength;
                color.rgb = lerp(color.rgb, color.rgb * float3(0.45, 0.58, 0.72), low * 0.30);
                return color;
            }
            ENDHLSL
        }
    }
}
