// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFYIMPOSTORS_INCLUDED
#define AMPLIFYIMPOSTORS_INCLUDED

#include "AmplifyImpostorsConfig.cginc"

#define AI_DEPTH_OPTIMIZATION // active by default; keep option only for debugging

#if defined( UNITY_REVERSED_Z ) && ( SHADER_TARGET >= 45 ) && defined( AI_DEPTH_OPTIMIZATION )
	#define AI_POSITION_QUALIFIERS linear noperspective centroid
	#define AI_DEPTH_SEMANTIC SV_DepthLessEqual
#else
	#define AI_POSITION_QUALIFIERS
	#define AI_DEPTH_SEMANTIC SV_Depth
#endif

#if ( defined( AI_HD_RENDERPIPELINE ) || defined( AI_LW_RENDERPIPELINE ) ) && !defined( AI_RENDERPIPELINE )
	#define AI_RENDERPIPELINE
#endif

float2 VectorToOctahedron( float3 N )
{
	N /= dot( 1.0, abs( N ) );
	if ( N.z <= 0 )
	{
		N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
	}
	return N.xy;
}

float2 VectorToHemiOctahedron( float3 N )
{
	N.xy /= dot( 1.0, abs( N ) );
	return float2( N.x + N.y, N.x - N.y );
}

float3 OctahedronToVector( float2 Oct )
{
	float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );
	if ( N.z < 0 )
	{
		N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );
	}
	return normalize( N );
}

float3 HemiOctahedronToVector( float2 Oct )
{
	Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) *0.5;
	float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );
	return normalize( N );
}

sampler2D _Albedo;
sampler2D _Normals;
#if defined( AI_RENDERPIPELINE )
	TEXTURE2D( _Specular );
	SAMPLER( sampler_Specular );
#else
	sampler2D _Specular;
#endif
sampler2D _Occlusion;
sampler2D _Emission;
sampler2D _Position;

CBUFFER_START( UnityPerMaterial )
	float _FramesX;
	float _FramesY;
	float _Frames;
	float _ImpostorSize;
	float _Parallax;
	float _TextureBias;
	float _ClipMask;
	float _DepthSize;
	float _AI_ShadowBias;
	float _AI_ShadowView;
	float _AI_ForwardBias;
	float4 _Offset;
	float4 _AI_SizeOffset;
	float4 _AI_AngleRange;
	float4 _AI_BoundsMin;
	float4 _AI_BoundsSize;
#if defined( EFFECT_HUE_VARIATION )
	half4 _HueVariation;
#endif
CBUFFER_END

#if defined( AI_RENDERPIPELINE )
	#define AI_SAMPLEBIAS( textureName, samplerName, coord2, bias ) SAMPLE_TEXTURE2D_BIAS( textureName, samplerName, coord2, bias )
	#define AI_ObjectToWorld GetObjectToWorldMatrix()
	#define AI_WorldToObject GetWorldToObjectMatrix()

	#define AI_INV_TWO_PI  INV_TWO_PI
	#define AI_PI          PI
	#define AI_INV_PI      INV_PI
#else
	#define AI_SAMPLEBIAS( textureName, samplerName, coord2, bias ) tex2Dbias( textureName, float4( coord2, 0, bias ) )
	#define AI_ObjectToWorld unity_ObjectToWorld
	#define AI_WorldToObject unity_WorldToObject

	#define AI_INV_TWO_PI  UNITY_INV_TWO_PI
	#define AI_PI          UNITY_PI
	#define AI_INV_PI      UNITY_INV_PI
#endif

