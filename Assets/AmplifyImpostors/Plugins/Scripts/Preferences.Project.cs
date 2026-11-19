// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Unity.Scripting.LifecycleManagement;

namespace AmplifyImpostors
{
	public partial class Preferences
	{
	#if UNITY_EDITOR
		[NoAutoStaticsCleanup]
		public class Project
		{
			public static bool AutoSRP { get { EnsureLoaded(); return Values.AutoSRP; } }
			public static bool DefineSymbol { get { EnsureLoaded(); return Values.DefineSymbol; } }
			public static int TexImport { get { EnsureLoaded(); return Values.TexImport; } }
			public static bool CreateLodGroup { get { EnsureLoaded(); return Values.CreateLodGroup; } }
			public static float LodTransition { get { EnsureLoaded(); return Values.LodTransition; } }
			public static bool GlobalDefaultMode { get { EnsureLoaded(); return Values.GlobalDefaultMode; } }
			public static string GlobalFolder { get { EnsureLoaded(); return Values.GlobalFolder; } }
			public static string RelativeFolder { get { EnsureLoaded(); return Values.RelativeFolder; } }
			public static string BakePresetGUID { get { EnsureLoaded(); return Values.BakePresetGUID; } }
			public static string AlbedoName { get { EnsureLoaded(); return Values.AlbedoName; } }
			public static string NormalsName { get { EnsureLoaded(); return Values.NormalsName; } }
			public static string SpecularName { get { EnsureLoaded(); return Values.SpecularName; } }
			public static string OcclusionName { get { EnsureLoaded(); return Values.OcclusionName; } }
			public static string EmissionName { get { EnsureLoaded(); return Values.EmissionName; } }
			public static string PositionName { get { EnsureLoaded(); return Values.PositionName; } }

			private class Styles
			{
				public static readonly GUIContent AutoSRP = new GUIContent( "Auto import SRP shaders", "By default Amplify Impostors checks for your SRP version and automatically imports compatible shaders.\nTurn this OFF if you prefer to import them manually." );
				public static readonly GUIContent DefineSymbol = new GUIContent( "Add Amplify Impostors define symbol", "Turning it OFF will disable the automatic insertion of the define symbol and remove it from the list while turning it ON will do the opposite.\nThis is used for compatibility with other plugins, if you are not sure if you need this leave it ON." );
				public static readonly GUIContent DefaultSettings = new GUIContent( "New Impostor Defaults", "Default settings to be assigned to new Impostors" );
				public static readonly GUIContent DefaultBakePreset = new GUIContent( "New Bake Preset Defaults", "Default Suffixes for new Bake Presets" );
			}

			private class Defaults
			{
				public const int Version = 1;
				public const bool AutoSRP = true;
				public const bool DefineSymbol = true;
				public const int TexImport = 0;
				public const bool CreateLodGroup = false;
				public const float LodTransition = 0.1f;
				public const bool GlobalDefaultMode = false;
				public const string GlobalFolder = "";
				public const string RelativeFolder = "";
				public const string BakePresetGUID = "e4786beb7716da54dbb02a632681cc37";
				public const string AlbedoName = "_AlbedoAlpha";
				public const string NormalsName = "_NormalDepth";
				public const string SpecularName = "_SpecularSmoothness";
				public const string OcclusionName = "_Occlusion";
				public const string EmissionName = "_EmissionOcclusion";
				public const string PositionName = "_Position";
			}

			[Serializable]
			private struct Layout
			{
				public int Version;
				public bool AutoSRP;
				public bool DefineSymbol;
				public int TexImport;
				public bool CreateLodGroup;
				public float LodTransition;
				public bool GlobalDefaultMode;
				public string GlobalFolder;
				public string RelativeFolder;
				public string BakePresetGUID;
				public string AlbedoName;
				public string NormalsName;
				public string SpecularName;
				public string OcclusionName;
				public string EmissionName;
				public string PositionName;
			}

			private const string RelativePath = "ProjectSettings/AmplifyImpostors.asset";
			private static string FullPath = Path.GetFullPath( RelativePath );
			private static Layout Values = new Layout();
			private static bool Loaded = false;
			private static long Timestamp = 0;

			private static void EnsureLoaded()
			{
				if ( !Loaded )
				{
					LoadSettings();
				}
			}

			public static void ResetSettings()
			{
				Values.Version = Defaults.Version;
				Values.AutoSRP = Defaults.AutoSRP;
				Values.DefineSymbol = Defaults.DefineSymbol;
				Values.TexImport = Defaults.TexImport;
				Values.CreateLodGroup = Defaults.CreateLodGroup;
				Values.LodTransition = Defaults.LodTransition;
				Values.GlobalDefaultMode = Defaults.GlobalDefaultMode;
				Values.GlobalFolder = Defaults.GlobalFolder;
				Values.RelativeFolder = Defaults.RelativeFolder;
				Values.BakePresetGUID = Defaults.BakePresetGUID;
				Values.AlbedoName = Defaults.AlbedoName;
				Values.NormalsName = Defaults.NormalsName;
				Values.SpecularName = Defaults.SpecularName;
				Values.OcclusionName = Defaults.OcclusionName;
				Values.EmissionName = Defaults.EmissionName;
				Values.PositionName = Defaults.PositionName;
				Loaded = true;
			}

