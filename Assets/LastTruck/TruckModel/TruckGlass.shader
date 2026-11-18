// 트럭 창문 유리 (로우폴리용, URP)
//  - 살짝 푸르스름한 반투명 유리. 비스듬히 볼수록(가장자리) 진해지고 하늘이 더 많이 비친다 (프레넬).
//  - 반사: 씬의 반사 프로브/하늘 + 주변광(낮/밤에 맞춰 자동으로 밝기가 바뀜)을 섞고, 지평선 아래는 어둡게 → 단순하지만 그럴듯한 반사.
//  - 해 방향 하이라이트는 또렷하게 끊어서 로우폴리 느낌을 살리고, 대각선 광택 줄무늬를 살짝 넣는다.
//  - 반사/하이라이트는 뒤 배경 위에 "더해서" 그린다 (진짜 유리처럼 어두운 곳에서도 반사가 보임).
Shader "LastTruck/TruckGlass"
{
    Properties
    {
        _Tint ("유리 색", Color) = (0.55, 0.72, 0.78, 1)
        _Opacity ("정면 불투명도", Range(0, 1)) = 0.28
        _EdgeOpacity ("가장자리 불투명도", Range(0, 1)) = 0.8
        _FresnelPower ("가장자리 범위 (클수록 좁음)", Range(0.5, 8)) = 3
        _ReflectionStrength ("반사 세기", Range(0, 2)) = 0.85
        _ProbeMix ("반사 프로브 비율 (0=주변광 하늘만)", Range(0, 1)) = 0.5
        _Smoothness ("매끈함", Range(0, 1)) = 0.92
        _HighlightStrength ("해 하이라이트 세기", Range(0, 4)) = 1.6
        _StreakStrength ("광택 줄무늬 세기", Range(0, 1)) = 0.22
        _StreakScale ("광택 줄무늬 간격 (1m당)", Range(0.1, 6)) = 1.3
        _StreakWidth ("광택 줄무늬 굵기", Range(0.01, 0.4)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardGlass"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha   // 미리 곱한 알파: 유리 몸체는 반투명, 반사/하이라이트는 더하기
            ZWrite Off
            Cull Off                     // 얇은 판이라 안쪽에서 봐도 보이게

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
                half _EdgeOpacity;
                half _FresnelPower;
                half _ReflectionStrength;
                half _ProbeMix;
                half _Smoothness;
                half _HighlightStrength;
                half _StreakStrength;
                half _StreakScale;
                half _StreakWidth;
            CBUFFER_END

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

            // 단순한 하늘 반사: 주변광(SH) 방향 색 + 지평선 아래는 땅처럼 어둡게, 반사 프로브와 섞는다
            half3 FakeReflection(float3 R, half smoothness)
            {
                half3 sky = SampleSH(R) * 1.25;
                half horizon = smoothstep(-0.12, 0.08, R.y);
                sky *= lerp(0.4, 1.0, horizon);
                half3 probe = GlossyEnvironmentReflection(R, 1.0 - smoothness, 1.0);
                return lerp(sky, probe, _ProbeMix);
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 N = normalize(i.normalWS);
                N *= IS_FRONT_VFACE(facing, 1.0, -1.0);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half NdotV = saturate(dot(N, V));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);

                Light mainLight = GetMainLight();
                half3 ambient = SampleSH(N);
                // 낮/밤 밝기 (밤에는 반사/광택도 같이 어두워진다)
                half lightLevel = saturate(Luminance(mainLight.color) * 0.7 + Luminance(ambient) * 1.5);

                // 유리 몸체 (색 + 약한 조명)
                half bodyAlpha = lerp(_Opacity, _EdgeOpacity, fresnel);
                half3 body = _Tint.rgb * (ambient + mainLight.color * saturate(dot(N, mainLight.direction)) * 0.35);

                // 반사 (정면은 약하게, 가장자리는 강하게)
                float3 R = reflect(-V, N);
                half3 refl = FakeReflection(R, _Smoothness) * _ReflectionStrength * lerp(0.2, 1.0, fresnel);

                // 해 하이라이트: 부드러운 퐁 대신 끊어지게 → 로우폴리 느낌
                float3 H = normalize(mainLight.direction + V);
                half specRaw = pow(saturate(dot(N, H)), exp2(10.0 * _Smoothness + 1.0));
                half3 spec = smoothstep(0.45, 0.55, specRaw) * _HighlightStrength * mainLight.color;

                // 대각선 광택 줄무늬 (오브젝트 기준이라 트럭이 움직여도 유리에 붙어 있다)
                float3 originWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float d = dot(i.positionWS - originWS, normalize(float3(0.75, 0.6, 0.3))) * _StreakScale;
                float band = abs(frac(d) - 0.5);
                half streak = (smoothstep(_StreakWidth, 0.0, band) + 0.5 * smoothstep(_StreakWidth * 0.4, 0.0, abs(band - 0.18)));
                streak *= _StreakStrength * lerp(0.6, 1.0, NdotV) * lightLevel;

                half3 color = body * bodyAlpha + refl + spec + streak.xxx;
                half alpha = saturate(bodyAlpha + Luminance(spec) * 0.5 + streak * 0.5);

                // 안개: 미리 곱한 알파라 안개 색도 불투명도만큼만 섞는다
                color = MixFogColor(color, unity_FogColor.rgb * alpha, i.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