inline void RayPlaneIntersectionUV( float3 normal, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )
{
	// n = normal
	// p0 = ( 0, 0, 0 ) assuming center as zero
	// l0 = ray position
	// l = ray direction
	// solving to:
	// t = distance along ray that intersects the plane = ( ( p0 - l0 ) . n ) / ( l . n )
	// p = intersection point

	float lDotN = dot( rayDirection, normal ); // l . n
	float p0l0DotN = dot( -rayPosition, normal ); // ( p0 - l0 ) . n

	float t = p0l0DotN / lDotN; // if > 0 then it's intersecting
	float3 p = rayDirection * t + rayPosition;

	// create frame UVs
	float3 upVector = float3( 0, 1, 0 );
	float3 tangent = normalize( cross( upVector, normal ) + float3( -0.001, 0, 0 ) );
	float3 bitangent = cross( tangent, normal );

	float frameX = dot( p, tangent );
	float frameZ = dot( p, bitangent );

	uvs = -float2( frameX, frameZ ); // why negative???

	if ( t <= 0.0 ) // not intersecting
	{
		uvs = 0;
	}

	float3x3 worldToLocal = float3x3( tangent, bitangent, normal ); // TBN (same as doing separate dots?, assembly looks the same)
	localNormal = normalize( mul( worldToLocal, rayDirection ) );
}

float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos )
{
#if defined( AI_DEPTH_OPTIMIZATION )
	const float depthOffset = _DepthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );
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
#endif
	return clipPos;
}

// Cap layout: the hemi-octahedral map re-centred on the range centre; basis rows are the map axes and forward of the centre, scale is the map coordinate at which the yaw and pitch ranges reach the map edges
void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )
{
	float2 sinCenter, cosCenter;
	sincos( radians( center ), sinCenter, cosCenter );
	float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );
	float3 right = float3( cosCenter.x, 0, -sinCenter.x );
	float3 up = cross( forward, right );
	basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward ); // the hemi-octahedral map axes run along the diagonals of right and up
	float2 sinHalf, cosHalf;
	sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );
	scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );
}

// View direction of a frame grid cell: layout 0 is the native octahedral map, 1 the yaw/pitch grid around the range centre, 2 the cap ( hemi-octahedral map re-centred on the range centre )
float3 OctaFrameToVector( float2 frameIndex, int layout, float2 gridOrigin, float2 gridStep, float3x3 capBasis, float2 capScale, float2 fractionsPrevFrame )
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
#if defined( _HEMI_ON )
	return HemiOctahedronToVector( octa ).xzy;
#else
	return OctahedronToVector( octa ).xzy;
#endif
}

inline void OctaImpostorVertex( inout float3 vertex, out float3 normal, out float4 tangent, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	// Inputs
	float2 uvOffset = _AI_SizeOffset.zw;
	float parallax = -_Parallax; // check sign later
	float UVscale = _ImpostorSize;
	float2 framesXY = float2( ( _FramesX > 0 ) ? _FramesX : _Frames, ( _FramesY > 0 ) ? _FramesY : _Frames ); // materials baked before decoupled octahedron frames only carry _Frames
	float2 prevFrame = framesXY - 1;
	float2 fractionsFrame = 1.0 / framesXY;
	float2 fractionsPrevFrame = 1.0 / max( prevFrame, 1 ); // single frame: avoid division by zero
	float fractionsUVscale = 1.0 / UVscale;
	// parallax: a texel at depth d above the frame plane is seen shifted by d * dir.xy / dir.z along it; fractionsUVscale * fractionsFrame takes object units to frame UV like uvFrameN, the clamp caps the shift at 5x the depth near grazing
	float2 parallaxScale = _DepthSize * fractionsUVscale * fractionsFrame * parallax;
	const float parallaxMinCos = 0.2;
	float3 worldCameraPos;

#if ( defined( SHADERPASS ) && ( defined( SHADERPASS_DEPTHNORMALSONLY ) && SHADERPASS == SHADERPASS_DEPTHNORMALSONLY ) ) || defined( UNITY_PASS_SHADOWCASTER )
	float3 worldOrigin = 0;
	float4 perspective = float4( 0, 0, 0, 1 );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		perspective = float4( 0, 0, 5000, 0 );
		worldOrigin = AI_ObjectToWorld._m03_m13_m23;
	}
	worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
#else
	// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
	}
	else
	{
	#if defined( AI_RENDERPIPELINE )
		worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	#else
		#if defined( SCENEPICKINGPASS ) || defined( SCENESELECTIONPASS )
			worldCameraPos = UNITY_MATRIX_I_V._m03_m13_m23;
		#else
			worldCameraPos = _WorldSpaceCameraPos;
		#endif
	#endif
	}
