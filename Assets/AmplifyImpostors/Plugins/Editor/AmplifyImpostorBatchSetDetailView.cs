// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace AmplifyImpostors
{
	// reusable editable detail panel for a single Batch Set (rename, include toggle, add/remove impostors,
	// per-impostor settings and "Bake This Set"). Used by both the Batch Set Manager's right panel and the
	// Batch Set inspector so they always show the same UI.
	public class AmplifyImpostorBatchSetDetailView
	{
		private readonly AmplifyImpostorBakeView m_view;
		private readonly Action m_repaint;
		// when false the impostor list is laid out without its own scroll view (for hosts that already scroll,
		// e.g. the inspector); when true it gets a scroll view (e.g. a window)
		private readonly bool m_useScroll;

		private bool m_autoCreateAssets = false;
		private string m_autoCreateFolder = string.Empty;
		private Vector2 m_scroll = Vector2.zero;
		private GUIStyle m_dropAreaStyle = null;

		private const string DialogTitle = "Batch Set";

		private static readonly GUIContent IncludeStr = new GUIContent( "Include in \"Bake All Included\"", "When checked, this set is baked by the Batch Set Manager's \"Bake All Included Sets\" button" );
		private static readonly GUIContent BakeThisSetStr = new GUIContent( "Bake This Set", "Sequentially bakes every enabled impostor in this set" );
		private static readonly GUIContent AddFolderStr = new GUIContent( "Add Folder...", "Recursively searches a folder for prefabs containing an Amplify Impostor" );
		private static readonly GUIContent AddSelectedStr = new GUIContent( "Add Selected", "Adds the currently selected objects that contain an Amplify Impostor" );
		private static readonly GUIContent AddAllStr = new GUIContent( "Add All", "Adds every prefab in the project that contains an Amplify Impostor" );
		private static readonly GUIContent ClearStr = new GUIContent( "Clear", "Removes all impostors from this set" );
		private static readonly GUIContent AutoCreateStr = new GUIContent( "Auto-create missing assets", "When enabled, impostors without an Impostor Asset get one created automatically on bake" );
		private static readonly GUIContent OutputFolderStr = new GUIContent( "Output", "Folder for auto-created assets. When empty they are created next to the prefab." );

		public AmplifyImpostorBatchSetDetailView( Action repaint, bool useScroll )
		{
			m_repaint = repaint;
			m_useScroll = useScroll;
			m_view = new AmplifyImpostorBakeView( repaint );
		}

		public void Cleanup()
		{
			if ( m_view != null )
			{
				m_view.Cleanup();
			}
		}

		public void Draw( AmplifyImpostorBatchSet set )
		{
			if ( set == null )
			{
				return;
			}

			EnsureStyles();
			SyncEnabled( set );

			// header: rename + include toggle
			EditorGUILayout.BeginVertical( EditorStyles.helpBox );
			{
				EditorGUILayout.BeginHorizontal();
				{
					GUILayout.Label( "Name", GUILayout.Width( 42 ) );
					EditorGUI.BeginChangeCheck();
					string newName = EditorGUILayout.DelayedTextField( set.name );
					if ( EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty( newName ) && newName != set.name )
					{
						AssetDatabase.RenameAsset( AssetDatabase.GetAssetPath( set ), newName );
						AssetDatabase.SaveAssets();
					}
				}
				EditorGUILayout.EndHorizontal();

				bool include = set.IncludeInBatch;
				bool newInclude = EditorGUILayout.ToggleLeft( IncludeStr, include );
				if ( newInclude != include )
				{
					Undo.RecordObject( set, "Toggle Batch Set" );
					set.IncludeInBatch = newInclude;
					EditorUtility.SetDirty( set );
					AssetDatabase.SaveAssetIfDirty( set );
				}
			}
			EditorGUILayout.EndVertical();

			DrawToolbar( set );
			DrawOptionsBar();
			DrawDropArea( set );
			DrawList( set );
			DrawFooter( set );

			// previews are generated asynchronously, keep repainting until they're ready
			if ( AssetPreview.IsLoadingAssetPreviews() && m_repaint != null )
			{
				m_repaint();
			}
		}

		private void EnsureStyles()
		{
			if ( m_dropAreaStyle == null )
			{
				m_dropAreaStyle = new GUIStyle( "box" );
				m_dropAreaStyle.alignment = TextAnchor.MiddleCenter;
				m_dropAreaStyle.normal.textColor = EditorStyles.label.normal.textColor;
			}
		}

		private void DrawToolbar( AmplifyImpostorBatchSet set )
		{
			EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				if ( GUILayout.Button( AddFolderStr, EditorStyles.toolbarButton ) )
				{
					AddFromFolderPanel( set );
				}
				if ( GUILayout.Button( AddSelectedStr, EditorStyles.toolbarButton ) )
				{
					AddTargets( set, Selection.gameObjects );
				}
				if ( GUILayout.Button( AddAllStr, EditorStyles.toolbarButton ) )
				{
					AddFromFolder( set, "Assets" );
				}

				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup( set.Targets.Count == 0 );
				if ( GUILayout.Button( ClearStr, EditorStyles.toolbarButton ) )
				{
					Undo.RecordObject( set, "Clear Batch Set" );
					set.Targets.Clear();
					set.Enabled.Clear();
					EditorUtility.SetDirty( set );
					AssetDatabase.SaveAssetIfDirty( set );
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawOptionsBar()
		{
			EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				m_autoCreateAssets = GUILayout.Toggle( m_autoCreateAssets, AutoCreateStr, EditorStyles.toolbarButton );

				EditorGUI.BeginDisabledGroup( !m_autoCreateAssets );
				GUILayout.Label( OutputFolderStr, EditorStyles.miniLabel, GUILayout.Width( 48 ) );
				string folderLabel = string.IsNullOrEmpty( m_autoCreateFolder ) ? "(next to prefab)" : m_autoCreateFolder;
				GUILayout.Label( folderLabel, EditorStyles.miniLabel, GUILayout.MinWidth( 60 ) );
				if ( GUILayout.Button( "...", EditorStyles.toolbarButton, GUILayout.Width( 26 ) ) )
				{
					PickOutputFolder();
				}
				if ( !string.IsNullOrEmpty( m_autoCreateFolder ) && GUILayout.Button( "x", EditorStyles.toolbarButton, GUILayout.Width( 22 ) ) )
				{
					m_autoCreateFolder = string.Empty;
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawDropArea( AmplifyImpostorBatchSet set )
		{
			Rect dropArea = GUILayoutUtility.GetRect( 0, 38, GUILayout.ExpandWidth( true ) );
			dropArea.xMin += 4;
			dropArea.xMax -= 4;
			dropArea.yMin += 4;
			GUI.Box( dropArea, "Drag prefabs, objects or folders here", m_dropAreaStyle );

			Event evt = Event.current;
			if ( ( evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform ) && dropArea.Contains( evt.mousePosition ) )
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				if ( evt.type == EventType.DragPerform )
				{
					DragAndDrop.AcceptDrag();
					foreach ( UnityEngine.Object dragged in DragAndDrop.objectReferences )
					{
						string path = AssetDatabase.GetAssetPath( dragged );
						if ( !string.IsNullOrEmpty( path ) && AssetDatabase.IsValidFolder( path ) )
						{
							AddFromFolder( set, path );
						}
						else if ( dragged is GameObject )
						{
							AddTarget( set, dragged as GameObject );
						}
					}
				}
				evt.Use();
			}
		}

		private void DrawList( AmplifyImpostorBatchSet set )
		{
			if ( m_useScroll )
			{
				m_scroll = EditorGUILayout.BeginScrollView( m_scroll );
			}

			if ( set.Targets.Count == 0 )
			{
				EditorGUILayout.HelpBox( "This set has no impostors. Add some with the buttons above or by dragging prefabs in.", MessageType.Info );
			}

			int removeIndex = -1;
			for ( int i = 0; i < set.Targets.Count; i++ )
			{
				bool enabled = ( i < set.Enabled.Count ) ? set.Enabled[ i ] : true;
				bool before = enabled;

				if ( m_view.DrawRow( set.Targets[ i ], ref enabled, m_autoCreateAssets, true ) )
				{
					removeIndex = i;
				}

				if ( enabled != before && i < set.Enabled.Count )
				{
					Undo.RecordObject( set, "Toggle Impostor" );
					set.Enabled[ i ] = enabled;
					EditorUtility.SetDirty( set );
				}
			}

			if ( removeIndex >= 0 )
			{
				Undo.RecordObject( set, "Remove Impostor" );
				set.Targets.RemoveAt( removeIndex );
				if ( removeIndex < set.Enabled.Count )
				{
					set.Enabled.RemoveAt( removeIndex );
				}
				EditorUtility.SetDirty( set );
				AssetDatabase.SaveAssetIfDirty( set );
			}

			if ( m_useScroll )
			{
				EditorGUILayout.EndScrollView();
			}
		}

		private void DrawFooter( AmplifyImpostorBatchSet set )
		{
			int ready = 0;
			float totalMemory = 0f;
			for ( int i = 0; i < set.Targets.Count; i++ )
			{
				bool enabled = ( i < set.Enabled.Count ) ? set.Enabled[ i ] : true;
				if ( !enabled || set.Targets[ i ] == null )
				{
					continue;
				}

				AmplifyImpostor impostor = AmplifyImpostorBakeView.ResolveImpostor( set.Targets[ i ] );
				if ( AmplifyImpostorBakeView.IsReady( impostor, m_autoCreateAssets ) )
				{
					ready++;
					if ( impostor.Data != null )
					{
						totalMemory += AmplifyImpostorBakeView.EstimateMemoryMB( impostor.Data );
					}
				}
			}

			bool inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;

			EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				if ( inPlayMode )
				{
					GUILayout.Label( "Exit Play Mode to bake", EditorStyles.miniLabel );
				}
				else
				{
					GUILayout.Label( set.Targets.Count + " in set  /  " + ready + " ready  /  ~" + totalMemory.ToString( "0.0" ) + " MB total", EditorStyles.miniLabel );
				}
				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup( ready == 0 || inPlayMode );
				if ( GUILayout.Button( BakeThisSetStr, EditorStyles.toolbarButton, GUILayout.Width( 110 ) ) )
				{
					BakeBatchSet( set );
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();
		}

		private void PickOutputFolder()
		{
			string absolute = EditorUtility.OpenFolderPanel( "Select Output Folder for Created Assets", Application.dataPath, "" );
			if ( string.IsNullOrEmpty( absolute ) )
			{
				return;
			}

			string relative = FileUtil.GetProjectRelativePath( absolute.Replace( "\\", "/" ) );
			if ( string.IsNullOrEmpty( relative ) || !AssetDatabase.IsValidFolder( relative ) )
			{
				EditorUtility.DisplayDialog( DialogTitle, "The output folder must be inside the project's Assets folder.", "OK" );
				return;
			}

			m_autoCreateFolder = relative;
			GUIUtility.ExitGUI();
		}

		// keeps the Enabled list the same length as Targets
		private static void SyncEnabled( AmplifyImpostorBatchSet set )
		{
			while ( set.Enabled.Count < set.Targets.Count )
			{
				set.Enabled.Add( true );
			}
			while ( set.Enabled.Count > set.Targets.Count )
			{
				set.Enabled.RemoveAt( set.Enabled.Count - 1 );
			}
		}

		private static bool SetContains( AmplifyImpostorBatchSet set, GameObject go )
		{
			for ( int i = 0; i < set.Targets.Count; i++ )
			{
				if ( set.Targets[ i ] == go )
				{
					return true;
				}
			}
			return false;
		}

		private void AddTargets( AmplifyImpostorBatchSet set, GameObject[] targets )
		{
			if ( targets == null )
			{
				return;
			}
			Undo.RecordObject( set, "Add Impostors" );
			for ( int i = 0; i < targets.Length; i++ )
			{
				AddTargetInternal( set, targets[ i ] );
			}
			EditorUtility.SetDirty( set );
			AssetDatabase.SaveAssetIfDirty( set );
		}

		private void AddTarget( AmplifyImpostorBatchSet set, GameObject go )
		{
			Undo.RecordObject( set, "Add Impostor" );
			if ( AddTargetInternal( set, go ) )
			{
				EditorUtility.SetDirty( set );
				AssetDatabase.SaveAssetIfDirty( set );
			}
		}

		private bool AddTargetInternal( AmplifyImpostorBatchSet set, GameObject go )
		{
			if ( go == null || SetContains( set, go ) )
			{
				return false;
			}
			if ( AmplifyImpostorBakeView.ResolveImpostor( go ) == null )
			{
				return false;
			}
			set.Targets.Add( go );
			set.Enabled.Add( true );
			return true;
		}

		private void AddFromFolderPanel( AmplifyImpostorBatchSet set )
		{
			string absolute = EditorUtility.OpenFolderPanel( "Select Folder to Search", Application.dataPath, "" );
			if ( string.IsNullOrEmpty( absolute ) )
			{
				return;
			}

			string relative = FileUtil.GetProjectRelativePath( absolute.Replace( "\\", "/" ) );
			if ( string.IsNullOrEmpty( relative ) || !AssetDatabase.IsValidFolder( relative ) )
			{
				EditorUtility.DisplayDialog( DialogTitle, "The selected folder must be inside the project's Assets folder.", "OK" );
				return;
			}

			AddFromFolder( set, relative );
		}

		private void AddFromFolder( AmplifyImpostorBatchSet set, string projectRelativeFolder )
		{
			string[] guids = AssetDatabase.FindAssets( "t:Prefab", new string[] { projectRelativeFolder } );
			int added = 0;
			Undo.RecordObject( set, "Add Impostors" );
			for ( int i = 0; i < guids.Length; i++ )
			{
				string path = AssetDatabase.GUIDToAssetPath( guids[ i ] );
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>( path );
				if ( prefab != null && AddTargetInternal( set, prefab ) )
				{
					added++;
				}
			}

			if ( added > 0 )
			{
				EditorUtility.SetDirty( set );
				AssetDatabase.SaveAssetIfDirty( set );
			}
			else
			{
				Debug.Log( "[AmplifyImpostors] No new prefabs with an Amplify Impostor were found in \"" + projectRelativeFolder + "\"." );
			}
		}

		private void BakeBatchSet( AmplifyImpostorBatchSet set )
		{
			List<GameObject> toBake = new List<GameObject>();
			AmplifyImpostorBakeView.GatherReady( set, m_autoCreateAssets, toBake );

			bool autoCreate = m_autoCreateAssets;
			string autoCreateFolder = m_autoCreateFolder;
			// defer so the bake runs outside OnGUI/OnInspectorGUI (avoids IMGUI re-entrancy aborting the bake)
			EditorApplication.delayCall += () => AmplifyImpostorBakeView.BakeTargets( toBake, autoCreate, autoCreateFolder );
		}
	}
}
#endif
