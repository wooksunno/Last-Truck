// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace AmplifyImpostors
{
	// ad-hoc batch baker: drop in prefabs/objects and bake them now. Reusable, named collections are
	// handled by the Batch Set Manager instead.
	public class AmplifyImpostorBatchBaker : EditorWindow
	{
		[Serializable]
		private class Entry
		{
			public GameObject Target;
			public bool Enabled = true;
		}

		[SerializeField]
		private List<Entry> m_entries = new List<Entry>();

		[SerializeField]
		private Vector2 m_scrollPosition = Vector2.zero;

		[SerializeField]
		private bool m_autoCreateAssets = false;

		[SerializeField]
		private string m_autoCreateFolder = string.Empty;

		private GUIStyle m_dropAreaStyle = null;
		private AmplifyImpostorBakeView m_view = null;

		private static readonly GUIContent BakeAllStr = new GUIContent( "Bake All", "Sequentially bakes every enabled impostor in the list" );
		private static readonly GUIContent AddFolderStr = new GUIContent( "Add Folder...", "Recursively searches a folder for prefabs containing an Amplify Impostor" );
		private static readonly GUIContent AddSelectedStr = new GUIContent( "Add Selected", "Adds the currently selected objects that contain an Amplify Impostor" );
		private static readonly GUIContent AddAllStr = new GUIContent( "Add All", "Adds every prefab in the project that contains an Amplify Impostor" );
		private static readonly GUIContent ClearStr = new GUIContent( "Clear", "Removes all entries from the list" );
		private static readonly GUIContent AutoCreateStr = new GUIContent( "Auto-create missing assets", "When enabled, impostors without an Impostor Asset get one created automatically on bake" );
		private static readonly GUIContent OutputFolderStr = new GUIContent( "Output", "Folder for auto-created assets. When empty they are created next to the prefab." );

		private const string WindowTitle = "Batch Baker";

		[MenuItem( "Window/Amplify Impostors/" + WindowTitle, false, 1000 )]
		public static void Init()
		{
			AmplifyImpostorBatchBaker window = ( AmplifyImpostorBatchBaker )GetWindow( typeof( AmplifyImpostorBatchBaker ), false, WindowTitle );
			window.titleContent = AmplifyImpostorBakeView.MakeWindowTitle( WindowTitle );
			window.minSize = new Vector2( 880, 320 );

			float width = 1000;
			float height = 540;
			Rect main = EditorGUIUtility.GetMainWindowPosition();
			window.position = new Rect( main.x + ( main.width - width ) * 0.5f, main.y + ( main.height - height ) * 0.5f, width, height );

			window.Show();
		}

		private void OnEnable()
		{
			m_view = new AmplifyImpostorBakeView( Repaint );
		}

		private void OnDisable()
		{
			if ( m_view != null )
			{
				m_view.Cleanup();
				m_view = null;
			}
		}

		private void OnGUI()
		{
			if ( m_dropAreaStyle == null )
			{
				m_dropAreaStyle = new GUIStyle( "box" );
				m_dropAreaStyle.alignment = TextAnchor.MiddleCenter;
				m_dropAreaStyle.normal.textColor = EditorStyles.label.normal.textColor;
			}

			DrawToolbar();
			DrawOptionsBar();
			DrawDropArea();
			DrawList();
			DrawFooter();

			// previews are generated asynchronously, keep repainting until they're ready
			if ( AssetPreview.IsLoadingAssetPreviews() )
			{
				Repaint();
			}
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				if ( GUILayout.Button( AddFolderStr, EditorStyles.toolbarButton ) )
				{
					AddFromFolderPanel();
				}

				if ( GUILayout.Button( AddSelectedStr, EditorStyles.toolbarButton ) )
				{
					AddTargets( Selection.gameObjects );
				}

				if ( GUILayout.Button( AddAllStr, EditorStyles.toolbarButton ) )
				{
					AddFromFolder( "Assets" );
				}

				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup( m_entries.Count == 0 );
				if ( GUILayout.Button( ClearStr, EditorStyles.toolbarButton ) )
				{
					m_entries.Clear();
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
				EditorUtility.DisplayDialog( WindowTitle, "The output folder must be inside the project's Assets folder.", "OK" );
				return;
			}

			m_autoCreateFolder = relative;
			GUIUtility.ExitGUI();
		}

		private void DrawDropArea()
		{
			Rect dropArea = GUILayoutUtility.GetRect( 0, 42, GUILayout.ExpandWidth( true ) );
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
							AddFromFolder( path );
						}
						else if ( dragged is GameObject )
						{
							AddTarget( dragged as GameObject );
						}
					}
				}
				evt.Use();
			}
		}

		private void DrawList()
		{
			m_scrollPosition = EditorGUILayout.BeginScrollView( m_scrollPosition );
			{
				if ( m_entries.Count == 0 )
				{
					EditorGUILayout.HelpBox( "No impostors added yet. Drag in prefabs/objects, add a folder, or add the current selection.\n\nOnly objects with an Amplify Impostor component can be baked. Without an assigned Impostor Asset, enable \"Auto-create missing assets\".", MessageType.Info );
				}

				int removeIndex = -1;
				for ( int i = 0; i < m_entries.Count; i++ )
				{
					Entry entry = m_entries[ i ];
					if ( m_view.DrawRow( entry.Target, ref entry.Enabled, m_autoCreateAssets, true ) )
					{
						removeIndex = i;
					}
				}

				if ( removeIndex >= 0 )
				{
					m_entries.RemoveAt( removeIndex );
				}
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawFooter()
		{
			int ready = 0;
			float totalMemory = 0f;
			for ( int i = 0; i < m_entries.Count; i++ )
			{
				if ( !m_entries[ i ].Enabled || m_entries[ i ].Target == null )
				{
					continue;
				}

				AmplifyImpostor impostor = AmplifyImpostorBakeView.ResolveImpostor( m_entries[ i ].Target );
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
					GUILayout.Label( m_entries.Count + " in list  /  " + ready + " ready  /  ~" + totalMemory.ToString( "0.0" ) + " MB total", EditorStyles.miniLabel );
				}
				GUILayout.FlexibleSpace();

				EditorGUI.BeginDisabledGroup( ready == 0 || inPlayMode );
				if ( GUILayout.Button( BakeAllStr, EditorStyles.toolbarButton, GUILayout.Width( 90 ) ) )
				{
					BakeAll();
				}
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();
		}

		private bool Contains( GameObject go )
		{
			for ( int i = 0; i < m_entries.Count; i++ )
			{
				if ( m_entries[ i ].Target == go )
				{
					return true;
				}
			}
			return false;
		}

		private void AddTargets( GameObject[] targets )
		{
			if ( targets == null )
			{
				return;
			}
			for ( int i = 0; i < targets.Length; i++ )
				AddTarget( targets[ i ] );
		}

		private void AddTarget( GameObject go )
		{
			if ( go == null || Contains( go ) )
			{
				return;
			}

			if ( AmplifyImpostorBakeView.ResolveImpostor( go ) == null )
			{
				return;
			}

			m_entries.Add( new Entry() { Target = go } );
		}

		private void AddFromFolderPanel()
		{
			string absolute = EditorUtility.OpenFolderPanel( "Select Folder to Search", Application.dataPath, "" );
			if ( string.IsNullOrEmpty( absolute ) )
			{
				return;
			}

			string relative = FileUtil.GetProjectRelativePath( absolute.Replace( "\\", "/" ) );
			if ( string.IsNullOrEmpty( relative ) || !AssetDatabase.IsValidFolder( relative ) )
			{
				EditorUtility.DisplayDialog( WindowTitle, "The selected folder must be inside the project's Assets folder.", "OK" );
				return;
			}

			AddFromFolder( relative );
		}

		private void AddFromFolder( string projectRelativeFolder )
		{
			string[] guids = AssetDatabase.FindAssets( "t:Prefab", new string[] { projectRelativeFolder } );
			int added = 0;
			for ( int i = 0; i < guids.Length; i++ )
			{
				string path = AssetDatabase.GUIDToAssetPath( guids[ i ] );
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>( path );
				if ( prefab != null && !Contains( prefab ) && AmplifyImpostorBakeView.ResolveImpostor( prefab ) != null )
				{
					m_entries.Add( new Entry() { Target = prefab } );
					added++;
				}
			}

			if ( added == 0 )
			{
				Debug.Log( "[AmplifyImpostors] No new prefabs with an Amplify Impostor were found in \"" + projectRelativeFolder + "\"." );
			}
		}

		private void BakeAll()
		{
			List<GameObject> toBake = new List<GameObject>();
			for ( int i = 0; i < m_entries.Count; i++ )
			{
				if ( !m_entries[ i ].Enabled || m_entries[ i ].Target == null )
				{
					continue;
				}
				if ( AmplifyImpostorBakeView.IsReady( AmplifyImpostorBakeView.ResolveImpostor( m_entries[ i ].Target ), m_autoCreateAssets ) )
				{
					toBake.Add( m_entries[ i ].Target );
				}
			}

			// defer so the bake runs outside OnGUI; baking prefab assets inside the IMGUI callback hits
			// GUI re-entrancy that aborts the bake mid-process and leaves the impostor white
			bool autoCreate = m_autoCreateAssets;
			string autoCreateFolder = m_autoCreateFolder;
			EditorApplication.delayCall += () => AmplifyImpostorBakeView.BakeTargets( toBake, autoCreate, autoCreateFolder );
		}
	}
}
#endif
