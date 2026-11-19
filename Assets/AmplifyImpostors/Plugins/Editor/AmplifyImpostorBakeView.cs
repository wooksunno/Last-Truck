// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;

namespace AmplifyImpostors
{
	// shared impostor-row rendering used by both the Batch Baker tool and the Batch Set Manager:
	// a lit/alpha-tested preview, the estimated memory and the editable Type/Size/Frames controls.
	[NoAutoStaticsCleanup]
	public class AmplifyImpostorBakeView
	{
		public const float RowHeight = 74;
		public const float PreviewSize = 60;
		private const int PreviewRenderSize = 128;
		private const int MaxAxisFrames = 64;

		private static readonly int[] SizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
		private static readonly GUIContent[] SizeOptionsStr = { new GUIContent( "32" ), new GUIContent( "64" ), new GUIContent( "128" ), new GUIContent( "256" ), new GUIContent( "512" ), new GUIContent( "1024" ), new GUIContent( "2048" ), new GUIContent( "4096" ), new GUIContent( "8192" ) };

		private static readonly GUIContent TypeStr = new GUIContent( "Type", "Technique used for both baking and rendering the impostor" );
		private static readonly GUIContent SizeStr = new GUIContent( "Size", "Texture size in pixels for the baked images" );
		private static readonly GUIContent FramesStr = new GUIContent( "Frames", "Amount of frames captured per axis" );
		private static readonly GUIContent AutoLodStr = new GUIContent( "LOD Group", "Auto-create a LOD Group on bake when none exists, adding the impostor as LOD1" );
		private static readonly GUIContent LodTransitionStr = new GUIContent( "Transition", "Screen relative height at which LOD0 is replaced by the impostor" );

		// every live view registers itself so a finished bake can refresh all open previews at once
		private static readonly List<AmplifyImpostorBakeView> s_views = new List<AmplifyImpostorBakeView>();

		// invoked to repaint the host (EditorWindow.Repaint or Editor.Repaint) so this view works from
		// both editor windows and custom inspectors
		private readonly Action m_repaint;

		// AssetPreview can't render the custom impostor shader properly, so we render the baked
		// mesh+material ourselves (lit, alpha tested) and cache the result per asset
		private PreviewRenderUtility m_previewUtility = null;
		private readonly Dictionary<AmplifyImpostorAsset, Texture2D> m_previewCache = new Dictionary<AmplifyImpostorAsset, Texture2D>();

		private GUIStyle m_nameStyle = null;
		private GUIStyle m_memoryStyle = null;

		public AmplifyImpostorBakeView( Action repaint )
		{
			m_repaint = repaint;
			s_views.Add( this );
		}

		public void Cleanup()
		{
			if ( m_previewUtility != null )
			{
				m_previewUtility.Cleanup();
				m_previewUtility = null;
			}
			ClearPreviewCache();
			s_views.Remove( this );
		}

		private void EnsureStyles()
		{
			if ( m_nameStyle == null )
			{
				m_nameStyle = new GUIStyle( EditorStyles.boldLabel );
				m_nameStyle.wordWrap = false;
				m_nameStyle.clipping = TextClipping.Clip;
			}

			if ( m_memoryStyle == null )
			{
				m_memoryStyle = new GUIStyle( EditorStyles.miniLabel );
				m_memoryStyle.alignment = TextAnchor.MiddleRight;
			}
		}

