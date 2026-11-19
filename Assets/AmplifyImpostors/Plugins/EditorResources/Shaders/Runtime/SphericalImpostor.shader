// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Impostors/Spherical Impostor"
{
	Properties
	{
		[NoScaleOffset] _Albedo( "Albedo (RGB) Alpha (A)", 2D ) = "white" {}
		[NoScaleOffset] _Normals( "Normals (RGB) Depth (A)", 2D ) = "white" {}
		[NoScaleOffset] _Specular( "Specular (RGB) Smoothness (A)", 2D ) = "black" {}
		[NoScaleOffset] _Occlusion( "Occlusion (RGB)", 2D ) = "white" {}
		[NoScaleOffset] _Emission( "Emission (RGB)", 2D ) = "black" {}
		[NoScaleOffset] _Position( "Position (RGB)", 2D ) = "black" {}
		_ClipMask( "Clip", Range( 0, 1 ) ) = 0.5
		_TextureBias( "Texture Bias", Float ) = -1
		[Toggle( _USE_PARALLAX_ON )] _Use_Parallax( "Use Parallax", Float ) = 0
		_Parallax( "Parallax", Range( -1, 1 ) ) = 1
		_AI_ShadowBias( "Shadow Bias", Range( 0, 2 ) ) = 0.333
		_AI_ShadowView( "Shadow View", Range( 0, 1 ) ) = 1
		_AI_ForwardBias( "Forward Bias", Range( 0, 2 ) ) = 0.0
		[Toggle( EFFECT_HUE_VARIATION )] _Hue( "Use SpeedTree Hue", Float ) = 0
		_HueVariation( "Hue Variation", Color ) = ( 0, 0, 0, 0 )
		[Toggle] _AI_AlphaToCoverage( "Alpha To Coverage", Float ) = 0
		[HideInInspector] _FramesX( "Frames X", Float ) = 16
		[HideInInspector] _FramesY( "Frames Y", Float ) = 16
		[HideInInspector] _AI_AngleRange( "Angle Range", Vector ) = ( 360, 180, 0, 0 )
		[HideInInspector] _DepthSize( "DepthSize", Float ) = 1
		[HideInInspector] _ImpostorSize( "Impostor Size", Float ) = 1
		[HideInInspector] _Offset( "Offset", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_SizeOffset( "Size & Offset", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_BoundsMin( "Bounds Min", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_BoundsSize( "Bounds Size", Vector ) = ( 0, 0, 0, 0 )
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }

		Cull Back
		AlphaToMask [_AI_AlphaToCoverage]

		CGINCLUDE
			#pragma target 4.5
			#define UNITY_SAMPLE_FULL_SH_PER_PIXEL 1

			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION

			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
		ENDCG

		Pass
		{
			Name "ForwardBase"
			Tags { "LightMode"="ForwardBase" }

			ZWrite On

			CGPROGRAM
				// compile directives
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fog
				#pragma multi_compile_fwdbase
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

				#include "AmplifyImpostors.cginc"

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_LIGHTING_COORDS(2,3)
					UNITY_FOG_COORDS(4)
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					o.pos = UnityObjectToClipPos(v.vertex);
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );

					float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					half3 worldNormal = UnityObjectToWorldNormal(v.normal);

					UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
					UNITY_TRANSFER_FOG(o,o.pos);
					return o;
				}

				half4 frag( v2f IN, out float outDepth : AI_DEPTH_SEMANTIC ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					#ifndef USING_DIRECTIONAL_LIGHT
						half3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
					#else
						half3 lightDir = _WorldSpaceLightPos0.xyz;
					#endif

					half3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

					UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
					UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)
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
					giInput.worldPos = worldPos;
					giInput.worldViewDir = worldViewDir;
					giInput.atten = atten;

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

					LightingStandardSpecular_GI(o, giInput, gi);

					c += LightingStandardSpecular (o, worldViewDir, gi);
					c.rgb += o.Emission;

					UNITY_APPLY_FOG(IN.fogCoord, c);
					return c;
				}
			ENDCG
		}

		Pass
		{
			Name "ForwardAdd"
			Tags { "LightMode"="ForwardAdd" }

			ZWrite Off
			Blend One One

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_fog
				#pragma multi_compile_instancing
				#pragma multi_compile_fwdadd_fullshadows
				#pragma multi_compile __ LOD_FADE_CROSSFADE
				#pragma skip_variants INSTANCING_ON
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

				#include "AmplifyImpostors.cginc"

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_LIGHTING_COORDS(2,3)
					UNITY_FOG_COORDS(4)
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					o.pos = UnityObjectToClipPos(v.vertex);
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );

					UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
					UNITY_TRANSFER_FOG(o,o.pos);
					return o;
				}

				half4 frag( v2f IN, out float outDepth : AI_DEPTH_SEMANTIC ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					#ifndef USING_DIRECTIONAL_LIGHT
						half3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
					#else
						half3 lightDir = _WorldSpaceLightPos0.xyz;
					#endif

					half3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

					UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
					UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)
					half4 c = 0;

					UnityGI gi;
					UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
					gi.indirect.diffuse = 0;
					gi.indirect.specular = 0;
					gi.light.color = _LightColor0.rgb;
					gi.light.dir = lightDir;
					gi.light.color *= atten;
					c += LightingStandardSpecular (o, worldViewDir, gi);

					UNITY_APPLY_FOG(IN.fogCoord, c);
					return c;
				}
			ENDCG
		}

		Pass
		{
			Name "Deferred"
			Tags { "LightMode"="Deferred" }

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
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

				#include "AmplifyImpostors.cginc"

				half4 unity_Ambient;

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					o.pos = UnityObjectToClipPos(v.vertex);
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );

					float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
					half3 worldNormal = UnityObjectToWorldNormal(v.normal);
					float3 viewDirForLight = UnityWorldSpaceViewDir(worldPos);

					return o;
				}

				void frag( v2f IN,
					out half4 outGBuffer0 : SV_Target0,
					out half4 outGBuffer1 : SV_Target1,
					out half4 outGBuffer2 : SV_Target2,
					out half4 outEmission : SV_Target3,
					out float outDepth : AI_DEPTH_SEMANTIC )
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					#ifndef USING_DIRECTIONAL_LIGHT
						half3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
					#else
						half3 lightDir = _WorldSpaceLightPos0.xyz;
					#endif

					half3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

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
					giInput.worldPos = worldPos;
					giInput.worldViewDir = worldViewDir;
					giInput.atten = atten;

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
					LightingStandardSpecular_GI(o, giInput, gi);

					outEmission = LightingStandardSpecular_Deferred (o, worldViewDir, gi, outGBuffer0, outGBuffer1, outGBuffer2);

					#ifndef UNITY_HDR_ON
						outEmission.rgb = exp2(-outEmission.rgb);
					#endif
				}
			ENDCG
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
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

				#include "AmplifyImpostors.cginc"

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					V2F_SHADOW_CASTER_NOPOS
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					TRANSFER_SHADOW_CASTER(o)
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );
					return o;
				}

				half4 frag( v2f IN, out float outDepth : AI_DEPTH_SEMANTIC ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
					SHADOW_CASTER_FRAGMENT(IN)
				}
			ENDCG
		}

		Pass
		{
			Name "SceneSelectionPass"
			Tags{ "LightMode" = "SceneSelectionPass" }

			ZWrite On

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __ LOD_FADE_CROSSFADE
				#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
				#pragma multi_compile_instancing
				#define SCENESELECTIONPASS
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

				#include "AmplifyImpostors.cginc"

				int _ObjectId;
				int _PassValue;

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					o.pos = UnityObjectToClipPos(v.vertex);
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );
					return o;
				}

				half4 frag( v2f IN, out float outDepth : AI_DEPTH_SEMANTIC ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
					return float4( _ObjectId, _PassValue, 1.0, 1.0 );
				}
			ENDCG
		}

		Pass
		{
			Name "ScenePickingPass"
			Tags { "LightMode" = "Picking" }

			ZWrite On

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __ LOD_FADE_CROSSFADE
				#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
				#pragma multi_compile_instancing
				#define SCENEPICKINGPASS
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

				#include "AmplifyImpostors.cginc"

				float4 _SelectionID;

				struct v2f
				{
					AI_POSITION_QUALIFIERS UNITY_POSITION(pos);
					float4 frameUVs : TEXCOORD0;
					float4 viewPos : TEXCOORD1;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert( appdata_full v )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					SphereImpostorVertex( v.vertex, v.normal, o.frameUVs, o.viewPos );

					o.pos = UnityObjectToClipPos(v.vertex);
					o.pos = ImpostorClipPosDepthBias( o.pos, o.viewPos );
					return o;
				}

				half4 frag( v2f IN, out float outDepth : AI_DEPTH_SEMANTIC ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(IN);
					SurfaceOutputStandardSpecular o;
					UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandardSpecular, o );

					float4 clipPos;
					float3 worldPos;
					SphereImpostorFragment( o, clipPos, worldPos, IN.frameUVs, IN.viewPos );
					IN.pos.zw = clipPos.zw;

					outDepth = IN.pos.z;

					UNITY_APPLY_DITHER_CROSSFADE(IN.pos.xy);
					return _SelectionID;
				}
			ENDCG
		}
	}

	Fallback Off
}