// Instanced grass carpet for GrassCarpet: blade clumps that sway in gusting wind, lean away from players and
// enemies (_GrassPushers), take their color from the zone map (_GrassMap: G dryness, B lushness) plus large and
// small scale variation, and shrink away near the draw distance so the edge of the carpet never shows.
Shader "Chopper/Grass"
{
    Properties
    {
        _BaseDark ("Root color", Color) = (0.13, 0.22, 0.07, 1)
        _TipGreen ("Tip color, fresh", Color) = (0.42, 0.62, 0.20, 1)
        _TipDry ("Tip color, dry", Color) = (0.55, 0.60, 0.24, 1)
        _TipLush ("Tip color, lush", Color) = (0.26, 0.48, 0.15, 1)
        _WindStrength ("Wind strength", Float) = 0.22
        _WindSpeed ("Wind speed", Float) = 1.1
        _GustScale ("Gust wave length (m)", Float) = 38
        _PushStrength ("Push strength", Float) = 0.75
        _FadeStart ("Shrink start (m)", Float) = 50
        _FadeEnd ("Shrink end (m)", Float) = 70
        _Translucency ("Back light", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Set every frame by WeatherSystem from the weather (1 a sunny breeze, 2 a rainy gale). Outside the material so
        // the asset never changes; 0 (never set, as in the editor's scene view) counts as 1.
        float _ChopperWind;

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseDark;
            half4 _TipGreen;
            half4 _TipDry;
            half4 _TipLush;
            float _WindStrength;
            float _WindSpeed;
            float _GustScale;
            float _PushStrength;
            float _FadeStart;
            float _FadeEnd;
            float _Translucency;
        CBUFFER_END

        // Set by GrassCarpet every frame.
        float4 _GrassPushers[16];
        float _GrassPusherCount;
        float4 _GrassMapRect; // x, z of the map corner, 1 / width, 1 / depth
        TEXTURE2D(_GrassMap);
        SAMPLER(sampler_GrassMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR; // r: height along the blade 0..1, g: random per blade
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        // Bends one vertex in world space. Returns the displaced position; height is 0 at the root and 1 at the tip.
        float3 DisplaceGrass(float3 positionWS, float3 rootWS, float height, float bladeRandom)
        {
            float weight = height * height;

            // Wind: a slow travelling gust wave across the meadow plus a quick flutter per blade.
            // The same direction as WeatherRules.WindDirection, which slants the rain.
            float2 windDir = normalize(float2(0.8, 0.6));
            float windScale = _ChopperWind > 0.001 ? _ChopperWind : 1.0;
            float wave = dot(rootWS.xz, windDir) / max(_GustScale, 1.0);
            float gust = sin(wave * 6.2831 - _Time.y * _WindSpeed) * 0.5 + 0.5;
            gust = gust * gust;
            float flutter = sin(_Time.y * (2.4 + bladeRandom * 1.8) + bladeRandom * 31.0 + rootWS.x * 0.7);
            float sway = ((0.25 + gust * 0.9) * _WindStrength + flutter * 0.05) * windScale;
            positionWS.xz += windDir * sway * weight;
            positionWS.y -= sway * sway * 0.35 * weight;

            // Players and enemies part the grass.
            int count = (int)_GrassPusherCount;
            for (int i = 0; i < 16; i++)
            {
                if (i >= count) break;
                float4 pusher = _GrassPushers[i];
                float2 offset = rootWS.xz - pusher.xz;
                float distance = length(offset);
                float reach = pusher.w;
                float vertical = saturate(1.0 - abs(rootWS.y - pusher.y) / 2.5);
                float strength = saturate(1.0 - distance / reach) * vertical;
                strength = strength * strength * (3.0 - 2.0 * strength);
                float2 away = distance > 0.001 ? offset / distance : float2(0, 1);
                positionWS.xz += away * strength * _PushStrength * weight;
                positionWS.y -= strength * _PushStrength * 0.6 * weight;
            }
            return positionWS;
        }

        // Shrinks the clump toward its root as it nears the draw distance.
        float DistanceShrink(float3 rootWS)
        {
            float distance = length(rootWS - _WorldSpaceCameraPos);
            return 1.0 - smoothstep(_FadeStart, _FadeEnd, distance);
        }

        float3 GrassWorldPosition(Attributes input, out float3 rootWS, out float bladeRandom)
        {
            rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            bladeRandom = input.color.g;
            float shrink = DistanceShrink(rootWS);
            positionWS = lerp(rootWS, positionWS, shrink);
            return DisplaceGrass(positionWS, rootWS, input.color.r, bladeRandom);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 color : TEXCOORD1;
                half height : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 rootWS;
                float bladeRandom;
                float3 positionWS = GrassWorldPosition(input, rootWS, bladeRandom);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                // Zone color from the map, then broad meadow patches and a little per clump and per blade variety.
                float2 mapUV = (rootWS.xz - _GrassMapRect.xy) * _GrassMapRect.zw;
                half4 zone = SAMPLE_TEXTURE2D_LOD(_GrassMap, sampler_GrassMap, mapUV, 0);
                half3 tip = _TipGreen.rgb;
                tip = lerp(tip, _TipLush.rgb, zone.b);
                tip = lerp(tip, _TipDry.rgb, zone.g);
                float patch = sin(rootWS.x * 0.043 + sin(rootWS.z * 0.031) * 2.0) * sin(rootWS.z * 0.037 + 1.7);
                // broad drier patches, mild: concept 2.0 grass is green with lighter tips, not wheat (was patch * 0.35 + 0.08)
                tip = lerp(tip, _TipDry.rgb, saturate(patch * 0.25));
                float clump = Hash21(rootWS.xz);
                tip *= 0.86 + clump * 0.24 + (bladeRandom - 0.5) * 0.12;
                output.color = tip;
                output.height = input.color.r;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half height = input.height;
                // Dark roots fade into the tip color: this is what makes a carpet read as dense.
                half3 albedo = lerp(_BaseDark.rgb, input.color, smoothstep(0.0, 0.85, height));

                // Grass normals point mostly up so the carpet shades like the ground beneath it.
                half3 normalWS = half3(0, 1, 0);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction) * 0.6 + 0.4);
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half back = pow(saturate(dot(-viewDir, mainLight.direction)), 3.0) * _Translucency * height;
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 color = albedo * mainLight.color * (ndl + back) * shadow;

                // Sky light, darker toward the roots.
                half3 ambient = SampleSH(normalWS);
                color += albedo * ambient * lerp(0.55, 1.0, height);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half lightNdl = saturate(dot(normalWS, light.direction) * 0.5 + 0.5);
                    color += albedo * light.color * light.distanceAttenuation * light.shadowAttenuation * lightNdl;
                LIGHT_LOOP_END
                #endif

                color = MixFog(color, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(Attributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 rootWS;
                float bladeRandom;
                output.positionCS = TransformWorldToHClip(GrassWorldPosition(input, rootWS, bladeRandom));
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            NormalsVaryings NormalsVert(Attributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 rootWS;
                float bladeRandom;
                output.positionCS = TransformWorldToHClip(GrassWorldPosition(input, rootWS, bladeRandom));
                return output;
            }

            half4 NormalsFrag(NormalsVaryings input) : SV_Target
            {
                // Straight up, like the ground: the carpet should not get ambient occlusion halos of its own.
                return half4(0, 1, 0, 0);
            }
            ENDHLSL
        }
    }
}
