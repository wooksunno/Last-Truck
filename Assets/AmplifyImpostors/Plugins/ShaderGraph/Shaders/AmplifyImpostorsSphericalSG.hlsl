// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_IMPOSTORS_SPHERICAL_SG_INCLUDED
#define AMPLIFY_IMPOSTORS_SPHERICAL_SG_INCLUDED

// spherical impostor functions ported from SphericalImpostorURP.shader (ase, the same in SphericalImpostorHDRP.shader), deliberate changes
// are marked "sg:"; textures are UnityTexture2D sampled with SAMPLE_TEXTURE2D_BIAS (ase: sampler2D and tex2Dbias)

#include "AmplifyImpostorsCommonSG.hlsl"

// sg: material properties are parameters
inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos,
	float _FramesX, float _FramesY, float _ImpostorSize, float3 _Offset, float4 _AI_SizeOffset, float4 _AI_AngleRange, float _DepthSize, float _Parallax, float4 _HueVariation )
{
	float2 uvOffset = _AI_SizeOffset.zw;
	float sizeX = _FramesX;
	float sizeY = max( _FramesY - 1, 1 );
	float UVscale = _ImpostorSize;
	float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
	float2 sizeFraction = fractions.xy;
	float fractionsUVscale = fractions.w;
	float3 worldCameraPos;
	// sg: AI_SHADOWCASTER_PASS
	#if defined( AI_SHADOWCASTER_PASS )
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
		// sg: AI_WorldSpaceCameraPos, shader graph previews
		worldCameraPos = AI_WorldSpaceCameraPos();
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
	// sg: AI_HueVariationAmount, absolute object position in hdrp
	float hueVariationAmount = AI_HueVariationAmount();
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
	#else
	viewPos.w = 0;
	#endif
}

// sg: material properties are parameters; positionWS is the interpolated surface position, depthOffset replaces positionCS
inline void SphereImpostorFragment( inout ImpostorOutput o, out float depthOffset, float3 positionWS, float4 frameUV, float4 viewPos,
	UnityTexture2D _Albedo, UnityTexture2D _Normals, UnityTexture2D _Specular, UnityTexture2D _Occlusion, UnityTexture2D _Emission, UnityTexture2D _Position,
	float _ClipMask, float _TextureBias, float _FramesX, float _FramesY, float _DepthSize, float _AI_ShadowBias, float _AI_ShadowView, float _AI_ForwardBias,
	float4 _HueVariation, float3 _AI_BoundsMin, float3 _AI_BoundsSize )
{
	#if defined( _USE_PARALLAX_ON )
	float parallax = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, frameUV.xy, -1 ).a;
	float2 frameCount = float2( _FramesX, _FramesY );
	float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
	float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
	frameUV.xy += parallaxOffset;
	clip( 0.5 - max( dist.x, dist.y ) );
	#endif
	float4 albedoSample = SAMPLE_TEXTURE2D_BIAS( _Albedo.tex, _Albedo.samplerstate, frameUV.xy, _TextureBias );
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
	float4 normalSample = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, frameUV.xy, _TextureBias );
	float4 objectNormal = normalSample * 2 - 1;
	o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;
	// sg: no "depth -= 0.001" standard rp deferred fix, dead in ase urp and hdrp
	#if defined( _SPECULARMAP )
	float4 specularSample = SAMPLE_TEXTURE2D_BIAS( _Specular.tex, _Specular.samplerstate, frameUV.xy, _TextureBias );
	o.Specular = specularSample.rgb;
	o.Smoothness = specularSample.a;
	#else
	o.Specular = 0;
	o.Smoothness = 0;
	#endif
	#if defined( _OCCLUSIONMAP )
	float4 occlusionSample = SAMPLE_TEXTURE2D_BIAS( _Occlusion.tex, _Occlusion.samplerstate, frameUV.xy, _TextureBias );
	o.Occlusion = occlusionSample.a;
	#else
	o.Occlusion = 1;
	#endif
	#if defined( _EMISSIONMAP )
	float4 emissionSample = SAMPLE_TEXTURE2D_BIAS( _Emission.tex, _Emission.samplerstate, frameUV.xy, _TextureBias );
	o.Emission = emissionSample.rgb;
	#else
	o.Emission = 0;
	#endif
	#if defined( _POSITIONMAP )
	float4 position = SAMPLE_TEXTURE2D_BIAS( _Position.tex, _Position.samplerstate, frameUV.xy, _TextureBias );
	float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;
	float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
	if ( position.a > 0 )
	{
		viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;
		depth = 0;
	}
	#endif
	float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );
	// sg: AI_SHADOWCASTER_PASS
	#if defined( AI_SHADOWCASTER_PASS )
	viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
	viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
	// sg: the signed depth offset of the final position instead of its world position and clip position
	depthOffset = AI_ImpostorDepthOffset( viewPos.xyz, positionWS );
}

// shader graph entry points (file mode custom functions)

void AISphereImpostorVertex_float( float3 PositionOS, float FramesX, float FramesY, float ImpostorSize, float3 Offset, float4 SizeOffset, float4 AngleRange,
	float DepthSize, float Parallax, float4 HueVariation,
	out float3 OutPositionOS, out float3 OutNormalOS, out float3 OutTangentOS, out float4 FrameUVs, out float4 ViewPos )
{
	float3 positionOS = PositionOS;
	float4 tangentOS;
	SphereImpostorVertex( positionOS, OutNormalOS, tangentOS, FrameUVs, ViewPos,
		FramesX, FramesY, ImpostorSize, Offset, SizeOffset, AngleRange, DepthSize, Parallax, HueVariation );
	OutPositionOS = positionOS;
	OutTangentOS = tangentOS.xyz;
}

void AISphereImpostorFragment_float( float3 PositionWS, float4 FrameUVs, float4 ViewPos,
	UnityTexture2D AlbedoMap, UnityTexture2D NormalsMap, UnityTexture2D SpecularMap, UnityTexture2D OcclusionMap, UnityTexture2D EmissionMap, UnityTexture2D PositionMap,
	float ClipMask, float TextureBias, float FramesX, float FramesY, float DepthSize, float ShadowBias, float ShadowView, float ForwardBias,
	float4 HueVariation, float3 BoundsMin, float3 BoundsSize,
	out float3 Albedo, out float3 NormalWS, out float3 Specular, out float Smoothness, out float Occlusion, out float3 Emission, out float Alpha, out float DepthOffset )
{
	ImpostorOutput o = ( ImpostorOutput )0;
	SphereImpostorFragment( o, DepthOffset, PositionWS, FrameUVs, ViewPos,
		AlbedoMap, NormalsMap, SpecularMap, OcclusionMap, EmissionMap, PositionMap,
		ClipMask, TextureBias, FramesX, FramesY, DepthSize, ShadowBias, ShadowView, ForwardBias, HueVariation, BoundsMin, BoundsSize );
	Albedo = o.Albedo;
	NormalWS = o.WorldNormal;
	Specular = o.Specular;
	Smoothness = o.Smoothness;
	Occlusion = o.Occlusion;
	Emission = o.Emission;
	Alpha = o.Alpha;
}

#endif
