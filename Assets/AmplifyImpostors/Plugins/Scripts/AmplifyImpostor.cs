// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

//#define AI_DEBUG_MODE

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using System.IO;
using Unity.Scripting.LifecycleManagement;

#if UNITY_EDITOR
	using UnityEditor;
	using Unity.Collections.LowLevel.Unsafe;
#endif

namespace AmplifyImpostors
{
	public enum LODReplacement
	{
		DoNothing = 0,
		ReplaceCulled = 1,
		ReplaceLast = 2,
		ReplaceAllExceptFirst = 3,
		ReplaceSpecific = 4,
		ReplaceAfterSpecific = 5,
		InsertAfter = 6
	}

	public enum LODGroupAutoCreate
	{
		UseGlobal = 0,
		Create = 1,
		DontCreate = 2
	}

	public enum CutMode
	{
		Automatic = 0,
		Manual = 1
	}

	public enum FolderMode
	{
		RelativeToPrefab = 0,
		Global = 1
	}

	public enum RenderPipelineInUse
	{
		None = 0,
		HDRP = 1,
		URP = 2,
		Custom = 3
	}

	//[ExecuteInEditMode]
	[NoAutoStaticsCleanup]
	[HelpURL( "https://wiki.amplify.pt/index.php?title=Unity_Products:Amplify_Impostors/Manual" )]
	public class AmplifyImpostor : MonoBehaviour
	{
		public const string ShaderBiRP = "e82933f4c0eb9ba42aab0739f48efe21";
		public const string ShaderOctaBiRP = "572f9be5706148142b8da6e9de53acdb";

		public const string ShaderHDRP = "175c951fec709c44fa2f26b8ab78b8dd";
		public const string ShaderOctaHDRP = "56236dc63ad9b7949b63a27f0ad180b3";

		public const string ShaderURP = "da79d698f4bf0164e910ad798d07efdf";
		public const string ShaderOctaURP = "83dd8de9a5c14874884f9012def4fdcc";

		public const string DilateGUID = "57c23892d43bc9f458360024c5985405";
		public const string PackerGUID = "31bd3cd74692f384a916d9d7ea87710d";
		public const string GBufferToOutputGUID = "9587d58ea8f1dac478d1adbf2a63d31f";

		private const string GlobalShaderVariablesQualifiedNameHDRP = "UnityEngine.Rendering.HighDefinition.ShaderVariablesGlobal, Unity.RenderPipelines.HighDefinition.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
		private const string HDShaderUtilsQualifiedNameHDRP = "UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor";

		public readonly static int _DetailNormalMap_PID = Shader.PropertyToID( "_DetailNormalMap" );

		// when true, the per-impostor "Baking Impostor" progress bar is suppressed (e.g. during batch baking)
		public static bool SuppressProgressBar = false;

		[SerializeField]
		private AmplifyImpostorAsset m_data;
		public AmplifyImpostorAsset Data { get { return m_data; } set { m_data = value; } }

		[SerializeField]
		private Transform m_rootTransform;
		public Transform RootTransform { get { return m_rootTransform; } set { m_rootTransform = value; } }

		[SerializeField]
		private LODGroup m_lodGroup;
		public LODGroup LodGroup { get { return m_lodGroup; } set { m_lodGroup = value; } }

		[SerializeField]
		private Renderer[] m_renderers;
		public Renderer[] Renderers { get { return m_renderers; } set { m_renderers = value; } }

		public LODReplacement m_lodReplacement = LODReplacement.ReplaceLast;

		// Auto-create a LOD Group on bake when none is assigned, adding the impostor as LOD1.
		public LODGroupAutoCreate m_autoCreateLodGroup = LODGroupAutoCreate.UseGlobal;
		[Range( 0f, 1f )]
		public float m_lodTransition = 0.1f;

#if UNITY_EDITOR
		// seed new components with the project-wide default transition
		private void Reset()
		{
			m_lodTransition = Preferences.Project.LodTransition;
		}
#endif

		public Material m_copyPropertiesSource = null;
		public MaterialPropertyCopyMask m_copyPropertiesMask =	MaterialPropertyCopyMask.Floats |
																MaterialPropertyCopyMask.Colors |
																MaterialPropertyCopyMask.Vectors |
																MaterialPropertyCopyMask.Textures;

		[SerializeField]
		public RenderPipelineInUse m_renderPipelineInUse = RenderPipelineInUse.None;

		public int m_insertIndex = 1;

		[SerializeField]
		public GameObject m_lastImpostor;

		[SerializeField]
		public string m_folderPath;

		[NonSerialized]
		public string m_impostorName = string.Empty;

		[SerializeField]
		public CutMode m_cutMode = CutMode.Automatic;

		[NonSerialized]
		private const float StartXRotation = -90;
		[NonSerialized]
		private const float StartYRotation = 90;
		[NonSerialized]
		private static readonly Vector3 SingleFrameViewDirection = new Vector3( 0, 0, 1 ); // object-space direction from object to camera when an axis has a single frame ( the object's forward, +Z )
		[NonSerialized]
		private const int MinAlphaResolution = 256;
		[NonSerialized]
		private RenderTexture[] m_rtGBuffers;
		[NonSerialized]
		private RenderTexture[] m_outBuffers;
		[NonSerialized]
		private RenderTexture[] m_alphaGBuffers;
		[NonSerialized]
		private RenderTexture m_trueDepth;
		[NonSerialized]
		public Texture2D m_alphaTex;
		[NonSerialized]
		private float m_xyFitSize = 0;
		[NonSerialized]
		private float m_depthFitSize = 0;
		[NonSerialized]
		private Vector2 m_pixelOffset = Vector2.zero;

		[NonSerialized]
		private Bounds m_originalBound = new Bounds();

		[NonSerialized]
		private Vector3 m_oriPos = Vector3.zero;

		[NonSerialized]
		private Quaternion m_oriRot = Quaternion.identity;

		[NonSerialized]
		private Vector3 m_oriSca = Vector3.one;

		[NonSerialized]
		private const int BlockSize = 65536;

	#if UNITY_EDITOR
		[NonSerialized]
		private readonly string[] m_propertyNames = { "_Albedo", "_Normals", "_Specular", "_Occlusion", "_Emission", "_Position" };

		[NonSerialized]
		private string[] m_standardFileNamesOld = { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };

		[NonSerialized]
		private string[] m_standardFileNames = { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };

		[NonSerialized]
		private string[] m_fileNames = { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };

		[Serializable]
		private class TextureData
		{
			public bool SRGB = true;
			public bool Alpha = true;
			public TextureCompression Compression = TextureCompression.Normal;
			public int MaxSize = -1;
			public TextureData() { }
			public TextureData( bool sRGB, TextureCompression compression, bool alpha, int maxSize )
			{
				SRGB = sRGB;
				Compression = compression;
				Alpha = alpha;
				MaxSize = maxSize;
			}
		}
	#endif

		[NonSerialized]
		private Matrix4x4[] m_cameraInvViewProjPerFrame = null;

	#if AI_DEBUG_MODE
		[SerializeField]
		private string m_renderInfo = string.Empty;
		public string RenderInfo { get { return m_renderInfo; } set { m_renderInfo = value; } }
		public bool m_createGameobject = true;
		public bool m_generateQuad = true;
	#endif

		private enum RenderImpostorMode
		{
			Alpha = 0,
			Normal = 1
		}

		private void GenerateTextures( List<TextureOutput> outputList, bool standardRendering )
		{
			m_outBuffers = new RenderTexture[ outputList.Count ];
			for ( int i = 0; i < outputList.Count; i++ )
			{
				m_outBuffers[ i ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, outputList[ i ].SRGB ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGBHalf );
				m_outBuffers[ i ].Create();
			}

			if ( standardRendering )
			{
				m_rtGBuffers = new RenderTexture[ 4 ];

				if ( m_renderPipelineInUse == RenderPipelineInUse.HDRP )
				{
					// Albedo (RGB) Occlusion (A) [A => occlusion(7) / isLightmap(1)]
					m_rtGBuffers[ 0 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_SRGB );
					m_rtGBuffers[ 0 ].Create();

					// Normals (RGB) PerceptualRoughness (A) [RGB => X:Y / 12:12 / Octa]
					m_rtGBuffers[ 1 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_UNorm );
					m_rtGBuffers[ 1 ].Create();

					// Specular (RGB)
					m_rtGBuffers[ 2 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_UNorm );
					m_rtGBuffers[ 2 ].Create();

					// Emission (RGB) Alpha (A)
					m_rtGBuffers[ 3 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R16G16B16A16_SFloat );
					m_rtGBuffers[ 3 ].Create();
				}
				else if ( m_renderPipelineInUse == RenderPipelineInUse.URP )
				{
					// Albedo (RGB) MaterialFlags (A) [A => miscFlags(7) / specularSetup(1)]
					m_rtGBuffers[ 0 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_SRGB );
					m_rtGBuffers[ 0 ].Create();

					// Specular (RGB) Occlusion (A) [specularSetup ? R => Metallic : RGB => Specular ]
					m_rtGBuffers[ 1 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_UNorm );
					m_rtGBuffers[ 1 ].Create();

					// Normals (RGB) Smoothness (A)
					m_rtGBuffers[ 2 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_SNorm );
					m_rtGBuffers[ 2 ].Create();

					// Emission (RGB) Alpha (A)
					m_rtGBuffers[ 3 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R16G16B16A16_SFloat );
					m_rtGBuffers[ 3 ].Create();
				}
				else
				{
					// Albedo (RGB) Occlusion (A)
					m_rtGBuffers[ 0 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_SRGB );
					m_rtGBuffers[ 0 ].Create();

					// Specular (RGB) Smoothness (A)
					m_rtGBuffers[ 1 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R8G8B8A8_SRGB );
					m_rtGBuffers[ 1 ].Create();

					// Normals (RGB)
					m_rtGBuffers[ 2 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.A2B10G10R10_UNormPack32 );
					m_rtGBuffers[ 2 ].Create();

					// Emission (RGB) Alpha (A)
					m_rtGBuffers[ 3 ] = new RenderTexture( ( int )m_data.TexSize.x, ( int )m_data.TexSize.y, 16, GraphicsFormat.R16G16B16A16_SFloat );
					m_rtGBuffers[ 3 ].Create();
				}
			}
			else
			{
				m_rtGBuffers = m_outBuffers;
			}

			m_trueDepth = new RenderTexture( (int)m_data.TexSize.x, (int)m_data.TexSize.y, 16, RenderTextureFormat.Depth );
			m_trueDepth.Create();
		}

		private void GenerateAlphaTextures( int targetAmount )
		{
			m_alphaGBuffers = new RenderTexture[ targetAmount ];

			for( int i = 0; i < m_alphaGBuffers.Length; ++i )
			{
				m_alphaGBuffers[ i ] = new RenderTexture( MinAlphaResolution, MinAlphaResolution, 16, RenderTextureFormat.ARGB32 );
				m_alphaGBuffers[ i ].Create();
			}

			m_trueDepth = new RenderTexture( MinAlphaResolution, MinAlphaResolution, 16, RenderTextureFormat.Depth );
			m_trueDepth.Create();
		}

		private void ClearBuffers()
		{
			RenderTexture.active = null;

			foreach( var rt in m_rtGBuffers )
			{
				rt.Release();
			}
			m_rtGBuffers = null;

			foreach ( var rt in m_outBuffers )
			{
				rt.Release();
			}
			m_outBuffers = null;
		}

		private void ClearAlphaBuffers( )
		{
			RenderTexture.active = null;
			foreach( var rt in m_alphaGBuffers )
			{
				rt.Release();
			}
			m_alphaGBuffers = null;
		}

		public static void GetFrameInfo( AmplifyImpostorAsset data, out int hframes, out int vframes )
		{
			hframes = data.HorizontalFrames;
			vframes = data.DecoupleAxisFrames ? data.VerticalFrames : data.HorizontalFrames;
		}

