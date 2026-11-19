// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_IMPOSTORS_COMMON_SG_INCLUDED
#define AMPLIFY_IMPOSTORS_COMMON_SG_INCLUDED

// helpers shared by the octahedron and spherical impostor ports (AmplifyImpostorsOctahedronSG.hlsl, AmplifyImpostorsSphericalSG.hlsl),
// the same in OctahedronImpostorURP.shader and SphericalImpostorURP.shader (ase), deliberate changes are marked "sg:"

#define AI_ObjectToWorld GetObjectToWorldMatrix()
#define AI_WorldToObject GetWorldToObjectMatrix()
#define AI_INV_TWO_PI  INV_TWO_PI
#define AI_PI          PI
#define AI_INV_PI      INV_PI

// sg: hdrp detection; SHADEROPTIONS_CAMERA_RELATIVE_RENDERING comes from the hdrp config package (ShaderConfig.cs.hlsl), which every
// hdrp pass includes first through the hdrp ShaderVariables.hlsl, urp never includes it; shader graph previews define it too (the shader
// graph ShaderConfig.cs.hlsl) but are not hdrp
#if defined( SHADEROPTIONS_CAMERA_RELATIVE_RENDERING ) && !defined( SHADERGRAPH_PREVIEW )
	#define AI_SG_HDRP
#endif

// sg: proper shadow caster detection, the ase urp and hdrp shadow passes never define UNITY_PASS_SHADOWCASTER (hdrp uses SHADERPASS_SHADOWS)
#if ( defined( SHADERPASS ) && defined( SHADERPASS_SHADOWCASTER ) && ( SHADERPASS == SHADERPASS_SHADOWCASTER ) ) || ( defined( SHADERPASS ) && defined( SHADERPASS_SHADOWS ) && ( SHADERPASS == SHADERPASS_SHADOWS ) ) || defined( UNITY_PASS_SHADOWCASTER )
	#define AI_SHADOWCASTER_PASS
#endif

// sg: direction toward the viewpoint that the pipeline's depth offset moves the surface against; hdrp: the V of
// ApplyDepthOffsetPositionInput (light position or direction in the shadow pass), urp: the depth offset sub-target's
#if defined( AI_SG_HDRP )
	#define AI_DEPTH_OFFSET_VIEW_DIR_WS( positionWS ) GetWorldSpaceNormalizeViewDir( positionWS )
#elif defined( AI_DEPTH_OFFSET_INCLUDED )
	#define AI_DEPTH_OFFSET_VIEW_DIR_WS( positionWS ) AI_GetViewOriginDirWS( positionWS )
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

// sg: ImpostorClipPosDepthBias removed, the card is rasterized where the vertex places it and the fragment writes plain SV_Depth
// (urp: AIDepthOffsetLitSubTarget, hdrp: lit depth offset), so the depth offset may be negative (surface in front of the card)

// sg: the camera position of the vertex functions, in the world space of the object matrix; shader graph previews turn camera relative
// rendering on (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING) but draw with absolute matrices, where GetCameraRelativePositionWS returns 0
// and the camera direction becomes NaN (a magenta preview), so they take it from the inverse view matrix
float3 AI_WorldSpaceCameraPos()
{
	#if defined( SHADERGRAPH_PREVIEW )
	return UNITY_MATRIX_I_V._m03_m13_m23;
	#else
	return GetCameraRelativePositionWS( _WorldSpaceCameraPos );
	#endif
}

// sg: the hue variation amount of the vertex functions, from the absolute object position in hdrp, where the object matrix is camera
// relative (the ase hdrp hue changes as the camera moves)
float AI_HueVariationAmount()
{
	#if defined( AI_SG_HDRP )
	float3 hueOrigin = GetAbsolutePositionWS( AI_ObjectToWorld._m03_m13_m23 );
	return frac( hueOrigin.x + hueOrigin.y + hueOrigin.z );
	#else
	return frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );
	#endif
}

// sg: replaces the clip position the ase fragment functions end with; the pipeline moves the surface along the view ray and writes its
// depth (urp sub-target: shadow bias and near clamp included), so this is the distance from the interpolated surface to the final view
// position along that ray, unclamped (negative in front of the card); positionWS and the final position are both in the pipeline's world
// space (camera relative in hdrp)
float AI_ImpostorDepthOffset( float3 viewPos, float3 positionWS )
{
	#if defined( AI_DEPTH_OFFSET_VIEW_DIR_WS )
	float3 finalWS = mul( UNITY_MATRIX_I_V, float4( viewPos, 1 ) ).xyz;
	return dot( finalWS - positionWS, -AI_DEPTH_OFFSET_VIEW_DIR_WS( positionWS ) );
	#else
	return 0;
	#endif
}

#endif
