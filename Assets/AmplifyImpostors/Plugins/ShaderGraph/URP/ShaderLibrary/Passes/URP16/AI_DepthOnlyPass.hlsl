#ifndef SG_DEPTH_ONLY_PASS_INCLUDED
#define SG_DEPTH_ONLY_PASS_INCLUDED

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
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);
    SurfaceDescription surfaceDescription = BuildSurfaceDescription(unpacked);

// AI: move the surface along the view ray and write its depth
#if defined(_AI_DEPTH_OFFSET)
    float3 aiPositionWS = AI_ApplyDepthOffset(unpacked.positionWS, surfaceDescription.AIDepthOffset);
    outDepth = AI_ClipPosToDepth(TransformWorldToHClip(aiPositionWS));
#endif

    #if defined(_ALPHATEST_ON)
        clip(surfaceDescription.Alpha - surfaceDescription.AlphaClipThreshold);
    #endif

    #if defined(LOD_FADE_CROSSFADE) && USE_UNITY_CROSSFADE
        LODFadeCrossFade(unpacked.positionCS);
    #endif

// AI: depth-as-color copies must match the written depth
#if defined(_AI_DEPTH_OFFSET)
    return outDepth;
#else
    return packedInput.positionCS.z;
#endif
}

#endif