			// Seeds a brand new project file from the per-user EditorPrefs keys these settings used before they became
			// project settings. The old keys are left in place. Mind the trailing space in the create LODGroup key.
			private static void SeedFromEditorPrefs()
			{
				Values.AutoSRP = EditorPrefs.GetBool( "IMPOSTORS_GLOBALAUTOSRP", Defaults.AutoSRP );
				Values.DefineSymbol = EditorPrefs.GetBool( "IMPOSTORS_GLOBALDEFINESYMBOL", Defaults.DefineSymbol );
				Values.TexImport = EditorPrefs.GetInt( "IMPOSTORS_GLOBALTEXIMPORT", Defaults.TexImport );
				Values.CreateLodGroup = EditorPrefs.GetBool( "IMPOSTORS_GLOBALCREATELODGROUP ", Defaults.CreateLodGroup );
				Values.LodTransition = EditorPrefs.GetFloat( "IMPOSTORS_GLOBALLODTRANSITION", Defaults.LodTransition );
				Values.GlobalDefaultMode = EditorPrefs.GetBool( "IMPOSTORS_GLOBALDEFAULT", Defaults.GlobalDefaultMode );
				Values.GlobalFolder = EditorPrefs.GetString( "IMPOSTORS_GLOBALFOLDER", Defaults.GlobalFolder );
				Values.RelativeFolder = EditorPrefs.GetString( "IMPOSTORS_GLOBALRELATIVEFOLDER", Defaults.RelativeFolder );
				Values.BakePresetGUID = EditorPrefs.GetString( "IMPOSTORS_GLOBALBAKEPRESETGUID", Defaults.BakePresetGUID );
				if ( string.IsNullOrEmpty( Values.BakePresetGUID ) )
				{
					Values.BakePresetGUID = Defaults.BakePresetGUID;
				}
				Values.AlbedoName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER0SUFFIX", Defaults.AlbedoName );
				Values.SpecularName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER1SUFFIX", Defaults.SpecularName );
				Values.NormalsName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER2SUFFIX", Defaults.NormalsName );
				Values.EmissionName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER3SUFFIX", Defaults.EmissionName );
				Values.OcclusionName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER4SUFFIX", Defaults.OcclusionName );
				Values.PositionName = EditorPrefs.GetString( "IMPOSTORS_GLOBALGBUFFER5SUFFIX", Defaults.PositionName );
			}

			public static void LoadSettings()
			{
				try
				{
					Values = JsonUtility.FromJson<Layout>( File.ReadAllText( FullPath ) );
					Timestamp = File.GetLastWriteTime( FullPath ).Ticks;
					Loaded = true;

					// An empty or hand edited file can deserialize with null strings; an empty GUID would load a null preset
					Values.GlobalFolder = Values.GlobalFolder ?? Defaults.GlobalFolder;
					Values.RelativeFolder = Values.RelativeFolder ?? Defaults.RelativeFolder;
					Values.BakePresetGUID = string.IsNullOrEmpty( Values.BakePresetGUID ) ? Defaults.BakePresetGUID : Values.BakePresetGUID;
					Values.AlbedoName = Values.AlbedoName ?? Defaults.AlbedoName;
					Values.NormalsName = Values.NormalsName ?? Defaults.NormalsName;
					Values.SpecularName = Values.SpecularName ?? Defaults.SpecularName;
					Values.OcclusionName = Values.OcclusionName ?? Defaults.OcclusionName;
					Values.EmissionName = Values.EmissionName ?? Defaults.EmissionName;
					Values.PositionName = Values.PositionName ?? Defaults.PositionName;
				}
				catch ( FileNotFoundException )
				{
					ResetSettings();
					SeedFromEditorPrefs();
					SaveSettings();
				}
				catch ( Exception )
				{
					// Not saved, so a file that might still be recoverable is not overwritten
					ResetSettings();
					Debug.LogWarning( "[AmplifyImpostors] Failed importing \"" + RelativePath + "\". Reverting to default settings." );
				}
			}

			public static void SaveSettings()
			{
				EnsureLoaded();

				if ( Values.DefineSymbol )
				{
					SetSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}
				else
				{
					RemoveSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}

				try
				{
					File.WriteAllText( FullPath, JsonUtility.ToJson( Values, true ) );
					Timestamp = File.GetLastWriteTime( FullPath ).Ticks;
				}
				catch ( Exception )
				{
					Debug.LogWarning( "[AmplifyImpostors] Failed saving \"" + RelativePath + "\"." );
				}
			}

			public static void SetGlobalFolder( string folder )
			{
				EnsureLoaded();
				Values.GlobalFolder = folder;
				SaveSettings();
			}