#endif

	float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz; //ray origin
	float3 objectCameraDirection = normalize( objectCameraPosition );

	// Create orthogonal vectors to define the billboard
	float3 upVector = float3( 0, 1, 0 );
	float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
	float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );

	// Billboard
	float2 uvExpansion = vertex.xy;
	float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;

	float3 objectDir = billboard - objectCameraPosition; // ray direction

	// @diogo: quantize to avoid a compiler bug causing mismatching values between passes
	objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;

	// Frame grid coordinates
	// _AI_AngleRange = ( yaw range, pitch range, centre yaw, centre pitch ); zero ranges mean the native map ( materials baked before the property existed )
	// layout 0: native octahedral map; 1: yaw/pitch grid around the centre ( yaw range above 180, or wrapping ); 2: cap, the hemi-octahedral map re-centred on the range centre ( yaw range up to 180 )
	float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
	float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
	int layout = ( yawRange < 360 || pitchRange < 180 ) ? ( ( yawRange <= 180 ) ? 2 : 1 ) : 0;
	float2 prevOctaFrame;
	float2 gridOrigin = 0; // yaw/pitch grid: yaw/pitch of frame ( 0, 0 ) relative to the centre, in degrees
	float2 gridStep = 0; // yaw/pitch grid: yaw/pitch step between frames, in degrees
	float3x3 capBasis = 0; // cap: rows are the map axes and forward of the centre
	float2 capScale = 1; // cap: map coordinate at which the ranges reach the map edges
	if ( layout == 2 )
	{
		CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
		prevOctaFrame = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 ) * prevFrame; // views behind the cap land on its rim
	}
	else if ( layout == 1 )
	{
		float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
		float pitch = degrees( asin( clamp( objectCameraDirection.y, -1, 1 ) ) ) - _AI_AngleRange.w;
		if ( yawRange >= 360 )
		{
			// full turn: columns wrap around, frame 0 baked at -90 degrees and later columns at decreasing yaw; column framesXY.x resolves to column 0 through the texture wrap mode
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
		// Octahedron Frame
#if defined( _HEMI_ON )
		objectCameraDirection.y = max( 0.001, objectCameraDirection.y );
		float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
#else
		float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;
#endif
		prevOctaFrame = frameOcta * prevFrame;
	}

	// Setup for octahedron
	float2 baseOctaFrame = floor( prevOctaFrame );
	float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );

	// Octa 1
	float3 octa1WorldY = OctaFrameToVector( baseOctaFrame, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );

	if ( all( framesXY == 1 ) )
	{
		// single frame: plain camera-facing billboard
		octa1WorldY = objectCameraDirection;
	}

	float3 octa1LocalY;
	float2 uvFrame1;
	RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );

	float2 uvParallax1 = octa1LocalY.xy / max( -octa1LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;
	uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );

	// Octa 2
	float2 fractPrevOctaFrame = frac( prevOctaFrame );
	float2 cornerDifference = lerp( float2( 0, 1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ) );
	float3 octa2WorldY = OctaFrameToVector( baseOctaFrame + cornerDifference, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );

	float3 octa2LocalY;
	float2 uvFrame2;
	RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );

	float2 uvParallax2 = octa2LocalY.xy / max( -octa2LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );
	uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );

	// Octa 3
	float3 octa3WorldY = OctaFrameToVector( baseOctaFrame + 1, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );

	float3 octa3LocalY;
	float2 uvFrame3;
	RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );

	float2 uvParallax3 = octa3LocalY.xy / max( -octa3LocalY.z, parallaxMinCos ) * parallaxScale;
	uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );
	uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );

	if ( all( framesXY == 1 ) )
	{
		// single frame: every slot samples frame 1, which the blend weights ( 1, 0, 0 ) select since octaFrame.xy is zero
		uvsFrame2 = uvsFrame1;
		uvsFrame3 = uvsFrame1;
	}

	// Blend weight inputs ( constant across the mesh ) and frame cell centre, used by the frame rect clip
	octaFrame.x = fractPrevOctaFrame.x - fractPrevOctaFrame.y;
	octaFrame.y = min( fractPrevOctaFrame.x, fractPrevOctaFrame.y );
	octaFrame.zw = fractionOctaFrame + 0.5 * fractionsFrame;

	vertex = billboard + _Offset.xyz;
	normal = objectCameraDirection;
	tangent = float4( objectHorizontalVector, 1 );

