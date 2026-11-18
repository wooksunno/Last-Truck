// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace AmplifyImpostors
{
	// a saved, reusable batch list. Only prefab/asset references survive across sessions; scene objects do not.
	[CreateAssetMenu( menuName = "Amplify Impostors/Batch Set", fileName = "New Impostor Batch Set", order = 87 )]
	public class AmplifyImpostorBatchSet : ScriptableObject
	{
		public List<GameObject> Targets = new List<GameObject>();
		public List<bool> Enabled = new List<bool>();

		// when checked in the Batch Baker tool, this set's impostors are added to the list and batch processed.
		// stored inverted so sets created before this field existed still default to checked/included.
		[SerializeField]
		private bool m_excludeFromBatch = false;
		public bool IncludeInBatch { get { return !m_excludeFromBatch; } set { m_excludeFromBatch = !value; } }
	}
}
#endif
