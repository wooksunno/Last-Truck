// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif
using UnityEngine;
using Unity.Scripting.LifecycleManagement;

namespace AmplifyImpostors
{
	[NoAutoStaticsCleanup]
	public partial class Preferences
	{
	#if UNITY_EDITOR
		public enum ShowOption
		{
			Always = 0,
			OnNewVersion = 1,
			Never = 2
		}

		public static readonly string PrefDataImpType = "IMPOSTORS_DATAIMPTYPE";
		public static readonly string PrefDataTexSizeLocked = "IMPOSTORS_DATATEXSIZEXLOCKED";
		public static readonly string PrefDataTexSizeSelected = "IMPOSTORS_DATATEXSIZEXSELECTED";
		public static readonly string PrefDataTexSizeX = "IMPOSTORS_DATATEXSIZEX";
		public static readonly string PrefDataTexSizeY = "IMPOSTORS_DATATEXSIZEY";
		public static readonly string PrefDataDecoupledFrames = "IMPOSTORS_DATADECOUPLEDFRAMES";
		public static readonly string PrefDataXFrames = "IMPOSTORS_DATAXFRAMES";
		public static readonly string PrefDataYFrames = "IMPOSTORS_DATAYFRAMES";
		public static readonly string PrefDataPixelBleeding = "IMPOSTORS_DATAPIXELBLEEDING";

		public static readonly string PrefDataTolerance = "IMPOSTORS_DATATOLERANCE ";
		public static readonly string PrefDataNormalScale = "IMPOSTORS_DATANORMALSCALE";
		public static readonly string PrefDataMaxVertices = "IMPOSTORS_DATAMAXVERTICES";

		[SettingsProvider]
		public static SettingsProvider ImpostorsSettings()
		{
			var provider = new SettingsProvider( "Preferences/Amplify Impostors", SettingsScope.User )
			{
				guiHandler = ( string searchContext ) => {
					PreferencesGUI();
				}
			};
			return provider;
		}

		public static void PreferencesGUI()
		{
			var cache = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 250;

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.LabelField( "User", EditorStyles.boldLabel );
				User.InspectorLayout();
			}
			if ( EditorGUI.EndChangeCheck() )
			{
				User.SaveSettings();
			}

			EditorGUILayout.Space( 5 );

			EditorGUI.BeginChangeCheck();
			{
				EditorGUILayout.LabelField( "Project", EditorStyles.boldLabel );
				Project.InspectorLayout();
			}
			if ( EditorGUI.EndChangeCheck() )
			{
				Project.SaveSettings();
			}

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if ( GUILayout.Button( "Reset and Forget All" ) )
			{
				ResetSettings();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUIUtility.labelWidth = cache;
		}

		private static void ResetSettings()
		{
			User.ResetSettings();
			Project.ResetSettings();

			User.SaveSettings();
			Project.SaveSettings();
		}

		private static void LoadSettings()
		{
			User.LoadSettings();
			Project.LoadSettings();
		}

		public static void Initialize()
		{
			LoadSettings();
		}

		// Scripting Define Symbol Helper
		private static readonly string AmplifyImpostorsDefineSymbol = "AMPLIFY_IMPOSTORS";
		private static bool Initialized = false;

		[InitializeOnLoadMethod]
		public static void Init()
		{
			if( !Initialized )
			{
				Initialized = true;
				Initialize();
				if ( Project.DefineSymbol )
				{
					SetSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}
			}
		}

		public static void SetSymbolOnBuildTargetGroup( BuildTargetGroup targetGroup )
		{
			var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup( targetGroup );
			string currData = PlayerSettings.GetScriptingDefineSymbols( namedBuildTarget );
			if( !currData.Contains( AmplifyImpostorsDefineSymbol ) )
			{
				if( string.IsNullOrEmpty( currData ) )
				{
					PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, AmplifyImpostorsDefineSymbol );
				}
				else
				{
					if( !currData[ currData.Length - 1 ].Equals( ';' ) )
					{
						currData += ';';
					}
					currData += AmplifyImpostorsDefineSymbol;

					PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, currData );
				}
			}
		}

		public static void RemoveSymbolOnBuildTargetGroup( BuildTargetGroup targetGroup )
		{
			var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup( targetGroup );
			string currData = PlayerSettings.GetScriptingDefineSymbols( namedBuildTarget );
			if( currData.Contains( AmplifyImpostorsDefineSymbol ) )
			{
				currData = currData.Replace( AmplifyImpostorsDefineSymbol + ";", "" );
				currData = currData.Replace( ";" + AmplifyImpostorsDefineSymbol, "" );
				currData = currData.Replace( AmplifyImpostorsDefineSymbol, "" );

				PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, currData );
			}
		}
	#endif
	}
}
