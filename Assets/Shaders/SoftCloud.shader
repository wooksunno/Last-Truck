// Last Truck - 몽글몽글한 하늘 구름 (PastelLook이 만든 구름 메시에 쓴다)
// - 해를 향한 쪽은 크림빛 흰색, 아래/그늘 쪽은 연한 라벤더·분홍 (파스텔)
// - 가장자리는 살짝 밝게 빛나서 솜처럼 보인다
// - 노을에는 해 빛 색을 받아 물들고, 밤에는 어두운 남색으로 가라앉는다 (_GameNightFactor)
// - 멀수록 하늘(안개) 색에 녹아든다
Shader "LastTruck/SoftCloud"
{
    Properties
    {
        _TopColor ("Lit Color", Color) = (1, 0.98, 0.95, 1)
        _BottomColor ("Shade Color", Color) = (0.80, 0.78, 0.94, 1)
        _RimColor ("Rim Glow", Color) = (1, 0.93, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _SunTint ("Sun Color Tint", Range(0,1)) = 0.45
        _NightColor ("Night Color", Color) = (0.16, 0.19, 0.32, 1)
        _FogKeep ("Fog Resist", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+50" }
        Pass
        {
            Name "Cloud"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half4 _RimColor;
                half _RimStrength;
                half _SunTint;
                half4 _NightColor;
                half _FogKeep;
            CBUFFER_END
            float _GameNightFactor;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half fogFactor : TEXCOORD2; };

            V vert(A input)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                Light sun = GetMainLight();
                half3 Vd = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // 해 쪽(반사광) + 위쪽(하늘빛)을 섞어 부드러운 2톤 그라데이션
                half sunT = dot(N, sun.direction) * 0.5h + 0.5h;
                half upT = N.y * 0.5h + 0.5h;
                half t = smoothstep(0.15h, 0.9h, sunT * 0.55h + upT * 0.45h);
                half3 col = lerp(_BottomColor.rgb, _TopColor.rgb, t);

                // 해 빛 색으로 물든다 (노을엔 분홍/주황 구름). 세기는 빼고 색만
                half3 sunCol = sun.color / max(max(sun.color.r, max(sun.color.g, sun.color.b)), 0.001h);
                col *= lerp(half3(1, 1, 1), sunCol, _SunTint);

                // 솜 같은 가장자리 빛
                half rim = pow(1.0h - saturate(dot(N, Vd)), 2.5h);
                col += _RimColor.rgb * rim * _RimStrength;

                // 밤: 어둡게 가라앉는다 (달빛 쪽은 조금 남는다)
                half night = saturate(_GameNightFactor);
                col = lerp(col, _NightColor.rgb * (0.7h + 0.6h * t) + rim * 0.06h, night);

                col = lerp(MixFog(col, i.fogFactor), col, _FogKeep);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