#if defined( AI_RENDERPIPELINE )
	viewPos.xyz = TransformWorldToView( TransformObjectToWorld( vertex.xyz ) );
#else
	viewPos.xyz = UnityObjectToViewPos( vertex.xyz );
#endif

#if defined( EFFECT_HUE_VARIATION )
	float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
#else
	viewPos.w = 0;
#endif
}

inline void OctaImpostorVertex( inout float4 vertex, out float3 normal, out float4 tangent, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	OctaImpostorVertex( vertex.xyz, normal, tangent, uvsFrame1, uvsFrame2, uvsFrame3, octaFrame, viewPos );
}

inline void OctaImpostorVertex( inout float4 vertex, out float3 normal, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )
{
	float4 tangent;
	OctaImpostorVertex( vertex.xyz, normal, tangent, uvsFrame1, uvsFrame2, uvsFrame3, octaFrame, viewPos );
}

inline void OctaImpostorFragment( inout SurfaceOutputStandardSpecular o, out float4 clipPos, out float3 worldPos, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos )
{
	// Weights ( constant across the mesh, see OctaImpostorVertex )
	float3 weights;
	weights.y = abs( octaFrame.x );
	weights.z = octaFrame.y;
	weights.x = 1 - weights.y - weights.z;

	float parallax1 = tex2Dbias( _Normals, float4( uvsFrame1.zw, 0, -1 ) ).a;
	float parallax2 = tex2Dbias( _Normals, float4( uvsFrame2.zw, 0, -1 ) ).a;
	float parallax3 = tex2Dbias( _Normals, float4( uvsFrame3.zw, 0, -1 ) ).a;

	float2 parallax1_uv = ( ( 0.5 - parallax1 ) * uvsFrame1.xy ) + uvsFrame1.zw;
	float2 parallax2_uv = ( ( 0.5 - parallax2 ) * uvsFrame2.xy ) + uvsFrame2.zw;
	float2 parallax3_uv = ( ( 0.5 - parallax3 ) * uvsFrame3.xy ) + uvsFrame3.zw;

	// albedo alpha
	float4 albedo1 = tex2Dbias( _Albedo, float4( parallax1_uv, 0, _TextureBias ) );
	float4 albedo2 = tex2Dbias( _Albedo, float4( parallax2_uv, 0, _TextureBias ) );
	float4 albedo3 = tex2Dbias( _Albedo, float4( parallax3_uv, 0, _TextureBias ) );
	float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;

	// Early clip
	o.Alpha = blendedAlbedo.a;
#if !defined( AI_SKIP_ALPHA_CLIP )
	clip( o.Alpha - _ClipMask );
#endif

	// Clip samples outside their frame rect (parallax overshoot into neighbour frames or past the texture border)
	float t = step( 0, octaFrame.x );
	float4 cornerDifference = float4( 0, 0, t, 1 - t );
	float2 framesXY = float2( ( _FramesX > 0 ) ? _FramesX : _Frames, ( _FramesY > 0 ) ? _FramesY : _Frames ); // materials baked before decoupled octahedron frames only carry _Frames
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

	// Normal
	float3 normal1 = tex2Dbias( _Normals, float4( parallax1_uv, 0, _TextureBias ) ).rgb;
	float3 normal2 = tex2Dbias( _Normals, float4( parallax2_uv, 0, _TextureBias ) ).rgb;
	float3 normal3 = tex2Dbias( _Normals, float4( parallax3_uv, 0, _TextureBias ) ).rgb;
	float3 blendedNormal = normal1 * weights.x  + normal2 * weights.y + normal3 * weights.z;

	float3 objectNormal = blendedNormal * 2.0 - 1.0;
	float3 worldNormal = normalize( mul( (float3x3)AI_ObjectToWorld, objectNormal ) );
	o.Normal = worldNormal;

	// Depth
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = parallax1 * weights.x + parallax2 * weights.y + parallax3 * weights.z;
	depth = ( depth - 0.5 ) * _DepthSize * objectScale;

	// Specular Smoothness
#if defined( _SPECULARMAP )
	float4 specular1 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax1_uv, _TextureBias );
	float4 specular2 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax2_uv, _TextureBias );
	float4 specular3 = AI_SAMPLEBIAS( _Specular, sampler_Specular, parallax3_uv, _TextureBias );
	float4 blendedSpecular = specular1 * weights.x  + specular2 * weights.y + specular3 * weights.z;
	o.Specular = blendedSpecular.rgb;
	o.Smoothness = blendedSpecular.a;
