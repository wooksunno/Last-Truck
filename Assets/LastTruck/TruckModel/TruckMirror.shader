// 트럭 사이드 미러 거울면 (로우폴리용, URP)
//  - 진짜 실시간 거울(카메라 한 대 더 렌더링)은 비싸서, 반사 프로브/하늘 + 주변광으로 "거울처럼 보이는" 은색 면을 만든다.
//  - 지평선 위는 하늘, 아래는 어두운 땅이 비쳐 거울 느낌이 나고, 낮/밤에 맞춰 밝기가 자동으로 바뀐다.
//  - 또렷한 해 하이라이트 + 대각선 광택 줄무늬 + 가장자리 살짝 어둡게 → 로우폴리 스타일과 어울리게.
Shader "LastTruck/TruckMirror"
{
    Properties
    {
        _Tint ("거울 색", Color) = (0.82, 0.86, 0.9, 1)
        _ReflectionStrength ("반사 세기", Range(0, 2)) = 1.0
        _ProbeMix ("반사 프로브 비율 (0=주변광 하늘만)", Range(0, 1)) = 0.6
        _Smoothness ("매끈함", Range(0, 1)) = 0.96
        _SkyBoost ("하늘 밝기", Range(0.5, 2.5)) = 1.35
        _GroundDarkness ("땅 반사 어둡기", Range(0, 1)) = 0.6
        _EdgeDarken ("가장자리 어둡게", Range(0, 1)) = 0.35
        _HighlightStrength ("해 하이라이트 세기", Range(0, 4)) = 2.0
        _StreakStrength ("광택 줄무늬 세기", Range(0, 1)) = 0.18
        _StreakScale ("광택 줄무늬 간격 (1m당)", Range(0.5, 20)) = 6
        _StreakWidth ("광택 줄무늬 굵기", Range(0.01, 0.4)) = 0.07
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _ReflectionStrength;
            half _ProbeMix;
            half _Smoothness;
            half _SkyBoost;
            half _GroundDarkness;
            half _EdgeDarken;
            half _HighlightStrength;
            half _StreakStrength;
            half _StreakScale;
            half _StreakWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardMirror"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half NdotV = saturate(dot(N, V));
                float3 R = reflect(-V, N);

                Light mainLight = GetMainLight();
                half3 ambient = SampleSH(N);
                half lightLevel = saturate(Luminance(mainLight.color) * 0.7 + Luminance(ambient) * 1.5);

                // 반사: 위는 하늘(주변광 방향 색), 아래는 어두운 땅 + 반사 프로브
                half horizon = smoothstep(-0.1, 0.06, R.y);
                half3 sky = SampleSH(R) * _SkyBoost;
                half3 ground = SampleSH(float3(0, -1, 0)) * (1.0 - _GroundDarkness) * 0.8;
                half3 fake = lerp(ground, sky, horizon);
                half3 probe = GlossyEnvironmentReflection(R, 1.0 - _Smoothness, 1.0);
                half3 refl = lerp(fake, probe, _ProbeMix) * _ReflectionStrength;

                // 가장자리는 살짝 어둡게 (거울 테두리 그늘 느낌)
                half edge = lerp(1.0 - _EdgeDarken, 1.0, NdotV);
                half3 color = refl * _Tint.rgb * edge;

                // 해 하이라이트 (끊어지는 하이라이트)
                float3 H = normalize(mainLight.direction + V);
                half specRaw = pow(saturate(dot(N, H)), exp2(10.0 * _Smoothness + 1.0));
                color += smoothstep(0.45, 0.55, specRaw) * _HighlightStrength * mainLight.color;

                // 대각선 광택 줄무늬 (작은 거울이라 촘촘하게)
                float3 originWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float d = dot(i.positionWS - originWS, normalize(float3(0.7, 0.7, 0.2))) * _StreakScale;
                float band = abs(frac(d) - 0.5);
                color += smoothstep(_StreakWidth, 0.0, band) * _StreakStrength * lightLevel;

                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 깊이 (SSAO, 깊이 텍스처용)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
