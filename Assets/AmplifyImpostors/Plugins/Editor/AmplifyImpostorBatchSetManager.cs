// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AmplifyImpostors
{
	// manages the project's reusable Batch Sets: create/delete/rename, curate each set's impostors, and
	// batch bake a single set or every set flagged for inclusion.
	public class AmplifyImpostorBatchSetManager : EditorWindow
	{
		[SerializeField]
		private AmplifyImpostorBatchSet m_selected = null;

		[SerializeField]
		private Vector2 m_setsScroll = Vector2.zero;

		[SerializeField]
		private Vector2 m_detailScroll = Vector2.zero;

		[SerializeField]
		private bool m_autoCreateAssets = false;

		[SerializeField]
		private string m_autoCreateFolder = string.Empty;

		// when true the right panel shows a read-only aggregate of every impostor across all included sets,
		// instead of the selected set's editable detail. Not serialized: the window always opens in this mode.
		private bool m_showIncluded = true;

		// all Batch Sets found in the project, rebuilt on enable/focus (not serialized)
		private readonly List<AmplifyImpostorBatchSet> m_sets = new List<AmplifyImpostorBatchSet>();

		private AmplifyImpostorBakeView m_view = null;
		private AmplifyImpostorBatchSetDetailView m_detail = null;

		// adjustable width of the left (sets) panel, dragged via the splitter between the panels
		[SerializeField]
		private float m_setsPanelWidth = 230f;
		private bool m_resizingSplitter = false;

		private const float SplitterWidth = 2f;
		private const float MinSetsPanelWidth = 150f;
		private const float MinDetailWidth = 320f;

		private static readonly GUIContent NewSetStr = new GUIContent( "New", "Creates a new empty Batch Set asset" );
		private static readonly GUIContent DeleteSetStr = new GUIContent( "Delete", "Deletes the selected Batch Set asset" );
		private static readonly GUIContent RefreshStr = new GUIContent( "Refresh", "Rescans the project for Batch Sets" );
		private static readonly GUIContent BakeIncludedStr = new GUIContent( "Bake All Included Sets", "Sequentially bakes every enabled impostor across all included sets" );
		private static readonly GUIContent ShowIncludedStr = new GUIContent( "Show All Included Impostors", "Lists every impostor across all included sets in a read-only view" );
		private static readonly GUIContent AutoCreateStr = new GUIContent( "Auto-create missing assets", "When enabled, impostors without an Impostor Asset get one created automatically on bake" );
		private static readonly GUIContent OutputFolderStr = new GUIContent( "Output", "Folder for auto-created assets. When empty they are created next to the prefab." );

		private const string WindowTitle = "Batch Set Manager";

		[MenuItem( "Window/Amplify Impostors/" + WindowTitle, false, 1000 )]
		public static void Init()
		{
			AmplifyImpostorBatchSetManager window = ( AmplifyImpostorBatchSetManager )GetWindow( typeof( AmplifyImpostorBatchSetManager ), false, WindowTitle );
			window.titleContent = AmplifyImpostorBakeView.MakeWindowTitle( WindowTitle );
			window.minSize = new Vector2( 1000, 360 );

			float width = 1200;
			float height = 560;
			Rect main = EditorGUIUtility.GetMainWindowPosition();
			window.position = new Rect( main.x + ( main.width - width ) * 0.5f, main.y + ( main.height - height ) * 0.5f, width, height );

			window.Show();
		}

		// opens the manager focused on a specific set (used by the Batch Set inspector)
		public static void OpenWithSet( AmplifyImpostorBatchSet set )
		{
			Init();
			AmplifyImpostorBatchSetManager window = ( AmplifyImpostorBatchSetManager )GetWindow( typeof( AmplifyImpostorBatchSetManager ), false, WindowTitle );
			window.RefreshSets();
			window.m_selected = set;
			window.m_showIncluded = false;
			window.Focus();
		}

		private void OnEnable()
		{
			m_view = new AmplifyImpostorBakeView( Repaint );
			m_detail = new AmplifyImpostorBatchSetDetailView( Repaint, true );
			m_showIncluded = true; // always open in the included-impostors aggregate view
			RefreshSets();
		}

		private void OnDisable()
		{
			if ( m_view != null )
			{
				m_view.Cleanup();
				m_view = null;
			}
			if ( m_detail != null )
			{
				m_detail.Cleanup();
				m_detail = null;
			}
		}

		private void OnFocus()
		{
			RefreshSets();
		}

		private void OnGUI()
		{
			EditorGUILayout.BeginHorizontal();
			{
				DrawSetsPanel();
				DrawSplitter();

				EditorGUILayout.BeginVertical();
				{
					DrawDetail();
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndHorizontal();

			if ( AssetPreview.IsLoadingAssetPreviews() )
			{
				Repaint();
			}
		}

		// left panel: the list of all sets with include checkboxes, selection and create/delete/bake-all
		private void DrawSetsPanel()
		{
			EditorGUILayout.BeginVertical( GUIStyle.none, GUILayout.Width( m_setsPanelWidth ), GUILayout.ExpandHeight( true ) );
			{
				EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
				{
					if ( GUILayout.Button( NewSetStr, EditorStyles.toolbarButton ) )
					{
						CreateNewSet();
					}
					EditorGUI.BeginDisabledGroup( m_selected == null );
					if ( GUILayout.Button( DeleteSetStr, EditorStyles.toolbarButton ) )
					{
						DeleteSelectedSet();
					}
					EditorGUI.EndDisabledGroup();
					GUILayout.FlexibleSpace();
					if ( GUILayout.Button( RefreshStr, EditorStyles.toolbarButton ) )
					{
						RefreshSets();
					}
				}
				EditorGUILayout.EndHorizontal();

				m_setsScroll = EditorGUILayout.BeginScrollView( m_setsScroll );
				{
					if ( m_sets.Count == 0 )
					{
						EditorGUILayout.HelpBox( "No Batch Sets in the project. Use \"New\" to create one.", MessageType.Info );
					}
					else
					{
						for ( int i = 0; i < m_sets.Count; i++ )
						{
							DrawSetRow( m_sets[ i ] );
						}
					}
				}
				EditorGUILayout.EndScrollView();

				GUILayout.FlexibleSpace();

				int includedReady = CountIncludedReady();
				using ( new EditorGUI.DisabledScope( includedReady == 0 || EditorApplication.isPlayingOrWillChangePlaymode ) )
				{
					if ( GUILayout.Button( BakeIncludedStr, GUILayout.Height( 26 ) ) )
					{
						BakeIncludedSets();
					}
				}
			}
			EditorGUILayout.EndVertical();
		}

		// draggable vertical divider that adjusts the left panel's width
		private void DrawSplitter()
		{
			Rect splitterRect = GUILayoutUtility.GetRect( SplitterWidth, SplitterWidth, GUILayout.ExpandHeight( true ) );
			EditorGUI.DrawRect( splitterRect, new Color( 0f, 0f, 0f, 0.25f ) );

			// the visible line is only 2px; widen the grab/cursor area so it stays easy to drag
			Rect hitRect = new Rect( splitterRect.x - 3f, splitterRect.y, splitterRect.width + 6f, splitterRect.height );
			EditorGUIUtility.AddCursorRect( hitRect, MouseCursor.ResizeHorizontal );

			Event evt = Event.current;
			switch ( evt.type )
			{
				case EventType.MouseDown:
					if ( evt.button == 0 && hitRect.Contains( evt.mousePosition ) )
					{
						m_resizingSplitter = true;
						evt.Use();
					}
					break;
				case EventType.MouseDrag:
					if ( m_resizingSplitter )
					{
						m_setsPanelWidth = Mathf.Clamp( evt.mousePosition.x, MinSetsPanelWidth, position.width - MinDetailWidth );
						evt.Use();
						Repaint();
					}
					break;
				case EventType.MouseUp:
					if ( m_resizingSplitter )
					{
						m_resizingSplitter = false;
						evt.Use();
					}
					break;
			}
		}

		private void DrawSetRow( AmplifyImpostorBatchSet set )
		{
			if ( set == null )
			{
				return;
			}

			bool isSelected = ( set == m_selected );
			Color cache = GUI.backgroundColor;
			if ( isSelected )
			{
				GUI.backgroundColor = new Color( 0.45f, 0.6f, 0.9f );
			}

			EditorGUILayout.BeginHorizontal( EditorStyles.helpBox );
			{
				GUI.backgroundColor = cache;

				bool include = set.IncludeInBatch;
				bool newInclude = EditorGUILayout.Toggle( include, GUILayout.Width( 16 ) );
				if ( newInclude != include )
				{
					Undo.RecordObject( set, "Toggle Batch Set" );
					set.IncludeInBatch = newInclude;
					EditorUtility.SetDirty( set );
					AssetDatabase.SaveAssetIfDirty( set );
				}

				int count = ( set.Targets != null ) ? set.Targets.Count : 0;
				if ( GUILayout.Button( set.name + "  (" + count + ")", EditorStyles.label ) )
				{
					m_selected = set;
					m_showIncluded = false;
					EditorGUIUtility.PingObject( set );
				}
			}
			EditorGUILayout.EndHorizontal();

			GUI.backgroundColor = cache;
		}

		// right panel: the selected set's impostors and per-set baking
		private void DrawDetail()
		{
			DrawDetailModeBar();

			if ( m_showIncluded )
			{
				DrawIncludedAggregate();
				return;
			}

			if ( m_selected == null )
			{
				EditorGUILayout.HelpBox( "Select a Batch Set on the left, or create one with \"New\".\n\nChecked sets are included in \"Bake All Included Sets\". A set can also be baked on its own with \"Bake This Set\" (and from its inspector).", MessageType.Info );
				return;
			}

			// shared single-set detail (same UI as the Batch Set inspector)
			m_detail.Draw( m_selected );
		}

		// top bar of the right panel: toggles between the selected set's detail and the included-impostors view
		private void DrawDetailModeBar()
		{
			EditorGUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				m_showIncluded = GUILayout.Toggle( m_showIncluded, ShowIncludedStr, EditorStyles.toolbarButton );
				GUILayout.FlexibleSpace();
			}
			EditorGUILayout.EndHorizontal();
		}

		// read-only aggregate of every impostor across all included sets; set-specific editing is disabled here
		private void DrawIncludedAggregate()
		{
			List<GameObject> targets = new List<GameObject>();
			GatherIncludedTargets( targets );

			EditorGUILayout.BeginVertical( EditorStyles.helpBox );
			{
				GUILayout.Label( "All Included Impostors (" + targets.Count + ")", EditorStyles.boldLabel );
				GUILayout.Label( "Read-only view across every included set. Edit membership from each set; impostor settings stay editable.", EditorStyles.miniLabel );
			}
			EditorGUILayout.EndVertical();

			DrawDetailOptionsBar();

			m_detailScroll = EditorGUILayout.BeginScrollView( m_detailScroll );
			{
				if ( targets.Count == 0 )
				{
					EditorGUILayout.HelpBox( "No impostors in included sets. Check a set's include box on the left, or add impostors to an included set.", MessageType.Info );
				}

				bool ignored = true;
				for ( int i = 0; i < targets.Count; i++ )
				{
					m_view.DrawRow( targets[ i ], ref ignored, m_autoCreateAssets, false );
				}
			}
			EditorGUILayout.EndScrollView();

			DrawIncludedFooter();
		}

		// footer for the included-impostors view: bakeable count/memory and the bake-all-included action
		private void DrawIncludedFooter()
		{
			List<GameObject> bakeable = new List<GameObject>();
			for ( int i = 0; i < m_sets.Count; i++ )
			{
				if ( m_sets[ i ] != null && m_sets[ i ].IncludeInBatch )
				{
					AmplifyImpostorBakeView.GatherReady( m_sets[ i ], m_autoCreateAssets, bakeable );
				}
			}

			float totalMemory = 0f;
			for ( int i = 0; i < bakeable.Count; i++ )
			{
				AmplifyImpostor impostor = AmplifyImpostorBakeView.ResolveImpostor( bakeable[ i ] );
				if ( impostor != null && impostor.Data != null )
				{
					totalMemory += AmplifyImpostorBakeView.EstimateMemoryMB( impostor.Data );
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
					GUILayout.Label( bakeable.Count + " ready to bake  /  ~" + totalMemory.ToString( "0.0" ) + " MB total", EditorStyles.miniLabel );
				}
				GUILayout.FlexibleSpace();

				// no bake button here on purpose: in this aggregate view baking is done from the
				// left panel's "Bake All Included Sets"
			}
			EditorGUILayout.EndHorizontal();
		}

		// collects every impostor target across all included sets, deduplicated, regardless of per-impostor enable
		private void GatherIncludedTargets( List<GameObject> list )
		{
			for ( int i = 0; i < m_sets.Count; i++ )
			{
				AmplifyImpostorBatchSet set = m_sets[ i ];
				if ( set == null || !set.IncludeInBatch )
				{
					continue;
				}
				for ( int j = 0; j < set.Targets.Count; j++ )
				{
					GameObject target = set.Targets[ j ];
					if ( target != null && !list.Contains( target ) )
					{
						list.Add( target );
					}
				}
			}
		}

		private void DrawDetailOptionsBar()
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

		private void CreateNewSet()
		{
			string path = EditorUtility.SaveFilePanelInProject( "New Batch Set", "ImpostorBatchSet", "asset", "Choose where to create the Batch Set" );
			if ( string.IsNullOrEmpty( path ) )
			{
				return;
			}

			AmplifyImpostorBatchSet set = CreateInstance<AmplifyImpostorBatchSet>();
			AssetDatabase.CreateAsset( set, path );
			AssetDatabase.SaveAssets();

			RefreshSets();
			m_selected = set;
			m_showIncluded = false;
			GUIUtility.ExitGUI();
		}

		private void DeleteSelectedSet()
		{
			if ( m_selected == null )
			{
				return;
			}

			if ( !EditorUtility.DisplayDialog( "Delete Batch Set", "Delete \"" + m_selected.name + "\"?\n\nThis only removes the set asset. The impostors and their baked textures are not affected.", "Delete", "Cancel" ) )
			{
				return;
			}

			string path = AssetDatabase.GetAssetPath( m_selected );
			m_selected = null;
			AssetDatabase.DeleteAsset( path );
			RefreshSets();
			GUIUtility.ExitGUI();
		}

		private void BakeIncludedSets()
		{
			List<GameObject> toBake = new List<GameObject>();
			for ( int i = 0; i < m_sets.Count; i++ )
			{
				if ( m_sets[ i ] != null && m_sets[ i ].IncludeInBatch )
				{
					AmplifyImpostorBakeView.GatherReady( m_sets[ i ], m_autoCreateAssets, toBake );
				}
			}

			bool autoCreate = m_autoCreateAssets;
			string autoCreateFolder = m_autoCreateFolder;
			EditorApplication.delayCall += () => AmplifyImpostorBakeView.BakeTargets( toBake, autoCreate, autoCreateFolder );
		}

		private int CountIncludedReady()
		{
			List<GameObject> ready = new List<GameObject>();
			for ( int i = 0; i < m_sets.Count; i++ )
			{
				if ( m_sets[ i ] != null && m_sets[ i ].IncludeInBatch )
				{
					AmplifyImpostorBakeView.GatherReady( m_sets[ i ], m_autoCreateAssets, ready );
				}
			}
			return ready.Count;
		}

		private void RefreshSets()
		{
			m_sets.Clear();
			string[] guids = AssetDatabase.FindAssets( "t:AmplifyImpostorBatchSet" );
			for ( int i = 0; i < guids.Length; i++ )
			{
				string path = AssetDatabase.GUIDToAssetPath( guids[ i ] );
				AmplifyImpostorBatchSet set = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBatchSet>( path );
				if ( set != null )
				{
					m_sets.Add( set );
				}
			}

			if ( m_selected != null && !m_sets.Contains( m_selected ) )
			{
				m_selected = null;
			}
		}
	}
}
#endif
