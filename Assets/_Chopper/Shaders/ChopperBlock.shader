// Block terrain (W5-08). Vertex colour is already linear; its alpha carries the block type (type / 8 + 1 / 16) and
// UV0.x the top height of a side face. Each face is drawn as Minecraft-style 1/16 m texels in three shades
// (terrain_palette.txt: a quarter dark, half base, a quarter light), grass blocks get a jagged green lip down their dirt
// sides, and a soft line on the block grid keeps greedy-merged faces reading as separate blocks (concept 2.0).
Shader "Chopper/Block"
{
    Properties
    {
        _Edge ("Edge darkening", Range(0, 1)) = 0.16
        _Jitter ("Texel shade strength", Range(0, 1)) = 1
        _Dark ("Dark texel shade", Range(0.3, 1)) = 0.72
        _Light ("Light texel shade", Range(1, 2)) = 1.32
        // sRGB: Unity converts Color properties to linear once. The old default was already linear, so it was
        // converted twice and the lip drew near black (the dark wedges on every half-block step).
        _LipColor ("Grass lip colour (sRGB)", Color) = (0.3373, 0.4941, 0.1803, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half _Edge;
            half _Jitter;
            half _Dark;
            half _Light;
            half4 _LipColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                float  faceTop    : TEXCOORD3;
                half4  color      : COLOR;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.color = v.color;
                o.faceTop = v.uv.x;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n0 = normalize(i.normalWS);
                bool isTop = n0.y > 0.5;
                int type = (int)floor(i.color.a * 8.0);
                // One shade per 1/16 m texel in 3D (nudged inside the face), so cliff faces get pixels, not streaks.
                float3 cell3 = floor(i.positionWS * 16.0 - n0 * 0.5);
                float h = Hash21(cell3.xz + cell3.y * float2(17.0, 31.0));
                float h2 = Hash21(cell3.zx * 1.37 + cell3.y * float2(5.0, 11.0));
                half shade = h < 0.25 ? _Dark : (h < 0.75 ? 1.0 : _Light);
                // a touch of hue drift between texels, warmer or cooler, as in the concept's grass
                half3 drift = half3(1.0 + (h2 - 0.5) * 0.10, 1.0, 1.0 - (h2 - 0.5) * 0.10);
                half3 base = i.color.rgb;
                // Grass sides: dirt with a green lip of 2 to 5 texels hanging from the top edge, per texel column.
                if (!isTop && type == 0)
                {
                    float along = abs(n0.x) > 0.5 ? i.positionWS.z : i.positionWS.x;
                    float column = floor(along * 16.0);
                    float lip = 2.0 + floor(Hash21(float2(column, 7.0 + i.faceTop * 3.0)) * 4.0);
                    float below = (i.faceTop - i.positionWS.y) * 16.0;
                    if (below < lip) base = _LipColor.rgb;
                }
                float3 g = frac(i.positionWS);
                float gy = frac(i.positionWS.y * 2.0);
                float edge = min(min(g.x, 1.0 - g.x), min(g.z, 1.0 - g.z));
                edge = min(edge, min(gy, 1.0 - gy));
                half gridLine = (half)(1.0 - saturate(edge * 28.0));
                // Fade the fine detail out where one screen pixel covers more of it than it can show: far away the
                // 6 cm texels and the 3.5 cm grid lines aliased into shimmering (the flicker on distant ground and cliffs).
                float3 fw3 = fwidth(i.positionWS);
                float footprint = max(fw3.x, max(fw3.y, fw3.z));
                half texel = (half)saturate(1.5 - footprint * 16.0) * _Jitter;
                gridLine *= (half)saturate(1.5 - footprint * 24.0);
                half3 texelShade = lerp(half3(1, 1, 1), shade * drift, texel);
                half3 albedo = base * texelShade * (1.0 - gridLine * _Edge);

                float3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(dot(n, mainLight.direction));
                // Sides facing away from the sun got only ambient and fill, which the grading crushed to black: thin
                // half-block steps read as black wedges once every riser had a wall (W5-08 step 5). A floor of
                // sunlight on sides keeps shaded dirt brown and the grass lip green, like the concept's cliffs.
                if (!isTop) ndl = max(ndl, 0.3h);
                half3 direct = mainLight.color * (ndl * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = SampleSH(n);
                // A low sun leaves flat tops in deep shadow. A little fill keeps the block colour readable.
                half3 c = albedo * (direct + ambient + 0.22);
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert (Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth and normals for the depth prepass, SSAO, and anything that reads the camera depth (water edges).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert (Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
