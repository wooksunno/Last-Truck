// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AmplifyImpostors
{
	[CustomEditor( typeof( AmplifyImpostorBatchSet ) )]
	[CanEditMultipleObjects]
	public class AmplifyImpostorBatchSetEditor : Editor
	{
		private AmplifyImpostorBatchSetDetailView m_detail;

		private static readonly GUIContent OpenStr = new GUIContent( "Open Batch Set Manager", "Opens the Batch Set Manager" );
		private static readonly GUIContent BakeSelectedStr = new GUIContent( "Bake All Selected", "Bakes every enabled impostor across all selected sets, regardless of their Include flag" );

		private void OnEnable()
		{
			// useScroll = false: the inspector already scrolls, so the list is laid out inline
			m_detail = new AmplifyImpostorBatchSetDetailView( Repaint, false );
		}

		private void OnDisable()
		{
			if ( m_detail != null )
			{
				m_detail.Cleanup();
				m_detail = null;
			}
		}

		public override void OnInspectorGUI()
		{
			// shortcut to the Batch Set Manager, available whether one or many sets are selected
			if ( GUILayout.Button( OpenStr ) )
			{
				AmplifyImpostorBatchSetManager.Init();
			}

			EditorGUILayout.Space( 4 );

			// multiple sets selected: the only action is to bake them all (selected = "I want to bake it",
			// so their Include flag is ignored)
			if ( targets.Length > 1 )
			{
				DrawMultiSelection();
				return;
			}

			// same UI as the Batch Set Manager's right panel
			m_detail.Draw( ( AmplifyImpostorBatchSet )target );
		}

		private void DrawMultiSelection()
		{
			// gather every enabled, ready impostor across the selected sets (deduplicated), ignoring Include
			List<GameObject> toBake = new List<GameObject>();
			for ( int i = 0; i < targets.Length; i++ )
			{
				AmplifyImpostorBakeView.GatherReady( targets[ i ] as AmplifyImpostorBatchSet, true, toBake );
			}

			EditorGUILayout.LabelField( targets.Length + " Batch Sets selected", EditorStyles.boldLabel );
			EditorGUILayout.LabelField( toBake.Count + " impostor(s) will be baked.", EditorStyles.miniLabel );
			EditorGUILayout.Space( 4 );

			using ( new EditorGUI.DisabledScope( toBake.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode ) )
			{
				if ( GUILayout.Button( BakeSelectedStr, GUILayout.Height( 28 ) ) )
				{
					// defer so the bake runs outside OnInspectorGUI (avoids IMGUI re-entrancy aborting the bake)
					EditorApplication.delayCall += () => AmplifyImpostorBakeView.BakeTargets( toBake, true, null );
				}
			}
		}
	}
}
#endif
