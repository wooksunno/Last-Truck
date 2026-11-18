#ifndef SG_SHADOW_PASS_INCLUDED
#define SG_SHADOW_PASS_INCLUDED

PackedVaryings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input);
    PackedVaryings packedOutput = (PackedVaryings)0;
    packedOutput = PackVaryings(output);
    return packedOutput;
}

half4 frag(PackedVaryings packedInput
// AI: per-pixel depth output
#if defined(_AI_DEPTH_OFFSET)
    , out float outDepth : SV_Depth
#endif
    ) : SV_TARGET
{
    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    SurfaceDescription surfaceDescription = BuildSurfaceDescription(unpacked);

// AI: move the surface along the light ray, then bias and clamp it like BuildVaryings does per vertex
#if defined(_AI_DEPTH_OFFSET)
    float3 aiPositionWS = AI_ApplyDepthOffset(unpacked.positionWS, surfaceDescription.AIDepthOffset);
    float3 aiLightDirectionWS = AI_GetViewOriginDirWS(aiPositionWS);
    float4 aiPositionCS = TransformWorldToHClip(ApplyShadowBias(aiPositionWS, float3(0, 0, 0), aiLightDirectionWS));
    // AI: slope bias in place of the hardware and normal bias (punctual lights)
    aiPositionCS = AI_ApplyShadowSlopeBias(aiPositionCS);
    #if UNITY_REVERSED_Z
        aiPositionCS.z = min(aiPositionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        aiPositionCS.z = max(aiPositionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    outDepth = AI_ClipPosToDepth(aiPositionCS);
#endif

    #if defined(_ALPHATEST_ON)
        clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
    #endif

    #if defined(LOD_FADE_CROSSFADE) && USE_UNITY_CROSSFADE
        LODFadeCrossFade(unpacked.positionCS);
    #endif

    return 0;
}

#endif