		// draws one impostor row (enable toggle, preview, info and editable settings). Returns true when
		// the row's remove button was pressed. When allowEditMembership is false the enable toggle and remove
		// button are disabled (used by the read-only "all included impostors" view).
		public bool DrawRow( GameObject target, ref bool enabled, bool autoCreate, bool allowEditMembership )
		{
			EnsureStyles();

			bool remove = false;

			Rect rect = EditorGUILayout.GetControlRect( false, RowHeight );
			GUI.Box( rect, GUIContent.none );

			Rect toggleRect = new Rect( rect.x + 4, rect.y + RowHeight * 0.5f - 8, 16, 16 );
			if ( allowEditMembership )
			{
				enabled = EditorGUI.Toggle( toggleRect, enabled );
			}
			else
			{
				using ( new EditorGUI.DisabledScope( true ) )
				{
					EditorGUI.Toggle( toggleRect, enabled );
				}
			}

			Rect previewRect = new Rect( rect.x + 24, rect.y + ( RowHeight - PreviewSize ) * 0.5f, PreviewSize, PreviewSize );

			AmplifyImpostor impostor = ( target != null ) ? ResolveImpostor( target ) : null;
			AmplifyImpostorAsset data = ( impostor != null ) ? impostor.Data : null;

			// prefer a proper render of the baked impostor (lit, alpha tested); fall back to the asset preview
			Texture preview = ( data != null ) ? GetImpostorPreview( data ) : null;
			if ( preview == null && target != null )
			{
				preview = AssetPreview.GetAssetPreview( target );
			}

			if ( preview != null )
			{
				GUI.DrawTexture( previewRect, preview, ScaleMode.ScaleToFit );
			}
			else
			{
				GUI.Box( previewRect, EditorGUIUtility.IconContent( "Prefab Icon" ) );
			}

			Rect removeRect = new Rect( rect.xMax - 24, rect.y + 4, 20, 20 );
			if ( allowEditMembership && GUI.Button( removeRect, EditorGUIUtility.IconContent( "Toolbar Minus", "|Remove from list" ) ) )
			{
				remove = true;
			}

			float contentX = previewRect.xMax + 8;
			float contentRight = rect.xMax - 8;

			if ( target == null )
			{
				GUI.Label( new Rect( contentX, rect.y + 6, contentRight - contentX, 18 ), "Missing object", m_nameStyle );
				return remove;
			}

			// line 1: name (left) + estimated memory (right)
			Rect memoryRect = new Rect( removeRect.x - 92, rect.y + 6, 88, 16 );
			Rect nameRect = new Rect( contentX, rect.y + 6, memoryRect.x - contentX - 6, 18 );
			GUI.Label( nameRect, target.name, m_nameStyle );

			if ( impostor == null )
			{
				GUI.Label( new Rect( contentX, rect.y + 28, contentRight - contentX, 16 ), "No Amplify Impostor component", EditorStyles.miniLabel );
				return remove;
			}

			if ( data == null )
			{
				if ( autoCreate )
				{
					GUI.Label( new Rect( contentX, rect.y + 28, contentRight - contentX, 16 ), "No Impostor Asset - one will be created on bake (defaults)", EditorStyles.miniLabel );
				}
				else
				{
					GUI.color = new Color( 1f, 0.75f, 0.4f );
					GUI.Label( new Rect( contentX, rect.y + 28, contentRight - contentX, 16 ), "Not ready: assign an Impostor Asset (or enable Auto-create)", EditorStyles.miniLabel );
					GUI.color = Color.white;
				}
				return remove;
			}

			string warning = GetWarning( impostor );
			if ( warning != null )
			{
				Rect warnRect = new Rect( contentX, rect.y + 48, contentRight - contentX, 16 );
				GUI.color = new Color( 1f, 0.75f, 0.4f );
				GUI.Label( warnRect, new GUIContent( " " + warning, EditorGUIUtility.IconContent( "console.warnicon.sml" ).image ), EditorStyles.miniLabel );
				GUI.color = Color.white;
			}

			GUI.Label( memoryRect, "~" + EstimateMemoryMB( data ).ToString( "0.0" ) + " MB", m_memoryStyle );

			// line 2: editable Type / Size / Frames + LOD Group options (fixed widths, left aligned)
			float y = rect.y + 30;
			float cacheLabel = EditorGUIUtility.labelWidth;
			const float colSpacing = 8;

			EditorGUI.BeginChangeCheck();

			EditorGUIUtility.labelWidth = 44;
			Rect typeRect = new Rect( contentX, y, 170, 18 );
			ImpostorType newType = ( ImpostorType )EditorGUI.EnumPopup( typeRect, TypeStr, data.ImpostorType );

			EditorGUIUtility.labelWidth = 32;
			Rect sizeRect = new Rect( typeRect.xMax + colSpacing, y, 110, 18 );
			int newSize = EditorGUI.IntPopup( sizeRect, SizeStr, ( int )data.TexSize.x, SizeOptionsStr, SizeOptions );

			EditorGUIUtility.labelWidth = 48;
			Rect framesRect = new Rect( sizeRect.xMax + colSpacing, y, 100, 18 );
			int newFrames = Mathf.Clamp( EditorGUI.IntField( framesRect, FramesStr, data.HorizontalFrames ), 1, MaxAxisFrames );

			if ( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( data, "Edit Impostor Settings" );
				data.ImpostorType = newType;
				data.SelectedSize = newSize;
				data.TexSize = new Vector2( newSize, newSize );
				data.HorizontalFrames = newFrames;
				if ( !data.DecoupleAxisFrames )
				{
					data.VerticalFrames = newFrames;
				}
				EditorUtility.SetDirty( data );
			}

			EditorGUIUtility.labelWidth = cacheLabel;

			// editable LOD Group options (live on the impostor component), continuing the same line
			DrawLodOptions( impostor, framesRect.xMax + colSpacing, y, contentRight );

			return remove;
		}

