// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if !UNITY_6000_5_OR_NEWER
using System;

namespace Unity.Scripting.LifecycleManagement
{
	// @diogo: keeps the namespace alive below 6000.5 so the "using" of it compiles unguarded
}

namespace AmplifyImpostors
{
	// Stand-in for the attribute Unity made public in 6000.5, so types can be tagged
	// unconditionally; statics must survive entering Play Mode, so Impostors opts out everywhere.
	[AttributeUsage( AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, AllowMultiple = false )]
	internal sealed class NoAutoStaticsCleanupAttribute : Attribute { }
}
#endif
