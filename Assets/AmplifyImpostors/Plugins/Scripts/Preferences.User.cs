// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

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
		public class User
		{
			private class Styles
			{
				public static readonly GUIContent StartUp = new GUIContent( "Show start screen on Unity launch", "You can set if you want to see the start screen everytime Unity launches, only just when there's a new version available or never." );
			}

			private class Defaults
			{
				public const ShowOption StartUp = ShowOption.Always;
				public const bool BakingOptions = true;
			}

			public class Keys
			{
				public const string StartUp = "IMPOSTORS_GLOBALSTARTUP";
				public const string BakingOptions = "IMPOSTORS_GLOBALBakingOptions";
			}

			public static ShowOption StartUp = ( ShowOption )EditorPrefs.GetInt( Keys.StartUp, ( int )Defaults.StartUp );
			public static bool BakingOptions = EditorPrefs.GetBool( Keys.BakingOptions, Defaults.BakingOptions );

			public static void ResetSettings()
			{
				EditorPrefs.DeleteKey( Keys.StartUp );
				EditorPrefs.DeleteKey( Keys.BakingOptions );

				StartUp = Defaults.StartUp;
				BakingOptions = Defaults.BakingOptions;
			}

			public static void LoadSettings()
			{
				StartUp = ( ShowOption )EditorPrefs.GetInt( Keys.StartUp, ( int )Defaults.StartUp );
				BakingOptions = EditorPrefs.GetBool( Keys.BakingOptions, Defaults.BakingOptions );
			}

			public static void SaveSettings()
			{
				EditorPrefs.SetInt( Keys.StartUp, ( int )StartUp );
				EditorPrefs.SetBool( Keys.BakingOptions, BakingOptions );
			}

			public static void InspectorLayout()
			{
				StartUp = ( ShowOption )EditorGUILayout.EnumPopup( Styles.StartUp, StartUp );
			}
		}
	#endif
	}
}
