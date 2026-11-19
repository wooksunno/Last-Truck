// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_IMPOSTORS_OCTAHEDRON_SG_INCLUDED
#define AMPLIFY_IMPOSTORS_OCTAHEDRON_SG_INCLUDED

// octahedron impostor functions ported from OctahedronImpostorURP.shader (ase), deliberate changes are marked "sg:"; textures are
// UnityTexture2D sampled with SAMPLE_TEXTURE2D_BIAS (ase: sampler2D and tex2Dbias)

#include "AmplifyImpostorsCommonSG.hlsl"

inline void RayPlaneIntersectionUV( float3 normalOS, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )
{
	float lDotN = dot( rayDirection, normalOS );
	float p0l0DotN = dot( -rayPosition, normalOS );
	float t = p0l0DotN / lDotN;
	float3 p = rayDirection * t + rayPosition;
	float3 upVector = float3( 0, 1, 0 );
	float3 tangent = normalize( cross( upVector, normalOS ) + float3( -0.001, 0, 0 ) );
	float3 bitangent = cross( tangent, normalOS );
	float frameX = dot( p, tangent );
	float frameZ = dot( p, bitangent );
	uvs = -float2( frameX, frameZ );
	if( t <= 0.0 )
	uvs = 0;
	float3x3 worldToLocal = float3x3( tangent, bitangent, normalOS );
	localNormal = normalize( mul( worldToLocal, rayDirection ) );
}

// sg: _AI_AngleRange is a parameter, _Hemi material property instead of the Hemi keyword
float3 OctaFrameToVector( float2 frameIndex, int layout, float2 gridOrigin, float2 gridStep, float3x3 capBasis, float2 capScale, float2 fractionsPrevFrame, float4 _AI_AngleRange, bool _Hemi )
{
	if ( layout == 1 )
	{
		float2 yawPitch = radians( gridOrigin + frameIndex * gridStep + _AI_AngleRange.zw );
		float2 sinYawPitch, cosYawPitch;
		sincos( yawPitch, sinYawPitch, cosYawPitch );
		return float3( sinYawPitch.x * cosYawPitch.y, sinYawPitch.y, cosYawPitch.x * cosYawPitch.y );
	}
	float2 octa = frameIndex * fractionsPrevFrame * 2.0 - 1.0;
	if ( layout == 2 )
	{
		return mul( HemiOctahedronToVector( clamp( octa * capScale, -1, 1 ) ), capBasis );
	}
	if ( _Hemi )
	{
		return HemiOctahedronToVector( octa ).xzy;
	}
	return OctahedronToVector( octa ).xzy;
}

// sg: ImpostorClipPosDepthBias removed, see AmplifyImpostorsCommonSG.hlsl

