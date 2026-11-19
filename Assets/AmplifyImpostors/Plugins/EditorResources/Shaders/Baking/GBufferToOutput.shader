// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/GBufferToOutput"
{
	Properties
	{
		_GBuffer0( "GBuffer0", 2D ) = "white" {}
		_GBuffer1( "GBuffer1", 2D ) = "white" {}
		_GBuffer2( "GBuffer2", 2D ) = "white" {}
		_GBuffer3( "GBuffer3", 2D ) = "white" {}
		_Depth( "Depth", 2D ) = "white" {}
	}

	CGINCLUDE
		#pragma target 4.5
		#pragma vertex vert_img
		#pragma fragment frag
		#include "UnityCG.cginc"

		#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
		#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2

		#define RENDER_PIPELINE_BiRP ( 0 )
		#define RENDER_PIPELINE_HDRP ( 1 )
		#define RENDER_PIPELINE_URP  ( 2 )

		uniform uint _RenderPipeline;
		uniform sampler2D _GBuffer0;
		uniform sampler2D _GBuffer1;
		uniform sampler2D _GBuffer2;
		uniform sampler2D _GBuffer3;
		uniform sampler2D _Depth;

		uniform StructuredBuffer<float4x4> _CameraInvViewProjPerFrame;
		uniform float2 _FrameCount;
		uniform float3 _BoundsMin;
		uniform float3 _BoundsSize;

		float2 Unpack888UIntToFloat2( uint3 x )
		{
			uint hi = x.z >> 4;
			uint lo = x.z & 15;
			uint2 cb = x.xy | uint2( lo << 8, hi << 8 );
			return cb / 4095.0;
		}

		float2 Unpack888ToFloat2( float3 x )
		{
			uint3 i = ( uint3 )( x * 255.5 );
			return Unpack888UIntToFloat2( i );
		}

		float3 UnpackNormalOctQuadEncode( float2 f )
		{
			float3 n = float3( f.x, f.y, 1.0 - ( f.x < 0 ? -f.x : f.x ) - ( f.y < 0 ? -f.y : f.y ) );
			float t = max( -n.z, 0.0 );
			n.xy += float2( n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t );
			return normalize( n );
		}

		void UnpackFloatInt( float val, float maxi, float precision, out float f, out uint i )
		{
			float precisionMinusOne = precision - 1.0;
			float t1 = ( ( precision / maxi ) - 1.0 ) / precisionMinusOne;
			float t2 = ( precision / maxi ) / precisionMinusOne;
			i = int( ( val / t2 ) + rcp( precisionMinusOne ) );
			f = saturate( ( -t2 * float( i ) + val ) / t1 );
		}

		void UnpackFloatInt8bit( float val, float maxi, out float f, out uint i )
		{
			UnpackFloatInt( val, maxi, 256.0, f, i );
		}

		float3 GammaToLinearSpaceExact( float3 linRGB )
		{
			return float3( GammaToLinearSpaceExact( linRGB.r ), GammaToLinearSpaceExact( linRGB.g ), GammaToLinearSpaceExact( linRGB.b ) );
		}

		#define kDielectricSpec float4( 0.04, 0.04, 0.04, 1.0 - 0.04 ) // standard dielectric reflectivity coef at incident angle (= 4%)

		uint UnpackMaterialFlags( float packedMaterialFlags )
		{
			return uint( ( packedMaterialFlags * 255.0 ) + 0.5 );
		}

		float MetallicFromReflectivity( float reflectivity )
		{
			float oneMinusDielectricSpec = kDielectricSpec.a;
			return ( reflectivity - kDielectricSpec.r ) / oneMinusDielectricSpec;
		}

		inline float2 FrameCoordsFromUV( float2 uv )
		{
			return uv * _FrameCount.xy;
		}

		inline uint FrameIndexFromFrameCoords( float2 frameCoords )
		{
			uint2 frame = ( uint2 )floor( frameCoords );
			return frame.y * _FrameCount.x + frame.x;
		}

		inline float3 ScreenToPosition( float2 uv, float deviceDepth )
		{
			float2 frameCoords = FrameCoordsFromUV( uv );
			uint frameIndex = FrameIndexFromFrameCoords( frameCoords );
			float4x4 invViewProjMatrix = _CameraInvViewProjPerFrame.Load( frameIndex );

			float4 positionCS = float4( frac( frameCoords ) * 2.0 - 1.0, deviceDepth, 1.0 );
		#if UNITY_UV_STARTS_AT_TOP
			positionCS.y = -positionCS.y;
		#endif
			float4 hposition = mul( invViewProjMatrix, positionCS );
			float3 position = hposition.xyz / hposition.w;
			return position;
		}

		float4 GBufferToOutput_BiRP( const float2 uv, const float depth, const int outputIndex )
		{
			// GBuffer0: Albedo (RGB) Occlusion (A)    {SRGB}
			// GBuffer1: Specular (RGB) Smoothness (A) {SRGB}
			// GBuffer2: Normals (RGB)                 {LINEAR}
			// GBuffer3: Emission (RGB)                {LINEAR}

			float alpha = 1 - step( depth, 0 );

			float4 result = 0;
			if ( outputIndex == 0 )
			{
				// _Albedo(Alpha)
				result.rgb = tex2D( _GBuffer0, uv ).rgb;
				result.a = alpha;
			}
			else if ( outputIndex == 1 )
			{
				// _Normal(Depth)
				result.rgb = tex2D( _GBuffer2, uv ).rgb;
				result.a = depth;
			}
			else if ( outputIndex == 2 )
			{
				// _Specular(Smoothness)
				result = tex2D( _GBuffer1, uv );
			}
			else if ( outputIndex == 3 )
			{
				// _Occlusion
				result.rgb = tex2D( _GBuffer0, uv ).aaa;
				result.a = 0;
			}
			else if ( outputIndex == 4 )
			{
				// _Emission
				result.rgb = tex2D( _GBuffer3, uv ).rgb;
				result.a = 0;
			}
			return result;
		}

		float4 GBufferToOutput_HDRP( const float2 uv, const float depth, const int outputIndex )
		{
			// GBuffer0: Albedo (RGB) Occlusion (A)             [A => occlusion(7) / isLightmap(1)]  {SRGB}
			// GBuffer1: Normals (RGB) PerceptualRoughness (A)  [RGB => X:Y / 12:12 / Octa]          {LINEAR}
			// GBuffer2: Specular (RGB)                                                              {LINEAR}
			// GBuffer3: Emission (RGB)                                                              {LINEAR}

			float alpha = 1 - step( depth, 0 );

			float4 result = 0;
			if ( outputIndex == 0 )
			{
				// _Albedo(Alpha)
				result.rgb = tex2D( _GBuffer0, uv ).rgb;
				result.a = alpha;
			}
			else if ( outputIndex == 1 )
			{
				// _Normal(Depth)
				// TODO: unpack normals to world
				result.rgb = UnpackNormalOctQuadEncode( Unpack888ToFloat2( tex2D( _GBuffer1, uv ).rgb ) * 2.0 - 1.0 ) * 0.5 + 0.5;
				result.a = depth;
			}
			else if ( outputIndex == 2 )
			{
				// _Specular(Smoothness)
				result.rgb = GammaToLinearSpaceExact( tex2D( _GBuffer2, uv ).rgb );
				result.a = 1 - tex2D( _GBuffer1, uv ).a;
			}
			else if ( outputIndex == 3 )
			{
				// _Occlusion
				float occlusion = tex2D( _GBuffer0, uv ).a;
				#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
					uint isLightmap;
					UnpackFloatInt8bit( occlusion, 2, occlusion, isLightmap );
				#endif
				result.rgb = occlusion;
				result.a = 0;
			}
			else if ( outputIndex == 4 )
			{
				// _Emission
				float3 emission = tex2D( _GBuffer3, uv ).rgb;

				#define AO_IN_GBUFFER3_TAG float3( 1 << 11, 1, 1 << 10 )
				emission *= all( emission.xz == AO_IN_GBUFFER3_TAG.xz ) ? 0 : 1;

				result.rgb = emission;
				result.a = 0;
			}
			return result;
		}

		float4 GBufferToOutput_URP( const float2 uv, const float depth, const int outputIndex )
		{
			// GBuffer0: Albedo (RGB) MaterialFlags (A)  [A => miscFlags(7) / specularSetup(1)]              {SRGB}
			// GBuffer1: Specular (RGB) Occlusion (A)    [specularSetup ? R => Metallic : RGB => Specular ]  {LINEAR}
			// GBuffer2: Normals (RGB) Smoothness (A)                                                        {LINEAR}
			// GBuffer3: Emission (RGB)                                                                      {LINEAR}

			float alpha = 1 - step( depth, 0 );

			float4 gbuffer0 = tex2D( _GBuffer0, uv );
			float4 gbuffer1 = tex2D( _GBuffer1, uv );
			float4 gbuffer2 = tex2D( _GBuffer2, uv );
			float4 gbuffer3 = tex2D( _GBuffer3, uv );

			const int kMaterialFlagSpecularSetup = 8;

			float3 albedo = gbuffer0.rgb;
			uint materialFlags = UnpackMaterialFlags( gbuffer0.a );
			float occlusion = gbuffer1.a;
			float smoothness = gbuffer2.a;
			float3 specular = gbuffer1.rgb;
			float3 emission = gbuffer3.rgb;

			if ( ( materialFlags & kMaterialFlagSpecularSetup ) == 0 ) // Metallic workflow?
			{
				float reflectivity = gbuffer1.r;
				float oneMinusReflectivity = 1.0 - reflectivity;
				float metallic = MetallicFromReflectivity( reflectivity );

				specular = lerp( kDielectricSpec.rgb, albedo, metallic );
				albedo *= oneMinusReflectivity;
			}

			float4 result = 0;
			if ( outputIndex == 0 )
			{
				// _Albedo(Alpha)
				result.rgb = albedo;
				result.a = alpha;
			}
			else if ( outputIndex == 1 )
			{
				// _Normal(Depth)
			#ifdef _GBUFFER_NORMALS_OCT
				result.rgb = UnpackNormalOctQuadEncode( Unpack888ToFloat2( gbuffer2.rgb ) * 2.0 - 1.0 ) * 0.5 + 0.5;
			#else
				result.rgb = gbuffer2.rgb * 0.5 + 0.5;
			#endif
				result.a = depth;
			}
			else if ( outputIndex == 2 )
			{
				// _Specular(Smoothness)
				result.rgb = specular;
				result.a = smoothness;
			}
			else if ( outputIndex == 3 )
			{
				// _Occlusion
				result.rgb = occlusion;
				result.a = 0;
			}
			else if ( outputIndex == 4 )
			{
				// _Emission
				result.rgb = emission;
				result.a = 0;
			}
			return result;
		}

		float4 GBufferToOutput( const float2 uv, const int outputIndex )
		{
			float depth = SAMPLE_RAW_DEPTH_TEXTURE( _Depth, uv ).r;
		#if !defined( UNITY_REVERSED_Z )
			depth = 1 - depth;
		#endif

			float4 result = 0;
			if ( outputIndex == 5 ) // Position: same for all render pipelines
			{
				result.rgb = ( ScreenToPosition( uv, depth ) - _BoundsMin ) / _BoundsSize;
				result.a = 0;
			}
			else
			{
				if ( _RenderPipeline == RENDER_PIPELINE_BiRP )
				{
					result = GBufferToOutput_BiRP( uv, depth, outputIndex );
				}
				else if ( _RenderPipeline == RENDER_PIPELINE_HDRP )
				{
					result = GBufferToOutput_HDRP( uv, depth, outputIndex );
				}
				else if ( _RenderPipeline == RENDER_PIPELINE_URP )
				{
					result = GBufferToOutput_URP( uv, depth, outputIndex );
				}
			}

			return result;
		}
	ENDCG

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		ZWrite On
		ZTest LEqual
		ColorMask RGBA
		Blend Off
		Cull Off
		Offset 0,0

		Pass // GBuffer to Output 0
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 0 );
				}
			ENDCG
		}
		Pass // GBuffer to Output 1
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 1 );
				}
			ENDCG
		}
		Pass // GBuffer to Output 2
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 2 );
				}
			ENDCG
		}
		Pass // GBuffer to Output 3
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 3 );
				}
			ENDCG
		}
		Pass // GBuffer to Output 4
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 4 );
				}
			ENDCG
		}
		Pass // GBuffer to Output 5
		{
			CGPROGRAM
				float4 frag( v2f_img i ) : SV_Target
				{
					return GBufferToOutput( i.uv, 5 );
				}
			ENDCG
		}
	}
}