		// draws the per-impostor LOD Group controls (auto-create mode + transition) on a single row line.
		// the transition is disabled when the current mode won't create a LOD Group, mirroring the inspector
		private static void DrawLodOptions( AmplifyImpostor impostor, float x, float y, float right )
		{
			float cacheLabel = EditorGUIUtility.labelWidth;
			const float colSpacing = 8;

			EditorGUI.BeginChangeCheck();

			EditorGUIUtility.labelWidth = 70;
			Rect lodRect = new Rect( x, y, 180, 18 );
			LODGroupAutoCreate newMode = ( LODGroupAutoCreate )EditorGUI.EnumPopup( lodRect, AutoLodStr, impostor.m_autoCreateLodGroup );

			bool willCreate = newMode == LODGroupAutoCreate.Create
				|| ( newMode == LODGroupAutoCreate.UseGlobal && Preferences.Project.CreateLodGroup );

			float transX = lodRect.xMax + colSpacing;
			Rect transRect = new Rect( transX, y, Mathf.Max( 140, right - transX ), 18 );
			float newTransition;
			using ( new EditorGUI.DisabledScope( !willCreate ) )
			{
				newTransition = EditorGUI.Slider( transRect, LodTransitionStr, impostor.m_lodTransition, 0f, 1f );
			}

			if ( EditorGUI.EndChangeCheck() )
			{
				Undo.RecordObject( impostor, "Edit Impostor LOD Settings" );
				impostor.m_autoCreateLodGroup = newMode;
				impostor.m_lodTransition = newTransition;
				EditorUtility.SetDirty( impostor );
			}

			EditorGUIUtility.labelWidth = cacheLabel;
		}

		// returns a cached render of the baked impostor, rendering it on demand during Repaint
		private Texture GetImpostorPreview( AmplifyImpostorAsset data )
		{
			if ( data == null || data.Mesh == null || data.Material == null )
			{
				return null;
			}

			if ( m_previewCache.TryGetValue( data, out Texture2D cached ) )
			{
				return cached;
			}

			// PreviewRenderUtility must render during a Repaint event
			if ( Event.current != null && Event.current.type != EventType.Repaint )
			{
				return null;
			}

			Texture2D rendered = null;
			try
			{
				rendered = RenderImpostorPreview( data, PreviewRenderSize );
			}
			catch ( Exception )
			{
				rendered = null;
			}

			m_previewCache[ data ] = rendered;
			return rendered;
		}