// sg: material properties are parameters
inline void OctaImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos,
	float _Frames, float _FramesX, float _FramesY, float _ImpostorSize, float3 _Offset, float4 _AI_SizeOffset, float4 _AI_AngleRange, float _DepthSize, float _Parallax, float4 _HueVariation, bool _Hemi )
{
	float2 uvOffset = _AI_SizeOffset.zw;
	float parallax = -_Parallax;
	float UVscale = _ImpostorSize;
	float2 framesXY = float2( ( _FramesX > 0 ) ? _FramesX : _Frames, ( _FramesY > 0 ) ? _FramesY : _Frames );
	float2 prevFrame = framesXY - 1;
	float2 fractionsFrame = 1.0 / framesXY;
	float2 fractionsPrevFrame = 1.0 / max( prevFrame, 1 );
	float fractionsUVscale = 1.0 / UVscale;
	float2 parallaxScale = _DepthSize * fractionsUVscale * fractionsFrame * parallax;
	const float parallaxMinCos = 0.2;
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
	float2 uvExpansion = positionOS.xy;
	float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;
	float3 objectDir = billboard - objectCameraPosition;
	objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;
	float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
	float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
	int layout = ( yawRange < 360 || pitchRange < 180 ) ? ( ( yawRange <= 180 ) ? 2 : 1 ) : 0;
	float2 prevOctaFrame;
	float2 gridOrigin = 0;
	float2 gridStep = 0;
	float3x3 capBasis = 0;
	float2 capScale = 1;
	if ( layout == 2 )
	{
		CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
		prevOctaFrame = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 ) * prevFrame;
	}
	else if ( layout == 1 )
	{
		float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
		float pitch = degrees( asin( clamp( objectCameraDirection.y, -1, 1 ) ) ) - _AI_AngleRange.w;
		if ( yawRange >= 360 )
		{
			prevOctaFrame.x = frac( -yaw / 360.0 - 0.25 ) * framesXY.x;
			gridOrigin.x = -90;
			gridStep.x = -360.0 / framesXY.x;
		}
		else
		{
			prevOctaFrame.x = saturate( yaw / yawRange + 0.5 ) * prevFrame.x;
			gridOrigin.x = -yawRange * 0.5;
			gridStep.x = yawRange / max( prevFrame.x, 1 );
		}
		prevOctaFrame.y = saturate( pitch / pitchRange + 0.5 ) * prevFrame.y;
		gridOrigin.y = -pitchRange * 0.5;
		gridStep.y = pitchRange / max( prevFrame.y, 1 );
	}
	else
	{
		// sg: _Hemi
		float2 frameOcta;
		if ( _Hemi )
		{
			objectCameraDirection.y = max( 0.001, objectCameraDirection.y );
			frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
		}
		else
		{
			frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
		}
		prevOctaFrame = frameOcta * prevFrame;
	}
	float2 baseOctaFrame = floor( prevOctaFrame );
	float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );
	float3 octa1WorldY = OctaFrameToVector( baseOctaFrame, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame, _AI_AngleRange, _Hemi );
	if ( all( framesXY == 1 ) )
	{
		octa1WorldY = objectCameraDirection;
	}
	float3 octa1LocalY;
	float2 uvFrame1;
	RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );
	float2 uvParallax1 = octa1LocalY.xy / max( -octa1LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;
	uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );
	float2 fractPrevOctaFrame = frac( prevOctaFrame );
	float2 cornerDifference = lerp( float2( 0,1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ));
	float3 octa2WorldY = OctaFrameToVector( baseOctaFrame + cornerDifference, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame, _AI_AngleRange, _Hemi );
	float3 octa2LocalY;
	float2 uvFrame2;
	RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );
	float2 uvParallax2 = octa2LocalY.xy / max( -octa2LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );
	uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );
	float3 octa3WorldY = OctaFrameToVector( baseOctaFrame + 1, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame, _AI_AngleRange, _Hemi );
	float3 octa3LocalY;
	float2 uvFrame3;
	RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );
	float2 uvParallax3 = octa3LocalY.xy / max( -octa3LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );
	uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );
	if ( all( framesXY == 1 ) )
	{
		uvsFrame2 = uvsFrame1;
		uvsFrame3 = uvsFrame1;
	}
	octaFrame.x = fractPrevOctaFrame.x - fractPrevOctaFrame.y;
	octaFrame.y = min( fractPrevOctaFrame.x, fractPrevOctaFrame.y );
	octaFrame.zw = fractionOctaFrame + 0.5 * fractionsFrame;
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
inline void OctaImpostorFragment( inout ImpostorOutput o, out float depthOffset, float3 positionWS, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos,
	UnityTexture2D _Albedo, UnityTexture2D _Normals, UnityTexture2D _Specular, UnityTexture2D _Occlusion, UnityTexture2D _Emission, UnityTexture2D _Position,
	float _ClipMask, float _TextureBias, float _Frames, float _FramesX, float _FramesY, float _DepthSize, float _AI_ShadowBias, float _AI_ShadowView, float _AI_ForwardBias,
	float4 _HueVariation, float3 _AI_BoundsMin, float3 _AI_BoundsSize )
{
	float3 weights;
	weights.y = abs( octaFrame.x );
	weights.z = octaFrame.y;
	weights.x = 1 - weights.y - weights.z;
	float parallax1 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, uvsFrame1.zw, -1 ).a;
	float parallax2 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, uvsFrame2.zw, -1 ).a;
	float parallax3 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, uvsFrame3.zw, -1 ).a;
	float2 parallax1_uv = ( ( 0.5 - parallax1 ) * uvsFrame1.xy ) + uvsFrame1.zw;
	float2 parallax2_uv = ( ( 0.5 - parallax2 ) * uvsFrame2.xy ) + uvsFrame2.zw;
	float2 parallax3_uv = ( ( 0.5 - parallax3 ) * uvsFrame3.xy ) + uvsFrame3.zw;
	float4 albedo1 = SAMPLE_TEXTURE2D_BIAS( _Albedo.tex, _Albedo.samplerstate, parallax1_uv, _TextureBias );
	float4 albedo2 = SAMPLE_TEXTURE2D_BIAS( _Albedo.tex, _Albedo.samplerstate, parallax2_uv, _TextureBias );
	float4 albedo3 = SAMPLE_TEXTURE2D_BIAS( _Albedo.tex, _Albedo.samplerstate, parallax3_uv, _TextureBias );
	float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;
	o.Alpha = blendedAlbedo.a;
	clip( o.Alpha - _ClipMask );
	float t = step( 0, octaFrame.x );
	float4 cornerDifference = float4( 0, 0, t, 1 - t );
	float2 framesXY = float2( ( _FramesX > 0 ) ? _FramesX : _Frames, ( _FramesY > 0 ) ? _FramesY : _Frames );
	float4 dist12 = abs( ( float4( parallax1_uv, parallax2_uv ) - octaFrame.zwzw ) * framesXY.xyxy - cornerDifference );
	float2 dist3 = abs( ( parallax3_uv - octaFrame.zw ) * framesXY - 1 );
	float3 inside = step( float3( max( dist12.x, dist12.y ), max( dist12.z, dist12.w ), max( dist3.x, dist3.y ) ), 0.5 );
	clip( dot( inside, weights ) - 0.5 );
	#if defined( EFFECT_HUE_VARIATION )
	half3 shiftedColor = lerp( blendedAlbedo.rgb, _HueVariation.rgb, viewPos.w );
	half maxBase = max( blendedAlbedo.r, max( blendedAlbedo.g, blendedAlbedo.b ) );
	half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
	maxBase /= newMaxBase;
	maxBase = maxBase * 0.5f + 0.5f;
	shiftedColor.rgb *= maxBase;
	blendedAlbedo.rgb = saturate( shiftedColor );
	#endif
	o.Albedo = blendedAlbedo.rgb;
	float3 normal1 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, parallax1_uv, _TextureBias ).rgb;
	float3 normal2 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, parallax2_uv, _TextureBias ).rgb;
	float3 normal3 = SAMPLE_TEXTURE2D_BIAS( _Normals.tex, _Normals.samplerstate, parallax3_uv, _TextureBias ).rgb;
	float3 blendedNormal = normal1 * weights.x  + normal2 * weights.y + normal3 * weights.z;
	float3 objectNormal = blendedNormal * 2.0 - 1.0;
	o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal ) );
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = parallax1 * weights.x + parallax2 * weights.y + parallax3 * weights.z;
	depth = ( depth - 0.5 ) * _DepthSize * objectScale;
	// sg: no "depth -= 0.001" standard rp deferred fix, dead in ase urp and hdrp
	#if defined( _SPECULARMAP )
	float4 specular1 = SAMPLE_TEXTURE2D_BIAS( _Specular.tex, _Specular.samplerstate, parallax1_uv, _TextureBias );
	float4 specular2 = SAMPLE_TEXTURE2D_BIAS( _Specular.tex, _Specular.samplerstate, parallax2_uv, _TextureBias );
	float4 specular3 = SAMPLE_TEXTURE2D_BIAS( _Specular.tex, _Specular.samplerstate, parallax3_uv, _TextureBias );
	float4 blendedSpecular = specular1 * weights.x  + specular2 * weights.y + specular3 * weights.z;
	o.Specular = blendedSpecular.rgb;
	o.Smoothness = blendedSpecular.a;
	#else
	o.Specular = 0;
	o.Smoothness = 0;
	#endif
	#if defined( _OCCLUSIONMAP )
	float occlusion1 = SAMPLE_TEXTURE2D_BIAS( _Occlusion.tex, _Occlusion.samplerstate, parallax1_uv, _TextureBias ).g;
	float occlusion2 = SAMPLE_TEXTURE2D_BIAS( _Occlusion.tex, _Occlusion.samplerstate, parallax2_uv, _TextureBias ).g;
	float occlusion3 = SAMPLE_TEXTURE2D_BIAS( _Occlusion.tex, _Occlusion.samplerstate, parallax3_uv, _TextureBias ).g;
	o.Occlusion = occlusion1 * weights.x  + occlusion2 * weights.y + occlusion3 * weights.z;
	#else
	o.Occlusion = 1;
	#endif
	#if defined( _EMISSIONMAP )
	float3 emission1 = SAMPLE_TEXTURE2D_BIAS( _Emission.tex, _Emission.samplerstate, parallax1_uv, _TextureBias ).rgb;
	float3 emission2 = SAMPLE_TEXTURE2D_BIAS( _Emission.tex, _Emission.samplerstate, parallax2_uv, _TextureBias ).rgb;
	float3 emission3 = SAMPLE_TEXTURE2D_BIAS( _Emission.tex, _Emission.samplerstate, parallax3_uv, _TextureBias ).rgb;
	o.Emission = emission1 * weights.x  + emission2 * weights.y + emission3 * weights.z;
	#else
	o.Emission = 0;
	#endif
	#if defined( _POSITIONMAP )
	float4 position1 = SAMPLE_TEXTURE2D_BIAS( _Position.tex, _Position.samplerstate, parallax1_uv, _TextureBias );
	float4 position2 = SAMPLE_TEXTURE2D_BIAS( _Position.tex, _Position.samplerstate, parallax2_uv, _TextureBias );
	float4 position3 = SAMPLE_TEXTURE2D_BIAS( _Position.tex, _Position.samplerstate, parallax3_uv, _TextureBias );
	float4 blendedPosition = position1 * weights.x  + position2 * weights.y + position3 * weights.z;
	float3 objectPosition = blendedPosition.xyz * _AI_BoundsSize + _AI_BoundsMin;
	float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;
	if ( blendedPosition.a > 0 )
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
	// sg: the signed depth offset of the final position instead of its clip position
	depthOffset = AI_ImpostorDepthOffset( viewPos.xyz, positionWS );
}

