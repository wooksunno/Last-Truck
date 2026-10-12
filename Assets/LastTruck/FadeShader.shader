Shader "Custom/FadeShader"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _FadeRadius ("Fade Radius", Float) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        // 1. 메인 패스 (오브젝트 렌더링)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float3 _PlayerPosition;
                float _FadeRadius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            // 간단한 디더링 패턴 함수 (도트 무늬 생성)
            float DitherScreen(float2 pos)
            {
                float dither[16] = {
                    0.0f, 0.5f, 0.125f, 0.625f,
                    0.75f, 0.25f, 0.875f, 0.375f,
                    0.1875f, 0.6875f, 0.0625f, 0.5625f,
                    0.9375f, 0.4375f, 0.8125f, 0.3125f
                };
                int x = (int)fmod(pos.x, 4);
                int y = (int)fmod(pos.y, 4);
                return dither[x + y * 4];
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 플레이어와의 거리 계산
                float dist = distance(input.positionWS, _PlayerPosition);

                // 거리가 FadeRadius 이내면 디더링 패턴으로 픽셀을 솎아냄 (구멍 뚫기)
                if (dist < _FadeRadius)
                {
                    float alphaThreshold = dist / _FadeRadius; // 가까울수록 0에 수렴
                    float dither = DitherScreen(input.positionCS.xy);
                    if (dither > alphaThreshold)
                    {
                        discard; // 해당 픽셀을 그려서 지워버림 (투명 효과)
                    }
                }

                half4 texColor = _BaseMap.Sample(sampler_BaseMap, input.uv) * _BaseColor;
                return texColor;
            }
            ENDHLSL
        }

        // 2. 섀도우 캐스터 패스 (이 부분이 있어야 그림자가 유지됩니다!)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 월드 포지션과 클립 포지션을 URP 표준 방식으로 계산
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // 빛의 방향에 따른 그림자 오프셋 적용 (지글거림 방지)
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 positionWS = vertexInput.positionWS;
                float3 normalWS = normalInput.normalWS;
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, 0));
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}