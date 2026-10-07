Shader "Custom/LowpolyShader"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "Forward"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = pos.positionCS;

                VertexNormalInputs normalInput =
                    GetVertexNormalInputs(IN.normalOS);

                OUT.worldNormal = normalInput.normalWS;

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.worldNormal);

                // °íÁ¤ ±¤¿ø
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));

                float NdotL = saturate(dot(normalWS, lightDir));

                // Toon Lighting
                float lighting;

                if (NdotL > 0.66)
                    lighting = 1.0;
                else if (NdotL > 0.33)
                    lighting = 0.7;
                else
                    lighting = 0.3;

                half4 texColor =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        IN.uv);

                return half4(
                    texColor.rgb * _BaseColor.rgb * lighting,
                    texColor.a
                );
            }
            ENDHLSL
        }
    }
    FallBack Off
}
