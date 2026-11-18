// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AmplifyImpostors
{
	// public, scriptable entry points for baking impostors without the editor windows. These run the bake
	// synchronously (they block until done), so they are safe to call from a -batchmode -executeMethod script.
	//
	// Example command-line usage (wrap a call in your own static, no-argument method):
	//   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod MyTools.BakeForCI
	//   where MyTools.BakeForCI() calls e.g. AmplifyImpostorBakeAPI.BakeAllIncludedBatchSets();
	public static class AmplifyImpostorBakeAPI
	{
		// Bakes every enabled, ready impostor in a single set. Returns the number of impostors submitted to bake.
		public static int BakeBatchSet( AmplifyImpostorBatchSet set, bool autoCreate = true, string autoCreateFolder = null )
		{
			List<GameObject> targets = new List<GameObject>();
			GatherSet( set, autoCreate, targets );
			return Bake( targets, autoCreate, autoCreateFolder );
		}

		// Bakes a single set loaded from its asset path (e.g. "Assets/Sets/Trees.asset").
		public static int BakeBatchSetAtPath( string setAssetPath, bool autoCreate = true, string autoCreateFolder = null )
		{
			AmplifyImpostorBatchSet set = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBatchSet>( setAssetPath );
			if ( set == null )
			{
				Debug.LogError( "[AmplifyImpostor] No Batch Set found at \"" + setAssetPath + "\"." );
				return 0;
			}
			return BakeBatchSet( set, autoCreate, autoCreateFolder );
		}

		// Bakes every enabled, ready impostor across the given sets (deduplicated).
		public static int BakeBatchSets( IEnumerable<AmplifyImpostorBatchSet> sets, bool autoCreate = true, string autoCreateFolder = null )
		{
			List<GameObject> targets = new List<GameObject>();
			if ( sets != null )
			{
				foreach ( AmplifyImpostorBatchSet set in sets )
				{
					GatherSet( set, autoCreate, targets );
				}
			}
			return Bake( targets, autoCreate, autoCreateFolder );
		}

		// Bakes multiple sets loaded from their asset paths.
		public static int BakeBatchSetsAtPaths( IEnumerable<string> setAssetPaths, bool autoCreate = true, string autoCreateFolder = null )
		{
			List<AmplifyImpostorBatchSet> sets = new List<AmplifyImpostorBatchSet>();
			if ( setAssetPaths != null )
			{
				foreach ( string path in setAssetPaths )
				{
					AmplifyImpostorBatchSet set = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBatchSet>( path );
					if ( set == null )
					{
						Debug.LogError( "[AmplifyImpostor] No Batch Set found at \"" + path + "\"." );
						continue;
					}
					sets.Add( set );
				}
			}
			return BakeBatchSets( sets, autoCreate, autoCreateFolder );
		}

		// Bakes every enabled, ready impostor across all sets flagged "Include in Bake All Included".
		public static int BakeAllIncludedBatchSets( bool autoCreate = true, string autoCreateFolder = null )
		{
			List<AmplifyImpostorBatchSet> included = new List<AmplifyImpostorBatchSet>();
			string[] guids = AssetDatabase.FindAssets( "t:AmplifyImpostorBatchSet" );
			for ( int i = 0; i < guids.Length; i++ )
			{
				string path = AssetDatabase.GUIDToAssetPath( guids[ i ] );
				AmplifyImpostorBatchSet set = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBatchSet>( path );
				if ( set != null && set.IncludeInBatch )
				{
					included.Add( set );
				}
			}
			return BakeBatchSets( included, autoCreate, autoCreateFolder );
		}

		// Bakes an explicit list of impostor objects (prefab assets or scene objects), deduplicated.
		public static int BakeImpostors( IEnumerable<GameObject> impostors, bool autoCreate = true, string autoCreateFolder = null )
		{
			List<GameObject> targets = new List<GameObject>();
			if ( impostors != null )
			{
				foreach ( GameObject go in impostors )
				{
					TryAdd( go, autoCreate, targets );
				}
			}
			return Bake( targets, autoCreate, autoCreateFolder );
		}

		// Bakes an explicit list of impostor prefabs loaded from their asset paths.
		public static int BakeImpostorsAtPaths( IEnumerable<string> prefabPaths, bool autoCreate = true, string autoCreateFolder = null )
		{
			List<GameObject> targets = new List<GameObject>();
			if ( prefabPaths != null )
			{
				foreach ( string path in prefabPaths )
				{
					GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>( path );
					if ( go == null )
					{
						Debug.LogError( "[AmplifyImpostor] No prefab found at \"" + path + "\"." );
						continue;
					}
					TryAdd( go, autoCreate, targets );
				}
			}
			return Bake( targets, autoCreate, autoCreateFolder );
		}

		// appends a set's enabled, ready, not-yet-listed impostors to the bake list
		private static void GatherSet( AmplifyImpostorBatchSet set, bool autoCreate, List<GameObject> targets )
		{
			if ( set == null )
			{
				return;
			}

			for ( int i = 0; i < set.Targets.Count; i++ )
			{
				bool enabled = ( i < set.Enabled.Count ) ? set.Enabled[ i ] : true;
				if ( enabled )
				{
					TryAdd( set.Targets[ i ], autoCreate, targets );
				}
			}
		}

		// adds an impostor to the list if it is valid, ready and not already present
		private static void TryAdd( GameObject go, bool autoCreate, List<GameObject> targets )
		{
			if ( go == null || targets.Contains( go ) )
			{
				return;
			}
			if ( AmplifyImpostorBakeView.IsReady( AmplifyImpostorBakeView.ResolveImpostor( go ), autoCreate ) )
			{
				targets.Add( go );
			}
		}

		private static int Bake( List<GameObject> targets, bool autoCreate, string autoCreateFolder )
		{
			AmplifyImpostorBakeView.BakeTargets( targets, autoCreate, autoCreateFolder );
			return targets.Count;
		}
	}
}
#endif