// shader graph entry points (file mode custom functions)

void AIOctaImpostorVertex_float( float3 PositionOS, float Frames, float FramesX, float FramesY, float ImpostorSize, float3 Offset, float4 SizeOffset, float4 AngleRange,
	float DepthSize, float Parallax, float4 HueVariation, bool Hemi,
	out float3 OutPositionOS, out float3 OutNormalOS, out float3 OutTangentOS, out float4 UVsFrame1, out float4 UVsFrame2, out float4 UVsFrame3, out float4 OctaFrame, out float4 ViewPos )
{
	float3 positionOS = PositionOS;
	float4 tangentOS;
	OctaImpostorVertex( positionOS, OutNormalOS, tangentOS, UVsFrame1, UVsFrame2, UVsFrame3, OctaFrame, ViewPos,
		Frames, FramesX, FramesY, ImpostorSize, Offset, SizeOffset, AngleRange, DepthSize, Parallax, HueVariation, Hemi );
	OutPositionOS = positionOS;
	OutTangentOS = tangentOS.xyz;
}

void AIOctaImpostorFragment_float( float3 PositionWS, float4 UVsFrame1, float4 UVsFrame2, float4 UVsFrame3, float4 OctaFrame, float4 ViewPos,
	UnityTexture2D AlbedoMap, UnityTexture2D NormalsMap, UnityTexture2D SpecularMap, UnityTexture2D OcclusionMap, UnityTexture2D EmissionMap, UnityTexture2D PositionMap,
	float ClipMask, float TextureBias, float Frames, float FramesX, float FramesY, float DepthSize, float ShadowBias, float ShadowView, float ForwardBias,
	float4 HueVariation, float3 BoundsMin, float3 BoundsSize,
	out float3 Albedo, out float3 NormalWS, out float3 Specular, out float Smoothness, out float Occlusion, out float3 Emission, out float Alpha, out float DepthOffset )
{
	ImpostorOutput o = ( ImpostorOutput )0;
	OctaImpostorFragment( o, DepthOffset, PositionWS, UVsFrame1, UVsFrame2, UVsFrame3, OctaFrame, ViewPos,
		AlbedoMap, NormalsMap, SpecularMap, OcclusionMap, EmissionMap, PositionMap,
		ClipMask, TextureBias, Frames, FramesX, FramesY, DepthSize, ShadowBias, ShadowView, ForwardBias, HueVariation, BoundsMin, BoundsSize );
	Albedo = o.Albedo;
	NormalWS = o.WorldNormal;
	Specular = o.Specular;
	Smoothness = o.Smoothness;
	Occlusion = o.Occlusion;
	Emission = o.Emission;
	Alpha = o.Alpha;
}

#endif
