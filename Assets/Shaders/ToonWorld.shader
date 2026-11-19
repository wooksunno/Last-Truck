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
        // 땅 전용: 위를 보는 면의 빛 계산용 노멀을 하늘 쪽으로 당겨서, 울퉁불퉁한 땅 메쉬의 면마다 밝고 어두운 색종이 무늬가 생기지 않게 한다.
        // (가파른 비탈/절벽은 그대로 면이 살아 있다. 소품/바위는 0 = 원래대로)
        _LightNormalUp ("Ground: Light Normal Toward Up", Range(0,1)) = 0

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

        [Header(Military Weathering (vehicles))]
        // 군용차 도장: 위장 무늬 + 페인트 벗겨짐 + 아래쪽 진흙 + 윗면 먼지 + 빗물 얼룩. 켜진 재질만 계산한다 (다른 재질엔 영향 없음)
        [Toggle(_WEATHERING_ON)] _Weathering ("Enable Weathering", Float) = 0
        _CamoScale ("Camo Scale", Float) = 0.55
        _CamoStrength ("Camo Strength", Range(0,1)) = 0.9
        _CamoDark ("Camo Dark Multiplier", Color) = (0.55, 0.6, 0.5, 1)
        _CamoBrown ("Camo Brown", Color) = (0.11, 0.075, 0.04, 1)
        _ChipColor ("Paint Chip Color (bare metal)", Color) = (0.2, 0.2, 0.19, 1)
        _ChipAmount ("Paint Chip Amount", Range(0,1)) = 0.5
        _MudColor ("Mud Color", Color) = (0.09, 0.06, 0.035, 1)
        _MudHeight ("Mud Height (local)", Float) = 0.95
        _MudStrength ("Mud Strength", Range(0,1)) = 0.85
        _DustColor ("Top Dust Color", Color) = (0.32, 0.27, 0.19, 1)
        _DustStrength ("Top Dust Strength", Range(0,1)) = 0.45
        _StreakStrength ("Rain Streak Strength", Range(0,1)) = 0.35
        _Grain ("Surface Grain", Range(0,0.5)) = 0.12
        // VehicleWeathering 스크립트가 매 프레임 넣는 트럭 기준 좌표 (트럭이 움직여도 무늬가 따라 다닌다). 0이면 월드 좌표를 쓴다
        [HideInInspector] _VehRow0 ("", Vector) = (0,0,0,0)
        [HideInInspector] _VehRow1 ("", Vector) = (0,0,0,0)
        [HideInInspector] _VehRow2 ("", Vector) = (0,0,0,0)

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
        half _LightNormalUp;
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
        float _CamoScale;
        half _CamoStrength;
        half4 _CamoDark;
        half4 _CamoBrown;
        half4 _ChipColor;
        half _ChipAmount;
        half4 _MudColor;
        float _MudHeight;
        half _MudStrength;
        half4 _DustColor;
        half _DustStrength;
        half _StreakStrength;
        half _Grain;
        float4 _VehRow0;
        float4 _VehRow1;
        float4 _VehRow2;
    CBUFFER_END

    // AtmosphereController가 매 프레임 설정 (낮 0 ~ 밤 1 / 날씨 안개 0 ~ 1)
    float _GameNightFactor;
    float _GameWeatherFog;
    // 공기 원근감 (AtmosphereController가 설정): 색 / (시작 m, 끝 m, 세기, 채도 빼기)
    float4 _LT_HazeColor;
    float4 _LT_HazeParams;
    // 파스텔 룩 (PastelLook이 설정, 0 = 원래 모습 ~ 1 = 파스텔). 그림자 면 색조 (라벤더)
    float _LT_Pastel;
    float4 _LT_PastelShade;
    // "호박빛 + 남보라" 시그니처 룩 (PastelLook이 설정, 0이면 원래 모습):
    // 햇빛 받는 면은 따뜻한 꿀빛, 그림자 면은 짙은 남보라, 윤곽엔 낮 호박색 / 밤 청록색 가장자리 빛, 채도는 살짝 올린다
    float _LT_Sig;
    float4 _LT_SigWarm;
    float4 _LT_SigCool;
    float4 _LT_SigRim;
    float4 _LT_SigFill;
    float _LT_SigSat;
    // 깊이 팔레트 (PastelLook이 설정, 0이면 끔): 멀어질수록 원색이 아니라 색조가 서서히 바뀐다 (낮 = 청보라, 노을 = 장밋빛, 밤 = 청록).
    // _LT_DepthTint = 거리에 따라 곱해지는 색조, _LT_DepthMist = 낮은 지형에 고이는 안개 색
    float _LT_Depth;
    float4 _LT_DepthTint;
    float4 _LT_DepthMist;
    // 간접광 (PastelLook이 설정, x = 세기 0이면 끔 / y = 그림자 색조를 무채색 쪽으로 되돌리는 정도):
    // 단색 앰비언트 대신 위에서는 하늘빛, 아래에서는 땅빛을 받고, 햇빛에 달궈진 땅이 옆/아랫면을 따뜻하게 되비춘다
    float4 _LT_GI;
    float4 _LT_GISky;
    float4 _LT_GIGround;
    float4 _LT_GIBounce;
    // 그림자 경계 부드럽게 (PastelLook이 설정, 0 = 재질 값 그대로 또렷한 툰 경계 ~ 1 = 부드럽게 번지는 경계)
    float _LT_ShadowSoft;
    // 그림자 면을 밝은 면 쪽으로 끌어올리는 정도 (PastelLook이 설정, 0 = 그대로): 그림자가 덜 진하게 연하게 깔린다
    float _LT_ShadeLift;
    // 깔끔한 툰 그림자 (PastelLook이 설정): rgb = 그림자 면이 밝은 면의 몇 배 밝기인지(색조별), a = 적용 세기 (0이면 끔)
    float4 _LT_CleanShade;
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
            #pragma shader_feature_local_fragment _ _WEATHERING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #if defined(_WEATHERING_ON)
            // ── 군용차 도장 (텍스처 없이 3D 노이즈로 계산) ──
            float WHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float WNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(WHash(i), WHash(i + float3(1,0,0)), f.x),
                                 lerp(WHash(i + float3(0,1,0)), WHash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(WHash(i + float3(0,0,1)), WHash(i + float3(1,0,1)), f.x),
                                 lerp(WHash(i + float3(0,1,1)), WHash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float WFbm(float3 p)
            {
                return WNoise(p) * 0.55 + WNoise(p * 2.03 + 7.1) * 0.3 + WNoise(p * 4.1 + 13.7) * 0.15;
            }

            half3 ApplyWeathering(half3 albedo, float3 posWS, half3 N)
            {
                // 트럭 기준 좌표 (스크립트가 안 넣었으면 월드 좌표)
                float3 p = posWS;
                if (dot(abs(_VehRow0.xyz), 1.0.xxx) > 0.001)
                    p = float3(dot(_VehRow0, float4(posWS, 1)), dot(_VehRow1, float4(posWS, 1)), dot(_VehRow2, float4(posWS, 1)));

                // 도색된 부분(올리브색)만 위장/벗겨짐을 받는다. 타이어/쇠붙이(회색)는 진흙/먼지만
                half g = albedo.g;
                half paint = saturate((g - max(albedo.r, albedo.b)) / max(g, 0.001h) * 4.0h);

                // 표면 결: 면마다 미세한 밝기 차이로 평평한 단색을 깬다
                albedo *= 1.0h + ((half)WNoise(p * 5.0) - 0.5h) * _Grain * 2.0h;

                // 위장 무늬 (우드랜드 3색): 짙은 녹색 반점 + 갈색 반점, 경계는 만화처럼 또렷하게
                float c1 = WFbm(p * _CamoScale);
                float c2 = WFbm(p * _CamoScale * 1.3 + 31.7);
                half dark = (half)smoothstep(0.55, 0.565, c1);
                half brown = (half)smoothstep(0.56, 0.575, c2) * (1.0h - dark);
                half3 camo = albedo;
                camo = lerp(camo, albedo * _CamoDark.rgb, dark);
                camo = lerp(camo, _CamoBrown.rgb, brown);
                albedo = lerp(albedo, camo, paint * _CamoStrength);

                // 빗물 얼룩: 위에서 아래로 흘러내린 세로 줄무늬
                half streak = (half)smoothstep(0.55, 0.85, WNoise(float3(p.x * 7.0, p.y * 0.5, p.z * 7.0)));
                albedo *= 1.0h - streak * _StreakStrength * 0.5h * paint;

                // 페인트 벗겨짐: 작은 반점에서 쇠 색이 드러난다
                half chipN = (half)(WNoise(p * 6.0 + 5.3) * WNoise(p * 21.0 + 2.1));
                half chip = (half)smoothstep(0.5h - _ChipAmount * 0.22h, 0.52h - _ChipAmount * 0.22h, chipN) * paint;
                albedo = lerp(albedo, _ChipColor.rgb, chip);

                // 윗면 먼지: 하늘을 향한 면에 흙먼지가 얇게 앉는다
                half up = saturate((N.y - 0.55h) * 3.0h);
                half dustN = (half)smoothstep(0.35, 0.75, WFbm(p * 1.7 + 11.0));
                albedo = lerp(albedo, _DustColor.rgb, up * dustN * _DustStrength);

                // 아래쪽 진흙: 바퀴/차체 하단이 울퉁불퉁한 경계로 흙탕물을 뒤집어쓴다
                float edge = (WFbm(p * 2.6 + 3.0) - 0.5) * 0.7;
                half mud = (half)saturate((_MudHeight + edge - p.y) / 0.22);
                half splat = (half)smoothstep(0.62, 0.66, WNoise(p * 9.0 + 1.7)) * (half)saturate((_MudHeight + 0.6 - p.y) / 0.5);
                albedo = lerp(albedo, _MudColor.rgb * (0.8h + 0.4h * (half)WNoise(p * 20.0)), saturate(mud + splat) * _MudStrength);
                return albedo;
            }
            #endif

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
                half soft = max(_ShadeSoftness, (half)saturate(_LT_ShadowSoft) * 0.16h);
                half lit = smoothstep(_ShadeThreshold - soft, _ShadeThreshold + soft, halfLambert);
                half mid = smoothstep(_ShadeThreshold - 0.25h - soft, _ShadeThreshold - 0.25h + soft, halfLambert);
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
                half sig = (half)saturate(_LT_Sig);

                half3 smoothN = normalize(i.normalWS);
                if (!frontFace) smoothN = -smoothN;
                // 면 노멀: 화면 미분으로 삼각형 평면을 구하고 정점 노멀 쪽을 향하게 맞춘다
                float3 faceN = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                faceN *= sign(dot(faceN, smoothN) + 1e-4);
                half3 N = normalize(lerp(smoothN, (half3)faceN, _FlatShading));
                // 빛 계산용 노멀: 땅(위를 보는 면)은 하늘 쪽으로 당겨서 면마다 어두워지는 얼룩을 없앤다. 가파른 비탈은 그대로.
                half groundW = _LightNormalUp * sig * (half)smoothstep(0.35, 0.75, (float)N.y);   // 시그니처 룩을 끄면(Shift+F2) 예전 땅 모습 그대로
                half3 Nl = normalize(lerp(N, half3(0.0h, 1.0h, 0.0h), groundW));
                // 땅의 큰 색 얼룩: 넓은 땅이 단색으로 밋밋하지 않게, 수십 m 단위로 따뜻한 곳/서늘한 곳이 번진다 (땅에만)
                if (groundW > 0.01h)
                {
                    float2 gp = i.positionWS.xz;
                    float g1 = frac(sin(dot(floor(gp * 0.045), float2(12.9898, 78.233))) * 43758.5453);
                    float g2 = frac(sin(dot(floor(gp * 0.045) + float2(1, 0), float2(12.9898, 78.233))) * 43758.5453);
                    float g3 = frac(sin(dot(floor(gp * 0.045) + float2(0, 1), float2(12.9898, 78.233))) * 43758.5453);
                    float g4 = frac(sin(dot(floor(gp * 0.045) + float2(1, 1), float2(12.9898, 78.233))) * 43758.5453);
                    float2 gf = frac(gp * 0.045); gf = gf * gf * (3.0 - 2.0 * gf);
                    half macro = (half)(lerp(lerp(g1, g2, gf.x), lerp(g3, g4, gf.x), gf.y) - 0.5);
                    // 부드럽게 번지는 얼룩 대신, 경계가 또렷한 3톤 면(밝은 곳 / 기본 / 어두운 곳)으로 칠한다. 손으로 칠한 땅 무늬처럼 읽힌다.
                    half gw = saturate(groundW * 2.0h);
                    half patch = smoothstep(0.13h, 0.15h, macro) - smoothstep(0.13h, 0.15h, -macro);   // +1 밝은 면 / 0 기본 / -1 어두운 면
                    albedo *= 1.0h + patch * 0.10h * gw;
                    albedo *= lerp((half3)1.0h, patch > 0 ? half3(1.05h, 1.0h, 0.93h) : half3(0.93h, 0.98h, 1.07h), abs(patch) * 0.8h * gw);
                }
                #if defined(_WEATHERING_ON)
                    albedo = ApplyWeathering(albedo, i.positionWS, N);
                #endif
                // 시그니처 색 보정 (위장 도장 판별 뒤에 적용해야 군용차 무늬가 그대로 남는다)
                // 채도를 올리고 (밝기는 유지), 초록은 라임/노랑 쪽에서 에메랄드 쪽으로 살짝 옮긴다 (붉은 기를 빼고 푸른 기를 더함).
                half lumaS = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                albedo = lerp(lumaS.xxx, albedo, 1.0h + (half)_LT_SigSat * sig);
                half greenDom = saturate((albedo.g - max(albedo.r, albedo.b)) * 3.0h);
                albedo.r *= 1.0h - 0.16h * greenDom * sig;
                albedo.b *= 1.0h + 0.30h * greenDom * sig;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = unity_ProbesOcclusion;

                // 메인 라이트(해/달): 그림자 경계를 또렷하게 눌러 툰 느낌을 낸다
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, shadowMask);
                half sharpW = lerp(0.5h, 0.08h, _ShadowSharpness);
                sharpW = lerp(sharpW, 0.32h, (half)saturate(_LT_ShadowSoft));   // 부드럽게: 가운데는 진하게 두고 가장자리만 번지게
                half rawShadow = mainLight.shadowAttenuation;
                // 그림자 가장자리 번짐: 빛 방향에 수직인 원판 위 8곳에서 그림자를 더 읽어 평균 낸다 (반경 최대 ~0.3m).
                // 픽셀마다 원판을 돌려서 단계 무늬 대신 고운 번짐이 된다. 화면 공간 그림자 모드에서는 쓸 수 없어 건너뛴다.
                #if (defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)) && !defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                {
                    half ss = (half)saturate(_LT_ShadowSoft);
                    if (ss > 0.001h && mainLight.distanceAttenuation > 0.0h)
                    {
                        float3 Ld = mainLight.direction;
                        float3 t1 = normalize(cross(Ld, abs(Ld.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                        float3 t2 = cross(Ld, t1);
                        float ang = frac(sin(dot(i.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                        float ca = cos(ang), sa = sin(ang);
                        float radius = 0.28 * ss;
                        float acc = rawShadow;
                        const float2 disk[8] = { float2(0.95, 0.10), float2(-0.62, 0.70), float2(0.20, -0.92), float2(-0.85, -0.38),
                                                 float2(0.48, 0.52), float2(-0.25, 0.30), float2(0.30, -0.35), float2(-0.40, -0.10) };
                        [unroll] for (int k = 0; k < 8; k++)
                        {
                            float2 d = float2(disk[k].x * ca - disk[k].y * sa, disk[k].x * sa + disk[k].y * ca) * radius;
                            float3 pw = i.positionWS + t1 * d.x + t2 * d.y;
                            acc += MainLightRealtimeShadow(TransformWorldToShadowCoord(pw));
                        }
                        // 거리에 따른 그림자 페이드는 원래 값에서 가져온다
                        half fade = GetMainLightShadowFade(i.positionWS);
                        rawShadow = lerp((half)(acc / 9.0), 1.0h, fade);
                    }
                }
                #endif
                half shadow = smoothstep(0.5h - sharpW, 0.5h + sharpW, rawShadow);
                // 땅은 기울기만으로 새까맣게 꺼지지 않게 밑단을 올린다 (나무 그림자 같은 실제 그림자는 그대로)
                half bandL = Band(dot(Nl, mainLight.direction) * 0.5h + 0.5h);
                bandL = lerp(bandL, max(bandL, 0.6h), saturate(groundW * 2.0h));
                half lit = bandL * shadow * mainLight.distanceAttenuation;

                // 멀수록 면 하나하나의 밝기 차이를 줄여 디테일을 뭉갠다 (먼 산은 덩어리로만 읽힌다)
                float viewDist = distance(i.positionWS, _WorldSpaceCameraPos);
                half far = (half)saturate((viewDist - _LT_HazeParams.x) / max(_LT_HazeParams.y - _LT_HazeParams.x, 1.0));
                far = far * far * (3.0h - 2.0h * far) * (half)step(0.5, _LT_HazeParams.y);   // 값이 설정되지 않았으면(에디터) 끈다
                lit = lerp(lit, 0.45h + 0.4h * shadow, far * 0.6h);

                half3 ambient = SampleSH(Nl) * _AmbientStrength;
                half3 shadeTint = _ShadeColor.rgb;
                // 간접광: 하늘(위) ~ 땅(아래) 반구 빛 + 땅에서 튀어 오른 햇빛. 그림자 면이 방향에 따라 색이 달라져 입체감이 산다.
                half gi = (half)saturate(_LT_GI.x);
                if (gi > 0.001h)
                {
                    half3 giN = normalize(lerp(Nl, smoothN, 0.5h));   // 면마다 끊기지 않게 부드러운 노멀을 섞는다
                    half upT = giN.y * 0.5h + 0.5h;
                    half3 hemi = lerp((half3)_LT_GIGround.rgb, (half3)_LT_GISky.rgb, upT * upT * (3.0h - 2.0h * upT));
                    // 땅 반사광: 해가 높을수록 세고, 아래/옆을 보는 면일수록 많이 받는다 (트럭 아랫면, 바위 옆면, 나무 밑동)
                    half sunUp = saturate(mainLight.direction.y * 2.0h);
                    half3 bounce = mainLight.color * (half3)_LT_GIBounce.rgb * saturate(0.65h - giN.y) * sunUp;
                    ambient = lerp(ambient, (hemi + bounce) * _AmbientStrength, gi);
                    // 재질의 라벤더 그림자 색조를 밝기는 유지한 채 무채색 쪽으로 (낮에 그림자가 보라색이 되지 않게)
                    half shadeL = dot(shadeTint, half3(0.299h, 0.587h, 0.114h));
                    shadeTint = lerp(shadeTint, shadeL.xxx * 1.08h, (half)saturate(_LT_GI.y) * gi);
                }
                // 그림자 면도 빛 색을 조금 받아 새까맣게 꺼지지 않는다 (노을엔 주황빛, 밤엔 푸른빛 그림자)
                half night = saturate(_GameNightFactor);
                half weather = saturate(_GameWeatherFog);
                half3 shadeLight = (ambient + mainLight.color * _ShadowLift) * shadeTint;
                // 밤: 달빛이 안 닿는 곳은 더 어둡게, 달빛을 받는 면은 조금 더 밝게 (대비를 키워 달빛이 형태를 잡아 준다)
                // 안개가 끼면 달빛이 퍼져서 대비가 다시 줄어든다
                half moonContrast = night * (1.0h - weather);
                shadeLight *= lerp(1.0h, _NightShadowDarken, night);
                half3 litLight = ambient + mainLight.color * lerp(1.0h, _MoonLitBoost, moonContrast);
                // 시그니처 색: 해가 비치는 곳은 꿀빛(밤엔 달빛 색을 그대로), 그림자는 남보라로 깊게 (색이 있는 그림자)
                litLight *= lerp((half3)1.0h, lerp((half3)_LT_SigWarm.rgb, (half3)1.0h, night), sig);
                shadeLight *= lerp((half3)1.0h, (half3)_LT_SigCool.rgb, sig);
                // 깔끔한 툰 그림자: 그림자 면 = 밝은 면과 같은 색조를 비율만큼 어둡게 (rgb = 비율, a = 적용 세기).
                // 하늘빛 간접광을 섞으면 갈색 땅의 그림자가 푸르죽죽한 회보라가 되므로, 낮에는 "같은 색을 한 단계 어둡게"로 정한다 (밤은 a = 0 → 기존 분위기).
                half cleanW = (half)saturate(_LT_CleanShade.a);
                shadeLight = lerp(shadeLight, litLight * (half3)_LT_CleanShade.rgb, cleanW);
                shadeLight = lerp(shadeLight, litLight, (half)saturate(_LT_ShadeLift));   // 낮에는 그림자를 연하게 (밤엔 0)
                half3 light = lerp(shadeLight, litLight, lit);

                // 파스텔 룩: 명암 경계를 부드러운 그라데이션으로 (면 노멀과 부드러운 노멀을 섞음),
                // 그림자 면은 까맣게 꺼지는 대신 밝은 라벤더빛으로 들어 올린다. _LT_Pastel이 0이면 원래와 똑같다.
                half pastel = (half)saturate(_LT_Pastel);
                if (pastel > 0.001h)
                {
                    half3 softN = normalize(lerp(Nl, smoothN, 0.7h));
                    half softT = smoothstep(0.2h, 0.8h, dot(softN, mainLight.direction) * 0.5h + 0.5h);
                    half softLit = softT * lerp(0.4h, 1.0h, shadow) * mainLight.distanceAttenuation;
                    softLit = lerp(lit, softLit, 0.65h);
                    // 땅: 언덕 위에 번지는 부드러운 명암(얼룩) 대신 3단 툰 톤으로 끊는다. 경계가 또렷한 등고선 같은 면이 된다.
                    half gq = saturate(groundW * 2.0h);
                    half q3 = softLit * 3.0h;
                    half stepW = 0.06h + 0.30h * (half)saturate(_LT_ShadowSoft);
                    half stepped = (floor(q3) + smoothstep(0.5h - stepW, 0.5h + stepW, frac(q3))) / 3.0h;
                    softLit = lerp(softLit, min(stepped, 1.0h), gq);
                    half3 pastelShade = lerp(shadeLight, litLight * (half3)_LT_PastelShade.rgb, 0.3h);
                    light = lerp(light, lerp(pastelShade, litLight, softLit), pastel);
                }

                // 달빛 반짝임: 달 쪽을 향한 면(플랫 셰이딩이라 면 단위로)이 은은하게 빛을 되받는다
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 H = normalize(mainLight.direction + V);
                half sheenT = pow(saturate(dot(Nl, H)), 12.0h);
                half sheen = smoothstep(1.0h - _MoonSheenSize, 1.0h - _MoonSheenSize + 0.08h, sheenT);
                // 땅에는 얹지 않는다: 울퉁불퉁한 땅의 면마다 반짝임이 따로 걸려서 밤에 색종이를 뿌린 것처럼 보였다
                light += mainLight.color * sheen * lit * _MoonSheen * moonContrast * (1.0h - far) * (1.0h - groundW);

                // 추가 라이트(횃불, 헤드라이트 등)도 단계로 비춘다
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = Nl;
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
                        light += l.color * l.distanceAttenuation * l.shadowAttenuation * Band(dot(Nl, l.direction) * 0.5h + 0.5h);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                        // 거리 감쇠도 몇 단계로 끊어서 횃불 빛 웅덩이가 만화처럼 보이게
                        half atten = l.distanceAttenuation * l.shadowAttenuation;
                        half steppedAtten = floor(saturate(atten) * 4.0h + 0.5h) / 4.0h;
                        atten = lerp(atten, max(steppedAtten, atten * 0.5h), 0.6h);
                        light += l.color * atten * Band(dot(Nl, l.direction) * 0.5h + 0.5h);
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo * light;
                // 색이 있는 그림자: 그림자 면에 남보라 빛을 더한다. 곱셈 색조만으로는 노란 풀/갈색 땅 그림자가 칙칙한 회색이 되기 때문에, 재질 색과 상관없이 얹는다.
                color += (half3)_LT_SigFill.rgb * (1.0h - lit) * sig;

                // 깊이 팔레트: 가까운 곳은 원래 색, 멀어질수록 색조가 서서히 차갑게(노을엔 장밋빛) 옮겨 가서 먼 산이 겹겹이 다른 색의 층으로 읽힌다.
                // 곱셈 색조라 재질 고유의 채도/디테일은 남는다. 낮은 지형(골짜기/늪)에는 먼 곳일수록 옅은 안개가 고인다.
                half dep = (half)saturate(_LT_Depth);
                if (dep > 0.001h)
                {
                    half midD = (half)smoothstep(12.0, 150.0, viewDist);
                    half farD = (half)smoothstep(110.0, 460.0, viewDist);
                    // 중거리부터 색조가 옮겨 가고, 먼 곳은 한 번 더 (색이 층층이 달라 보인다)
                    color *= lerp((half3)1.0h, (half3)_LT_DepthTint.rgb, saturate(midD * 0.75h + farD * 0.6h) * dep);
                    // 안개는 낮은 곳일수록 진하고 높은 산 위로 갈수록 옅어진다 (먼 산 아래가 안개에 잠겨 윤곽이 겹겹이 나뉜다)
                    half mist = 1.0h - saturate((half)(i.positionWS.y - 1.0) / 95.0h);
                    half mistAmt = (farD * 0.78h + midD * 0.07h) * (0.25h + 0.75h * mist) * dep;
                    color = lerp(color, (half3)_LT_DepthMist.rgb, saturate(mistAmt));
                }

                // 공기 원근감: 멀수록 채도가 빠지고 하늘/안개 색으로 옅어진다
                half haze = far * (half)_LT_HazeParams.z;
                half lum = dot(color, half3(0.299h, 0.587h, 0.114h));
                color = lerp(color, lum.xxx, haze * (half)_LT_HazeParams.w);
                color = lerp(color, (half3)_LT_HazeColor.rgb, haze);
                // 가장자리 빛: 시선과 비스듬한 면(윤곽)을 밝혀서 배경과 색이 비슷해도 형태가 읽히게 한다 (밤엔 약하게)
                half rim = pow(1.0h - saturate(dot(smoothN, V)), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength * lerp(1.0h, 0.5h, night) * saturate(light.r + 0.35h);
                // 시그니처 가장자리 빛: 낮엔 호박색, 밤엔 청록색. 옆/아래를 보는 면(나무·바위·캐릭터 윤곽)에만 얹어서 형태가 색으로 읽히게 한다 (땅 위에는 안 얹는다)
                half sigRim = pow(1.0h - saturate(dot(smoothN, V)), 3.0h) * (1.0h - (half)smoothstep(0.55, 0.9, (float)N.y));
                color += (half3)_LT_SigRim.rgb * sigRim * sig * saturate(lit * 0.7h + 0.3h) * (1.0h - far);
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
