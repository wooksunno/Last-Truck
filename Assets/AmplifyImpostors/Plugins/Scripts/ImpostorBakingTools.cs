// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace AmplifyImpostors
{
	public static class ImpostorBakingTools
	{
#if UNITY_EDITOR
		public static string OpenFolderForImpostor( this AmplifyImpostor instance )
		{
			string oneLevelUp = Application.dataPath + "/../";
			string directory = Path.GetFullPath( oneLevelUp ).Replace( "\\", "/" );
			string objectPath = AssetDatabase.GetAssetPath( instance.RootTransform );

			// Find Path next to prefab
			if( string.IsNullOrEmpty( objectPath ) )
			{
				objectPath = AssetDatabase.GetAssetPath( PrefabUtility.GetCorrespondingObjectFromSource( instance.RootTransform ) );
			}

			string fullpath = string.Empty;
			string suggestedRelativePath = directory + objectPath;
			if( string.IsNullOrEmpty( objectPath ) )
				suggestedRelativePath = Application.dataPath;
			else
				suggestedRelativePath = Path.GetDirectoryName( suggestedRelativePath ).Replace( "\\", "/" );

			// Find best match
			if( Preferences.Project.GlobalDefaultMode && AssetDatabase.IsValidFolder( Preferences.Project.GlobalFolder.TrimStart( '/' ) ) )
				fullpath = directory + Preferences.Project.GlobalFolder;
			else if( AssetDatabase.IsValidFolder( FileUtil.GetProjectRelativePath( suggestedRelativePath + Preferences.Project.RelativeFolder ).TrimEnd( '/' ) ) )
				fullpath = suggestedRelativePath + Preferences.Project.RelativeFolder;
			else if( AssetDatabase.IsValidFolder( FileUtil.GetProjectRelativePath( suggestedRelativePath ).TrimEnd( '/' ) ) )
				fullpath = suggestedRelativePath;
			else
				fullpath = Application.dataPath;

			string fileName = instance.name + "_Impostor";
			if( !string.IsNullOrEmpty( instance.m_impostorName ) )
				fileName = instance.m_impostorName;

			//Debug.Log( fullpath );
			//Debug.Log( fileName );

			string folderpath = EditorUtility.SaveFilePanelInProject( "Save Impostor to folder", fileName, "asset", "", FileUtil.GetProjectRelativePath( fullpath ) );
			fileName = Path.GetFileNameWithoutExtension( folderpath );

			if( !string.IsNullOrEmpty( fileName ) )
			{
				folderpath = Path.GetDirectoryName( folderpath ).Replace( "\\", "/" );
				if( !string.IsNullOrEmpty( folderpath ) )
				{
					folderpath += "/";
					if( !Preferences.Project.GlobalDefaultMode )
					{
						instance.m_folderPath = folderpath;
					}
					else
					{
						Preferences.Project.SetGlobalFolder( folderpath );
					}
					instance.m_impostorName = fileName;
				}
			}
			return folderpath;
		}
#endif
	}
}