		private Texture2D RenderImpostorPreview( AmplifyImpostorAsset data, int size )
		{
			if ( m_previewUtility == null )
			{
				m_previewUtility = new PreviewRenderUtility();
			}

			PreviewRenderUtility pru = m_previewUtility;

			Bounds bounds = data.Mesh.bounds;
			float radius = Mathf.Max( 0.0001f, bounds.extents.magnitude );

			pru.BeginStaticPreview( new Rect( 0, 0, size, size ) );

			// angled 3/4 view, matching the look of Unity's default asset preview
			Camera cam = pru.camera;
			cam.transform.rotation = Quaternion.Euler( 20f, -30f, 0f );
			cam.transform.position = bounds.center - cam.transform.forward * radius * 4.0f;
			cam.fieldOfView = 30f;
			cam.nearClipPlane = 0.01f;
			cam.farClipPlane = radius * 50f;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color( 0.22f, 0.22f, 0.22f, 0f );

			// soft key light coming from an upper angle, with a dim opposite fill
			pru.lights[ 0 ].intensity = 0.9f;
			pru.lights[ 0 ].transform.rotation = Quaternion.Euler( 35f, 40f, 0f );
			pru.lights[ 1 ].intensity = 0.3f;
			pru.lights[ 1 ].transform.rotation = Quaternion.Euler( 20f, -130f, 0f );
			pru.ambientColor = new Color( 0.27f, 0.27f, 0.27f, 1f );

			pru.DrawMesh( data.Mesh, Matrix4x4.identity, data.Material, 0 );
			cam.Render();

			return ( Texture2D )pru.EndStaticPreview();
		}

		public void ClearPreviewCache()
		{
			foreach ( Texture2D tex in m_previewCache.Values )
			{
				if ( tex != null )
				{
					UnityEngine.Object.DestroyImmediate( tex );
				}
			}
			m_previewCache.Clear();
		}

		// clears every open view's preview cache and repaints its host (used after a bake)
		public static void RefreshAllPreviews()
		{
			for ( int i = 0; i < s_views.Count; i++ )
			{
				s_views[ i ].ClearPreviewCache();
				if ( s_views[ i ].m_repaint != null )
				{
					s_views[ i ].m_repaint();
				}
			}
		}

		// the Amplify Impostors icon (Icon64.png), shared with the Start Screen
		private const string IconGUID = "e6bfb7c560b24a749aaa8a04126e3028";

		// builds a window tab title carrying the Amplify Impostors icon, so the tools read as part of the suite
		public static GUIContent MakeWindowTitle( string title )
		{
			Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>( AssetDatabase.GUIDToAssetPath( IconGUID ) );
			return new GUIContent( title, icon, "Amplify Impostors - " + title );
		}

		public static AmplifyImpostor ResolveImpostor( GameObject go )
		{
			if ( go == null )
			{
				return null;
			}
			return go.GetComponentInChildren<AmplifyImpostor>( true );
		}

		public static bool IsReady( AmplifyImpostor impostor, bool autoCreate )
		{
			return impostor != null && ( impostor.Data != null || autoCreate );
		}

		// appends a set's enabled, ready, not-yet-listed impostors to the list (deduplicated)
		public static void GatherReady( AmplifyImpostorBatchSet set, bool autoCreate, List<GameObject> list )
		{
			if ( set == null )
			{
				return;
			}

			for ( int i = 0; i < set.Targets.Count; i++ )
			{
				GameObject target = set.Targets[ i ];
				bool enabled = ( i < set.Enabled.Count ) ? set.Enabled[ i ] : true;
				if ( !enabled || target == null || list.Contains( target ) )
				{
					continue;
				}
				if ( IsReady( ResolveImpostor( target ), autoCreate ) )
				{
					list.Add( target );
				}
			}
		}

		// runtime VRAM estimate (MB) for the impostor's baked textures, taking resolution, channels
		// and the texture format Unity would pick for the active build target into account
		public static float EstimateMemoryMB( AmplifyImpostorAsset data )
		{
			if ( data == null || data.Preset == null )
			{
				return 0f;
			}

			List<TextureOutput> outputs = BuildEffectiveOutputs( data );

			long totalBytes = 0;
			for ( int i = 0; i < outputs.Count; i++ )
			{
				TextureOutput o = outputs[ i ];
				if ( !o.Active )
				{
					continue;
				}

				int scale = Mathf.Max( 1, ( int )o.Scale );
				int width = Mathf.Max( 1, ( int )data.TexSize.x / scale );
				int height = Mathf.Max( 1, ( int )data.TexSize.y / scale );

				bool compressed = o.Compression != TextureCompression.None;
				bool hdr = o.ImageFormat == ImageFormat.EXR;
				bool hasAlpha = o.Channels == TextureChannels.RGBA;

				float bytesPerPixel = BytesPerPixel( hdr, compressed, hasAlpha, o.SRGB );

				// +1/3 for the full mip chain
				totalBytes += ( long )( width * height * bytesPerPixel * ( 4f / 3f ) );
			}

			return totalBytes / ( 1024f * 1024f );
		}