#else
	o.Specular = 0;
	o.Smoothness = 0;
#endif

#if defined( _OCCLUSIONMAP )
	float occlusion1 = tex2Dbias( _Occlusion, float4( parallax1_uv, 0, _TextureBias ) ).g;
	float occlusion2 = tex2Dbias( _Occlusion, float4( parallax2_uv, 0, _TextureBias ) ).g;
	float occlusion3 = tex2Dbias( _Occlusion, float4( parallax3_uv, 0, _TextureBias ) ).g;
	o.Occlusion = occlusion1 * weights.x  + occlusion2 * weights.y + occlusion3 * weights.z;
#else
	o.Occlusion = 1;
#endif

#if defined( _EMISSIONMAP )
	// Emission Occlusion
	float3 emission1 = tex2Dbias( _Emission, float4( parallax1_uv, 0, _TextureBias ) ).rgb;
	float3 emission2 = tex2Dbias( _Emission, float4( parallax2_uv, 0, _TextureBias ) ).rgb;
	float3 emission3 = tex2Dbias( _Emission, float4( parallax3_uv, 0, _TextureBias ) ).rgb;
	o.Emission = emission1 * weights.x  + emission2 * weights.y + emission3 * weights.z;
#else
	o.Emission = 0;
#endif

#if defined( _POSITIONMAP )
	// Position
	float4 position1 = tex2Dbias( _Position, float4( parallax1_uv, 0, _TextureBias ) );
	float4 position2 = tex2Dbias( _Position, float4( parallax2_uv, 0, _TextureBias ) );
	float4 position3 = tex2Dbias( _Position, float4( parallax3_uv, 0, _TextureBias ) );
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
#if defined( AI_RENDERPIPELINE )
	#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack
		{
			viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
		}
		else
		{
			viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
		}
	#elif defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#endif

	worldPos = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
	clipPos = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );

