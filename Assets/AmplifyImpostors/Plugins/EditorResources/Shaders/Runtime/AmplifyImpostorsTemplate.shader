Shader /*ase_name*/ "Hidden/Impostors/Lit (Deprecated)"/*end*/
{
	Properties
	{
		/*ase_props*/
		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5
	}

	SubShader
	{
		/*ase_subshader_options:Name=Additional Options
			Option:Material Type,InvertActionOnDeselection:Standard,Specular Color:Specular Color
				Standard:ShowPort:Metallic
				Specular Color:ShowPort:Specular
				Specular Color:SetDefine:_SPECULAR_SETUP 1
			Port:ForwardBase:Alpha Clip Threshold
				On:SetDefine:_ALPHATEST_ON 1
			Option:Transmission:false,true:false
				false:RemoveDefine:ASE_TRANSMISSION 1
				false:HidePort:ForwardBase:Transmission
				false:HideOption:  Transmission Shadow
				true:SetDefine:ASE_TRANSMISSION 1
				true:ShowPort:ForwardBase:Transmission
				true:ShowOption:  Transmission Shadow
				true:SetOption:Deferred Pass,0
			Field:  Transmission Shadow:Float:0.5:0:1:_TransmissionShadow
				Change:SetMaterialProperty:_TransmissionShadow
				Change:SetShaderProperty:_TransmissionShadow,_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
				Inline,disable:SetShaderProperty:_TransmissionShadow,//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
			Option:Translucency:false,true:false
				false:RemoveDefine:ASE_TRANSLUCENCY 1
				false:HidePort:ForwardBase:Translucency
				false:HideOption:  Translucency Strength
				false:HideOption:  Normal Distortion
				false:HideOption:  Scattering
				false:HideOption:  Direct
				false:HideOption:  Ambient
				false:HideOption:  Shadow
				true:SetDefine:ASE_TRANSLUCENCY 1
				true:ShowPort:ForwardBase:Translucency
				true:ShowOption:  Translucency Strength
				true:ShowOption:  Normal Distortion
				true:ShowOption:  Scattering
				true:ShowOption:  Direct
				true:ShowOption:  Ambient
				true:ShowOption:  Shadow
				true:SetOption:Deferred Pass,0
			Field:  Translucency Strength:Float:1:0:50:_TransStrength
				Change:SetMaterialProperty:_TransStrength
				Change:SetShaderProperty:_TransStrength,_TransStrength( "Strength", Range( 0, 50 ) ) = 1
				Inline,disable:SetShaderProperty:_TransStrength,//_TransStrength( "Strength", Range( 0, 50 ) ) = 1
			Field:  Normal Distortion:Float:0.5:0:1:_TransNormal
				Change:SetMaterialProperty:_TransNormal
				Change:SetShaderProperty:_TransNormal,_TransNormal( "Normal Distortion", Range( 0, 1 ) ) = 0.5
				Inline,disable:SetShaderProperty:_TransNormal,//_TransNormal( "Normal Distortion", Range( 0, 1 ) ) = 0.5
			Field:  Scattering:Float:2:1:50:_TransScattering
				Change:SetMaterialProperty:_TransScattering
				Change:SetShaderProperty:_TransScattering,_TransScattering( "Scattering", Range( 1, 50 ) ) = 2
				Inline,disable:SetShaderProperty:_TransScattering,//_TransScattering( "Scattering", Range( 1, 50 ) ) = 2
			Field:  Direct:Float:0.9:0:1:_TransDirect
				Change:SetMaterialProperty:_TransDirect
				Change:SetShaderProperty:_TransDirect,_TransDirect( "Direct", Range( 0, 1 ) ) = 0.9
				Inline,disable:SetShaderProperty:_TransDirect,//_TransDirect( "Direct", Range( 0, 1 ) ) = 0.9
			Field:  Ambient:Float:0.1:0:1:_TransAmbient
				Change:SetMaterialProperty:_TransAmbient
				Change:SetShaderProperty:_TransAmbient,_TransAmbient( "Ambient", Range( 0, 1 ) ) = 0.1
				Inline,disable:SetShaderProperty:_TransAmbient,//_TransAmbient( "Ambient", Range( 0, 1 ) ) = 0.1
			Field:  Shadow:Float:0.5:0:1:_TransShadow
				Change:SetMaterialProperty:_TransShadow
				Change:SetShaderProperty:_TransShadow,_TransShadow( "Shadow", Range( 0, 1 ) ) = 0.5
				Inline,disable:SetShaderProperty:_TransShadow,//_TransShadow( "Shadow", Range( 0, 1 ) ) = 0.5
			Option:Cast Shadows:false,true:true
				true:IncludePass:ShadowCaster
				false,disable:ExcludePass:ShadowCaster
			Option:Deferred Pass:false,true:true
				true:IncludePass:Deferred
				false:ExcludePass:Deferred
			Option:Add Pass:false,true:true
				true:IncludePass:ForwardAdd
				false,disable:ExcludePass:ForwardAdd
			Option:Meta Pass:false,true:true
				true:IncludePass:Meta
				false,disable:ExcludePass:Meta

		*/
		CGINCLUDE
		#pragma target 3.0
		#define UNITY_SAMPLE_FULL_SH_PER_PIXEL 1
		ENDCG
		Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
		Cull Back
		AlphaToMask Off

		Pass
		{
			/*ase_pass_options:Name=Misc Options
			Port:Baked GI
				On:SetDefine:LIGHTMAP_ON 1
				On:SetDefine:DIRLIGHTMAP_COMBINED 1
				On:SetDefine:CUSTOM_BAKED_GI 1
			*/
			ZWrite On
			Name "ForwardBase"
			Tags { "LightMode"="ForwardBase" }

			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma multi_compile_fwdbase

			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile __ LOD_FADE_CROSSFADE
			#include "HLSLSupport.cginc"
			#if !defined( UNITY_INSTANCED_LOD_FADE )
				#define UNITY_INSTANCED_LOD_FADE
			#endif
			#if !defined( UNITY_INSTANCED_SH )
				#define UNITY_INSTANCED_SH
			#endif
			#if !defined( UNITY_INSTANCED_LIGHTMAPSTS )
				#define UNITY_INSTANCED_LIGHTMAPSTS
			#endif
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#ifndef UNITY_PASS_FORWARDBASE
			#define UNITY_PASS_FORWARDBASE
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityStandardUtils.cginc"

			/*ase_pragma*/

			/*ase_globals*/

			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif

			struct appdata
			{
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 texcoord3 : TEXCOORD3;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;t=t;n=n;uv0=tc0.xyzw;uv1=tc1.xyzw;uv2=tc2.xyzw;uv3=tc3.xyzw;c=c*/
			};

			struct v2f_surf
			{
				UNITY_POSITION(pos);
				float3 positionWS : TEXCOORD0;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					half3 sh : TEXCOORD1;
				#endif
				UNITY_LIGHTING_COORDS(2,3)
				UNITY_FOG_COORDS(4)
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(5,):sp=sp.xyzw;wp=tc0.xyz*/
			};

			v2f_surf vert_surf (appdata v /*ase_vert_input*/)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f_surf o;
				UNITY_INITIALIZE_OUTPUT(v2f_surf,o);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				/*ase_vert_code:v=appdata;o=v2f_surf*/

				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3;10;-1;_Vertex*/ float3(0,0,0) /*end*/;

				float3 positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
				half3 normalWS = UnityObjectToWorldNormal(v.normal);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.positionWS = positionWS;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					o.sh = 0;
					#ifdef VERTEXLIGHT_ON
					o.sh += Shade4PointLights (
						unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
						unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
						unity_4LightAtten0, positionWS, normalWS);
					#endif
					o.sh = ShadeSHPerVertex (normalWS, o.sh);
				#endif

				UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
				UNITY_TRANSFER_FOG(o,o.pos);
				return o;
			}

			half4 frag_surf (v2f_surf IN, out float outDepth : SV_Depth /*ase_frag_input*/) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				#if defined(_SPECULAR_SETUP)
					SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
				#else
					SurfaceOutputStandard o = (SurfaceOutputStandard)0;
				#endif

				/*ase_local_var:sp*/float4 positionCS = IN.pos;
				/*ase_local_var:wp*/float3 positionWS = IN.positionWS;

				/*ase_frag_code:IN=v2f_surf*/

				half3 albedo = /*ase_frag_out:Albedo;Float3;0;-1;_Albedo*/half3( 0, 0, 0 )/*end*/;
				half3 normal = /*ase_frag_out:Normal WS;Float3;1;-1;_Normal*/half3( 0, 0, 1 )/*end*/;
				half3 emission = /*ase_frag_out:Emission;Float3;2;-1;_Emission*/half3( 0, 0, 0 )/*end*/;
				half3 specular = /*ase_frag_out:Specular;Float3;3;-1;_Specular*/half3( 0, 0, 0 )/*end*/;
				half metallic = /*ase_frag_out:Metallic;Float;7;-1;_Metallic*/0/*end*/;
				half smoothness = /*ase_frag_out:Smoothness;Float;4;-1;_Smoothness*/0/*end*/;
				half occlusion = /*ase_frag_out:Occlusion;Float;5;-1;_Occlusion*/1/*end*/;
				float3 Transmission = /*ase_frag_out:Transmission;Float3;13;-1;_Transmission*/1/*end*/;
				float3 Translucency = /*ase_frag_out:Translucency;Float3;14;-1;_Translucency*/1/*end*/;
				half alpha = /*ase_frag_out:Alpha;Float;6;-1;_Alpha*/1/*end*/;
				half alphaClipThreshold = /*ase_frag_out:Alpha Clip Threshold;Float;9;-1;_AlphaClipThreshold*/0/*end*/;
				float4 bakedGI = /*ase_frag_out:Baked GI;Float4;8;-1;_BakedGI*/float4( 0, 0, 0, 0 )/*end*/;
				float depth = /*ase_frag_out:Depth;Float;15;-1;_Depth*/IN.pos.z/*end*/;

				outDepth = depth;

				o.Albedo = albedo;
				o.Normal = normal;
				o.Emission = emission;
				#if defined(_SPECULAR_SETUP)
					o.Specular = specular;
				#else
					o.Metallic = metallic;
				#endif
				o.Smoothness = smoothness;
				o.Occlusion = occlusion;
				o.Alpha = alpha;
				#if _ALPHATEST_ON
					clip( o.Alpha - alphaClipThreshold );
				#endif

				#ifndef USING_DIRECTIONAL_LIGHT
					half3 lightDir = normalize(UnityWorldSpaceLightDir(positionWS));
				#else
					half3 lightDir = _WorldSpaceLightPos0.xyz;
				#endif

				half3 worldViewDir = normalize(UnityWorldSpaceViewDir(positionWS));

				UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
				UNITY_LIGHT_ATTENUATION(atten, IN, positionWS)
				half4 c = 0;

				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
				gi.indirect.diffuse = 0;
				gi.indirect.specular = 0;
				gi.light.color = _LightColor0.rgb;
				gi.light.dir = lightDir;

				UnityGIInput giInput;
				UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
				giInput.light = gi.light;
				giInput.worldPos = positionWS;
				giInput.worldViewDir = worldViewDir;
				giInput.atten = atten;
				giInput.lightmapUV = 0.0;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					giInput.ambient = IN.sh;
				#else
					giInput.ambient.rgb = 0.0;
				#endif
				giInput.probeHDR[0] = unity_SpecCube0_HDR;
				giInput.probeHDR[1] = unity_SpecCube1_HDR;
				#if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
					giInput.boxMin[0] = unity_SpecCube0_BoxMin;
				#endif
				#if UNITY_SPECCUBE_BOX_PROJECTION
					giInput.boxMax[0] = unity_SpecCube0_BoxMax;
					giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
					giInput.boxMax[1] = unity_SpecCube1_BoxMax;
					giInput.boxMin[1] = unity_SpecCube1_BoxMin;
					giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
				#endif

				#if defined(_SPECULAR_SETUP)
					LightingStandardSpecular_GI(o, giInput, gi);
					#if defined(CUSTOM_BAKED_GI)
						gi.indirect.diffuse = DecodeLightmapRGBM( bakedGI, 1 ) * EMISSIVE_RGBM_SCALE;
					#endif
					c += LightingStandardSpecular (o, worldViewDir, gi);
				#else
					LightingStandard_GI( o, giInput, gi );
					#if defined(CUSTOM_BAKED_GI)
						gi.indirect.diffuse = DecodeLightmapRGBM( bakedGI, 1) * EMISSIVE_RGBM_SCALE;
					#endif
					c += LightingStandard( o, worldViewDir, gi );
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = /*ase_inline_begin*/_TransmissionShadow/*ase_inline_end*/;

					#ifdef DIRECTIONAL
						float3 lightAtten = lerp(_LightColor0.rgb, gi.light.color, shadow);
					#else
						float3 lightAtten = gi.light.color;
					#endif

					half3 transmission = max(0, -dot(o.Normal, gi.light.dir)) * lightAtten * Transmission;
					c.rgb += o.Albedo * transmission;
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = /*ase_inline_begin*/_TransShadow/*ase_inline_end*/;
					float normal = /*ase_inline_begin*/_TransNormal/*ase_inline_end*/;
					float scattering = /*ase_inline_begin*/_TransScattering/*ase_inline_end*/;
					float direct = /*ase_inline_begin*/_TransDirect/*ase_inline_end*/;
					float ambient = /*ase_inline_begin*/_TransAmbient/*ase_inline_end*/;
					float strength = /*ase_inline_begin*/_TransStrength/*ase_inline_end*/;

					#ifdef DIRECTIONAL
						float3 lightAtten = lerp(_LightColor0.rgb, gi.light.color, shadow);
					#else
						float3 lightAtten = gi.light.color;
					#endif

					half3 lightDir = gi.light.dir + o.Normal * normal;
					half transVdotL = pow(saturate(dot(worldViewDir, -lightDir)), scattering);
					half3 translucency = lightAtten * (transVdotL * direct + gi.indirect.diffuse * ambient) * Translucency;
					c.rgb += o.Albedo * translucency * strength;
				}
				#endif

				c.rgb += o.Emission;
				UNITY_APPLY_FOG(IN.fogCoord, c);
				return c;
			}

			ENDCG
		}

		Pass
		{
			/*ase_hide_pass*/
			Name "ForwardAdd"
			Tags { "LightMode"="ForwardAdd" }
			ZWrite Off
			Blend One One

			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma multi_compile_fwdadd_fullshadows

			#pragma multi_compile_fog
			#pragma multi_compile __ LOD_FADE_CROSSFADE
			#pragma multi_compile_instancing
			#include "HLSLSupport.cginc"
			#if !defined( UNITY_INSTANCED_LOD_FADE )
				#define UNITY_INSTANCED_LOD_FADE
			#endif
			#if !defined( UNITY_INSTANCED_SH )
				#define UNITY_INSTANCED_SH
			#endif
			#if !defined( UNITY_INSTANCED_LIGHTMAPSTS )
				#define UNITY_INSTANCED_LIGHTMAPSTS
			#endif
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#ifndef UNITY_PASS_FORWARDADD
			#define UNITY_PASS_FORWARDADD
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityStandardUtils.cginc"

			/*ase_pragma*/

			/*ase_globals*/

			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif

			struct appdata
			{
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 texcoord3 : TEXCOORD3;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;t=t;n=n;uv0=tc0.xyzw;uv1=tc1.xyzw;uv2=tc2.xyzw;uv3=tc3.xyzw;c=c*/
			};

			struct v2f_surf
			{
				UNITY_POSITION(pos);
				float3 positionWS : TEXCOORD0;
				UNITY_LIGHTING_COORDS(1,2)
				UNITY_FOG_COORDS(3)
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(4,):sp=sp.xyzw;wp=tc0.xyz*/
			};

			v2f_surf vert_surf ( appdata v /*ase_vert_input*/ )
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f_surf o;
				UNITY_INITIALIZE_OUTPUT(v2f_surf,o);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				/*ase_vert_code:v=appdata;o=v2f_surf*/

				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3;10;-1;_Vertex*/ float3(0,0,0) /*end*/;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;

				UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
				UNITY_TRANSFER_FOG(o,o.pos);
				return o;
			}

			half4 frag_surf (v2f_surf IN, out float outDepth : SV_Depth /*ase_frag_input*/) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				#if defined(_SPECULAR_SETUP)
					SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
				#else
					SurfaceOutputStandard o = (SurfaceOutputStandard)0;
				#endif

				/*ase_local_var:sp*/float4 positionCS = IN.pos;
				/*ase_local_var:wp*/float3 positionWS = IN.positionWS;

				/*ase_frag_code:IN=v2f_surf*/

				half3 albedo = /*ase_frag_out:Albedo;Float3;0;-1;_Albedo*/half3( 0, 0, 0 )/*end*/;
				half3 normal = /*ase_frag_out:Normal WS;Float3;1;-1;_Normal*/half3( 0, 0, 1 )/*end*/;
				half3 emission = /*ase_frag_out:Emission;Float3;2;-1;_Emission*/half3( 0, 0, 0 )/*end*/;
				half3 specular = /*ase_frag_out:Specular;Float3;3;-1;_Specular*/half3( 0, 0, 0 )/*end*/;
				half metallic = /*ase_frag_out:Metallic;Float;7;-1;_Metallic*/0/*end*/;
				half smoothness = /*ase_frag_out:Smoothness;Float;4;-1;_Smoothness*/0/*end*/;
				half occlusion = /*ase_frag_out:Occlusion;Float;5;-1;_Occlusion*/1/*end*/;
				float3 Transmission = /*ase_frag_out:Transmission;Float3;13;-1;_Transmission*/1/*end*/;
				float3 Translucency = /*ase_frag_out:Translucency;Float3;14;-1;_Translucency*/1/*end*/;
				half alpha = /*ase_frag_out:Alpha;Float;6;-1;_Alpha*/1/*end*/;
				half alphaClipThreshold = /*ase_frag_out:Alpha Clip Threshold;Float;9;-1;_AlphaClipThreshold*/0/*end*/;
				float depth = /*ase_frag_out:Depth;Float;15;-1;_Depth*/IN.pos.z/*end*/;

				outDepth = depth;

				o.Albedo = albedo;
				o.Normal = normal;
				o.Emission = emission;
				#if defined(_SPECULAR_SETUP)
					o.Specular = specular;
				#else
					o.Metallic = metallic;
				#endif
				o.Smoothness = smoothness;
				o.Occlusion = occlusion;
				o.Alpha = alpha;
				#if _ALPHATEST_ON
					clip( o.Alpha - alphaClipThreshold );
				#endif

				#ifndef USING_DIRECTIONAL_LIGHT
					half3 lightDir = normalize(UnityWorldSpaceLightDir(positionWS));
				#else
					half3 lightDir = _WorldSpaceLightPos0.xyz;
				#endif

				half3 worldViewDir = normalize(UnityWorldSpaceViewDir(positionWS));

				UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
				UNITY_LIGHT_ATTENUATION(atten, IN, positionWS)
				half4 c = 0;

				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
				gi.indirect.diffuse = 0;
				gi.indirect.specular = 0;
				gi.light.color = _LightColor0.rgb;
				gi.light.dir = lightDir;
				gi.light.color *= atten;
				#if defined(_SPECULAR_SETUP)
					c += LightingStandardSpecular( o, worldViewDir, gi );
				#else
					c += LightingStandard( o, worldViewDir, gi );
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = /*ase_inline_begin*/_TransmissionShadow/*ase_inline_end*/;

					#ifdef DIRECTIONAL
						float3 lightAtten = lerp(_LightColor0.rgb, gi.light.color, shadow);
					#else
						float3 lightAtten = gi.light.color;
					#endif

					half3 transmission = max(0, -dot(o.Normal, gi.light.dir)) * lightAtten * Transmission;
					c.rgb += o.Albedo * transmission;
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = /*ase_inline_begin*/_TransShadow/*ase_inline_end*/;
					float normal = /*ase_inline_begin*/_TransNormal/*ase_inline_end*/;
					float scattering = /*ase_inline_begin*/_TransScattering/*ase_inline_end*/;
					float direct = /*ase_inline_begin*/_TransDirect/*ase_inline_end*/;
					float ambient = /*ase_inline_begin*/_TransAmbient/*ase_inline_end*/;
					float strength = /*ase_inline_begin*/_TransStrength/*ase_inline_end*/;

					#ifdef DIRECTIONAL
						float3 lightAtten = lerp(_LightColor0.rgb, gi.light.color, shadow);
					#else
						float3 lightAtten = gi.light.color;
					#endif

					half3 lightDir = gi.light.dir + o.Normal * normal;
					half transVdotL = pow(saturate(dot(worldViewDir, -lightDir)), scattering);
					half3 translucency = lightAtten * (transVdotL * direct + gi.indirect.diffuse * ambient) * Translucency;
					c.rgb += o.Albedo * translucency * strength;
				}
				#endif

				UNITY_APPLY_FOG(IN.fogCoord, c);
				return c;
			}
			ENDCG
		}

		Pass
		{
			/*ase_hide_pass*/
			/*ase_pass_options:Name=Misc Options
			Port:Baked GI
				On:SetDefine:LIGHTMAP_ON 1
				On:SetDefine:DIRLIGHTMAP_COMBINED 1
				On:SetDefine:CUSTOM_BAKED_GI 1
			*/

			Name "Deferred"
			Tags { "LightMode"="Deferred" }

			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma multi_compile_instancing
			#pragma multi_compile __ LOD_FADE_CROSSFADE
			#pragma exclude_renderers nomrt
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#pragma multi_compile_prepassfinal
			#include "HLSLSupport.cginc"
			#if !defined( UNITY_INSTANCED_LOD_FADE )
				#define UNITY_INSTANCED_LOD_FADE
			#endif
			#if !defined( UNITY_INSTANCED_SH )
				#define UNITY_INSTANCED_SH
			#endif
			#if !defined( UNITY_INSTANCED_LIGHTMAPSTS )
				#define UNITY_INSTANCED_LIGHTMAPSTS
			#endif
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#ifndef UNITY_PASS_DEFERRED
			#define UNITY_PASS_DEFERRED
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			#include "UnityStandardUtils.cginc"

			#ifdef LIGHTMAP_ON
			float4 unity_LightmapFade;
			#endif
			half4 unity_Ambient;

			/*ase_pragma*/

			/*ase_globals*/

			struct appdata
			{
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 texcoord3 : TEXCOORD3;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;t=t;n=n;uv0=tc0.xyzw;uv1=tc1.xyzw;uv2=tc2.xyzw;uv3=tc3.xyzw;c=c*/
			};

			struct v2f_surf
			{
				UNITY_POSITION(pos);
				float3 positionWS : TEXCOORD0;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					half3 sh : TEXCOORD1;
				#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(2,):sp=sp.xyzw;wp=tc0.xyz*/
			};

			v2f_surf vert_surf (appdata v /*ase_vert_input*/ )
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f_surf o;
				UNITY_INITIALIZE_OUTPUT(v2f_surf,o);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				/*ase_vert_code:v=appdata;o=v2f_surf*/

				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3;10;-1;_Vertex*/ float3(0,0,0) /*end*/;

				half3 normalWS = UnityObjectToWorldNormal(v.normal);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					o.sh = 0;
					o.sh = ShadeSHPerVertex (normalWS, o.sh);
				#endif
				return o;
			}

			void frag_surf (v2f_surf IN /*ase_frag_input*/, out half4 outGBuffer0 : SV_Target0, out half4 outGBuffer1 : SV_Target1, out half4 outGBuffer2 : SV_Target2, out half4 outEmission : SV_Target3
			#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
				, out half4 outShadowMask : SV_Target4
			#endif
			, out float outDepth : SV_Depth)
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				#if defined(_SPECULAR_SETUP)
					SurfaceOutputStandardSpecular o = (SurfaceOutputStandardSpecular)0;
				#else
					SurfaceOutputStandard o = (SurfaceOutputStandard)0;
				#endif

				/*ase_local_var:sp*/float4 positionCS = IN.pos;
				/*ase_local_var:wp*/float3 positionWS = IN.positionWS;

				/*ase_frag_code:IN=v2f_surf*/

				half3 albedo = /*ase_frag_out:Albedo;Float3;0;-1;_Albedo*/half3( 0, 0, 0 )/*end*/;
				half3 normal = /*ase_frag_out:Normal WS;Float3;1;-1;_Normal*/half3( 0, 0, 1 )/*end*/;
				half3 emission = /*ase_frag_out:Emission;Float3;2;-1;_Emission*/half3( 0, 0, 0 )/*end*/;
				half3 specular = /*ase_frag_out:Specular;Float3;3;-1;_Specular*/half3( 0, 0, 0 )/*end*/;
				half metallic = /*ase_frag_out:Metallic;Float;7;-1;_Metallic*/0/*end*/;
				half smoothness = /*ase_frag_out:Smoothness;Float;4;-1;_Smoothness*/0/*end*/;
				half occlusion = /*ase_frag_out:Occlusion;Float;5;-1;_Occlusion*/1/*end*/;
				half alpha = /*ase_frag_out:Alpha;Float;6;-1;_Alpha*/1/*end*/;
				half alphaClipThreshold = /*ase_frag_out:Alpha Clip Threshold;Float;9;-1;_AlphaClipThreshold*/0/*end*/;
				float4 bakedGI = /*ase_frag_out:Baked GI;Float4;8;-1;_BakedGI*/float4( 0, 0, 0, 0 )/*end*/;
				float depth = /*ase_frag_out:Depth;Float;15;-1;_Depth*/IN.pos.z/*end*/;

				outDepth = depth;

				o.Albedo = albedo;
				o.Normal = normal;
				o.Emission = emission;
				#if defined(_SPECULAR_SETUP)
					o.Specular = specular;
				#else
					o.Metallic = metallic;
				#endif
				o.Smoothness = smoothness;
				o.Occlusion = occlusion;
				o.Alpha = alpha;
				#if _ALPHATEST_ON
					clip( o.Alpha - alphaClipThreshold );
				#endif

				#ifndef USING_DIRECTIONAL_LIGHT
					half3 lightDir = normalize(UnityWorldSpaceLightDir(positionWS));
				#else
					half3 lightDir = _WorldSpaceLightPos0.xyz;
				#endif

				half3 worldViewDir = normalize(UnityWorldSpaceViewDir(positionWS));

				UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
				half atten = 1;

				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
				gi.indirect.diffuse = 0;
				gi.indirect.specular = 0;
				gi.light.color = 0;
				gi.light.dir = half3(0,1,0);

				UnityGIInput giInput;
				UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
				giInput.light = gi.light;
				giInput.worldPos = positionWS;
				giInput.worldViewDir = worldViewDir;
				giInput.atten = atten;
				giInput.lightmapUV = 0.0;
				#if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
					giInput.ambient = IN.sh;
				#else
					giInput.ambient.rgb = 0.0;
				#endif
				giInput.probeHDR[0] = unity_SpecCube0_HDR;
				giInput.probeHDR[1] = unity_SpecCube1_HDR;
				#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
					giInput.boxMin[0] = unity_SpecCube0_BoxMin;
				#endif
				#ifdef UNITY_SPECCUBE_BOX_PROJECTION
					giInput.boxMax[0] = unity_SpecCube0_BoxMax;
					giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
					giInput.boxMax[1] = unity_SpecCube1_BoxMax;
					giInput.boxMin[1] = unity_SpecCube1_BoxMin;
					giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
				#endif

				#if defined(_SPECULAR_SETUP)
					LightingStandardSpecular_GI( o, giInput, gi );
					#if defined(CUSTOM_BAKED_GI)
						gi.indirect.diffuse = DecodeLightmapRGBM( bakedGI, 1 ) * EMISSIVE_RGBM_SCALE;
					#endif
					outEmission = LightingStandardSpecular_Deferred( o, worldViewDir, gi, outGBuffer0, outGBuffer1, outGBuffer2 );
				#else
					LightingStandard_GI( o, giInput, gi );
					#if defined(CUSTOM_BAKED_GI)
						gi.indirect.diffuse = DecodeLightmapRGBM( bakedGI, 1 ) * EMISSIVE_RGBM_SCALE;
					#endif
					outEmission = LightingStandard_Deferred( o, worldViewDir, gi, outGBuffer0, outGBuffer1, outGBuffer2 );
				#endif

				#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
					outShadowMask = half4(1.0, 1.0, 1.0, 1.0);
				#endif
				#ifndef UNITY_HDR_ON
					outEmission.rgb = exp2(-outEmission.rgb);
				#endif
			}
			ENDCG
		}

		Pass
		{
			/*ase_hide_pass*/
			Name "Meta"
			Tags { "LightMode"="Meta" }
			Cull Off

			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#pragma multi_compile_instancing
			#pragma shader_feature EDITOR_VISUALIZATION
			#include "HLSLSupport.cginc"
			#if !defined( UNITY_INSTANCED_LOD_FADE )
				#define UNITY_INSTANCED_LOD_FADE
			#endif
			#if !defined( UNITY_INSTANCED_SH )
				#define UNITY_INSTANCED_SH
			#endif
			#if !defined( UNITY_INSTANCED_LIGHTMAPSTS )
				#define UNITY_INSTANCED_LIGHTMAPSTS
			#endif
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#ifndef UNITY_PASS_META
			#define UNITY_PASS_META
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			#include "UnityStandardUtils.cginc"
			#include "UnityMetaPass.cginc"

			/*ase_pragma*/

			/*ase_globals*/

			struct appdata
			{
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 texcoord3 : TEXCOORD3;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;t=t;n=n;uv0=tc0.xyzw;uv1=tc1.xyzw;uv2=tc2.xyzw;uv3=tc3.xyzw;c=c*/
			};

			struct v2f_surf
			{
				UNITY_POSITION(pos);
				float3 positionWS : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(1,):sp=sp.xyzw;wp=tc0.xyz*/
			};

			v2f_surf vert_surf (appdata v /*ase_vert_input*/ )
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f_surf o;
				UNITY_INITIALIZE_OUTPUT(v2f_surf,o);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				/*ase_vert_code:v=appdata;o=v2f_surf*/

				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3;10;-1;_Vertex*/ float3(0,0,0) /*end*/;

				o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST);
				o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
				return o;
			}

			half4 frag_surf (v2f_surf IN /*ase_frag_input*/ ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);

				/*ase_local_var:sp*/float4 positionCS = IN.pos;
				/*ase_local_var:wp*/float3 positionWS = IN.positionWS;

				/*ase_frag_code:IN=v2f_surf*/

				half3 albedo = /*ase_frag_out:Albedo;Float3;0;-1;_Albedo*/half3( 0, 0, 0 )/*end*/;
				half3 emission = /*ase_frag_out:Emission;Float3;2;-1;_Emission*/half3( 0, 0, 0 )/*end*/;
				half3 specular = /*ase_frag_out:Specular;Float3;3;-1;_Specular*/half3( 0, 0, 0 )/*end*/;
				half metallic = /*ase_frag_out:Metallic;Float;7;-1;_Metallic*/0/*end*/;
				half alpha = /*ase_frag_out:Alpha;Float;6;-1;_Alpha*/1/*end*/;
				half alphaClipThreshold = /*ase_frag_out:Alpha Clip Threshold;Float;9;-1;_AlphaClipThreshold*/0/*end*/;

				#if _ALPHATEST_ON
					clip( alpha - alphaClipThreshold );
				#endif

				half3 diffColor;
				half3 specColor;
				half oneMinusReflectivity;

				#if defined(_SPECULAR_SETUP)
					diffColor = DiffuseAndSpecularFromMetallic( albedo, metallic, /*out*/ specColor, /*out*/ oneMinusReflectivity );
				#else
					diffColor = EnergyConservationBetweenDiffuseAndSpecular( albedo, specular, /*out*/ oneMinusReflectivity );
				#endif

				UnityMetaInput metaIN;
				UNITY_INITIALIZE_OUTPUT(UnityMetaInput, metaIN);
				metaIN.Albedo = diffColor;
				metaIN.SpecularColor = specColor;
				metaIN.Emission = emission;
				return UnityMetaFragment(metaIN);
			}
			ENDCG
		}

		Pass
		{
			/*ase_hide_pass*/
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			ZWrite On

			CGPROGRAM
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma multi_compile_shadowcaster
			#pragma multi_compile __ LOD_FADE_CROSSFADE
			#ifndef UNITY_PASS_SHADOWCASTER
			#define UNITY_PASS_SHADOWCASTER
			#endif
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#pragma multi_compile_instancing
			#include "HLSLSupport.cginc"
			#if !defined( UNITY_INSTANCED_LOD_FADE )
				#define UNITY_INSTANCED_LOD_FADE
			#endif
			#include "UnityShaderVariables.cginc"
			#include "UnityShaderUtilities.cginc"
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			#include "UnityStandardUtils.cginc"

			/*ase_pragma*/

			/*ase_globals*/

			struct appdata
			{
				float4 vertex : POSITION;
				float4 tangent : TANGENT;
				float3 normal : NORMAL;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 texcoord3 : TEXCOORD3;
				half4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				/*ase_vdata:p=p;t=t;n=n;uv0=tc0.xyzw;uv1=tc1.xyzw;uv2=tc2.xyzw;uv3=tc3.xyzw;c=c*/
			};

			struct v2f_surf
			{
				UNITY_POSITION(pos);
				float3 positionWS : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				/*ase_interp(1,):sp=sp.xyzw;wp=tc0.xyz*/
			};

			v2f_surf vert_surf (appdata v /*ase_vert_input*/ )
			{
				UNITY_SETUP_INSTANCE_ID(v);
				v2f_surf o;
				UNITY_INITIALIZE_OUTPUT(v2f_surf,o);
				UNITY_TRANSFER_INSTANCE_ID(v,o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				/*ase_vert_code:v=appdata;o=v2f_surf*/

				v.vertex.xyz += /*ase_vert_out:Local Vertex;Float3;10;-1;_Vertex*/ float3(0,0,0) /*end*/;

				// Disable "Normal Bias" because we're rendering billboard impostors and there's no vertex normals.
				unity_LightShadowBias.z = 0;

				TRANSFER_SHADOW_CASTER_NOPOS(o,o.pos);
				o.positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
				return o;
			}

			half4 frag_surf (v2f_surf IN, out float outDepth : SV_Depth /*ase_frag_input*/) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);

				/*ase_local_var:sp*/float4 positionCS = IN.pos;
				/*ase_local_var:wp*/float3 positionWS = IN.positionWS;

				/*ase_frag_code:IN=v2f_surf*/

				half alpha = /*ase_frag_out:Alpha;Float;6;-1;_Alpha*/1/*end*/;
				half alphaClipThreshold = /*ase_frag_out:Alpha Clip Threshold;Float;9;-1;_AlphaClipThreshold*/0/*end*/;
				float depth = /*ase_frag_out:Depth;Float;15;-1;_Depth*/IN.pos.z/*end*/;

				outDepth = depth;

				#if _ALPHATEST_ON
					clip( alpha - alphaClipThreshold );
				#endif

				UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
				SHADOW_CASTER_FRAGMENT(IN)
			}
			ENDCG
		}
		/*ase_pass_end*/
	}
}
