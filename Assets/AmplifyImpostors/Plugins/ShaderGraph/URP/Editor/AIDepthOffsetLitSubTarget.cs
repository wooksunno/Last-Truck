// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.Rendering.Universal.ShaderUtils;

namespace AmplifyImpostors.ShaderGraph
{
	sealed class AIDepthOffsetLitSubTarget : UniversalSubTarget
	{
		static readonly GUID kSourceCodeGuid = new GUID( "41814388130348799f5e4a358afa7bf3" ); // AIDepthOffsetLitSubTarget.cs

		const string kURPIncludes = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/";

		// our includes by asset guid (their .meta), resolved to the current path at setup so the folder can be moved or renamed
		const string kPregraphGuid = "a4cc8bf17fbb80e43a0842ce172b1b91"; // ShaderLibrary/AIDepthOffset.hlsl

		// urp pass include file name -> guid of our copy for the urp major version in use (ShaderLibrary/Passes/URP<major>/AI_<name>);
		// the urp major version is tied to the unity version
		static readonly Dictionary<string, string> kPassIncludeGuids = new Dictionary<string, string>()
		{
#if UNITY_6000_0_OR_NEWER
			// URP 17 (ShaderLibrary/Passes/URP17), verified on URP 17.5 only, URP 17.0 to 17.4 still need checking
			{ "PBRForwardPass.hlsl", "414e15df570b454bafee086356149404" },
			{ "PBRGBufferPass.hlsl", "60244c6cf0ca407ba417952ff795e68e" },
			{ "ShadowCasterPass.hlsl", "481c19d056d742a3a7e0b26b4cf945ba" },
			{ "DepthOnlyPass.hlsl", "73b13a4309cb4e319d972617e1d452d6" },
			{ "DepthNormalsOnlyPass.hlsl", "0b4a93ca39b14a10a41837d574c449f8" },
			{ "MotionVectorPass.hlsl", "5f0e09fead48410184e4475ee5962619" },
#elif UNITY_2023_2_OR_NEWER
			// URP 16 (ShaderLibrary/Passes/URP16)
			{ "PBRForwardPass.hlsl", "c89d6c395a254e19ac13f93a0aea70ef" },
			{ "PBRGBufferPass.hlsl", "6635afbe75e44b57bc0f41a9950e760e" },
			{ "ShadowCasterPass.hlsl", "a7aafc8b29d94175bf3b0f987c775cab" },
			{ "DepthOnlyPass.hlsl", "3b69933701284fabb6b39d9970b80e30" },
			{ "DepthNormalsOnlyPass.hlsl", "341ab2fa1b2e43bf9dfaa90fb09c20db" },
			{ "MotionVectorPass.hlsl", "55e2e2246fc14bbd80d12c83348cf8a2" },
#elif UNITY_2023_1_OR_NEWER
			// URP 15 (ShaderLibrary/Passes/URP15)
			{ "PBRForwardPass.hlsl", "e3cff1ca14c2490b940000df246d6ff9" },
			{ "PBRGBufferPass.hlsl", "c864efef86ba40688febb2d86e0653dc" },
			{ "ShadowCasterPass.hlsl", "ced0bdf971c9422ea82735dd1a23ec46" },
			{ "DepthOnlyPass.hlsl", "ee50735e79d3464fb1daf8c95d4c1b6d" },
			{ "DepthNormalsOnlyPass.hlsl", "74d2e30f49bf41cd9ecbaf8b4fb769c3" },
#else
			// URP 14 (ShaderLibrary/Passes/URP14)
			{ "PBRForwardPass.hlsl", "25098f4c2dd44d042ad9dec22de6adeb" },
			{ "PBRGBufferPass.hlsl", "d3d539d9294b97d48be002a8be3fa3cb" },
			{ "ShadowCasterPass.hlsl", "772e4cb5dd151bb46a6c46b64ec0b736" },
			{ "DepthOnlyPass.hlsl", "f922e93cef1ca4148a1325a657ddf12a" },
			{ "DepthNormalsOnlyPass.hlsl", "434dad39e7f657942897a613e3cb97a8" },
#endif
		};

