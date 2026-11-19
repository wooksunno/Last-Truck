// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AI_DEPTH_OFFSET_INCLUDED
#define AI_DEPTH_OFFSET_INCLUDED

// depth offset contract (same as hdrp): world units along the view ray, positive = away from the viewpoint, negative offsets are valid

// normalized direction from the surface toward the viewpoint (the light, in the shadow caster pass)
float3 AI_GetViewOriginDirWS( float3 positionWS )
{
#if ( SHADERPASS == SHADERPASS_SHADOWCASTER )
	// urp renders each shadow slice with the light's view and projection matrices (ShadowUtils.RenderShadowSlice), while
	// _WorldSpaceCameraPos and unity_OrthoParams stay the main camera's; _LightDirection / _LightPosition are declared after
	// the graph code (Varyings.hlsl), so the light is recovered from the view matrix
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )
	{
		// orthographic (directional light): view +z, toward the light, same as _LightDirection
		return UNITY_MATRIX_V[ 2 ].xyz;
	}
	else
	{
		// perspective (spot light, point light face): the eye position, same as _LightPosition
		float3 eye = -mul( transpose( ( float3x3 )UNITY_MATRIX_V ), UNITY_MATRIX_V._m03_m13_m23 );
		return normalize( eye - positionWS );
	}
#else
	// GetWorldSpaceNormalizeViewDir without the half precision return
	if ( IsPerspectiveProjection() )
		return normalize( GetCurrentViewPosition() - positionWS );
	else
		return -GetViewForwardDir();
#endif
}

float3 AI_ApplyDepthOffset( float3 positionWS, float depthOffset )
{
	return positionWS - AI_GetViewOriginDirWS( positionWS ) * depthOffset;
}

// receiver side normal offset, the shadow caster of a per pixel depth surface cannot apply urp's caster side normal bias
#ifndef AI_RECEIVER_SHADOW_NORMAL_BIAS
	// in shadow texels, 1.0 is urp's light default
	#define AI_RECEIVER_SHADOW_NORMAL_BIAS 1.0
#endif

#if defined( MAIN_LIGHT_CALCULATE_SHADOWS )
// TransformWorldToShadowCoord with the position pushed along the normal, mirrors ShadowUtils.GetShadowBias and ApplyShadowBias
float4 AI_TransformWorldToShadowCoordNormalOffset( float3 positionWS, float3 normalWS )
{
#if defined( _MAIN_LIGHT_SHADOWS_SCREEN ) && !defined( _SURFACE_TYPE_TRANSPARENT )
	// screen space shadows were resolved from the depth buffer, no receiver offset possible
	return TransformWorldToShadowCoord( positionWS );
#else
	#if defined( _MAIN_LIGHT_SHADOWS_CASCADE )
		half cascadeIndex = ComputeCascadeIndex( positionWS );
	#else
		half cascadeIndex = half( 0.0 );
	#endif

	// row 0 maps world units to atlas u, the no-op matrix past the last cascade has a zero row
	float texelWorldSize = _MainLightShadowmapSize.x / max( length( _MainLightWorldToShadow[ cascadeIndex ][ 0 ].xyz ), 1e-6 );
	// _MainLightShadowParams.y is 0 for hard shadows, else the resolved soft quality (1 low, 2 medium, 3 high)
	float kernelScale = _MainLightShadowParams.y >= 1.0 ? 0.5 + _MainLightShadowParams.y : 1.0;
	float scale = 1.0 - saturate( dot( _MainLightPosition.xyz, normalWS ) );
	return TransformWorldToShadowCoord( positionWS + normalWS * ( AI_RECEIVER_SHADOW_NORMAL_BIAS * texelWorldSize * kernelScale * scale ) );
#endif
}
#endif

// caster side slope bias for punctual light shadows: a per pixel depth caster gets neither the hardware slope bias urp renders
// shadow maps with (ShadowUtils.RenderShadowSlice SetGlobalDepthBias( 1, 2.5 ), from the card triangle and not applied to
// SV_Depth) nor urp's normal bias (ApplyShadowBias moves vertices along the mesh normal); both are rebuilt from the screen space
// derivatives of the caster's own light depth. directional lights keep the receiver side normal offset above instead
#ifndef AI_SHADOW_SLOPE_BIAS
	// same slope factor as the hardware bias
	#define AI_SHADOW_SLOPE_BIAS 2.5
#endif
#ifndef AI_SHADOW_SLOPE_BIAS_MAX
	// in shadow texels, bounds the bias across depth discontinuities (silhouettes, alpha clip edges)
	#define AI_SHADOW_SLOPE_BIAS_MAX 16.0
#endif

#if ( SHADERPASS == SHADERPASS_SHADOWCASTER )
// positionCS: clip position of a point on the pixel's light ray
float4 AI_ApplyShadowSlopeBias( float4 positionCS )
{
	// punctual lights only (perspective light projection); UNITY_MATRIX_P is uniform, so the branch is uniform and the derivatives inside
	// it are well defined, directional shadow passes skip the whole computation
	[branch]
	if ( UNITY_MATRIX_P[ 3 ][ 3 ] != 1.0 )
	{
		// clip w is the linear depth along the light's view axis, the ndc x of the pixel's ray steps 2 / resolution per pixel
		float slope = max( abs( ddx( positionCS.w ) ), abs( ddy( positionCS.w ) ) );
		float texelSize = abs( ddx( positionCS.x / positionCS.w ) ) * positionCS.w / abs( UNITY_MATRIX_P[ 0 ][ 0 ] );
		float tanTheta = slope / max( texelSize, 1e-7 );

		// urp's normal bias (_ShadowBias.y: negative world units, 0 for point lights) seen along the light ray: normalBias * ( 1 / cos - 1 )
		float bias = AI_SHADOW_SLOPE_BIAS * slope + max( -_ShadowBias.y, 0.0 ) * ( sqrt( 1.0 + tanTheta * tanTheta ) - 1.0 );
		bias = min( bias, AI_SHADOW_SLOPE_BIAS_MAX * texelSize );

		// away from the light along its view axis, clip += P * ( 0, 0, -bias, 0 ), reversed z or not
		positionCS.zw -= bias * float2( UNITY_MATRIX_P[ 2 ][ 2 ], UNITY_MATRIX_P[ 3 ][ 2 ] );
	}
	return positionCS;
}
#endif

// clip space position to the value expected by SV_Depth
float AI_ClipPosToDepth( float4 positionCS )
{
	float depth = positionCS.z / positionCS.w;
#if !UNITY_REVERSED_Z
	// gl: clip z/w is [-1,1], depth output is [0,1]
	depth = depth * 0.5 + 0.5;
#endif
	return depth;
}

#endif