		// resolves the texture format Unity would assign for the active build target and returns its bytes-per-pixel
		private static float BytesPerPixel( bool hdr, bool compressed, bool hasAlpha, bool sRGB )
		{
			GraphicsFormat format = SelectGraphicsFormat( hdr, compressed, hasAlpha, sRGB );
			try
			{
				uint blockBytes = GraphicsFormatUtility.GetBlockSize( format );
				uint blockWidth = GraphicsFormatUtility.GetBlockWidth( format );
				uint blockHeight = GraphicsFormatUtility.GetBlockHeight( format );
				if ( blockWidth == 0 || blockHeight == 0 )
				{
					return hasAlpha ? 4f : 3f;
				}
				return ( float )blockBytes / ( blockWidth * blockHeight );
			}
			catch ( Exception )
			{
				return hasAlpha ? 4f : 3f;
			}
		}

		private static GraphicsFormat SelectGraphicsFormat( bool hdr, bool compressed, bool hasAlpha, bool sRGB )
		{
			bool mobile = IsMobilePlatform();

			if ( hdr )
			{
				if ( !compressed )
				{
					return GraphicsFormat.R16G16B16A16_SFloat;
				}
				return mobile ? GraphicsFormat.RGBA_ASTC6X6_UFloat : GraphicsFormat.RGB_BC6H_SFloat;
			}

			if ( compressed )
			{
				if ( mobile )
				{
					return sRGB ? GraphicsFormat.RGBA_ASTC6X6_SRGB : GraphicsFormat.RGBA_ASTC6X6_UNorm;
				}
				if ( hasAlpha )
				{
					return sRGB ? GraphicsFormat.RGBA_DXT5_SRGB : GraphicsFormat.RGBA_DXT5_UNorm;
				}
				return sRGB ? GraphicsFormat.RGBA_DXT1_SRGB : GraphicsFormat.RGBA_DXT1_UNorm;
			}

			if ( hasAlpha )
			{
				return sRGB ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			}
			return sRGB ? GraphicsFormat.R8G8B8_SRGB : GraphicsFormat.R8G8B8_UNorm;
		}

		private static bool IsMobilePlatform()
		{
			BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
			return target == BuildTarget.Android || target == BuildTarget.iOS;
		}

		// returns a per-row warning for conditions that produce bad bakes, or null when fine
		public static string GetWarning( AmplifyImpostor impostor )
		{
			if ( impostor == null )
			{
				return null;
			}

			Transform t = impostor.RootTransform != null ? impostor.RootTransform : impostor.transform;
			Vector3 scale = t.lossyScale;
			if ( Mathf.Abs( scale.x - scale.y ) > 0.001f || Mathf.Abs( scale.y - scale.z ) > 0.001f )
			{
				return "Non-uniform scale - impostor may render incorrectly";
			}

			Renderer[] renderers = impostor.Renderers;
			bool hasRenderer = false;
			if ( renderers != null )
			{
				for ( int i = 0; i < renderers.Length; i++ )
				{
					if ( renderers[ i ] != null )
					{
						hasRenderer = true;
						break;
					}
				}
			}
			// when the array is empty the bake auto-fills from children, so only warn if there are truly none
			if ( !hasRenderer && t.GetComponentInChildren<Renderer>() == null )
			{
				return "No renderers found";
			}

			return null;
		}

