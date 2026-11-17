// 경찰 표식 연출용 셰이더 (URP).
//  - _Fresnel = 1 : 몬스터 몸에 덧씌우는 림라이트 (가장자리만 빛남, 가산 합성)
//  - _Fresnel = 0 : 발밑 표식 링처럼 단색으로 빛나는 선
// 몬스터 원래 머티리얼은 건드리지 않고 머티리얼을 하나 "더" 붙이는 방식이라 어떤 셰이더의 몬스터에도 쓸 수 있다.
// Resources 폴더에 있어서 빌드에도 자동으로 포함된다 (Shader.Find("LastTruck/PoliceMarkRim")).
Shader "LastTruck/PoliceMarkRim"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (1, 0.85, 0.2, 1)
        _RimPower ("Rim Power (클수록 가장자리만)", Range(0.5, 8)) = 2.2
        _Intensity ("Intensity", Range(0, 8)) = 1.6
        _Fill ("Inner Fill (안쪽 은은한 색)", Range(0, 1)) = 0.08
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 6
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.55
        _Fresnel ("Fresnel (1=림, 0=단색)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "PoliceMarkRim"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimPower;
                half _Intensity;
                half _Fill;
                half _PulseSpeed;
                half _PulseMin;
                half _Fresnel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = lerp(_PulseMin, 1.0h, 0.5h + 0.5h * sin(_Time.y * _PulseSpeed));

                half amount = 1.0h;   // 단색 모드 (선 렌더러는 법선이 없을 수 있어서 계산하지 않는다)
                if (_Fresnel > 0.5h)
                {
                    half3 normalWS = normalize(input.normalWS);
                    half3 viewWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                    half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                    amount = saturate(rim + _Fill);
                }

                half3 color = _RimColor.rgb * amount * _Intensity * pulse;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
