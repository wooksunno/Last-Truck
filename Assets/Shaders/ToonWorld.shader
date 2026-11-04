// Last Truck - 맵/소품용 캐주얼 툰 셰이더 (URP 17, Forward)
// - 플랫 셰이딩: 면 단위 노멀로 로우폴리 각이 또렷하게 보인다 (_FlatShading 0이면 원래 부드러운 노멀)
// - 2단 톤 램프(밝은 면 / 색조가 들어간 그림자 면) + 그림자 경계도 계단처럼 또렷하게
// - 낮/밤 색 변화는 해·달 빛 색과 주변광(AtmosphereController)을 그대로 따른다
// - 횃불 같은 추가 라이트도 톤 단계로 비춘다. 노이즈가 생기는 화면 공간 효과 없이 셰이더만으로 처리
Shader "LastTruck/ToonWorld"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Toon Shading)]
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.62, 0.82, 1)
        _ShadeThreshold ("Shade Threshold", Range(0,1)) = 0.5
        _ShadeSoftness ("Shade Softness", Range(0.001,0.5)) = 0.04
        _MidTone ("Mid Tone Strength", Range(0,1)) = 0.3
        _ShadowSharpness ("Shadow Edge Sharpness", Range(0,1)) = 0.7
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1.0
        _ShadowLift ("Shadow Fill From Light", Range(0,1)) = 0.3

        [Header(Night Moonlight)]
        _NightShadowDarken ("Night Shadow Brightness", Range(0,1)) = 0.5
        _MoonLitBoost ("Night Moonlit Boost", Range(1,2)) = 1.35
        _MoonSheen ("Moon Sheen Strength", Range(0,1)) = 0.35
        _MoonSheenSize ("Moon Sheen Size", Range(0.01,1)) = 0.3
        [Toggle] _FlatShading ("Flat Shading", Float) = 1

        [Header(Stone Readability)]
        // 갈색 땅 위의 갈색 돌처럼 색이 겹쳐 덩어리로만 보일 때: 색을 회색 쪽으로 빼고(_Desaturate), 가장자리를 밝게(_Rim) 해서 윤곽을 살린다
        _Desaturate ("Desaturate", Range(0,1)) = 0
        _DesaturateTint ("Desaturate Tint", Color) = (1,1,1,1)
        _RimStrength ("Rim Light Strength", Range(0,1)) = 0
        _RimColor ("Rim Light Color", Color) = (1,0.95,0.85,1)
        _RimPower ("Rim Light Power", Range(0.5,6)) = 2.5

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)

        [HideInInspector] _Cull ("Cull", Float) = 2
        // 카메라 가림 투명 처리(CameraOcclusionFader)가 바꾸는 값. 평소엔 불투명 그대로
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _ShadeColor;
        half _ShadeThreshold;
        half _ShadeSoftness;
        half _MidTone;
        half _ShadowSharpness;
        half _AmbientStrength;
        half _ShadowLift;
        half _NightShadowDarken;
        half _MoonLitBoost;
        half _MoonSheen;
        half _MoonSheenSize;
        half _FlatShading;
        half _Desaturate;
        half4 _DesaturateTint;
        half _RimStrength;
        half4 _RimColor;
        half _RimPower;
        half4 _EmissionColor;
        half _Cull;
    CBUFFER_END

    // AtmosphereController가 매 프레임 설정 (낮 0 ~ 밤 1 / 날씨 안개 0 ~ 1)
    float _GameNightFactor;
    float _GameWeatherFog;
    // 공기 원근감 (AtmosphereController가 설정): 색 / (시작 m, 끝 m, 세기, 채도 빼기)
    float4 _LT_HazeColor;
    float4 _LT_HazeParams;
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
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
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            // 톤 단계: 0(그림자 면) ~ 1(밝은 면), 중간에 옅은 한 단계
            half Band(half halfLambert)
            {
                half lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, halfLambert);
                half mid = smoothstep(_ShadeThreshold - 0.25h - _ShadeSoftness, _ShadeThreshold - 0.25h + _ShadeSoftness, halfLambert);
                return max(lit, mid * _MidTone);
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 albedo = baseTex.rgb;
                // 색 빼기: 밝기는 유지한 채 회색(+색조)쪽으로
                half luma = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                albedo = lerp(albedo, luma * _DesaturateTint.rgb, _Desaturate);

                half3 smoothN = normalize(i.normalWS);
                if (!frontFace) smoothN = -smoothN;
                // 면 노멀: 화면 미분으로 삼각형 평면을 구하고 정점 노멀 쪽을 향하게 맞춘다
                float3 faceN = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                faceN *= sign(dot(faceN, smoothN) + 1e-4);
                half3 N = normalize(lerp(smoothN, (half3)faceN, _FlatShading));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = unity_ProbesOcclusion;

                // 메인 라이트(해/달): 그림자 경계를 또렷하게 눌러 툰 느낌을 낸다
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half sharpW = lerp(0.5h, 0.08h, _ShadowSharpness);
                half shadow = smoothstep(0.5h - sharpW, 0.5h + sharpW, mainLight.shadowAttenuation);
                half lit = Band(dot(N, mainLight.direction) * 0.5h + 0.5h) * shadow * mainLight.distanceAttenuation;

                // 멀수록 면 하나하나의 밝기 차이를 줄여 디테일을 뭉갠다 (먼 산은 덩어리로만 읽힌다)
                float viewDist = distance(i.positionWS, _WorldSpaceCameraPos);
                half far = (half)saturate((viewDist - _LT_HazeParams.x) / max(_LT_HazeParams.y - _LT_HazeParams.x, 1.0));
                far = far * far * (3.0h - 2.0h * far) * (half)step(0.5, _LT_HazeParams.y);   // 값이 설정되지 않았으면(에디터) 끈다
                lit = lerp(lit, 0.45h + 0.4h * shadow, far * 0.6h);

                half3 ambient = SampleSH(N) * _AmbientStrength;
                // 그림자 면도 빛 색을 조금 받아 새까맣게 꺼지지 않는다 (노을엔 주황빛, 밤엔 푸른빛 그림자)
                half night = saturate(_GameNightFactor);
                half weather = saturate(_GameWeatherFog);
                half3 shadeLight = (ambient + mainLight.color * _ShadowLift) * _ShadeColor.rgb;
                // 밤: 달빛이 안 닿는 곳은 더 어둡게, 달빛을 받는 면은 조금 더 밝게 (대비를 키워 달빛이 형태를 잡아 준다)
                // 안개가 끼면 달빛이 퍼져서 대비가 다시 줄어든다
                half moonContrast = night * (1.0h - weather);
                shadeLight *= lerp(1.0h, _NightShadowDarken, night);
                half3 litLight = ambient + mainLight.color * lerp(1.0h, _MoonLitBoost, moonContrast);
                half3 light = lerp(shadeLight, litLight, lit);

                // 달빛 반짝임: 달 쪽을 향한 면(플랫 셰이딩이라 면 단위로)이 은은하게 빛을 되받는다
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 H = normalize(mainLight.direction + V);
                half sheenT = pow(saturate(dot(N, H)), 12.0h);
                half sheen = smoothstep(1.0h - _MoonSheenSize, 1.0h - _MoonSheenSize + 0.08h, sheenT);
                light += mainLight.color * sheen * lit * _MoonSheen * moonContrast * (1.0h - far);

                // 추가 라이트(횃불, 헤드라이트 등)도 단계로 비춘다
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.viewDirectionWS = V;
                    inputData.shadowCoord = shadowCoord;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    AmbientOcclusionFactor aoFactor = (AmbientOcclusionFactor)0;
                    aoFactor.indirectAmbientOcclusion = 1; aoFactor.directAmbientOcclusion = 1;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light l = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                        light += l.color * l.distanceAttenuation * l.shadowAttenuation * Band(dot(N, l.direction) * 0.5h + 0.5h);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                        // 거리 감쇠도 몇 단계로 끊어서 횃불 빛 웅덩이가 만화처럼 보이게
                        half atten = l.distanceAttenuation * l.shadowAttenuation;
                        half steppedAtten = floor(saturate(atten) * 4.0h + 0.5h) / 4.0h;
                        atten = lerp(atten, max(steppedAtten, atten * 0.5h), 0.6h);
                        light += l.color * atten * Band(dot(N, l.direction) * 0.5h + 0.5h);
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo * light;

                // 공기 원근감: 멀수록 채도가 빠지고 하늘/안개 색으로 옅어진다
                half haze = far * (half)_LT_HazeParams.z;
                half lum = dot(color, half3(0.299h, 0.587h, 0.114h));
                color = lerp(color, lum.xxx, haze * (half)_LT_HazeParams.w);
                color = lerp(color, (half3)_LT_HazeColor.rgb, haze);
                // 가장자리 빛: 시선과 비스듬한 면(윤곽)을 밝혀서 배경과 색이 비슷해도 형태가 읽히게 한다 (밤엔 약하게)
                half rim = pow(1.0h - saturate(dot(smoothN, V)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * lerp(1.0h, 0.5h, night) * saturate(light.r + 0.35h);
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                // 불투명일 땐 Blend One Zero라 알파가 무시되고, 가림 투명 처리 중엔 _BaseColor.a 만큼 비친다
                return half4(color, baseTex.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

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
            ZWrite On ColorMask R Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vertDepth(A input) { V o; UNITY_SETUP_INSTANCE_ID(input); o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half fragDepth(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            V vertDN(A input) { V o; UNITY_SETUP_INSTANCE_ID(input); o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS); return o; }
            half4 fragDN(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