		// merges the preset outputs with the per-impostor overrides, mirroring RenderAllDeferredGroups
		private static List<TextureOutput> BuildEffectiveOutputs( AmplifyImpostorAsset data )
		{
			List<TextureOutput> outputs = new List<TextureOutput>();
			for ( int i = 0; i < data.Preset.Output.Count; i++ )
				outputs.Add( data.Preset.Output[ i ].Clone() );

			for ( int i = 0; i < data.OverrideOutput.Count; i++ )
			{
				TextureOutput ov = data.OverrideOutput[ i ];
				if ( ov.Index < 0 || ov.Index >= outputs.Count )
				{
					continue;
				}

				if ( ( ov.OverrideMask & OverrideMask.OutputToggle ) == OverrideMask.OutputToggle )
				{
					outputs[ ov.Index ].Active = ov.Active;
				}
				if ( ( ov.OverrideMask & OverrideMask.RelativeScale ) == OverrideMask.RelativeScale )
				{
					outputs[ ov.Index ].Scale = ov.Scale;
				}
				if ( ( ov.OverrideMask & OverrideMask.ColorSpace ) == OverrideMask.ColorSpace )
				{
					outputs[ ov.Index ].SRGB = ov.SRGB;
				}
				if ( ( ov.OverrideMask & OverrideMask.QualityCompression ) == OverrideMask.QualityCompression )
				{
					outputs[ ov.Index ].Compression = ov.Compression;
				}
				if ( ( ov.OverrideMask & OverrideMask.FileFormat ) == OverrideMask.FileFormat )
				{
					outputs[ ov.Index ].ImageFormat = ov.ImageFormat;
				}
			}

			return outputs;
		}

		// bakes a list of targets sequentially; shared by the Batch Baker tool, the Batch Set Manager and the set inspector
		public static void BakeTargets( List<GameObject> targets, bool autoCreate, string autoCreateFolder )
		{
			if ( EditorApplication.isPlayingOrWillChangePlaymode )
			{
				return;
			}

			if ( targets == null || targets.Count == 0 )
			{
				return;
			}

			int succeeded = 0;
			List<string> failures = new List<string>();
			List<GameObject> bakedTargets = new List<GameObject>();

			// the bake events are raised once for the whole batch; prefab targets are baked on temporary
			// prefab contents, so the arrays hold the persistent components resolved from the targets
			AmplifyImpostor[] impostors = ResolveImpostors( targets );
			AmplifyImpostor.RaiseBakeStarted( impostors );

			AmplifyImpostor.SuppressProgressBar = true;     // our batch shows its own progress bar
			try
			{
				// NOTE: the bake imports its textures mid-process, so we must NOT suppress asset
				// refreshes here (e.g. DisallowAutoRefresh) or freshly baked textures fail to load
				// onto the material and the impostor renders white.
				for ( int i = 0; i < targets.Count; i++ )
				{
					GameObject target = targets[ i ];
					if ( target == null )
					{
						continue;
					}

					if ( EditorUtility.DisplayCancelableProgressBar( "Batch Baking Impostors", "Baking " + target.name + " (" + ( i + 1 ) + "/" + targets.Count + ")", ( float )i / targets.Count ) )
					{
						break;
					}

					string error;
					if ( BakeEntry( target, autoCreate, autoCreateFolder, out error ) )
					{
						succeeded++;
						bakedTargets.Add( target );
					}
					else
					{
						failures.Add( target.name + ": " + error );
					}
				}
			}
			finally
			{
				AmplifyImpostor.SuppressProgressBar = false;
				EditorUtility.ClearProgressBar();
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				RefreshAllPreviews();

				// everything that was started but not baked (failed or cancelled) is reported as failed
				AmplifyImpostor[] baked = ResolveImpostors( bakedTargets );
				List<AmplifyImpostor> failed = new List<AmplifyImpostor>( impostors );
				for ( int i = 0; i < baked.Length; i++ )
				{
					failed.Remove( baked[ i ] );
				}
				AmplifyImpostor.RaiseBakeFinished( baked, failed.ToArray() );
			}

			if ( failures.Count > 0 )
			{
				Debug.LogWarning( "[AmplifyImpostors] Batch bake finished with errors. Baked " + succeeded + ", failed " + failures.Count + ":\n" + string.Join( "\n", failures.ToArray() ) );
			}
			else
			{
				Debug.Log( "[AmplifyImpostors] Batch bake complete. Baked " + succeeded + " impostor" + ( succeeded == 1 ? "" : "s" ) + "." );
			}
		}

