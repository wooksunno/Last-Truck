// Last Truck - 캐주얼 카툰 캐릭터 셰이더 (URP 17, Forward)
// - 2~3단 톤 램프(밝은 면 / 그림자 면 색조) + 부드러운 경계
// - 스타일라이즈드 스펙큘러(반짝이는 하이라이트), 림 라이트, 환경 반사 틴트
// - 밤이 되면(_GameNightFactor) 림/반사가 강해져서 어두운 곳에서도 실루엣이 읽힌다
// - 히트 플래시(_HitFlash), 얇은 아웃라인 패스
Shader "LastTruck/StylizedCharacter"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Toon Shading)]
        _ShadeColor ("Shade Tint", Color) = (0.70, 0.66, 0.88, 1)
        _ShadeThreshold ("Shade Threshold", Range(0,1)) = 0.48
        _ShadeSoftness ("Shade Softness", Range(0.001,0.5)) = 0.07
        _MidTone ("Mid Tone Strength", Range(0,1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.9

        [Header(Specular)]
        _SpecColor ("Specular Color", Color) = (1,0.97,0.9,1)
        _SpecSize ("Specular Size", Range(0.01,1)) = 0.12
        _SpecSoftness ("Specular Softness", Range(0.001,0.5)) = 0.04
        _SpecStrength ("Specular Strength", Range(0,2)) = 0.55

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (0.75, 0.88, 1, 1)
        _RimThreshold ("Rim Threshold", Range(0,1)) = 0.70
        _RimSoftness ("Rim Softness", Range(0.001,0.5)) = 0.12
        _RimDay ("Rim Strength (Day)", Range(0,3)) = 0.35
        _RimNight ("Rim Strength (Night)", Range(0,3)) = 0.8

        [Header(Reflection)]
        _ReflectStrength ("Reflection Strength", Range(0,2)) = 0.35
        _ReflectRoughness ("Reflection Roughness", Range(0,1)) = 0.45

        [Header(Emission and Feedback)]
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        [HDR] _FlashColor ("Hit Flash Color", Color) = (1,1,1,1)
        _HitFlash ("Hit Flash", Range(0,1)) = 0

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.12, 0.08, 0.14, 1)
        _OutlineWidth ("Outline Width", Range(0,0.05)) = 0.012
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _ShadeColor;
        half _ShadeThreshold;
        half _ShadeSoftness;
        half _MidTone;
        half _AmbientStrength;
        half4 _SpecColor;
        half _SpecSize;
        half _SpecSoftness;
        half _SpecStrength;
        half4 _RimColor;
        half _RimThreshold;
        half _RimSoftness;
        half _RimDay;
        half _RimNight;
        half _ReflectStrength;
        half _ReflectRoughness;
        half4 _EmissionColor;
        half4 _FlashColor;
        half _HitFlash;
        half4 _OutlineColor;
        half _OutlineWidth;
    CBUFFER_END

    // AtmosphereController가 매 프레임 설정하는 전역값(낮 0 ~ 밤 1)
    float _GameNightFactor;
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            // 톤 램프: 그림자 면 -> (중간 톤) -> 밝은 면
            half3 ToonRamp(half ndl, half3 albedo)
            {
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                half mid = smoothstep(_ShadeThreshold - 0.32 - _ShadeSoftness, _ShadeThreshold - 0.32 + _ShadeSoftness, ndl);
                half3 shade = albedo * _ShadeColor.rgb;
                half3 midc = lerp(shade, albedo, 0.5h);
                half3 c = lerp(shade, midc, mid * _MidTone);
                return lerp(c, albedo, lit);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 albedo = tex.rgb;
                half3 N = normalize(i.normalWS);
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = unity_ProbesOcclusion;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // 메인 라이트
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half ndl = dot(N, mainLight.direction) * 0.5h + 0.5h;
                half mainAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 mainLit = mainLight.color * mainAtten;
                half toonT = ndl * lerp(1.0h, mainAtten, 0.85h);

                half3 H = normalize(mainLight.direction + V);
                half specT = pow(saturate(dot(N, H)), 48.0h);
                half specMask = smoothstep(1.0h - _SpecSize - _SpecSoftness, 1.0h - _SpecSize + _SpecSoftness, specT) * step(0.001h, mainAtten);
                half3 spec = _SpecColor.rgb * specMask * _SpecStrength * saturate(mainLight.color);

                half nov = saturate(dot(N, V));
                half rimBase = 1.0h - nov;
                half rimMask = smoothstep(_RimThreshold - _RimSoftness, _RimThreshold + _RimSoftness, rimBase);
                half rimStrength = lerp(_RimDay, _RimNight, saturate(_GameNightFactor));
                // 낮에는 빛이 닿는 쪽에서 림이 강하고, 밤에는 사방에서 윤곽을 잡아 준다
                half rimLightFacing = saturate(dot(N, mainLight.direction) * 0.5h + 0.6h);
                half3 rim = _RimColor.rgb * rimMask * rimStrength * lerp(rimLightFacing, 1.0h, saturate(_GameNightFactor));

                // 추가 라이트(보조 태양/횃불/트럭 헤드라이트 등): 톤 램프 + 림에 색을 더한다
                half3 extra = 0;
                half3 extraRim = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.viewDirectionWS = V;
                    inputData.shadowCoord = shadowCoord;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    AmbientOcclusionFactor aoFactor = (AmbientOcclusionFactor)0;
                    aoFactor.indirectAmbientOcclusion = 1; aoFactor.directAmbientOcclusion = 1;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light l = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                        half t = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, dot(N, l.direction) * 0.5h + 0.5h);
                        extra += l.color * l.distanceAttenuation * l.shadowAttenuation * t;
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                        half atten = l.distanceAttenuation * l.shadowAttenuation;
                        half t = smoothstep(_ShadeThreshold - _ShadeSoftness - 0.1h, _ShadeThreshold + _ShadeSoftness + 0.1h, dot(N, l.direction) * 0.5h + 0.5h);
                        extra += l.color * atten * t;
                        half rl = saturate(dot(N, l.direction) * 0.5h + 0.55h);
                        extraRim += l.color * atten * rl * rimMask;
                    LIGHT_LOOP_END
                #endif

                // 모든 빛을 합친 뒤 부드럽게 눌러서(과노출 방지) 톤 램프에 곱한다
                half3 lightSum = mainLit + extra;
                half3 lightNorm = lightSum / (1.0h + 0.35h * lightSum);
                half3 ramp = ToonRamp(toonT, albedo);
                half3 ambient = SampleSH(N) * _AmbientStrength;
                half3 color = ramp * lightNorm * 1.15h + albedo * ambient * 0.6h + spec;
                // 어두운 곳에서도 톤이 죽지 않게 최소 밝기 보정
                color = max(color, albedo * _ShadeColor.rgb * 0.14h);

                // 환경 반사(스카이/큐브맵) + 프레넬
                half3 reflVec = reflect(-V, N);
                half3 env = GlossyEnvironmentReflection(reflVec, i.positionWS, _ReflectRoughness, 1.0h, screenUV);
                half fres = pow(1.0h - nov, 3.0h);
                half reflBoost = lerp(1.0h, 1.8h, saturate(_GameNightFactor));
                color += env * fres * _ReflectStrength * reflBoost;

                color += rim + extraRim * _RimColor.rgb * rimStrength * 0.6h;
                color += _EmissionColor.rgb;
                color = lerp(color, _FlashColor.rgb, _HitFlash);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // 반전 껍질 아웃라인
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            V vertOutline(A input)
            {
                V o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                // 카메라 거리에 비례해서 두께가 너무 얇아지거나 굵어지지 않게 보정
                float dist = distance(_WorldSpaceCameraPos, posWS);
                float w = _OutlineWidth * clamp(dist * 0.12, 0.5, 2.2);
                posWS += nWS * w;
                o.positionCS = TransformWorldToHClip(posWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 fragOutline(V i) : SV_Target
            {
                half3 c = _OutlineColor.rgb;
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V vertShadow(A input)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 fragShadow(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vertDepth(A input) { V o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half fragDepth(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            V vertDN(A input) { V o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o; }
            half4 fragDN(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