		// Object-space direction from the object towards the bake camera of frame ( x, y ); fractional coordinates give the directions between frames
		public static Vector3 GetFrameViewDirection( AmplifyImpostorAsset data, float x, float y )
		{
			GetFrameInfo( data, out int hframes, out int vframes );
			Matrix4x4 camMatrixRot = GetCameraRotationMatrix( data.ImpostorType, hframes, vframes, x, y, data.AngleRange, data.AngleCenter );
			return camMatrixRot.inverse.MultiplyVector( Vector3.back ).normalized;
		}

	#if UNITY_EDITOR
		private static readonly List<KeyValuePair<string, int>> m_hdrpStencilCheck = new List<KeyValuePair<string, int>> {
			new KeyValuePair<string,int>( "_StencilForwardRef", 0 ),
			new KeyValuePair<string,int>( "_StencilForwardMask", 6 ),
			new KeyValuePair<string,int>( "_StencilMotionRef", 40 ),
			new KeyValuePair<string,int>( "_StencilMotionMask", 40 ),
			new KeyValuePair<string,int>( "_StencilDepthRef", 8 ),
			new KeyValuePair<string,int>( "_StencilDepthMask", 8 ),
			new KeyValuePair<string,int>( "_StencilGBufferRef", 10 ),
			new KeyValuePair<string,int>( "_StencilGBufferMask", 14 )
		};

		public void CheckHDRPMaterial()
		{
			if( m_renderPipelineInUse != RenderPipelineInUse.HDRP )
				return;

			if( m_data == null || m_data.Preset == null || m_data.Material == null )
				return;

			foreach ( var pair in m_hdrpStencilCheck )
			{
				if ( m_data.Material.HasProperty( pair.Key ) && m_data.Material.GetInt( pair.Key ) != pair.Value )
				{
					m_data.Material.SetInt( pair.Key, pair.Value );
				}
			}
		}

		// an HDRP Shader Graph material only gets its HDRP keywords (like _DEPTHOFFSET_ON) once HDRP validates it, which new Material( shader ) never does by itself
		private static void ValidateHDRPShaderGraphMaterial( Material material )
		{
			// HDShaderUtils does not know the other shaders (like the ASE ones), so only Shader Graph shaders are validated; they are the ones imported by the ShaderGraphImporter
			AssetImporter shaderImporter = AssetImporter.GetAtPath( AssetDatabase.GetAssetPath( material.shader ) );
			if( shaderImporter == null || shaderImporter.GetType().Name != "ShaderGraphImporter" )
				return;

			try
			{
				Type shaderUtilsType = Type.GetType( HDShaderUtilsQualifiedNameHDRP );
				System.Reflection.MethodInfo resetKeywords = shaderUtilsType != null ? shaderUtilsType.GetMethod( "ResetMaterialKeywords", new Type[] { typeof( Material ) } ) : null;
				if( resetKeywords != null )
					resetKeywords.Invoke( null, new object[] { material } );
			}
			catch( Exception )
			{
				// the validation is a courtesy, a missing or failing HDRP editor API must not break the bake
			}
		}

		public void SaveDebugRenderTexturePNG( ref RenderTexture rtex, string path )
		{
			SaveTexture( ref rtex, path, ImageFormat.PNG, 1, TextureChannels.RGBA, rtex.width, rtex.height );
		}

		public void SaveTexture( ref RenderTexture tex, string path, ImageFormat imageFormat, int resizeScale, TextureChannels channels, int width = 0, int height = 0 )
		{
			width = ( width != 0 ) ? width : ( int )m_data.TexSize.x;
			height = ( height != 0 ) ? height : ( int )m_data.TexSize.y;

			Texture2D outfile = AssetDatabase.LoadAssetAtPath<Texture2D>( path );
			if( imageFormat == ImageFormat.EXR )
				outfile = new Texture2D( (int)width / resizeScale, (int)height / resizeScale, TextureFormat.RGBAFloat, false );
			else
				outfile = new Texture2D( (int)width / resizeScale, (int)height / resizeScale, channels == TextureChannels.RGB ? TextureFormat.RGB24 : TextureFormat.RGBA32, true );

			outfile.name = Path.GetFileNameWithoutExtension( path );
			RenderTexture temp = RenderTexture.active;
			RenderTexture.active = tex;
			outfile.ReadPixels( new Rect( 0, 0, (int)width / resizeScale, (int)height / resizeScale ), 0, 0 );

			RenderTexture.active = temp;
			outfile.Apply();
			byte[] bytes;
			switch( imageFormat )
			{
				case ImageFormat.PNG:
				bytes = outfile.EncodeToPNG();
				break;
				default:
				case ImageFormat.TGA:
				bytes = outfile.EncodeToTGA( Texture2DEx.Compression.RLE );
				break;
				case ImageFormat.EXR:
				bytes = ImageConversion.EncodeToEXR( outfile, Texture2D.EXRFlags.CompressZIP );
				break;
			}

			if( imageFormat == ImageFormat.EXR )
			{
				File.WriteAllBytes( path, bytes );
				DestroyImmediate( outfile );
			}
			else
			{
				int BytesToWrite, BufIndex;
				int bytesLength = bytes.Length;
				FileStream FSFile = new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None, BlockSize, false );
				BufIndex = 0;
				do
				{
					BytesToWrite = Math.Min( BlockSize, bytesLength - BufIndex );
					FSFile.Write( bytes, BufIndex, BytesToWrite );
					BufIndex += BytesToWrite;
				} while( BufIndex < bytesLength );
				FSFile.Close();
				FSFile.Dispose();
				DestroyImmediate( outfile );
			}
		}

		public void ChangeTextureImporter( ref RenderTexture tex, string path, bool sRGB = true, bool changeResolution = false, TextureCompression compression = TextureCompression.Normal, bool alpha = true )
		{
			Texture2D outfile = AssetDatabase.LoadAssetAtPath<Texture2D>( path );
			TextureImporter tImporter = AssetImporter.GetAtPath( path ) as TextureImporter;
			if( tImporter != null )
			{
				if( (tImporter.alphaSource == TextureImporterAlphaSource.FromInput && !alpha) ||
					( tImporter.textureCompression != (TextureImporterCompression)compression ) ||
					tImporter.sRGBTexture != sRGB ||
					( changeResolution && tImporter.maxTextureSize != (int)m_data.TexSize.x ) )
				{
					tImporter.sRGBTexture = sRGB;
					tImporter.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
					tImporter.textureCompression = (TextureImporterCompression)compression;
					if( changeResolution )
						tImporter.maxTextureSize = (int)m_data.TexSize.x;

					EditorUtility.SetDirty( tImporter );
					EditorUtility.SetDirty( outfile );
					tImporter.SaveAndReimport();
				}
			}
		}

		public void CalculateSheetBounds()
		{
			m_xyFitSize = 0;
			m_depthFitSize = 0;

			GetFrameInfo( m_data, out int hframes, out int vframes );

			for( int x = 0; x < hframes; x++ )
			{
				for( int y = 0; y < vframes; y++ )
				{
					Bounds frameBounds = new Bounds();
					Matrix4x4 camMatrixRot = GetCameraRotationMatrix( m_data.ImpostorType, hframes, vframes, x, y, m_data.AngleRange, m_data.AngleCenter );

					for( int i = 0; i < Renderers.Length; i++ )
					{
						if( Renderers[ i ] == null || !Renderers[ i ].enabled || Renderers[ i ].shadowCastingMode == ShadowCastingMode.ShadowsOnly )
							continue;

						MeshFilter mf = Renderers[ i ].GetComponent<MeshFilter>();
						if( mf == null || mf.sharedMesh == null )
							continue;

						if( frameBounds.size == Vector3.zero )
							frameBounds = mf.sharedMesh.bounds.Transform( m_rootTransform.worldToLocalMatrix * Renderers[ i ].localToWorldMatrix );
						else
							frameBounds.Encapsulate( mf.sharedMesh.bounds.Transform( m_rootTransform.worldToLocalMatrix * Renderers[ i ].localToWorldMatrix ) );
					}

					if( x == 0 && y == 0 )
						m_originalBound = frameBounds;

					frameBounds = frameBounds.Transform( camMatrixRot );
					m_xyFitSize = Mathf.Max( m_xyFitSize, frameBounds.size.x, frameBounds.size.y );

					const float depthFitCorrection = 1.0f; // @diogo: cube with a smaller cube on top was intersecting near plane

					m_depthFitSize = Mathf.Max( m_depthFitSize, frameBounds.size.z * depthFitCorrection );
				}
			}

		#if AI_DEBUG_MODE
			m_renderInfo = "";
			m_renderInfo += "\nXY fit:\t" + m_trueFitsize;
			m_renderInfo += "\nDepth:\t" + m_depthFitsize;
		#endif
		}

		public void DilateRenderTextureUsingMask( ref RenderTexture mainTex, ref RenderTexture maskTex, int pixelBleed, bool alpha, Material dilateMat = null )
		{
			if( pixelBleed == 0 )
				return;

			bool destroyMaterial = false;
			if( dilateMat == null )
			{
				destroyMaterial = true;
				Shader dilateShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( DilateGUID ) );
				dilateMat = new Material( dilateShader );
			}

			RenderTexture tempTex = RenderTexture.GetTemporary( mainTex.width, mainTex.height, mainTex.depth, mainTex.format );
			RenderTexture tempMask = RenderTexture.GetTemporary( maskTex.width, maskTex.height, maskTex.depth, maskTex.format );
			RenderTexture dilatedMask = RenderTexture.GetTemporary( maskTex.width, maskTex.height, maskTex.depth, maskTex.format );

			Graphics.Blit( maskTex, dilatedMask );

			for( int i = 0; i < pixelBleed; i++ )
			{
				dilateMat.SetTexture( "_MaskTex", dilatedMask );

				Graphics.Blit( mainTex, tempTex, dilateMat, alpha ? 1 : 0 );
				Graphics.Blit( tempTex, mainTex );

				Graphics.Blit( dilatedMask, tempMask, dilateMat, 1 );
				Graphics.Blit( tempMask, dilatedMask );
			}

			RenderTexture.ReleaseTemporary( tempTex );
			RenderTexture.ReleaseTemporary( tempMask );
			RenderTexture.ReleaseTemporary( dilatedMask );

