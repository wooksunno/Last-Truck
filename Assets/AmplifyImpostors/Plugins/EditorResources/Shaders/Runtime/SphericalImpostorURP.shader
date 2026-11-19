// Made with Amplify Shader Editor v1.9.9.14
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Hidden/Amplify Impostors/Spherical Impostor URP"
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
		_AI_ForwardBias( "Forward Bias", Range( 0, 2 ) ) = 0
		[Toggle( EFFECT_HUE_VARIATION )] _Hue( "Use SpeedTree Hue", Float ) = 0
		_HueVariation( "Hue Variation", Color ) = ( 0, 0, 0, 0 )
		[Toggle] _AI_AlphaToCoverage( "Alpha To Coverage", Float ) = 0
		[HideInInspector] _FramesX( "Frames X", Float ) = 16
		[HideInInspector] _FramesY( "Frames Y", Float ) = 16
		[HideInInspector] _DepthSize( "DepthSize", Float ) = 1
		[HideInInspector] _ImpostorSize( "Impostor Size", Float ) = 1
		[HideInInspector] _Offset( "Offset", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_SizeOffset( "Size & Offset", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_BoundsMin( "Bounds Min", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_BoundsSize( "Bounds Size", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _AI_AngleRange( "Angle Range", Vector ) = ( 360, 180, 0, 0 )


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5

		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		//_InstancedTerrainNormals("Instanced Terrain Normals", Float) = 1.0

		[ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
		[ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0
		[HideInInspector][ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
		//[HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1

		//[HideInInspector] _AlphaClip("__clip", Float) = 0.0
	}

	SubShader
	{
		PackageRequirements
		{
			"com.unity.render-pipelines.universal": "[17.0,18.0]"
		}

		

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "ImpostorType"="Spherical" }

	LOD 0

		Cull Back
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		AlphaToMask [_AI_AlphaToCoverage]

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#define ASE_ADJUST_CLIP_POSITION( x ) x

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
			#endif

			#if defined(UNITY_PLATFORM_META_QUEST) && ( UNITY_VERSION >= 60050000 )
            #pragma multi_compile _ META_QUEST_LIGHTUNROLL
            #endif

			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
			#endif
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#else
			#pragma multi_compile _ _FORWARD_PLUS
			#endif

            #if defined(UNITY_PLATFORM_META_QUEST) && ( UNITY_VERSION >= 60050000 )
            #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #pragma multi_compile _ META_QUEST_NO_SPOTLIGHTS_LIGHT_LOOP
            #endif

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
			#endif
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
			#endif
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#if ( UNITY_VERSION >= 60010000 )
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
			#else
			#pragma multi_compile_fog
			#endif
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			#if ( UNITY_VERSION < 60010000 )
				#define USE_CLUSTER_LIGHT_LOOP USE_FORWARD_PLUS
				#define CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 frameUVs117 : TEXCOORD7;
				float4 viewPos117 : TEXCOORD8;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
					// Declared so the Motion Vector output port surfaces on the master node; only consumed by the motion vector passes.
					float3 aseCustomMotionVector = float3(0, 0, 0);
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						output.fogFactorAndVertexLight.x = ComputeFogFactor(vertexInput.positionCS.z);
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag ( PackedVaryings input
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						#if ( UNITY_VERSION >= 60020000 )
						, out uint outRenderingLayers : SV_Target1
						#else
						, out float4 outRenderingLayers : SV_Target1
						#endif
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined( _SURFACE_TYPE_TRANSPARENT )
					const bool isTransparent = true;
				#else
					const bool isTransparent = false;
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float3 BaseColor = io.Albedo;
				float3 Normal = io.WorldNormal;
				float3 Specular = io.Specular;
				float Metallic = 0;
				float Smoothness = io.Smoothness;
				float Occlusion = io.Occlusion;
				float3 Emission = io.Emission;
				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
					float AlphaClipThresholdShadow = 0.5;
				#endif
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = input.positionCS;
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.viewDirectionWS = ViewDirWS;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = NormalWS;
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(_SCREEN_SPACE_IRRADIANCE) && ( UNITY_VERSION >= 60030000 )
					#if ( UNITY_VERSION >= 60060000 )
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS));
					#else
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
					#endif
				#elif defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI( SH, GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask );
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
					#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
				#endif

				#ifdef ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_CLUSTER_LIGHT_LOOP
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_CLUSTER_LIGHT_LOOP
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( NormalWS,0 ) ).xyz * ( 1.0 - dot( NormalWS, ViewDirWS ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3(0,0,0), inputData.fogCoord);
					#else
						color.rgb = MixFog(color.rgb, inputData.fogCoord);
					#endif
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					#if ( UNITY_VERSION >= 60020000 )
					outRenderingLayers = EncodeMeshRenderingLayer();
					#else
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
					#endif
				#endif

				#if defined( ASE_OPAQUE_KEEP_ALPHA )
					return half4( color.rgb, color.a );
				#else
					return half4( color.rgb, OutputAlpha( color.a, isTransparent ) );
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off
			ColorMask 0

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW // @diogo: removed _vertex for POM node

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 frameUVs117 : TEXCOORD1;
				float4 viewPos117 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			float3 _LightDirection;
			float3 _LightPosition;

			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				float3 normalWS = TransformObjectToWorldDir(input.normalOS);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = ASE_ADJUST_CLIP_POSITION( TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)) );

				//code for UNITY_REVERSED_Z is moved into Shadows.hlsl from 6000.0.22 and or higher
				positionCS = ApplyShadowClamping(positionCS);

				output.positionCS = positionCS;
				output.positionWS = positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
					float AlphaClipThresholdShadow = 0.5;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#if defined( _ALPHATEST_ON )
					#if defined( _ALPHATEST_SHADOW_ON )
						AlphaDiscard( Alpha, AlphaClipThresholdShadow );
					#else
						AlphaDiscard( Alpha, AlphaClipThreshold );
					#endif
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask R
			AlphaToMask Off

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 frameUVs117 : TEXCOORD1;
				float4 viewPos117 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				half4 texcoord : TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 frameUVs117 : TEXCOORD3;
				float4 viewPos117 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#if defined( ASE_WRITE_DEPTH )
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						#if ( UNITY_VERSION >= 60020000 )
						, out uint outRenderingLayers : SV_Target1
						#else
						, out float4 outRenderingLayers : SV_Target1
						#endif
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( input.positionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float3 Normal = io.WorldNormal;
				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(NormalWS);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(TangentWS, BitangentWS, NormalWS));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = NormalWS;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					#if ( UNITY_VERSION >= 60020000 )
					outRenderingLayers = EncodeMeshRenderingLayer();
					#else
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
					#endif
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "GBuffer"
			Tags { "LightMode"="UniversalGBuffer" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			// Deferred Rendering Path does not support the OpenGL-based graphics API:
			// Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
			#pragma exclude_renderers glcore gles3 switch2 webgpu 

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#if ( UNITY_VERSION >= 60000058 )
			#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#endif
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
			#endif
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#endif

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LIGHTMAP_ON
			#if ( UNITY_VERSION >= 60010000 )
			#pragma multi_compile _ LIGHTMAP_BICUBIC_SAMPLING
			#endif
			#if ( UNITY_VERSION >= 60030000 )
			#pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
			#endif
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SHADERPASS SHADERPASS_GBUFFER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if ( UNITY_VERSION >= 60030016 && UNITY_VERSION < 60040000 ) || ( UNITY_VERSION >= 60040010 )
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutputFormat.hlsl"
			#endif

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined( UNITY_INSTANCING_ENABLED ) && defined( ASE_INSTANCED_TERRAIN ) && ( defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL) || defined(_INSTANCEDTERRAINNORMALS_PIXEL) )
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				half3 normalWS : TEXCOORD1;
				float4 tangentWS : TEXCOORD2; // holds terrainUV ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				float4 lightmapUVOrVertexSH : TEXCOORD3;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD4;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD5;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD6;
				#endif
				float4 frameUVs117 : TEXCOORD7;
				float4 viewPos117 : TEXCOORD8;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			#if ( UNITY_VERSION >= 60010000 )
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
			#else
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
			#endif

			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				#ifdef ASE_CUSTOM_MOTION_VECTOR
					// Declared so the Motion Vector output port surfaces on the master node; only consumed by the motion vector passes.
					float3 aseCustomMotionVector = float3(0, 0, 0);
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif
				OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						// @diogo: no fog applied in GBuffer
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalInput.normalWS;
				output.tangentWS = float4( normalInput.tangentWS, ( input.tangentOS.w > 0.0 ? 1.0 : -1.0 ) * GetOddNegativeScale() );

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					output.tangentWS.zw = input.texcoord.xy;
					output.tangentWS.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					float4 texcoord1 : TEXCOORD1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					float4 texcoord2 : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = input.texcoord1;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = input.texcoord2;
				#endif
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				#if defined(LIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES1)
					output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON) || defined(ASE_NEEDS_TEXTURE_COORDINATES2)
					output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				#endif
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

		#if ( UNITY_VERSION >= 60010000 )
			GBufferFragOutput frag ( PackedVaryings input
		#else
			FragmentOutput frag ( PackedVaryings input
		#endif
								#if defined( ASE_WRITE_DEPTH )
								,out float outputDepth : ASE_SV_DEPTH
								#endif
								 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord( input.positionWS );
				#else
					float4 shadowCoord = float4(0, 0, 0, 0);
				#endif

				// @diogo: mikktspace compliant
				float renormFactor = 1.0 / max( FLT_MIN, length( input.normalWS ) );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float3 ViewDirWS = GetWorldSpaceNormalizeViewDir( PositionWS );
				float4 ShadowCoord = shadowCoord;
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;
				float4 ScreenPos = ComputeScreenPos( ClipPos );
				float3 TangentWS = input.tangentWS.xyz * renormFactor;
				float3 BitangentWS = cross( input.normalWS, input.tangentWS.xyz ) * input.tangentWS.w * renormFactor;
				float3 NormalWS = input.normalWS * renormFactor;

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float2 sampleCoords = (input.tangentWS.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					NormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					TangentWS = -cross(GetObjectToWorldMatrix()._13_23_33, NormalWS);
					BitangentWS = cross(NormalWS, -TangentWS);
				#endif

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float3 BaseColor = io.Albedo;
				float3 Normal = io.WorldNormal;
				float3 Specular = io.Specular;
				float Metallic = 0;
				float Smoothness = io.Smoothness;
				float Occlusion = io.Occlusion;
				float3 Emission = io.Emission;
				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
					float AlphaClipThresholdShadow = 0.5;
				#endif
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#if defined( _ALPHATEST_ON )
					AlphaDiscard( Alpha, AlphaClipThreshold );
				#endif

				#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && defined(ASE_CHANGES_WORLD_POS)
					ShadowCoord = TransformWorldToShadowCoord( PositionWS );
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = PositionWS;
				inputData.positionCS = input.positionCS;
				inputData.normalizedScreenSpaceUV = ScreenPosNorm.xy;
				inputData.shadowCoord = ShadowCoord;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
						inputData.normalWS = TransformTangentToWorld(Normal, half3x3( TangentWS, BitangentWS, NormalWS ));
					#elif _NORMAL_DROPOFF_OS
						inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
						inputData.normalWS = Normal;
					#endif
				#else
					inputData.normalWS = NormalWS;
				#endif

				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.viewDirectionWS = SafeNormalize( ViewDirWS );

				#ifdef ASE_FOG
					// @diogo: no fog applied in GBuffer
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined( ENABLE_TERRAIN_PERPIXEL_NORMAL )
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(_SCREEN_SPACE_IRRADIANCE) && ( UNITY_VERSION >= 60030000 )
					#if ( UNITY_VERSION >= 60060000 )
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS));
					#else
						inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
					#endif
				#elif defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI(SH,
						GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask);
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				#ifdef _DBUFFER
					ApplyDecal(input.positionCS,
						BaseColor,
						Specular,
						inputData.normalWS,
						Metallic,
						Occlusion,
						Smoothness);
				#endif

				BRDFData brdfData;
				InitializeBRDFData(BaseColor, Metallic, Specular, Smoothness, Alpha, brdfData);

				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
				half4 color;
				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);

			#if ( UNITY_VERSION >= 60010000 )
				color.rgb = GlobalIllumination(brdfData, (BRDFData)0, 0,
                              inputData.bakedGI, Occlusion, inputData.positionWS,
                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
			#else
				color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, Occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
			#endif

				color.a = Alpha;

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

			#if ( UNITY_VERSION >= 60010000 )
				return PackGBuffersBRDFData(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			#else
				return BRDFDataToGbuffer(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			#endif
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 frameUVs117 : TEXCOORD1;
				float4 viewPos117 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				surfaceDescription.Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					surfaceDescription.AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#ifdef _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return half4( _ObjectId, _PassValue, 1.0, 1.0 );
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 frameUVs117 : TEXCOORD1;
				float4 viewPos117 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( vertexInput.positionCS );
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag( PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				surfaceDescription.Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					surfaceDescription.AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#ifdef _ALPHATEST_ON
					clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				return unity_SelectionID;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#define ASE_TIME_BASED_MOTION_VECTORS
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define ASE_WRITE_DEPTH_CONSERVATIVE
			#define _SPECULAR_SETUP 1
			#define _NORMAL_DROPOFF_WS 1
			#define ASE_WRITE_DEPTH
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#define ASE_IMPOSTOR
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19914
			#define ASE_SRP_VERSION 170600
			#undef ASE_ADJUST_CLIP_POSITION
			#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP
			#else//AI_SRP
			#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP
			#endif//AI_SRP


			#pragma vertex vert
			#pragma fragment frag

			#if defined( _SPECULAR_SETUP ) && defined( ASE_LIGHTING_SIMPLE )
				#if defined( _SPECULARHIGHLIGHTS_OFF )
					#undef _SPECULAR_COLOR
				#else
					#define _SPECULAR_COLOR
				#endif
			#endif

            #define SHADERPASS SHADERPASS_MOTION_VECTORS

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

			#define AI_ObjectToWorld GetObjectToWorldMatrix()
			#define AI_WorldToObject GetWorldToObjectMatrix()
			#define AI_INV_TWO_PI  INV_TWO_PI
			#define AI_PI          PI
			#define AI_INV_PI      INV_PI
			#define ASE_CHANGES_WORLD_POS
			#define ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, output.viewPos117, _DepthSize )
			#define ASE_NEEDS_CLIP_POS
			#define ASE_NEEDS_FRAG_CLIP_POS
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#pragma shader_feature_local_fragment _SPECULARMAP
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _EMISSIONMAP
			#pragma shader_feature_local_fragment _POSITIONMAP
			#pragma shader_feature_local _USE_PARALLAX_ON
			#pragma shader_feature_local EFFECT_HUE_VARIATION


			#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			#if ( UNITY_VERSION < 60010000 )
				#define APPLICATION_SPACE_WARP_MOTION APLICATION_SPACE_WARP_MOTION
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 positionOld : TEXCOORD4;
				#if _ADD_PRECOMPUTED_VELOCITY
					float3 alembicMotionVector : TEXCOORD5;
				#endif
				half3 normalOS : NORMAL;
				half4 tangentOS : TANGENT;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 positionCSNoJitter : TEXCOORD0;
				float4 previousPositionCSNoJitter : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float4 frameUVs117 : TEXCOORD3;
				float4 viewPos117 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _AI_SizeOffset;
			float4 _AI_AngleRange;
			float4 _HueVariation;
			float3 _AI_BoundsSize;
			float3 _Offset;
			float3 _AI_BoundsMin;
			float _ImpostorSize;
			float _DepthSize;
			float _FramesY;
			float _FramesX;
			float _AI_ShadowView;
			float _AI_ShadowBias;
			float _Parallax;
			float _TextureBias;
			float _ClipMask;
			float _AI_ForwardBias;
			float _AI_AlphaToCoverage;
			float _AlphaClip;
			float _Cutoff;
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
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Albedo;
			sampler2D _Normals;
			sampler2D _Specular;
			sampler2D _Occlusion;
			sampler2D _Emission;
			sampler2D _Position;


			struct ImpostorOutput
			{
				half3 Albedo;
				half3 Specular;
				half Metallic;
				half3 WorldNormal;
				half Smoothness;
				half Occlusion;
				half3 Emission;
				half Alpha;
			};
			
			float2 VectorToOctahedron( float3 N )
			{
				N /= dot( 1.0, abs( N ) );
				if( N.z <= 0 )
				{
					N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
				}
				return N.xy;
			}
			
			float3 OctahedronToVector( float2 Oct )
			{
				float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
				if(N.z< 0 )
				{
					N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );
				}
				return normalize( N);
			}
			
			float2 VectorToHemiOctahedron( float3 N )
			{
				N.xy /= dot( 1.0, abs( N ) );
				return float2( N.x + N.y, N.x - N.y );
			}
			
			float3 HemiOctahedronToVector( float2 Oct )
			{
				Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;
				float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
				return normalize( N );
			}
			
			void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
			{
				float2 sinCenter, cosCenter;
				sincos( radians( center ), sinCenter, cosCenter );
				float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
				float3 right = float3( cosCenter.x, 0, -sinCenter.x );
				float3 up = cross( forward, right );
				basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );
				float2 sinHalf, cosHalf;
				sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
				scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
			}
			
			float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )
			{
				const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
				{
					// Orthographic
					clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;
				}
				else
				{
					// Perspective
					float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );
					float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );
					clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;
				}
				return clipPos;
			}
			
			inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )
			{
				float2 uvOffset = _AI_SizeOffset.zw;
				float sizeX = _FramesX;
				float sizeY = max( _FramesY - 1, 1 );
				float UVscale = _ImpostorSize;
				float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
				float2 sizeFraction = fractions.xy;
				float fractionsUVscale = fractions.w;
				float3 worldCameraPos;
				#if defined(UNITY_PASS_SHADOWCASTER)
				float3 worldOrigin = 0;
				float4 perspective = float4( 0, 0, 0, 1 );
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					perspective = float4( 0, 0, 5000, 0 );
					worldOrigin = AI_ObjectToWorld._m03_m13_m23;
				}
				worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
				#else
				if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
				{
					worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
				}
				else
				{
					worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
				}
				#endif
				float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz;
				float3 objectCameraDirection = normalize( objectCameraPosition );
				float3 upVector = float3( 0,1,0 );
				float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
				float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );
				float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
				float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
				float verticalDot = dot( objectCameraDirection, upVector );
				float frameX;
				float frameY;
				float yRot;
				if ( yawRange <= 180 )
				{
					float3x3 capBasis;
					float2 capScale;
					CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
					float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );
					frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
					frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
					yRot = 0;
				}
				else
				{
					float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
					float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;
					float residualYaw;
					if ( yawRange >= 360 )
					{
						float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
						frameX = floor( a );
						residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
					}
					else
					{
						float yawStep = yawRange / max( sizeX - 1, 1 );
						frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
						residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;
					}
					yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;
					frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
				}
				float2 uvExpansion = positionOS.xy;
				float cosY = cos( yRot );
				float sinY = sin( yRot );
				float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );
				float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;
				float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
				float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;
				frameUVs.xy = frameUV - uvOffset;
				#if defined( _USE_PARALLAX_ON )
				float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
				float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
				float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
				frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
				#else
				frameUVs.zw = 0;
				#endif
				positionOS = billboard + _Offset;
				normalOS = objectCameraDirection;
				tangentOS = float4( objectHorizontalVector, 1 );
				viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );
				#if defined( EFFECT_HUE_VARIATION )
				float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
				viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
				#else
				viewPos.w = 0;
				#endif
			}
			
			inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos )
			{
				#if defined( _USE_PARALLAX_ON )
				float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
				float2 frameCount = float2( _FramesX, _FramesY );
				float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
				float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
				frameUV.xy += parallaxOffset;
				clip( 0.5 - max( dist.x, dist.y ) );
				#endif
				float4 albedoSample = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );
				o.Alpha = ( albedoSample.a - _ClipMask );
				clip( o.Alpha );
				#if defined( EFFECT_HUE_VARIATION )
				half3 shiftedColor = lerp( albedoSample.rgb, _HueVariation.rgb, viewPos.w );
				half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );
				half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
				maxBase /= newMaxBase;
				maxBase = maxBase * 0.5f + 0.5f;
				shiftedColor.rgb *= maxBase;
				albedoSample.rgb = saturate( shiftedColor );
				#endif
				o.Albedo = albedoSample.rgb;
				float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
				float4 objectNormal = normalSample * 2 - 1;
				o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
				const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
				float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
				#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path
				depth -= 0.001;
				#endif
				#if defined( _SPECULARMAP )
				float4 specularSample = tex2Dbias( _Specular, float4( frameUV.xy, 0, _TextureBias ) );
				o.Specular = specularSample.rgb;
				o.Smoothness = specularSample.a;
				#else
				o.Specular = 0;
				o.Smoothness = 0;
				#endif
				#if defined( _OCCLUSIONMAP )
				float4 occlusionSample = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) );
				o.Occlusion = occlusionSample.a;
				#else
				o.Occlusion = 1;
				#endif
				#if defined( _EMISSIONMAP )
				float4 emissionSample = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) );
				o.Emission = emissionSample.rgb;
				#else
				o.Emission = 0;
				#endif
				#if defined( _POSITIONMAP )
				float4 position = tex2Dbias( _Position, float4( frameUV.xy, 0, _TextureBias ) );
				float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
				float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
				if ( position.a > 0 )
				{
					viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
					depth = 0;
				}
				#endif
				float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
				#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
				viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
				#else
				viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
				#endif
				positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
				#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)
				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize( _LightPosition - positionWS );
				#else
				float3 lightDirectionWS = _LightDirection;
				#endif
				positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );
				#if UNITY_REVERSED_Z
				positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#else
				positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );
				#endif
				#else
				positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );
				#endif
			}
			

			// Applies the graph's vertex stage at a given time so the motion vector pass can
			// evaluate the current frame and re-evaluate the previous frame (procedural / time-based animation).
			Attributes ASEApplyVertexModification( Attributes input, float3 timeParameters, inout PackedVaryings output, out float3 customMotionVector  )
			{
				float3 currentTimeParameters = _TimeParameters.xyz;
				_TimeParameters.xyz = timeParameters;

				SphereImpostorVertex( input.positionOS.xyz, input.normalOS.xyz, input.tangentOS, output.frameUVs117, output.viewPos117 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				customMotionVector = float3(0, 0, 0);

				_TimeParameters.xyz = currentTimeParameters;
				return input;
			}

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				Attributes defaultInput = input;
				float3 currentMotionVector;
				input = ASEApplyVertexModification( input, _TimeParameters.xyz, output, currentMotionVector );

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(APPLICATION_SPACE_WARP_MOTION)
					float4 positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
					float4 positionCS = positionCSNoJitter;
				#else
					float4 positionCS = vertexInput.positionCS;
					float4 positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
				#endif

				// Custom output and automatic time-based motion are mutually exclusive.
				#if defined(ASE_CUSTOM_MOTION_VECTOR)
					float3 prevPositionOS = ( unity_MotionVectorsParams.x == 1 ) ? input.positionOld : input.positionOS.xyz;
					prevPositionOS -= currentMotionVector;
				#else
					float3 prevPositionOS = ( unity_MotionVectorsParams.x == 1 ) ? input.positionOld : defaultInput.positionOS.xyz;
					#ifdef ASE_TIME_BASED_MOTION_VECTORS
						Attributes prevInput = defaultInput;
						prevInput.positionOS.xyz = prevPositionOS;
						PackedVaryings prevOutput = (PackedVaryings)0;
						float3 prevMotionVector;
						prevInput = ASEApplyVertexModification( prevInput, _LastTimeParameters.xyz, prevOutput, prevMotionVector );
						prevPositionOS = prevInput.positionOS.xyz;
					#endif
				#endif
				#if _ADD_PRECOMPUTED_VELOCITY
					prevPositionOS -= input.alembicMotionVector;
				#endif
				float4 previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, float4( prevPositionOS, 1 ) ) );

				output.positionCS = ASE_ADJUST_CLIP_POSITION( positionCS );
				output.positionCSNoJitter = ASE_ADJUST_CLIP_POSITION( positionCSNoJitter );
				output.previousPositionCSNoJitter = ASE_ADJUST_CLIP_POSITION( previousPositionCSNoJitter );
				output.positionWS = vertexInput.positionWS;

				return output;
			}

			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}

			half4 frag(	PackedVaryings input
				#if defined( ASE_WRITE_DEPTH )
				,out float outputDepth : ASE_SV_DEPTH
				#endif
				 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float3 PositionWS = input.positionWS;
				float3 PositionRWS = GetCameraRelativePositionWS( PositionWS );
				float4 ScreenPosNorm = float4( GetNormalizedScreenSpaceUV( input.positionCS ), input.positionCS.zw );
				float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, input.positionCS.z ) * input.positionCS.w;

				ImpostorOutput io = ( ImpostorOutput )0;
				SphereImpostorFragment( io, ClipPos, PositionWS, input.frameUVs117, input.viewPos117 );
				

				float Alpha = io.Alpha;
				#if defined( _ALPHATEST_ON )
					float AlphaClipThreshold = _Cutoff;
				#endif

				#if defined( ASE_WRITE_DEPTH )
					input.positionCS.z = ( ClipPos.z / ClipPos.w );
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(ASE_CHANGES_WORLD_POS)
					float3 positionOS = mul( GetWorldToObjectMatrix(),  float4( PositionWS, 1.0 ) ).xyz;
					float3 previousPositionWS = mul( GetPrevObjectToWorldMatrix(),  float4( positionOS, 1.0 ) ).xyz;
					input.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, float4( PositionWS, 1.0 ) );
					input.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, float4( previousPositionWS, 1.0 ) );
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined( ASE_WRITE_DEPTH )
					outputDepth = input.positionCS.z;
				#endif

				#if defined(APPLICATION_SPACE_WARP_MOTION)
					return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
				#else
					return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
				#endif
			}
			ENDHLSL
		}

	
	}
	

	

	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19914
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":116,"pos":[-512,0],"params":["Inherit","False","Property","_AI_AlphaToCoverage","Alpha To Coverage","17","1","[Toggle]","Create","False","0","0","0","True","0","False","Object","-1","","0","0","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.AmplifyImpostorNode, AIToASE","id":117,"pos":[-288,0],"params":["Inherit","False","11000","Spherical","ShaderFeature","ShaderFeature","ShaderFeature","False","True","18","19","20","22","9","23","24","7","0","1","2","4","21","10","11","6","15","3","5","8","13","14","16","12","True","0","Specular","True","27","ShaderFeature","False","True","9","0","SAMPLER2D","","False","1","SAMPLER2D","","False","2","SAMPLER2D","","False","3","SAMPLER2D","","False","4","SAMPLER2D","","False","5","SAMPLER2D","","False","6","SAMPLER2D","","False","7","SAMPLER2D","","False","8","SAMPLERSTATE","","False","18","FLOAT4","8","FLOAT4","9","FLOAT4","10","FLOAT4","11","FLOAT4","12","FLOAT4","13","FLOAT4","14","FLOAT4","15","FLOAT3","0","FLOAT3","1","FLOAT3","3","FLOAT","4","FLOAT","5","FLOAT3","2","FLOAT","6","FLOAT","17","FLOAT3","7","FLOAT3","16"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":58,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","ExtraPrePass","0","0","ExtraPrePass","6","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","0","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","0","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":60,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","ShadowCaster","0","2","ShadowCaster","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","True","False","False","False","False","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","False","False","True","1","LightMode=ShadowCaster","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":61,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","DepthOnly","0","3","DepthOnly","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","True","True","False","False","False","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","False","False","False","True","1","LightMode=DepthOnly","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":62,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","Meta","0","4","Meta","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=Meta","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":63,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","Universal2D","0","5","Universal2D","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","1","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","1","LightMode=Universal2D","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":64,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","DepthNormals","0","6","DepthNormals","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","0","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","False","False","True","1","LightMode=DepthNormals","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":65,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","GBuffer","0","7","GBuffer","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","True","1","1","False","","0","False","","1","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","1","LightMode=UniversalGBuffer","False","True","10","d3d11","gles","metal","vulkan","xboxone","xboxseries","playstation","ps4","ps5","switch","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":66,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","SceneSelectionPass","0","8","SceneSelectionPass","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","2","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=SceneSelectionPass","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":67,"pos":[0,0],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","ScenePickingPass","0","9","ScenePickingPass","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=Picking","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":59,"pos":[0,0],"params":["Float","False","True","-1","3","UnityEditor.ShaderGraphLitGUI","0","23","Hidden/Amplify Impostors/Spherical Impostor URP","ad3e6cbf440fc8843823405e7e862e1a","True","Forward","0","1","Forward","22","False","False","False","False","False","False","False","False","False","False","False","False","True","0","True","_AI_AlphaToCoverage","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","5","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","ImpostorType=Spherical","True","5","True","14","all","0","False","True","1","1","False","","0","False","","1","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","1","LightMode=UniversalForward","False","False","0","","0","0","Standard","52","Category","2","639041650753749338","  Instanced Terrain Normals","1","0","Lighting Model","0","0","Workflow","0","638764363290246051","Surface","0","0","  Keep Alpha","0","0","  Refraction Model","0","0","  Blend","0","0","Two Sided","1","0","Alpha Clipping","0","639041659249250850","  Use Shadow Threshold","0","0","Fragment Normal Space","2","638764363944481560","Forward Only","0","0","Transmission","0","0","  Transmission Shadow","0.5,False,","0","Translucency","0","0","  Translucency Strength","1,False,","0","  Normal Distortion","0.5,False,","0","  Scattering","2,False,","0","  Direct","0.9,False,","0","  Ambient","0.1,False,","0","  Shadow","0.5,False,","0","Cast Shadows","1","0","Receive Shadows","2","639041650269679028","Specular Highlights","2","0","Environment Reflections","2","0","Receive SSAO","1","0","Motion Vectors","1","0","  Additional Motion Vectors","1","0","  Alembic Motion Vectors","0","0","  XR Motion Vectors","0","0","GPU Instancing","1","0","LOD CrossFade","1","0","Built-in Fog","1","0","_FinalColorxAlpha","0","0","Meta Pass","0","638794034860033227","Override Baked GI","0","0","Extra Pre Pass","0","0","Tessellation","0","0","  Phong","0","0","  Strength","0.5,False,","0","  Type","0","0","  Tess","16,False,","0","  Min","10,False,","0","  Max","25,False,","0","  Edge Length","16,False,","0","  Max Displacement","25,False,","0","Write Depth","1","638764364121229627","  Conservative","1","0","Vertex Position","1","0","Debug Display","1","639041650538496041","Clear Coat","0","0","0","12","False","True","True","True","False","False","True","True","True","True","True","False","False","","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":144,"pos":[0,100],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","MotionVectors","0","10","MotionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","False","False","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","LightMode=MotionVectors","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":145,"pos":[0,100],"params":["Float","False","False","-1","3","UnityEditor.ShaderGraphLitGUI","0","1","New Amplify Shader","ad3e6cbf440fc8843823405e7e862e1a","True","XRMotionVectors","0","11","XRMotionVectors","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","False","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","4","RenderPipeline=UniversalPipeline","RenderType=Opaque=RenderType","Queue=Geometry=Queue=0","UniversalMaterialType=Lit","True","5","True","14","all","0","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","True","1","False","","255","False","","1","False","","7","False","","3","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","False","False","False","False","True","1","LightMode=XRMotionVectors","False","False","0","","0","0","Standard","0","False","0"]}
{"wire":[59,0,117,0]}
{"wire":[59,1,117,1]}
{"wire":[59,9,117,3]}
{"wire":[59,4,117,4]}
{"wire":[59,5,117,5]}
{"wire":[59,2,117,2]}
{"wire":[59,6,117,6]}
{"wire":[59,17,117,17]}
ASEEND*/
//CHKSM=95C3E49AF5204A657073D4D335137C6BF59417E3