#if defined( AI_RENDERPIPELINE )
	#if defined( UNITY_PASS_SHADOWCASTER ) && !defined( SHADERPASS )
		#if defined( UNITY_REVERSED_Z )
			clipPos.z = min( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#else
			clipPos.z = max( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#endif
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		clipPos = UnityApplyLinearShadowBias( clipPos );
	#endif
#endif

	clipPos.xyz /= clipPos.w;

	if ( UNITY_NEAR_CLIP_VALUE < 0 )
	{
		clipPos = clipPos * 0.5 + 0.5;
	}
}

inline void SphereImpostorVertex( inout float3 vertex, out float3 normal, out float4 tangent, out float4 frameUVs, out float4 viewPos )
{
	// INPUTS
	float2 uvOffset = _AI_SizeOffset.zw;
	float sizeX = _FramesX;
	float sizeY = max( _FramesY - 1, 1 ); // adjusted; single row: avoid division by zero, row index is clamped below
	float UVscale = _ImpostorSize;
	float4 fractions = 1 / float4( sizeX, _FramesY, sizeY, UVscale );
	float2 sizeFraction = fractions.xy;
	float fractionsUVscale = fractions.w;
	float3 worldCameraPos;

#if ( defined(SHADERPASS) && (defined(SHADERPASS_DEPTHNORMALSONLY) && SHADERPASS == SHADERPASS_DEPTHNORMALSONLY) ) || defined(UNITY_PASS_SHADOWCASTER)
	float3 worldOrigin = 0;
	float4 perspective = float4( 0, 0, 0, 1 );
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		perspective = float4( 0, 0, 5000, 0 );
		worldOrigin = AI_ObjectToWorld._m03_m13_m23;
	}
	worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;
#else
	// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )
	{
		worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;
	}
	else
	{
	#if defined( AI_RENDERPIPELINE )
		worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	#else
		worldCameraPos = _WorldSpaceCameraPos;
	#endif
	}
#endif

	float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - _Offset.xyz; //ray origin
	float3 objectCameraDirection = normalize( objectCameraPosition );

	// Create orthogonal vectors to define the billboard
	float3 upVector = float3( 0,1,0 );
	float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );
	float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );

	// Frame selection
	// _AI_AngleRange = ( yaw range, pitch range, centre yaw, centre pitch ); zero ranges mean the full sphere ( materials baked before the property existed )
	float yawRange = ( _AI_AngleRange.x > 0 ) ? _AI_AngleRange.x : 360;
	float pitchRange = ( _AI_AngleRange.y > 0 ) ? _AI_AngleRange.y : 180;
	float verticalDot = dot( objectCameraDirection, upVector );
	float frameX;
	float frameY;
	float yRot;
	if ( yawRange <= 180 )
	{
		// cap: nearest frame on the hemi-octahedral map re-centred on the range centre; no billboard roll compensation
		float3x3 capBasis;
		float2 capScale;
		CapLayout( yawRange, pitchRange, _AI_AngleRange.zw, capBasis, capScale );
		float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 ); // views behind the cap land on its rim
		frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );
		frameY = min( floor( cell.y * sizeY + 0.5 ), _FramesY - 1 );
		yRot = 0;
	}
	else
	{
		// yaw/pitch grid: camera yaw and pitch in degrees relative to the range centre ( yaw 0 at +Z, positive towards +X; positive pitch from above )
		float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - _AI_AngleRange.z / 360.0 + 0.5 ) - 0.5 ) * 360.0;
		float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - _AI_AngleRange.w;

		// horizontal frame and residual yaw ( drives the billboard roll below )
		float residualYaw;
		if ( yawRange >= 360 )
		{
			// full turn: frames wrap around, frame 0 baked at -90 degrees and later frames at decreasing yaw
			float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;
			frameX = floor( a );
			residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );
		}
		else
		{
			// limited range: frames span the range inclusively and clamp at its edges
			float yawStep = yawRange / max( sizeX - 1, 1 );
			frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );
			residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw; // same sense as the full turn, which runs the other way
		}
		yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0; // single column: plain billboard

		// vertical frame
		frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), _FramesY - 1 );
	}

	// Billboard rotation
	float2 uvExpansion = vertex.xy;
	float cosY = cos( yRot );
	float sinY = sin( yRot );
	float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );

	// Billboard
	float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;

	// Frame coords
	float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );
	float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;

	frameUVs.xy = frameUV - uvOffset;

	// Parallax
#if defined( _USE_PARALLAX_ON )
	float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );
	float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );
	float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );
	// shift = depth * dir.xy / dir.z taken to frame UV, see OctaImpostorVertex
	frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * _DepthSize * fractionsUVscale * sizeFraction * _Parallax;
#else
	frameUVs.zw = 0;
#endif

	vertex = billboard + _Offset.xyz;
	normal = objectCameraDirection;
	tangent = float4( objectHorizontalVector, 1 );

#if defined( AI_RENDERPIPELINE )
	viewPos.xyz = TransformWorldToView( TransformObjectToWorld( vertex.xyz ) );
#else
	viewPos.xyz = UnityObjectToViewPos( vertex.xyz );
#endif

#if defined( EFFECT_HUE_VARIATION )
	float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	viewPos.w = saturate( hueVariationAmount * _HueVariation.a );
#else
	viewPos.w = 0;
#endif
}

