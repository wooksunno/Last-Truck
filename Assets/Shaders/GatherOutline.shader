// 채집 대상 윤곽선: 모델을 법선 방향으로 부풀린 뒷면만 그려서 테두리를 만든다 (inverted hull).
// 화면에서 항상 같은 굵기(_Width 픽셀)로 보이고, 날카로운 모서리에서 틈이 벌어지지 않게 "모델 중심에서 바깥쪽 방향"을 섞는다.
Shader "LastTruck/GatherOutline"
{
    Properties
    {
        _Color ("윤곽선 색", Color) = (1, 0.93, 0.76, 1)
        _Width ("굵기 (픽셀)", Float) = 7
        _Center ("모델 중심 (오브젝트 좌표)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+20" }
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float4 _Center;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 radialOS = input.positionOS.xyz - _Center.xyz;
                float3 radialWS = TransformObjectToWorldDir(radialOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 dirWS = normalize(normalize(normalWS) + normalize(radialWS + 1e-5));

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;
                dirCS = dirCS / max(length(dirCS), 1e-4);
                // 화면 픽셀 단위로 밀어서 멀리서도 가까이서도 같은 굵기
                positionCS.xy += dirCS * (_Width * 2.0 / _ScreenParams.xy) * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