			public static void InspectorLayout()
			{
				EnsureLoaded();

				if ( File.Exists( FullPath ) )
				{
					// Picks up changes made outside the editor while the page is open ( version control, hand edits )
					long fileTimestamp = File.GetLastWriteTime( FullPath ).Ticks;
					if ( fileTimestamp != Timestamp )
					{
						LoadSettings();
						Timestamp = fileTimestamp;
					}
				}
				else
				{
					SaveSettings();
				}

				Values.AutoSRP = EditorGUILayout.Toggle( Styles.AutoSRP, Values.AutoSRP );
				Values.DefineSymbol = EditorGUILayout.Toggle( Styles.DefineSymbol, Values.DefineSymbol );

				Values.TexImport = EditorGUILayout.Popup( "Texture Importer Settings", Values.TexImport, new string[] { "Ask if resolution is different", "Don't ask, always change", "Don't ask, never change" } );
				Values.CreateLodGroup = EditorGUILayout.Toggle( "Create LODGroup if not present", Values.CreateLodGroup );
				Values.LodTransition = EditorGUILayout.Slider( "Default LOD Transition", Values.LodTransition, 0f, 1f );
				GUILayout.Space( 5 );

				GUILayout.Label( Styles.DefaultSettings, "boldlabel" );

				Values.GlobalDefaultMode = ( FolderMode )EditorGUILayout.EnumPopup( "Default Path", Values.GlobalDefaultMode ? FolderMode.Global : FolderMode.RelativeToPrefab ) == FolderMode.Global;
				EditorGUILayout.BeginHorizontal();
				if ( Values.GlobalDefaultMode )
				{
					EditorGUI.BeginChangeCheck();
					Values.GlobalFolder = EditorGUILayout.TextField( "    Global Folder", Values.GlobalFolder );
					if ( EditorGUI.EndChangeCheck() )
					{
						Values.GlobalFolder = Values.GlobalFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
						Values.GlobalFolder = "/" + Values.GlobalFolder;
						Values.GlobalFolder = Values.GlobalFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
					}
					if ( GUILayout.Button( "...", "minibutton", GUILayout.Width( 20 )/*GUILayout.MaxWidth( Screen.width * 0.5f )*/ ) )
					{
						string oneLevelUp = Application.dataPath + "/../";
						string directory = Path.GetFullPath( oneLevelUp ).Replace( "\\", "/" );
						string fullpath = directory + Values.GlobalFolder;
						string folderpath = EditorUtility.SaveFolderPanel( "Save Impostor to folder", FileUtil.GetProjectRelativePath( fullpath ), null );

						folderpath = FileUtil.GetProjectRelativePath( folderpath );
						if ( !string.IsNullOrEmpty( folderpath ) )
						{
							Values.GlobalFolder = folderpath;
							Values.GlobalFolder = Values.GlobalFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
							Values.GlobalFolder = "/" + Values.GlobalFolder;
							Values.GlobalFolder = Values.GlobalFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
							// the modal panel can clear GUI.changed; flag it so the outer change check saves
							GUI.changed = true;
						}
					}
				}
				else
				{
					EditorGUI.BeginChangeCheck();
					Values.RelativeFolder = EditorGUILayout.TextField( "    Relative to Prefab Folder", Values.RelativeFolder );
					if ( EditorGUI.EndChangeCheck() )
					{
						Values.RelativeFolder = Values.RelativeFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
						Values.RelativeFolder = "/" + Values.RelativeFolder;
						Values.RelativeFolder = Values.RelativeFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
					}
					EditorGUI.BeginDisabledGroup( true );
					GUILayout.Button( "...", "minibutton", GUILayout.Width( 20 ) );
					EditorGUI.EndDisabledGroup();
				}
				EditorGUILayout.EndHorizontal();

				var bakePreset = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBakePreset>( AssetDatabase.GUIDToAssetPath( Values.BakePresetGUID ) );
				var newBakePreset = ( AmplifyImpostorBakePreset )EditorGUILayout.ObjectField( "Default Bake Preset", bakePreset, typeof( AmplifyImpostorBakePreset ), false );
				if ( newBakePreset != bakePreset )
				{
					// clearing the field falls back to the shipped preset instead of storing an empty GUID
					Values.BakePresetGUID = ( newBakePreset != null ) ? AssetDatabase.GUIDFromAssetPath( AssetDatabase.GetAssetPath( newBakePreset ) ).ToString() : Defaults.BakePresetGUID;
				}

				GUILayout.Space( 5 );

				GUILayout.Label( Styles.DefaultBakePreset, "boldlabel" );
				Values.AlbedoName = EditorGUILayout.TextField( "Albedo (RGB) Alpha (A)", Values.AlbedoName );
				Values.NormalsName = EditorGUILayout.TextField( "Normal (RGB) Depth (A)", Values.NormalsName );
				Values.SpecularName = EditorGUILayout.TextField( "Specular (RGB) Smoothness (A)", Values.SpecularName );
				Values.OcclusionName = EditorGUILayout.TextField( "Occlusion (RGB)", Values.OcclusionName );
				Values.EmissionName = EditorGUILayout.TextField( "Emission (RGB)", Values.EmissionName );
				Values.PositionName = EditorGUILayout.TextField( "Position (RGB)", Values.PositionName );
			}
		}
	#endif
	}
}