inline void SphereImpostorVertex( inout float4 vertex, out float3 normal, out float4 tangent, out float4 frameUVs, out float4 viewPos )
{
	SphereImpostorVertex( vertex.xyz, normal, tangent, frameUVs, viewPos );
}

inline void SphereImpostorVertex( inout float4 vertex, out float3 normal, out float4 frameUVs, out float4 viewPos )
{
	float4 tangent;
	SphereImpostorVertex( vertex.xyz, normal, tangent, frameUVs, viewPos );
}

inline void SphereImpostorFragment( inout SurfaceOutputStandardSpecular o, out float4 clipPos, out float3 worldPos, float4 frameUV, float4 viewPos )
{
#if defined( _USE_PARALLAX_ON )
	float parallax = tex2Dbias( _Normals, float4( frameUV.xy, 0, -1 ) ).a;
	float2 frameCount = float2( _FramesX, _FramesY );
	float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;
	// Clip samples outside the frame rect (parallax overshoot into neighbour frames or past the texture border)
	float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );
	frameUV.xy += parallaxOffset;
	clip( 0.5 - max( dist.x, dist.y ) );
#endif

	// Albedo + Alpha
	float4 albedo = tex2Dbias( _Albedo, float4( frameUV.xy, 0, _TextureBias ) );

	// Early clip
	o.Alpha = albedo.a;
#if !defined( AI_SKIP_ALPHA_CLIP )
	clip( o.Alpha - _ClipMask );
#endif

#if defined( EFFECT_HUE_VARIATION )
	half3 shiftedColor = lerp( albedo.rgb, _HueVariation.rgb, viewPos.w );
	half maxBase = max( albedo.r, max( albedo.g, albedo.b ) );
	half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );
	maxBase /= newMaxBase;
	maxBase = maxBase * 0.5f + 0.5f;
	shiftedColor.rgb *= maxBase;
	albedo.rgb = saturate( shiftedColor );
#endif
	o.Albedo = albedo.rgb;

	// Normal
	float4 normalSample = tex2Dbias( _Normals, float4( frameUV.xy, 0, _TextureBias ) );
	float4 objectNormal = normalSample * 2 - 1;
	float3 worldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );
	o.Normal = worldNormal;

	// Depth
	const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );
	float depth = ( normalSample.a - 0.5 ) * _DepthSize * objectScale;

	// Specular Smoothness
#if defined( _SPECULARMAP )
	float4 specular = AI_SAMPLEBIAS( _Specular, sampler_Specular, frameUV.xy, _TextureBias );
	o.Specular = specular.rgb;
	o.Smoothness = specular.a;
#else
	o.Specular = 0;
	o.Smoothness = 0;
#endif

	// Occlusion
#if defined( _OCCLUSIONMAP )
	o.Occlusion = tex2Dbias( _Occlusion, float4( frameUV.xy, 0, _TextureBias ) ).g;
#else
	o.Occlusion = 1;
#endif

	// Emission
#if defined( _EMISSIONMAP )
	o.Emission = tex2Dbias( _Emission, float4( frameUV.xy, 0, _TextureBias ) ).rgb;
#else
	o.Emission = 0;
#endif

#if defined( _POSITIONMAP )
	// Position
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
#if defined( AI_RENDERPIPELINE )
	#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack
		{
			viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
		}
		else
		{
			viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
		}
	#elif defined( UNITY_PASS_SHADOWCASTER )
		viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );
	#else
		viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );
	#endif
#endif

	worldPos = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;
	clipPos = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );

#if defined( AI_RENDERPIPELINE )
	#if defined( UNITY_PASS_SHADOWCASTER ) && !defined( SHADERPASS )
		#if defined( UNITY_REVERSED_Z )
			clipPos.z = min( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#else
			clipPos.z = max( clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE );
		#endif
	#endif
#else
	#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )
		clipPos = UnityApplyLinearShadowBias( clipPos );
	#endif
#endif

	clipPos.xyz /= clipPos.w;

	if ( UNITY_NEAR_CLIP_VALUE < 0 )
	{
		clipPos = clipPos * 0.5 + 0.5;
	}
}
#endif //AMPLIFYIMPOSTORS_INCLUDED