		static readonly KeywordDescriptor kDepthOffsetDefine = new KeywordDescriptor()
		{
			displayName = "AI Depth Offset",
			referenceName = "_AI_DEPTH_OFFSET",
			type = KeywordType.Boolean,
			definition = KeywordDefinition.ShaderFeature,
			scope = KeywordScope.Local,
		};

		static readonly MethodInfo kLitComputeHash = typeof( UniversalLitSubTarget ).GetMethod( "ComputeMaterialNeedsUpdateHash", BindingFlags.Instance | BindingFlags.NonPublic );

		[SerializeField]
		WorkflowMode m_WorkflowMode = WorkflowMode.Metallic;

		[SerializeField]
		NormalDropOffSpace m_NormalDropOffSpace = NormalDropOffSpace.Tangent;

		[SerializeField]
		bool m_ClearCoat = false;

		[SerializeField]
		bool m_BlendModePreserveSpecular = true;

		[SerializeField]
		bool m_DepthOffset = true;

		[NonSerialized]
		readonly UniversalLitSubTarget m_Lit = new UniversalLitSubTarget();

		public AIDepthOffsetLitSubTarget()
		{
			displayName = "Lit (Depth Offset)";
		}

		protected override ShaderID shaderID => ShaderID.SG_Lit;

		public WorkflowMode workflowMode
		{
			get => m_WorkflowMode;
			set => m_WorkflowMode = value;
		}

		public NormalDropOffSpace normalDropOffSpace
		{
			get => m_NormalDropOffSpace;
			set => m_NormalDropOffSpace = value;
		}

		public bool depthOffset
		{
			get => m_DepthOffset;
			set => m_DepthOffset = value;
		}

		UniversalLitSubTarget SyncLit()
		{
			m_Lit.target = target;
			m_Lit.workflowMode = m_WorkflowMode;
			m_Lit.normalDropOffSpace = m_NormalDropOffSpace;
			m_Lit.clearCoat = m_ClearCoat;
			m_Lit.blendModePreserveSpecular = m_BlendModePreserveSpecular;
			return m_Lit;
		}

		void SyncFromLit()
		{
			m_WorkflowMode = m_Lit.workflowMode;
			m_NormalDropOffSpace = m_Lit.normalDropOffSpace;
			m_ClearCoat = m_Lit.clearCoat;
			m_BlendModePreserveSpecular = m_Lit.blendModePreserveSpecular;
		}

		public override bool IsActive() => true;

		public override void Setup( ref TargetSetupContext context )
		{
			// registered before lit, which then skips its own; a custom gui set on the target still wins
			var universalRPType = typeof( UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset );
			if ( !context.HasCustomEditorForRenderPipeline( universalRPType ) )
				context.AddCustomEditorForRenderPipeline( typeof( AIDepthOffsetLitGUI ).FullName, universalRPType );
			SyncLit().Setup( ref context );
			context.AddAssetDependency( kSourceCodeGuid, AssetCollection.Flags.SourceDependency );

			if ( !m_DepthOffset )
				return;

			// every include must resolve, else the graph is generated as plain lit; tracked by guid so edits reimport the graph
			var missing = new List<string>();
			foreach ( string guid in kPassIncludeGuids.Values.Append( kPregraphGuid ) )
			{
				if ( string.IsNullOrEmpty( AssetDatabase.GUIDToAssetPath( guid ) ) )
					missing.Add( guid );
				else
					context.AddAssetDependency( new GUID( guid ), AssetCollection.Flags.SourceDependency );
			}
			if ( missing.Count > 0 )
			{
				Debug.LogError( "Amplify Impostors: the Lit (Depth Offset) include files with guids " + string.Join( ", ", missing ) +
					" (ShaderLibrary/AIDepthOffset.hlsl, ShaderLibrary/Passes/URP<major>/AI_*.hlsl) are missing, the shader is generated as Lit without depth offset; reimport them" );
				return;
			}

			for ( int i = 0; i < context.subShaders.Count; i++ )
			{
				var subShader = context.subShaders[ i ];
				var passes = new PassCollection();
				foreach ( PassCollection.Item item in subShader.passes )
					passes.Add( PatchPass( item.descriptor ), item.fieldConditions );
				subShader.passes = passes;
				context.subShaders[ i ] = subShader;
			}
		}