			if( destroyMaterial )
			{
				DestroyImmediate( dilateMat );
				dilateMat = null;
			}
		}

		public void PackingRemapping( ref RenderTexture src, ref RenderTexture dst, int passIndex, Material packerMat = null, Texture extraTex = null, string texName = null )
		{
			bool destroyMaterial = false;
			if( packerMat == null )
			{
				destroyMaterial = true;
				Shader packerShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( PackerGUID ) );
				packerMat = new Material( packerShader );
			}

			if( extraTex != null )
			{
				if( string.IsNullOrEmpty( texName ) )
					packerMat.SetTexture( "_A", extraTex );
				else
					packerMat.SetTexture( texName, extraTex );
			}

			if( src == dst )
			{
				int width = src.width;
				int height = src.height;
				int depth = src.depth;
				RenderTextureFormat format = src.format;

				RenderTexture tempTex = RenderTexture.GetTemporary( width, height, depth, format );
				Graphics.Blit( src, tempTex, packerMat, passIndex );
				Graphics.Blit( tempTex, dst );
				RenderTexture.ReleaseTemporary( tempTex );
			}
			else
			{
				Graphics.Blit( src, dst, packerMat, passIndex );
			}

			if( destroyMaterial )
			{
				DestroyImmediate( packerMat );
				packerMat = null;
			}
		}

		private void CopyTransform()
		{
			m_oriPos = RootTransform.position;
			m_oriRot = RootTransform.rotation;
			m_oriSca = RootTransform.localScale;
			RootTransform.position = Vector3.zero;
			RootTransform.rotation = Quaternion.identity;
			RootTransform.localScale = Vector3.one;
		}

		private void PasteTransform()
		{
			RootTransform.position = m_oriPos;
			RootTransform.rotation = m_oriRot;
			RootTransform.localScale = m_oriSca;
		}

		public void CalculatePixelBounds( int targetAmount )
		{
			bool sRGBcache = GL.sRGBWrite;

			CalculateSheetBounds();
			GenerateAlphaTextures( targetAmount );

			GL.sRGBWrite = true;

			m_pixelOffset = Vector2.zero;

			// TODO: remove this temporary solution
			CopyTransform();
			try
			{
				RenderImpostor( targetAmount, RenderImpostorMode.Alpha, true, m_data.Preset.BakeShader );
				PasteTransform();
			}
			finally
			{
				PasteTransform();
				if( !SuppressProgressBar )
				{
					EditorUtility.ClearProgressBar();
				}
			}

			GL.sRGBWrite = sRGBcache;

			bool standardRendering = ( m_data.Preset.BakeShader == null );
			int alphaIndex = standardRendering ? 3 : m_data.Preset.AlphaIndex;

			Shader packerShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( PackerGUID ) );
			Material packerMat = new Material( packerShader );

			// Generate Alpha using Depth
			RenderTexture tempTex2 = RenderTextureEx.GetTemporary( m_alphaGBuffers[ alphaIndex ] );
			Graphics.Blit( m_alphaGBuffers[ alphaIndex ], tempTex2 );
			packerMat.SetTexture( "_A", tempTex2 );
			Graphics.Blit( m_trueDepth, m_alphaGBuffers[ alphaIndex ], packerMat, 11 );
			RenderTexture.ReleaseTemporary( tempTex2 );

			m_trueDepth.Release();
			m_trueDepth = null;

			// Render just alpha
			RenderTexture combinedAlphaTexture = RenderTexture.GetTemporary( MinAlphaResolution, MinAlphaResolution, m_alphaGBuffers[ alphaIndex ].depth, m_alphaGBuffers[ alphaIndex ].format );
			PackingRemapping( ref m_alphaGBuffers[ alphaIndex ], ref combinedAlphaTexture, 8, packerMat );

			DestroyImmediate( packerMat );
			packerMat = null;

			ClearAlphaBuffers();

			RenderTexture.active = combinedAlphaTexture;
			Texture2D tempTex = new Texture2D( combinedAlphaTexture.width, combinedAlphaTexture.height, TextureFormat.RGBA32, false );
			tempTex.ReadPixels( new Rect( 0, 0, combinedAlphaTexture.width, combinedAlphaTexture.height ), 0, 0 );
			tempTex.Apply();
			RenderTexture.active = null;

			RenderTexture.ReleaseTemporary( combinedAlphaTexture );

			Rect testRect = new Rect( 0, 0, tempTex.width, tempTex.height );
			Vector2[][] paths;
			SpriteUtilityEx.GenerateOutline( tempTex, testRect, 0.2f, 0, false, out paths );
			int sum = 0;
			for( int i = 0; i < paths.Length; i++ )
			{
				sum += paths[ i ].Length;
			}

			Vector2[] minMaxPoints = new Vector2[ sum ];
			int index = 0;
			for( int i = 0; i < paths.Length; i++ )
			{
				for( int j = 0; j < paths[ i ].Length; j++ )
				{
					minMaxPoints[ index ] = (Vector2)( paths[ i ][ j ] ) + ( new Vector2( tempTex.width * 0.5f, tempTex.height * 0.5f ) );
					minMaxPoints[ index ] = Vector2.Scale( minMaxPoints[ index ], new Vector2( 1.0f / tempTex.width, 1.0f / tempTex.height ) );
					index++;
				}
			}

			Vector2 mins = Vector2.one;
			Vector2 maxs = Vector2.zero;

			for( int i = 0; i < minMaxPoints.Length; i++ )
			{
				mins.x = Mathf.Min( minMaxPoints[ i ].x, mins.x );
				mins.y = Mathf.Min( minMaxPoints[ i ].y, mins.y );
				maxs.x = Mathf.Max( minMaxPoints[ i ].x, maxs.x );
				maxs.y = Mathf.Max( minMaxPoints[ i ].y, maxs.y );
			}

			Vector2 height = ( maxs - mins );
			float maxBound = Mathf.Max( height.x, height.y );
			Vector2 center = mins + ( height * 0.5f );
			m_pixelOffset = ( center - ( Vector2.one * 0.5f ) ) * m_xyFitSize;
			//Debug.Log( m_pixelOffset.ToString( "N5" ) );
			//Debug.Log( height.ToString( "N5" ) );
			m_xyFitSize *= maxBound;
			m_depthFitSize *= maxBound;
		}

		// For inspector
		public void RenderCombinedAlpha( AmplifyImpostorAsset data = null )
		{
			AmplifyImpostorAsset tempData = m_data;
			if( data != null )
				m_data = data;

			bool standardRendering = ( m_data.Preset.BakeShader == null );
			int targetAmount = standardRendering ? 4 : m_data.Preset.Output.Count;

			CalculatePixelBounds( targetAmount );
			GenerateAlphaTextures( targetAmount );

			bool sRGBcache = GL.sRGBWrite;
			GL.sRGBWrite = true;

			// TODO: remove this temporary solution
			CopyTransform();
			try
			{
				RenderImpostor( targetAmount, RenderImpostorMode.Alpha, false, m_data.Preset.BakeShader );
				PasteTransform();
			}
			finally
			{
				PasteTransform();
				if( !SuppressProgressBar )
				{
					EditorUtility.ClearProgressBar();
				}
			}

			GL.sRGBWrite = sRGBcache;

			int alphaIndex = standardRendering ? 3 : m_data.Preset.AlphaIndex;

			Shader packerShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( PackerGUID ) );
			Material packerMat = new Material( packerShader );

			// Generate Alpha using Depth
			RenderTexture tempTex = RenderTextureEx.GetTemporary( m_alphaGBuffers[ alphaIndex ] );
			Graphics.Blit( m_alphaGBuffers[ alphaIndex ], tempTex );
			packerMat.SetTexture( "_A", tempTex );
			Graphics.Blit( m_trueDepth, m_alphaGBuffers[ alphaIndex ], packerMat, 11 );
			RenderTexture.ReleaseTemporary( tempTex );

			m_trueDepth.Release();
			m_trueDepth = null;

			// Render just alpha
			RenderTexture combinedAlphaTexture = RenderTexture.GetTemporary( MinAlphaResolution, MinAlphaResolution, m_alphaGBuffers[ alphaIndex ].depth, m_alphaGBuffers[ alphaIndex ].format );
			PackingRemapping( ref m_alphaGBuffers[ alphaIndex ], ref combinedAlphaTexture, 8, packerMat );

			DestroyImmediate( packerMat );
			packerMat = null;

			ClearAlphaBuffers();

			RenderTexture.active = combinedAlphaTexture;
			m_alphaTex = new Texture2D( combinedAlphaTexture.width, combinedAlphaTexture.height, TextureFormat.RGBAFloat, false );
			m_alphaTex.ReadPixels( new Rect( 0, 0, combinedAlphaTexture.width, combinedAlphaTexture.height ), 0, 0 );
			m_alphaTex.Apply();
			RenderTexture.active = null;

			RenderTexture.ReleaseTemporary( combinedAlphaTexture );

			m_data = tempData;
		}

		public void CreateAssetFile( AmplifyImpostorAsset data = null )
		{
			string folderPath = this.OpenFolderForImpostor();

			if( string.IsNullOrEmpty( folderPath ) )
				return;

			string fileName = m_impostorName;

			if( string.IsNullOrEmpty( fileName ) )
				fileName = m_rootTransform.name + "_Impostor";

			folderPath = folderPath.TrimEnd( new char[] { '/', '*', '.', ' ' } );
			folderPath += "/";
			folderPath = folderPath.TrimStart( new char[] { '/', '*', '.', ' ' } );

			if( m_data == null )
			{
				Undo.RegisterCompleteObjectUndo( this, "Create Impostor Asset" );
				AmplifyImpostorAsset existingAsset = AssetDatabase.LoadAssetAtPath<AmplifyImpostorAsset>( folderPath + fileName + ".asset" );
				if( existingAsset != null )
				{
					m_data = existingAsset;
				}
				else
				{
					m_data = ScriptableObject.CreateInstance<AmplifyImpostorAsset>();
					AssetDatabase.CreateAsset( m_data, folderPath + fileName + ".asset" );
				}
			}
		}

		private void DisplayProgress( float progress, string message )
		{
		#if UNITY_EDITOR
			if( !Application.isPlaying && !SuppressProgressBar )
			{
				EditorUtility.DisplayProgressBar( "Baking Impostor", message, progress );

				if( progress >= 1.0f )
					EditorUtility.ClearProgressBar();
			}
		#endif
		}

		public void DetectRenderPipeline()
		{
			string pipelineName = string.Empty;
			try
			{
				pipelineName = UnityEngine.Rendering.RenderPipelineManager.currentPipeline.ToString();
			}
			catch( Exception )
			{
				pipelineName = "";
			}

			if( pipelineName.Contains( "UniversalRenderPipeline" ) )
			{
				m_renderPipelineInUse = RenderPipelineInUse.URP;
			}
			else if( pipelineName.Contains( "HDRenderPipeline" ) )
			{
				m_renderPipelineInUse = RenderPipelineInUse.HDRP;
			}
			else if( pipelineName.Equals( "" ) )
			{
				m_renderPipelineInUse = RenderPipelineInUse.None;
			}
			else
			{
				m_renderPipelineInUse = RenderPipelineInUse.Custom;
			}
		}

		// the render pipeline is only created by the first render; in batch mode nothing has rendered yet and the bake
		// would run against an uninitialised pipeline, so render an empty frame once
		private static void EnsureRenderPipelineCreated()
		{
			if( UnityEngine.Rendering.RenderPipelineManager.currentPipeline != null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null )
				return;

			GameObject warmUpObject = new GameObject( "AI_PipelineWarmUp" ) { hideFlags = HideFlags.HideAndDontSave };
			Camera warmUpCamera = warmUpObject.AddComponent<Camera>();
			warmUpCamera.enabled = false;
			warmUpCamera.cullingMask = 0;
			RenderTexture warmUpTarget = RenderTexture.GetTemporary( 4, 4, 16 );
			warmUpCamera.targetTexture = warmUpTarget;
			warmUpCamera.Render();
			warmUpCamera.targetTexture = null;
			RenderTexture.ReleaseTemporary( warmUpTarget );
			DestroyImmediate( warmUpObject );
		}

		public static void UpdateKeywords( Material material, AmplifyImpostorBakePreset preset )
		{
			foreach ( var outout in preset.Output )
			{
				if ( material.HasProperty( outout.Name ) )
				{
					if ( !outout.Active )
					{
						material.SetTexture( outout.Name, null );
					}
					material.EnsureKeywordState( outout.Name.ToUpper() + "MAP", outout.Active );
				}
			}
		}

		private void PostProcessTextures( Dictionary<string, TextureData> textureImportData )
		{
			foreach ( var pair in textureImportData )
			{
				TextureData desc = pair.Value;
				TextureImporter textureImporter = ( TextureImporter )AssetImporter.GetAtPath( pair.Key );

				var desiredSRGB = desc.SRGB;
				var desiredAlphaSource = desc.Alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
				var desiredCompression = ( TextureImporterCompression )desc.Compression;
				var desiredMaxSize = ( desc.MaxSize > -1 ) ? desc.MaxSize : textureImporter.maxTextureSize;

				if ( textureImporter.sRGBTexture != desiredSRGB ||
					 textureImporter.alphaSource != desiredAlphaSource ||
					 textureImporter.textureCompression != desiredCompression ||
					 textureImporter.maxTextureSize != desiredMaxSize )
				{
					textureImporter.sRGBTexture = desiredSRGB;
					textureImporter.alphaSource = desiredAlphaSource;
					textureImporter.textureCompression = desiredCompression;
					textureImporter.maxTextureSize = desiredMaxSize;
					textureImporter.SaveAndReimport();
				}
			}
		}

		// raised once per bake run with every impostor in it; a single bake passes an array of one.
		// OnBakeFinished splits the same impostors into the ones that baked and the ones that failed
		public static event Action<AmplifyImpostor[]> OnBakeStarted;
		public static event Action<AmplifyImpostor[], AmplifyImpostor[]> OnBakeFinished;

		// for batch bakers, which raise the events once for the whole batch instead of per impostor
		public static void RaiseBakeStarted( AmplifyImpostor[] impostors )
		{
			OnBakeStarted?.Invoke( impostors );
		}

		public static void RaiseBakeFinished( AmplifyImpostor[] baked, AmplifyImpostor[] failed )
		{
			OnBakeFinished?.Invoke( baked, failed );
		}

		public void RenderAllDeferredGroups( AmplifyImpostorAsset data = null, bool raiseEvents = true )
		{
			string folderPath = m_folderPath;
			if( m_data == null )
			{
				folderPath = this.OpenFolderForImpostor();
			}
			else
			{
				m_impostorName = m_data.name;
				folderPath = Path.GetDirectoryName( AssetDatabase.GetAssetPath( m_data ) ).Replace( "\\", "/" ) + "/";
			}
			if( string.IsNullOrEmpty( folderPath ) )
				return;

			AmplifyImpostor[] self = new AmplifyImpostor[] { this };
			AmplifyImpostor[] none = new AmplifyImpostor[ 0 ];
			if ( raiseEvents )
			{
				RaiseBakeStarted( self );
			}

			bool succeeded = false;
			try
			{
				RenderAllDeferredGroupsInternal( data, folderPath );
				succeeded = true;
			}
			finally
			{
				if ( raiseEvents )
				{
					RaiseBakeFinished( succeeded ? self : none, succeeded ? none : self );
				}
			}
		}

		private void RenderAllDeferredGroupsInternal( AmplifyImpostorAsset data, string folderPath )
		{
			DisplayProgress( 0, "Please Wait... Setting up" );
			string fileName = m_impostorName;

			if( string.IsNullOrEmpty( fileName ) )
				fileName = m_rootTransform.name + "_Impostor";

			m_folderPath = folderPath;
			folderPath = folderPath.TrimEnd( new char[] { '/', '*', '.', ' ' } );
			folderPath += "/";
			folderPath = folderPath.TrimStart( new char[] { '/', '*', '.', ' ' } );
			m_impostorName = fileName;

			Undo.RegisterCompleteObjectUndo( this, "Create Impostor" );

			EnsureRenderPipelineCreated();
			DetectRenderPipeline();

			if( m_data == null )
			{
				AmplifyImpostorAsset existingAsset = AssetDatabase.LoadAssetAtPath<AmplifyImpostorAsset>( folderPath + fileName + ".asset" );

				m_data = ScriptableObject.CreateInstance<AmplifyImpostorAsset>();
				AssetDatabase.CreateAsset( m_data, folderPath + fileName + ".asset" );

				if( data != null )
				{
					m_data.ShapePoints = data.ShapePoints;
				}
			}
			else
			if( data != null )
			{
				m_data = data;
			}
			bool chache = GL.sRGBWrite;
			GL.sRGBWrite = true;

			if( !m_data.DecoupleAxisFrames )
				m_data.HorizontalFrames = m_data.VerticalFrames;

			if( m_data.Preset == null )
			{
				m_data.Preset = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBakePreset>( AssetDatabase.GUIDToAssetPath( Preferences.Project.BakePresetGUID ) );
			}

			bool standardRendering = ( m_data.Preset.BakeShader == null );
			if ( standardRendering && m_data.Preset.Output.Count != AmplifyImpostorBakePreset.DefaultOutputCount )
			{
				Debug.LogError( "[AmplifyImpostors] Detected an error in Bake Preset " + m_data.Preset.name + ". It uses a default/null baking shader but the outputs are non-default." );
				return;
			}

			List<TextureOutput> outputList = new List<TextureOutput>();
			for( int i = 0; i < m_data.Preset.Output.Count; i++ )
				outputList.Add( m_data.Preset.Output[ i ].Clone() );

			for( int i = 0; i < m_data.OverrideOutput.Count && i < m_data.Preset.Output.Count; i++ )
			{
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.OutputToggle ) == OverrideMask.OutputToggle )
					outputList[ m_data.OverrideOutput[ i ].Index ].Active = m_data.OverrideOutput[ i ].Active;
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.NameSuffix ) == OverrideMask.NameSuffix )
					outputList[ m_data.OverrideOutput[ i ].Index ].Name = m_data.OverrideOutput[ i ].Name;
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.RelativeScale ) == OverrideMask.RelativeScale )
					outputList[ m_data.OverrideOutput[ i ].Index ].Scale = m_data.OverrideOutput[ i ].Scale;
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.ColorSpace ) == OverrideMask.ColorSpace )
					outputList[ m_data.OverrideOutput[ i ].Index ].SRGB = m_data.OverrideOutput[ i ].SRGB;
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.QualityCompression ) == OverrideMask.QualityCompression )
					outputList[ m_data.OverrideOutput[ i ].Index ].Compression = m_data.OverrideOutput[ i ].Compression;
				if( ( m_data.OverrideOutput[ i ].OverrideMask & OverrideMask.FileFormat ) == OverrideMask.FileFormat )
					outputList[ m_data.OverrideOutput[ i ].Index ].ImageFormat = m_data.OverrideOutput[ i ].ImageFormat;
			}
			m_fileNames = new string[ outputList.Count ];

			string guid = string.Empty;
			if( m_renderPipelineInUse == RenderPipelineInUse.HDRP )
				guid = m_data.ImpostorType == ImpostorType.Spherical ? ShaderHDRP : ShaderOctaHDRP;
			else if( m_renderPipelineInUse == RenderPipelineInUse.URP )
				guid = m_data.ImpostorType == ImpostorType.Spherical ? ShaderURP : ShaderOctaURP;
			else
				guid = m_data.ImpostorType == ImpostorType.Spherical ? ShaderBiRP : ShaderOctaBiRP;

			int targetAmount = standardRendering ? 4 : outputList.Count;

			CalculatePixelBounds( targetAmount );
			DisplayProgress( 0.1f, "Please Wait... Allocating Resources" );

			GenerateTextures( outputList, standardRendering );
			DisplayProgress( 0.2f, "Please Wait... Baking" );

			// TODO: remove this temporary solution
			CopyTransform();

			GetFrameInfo( m_data, out int hframes, out int vframes );
			m_cameraInvViewProjPerFrame = new Matrix4x4[ hframes * vframes ];

			try
			{
				RenderImpostor( targetAmount, RenderImpostorMode.Normal, true, m_data.Preset.BakeShader );
				PasteTransform();
			}
			finally
			{
				PasteTransform();
				if( !SuppressProgressBar )
				{
					EditorUtility.ClearProgressBar();
				}
			}

			DisplayProgress( 0.5f, "Please Wait... Remapping" );

			Shader packerShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( PackerGUID ) );
			Material packerMat = new Material( packerShader );

			Shader gbufferToOutputShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( GBufferToOutputGUID ) );
			Material gbufferToOutputMat = new Material( gbufferToOutputShader );

			int alphaIndex = m_data.Preset.AlphaIndex;
			if ( standardRendering )
			{
				gbufferToOutputMat.SetInt( "_RenderPipeline", ( int )m_renderPipelineInUse );
				gbufferToOutputMat.SetTexture( "_GBuffer0", m_rtGBuffers[ 0 ] );
				gbufferToOutputMat.SetTexture( "_GBuffer1", m_rtGBuffers[ 1 ] );
				gbufferToOutputMat.SetTexture( "_GBuffer2", m_rtGBuffers[ 2 ] );
				gbufferToOutputMat.SetTexture( "_GBuffer3", m_rtGBuffers[ 3 ] );
				gbufferToOutputMat.SetTexture( "_Depth", m_trueDepth );

				var invViewProjBuffer = new ComputeBuffer( m_cameraInvViewProjPerFrame.Length, sizeof( float ) * 16, ComputeBufferType.Structured );
				invViewProjBuffer.SetData( m_cameraInvViewProjPerFrame );

				gbufferToOutputMat.SetBuffer( "_CameraInvViewProjPerFrame", invViewProjBuffer );
				gbufferToOutputMat.SetVector( "_FrameCount", new Vector2( hframes, vframes ) );
				gbufferToOutputMat.SetFloat( "_DepthSize", m_depthFitSize );
				gbufferToOutputMat.SetVector( "_BoundsMin", m_originalBound.min );
				gbufferToOutputMat.SetVector( "_BoundsSize", m_originalBound.size );

				for ( int i = 0; i < m_outBuffers.Length; i++ )
				{
					Graphics.Blit( null, m_outBuffers[ i ], gbufferToOutputMat, i );
				}

				invViewProjBuffer.Release();

				m_trueDepth.Release();
				m_trueDepth = null;
			}

			m_cameraInvViewProjPerFrame = null;

			// TGA
			for ( int i = 0; i < outputList.Count; i++ )
			{
				if( outputList[ i ].ImageFormat == ImageFormat.TGA )
					PackingRemapping( ref m_outBuffers[ i ], ref m_outBuffers[ i ], 6, packerMat );
			}

			if( m_data.PixelPadding > 0 )
				DisplayProgress( 0.55f, "Please Wait... Dilating" );

			Shader dilateShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( DilateGUID ) );
			Material dilateMat = new Material( dilateShader );

			// Dilation
			for( int i = 0; i < outputList.Count; i++ )
			{
				if( outputList[ i ].Active )
					DilateRenderTextureUsingMask( ref m_outBuffers[ i ], ref m_outBuffers[ alphaIndex ], m_data.PixelPadding, alphaIndex != i, dilateMat );
			}

			DestroyImmediate( dilateMat );
			dilateMat = null;

			DisplayProgress( 0.575f, "Please Wait... Resizing" );

			// Resize Final Textures
			for( int i = 0; i < outputList.Count; i++ )
			{
				if( outputList[ i ].Scale != TextureScale.Full )
				{
					RenderTexture resTex = RenderTexture.GetTemporary( m_outBuffers[ i ].width / (int)outputList[ i ].Scale, m_outBuffers[ i ].height / (int)outputList[ i ].Scale, m_outBuffers[ i ].depth, m_outBuffers[ i ].graphicsFormat );
					Graphics.Blit( m_outBuffers[ i ], resTex );
					m_outBuffers[ i ].Release();
					m_outBuffers[ i ] = new RenderTexture( resTex.width, resTex.height, m_outBuffers[ i ].depth, m_outBuffers[ i ].graphicsFormat );
					m_outBuffers[ i ].Create();
					Graphics.Blit( resTex, m_outBuffers[ i ] );
					RenderTexture.ReleaseTemporary( resTex );
				}
			}

			DestroyImmediate( packerMat );
			packerMat = null;

			DestroyImmediate( gbufferToOutputMat );
			gbufferToOutputMat = null;

			DisplayProgress( 0.6f, "Please Wait... Creating Asset and Textures" );

			bool isPrefab = PrefabUtility.IsPartOfPrefabAsset( gameObject );

			// Create billboard
			Shader defaultShader = null;
			if( m_data.Preset.RuntimeShader != null )
			{
				defaultShader = m_data.Preset.RuntimeShader;
			}
			else
			{
				defaultShader = AssetDatabase.LoadAssetAtPath<Shader>( AssetDatabase.GUIDToAssetPath( guid ) );
			}
			Material material = m_data.Material;
			if( material == null )
			{
				material = new Material( defaultShader );
				material.name = fileName;
				material.enableInstancing = true;
				AssetDatabase.AddObjectToAsset( material, m_data );
				m_data.Material = material;
				EditorUtility.SetDirty( material );
			}
			else
			{
				material.shader = defaultShader;
				material.name = fileName;
				EditorUtility.SetDirty( material );
			}

			Texture2D tex = null;
			bool hasDifferentResolution = false;

			// Construct file names
			m_standardFileNamesOld[ 0 ] = "_AlbedoAlpha";
			m_standardFileNamesOld[ 1 ] = "_NormalDepth";
			m_standardFileNamesOld[ 2 ] = "_SpecularSmoothness";
			m_standardFileNamesOld[ 3 ] = "_EmissionOcclusion";

			m_standardFileNames[ 0 ] = Preferences.Project.AlbedoName;
			m_standardFileNames[ 1 ] = Preferences.Project.NormalsName;
			m_standardFileNames[ 2 ] = Preferences.Project.SpecularName;
			m_standardFileNames[ 3 ] = Preferences.Project.OcclusionName;
			m_standardFileNames[ 4 ] = Preferences.Project.EmissionName;

			for( int i = 0; i < outputList.Count; i++ )
			{
				tex = null;
				m_fileNames[ i ] = string.Empty;
				if( material.HasProperty( outputList[ i ].Name ) )
					tex = material.GetTexture( outputList[ i ].Name ) as Texture2D;
				if( tex != null )
				{
					m_fileNames[ i ] = AssetDatabase.GetAssetPath( tex );
					//m_fileNames[ i ] = Path.GetDirectoryName( AssetDatabase.GetAssetPath( tex ) ).Replace( "\\", "/" ) + "/";
					if( tex.width != (int)m_data.TexSize.x / (int)outputList[ i ].Scale )
						hasDifferentResolution = true;
				}
				else
				{
					m_fileNames[ i ] = folderPath;
					m_fileNames[ i ] += fileName + outputList[ i ].Name + "." + outputList[ i ].ImageFormat.ToString().ToLower();
				}
			}

			for( int i = 0; i < m_propertyNames.Length; i++ )
			{
				tex = null;
				if( material.HasProperty( m_propertyNames[ i ] ) )
				{
					tex = material.GetTexture( m_propertyNames[ i ] ) as Texture2D;
					if( tex != null )
					{
						int indexFound = outputList.FindIndex( x => x.Name == m_standardFileNames[ i ] );
						if( indexFound > -1 )
						{
							m_fileNames[ indexFound ] = AssetDatabase.GetAssetPath( tex );
							//m_fileNames[ indexFound ] = Path.GetDirectoryName( AssetDatabase.GetAssetPath( tex ) ).Replace( "\\", "/" ) + "/";
							//m_fileNames[ indexFound ] += fileName + outputList[ indexFound ].Name + "." + outputList[ indexFound ].ImageFormat.ToString().ToLower();

							if( tex.width != (int)m_data.TexSize.x / (int)outputList[ indexFound ].Scale )
								hasDifferentResolution = true;
						}
					}
				}
			}

			int activeCount = 0;
			int missingCount = 0;
			for ( int i = 0; i < outputList.Count; i++ )
			{
				activeCount += outputList[ i ].Active ? 1 : 0;
				missingCount += string.IsNullOrEmpty( AssetDatabase.AssetPathToGUID( m_fileNames[ i ], AssetPathToGUIDOptions.OnlyExistingAssets ) ) ? 1 : 0;
			}

			bool resizeTextures = false;
			if ( missingCount == activeCount )
			{
				// @diogo: they're all missing; always resize in this case.
				resizeTextures = true;
			}
			else
			{
				if ( hasDifferentResolution && Preferences.Project.TexImport == 0 )
					resizeTextures = EditorUtility.DisplayDialog( "Resize Textures?", "Do you wish to override the Texture Import settings to match the provided Impostor Texture Size?", "Yes", "No" );
				else if ( Preferences.Project.TexImport == 1 )
					resizeTextures = true;
				else
					resizeTextures = false;
			}

			// save to texture files
			var textureImportData = new Dictionary<string, TextureData>();
			if( !Application.isPlaying )
			{
				for( int i = 0; i < outputList.Count; i++ )
				{
					if( outputList[ i ].Active )
					{
						SaveTexture( ref m_outBuffers[ i ], m_fileNames[ i ], outputList[ i ].ImageFormat, (int)outputList[ i ].Scale, outputList[ i ].Channels );
						textureImportData.Add( m_fileNames[ i ], new TextureData( outputList[ i ].SRGB, outputList[ i ].Compression, outputList[ i ].Channels == TextureChannels.RGBA, resizeTextures ? (int)m_data.TexSize.x / (int)outputList[ i ].Scale : -1 ) );
					}
				}
			}

			GL.sRGBWrite = chache;

			GameObject impostorObject = null;
			DisplayProgress( 0.65f, "Please Wait... Generating Mesh and Material" );
			//RenderCombinedAlpha();
			Vector4 offsetCalc = /*transform.worldToLocalMatrix **/ new Vector4( m_originalBound.center.x, m_originalBound.center.y, m_originalBound.center.z, 1 );
			Vector4 offset = new Vector4( offsetCalc.x, offsetCalc.y, offsetCalc.z, -m_pixelOffset.y / m_xyFitSize/*(-pixelOffset.y / m_data.VerticalFrames) * ( m_trueFitsize / m_data.VerticalFrames )*/ );
			Vector4 sizeOffset = new Vector4( m_xyFitSize, m_depthFitSize, (m_pixelOffset.x / m_xyFitSize) / (float)m_data.HorizontalFrames, (m_pixelOffset.y / m_xyFitSize) / (float)m_data.VerticalFrames );
			//offset.y += pixelOffset.y;
			bool justCreated = false;
			UnityEngine.Object targetPrefab = null;
			GameObject tempGO = null;


			Mesh mesh = m_data.Mesh;
			if( mesh == null )
			{
				mesh = GenerateMesh( m_data.ShapePoints, offset, m_xyFitSize, m_xyFitSize, true );
				mesh.name = fileName;
				AssetDatabase.AddObjectToAsset( mesh, m_data );
				m_data.Mesh = mesh;
				EditorUtility.SetDirty( mesh );
			}
			else
			{
				Mesh tempmesh = GenerateMesh( m_data.ShapePoints, offset, m_xyFitSize, m_xyFitSize, true );
				EditorUtility.CopySerialized( tempmesh, mesh );
				mesh.vertices = tempmesh.vertices;
				mesh.triangles = tempmesh.triangles;
				mesh.uv = tempmesh.uv;
				mesh.normals = tempmesh.normals;
				mesh.bounds = tempmesh.bounds;
				mesh.name = fileName;
				EditorUtility.SetDirty( mesh );
			}

			// when the impostor component shares its GameObject with the source mesh, prefix the name (e.g. "Sphere_Impostor")
			string impostorName = ( GetComponent<MeshFilter>() != null ) ? gameObject.name + "_Impostor" : "Impostor";

			if( isPrefab )
			{
				if( m_lastImpostor != null && PrefabUtility.GetPrefabAssetType( m_lastImpostor ) == PrefabAssetType.Regular )
				{
					impostorObject = m_lastImpostor;
				}
				else
				{
					GameObject mainGO = new GameObject( impostorName, new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ) } );
					impostorObject = mainGO;
					justCreated = true;
				}
			}
			else
			{
				if( m_lastImpostor != null )
				{
					impostorObject = m_lastImpostor;
					//impostorObject.transform.position = m_rootTransform.position;
					//impostorObject.transform.rotation = m_rootTransform.rotation;
				}
				else
				{
					impostorObject = new GameObject( impostorName, new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ) } );
					Undo.RegisterCreatedObjectUndo( impostorObject, "Create Impostor" );
					impostorObject.transform.position = m_rootTransform.position;
					impostorObject.transform.rotation = m_rootTransform.rotation;

					justCreated = true;
				}
			}
			m_lastImpostor = impostorObject;
			impostorObject.transform.localScale = Vector3.one;
			impostorObject.GetComponent<MeshFilter>().sharedMesh = mesh;

			if( justCreated )
			{
				if( LodGroup != null )
				{
					if( isPrefab )
					{
						targetPrefab = PrefabUtility.GetPrefabInstanceHandle( ( Selection.activeObject as GameObject ).transform.root.gameObject );
						GameObject targetGO = AssetDatabase.LoadAssetAtPath( folderPath + ( Selection.activeObject as GameObject ).transform.root.gameObject.name + ".prefab", typeof( GameObject ) ) as GameObject;
						UnityEngine.Object inst = PrefabUtility.InstantiatePrefab( targetGO );
						tempGO = inst as GameObject;
						AmplifyImpostor ai = tempGO.GetComponentInChildren<AmplifyImpostor>();
						impostorObject.transform.SetParent( ai.LodGroup.transform );
						ai.m_lastImpostor = impostorObject;
						PrefabUtility.SaveAsPrefabAssetAndConnect( tempGO, AssetDatabase.GetAssetPath( targetPrefab ), InteractionMode.AutomatedAction );
						ai = targetGO.GetComponentInChildren<AmplifyImpostor>();
						impostorObject = ai.m_lastImpostor;
						DestroyImmediate( tempGO );
					}
					else
					{
						impostorObject.transform.SetParent( LodGroup.transform, true );
						impostorObject.transform.localScale = Vector3.one;
					}


					float objectSize = LodGroup.size;

					switch( m_lodReplacement )
					{
						default:
						case LODReplacement.DoNothing:
						break;
						case LODReplacement.ReplaceCulled:
						{
							LOD[] lods = LodGroup.GetLODs();
							Array.Resize( ref lods, lods.Length + 1 );
							LOD lastLOD = new LOD();
							lastLOD.screenRelativeTransitionHeight = 0;
							lastLOD.renderers = impostorObject.GetComponents<Renderer>();
							lods[ lods.Length - 1 ] = lastLOD;
							LodGroup.SetLODs( lods );
						}
						break;
						case LODReplacement.ReplaceLast:
						{
							LOD[] lods = LodGroup.GetLODs();

							foreach( Renderer item in lods[ lods.Length - 1 ].renderers )
								if( item )
									item.enabled = false;

							lods[ lods.Length - 1 ].renderers = impostorObject.GetComponents<Renderer>();
							LodGroup.SetLODs( lods );
						}
						break;
						case LODReplacement.ReplaceAllExceptFirst:
						{
							LOD[] lods = LodGroup.GetLODs();
							for( int i = lods.Length - 1; i > 0; i-- )
							{
								foreach( Renderer item in lods[ i ].renderers )
									if( item )
										item.enabled = false;
							}
							float lastTransition = lods[ lods.Length - 1 ].screenRelativeTransitionHeight;
							Array.Resize( ref lods, 2 );
							lods[ lods.Length - 1 ].screenRelativeTransitionHeight = lastTransition;
							lods[ lods.Length - 1 ].renderers = impostorObject.GetComponents<Renderer>();
							LodGroup.SetLODs( lods );
						}
						break;
						case LODReplacement.ReplaceSpecific:
						{
							LOD[] lods = LodGroup.GetLODs();
							foreach( Renderer item in lods[ m_insertIndex ].renderers )
								if( item )
									item.enabled = false;

							lods[ m_insertIndex ].renderers = impostorObject.GetComponents<Renderer>();
							LodGroup.SetLODs( lods );
						}
						break;
						case LODReplacement.ReplaceAfterSpecific:
						{
							LOD[] lods = LodGroup.GetLODs();
							for( int i = lods.Length - 1; i > m_insertIndex; i-- )
							{
								foreach( Renderer item in lods[ i ].renderers )
									if( item )
										item.enabled = false;
							}
							float lastTransition = lods[ lods.Length - 1 ].screenRelativeTransitionHeight;
							if( m_insertIndex == lods.Length - 1 )
								lastTransition = 0;
							Array.Resize( ref lods, 2 + m_insertIndex );
							lods[ lods.Length - 1 ].screenRelativeTransitionHeight = lastTransition;
							lods[ lods.Length - 1 ].renderers = impostorObject.GetComponents<Renderer>();
							LodGroup.SetLODs( lods );
						}
						break;
						case LODReplacement.InsertAfter:
						{
							LOD[] lods = LodGroup.GetLODs();
							Array.Resize( ref lods, lods.Length + 1 );
							for( int i = lods.Length - 1; i > m_insertIndex; i-- )
							{
								lods[ i ].screenRelativeTransitionHeight = lods[ i - 1 ].screenRelativeTransitionHeight;
								lods[ i ].fadeTransitionWidth = lods[ i - 1 ].fadeTransitionWidth;
								lods[ i ].renderers = lods[ i - 1 ].renderers;
							}

							float firstTransition = 1;
							if( m_insertIndex > 0 )
								firstTransition = lods[ m_insertIndex - 1 ].screenRelativeTransitionHeight;

							lods[ m_insertIndex + 1 ].renderers = impostorObject.GetComponents<Renderer>();
							lods[ m_insertIndex ].screenRelativeTransitionHeight = ( lods[ m_insertIndex + 1 ].screenRelativeTransitionHeight + firstTransition ) * 0.5f;
							LodGroup.SetLODs( lods );
						}
						break;
					}

					LodGroup.size = objectSize;

					Undo.RegisterCompleteObjectUndo( LodGroup, "Create Impostor" );
				}
				else if( !isPrefab )
				{
					impostorObject.transform.SetParent( m_rootTransform.parent );
					int sibIndex = m_rootTransform.GetSiblingIndex();
					impostorObject.transform.SetSiblingIndex( sibIndex + 1 );
					m_rootTransform.SetSiblingIndex( sibIndex );
					impostorObject.transform.localScale = Vector3.one;
				}
			}

			if( LodGroup == null )
			{
				Transform par = impostorObject.transform.parent;
				int sibIndex = impostorObject.transform.GetSiblingIndex();
				if ( !isPrefab )
				{
					impostorObject.transform.SetParent( m_rootTransform, true );
				}
				impostorObject.transform.localScale = Vector3.one;
				if ( !isPrefab )
				{
					impostorObject.transform.SetParent( par, true );
					impostorObject.transform.SetSiblingIndex( sibIndex );
				}
			}


			EditorUtility.SetDirty( m_data );
			if( m_lastImpostor == null )
				impostorObject.name = fileName;
			impostorObject.GetComponent<Renderer>().sharedMaterial = material;
			EditorUtility.SetDirty( impostorObject );

			DisplayProgress( 0.7f, "Please Wait... Saving and Importing" );

			// saving and refreshing to make sure textures can be set properly into the material
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			PostProcessTextures( textureImportData );

			DisplayProgress( 0.8f, "Please Wait... Changing Texture Import Settings" );

			hasDifferentResolution = false;
			tex = null;

			if ( m_copyPropertiesSource != null )
			{
				material.CopyPropertiesFrom( m_copyPropertiesSource, m_copyPropertiesMask );
			}

			if( standardRendering )
			{
				for( int i = 0; i < outputList.Count; i++ )
				{
					tex = null;
					if( outputList[ i ].Active )
					{
						if( material.HasProperty( m_propertyNames[ i ] ) )
							tex = material.GetTexture( m_propertyNames[ i ] ) as Texture2D;
						if( tex == null )
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( m_fileNames[ i ] );
						if( tex != null )
							material.SetTexture( m_propertyNames[ i ], tex );

						if( tex != null && tex.width != m_data.TexSize.x / (int)outputList[ i ].Scale )
							hasDifferentResolution = true;
					}
				}
			}
			else
			{
				for( int i = 0; i < outputList.Count; i++ )
				{
					tex = null;
					if( outputList[ i ].Active )
					{
						if( material.HasProperty( outputList[ i ].Name ) )
							tex = material.GetTexture( outputList[ i ].Name ) as Texture2D;
						if( tex == null )
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( m_fileNames[ i ] );
						if( tex != null )
							material.SetTexture( outputList[ i ].Name, tex );

						if( tex != null && tex.width != m_data.TexSize.x / (int)outputList[ i ].Scale )
							hasDifferentResolution = true;
					}
				}

				for( int i = 0; i < m_propertyNames.Length; i++ )
				{
					tex = null;
					if( material.HasProperty( m_propertyNames[ i ] ) )
					{
						tex = material.GetTexture( m_propertyNames[ i ] ) as Texture2D;
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNames[ i ] + ".tga";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNames[ i ] + ".png";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNames[ i ] + ".exr";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNamesOld[ i ] + ".tga";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNamesOld[ i ] + ".png";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex == null )
						{
							string filen = folderPath + fileName + m_standardFileNamesOld[ i ] + ".exr";
							tex = AssetDatabase.LoadAssetAtPath<Texture2D>( filen );
						}
						if ( tex != null )
							material.SetTexture( m_propertyNames[ i ], tex );

						if ( tex != null )
						{
							int indexFound = outputList.FindIndex( x => x.Name == m_standardFileNames[ i ] );
							if ( indexFound > -1 && tex.width != ( ( int )m_data.TexSize.x / ( int )outputList[ indexFound ].Scale ) )
								hasDifferentResolution = true;
						}
					}
				}
			}

			if( m_data.ImpostorType == ImpostorType.HemiOctahedron )
			{
				material.SetFloat( "_Hemi", 1 );
				material.SetFloat( "_AI_Hemi", 1 );
				material.EnableKeyword( "_HEMI_ON" );
			}
			else
			{
				material.SetFloat( "_Hemi", 0 );
				material.SetFloat( "_AI_Hemi", 0 );
				material.DisableKeyword( "_HEMI_ON" );
			}

			material.SetFloat( "_Frames", m_data.HorizontalFrames );
			material.SetFloat( "_ImpostorSize", m_xyFitSize );
			material.SetVector( "_Offset", offset );
			material.SetFloat( "_DepthSize", m_depthFitSize );
			material.SetFloat( "_FramesX", m_data.HorizontalFrames );
			material.SetFloat( "_FramesY", m_data.VerticalFrames );
			material.SetVector( "_BoundsMin", m_originalBound.min );
			material.SetVector( "_BoundsSize", m_originalBound.size );

			material.SetFloat( "_AI_Frames", m_data.HorizontalFrames );
			material.SetFloat( "_AI_ImpostorSize", m_xyFitSize );
			material.SetVector( "_AI_Offset", offset );
			material.SetVector( "_AI_SizeOffset", sizeOffset );
			material.SetFloat( "_AI_DepthSize", m_depthFitSize );
			material.SetFloat( "_AI_FramesX", m_data.HorizontalFrames );
			material.SetFloat( "_AI_FramesY", m_data.VerticalFrames );
			Vector4 angleRange = new Vector4( m_data.AngleRange.x, m_data.AngleRange.y, m_data.AngleCenter.x, m_data.AngleCenter.y );
			material.SetVector( "_AI_AngleRange", angleRange );
			material.SetVector( "_AI_BoundsMin", m_originalBound.min );
			material.SetVector( "_AI_BoundsSize", m_originalBound.size );

			UpdateKeywords( material, m_data.Preset );

			CheckHDRPMaterial();

			if( m_renderPipelineInUse == RenderPipelineInUse.HDRP )
				ValidateHDRPShaderGraphMaterial( material );

			//if( standardRendering && m_renderPipelineInUse == RenderPipelineInUse.HDRP )
			//	material.SetShaderPassEnabled( "MotionVectors", true );

			EditorUtility.SetDirty( material );

			if( hasDifferentResolution && resizeTextures )
				resizeTextures = true;
			else
				resizeTextures = false;

			DisplayProgress( 1f, "Complete!" );

			for( int i = 0; i < outputList.Count; i++ )
			{
				if( outputList[ i ].Active )
					ChangeTextureImporter( ref m_outBuffers[ i ], m_fileNames[ i ], outputList[i].SRGB, resizeTextures, outputList[ i ].Compression, outputList[ i ].Channels == TextureChannels.RGBA );
			}
			ClearBuffers();

			Data.Version = VersionInfo.FullNumber;
		}
	#endif

		private Cubemap CreateBlackCubemap()
		{
			var blackCubemap = new Cubemap( 1, TextureFormat.RGBA32, false );
			blackCubemap.name = "BlackCube";
			blackCubemap.SetPixel( CubemapFace.PositiveX, 1, 1, Color.black );
			blackCubemap.SetPixel( CubemapFace.NegativeX, 1, 1, Color.black );
			blackCubemap.SetPixel( CubemapFace.PositiveY, 1, 1, Color.black );
			blackCubemap.SetPixel( CubemapFace.NegativeY, 1, 1, Color.black );
			blackCubemap.SetPixel( CubemapFace.PositiveZ, 1, 1, Color.black );
			blackCubemap.SetPixel( CubemapFace.NegativeZ, 1, 1, Color.black );
			blackCubemap.Apply( false, true );
			return blackCubemap;
		}

		private void CopyConstantStructToArray( object constants, Vector4[] array, int stride )
		{
			// @diogo: I know this looks dirty but couldn't find another way to do it as HighDefinition.ShaderVariablesGlobal
			//         is internal. Should be safe, however. This avoids having to duplicate the struct on our side and all
			//         the different versions/sizes for different versions of HDRP, while saving us any kind of maintenance.
			GCHandle handle = GCHandle.Alloc( constants, GCHandleType.Pinned );
			try
			{
				IntPtr src = handle.AddrOfPinnedObject();
				unsafe
				{
					fixed ( Vector4* dst = array )
					{
						Buffer.MemoryCopy( ( void* )src, dst, stride, stride );
					}
				}
			}
			finally
			{
				// Always free the handle
				handle.Free();
			}
		}

		/// <summary>
		/// Renders Impostors maps to render textures
		/// </summary>
		/// <param name="impostorType"></param>
		/// <param name="impostorMaps">set to true to render all selected maps</param>
		/// <param name="combinedAlphas">set to true to render the combined alpha map which is used to generate the mesh</param>
		private void RenderImpostor( int targetAmount, RenderImpostorMode mode, bool useMinResolution = false, Shader customShader = null )
		{
			if( targetAmount <= 0 )
				return;

			bool standardRendering = customShader == null;
			Dictionary<Material, Material> bakeMats = new Dictionary<Material, Material>();

			CommandBuffer commandBuffer = new CommandBuffer();

			if ( mode == RenderImpostorMode.Normal )
			{
				commandBuffer.name = "GBufferCatcher";
				RenderTargetIdentifier[] rtIDs = new RenderTargetIdentifier[ targetAmount ];
				for( int i = 0; i < targetAmount; i++ )
				{
					rtIDs[ i ] = m_rtGBuffers[ i ];
				}
				commandBuffer.SetRenderTarget( rtIDs, m_trueDepth );
				commandBuffer.ClearRenderTarget( true, true, Color.clear, 1 );
			}

			if ( mode == RenderImpostorMode.Alpha )
			{
				commandBuffer.name = "DepthAlphaCatcher";
				RenderTargetIdentifier[] rtIDsAlpha = new RenderTargetIdentifier[ targetAmount ];
				for( int i = 0; i < targetAmount; i++ )
				{
					rtIDsAlpha[ i ] = m_alphaGBuffers[ i ];
				}
				commandBuffer.SetRenderTarget( rtIDsAlpha, m_trueDepth );
				commandBuffer.ClearRenderTarget( true, true, Color.clear, 1 );
			}

			GetFrameInfo( m_data, out int hframes, out int vframes );

			List<MeshFilter> validMeshes = new List<MeshFilter>();
			for( int i = 0; i < Renderers.Length; i++ )
			{
				// only allow for renderers that are enabled and not marked as shadow only
				if( Renderers[ i ] == null || !Renderers[ i ].enabled || Renderers[ i ].shadowCastingMode == ShadowCastingMode.ShadowsOnly )
				{
					validMeshes.Add( null );
					continue;
				}

				// skip non-meshes, for now
				MeshFilter mf = Renderers[ i ].GetComponent<MeshFilter>();
				if( mf == null || mf.sharedMesh == null )
				{
					validMeshes.Add( null );
					continue;
				}

				validMeshes.Add( mf );
			}

			int validMeshesCount = validMeshes.Count;

			Type constantsType = null;
			ComputeBuffer constantsBuffer = null;
			object constants = null;
			Vector4[] constantsArray = null;

			if ( m_renderPipelineInUse == RenderPipelineInUse.HDRP )
			{
				constantsType = Type.GetType( GlobalShaderVariablesQualifiedNameHDRP );
				if ( constantsType != null )
				{
					constantsBuffer = new ComputeBuffer( 1, Marshal.SizeOf( constantsType ), ComputeBufferType.Constant );
					constantsArray = new Vector4 [ constantsBuffer.stride / Marshal.SizeOf<Vector4>() ];
					constants = Activator.CreateInstance( constantsType, nonPublic: true );
				}
			}

			var blackCubemap = CreateBlackCubemap();

			Bounds rendererBounds = new Bounds();
			for ( int i = 0; i < validMeshesCount; i++ )
			{
				if ( validMeshes[ i ] == null )
					continue;

				if ( rendererBounds.size == Vector3.zero )
					rendererBounds = validMeshes[ i ].sharedMesh.bounds.Transform( m_rootTransform.worldToLocalMatrix * Renderers[ i ].localToWorldMatrix );
				else
					rendererBounds.Encapsulate( validMeshes[ i ].sharedMesh.bounds.Transform( m_rootTransform.worldToLocalMatrix * Renderers[ i ].localToWorldMatrix ) );
			}
			m_originalBound = rendererBounds;

			// optional bake-time signal for user shaders; ignored by shaders that don't declare them
			commandBuffer.SetGlobalFloat( "_AI_Baking", 1 );
			commandBuffer.EnableShaderKeyword( "_AI_BAKING" );

			for ( int x = 0; x < hframes; x++ )
			{
				for( int y = 0; y < vframes; y++ )
				{
					int frameIndex = y * hframes + x;

					float fitSize = m_xyFitSize * 0.5f;

					Matrix4x4 camMatrixRot = GetCameraRotationMatrix( m_data.ImpostorType, hframes, vframes, x, y, m_data.AngleRange, m_data.AngleCenter );
					Bounds frameBounds = rendererBounds.Transform( camMatrixRot );

					Matrix4x4 matrixP = Matrix4x4.Ortho( -fitSize + m_pixelOffset.x, fitSize + m_pixelOffset.x, -fitSize + m_pixelOffset.y, fitSize + m_pixelOffset.y, 0, -m_depthFitSize );
					Matrix4x4 matrixV = Matrix4x4.Inverse( camMatrixRot ) * Matrix4x4.LookAt( frameBounds.center - new Vector3( 0, 0, m_depthFitSize * 0.5f ), frameBounds.center, Vector3.up );
					matrixV = Matrix4x4.Inverse( matrixV ) * m_rootTransform.worldToLocalMatrix;

					commandBuffer.SetViewProjectionMatrices( matrixV, matrixP );

					matrixP = GL.GetGPUProjectionMatrix( matrixP, true );
					Matrix4x4 matrixVP = matrixP * matrixV;
					Matrix4x4 matrixInvVP = Matrix4x4.Inverse( matrixVP );

					if ( m_renderPipelineInUse == RenderPipelineInUse.HDRP )
					{
						if ( constants != null )
						{
							constantsType.GetField( "_ViewMatrix" ).SetValue( constants, matrixV );
							constantsType.GetField( "_CameraViewMatrix" ).SetValue( constants, matrixV );
							constantsType.GetField( "_InvViewMatrix" ).SetValue( constants, Matrix4x4.Inverse( matrixV ) );
							constantsType.GetField( "_ProjMatrix" ).SetValue( constants, matrixP );
							constantsType.GetField( "_InvProjMatrix" ).SetValue( constants, Matrix4x4.Inverse( matrixP ) );
							constantsType.GetField( "_ViewProjMatrix" ).SetValue( constants, matrixVP );
							constantsType.GetField( "_CameraViewProjMatrix" ).SetValue( constants, matrixVP );
							constantsType.GetField( "_InvViewProjMatrix" ).SetValue( constants, matrixInvVP );
							constantsType.GetField( "_ProbeExposureScale" ).SetValue( constants, 1 );
						}

						CopyConstantStructToArray( constants, constantsArray, constantsBuffer.stride );
						commandBuffer.SetBufferData( constantsBuffer, constantsArray );
						commandBuffer.SetGlobalConstantBuffer( constantsBuffer, "ShaderVariablesGlobal", 0, constantsBuffer.stride );
					}
					else if ( m_renderPipelineInUse == RenderPipelineInUse.URP )
					{
						commandBuffer.SetGlobalTexture( "_GlossyEnvironmentCubeMap", blackCubemap );
					}

					if ( mode == RenderImpostorMode.Normal && m_cameraInvViewProjPerFrame != null )
					{
						m_cameraInvViewProjPerFrame[ y * hframes + x ] = Matrix4x4.Transpose( matrixInvVP );
					}

					commandBuffer.SetGlobalVector( "unity_SHAr", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHAg", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHAb", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHBr", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHBg", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHBb", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SHC", Vector4.zero );

					commandBuffer.SetGlobalTexture( "unity_SpecCube0", blackCubemap );
					commandBuffer.SetGlobalTexture( "unity_SpecCube1", blackCubemap );

					commandBuffer.SetGlobalVector( "unity_SpecCube0_HDR", Vector4.zero );
					commandBuffer.SetGlobalVector( "unity_SpecCube1_HDR", Vector4.zero );

					commandBuffer.SetGlobalVector( "_AI_BoundsMin", m_originalBound.min );
					commandBuffer.SetGlobalVector( "_AI_BoundsSize", m_originalBound.size );

					if ( mode == RenderImpostorMode.Normal )
					{
						float targetWidth = m_data.TexSize.x / m_data.HorizontalFrames;
						float targetHeight = m_data.TexSize.y / m_data.VerticalFrames;

						commandBuffer.SetViewport( new Rect( ( m_data.TexSize.x / hframes ) * x, ( m_data.TexSize.y / vframes ) * y, targetWidth, targetHeight ) );
					}
					else if ( mode == RenderImpostorMode.Alpha )
					{
						float targetWidth = MinAlphaResolution;
						float targetHeight = MinAlphaResolution;

						commandBuffer.SetViewport( new Rect( 0, 0, targetWidth, targetHeight ) );
					}

					for ( int j = 0; j < validMeshesCount; j++ )
					{
						if( validMeshes[ j ] == null )
							continue;

						// Renderer shares array position with validMesh
						Material[] meshMaterials = Renderers[ j ].sharedMaterials;

						// Draw Mesh
						for( int k = 0; k < meshMaterials.Length; k++ )
						{
							Material renderMaterial = null;
							Mesh mesh = validMeshes[ j ].sharedMesh;
							int pass = 0;
							int prePass = 0;
							if( standardRendering )
							{
								renderMaterial = meshMaterials[ k ];

								pass = renderMaterial.FindPass( "DEFERRED" );
								if( pass == -1 )
									pass = renderMaterial.FindPass( "Deferred" );
								if( pass == -1 )
									pass = renderMaterial.FindPass( "GBuffer" );

								prePass = renderMaterial.FindPass( "DepthOnly" );

								if( pass == -1 ) // last resort fallback
								{
									pass = 0;
									for( int sp = 0; sp < renderMaterial.passCount; sp++ )
									{
										string lightmode = renderMaterial.GetTag( "LightMode", true );
										if( lightmode.Equals( "Deferred" ) )
										{
											pass = sp;
											break;
										}
									}
								}
								// Only useful for 2017.1 and 2017.2
								commandBuffer.EnableShaderKeyword( "UNITY_HDR_ON" );
							}
							else
							{
								prePass = -1;
								if( !bakeMats.TryGetValue( meshMaterials[ k ], out renderMaterial ) )
								{

									renderMaterial = new Material( customShader ) { hideFlags = HideFlags.HideAndDontSave };
									renderMaterial.CopyPropertiesFromMaterial( meshMaterials[ k ] );

									// @diogo: workaround for a Unity bug; not assigning default detail normal map
									if ( m_renderPipelineInUse == RenderPipelineInUse.URP && meshMaterials[ k ].HasProperty( _DetailNormalMap_PID ) )
									{
										if ( meshMaterials[ k ].GetTexture( _DetailNormalMap_PID ) == null )
										{
											renderMaterial.SetTexture( _DetailNormalMap_PID, Texture2D.normalTexture );
										}
									}

									bakeMats.Add( meshMaterials[ k ], renderMaterial );
								}
							}

							// Setup Lightmap keywords and values
							bool isUsingBakedGI = Renderers[ j ].lightmapIndex > -1;
							#pragma warning disable 0618 // Enlighten realtime GI is deprecated in 6000.7
							bool isUsingRealtimeGI = Renderers[ j ].realtimeLightmapIndex > -1;
							#pragma warning restore 0618
							if(( isUsingBakedGI || isUsingRealtimeGI) && !standardRendering )
							{
								commandBuffer.EnableShaderKeyword( "LIGHTMAP_ON" );
								if( isUsingBakedGI )
								{
									commandBuffer.SetGlobalVector( "unity_LightmapST", Renderers[ j ].lightmapScaleOffset );
								}

								if( isUsingRealtimeGI )
								{
									commandBuffer.EnableShaderKeyword( "DYNAMICLIGHTMAP_ON" );
									#pragma warning disable 0618 // Enlighten realtime GI is deprecated in 6000.7
									commandBuffer.SetGlobalVector( "unity_DynamicLightmapST", Renderers[ j ].realtimeLightmapScaleOffset );
									#pragma warning restore 0618
								}
								else
								{
									commandBuffer.DisableShaderKeyword( "DYNAMICLIGHTMAP_ON" );
								}

								if( isUsingBakedGI && isUsingRealtimeGI )
								{
									commandBuffer.EnableShaderKeyword( "DIRLIGHTMAP_COMBINED" );
								}
								else
								{
									commandBuffer.DisableShaderKeyword( "DIRLIGHTMAP_COMBINED" );
								}
							}
							else
							{
								commandBuffer.DisableShaderKeyword( "LIGHTMAP_ON" );
								commandBuffer.DisableShaderKeyword( "DYNAMICLIGHTMAP_ON" );
								commandBuffer.DisableShaderKeyword( "DIRLIGHTMAP_COMBINED" );
							}

							commandBuffer.DisableShaderKeyword( "LIGHTPROBE_SH" );
							commandBuffer.DisableShaderKeyword( "USING_STEREO_MATRICES" );
							commandBuffer.DisableShaderKeyword( "SHADEROPTIONS_CAMERA_RELATIVE_RENDERING" );
							commandBuffer.DisableShaderKeyword( "WRITE_DECAL_BUFFER" );

							if ( prePass > -1 )
							{
								commandBuffer.DrawRenderer( Renderers[ j ], renderMaterial, k, prePass );
							}

							commandBuffer.DrawRenderer( Renderers[ j ], renderMaterial, k, pass );
						}
					}
				}
			}

			// globals persist after execution; restore the non-baking state
			commandBuffer.SetGlobalFloat( "_AI_Baking", 0 );
			commandBuffer.DisableShaderKeyword( "_AI_BAKING" );

			Graphics.ExecuteCommandBuffer( commandBuffer );

			if ( constantsBuffer != null )
			{
				constantsBuffer.Release();
			}

			validMeshes.Clear();

			Cubemap.DestroyImmediate( blackCubemap );

			foreach ( var pair in bakeMats )
			{
				Material bakeMat = pair.Value;
				if( bakeMat != null )
				{
					if( !Application.isPlaying )
						DestroyImmediate( bakeMat );
					bakeMat = null;
				}
			}
			bakeMats.Clear();

			commandBuffer.Release();
			commandBuffer = null;
		}

		//public void Update()
		//{
		//	RenderAllDeferredGroups( m_data );
		//}

		private static Matrix4x4 GetCameraRotationMatrix( ImpostorType impostorType, int hframes, int vframes, float x, float y, Vector2 angleRange, Vector2 angleCenter )
		{
			Matrix4x4 camMatrixRot = Matrix4x4.identity;
			if( angleRange.x <= 180 ) //CAP: the hemi-octahedral map re-centred on the range centre, any type
			{
				Vector3 direction = CapFrameDirection( hframes, vframes, x, y, angleRange, angleCenter );
				camMatrixRot = Matrix4x4.Rotate( Quaternion.LookRotation( -direction, Vector3.up ) ).inverse;
			}
			else if( impostorType == ImpostorType.Spherical || angleRange.x < 360 || angleRange.y < 180 ) //SPHERICAL, or any type with a wider limited range: yaw/pitch grid
			{
				// yaw ( 0 looks from +Z, positive towards +X ): a full turn keeps frame 0 at StartXRotation and runs towards decreasing yaw, as baked before the angle limits existed; a limited range is spanned inclusively; a single column looks along SingleFrameViewDirection
				float yaw = angleCenter.x;
				if( hframes > 1 )
					yaw += ( angleRange.x >= 360 ) ? StartXRotation - ( 360.0f / hframes ) * x : -angleRange.x * 0.5f + ( angleRange.x / ( hframes - 1 ) ) * x;
				else
					yaw += Mathf.Atan2( SingleFrameViewDirection.x, SingleFrameViewDirection.z ) * Mathf.Rad2Deg;
				// pitch: positive looks from above; a single row looks from the horizon
				float pitch = angleCenter.y;
				if( vframes > 1 )
					pitch += -angleRange.y * 0.5f + ( angleRange.y / ( vframes - 1 ) ) * y;
				pitch = Mathf.Clamp( pitch, -90, 90 );
				Quaternion hRot = Quaternion.Euler( -pitch, 0, 0 );
				Quaternion vRot = Quaternion.Euler( 0, 180 - yaw, 0 ); // the rig starts on the -Z side
				camMatrixRot = Matrix4x4.Rotate( hRot * vRot );
			}
			else if( hframes == 1 && vframes == 1 ) //SINGLE FRAME
			{
				// camera facing the object directly along SingleFrameViewDirection
				Quaternion look = Quaternion.LookRotation( -SingleFrameViewDirection, Vector3.up );
				camMatrixRot = Matrix4x4.Rotate( look ).inverse;
			}
			else if( impostorType == ImpostorType.Octahedron ) //OCTAHEDRON
			{
				Vector3 forw = OctahedronToVector( Mathf.Clamp( ( hframes > 1 ) ? ( (float)( x ) / ( (float)hframes - 1 ) ) * 2f - 1f : 0f, -1f, 1f ), Mathf.Clamp( ( vframes > 1 ) ? ( (float)( y ) / ( (float)vframes - 1 ) ) * 2f - 1f : 0f, -1f, 1f ) );
				Quaternion octa = Quaternion.LookRotation( new Vector3( forw.x * -1, forw.z * -1, forw.y * -1 ), Vector3.up );
				camMatrixRot = Matrix4x4.Rotate( octa ).inverse;
			}
			else if( impostorType == ImpostorType.HemiOctahedron ) //HEMIOCTAHEDRON
			{
				Vector3 forw = HemiOctahedronToVector( Mathf.Clamp( ( hframes > 1 ) ? ( (float)( x ) / ( (float)hframes - 1 ) ) * 2f - 1f : 0f, -1f, 1f ), Mathf.Clamp( ( vframes > 1 ) ? ( (float)( y ) / ( (float)vframes - 1 ) ) * 2f - 1f : 0f, -1f, 1f ) );
				Quaternion octa = Quaternion.LookRotation( new Vector3( forw.x * -1, forw.z * -1, forw.y * -1 ), Vector3.up );
				camMatrixRot = Matrix4x4.Rotate( octa ).inverse;
			}
			return camMatrixRot;
		}

		// Object-space camera direction of cap layout grid coordinates ( x, y ), fractional allowed: the hemi-octahedral map re-centred on the range centre and scaled so the ranges reach the map edges
		private static Vector3 CapFrameDirection( int hframes, int vframes, float x, float y, Vector2 angleRange, Vector2 angleCenter )
		{
			float yaw = angleCenter.x * Mathf.Deg2Rad;
			float pitch = angleCenter.y * Mathf.Deg2Rad;
			Vector3 forward = new Vector3( Mathf.Sin( yaw ) * Mathf.Cos( pitch ), Mathf.Sin( pitch ), Mathf.Cos( yaw ) * Mathf.Cos( pitch ) );
			Vector3 right = new Vector3( Mathf.Cos( yaw ), 0, -Mathf.Sin( yaw ) );
			Vector3 up = Vector3.Cross( forward, right );
			float halfYaw = angleRange.x * 0.5f * Mathf.Deg2Rad;
			float halfPitch = angleRange.y * 0.5f * Mathf.Deg2Rad;
			const float sqrt2 = 1.41421356f;
			Vector2 scale = new Vector2( sqrt2 * Mathf.Sin( halfYaw ) / ( Mathf.Cos( halfYaw ) + sqrt2 * Mathf.Sin( halfYaw ) ), sqrt2 * Mathf.Sin( halfPitch ) / ( Mathf.Cos( halfPitch ) + sqrt2 * Mathf.Sin( halfPitch ) ) );
			float u = ( hframes > 1 ) ? ( x / ( hframes - 1 ) ) * 2f - 1f : 0f;
			float v = ( vframes > 1 ) ? ( y / ( vframes - 1 ) ) * 2f - 1f : 0f;
			Vector3 octa = HemiOctahedronToVector( Mathf.Clamp( u * scale.x, -1f, 1f ), Mathf.Clamp( v * scale.y, -1f, 1f ) );
			return ( octa.x * ( right + up ) + octa.y * ( right - up ) ) * 0.70710678f + octa.z * forward; // the hemi-octahedral map axes run along the diagonals of right and up
		}

		private static Vector3 OctahedronToVector( Vector2 oct )
		{
			Vector3 N = new Vector3( oct.x, oct.y, 1.0f - Mathf.Abs( oct.x ) - Mathf.Abs( oct.y ) );
			float t = Mathf.Clamp01( -N.z );
			N.Set( N.x + ( N.x >= 0.0f ? -t : t ), N.y + ( N.y >= 0.0f ? -t : t ), N.z );
			N = Vector3.Normalize( N );
			return N;
		}

		private static Vector3 OctahedronToVector( float x, float y )
		{
			Vector3 N = new Vector3( x, y, 1.0f - Mathf.Abs( x ) - Mathf.Abs( y ) );
			float t = Mathf.Clamp01( -N.z );
			N.Set( N.x + ( N.x >= 0.0f ? -t : t ), N.y + ( N.y >= 0.0f ? -t : t ), N.z );
			N = Vector3.Normalize( N );
			return N;
		}

		private static Vector3 HemiOctahedronToVector( float x, float y )
		{
			float tempx = x;
			float tempy = y;

			x = ( tempx + tempy ) * 0.5f;
			y = ( tempx - tempy ) * 0.5f;
			Vector3 N = new Vector3( x, y, 1.0f - Mathf.Abs( x ) - Mathf.Abs( y ) );
			N = Vector3.Normalize( N );
			return N;
		}

		public void GenerateAutomaticMesh( AmplifyImpostorAsset data )
		{
			// create a 2d texture for calculations
			Rect testRect = new Rect( 0, 0, m_alphaTex.width, m_alphaTex.height );
			Vector2[][] paths;
			SpriteUtilityEx.GenerateOutline( m_alphaTex, testRect, data.Tolerance, 254, false, out paths );
			int sum = 0;
			for( int i = 0; i < paths.Length; i++ )
			{
				sum += paths[ i ].Length;
			}

			data.ShapePoints = new Vector2[ sum ];
			int index = 0;
			for( int i = 0; i < paths.Length; i++ )
			{
				for( int j = 0; j < paths[ i ].Length; j++ )
				{
					data.ShapePoints[ index ] = (Vector2)( paths[ i ][ j ] ) + ( new Vector2( m_alphaTex.width * 0.5f, m_alphaTex.height * 0.5f ) );
					data.ShapePoints[ index ] = Vector2.Scale( data.ShapePoints[ index ], new Vector2( 1.0f / m_alphaTex.width, 1.0f / m_alphaTex.height ) );
					index++;
				}
			}

			// make it convex hull
			data.ShapePoints = Vector2Ex.ConvexHull( data.ShapePoints );

			// reduce vertices
			data.ShapePoints = Vector2Ex.ReduceVertices( data.ShapePoints, data.MaxVertices );

			// Resize the mesh using calculated normals
			data.ShapePoints = Vector2Ex.ScaleAlongNormals( data.ShapePoints, data.NormalScale );

			// clamp to box (needs a cut algorithm)
			for( int i = 0; i < data.ShapePoints.Length; i++ )
			{
				data.ShapePoints[ i ].x = Mathf.Clamp01( data.ShapePoints[ i ].x );
				data.ShapePoints[ i ].y = Mathf.Clamp01( data.ShapePoints[ i ].y );
			}

			// make it convex hull gain to clean edges
			data.ShapePoints = Vector2Ex.ConvexHull( data.ShapePoints );

			// invert Y
			for( int i = 0; i < data.ShapePoints.Length; i++ )
			{
				data.ShapePoints[ i ] = new Vector2( data.ShapePoints[ i ].x, 1 - data.ShapePoints[ i ].y );
			}
		}

		public Mesh GenerateMesh( Vector2[] points, Vector3 offset, float width = 1, float height = 1, bool invertY = true )
		{
			Vector2[] newPoints = new Vector2[ points.Length ];
			Vector2[] UVs = new Vector2[ points.Length ];
			Array.Copy( points, newPoints, points.Length );
			float halfWidth = width * 0.5f;
			float halfHeight = height * 0.5f;

			if( invertY )
			{
				for( int i = 0; i < newPoints.Length; i++ )
				{
					newPoints[ i ] = new Vector2( newPoints[ i ].x, 1 - newPoints[ i ].y );
				}
			}

			Array.Copy( newPoints, UVs, newPoints.Length );

			for( int i = 0; i < newPoints.Length; i++ )
			{
				newPoints[ i ] = new Vector2( newPoints[ i ].x * width - halfWidth + m_pixelOffset.x, newPoints[ i ].y * height - halfHeight + m_pixelOffset.y );
			}

			Triangulator tr = new Triangulator( newPoints );
			int[] indices = tr.Triangulate();

			Vector3[] vertices = new Vector3[ tr.Points.Count ];
			for( int i = 0; i < vertices.Length; i++ )
			{
				vertices[ i ] = new Vector3( tr.Points[ i ].x, tr.Points[ i ].y, 0 );
			}

			//Vector4[] tangents = new Vector4[ tr.Points.Count ];
			//for( int i = 0; i < vertices.Length; i++ )
			//{
			//	tangents[ i ] = new Vector4( 1, 0, 0, 1 );
			//}

			Mesh mesh = new Mesh();
			mesh.vertices = vertices;
			mesh.uv = UVs;
			//mesh.tangents = tangents;

			mesh.triangles = indices;
			mesh.RecalculateNormals();
			mesh.bounds = new Bounds( offset, m_originalBound.size );

			return mesh;
		}
	}
}
