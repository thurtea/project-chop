// Water for the pond, marsh pools, forest lakes and the sea. It reads the scene behind it (the camera's depth and
// opaque textures, on in the PC pipeline asset) so it can look like water rather than tinted glass:
// - the color deepens and the bottom fades out the deeper the water is, so shallows are clear and the middle is dark,
// - the shore fades in softly with a thin broken foam line where the water meets the ground,
// - the bottom wobbles behind the ripples (refraction),
// - the sky reflects more at low angles (Fresnel), and the sun leaves a hard glint on the ripples,
// - two scrolling ripple maps and a slow swell move the surface; everything dims with the day and night light.
Shader "Chopper/Water"
{
    Properties
    {
        _ShallowColor ("Shallow color", Color) = (0.32, 0.56, 0.55, 1)
        _DeepColor ("Deep color", Color) = (0.04, 0.15, 0.21, 1)
        _DepthDistance ("Depth to full color (m)", Float) = 2.2
        _Clarity ("Clarity", Range(0, 1)) = 0.8
        [Normal][NoScaleOffset] _BumpMap ("Ripples", 2D) = "bump" {}
        _RippleTile ("Ripple tile (m)", Float) = 3.5
        _RippleStrength ("Ripple strength", Range(0, 2)) = 0.6
        _RippleSpeed ("Ripple speed", Float) = 0.06
        _Swell ("Swell", Range(0, 1)) = 0.12
        _Refraction ("Refraction", Range(0, 0.1)) = 0.03
        _Reflection ("Reflection", Range(0, 1)) = 0.85
        _Specular ("Sun glint", Float) = 4
        _FoamColor ("Foam color", Color) = (0.86, 0.9, 0.86, 1)
        _FoamDepth ("Foam depth (m)", Float) = 0.3
        _EdgeFade ("Shore fade (m)", Float) = 0.2
        _Bands ("Depth bands", Float) = 4
        _BandMix ("Band mix", Range(0, 1)) = 0.6
        _FoamWidth ("Foam band (m)", Float) = 0.9
        _FoamRings ("Foam rings", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                half _Clarity;
                float _RippleTile;
                half _RippleStrength;
                float _RippleSpeed;
                half _Swell;
                half _Refraction;
                half _Reflection;
                half _Specular;
                half4 _FoamColor;
                float _FoamDepth;
                float _EdgeFade;
                float _Bands;
                half _BandMix;
                float _FoamWidth;
                float _FoamRings;
            CBUFFER_END

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // The surface normal: two ripple maps scrolling different ways, plus a slow swell of crossing waves.
            half3 SurfaceNormal(float3 positionWS, out half foamNoise)
            {
                float2 uv = positionWS.xz / max(0.1, _RippleTile);
                float t = _Time.y * _RippleSpeed;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv + float2(t, t * 0.6)), _RippleStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * 0.61 + float2(-t * 0.8, t * 1.1)), _RippleStrength);
                float2 p = positionWS.xz;
                float s = _Time.y;
                half2 swell = half2(cos(p.x * 0.55 + p.y * 0.2 + s * 0.9), cos(p.y * 0.47 - p.x * 0.31 + s * 0.7)) * _Swell;
                foamNoise = a.x * 0.5 + b.y * 0.5;
                half3 tangent = normalize(half3(a.xy + b.xy + swell, a.z * b.z));
                // Tangent space has z up; the water lies flat with y up.
                return normalize(half3(tangent.x, tangent.z, tangent.y));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half foamNoise;
                half3 n = SurfaceNormal(input.positionWS, foamNoise);
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float surfaceEye = input.screenPos.w;

                // How much water lies between the surface and whatever is behind it, in meters along the view.
                float depth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - surfaceEye;

                // The bottom seen through the ripples. A refracted sample that lands on something in front of the
                // water (a leg, a reed) is thrown away so nothing above the surface smears into it.
                float2 refractedUV = screenUV + n.xz * _Refraction * saturate(depth);
                float refractedDepth = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams) - surfaceEye;
                if (refractedDepth < 0.0)
                {
                    refractedUV = screenUV;
                    refractedDepth = depth;
                }
                half3 bottom = SampleSceneColor(refractedUV);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 sunLight = sun.color * sun.shadowAttenuation;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 lit = ambient + sunLight * saturate(sun.direction.y) * 0.75;

                // Light is soaked up with depth: the bottom fades into the water's own color.
                half absorb = saturate(1.0 - exp(-max(0.0, refractedDepth) / max(0.05, _DepthDistance)));
                half bands = max(1.0, _Bands);
                absorb = lerp(absorb, floor(absorb * bands + 0.5) / bands, _BandMix);
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorb) * lit;
                half3 tintedBottom = bottom * lerp(half3(1, 1, 1), _ShallowColor.rgb * 1.6, 0.55);
                half3 body = lerp(tintedBottom, waterColor, saturate(absorb + (1.0 - _Clarity) * 0.5));

                // The sky in the ripples, strongest at a glancing look, and dimmer at night.
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(n, view));
                half fresnel = 0.03 + 0.97 * pow(1.0 - facing, 5.0);
                half3 reflected = reflect(-view, n);
                // The baked reflection does not follow the day and night sky, so the live ambient sky is the floor.
                half3 sky = max(GlossyEnvironmentReflection(reflected, 0.04, 1.0) * saturate(Luminance(ambient) * 2.5), SampleSH(reflected) * 0.9);
                half3 color = lerp(body, sky, saturate(fresnel * _Reflection));

                // The sun's glint on the ripples.
                half3 halfway = normalize(sun.direction + view);
                color += pow(saturate(dot(n, halfway)), 420.0) * _Specular * sunLight;

                // A thin broken foam line where the water meets the shore or anything standing in it.
                half shore = saturate(1.0 - depth / max(0.01, _FoamDepth));
                half foam = shore * smoothstep(0.45, 0.85, shore + foamNoise * 0.8);
                half ring = saturate(1.0 - depth / max(0.01, _FoamWidth));
                half stripe = step(0.55, frac(ring * _FoamRings - _Time.y * 0.12 + foamNoise * 0.35)) * ring * ring * step(0.01, _FoamRings);
                color = lerp(color, _FoamColor.rgb * lit * 1.15, stripe * 0.75);
                color = lerp(color, _FoamColor.rgb * lit, foam * 0.5);

                color = MixFog(color, input.fogCoord);
                half alpha = saturate(depth / max(0.01, _EdgeFade));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