		PassDescriptor PatchPass( PassDescriptor pass )
		{
			string passGuid = null;
			foreach ( IncludeDescriptor include in pass.includes )
			{
				if ( include.path.StartsWith( kURPIncludes ) && kPassIncludeGuids.TryGetValue( Path.GetFileName( include.path ), out passGuid ) )
					break;
			}

			if ( passGuid == null )
				return pass;

			var validPixelBlocks = new BlockFieldDescriptor[ pass.validPixelBlocks.Length + 1 ];
			pass.validPixelBlocks.CopyTo( validPixelBlocks, 0 );
			validPixelBlocks[ pass.validPixelBlocks.Length ] = AIBlockFields.SurfaceDescription.AIDepthOffset;
			pass.validPixelBlocks = validPixelBlocks;

			var requiredFields = new FieldCollection();
			if ( pass.requiredFields != null )
				requiredFields.Add( pass.requiredFields );
			requiredFields.Add( StructFields.Varyings.positionWS );
			pass.requiredFields = requiredFields;

			var includes = new IncludeCollection();
			foreach ( IncludeDescriptor include in pass.includes )
			{
				// the generator writes the include from the guid's current path (IncludeDescriptor.value)
				if ( include.path.StartsWith( kURPIncludes ) && kPassIncludeGuids.ContainsKey( Path.GetFileName( include.path ) ) )
					includes.AddInternal( passGuid, AssetDatabase.GUIDToAssetPath( passGuid ), include.location, include.fieldConditions, include.shouldIncludeWithPragmas );
				else
					includes.AddInternal( include.guid, include.path, include.location, include.fieldConditions, include.shouldIncludeWithPragmas );
			}
			includes.AddInternal( kPregraphGuid, AssetDatabase.GUIDToAssetPath( kPregraphGuid ), IncludeLocation.Pregraph );
			pass.includes = includes;

			var defines = new DefineCollection( pass.defines );
			defines.Add( kDepthOffsetDefine, 1 );
			pass.defines = defines;

			return pass;
		}

		public override void ProcessPreviewMaterial( Material material )
		{
			SyncLit().ProcessPreviewMaterial( material );
		}

		public override void GetFields( ref TargetFieldContext context )
		{
			SyncLit().GetFields( ref context );
		}

		public override void GetActiveBlocks( ref TargetActiveBlockContext context )
		{
			SyncLit().GetActiveBlocks( ref context );
			context.AddBlock( AIBlockFields.SurfaceDescription.AIDepthOffset, m_DepthOffset );
		}

		public override void CollectShaderProperties( PropertyCollector collector, GenerationMode generationMode )
		{
			SyncLit().CollectShaderProperties( collector, generationMode );
		}

		public override void GetPropertiesGUI( ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo )
		{
			SyncLit().GetPropertiesGUI( ref context, () => { SyncFromLit(); onChange(); }, registerUndo );

			context.AddProperty( "Depth Offset", new Toggle() { value = m_DepthOffset }, ( evt ) =>
			{
				if ( Equals( m_DepthOffset, evt.newValue ) )
					return;

				registerUndo( "Change Depth Offset" );
				m_DepthOffset = evt.newValue;
				onChange();
			} );
		}

		// protected on UniversalLitSubTarget, so it can't be called directly on the inner instance
		protected override int ComputeMaterialNeedsUpdateHash()
		{
			return ( int )kLitComputeHash.Invoke( SyncLit(), null );
		}

		internal override void OnAfterParentTargetDeserialized()
		{
			SyncLit().OnAfterParentTargetDeserialized();
			SyncFromLit();
		}
	}
}
