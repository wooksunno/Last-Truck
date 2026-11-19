// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if AMPLIFY_SHADER_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AmplifyShaderEditor
{
	public static class ASEHelper
	{
		public static class RangedFloatNode
		{
			public static void SetMinMax( AmplifyShaderEditor.PropertyNode prop, float min, float max )
			{
				prop.GetType().GetField( "m_min", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, min );
				prop.GetType().GetField( "m_max", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, max );
			}
		}

		public static class PropertyNode
		{
			public static void SetInspectorName( AmplifyShaderEditor.PropertyNode prop, string name )
			{
				prop.GetType().GetField( "m_propertyInspectorName", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, name );
			}

			public static void AddAttribute( AmplifyShaderEditor.PropertyNode prop, int attr )
			{
				( prop.GetType().GetField( "m_selectedAttribs", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue( prop ) as List<int> ).Add( attr );
			}
		}
	}

	[Serializable]
	[NodeAttributes( "Amplify Impostor", "Amplify Impostor Runtime", "Amplify Impostor node for runtime shaders.", CustomCategoryColor = "#7d5299", NodeAvailabilityFlags = ( int )NodeAvailability.TemplateShader )]
	public sealed class AmplifyImpostorNode : ParentNode
	{
		private static readonly string[] ImpostorOutputStructDeclaration = new string[] {
			"struct ImpostorOutput",
			"{",
			"	half3 Albedo;",
			"	half3 Specular;",
			"	half Metallic;",
			"	half3 WorldNormal;",
			"	half Smoothness;",
			"	half Occlusion;",
			"	half3 Emission;",
			"	half Alpha;",
			"};" };

		private string m_functionHeaderSphereFrag = "SphereImpostorFragment( io, {0}, {1}, {2}, {3} )";
		private string m_functionHeaderSphere = "SphereImpostorVertex( {0}, {1}, {2}, {3}, {4} )";
		private string m_functionHeaderFrag = "OctaImpostorFragment( io, {0}, {1}, {2}, {3}, {4}, {5}, {6} )";
		private string m_functionHeader = "OctaImpostorVertex( {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7} )";
		private string m_functionBody = string.Empty;

		private enum CustomImpostorType
		{
			Spherical = 0,
			Octahedron = 1,
			HemiOctahedron = 2,
			CombinedOctahedron = 3
		}

		private enum KeywordMode
		{
			Off = 0,
			On = 1,
			ShaderFeature = 2,
			MultiCompile = 3
		}

		private static readonly string[] KeywordModeNames = { "Off", "On", "Shader Feature", "Multi Compile" };

		private const string WorkflowStr = "Workflow";
		private string[] DielecticSRPFix =
		{
			"#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP",
			"#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP",
			"#else//AI_SRP",
			"#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP",
			"#endif//AI_SRP"
		};

		struct PropertyNames
		{
			public string unique;
			public string legacy;
			public string inspector;
			public PropertyNames( string unique, string legacy, string inspector )
			{
				this.unique = unique;
				this.legacy = legacy;
				this.inspector = inspector;
			}
		}

		struct PropertyEntry
		{
			public PropertyNode prop;
			public PropertyNames names;
			public PropertyEntry( PropertyNode prop, PropertyNames names )
			{
				this.prop = prop;
				this.names = names;
			}
		}

		Dictionary<string, PropertyEntry> m_propertyNodes = new Dictionary<string, PropertyEntry>();

		[SerializeField] private ASEStandardSurfaceWorkflow m_workflow = ASEStandardSurfaceWorkflow.Specular;
		[SerializeField] private CustomImpostorType m_customImpostorType = CustomImpostorType.Octahedron;

		private bool IsSpherical => m_customImpostorType == CustomImpostorType.Spherical;
		private bool IsOctahedron => m_customImpostorType == CustomImpostorType.Octahedron;
		private bool IsHemiOctahedron => m_customImpostorType == CustomImpostorType.HemiOctahedron;
		private bool IsCombinedOctahedron => m_customImpostorType == CustomImpostorType.CombinedOctahedron;
		private bool IsAnyOctahedron => IsOctahedron || IsHemiOctahedron || IsCombinedOctahedron;

		// the combined octahedron serves both bake types through the hemi switch, so it is always keyworded there
		private KeywordMode HemiMode => ( IsCombinedOctahedron && !IsKeyword( m_hemiMode ) ) ? KeywordMode.ShaderFeature : m_hemiMode;

		[SerializeField] private KeywordMode m_hemiMode = KeywordMode.Off;
		[SerializeField] private KeywordMode m_parallaxMode = KeywordMode.Off;
		[SerializeField] private KeywordMode m_hueMode = KeywordMode.Off;
		[SerializeField] private KeywordMode m_textureKeywordMode = KeywordMode.ShaderFeature;
		[SerializeField] private bool m_extraSamplerKeywords = false;
		[SerializeField] private bool m_alphaClip = true;
		[SerializeField] private bool m_frameClampSupport; // unused: frame clipping is always on; kept for serialization compatibility
		[SerializeField] private bool m_positionTextureSupport;
		[SerializeField] private bool m_showExtraData;
		[SerializeField] private int m_extraSamplers = 0;

		private const int MaxExtraSamplers = 8;
		private string[] m_extraPropertyNames;
		private InputPort m_samplerStatePort;
		[SerializeField] private bool m_matchPropertyNames = false;

		[SerializeField] private TexturePropertyNode m_albedoTexture;
		[SerializeField] private TexturePropertyNode m_normalTexture;
		[SerializeField] private TexturePropertyNode m_specularTexture;
		[SerializeField] private TexturePropertyNode m_occlusionTexture;
		[SerializeField] private TexturePropertyNode m_emissionTexture;
		[SerializeField] private TexturePropertyNode m_positionTexture;
		[SerializeField] private RangedFloatNode m_clipProp;
		[SerializeField] private RangedFloatNode m_textureBiasProp;
		[SerializeField] private RangedFloatNode m_useParallaxProp;
		[SerializeField] private RangedFloatNode m_parallaxProp;
		[SerializeField] private RangedFloatNode m_shadowBiasProp;
		[SerializeField] private RangedFloatNode m_shadowViewProp;
		[SerializeField] private RangedFloatNode m_forwardBiasProp;
		[SerializeField] private RangedFloatNode m_hemiProp;
		[SerializeField] private RangedFloatNode m_useHueProp;
		[SerializeField] private ColorNode m_hueProp;
		[SerializeField] private RangedFloatNode m_clipNeighborsProp;
		[SerializeField] private RangedFloatNode m_framesProp;
		[SerializeField] private RangedFloatNode m_framesXProp;
		[SerializeField] private RangedFloatNode m_framesYProp;
		[SerializeField] private RangedFloatNode m_depthProp;
		[SerializeField] private RangedFloatNode m_impostorSizeProp;
		[SerializeField] private Vector3Node m_offsetProp;
		[SerializeField] private Vector4Node m_sizeOffsetProp;
		[SerializeField] private Vector4Node m_angleRangeProp;
		[SerializeField] private Vector3Node m_boundsMinProp;
		[SerializeField] private Vector3Node m_boundsSizeProp;

		private int m_orderAlbedoTexture = 0;
		private int m_orderNormalTexture = 1;
		private int m_orderSpecularTexture = 2;
		private int m_orderOcclusionTexture = 3;
		private int m_orderEmissionTexture = 4;
		private int m_orderPositionTexture = 5;
		private int m_orderClipProp = 6;
		private int m_orderTextureBiasProp = 7;
		private int m_orderUseParallaxProp = 8;
		private int m_orderParallaxProp = 9;
		private int m_orderShadowBiasProp = 10;
		private int m_orderShadowViewProp = 11;
		private int m_orderForwardBiasProp = 12;
		private int m_orderHemiProp = 13;
		private int m_orderUseHueProp = 14;
		private int m_orderHueProp = 15;
		private int m_orderClipNeighbors = 16;
		private int m_orderFramesProp = 17;
		private int m_orderFramesXProp = 18;
		private int m_orderFramesYProp = 19;
		private int m_orderDepthProp = 20;
		private int m_orderImpostorSizeProp = 21;
		private int m_orderOffsetProp = 22;
		private int m_orderSizeOffsetProp = 23;
		private int m_orderBoundsMinProp = 24;
		private int m_orderBoundsSizeProp = 25;
		private int m_orderAngleRangeProp = 26;

		private static readonly PropertyNames m_albedoTextureNames = new PropertyNames( "_Albedo", null, "Albedo (RGB) Alpha (A)" );
		private static readonly PropertyNames m_normalTextureNames = new PropertyNames( "_Normals", null, "Normals (RGB) Depth (A)" );
		private static readonly PropertyNames m_specularTextureNames = new PropertyNames( "_Specular", null, "Specular (RGB) Smoothness (A)" );
		private static readonly PropertyNames m_occlusionTextureNames = new PropertyNames( "_Occlusion", null, "Occlusion (RGB)" );
		private static readonly PropertyNames m_emissionTextureNames = new PropertyNames( "_Emission", null, "Emission (RGB)" );
		private static readonly PropertyNames m_positionTextureNames = new PropertyNames( "_Position", null, "Position (RGB)" );
		private static readonly PropertyNames m_clipNames = new PropertyNames( "_AI_Clip", "_ClipMask", "Clip" );
		private static readonly PropertyNames m_textureBiasNames = new PropertyNames( "_AI_TextureBias", "_TextureBias", "Texture Bias" );
		private static readonly PropertyNames m_useParallaxNames = new PropertyNames( "_AI_Use_Parallax", "_Use_Parallax", "Use Parallax" );
		private static readonly PropertyNames m_parallaxNames = new PropertyNames( "_AI_Parallax", "_Parallax", "Parallax" );
		private static readonly PropertyNames m_shadowBiasNames = new PropertyNames( "_AI_ShadowBias", null, "Shadow Bias" );
		private static readonly PropertyNames m_shadowViewNames = new PropertyNames( "_AI_ShadowView", null, "Shadow View" );
		private static readonly PropertyNames m_forwardBiasNames = new PropertyNames( "_AI_ForwardBias", null, "Forward Bias" );
		private static readonly PropertyNames m_hemiNames = new PropertyNames( "_AI_Hemi", "_Hemi", "Hemi" );
		private static readonly PropertyNames m_useHueNames = new PropertyNames( "_AI_Hue", "_Hue", "Use SpeedTree Hue" );
		private static readonly PropertyNames m_hueNames = new PropertyNames( "_AI_HueVariation", "_HueVariation", "Hue Variation" );
		private static readonly PropertyNames m_clipNeighborsNames = new PropertyNames( "_AI_ClipNeighborsFrames", "AI_CLIP_NEIGHBOURS_FRAMES", "Clip Neighbours Frames" );
		private static readonly PropertyNames m_framesNames = new PropertyNames( "_AI_Frames", "_Frames", "Frames" );
		private static readonly PropertyNames m_framesXNames = new PropertyNames( "_AI_FramesX", "_FramesX", "Frames X" );
		private static readonly PropertyNames m_framesYNames = new PropertyNames( "_AI_FramesY", "_FramesY", "Frames Y" );
		private static readonly PropertyNames m_depthNames = new PropertyNames( "_AI_DepthSize", "_DepthSize", "DepthSize" );
		private static readonly PropertyNames m_impostorSizeNames = new PropertyNames( "_AI_ImpostorSize", "_ImpostorSize", "Impostor Size" );
		private static readonly PropertyNames m_offsetNames = new PropertyNames( "_AI_Offset", "_Offset", "Offset" );
		private static readonly PropertyNames m_sizeOffsetNames = new PropertyNames( "_AI_SizeOffset", null, "Size & Offset" );
		private static readonly PropertyNames m_boundsMinNames = new PropertyNames( "_AI_BoundsMin", null, "Bounds Min" );
		private static readonly PropertyNames m_boundsSizeNames = new PropertyNames( "_AI_BoundsSize", null, "Bounds Size" );
		private static readonly PropertyNames m_angleRangeNames = new PropertyNames( "_AI_AngleRange", null, "Angle Range" );

		private bool m_propertiesInitialized = false;

		//private static readonly string ASEUpgradeMessage1 = "To work correctly, this version of Amplify Impostor Node requires Amplify Shader Editor v1.9.8.2 or higher. Please update ASE to latest.";
		private static readonly string NodeErrorMsg = "Amplify Impostor node does not support Surface shaders. To create Amplify Impostor runtime shaders for Built-in RP, please use a template shader; e.g. Legacy/Lit.";
		private static readonly string ErrorOnCompilationMsg = "Attempting to use Amplify Impostor node on Surface shaders. Only template-based shaders are supported.";

		private static readonly GUIContent ImpostorTypeContent = new GUIContent( "Impostor Type", "Impostor layout the shader is generated for; must match the bake type of the impostor that uses it. Combined Octahedron serves both Octahedron and Hemi Octahedron bakes through the Hemi toggle." );
		private static readonly GUIContent WorkflowContent = new GUIContent( WorkflowStr, "Specular outputs the baked specular color; Metallic derives a metallic value from the baked specular and albedo maps." );
		private static readonly GUIContent ParallaxSupportContent = new GUIContent( "Parallax Support", "Offsets the frame UVs by the baked depth so spherical impostors look less flat.\nOn: always applied.\nShader Feature / Multi Compile: controlled by the material's Use Parallax toggle ( _USE_PARALLAX_ON )." );
		private static readonly GUIContent HemiToggleSupportContent = new GUIContent( "Hemi Toggle Support", "Adds a material Hemi toggle ( _HEMI_ON ) so one shader renders both Octahedron and Hemi Octahedron bakes; the baker sets it to match the bake. Always present on Combined Octahedron." );
		private static readonly GUIContent HueSupportContent = new GUIContent( "SpeedTree Hue Support", "Shifts the albedo towards the Hue Variation color per instance, like SpeedTree shaders.\nOn: always applied.\nShader Feature / Multi Compile: controlled by the material's Use SpeedTree Hue toggle ( EFFECT_HUE_VARIATION )." );
		private static readonly GUIContent PositionTextureSupportContent = new GUIContent( "Position Texture Support", "Samples the baked Position map to place each pixel at its baked position instead of the card depth. Needs a bake preset that outputs a Position texture." );
		private static readonly GUIContent AlphaClipContent = new GUIContent( "Alpha Clip", "Discards pixels whose baked alpha is below the material's Clip value. Disable to handle the Alpha output yourself." );
		private static readonly GUIContent TextureKeywordsContent = new GUIContent( "Texture Keywords", "How the optional Specular, Occlusion, Emission and Position maps are compiled.\nOn: always sampled.\nShader Feature / Multi Compile: each map is sampled behind its own keyword, set by the baker." );
		private static readonly GUIContent MatchPropertyNamesContent = new GUIContent( "Match Native Property Names", "Uses the property names of the built-in Amplify Impostors shaders ( e.g. _Frames, _ClipMask ) instead of the _AI_ prefixed ones, so materials keep their values when switching shaders." );
		private static readonly GUIContent OutputExtraDataContent = new GUIContent( "Output Extra Data", "Shows the World Position and View Position outputs." );
		private static readonly GUIContent ExtraSamplersContent = new GUIContent( "Extra Samplers", "Number of additional textures sampled with the impostor frame UVs, e.g. extra outputs of a custom bake preset. Adds a Tex input/output pair each, plus a shared SS sampler state input." );
		private static readonly GUIContent ExtraSamplerKeywordsContent = new GUIContent( "Extra Sampler Keywords", "Samples each extra texture behind a keyword named after its property, using the Texture Keywords mode." );

		private static readonly string TextureKeywordsHelp = "Texture Keywords: Specular, Occlusion, Emission and Position maps are each sampled behind a local fragment keyword: _SPECULARMAP, _OCCLUSIONMAP, _EMISSIONMAP and _POSITIONMAP. Keywords are only generated for maps whose outputs are used.\n\nWhen the impostor is baked, the material enables the keyword of every map the bake preset outputs and disables the rest, so maps that were not baked cost no texture fetch and fall back to no specular, full occlusion and no emission. Materials not set up by the baker need these keywords enabled by hand.\n\nShader Feature strips the variants no material uses from builds; use Multi Compile if the keywords are changed at runtime.";
		private static readonly string ExtraSamplerKeywordsHelp = "Extra Sampler Keywords: Each extra sampler connected to a texture property is sampled behind a local fragment keyword named after that property, upper-cased with MAP appended: _Mask becomes _MASKMAP.\n\nThe baker enables it only when the bake preset has an active output with the same name as the property, so name the property after its bake output; otherwise the keyword stays off and the sampler returns 0. Textures not coming from a texture property node are always sampled.\n\nKeywords follow the Texture Keywords mode.";

		protected override void CommonInit( int uniqueId )
		{
			base.CommonInit( uniqueId );

			//if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			//{
			//	Debug.LogWarning( "[AmplifyImpostors] " + ASEUpgradeMessage1 );
			//}

			for ( int i = 0; i < MaxExtraSamplers; i++ )
			{
				AddInputPort( WirePortDataType.SAMPLER2D, true, "Tex" + i );
				AddOutputPort( WirePortDataType.FLOAT4, "Tex" + i, i + 8 );
			}

			AddInputPort( WirePortDataType.SAMPLERSTATE, false, "SS" );
			m_samplerStatePort = m_inputPorts[ m_inputPorts.Count - 1 ];

			AddOutputPort( WirePortDataType.FLOAT3, "Albedo", 0 );
			AddOutputPort( WirePortDataType.FLOAT3, "World Normal", 1 );
			AddOutputPort( WirePortDataType.FLOAT3, "Specular", 3 );
			AddOutputPort( WirePortDataType.FLOAT, "Smoothness", 4 );
			AddOutputPort( WirePortDataType.FLOAT, "Occlusion", 5 );
			AddOutputPort( WirePortDataType.FLOAT3, "Emission", 2 );
			AddOutputPort( WirePortDataType.FLOAT, "Alpha", 6 );
			AddOutputPort( WirePortDataType.FLOAT, "Depth", 17 );
			AddOutputPort( WirePortDataType.FLOAT3, "World Position", 7 );
			AddOutputPort( WirePortDataType.FLOAT3, "View Position", 16 );

			m_textLabelWidth = 160;

			m_errorMessageTooltip = NodeErrorMsg;
			m_errorMessageTypeIsError = NodeMessageType.Error;

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		public override void AfterCommonInit()
		{
			base.AfterCommonInit();
			UpdateTag();
		}

		void UpdateTag()
		{
			if ( m_containerGraph.CurrentMasterNode != null && !m_showErrorMessage )
			{
				List<CustomTagData> allTags = null;
				if ( VersionInfo.FullNumber > 15500 )
				{
					allTags = ( ( TemplateMultiPassMasterNode )m_containerGraph.CurrentMasterNode ).SubShaderModule.TagsHelper.AvailableTags;
				}
				else
				{
					allTags = ( m_containerGraph.MultiPassMasterNodes.NodesList[ m_containerGraph.MultiPassMasterNodes.Count - 1 ] ).SubShaderModule.TagsHelper.AvailableTags;
				}

				CustomTagData importorTag = allTags.Find( x => x.TagName == "ImpostorType" );
				if ( importorTag != null )
					importorTag.TagValue = m_customImpostorType.ToString();
				else
					allTags.Add( new CustomTagData( "ImpostorType", m_customImpostorType.ToString(), 0 ) );
			}
		}

		public override void DrawProperties()
		{
			//if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			//{
			//	EditorGUILayout.HelpBox( ASEUpgradeMessage1, MessageType.Warning );
			//}

			base.DrawProperties();
			NodeUtils.DrawPropertyGroup( ref m_propertiesFoldout, Constants.ParameterLabelStr, DrawParameters );

			if ( IsKeyword( m_textureKeywordMode ) )
			{
				EditorGUILayout.HelpBox( TextureKeywordsHelp, MessageType.Info );
			}

			if ( ( m_extraSamplers > 0 ) && m_extraSamplerKeywords )
			{
				EditorGUILayout.HelpBox( ExtraSamplerKeywordsHelp, MessageType.Info );
			}

			if ( m_showErrorMessage )
			{
				EditorGUILayout.HelpBox( NodeErrorMsg, MessageType.Error );
			}
		}

		private void DrawParameters()
		{
			EditorGUI.BeginChangeCheck();
			m_customImpostorType = ( CustomImpostorType )EditorGUILayoutEnumPopup( ImpostorTypeContent, m_customImpostorType );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdateTitle();
				UpdateTag();
			}

			EditorGUI.BeginChangeCheck();
			m_workflow = ( ASEStandardSurfaceWorkflow )EditorGUILayoutEnumPopup( WorkflowContent, m_workflow );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePorts();
			}

			if ( IsSpherical )
			{
				m_parallaxMode = DrawKeywordMode( ParallaxSupportContent, m_parallaxMode, KeywordMode.Off, KeywordMode.On, KeywordMode.ShaderFeature, KeywordMode.MultiCompile );
			}
			else if ( IsCombinedOctahedron )
			{
				EditorGUI.BeginChangeCheck();
				KeywordMode hemiMode = DrawKeywordMode( HemiToggleSupportContent, HemiMode, KeywordMode.ShaderFeature, KeywordMode.MultiCompile );
				if ( EditorGUI.EndChangeCheck() )
				{
					m_hemiMode = hemiMode;
				}
			}
			else
			{
				// no "On": an always-on hemi switch is just the HemiOctahedron type
				m_hemiMode = DrawKeywordMode( HemiToggleSupportContent, m_hemiMode, KeywordMode.Off, KeywordMode.ShaderFeature, KeywordMode.MultiCompile );
			}

			m_hueMode = DrawKeywordMode( HueSupportContent, m_hueMode, KeywordMode.Off, KeywordMode.On, KeywordMode.ShaderFeature, KeywordMode.MultiCompile );

			m_positionTextureSupport =  EditorGUILayoutToggle( PositionTextureSupportContent, m_positionTextureSupport );

			m_alphaClip = EditorGUILayoutToggle( AlphaClipContent, m_alphaClip );

			m_textureKeywordMode = DrawKeywordMode( TextureKeywordsContent, m_textureKeywordMode, KeywordMode.On, KeywordMode.ShaderFeature, KeywordMode.MultiCompile );

			EditorGUI.BeginChangeCheck();
			m_matchPropertyNames = EditorGUILayoutToggle( MatchPropertyNamesContent, m_matchPropertyNames );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePropertyNames();
			}

			EditorGUI.BeginChangeCheck();
			m_showExtraData = EditorGUILayoutToggle( OutputExtraDataContent, m_showExtraData );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePorts();
			}

			EditorGUI.BeginChangeCheck();
			float cacha = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 120;
			m_extraSamplers = EditorGUILayoutIntSlider( ExtraSamplersContent, m_extraSamplers, 0, MaxExtraSamplers );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdateInputPorts();
			}
			EditorGUIUtility.labelWidth = cacha;

			if ( m_extraSamplers > 0 )
			{
				m_extraSamplerKeywords = EditorGUILayoutToggle( ExtraSamplerKeywordsContent, m_extraSamplerKeywords );
			}
		}

		private KeywordMode DrawKeywordMode( GUIContent label, KeywordMode mode, params KeywordMode[] options )
		{
			GUIContent[] names = new GUIContent[ options.Length ];
			int selected = -1;
			for ( int i = 0; i < options.Length; i++ )
			{
				names[ i ] = new GUIContent( KeywordModeNames[ ( int )options[ i ] ] );
				if ( options[ i ] == mode )
				{
					selected = i;
				}
			}
			// a mode outside the options draws blank and is kept, like the int popup did
			int index = EditorGUILayoutPopup( label, selected, names );
			return ( index >= 0 ) ? options[ index ] : mode;
		}

		public void UpdateInputPorts()
		{
			m_extraPropertyNames = new string[ m_extraSamplers ];
			for ( int i = 0; i < MaxExtraSamplers; i++ )
			{
				GetOutputPortByArrayId( i ).Visible = i < m_extraSamplers;
				GetInputPortByArrayId( i ).Visible = i < m_extraSamplers;
			}
			m_samplerStatePort.Visible = m_extraSamplers > 0;
			m_sizeIsDirty = true;
		}

		public void UpdatePorts()
		{
			if ( m_showExtraData )
			{
				GetOutputPortByUniqueId( 7 ).Visible = true;
				GetOutputPortByUniqueId( 16 ).Visible = true;
			}
			else
			{
				GetOutputPortByUniqueId( 7 ).Visible = false;
				GetOutputPortByUniqueId( 16 ).Visible = false;
			}

			if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
			{
				GetOutputPortByUniqueId( 3 ).ChangeProperties( "Specular", WirePortDataType.FLOAT3, false );
			}
			else
			{
				GetOutputPortByUniqueId( 3 ).ChangeProperties( "Metallic", WirePortDataType.FLOAT, false );
			}
			m_sizeIsDirty = true;
		}

		void UpdatePropertyNames()
		{
			foreach ( var pair in m_propertyNodes )
			{
				PropertyEntry entry = pair.Value;
				if ( !string.IsNullOrEmpty( entry.names.legacy ) )
				{
					entry.prop.RegisterPropertyName( true, m_matchPropertyNames ? entry.names.legacy : entry.names.unique );
				}
				ASEHelper.PropertyNode.SetInspectorName( entry.prop, entry.names.inspector );
			}
		}

		void InitProperty<T>( ref T prop, int order, PropertyNames names, bool hideInInspector = false,
			bool toggle = false ) where T : PropertyNode
		{
			prop = ( prop != null ) ? prop : ScriptableObject.CreateInstance<T>();
			prop.ContainerGraph = ContainerGraph;
			prop.OrderIndex = order;
			prop.UniqueId = ContainerGraph.GetValidId();
			prop.ChangeParameterType( PropertyType.Property );
			prop.RegisterPropertyName( true, names.unique );

			UIUtils.RegisterPropertyNode( prop );
			ASEHelper.PropertyNode.SetInspectorName( prop, names.inspector );

			if ( hideInInspector )
			{
				ASEHelper.PropertyNode.AddAttribute( prop, 0 );
			}

			if ( toggle )
			{
				ASEHelper.PropertyNode.AddAttribute( prop, 5 );
			}

			m_propertyNodes.TryAdd( names.unique, new PropertyEntry( prop, names ) );
		}

		void InitFloatProperty( ref RangedFloatNode prop, int order, PropertyNames names, bool hideInInspector = false, bool toggle = false,
			float value = 0, bool floatMode = true, float min = 0, float max = 0 )
		{
			InitProperty<RangedFloatNode>( ref prop, order, names, hideInInspector, toggle );

			UIUtils.RegisterFloatIntNode( prop );
			prop.Value = value;
			prop.SetFloatMode( floatMode );

			if ( !floatMode )
			{
				ASEHelper.RangedFloatNode.SetMinMax( prop, min, max );
			}
		}

		void InitTextureProperty( ref TexturePropertyNode prop, int order, PropertyNames names, TexturePropertyValues value )
		{
			InitProperty<TexturePropertyNode>( ref prop, order, names );

			UIUtils.RegisterTexturePropertyNode( prop );
			prop.DefaultTextureValue = value;
			prop.CustomPrefix = names.unique.Substring( 1 );
			prop.DrawAutocast = false;

			ASEHelper.PropertyNode.AddAttribute( prop, 5 );
		}

		public void Init()
		{
			if ( !m_propertiesInitialized )
			{
				InitTextureProperty( ref m_albedoTexture, m_orderAlbedoTexture, m_albedoTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_normalTexture, m_orderNormalTexture, m_normalTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_specularTexture, m_orderSpecularTexture, m_specularTextureNames, TexturePropertyValues.black );
				InitTextureProperty( ref m_occlusionTexture, m_orderOcclusionTexture, m_occlusionTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_emissionTexture, m_orderEmissionTexture, m_emissionTextureNames, TexturePropertyValues.black );
				InitTextureProperty( ref m_positionTexture, m_orderPositionTexture, m_positionTextureNames, TexturePropertyValues.black );
				InitFloatProperty( ref m_clipProp, m_orderClipProp, m_clipNames, value: 0.5f, floatMode: false, min: 0, max: 1 );
				InitFloatProperty( ref m_textureBiasProp, m_orderTextureBiasProp, m_textureBiasNames, value: -1 );
				InitFloatProperty( ref m_useParallaxProp, m_orderUseParallaxProp, m_useParallaxNames, value: 0, toggle: true );
				InitFloatProperty( ref m_parallaxProp, m_orderParallaxProp, m_parallaxNames, value: 1, floatMode: false, min: -1, max: 1 );
				InitFloatProperty( ref m_shadowBiasProp, m_orderShadowBiasProp, m_shadowBiasNames, value: 0.333f, floatMode: false, min: 0, max: 2 );
				InitFloatProperty( ref m_shadowViewProp, m_orderShadowViewProp, m_shadowViewNames, value: 1, floatMode: false, min: 0, max: 1 );
				InitFloatProperty( ref m_forwardBiasProp, m_orderForwardBiasProp, m_forwardBiasNames, value: 0.0f, floatMode: false, min: 0, max: 2 );
				InitFloatProperty( ref m_hemiProp, m_orderHemiProp, m_hemiNames, value: 0, toggle: true );
				InitFloatProperty( ref m_useHueProp, m_orderUseHueProp, m_useHueNames, value: 0, toggle: true );
				InitProperty<ColorNode>( ref m_hueProp, m_orderHueProp, m_hueNames );
				InitFloatProperty( ref m_clipNeighborsProp, m_orderClipNeighbors, m_clipNeighborsNames, value: 0, toggle: true );
				InitFloatProperty( ref m_framesProp, m_orderFramesProp, m_framesNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_framesXProp, m_orderFramesXProp, m_framesXNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_framesYProp, m_orderFramesYProp, m_framesYNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_depthProp, m_orderDepthProp, m_depthNames, hideInInspector: true, value: 1 );
				InitFloatProperty( ref m_impostorSizeProp, m_orderImpostorSizeProp, m_impostorSizeNames, hideInInspector: true, value: 1 );
				InitProperty<Vector3Node>( ref m_offsetProp, m_orderOffsetProp, m_offsetNames, hideInInspector: true );
				InitProperty<Vector4Node>( ref m_sizeOffsetProp, m_orderSizeOffsetProp, m_sizeOffsetNames, hideInInspector: true );
				InitProperty<Vector4Node>( ref m_angleRangeProp, m_orderAngleRangeProp, m_angleRangeNames, hideInInspector: true );
				m_angleRangeProp.Value = new Vector4( 360, 180, 0, 0 );
				InitProperty<Vector3Node>( ref m_boundsMinProp, m_orderBoundsMinProp, m_boundsMinNames, hideInInspector: true );
				InitProperty<Vector3Node>( ref m_boundsSizeProp, m_orderBoundsSizeProp, m_boundsSizeNames, hideInInspector: true );

				UpdatePropertyNames();

				m_propertiesInitialized = true;
			}
		}

		public override void SetMaterialMode( Material mat, bool fetchMaterialValues )
		{
			base.SetMaterialMode( mat, fetchMaterialValues );

			if ( !m_propertiesInitialized )
				return;

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.SetMaterialMode( mat, fetchMaterialValues );
			}
		}

		public override void RefreshExternalReferences()
		{
			base.RefreshExternalReferences();

			Init();

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		void UpdateTitle()
		{
			SetAdditonalTitleText( "Type( " + m_customImpostorType + " )" );

			//List<CustomTagData> allTags = null;
			//if( VersionInfo.FullNumber > 15500 )
			//{
			//	allTags = ( (TemplateMultiPassMasterNode)m_containerGraph.CurrentMasterNode ).SubShaderModule.TagsHelper.AvailableTags;
			//}
			//else
			//{
			//	allTags = ( m_containerGraph.MultiPassMasterNodes.NodesList[ m_containerGraph.MultiPassMasterNodes.Count - 1 ] ).SubShaderModule.TagsHelper.AvailableTags;
			//}

			//CustomTagData importorTag = allTags.Find( x => x.TagName == "ImpostorType" );
			//if( importorTag != null )
			//	importorTag.TagValue = m_customImpostorType.ToString();
			//else
			//	allTags.Add( new CustomTagData( "ImpostorType", m_customImpostorType.ToString(), 0 ) );
		}

		public override void OnNodeLogicUpdate( DrawInfo drawInfo )
		{
			base.OnNodeLogicUpdate( drawInfo );

			Init();

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.OnNodeLogicUpdate( drawInfo );
			}

			m_showErrorMessage = m_containerGraph != null && m_containerGraph.ParentWindow != null &&
				m_containerGraph.ParentWindow.MainGraphInstance.CurrentCanvasMode != NodeAvailability.TemplateShader &&
				m_containerGraph.ParentWindow.MainGraphInstance.CurrentCanvasMode != NodeAvailability.ShaderFunction;
		}

		public override void Destroy()
		{
			base.Destroy();

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.Destroy();
			}
		}

		public override string GenerateShaderForOutput( int outputId, ref MasterNodeDataCollector dataCollector, bool ignoreLocalvar )
		{
			if ( m_showErrorMessage )
			{
				UIUtils.ShowMessage( ErrorOnCompilationMsg, MessageSeverity.Error );
				return GenerateErrorValue();
			}

			UpdateInputPorts();

			bool hasSpec = true;
			if ( !GetOutputPortByUniqueId( 3 ).IsConnected && !GetOutputPortByUniqueId( 4 ).IsConnected )
				hasSpec = false;

			bool hasOcclusion = true;
			if ( !GetOutputPortByUniqueId( 5 ).IsConnected )
				hasOcclusion = false;

			bool hasEmission = true;
			if ( !GetOutputPortByUniqueId( 2 ).IsConnected )
				hasEmission = false;

			bool hasPosition = m_positionTextureSupport;
			bool isHDRP = ( dataCollector.IsSRP && dataCollector.CurrentSRPType == TemplateSRPType.HDRP );

			bool generateSamplingMacros = UIUtils.CurrentWindow.OutsideGraph.CurrentMasterNode.SamplingMacros;
			m_albedoTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_normalTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_specularTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_occlusionTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_emissionTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_positionTexture.ForceSamplingMacrosGen = generateSamplingMacros;

			m_albedoTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_normalTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			if ( hasSpec )
			{
				m_specularTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				AddKeywordPragma( ref dataCollector, m_textureKeywordMode, "_SPECULARMAP", fragmentOnly: true );
			}

			if ( hasOcclusion )
			{
				m_occlusionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				AddKeywordPragma( ref dataCollector, m_textureKeywordMode, "_OCCLUSIONMAP", fragmentOnly: true );
			}

			if ( hasEmission )
			{
				m_emissionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				AddKeywordPragma( ref dataCollector, m_textureKeywordMode, "_EMISSIONMAP", fragmentOnly: true );
			}

			if ( m_positionTextureSupport )
			{
				m_positionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				AddKeywordPragma( ref dataCollector, m_textureKeywordMode, "_POSITIONMAP", fragmentOnly: true );
			}

			m_clipProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_textureBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			if ( IsAnyOctahedron )
			{
				// octahedron parallax is always on; only the sphere exposes it through the parallax mode
				m_parallaxProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}
			else if ( m_parallaxMode != KeywordMode.Off )
			{
				if ( IsKeyword( m_parallaxMode ) )
				{
					dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( _USE_PARALLAX_ON )] {0}( \"{1}\", Float ) = {2}", m_useParallaxProp.PropertyName, m_useParallaxProp.PropertyInspectorName, m_useParallaxProp.Value ), m_useParallaxProp.OrderIndex );
					AddKeywordPragma( ref dataCollector, m_parallaxMode, "_USE_PARALLAX_ON" );
				}
				m_parallaxProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}

			m_shadowBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_shadowViewProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_forwardBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			if ( m_hueMode != KeywordMode.Off )
			{
				if ( IsKeyword( m_hueMode ) )
				{
					dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( EFFECT_HUE_VARIATION )] {0}( \"{1}\", Float ) = {2}", m_useHueProp.PropertyName, m_useHueProp.PropertyInspectorName, m_useHueProp.Value ), m_useHueProp.OrderIndex );
					AddKeywordPragma( ref dataCollector, m_hueMode, "EFFECT_HUE_VARIATION" );
				}
				m_hueProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}

			if ( IsAnyOctahedron && IsKeyword( HemiMode ) )
			{
				dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( _HEMI_ON )] {0}( \"{1}\", Float ) = {2}", m_hemiProp.PropertyName, m_hemiProp.PropertyInspectorName, m_hemiProp.Value ), m_hemiProp.OrderIndex );
				AddKeywordPragma( ref dataCollector, HemiMode, "_HEMI_ON" );
			}

			if ( IsAnyOctahedron )
			{
				m_framesProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}
			// octahedron: zero defaults mean "use Frames", for materials baked before decoupled octahedron frames
			m_framesXProp.Value = IsAnyOctahedron ? 0 : 16;
			m_framesYProp.Value = IsAnyOctahedron ? 0 : 16;
			m_framesXProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_framesYProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_angleRangeProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_depthProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_impostorSizeProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_offsetProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_sizeOffsetProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_boundsMinProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_boundsSizeProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			dataCollector.AddFunction( ImpostorOutputStructDeclaration[ 0 ], ImpostorOutputStructDeclaration, true );

			if ( dataCollector.IsSRP )
			{
				dataCollector.AddToDefines( UniqueId, "AI_ObjectToWorld GetObjectToWorldMatrix()" );
				dataCollector.AddToDefines( UniqueId, "AI_WorldToObject GetWorldToObjectMatrix()" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_TWO_PI  INV_TWO_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_PI          PI" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_PI      INV_PI" );
			}
			else
			{
				dataCollector.AddToDefines( UniqueId, "AI_ObjectToWorld unity_ObjectToWorld" );
				dataCollector.AddToDefines( UniqueId, "AI_WorldToObject unity_WorldToObject" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_TWO_PI  UNITY_INV_TWO_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_PI          UNITY_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_PI      UNITY_INV_PI" );
			}

			if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			{
				// @diogo: These are automatically added, when needed, from ASE 1.9.8.2 or higher
				dataCollector.AddToDefines( UniqueId, "ASE_NEEDS_TEXTURE_COORDINATES1" );
				dataCollector.AddToDefines( UniqueId, "ASE_NEEDS_TEXTURE_COORDINATES2" );
			}

			dataCollector.AddToDefines( UniqueId, "ASE_CHANGES_WORLD_POS" );

			// the octahedral map functions and the cap layout ( hemi-octahedral map ) are used by every impostor type
			GenerateVectorToOctahedron();
			dataCollector.AddFunctions( "VectorToOctahedron", m_functionBody );

			GenerateOctahedronToVector();
			dataCollector.AddFunctions( "OctahedronToVector", m_functionBody );

			GenerateVectorToHemiOctahedron();
			dataCollector.AddFunctions( "VectorToHemiOctahedron", m_functionBody );

			GenerateHemiOctahedronToVector();
			dataCollector.AddFunctions( "HemiOctahedronToVector", m_functionBody );

			GenerateCapLayout();
			dataCollector.AddFunctions( "CapLayout", m_functionBody );

			if ( IsAnyOctahedron )
			{
				GenerateOctahedronRayPlaneIntersectionUV();
				dataCollector.AddFunctions( "RayPlaneIntersectionUV", m_functionBody );

				GenerateOctaFrameToVector();
				dataCollector.AddFunctions( "OctaFrameToVector", m_functionBody );
			}

			GenerateImpostorClipPosDepthBias();
			dataCollector.AddFunctions( "ImpostorClipPosDepthBias", m_functionBody );

			if ( IsSpherical )
				GenerateSphereImpostorVertex( ref dataCollector );
			else
				GenerateImpostorVertex( ref dataCollector );

			string uvFrame1Name = "UVsFrame1" + OutputId;
			string uvFrame2Name = "UVsFrame2" + OutputId;
			string uvFrame3Name = "UVsFrame3" + OutputId;
			string octaFrameName = "octaframe" + OutputId;
			string sphereFrames = "frameUVs" + OutputId;
			string viewPosName = "viewPos" + OutputId;
			TemplateVertexData data = null;

			if ( IsSpherical )
			{
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, sphereFrames );
				if ( data != null )
					sphereFrames = data.VarName;
			}
			else
			{
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame1Name );
				if ( data != null )
					uvFrame1Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame2Name );
				if ( data != null )
					uvFrame2Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame3Name );
				if ( data != null )
					uvFrame3Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, octaFrameName );
				if ( data != null )
					octaFrameName = data.VarName;
			}
			data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, viewPosName );
			if ( data != null )
				viewPosName = data.VarName;

			MasterNodePortCategory portCategory = dataCollector.PortCategory;
			dataCollector.PortCategory = MasterNodePortCategory.Vertex;
			//Debug.Log( dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.FragmentFunctionData.InVarType );
			string vertOut = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.VertexFunctionData.OutVarName;
			string vertIN = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.VertexFunctionData.InVarName;

			dataCollector.AddToDirectives( "#undef ASE_ADJUST_CLIP_POSITION" );
			dataCollector.AddToDefines( UniqueId, string.Format( "ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, {0}, {1} )",
				vertOut + "." + viewPosName, m_depthProp.PropertyName ) );

			string positionOS = dataCollector.IsSRP ? ".positionOS.xyz" : ".vertex.xyz";
			string normalOS = dataCollector.IsSRP ? ".normalOS.xyz" : ".normal.xyz";
			string tangentOS = dataCollector.IsSRP ? ".tangentOS" : ".tangent";

			string functionResult;
			if ( IsSpherical )
			{
				functionResult = dataCollector.AddFunctions( m_functionHeaderSphere, m_functionBody, vertIN + positionOS, vertIN + normalOS, vertIN + tangentOS, vertOut + "." + sphereFrames, vertOut + "." + viewPosName );
			}
			else
			{
				functionResult = dataCollector.AddFunctions( m_functionHeader, m_functionBody, vertIN + positionOS, vertIN + normalOS, vertIN + tangentOS, vertOut + "." + uvFrame1Name, vertOut + "." + uvFrame2Name, vertOut + "." + uvFrame3Name, vertOut + "." + octaFrameName, vertOut + "." + viewPosName );
			}

			dataCollector.AddLocalVariable( UniqueId, functionResult + ";" );

			dataCollector.PortCategory = portCategory;

			if ( dataCollector.IsFragmentCategory )
			{
				string extraHeader = string.Empty;

				for ( int i = 0; i < m_extraSamplers; i++ )
				{
					if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					{
						m_extraPropertyNames[ i ] = GetInputPortByArrayId( i ).GeneratePortInstructions( ref dataCollector );
						dataCollector.AddLocalVariable( UniqueId, "float4 output" + i + " = 0;" );
						extraHeader += ", output" + i;
					}
				}

				string positionCS = dataCollector.TemplateDataCollectorInstance.GetClipPos();
				string positionWS = dataCollector.TemplateDataCollectorInstance.GetPosition( isHDRP ? PositionNode.Space.RelativeWorld : PositionNode.Space.World );
				dataCollector.AddLocalVariable( UniqueId, "ImpostorOutput io = ( ImpostorOutput )0;" );

				string finalHeader = m_functionHeaderFrag;
				if ( IsSpherical )
				{
					finalHeader = m_functionHeaderSphereFrag;
					finalHeader = finalHeader.Replace( "{3}", "{3}" + extraHeader );
				}
				else
				{
					finalHeader = finalHeader.Replace( "{6}", "{6}" + extraHeader );

				}

				string fragIN = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.FragmentFunctionData.InVarName;
				if ( IsSpherical )
				{
					GenerateSphereImpostorFragment( ref dataCollector, hasSpec, hasOcclusion, hasEmission, hasPosition );
					functionResult = dataCollector.AddFunctions( finalHeader, m_functionBody, positionCS, positionWS, fragIN + "." + sphereFrames, fragIN + "." + viewPosName );
				}
				else
				{
					GenerateImpostorFragment( ref dataCollector, hasSpec, hasOcclusion, hasEmission, hasPosition );
					functionResult = dataCollector.AddFunctions( finalHeader, m_functionBody, positionCS, positionWS, fragIN + "." + uvFrame1Name, fragIN + "." + uvFrame2Name, fragIN + "." + uvFrame3Name, fragIN + "." + octaFrameName, fragIN + "." + viewPosName );
				}
				dataCollector.AddLocalVariable( UniqueId, functionResult + ";" );

				switch ( outputId )
				{
					case 0:
						return "io.Albedo";
					case 1:
						return "io.WorldNormal";
					case 2:
						return "io.Emission";
					case 3:
						return m_workflow == ASEStandardSurfaceWorkflow.Specular ? "io.Specular" : "io.Metallic";
					case 4:
						return "io.Smoothness";
					case 5:
						return "io.Occlusion";
					case 6:
						return "io.Alpha";
					case 7:
						return isHDRP ? string.Format( "GetAbsolutePositionWS( {0} )", positionWS ): positionWS;
					case 17:
						return string.Format( "( {0}.z / {0}.w )", positionCS );
					case 16:
						dataCollector.AddLocalVariable( UniqueId, string.Format( "float3 viewPosOut{0} = mul( UNITY_MATRIX_V, float4( {1}.xyz, 1.0 ) ).xyz;", OutputId, positionWS ) );
						return "viewPosOut" + OutputId;
					default:
						return "output" + ( outputId - 8 );
				}
			}
			else
			{
				switch ( outputId )
				{
					case 7:
					{
						string positionWS;
						if ( !dataCollector.IsSRP )
						{
							positionWS = string.Format( "mul( unity_ObjectToWorld, float4( {0}{1}.xyz, 1.0 ) ).xyz", vertIN, positionOS );
						}
						else
						{
							positionWS = string.Format( "TransformObjectToWorld( {0}{1} )", vertIN, positionOS );
							positionWS = isHDRP ? "GetAbsolutePositionWS( " + positionWS + " )" : positionWS;
						}
						dataCollector.AddLocalVariable( UniqueId, "float3 worldPosOut" + OutputId + " = " + positionWS + ";" );
						return "worldPosOut" + OutputId;
					}
					case 16:
						return vertOut + "." + viewPosName + ".xyz";
					default:
						return "0";
				}
			}
		}

		public override void WriteToString( ref string nodeInfo, ref string connectionsInfo )
		{
			base.WriteToString( ref nodeInfo, ref connectionsInfo );

			IOUtils.AddFieldValueToString( ref nodeInfo, AmplifyImpostors.VersionInfo.FullNumber );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_customImpostorType );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hemiMode );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_parallaxMode );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hueMode );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_showExtraData );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_frameClampSupport );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesXProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesYProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_impostorSizeProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_parallaxProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_offsetProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_sizeOffsetProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_textureBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_albedoTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_normalTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_specularTexture.OrderIndex.ToString() );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_emissionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_depthProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_shadowBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_shadowViewProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_clipProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hueProp.OrderIndex.ToString() );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_occlusionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_positionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_useParallaxProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hemiProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_useHueProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_clipNeighborsProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_forwardBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_positionTextureSupport );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_extraSamplers );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_workflow );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_matchPropertyNames );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_angleRangeProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_textureKeywordMode );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_extraSamplerKeywords );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_alphaClip );
		}

		private static KeywordMode ReadKeywordMode( string param )
		{
			// graphs saved before keyword modes existed stored the feature toggles as booleans
			if ( bool.TryParse( param, out bool toggle ) )
			{
				return toggle ? KeywordMode.ShaderFeature : KeywordMode.Off;
			}
			return ( KeywordMode )Enum.Parse( typeof( KeywordMode ), param );
		}

		public override void ReadFromString( ref string[] nodeParams )
		{
			base.ReadFromString( ref nodeParams );

			int version = 0;
			if ( UIUtils.CurrentShaderVersion() > 15602 && AmplifyImpostors.VersionInfo.FullNumber >= 9202 )
				version = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );

			m_customImpostorType = ( CustomImpostorType )Enum.Parse( typeof( CustomImpostorType ), GetCurrentParam( ref nodeParams ) );
			if ( version >= 9906 )
			{
				m_hemiMode = ReadKeywordMode( GetCurrentParam( ref nodeParams ) );
				m_parallaxMode = ReadKeywordMode( GetCurrentParam( ref nodeParams ) );
			}
			// a boolean here means the graph predates keyword modes (early v1.1.0 included) and carries none of the
			// fields appended at the end; the port data follows right after, so the array length cannot tell
			string hueParam = GetCurrentParam( ref nodeParams );
			bool hasKeywordModes = !bool.TryParse( hueParam, out _ );
			m_hueMode = ReadKeywordMode( hueParam );
			m_showExtraData = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9202 )
			{
				m_frameClampSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}
			m_orderFramesProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderFramesXProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderFramesYProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderImpostorSizeProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderParallaxProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderOffsetProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9500 )
			{
				m_orderSizeOffsetProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			}
			m_orderTextureBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderAlbedoTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderNormalTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderSpecularTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderEmissionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderDepthProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderShadowBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9300 )
			{
				m_orderShadowViewProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			}
			m_orderClipProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderHueProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );

			if ( version >= 9906 )
			{
				m_orderOcclusionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderPositionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderUseParallaxProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderHemiProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderUseHueProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderClipNeighbors = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderForwardBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_positionTextureSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}

			if ( UIUtils.CurrentShaderVersion() > 15405 )
			{
				m_extraSamplers = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_workflow = ( ASEStandardSurfaceWorkflow )Enum.Parse( typeof( ASEStandardSurfaceWorkflow ), GetCurrentParam( ref nodeParams ) );
			}
			if ( version > 9707 )
			{
				m_matchPropertyNames = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}

			if ( version >= 10501 )
			{
				m_orderAngleRangeProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			}

			if ( hasKeywordModes )
			{
				m_textureKeywordMode = ReadKeywordMode( GetCurrentParam( ref nodeParams ) );
				m_extraSamplerKeywords = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );

				// graphs saved with keyword modes but before this field end here and run straight into the port
				// data, which ASE appends to this same array and opens with the input port count (an integer); the
				// version number is the same on both sides, so peek: only this field can be a boolean at this slot
				if ( ( m_currentReadParamIdx < nodeParams.Length ) && bool.TryParse( nodeParams[ m_currentReadParamIdx ], out bool alphaClip ) )
				{
					m_alphaClip = alphaClip;
					m_currentReadParamIdx++;
				}
			}

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		private void GenerateOctaFrameToVector()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float3 OctaFrameToVector( float2 frameIndex, int layout, float2 gridOrigin, float2 gridStep, float3x3 capBasis, float2 capScale, float2 fractionsPrevFrame )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( layout == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 yawPitch = radians( gridOrigin + frameIndex * gridStep + " + m_angleRangeProp.PropertyName + ".zw );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 sinYawPitch, cosYawPitch;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "sincos( yawPitch, sinYawPitch, cosYawPitch );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return float3( sinYawPitch.x * cosYawPitch.y, sinYawPitch.y, cosYawPitch.x * cosYawPitch.y );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 octa = frameIndex * fractionsPrevFrame * 2.0 - 1.0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( layout == 2 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return mul( HemiOctahedronToVector( clamp( octa * capScale, -1, 1 ) ), capBasis );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			if ( IsKeyword( HemiMode ) )
			{
				AddFeatureLines( HemiMode, "_HEMI_ON", new string[] { "return HemiOctahedronToVector( octa ).xzy;" }, new string[] { "return OctahedronToVector( octa ).xzy;" } );
			}
			else if ( IsHemiOctahedron )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "return HemiOctahedronToVector( octa ).xzy;" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "return OctahedronToVector( octa ).xzy;" );
			}
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateCapLayout()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "void CapLayout( float yawRange, float pitchRange, float2 center, out float3x3 basis, out float2 scale )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 sinCenter, cosCenter;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "sincos( radians( center ), sinCenter, cosCenter );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 forward = float3( sinCenter.x * cosCenter.y, sinCenter.y, cosCenter.x * cosCenter.y );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 right = float3( cosCenter.x, 0, -sinCenter.x );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 up = cross( forward, right );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "basis = float3x3( ( right + up ) * 0.70710678, ( right - up ) * 0.70710678, forward );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 sinHalf, cosHalf;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "sincos( radians( float2( yawRange, pitchRange ) * 0.5 ), sinHalf, cosHalf );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "scale = 1.41421356 * sinHalf / ( cosHalf + 1.41421356 * sinHalf );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateVectorToOctahedron()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float2 VectorToOctahedron( float3 N )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N /= dot( 1.0, abs( N ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if( N.z <= 0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return N.xy;" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateOctahedronToVector()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float3 OctahedronToVector( float2 Oct )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if(N.z< 0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return normalize( N);" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateVectorToHemiOctahedron()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float2 VectorToHemiOctahedron( float3 N )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy /= dot( 1.0, abs( N ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return float2( N.x + N.y, N.x - N.y );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateHemiOctahedronToVector()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float3 HemiOctahedronToVector( float2 Oct )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return normalize( N );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateOctahedronRayPlaneIntersectionUV()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void RayPlaneIntersectionUV( float3 normalOS, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float lDotN = dot( rayDirection, normalOS ); " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float p0l0DotN = dot( -rayPosition, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float t = p0l0DotN / lDotN;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 p = rayDirection * t + rayPosition;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0, 1, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 tangent = normalize( cross( upVector, normalOS ) + float3( -0.001, 0, 0 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 bitangent = cross( tangent, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameX = dot( p, tangent );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameZ = dot( p, bitangent );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvs = -float2( frameX, frameZ );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if( t <= 0.0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvs = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3x3 worldToLocal = float3x3( tangent, bitangent, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "localNormal = normalize( mul( worldToLocal, rayDirection ) );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateImpostorClipPosDepthBias()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "// Orthographic" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "// Perspective" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return clipPos;" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, string propertyName, string uv, string biasName )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, propertyName, "sampler" + propertyName, uv, MipType.MipBias, biasName );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, TexturePropertyNode property, string uv, RangedFloatNode bias )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, property.PropertyName, "sampler" + property.PropertyName, uv, MipType.MipBias, bias.PropertyName );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, string property, string sampler, string uv, string bias )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, property, sampler, uv, MipType.MipBias, bias );
		}

		private static bool IsKeyword( KeywordMode mode )
		{
			return ( mode == KeywordMode.ShaderFeature ) || ( mode == KeywordMode.MultiCompile );
		}

		private void AddKeywordPragma( ref MasterNodeDataCollector dataCollector, KeywordMode mode, string keyword, bool fragmentOnly = false )
		{
			if ( !IsKeyword( mode ) )
			{
				return;
			}

			string pragma = ( mode == KeywordMode.MultiCompile ) ? "multi_compile_local" : "shader_feature_local";
			pragma += fragmentOnly ? "_fragment " : " ";
			pragma += ( mode == KeywordMode.MultiCompile ) ? "_ " + keyword : keyword;
			dataCollector.AddToPragmas( UniqueId, pragma );
		}

		// keyworded modes wrap both branches in #if/#else; the static modes emit only the branch that applies
		private void AddFeatureLines( KeywordMode mode, string keyword, string[] onLines, string[] offLines = null )
		{
			bool keyworded = IsKeyword( mode );
			if ( keyworded )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( " + keyword + " )" );
			}
			if ( mode != KeywordMode.Off )
			{
				foreach ( string line in onLines )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, line );
				}
			}
			if ( ( offLines != null ) && ( keyworded || ( mode == KeywordMode.Off ) ) )
			{
				if ( keyworded )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				}
				foreach ( string line in offLines )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, line );
				}
			}
			if ( keyworded )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
		}

		// when opted in, extra textures get a keyword named after their property, like the built-in maps, so the
		// baker's keyword update strips the ones it leaves unassigned; textures not coming from a property node
		// are always sampled
		private void GenerateExtraSamplers( ref MasterNodeDataCollector dataCollector, params string[] uvs )
		{
			string customSamplerState = m_samplerStatePort.IsConnected ? m_samplerStatePort.GeneratePortInstructions( ref dataCollector ) : string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( !GetInputPortByArrayId( i ).IsConnected || !GetOutputPortByArrayId( i ).IsConnected )
				{
					continue;
				}

				TexturePropertyNode node = GetInputPortByArrayId( i ).GetOutputNodeWhichIsNotRelay() as TexturePropertyNode;
				string samplerState = string.Empty;
				if ( m_samplerStatePort.IsConnected )
				{
					samplerState = customSamplerState;
				}
				else if ( node != null )
				{
					samplerState = node.GenerateSamplerState( ref dataCollector );
				}

				// one tap for the sphere, three weighted taps for the octahedron
				string output = "output" + i;
				string[] onLines;
				if ( uvs.Length == 1 )
				{
					onLines = new string[] { output + " = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, uvs[ 0 ], m_textureBiasProp.PropertyName ) + ";" };
				}
				else
				{
					onLines = new string[]
					{
						"float4 " + output + "a = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, uvs[ 0 ], m_textureBiasProp.PropertyName ) + ";",
						"float4 " + output + "b = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, uvs[ 1 ], m_textureBiasProp.PropertyName ) + ";",
						"float4 " + output + "c = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, uvs[ 2 ], m_textureBiasProp.PropertyName ) + ";",
						output + " = " + output + "a * weights.x  + " + output + "b * weights.y + " + output + "c * weights.z; "
					};
				}

				bool keyworded = m_extraSamplerKeywords && ( node != null );
				KeywordMode mode = keyworded ? m_textureKeywordMode : KeywordMode.On;
				string keyword = keyworded ? node.PropertyName.ToUpper() + "MAP" : string.Empty;
				AddKeywordPragma( ref dataCollector, mode, keyword, fragmentOnly: true );
				AddFeatureLines( mode, keyword, onLines, new string[] { output + " = 0;" } );
			}
		}

		private void GenerateSphereImpostorVertex( ref MasterNodeDataCollector dataCollector )
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvOffset = _AI_SizeOffset.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sizeX = " + m_framesXProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sizeY = max( " + m_framesYProp.PropertyName + " - 1, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float UVscale = " + m_impostorSizeProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 fractions = 1 / float4( sizeX, " + m_framesYProp.PropertyName + ", sizeY, UVscale );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 sizeFraction = fractions.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsUVscale = fractions.w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldCameraPos;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER)" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldOrigin = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 perspective = float4( 0, 0, 0, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "perspective = float4( 0, 0, 5000, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldOrigin = AI_ObjectToWorld._m03_m13_m23;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SCENEPICKINGPASS ) || defined( SCENESELECTIONPASS )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = UNITY_MATRIX_I_V._m03_m13_m23;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = _WorldSpaceCameraPos;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - " + m_offsetProp.PropertyName + ".xyz; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraDirection = normalize( objectCameraPosition );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0,1,0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );" );
			// frame selection; zero ranges mean the full sphere ( materials baked before the property existed )
			IOUtils.AddFunctionLine( ref m_functionBody, "float yawRange = ( " + m_angleRangeProp.PropertyName + ".x > 0 ) ? " + m_angleRangeProp.PropertyName + ".x : 360;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float pitchRange = ( " + m_angleRangeProp.PropertyName + ".y > 0 ) ? " + m_angleRangeProp.PropertyName + ".y : 180;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float verticalDot = dot( objectCameraDirection, upVector );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameX;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float yRot;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( yawRange <= 180 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3x3 capBasis;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 capScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "CapLayout( yawRange, pitchRange, " + m_angleRangeProp.PropertyName + ".zw, capBasis, capScale );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 cell = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameX = floor( cell.x * ( sizeX - 1 ) + 0.5 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameY = min( floor( cell.y * sizeY + 0.5 ), " + m_framesYProp.PropertyName + " - 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "yRot = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - " + m_angleRangeProp.PropertyName + ".z / 360.0 + 0.5 ) - 0.5 ) * 360.0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float pitch = degrees( asin( clamp( verticalDot, -1, 1 ) ) ) - " + m_angleRangeProp.PropertyName + ".w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float residualYaw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( yawRange >= 360 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float a = frac( -yaw / 360.0 - 0.25 ) * sizeX + 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameX = floor( a );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "residualYaw = ( frac( a ) - 0.5 ) * ( 360.0 / sizeX );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float yawStep = yawRange / max( sizeX - 1, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameX = floor( saturate( yaw / yawRange + 0.5 ) * ( sizeX - 1 ) + 0.5 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "residualYaw = ( frameX * yawStep - yawRange * 0.5 ) - yaw;" ); // same sense as the full turn, which runs the other way
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "yRot = ( sizeX > 1 ) ? radians( residualYaw ) * verticalDot : 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameY = min( floor( saturate( pitch / pitchRange + 0.5 ) * sizeY + 0.5 ), " + m_framesYProp.PropertyName + " - 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvExpansion = positionOS.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float cosY = cos( yRot );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sinY = sin( yRot );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 relativeCoords = float2( ( sizeX > 1 ) ? frameX : 0, frameY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameUVs.xy = frameUV - uvOffset;" );

			AddFeatureLines( m_parallaxMode, "_USE_PARALLAX_ON", new string[]
			{
				"float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );",
				"float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );",
				"float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );",
				// shift = depth * dir.xy / dir.z taken to frame UV, see OctaImpostorVertex
				"frameUVs.zw = sphereLocal.xy / max( -sphereLocal.z, 0.2 ) * " + m_depthProp.PropertyName + " * fractionsUVscale * sizeFraction * " + m_parallaxProp.PropertyName + ";"
			},
			new string[] { "frameUVs.zw = 0;" } );

			IOUtils.AddFunctionLine( ref m_functionBody, "positionOS = billboard + " + m_offsetProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "normalOS = objectCameraDirection;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "tangentOS = float4( objectHorizontalVector, 1 );" );

			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = UnityObjectToViewPos( positionOS );" );
			}

			AddFeatureLines( m_hueMode, "EFFECT_HUE_VARIATION", new string[]
			{
				"float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );",
				"viewPos.w = saturate( hueVariationAmount * " + m_hueProp.PropertyName + ".a );"
			},
			new string[] { "viewPos.w = 0;" } );

			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateSphereImpostorFragment( ref MasterNodeDataCollector dataCollector, bool withSpec = true, bool withOcclusion = true, bool withEmission = true, bool withPosition = false )
		{
			m_functionBody = string.Empty;

			string extraHeader = string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					extraHeader += ", out float4 output" + i;
			}

			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos" + extraHeader + " )" );
			// clip samples outside the frame rect (parallax overshoot into neighbour frames or past the texture border)
			AddFeatureLines( m_parallaxMode, "_USE_PARALLAX_ON", new string[]
			{
				"float parallax = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "frameUV.xy", "-1" ) + ".a;",
				"float2 frameCount = float2( " + m_framesXProp.PropertyName + ", " + m_framesYProp.PropertyName + " );",
				"float2 parallaxOffset = ( 0.5 - parallax ) * frameUV.zw;",
				"float2 dist = abs( frac( frameUV.xy * frameCount ) + parallaxOffset * frameCount - 0.5 );",
				"frameUV.xy += parallaxOffset;",
				"clip( 0.5 - max( dist.x, dist.y ) );"
			} );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedoSample = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "frameUV.xy", m_textureBiasProp ) + ";" );

			IOUtils.AddFunctionLine( ref m_functionBody, "o.Alpha = ( albedoSample.a - " + m_clipProp.PropertyName + " );" );
			if ( m_alphaClip )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "clip( o.Alpha );" );
			}
			AddFeatureLines( m_hueMode, "EFFECT_HUE_VARIATION", new string[]
			{
				"half3 shiftedColor = lerp( albedoSample.rgb, " + m_hueProp.PropertyName + ".rgb, viewPos.w );",
				"half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );",
				"half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );",
				"maxBase /= newMaxBase;",
				"maxBase = maxBase * 0.5f + 0.5f;",
				"shiftedColor.rgb *= maxBase;",
				"albedoSample.rgb = saturate( shiftedColor );"
			} );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Albedo = albedoSample.rgb;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 normalSample = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 objectNormal = normalSample * 2 - 1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float depth = ( normalSample.a - 0.5 ) * " + m_depthProp.PropertyName + " * objectScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth -= 0.001;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			bool specularWorkflow = ( m_workflow == ASEStandardSurfaceWorkflow.Specular );
			if ( withSpec )
			{
				AddFeatureLines( m_textureKeywordMode, "_SPECULARMAP", new string[]
				{
					"float4 specularSample = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "frameUV.xy", m_textureBiasProp ) + ";",
					specularWorkflow ? "o.Specular = specularSample.rgb;" : "o.Metallic = saturate( ( specularSample.rgb - unity_ColorSpaceDielectricSpec.rgb ) / ( albedoSample.rgb - unity_ColorSpaceDielectricSpec.rgb ) ).g; ",
					"o.Smoothness = specularSample.a;"
				},
				new string[]
				{
					specularWorkflow ? "o.Specular = 0;" : "o.Metallic = 0; ",
					"o.Smoothness = 0;"
				} );
			}
			if ( withOcclusion )
			{
				AddFeatureLines( m_textureKeywordMode, "_OCCLUSIONMAP", new string[]
				{
					"float4 occlusionSample = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "frameUV.xy", m_textureBiasProp ) + ";",
					"o.Occlusion = occlusionSample.a;"
				},
				new string[] { "o.Occlusion = 1;" } );
			}
			if ( withEmission )
			{
				AddFeatureLines( m_textureKeywordMode, "_EMISSIONMAP", new string[]
				{
					"float4 emissionSample = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "frameUV.xy", m_textureBiasProp ) + ";",
					"o.Emission = emissionSample.rgb;"
				},
				new string[] { "o.Emission = 0;" } );
			}
			if ( withPosition )
			{
				AddFeatureLines( m_textureKeywordMode, "_POSITIONMAP", new string[]
				{
					"float4 position = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "frameUV.xy", m_textureBiasProp ) + ";",
					"float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;",
					"float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;",
					"if ( position.a > 0 )",
					"{",
					"viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;",
					"depth = 0;",
					"}"
				} );
			}

			if ( dataCollector.IsSRP )
			{
				for ( int i = 0; i < DielecticSRPFix.Length; i++ )
				{
					dataCollector.AddToDirectives( DielecticSRPFix[ i ] );
				}
			}

			GenerateExtraSamplers( ref dataCollector, "frameUV.xy" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );" );
			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#elif defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;" );

			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADOWS_DEPTH) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = UnityApplyLinearShadowBias( positionCS );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)" );
				if ( dataCollector.CurrentSRPType == TemplateSRPType.URP )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "#if _CASTING_PUNCTUAL_LIGHT_SHADOW" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = normalize( _LightPosition - positionWS );" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = _LightDirection;" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );" );
				}
				else if ( dataCollector.CurrentSRPType == TemplateSRPType.HDRP )
				{
					// TODO HDRP
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				}
				IOUtils.AddFunctionLine( ref m_functionBody, "#if UNITY_REVERSED_Z" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateImpostorVertex( ref MasterNodeDataCollector dataCollector )
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void OctaImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvOffset = _AI_SizeOffset.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax = -" + m_parallaxProp.PropertyName + "; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float UVscale = " + m_impostorSizeProp.PropertyName + ";" );
			// materials baked before decoupled octahedron frames only carry Frames
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 framesXY = float2( ( " + m_framesXProp.PropertyName + " > 0 ) ? " + m_framesXProp.PropertyName + " : " + m_framesProp.PropertyName + ", ( " + m_framesYProp.PropertyName + " > 0 ) ? " + m_framesYProp.PropertyName + " : " + m_framesProp.PropertyName + " );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 prevFrame = framesXY - 1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractionsFrame = 1.0 / framesXY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractionsPrevFrame = 1.0 / max( prevFrame, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsUVscale = 1.0 / UVscale;" );
			// parallax: a texel at depth d above the frame plane shifts by d * dir.xy / dir.z; the clamp caps the shift at 5x the depth near grazing
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallaxScale = " + m_depthProp.PropertyName + " * fractionsUVscale * fractionsFrame * parallax;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "const float parallaxMinCos = 0.2;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldCameraPos;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER)" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldOrigin = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 perspective = float4( 0, 0, 0, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "perspective = float4( 0, 0, 5000, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldOrigin = AI_ObjectToWorld._m03_m13_m23;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#else" );

			// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = _WorldSpaceCameraPos;" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );

			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - " + m_offsetProp.PropertyName + ".xyz; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraDirection = normalize( objectCameraPosition );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0,1,0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvExpansion = positionOS.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectDir = billboard - objectCameraPosition; " );

			// @diogo: quantize to avoid a heinsenbug causing mismatching values between passes
			IOUtils.AddFunctionLine( ref m_functionBody, "objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;" );

			// frame grid coordinates; layout 0: native octahedral map; 1: yaw/pitch grid around the centre ( yaw range above 180, or wrapping ); 2: cap, the hemi-octahedral map re-centred on the range centre ( yaw range up to 180 )
			IOUtils.AddFunctionLine( ref m_functionBody, "float yawRange = ( " + m_angleRangeProp.PropertyName + ".x > 0 ) ? " + m_angleRangeProp.PropertyName + ".x : 360;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float pitchRange = ( " + m_angleRangeProp.PropertyName + ".y > 0 ) ? " + m_angleRangeProp.PropertyName + ".y : 180;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "int layout = ( yawRange < 360 || pitchRange < 180 ) ? ( ( yawRange <= 180 ) ? 2 : 1 ) : 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 prevOctaFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 gridOrigin = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 gridStep = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3x3 capBasis = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 capScale = 1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( layout == 2 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "CapLayout( yawRange, pitchRange, " + m_angleRangeProp.PropertyName + ".zw, capBasis, capScale );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "prevOctaFrame = saturate( VectorToHemiOctahedron( max( mul( capBasis, objectCameraDirection ), float3( -1, -1, 1e-4 ) ) ) / capScale * 0.5 + 0.5 ) * prevFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else if ( layout == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float yaw = ( frac( atan2( objectCameraDirection.x, objectCameraDirection.z ) * AI_INV_TWO_PI - " + m_angleRangeProp.PropertyName + ".z / 360.0 + 0.5 ) - 0.5 ) * 360.0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float pitch = degrees( asin( clamp( objectCameraDirection.y, -1, 1 ) ) ) - " + m_angleRangeProp.PropertyName + ".w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( yawRange >= 360 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "prevOctaFrame.x = frac( -yaw / 360.0 - 0.25 ) * framesXY.x;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridOrigin.x = -90;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridStep.x = -360.0 / framesXY.x;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "prevOctaFrame.x = saturate( yaw / yawRange + 0.5 ) * prevFrame.x;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridOrigin.x = -yawRange * 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridStep.x = yawRange / max( prevFrame.x, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "prevOctaFrame.y = saturate( pitch / pitchRange + 0.5 ) * prevFrame.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridOrigin.y = -pitchRange * 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "gridStep.y = pitchRange / max( prevFrame.y, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			if ( IsKeyword( HemiMode ) )
			{
				AddFeatureLines( HemiMode, "_HEMI_ON", new string[]
				{
					"objectCameraDirection.y = max( 0.001, objectCameraDirection.y );",
					"float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;"
				},
				new string[] { "float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" } );
			}
			else
			{
				if ( IsHemiOctahedron )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "objectCameraDirection.y = max(0.001, objectCameraDirection.y);" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				}
				else
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				}
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "prevOctaFrame = frameOcta * prevFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 baseOctaFrame = floor( prevOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1WorldY = OctaFrameToVector( baseOctaFrame, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );" );

			// single frame: plain camera-facing billboard
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( all( framesXY == 1 ) )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "octa1WorldY = objectCameraDirection;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax1 = octa1LocalY.xy / max( -octa1LocalY.z, parallaxMinCos ) * parallaxScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractPrevOctaFrame = frac( prevOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 cornerDifference = lerp( float2( 0,1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ));" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2WorldY = OctaFrameToVector( baseOctaFrame + cornerDifference, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame2;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax2 = octa2LocalY.xy / max( -octa2LocalY.z, parallaxMinCos ) * parallaxScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3WorldY = OctaFrameToVector( baseOctaFrame + 1, layout, gridOrigin, gridStep, capBasis, capScale, fractionsPrevFrame );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame3;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax3 = octa3LocalY.xy / max( -octa3LocalY.z, parallaxMinCos ) * parallaxScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );" );
			// single frame: every slot samples frame 1, selected by blend weights ( 1, 0, 0 ) since octaFrame.xy is zero
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( all( framesXY == 1 ) )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame2 = uvsFrame1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame3 = uvsFrame1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			// blend weight inputs ( constant across the mesh ) and frame cell centre, used by the frame rect clip
			IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame.x = fractPrevOctaFrame.x - fractPrevOctaFrame.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame.y = min( fractPrevOctaFrame.x, fractPrevOctaFrame.y );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame.zw = fractionOctaFrame + 0.5 * fractionsFrame;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "positionOS = billboard + " + m_offsetProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "normalOS = objectCameraDirection;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "tangentOS = float4( objectHorizontalVector, 1 );" );

			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = UnityObjectToViewPos( positionOS );" );
			}

			AddFeatureLines( m_hueMode, "EFFECT_HUE_VARIATION", new string[]
			{
				"float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );",
				"viewPos.w = saturate( hueVariationAmount * " + m_hueProp.PropertyName + ".a );"
			},
			new string[] { "viewPos.w = 0;" } );

			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		void GenerateImpostorFragment( ref MasterNodeDataCollector dataCollector, bool withSpec = true, bool withOcclusion = true, bool withEmission = true, bool withPosition = false )
		{
			m_functionBody = string.Empty;

			string extraHeader = string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					extraHeader += ", out float4 output" + i;
			}

			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void OctaImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos" + extraHeader + " )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 weights;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.y = abs( octaFrame.x );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.z = octaFrame.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.x = 1 - weights.y - weights.z;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax1 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame1.zw", "-1" ) + ".a;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax2 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame2.zw", "-1" ) + ".a;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax3 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame3.zw", "-1" ) + ".a;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax1_uv = ( ( 0.5 - parallax1 ) * uvsFrame1.xy ) + uvsFrame1.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax2_uv = ( ( 0.5 - parallax2 ) * uvsFrame2.xy ) + uvsFrame2.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax3_uv = ( ( 0.5 - parallax3 ) * uvsFrame3.xy ) + uvsFrame3.zw;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo1 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax1_uv", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo2 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax2_uv", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo3 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax3_uv", m_textureBiasProp ) + ";" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Alpha = blendedAlbedo.a;" );
			if ( m_alphaClip )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "clip( o.Alpha - " + m_clipProp.PropertyName + " );" );
			}
			// clip samples outside their frame rect (parallax overshoot into neighbour frames or past the texture border)
			IOUtils.AddFunctionLine( ref m_functionBody, "float t = step( 0, octaFrame.x );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 cornerDifference = float4( 0, 0, t, 1 - t );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 framesXY = float2( ( " + m_framesXProp.PropertyName + " > 0 ) ? " + m_framesXProp.PropertyName + " : " + m_framesProp.PropertyName + ", ( " + m_framesYProp.PropertyName + " > 0 ) ? " + m_framesYProp.PropertyName + " : " + m_framesProp.PropertyName + " );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 dist12 = abs( ( float4( parallax1_uv, parallax2_uv ) - octaFrame.zwzw ) * framesXY.xyxy - cornerDifference );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 dist3 = abs( ( parallax3_uv - octaFrame.zw ) * framesXY - 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 inside = step( float3( max( dist12.x, dist12.y ), max( dist12.z, dist12.w ), max( dist3.x, dist3.y ) ), 0.5 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clip( dot( inside, weights ) - 0.5 );" );
			AddFeatureLines( m_hueMode, "EFFECT_HUE_VARIATION", new string[]
			{
				"half3 shiftedColor = lerp( blendedAlbedo.rgb, " + m_hueProp.PropertyName + ".rgb, viewPos.w );",
				"half maxBase = max( blendedAlbedo.r, max( blendedAlbedo.g, blendedAlbedo.b ) );",
				"half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );",
				"maxBase /= newMaxBase;",
				"maxBase = maxBase * 0.5f + 0.5f;",
				"shiftedColor.rgb *= maxBase;",
				"blendedAlbedo.rgb = saturate( shiftedColor );"
			} );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Albedo = blendedAlbedo.rgb;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal1 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax1_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal2 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax2_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal3 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax3_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 blendedNormal = normal1 * weights.x  + normal2 * weights.y + normal3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectNormal = blendedNormal * 2.0 - 1.0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal ) );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float depth = parallax1 * weights.x + parallax2 * weights.y + parallax3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth = ( depth - 0.5 ) * " + m_depthProp.PropertyName + " * objectScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth -= 0.001;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			bool specularWorkflow = ( m_workflow == ASEStandardSurfaceWorkflow.Specular );
			if ( withSpec )
			{
				AddFeatureLines( m_textureKeywordMode, "_SPECULARMAP", new string[]
				{
					"float4 specular1 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax1_uv", m_textureBiasProp ) + ";",
					"float4 specular2 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax2_uv", m_textureBiasProp ) + ";",
					"float4 specular3 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax3_uv", m_textureBiasProp ) + ";",
					"float4 blendedSpecular = specular1 * weights.x  + specular2 * weights.y + specular3 * weights.z;",
					specularWorkflow ? "o.Specular = blendedSpecular.rgb;" : "o.Metallic = saturate( ( blendedSpecular.rgb - unity_ColorSpaceDielectricSpec.rgb ) / ( blendedAlbedo.rgb - unity_ColorSpaceDielectricSpec.rgb ) ).g; ",
					"o.Smoothness = blendedSpecular.a;"
				},
				new string[]
				{
					specularWorkflow ? "o.Specular = 0;" : "o.Metallic = 0; ",
					"o.Smoothness = 0;"
				} );
			}
			if ( withOcclusion )
			{
				AddFeatureLines( m_textureKeywordMode, "_OCCLUSIONMAP", new string[]
				{
					"float occlusion1 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax1_uv", m_textureBiasProp ) + ".g;",
					"float occlusion2 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax2_uv", m_textureBiasProp ) + ".g;",
					"float occlusion3 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax3_uv", m_textureBiasProp ) + ".g;",
					"o.Occlusion = occlusion1 * weights.x  + occlusion2 * weights.y + occlusion3 * weights.z;"
				},
				new string[] { "o.Occlusion = 1;" } );
			}
			if ( withEmission )
			{
				AddFeatureLines( m_textureKeywordMode, "_EMISSIONMAP", new string[]
				{
					"float3 emission1 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax1_uv", m_textureBiasProp ) + ".rgb;",
					"float3 emission2 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax2_uv", m_textureBiasProp ) + ".rgb;",
					"float3 emission3 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax3_uv", m_textureBiasProp ) + ".rgb;",
					"o.Emission = emission1 * weights.x  + emission2 * weights.y + emission3 * weights.z;"
				},
				new string[] { "o.Emission = 0;" } );
			}
			if ( withPosition )
			{
				AddFeatureLines( m_textureKeywordMode, "_POSITIONMAP", new string[]
				{
					"float4 position1 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax1_uv", m_textureBiasProp ) + ";",
					"float4 position2 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax2_uv", m_textureBiasProp ) + ";",
					"float4 position3 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax3_uv", m_textureBiasProp ) + ";",
					"float4 blendedPosition = position1 * weights.x  + position2 * weights.y + position3 * weights.z;",
					"float3 objectPosition = blendedPosition.xyz * _AI_BoundsSize + _AI_BoundsMin;",
					"float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;",
					"if ( blendedPosition.a > 0 )",
					"{",
					"viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;",
					"depth = 0;",
					"}"
				} );
			}

			if ( dataCollector.IsSRP )
			{
				for ( int i = 0; i < DielecticSRPFix.Length; i++ )
				{
					dataCollector.AddToDirectives( DielecticSRPFix[ i ] );
				}
			}

			GenerateExtraSamplers( ref dataCollector, "parallax1_uv", "parallax2_uv", "parallax3_uv" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );" );
			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#elif defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale  );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;" );

			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADOWS_DEPTH) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = UnityApplyLinearShadowBias( positionCS );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)" );
				if ( dataCollector.CurrentSRPType == TemplateSRPType.URP )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "#if _CASTING_PUNCTUAL_LIGHT_SHADOW" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = normalize( _LightPosition - positionWS );" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = _LightDirection;" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3( 0, 0, 0 ), lightDirectionWS ) );" );
				}
				else if ( dataCollector.CurrentSRPType == TemplateSRPType.HDRP )
				{
					// TODO HDRP
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				}
				IOUtils.AddFunctionLine( ref m_functionBody, "#if UNITY_REVERSED_Z" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}
	}
}
#endif
