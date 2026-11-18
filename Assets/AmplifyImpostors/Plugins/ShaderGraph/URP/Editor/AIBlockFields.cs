// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using UnityEditor.ShaderGraph;

namespace AmplifyImpostors.ShaderGraph
{
	static class AIBlockFields
	{
		[GenerateBlocks( "Amplify Impostors" )]
		public struct SurfaceDescription
		{
			// not "DepthOffset": hdrp registers SurfaceDescription.DepthOffset and blocks are matched by name
			public static BlockFieldDescriptor AIDepthOffset = new BlockFieldDescriptor( BlockFields.SurfaceDescription.name, "AIDepthOffset", "Depth Offset", "SURFACEDESCRIPTION_AIDEPTHOFFSET",
				new FloatControl( 0.0f ), ShaderStage.Fragment );
		}
	}
}