		// bakes a single target; prefab assets are baked in isolation (no scene involved), scene objects in place
		private static bool BakeEntry( GameObject target, bool autoCreate, string autoCreateFolder, out string error )
		{
			error = string.Empty;

			if ( EditorUtility.IsPersistent( target ) )
			{
				// prefab asset: edit the prefab contents directly, then save back
				string assetPath = AssetDatabase.GetAssetPath( target );
				GameObject root = PrefabUtility.LoadPrefabContents( assetPath );
				try
				{
					AmplifyImpostor impostor = ResolveImpostor( root );
					if ( !PrepareAndBake( impostor, target, autoCreate, autoCreateFolder, out error ) )
					{
						return false;
					}

					PrefabUtility.SaveAsPrefabAsset( root, assetPath );
					return true;
				}
				catch ( Exception e )
				{
					error = e.Message;
					return false;
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents( root );
				}
			}
			else
			{
				// scene object: bake in place
				try
				{
					return PrepareAndBake( ResolveImpostor( target ), target, autoCreate, autoCreateFolder, out error );
				}
				catch ( Exception e )
				{
					error = e.Message;
					return false;
				}
			}
		}

		private static bool PrepareAndBake( AmplifyImpostor impostor, GameObject originalTarget, bool autoCreate, string autoCreateFolder, out string error )
		{
			error = string.Empty;

			if ( impostor == null )
			{
				error = "no Amplify Impostor component";
				return false;
			}

			if ( impostor.RootTransform == null )
			{
				impostor.RootTransform = impostor.transform;
			}

			if ( impostor.Renderers == null || impostor.Renderers.Length == 0 )
			{
				impostor.Renderers = impostor.RootTransform.GetComponentsInChildren<Renderer>();
			}

			if ( impostor.Data == null )
			{
				if ( !autoCreate )
				{
					error = "no Impostor Asset assigned";
					return false;
				}

				AmplifyImpostorAsset created = CreateImpostorAsset( originalTarget, impostor, autoCreateFolder );
				if ( created == null )
				{
					error = "could not create Impostor Asset";
					return false;
				}
				impostor.Data = created;
			}

			impostor.RenderAllDeferredGroups( impostor.Data, raiseEvents: false );
			return true;
		}

		private static AmplifyImpostor[] ResolveImpostors( List<GameObject> targets )
		{
			List<AmplifyImpostor> impostors = new List<AmplifyImpostor>();
			for ( int i = 0; i < targets.Count; i++ )
			{
				AmplifyImpostor impostor = ResolveImpostor( targets[ i ] );
				if ( impostor != null )
				{
					impostors.Add( impostor );
				}
			}
			return impostors.ToArray();
		}

		// creates a fresh Impostor Asset for a target that doesn't have one yet
		private static AmplifyImpostorAsset CreateImpostorAsset( GameObject target, AmplifyImpostor impostor, string autoCreateFolder )
		{
			string folder = autoCreateFolder;
			if ( string.IsNullOrEmpty( folder ) )
			{
				string targetPath = AssetDatabase.GetAssetPath( target );
				folder = !string.IsNullOrEmpty( targetPath ) ? Path.GetDirectoryName( targetPath ).Replace( "\\", "/" ) : "Assets";
			}
			if ( !AssetDatabase.IsValidFolder( folder ) )
			{
				folder = "Assets";
			}

			string name = ( impostor.RootTransform != null ? impostor.RootTransform.name : target.name ) + "_Impostor";
			string assetPath = AssetDatabase.GenerateUniqueAssetPath( folder + "/" + name + ".asset" );

			AmplifyImpostorAsset asset = ScriptableObject.CreateInstance<AmplifyImpostorAsset>();
			AssetDatabase.CreateAsset( asset, assetPath );
			return asset;
		}
	}
}
#endif
